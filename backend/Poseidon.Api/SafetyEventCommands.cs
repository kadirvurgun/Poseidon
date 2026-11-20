using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record SafetyFailure(string Code, string[] Missing, string CorrelationId);
// Extra carries a one-time value of THIS committed command (the confidential follow-up secret). It is never stored
// in the receipt; an idempotent replay therefore returns the committed resource without it.
internal sealed record SafetyOutcome(int Status, string? Code, Guid ResourceId, long Revision, string[]? Missing = null, string? Extra = null)
{
    internal static SafetyOutcome Ok(Guid id, long revision, string? extra = null) => new(200, null, id, revision, null, extra);
    internal static SafetyOutcome Fail(int status, string code, string[]? missing = null) => new(status, code, Guid.Empty, 0, missing);
    internal static SafetyOutcome From(SafetyDecision d) => Fail(d.Status, d.Code!, d.Missing);
}
internal sealed record SafetyContext<T>(HttpContext Http, TrustedActor Actor, Guid Vessel, Guid? RouteId, Guid? SubId, T Command, DateTimeOffset Now,
    PoseidonDbContext Db, CancellationToken Ct);

// Shared closed command transport for DEV-060/061 (NATIVE-COMMAND-1): server authorization, exact vessel scope
// (?vesselId=), CSRF, Idempotency-Key receipt, bounded strict JSON, one SERIALIZABLE transaction with attributed EF
// audit, optional human-only gate. Replays re-read the committed resource under current authorization.
internal static class SafetyEventCommands
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private const int MaxBody = 262144;
    internal const string ReadAction = "SafetyEventRead", CommandAction = "SafetyEventCommand";

    internal static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static DateTimeOffset Now(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    internal static Task Audit(HttpContext h, string action, string result, Guid? id = null) =>
        h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, result, resourceId: id);
    internal static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null, string[]? missing = null)
    {
        h.Response.Headers.CacheControl = "no-store";
        await Audit(h, action, code, id);
        return missing == null ? Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status)
            : Results.Json(new SafetyFailure(code, missing, h.TraceIdentifier), statusCode: status);
    }
    internal static bool Query(HttpContext h, out Guid vessel, params string[] extra)
    {
        vessel = Guid.Empty;
        if (h.Request.Query.Any(q => q.Value.Count != 1 || q.Key != "vesselId" && !extra.Contains(q.Key))) return false;
        return Guid.TryParseExact(h.Request.Query["vesselId"], "D", out vessel) && vessel != Guid.Empty;
    }
    internal static IQueryable<Vessel> Vessels(PoseidonDbContext db, TrustedActor actor, Guid vessel) => actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel);

    internal static async Task<T?> Body<T>(HttpContext h, CancellationToken ct) where T : class
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
        return document.RootElement.Deserialize<T>(Json);
    }
    internal static async Task<bool> Csrf(HttpContext h)
    {
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    internal static async Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string operation, Guid? routeId, Guid? subId,
        Func<TrustedActor, Guid, bool> allowed, Func<T, Guid?> resourceOf, Func<SafetyContext<T>, Task<SafetyOutcome>> apply,
        Func<PoseidonDbContext, TrustedActor, Guid, Guid, SafetyOutcome?, HttpContext, CancellationToken, Task<object?>> read, bool humanOnly = false) where T : class
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (!Query(h, out var vessel)) return await Fail(h, CommandAction, 400, "invalid_query", routeId);
        var actor = Actor(h);
        if (actor == null || !actor.HasWriteAttribution || db.TenantId != actor.TenantId || !allowed(actor, vessel)) return await Fail(h, CommandAction, 403, "permission_denied", routeId);
        if (!await Vessels(db, actor, vessel).AnyAsync(ct)) return await Fail(h, CommandAction, 404, "resource_not_found", routeId);
        if (humanOnly && !CertificateRenewalEndpoints.Human(h, actor)) return await Fail(h, CommandAction, 403, "human_authority_required", routeId);
        if (!await Csrf(h)) return await Fail(h, CommandAction, 403, "csrf_invalid", routeId);
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await Fail(h, CommandAction, 400, "invalid_idempotency_key", routeId);
        var key = parsedKey.ToString("D");
        T command;
        try { command = await Body<T>(h, ct) ?? throw new JsonException(); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException)
        { return await Fail(h, CommandAction, 400, "invalid_request", routeId); }
        var resource = resourceOf(command) ?? routeId ?? Guid.Empty;
        if (resource == Guid.Empty) return await Fail(h, CommandAction, 400, "invalid_request");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation, vessel, routeId, subId, command }, Json));
        async Task<IResult> Fresh(Guid id, SafetyOutcome? outcome)
        {
            var live = Actor(h);
            if (live == null || !allowed(live, vessel)) return await Fail(h, CommandAction, 403, "permission_denied", id);
            db.ChangeTracker.Clear();
            var value = await read(db, live, vessel, id, outcome, h, ct);
            if (value == null) return await Fail(h, CommandAction, 503, "command_outcome_withheld", id);
            await Audit(h, CommandAction, "Success", id);
            return Results.Json(value, Json);
        }
        await Audit(h, CommandAction, "Attempted", resource);
        SafetyOutcome? committed = null, failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                var receipt = await db.SafetyEventReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Action != operation || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel)
                        return await Fail(h, CommandAction, 409, "idempotency_key_reused", resource);
                    return await Fresh(receipt.ResourceId, null);
                }
                var outcome = await apply(new(h, actor, vessel, routeId, subId, command, Now(h), db, ct));
                if (outcome.Status != 200) { failure = outcome; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
                else
                {
                    db.SafetyEventReceipts.Add(new() { TenantId = actor.TenantId, VesselId = vessel, ActorId = actor.Id, Key = key, Action = operation, Fingerprint = fingerprint,
                        ResourceId = outcome.ResourceId, Revision = Math.Max(1, outcome.Revision), CommittedAt = Now(h) });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = outcome;
                }
            }
            catch (DbUpdateConcurrencyException)
            { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); failure = SafetyOutcome.Fail(409, "version_conflict"); }
            catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
            {
                // Only serialization/deadlock (40001/40P01) and unique races (23505) are conflicts.
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                failure = PostgresFailure.IsConflict(ex) ? SafetyOutcome.Fail(409, "version_conflict") : SafetyOutcome.Fail(503, "service_unavailable");
            }
        }
        if (failure != null) return await Fail(h, CommandAction, failure.Status, failure.Code!, resource, failure.Missing);
        try { return await Fresh(committed!.ResourceId, committed); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex))
        { return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503); }
    }

    internal static Guid[] Ids(string json) => JsonSerializer.Deserialize<Guid[]>(json, Json) ?? [];
    internal static string WithId(string json, Guid id) => JsonSerializer.Serialize(Ids(json).Append(id).Distinct().Order().ToArray(), Json);
}
