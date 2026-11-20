using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record TowageOutcome(int Status, string? Code, Guid ResourceId, long Revision, string[]? Missing = null)
{
    internal static TowageOutcome Ok(Guid id, long revision) => new(200, null, id, revision);
    internal static TowageOutcome Fail(int status, string code, string[]? missing = null) => new(status, code, Guid.Empty, 0, missing);
}
internal sealed record TowageContext<T>(HttpContext Http, TrustedActor Actor, Guid Vessel, Guid? RouteId, T Command, DateTimeOffset Now, PoseidonDbContext Db, CancellationToken Ct);

// Current towage facts for one tug job, read through owning gates. Used by the towage screens, the DEV-084
// readiness engine (communication + briefing inputs) and the DEV-107 fleet projection.
internal sealed record TowageSnapshot(TowagePlan? PlanRow, TowagePlanFacts? Plan, TowageBriefing? BriefingRow, TowageBriefingFacts? Briefing,
    TowageRiskFacts? Risk, bool RiskReadable, string? RiskAcknowledgementState);

internal static class TowageReader
{
    internal static TowagePlanContent PlanContent(string json) => JsonSerializer.Deserialize<TowagePlanContent>(json, TowageJson.Options) ?? throw new InvalidDataException("towage_content_unavailable");
    internal static TowageBriefingContent BriefingContent(string json) => JsonSerializer.Deserialize<TowageBriefingContent>(json, TowageJson.Options) ?? throw new InvalidDataException("towage_content_unavailable");
    internal static Guid[] Authors(string json) => JsonSerializer.Deserialize<Guid[]>(json, TowageJson.Options) ?? [];
    internal static string WithAuthor(string json, Guid actor) => JsonSerializer.Serialize(Authors(json).Append(actor).Distinct().Order().ToArray(), TowageJson.Options);

    internal static TowagePlanFacts Facts(TowagePlan p) => new(p.Id, p.Revision, p.State, p.ApprovedRevision, p.JobRevision, PlanContent(p.ContentJson));

    internal static async Task<TowageRiskFacts?> Risk(PoseidonDbContext db, Guid? id, CancellationToken ct) => id == null ? null :
        await db.RiskAssessments.AsNoTracking().Where(r => r.Id == id).Select(r => new TowageRiskFacts(r.Id, r.State, r.Revision, r.ApprovedRevision)).SingleOrDefaultAsync(ct);

    internal static async Task<string?> RiskAcknowledgement(PoseidonDbContext db, Guid id, CancellationToken ct) {
        var r = await db.RiskAssessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r == null || r.ApprovedRevision == null) return null;
        var content = JsonSerializer.Deserialize<RiskAssessmentContent>(r.ContentJson, RiskCommands.Json);
        if (content == null) return null;
        var acks = await db.RiskAssessmentAcknowledgements.AsNoTracking().Where(k => k.AssessmentId == id && k.Revision == r.Revision)
            .Select(k => new { k.ActorId, k.Choice }).ToArrayAsync(ct);
        return RiskPolicy.AcknowledgementState(r.State, content, acks.Select(k => (k.ActorId, k.Choice)));
    }

    internal static async Task<TowageSnapshot> Read(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid job, CancellationToken ct) {
        var plan = await db.TowagePlans.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.JobId == job, ct);
        var briefing = await db.TowageBriefings.AsNoTracking().SingleOrDefaultAsync(b => b.VesselId == vessel && b.JobId == job, ct);
        TowageBriefingFacts? facts = null;
        if (briefing != null) {
            var acks = await db.TowageBriefingAcknowledgements.AsNoTracking().Where(k => k.BriefingId == briefing.Id && k.Revision == briefing.Revision)
                .Select(k => new { k.ActorId, k.Choice }).ToArrayAsync(ct);
            facts = new(briefing.Id, briefing.Revision, briefing.State, briefing.PlanRevision, briefing.RiskRevision, BriefingContent(briefing.ContentJson),
                acks.Select(k => (k.ActorId, k.Choice)).ToArray());
        }
        var readable = RiskAssessmentEndpoints.CanRead(a);
        var riskId = facts?.Content.RiskAssessmentId;
        var risk = readable ? await Risk(db, riskId, ct) : null;
        var ack = readable && risk != null ? await RiskAcknowledgement(db, risk.Id, ct) : null;
        return new(plan, plan == null ? null : Facts(plan), briefing, facts, risk, readable, ack);
    }
}

// Shared closed command transport for DEV-087/088: server authorization, exact vessel scope, CSRF,
// Idempotency-Key receipt, bounded strict JSON, one SERIALIZABLE transaction and attributed EF audit.
// Replays re-read the committed resource under current authorization; they never repeat the effect.
internal static class TowageCommands
{
    private const int MaxBody = 131072;
    internal static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null, string[]? missing = null) {
        h.Response.Headers.CacheControl = "no-store";
        await TugReadinessHttp.Observe(h, action, code, id);
        return missing == null ? Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status)
            : Results.Json(new RiskFailure(code, missing, h.TraceIdentifier), statusCode: status);
    }

    internal static async Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string operation, string auditAction, Guid? routeId,
        Func<TrustedActor, Guid, bool> allowed, Func<T, Guid?> resourceOf, Func<TowageContext<T>, Task<TowageOutcome>> apply,
        Func<HttpContext, PoseidonDbContext, TrustedActor, Guid, Guid, CancellationToken, Task<object?>> read) where T : class
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await Fail(h, auditAction, 400, "invalid_query", routeId);
        var vessel = q["vessel"];
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (actor == null || !actor.CanReadTug || !actor.HasWriteAttribution || db.TenantId != actor.TenantId) return await Fail(h, auditAction, 403, "permission_denied", routeId);
        if (!await TugReadinessHttp.Tugs(db, actor).AnyAsync(v => v.Id == vessel, ct)) return await Fail(h, auditAction, 404, "resource_not_found", routeId);
        if (!allowed(actor, vessel)) return await Fail(h, auditAction, 403, "permission_denied", routeId);
        if (h.Request.Headers["X-CSRF-TOKEN"].Count != 1) return await Fail(h, auditAction, 400, "csrf_invalid", routeId);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, auditAction, 400, "csrf_invalid", routeId); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await Fail(h, auditAction, 400, "invalid_idempotency_key", routeId);
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
            command = document.RootElement.Deserialize<T>(TowageJson.Options) ?? throw new JsonException();
        } catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException) {
            return await Fail(h, auditAction, 400, "invalid_request", routeId);
        }
        var resource = resourceOf(command) ?? routeId ?? Guid.Empty;
        if (resource == Guid.Empty) return await Fail(h, auditAction, 400, "invalid_request");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation, vessel, routeId, command }, TowageJson.Options));
        async Task<IResult> Fresh(Guid id) {
            var live = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            if (live == null || !live.CanReadTug) return await Fail(h, auditAction, 403, "permission_denied", id);
            db.ChangeTracker.Clear();
            var value = await read(h, db, live, vessel, id, ct);
            if (value == null) return await Fail(h, auditAction, 503, "command_outcome_withheld", id);
            await TugReadinessHttp.Observe(h, auditAction, "Success", id);
            return Results.Json(value, TowageJson.Options);
        }
        await TugReadinessHttp.Observe(h, auditAction, "Attempted", resource);
        Guid? committed = null; TowageOutcome? failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)) {
            try {
                var receipt = await db.TowageReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null) {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Action != operation || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel)
                        return await Fail(h, auditAction, 409, "idempotency_key_reused", resource);
                    return await Fresh(receipt.ResourceId);
                }
                var outcome = await apply(new(h, actor, vessel, routeId, command, TugReadinessHttp.Now(h), db, ct));
                if (outcome.Status != 200) { failure = outcome; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
                else {
                    db.TowageReceipts.Add(new() { TenantId = actor.TenantId, VesselId = vessel, ActorId = actor.Id, Key = key, Action = operation, Fingerprint = fingerprint,
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
        if (failure != null) return await Fail(h, auditAction, failure.Status, failure.Code!, resource, failure.Missing);
        try { return await Fresh(committed!.Value); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)) {
            return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503);
        }
    }
}
