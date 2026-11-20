using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record AssuranceFailure(string Code, string[] Missing, string CorrelationId);
internal sealed record AssuranceOutcome(int Status, string? Code, Guid ResourceId, long Revision, string[]? Missing = null)
{
    internal static AssuranceOutcome Ok(Guid id, long revision) => new(200, null, id, revision);
    internal static AssuranceOutcome Fail(int status, string code, string[]? missing = null) => new(status, code, Guid.Empty, 0, missing);
}
internal sealed record AssuranceContext<T>(HttpContext Http, TrustedActor Actor, Guid? Vessel, Guid? RouteId, T Command, DateTimeOffset Now,
    PoseidonDbContext Db, CancellationToken Ct);

// S10 WP-A closed command transport (NATIVE-COMMAND-1) for DEV-064 packs and DEV-113 assurance commands: server
// authorization, exact vessel scope (?vesselId=; tenant-scoped commands take no query), CSRF, Idempotency-Key receipt
// (the key doubles as the offline command id for replayed field evidence), bounded strict JSON, one SERIALIZABLE
// transaction with attributed EF audit, optional human-only gate. Replays re-read the committed resource under
// current authorization. Failures are classified only through PostgresFailure.
internal static class AssuranceCommands
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal const int DefaultMaxBody = 262144, ImportMaxBody = 3 * 1024 * 1024;

    internal static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null, string[]? missing = null)
    {
        h.Response.Headers.CacheControl = "no-store";
        await SafetyEventCommands.Audit(h, action, code, id);
        return missing == null ? Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status)
            : Results.Json(new AssuranceFailure(code, missing, h.TraceIdentifier), statusCode: status);
    }

    private static async Task<T?> Body<T>(HttpContext h, int max, CancellationToken ct) where T : class
    {
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > max) throw new JsonException();
        using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
        while ((count = await h.Request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > max) throw new JsonException();
            buffer.Write(chunk, 0, count);
        }
        using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
        return document.RootElement.Deserialize<T>(Json);
    }

    internal static async Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string action, string operation, Guid? routeId, bool vesselScoped,
        Func<TrustedActor, bool> allowed, Func<T, Guid?> resourceOf, Func<AssuranceContext<T>, Task<AssuranceOutcome>> apply,
        Func<PoseidonDbContext, TrustedActor, Guid?, Guid, HttpContext, CancellationToken, Task<object?>> read, bool humanOnly = false, int maxBody = DefaultMaxBody) where T : class
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        Guid? vessel = null;
        if (vesselScoped) { if (!SafetyEventCommands.Query(h, out var v)) return await Fail(h, action, 400, "invalid_query", routeId); vessel = v; }
        else if (h.Request.Query.Count != 0) return await Fail(h, action, 400, "invalid_query", routeId);
        var actor = SafetyEventCommands.Actor(h);
        if (actor == null || !actor.HasWriteAttribution || db.TenantId != actor.TenantId || !allowed(actor)) return await Fail(h, action, 403, "permission_denied", routeId);
        if (vessel is { } scoped && !await SafetyEventCommands.Vessels(db, actor, scoped).AnyAsync(ct)) return await Fail(h, action, 404, "resource_not_found", routeId);
        if (humanOnly && !CertificateRenewalEndpoints.Human(h, actor)) return await Fail(h, action, 403, "human_authority_required", routeId);
        if (!await SafetyEventCommands.Csrf(h)) return await Fail(h, action, 403, "csrf_invalid", routeId);
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await Fail(h, action, 400, "invalid_idempotency_key", routeId);
        var key = parsedKey.ToString("D");
        T command;
        try { command = await Body<T>(h, maxBody, ct) ?? throw new JsonException(); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException)
        { return await Fail(h, action, 400, "invalid_request", routeId); }
        var resource = resourceOf(command) ?? routeId ?? Guid.Empty;
        if (resource == Guid.Empty) return await Fail(h, action, 400, "invalid_request");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation, vessel, routeId, command }, Json));
        async Task<IResult> Fresh(Guid id)
        {
            var live = SafetyEventCommands.Actor(h);
            if (live == null || !allowed(live)) return await Fail(h, action, 403, "permission_denied", id);
            db.ChangeTracker.Clear();
            var value = await read(db, live, vessel, id, h, ct);
            if (value == null) return await Fail(h, action, 503, "command_outcome_withheld", id);
            await SafetyEventCommands.Audit(h, action, "Success", id);
            return Results.Json(value, Json);
        }
        await SafetyEventCommands.Audit(h, action, "Attempted", resource);
        AssuranceOutcome? committed = null, failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                var receipt = await db.AssuranceReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Action != operation || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel)
                        return await Fail(h, action, 409, "idempotency_key_reused", resource);
                    return await Fresh(receipt.ResourceId);
                }
                var outcome = await apply(new(h, actor, vessel, routeId, command, SafetyEventCommands.Now(h), db, ct));
                if (outcome.Status != 200) { failure = outcome; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
                else
                {
                    db.AssuranceReceipts.Add(new() { TenantId = actor.TenantId, VesselId = vessel, ActorId = actor.Id, Key = key, Action = operation, Fingerprint = fingerprint,
                        ResourceId = outcome.ResourceId, Revision = Math.Max(1, outcome.Revision), CommittedAt = SafetyEventCommands.Now(h) });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = outcome;
                }
            }
            catch (DbUpdateConcurrencyException)
            { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); failure = AssuranceOutcome.Fail(409, "version_conflict"); }
            catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
            {
                // Only serialization/deadlock (40001/40P01) and unique races (23505) are conflicts.
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                failure = PostgresFailure.IsConflict(ex) ? AssuranceOutcome.Fail(409, "version_conflict") : AssuranceOutcome.Fail(503, "service_unavailable");
            }
        }
        if (failure != null) return await Fail(h, action, failure.Status, failure.Code!, resource, failure.Missing);
        try { return await Fresh(committed!.ResourceId); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex))
        { return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503); }
    }
}
