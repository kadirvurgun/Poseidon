using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence.Seeding;

// Optional administrative add-missing fixture, never normal startup/import.
// SyntheticUnverified cannot appear in the real inbox or claim a triggered alert.
public static class NotificationFixtures
{
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException("Synthetic fixture requires administrative import context.");
        foreach (var vessel in vessels) {
            if (await db.Notifications.FindAsync([vessel.TenantId, vessel.Id], ct) != null) continue;
            if (!await db.MaintenancePlanRevisions.AnyAsync(p => p.TenantId == vessel.TenantId && p.VesselId == vessel.Id && p.PlanId == vessel.Id && p.Revision == 1, ct))
                throw new InvalidOperationException("Synthetic source revision is unavailable.");
            var now = DateTimeOffset.UtcNow;
            db.Notifications.Add(new() { TenantId = vessel.TenantId, Id = vessel.Id, VesselId = vessel.Id, PlanId = vessel.Id, PlanRevision = 1,
                RuleKey = "synthetic.fixture", PolicyVersion = "synthetic-incomplete-v1", PolicyDigest = RegisterStorage.Hash("synthetic-incomplete-notification-fixture"),
                SourceContextHash = RegisterStorage.Hash("synthetic-incomplete-source-context"),
                SourceState = "Unknown", State = "SyntheticUnverified", Severity = "Unknown", RecipientRoleId = Guid.Empty, CreatedAt = now });
            db.NotificationEvents.Add(new() { TenantId = vessel.TenantId, Id = vessel.Id, VesselId = vessel.Id, NotificationId = vessel.Id,
                Sequence = 1, Kind = "FixturePrepared", RecordedAt = now });
        }
    }
}
