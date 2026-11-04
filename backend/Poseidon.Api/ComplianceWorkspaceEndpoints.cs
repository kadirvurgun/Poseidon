using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record ComplianceOnboardingDto(Guid VesselId, long? ProfileRevision, DateTimeOffset? SavedAt,
    string Provenance, string Freshness, string State, OnboardingField[] Fields, string[] Blockers,
    bool CanEdit, bool CanSubmit = false, string Source = "18.01/Backlog/row:40");
public sealed record ComplianceApplicabilityDto(bool Available, Guid? EvaluationId, long? ProfileRevision,
    DateTimeOffset? EvaluatedAt, bool Current, EvaluationPrediction[] Results, RuleConflict[] Conflicts,
    string[] Blockers, string ReviewerState, bool ManualOverrideAvailable, string Href,
    JsonElement? Inputs = null, string? InputHash = null, string? RuleSetHash = null);
public sealed record ComplianceRequirementPlanDto(Guid RuleId, long RuleRevision, Guid? RequirementId,
    long? RequirementRevision, Guid? SourceId, long? SourceRevision, Guid[] ControlIds, string State, string[] Blockers);
public sealed record ComplianceControlPlanDto(string Coverage, ComplianceRequirementPlanDto[] Requirements,
    ControlEvidenceDto[] Controls, ControlEvidenceDto[] Evidence, bool HasMore,
    int? UncontrolledRequirementCount, int? OrphanCriticalCount, string[] Blockers);
public sealed record CompliancePassportDto(Guid VesselId, DateTimeOffset ReadAt, string State, string Freshness,
    string CertificateCoverage, string[] Blockers, ComplianceOnboardingDto Onboarding,
    ComplianceApplicabilityDto Applicability, ComplianceControlPlanDto Plan);

// S5 is an owning-data workspace. It never declares a certificate inventory,
// overrides a rule, generates approved obligations, or turns an empty page green.
public static class ComplianceWorkspaceEndpoints
{
    public static void MapComplianceWorkspaces(this IEndpointRouteBuilder app)
    {
        // 15.01/paragraph:597 canonical passport alias, with the same scope gate.
        app.MapGet("/api/v1/vessel-compliance-passport", PassportAlias).RequireAuthorization("DirectoryRead")
            .WithName("VesselCompliancePassport").Produces<CompliancePassportDto>();
        var group = app.MapGroup("/api/v1/vessels/{id:guid}").RequireAuthorization("DirectoryRead");
        group.MapGet("/compliance-onboarding", Onboarding).WithName("ComplianceOnboarding").Produces<ComplianceOnboardingDto>();
        group.MapGet("/compliance-passport", (Guid id, HttpContext h, PoseidonDbContext db) => Workspace(id, h, db, "passport"))
            .WithName("CompliancePassport").Produces<CompliancePassportDto>();
        group.MapGet("/compliance-applicability-review", (Guid id, HttpContext h, PoseidonDbContext db) => Workspace(id, h, db, "review"))
            .WithName("ComplianceApplicabilityReview").Produces<ComplianceApplicabilityDto>();
        group.MapGet("/compliance-control-plan", (Guid id, HttpContext h, PoseidonDbContext db) => Workspace(id, h, db, "plan"))
            .WithName("ComplianceControlPlan").Produces<ComplianceControlPlanDto>();
    }
    private static Task<IResult> PassportAlias(HttpContext h, PoseidonDbContext db) =>
        h.Request.Query["vesselId"].Count == 1 && Guid.TryParseExact(h.Request.Query["vesselId"], "D", out var id) && id != Guid.Empty
            ? Workspace(id, h, db, "passport", true) : Fail(h, 400, "invalid_query");
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static async Task<IResult> Fail(HttpContext h, int status, string result) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ComplianceWorkspaceRead", result);
        return Results.Problem(statusCode: status, title: result, extensions: new Dictionary<string, object?> { ["code"] = result, ["correlationId"] = h.TraceIdentifier });
    }
    private static async Task<ComplianceOnboardingDto?> Assess(Vessel vessel, TrustedActor actor, PoseidonDbContext db, CancellationToken ct) {
        var current = await RegulatoryDraftEndpoints.ReadCurrent(actor, vessel.Id, db, ct);
        if (!current.Readable) return null;
        var draft = current.Draft;
        var content = draft == null ? null : RegulatoryDraftStorage.Deserialize(draft.ContentJson);
        var assessment = ComplianceOnboardingPolicy.Assess(vessel.Code, vessel.Name, content);
        return new(vessel.Id, draft?.Revision, draft?.SavedAt, draft?.Provenance ?? "Missing", "Unverified",
            assessment.State, assessment.Fields, assessment.Blockers, actor.CanEditVessel(vessel.Id));
    }
    private static async Task<IResult> Onboarding(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_query");
        var actor = Actor(h);
        var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(v => v.Id == id, h.RequestAborted);
        if (vessel == null) return await Fail(h, 404, "resource_not_found");
        var dto = await Assess(vessel, actor, db, h.RequestAborted);
        if (dto == null) return await Fail(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ComplianceWorkspaceRead", "Success", resourceId: id);
        return Results.Ok(dto);
    }
    private static async Task<ComplianceApplicabilityDto> Review(Guid vessel, TrustedActor actor, HttpContext h,
        PoseidonDbContext db, DateTimeOffset now) {
        var href = $"/app/fleet/{vessel:D}/passport#applicability";
        var row = await OperationEquipmentCommands.Evaluations(db, actor)
            .Where(e => e.VesselId == vessel && db.EvaluationQualifications.Any(q => q.EvaluationId == e.Id))
            .OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).FirstOrDefaultAsync(h.RequestAborted);
        if (row == null) return new(false, null, null, null, false, [], [], ["QUALIFIED_EVALUATION_UNAVAILABLE"], "REVIEW_REQUIRED", false, href);
        var result = JsonSerializer.Deserialize<EvaluationDto>(row.ResultJson, RegulatoryQualificationEndpoints.Json);
        if (result == null || result.VesselId != vessel || result.Results is not { Length: > 0 and <= 64 } || result.Conflicts == null)
            return new(false, null, null, null, false, [], [], ["EVALUATION_UNAVAILABLE"], "REVIEW_REQUIRED", false, href);
        var blockers = (await CurrentRegulatoryContext.Stale(db, row.Id, vessel, now, h.RequestAborted,
            h.RequestServices.GetRequiredService<FederationSettings>())).ToList();
        var eligible = await db.RegulatoryQualificationEvents.AsNoTracking().Where(q => q.State == "IN_FORCE" &&
            db.RegulatoryQualificationHeads.Any(head => head.RuleId == q.RuleId && head.Revision == q.Revision) &&
            db.RuleDrafts.Any(head => head.Id == q.RuleId && head.Revision == q.RuleRevision)).Select(q => q.RuleId).ToArrayAsync(h.RequestAborted);
        if (eligible.Length == 0) blockers.Add("QUALIFIED_CATALOG_UNAVAILABLE");
        if (!result.Authoritative || result.Conflicts.Length != 0 || result.Results.Any(r =>
                !r.Prediction.Authoritative || r.Prediction.Result is not ("APPLICABLE" or "NOT_APPLICABLE") ||
                r.RequirementId == null || r.RequirementRevision is not > 0 || r.SourceId == null || r.SourceRevision is not > 0) ||
            eligible.Any(id => !result.Results.Any(r =>
                r.RuleId == id && r.Prediction.Authoritative && r.Prediction.Result is "APPLICABLE" or "NOT_APPLICABLE")))
            blockers.Add("APPLICABILITY_REVIEW_REQUIRED");
        var current = blockers.Count == 0;
        // Preserve the immutable rule/source/reason tree. Currentness is separate
        // from the recorded result; there is no generic admin override authority.
        return new(current, row.Id, row.ProfileRevision, row.SavedAt, current, result.Results, result.Conflicts,
            blockers.Distinct().Order(StringComparer.Ordinal).ToArray(), current ? "QUALIFIED_CURRENT" : "REVIEW_REQUIRED", false, href,
            result.Snapshot.Clone(), row.SnapshotHash, row.RuleSetHash);
    }
    private static async Task<ComplianceControlPlanDto> Plan(Guid vessel, TrustedActor actor, HttpContext h,
        PoseidonDbContext db, DateTimeOffset now, int offset, ComplianceApplicabilityDto review) {
        var visible = ControlEvidenceEndpoints.CurrentVisible(db, vessel, actor);
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        async Task<(ControlEvidenceDto[] Items, bool More)> Page(string kind) {
            var rows = await visible.Where(r => r.Kind == kind).OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id)
                .Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
            var items = new List<ControlEvidenceDto>();
            foreach (var row in rows.Take(25)) {
                var version = await db.ControlEvidenceHeads.AsNoTracking().Where(head => head.VesselId == vessel && head.Kind == kind && head.Id == row.Id)
                    .Select(head => head.Version).SingleAsync(h.RequestAborted);
                items.Add(await ControlEvidenceEndpoints.ProjectCurrent(db, row, version, now, h.RequestAborted, actor, settings));
            }
            return (items.ToArray(), rows.Length > 25);
        }
        var controls = await Page("Control"); var evidence = await Page("Evidence");
        var requirements = new List<ComplianceRequirementPlanDto>();
        var incomplete = false;
        if (review.Available) foreach (var result in review.Results.Where(r => r.Prediction.Result == "APPLICABLE")) {
            var linked = await db.ControlEvidenceRequirements.AsNoTracking().Where(p => p.VesselId == vessel && p.Kind == "Control" &&
                p.RuleId == result.RuleId && p.RuleRevision == result.Revision && p.RequirementId == result.RequirementId &&
                p.RequirementRevision == result.RequirementRevision && p.SourceId == result.SourceId && p.SourceRevision == result.SourceRevision &&
                visible.Any(r => r.Kind == p.Kind && r.Id == p.Id && r.Revision == p.Revision))
                .Select(p => p.Id).Distinct().Order().Take(26).ToArrayAsync(h.RequestAborted);
            incomplete |= linked.Length > 25;
            requirements.Add(new(result.RuleId, result.Revision, result.RequirementId, result.RequirementRevision,
                result.SourceId, result.SourceRevision, linked.Take(25).ToArray(), linked.Length > 0 ? "LINKED" : "REVIEW_REQUIRED",
                linked.Length == 0 ? ["CONTROL_PLAN_MISSING"] : linked.Length > 25 ? ["CONTROL_LINKS_TRUNCATED"] : []));
        }
        var blockers = new List<string>();
        if (!review.Available) blockers.Add("CURRENT_APPLICABILITY_UNAVAILABLE");
        if (requirements.Any(r => r.ControlIds.Length == 0)) blockers.Add("CONTROL_PLAN_MISSING");
        if (incomplete || controls.More || evidence.More) blockers.Add("BOUNDED_PROJECTION");
        // A completed control or a file is not verification. Missing criticality
        // and the absent certificate authority/catalog cannot become a zero.
        blockers.Add("CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE");
        return new(review.Available ? "CURRENT_QUALIFIED_CATALOG_ONLY" : "UNAVAILABLE", requirements.ToArray(), controls.Items,
            evidence.Items, controls.More || evidence.More || incomplete,
            review.Available ? requirements.Count(r => r.ControlIds.Length == 0) : null, null, blockers.ToArray());
    }
    private static async Task<IResult> Workspace(Guid id, HttpContext h, PoseidonDbContext db, string view, bool canonicalAlias = false) {
        h.Response.Headers.CacheControl = "no-store";
        var actor = Actor(h); var ct = h.RequestAborted;
        if (!ControlEvidenceEndpoints.Read(actor)) return await Fail(h, 403, "permission_denied");
        var offset = 0;
        if (h.Request.Query.Any(q => q.Key != "offset" && !(canonicalAlias && q.Key == "vesselId") || q.Value.Count != 1) ||
            h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset is < 0 or > 100000))
            return await Fail(h, 400, "invalid_query");
        // One database snapshot for source gates, head/version linkage, current
        // applicability and graph pages. Concurrent new restrictions never mix.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(v => v.Id == id, ct);
        if (vessel == null) return await Fail(h, 404, "resource_not_found");
        var onboarding = await Assess(vessel, actor, db, ct);
        if (onboarding == null) return await Fail(h, 403, "permission_denied");
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var review = await Review(id, actor, h, db, now);
        object outcome = review;
        if (view != "review") {
            var plan = await Plan(id, actor, h, db, now, offset, review);
            outcome = view == "plan" ? plan : new CompliancePassportDto(id, now, "REVIEW_REQUIRED", "Unverified", "UNAVAILABLE",
                onboarding.Blockers.Concat(review.Blockers).Concat(plan.Blockers).Append("CERTIFICATE_CATALOG_UNAVAILABLE").Distinct().Order(StringComparer.Ordinal).ToArray(), onboarding, review, plan);
        }
        await transaction.CommitAsync(ct);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ComplianceWorkspaceRead", "Success", resourceId: id);
        return Results.Ok(outcome);
    }
}
