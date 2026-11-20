using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// S9 WP-D shared closed command transport (DEV-089/090/111): server authorization, exact vessel scope, CSRF,
// Idempotency-Key receipt (the offline/replay key: a replay re-reads the committed resource and never repeats
// the effect), bounded strict JSON, one SERIALIZABLE transaction classified only through PostgresFailure, and
// attributed EF audit. Same envelope as the S8 towage commands.
internal static class FieldOpsCommands
{
    private const int MaxBody = 262144;

    internal static bool Human(HttpContext h, TrustedActor a) {
        var members = h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m => m.ActorId == a.Id && m.TenantId == a.TenantId).ToArray();
        return members.Length == 1 && string.Equals(members[0].ActorKind, "Human", StringComparison.Ordinal);
    }

    internal static async Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string operation, string auditAction, Guid? routeId,
        Func<TrustedActor, bool> canRead, Func<PoseidonDbContext, TrustedActor, IQueryable<Vessel>> vessels,
        Func<TrustedActor, Guid, bool> allowed, Func<T, Guid?> resourceOf, Func<TowageContext<T>, Task<TowageOutcome>> apply,
        Func<HttpContext, PoseidonDbContext, TrustedActor, Guid, Guid, CancellationToken, Task<object?>> read) where T : class
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TowageCommands.Fail(h, auditAction, 400, "invalid_query", routeId);
        var vessel = q["vessel"];
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (actor == null || !canRead(actor) || !actor.HasWriteAttribution || db.TenantId != actor.TenantId) return await TowageCommands.Fail(h, auditAction, 403, "permission_denied", routeId);
        if (!await vessels(db, actor).AnyAsync(v => v.Id == vessel, ct)) return await TowageCommands.Fail(h, auditAction, 404, "resource_not_found", routeId);
        if (!allowed(actor, vessel)) return await TowageCommands.Fail(h, auditAction, 403, "permission_denied", routeId);
        if (h.Request.Headers["X-CSRF-TOKEN"].Count != 1) return await TowageCommands.Fail(h, auditAction, 400, "csrf_invalid", routeId);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await TowageCommands.Fail(h, auditAction, 400, "csrf_invalid", routeId); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await TowageCommands.Fail(h, auditAction, 400, "invalid_idempotency_key", routeId);
        var key = parsedKey.ToString("D");
        T command;
        try {
            if (!h.Request.HasJsonContentType() || h.Request.ContentLength > MaxBody) throw new JsonException();
            using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
            while ((count = await h.Request.Body.ReadAsync(chunk, ct)) > 0) {
                if (buffer.Length + count > MaxBody) throw new JsonException();
                buffer.Write(chunk, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command = document.RootElement.Deserialize<T>(FieldOpsJson.Options) ?? throw new JsonException();
        } catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException) {
            return await TowageCommands.Fail(h, auditAction, 400, "invalid_request", routeId);
        }
        var resource = resourceOf(command) ?? routeId ?? Guid.Empty;
        if (resource == Guid.Empty) return await TowageCommands.Fail(h, auditAction, 400, "invalid_request");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation, vessel, routeId, command }, FieldOpsJson.Options));
        async Task<IResult> Fresh(Guid id) {
            var live = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            if (live == null || !canRead(live)) return await TowageCommands.Fail(h, auditAction, 403, "permission_denied", id);
            db.ChangeTracker.Clear();
            var value = await read(h, db, live, vessel, id, ct);
            if (value == null) return await TowageCommands.Fail(h, auditAction, 503, "command_outcome_withheld", id);
            await TugReadinessHttp.Observe(h, auditAction, "Success", id);
            return Results.Json(value, FieldOpsJson.Options);
        }
        await TugReadinessHttp.Observe(h, auditAction, "Attempted", resource);
        Guid? committed = null; TowageOutcome? failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)) {
            try {
                var receipt = await db.FieldOperationReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null) {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Action != operation || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel)
                        return await TowageCommands.Fail(h, auditAction, 409, "idempotency_key_reused", resource);
                    return await Fresh(receipt.ResourceId);
                }
                var outcome = await apply(new(h, actor, vessel, routeId, command, TugReadinessHttp.Now(h), db, ct));
                if (outcome.Status != 200) { failure = outcome; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
                else {
                    db.FieldOperationReceipts.Add(new() { TenantId = actor.TenantId, VesselId = vessel, ActorId = actor.Id, Key = key, Action = operation, Fingerprint = fingerprint,
                        ResourceId = outcome.ResourceId, Revision = Math.Max(1, outcome.Revision), CommittedAt = TugReadinessHttp.Now(h) });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = outcome.ResourceId;
                }
            } catch (DbUpdateConcurrencyException) {
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); failure = TowageOutcome.Fail(409, "version_conflict");
            } catch (Exception ex) when (PostgresFailure.IsDatabase(ex)) {
                // Only serialization/deadlock (40001/40P01) and unique races (23505) are conflicts.
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                failure = PostgresFailure.IsConflict(ex) ? TowageOutcome.Fail(409, "version_conflict") : TowageOutcome.Fail(503, "service_unavailable");
            }
        }
        if (failure != null) return await TowageCommands.Fail(h, auditAction, failure.Status, failure.Code!, resource, failure.Missing);
        try { return await Fresh(committed!.Value); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)) {
            return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503);
        }
    }

    internal static Guid[] Authors(string json) => JsonSerializer.Deserialize<Guid[]>(json, FieldOpsJson.Options) ?? [];
    internal static string WithAuthor(string json, Guid actor) => JsonSerializer.Serialize(Authors(json).Append(actor).Distinct().Order().ToArray(), FieldOpsJson.Options);
}
