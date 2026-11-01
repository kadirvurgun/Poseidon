using Microsoft.EntityFrameworkCore;
namespace Poseidon.Persistence.Seeding;

internal static class TechnicalFixtures
{
    internal static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct)
    {
        foreach (var v in vessels) {
            // Deterministic type-scoped IDs, add missing only. No readings, owners or completed work.
            if (await db.Assets.FindAsync([v.TenantId, v.Id], ct) == null) {
                var a = new Asset { TenantId = v.TenantId, VesselId = v.Id, Id = v.Id,
                    Code = "SYNTHETIC-UNVERIFIED", Name = "FICTIONAL equipment", Provenance = "SyntheticUnverified" };
                db.Assets.Add(a); db.AssetRevisions.Add(a.Snapshot());
            }
            if (await db.MaintenanceJobs.FindAsync([v.TenantId, v.Id, v.Id], ct) == null)
                db.MaintenanceJobs.Add(new() { TenantId = v.TenantId, VesselId = v.Id, Id = v.Id,
                    Code = "SYNTHETIC-UNVERIFIED", Title = "FICTIONAL draft job", Provenance = "SyntheticUnverified" });
            if (await db.MaintenancePlans.FindAsync([v.TenantId, v.Id], ct) == null) {
                var plan = new MaintenancePlan { TenantId = v.TenantId, VesselId = v.Id, Id = v.Id, AssetId = v.Id,
                    JobId = v.Id, Provenance = "SyntheticUnverified" };
                db.MaintenancePlans.Add(plan); db.MaintenancePlanRevisions.Add(plan.Snapshot(DateTimeOffset.UnixEpoch, 1));
            }
        }
    }
}
