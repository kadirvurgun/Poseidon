using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowagePlanCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] Guid JobId, [property: JsonRequired] TowagePlanContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowagePlanUpdateCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] TowagePlanContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageSubmitCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowagePlanDecisionCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason, [property: JsonRequired] Guid AuthorityId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageAmendCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageBriefingCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] Guid JobId, [property: JsonRequired] TowageBriefingContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageBriefingUpdateCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] TowageBriefingContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageBriefingIssueCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash, [property: JsonRequired] Guid AuthorityId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageBriefingAcknowledgeCommand([property: JsonRequired] long Revision, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Choice, string? Reason = null);

public sealed record TowageDecisionDto(Guid Id, long Revision, long ResultRevision, string Decision, string Duty, Guid TugId, Guid ActorId, string Reason, DateTimeOffset DecidedAt);
public sealed record TowageRevisionDto(long Revision, string State, string ReasonCode, string Hash, Guid ActorId, DateTimeOffset SavedAt);
public sealed record TowagePlanDto(Guid Id, Guid VesselId, Guid JobId, long JobRevision, long Revision, uint Version, string State, TowagePlanContent Content,
    string Hash, long? ApprovedRevision, Guid? SubmittedBy, string Provenance, DateTimeOffset SavedAt);
public sealed record TowagePlanView(Guid VesselId, Guid JobId, long JobRevision, string RequestState, TowagePlanDto? Plan, string[] Blockers, Guid[] AssignedTugs,
    TowageDecisionDto[] Decisions, TowageRevisionDto[] Revisions, TugAuthorityDto[] Authorities, bool CanEdit, bool CanSubmit, bool CanDecide, bool CanAmend,
    string[] Positions, string[] Methods, string[] LineSources, Dictionary<string, string> LimitUnits, string[] Scenarios,
    string ApprovalAuthority = "MasterOrTugMasterHumanRecord;AuthorNeverApprover;NotAGoDecision", string SourceVersion = TowagePlanPolicy.SourceVersion);
public sealed record TowageAcknowledgementDto(Guid ActorId, long Revision, string Choice, string? Reason, DateTimeOffset RecordedAt);
public sealed record TowageRiskStateDto(Guid? AssessmentId, string State, string Reason, long? Revision, long? ApprovedRevision, string? AcknowledgementState);
public sealed record TowageBriefingDto(Guid Id, Guid VesselId, Guid JobId, long Revision, uint Version, string State, TowageBriefingContent Content, string Hash,
    long? PlanRevision, long? RiskRevision, string Provenance, DateTimeOffset SavedAt);
public sealed record TowageBriefingView(Guid VesselId, Guid JobId, TowageBriefingDto? Briefing, string[] Blockers, string AcknowledgementState,
    TowageAcknowledgementDto[] Acknowledgements, TowageRiskStateDto Risk, string? PlanState, long? PlanRevision, TugReadinessCheck[] ReadinessChecks,
    TowageRevisionDto[] Revisions, TugAuthorityDto[] Authorities, bool CanEdit, bool CanIssue, bool CanAmend, bool CanAcknowledge, string[] Roles,
    string ReadinessEffect = "IncompleteBriefingIsFailAndBlocksReady", string SourceVersion = TowageBriefingPolicy.SourceVersion);

// DEV-087 /api/v1/towage-plans and DEV-088 /api/v1/towage-risk-briefings. Plans and briefings are preparation
// records; the Master/Tug Master plan approval and the briefing issue are human records from a
// server-provisioned TugOperationsAuthority on the coordinating tug. Neither is a GO, a permit or a release;
// the DEV-084 engine only consumes them as communication/briefing inputs.
public static class TowageEndpoints
{
    private const string PlanRead = "TowagePlanRead", PlanCommand = "TowagePlanCommand", BriefingRead = "TowageBriefingRead", BriefingCommand = "TowageBriefingCommand";

    internal static bool PlanWrite(TrustedActor a, Guid vessel) => TugReadinessHttp.Write(a, vessel) && a.Permissions.Contains("tug.plan.write");
    internal static bool BriefingWrite(TrustedActor a, Guid vessel) => TugReadinessHttp.Write(a, vessel);

    public static void MapTowage(this IEndpointRouteBuilder app) {
        var p = app.MapGroup("/api/v1/towage-plans").RequireAuthorization("DirectoryRead");
        p.MapGet("", PlanView).WithName("TowagePlanRead").Produces<TowagePlanView>();
        p.MapPost("", (HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowagePlanCreateCommand>(h, db, "TowagePlanCreate", PlanCommand, null,
            PlanWrite, c => c.Id, CreatePlan, ReadPlan)).WithName("TowagePlanCreate").Accepts<TowagePlanCreateCommand>("application/json").Produces<TowagePlanDto>();
        p.MapPut("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowagePlanUpdateCommand>(h, db, "TowagePlanUpdate", PlanCommand, id,
            PlanWrite, _ => id, UpdatePlan, ReadPlan)).WithName("TowagePlanUpdate").Accepts<TowagePlanUpdateCommand>("application/json").Produces<TowagePlanDto>();
        p.MapPost("/{id:guid}/submit", (Guid id, HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowageSubmitCommand>(h, db, "TowagePlanSubmit", PlanCommand, id,
            PlanWrite, _ => id, SubmitPlan, ReadPlan)).WithName("TowagePlanSubmit").Accepts<TowageSubmitCommand>("application/json").Produces<TowagePlanDto>().Produces<RiskFailure>(422);
        p.MapPost("/{id:guid}/decisions", (Guid id, HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowagePlanDecisionCommand>(h, db, "TowagePlanDecide", PlanCommand, id,
            (_, _) => true, _ => id, DecidePlan, ReadPlan)).WithName("TowagePlanDecide").Accepts<TowagePlanDecisionCommand>("application/json").Produces<TowagePlanDto>();
        p.MapPost("/{id:guid}/amend", (Guid id, HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowageAmendCommand>(h, db, "TowagePlanAmend", PlanCommand, id,
            PlanWrite, _ => id, AmendPlan, ReadPlan)).WithName("TowagePlanAmend").Accepts<TowageAmendCommand>("application/json").Produces<TowagePlanDto>();

        var b = app.MapGroup("/api/v1/towage-risk-briefings").RequireAuthorization("DirectoryRead");
        b.MapGet("", BriefingView).WithName("TowageBriefingRead").Produces<TowageBriefingView>();
        b.MapPost("", (HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowageBriefingCreateCommand>(h, db, "TowageBriefingCreate", BriefingCommand, null,
            BriefingWrite, c => c.Id, CreateBriefing, ReadBriefing)).WithName("TowageBriefingCreate").Accepts<TowageBriefingCreateCommand>("application/json").Produces<TowageBriefingDto>();
        b.MapPut("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowageBriefingUpdateCommand>(h, db, "TowageBriefingUpdate", BriefingCommand, id,
            BriefingWrite, _ => id, UpdateBriefing, ReadBriefing)).WithName("TowageBriefingUpdate").Accepts<TowageBriefingUpdateCommand>("application/json").Produces<TowageBriefingDto>();
        b.MapPost("/{id:guid}/issue", (Guid id, HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowageBriefingIssueCommand>(h, db, "TowageBriefingIssue", BriefingCommand, id,
            (_, _) => true, _ => id, IssueBriefing, ReadBriefing)).WithName("TowageBriefingIssue").Accepts<TowageBriefingIssueCommand>("application/json").Produces<TowageBriefingDto>().Produces<RiskFailure>(422);
        b.MapPost("/{id:guid}/amend", (Guid id, HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowageAmendCommand>(h, db, "TowageBriefingAmend", BriefingCommand, id,
            BriefingWrite, _ => id, AmendBriefing, ReadBriefing)).WithName("TowageBriefingAmend").Accepts<TowageAmendCommand>("application/json").Produces<TowageBriefingDto>();
        b.MapPost("/{id:guid}/acknowledgements", (Guid id, HttpContext h, PoseidonDbContext db) => TowageCommands.Run<TowageBriefingAcknowledgeCommand>(h, db, "TowageBriefingAcknowledge", BriefingCommand, id,
            (_, _) => true, _ => id, AcknowledgeBriefing, ReadBriefing)).WithName("TowageBriefingAcknowledge").Accepts<TowageBriefingAcknowledgeCommand>("application/json").Produces<TowageBriefingDto>();
    }

    private static TowagePlanDto Dto(TowagePlan p) => new(p.Id, p.VesselId, p.JobId, p.JobRevision, p.Revision, p.Version, p.State, TowageReader.PlanContent(p.ContentJson),
        p.Hash, p.ApprovedRevision, p.SubmittedBy, p.Provenance, p.SavedAt);
    private static TowageBriefingDto Dto(TowageBriefing b) => new(b.Id, b.VesselId, b.JobId, b.Revision, b.Version, b.State, TowageReader.BriefingContent(b.ContentJson),
        b.Hash, b.PlanRevision, b.RiskRevision, b.Provenance, b.SavedAt);

    private static async Task<object?> ReadPlan(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, CancellationToken ct) {
        var p = await db.TowagePlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        return p == null ? null : Dto(p);
    }
    private static async Task<object?> ReadBriefing(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, CancellationToken ct) {
        var b = await db.TowageBriefings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        return b == null ? null : Dto(b);
    }

    private static async Task<TugJob?> Job(PoseidonDbContext db, Guid vessel, Guid job, CancellationToken ct) =>
        await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == job && j.VesselId == vessel, ct);

    // Tugs that currently hold an active DEV-083 proposal for this job.
    private static async Task<Guid[]> AssignedTugs(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid job, CancellationToken ct) {
        var visible = TugReadinessHttp.Tugs(db, a).Select(v => v.Id);
        return await db.TugAssignmentProposals.AsNoTracking().Where(p => p.VesselId == vessel && p.JobId == job && p.State == TugAssignmentWorkflow.Proposed && visible.Contains(p.TugId))
            .Select(p => p.TugId).Distinct().OrderBy(x => x).ToArrayAsync(ct);
    }

    // Every tug and equipment link must be visible and belong to that tug with the right kind.
    private static async Task<bool> LinksValid(PoseidonDbContext db, TrustedActor a, TowagePlanContent c, CancellationToken ct) {
        var tugs = c.Tugs.Select(t => t.TugId).ToArray();
        if (await TugReadinessHttp.Tugs(db, a).CountAsync(v => tugs.Contains(v.Id), ct) != tugs.Length) return false;
        foreach (var t in c.Tugs) {
            if (t.TowlineItemId is Guid line && !await db.TowEquipmentItems.AnyAsync(x => x.Id == line && x.VesselId == t.TugId && x.Kind == "Towline", ct)) return false;
            if (t.EmergencyReleaseItemId is Guid release && !await db.TowEquipmentItems.AnyAsync(x => x.Id == release && x.VesselId == t.TugId && x.Kind == "EmergencyRelease", ct)) return false;
        }
        return true;
    }

    private static void Stamp(TowagePlan p, Guid actor, DateTimeOffset now) {
        p.ActorId = actor; p.SavedAt = now;
        p.Hash = TowagePlanPolicy.Hash("towage-plan", p.VesselId, p.Id, p.Revision, p.State, p.JobRevision, p.ContentJson);
    }
    private static void Append(PoseidonDbContext db, TowagePlan p, string reason) => db.TowagePlanRevisions.Add(new() { TenantId = p.TenantId, VesselId = p.VesselId,
        PlanId = p.Id, Revision = p.Revision, JobRevision = p.JobRevision, State = p.State, ReasonCode = reason, ContentJson = p.ContentJson, Hash = p.Hash,
        ActorId = p.ActorId, SavedAt = p.SavedAt, Provenance = p.Provenance });

    private static async Task<TowageOutcome> CreatePlan(TowageContext<TowagePlanCreateCommand> c) {
        var cmd = c.Command; var db = c.Db;
        if (cmd.Id == Guid.Empty || cmd.JobId == Guid.Empty || !TowagePlanPolicy.ValidDraft(cmd.Content)) return TowageOutcome.Fail(400, "validation_failed");
        var job = await Job(db, c.Vessel, cmd.JobId, c.Ct);
        if (job == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (job.Status == TugJobLifecycle.Cancelled) return TowageOutcome.Fail(409, "invalid_state");
        if (await db.TowagePlans.AnyAsync(x => x.Id == cmd.Id || x.VesselId == c.Vessel && x.JobId == cmd.JobId, c.Ct)) return TowageOutcome.Fail(409, "version_conflict");
        if (!await LinksValid(db, c.Actor, cmd.Content, c.Ct)) return TowageOutcome.Fail(422, "linked_resource_unavailable");
        var p = new TowagePlan { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, JobId = job.Id, JobRevision = job.Revision,
            ContentJson = TowageBriefingPolicy.ContentJson(cmd.Content), AuthorsJson = TowageReader.WithAuthor("[]", c.Actor.Id) };
        Stamp(p, c.Actor.Id, c.Now); db.TowagePlans.Add(p); Append(db, p, "CREATE");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static async Task<TowageOutcome> UpdatePlan(TowageContext<TowagePlanUpdateCommand> c) {
        var db = c.Db;
        if (!TowagePlanPolicy.ValidDraft(c.Command.Content)) return TowageOutcome.Fail(400, "validation_failed");
        var p = await db.TowagePlans.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (p == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (p.Version != c.Command.ExpectedVersion) return TowageOutcome.Fail(409, "version_conflict");
        if (p.State is not (TowagePlanPolicy.Draft or TowagePlanPolicy.Returned)) return TowageOutcome.Fail(409, "invalid_state");
        var job = await Job(db, c.Vessel, p.JobId, c.Ct);
        if (job == null || job.Status == TugJobLifecycle.Cancelled) return TowageOutcome.Fail(409, "invalid_state");
        if (!await LinksValid(db, c.Actor, c.Command.Content, c.Ct)) return TowageOutcome.Fail(422, "linked_resource_unavailable");
        p.Revision = checked(p.Revision + 1); p.State = TowagePlanPolicy.Draft; p.JobRevision = job.Revision;
        p.ContentJson = TowageBriefingPolicy.ContentJson(c.Command.Content); p.AuthorsJson = TowageReader.WithAuthor(p.AuthorsJson, c.Actor.Id);
        Stamp(p, c.Actor.Id, c.Now); Append(db, p, "EDIT");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static async Task<TowageOutcome> SubmitPlan(TowageContext<TowageSubmitCommand> c) {
        var db = c.Db;
        var p = await db.TowagePlans.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (p == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (p.Version != c.Command.ExpectedVersion || p.Hash != c.Command.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        var job = await Job(db, c.Vessel, p.JobId, c.Ct);
        if (job == null || job.Status == TugJobLifecycle.Cancelled) return TowageOutcome.Fail(409, "invalid_state");
        if (job.Revision != p.JobRevision) return TowageOutcome.Fail(409, "version_conflict");
        var content = TowageReader.PlanContent(p.ContentJson);
        var transition = TowagePlanPolicy.Submit(p.State, TowagePlanPolicy.SubmitBlockers(content, await AssignedTugs(db, c.Actor, c.Vessel, p.JobId, c.Ct)));
        if (transition.Failure != null) return TowageOutcome.Fail(transition.Failure == "invalid_state" ? 409 : 422, transition.Failure, transition.Blockers.Length > 0 ? transition.Blockers : null);
        p.Revision = checked(p.Revision + 1); p.State = transition.ToState!; p.SubmittedBy = c.Actor.Id;
        p.AuthorsJson = TowageReader.WithAuthor(p.AuthorsJson, c.Actor.Id);
        Stamp(p, c.Actor.Id, c.Now); Append(db, p, "SUBMIT");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static async Task<TowageOutcome> DecidePlan(TowageContext<TowagePlanDecisionCommand> c) {
        var db = c.Db; var cmd = c.Command;
        var p = await db.TowagePlans.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (p == null) return TowageOutcome.Fail(404, "resource_not_found");
        var content = TowageReader.PlanContent(p.ContentJson);
        var coordinating = content.Tugs.Where(t => t.Coordinating).Select(t => (Guid?)t.TugId).SingleOrDefault();
        var grant = coordinating == null ? null : TugOperationsAuthorities.Resolve(c.Http, c.Actor, coordinating.Value, TugOperationsAuthorities.MasterDuties).SingleOrDefault(g => g.Id == cmd.AuthorityId);
        if (grant == null || !TugReadinessHttp.Write(c.Actor, coordinating!.Value)) return TowageOutcome.Fail(403, "tug_authority_required");
        if (p.Version != cmd.ExpectedVersion || p.Hash != cmd.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        var job = await Job(db, c.Vessel, p.JobId, c.Ct);
        if (job == null || job.Revision != p.JobRevision) return TowageOutcome.Fail(409, "version_conflict");
        var transition = TowagePlanPolicy.Review(p.State, cmd.Decision, c.Actor.Id, TowageReader.Authors(p.AuthorsJson), p.SubmittedBy, cmd.Reason);
        if (transition.Failure != null) return TowageOutcome.Fail(transition.Failure switch { "invalid_state" => 409, "separation_of_duties" => 403, _ => 400 }, transition.Failure);
        var reviewed = p.Revision; var reviewedHash = p.Hash;
        p.Revision = checked(p.Revision + 1); p.State = transition.ToState!;
        p.ApprovedRevision = p.State == TowagePlanPolicy.Approved ? p.Revision : null;
        Stamp(p, c.Actor.Id, c.Now); Append(db, p, cmd.Decision == "APPROVE" ? "APPROVE" : "RETURN");
        db.TowagePlanDecisions.Add(new() { TenantId = p.TenantId, VesselId = p.VesselId, Id = Guid.NewGuid(), PlanId = p.Id, Revision = reviewed, ResultRevision = p.Revision,
            Decision = cmd.Decision, ContentHash = reviewedHash, TugId = coordinating.Value, ActorId = c.Actor.Id, AuthorityId = grant.Id, Duty = grant.Duty, Reason = cmd.Reason, DecidedAt = c.Now });
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static async Task<TowageOutcome> AmendPlan(TowageContext<TowageAmendCommand> c) {
        var db = c.Db;
        var p = await db.TowagePlans.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (p == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (p.Version != c.Command.ExpectedVersion) return TowageOutcome.Fail(409, "version_conflict");
        var transition = TowagePlanPolicy.Amend(p.State, c.Command.Reason);
        if (transition.Failure != null) return TowageOutcome.Fail(transition.Failure == "invalid_state" ? 409 : 400, transition.Failure);
        var job = await Job(db, c.Vessel, p.JobId, c.Ct);
        p.Revision = checked(p.Revision + 1); p.State = TowagePlanPolicy.Draft; p.ApprovedRevision = null; p.SubmittedBy = null;
        p.JobRevision = job?.Revision ?? p.JobRevision; p.AuthorsJson = TowageReader.WithAuthor("[]", c.Actor.Id);
        Stamp(p, c.Actor.Id, c.Now); Append(db, p, "AMEND");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static Guid? CoordinatingTug(TowagePlanFacts? plan) => plan?.Content.Tugs.Where(t => t.Coordinating).Select(t => (Guid?)t.TugId).SingleOrDefault();

    private static async Task<IResult> PlanView(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TowageCommands.Fail(h, PlanRead, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel", "job"], [], out var q)) return await TowageCommands.Fail(h, PlanRead, 400, "invalid_request");
        var ct = h.RequestAborted; var vessel = q["vessel"];
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TowageCommands.Fail(h, PlanRead, 404, "resource_not_found");
        var job = await Job(db, vessel, q["job"], ct);
        if (job == null) return await TowageCommands.Fail(h, PlanRead, 404, "resource_not_found", q["job"]);
        var p = await db.TowagePlans.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.JobId == job.Id, ct);
        var assigned = await AssignedTugs(db, a, vessel, job.Id, ct);
        var facts = p == null ? null : TowageReader.Facts(p);
        if (facts != null && !await LinksValid(db, a, facts.Content, ct)) return await TowageCommands.Fail(h, PlanRead, 403, "permission_denied", p!.Id);
        var decisions = p == null ? [] : await db.TowagePlanDecisions.AsNoTracking().Where(d => d.PlanId == p.Id).OrderByDescending(d => d.DecidedAt).Take(32)
            .Select(d => new TowageDecisionDto(d.Id, d.Revision, d.ResultRevision, d.Decision, d.Duty, d.TugId, d.ActorId, d.Reason, d.DecidedAt)).ToArrayAsync(ct);
        var revisions = p == null ? [] : await db.TowagePlanRevisions.AsNoTracking().Where(r => r.PlanId == p.Id).OrderByDescending(r => r.Revision).Take(50)
            .Select(r => new TowageRevisionDto(r.Revision, r.State, r.ReasonCode, r.Hash, r.ActorId, r.SavedAt)).ToArrayAsync(ct);
        var coordinating = CoordinatingTug(facts);
        var grants = coordinating == null ? [] : TugOperationsAuthorities.Resolve(h, a, coordinating.Value, TugOperationsAuthorities.MasterDuties);
        var write = PlanWrite(a, vessel) && job.Status != TugJobLifecycle.Cancelled;
        var authors = p == null ? [] : TowageReader.Authors(p.AuthorsJson);
        await TugReadinessHttp.Observe(h, PlanRead, "Success", p?.Id ?? job.Id);
        return Results.Ok(new TowagePlanView(vessel, job.Id, job.Revision, p == null ? "Missing" : p.JobRevision == job.Revision ? "CurrentRequestRevision" : "RequestChangedSincePlan",
            p == null ? null : Dto(p), facts == null ? [] : TowagePlanPolicy.SubmitBlockers(facts.Content, assigned), assigned, decisions, revisions, TugOperationsAuthorities.Dto(grants),
            write && (p == null || p.State is TowagePlanPolicy.Draft or TowagePlanPolicy.Returned),
            write && p != null && p.State is TowagePlanPolicy.Draft or TowagePlanPolicy.Returned,
            p != null && p.State == TowagePlanPolicy.InReview && grants.Length > 0 && !authors.Contains(a.Id) && p.SubmittedBy != a.Id && TugReadinessHttp.Write(a, coordinating!.Value),
            write && p != null && p.State is TowagePlanPolicy.Approved or TowagePlanPolicy.InReview,
            TowagePlanPolicy.Positions, TowagePlanPolicy.Methods, TowagePlanPolicy.LineSources, new(TowagePlanPolicy.LimitUnits), TowagePlanPolicy.Scenarios));
    }

    // ---- DEV-088 briefing ----

    private static void Stamp(TowageBriefing b, Guid actor, DateTimeOffset now) {
        b.ActorId = actor; b.SavedAt = now;
        b.Hash = TowagePlanPolicy.Hash("towage-briefing", b.VesselId, b.Id, b.Revision, b.State, b.PlanRevision ?? 0, b.ContentJson + "|" + (b.RiskRevision ?? 0));
    }
    private static void Append(PoseidonDbContext db, TowageBriefing b, string reason) => db.TowageBriefingRevisions.Add(new() { TenantId = b.TenantId, VesselId = b.VesselId,
        BriefingId = b.Id, Revision = b.Revision, State = b.State, ReasonCode = reason, ContentJson = b.ContentJson, Hash = b.Hash, PlanRevision = b.PlanRevision,
        RiskRevision = b.RiskRevision, ActorId = b.ActorId, SavedAt = b.SavedAt, Provenance = b.Provenance });

    // Participants' tugs must be visible; the risk assessment must be readable and belong to the job vessel or a planned tug.
    private static async Task<bool> BriefingLinksValid(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid job, TowageBriefingContent c, CancellationToken ct) {
        var tugs = c.Participants.Where(p => p.TugId != null).Select(p => p.TugId!.Value).Distinct().ToArray();
        if (await TugReadinessHttp.Tugs(db, a).CountAsync(v => tugs.Contains(v.Id), ct) != tugs.Length) return false;
        if (c.RiskAssessmentId is not Guid risk) return true;
        if (!RiskAssessmentEndpoints.CanRead(a)) return false;
        var plan = await db.TowagePlans.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.JobId == job, ct);
        var allowed = new List<Guid> { vessel };
        if (plan != null) allowed.AddRange(TowageReader.PlanContent(plan.ContentJson).Tugs.Select(t => t.TugId));
        var scoped = a.Scope(db.Vessels).Select(v => v.Id);
        return await db.RiskAssessments.AnyAsync(r => r.Id == risk && allowed.Contains(r.VesselId) && scoped.Contains(r.VesselId), ct);
    }

    private static async Task<TowageOutcome> CreateBriefing(TowageContext<TowageBriefingCreateCommand> c) {
        var cmd = c.Command; var db = c.Db;
        if (cmd.Id == Guid.Empty || cmd.JobId == Guid.Empty || !TowageBriefingPolicy.ValidDraft(cmd.Content)) return TowageOutcome.Fail(400, "validation_failed");
        var job = await Job(db, c.Vessel, cmd.JobId, c.Ct);
        if (job == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (job.Status == TugJobLifecycle.Cancelled) return TowageOutcome.Fail(409, "invalid_state");
        if (await db.TowageBriefings.AnyAsync(x => x.Id == cmd.Id || x.VesselId == c.Vessel && x.JobId == cmd.JobId, c.Ct)) return TowageOutcome.Fail(409, "version_conflict");
        if (!await BriefingLinksValid(db, c.Actor, c.Vessel, job.Id, cmd.Content, c.Ct)) return TowageOutcome.Fail(422, "linked_resource_unavailable");
        var b = new TowageBriefing { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, JobId = job.Id, ContentJson = TowageBriefingPolicy.ContentJson(cmd.Content) };
        Stamp(b, c.Actor.Id, c.Now); db.TowageBriefings.Add(b); Append(db, b, "CREATE");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(b.Id, b.Revision);
    }

    private static async Task<TowageOutcome> UpdateBriefing(TowageContext<TowageBriefingUpdateCommand> c) {
        var db = c.Db;
        if (!TowageBriefingPolicy.ValidDraft(c.Command.Content)) return TowageOutcome.Fail(400, "validation_failed");
        var b = await db.TowageBriefings.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (b == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (b.Version != c.Command.ExpectedVersion) return TowageOutcome.Fail(409, "version_conflict");
        if (b.State != TowageBriefingPolicy.Draft) return TowageOutcome.Fail(409, "invalid_state");
        if (!await BriefingLinksValid(db, c.Actor, c.Vessel, b.JobId, c.Command.Content, c.Ct)) return TowageOutcome.Fail(422, "linked_resource_unavailable");
        b.Revision = checked(b.Revision + 1); b.ContentJson = TowageBriefingPolicy.ContentJson(c.Command.Content);
        Stamp(b, c.Actor.Id, c.Now); Append(db, b, "EDIT");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(b.Id, b.Revision);
    }

    // The coordinating tug's appointed Tug Master/Master issues the briefing; it pins the approved plan and risk revisions.
    private static async Task<TowageOutcome> IssueBriefing(TowageContext<TowageBriefingIssueCommand> c) {
        var db = c.Db; var cmd = c.Command;
        var b = await db.TowageBriefings.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (b == null) return TowageOutcome.Fail(404, "resource_not_found");
        var snapshot = await TowageReader.Read(db, c.Actor, c.Vessel, b.JobId, c.Ct);
        var coordinating = CoordinatingTug(snapshot.Plan);
        var grant = coordinating == null ? null : TugOperationsAuthorities.Resolve(c.Http, c.Actor, coordinating.Value, TugOperationsAuthorities.MasterDuties).SingleOrDefault(g => g.Id == cmd.AuthorityId);
        if (coordinating == null) return TowageOutcome.Fail(422, "towage_plan_not_approved");
        if (grant == null || !TugReadinessHttp.Write(c.Actor, coordinating.Value)) return TowageOutcome.Fail(403, "tug_authority_required");
        if (b.Version != cmd.ExpectedVersion || b.Hash != cmd.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        if (b.State != TowageBriefingPolicy.Draft) return TowageOutcome.Fail(409, "invalid_state");
        if (!snapshot.RiskReadable) return TowageOutcome.Fail(403, "permission_denied");
        var content = TowageReader.BriefingContent(b.ContentJson);
        var blockers = TowageBriefingPolicy.IssueBlockers(content, snapshot.Plan, snapshot.Risk);
        if (blockers.Length > 0) return TowageOutcome.Fail(422, "towage_record_incomplete", blockers);
        b.Revision = checked(b.Revision + 1); b.State = TowageBriefingPolicy.Issued; b.PlanId = snapshot.Plan!.Id; b.PlanRevision = snapshot.Plan.Revision;
        b.RiskRevision = snapshot.Risk!.Revision; b.IssuedBy = c.Actor.Id; b.IssuedAuthorityId = grant.Id;
        Stamp(b, c.Actor.Id, c.Now); Append(db, b, "ISSUE");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(b.Id, b.Revision);
    }

    private static async Task<TowageOutcome> AmendBriefing(TowageContext<TowageAmendCommand> c) {
        var db = c.Db;
        var b = await db.TowageBriefings.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (b == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (b.Version != c.Command.ExpectedVersion) return TowageOutcome.Fail(409, "version_conflict");
        if (b.State != TowageBriefingPolicy.Issued) return TowageOutcome.Fail(409, "invalid_state");
        if (!TowagePlanPolicy.Reason(c.Command.Reason)) return TowageOutcome.Fail(400, "invalid_reason");
        // A re-briefing starts a new revision: earlier acknowledgements never count for it.
        b.Revision = checked(b.Revision + 1); b.State = TowageBriefingPolicy.Draft; b.PlanId = null; b.PlanRevision = null; b.RiskRevision = null;
        b.IssuedBy = null; b.IssuedAuthorityId = null;
        Stamp(b, c.Actor.Id, c.Now); Append(db, b, "AMEND");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(b.Id, b.Revision);
    }

    // Each participant acknowledges the exact issued revision for themselves (ACK or DISPUTE with reason).
    private static async Task<TowageOutcome> AcknowledgeBriefing(TowageContext<TowageBriefingAcknowledgeCommand> c) {
        var db = c.Db; var cmd = c.Command;
        var b = await db.TowageBriefings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (b == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (b.Hash != cmd.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        var failure = TowageBriefingPolicy.AcknowledgementFailure(b.State, b.Revision, cmd.Revision, TowageReader.BriefingContent(b.ContentJson), c.Actor.Id, cmd.Choice, cmd.Reason);
        if (failure != null) return TowageOutcome.Fail(failure switch { "invalid_state" => 409, "acknowledgment_role_required" => 403, _ => 400 }, failure);
        if (await db.TowageBriefingAcknowledgements.AnyAsync(k => k.BriefingId == b.Id && k.Revision == b.Revision && k.ActorId == c.Actor.Id, c.Ct))
            return TowageOutcome.Fail(409, "version_conflict");
        db.TowageBriefingAcknowledgements.Add(new() { TenantId = b.TenantId, VesselId = b.VesselId, Id = Guid.NewGuid(), BriefingId = b.Id, Revision = b.Revision,
            ActorId = c.Actor.Id, Choice = cmd.Choice, ContentHash = b.Hash, Reason = cmd.Reason, RecordedAt = c.Now });
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(b.Id, b.Revision);
    }

    private static async Task<IResult> BriefingView(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TowageCommands.Fail(h, BriefingRead, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel", "job"], [], out var q)) return await TowageCommands.Fail(h, BriefingRead, 400, "invalid_request");
        var ct = h.RequestAborted; var vessel = q["vessel"];
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TowageCommands.Fail(h, BriefingRead, 404, "resource_not_found");
        var job = await Job(db, vessel, q["job"], ct);
        if (job == null) return await TowageCommands.Fail(h, BriefingRead, 404, "resource_not_found", q["job"]);
        var s = await TowageReader.Read(db, a, vessel, job.Id, ct);
        var b = s.BriefingRow;
        var acks = b == null ? [] : await db.TowageBriefingAcknowledgements.AsNoTracking().Where(k => k.BriefingId == b.Id).OrderByDescending(k => k.RecordedAt).Take(100)
            .Select(k => new TowageAcknowledgementDto(k.ActorId, k.Revision, k.Choice, k.Reason, k.RecordedAt)).ToArrayAsync(ct);
        var revisions = b == null ? [] : await db.TowageBriefingRevisions.AsNoTracking().Where(r => r.BriefingId == b.Id).OrderByDescending(r => r.Revision).Take(50)
            .Select(r => new TowageRevisionDto(r.Revision, r.State, r.ReasonCode, r.Hash, r.ActorId, r.SavedAt)).ToArrayAsync(ct);
        var (riskState, riskReason) = TowageBriefingPolicy.RiskState(s.Risk, s.RiskReadable, s.RiskAcknowledgementState);
        if (s.Briefing?.Content.RiskAssessmentId == null && s.RiskReadable) (riskState, riskReason) = ("BLOCKED", "RiskAssessmentUnlinked");
        var coordinating = CoordinatingTug(s.Plan);
        var grants = coordinating == null ? [] : TugOperationsAuthorities.Resolve(h, a, coordinating.Value, TugOperationsAuthorities.MasterDuties);
        var write = BriefingWrite(a, vessel) && job.Status != TugJobLifecycle.Cancelled;
        var participant = s.Briefing?.Content.Participants.Any(p => p.ActorId == a.Id) == true;
        var acknowledged = s.Briefing?.CurrentAcknowledgements.Any(k => k.Actor == a.Id) == true;
        await TugReadinessHttp.Observe(h, BriefingRead, "Success", b?.Id ?? job.Id);
        return Results.Ok(new TowageBriefingView(vessel, job.Id, b == null ? null : Dto(b),
            s.Briefing == null ? [] : TowageBriefingPolicy.IssueBlockers(s.Briefing.Content, s.Plan, s.Risk),
            s.Briefing == null ? "NOT_APPLICABLE" : TowageBriefingPolicy.AcknowledgementState(s.Briefing.State, s.Briefing.Content, s.Briefing.CurrentAcknowledgements), acks,
            new(s.Briefing?.Content.RiskAssessmentId, riskState, riskReason, s.Risk?.Revision, s.Risk?.ApprovedRevision, s.RiskAcknowledgementState),
            s.Plan?.State, s.Plan?.Revision, TowageBriefingPolicy.Checks(s.Briefing, s.Plan, s.Risk, s.RiskReadable), revisions, TugOperationsAuthorities.Dto(grants),
            write && (b == null || b.State == TowageBriefingPolicy.Draft),
            b != null && b.State == TowageBriefingPolicy.Draft && grants.Length > 0 && s.RiskReadable && TugReadinessHttp.Write(a, coordinating!.Value),
            write && b != null && b.State == TowageBriefingPolicy.Issued,
            b != null && b.State == TowageBriefingPolicy.Issued && participant && !acknowledged && a.HasWriteAttribution,
            TowageBriefingPolicy.Roles));
    }
}
