using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S8 WP-D DEV-087/088 optional administrative fixture (docs/seed-data.md). Not wired into the default seed.
// Per seeded synthetic tug job: one FICTIONAL towage plan DRAFT with no tug roles, limits, channels,
// emergency release, contingency or snap-back control, and one FICTIONAL briefing DRAFT with no
// conditions, hazards, barriers, risk link or participants. No submission, approval, issue,
// acknowledgement or receipt is seeded; DB checks reject a synthetic non-DRAFT row. Readiness therefore
// keeps communication UNKNOWN and briefing FAIL. Add-missing; operator edits are kept.
public static class TowageFixtures
{
    private static Guid Id(string kind, Guid scope) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.towage." + kind + ".v1:" + scope.ToString("D"))).AsSpan(0, 16));
    public static Guid PlanId(Guid job) => Id("plan", job);
    public static Guid BriefingId(Guid job) => Id("briefing", job);

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic towage fixtures require an administrative audited transaction.");
        var now = DateTimeOffset.UtcNow;
        foreach (var v in vessels.Where(v => v.Kind == VesselKind.Tug))
        {
            var job = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.TenantId == v.TenantId && j.VesselId == v.Id && j.Id == v.Id && j.Provenance == "SyntheticUnverified", ct);
            if (job == null) continue;
            if (!await db.TowagePlans.AnyAsync(p => p.TenantId == v.TenantId && p.VesselId == v.Id && p.JobId == job.Id, ct))
            {
                var content = new TowagePlanContent([], null, [], new(null, null, null, null), new(null, null), [], new(null, []));
                if (!TowagePlanPolicy.ValidDraft(content)) throw new InvalidOperationException("Synthetic towage plan schema unavailable.");
                var plan = new TowagePlan { TenantId = v.TenantId, VesselId = v.Id, Id = PlanId(job.Id), JobId = job.Id, JobRevision = job.Revision,
                    ContentJson = TowageBriefingPolicy.ContentJson(content), SavedAt = now, Provenance = "SyntheticUnverified" };
                plan.Hash = TowagePlanPolicy.Hash("towage-plan", plan.VesselId, plan.Id, plan.Revision, plan.State, plan.JobRevision, plan.ContentJson);
                db.TowagePlans.Add(plan); await db.SaveChangesAsync(ct);
                db.TowagePlanRevisions.Add(new() { TenantId = plan.TenantId, VesselId = plan.VesselId, PlanId = plan.Id, Revision = 1, JobRevision = plan.JobRevision,
                    State = TowagePlanPolicy.Draft, ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", ContentJson = plan.ContentJson, Hash = plan.Hash, SavedAt = now, Provenance = "SyntheticUnverified" });
                await db.SaveChangesAsync(ct);
            }
            if (!await db.TowageBriefings.AnyAsync(b => b.TenantId == v.TenantId && b.VesselId == v.Id && b.JobId == job.Id, ct))
            {
                var content = new TowageBriefingContent(null, null, null, [], [], [], null, [], null, null, []);
                if (!TowageBriefingPolicy.ValidDraft(content)) throw new InvalidOperationException("Synthetic towage briefing schema unavailable.");
                var b = new TowageBriefing { TenantId = v.TenantId, VesselId = v.Id, Id = BriefingId(job.Id), JobId = job.Id,
                    ContentJson = TowageBriefingPolicy.ContentJson(content), SavedAt = now, Provenance = "SyntheticUnverified" };
                b.Hash = TowagePlanPolicy.Hash("towage-briefing", b.VesselId, b.Id, b.Revision, b.State, 0, b.ContentJson + "|0");
                db.TowageBriefings.Add(b); await db.SaveChangesAsync(ct);
                db.TowageBriefingRevisions.Add(new() { TenantId = b.TenantId, VesselId = b.VesselId, BriefingId = b.Id, Revision = 1, State = TowageBriefingPolicy.Draft,
                    ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", ContentJson = b.ContentJson, Hash = b.Hash, SavedAt = now, Provenance = "SyntheticUnverified" });
                await db.SaveChangesAsync(ct);
            }
        }
        // Caller commits.
    }
}
