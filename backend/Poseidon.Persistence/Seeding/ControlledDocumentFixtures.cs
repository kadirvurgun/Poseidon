using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Persistence.Seeding;

// PLAN-1511-5802 explicit optional fixture: one FICTIONAL empty controlled-document shell per
// vessel with a single synthetic CREATED custody event. Never wired into normal startup/seed.
// No file, revision, release, publication, verification, hold, retention policy, disposal
// decision or domain link is invented; retention therefore stays UNCONFIGURED (disposal closed).
public static class ControlledDocumentFixtures
{
    public static Guid Id(Guid vessel) => new(SHA256.HashData(vessel.ToByteArray().Concat(Encoding.UTF8.GetBytes("FICTIONAL-CONTROLLED-DOCUMENT-SHELL-v1")).ToArray()).AsSpan(0, 16));

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException("Controlled document fixtures require administrative import.");
        foreach (var vessel in vessels)
        {
            var id = Id(vessel.Id);
            if (db.ControlledDocuments.Local.Any(x => x.TenantId == vessel.TenantId && x.Id == id) ||
                await db.ControlledDocuments.FindAsync([vessel.TenantId, id], ct) is not null) continue;
            var now = DateTimeOffset.UtcNow;
            db.ControlledDocuments.Add(new() { TenantId = vessel.TenantId, Id = id, VesselId = vessel.Id,
                Code = "FICTIONAL-" + id.ToString("N")[..12].ToUpperInvariant(), Title = "FICTIONAL controlled document shell (no content)",
                Classification = "INTERNAL", Sequence = 1, CreatedBy = db.AuditActorId, CreatedAt = now, Provenance = "SyntheticUnverified" });
            db.ControlledDocumentCustodyEvents.Add(new() { TenantId = vessel.TenantId, DocumentId = id, Sequence = 1, VesselId = vessel.Id,
                EventType = "CREATED", RecordedBy = db.AuditActorId, OccurredAt = now, RecordedAt = now, Source = "SYNTHETIC_SEED" });
        }
        // Caller owns the audited SaveChanges/transaction.
    }
}
