using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S7 WP-A DEV-049 optional administrative fixture (not startup data, not normal seed).
// Adds two FICTIONAL matrix rows to the synthetic crew position of each vessel. It never
// seeds a credential, verification assertion, competence record, sign-off, Safe Manning
// decision or appointment, so every synthetic evaluation stays NOT_ELIGIBLE or
// REVIEW_REQUIRED. Add-missing only: operator edits and existing rows are never touched.
public static class CrewCompetenceFixtures
{
    public static Guid Id(Guid vessel, string slot) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.crew-competence.v1:" + vessel.ToString("D") + ":" + slot)).AsSpan(0, 16));
    public static readonly (string Slot, string Type, string Reference)[] Rows = [
        ("certificate", CrewRequirementTypes.Certificate, "FICTIONAL-STCW-REFERENCE"),
        ("familiarization", CrewRequirementTypes.Familiarization, "FICTIONAL-SHIP-FAMILIARIZATION")];

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic crew competence rows require an administrative audited transaction.");
        foreach (var vessel in vessels)
        {
            // The synthetic crew position shares the vessel id (CrewFixtures); absent position means nothing to attach.
            if (!await db.CrewPositions.AnyAsync(p => p.TenantId == vessel.TenantId && p.VesselId == vessel.Id && p.Id == vessel.Id, ct)) continue;
            foreach (var (slot, type, reference) in Rows)
            {
                var id = Id(vessel.Id, slot);
                if (await db.CrewCompetenceRequirements.AnyAsync(r => r.TenantId == vessel.TenantId && r.Id == id, ct)) continue;
                if (await db.CrewCompetenceRequirements.AnyAsync(r => r.TenantId == vessel.TenantId && r.VesselId == vessel.Id && r.PositionId == vessel.Id &&
                    r.RequirementType == type && r.Reference == reference, ct)) continue;
                db.CrewCompetenceRequirements.Add(new() { TenantId = vessel.TenantId, Id = id, VesselId = vessel.Id, PositionId = vessel.Id,
                    RequirementType = type, Reference = reference, Mandatory = true, Provenance = "SyntheticUnverified", ActorId = Guid.Empty, SavedAt = db.Clock.GetUtcNow() });
                await db.SaveChangesAsync(ct);
            }
        }
        // The caller commits.
    }
}
