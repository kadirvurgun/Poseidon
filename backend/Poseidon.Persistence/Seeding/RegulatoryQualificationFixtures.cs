using System.Text.Json;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

public static class RegulatoryQualificationFixtures
{
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Tenant> tenants, CancellationToken ct)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException();
        foreach (var tenant in tenants) {
            var rule = ApplicabilityFixtures.Id(tenant.Id);
            if (await db.RegulatoryQualificationHeads.FindAsync([tenant.Id, rule], ct) != null) continue;
            db.RegulatoryQualificationHeads.Add(new() { TenantId = tenant.Id, RuleId = rule, Revision = 1 });
            // Explicitly incomplete: no source bytes, appointed owner/reviewer, input,
            // attestation or qualified policy. These empty IDs are absence, never authority.
            var json = JsonSerializer.Serialize(new QualificationContent(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            db.RegulatoryQualificationEvents.Add(new() { TenantId = tenant.Id, RuleId = rule, Revision = 1, RuleRevision = 1, State = "DRAFT",
                ContentJson = json, SourceHash = "", Hash = RegisterStorage.Hash(JsonSerializer.Serialize(new { tenant.Id, rule, revision = 1, json })), AuthorId = Guid.Empty, ActorId = Guid.Empty,
                AuthorityId = Guid.Empty, RoleId = Guid.Empty, AuthorityHash = "", ContractJson = "null", PolicyVersion = RegulatoryQualificationPolicy.Version, SavedAt = DateTimeOffset.UtcNow });
        }
    }
}
