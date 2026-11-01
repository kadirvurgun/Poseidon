using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TugBpEvidence(Guid TugId, string State, string? DeclaredValue, string? Source, string? PolicyVersion,
    long? SourceRevision, Guid? ObservationId, DateTimeOffset? ObservedAt, string Freshness, string Provenance);
public sealed record TugPlanConflict(Guid JobId, long Revision, string Relation, string Source, DateTimeOffset ObservedAt);
public sealed record TugPlanningEvidence(string? AvailableBollardPull, string CapacityState, TugBpEvidence[] Bp,
    string ConflictState, TugPlanConflict[] Conflicts, string Coverage, string Readiness);

public static class TugPlanningEvidenceAdapter
{
    public static async Task<TugPlanningEvidence> Read(PoseidonDbContext db, TrustedActor actor, TugPlanningPolicy policy, MaintenanceObservationSettings observationSettings,
        Guid jobId, TugPlanInputs plan, CancellationToken ct) {
        var now = DateTimeOffset.UtcNow; var bp = new List<TugBpEvidence>();
        var visible = await actor.Scope(db.Vessels).Where(v => v.Kind == VesselKind.Tug).Select(v => v.Id).ToArrayAsync(ct);
        foreach (var tug in plan.AssignedTugs.Where(visible.Contains)) {
            var source = policy.Current(now) ? policy.BpSources.SingleOrDefault(s => s.TenantId == actor.TenantId && s.VesselId == tug) : null;
            if (!actor.Permissions.Contains("equipment.read") || !actor.Permissions.Contains("maintenance.read") || !policy.Current(now) || source == null || source.MaxAgeSeconds is < 1 or > 31536000) {
                bp.Add(new(tug, "Unavailable", null, null, null, null, null, null, "Unavailable", "Unavailable")); continue;
            }
            var rule = observationSettings.Rule(actor.TenantId, tug, source.AssetId, source.Source);
            if (rule == null || rule.Version != source.PolicyVersion || rule.Kind != "Meter" || rule.Unit != "tonnes") {
                bp.Add(new(tug, "Unavailable", null, null, null, null, null, null, "Unavailable", "Unavailable")); continue;
            }
            var observation = await db.MaintenanceObservations.AsNoTracking().Where(o => o.VesselId == tug && o.AssetId == source.AssetId && o.Source == source.Source)
                .OrderByDescending(o => o.SourceRevision).FirstOrDefaultAsync(ct);
            if (observation == null) { bp.Add(new(tug, "Missing", null, source.Source, source.PolicyVersion, null, null, null, "Unknown", "Missing")); continue; }
            var revoked = await db.MaintenanceObservationRevocations.AnyAsync(r => r.ObservationId == observation.Id, ct);
            var freshness = observation.ObservedAt == null ? "Unknown" : observation.ObservedAt > now ? "Future" : now - observation.ObservedAt > TimeSpan.FromSeconds(source.MaxAgeSeconds) ? "Stale" : "Current";
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
        return new(null, bp.Count == 0 ? "Missing" : bp.Any(b => b.State == "Unavailable") ? "Unavailable" : bp.Select(b => b.State).Distinct().Count() == 1 ? bp[0].State : "UnverifiedOrIncomplete", bp.ToArray(), stateConflict, conflicts.ToArray(),
            "CurrentVisibleDraftPlansOnly;OperationalEvidenceUnavailable;NoSafetyClearance", "Unknown");
    }
}
