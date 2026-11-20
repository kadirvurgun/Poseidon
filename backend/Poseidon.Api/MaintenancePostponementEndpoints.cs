using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record MaintenancePostponementDto(Guid Id, Guid PlanId, long PlanRevision, Guid AssetId, Guid? WorkOrderId, Guid[] DefectIds,
    string[] RequirementRefs, DateOnly OriginalDue, DateOnly RequestedDeadline, int ExtensionDays, string Reason, Guid? RiskAssessmentId,
    string Criticality, string State, string EffectiveState, string ReadinessImpact, Guid RequestedBy, DateTimeOffset RequestedAt,
    Guid? DecidedBy, DateTimeOffset? DecidedAt, string? DecisionReason, string? PolicyRef, string? PolicyVersion, int? MaxExtensionDays,
    long Revision, uint Version, bool CanDecide, string[] DecisionBlockers);
public sealed record MaintenancePostponementPage(Guid PlanId, long PlanRevision, DateOnly? OriginalDue, string DueState, string Criticality,
    string PolicyState, string? PolicyRef, string? PolicyVersion, int? MaxExtensionDays, bool CanRequest, bool CanDecide,
    MaintenancePostponementDto[] Items, string SourceVersion);
public sealed record MaintenancePostponementOutcome(MaintenancePostponementDto Postponement);
// Plan projection overlay: the original due date is unchanged; this only states the open/active request.
public sealed record MaintenancePostponementSummary(Guid Id, string State, string EffectiveState, DateOnly OriginalDue, DateOnly RequestedDeadline,
    string Criticality, string ReadinessImpact);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaintenancePostponementRequestCommand([property: JsonRequired] Guid Id, [property: JsonRequired] DateOnly RequestedDeadline,
    [property: JsonRequired] string Reason, Guid? RiskAssessmentId = null, Guid? WorkOrderId = null, Guid[]? DefectIds = null, string[]? RequirementRefs = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaintenancePostponementDecisionCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Decision,
    [property: JsonRequired] string Reason);

// DEV-109 / T-PMS2-003 (18.01 row 110; 06.01 FR-V463-PMS-003 p284; 59.04 Rev2 §12 p146-147,
// p49 pms.defer.approve, p265-266 pms.deferral.policy_ref, AT-033/AT-034 p369-372).
// Request -> one human APPROVE/REJECT by a different person. Approval needs the tenant policy
// (max extension), a risk review reference for CRITICAL/UNKNOWN equipment and an unchanged plan.
// The original due date is never overwritten and a postponement never yields readiness.
public static class MaintenancePostponementEndpoints
{
    private const string ReadAction = "MaintenancePostponementRead", CommandAction = "MaintenancePostponementCommand";
    public const string SourceVersion = "59.04-Rev2:fa795bfb06694d9265dccd28c797be0f6f8cb7ecbbe036d07e070f00c9727553:p49,p146-147,p265-266,p369-372";
    internal static bool CanRead(TrustedActor? a) => a != null && MaintenanceCurrentReader.Permitted(a);
    internal static bool CanRequest(TrustedActor? a, Guid vessel) => a != null && CanRead(a) && a.HasWriteAttribution && a.CanEditVessel(vessel) &&
        a.Permissions.Contains("maintenance.postponement.request");
    internal static bool CanDecide(TrustedActor? a, Guid vessel) => a != null && CanRead(a) && a.HasWriteAttribution && a.CanEditVessel(vessel) &&
        a.Permissions.Contains("maintenance.postponement.decide");
    // Capability shown to the client: the decision is a human record (403 human_actor_required otherwise).
    internal static bool CanDecide(HttpContext h, TrustedActor? a, Guid vessel) => CanDecide(a, vessel) && CertificateRenewalEndpoints.Human(h, a!);

    public static void MapMaintenancePostponements(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/maintenance-plan/{vessel:guid}/{id:guid}").RequireAuthorization("DirectoryRead");
        g.MapGet("/postponements", List).WithName("MaintenancePostponementList").Produces<MaintenancePostponementPage>();
        // Replaces the former always-denied route: the request is now an audited draft command.
        g.MapPost("/postpone", (Guid vessel, Guid id, HttpContext h, PoseidonDbContext db) => PmsCommands.Run<MaintenancePostponementRequestCommand>(h, db, vessel,
                "MaintenancePostpone", CommandAction, id, null, (a, v) => CanRequest(a, v), c => c.Id, Request, ReadOne))
            .WithName("MaintenancePostpone").Accepts<MaintenancePostponementRequestCommand>("application/json")
            .Produces<MaintenancePostponementOutcome>().Produces<PmsFailure>(422);
        g.MapPost("/postponements/{postponement:guid}/decision", (Guid vessel, Guid id, Guid postponement, HttpContext h, PoseidonDbContext db) =>
                PmsCommands.Run<MaintenancePostponementDecisionCommand>(h, db, vessel, "MaintenancePostponementDecide", CommandAction, id, postponement,
                    (a, v) => CanDecide(a, v), _ => postponement, Decide, ReadOne, humanOnly: true))
            .WithName("MaintenancePostponementDecide").Accepts<MaintenancePostponementDecisionCommand>("application/json")
            .Produces<MaintenancePostponementOutcome>().Produces<PmsFailure>(422);
    }

    private sealed record PlanContext(MaintenancePlan Plan, DateOnly? OriginalDue, string DueState, string Criticality, bool PostponementDeclaredNotAllowed);

    private static async Task<PlanContext?> Plan(PoseidonDbContext db, Guid vessel, Guid id, HttpContext h, DateTimeOffset now, CancellationToken ct)
    {
        var plan = await db.MaintenancePlans.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.Id == id, ct);
        if (plan == null) return null;
        var owners = TechnicalEndpoints.EligibleOwners(h.RequestServices.GetRequiredService<FederationSettings>(), db.TenantId, vessel);
        var current = (await MaintenanceCurrentReader.ReadOwned(vessel, db, owners, h.RequestServices.GetRequiredService<MaintenanceObservationSettings>(),
            now, ct, planId: id, withPostponements: false)).SingleOrDefault();
        var criticality = EquipmentCriticality.Of(await db.Assets.AsNoTracking().Where(a => a.VesselId == vessel && a.Id == plan.AssetId)
            .Select(a => a.Criticality).SingleOrDefaultAsync(ct));
        var inputs = JsonSerializer.Deserialize<MaintenanceInputs>(plan.ContentJson);
        return new(plan, current?.Row.Due.NextDueDate, current?.Row.Due.State ?? "ReviewRequired", criticality, inputs?.PostponementAllowed == "False");
    }

    private static async Task<bool> ApprovedRiskReview(PoseidonDbContext db, Guid vessel, Guid? id, CancellationToken ct) =>
        id != null && await db.RiskAssessments.AsNoTracking().AnyAsync(r => r.VesselId == vessel && r.Id == id && r.ApprovedRevision != null, ct);

    private static async Task<MaintenancePostponementDto> Dto(PoseidonDbContext db, MaintenancePostponement x, PlanContext? plan, TrustedActor actor,
        MaintenanceObservationSettings settings, DateTimeOffset now, bool human, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var effective = MaintenancePostponementPolicy.EffectiveState(x.State, x.RequestedDeadline, today);
        var criticality = EquipmentCriticality.RequiresReview(x.Criticality) || plan != null && EquipmentCriticality.RequiresReview(plan.Criticality)
            ? (x.Criticality == EquipmentCriticality.Critical || plan?.Criticality == EquipmentCriticality.Critical ? EquipmentCriticality.Critical : EquipmentCriticality.Unknown)
            : x.Criticality;
        var blockers = new List<string>();
        var canDecide = false;
        if (x.State == MaintenancePostponementPolicy.Requested && human && CanDecide(actor, x.VesselId))
        {
            var current = plan != null && plan.Plan.Revision == x.PlanRevision && plan.OriginalDue == x.OriginalDue;
            var code = MaintenancePostponementPolicy.ValidateApproval(x.RequestedBy, actor.Id, criticality,
                await ApprovedRiskReview(db, x.VesselId, x.RiskAssessmentId, ct), x.OriginalDue, x.RequestedDeadline, settings.Postponement(x.TenantId), current);
            if (code != null) blockers.Add(code);
            canDecide = x.RequestedBy != actor.Id;
        }
        return new(x.Id, x.PlanId, x.PlanRevision, x.AssetId, x.WorkOrderId, JsonSerializer.Deserialize<Guid[]>(x.DefectIdsJson) ?? [],
            JsonSerializer.Deserialize<string[]>(x.RequirementRefsJson) ?? [], x.OriginalDue, x.RequestedDeadline, x.ExtensionDays, x.Reason, x.RiskAssessmentId,
            x.Criticality, x.State, effective, MaintenancePostponementPolicy.ReadinessImpact(criticality, effective), x.RequestedBy, x.RequestedAt,
            x.DecidedBy, x.DecidedAt, x.DecisionReason, x.PolicyRef, x.PolicyVersion, x.MaxExtensionDays, x.Revision, x.Version, canDecide, [.. blockers]);
    }

    private static async Task<IResult> List(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db, MaintenanceObservationSettings settings)
    {
        h.Response.Headers.CacheControl = "no-store";
        var actor = PmsCommands.Actor(h); var ct = h.RequestAborted; var now = PmsCommands.Now(h);
        if (actor == null || !CanRead(actor)) return await PmsCommands.Fail(h, ReadAction, 403, "permission_denied");
        if (!await PmsCommands.InScope(db, actor, vessel, ct)) return await PmsCommands.Fail(h, ReadAction, 404, "resource_not_found");
        var plan = await Plan(db, vessel, id, h, now, ct);
        if (plan == null) return await PmsCommands.Fail(h, ReadAction, 404, "resource_not_found", id);
        var rows = await db.MaintenancePostponements.AsNoTracking().Where(x => x.VesselId == vessel && x.PlanId == id)
            .OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).Take(100).ToArrayAsync(ct);
        var items = new List<MaintenancePostponementDto>();
        var human = CertificateRenewalEndpoints.Human(h, actor);
        foreach (var row in rows) items.Add(await Dto(db, row, plan, actor, settings, now, human, ct));
        var rule = settings.Postponement(actor.TenantId);
        await PmsCommands.Audit(h, ReadAction, "Success", id);
        return Results.Json(new MaintenancePostponementPage(id, plan.Plan.Revision, plan.OriginalDue, plan.DueState, plan.Criticality,
            rule == null ? "UNCONFIGURED" : "CONFIGURED", rule?.PolicyRef, rule?.Version, rule?.MaxExtensionDays, CanRequest(actor, vessel), human && CanDecide(actor, vessel),
            [.. items], SourceVersion), PmsCommands.Json);
    }

    private static async Task<object?> ReadOne(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid id, HttpContext h, CancellationToken ct)
    {
        var row = await db.MaintenancePostponements.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
        if (row == null) return null;
        var now = PmsCommands.Now(h);
        var plan = await Plan(db, vessel, row.PlanId, h, now, ct);
        return new MaintenancePostponementOutcome(await Dto(db, row, plan, actor, h.RequestServices.GetRequiredService<MaintenanceObservationSettings>(), now,
            CertificateRenewalEndpoints.Human(h, actor), ct));
    }

    private static async Task<PmsOutcome> Request(PmsContext<MaintenancePostponementRequestCommand> c)
    {
        var cmd = c.Command; var db = c.Db; var planId = c.RouteId!.Value;
        var defects = cmd.DefectIds ?? []; var refs = cmd.RequirementRefs ?? [];
        if (cmd.Id == Guid.Empty || cmd.RiskAssessmentId == Guid.Empty || cmd.WorkOrderId == Guid.Empty || defects.Length > 20 || defects.Any(d => d == Guid.Empty) ||
            defects.Distinct().Count() != defects.Length || refs.Length > 10 || refs.Any(r => r is not { Length: > 0 and <= 200 } || !PmsCommands.Text(r, 200)) ||
            refs.Distinct(StringComparer.Ordinal).Count() != refs.Length) return PmsOutcome.Fail(400, "invalid_request");
        if (await db.MaintenancePostponements.AnyAsync(x => x.Id == cmd.Id, c.Ct)) return PmsOutcome.Fail(409, "version_conflict");
        var plan = await Plan(db, c.Vessel, planId, c.Http, c.Now, c.Ct);
        if (plan == null) return PmsOutcome.Fail(404, "resource_not_found");
        // Read-only id references owned by other modules (WP: work orders, defects, risk); same vessel only.
        if (cmd.WorkOrderId is { } wo && !await db.WorkOrders.AsNoTracking().AnyAsync(w => w.VesselId == c.Vessel && w.Id == wo, c.Ct) ||
            defects.Length > 0 && await db.Defects.AsNoTracking().CountAsync(d => d.VesselId == c.Vessel && defects.Contains(d.Id), c.Ct) != defects.Length ||
            cmd.RiskAssessmentId is { } risk && !await db.RiskAssessments.AsNoTracking().AnyAsync(r => r.VesselId == c.Vessel && r.Id == risk, c.Ct))
            return PmsOutcome.Fail(400, "linked_resource_unavailable");
        if (await db.MaintenancePostponements.AnyAsync(x => x.VesselId == c.Vessel && x.PlanId == planId && x.State == MaintenancePostponementPolicy.Requested, c.Ct))
            return PmsOutcome.Fail(409, "postponement_pending");
        DateOnly? previous = null;
        if (plan.OriginalDue is { } due)
        {
            var approved = await db.MaintenancePostponements.AsNoTracking().Where(x => x.VesselId == c.Vessel && x.PlanId == planId &&
                x.State == MaintenancePostponementPolicy.Approved && x.OriginalDue == due).Select(x => x.RequestedDeadline).ToArrayAsync(c.Ct);
            previous = approved.Length == 0 ? null : approved.Max();
        }
        var rule = c.Settings.Postponement(c.Actor.TenantId);
        if (MaintenancePostponementPolicy.ValidateRequest(cmd.Reason, plan.OriginalDue, cmd.RequestedDeadline, previous, rule, plan.PostponementDeclaredNotAllowed) is { } refused)
            return PmsOutcome.Fail(422, refused);
        var row = new MaintenancePostponement { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, PlanId = planId, PlanRevision = plan.Plan.Revision,
            AssetId = plan.Plan.AssetId, WorkOrderId = cmd.WorkOrderId, DefectIdsJson = JsonSerializer.Serialize(defects.Order().ToArray()),
            RequirementRefsJson = JsonSerializer.Serialize(refs), OriginalDue = plan.OriginalDue!.Value, RequestedDeadline = cmd.RequestedDeadline,
            ExtensionDays = MaintenancePostponementPolicy.ExtensionDays(plan.OriginalDue.Value, cmd.RequestedDeadline), Reason = cmd.Reason.Trim(),
            RiskAssessmentId = cmd.RiskAssessmentId, Criticality = plan.Criticality, State = MaintenancePostponementPolicy.Requested,
            RequestedBy = c.Actor.Id, RequestedAt = c.Now, Revision = 1 };
        db.MaintenancePostponements.Add(row); await db.SaveChangesAsync(c.Ct);
        return PmsOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<PmsOutcome> Decide(PmsContext<MaintenancePostponementDecisionCommand> c)
    {
        var cmd = c.Command; var db = c.Db;
        if (!MaintenancePostponementPolicy.Decisions.Contains(cmd.Decision, StringComparer.Ordinal)) return PmsOutcome.Fail(400, "invalid_request");
        if (!MaintenancePostponementPolicy.ValidReason(cmd.Reason)) return PmsOutcome.Fail(422, "invalid_reason");
        var row = await db.MaintenancePostponements.SingleOrDefaultAsync(x => x.VesselId == c.Vessel && x.PlanId == c.RouteId && x.Id == c.SubId, c.Ct);
        if (row == null) return PmsOutcome.Fail(404, "resource_not_found");
        if (row.Version != cmd.ExpectedVersion) return PmsOutcome.Fail(409, "version_conflict");
        if (row.State != MaintenancePostponementPolicy.Requested) return PmsOutcome.Fail(409, "invalid_state");
        // Separation of duties applies to both outcomes: the requester never decides own request.
        if (row.RequestedBy == c.Actor.Id) return PmsOutcome.Fail(403, "separation_of_duties");
        var rule = c.Settings.Postponement(c.Actor.TenantId);
        if (cmd.Decision == MaintenancePostponementPolicy.Approve)
        {
            var plan = await Plan(db, c.Vessel, row.PlanId, c.Http, c.Now, c.Ct);
            var criticality = EquipmentCriticality.RequiresReview(row.Criticality) || plan != null && EquipmentCriticality.RequiresReview(plan.Criticality)
                ? EquipmentCriticality.Critical : row.Criticality;
            var current = plan != null && plan.Plan.Revision == row.PlanRevision && plan.OriginalDue == row.OriginalDue;
            var code = MaintenancePostponementPolicy.ValidateApproval(row.RequestedBy, c.Actor.Id, criticality,
                await ApprovedRiskReview(db, c.Vessel, row.RiskAssessmentId, c.Ct), row.OriginalDue, row.RequestedDeadline, rule, current);
            if (code != null) return PmsOutcome.Fail(code switch { "separation_of_duties" => 403, "plan_context_stale" => 409, _ => 422 }, code);
            row.PolicyRef = rule!.PolicyRef; row.PolicyVersion = rule.Version; row.MaxExtensionDays = rule.MaxExtensionDays;
            row.State = MaintenancePostponementPolicy.Approved;
        }
        else row.State = MaintenancePostponementPolicy.Rejected;
        row.DecidedBy = c.Actor.Id; row.DecidedAt = c.Now; row.DecisionReason = cmd.Reason.Trim(); row.Revision = checked(row.Revision + 1);
        await db.SaveChangesAsync(c.Ct);
        return PmsOutcome.Ok(row.Id, row.Revision);
    }

    // Overlay for the current plan projection; one open/active postponement per plan.
    internal static async Task<Dictionary<Guid, MaintenancePostponementSummary>> Summaries(PoseidonDbContext db, Guid vessel, Guid[] plans,
        IReadOnlyDictionary<Guid, string> assetCriticality, DateTimeOffset now, CancellationToken ct)
    {
        if (plans.Length == 0) return [];
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var rows = await db.MaintenancePostponements.AsNoTracking().Where(x => x.VesselId == vessel && plans.Contains(x.PlanId) && x.State != MaintenancePostponementPolicy.Rejected)
            .OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var result = new Dictionary<Guid, MaintenancePostponementSummary>();
        foreach (var x in rows)
        {
            if (result.ContainsKey(x.PlanId)) continue;
            var effective = MaintenancePostponementPolicy.EffectiveState(x.State, x.RequestedDeadline, today);
            if (effective == MaintenancePostponementPolicy.Expired) continue;
            var current = EquipmentCriticality.Of(assetCriticality.GetValueOrDefault(x.AssetId));
            var criticality = EquipmentCriticality.RequiresReview(x.Criticality) || EquipmentCriticality.RequiresReview(current) ? EquipmentCriticality.Critical : x.Criticality;
            result[x.PlanId] = new(x.Id, x.State, effective, x.OriginalDue, x.RequestedDeadline, x.Criticality, MaintenancePostponementPolicy.ReadinessImpact(criticality, effective));
        }
        return result;
    }
}
