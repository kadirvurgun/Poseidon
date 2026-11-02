using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record MaintenanceCurrentProjection(MaintenanceRow Row, long? CurrentAssetRevision, bool JobAvailable, bool OwnerEligible);

internal static class MaintenanceCurrentReader
{
    internal static bool Permitted(TrustedActor actor) => actor.CanRead && actor.Permissions.Contains("maintenance.read") && actor.Permissions.Contains("equipment.read");

    internal static async Task<MaintenanceCurrentProjection[]?> Read(Guid vessel, HttpContext http, PoseidonDbContext db,
        TrustedActor actor, DateTimeOffset readAt, Guid? planId = null)
    {
        // The same owning-module gate protects direct reads AND cross-module projections.
        if (!Permitted(actor) || !await actor.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, http.RequestAborted)) return null;
        var owners = TechnicalEndpoints.EligibleOwners(http, vessel).ToHashSet();
        var rows = await (from p in db.MaintenancePlans.AsNoTracking().Where(x => x.VesselId == vessel && (planId == null || x.Id == planId))
            join r in db.MaintenancePlanRevisions.AsNoTracking() on new { p.TenantId, p.VesselId, PlanId = p.Id, p.Revision } equals new { r.TenantId, r.VesselId, r.PlanId, r.Revision }
            orderby p.Id select new { p.Id, p.AssetId, p.JobId, p.OwnerRoleId, p.Version, p.Revision, p.Status, p.Provenance,
                r.ContentJson, r.SavedAt, r.AssetRevision,
                CurrentAssetRevision = db.Assets.Where(a => a.VesselId == vessel && a.Id == p.AssetId).Select(a => (long?)a.Revision).SingleOrDefault(),
                JobAvailable = p.JobId != null && db.MaintenanceJobs.Any(j => j.VesselId == vessel && j.Id == p.JobId) }).ToArrayAsync(http.RequestAborted);
        var result = new List<MaintenanceCurrentProjection>();
        foreach (var p in rows) {
            var inputs = JsonSerializer.Deserialize<MaintenanceInputs>(p.ContentJson)!;
            var observation = inputs.ObservationId == null ? null : await MaintenanceObservationEndpoints.Resolve(vessel, p.AssetId, inputs, http, db, readAt);
            var ownerEligible = p.OwnerRoleId != null && owners.Contains(p.OwnerRoleId.Value);
            var due = MaintenancePlanningPolicy.Evaluate(inputs, readAt,
                new(p.JobAvailable, ownerEligible, p.CurrentAssetRevision == p.AssetRevision), observation);
            result.Add(new(new(p.Id, p.AssetId, p.JobId, p.OwnerRoleId, p.Version, p.Revision, p.Status, p.Provenance, inputs,
                due, readAt, p.SavedAt, p.AssetRevision, observation?.State ?? "NotRequested", observation == null ? null : readAt),
                p.CurrentAssetRevision, p.JobAvailable, ownerEligible));
        }
        return result.ToArray();
    }
}
