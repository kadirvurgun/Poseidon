using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S7 WP-B DEV-085 synthetic tow equipment declarations (docs/seed-data.md): one FICTIONAL record per kind on
// each seeded tug, condition Unknown, criticality unassessed, no SWL/test dates/PMS/certificate links and NO
// Chief Engineer verification, Master acknowledgement or GO/NO_GO record. Every item therefore stays UNKNOWN
// (REVIEW_REQUIRED). Ids are deterministic so a re-import adds nothing.
internal static class TowEquipmentFixtures
{
    internal static Guid Id(Guid tug, string kind) {
        var bytes = tug.ToByteArray(); var k = System.Text.Encoding.ASCII.GetBytes(kind);
        for (var i = 0; i < k.Length; i++) bytes[i % 16] ^= k[i];
        bytes[15] ^= 0x5B; return new Guid(bytes);
    }
    internal static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct) {
        foreach (var v in vessels.Where(v => v.Kind == VesselKind.Tug))
        foreach (var kind in TowEquipmentPolicy.Kinds) {
            var id = Id(v.Id, kind);
            if (await db.TowEquipmentItems.FindAsync([v.TenantId, id], ct) != null) continue;
            var item = new TowEquipmentItem { TenantId = v.TenantId, VesselId = v.Id, Id = id, Kind = kind, Label = "FICTIONAL " + kind,
                Condition = TowEquipmentPolicy.ConditionUnknown, Provenance = "SyntheticUnverified", SavedAt = DateTimeOffset.UnixEpoch, ActorId = Guid.Empty };
            db.TowEquipmentItems.Add(item); db.TowEquipmentRevisions.Add(item.Snapshot());
        }
    }
}
