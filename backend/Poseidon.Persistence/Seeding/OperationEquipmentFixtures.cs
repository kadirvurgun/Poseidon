using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional incomplete synthetic fixture for the owned bundle lab. Not startup data,
// authorization, a production catalog or evidence verification. Add-missing only.
public static class OperationEquipmentFixtures
{
    public static Guid Id(Guid vessel, string kind) => new(System.Security.Cryptography.SHA256.HashData(vessel.ToByteArray().Concat(System.Text.Encoding.UTF8.GetBytes("OWNING-INPUT-DRAFT-v1/" + kind)).ToArray()).AsSpan(0, 16));
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct) {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException();
        foreach (var vessel in vessels) {
            var contents = new List<ControlEvidenceContent> { new(CargoPlan: new()), new(WinchFitment: new()) };
            if (vessel.Kind == VesselKind.Tug) contents.AddRange([new(Operation: new()), new(ServiceAuthorization: new())]);
            foreach (var content in contents) {
                var kind = OperationEquipmentInputs.Kind(content); var id = Id(vessel.Id, kind);
                if (await db.ControlEvidenceHeads.AnyAsync(h => h.TenantId == vessel.TenantId && h.VesselId == vessel.Id && h.Kind == "Evidence" && h.Id == id, ct)) continue;
                var json = JsonSerializer.Serialize(content with { Title = "FICTIONAL incomplete " + kind, Provenance = "SyntheticUnverified" }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                db.ControlEvidenceHeads.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, Kind = "Evidence", Id = id, Revision = 1 });
                db.ControlEvidenceRevisions.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, Kind = "Evidence", Id = id, Revision = 1, ContentJson = json, Hash = RegisterStorage.Hash(json), ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", SavedAt = DateTimeOffset.UtcNow });
                db.OperationEquipmentBindings.Add(OperationEquipmentBinding.From(vessel.TenantId, vessel.Id, id, 1, content));
            }
        }
        // Caller uses the importer transaction and attributed SaveChanges; no receipt,
        // file, appointed actor, job/plan, fitment, cargo truth or approval is invented.
    }
}
