using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Server-provisioned appointment of one human to one tug and one duty (60.02 paragraph 49/61: the verified
// Tug Master of that tug for that period; delegation and period come from the source, never from a request,
// IdP role name, crew list or administrator). Duty: TugMaster | Master | ChiefEngineer.
public sealed record TugOperationsAuthority(Guid Id, Guid VesselId, Guid RoleId, string Duty, string AppointmentSource,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
public sealed record TugAuthorityDto(Guid Id, string Duty);

internal static class TugOperationsAuthorities
{
    public const string TugMaster = "TugMaster", Master = "Master", ChiefEngineer = "ChiefEngineer";
    public static readonly string[] MasterDuties = [TugMaster, Master];
    public static readonly string[] EngineDuties = [ChiefEngineer];

    // Exactly one Human membership, a current appointment for this tug and duty, and an active role context.
    internal static TugOperationsAuthority[] Resolve(HttpContext h, TrustedActor? actor, Guid tug, string[] duties) {
        if (actor == null || !actor.HasWriteAttribution || actor.RoleContext == null || !actor.CanReadTug) return [];
        var members = h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).ToArray();
        if (members.Length != 1 || !string.Equals(members[0].ActorKind, "Human", StringComparison.Ordinal)) return [];
        var grants = members[0].TugOperationsAuthorities ?? [];
        if (grants.Length > 256) return [];
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var roles = actor.RoleContext.Split(',');
        return grants.Where(g => g != null && g.Id != Guid.Empty && grants.Count(o => o != null && o.Id == g.Id) == 1 && g.VesselId == tug && g.VesselId != Guid.Empty &&
            duties.Contains(g.Duty, StringComparer.Ordinal) && g.RoleId != Guid.Empty && roles.Contains(g.RoleId.ToString("D")) &&
            g.AppointmentSource is { Length: > 0 and <= 200 } source && source.Trim() == source && !source.Any(char.IsControl) &&
            g.ValidUntil > g.ValidFrom && g.ValidFrom <= now && now < g.ValidUntil).OrderBy(g => g.Id).ToArray();
    }
    internal static TugAuthorityDto[] Dto(TugOperationsAuthority[] grants) => grants.Select(g => new TugAuthorityDto(g.Id, g.Duty)).ToArray();
}

// Shared bounded command transport for the S7 tug readiness family (DEV-083..086).
internal static class TugReadinessHttp
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false };
    internal static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    internal static IQueryable<Vessel> Tugs(PoseidonDbContext db, TrustedActor a) => a.Scope(db.Vessels).Where(v => v.Kind == VesselKind.Tug);
    internal static bool Write(TrustedActor a, Guid vessel) => a.CanReadTug && a.HasWriteAttribution && a.Permissions.Contains("tug.write") &&
        (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit));
    internal static DateTimeOffset Now(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    internal static Task Observe(HttpContext h, string action, string result, Guid? id = null) =>
        h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, result, resourceId: id);
    internal static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null) {
        await Observe(h, action, code, id);
        return Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status);
    }
    // Exact query keys only; each once. Unknown keys are refused instead of ignored.
    internal static bool Query(HttpContext h, string[] required, string[] optional, out Dictionary<string, Guid> values) {
        values = [];
        if (h.Request.Query.Any(q => !required.Contains(q.Key) && !optional.Contains(q.Key) || q.Value.Count != 1)) return false;
        foreach (var key in required.Concat(optional.Where(o => h.Request.Query.ContainsKey(o)))) {
            if (!Guid.TryParseExact(h.Request.Query[key], "D", out var id) || id == Guid.Empty) return false;
            values[key] = id;
        }
        return true;
    }
    // CSRF, single UUID Idempotency-Key, bounded JSON object with unique members and no unknown members.
    internal static async Task<(T? Command, string Key, IResult? Failure)> Read<T>(HttpContext h, string action, int max = 8192) where T : class {
        if (h.Request.Headers["X-CSRF-TOKEN"].Count != 1) return (null, "", await Fail(h, action, 400, "csrf_invalid"));
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return (null, "", await Fail(h, action, 400, "csrf_invalid")); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsed) || parsed == Guid.Empty) return (null, "", await Fail(h, action, 400, "invalid_idempotency_key"));
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > max) return (null, "", await Fail(h, action, 400, "invalid_request"));
        try {
            using var buffer = new MemoryStream(); var bytes = new byte[2048]; int count;
            while ((count = await h.Request.Body.ReadAsync(bytes, h.RequestAborted)) > 0) {
                if (buffer.Length + count > max) return (null, "", await Fail(h, action, 400, "invalid_request"));
                buffer.Write(bytes, 0, count);
            }
            using var doc = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 4 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !TugEndpoints.UniqueMembers(doc.RootElement)) return (null, "", await Fail(h, action, 400, "invalid_request"));
            var command = doc.RootElement.Deserialize<T>(Json) ?? throw new JsonException();
            return (command, parsed.ToString("D"), null);
        } catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException) {
            return (null, "", await Fail(h, action, 400, "invalid_request"));
        }
    }
    internal static string Fingerprint(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    // Only serialization/deadlock (40001/40P01) and unique races (23505) are conflicts; anything else is unavailable.
    internal static async Task<IResult> Database(HttpContext h, PoseidonDbContext db, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, string action, Exception e, Guid? id) {
        await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
        if (e is DbUpdateConcurrencyException) return await Fail(h, action, 409, "version_conflict", id);
        var conflict = PostgresFailure.IsConflict(e);
        return await Fail(h, action, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable", id);
    }
    internal static async Task<TugReadinessReceipt?> Receipt(PoseidonDbContext db, TrustedActor a, string key, CancellationToken ct) =>
        await db.TugReadinessReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
}
