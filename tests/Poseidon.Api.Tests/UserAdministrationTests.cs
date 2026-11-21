using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// USER-ADMIN-01 / UA-AC-06..14. Actual host, antiforgery, idempotency and real PostgreSQL. Synthetic identities only:
// no IdP account, password or credential is created; the provisioned session binding is what the OIDC login stamps.
public sealed partial class DirectoryWorkspaceTests
{
    // Administrator A ("synthetic") additionally holds the read-only and tenant-admin bundle permissions, but not the
    // Master bundle (no daily.noon.write ...), so least-privilege refusals are observable.
    private static readonly string[] AdminExtra = ["workspace.search", "notification.read", "certificate.read", "equipment.read", "maintenance.read",
        "workorder.read", "defect.read", "daily.noon.read", "daily.deck.read", "daily.engine.read", "membership.read", "audit.read", "config.read",
        "config.draft", "system.health.read", "crew.read"];

    private WebApplicationFactory<Program> UserAdminHost() => MembershipHost().WithWebHostBuilder(b =>
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(AdminExtra.Select((p, i) =>
            new KeyValuePair<string, string?>($"Authentication:Oidc:Memberships:0:Permissions:{50 + i}", p)))));

    private static async Task<HttpClient> Provisioned(WebApplicationFactory<Program> host, string subject, Guid tenant, Guid account)
    {
        var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Membership-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Membership-Provisioned", $"{tenant:D}/{account:D}");
        using var session = await client.GetAsync("/api/v1/auth/session", TestContext.Current.CancellationToken);
        if (session.IsSuccessStatusCode && (await session.Content.ReadFromJsonAsync<SessionDto>(TestContext.Current.CancellationToken))?.CsrfToken is { Length: > 0 } csrf)
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        return client;
    }

    private static object Grant(string[] bundles, bool read = true, bool manage = false, string[]? permissions = null, object[]? vessels = null, long expected = 1) =>
        new { expectedVersion = expected, bundles, permissions = permissions ?? [], tenantWideRead = read, tenantWideManage = manage, vesselGrants = vessels ?? [] };

    private static object Create(string subject, string[] bundles, bool read = true, bool manage = false, string[]? permissions = null, object[]? vessels = null, string issuer = MemberIssuer) =>
        new { issuer, subject, bundles, permissions = permissions ?? [], tenantWideRead = read, tenantWideManage = manage, vesselGrants = vessels ?? [] };

    private async Task<MembershipAccessDto> Created(HttpClient admin, object body)
    {
        using var response = await Post(admin, "memberships", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<MembershipAccessDto>(Ct))!;
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-01")]
    public async Task Admin_provisions_a_database_membership_that_governs_the_members_next_request()
    {
        await using var host = UserAdminHost();
        using var admin = await Member(host, "synthetic");
        var tenant = seed.Tenants[0].Id;

        var catalog = (await admin.GetFromJsonAsync<MembershipCatalogDto>("/api/v1/memberships/catalog", Ct))!;
        Assert.True(catalog.CanManage);
        Assert.True(Assert.Single(catalog.Bundles, b => b.Code == "read-only").Grantable);
        Assert.False(Assert.Single(catalog.Bundles, b => b.Code == "master").Grantable);
        Assert.Equal(ActorResolver.KnownPermissions.Length, catalog.Permissions.Length);
        Assert.Contains(catalog.Vessels, v => v.Id == SeedVessel.Id);
        Assert.DoesNotContain(catalog.Vessels, v => seed.Vessels.Any(s => s.Id == v.Id && s.TenantId != tenant));

        // Least privilege, closed input and the trusted issuer are checked before anything is written.
        using (var wider = await Post(admin, "memberships", Create("provisioned-e", ["master"])))
        { Assert.Equal(HttpStatusCode.BadRequest, wider.StatusCode); Assert.Equal("exceeds_grantor_authority", await FieldError(wider, "bundles")); }
        using (var individual = await Post(admin, "memberships", Create("provisioned-e", ["read-only"], permissions: ["departure.decide"])))
            Assert.Equal("exceeds_grantor_authority", await FieldError(individual, "permissions"));
        using (var plain = await Post(admin, "memberships", Create("provisioned-e", ["read-only"], issuer: "http://directory-test.example")))
            Assert.Equal("issuer_not_trusted", await FieldError(plain, "issuer"));
        using (var foreign = await Post(admin, "memberships", Create("provisioned-e", ["read-only"], read: false,
            vessels: [new { vesselId = seed.Vessels.First(v => v.TenantId != tenant).Id, canEdit = false }])))
            Assert.Equal("unknown_vessel", await FieldError(foreign, "vesselGrants"));
        using (var unknownField = await Post(admin, "memberships", new { issuer = MemberIssuer, subject = "provisioned-e", bundles = new[] { "read-only" }, tenantWideRead = true, password = "x" }))
            Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        using (var configured = await Post(admin, "memberships", Create("member-b", ["read-only"])))
        { Assert.Equal(HttpStatusCode.Conflict, configured.StatusCode); Assert.Equal("identity_unavailable", await MembershipCode(configured)); }

        var key = Guid.NewGuid().ToString();
        using var created = await Post(admin, "memberships", Create("provisioned-e", ["read-only"]), key);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var text = await created.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("provisioned-e", text); Assert.DoesNotContain(MemberIssuer, text);
        var view = (await created.Content.ReadFromJsonAsync<MembershipAccessDto>(Ct))!;
        Assert.Equal(("Database", "AdminProvisioned", "Active", 1), (view.Membership.ManagedBy, view.Membership.Source, view.Membership.Status, view.Membership.GrantRevision));
        Assert.Equal(["read-only"], view.Membership.Bundles);
        Assert.False(view.Membership.Administrator);
        Assert.True(view.Editable);
        using (var replay = await Post(admin, "memberships", Create("provisioned-e", ["read-only"]), key))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
            Assert.Equal(view.Membership.Id, (await replay.Content.ReadFromJsonAsync<MembershipAccessDto>(Ct))!.Membership.Id);
        }
        using (var duplicate = await Post(admin, "memberships", Create("provisioned-e", ["read-only"])))
            Assert.Equal("identity_unavailable", await MembershipCode(duplicate));

        // The provisioned session resolves to the database grant on its next request; without the binding nothing.
        var account = view.Membership.AccountId;
        using var member = await Provisioned(host, "provisioned-e", tenant, account);
        var fleet = (await member.GetFromJsonAsync<PageDto<VesselDto>>("/api/v1/vessels?limit=100", Ct))!;
        Assert.NotEmpty(fleet.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/memberships/catalog", Ct)).StatusCode);
        using (var unbound = await Member(host, "provisioned-e"))
            Assert.Equal(HttpStatusCode.Forbidden, (await unbound.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        using (var forged = await Provisioned(host, "provisioned-e", seed.Tenants[1].Id, account))
            Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/v1/vessels", Ct)).StatusCode);

        // A grant change appends revision 2 and narrows the very next request to the granted vessel.
        using (var stale = await Post(admin, $"memberships/{view.Membership.Id}/grant", Grant(["read-only"], expected: 0)))
        { Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal("version_conflict", await MembershipCode(stale)); }
        using (var narrowed = await Post(admin, $"memberships/{view.Membership.Id}/grant", Grant(["read-only"], read: false,
            vessels: [new { vesselId = SeedVessel.Id, canEdit = false }], expected: 1)))
        {
            Assert.Equal(HttpStatusCode.OK, narrowed.StatusCode);
            var after = (await narrowed.Content.ReadFromJsonAsync<MembershipAccessDto>(Ct))!;
            Assert.Equal(2, after.Membership.GrantRevision);
            Assert.Equal([2, 1], after.History.Select(h => h.Revision));
            Assert.Equal(SeedVessel.Id, Assert.Single(after.VesselGrants).VesselId);
        }
        var scoped = (await member.GetFromJsonAsync<PageDto<VesselDto>>("/api/v1/vessels?limit=100", Ct))!;
        Assert.Equal(SeedVessel.Id.ToString(), Assert.Single(scoped.Items).Id.ToString());
        using (var again = await Post(admin, $"memberships/{view.Membership.Id}/grant", Grant(["read-only"], expected: 1)))
            Assert.Equal("version_conflict", await MembershipCode(again));

        // Search by exact IdP subject (never returned), by id prefix and by bundle; status filter.
        var bySubject = (await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?q=provisioned-e", Ct))!;
        Assert.Equal(view.Membership.Id, Assert.Single(bySubject.Items).Id);
        var byBundle = (await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?bundle=read-only", Ct))!;
        Assert.Contains(byBundle.Items, x => x.Id == view.Membership.Id);
        Assert.Empty((await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?q=no-such-subject", Ct))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/memberships/access?bundle=captain", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/memberships/access?limit=abc", Ct)).StatusCode);

        // Lifecycle through the existing exact transitions: suspension denies the provisioned session's next request.
        var lifecycle = await MembershipOf(admin, account);
        using (var suspend = await Post(admin, $"memberships/{lifecycle.Id}/suspend", new { expectedVersion = lifecycle.Version }))
            Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/v1/vessels", Ct)).StatusCode);

        await using var db = Db();
        Assert.Equal(2, await db.MembershipGrantRevisions.AsNoTracking().CountAsync(g => g.MembershipId == view.Membership.Id, Ct));
        Assert.Equal("AdminProvisioned", (await db.UserAccounts.AsNoTracking().SingleAsync(a => a.Id == account, Ct)).Source);
        var located = await host.Services.GetRequiredService<ProvisionedIdentityLocator>().FindAsync(MemberIssuer, "provisioned-e", "test", Ct);
        Assert.Equal((tenant, account, true), (located!.TenantId, located.ActorId, located.Provisioned));
        Assert.Null(await host.Services.GetRequiredService<ProvisionedIdentityLocator>().FindAsync(MemberIssuer, "member-b", "test", Ct));
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-01")]
    public async Task Self_modification_last_admin_configuration_source_and_tenant_isolation_are_enforced()
    {
        await using var host = UserAdminHost();
        using var admin = await Member(host, "synthetic");
        await Imported(admin);
        var tenant = seed.Tenants[0].Id;
        var p = await Created(admin, Create("provisioned-admin", ["tenant-admin"], manage: true));
        Assert.True(p.Membership.Administrator);
        using var provisionedAdmin = await Provisioned(host, "provisioned-admin", tenant, p.Membership.AccountId);
        Assert.Equal(HttpStatusCode.OK, (await provisionedAdmin.GetAsync("/api/v1/memberships/catalog", Ct)).StatusCode);

        // No self-escalation or self-lockout through the grant.
        var own = (await provisionedAdmin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{p.Membership.Id}/access", Ct))!;
        Assert.False(own.Editable); Assert.Equal("self_modification", own.EditBlockedReason);
        using (var self = await Post(provisionedAdmin, $"memberships/{p.Membership.Id}/grant", Grant(["read-only"], expected: 1)))
        { Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode); Assert.Equal("self_modification", await MembershipCode(self)); }

        // The operator configuration keeps governing its own identities (bootstrap/break-glass source).
        var a = await MembershipOf(admin, actorId);
        using (var configManaged = await Post(provisionedAdmin, $"memberships/{a.Id}/grant", Grant(["read-only"], expected: 1)))
        { Assert.Equal(HttpStatusCode.Conflict, configManaged.StatusCode); Assert.Equal("configuration_managed", await MembershipCode(configManaged)); }
        var configView = (await provisionedAdmin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{a.Id}/access", Ct))!;
        Assert.Equal(("Configuration", "configuration_managed"), (configView.Membership.ManagedBy, configView.EditBlockedReason));

        // A provisioned administrator counts for the last-admin guard: once the configured admins are suspended it is
        // the last one and cannot be suspended voluntarily.
        var c = await MembershipOf(admin, adminC);
        using (var suspendC = await Post(admin, $"memberships/{c.Id}/suspend", new { expectedVersion = c.Version })) Assert.Equal(HttpStatusCode.OK, suspendC.StatusCode);
        using (var suspendA = await Post(provisionedAdmin, $"memberships/{a.Id}/suspend", new { expectedVersion = a.Version })) Assert.Equal(HttpStatusCode.OK, suspendA.StatusCode);
        var last = (await provisionedAdmin.GetFromJsonAsync<MembershipPageDto>("/api/v1/memberships?limit=100", Ct))!.Items.Single(x => x.Id == p.Membership.Id);
        Assert.True(last.Administrator);
        using (var lastAdmin = await Post(provisionedAdmin, $"memberships/{p.Membership.Id}/suspend", new { expectedVersion = last.Version }))
        { Assert.Equal(HttpStatusCode.Conflict, lastAdmin.StatusCode); Assert.Equal("last_admin", await MembershipCode(lastAdmin)); }

        // Tenant isolation: the other tenant's administrator sees nothing of this tenant and cannot write it.
        using var foreign = await Member(host, "other-tenant-admin");
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/v1/memberships/{p.Membership.Id}/access", Ct)).StatusCode);
        using (var foreignGrant = await Post(foreign, $"memberships/{p.Membership.Id}/grant", Grant(["read-only"], expected: 1)))
            Assert.Equal(HttpStatusCode.NotFound, foreignGrant.StatusCode);
        Assert.DoesNotContain((await foreign.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?limit=100", Ct))!.Items, x => x.Id == p.Membership.Id);

        // Non-administrators: 403 on catalog, reads and writes; nothing is written.
        using var member = await Member(host, "member-b");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/memberships/catalog", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/memberships/access", Ct)).StatusCode);
        using (var denied = await Post(member, "memberships", Create("provisioned-x", ["read-only"])))
        { Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal("permission_denied", await MembershipCode(denied)); }
        await using var db = Db();
        Assert.False(await db.UserAccounts.AsNoTracking().AnyAsync(x => x.Subject == "provisioned-x", Ct));
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-01")]
    public async Task Delegation_from_a_provisioned_source_follows_its_current_grant()
    {
        await using var host = UserAdminHost();
        using var admin = await Member(host, "synthetic");
        await Imported(admin);
        var tenant = seed.Tenants[0].Id;
        var source = await Created(admin, Create("provisioned-source", ["read-only"], permissions: ["crew.read"], read: false,
            vessels: [new { vesselId = SeedVessel.Id, canEdit = false }]));
        var b = await MembershipOf(admin, memberB);
        var now = DateTimeOffset.UtcNow;
        using (var wider = await Post(admin, $"memberships/{source.Membership.Id}/delegations", new { delegateMembershipId = b.Id, permissions = new[] { "crew.write" },
            validFrom = now, validUntil = now.AddDays(2), reasonCode = "PlannedAbsence", vesselScope = new[] { new { vesselId = SeedVessel.Id, canEdit = false } } }))
            Assert.Equal("exceeds_source_authority", await FieldError(wider, "permissions"));
        using (var created = await Post(admin, $"memberships/{source.Membership.Id}/delegations", new { delegateMembershipId = b.Id, permissions = new[] { "crew.read" },
            validFrom = now, validUntil = now.AddDays(2), reasonCode = "PlannedAbsence", vesselScope = new[] { new { vesselId = SeedVessel.Id, canEdit = false } } }))
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var member = await Member(host, "member-b");
        var current = (await member.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!;
        Assert.Equal(["crew.read"], Assert.Single(current.ReceivedDelegations).Permissions);
        // Removing crew.read from the source's grant removes the delegated permission at request time.
        using (var narrowed = await Post(admin, $"memberships/{source.Membership.Id}/grant", Grant(["read-only"], read: false,
            vessels: [new { vesselId = SeedVessel.Id, canEdit = false }], expected: 1)))
            Assert.Equal(HttpStatusCode.OK, narrowed.StatusCode);
        Assert.Empty((await member.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!.ReceivedDelegations);
    }

    // QA N5: filters, totals and paging are computed in the database over the whole tenant, not over a capped read.
    [Fact]
    [Trait("Requirement", "USER-ADMIN-01")]
    public async Task Access_list_filters_counts_and_pages_in_the_database_beyond_two_thousand_rows()
    {
        await using var host = UserAdminHost();
        using var admin = await Member(host, "synthetic");
        var tenant = seed.Tenants[0].Id;
        const int synthetic = 2005;
        await using (var import = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        {
            for (var i = 0; i < synthetic; i++)
            {
                var account = Guid.NewGuid();
                import.UserAccounts.Add(new UserAccount { TenantId = tenant, Id = account, Issuer = AccountMembershipFixtures.SyntheticIssuer,
                    Subject = $"synthetic-bulk-{i:D5}", Source = "SyntheticUnverified", CreatedAt = DateTimeOffset.UnixEpoch });
                import.AccountMemberships.Add(new AccountMembership { TenantId = tenant, Id = Guid.NewGuid(), AccountId = account, Status = "Active",
                    StatusReason = "SyntheticFixture", StatusChangedAt = DateTimeOffset.UnixEpoch, AccessRevision = 0, Source = "SyntheticUnverified", CreatedAt = DateTimeOffset.UnixEpoch });
            }
            await import.SaveChangesAsync(Ct);
        }
        var created = new List<Guid>();
        foreach (var subject in new[] { "bulk-reader-1", "bulk-reader-2", "bulk-reader-3" })
            created.Add((await Created(admin, Create(subject, ["read-only"]))).Membership.Id);

        var all = (await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?limit=100", Ct))!;
        Assert.Equal(synthetic + 3, all.Total);
        Assert.Equal(100, all.Items.Length); Assert.True(all.HasMore);
        var tail = (await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?limit=100&offset=2000", Ct))!;
        Assert.Equal(8, tail.Items.Length); Assert.False(tail.HasMore);
        var first = (await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?bundle=read-only&limit=2", Ct))!;
        Assert.Equal((3, 2, true), (first.Total, first.Items.Length, first.HasMore));
        var second = (await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?bundle=read-only&limit=2&offset=2", Ct))!;
        Assert.Equal((3, 1, false), (second.Total, second.Items.Length, second.HasMore));
        Assert.Equal(created.Order(), first.Items.Concat(second.Items).Select(x => x.Id).Order());
        var bySubject = (await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?q=synthetic-bulk-02004", Ct))!;
        Assert.Equal(1, bySubject.Total);
        var byPrefix = (await admin.GetFromJsonAsync<MembershipAccessPageDto>($"/api/v1/memberships/access?q={created[0].ToString("D")[..13].ToUpperInvariant()}", Ct))!;
        Assert.Contains(byPrefix.Items, x => x.Id == created[0]);
        Assert.Equal(0, (await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?status=Suspended&limit=1", Ct))!.Total);
    }

    // QA N4: an operator entry for a provisioned identity (other tenant/actor) shadows the database grant: the screen
    // shows configuration governance without leaking that entry, grant edits are refused and the session gets no grant.
    [Fact]
    [Trait("Requirement", "USER-ADMIN-01")]
    public async Task A_configuration_entry_shadows_a_provisioned_grant_which_becomes_read_only()
    {
        await using var host = UserAdminHost();
        using var admin = await Member(host, "synthetic");
        var tenant = seed.Tenants[0].Id;
        var view = await Created(admin, Create("provisioned-shadow", ["read-only"]));
        using var member = await Provisioned(host, "provisioned-shadow", tenant, view.Membership.AccountId);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var original = settings.Memberships[4]; // other-tenant-admin: different tenant and actor id.
        settings.Memberships[4] = original with { Subject = "provisioned-shadow" };
        try
        {
            var shadowed = (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{view.Membership.Id}/access", Ct))!;
            Assert.Equal(("Configuration", "configuration_shadowed", false), (shadowed.Membership.ManagedBy, shadowed.EditBlockedReason, shadowed.Editable));
            Assert.Equal((0, (int?)null), (shadowed.Membership.PermissionCount, shadowed.Membership.GrantRevision));
            Assert.Empty(shadowed.Permissions); Assert.Empty(shadowed.Membership.Bundles);
            using (var edit = await Post(admin, $"memberships/{view.Membership.Id}/grant", Grant(["read-only"], expected: 1)))
            { Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode); Assert.Equal("configuration_managed", await MembershipCode(edit)); }
            Assert.DoesNotContain((await admin.GetFromJsonAsync<MembershipAccessPageDto>("/api/v1/memberships/access?bundle=read-only&limit=100", Ct))!.Items,
                x => x.Id == view.Membership.Id);
            // Configuration governs the identity entirely: it resolves to the configured actor/tenant, never to the DB grant.
            var current = (await member.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!;
            Assert.Equal((otherTenantAdmin, seed.Tenants[1].Id), (current.ActorId, current.TenantId));
        }
        finally { settings.Memberships[4] = original; }
        Assert.Equal("Database", (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{view.Membership.Id}/access", Ct))!.Membership.ManagedBy);
    }

    // QA N9: the user-admin destination is projected only with explicit membership.read and tenant-wide scope.
    [Fact]
    [Trait("Requirement", "USER-ADMIN-01")]
    public async Task User_admin_destination_needs_membership_read()
    {
        await using var host = UserAdminHost();
        async Task<bool> Has(string subject)
        {
            using var client = await Member(host, subject);
            var body = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/workspace", Ct);
            return body.GetProperty("destinations").EnumerateArray().Any(d => d.GetProperty("key").GetString() == "user-admin" &&
                d.GetProperty("href").GetString() == "/app/user-admin");
        }
        Assert.True(await Has("synthetic"));   // membership.read + membership.manage, tenant-wide manage.
        Assert.False(await Has("admin-c"));    // membership.manage only.
        Assert.False(await Has("member-b"));   // no membership permission.
    }
}
