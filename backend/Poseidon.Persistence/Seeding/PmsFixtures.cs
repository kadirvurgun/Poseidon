using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence.Seeding;

// DEV-108 optional administrative fixture (not part of the default seed, so default seed audit
// counts stay exact). Per vessel one FICTIONAL draft running-hours measurement point on the
// synthetic seed asset. No reading, threshold, postponement, decision or receipt is seeded:
// every band stays UNKNOWN/UNCONFIGURED and every human gate stays open. Add-missing only.
public static class PmsFixtures
{
    public static Guid PointId(Guid vessel) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.pms.point.v1:" + vessel.ToString("D"))).AsSpan(0, 16));

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic PMS fixtures require an administrative audited transaction.");
        var now = db.Clock.GetUtcNow();
        foreach (var vessel in vessels)
        {
            if (!await db.Assets.AnyAsync(a => a.TenantId == vessel.TenantId && a.VesselId == vessel.Id && a.Id == vessel.Id, ct)) continue;
            var id = PointId(vessel.Id);
            if (await db.MeasurementPoints.AnyAsync(p => p.TenantId == vessel.TenantId && p.Id == id, ct)) continue;
            var point = new MeasurementPoint { TenantId = vessel.TenantId, VesselId = vessel.Id, Id = id, AssetId = vessel.Id,
                Code = "FICTIONAL-RH-" + id.ToString("N")[..8].ToUpperInvariant(), Kind = "RUNNING_HOURS", Unit = "HOUR", Source = "MANUAL", Precision = 1,
                Revision = 1, SavedAt = now, Provenance = "SyntheticUnverified" };
            db.MeasurementPoints.Add(point); await db.SaveChangesAsync(ct);
            db.MeasurementPointRevisions.Add(point.Snapshot()); await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
