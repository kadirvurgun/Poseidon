using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record HighRiskOwnerDue(string? OwnerRole, DateTimeOffset? DueAt, string? SourceKind);
public sealed record HighRiskWorkRow(string WorkKind, Guid WorkId, string WorkRef, Guid VesselId, string VesselCode, string VesselName, string VesselKind,
    string WorkType, DateTimeOffset? PlannedStart, string? ResponsibleRole, string RiskState, string PermitState, string CriticalBarrierState,
    string CrewReadiness, string EquipmentReadiness, HighRiskNextAction NextAction, HighRiskOwnerDue OwnerDue, HighRiskDimension[] Why,
    int CriticalBlockers, int ReviewItems, string Link);
public sealed record HighRiskWorkPage(HighRiskWorkRow[] Items, int Offset, int Limit, bool HasMore, string[] Coverage, string[] WorkTypes,
    HighRiskVesselOption[] Vessels, HighRiskCompanyOption[] Fleets, DateTimeOffset EvaluatedAt,
    string Scoring = "NoAggregateScore;EveryDimensionShown;CriticalFirst", string SourceVersion = HighRiskWorkPolicy.SourceVersion);
public sealed record HighRiskVesselOption(Guid Id, string Code, string Name, string Kind, Guid CompanyId);
public sealed record HighRiskCompanyOption(Guid Id, string Name);

// DEV-107 GET /api/v1/hse/high-risk-work (50.01 paragraph 907/1072): read-only fleet exception projection over
// the actual owning records the actor may read (risk assessments; tug jobs with their towage plan, briefing and
// live DEV-084 readiness). It writes nothing, issues no permit and never turns UNKNOWN into READY.
public static class HighRiskWorkEndpoints
{
    private const string Read = "HighRiskWorkRead";
    private const int MaxSource = 200, MaxLimit = 20, MaxProposals = 4;
    internal static bool CanRead(TrustedActor? a) => RiskAssessmentEndpoints.CanRead(a) && a!.Permissions.Contains("hse.high_risk_work.read");

    public static void MapHighRiskWork(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/v1/hse").RequireAuthorization("DirectoryRead").MapGet("/high-risk-work", List).WithName("HighRiskWorkList").Produces<HighRiskWorkPage>();

    private static bool Parse(HttpContext h, out Guid? company, out Guid? vessel, out string? workType, out int offset, out int limit) {
        company = null; vessel = null; workType = null; offset = 0; limit = MaxLimit;
        string[] allowed = ["companyId", "vesselId", "workType", "offset", "limit"];
        if (h.Request.Query.Any(q => !allowed.Contains(q.Key) || q.Value.Count != 1)) return false;
        bool Id(string key, out Guid? value) {
            value = null;
            if (!h.Request.Query.TryGetValue(key, out var raw)) return true;
            if (!Guid.TryParseExact(raw, "D", out var id) || id == Guid.Empty) return false;
            value = id; return true;
        }
        if (!Id("companyId", out company) || !Id("vesselId", out vessel)) return false;
        if (h.Request.Query.TryGetValue("workType", out var type)) { if (!HighRiskWorkPolicy.WorkTypes.Contains(type.ToString())) return false; workType = type; }
        if (h.Request.Query.TryGetValue("offset", out var o) && (!int.TryParse(o, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 10000)) return false;
        if (h.Request.Query.TryGetValue("limit", out var l) && (!int.TryParse(l, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit < 1 || limit > MaxLimit)) return false;
        return true;
    }

    // Worst-first over the COMPLETE dimension set of each row (critical first, then review, then planned start).
    internal static HighRiskWorkRow[] Order(IEnumerable<(HighRiskWorkRow Row, DateTimeOffset UpdatedAt)> rows) =>
        rows.OrderBy(r => HighRiskWorkPolicy.Rank(r.Row.Why)).ThenBy(r => r.Row.PlannedStart ?? DateTimeOffset.MaxValue)
            .ThenByDescending(r => r.UpdatedAt).ThenBy(r => r.Row.WorkId).Select(r => r.Row).ToArray();

    private sealed record Candidate(string Kind, Guid Id, string Ref, Vessel Vessel, string WorkType, DateTimeOffset? PlannedStart, string? Role,
        (string State, string Reason, Guid? Source) Risk, List<HighRiskDimension> Why, HighRiskOwnerDue OwnerDue, DateTimeOffset UpdatedAt, TugJob? Job);

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!CanRead(a)) return await TowageCommands.Fail(h, Read, 403, "permission_denied");
        if (!Parse(h, out var company, out var vesselFilter, out var workType, out var offset, out var limit)) return await TowageCommands.Fail(h, Read, 400, "invalid_query");
        var ct = h.RequestAborted; var now = TugReadinessHttp.Now(h);
        var scoped = await a.Scope(db.Vessels.AsNoTracking()).OrderBy(v => v.Code).ThenBy(v => v.Id).Take(500).ToArrayAsync(ct);
        var companyIds = scoped.Select(v => v.CompanyId).Distinct().ToArray();
        var fleets = await db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id)).OrderBy(c => c.Name).Select(c => new HighRiskCompanyOption(c.Id, c.Name)).ToArrayAsync(ct);
        // An out-of-scope filter is indistinguishable from an empty one: no existence or count leaks.
        var vessels = scoped.Where(v => (company == null || v.CompanyId == company) && (vesselFilter == null || v.Id == vesselFilter)).ToDictionary(v => v.Id);
        var ids = vessels.Keys.ToArray();
        var coverage = new List<string> { "PermitToWorkSourceUnavailable", "BarrierHealthSourceUnavailable" };
        var rows = new List<Candidate>();
        var jobRows = new HashSet<Guid>();

        if (a.CanReadTug) {
            var jobs = await db.TugJobs.AsNoTracking().Where(j => ids.Contains(j.VesselId) && j.Status == TugJobLifecycle.Submitted)
                .OrderByDescending(j => j.SavedAt).Take(MaxSource + 1).ToArrayAsync(ct);
            if (jobs.Length > MaxSource) { coverage.Add("TugJobSourceLimitExceeded"); jobs = jobs.Take(MaxSource).ToArray(); }
            foreach (var job in jobs) {
                jobRows.Add(job.Id);
                var s = await TowageReader.Read(db, a, job.VesselId, job.Id, ct);
                var why = new List<HighRiskDimension>();
                // Risk: the briefing's linked assessment, else none linked.
                var (riskState, riskReason) = s.Briefing?.Content.RiskAssessmentId == null ? ("BLOCKED", "RiskAssessmentUnlinked") : TowageBriefingPolicy.RiskState(s.Risk, s.RiskReadable, s.RiskAcknowledgementState);
                if (s.Risk?.State == RiskPolicy.Returned) riskReason = "RiskAssessmentReturned";
                why.Add(new("risk", riskState, riskReason, s.Risk?.Id));
                why.Add(new("permit", HighRiskWorkPolicy.PermitUnknown, "PermitToWorkSourceUnavailable"));
                // Plan and briefing states are shown as their own dimensions.
                var plan = s.Plan == null ? new HighRiskDimension("towage_plan", "BLOCKED", "TowagePlanMissing")
                    : s.Plan.State == TowagePlanPolicy.Approved && s.Plan.ApprovedRevision == s.Plan.Revision
                        ? s.Plan.JobRevision == job.Revision ? new("towage_plan", "READY", "TowagePlanApproved", s.Plan.Id) : new("towage_plan", "BLOCKED", "TowagePlanRequestChanged", s.Plan.Id)
                        : s.Plan.State == TowagePlanPolicy.InReview ? new("towage_plan", "REVIEW_REQUIRED", "TowagePlanInReview", s.Plan.Id) : new("towage_plan", "BLOCKED", "TowagePlanNotApproved", s.Plan.Id);
                why.Add(plan);
                foreach (var c in TowageBriefingPolicy.Checks(s.Briefing, s.Plan, s.Risk, s.RiskReadable))
                    why.Add(new("briefing", c.Result switch { TugReadinessEngine.Pass => "READY", TugReadinessEngine.Fail => "BLOCKED", _ => "REVIEW_REQUIRED" }, c.Reason, c.SourceId));
                var draft = await db.TugDraftPlans.AsNoTracking().Where(p => p.VesselId == job.VesselId && p.JobId == job.Id).Select(p => p.PlannedStart).SingleOrDefaultAsync(ct);
                var start = draft ?? job.RequestedStart;
                rows.Add(new("TugJob", job.Id, job.JobNumber, vessels[job.VesselId], "TOWAGE", start, "TugMaster", (riskState, riskReason, s.Risk?.Id), why,
                    new("TugMaster", start, start == null ? null : draft != null ? "TugDraftPlan" : "TugRequest"), job.SavedAt, job));
            }
        } else coverage.Add("TugJobsRequireTugRead");

        var assessments = await db.RiskAssessments.AsNoTracking().Where(r => ids.Contains(r.VesselId)).OrderByDescending(r => r.SavedAt).Take(MaxSource + 1).ToArrayAsync(ct);
        if (assessments.Length > MaxSource) { coverage.Add("RiskAssessmentSourceLimitExceeded"); assessments = assessments.Take(MaxSource).ToArray(); }
        foreach (var r in assessments) {
            RiskAssessmentContent content; RiskLinkPin[] links;
            try {
                content = JsonSerializer.Deserialize<RiskAssessmentContent>(r.ContentJson, RiskCommands.Json) ?? throw new JsonException();
                links = JsonSerializer.Deserialize<RiskLinkPin[]>(r.LinksJson, RiskCommands.Json) ?? [];
            } catch (JsonException) { coverage.Add("RiskAssessmentContentUnavailable"); continue; }
            // A towage assessment linked to a projected tug job is shown inside that job's row.
            if (links.Any(l => l.Kind == "TugJob" && jobRows.Contains(l.Id))) continue;
            var type = HighRiskWorkPolicy.FromRiskWorkType(content.WorkType);
            if (type == null) continue;
            var ack = await TowageReader.RiskAcknowledgement(db, r.Id, ct);
            var (state, reason) = TowageBriefingPolicy.RiskState(new(r.Id, r.State, r.Revision, r.ApprovedRevision), true, ack);
            if (r.State == RiskPolicy.Returned) reason = "RiskAssessmentReturned";
            var why = new List<HighRiskDimension> {
                new("risk", state, reason, r.Id),
                new("permit", HighRiskWorkPolicy.PermitUnknown, "PermitToWorkSourceUnavailable"),
                new("critical_barrier", "UNKNOWN", content.Hazards.Any(x => x.BarrierRefs.Length > 0 || x.Controls.Any(c => c.BarrierRefs.Length > 0)) ? "BarrierHealthSourceUnavailable" : "NoCriticalBarrierDeclared"),
                new("crew", TugReadinessEngine.ReviewRequired, "CrewReadinessSourceUnavailable"),
                new("equipment", TugReadinessEngine.ReviewRequired, "EquipmentReadinessSourceUnavailable") };
            if (type == "UNCLASSIFIED") why.Add(new("work_type", "REVIEW_REQUIRED", content.WorkType == null ? "WorkTypeMissing" : "HighRiskClassificationUnconfirmed", r.Id));
            var role = content.Participants.Select(p => p.Role).FirstOrDefault(x => x is "ASSESSOR" or "PREPARER");
            var owner = r.State switch { RiskPolicy.InReview => "RISK_APPROVER", RiskPolicy.Approved => "PARTICIPANT", _ => "PREPARER" };
            rows.Add(new("RiskAssessment", r.Id, r.AssessmentNo, vessels[r.VesselId], type, null, role, (state, reason, r.Id), why, new(owner, null, null), r.SavedAt, null));
        }

        // Every dimension (including the live crew/equipment readiness below) is added BEFORE ranking and
        // paging, so a row whose only critical blocker comes from live tug readiness still sorts first.
        // Bounded by MaxSource tug jobs x MaxProposals live evaluations.
        var candidates = rows.Where(x => workType == null || x.WorkType == workType).ToArray();
        var items = new List<(HighRiskWorkRow Row, DateTimeOffset UpdatedAt)>();
        foreach (var x in candidates) {
            var why = x.Why;
            string crew, equipment, barrier;
            if (x.Job is { } job) {
                // Live DEV-084 evaluation of every active tug proposal for this job (bounded); worst state wins.
                var proposals = await db.TugAssignmentProposals.AsNoTracking().Where(p => p.VesselId == job.VesselId && p.JobId == job.Id && p.State == TugAssignmentWorkflow.Proposed)
                    .OrderBy(p => p.Id).Take(MaxProposals + 1).ToArrayAsync(ct);
                var crewStates = new List<string>(); var equipmentStates = new List<string>(); barrier = "UNKNOWN"; var barrierReason = "BarrierHealthSourceUnavailable";
                if (proposals.Length == 0) {
                    why.Add(new("crew", TugReadinessEngine.ReviewRequired, "NoTugAssignmentProposed"));
                    why.Add(new("equipment", TugReadinessEngine.ReviewRequired, "NoTugAssignmentProposed"));
                } else if (proposals.Length > MaxProposals) {
                    why.Add(new("crew", TugReadinessEngine.ReviewRequired, "TugProposalLimitExceeded"));
                    why.Add(new("equipment", TugReadinessEngine.ReviewRequired, "TugProposalLimitExceeded"));
                } else foreach (var p in proposals) {
                    if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == p.TugId, ct)) { crewStates.Add(TugReadinessEngine.ReviewRequired); equipmentStates.Add(TugReadinessEngine.ReviewRequired); continue; }
                    var assessed = await TugReadinessAssessor.Assess(h, db, a, p, job, now, ct);
                    var crewChecks = assessed.Checks.Where(c => c.Category is "crew" or "crew_competence").ToArray();
                    var equipmentChecks = assessed.Checks.Where(c => c.Category is "equipment" or "tow_equipment" or "pms").ToArray();
                    crewStates.Add(TugReadinessEngine.StateOf(crewChecks)); equipmentStates.Add(TugReadinessEngine.StateOf(equipmentChecks));
                    foreach (var c in crewChecks.Concat(equipmentChecks).Where(c => c.Result is TugReadinessEngine.Fail or TugReadinessEngine.Unknown).Take(8))
                        why.Add(new(c.Category is "crew" or "crew_competence" ? "crew" : "equipment", c.Result == TugReadinessEngine.Fail ? TugReadinessEngine.NotReady : TugReadinessEngine.ReviewRequired, c.Reason, c.SourceId));
                    if (equipmentChecks.Any(c => c.Reason.StartsWith("EmergencyRelease:", StringComparison.Ordinal) && c.Result == TugReadinessEngine.Fail)) { barrier = "FAILED"; barrierReason = "EmergencyReleaseFailed"; }
                }
                crew = proposals.Length is 0 or > MaxProposals ? TugReadinessEngine.ReviewRequired : HighRiskWorkPolicy.Readiness(crewStates);
                equipment = proposals.Length is 0 or > MaxProposals ? TugReadinessEngine.ReviewRequired : HighRiskWorkPolicy.Readiness(equipmentStates);
                why.Add(new("critical_barrier", barrier, barrierReason));
                if (proposals.Length is > 0 and <= MaxProposals) { why.Add(new("crew", crew, "LiveTugReadinessCrew")); why.Add(new("equipment", equipment, "LiveTugReadinessEquipment")); }
            } else {
                crew = TugReadinessEngine.ReviewRequired; equipment = TugReadinessEngine.ReviewRequired; barrier = "UNKNOWN";
            }
            var dims = why.ToArray();
            var link = x.Kind == "TugJob" ? $"/app/towage-risk-briefing?vessel={x.Vessel.Id:D}&job={x.Id:D}" : $"/app/risk-assessment/{x.Vessel.Id:D}?id={x.Id:D}";
            items.Add((new(x.Kind, x.Id, x.Ref, x.Vessel.Id, x.Vessel.Code, x.Vessel.Name, x.Vessel.Kind.ToString(), x.WorkType, x.PlannedStart, x.Role, x.Risk.State,
                HighRiskWorkPolicy.PermitUnknown, barrier, crew, equipment, HighRiskWorkPolicy.NextAction(dims), x.OwnerDue, dims,
                HighRiskWorkPolicy.Count(dims, "CRITICAL"), HighRiskWorkPolicy.Count(dims, "REVIEW"), link), x.UpdatedAt));
        }
        var filtered = Order(items);
        var page = filtered.Skip(offset).Take(limit).ToArray();
        await TugReadinessHttp.Observe(h, Read, "Success");
        return Results.Ok(new HighRiskWorkPage(page, offset, limit, filtered.Length > offset + page.Length, [.. coverage.Distinct()], HighRiskWorkPolicy.WorkTypes,
            scoped.Select(v => new HighRiskVesselOption(v.Id, v.Code, v.Name, v.Kind.ToString(), v.CompanyId)).ToArray(), fleets, now));
    }
}
