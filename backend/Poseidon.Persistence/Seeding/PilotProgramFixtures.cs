using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Pilot program kit optional administrative fixture (docs/seed-data.md). Not wired into the default seed. Per tenant
// with a ship vessel: one FICTIONAL program header (first ship + first tug by code, start 2026-12-01, UTC) with
// provenance SyntheticUnverified, created by a deterministic synthetic actor without membership. No checklist link,
// feedback, issue, measurement, week close, program decision, appointment or receipt is seeded; the API refuses week
// close and decisions on a synthetic program. Add-missing; operator edits are kept.
public static class PilotProgramFixtures
{
    private static Guid Id(string kind, Guid scope) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.pilot-program." + kind + ".v1:" + scope.ToString("D"))).AsSpan(0, 16));
    public static Guid ProgramId(Guid tenant) => Id("program", tenant);
    public static Guid SyntheticCreator(Guid tenant) => Id("creator", tenant);

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Guid> tenants, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic pilot program fixtures require an administrative audited transaction.");
        foreach (var tenant in tenants.Distinct())
        {
            var id = ProgramId(tenant);
            if (await db.PilotPrograms.AnyAsync(p => p.TenantId == tenant && p.Id == id, ct)) continue;
            var ship = await db.Vessels.Where(v => v.TenantId == tenant && v.Kind == VesselKind.Ship).OrderBy(v => v.Code).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(ct);
            if (ship == null) continue;
            var tug = await db.Vessels.Where(v => v.TenantId == tenant && v.Kind == VesselKind.Tug).OrderBy(v => v.Code).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(ct);
            db.PilotPrograms.Add(new PilotProgram { TenantId = tenant, Id = id, Name = "FICTIONAL pilot program (synthetic, not a real pilot)", ShipVesselId = ship.Value,
                TugVesselId = tug, StartDate = new DateOnly(2026, 12, 1), TimeZone = "UTC", CatalogVersion = PilotProgramCatalog.Version,
                CreatedBy = SyntheticCreator(tenant), CreatedAt = db.Clock.GetUtcNow(), Provenance = "SyntheticUnverified" });
            await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
