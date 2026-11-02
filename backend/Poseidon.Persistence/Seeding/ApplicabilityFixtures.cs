using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Empty training authoring only. No legal rule, observed fact, evaluation,
// reviewer, evidence, receipt, appointment or signed authority is seeded.
public static class ApplicabilityFixtures
{
    public static Guid Id(Guid tenant) => new(System.Security.Cryptography.SHA256.HashData(tenant.ToByteArray().Concat("LIFECYCLE-DRAFT-v1"u8.ToArray()).ToArray()).AsSpan(0, 16));
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Tenant> tenants, CancellationToken ct)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException();
        foreach (var tenant in tenants) {
            var id = Id(tenant.Id);
            if (await db.RuleDrafts.FindAsync([tenant.Id, id], ct) != null) continue;
            db.RuleDrafts.Add(new() { TenantId = tenant.Id, Id = id, Revision = 1 });
            var revision = new RuleRevision { TenantId = tenant.Id, Id = id, Revision = 1, Contract = ApplicabilityEngine.Contract, RuleJson = null,
                Hash = "", ActorId = Guid.Empty, RoleContext = "SystemSyntheticFixture",
                ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", RequestedLifecycle = "DRAFT", SavedAt = DateTimeOffset.UtcNow };
            revision.Hash = ApplicabilityStorage.Hash(revision); db.RuleRevisions.Add(revision);
        }
    }
}
