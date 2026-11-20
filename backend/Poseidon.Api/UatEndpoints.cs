using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UatThresholdCommand(string Scope, [property: JsonRequired] int MinScenarioPassPercent, [property: JsonRequired] int MaxOpenMajorFindings,
    [property: JsonRequired] bool RequireDesignConformance, string AgreementReference, string Rationale, [property: JsonRequired] long ExpectedSequence);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UatRunCreateCommand(string Scope, string Role, string Environment, string BuildCommit, string ParticipantPseudonym);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UatResultCommand(string ScenarioId, string Outcome, int? DurationSeconds, [property: JsonRequired] bool Assisted,
    string? AutomatedEvidenceReference, string? Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UatFindingCommand(string ScenarioId, string Severity, [property: JsonRequired] bool IsUx, [property: JsonRequired] bool DesignConformance, string Title);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UatFindingResolveCommand(string State, string Resolution, string? ResolutionReference, [property: JsonRequired] uint ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UatRunSubmitCommand([property: JsonRequired] uint ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UatRunAttestCommand(string Statement, [property: JsonRequired] uint ExpectedVersion);

public sealed record UatCatalogView(string Version, UatScenario[] Scenarios, string[] ShipRoles, string[] TugRoles, string[] Environments, string[] Outcomes,
    string[] Severities, string Authority = UatPolicy.Authority);
public sealed record UatSummaryView(string Scope, string CatalogVersion, UatThresholdDto? Threshold, long ThresholdSequence, UatGateOutcome Gate, UatRunDto[] Runs,
    UatFindingDto[] OpenFindings, bool CanRecord, bool CanRecordThreshold, bool CanAttest, string Authority = UatPolicy.Authority);
public sealed record UatRunDetailView(UatRunDto Run, UatResultDto[] Results, UatFindingDto[] Findings, bool CanRecord, bool CanAttest);

// DEV-074 / DEV-094 UAT record infrastructure (18.01 rows 75/95; 19.01 §3-4 p68-123). Facilitators record human
// participant outcomes and findings; a second human attests the run. The summary aggregates against the recorded
// human threshold; nothing here accepts UAT or authorizes the pilot.
public static class UatEndpoints
{
    private const string ReadAction = "UatRead", CommandAction = "UatCommand";

    public static void MapUat(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/uat").RequireAuthorization("DirectoryRead");
        g.MapGet("/scenarios", (Func<HttpContext, Task<IResult>>)Catalog).WithName("UatScenarioCatalog").Produces<UatCatalogView>();
        g.MapGet("/summary", Summary).WithName("UatSummaryRead").Produces<UatSummaryView>();
        g.MapGet("/runs/{id:guid}", Detail).WithName("UatRunRead").Produces<UatRunDetailView>();
        g.MapPost("/thresholds", Threshold).WithName("UatThresholdRecord").Accepts<UatThresholdCommand>("application/json").Produces<UatThresholdDto>();
        g.MapPost("/runs", CreateRun).WithName("UatRunCreate").Accepts<UatRunCreateCommand>("application/json").Produces<UatRunDto>();
        g.MapPost("/runs/{id:guid}/results", Result).WithName("UatResultRecord").Accepts<UatResultCommand>("application/json").Produces<UatResultDto>();
        g.MapPost("/runs/{id:guid}/findings", Finding).WithName("UatFindingRecord").Accepts<UatFindingCommand>("application/json").Produces<UatFindingDto>();
        g.MapPost("/runs/{id:guid}/submit", Submit).WithName("UatRunSubmit").Accepts<UatRunSubmitCommand>("application/json").Produces<UatRunDto>();
        g.MapPost("/runs/{id:guid}/attest", Attest).WithName("UatRunAttest").Accepts<UatRunAttestCommand>("application/json").Produces<UatRunDto>();
        g.MapPost("/findings/{id:guid}/resolve", Resolve).WithName("UatFindingResolve").Accepts<UatFindingResolveCommand>("application/json").Produces<UatFindingDto>();
    }

    private static FederationSettings Settings(HttpContext h) => h.RequestServices.GetRequiredService<FederationSettings>();

    private static async Task<IResult> Catalog(HttpContext h) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!PilotAccess.UatRead(a)) return await TugReadinessHttp.Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Count > 0) return await TugReadinessHttp.Fail(h, ReadAction, 400, "invalid_query");
        await TugReadinessHttp.Observe(h, ReadAction, "Success");
        return Results.Ok(new UatCatalogView(UatScenarioCatalog.Version, UatScenarioCatalog.Scenarios, UatScenarioCatalog.ShipRoles, UatScenarioCatalog.TugRoles,
            UatPolicy.Environments, UatPolicy.Outcomes, UatPolicy.Severities));
    }

    private static async Task<IResult> Summary(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!PilotAccess.UatRead(a)) return await TugReadinessHttp.Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Any(q => q.Key != "scope" || q.Value.Count != 1) || !UatScenarioCatalog.Scopes.Contains(h.Request.Query["scope"].ToString(), StringComparer.Ordinal))
            return await TugReadinessHttp.Fail(h, ReadAction, 400, "invalid_query");
        var scope = h.Request.Query["scope"].ToString();
        var s = await UatReader.Evaluate(db, scope, ct);
        var human = PilotAccess.Human(Settings(h), a);
        await TugReadinessHttp.Observe(h, ReadAction, "Success");
        return Results.Ok(new UatSummaryView(scope, UatScenarioCatalog.Version, s.Threshold == null ? null : UatReader.Dto(s.Threshold), s.Threshold?.Sequence ?? 0, s.Gate,
            s.Runs.Take(100).Select(UatReader.Dto).ToArray(), s.Findings.Where(f => f.State == UatPolicy.Open).Take(200).Select(UatReader.Dto).ToArray(),
            human && PilotAccess.UatRecord(a), human && PilotAccess.UatThreshold(a), human && PilotAccess.UatAttest(a)));
    }

    private static async Task<UatRunDetailView> DetailView(PoseidonDbContext db, TrustedActor a, bool human, UatRun run, CancellationToken ct) {
        var results = await db.UatStepResults.AsNoTracking().Where(x => x.RunId == run.Id).OrderBy(x => x.ScenarioId).ThenBy(x => x.Attempt).Take(512).ToArrayAsync(ct);
        var findings = await db.UatFindings.AsNoTracking().Where(f => f.RunId == run.Id).OrderBy(f => f.ReportedAt).ThenBy(f => f.Id).Take(256).ToArrayAsync(ct);
        return new(UatReader.Dto(run), results.Select(UatReader.Dto).ToArray(), findings.Select(UatReader.Dto).ToArray(),
            human && PilotAccess.UatRecord(a) && run.State == UatPolicy.RunOpen && run.FacilitatorActorId == a.Id,
            human && PilotAccess.UatAttest(a) && run.State == UatPolicy.RunSubmitted && run.FacilitatorActorId != a.Id);
    }

    private static async Task<IResult> Detail(HttpContext h, PoseidonDbContext db, Guid id) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!PilotAccess.UatRead(a)) return await TugReadinessHttp.Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Count > 0) return await TugReadinessHttp.Fail(h, ReadAction, 400, "invalid_query");
        var run = await db.UatRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (run == null) return await TugReadinessHttp.Fail(h, ReadAction, 404, "resource_not_found");
        await TugReadinessHttp.Observe(h, ReadAction, "Success", id);
        return Results.Ok(await DetailView(db, a, PilotAccess.Human(Settings(h), a), run, ct));
    }

    // Shared write preamble: permission, human principal, no query string, CSRF/idempotency/strict JSON.
    private static async Task<(TrustedActor? Actor, T? Command, string Key, IResult? Failure)> Begin<T>(HttpContext h, Func<TrustedActor?, bool> permitted) where T : class {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!permitted(a)) return (null, null, "", await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied"));
        if (!PilotAccess.Human(Settings(h), a)) return (null, null, "", await TugReadinessHttp.Fail(h, CommandAction, 403, "human_actor_required"));
        if (h.Request.Query.Count > 0) return (null, null, "", await TugReadinessHttp.Fail(h, CommandAction, 400, "invalid_request"));
        var (command, key, failure) = await TugReadinessHttp.Read<T>(h, CommandAction);
        return (a, command, key, failure);
    }

    private static async Task<IResult> Threshold(HttpContext h, PoseidonDbContext db) {
        var (a, c, key, failure) = await Begin<UatThresholdCommand>(h, PilotAccess.UatThreshold);
        if (failure != null) return failure;
        if (!UatScenarioCatalog.Scopes.Contains(c!.Scope, StringComparer.Ordinal) || !UatPolicy.ValidThreshold(new(c.MinScenarioPassPercent, c.MaxOpenMajorFindings, c.RequireDesignConformance)) ||
            !UatPolicy.ValidText(c.AgreementReference, 3, UatPolicy.ReferenceMax) || !UatPolicy.ValidText(c.Rationale, 10, UatPolicy.TextMax) || c.ExpectedSequence < 0)
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed");
        var now = TugReadinessHttp.Now(h);
        return await PilotHttp.Commit(h, db, a!, CommandAction, "UatThresholdRecord", key, new { c }, async ct => {
            var previous = await db.UatThresholds.AsNoTracking().Where(t => t.Scope == c.Scope).OrderByDescending(t => t.Sequence).Select(t => new { t.Id, t.Sequence }).FirstOrDefaultAsync(ct);
            if ((previous?.Sequence ?? 0) != c.ExpectedSequence) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "threshold_sequence_conflict"), Guid.Empty);
            var t = new UatThreshold { TenantId = a!.TenantId, Id = Guid.NewGuid(), Scope = c.Scope, Sequence = (previous?.Sequence ?? 0) + 1, PreviousThresholdId = previous?.Id,
                MinScenarioPassPercent = c.MinScenarioPassPercent, MaxOpenMajorFindings = c.MaxOpenMajorFindings, RequireDesignConformance = c.RequireDesignConformance,
                AgreementReference = c.AgreementReference, Rationale = c.Rationale, ActorId = a.Id, RecordedAt = now };
            db.UatThresholds.Add(t);
            return (null, t.Id);
        }, async (id, ct) => UatReader.Dto(await db.UatThresholds.AsNoTracking().SingleAsync(t => t.Id == id, ct)));
    }

    private static async Task<IResult> CreateRun(HttpContext h, PoseidonDbContext db) {
        var (a, c, key, failure) = await Begin<UatRunCreateCommand>(h, PilotAccess.UatRecord);
        if (failure != null) return failure;
        if (!UatScenarioCatalog.Scopes.Contains(c!.Scope, StringComparer.Ordinal) || !UatScenarioCatalog.Roles(c.Scope).Contains(c.Role, StringComparer.Ordinal) ||
            !UatPolicy.Environments.Contains(c.Environment, StringComparer.Ordinal) || !UatPolicy.ValidCommit(c.BuildCommit) || !UatPolicy.ValidPseudonym(c.ParticipantPseudonym))
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed");
        var now = TugReadinessHttp.Now(h);
        return await PilotHttp.Commit(h, db, a!, CommandAction, "UatRunCreate", key, new { c }, ct => {
            var run = new UatRun { TenantId = a!.TenantId, Id = Guid.NewGuid(), Scope = c.Scope, Role = c.Role, Environment = c.Environment, BuildCommit = c.BuildCommit,
                ParticipantPseudonym = c.ParticipantPseudonym, CatalogVersion = UatScenarioCatalog.Version, FacilitatorActorId = a.Id, CreatedAt = now };
            db.UatRuns.Add(run);
            return Task.FromResult<(IResult?, Guid)>((null, run.Id));
        }, async (id, ct) => UatReader.Dto(await db.UatRuns.AsNoTracking().SingleAsync(r => r.Id == id, ct)));
    }

    // Only the run's facilitator records outcomes while the run is OPEN. The outcome is the participant's observed
    // result; an automated evidence reference is stored as an attachment and never changes it.
    private static async Task<IResult> Result(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, c, key, failure) = await Begin<UatResultCommand>(h, PilotAccess.UatRecord);
        if (failure != null) return failure;
        if (UatScenarioCatalog.Find(c!.ScenarioId) is not { } scenario || !UatPolicy.Outcomes.Contains(c.Outcome, StringComparer.Ordinal) ||
            c.DurationSeconds is < 0 or > 86400 || !UatPolicy.ValidOptionalReference(c.AutomatedEvidenceReference) || c.Note != null && !UatPolicy.ValidText(c.Note, 1, UatPolicy.TextMax))
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed", id);
        var now = TugReadinessHttp.Now(h);
        return await PilotHttp.Commit(h, db, a!, CommandAction, "UatResultRecord", key, new { id, c }, async ct => {
            var run = await db.UatRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
            if (run == null) return (await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found", id), Guid.Empty);
            if (run.FacilitatorActorId != a!.Id) return (await TugReadinessHttp.Fail(h, CommandAction, 403, "facilitator_required", id), Guid.Empty);
            if (run.State != UatPolicy.RunOpen) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "uat_run_not_open", id), Guid.Empty);
            if (scenario.Scope != run.Scope) return (await TugReadinessHttp.Fail(h, CommandAction, 422, "scenario_out_of_scope", id), Guid.Empty);
            var attempt = await db.UatStepResults.AsNoTracking().Where(x => x.RunId == id && x.ScenarioId == c.ScenarioId).MaxAsync(x => (int?)x.Attempt, ct) ?? 0;
            var row = new UatStepResult { TenantId = a.TenantId, Id = Guid.NewGuid(), RunId = id, ScenarioId = c.ScenarioId, Attempt = attempt + 1, Outcome = c.Outcome,
                DurationSeconds = c.DurationSeconds, Assisted = c.Assisted, AutomatedEvidenceReference = c.AutomatedEvidenceReference, Note = c.Note, RecordedBy = a.Id, RecordedAt = now };
            db.UatStepResults.Add(row);
            return (null, row.Id);
        }, async (rid, ct) => UatReader.Dto(await db.UatStepResults.AsNoTracking().SingleAsync(x => x.Id == rid, ct)));
    }

    private static async Task<IResult> Finding(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, c, key, failure) = await Begin<UatFindingCommand>(h, PilotAccess.UatRecord);
        if (failure != null) return failure;
        if (UatScenarioCatalog.Find(c!.ScenarioId) is not { } scenario || !UatPolicy.Severities.Contains(c.Severity, StringComparer.Ordinal) || !UatPolicy.ValidText(c.Title, 5, UatPolicy.TitleMax))
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed", id);
        var now = TugReadinessHttp.Now(h);
        return await PilotHttp.Commit(h, db, a!, CommandAction, "UatFindingRecord", key, new { id, c }, async ct => {
            var run = await db.UatRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
            if (run == null) return (await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found", id), Guid.Empty);
            if (run.FacilitatorActorId != a!.Id) return (await TugReadinessHttp.Fail(h, CommandAction, 403, "facilitator_required", id), Guid.Empty);
            if (run.State != UatPolicy.RunOpen) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "uat_run_not_open", id), Guid.Empty);
            if (scenario.Scope != run.Scope) return (await TugReadinessHttp.Fail(h, CommandAction, 422, "scenario_out_of_scope", id), Guid.Empty);
            var f = new UatFinding { TenantId = a.TenantId, Id = Guid.NewGuid(), RunId = id, Scope = run.Scope, ScenarioId = c.ScenarioId, Severity = c.Severity, IsUx = c.IsUx,
                DesignConformance = c.DesignConformance, Title = c.Title, ReportedBy = a.Id, ReportedAt = now };
            db.UatFindings.Add(f);
            return (null, f.Id);
        }, async (fid, ct) => UatReader.Dto(await db.UatFindings.AsNoTracking().SingleAsync(x => x.Id == fid, ct)));
    }

    private static async Task<IResult> Submit(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, c, key, failure) = await Begin<UatRunSubmitCommand>(h, PilotAccess.UatRecord);
        if (failure != null) return failure;
        var now = TugReadinessHttp.Now(h);
        return await PilotHttp.Commit(h, db, a!, CommandAction, "UatRunSubmit", key, new { id, c }, async ct => {
            var run = await db.UatRuns.SingleOrDefaultAsync(r => r.Id == id, ct);
            if (run == null) return (await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found", id), Guid.Empty);
            if (run.FacilitatorActorId != a!.Id) return (await TugReadinessHttp.Fail(h, CommandAction, 403, "facilitator_required", id), Guid.Empty);
            if (run.Version != c!.ExpectedVersion) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "version_conflict", id), Guid.Empty);
            if (run.State != UatPolicy.RunOpen) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "uat_run_not_open", id), Guid.Empty);
            // A synthetic fixture run never leaves OPEN (DB CHECK CK_UatRun_Synthetic).
            if (run.Provenance != "UserDeclaredUnverified") return (await TugReadinessHttp.Fail(h, CommandAction, 409, "synthetic_record_not_submittable", id), Guid.Empty);
            run.State = UatPolicy.RunSubmitted; run.SubmittedAt = now;
            return (null, run.Id);
        }, async (rid, ct) => UatReader.Dto(await db.UatRuns.AsNoTracking().SingleAsync(r => r.Id == rid, ct)));
    }

    // Separation of duties: the attester is a different human than the facilitator (DB CHECK as well). The
    // statement is the attester's own text; the system never fills or generates it.
    private static async Task<IResult> Attest(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, c, key, failure) = await Begin<UatRunAttestCommand>(h, PilotAccess.UatAttest);
        if (failure != null) return failure;
        if (!UatPolicy.ValidText(c!.Statement, 10, UatPolicy.TextMax)) return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed", id);
        var now = TugReadinessHttp.Now(h);
        return await PilotHttp.Commit(h, db, a!, CommandAction, "UatRunAttest", key, new { id, c }, async ct => {
            var run = await db.UatRuns.SingleOrDefaultAsync(r => r.Id == id, ct);
            if (run == null) return (await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found", id), Guid.Empty);
            if (run.FacilitatorActorId == a!.Id) return (await TugReadinessHttp.Fail(h, CommandAction, 403, "separation_of_duties_required", id), Guid.Empty);
            if (run.Version != c.ExpectedVersion) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "version_conflict", id), Guid.Empty);
            if (run.State != UatPolicy.RunSubmitted) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "uat_run_not_submitted", id), Guid.Empty);
            run.State = UatPolicy.RunAttested; run.AttestedBy = a.Id; run.AttestedAt = now; run.AttestationStatement = c.Statement;
            return (null, run.Id);
        }, async (rid, ct) => UatReader.Dto(await db.UatRuns.AsNoTracking().SingleAsync(r => r.Id == rid, ct)));
    }

    // A finding is resolved by a different human than its reporter; a BLOCKER is only RESOLVED (fixed and retested),
    // never ACCEPTED_RISK (19.01 p277 "UAT critical blocker: 0 at pilot GO").
    private static async Task<IResult> Resolve(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, c, key, failure) = await Begin<UatFindingResolveCommand>(h, PilotAccess.UatRecord);
        if (failure != null) return failure;
        if (!UatPolicy.FindingStates.Contains(c!.State, StringComparer.Ordinal) || c.State == UatPolicy.Open || !UatPolicy.ValidText(c.Resolution, 10, UatPolicy.TextMax) ||
            !UatPolicy.ValidOptionalReference(c.ResolutionReference))
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed", id);
        var now = TugReadinessHttp.Now(h);
        return await PilotHttp.Commit(h, db, a!, CommandAction, "UatFindingResolve", key, new { id, c }, async ct => {
            var f = await db.UatFindings.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (f == null) return (await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found", id), Guid.Empty);
            if (f.ReportedBy == a!.Id) return (await TugReadinessHttp.Fail(h, CommandAction, 403, "separation_of_duties_required", id), Guid.Empty);
            if (f.Version != c.ExpectedVersion) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "version_conflict", id), Guid.Empty);
            if (f.State != UatPolicy.Open) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "uat_finding_not_open", id), Guid.Empty);
            if (UatPolicy.ResolutionRefusal(f.Severity, c.State) is { } refusal) return (await TugReadinessHttp.Fail(h, CommandAction, 422, refusal, id), Guid.Empty);
            f.State = c.State; f.ResolvedBy = a.Id; f.ResolvedAt = now; f.Resolution = c.Resolution; f.ResolutionReference = c.ResolutionReference;
            return (null, f.Id);
        }, async (fid, ct) => UatReader.Dto(await db.UatFindings.AsNoTracking().SingleAsync(x => x.Id == fid, ct)));
    }
}

// Shared SERIALIZABLE command path with a per-actor idempotency receipt (PilotReceipt). Replays return the committed
// resource with Idempotency-Replayed: true; a reused key with another body is 409.
internal static class PilotHttp
{
    internal static async Task<IResult> Commit(HttpContext h, PoseidonDbContext db, TrustedActor a, string action, string receiptAction, string key, object body,
        Func<CancellationToken, Task<(IResult? Failure, Guid RecordId)>> work, Func<Guid, CancellationToken, Task<object>> read) {
        var ct = h.RequestAborted; var now = TugReadinessHttp.Now(h);
        var fingerprint = TugReadinessHttp.Fingerprint(new { receiptAction, body });
        await TugReadinessHttp.Observe(h, action, "Attempted");
        Guid recordId;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)) {
            try {
                var receipt = await db.PilotReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
                if (receipt != null) {
                    if (receipt.Action != receiptAction || receipt.Fingerprint != fingerprint) return await TugReadinessHttp.Fail(h, action, 409, "idempotency_key_reused");
                    h.Response.Headers["Idempotency-Replayed"] = "true";
                    var replay = await read(receipt.RecordId, ct);
                    await TugReadinessHttp.Observe(h, action, "Success", receipt.RecordId);
                    return Results.Ok(replay);
                }
                var (failure, id) = await work(ct);
                if (failure != null) return failure;
                db.PilotReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = receiptAction, Fingerprint = fingerprint, RecordId = id, CommittedAt = now });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                recordId = id;
            } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
                return await TugReadinessHttp.Database(h, db, tx, action, e, null);
            }
        }
        db.ChangeTracker.Clear();
        await TugReadinessHttp.Observe(h, action, "Success", recordId);
        return Results.Ok(await read(recordId, ct));
    }
}
