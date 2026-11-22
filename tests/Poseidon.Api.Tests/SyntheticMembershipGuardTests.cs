using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// USER-GUIDE-01 UG-2 QA N6 / USER-ADMIN-01: a membership row that an administrator did not provision (the
// DemoFixtureCommand / AccountMembershipFixtures FICTIONAL member, or any other non-AdminProvisioned source) can
// never receive or change a grant through the user-admin endpoint and never resolves to an identity at login.
// Generic over every non-AdminProvisioned membership source (closed set: ConfigurationImport, SyntheticUnverified) and over the issuer (reserved ".invalid" and the trusted test issuer), so a
// later provisioning path (USER-ADMIN-02) cannot silently turn such a row into a granted membership.
public sealed partial class DirectoryWorkspaceTests
{
    [Theory]
    [Trait("Requirement", "USER-ADMIN-01")]
    [InlineData("fixture", false)]
    [InlineData("SyntheticUnverified", true)]
    [InlineData("ConfigurationImport", true)]
    [InlineData("ConfigurationImport", false)]
    public async Task A_membership_not_provisioned_by_an_administrator_is_never_grantable_editable_or_resolvable(string source, bool trustedIssuer)
    {
        await using var host = UserAdminHost();
        using var admin = await Member(host, "synthetic");
        var tenant = seed.Tenants[0].Id;
        string issuer, subject;
        Guid account, membership;
        await using (var import = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        {
            await using var tx = await import.Database.BeginTransactionAsync(Ct);
            if (source == "fixture")
            {
                await AccountMembershipFixtures.AddMissingAsync(import, seed.Tenants.Where(t => t.Id == tenant), Ct);
                await import.SaveChangesAsync(Ct);
                account = AccountMembershipFixtures.AccountId(tenant);
                membership = AccountMembershipFixtures.MembershipId(tenant);
                issuer = AccountMembershipFixtures.SyntheticIssuer;
                subject = "synthetic-member-" + tenant.ToString("N");
            }
            else
            {
                account = Guid.NewGuid(); membership = Guid.NewGuid();
                issuer = trustedIssuer ? MemberIssuer : AccountMembershipFixtures.SyntheticIssuer;
                subject = "non-provisioned-" + source.ToLowerInvariant() + "-" + account.ToString("N")[..8];
                import.UserAccounts.Add(new UserAccount { TenantId = tenant, Id = account, Issuer = issuer, Subject = subject,
                    Source = source, CreatedAt = DateTimeOffset.UnixEpoch });
                import.AccountMemberships.Add(new AccountMembership { TenantId = tenant, Id = membership, AccountId = account, Status = "Active",
                    StatusReason = "SyntheticFixture", StatusChangedAt = DateTimeOffset.UnixEpoch, AccessRevision = 0, Source = source,
                    CreatedAt = DateTimeOffset.UnixEpoch });
                await import.SaveChangesAsync(Ct);
            }
            await tx.CommitAsync(Ct);
        }

        // The administrator sees the row, but it is not governed by a database grant and cannot be edited.
        var view = (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{membership}/access", Ct))!;
        Assert.False(view.Editable);
        Assert.Null(view.Membership.GrantRevision);
        Assert.Empty(view.Membership.Bundles);
        Assert.Empty(view.Permissions);
        foreach (var expected in new long[] { 0, 1 })
            using (var edit = await Post(admin, $"memberships/{membership}/grant", Grant(["read-only"], expected: expected)))
            {
                Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
                Assert.Equal("configuration_managed", await MembershipCode(edit));
            }

        // It never resolves to an identity at login, neither by issuer/subject nor by a forged provisioned binding.
        Assert.Null(await host.Services.GetRequiredService<ProvisionedIdentityLocator>().FindAsync(issuer, subject, "test", Ct));
        using (var bound = await Provisioned(host, subject, tenant, account))
            Assert.NotEqual(HttpStatusCode.OK, (await bound.GetAsync("/api/v1/vessels", Ct)).StatusCode);

        await using var db = Db();
        Assert.Equal(0, await db.MembershipGrantRevisions.AsNoTracking().CountAsync(g => g.MembershipId == membership, Ct));
        var stored = await db.AccountMemberships.AsNoTracking().SingleAsync(m => m.Id == membership, Ct);
        Assert.Equal((0L, source == "fixture" ? "SyntheticUnverified" : source), ((long)stored.AccessRevision, stored.Source));
    }
}
