using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence.Seeding;

internal static class CrewFixtures
{
    internal static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct)
    {
        foreach (var vessel in vessels) {
            // IDs are deterministic per entity type; no real people, dates, credentials or appointments.
            if (await db.Persons.FindAsync([vessel.TenantId, vessel.Id], ct) == null)
                db.Persons.Add(new() { TenantId = vessel.TenantId, Id = vessel.Id, Provenance = "SyntheticUnverified" });
            if (await db.CrewPersonalProfiles.FindAsync([vessel.TenantId, vessel.Id], ct) == null) {
                db.CrewPersonalProfiles.Add(new() { TenantId = vessel.TenantId, PersonId = vessel.Id });
                db.CrewPersonalProfileRevisions.Add(new() { TenantId = vessel.TenantId, PersonId = vessel.Id, Revision = 1,
                    PolicyVersion = "synthetic-empty-v1", Provenance = "SyntheticUnverified", SavedAt = DateTimeOffset.UtcNow });
            }
            if (await db.CrewPositions.FindAsync([vessel.TenantId, vessel.Id], ct) == null)
                db.CrewPositions.Add(new() { TenantId = vessel.TenantId, Id = vessel.Id, VesselId = vessel.Id,
                    Code = "SYNTHETIC-UNVERIFIED", Provenance = "SyntheticUnverified" });
            if (await db.CrewAssignments.FindAsync([vessel.TenantId, vessel.Id], ct) != null) continue;
            var draft = new CrewAssignment { TenantId = vessel.TenantId, Id = vessel.Id, VesselId = vessel.Id,
                PersonId = vessel.Id, PositionId = vessel.Id, Provenance = "SyntheticUnverified" };
            db.CrewAssignments.Add(draft); db.CrewAssignmentRevisions.Add(draft.Snapshot());
        }
    }
}
