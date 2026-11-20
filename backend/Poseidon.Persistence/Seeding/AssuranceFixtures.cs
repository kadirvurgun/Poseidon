using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S10 WP-A DEV-113 optional administrative fixture (docs/seed-data.md). Not wired into the default seed.
// Per seeded SHIP vessel: one FICTIONAL empty DRAFT SIRE 2.0 inspection header (SyntheticUnverified, applicability
// REVIEW_REQUIRED because the synthetic vessel declares no ship type). No mapping, import, item, finding, evidence,
// acceptance, action link or audit pack is seeded; DB checks keep findings OPEN/UNVERIFIED. Add-missing; edits kept.
public static class AssuranceFixtures
{
    private static Guid Id(string kind, Guid scope) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.assurance." + kind + ".v1:" + scope.ToString("D"))).AsSpan(0, 16));
    public static Guid InspectionId(Guid vessel) => Id("inspection", vessel);
    public static Guid SyntheticAuthor(Guid tenant) => Id("author", tenant);

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic assurance fixtures require an administrative audited transaction.");
        var now = DateTimeOffset.UtcNow;
        foreach (var v in vessels.Where(v => v.Kind == VesselKind.Ship))
        {
            var id = InspectionId(v.Id);
            if (await db.AssuranceInspections.AnyAsync(x => x.TenantId == v.TenantId && x.Id == id, ct)) continue;
            db.AssuranceInspections.Add(new AssuranceInspection { TenantId = v.TenantId, VesselId = v.Id, Id = id, ReferenceNo = "FICTIONAL-SIRE-" + id.ToString("N")[..8].ToUpperInvariant(),
                InspectionDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-7)), Classification = AssurancePolicy.Unknown, Applicability = AssurancePolicy.ReviewRequired,
                Provenance = "SyntheticUnverified", CreatedBy = SyntheticAuthor(v.TenantId), CreatedAt = now, SavedAt = now });
            await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
