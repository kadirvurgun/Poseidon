using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// PLAN-1511-5401 explicit optional fixture, never enabled by normal startup or normal seed import.
// One FICTIONAL pending INHERIT proposal per tenant for rpt.export.rows: it carries no value, is
// SyntheticUnverified, has no author and can never be approved (database and domain guards).
// No approved value, decision, deployment acceptance or owner binding is seeded.
public static class ParameterFixtures
{
    public const string Key = "rpt.export.rows";
    private static Guid Derive(Guid tenant, string label) =>
        new(SHA256.HashData(tenant.ToByteArray().Concat(Encoding.UTF8.GetBytes(label)).ToArray()).AsSpan(0, 16));
    public static Guid RootId(Guid tenant) => Derive(tenant, "FICTIONAL-PARAMETER-ROOT-v1");
    public static Guid RevisionId(Guid tenant) => Derive(tenant, "FICTIONAL-PARAMETER-REVISION-v1");

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Tenant> tenants, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException("Parameter fixtures require administrative import.");
        var definition = ParameterRegistry.Find(Key)!;
        var selector = ParameterValuePolicy.Selectors(definition, null)!.Value;
        foreach (var tenant in tenants)
        {
            var id = RootId(tenant.Id);
            if (await db.ParameterRoots.FindAsync([tenant.Id, id], ct) is not null) continue;
            // Any operator root for the same key/scope keeps precedence; the fixture never competes with it.
            if (await db.ParameterRoots.AnyAsync(x => x.TenantId == tenant.Id && x.Key == Key && x.ScopeKind == "TENANT" &&
                x.ScopeId == tenant.Id && x.SelectorHash == selector.Hash, ct)) continue;
            var now = ParameterValuePolicy.Micro(db.Clock.GetUtcNow());
            var revision = RevisionId(tenant.Id);
            db.ParameterRoots.Add(new() { TenantId = tenant.Id, Id = id, Key = Key, ScopeKind = "TENANT", ScopeId = tenant.Id,
                SelectorJson = selector.Json, SelectorHash = selector.Hash, Revision = 1, PendingRevisionId = revision, CreatedAt = now });
            db.ParameterRevisions.Add(new() { TenantId = tenant.Id, Id = revision, RootId = id, Sequence = 1, Key = Key,
                DefinitionVersion = definition.DefinitionVersion, ValueMode = "INHERIT", ValueJson = null, EffectiveFrom = now,
                Reason = "FICTIONAL synthetic fixture proposal; not approvable", AuthorId = Guid.Empty, AuthoredAt = now,
                ContentHash = ParameterValuePolicy.Hash("SYNTHETIC_INCOMPLETE_PARAMETER_PROPOSAL/" + tenant.Id.ToString("D")),
                Provenance = "SyntheticUnverified" });
        }
        // Caller owns audited SaveChanges/transaction.
    }
}
