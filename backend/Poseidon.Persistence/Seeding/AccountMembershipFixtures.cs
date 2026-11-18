using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Persistence.Seeding;

// PLAN-1511-5202. Explicit optional fixture (not part of normal SeedImporter, which has
// exact audit-count expectations). Synthetic identities use the reserved ".invalid"
// issuer, so they can never match a real IdP; no password, hash, token or credential
// is seeded and no fixture confers permissions (grants come from the operator source).
public static class AccountMembershipFixtures
{
    public const string SyntheticIssuer = "https://synthetic-idp.invalid";
    private static Guid Derive(Guid tenant, string label) =>
        new(SHA256.HashData(tenant.ToByteArray().Concat(Encoding.UTF8.GetBytes(label)).ToArray()).AsSpan(0, 16));
    public static Guid AccountId(Guid tenant) => Derive(tenant, "FICTIONAL-ACCOUNT-v1");
    public static Guid MembershipId(Guid tenant) => Derive(tenant, "FICTIONAL-MEMBERSHIP-v1");

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Tenant> tenants, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException("Membership fixtures require administrative import.");
        foreach (var tenant in tenants)
        {
            var account = AccountId(tenant.Id);
            if (await db.UserAccounts.FindAsync([tenant.Id, account], ct) is null)
                db.UserAccounts.Add(new UserAccount { TenantId = tenant.Id, Id = account, Issuer = SyntheticIssuer,
                    Subject = "synthetic-member-" + tenant.Id.ToString("N"), Source = "SyntheticUnverified", CreatedAt = DateTimeOffset.UtcNow });
            var membership = MembershipId(tenant.Id);
            if (await db.AccountMemberships.FindAsync([tenant.Id, membership], ct) is null)
                db.AccountMemberships.Add(new AccountMembership { TenantId = tenant.Id, Id = membership, AccountId = account,
                    Status = "Active", StatusReason = "SyntheticFixture", StatusChangedAt = DateTimeOffset.UtcNow, AccessRevision = 0,
                    Source = "SyntheticUnverified", CreatedAt = DateTimeOffset.UtcNow });
        }
        // Caller owns the audited SaveChanges/transaction. No role context, company,
        // vessel grant, delegation or administrator status is seeded.
    }
}
