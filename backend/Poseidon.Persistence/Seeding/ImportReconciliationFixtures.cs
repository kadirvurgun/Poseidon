using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S11 WP-A DEV-070 optional administrative fixture (docs/seed-data.md). Not wired into the default seed.
// Per synthetic vessel without a current report: one FICTIONAL, empty reconciliation report AWAITING_SIGN_OFF (no pinned
// batches, all counts zero, no sample). No import provenance, no data owner sign-off and no receipt is ever seeded; the
// DB CHECK rejects a synthetic report that carries a sign-off. Add-missing; operator reports are kept.
public static class ImportReconciliationFixtures
{
    public static Guid ReportId(Guid vessel) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.reconciliation.v1:" + vessel.ToString("D"))).AsSpan(0, 16));

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic reconciliation fixtures require an administrative audited transaction.");
        var now = DateTimeOffset.UtcNow;
        foreach (var v in vessels)
        {
            var id = ReportId(v.Id);
            if (await db.ReconciliationReports.AnyAsync(r => r.TenantId == v.TenantId && (r.Id == id || r.VesselId == v.Id && r.SupersededAt == null), ct)) continue;
            var counts = ImportReconciliationPolicy.Count([], 0); var seed = ImportReconciliationPolicy.Seed(id);
            db.ReconciliationReports.Add(new() { TenantId = v.TenantId, VesselId = v.Id, Id = id, SampleSeed = seed, CreatedAt = now, Provenance = "SyntheticUnverified",
                Hash = ImportReconciliationPolicy.Hash(v.Id, id, [], counts, [], [], seed) });
            await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
