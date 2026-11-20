using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageExecutionStartCommand([property: JsonRequired] Guid Id, [property: JsonRequired] Guid JobId, [property: JsonRequired] Guid TugId,
    [property: JsonRequired] Guid AuthorityId, [property: JsonRequired] Guid ReadinessDecisionId, string? StartPosition);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageExecutionTransitionCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Action,
    [property: JsonRequired] string Reason, [property: JsonRequired] DateTimeOffset OccurredAt, [property: JsonRequired] Guid AuthorityId, string? EndPosition);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageExecutionEventCommand([property: JsonRequired] TowageEventInput Event);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageExecutionVerifyCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason);

public sealed record TowageExecutionDto(Guid Id, Guid VesselId, Guid JobId, Guid TugId, Guid AssignmentId, long AssignmentRevision, Guid PlanId, long PlanRevision,
    Guid BriefingId, long BriefingRevision, Guid ReadinessDecisionId, string State, string VerificationState, long Revision, uint Version, long Sequence, string Hash,
    DateTimeOffset StartedAt, Guid StartedBy, string? StartPosition, DateTimeOffset? EndedAt, Guid? EndedBy, string? EndPosition, Guid? VerifiedBy, DateTimeOffset? VerifiedAt,
    string Provenance);
public sealed record TowageExecutionEventDto(long Sequence, string Kind, DateTimeOffset OccurredAt, DateTimeOffset RecordedAt, Guid ActorId, string? Duty,
    string? Value, string? Unit, string? Result, string? Text, string? Reference, TowageWeather? Weather, string? Reason, string? Position, string Hash);
public sealed record TowageExecutionView(TowageExecutionDto Execution, string? JobNumber, string? AssistedVessel, TowageLedgerFacts Ledger, TowageExecutionEventDto[] Timeline,
    TugAuthorityDto[] Authorities, bool CanRecord, bool CanTransition, bool CanVerify, string[] AllowedActions, string[] CompleteBlockers, string[] RecordableKinds,
    string[] LoadUnits, string[] CommunicationStates, string[] WinchStates, string[] ReleaseTestResults, string[] EvidencePurposes, string SafetyEvents = "UNKNOWN:SafetyEventSourceUnavailable",
    string Authority = "TugMasterHumanRecord;ShoreAiSystemCannotStartOrAbort;NotAGoDecision", string SourceVersion = TowageExecutionPolicy.SourceVersion);
public sealed record TowageStartGateDto(Guid TugId, Guid? AssignmentId, Guid? ReadinessDecisionId, string? Decision, string[] Blockers, TugAuthorityDto[] Authorities,
    bool CanStart, Guid? ExecutionId, string? ExecutionState);
public sealed record TowageExecutionJobView(Guid VesselId, Guid JobId, string JobNumber, string JobStatus, TowageStartGateDto[] Tugs, TowageExecutionDto[] Executions,
    string SourceVersion = TowageExecutionPolicy.SourceVersion);

// DEV-089 /api/v1/towage-executions. Start, suspend, resume, abort and complete are Tug Master/Master human records
// from a server-provisioned appointment on the executing tug (60.02 p240-246); a shore, AI or system identity
// never holds one. Start requires the approved current plan, the issued and fully acknowledged briefing and the
// current GO human record bound to the current readiness assessment; it never creates a GO. The timeline is an
// append-only, hash-chained ledger; a terminal execution accepts only evidence and notes. Verification is a
// separate person's record.
public static class TowageExecutionEndpoints
{
    private const string Read = "TowageExecutionRead", Command = "TowageExecutionCommand";

    internal static bool CanVerify(TrustedActor a) => a.CanReadTug && a.HasWriteAttribution && a.Permissions.Contains("tug.execution.verify");

    public static void MapTowageExecutions(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/towage-executions").RequireAuthorization("DirectoryRead");
        g.MapGet("", JobView).WithName("TowageExecutionJobRead").Produces<TowageExecutionJobView>();
        g.MapGet("/{id:guid}", ExecutionView).WithName("TowageExecutionRead").Produces<TowageExecutionView>();
        g.MapPost("", (HttpContext h, PoseidonDbContext db) => Run<TowageExecutionStartCommand>(h, db, "TowageExecutionStart", null, (_, _) => true, c => c.Id, Start))
            .WithName("TowageExecutionStart").Accepts<TowageExecutionStartCommand>("application/json").Produces<TowageExecutionView>().Produces<RiskFailure>(422);
        g.MapPost("/{id:guid}/transitions", (Guid id, HttpContext h, PoseidonDbContext db) => Run<TowageExecutionTransitionCommand>(h, db, "TowageExecutionTransition", id, (_, _) => true, _ => id, Transition))
            .WithName("TowageExecutionTransition").Accepts<TowageExecutionTransitionCommand>("application/json").Produces<TowageExecutionView>().Produces<RiskFailure>(422);
        g.MapPost("/{id:guid}/events", (Guid id, HttpContext h, PoseidonDbContext db) => Run<TowageExecutionEventCommand>(h, db, "TowageExecutionEvent", id, (_, _) => true, _ => id, Record))
            .WithName("TowageExecutionEvent").Accepts<TowageExecutionEventCommand>("application/json").Produces<TowageExecutionView>();
        g.MapPost("/{id:guid}/verification", (Guid id, HttpContext h, PoseidonDbContext db) => Run<TowageExecutionVerifyCommand>(h, db, "TowageExecutionVerify", id, (a, _) => CanVerify(a), _ => id, Verify))
            .WithName("TowageExecutionVerify").Accepts<TowageExecutionVerifyCommand>("application/json").Produces<TowageExecutionView>();
    }

    private static Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string operation, Guid? id, Func<TrustedActor, Guid, bool> allowed, Func<T, Guid?> resource,
        Func<TowageContext<T>, Task<TowageOutcome>> apply) where T : class =>
        FieldOpsCommands.Run(h, db, operation, Command, id, a => a.CanReadTug, TugReadinessHttp.Tugs, allowed, resource, apply, ReadView);

    internal static TowageExecutionDto Dto(TowageExecution x) => new(x.Id, x.VesselId, x.JobId, x.TugId, x.AssignmentId, x.AssignmentRevision, x.PlanId, x.PlanRevision,
        x.BriefingId, x.BriefingRevision, x.ReadinessDecisionId, x.State, x.VerificationState, x.Revision, x.Version, x.Sequence, x.Hash, x.StartedAt, x.StartedBy,
        x.StartPosition, x.EndedAt, x.EndedBy, x.EndPosition, x.VerifiedBy, x.VerifiedAt, x.Provenance);

    internal static async Task<TowageLedgerEvent[]> LedgerEvents(PoseidonDbContext db, Guid execution, CancellationToken ct) {
        var rows = await db.TowageExecutionEvents.AsNoTracking().Where(e => e.ExecutionId == execution).OrderBy(e => e.Sequence).Take(TowageExecutionPolicy.MaxEvents)
            .Select(e => new { e.Sequence, e.Kind, e.OccurredAt, e.PayloadJson }).ToArrayAsync(ct);
        return rows.Select(r => { var p = TowageExecutionPolicy.Payload(r.PayloadJson); return new TowageLedgerEvent(r.Sequence, r.Kind, r.OccurredAt, p.Value, p.Unit, p.Result); }).ToArray();
    }

    private static async Task<object?> ReadView(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, CancellationToken ct) {
        var x = await db.TowageExecutions.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id && e.VesselId == vessel, ct);
        if (x == null || !await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == x.TugId, ct)) return null;
        var job = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == x.JobId && j.VesselId == vessel, ct);
        var rows = await db.TowageExecutionEvents.AsNoTracking().Where(e => e.ExecutionId == x.Id).OrderBy(e => e.Sequence).Take(TowageExecutionPolicy.MaxEvents).ToArrayAsync(ct);
        var timeline = rows.Select(e => { var p = TowageExecutionPolicy.Payload(e.PayloadJson);
            return new TowageExecutionEventDto(e.Sequence, e.Kind, e.OccurredAt, e.RecordedAt, e.ActorId, e.Duty, p.Value, p.Unit, p.Result, p.Text, p.Reference, p.Weather, p.Reason, p.Position, e.Hash); }).ToArray();
        var now = TugReadinessHttp.Now(h);
        var ledger = TowageExecutionPolicy.Ledger(rows.Select(e => { var p = TowageExecutionPolicy.Payload(e.PayloadJson); return new TowageLedgerEvent(e.Sequence, e.Kind, e.OccurredAt, p.Value, p.Unit, p.Result); }).ToArray(), now);
        var grants = TugOperationsAuthorities.Resolve(h, a, x.TugId, TugOperationsAuthorities.MasterDuties);
        var running = x.State is TowageExecutionPolicy.Active or TowageExecutionPolicy.Suspended;
        var write = TugReadinessHttp.Write(a, x.TugId);
        var master = grants.Length > 0 && write;
        string[] actions = !master ? [] : x.State switch {
            TowageExecutionPolicy.Active => ["SUSPEND", "ABORT", "COMPLETE"], TowageExecutionPolicy.Suspended => ["RESUME", "ABORT"], _ => [] };
        return new TowageExecutionView(Dto(x), job?.JobNumber, job?.AssistedVessel, ledger, timeline, TugOperationsAuthorities.Dto(grants),
            write && x.VerificationState == TowageExecutionPolicy.Pending, master && running,
            CanVerify(a) && !running && x.VerificationState == TowageExecutionPolicy.Pending && a.Id != x.StartedBy && a.Id != x.EndedBy && FieldOpsCommands.Human(h, a),
            actions, running ? TowageExecutionPolicy.CompleteBlockers("-", ledger).Where(b => b != "end_position_missing").ToArray() : [],
            running ? TowageExecutionPolicy.RecordableKinds : TowageExecutionPolicy.TerminalKinds, TowageExecutionPolicy.LoadUnits, TowageExecutionPolicy.CommunicationStates,
            TowageExecutionPolicy.WinchStates, TowageExecutionPolicy.ReleaseTestResults, TowageExecutionPolicy.EvidencePurposes);
    }

    private sealed record Gate(TowageStartFacts Facts, TugAssignmentProposal? Assignment, TugReadinessDecision? Decision, TowageSnapshot Snapshot, TugJob Job, TowageExecution? Existing);

    // Current start facts for one tug on one job, read through the owning gates at server time.
    private static async Task<Gate> StartGate(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, TugJob job, Guid tug, bool assess, CancellationToken ct) {
        var s = await TowageReader.Read(db, a, vessel, job.Id, ct);
        var existing = await db.TowageExecutions.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.JobId == job.Id && x.TugId == tug, ct);
        var p = await db.TugAssignmentProposals.AsNoTracking().Where(x => x.VesselId == vessel && x.JobId == job.Id && x.TugId == tug && x.State == TugAssignmentWorkflow.Proposed)
            .OrderByDescending(x => x.ProposedAt).FirstOrDefaultAsync(ct);
        var d = p == null ? null : await db.TugReadinessDecisions.AsNoTracking().Where(x => x.VesselId == vessel && x.AssignmentId == p.Id)
            .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var hashCurrent = false;
        if (assess && p != null && d?.Decision == TugReadinessDecisionPolicy.Go)
            hashCurrent = (await TugReadinessAssessor.Assess(h, db, a, p, job, TugReadinessHttp.Now(h), ct)).Outcome.Hash == d.AssessmentHash;
        var plan = s.Plan;
        var facts = new TowageStartFacts(job.Status, existing != null, plan != null,
            plan != null && plan.State == TowagePlanPolicy.Approved && plan.ApprovedRevision == plan.Revision, plan != null && plan.JobRevision == job.Revision,
            plan != null && plan.Content.Tugs.Any(t => t.TugId == tug), s.Briefing == null ? [] : TowageBriefingPolicy.Checks(s.Briefing, s.Plan, s.Risk, s.RiskReadable),
            p != null, d?.Decision, d != null && p != null && d.AssignmentRevision == p.Revision, hashCurrent);
        return new(facts, p, d, s, job, existing);
    }

    private static async Task<TowageOutcome> Start(TowageContext<TowageExecutionStartCommand> c) {
        var cmd = c.Command; var db = c.Db;
        if (cmd.Id == Guid.Empty || cmd.JobId == Guid.Empty || cmd.TugId == Guid.Empty || cmd.StartPosition != null && !TowageExecutionPolicy.Short(cmd.StartPosition, 400))
            return TowageOutcome.Fail(400, "validation_failed");
        // Onboard authority first: only the appointed Tug Master/Master of the executing tug may start.
        var grant = TugOperationsAuthorities.Resolve(c.Http, c.Actor, cmd.TugId, TugOperationsAuthorities.MasterDuties).SingleOrDefault(g => g.Id == cmd.AuthorityId);
        if (grant == null || !TugReadinessHttp.Write(c.Actor, cmd.TugId) || !await TugReadinessHttp.Tugs(db, c.Actor).AnyAsync(v => v.Id == cmd.TugId, c.Ct))
            return TowageOutcome.Fail(403, "tug_authority_required");
        var job = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == cmd.JobId && j.VesselId == c.Vessel, c.Ct);
        if (job == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (await db.TowageExecutions.AnyAsync(x => x.Id == cmd.Id, c.Ct)) return TowageOutcome.Fail(409, "version_conflict");
        var gate = await StartGate(c.Http, db, c.Actor, c.Vessel, job, cmd.TugId, true, c.Ct);
        var blockers = TowageExecutionPolicy.StartBlockers(gate.Facts);
        if (blockers.Length > 0) return TowageOutcome.Fail(422, "towage_start_blocked", blockers);
        if (gate.Decision!.Id != cmd.ReadinessDecisionId) return TowageOutcome.Fail(409, "version_conflict");
        var plan = gate.Snapshot.Plan!; var briefing = gate.Snapshot.Briefing!;
        var x = new TowageExecution { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, JobId = job.Id, JobRevision = job.Revision, TugId = cmd.TugId,
            AssignmentId = gate.Assignment!.Id, AssignmentRevision = gate.Assignment.Revision, PlanId = plan.Id, PlanRevision = plan.Revision, BriefingId = briefing.Id,
            BriefingRevision = briefing.Revision, ReadinessDecisionId = gate.Decision.Id, StartedAt = c.Now, StartedBy = c.Actor.Id, StartAuthorityId = grant.Id,
            StartPosition = cmd.StartPosition, ActorId = c.Actor.Id, SavedAt = c.Now };
        Append(db, x, "STARTED", c.Now, c.Now, c.Actor.Id, grant, TowageExecutionPolicy.PayloadJson(null, position: cmd.StartPosition));
        db.TowageExecutions.Add(x);
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(x.Id, x.Revision);
    }

    // Appends one ledger entry and advances the head by exactly one revision.
    private static void Append(PoseidonDbContext db, TowageExecution x, string kind, DateTimeOffset occurred, DateTimeOffset now, Guid actor, TugOperationsAuthority? grant, string payload) {
        var previous = x.LastEventHash; var sequence = x.Sequence + 1;
        var hash = TowageExecutionPolicy.EventHash(previous, x.Id, sequence, kind, occurred, actor, payload);
        db.TowageExecutionEvents.Add(new() { TenantId = x.TenantId, VesselId = x.VesselId, ExecutionId = x.Id, Sequence = sequence, Kind = kind, OccurredAt = occurred,
            RecordedAt = now, ActorId = actor, AuthorityId = grant?.Id, Duty = grant?.Duty, PayloadJson = payload, PreviousHash = previous, Hash = hash });
        if (x.Sequence > 0) x.Revision = checked(x.Revision + 1);
        x.Sequence = sequence; x.LastEventHash = hash; x.ActorId = actor; x.SavedAt = now;
        x.Hash = TowageExecutionPolicy.HeadHash(x.VesselId, x.Id, x.Revision, x.State, x.VerificationState, x.Sequence, x.LastEventHash);
    }

    private static async Task<TowageExecution?> Load(TowageContext<TowageExecutionTransitionCommand> c) => await c.Db.TowageExecutions.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);

    private static async Task<TowageOutcome> Transition(TowageContext<TowageExecutionTransitionCommand> c) {
        var cmd = c.Command; var db = c.Db;
        var x = await Load(c);
        if (x == null || !await TugReadinessHttp.Tugs(db, c.Actor).AnyAsync(v => v.Id == x.TugId, c.Ct)) return TowageOutcome.Fail(404, "resource_not_found");
        var grant = TugOperationsAuthorities.Resolve(c.Http, c.Actor, x.TugId, TugOperationsAuthorities.MasterDuties).SingleOrDefault(g => g.Id == cmd.AuthorityId);
        if (grant == null || !TugReadinessHttp.Write(c.Actor, x.TugId)) return TowageOutcome.Fail(403, "tug_authority_required");
        if (cmd.EndPosition != null && !TowageExecutionPolicy.Short(cmd.EndPosition, 400)) return TowageOutcome.Fail(400, "validation_failed");
        var (to, failure) = TowageExecutionPolicy.Transition(x.State, cmd.Action, cmd.Reason);
        if (failure != null) return TowageOutcome.Fail(failure switch { "invalid_state" => 409, _ => 400 }, failure);
        // ABORT is never refused for a stale version: the Tug Master's stop decision is recorded on the current head.
        if (cmd.Action != "ABORT" && x.Version != cmd.ExpectedVersion) return TowageOutcome.Fail(409, "version_conflict");
        var events = await LedgerEvents(db, x.Id, c.Ct);
        var last = events.Where(e => TowageExecutionPolicy.LifecycleKinds.Contains(e.Kind)).Select(e => e.OccurredAt).DefaultIfEmpty(x.StartedAt).Max();
        if (!TowageExecutionPolicy.Chronological(cmd.OccurredAt, last, c.Now)) return TowageOutcome.Fail(400, "validation_failed");
        if (cmd.Action == "COMPLETE") {
            var blockers = TowageExecutionPolicy.CompleteBlockers(cmd.EndPosition, TowageExecutionPolicy.Ledger(events, c.Now));
            if (blockers.Length > 0) return TowageOutcome.Fail(422, "towage_record_incomplete", blockers);
        }
        if (cmd.Action == "RESUME") {
            // 60.02 p242: resuming re-checks the pinned approved plan and issued briefing.
            var plan = await db.TowagePlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == x.PlanId && p.VesselId == x.VesselId, c.Ct);
            var briefing = await db.TowageBriefings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == x.BriefingId && b.VesselId == x.VesselId, c.Ct);
            var missing = new List<string>();
            if (plan == null || plan.State != TowagePlanPolicy.Approved || plan.Revision != x.PlanRevision) missing.Add("towage_plan_changed");
            if (briefing == null || briefing.State != TowageBriefingPolicy.Issued || briefing.Revision != x.BriefingRevision) missing.Add("briefing_changed");
            if (missing.Count > 0) return TowageOutcome.Fail(422, "towage_resume_blocked", [.. missing]);
        }
        x.State = to!;
        if (to is TowageExecutionPolicy.Completed or TowageExecutionPolicy.Aborted) { x.EndedAt = cmd.OccurredAt; x.EndedBy = c.Actor.Id; x.EndPosition = cmd.EndPosition; }
        Append(db, x, TowageExecutionPolicy.LifecycleKind(cmd.Action), cmd.OccurredAt, c.Now, c.Actor.Id, grant, TowageExecutionPolicy.PayloadJson(null, cmd.Reason, cmd.EndPosition));
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(x.Id, x.Revision);
    }

    // Crew on the executing tug record observations; the ledger is append-only and hash-chained.
    private static async Task<TowageOutcome> Record(TowageContext<TowageExecutionEventCommand> c) {
        var e = c.Command.Event; var db = c.Db;
        if (!TowageExecutionPolicy.ValidEvent(e)) return TowageOutcome.Fail(400, "validation_failed");
        var x = await db.TowageExecutions.SingleOrDefaultAsync(r => r.Id == c.RouteId && r.VesselId == c.Vessel, c.Ct);
        if (x == null || !await TugReadinessHttp.Tugs(db, c.Actor).AnyAsync(v => v.Id == x.TugId, c.Ct)) return TowageOutcome.Fail(404, "resource_not_found");
        if (!TugReadinessHttp.Write(c.Actor, x.TugId)) return TowageOutcome.Fail(403, "permission_denied");
        var running = x.State is TowageExecutionPolicy.Active or TowageExecutionPolicy.Suspended;
        if (!running && (!TowageExecutionPolicy.TerminalKinds.Contains(e.Kind) || x.VerificationState != TowageExecutionPolicy.Pending)) return TowageOutcome.Fail(409, "invalid_state");
        if (x.Sequence >= TowageExecutionPolicy.MaxEvents) return TowageOutcome.Fail(409, "invalid_state");
        if (!TowageExecutionPolicy.Chronological(e.OccurredAt, x.StartedAt, c.Now)) return TowageOutcome.Fail(400, "validation_failed");
        var ledger = TowageExecutionPolicy.Ledger(await LedgerEvents(db, x.Id, c.Ct), c.Now);
        if (e.Kind == "TOWLINE_CONNECTED" && ledger.TowlineConnected || e.Kind == "TOWLINE_RELEASED" && !ledger.TowlineConnected) return TowageOutcome.Fail(409, "invalid_state");
        Append(db, x, e.Kind, e.OccurredAt, c.Now, c.Actor.Id, null, TowageExecutionPolicy.PayloadJson(e));
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(x.Id, x.Revision);
    }

    private static async Task<TowageOutcome> Verify(TowageContext<TowageExecutionVerifyCommand> c) {
        var cmd = c.Command; var db = c.Db;
        if (!FieldOpsCommands.Human(c.Http, c.Actor)) return TowageOutcome.Fail(403, "human_actor_required");
        var x = await db.TowageExecutions.SingleOrDefaultAsync(r => r.Id == c.RouteId && r.VesselId == c.Vessel, c.Ct);
        if (x == null || !await TugReadinessHttp.Tugs(db, c.Actor).AnyAsync(v => v.Id == x.TugId, c.Ct)) return TowageOutcome.Fail(404, "resource_not_found");
        if (x.Version != cmd.ExpectedVersion || x.Hash != cmd.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        var failure = TowageExecutionPolicy.VerificationFailure(x.State, x.VerificationState, c.Actor.Id, x.StartedBy, x.EndedBy, cmd.Decision, cmd.Reason);
        if (failure != null) return TowageOutcome.Fail(failure switch { "invalid_state" => 409, "separation_of_duties" => 403, _ => 400 }, failure);
        x.VerificationState = cmd.Decision; x.VerifiedBy = c.Actor.Id; x.VerifiedAt = c.Now;
        Append(db, x, cmd.Decision == TowageExecutionPolicy.Verified ? "VERIFIED" : "VERIFICATION_REJECTED", c.Now, c.Now, c.Actor.Id, null, TowageExecutionPolicy.PayloadJson(null, cmd.Reason));
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(x.Id, x.Revision);
    }

    private static async Task<IResult> JobView(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TowageCommands.Fail(h, Read, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel", "job"], [], out var q)) return await TowageCommands.Fail(h, Read, 400, "invalid_request");
        var ct = h.RequestAborted; var vessel = q["vessel"];
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TowageCommands.Fail(h, Read, 404, "resource_not_found");
        var job = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == q["job"] && j.VesselId == vessel, ct);
        if (job == null) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", q["job"]);
        var visible = TugReadinessHttp.Tugs(db, a).Select(v => v.Id);
        var tugs = await db.TugAssignmentProposals.AsNoTracking().Where(p => p.VesselId == vessel && p.JobId == job.Id && p.State == TugAssignmentWorkflow.Proposed && visible.Contains(p.TugId))
            .Select(p => p.TugId).Distinct().OrderBy(x => x).Take(12).ToArrayAsync(ct);
        var executions = await db.TowageExecutions.AsNoTracking().Where(x => x.VesselId == vessel && x.JobId == job.Id && visible.Contains(x.TugId)).OrderBy(x => x.StartedAt).ToArrayAsync(ct);
        var gates = new List<TowageStartGateDto>();
        foreach (var tug in tugs.Union(executions.Select(x => x.TugId)).Distinct()) {
            var grants = TugOperationsAuthorities.Resolve(h, a, tug, TugOperationsAuthorities.MasterDuties);
            var gate = await StartGate(h, db, a, vessel, job, tug, grants.Length > 0, ct);
            var blockers = TowageExecutionPolicy.StartBlockers(gate.Facts);
            // Without an appointment the readiness hash is not re-assessed for this reader; the gate says so.
            if (grants.Length == 0 && gate.Facts.Decision == TugReadinessDecisionPolicy.Go) blockers = [.. blockers.Where(b => b != "readiness_go_stale"), "tug_authority_required"];
            gates.Add(new(tug, gate.Assignment?.Id, gate.Decision?.Id, gate.Decision?.Decision, [.. blockers.Distinct().Order(StringComparer.Ordinal)], TugOperationsAuthorities.Dto(grants),
                grants.Length > 0 && TugReadinessHttp.Write(a, tug) && blockers.Length == 0, gate.Existing?.Id, gate.Existing?.State));
        }
        await TugReadinessHttp.Observe(h, Read, "Success", job.Id);
        return Results.Json(new TowageExecutionJobView(vessel, job.Id, job.JobNumber, job.Status, [.. gates], executions.Select(Dto).ToArray()), FieldOpsJson.Options);
    }

    private static async Task<IResult> ExecutionView(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TowageCommands.Fail(h, Read, 403, "permission_denied", id);
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TowageCommands.Fail(h, Read, 400, "invalid_request", id);
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == q["vessel"], h.RequestAborted)) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", id);
        var view = await ReadView(h, db, a, q["vessel"], id, h.RequestAborted);
        if (view == null) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", id);
        await TugReadinessHttp.Observe(h, Read, "Success", id);
        return Results.Json(view, FieldOpsJson.Options);
    }
}
