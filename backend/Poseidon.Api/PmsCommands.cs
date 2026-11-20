using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record PmsFailure(string Code, string CorrelationId);
internal sealed record PmsOutcome(int Status, string? Code, Guid ResourceId, long Revision)
{
    internal static PmsOutcome Ok(Guid id, long revision) => new(200, null, id, revision);
    internal static PmsOutcome Fail(int status, string code) => new(status, code, Guid.Empty, 0);
}
internal sealed record PmsContext<T>(TrustedActor Actor, Guid Vessel, Guid? RouteId, Guid? SubId, T Command, DateTimeOffset Now, PoseidonDbContext Db,
    MaintenanceObservationSettings Settings, HttpContext Http, CancellationToken Ct);

// Shared closed command transport for DEV-108/DEV-109 (NATIVE-COMMAND-1): server authorization,
// route vessel scope, CSRF, Idempotency-Key receipt, bounded strict JSON, one SERIALIZABLE
// transaction (failures classified only through PostgresFailure) and attributed audit. A replay
// re-reads the committed resource under current authorization and never repeats the effect.
internal static class PmsCommands
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private const int MaxBody = 65536;

    internal static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static DateTimeOffset Now(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    internal static Task Audit(HttpContext h, string action, string result, Guid? id = null) =>
        h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, result, resourceId: id);
    internal static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null)
    {
        h.Response.Headers.CacheControl = "no-store";
        await Audit(h, action, code, id);
        return Results.Json(new PmsFailure(code, h.TraceIdentifier), statusCode: status);
    }
    internal static Task<bool> InScope(PoseidonDbContext db, TrustedActor actor, Guid vessel, CancellationToken ct) =>
        actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == vessel, ct);

    internal static async Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, Guid vessel, string operation, string auditAction,
        Guid? routeId, Guid? subId, Func<TrustedActor, Guid, bool> allowed, Func<T, Guid?> resourceOf,
        Func<PmsContext<T>, Task<PmsOutcome>> apply, Func<PoseidonDbContext, TrustedActor, Guid, Guid, HttpContext, CancellationToken, Task<object?>> read) where T : class
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (h.Request.Query.Count != 0) return await Fail(h, auditAction, 400, "invalid_query");
        var actor = Actor(h);
        if (actor == null || !actor.HasWriteAttribution || db.TenantId != actor.TenantId || !allowed(actor, vessel)) return await Fail(h, auditAction, 403, "permission_denied", routeId);
        if (!await InScope(db, actor, vessel, ct)) return await Fail(h, auditAction, 404, "resource_not_found", routeId);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, auditAction, 403, "csrf_invalid", routeId); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await Fail(h, auditAction, 400, "invalid_idempotency_key", routeId);
        var key = parsedKey.ToString("D");
        T command;
        try
        {
            if (!h.Request.HasJsonContentType() || h.Request.ContentLength > MaxBody) throw new JsonException();
            using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
            while ((count = await h.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > MaxBody) throw new JsonException();
                buffer.Write(chunk, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command = document.RootElement.Deserialize<T>(Json) ?? throw new JsonException();
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException)
        { return await Fail(h, auditAction, 400, "invalid_request", routeId); }
        var resource = resourceOf(command) ?? routeId ?? Guid.Empty;
        if (resource == Guid.Empty) return await Fail(h, auditAction, 400, "invalid_request");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation, vessel, routeId, subId, command }, Json));
        async Task<IResult> Fresh(Guid id)
        {
            var live = Actor(h);
            if (live == null || !allowed(live, vessel)) return await Fail(h, auditAction, 403, "permission_denied", id);
            db.ChangeTracker.Clear();
            var value = await read(db, live, vessel, id, h, ct);
            if (value == null) return await Fail(h, auditAction, 503, "command_outcome_withheld", id);
            await Audit(h, auditAction, "Success", id);
            return Results.Json(value, Json);
        }
        await Audit(h, auditAction, "Attempted", resource);
        Guid? committed = null; PmsOutcome? failure = null;
        var settings = h.RequestServices.GetRequiredService<MaintenanceObservationSettings>();
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                var receipt = await db.PmsCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Action != operation || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel)
                        return await Fail(h, auditAction, 409, "idempotency_key_reused", resource);
                    return await Fresh(receipt.ResourceId);
                }
                var outcome = await apply(new(actor, vessel, routeId, subId, command, Now(h), db, settings, h, ct));
                if (outcome.Status != 200) { failure = outcome; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
                else
                {
                    db.PmsCommandReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = operation, Fingerprint = fingerprint,
                        VesselId = vessel, ResourceId = outcome.ResourceId, Revision = Math.Max(1, outcome.Revision), SavedAt = Now(h) });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = outcome.ResourceId;
                }
            }
            catch (DbUpdateConcurrencyException)
            { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); failure = PmsOutcome.Fail(409, "version_conflict"); }
            catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
            {
                // Only serialization/deadlock (40001/40P01) and unique races (23505) are conflicts.
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                failure = PostgresFailure.IsConflict(ex) ? PmsOutcome.Fail(409, "version_conflict") : PmsOutcome.Fail(503, "service_unavailable");
            }
        }
        if (failure != null) return await Fail(h, auditAction, failure.Status, failure.Code!, resource);
        try { return await Fresh(committed!.Value); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex))
        { return Results.Json(new PmsFailure("command_outcome_withheld", h.TraceIdentifier), statusCode: 503); }
    }

    internal static bool Text(string? value, int max) => value == null || value.Length <= max && !value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t');
}
