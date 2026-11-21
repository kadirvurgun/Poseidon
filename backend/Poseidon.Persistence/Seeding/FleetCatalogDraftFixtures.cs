using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence.Seeding;

// Explicit optional fixture, never enabled by normal startup or normal seed import.
public static class FleetCatalogDraftFixtures
{
    public static Guid Id(Guid tenant) => new(SHA256.HashData(tenant.ToByteArray().Concat(Encoding.UTF8.GetBytes("FICTIONAL-FLEET-CATALOG-DRAFT-v1")).ToArray()).AsSpan(0, 16));
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Tenant> tenants, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException("Catalog fixtures require administrative import.");
        foreach (var tenant in tenants)
        {
            var id = Id(tenant.Id);
            if (await db.FleetCatalogDrafts.FindAsync([tenant.Id, id], ct) is not null) continue;
            var json = FleetCatalogDraftWriter.Serialize(new("FICTIONAL incomplete type catalog draft", []));
            db.FleetCatalogDrafts.Add(new() { TenantId = tenant.Id, Id = id, Revision = 1 });
            db.FleetCatalogDraftRevisions.Add(new() { TenantId = tenant.Id, Id = Id(id), CatalogId = id, Revision = 1,
                ExpectedRevision = 0, ActorId = db.AuditActorId, ContentJson = json, ContentHash = FleetCatalogDraftWriter.Hash(json),
                IntentHash = FleetCatalogDraftWriter.Hash("SYNTHETIC_INCOMPLETE_DRAFT"), RecordedAt = db.Clock.GetUtcNow(),
                Provenance = "SyntheticUnverified" });
        }
        // Caller owns audited SaveChanges/transaction; no members, approvals,
        // schema bindings, accepted selectors, or vessel-type assignments are seeded.
    }
}
