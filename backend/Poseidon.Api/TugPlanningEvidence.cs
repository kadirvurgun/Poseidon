using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TugBpEvidence(Guid TugId, string State, string? DeclaredValue, string? Source, string? PolicyVersion,
    long? SourceRevision, Guid? ObservationId, DateTimeOffset? ObservedAt, string Freshness, string Provenance);
public sealed record TugPlanConflict(Guid JobId, long Revision, string Relation, string Source, DateTimeOffset ObservedAt);
public sealed record TugPlanningEvidence(string? AvailableBollardPull, string CapacityState, TugBpEvidence[] Bp,
    string ConflictState, TugPlanConflict[] Conflicts, string Coverage, string Readiness,
    TugOwnedCapacity[]? OwnedBp = null, TugReadinessFact[]? ReadinessFacts = null, TugOperationalConflict[]? OperationalConflicts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TugCrewReadinessDto[]? Crew = null);

public sealed record TugReadinessFact(Guid TugId, string Source, Guid? Id, long? Revision, string State, string Why,
    DateTimeOffset? ObservedAt = null, DateTimeOffset? ExpiresAt = null);

public static class TugPlanningEvidenceAdapter
{
    public static async Task<TugPlanningEvidence> Read(PoseidonDbContext db, TrustedActor actor, TugPlanningPolicy policy, MaintenanceObservationSettings observationSettings,
        Guid jobId, TugPlanInputs plan, CancellationToken ct, FederationSettings? federation = null, CrewFilePolicy? files = null,
        HttpContext? http = null, DateTimeOffset? readAt = null) {
        var now = readAt ?? DateTimeOffset.UtcNow; var bp = new List<TugBpEvidence>();
        var visible = await actor.Scope(db.Vessels).Where(v => v.Kind == VesselKind.Tug).Select(v => v.Id).ToArrayAsync(ct);
        foreach (var tug in plan.AssignedTugs.Where(visible.Contains)) {
            var source = policy.Current(now) ? policy.BpSources.SingleOrDefault(s => s.TenantId == actor.TenantId && s.VesselId == tug) : null;
            if (!actor.Permissions.Contains("equipment.read") || !actor.Permissions.Contains("maintenance.read") || !policy.Current(now) || source == null || source.MaxAgeSeconds is < 1 or > 31536000) {
                OperationalTelemetry.ObserveFreshness(ObservationArea.Tug, SourceFreshness.Unavailable, db.TenantId);
                bp.Add(new(tug, "Unavailable", null, null, null, null, null, null, "Unavailable", "Unavailable")); continue;
            }
            var rule = observationSettings.Rule(actor.TenantId, tug, source.AssetId, source.Source);
            if (rule == null || rule.Version != source.PolicyVersion || rule.Kind != "Meter" || rule.Unit != "tonnes") {
                OperationalTelemetry.ObserveFreshness(ObservationArea.Tug, SourceFreshness.Unavailable, db.TenantId);
                bp.Add(new(tug, "Unavailable", null, null, null, null, null, null, "Unavailable", "Unavailable")); continue;
            }
            var observation = await db.MaintenanceObservations.AsNoTracking().Where(o => o.VesselId == tug && o.AssetId == source.AssetId && o.Source == source.Source)
                .OrderByDescending(o => o.SourceRevision).FirstOrDefaultAsync(ct);
            if (observation == null) { OperationalTelemetry.ObserveFreshness(ObservationArea.Tug, SourceFreshness.Unknown, db.TenantId); bp.Add(new(tug, "Missing", null, source.Source, source.PolicyVersion, null, null, null, "Unknown", "Missing")); continue; }
            var revoked = await db.MaintenanceObservationRevocations.AnyAsync(r => r.ObservationId == observation.Id, ct);
            var freshnessState = SourceFreshnessPolicy.Assess(observation.ObservedAt, now, TimeSpan.FromSeconds(source.MaxAgeSeconds));
            OperationalTelemetry.ObserveFreshness(ObservationArea.Tug, freshnessState, db.TenantId);
            var freshness = freshnessState.ToString();
            var state = revoked ? "Revoked" : observation.PolicyVersion != source.PolicyVersion ? "PolicyChanged" : observation.Unit != "tonnes" || observation.Kind != "Meter" ? "UnitOrKindMismatch" :
                !TugRequestPolicy.TryBp(observation.Value, out var value) || value == null ? "Unknown" : freshness != "Current" ? freshness : "DeclaredUnverified";
            // Normalized technical observations are declarations; their numeric sum is NEVER verified capacity.
            bp.Add(new(tug, state, state == "DeclaredUnverified" ? observation.Value : null, observation.Source, observation.PolicyVersion,
                observation.SourceRevision, observation.Id, observation.ObservedAt, freshness, observation.Provenance));
        }
        var conflicts = new List<TugPlanConflict>();
        var scoped = actor.Scope(db.Vessels).Where(v => v.Kind == VesselKind.Tug).Select(v => v.Id);
        // Compare all currently visible plans, not just the board page. Never query hidden counts.
        var others = await db.TugDraftPlans.AsNoTracking().Where(p => p.JobId != jobId && scoped.Contains(p.VesselId) &&
            !db.TugDraftPlanTugs.Any(t => t.JobId == p.JobId && t.VesselId == p.VesselId && t.Revision == p.Revision && !visible.Contains(t.TugId))).ToArrayAsync(ct);
        var links = await db.TugDraftPlanTugs.AsNoTracking().Where(t => scoped.Contains(t.VesselId) && visible.Contains(t.TugId) && plan.AssignedTugs.Contains(t.TugId)).ToArrayAsync(ct);
        foreach (var other in others.Where(p => links.Any(t => t.JobId == p.JobId && t.Revision == p.Revision))) {
            var relation = TugPlanningWindowPolicy.CompareDeclared(new(plan.PlannedStart, plan.PlannedEnd), new(other.PlannedStart, other.PlannedEnd));
            if (relation != TugPlanningWindowRelation.Separate) conflicts.Add(new(other.JobId, other.Revision, relation.ToString(), "CurrentDraftPlanGeometry", other.SavedAt));
        }
        var stateConflict = conflicts.Any(c => c.Relation == "Intersecting") ? "HumanReviewRequired" : conflicts.Any(c => c.Relation == "BoundaryTouch") ? "BoundaryReviewRequired" : "Unknown";
        var capacity = new List<TugOwnedCapacity>(); var readiness = new List<TugReadinessFact>(); var crew = new List<TugCrewReadinessDto>();
        var job = await db.TugJobs.AsNoTracking().SingleAsync(j => j.Id == jobId, ct);
        foreach (var tug in plan.AssignedTugs.Where(visible.Contains)) {
            var currentCapacity = await TugOwnedCapacityConsumer.Read(db, actor, federation ?? new(), tug, job, plan, now, ct);
            readiness.Add(await OperationEquipmentContext.TugCurrent(db, actor, federation ?? new(), job, tug, now, ct));
            capacity.Add(currentCapacity);
            // These exact qualified facts cover BP and its owning equipment condition,
            // not all equipment, minimum manning, work/rest or a Master decision.
            if (currentCapacity.State == "CurrentPlanningCapacity") {
                readiness.Add(new(tug, "BollardPullCertificate", currentCapacity.EvidenceId, currentCapacity.EvidenceRevision,
                    "Current", "CurrentIndependentCertificateVerification;MasterDecisionSeparate", currentCapacity.ObservedAt, currentCapacity.ValidUntil));
                readiness.Add(new(tug, "CapabilityEquipmentCondition", currentCapacity.ConditionId, currentCapacity.ConditionRevision,
                    "Current", "ExactQualifiedCapabilityCondition;OtherEquipmentCoverageSeparate", ExpiresAt: currentCapacity.ValidUntil));
            }
            readiness.AddRange(await TugReadinessConsumer.Read(db, actor, tug, plan, now, ct, files, http));
            var currentCrew = await TugCrewReadinessReader.Read(db, actor, federation ?? new(), tug, plan, now, ct,
                http?.RequestServices.GetRequiredService<CrewAssignmentPolicy>(), files);
            crew.Add(currentCrew);
            readiness.Add(new(tug, "CrewMinimumManning", currentCrew.MsmdId, currentCrew.MsmdRevision,
                currentCrew.State == "CurrentMinimumRoleCoverage" ? "Current" : currentCrew.State == "Blocked" ? "Blocked" : currentCrew.State == "Unavailable" ? "Unavailable" : "Unknown",
                currentCrew.Reason, ExpiresAt: currentCrew.ValidUntil));
        }
        // Aggregation is allowed only by one exact, currently qualified source policy.
        // A missing/changed tug never contributes a partial sum that looks like total capacity.
        var complete = capacity.Count > 0 && capacity.Count == plan.AssignedTugs.Length && capacity.All(c => c.State == "CurrentPlanningCapacity") &&
            capacity.Select(c => c.QualificationHash).Distinct().Count() == 1;
        var total = complete ? capacity.Sum(c => decimal.Parse(c.Tonnes!, CultureInfo.InvariantCulture)).ToString("0.######", CultureInfo.InvariantCulture) : null;
        var readinessState = readiness.Any(f => f.State == "Blocked") ? "Blocked" : "ReviewRequired";
        var operational = await TugOperationalConflicts.Read(db, actor, jobId, plan.AssignedTugs.Where(visible.Contains).ToArray(), plan.PlannedStart, plan.PlannedEnd, ct);
        if (operational.Length != 0) stateConflict = "HumanReviewRequired";
        return new(total, complete ? "CurrentPlanningCapacity" : capacity.Count == 0 ? "Missing" : "ReviewRequired", bp.ToArray(), stateConflict, conflicts.ToArray(),
            "CurrentAuthorizedOwningFacts;DraftGeometry;MasterDecisionSeparate", readinessState, capacity.ToArray(), readiness.ToArray(), operational, crew.ToArray());
    }
}
