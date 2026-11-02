using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional administrative owned-lab fixture, never automatically enabled.
public static class ComplianceInputFixtures
{
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct) {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException();
        foreach (var vessel in vessels)
            foreach (var content in new ControlEvidenceContent[] { new(ClassPack: new()), new(Msmd: new()), new(CrewRoleAssessment: new()) }) {
                var kind = ComplianceInputs.Kind(content); var id = OperationEquipmentFixtures.Id(vessel.Id, kind);
                if (await db.ControlEvidenceHeads.AnyAsync(x => x.TenantId == vessel.TenantId && x.VesselId == vessel.Id && x.Kind == "Evidence" && x.Id == id, ct)) continue;
                var json = JsonSerializer.Serialize(content with { Title = "FICTIONAL incomplete " + kind, Provenance = "SyntheticUnverified" }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                db.ControlEvidenceHeads.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, Kind = "Evidence", Id = id, Revision = 1 });
                db.ControlEvidenceRevisions.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, Kind = "Evidence", Id = id, Revision = 1, ContentJson = json, Hash = RegisterStorage.Hash(json), ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", SavedAt = DateTimeOffset.UtcNow });
                db.OperationEquipmentBindings.Add(OperationEquipmentBinding.From(vessel.TenantId, vessel.Id, id, 1, content));
            }
        // Caller owns transaction/audited SaveChanges. No files, appointments,
        // assessment outcomes, current evaluation, approval or verification seed.
    }
}
