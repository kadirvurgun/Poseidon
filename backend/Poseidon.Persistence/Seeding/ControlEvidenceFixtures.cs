using System.Text.Json;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

public static class ControlEvidenceFixtures
{
    public static Guid Id(Guid vessel, string kind) => new(System.Security.Cryptography.SHA256.HashData(vessel.ToByteArray().Concat(System.Text.Encoding.UTF8.GetBytes("CONTROL-EVIDENCE-DRAFT-v1/" + kind)).ToArray()).AsSpan(0, 16));
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException();
        foreach (var vessel in vessels)
        foreach (var kind in new[] { "Control", "Evidence" }) {
            var id = Id(vessel.Id, kind);
            if (await db.ControlEvidenceHeads.FindAsync([vessel.TenantId, vessel.Id, kind, id], ct) != null) continue;
            var content = JsonSerializer.Serialize(new ControlEvidenceContent(Title: "FICTIONAL incomplete " + kind + " draft", Provenance: "SyntheticUnverified"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            db.ControlEvidenceHeads.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, Kind = kind, Id = id, Revision = 1 });
            db.ControlEvidenceRevisions.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, Kind = kind, Id = id, Revision = 1,
                ContentJson = content, Hash = RegisterStorage.Hash(content), ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", SavedAt = DateTimeOffset.UtcNow });
        }
        // No file, observed time, actor appointment, evidence existence, verification,
        // completed control, policy, receipt or human decision is invented.
    }
}
