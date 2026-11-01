using Microsoft.EntityFrameworkCore;
namespace Poseidon.Persistence.Seeding;

internal static class TugFixtures
{
    internal static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct) {
        foreach (var v in vessels.Where(v => v.Kind == VesselKind.Tug)) {
            var job = await db.TugJobs.FindAsync([v.TenantId, v.Id], ct);
            if (job == null) {
            job = new TugJob { TenantId = v.TenantId, VesselId = v.Id, Id = v.Id,
                JobNumber = "TUG-SYN-" + v.Id.ToString("N"), Provenance = "SyntheticUnverified", SavedAt = DateTimeOffset.UnixEpoch };
            // All operational requirements, owners, measurements and assignments deliberately absent.
            db.TugJobs.Add(job); db.TugJobRevisions.Add(job.Snapshot());
            }
            if (await db.TugDraftPlans.FindAsync([v.TenantId, v.Id, job.Id], ct) != null) continue;
            db.TugDraftPlans.Add(new() { TenantId = v.TenantId, VesselId = v.Id, JobId = job.Id, RequestRevision = job.Revision,
                PolicyVersion = "SyntheticUnverified", SavedAt = DateTimeOffset.UnixEpoch });
            db.TugDraftPlanRevisions.Add(new() { TenantId = v.TenantId, VesselId = v.Id, JobId = job.Id, Revision = 1, RequestRevision = job.Revision,
                PolicyVersion = "SyntheticUnverified", Provenance = "SyntheticUnverified", SavedAt = DateTimeOffset.UnixEpoch,
                ContentJson = System.Text.Json.JsonSerializer.Serialize(new Poseidon.Domain.TugPlanInputs(null, null, [])) });
        }
    }
}
