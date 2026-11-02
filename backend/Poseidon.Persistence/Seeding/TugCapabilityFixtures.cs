using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

public static class TugCapabilityFixtures
{
    public static Guid Id(Guid vessel) => ControlEvidenceFixtures.Id(vessel, "TugCapability-v1");
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct) {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException();
        foreach (var v in vessels.Where(v => v.Kind == VesselKind.Tug)) {
            var id = Id(v.Id);
            if (await db.ControlEvidenceHeads.AnyAsync(h => h.VesselId == v.Id && h.Kind == "Evidence" && h.Id == id, ct)) continue;
            var content = JsonSerializer.Serialize(new ControlEvidenceContent(Title: "FICTIONAL incomplete tug capability draft", Provenance: "SyntheticUnverified",
                TugCapability: new("BollardPullTest")), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            db.ControlEvidenceHeads.Add(new() { TenantId = v.TenantId, VesselId = v.Id, Kind = "Evidence", Id = id, Revision = 1 });
            db.ControlEvidenceRevisions.Add(new() { TenantId = v.TenantId, VesselId = v.Id, Kind = "Evidence", Id = id, Revision = 1,
                ContentJson = content, Hash = RegisterStorage.Hash(content), ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", SavedAt = DateTimeOffset.UtcNow });
            db.TugCapabilityBindings.Add(new() { TenantId = v.TenantId, VesselId = v.Id, Id = id, Revision = 1 });
        }
    }
}
