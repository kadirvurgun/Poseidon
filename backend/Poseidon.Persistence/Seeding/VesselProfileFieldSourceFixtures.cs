using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Explicit optional administrative fixture (like FleetCatalogDraftFixtures); never enabled by
// normal startup or normal seed import. One incomplete SyntheticUnverified declaration per
// untouched synthetic profile: source UNKNOWN, reference/observation Unknown. It is never a
// declared, verified or fresh source and never annotates an operator-edited profile.
public static class VesselProfileFieldSourceFixtures
{
    public const string Field = "imo";
    public static async Task<int> AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException("Profile field fixtures require administrative import.");
        var added = 0;
        foreach (var vessel in vessels)
        {
            var draft = await db.RegulatoryDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == vessel.TenantId && x.VesselId == vessel.Id, ct);
            // Operator edits (any later revision) and existing provenance are preserved.
            if (draft is not { Revision: 1, Provenance: "Synthetic" }) continue;
            if (await db.VesselProfileFieldSources.AnyAsync(x => x.TenantId == vessel.TenantId && x.VesselId == vessel.Id, ct)) continue;
            if (db.VesselProfileFieldSources.Local.Any(x => x.TenantId == vessel.TenantId && x.VesselId == vessel.Id)) continue;
            var (state, value) = VesselProfileFieldPolicy.Value(RegulatoryDraftStorage.Deserialize(draft.ContentJson), Field);
            db.VesselProfileFieldSources.Add(new()
            {
                TenantId = vessel.TenantId, VesselId = vessel.Id, FieldPath = Field, Sequence = 1, ValueRevision = 1,
                ValueState = state, ValueHash = VesselProfileFieldPolicy.ValueHash(state, value), SourceKind = "UNKNOWN",
                ReferenceState = "Unknown", ObservedAtState = "Unknown", DeclaredBy = Guid.Empty, StewardCompanyId = vessel.CompanyId,
                RecordedAt = VesselProfileFieldPolicy.Truncate(DateTimeOffset.UtcNow), Provenance = "SyntheticUnverified"
            });
            added++;
        }
        // Caller owns the audited SaveChanges/transaction.
        return added;
    }
}
