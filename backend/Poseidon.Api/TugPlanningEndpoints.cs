using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugPlanCommand(uint? BaseVersion, uint RequestVersion, string PolicyVersion, TugPlanChange Change);
public sealed record TugPlanDto(Guid JobId, long Revision, uint? Version, long RequestRevision, string PolicyVersion,
    string SourceVersion, TugPlanInputs Inputs, string Operation, string Provenance, DateTimeOffset SavedAt, bool Replayed = false);
public static partial class TugEndpoints
{
    private static void MapPlanning(IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/tug-jobs/{id:guid}/plan").RequireAuthorization("DirectoryRead");
        g.MapGet("", PlanDetail).WithName("TugPlanDetail");
        g.MapGet("/history", PlanHistory).WithName("TugPlanHistory");
        g.MapGet("/history/{revision:long}", PlanRevision).WithName("TugPlanRevision");
        g.MapGet("/options", PlanOptions).WithName("TugPlanOptions");
        g.MapPut("", PlanSave).Accepts<TugPlanCommand>("application/json").Produces<TugPlanDto>().WithName("TugPlanSave");
    }
    private static TugPlanningPolicy Planning(HttpContext h) => h.RequestServices.GetRequiredService<TugPlanningPolicy>();
    private static async Task<(Guid Vessel, IResult? Failure)> PlanningGate(HttpContext h, PoseidonDbContext db, bool write = false) {
        if (h.Request.Query.Any(q => q.Key != "vessel")) return (default, await Fail(h, 400, "invalid_request"));
        return await Gate(h, db, write);
    }
    private static bool PlanWrite(TrustedActor a, Guid vessel) => Write(a, vessel) && a.Permissions.Contains("tug.plan.write");
    private static async Task<Guid[]> Eligible(HttpContext h, PoseidonDbContext db, Guid vessel) {
        var a = Actor(h); var scope = Planning(h).Scope(a, vessel);
        if (scope == null || !PlanWrite(a, vessel)) return [];
        // Both explicit configured eligibility and actual current editable Tug membership are required.
        var configured = scope.EligibleTugs;
        var candidates = await Tugs(db, a).Where(v => configured.Contains(v.Id)).Select(v => v.Id).ToArrayAsync(h.RequestAborted);
        return candidates.Where(v => Write(a, v)).Order().ToArray();
    }
    private static TugPlanDto PlanDto(TugDraftPlanRevision r, uint? version, bool replayed = false) => new(r.JobId, r.Revision, version,
        r.RequestRevision, r.PolicyVersion, r.SourceVersion, r.Inputs(), r.Operation, r.Provenance, r.SavedAt, replayed);
    private static async Task<bool> PlanVisible(HttpContext h, PoseidonDbContext db, TugPlanInputs inputs) {
        var visible = await Tugs(db, Actor(h)).Where(v => inputs.AssignedTugs.Contains(v.Id)).Select(v => v.Id).ToArrayAsync(h.RequestAborted);
        return inputs.AssignedTugs.All(visible.Contains);
    }
    private static async Task<IResult> PlanOptions(Guid id, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db); if (failure != null) return failure;
        if (!await db.TugJobs.AnyAsync(j => j.Id == id && j.VesselId == vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var policy = Planning(h); var scope = policy.Scope(Actor(h), vessel);
        var ids = await Eligible(h, db, vessel);
        var tugs = await Tugs(db, Actor(h)).Where(v => ids.Contains(v.Id)).Select(v => new { v.Id, v.Code }).ToArrayAsync(h.RequestAborted);
        await Observe(h, "Success", id);
        return Results.Ok(new { state = scope == null ? "Unavailable" : "Configured", policyVersion = scope == null ? null : policy.Version,
            canWrite = scope != null && PlanWrite(Actor(h), vessel), tugs, types = scope == null ? [] : policy.Types,
            offline = "UnavailableForTug;ExistingOfflinePolicySupportsCrewWatchGroupOnly" });
    }
    private static async Task<IResult> PlanDetail(Guid id, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db); if (failure != null) return failure;
        var job = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id && j.VesselId == vessel, h.RequestAborted);
        if (job == null) return await Fail(h, 404, "resource_not_found");
        var p = await db.TugDraftPlans.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.JobId == id, h.RequestAborted);
        if (p == null) { await Observe(h, "Success", id); return Results.Ok(new { state = "Missing", plan = (TugPlanDto?)null, evidence = (TugPlanningEvidence?)null }); }
        var r = await db.TugDraftPlanRevisions.AsNoTracking().SingleAsync(r => r.VesselId == vessel && r.JobId == id && r.Revision == p.Revision, h.RequestAborted);
        if (!await PlanVisible(h, db, r.Inputs())) return await Fail(h, 403, "permission_denied");
        var evidence = await TugPlanningEvidenceAdapter.Read(db, Actor(h), Planning(h), h.RequestServices.GetRequiredService<MaintenanceObservationSettings>(), id, r.Inputs(), h.RequestAborted);
        await Observe(h, "Success", id); return Results.Ok(new { state = "DraftUnverified", requestState = job.Revision == p.RequestRevision ? "CurrentRequestRevision" : "RequestChangedSincePlan", plan = PlanDto(r, p.Version), evidence });
    }
    private static async Task<IResult> PlanHistory(Guid id, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db); if (failure != null) return failure;
        if (!await db.TugJobs.AnyAsync(j => j.Id == id && j.VesselId == vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var visible = Tugs(db, Actor(h)).Select(v => v.Id);
        // Scope before bounding. No total/count of unavailable revisions or hidden Tug references.
        var rows = await db.TugDraftPlanRevisions.AsNoTracking().Where(r => r.JobId == id && r.VesselId == vessel &&
            !db.TugDraftPlanTugs.Any(t => t.JobId == r.JobId && t.VesselId == r.VesselId && t.Revision == r.Revision && !visible.Contains(t.TugId)))
            .OrderByDescending(r => r.Revision).Take(100).Select(r => new { r.Revision, r.RequestRevision, r.Operation, r.SourceVersion, r.PolicyVersion, r.Provenance, r.SavedAt }).ToArrayAsync(h.RequestAborted);
        await Observe(h, "Success", id); return Results.Ok(new { items = rows, coverage = "Latest100CurrentlyAuthorizedRevisions" });
    }
    private static async Task<IResult> PlanRevision(Guid id, long revision, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db); if (failure != null) return failure;
        var r = await db.TugDraftPlanRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.VesselId == vessel && r.JobId == id && r.Revision == revision, h.RequestAborted);
        if (r == null || !await PlanVisible(h, db, r.Inputs())) return await Fail(h, 404, "resource_not_found");
        await Observe(h, "Success", id); var version = await db.CommandReceipts.AsNoTracking().Where(c => c.ResourceId == id && c.Action == "TugPlanSave" && c.RegisterRevision == revision).Select(c => c.RegisterVersion).FirstOrDefaultAsync(h.RequestAborted);
        return Results.Ok(PlanDto(r, version)); // missing historical xmin remains null; never substitute the current token
    }
    private static async Task<IResult> PlanSave(Guid id, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db, true); if (failure != null) return failure;
        var a = Actor(h); var policy = Planning(h);
        if (!PlanWrite(a, vessel) || policy.Scope(a, vessel) == null) return await Fail(h, 403, "permission_denied");
        if (h.Request.Headers["X-CSRF-TOKEN"].Count != 1) return await Fail(h, 400, "csrf_invalid");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        if (h.Request.Headers["Idempotency-Key"].Count != 1 || !Guid.TryParseExact(h.Request.Headers["Idempotency-Key"], "D", out var keyId) || keyId == Guid.Empty)
            return await Fail(h, 400, "invalid_idempotency_key");
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > 4096) return await Fail(h, 400, "invalid_request");
        TugPlanCommand command;
        try {
            using var buffer = new MemoryStream(); var bytes = new byte[1024]; int n;
            while ((n = await h.Request.Body.ReadAsync(bytes, h.RequestAborted)) > 0) { if (buffer.Length + n > 4096) return await Fail(h, 400, "invalid_request"); buffer.Write(bytes, 0, n); }
            using var doc = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 4 });
            if (!Unique(doc.RootElement)) return await Fail(h, 400, "invalid_request");
            foreach (var p in doc.RootElement.GetProperty("change").EnumerateObject().Where(p => (p.Name is "plannedStart" or "plannedEnd") && p.Value.ValueKind != JsonValueKind.Null)) {
                if (p.Value.ValueKind != JsonValueKind.String || !System.Text.RegularExpressions.Regex.IsMatch(p.Value.GetString()!, @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})\z")) return await Fail(h, 400, "validation_failed");
            }
            command = doc.RootElement.Deserialize<TugPlanCommand>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false }) ?? throw new JsonException();
        } catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return await Fail(h, 400, "invalid_request"); }
        if (command.Change == null || command.RequestVersion == 0 || command.BaseVersion == 0 || command.PolicyVersion != policy.Version) return await Fail(h, 409, "version_conflict");
        var eligible = await Eligible(h, db, vessel);
        if (new[] { command.Change.TugId, command.Change.ReplacementTugId }.Any(t => t != null && !eligible.Contains(t.Value))) return await Fail(h, 403, "permission_denied");
        var key = keyId.ToString("D"); var ct = h.RequestAborted;
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, vessel, command })));
        await Observe(h, "Attempted", id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            // Actual current object/mode/write/assignment eligibility precede recovery, including old receipts.
            var job = await db.TugJobs.SingleOrDefaultAsync(j => j.Id == id && j.VesselId == vessel, ct);
            if (job == null) return await Fail(h, 404, "resource_not_found");
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != "TugPlanSave" || receipt.ResourceId != id || receipt.Fingerprint != digest) return await Fail(h, 409, "idempotency_key_reused");
                var saved = await db.TugDraftPlanRevisions.AsNoTracking().SingleAsync(r => r.JobId == id && r.VesselId == vessel && r.Revision == receipt.RegisterRevision, ct);
                if (saved.Inputs().AssignedTugs.Any(t => !eligible.Contains(t))) return await Fail(h, 403, "permission_denied");
                await Observe(h, "Success", id); return Results.Ok(PlanDto(saved, receipt.RegisterVersion!.Value, true));
            }
            var plan = await db.TugDraftPlans.SingleOrDefaultAsync(p => p.JobId == id && p.VesselId == vessel, ct);
            if (job.Version != command.RequestVersion || (plan == null ? command.BaseVersion != null : plan.Version != command.BaseVersion)) return await Fail(h, 409, "version_conflict");
            var current = plan == null ? new TugPlanInputs(null, null, []) : (await db.TugDraftPlanRevisions.SingleAsync(r => r.JobId == id && r.VesselId == vessel && r.Revision == plan.Revision, ct)).Inputs();
            if (current.AssignedTugs.Any(t => !eligible.Contains(t))) return await Fail(h, 403, "permission_denied");
            TugPlanInputs next;
            try { next = TugDraftPlanning.Apply(current, command.Change); }
            catch (ArgumentException) { return await Fail(h, 409, "version_conflict"); }
            if (plan == null) { plan = new() { TenantId = a.TenantId, VesselId = vessel, JobId = id }; db.TugDraftPlans.Add(plan); }
            else plan.Revision = checked(plan.Revision + 1);
            plan.PlannedStart = next.PlannedStart?.ToUniversalTime(); plan.PlannedEnd = next.PlannedEnd?.ToUniversalTime();
            next = next with { PlannedStart = plan.PlannedStart, PlannedEnd = plan.PlannedEnd };
            plan.RequestRevision = job.Revision; plan.PolicyVersion = policy.Version; plan.SavedAt = DateTimeOffset.UtcNow;
            var revision = new TugDraftPlanRevision { TenantId = a.TenantId, VesselId = vessel, JobId = id, Revision = plan.Revision,
                RequestRevision = job.Revision, PolicyVersion = policy.Version, ContentJson = JsonSerializer.Serialize(next), Operation = command.Change.Operation, SavedAt = plan.SavedAt };
            db.TugDraftPlanRevisions.Add(revision);
            foreach (var tug in next.AssignedTugs) db.TugDraftPlanTugs.Add(new() { TenantId = a.TenantId, VesselId = vessel, JobId = id, Revision = plan.Revision, TugId = tug });
            await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = "TugPlanSave", Fingerprint = digest,
                ResourceId = id, RegisterRevision = plan.Revision, RegisterVersion = plan.Version, CommittedAt = plan.SavedAt });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await Observe(h, "Success", id);
            return Results.Ok(PlanDto(revision, plan.Version));
        } catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) {
            await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict");
        }
    }
}
