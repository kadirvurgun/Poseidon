using Microsoft.EntityFrameworkCore;
namespace Poseidon.Persistence.Seeding;

internal static class TugFixtures
{
    internal static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct) {
        foreach (var v in vessels.Where(v => v.Kind == VesselKind.Tug)) {
            if (await db.TugJobs.FindAsync([v.TenantId, v.Id], ct) != null) continue;
            var job = new TugJob { TenantId = v.TenantId, VesselId = v.Id, Id = v.Id,
                JobNumber = "TUG-SYN-" + v.Id.ToString("N"), Provenance = "SyntheticUnverified", SavedAt = DateTimeOffset.UnixEpoch };
            // All operational requirements, owners, measurements and assignments deliberately absent.
            db.TugJobs.Add(job); db.TugJobRevisions.Add(job.Snapshot());
        }
    }
}
