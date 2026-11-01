using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Explicit fictional records; no law text, approval, credentials, or human signature.
public static class RegulatoryRegisterFixtures
{
    public static Guid SourceId(Guid tenant) => FixtureId(tenant, 1);
    public static Guid RequirementId(Guid tenant) => FixtureId(tenant, 2);
    private static Guid FixtureId(Guid tenant, byte suffix)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(tenant.ToByteArray().Concat(new byte[] { 82, 69, 71, suffix }).ToArray());
        return new Guid(bytes.AsSpan(0, 16));
    }
    // Caller holds migration/import lock and the audited import transaction.
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Tenant> tenants, CancellationToken token)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException();
        foreach (var tenant in tenants)
        {
            var sourceId = SourceId(tenant.Id);
            foreach (var kind in new[] { "Source", "Requirement" })
            {
                var id = kind == "Source" ? sourceId : RequirementId(tenant.Id);
                if (await db.RegisterDrafts.FindAsync([tenant.Id, kind, id], token) != null) continue;
                var content = kind == "Source" ? new RegisterContent { Title = new("Known", "FICTIONAL training source — unverified"),
                    Locator = new("Known", "synthetic-demo/reg-source-draft-v1"), Declaration = new("Unknown"), ObservedAt = new("Unknown") }
                    : new RegisterContent { Instrument = new("Known", "FICTIONAL preparation example"), Declaration = new("Unknown"), ObservedAt = new("Unknown") };
                var draft = new RegisterDraft { TenantId = tenant.Id, Id = id, Kind = kind, ContentJson = RegisterStorage.Serialize(content),
                    Revision = 1, SavedAt = DateTimeOffset.UtcNow, Provenance = "SyntheticUnverified" };
                db.RegisterDrafts.Add(draft);
                db.RegisterRevisions.Add(draft.Snapshot(kind == "Source" ? null : sourceId, kind == "Source" ? null : 1));
            }
        }
    }
}
