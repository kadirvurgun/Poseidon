using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private const string ContextRole = "91c26eb5-cf16-4553-981d-1c275358c535";
    private Dictionary<string, string?> ContextLabels() => new() {
        ["WorkspaceContext:RoleCatalog:Version"] = "synthetic-v1",
        ["WorkspaceContext:RoleCatalog:Roles:0:TenantId"] = seed.Tenants[0].Id.ToString(),
        ["WorkspaceContext:RoleCatalog:Roles:0:Reference"] = ContextRole,
        ["WorkspaceContext:RoleCatalog:Roles:0:Tr"] = "KURGUSAL yerel rol",
        ["WorkspaceContext:RoleCatalog:Roles:0:En"] = "FICTIONAL local role",
        ["WorkspaceContext:RoleCatalog:Roles:1:TenantId"] = seed.Tenants[1].Id.ToString(),
        ["WorkspaceContext:RoleCatalog:Roles:1:Reference"] = ContextRole,
        ["WorkspaceContext:RoleCatalog:Roles:1:Tr"] = "FOREIGN HIDDEN TR",
        ["WorkspaceContext:RoleCatalog:Roles:1:En"] = "FOREIGN HIDDEN EN"
    };

    [Fact]
    public async Task Workspace_context_resolves_only_current_provisioned_roles_and_authorized_vessel()
    {
        using var host = Host(manage: false, read: false).WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(ContextLabels())));
        using var client = Client(host);
        using var response = await client.GetAsync("/api/v1/workspace/context/" + SeedVessel.Id, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var dto = (await response.Content.ReadFromJsonAsync<WorkspaceContextDto>(Ct))!;
        Assert.Equal(seed.Tenants[0].Id, dto.TenantId); Assert.Equal(actorId, dto.ActorId);
        Assert.Equal(SeedVessel.Id, dto.Vessel!.Id); Assert.Equal("Configured", dto.RoleContext.State);
        Assert.Equal(SeedVessel.CompanyId, dto.Company!.Id);
        Assert.Equal(seed.Companies.Single(x => x.Id == SeedVessel.CompanyId).Name, dto.Company.Name);
        Assert.Equal("synthetic-v1", dto.RoleContext.CatalogVersion);
        Assert.Equal("FICTIONAL local role", Assert.Single(dto.RoleContext.Roles).En);
        Assert.DoesNotContain("FOREIGN", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Workspace_context_uses_the_trusted_active_company_independently_from_vessel_ownership()
    {
        var activeCompany = Guid.NewGuid();
        await using (var db = Db()) {
            db.Companies.Add(new Company { Id = activeCompany, TenantId = seed.Tenants[0].Id,
                Code = "SYN-ACTIVE", Name = "Synthetic active session company" });
            await db.SaveChangesAsync(Ct);
        }
        using var host = Host(manage: false, read: false);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = [settings.Memberships[0] with { ActiveCompanyId = activeCompany }];
        using var client = Client(host);
        var dto = (await client.GetFromJsonAsync<WorkspaceContextDto>("/api/v1/workspace/context/" + SeedVessel.Id, Ct))!;
        Assert.Equal(activeCompany, dto.Company!.Id); Assert.Equal("SYN-ACTIVE", dto.Company.Code);
        Assert.Equal(SeedVessel.CompanyId, dto.Vessel!.CompanyId); Assert.NotEqual(dto.Company.Id, dto.Vessel.CompanyId);
    }

    [Fact]
    public async Task Missing_active_company_is_explicit_and_never_inferred_from_the_selected_vessel()
    {
        using var host = Host(manage: false, read: false);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = [settings.Memberships[0] with { ActiveCompanyId = null }];
        using var client = Client(host);
        var dto = (await client.GetFromJsonAsync<WorkspaceContextDto>("/api/v1/workspace/context/" + SeedVessel.Id, Ct))!;
        Assert.Null(dto.Company); Assert.Equal(SeedVessel.Id, dto.Vessel!.Id);
    }

    [Fact]
    public async Task Workspace_context_denies_every_ungranted_or_foreign_vessel_without_labels_or_counts()
    {
        using var host = Host(manage: false, read: false); using var client = Client(host);
        foreach (var vessel in seed.Vessels.Where(x => x.Id != SeedVessel.Id)) {
            using var response = await client.GetAsync("/api/v1/workspace/context/" + vessel.Id, Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain(vessel.Name, body); Assert.DoesNotContain(vessel.Code, body);
            Assert.DoesNotContain("roles", body); Assert.DoesNotContain("count", body);
        }
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("role")]
    [InlineData("vessel")]
    public async Task Workspace_context_rejects_request_supplied_identity_role_or_scope_selectors(string query)
    {
        using var host = Host(); using var client = Client(host);
        var response = await client.GetAsync("/api/v1/workspace/context?" + query + "=" + Guid.NewGuid(), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("roleContext", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Workspace_context_missing_labels_do_not_fabricate_an_admin_role_or_deny_valid_read_scope()
    {
        using var host = Host(); using var client = Client(host);
        var dto = (await client.GetFromJsonAsync<WorkspaceContextDto>("/api/v1/workspace/context", Ct))!;
        Assert.Equal("Unavailable", dto.RoleContext.State); Assert.Empty(dto.RoleContext.Roles);
        Assert.Null(dto.RoleContext.CatalogVersion); Assert.Equal(SeedVessel.CompanyId, dto.Company!.Id); Assert.Null(dto.Vessel);
    }

    [Fact]
    public async Task Workspace_context_invalid_role_attribution_remains_missing_and_never_gains_write_permission()
    {
        using var host = Host().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Oidc:Memberships:0:RoleReferences:0"] = "admin" })));
        using var client = Client(host);
        var dto = (await client.GetFromJsonAsync<WorkspaceContextDto>("/api/v1/workspace/context/" + SeedVessel.Id, Ct))!;
        Assert.Equal("Missing", dto.RoleContext.State); Assert.Empty(dto.RoleContext.Roles);
        Assert.False(dto.Vessel!.CanEdit);
    }

    [Fact]
    public async Task Workspace_context_rechecks_withdrawn_vessel_grant()
    {
        using var allowedHost = Host(manage: false, read: false); using var allowed = Client(allowedHost);
        Assert.Equal(HttpStatusCode.OK, (await allowed.GetAsync("/api/v1/workspace/context/" + SeedVessel.Id, Ct)).StatusCode);
        using var deniedHost = Host(manage: false, read: false, scope: false); using var denied = Client(deniedHost);
        var response = await denied.GetAsync("/api/v1/workspace/context/" + SeedVessel.Id, Ct);
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        Assert.DoesNotContain(SeedVessel.Name, await response.Content.ReadAsStringAsync(Ct));
    }

    // DEV-021 / T-S2-002 (DEV021-AC-03..07): a context switch is a server-verified read of the
    // target; the actor's CURRENT membership decides, every denial is non-enumerating and the
    // attempted target is attributable in the security audit.
    private async Task<SecurityEvent[]> WorkspaceAudit(Guid? resource)
    {
        await using var db = Db();
        return await db.Set<SecurityEvent>().Where(e => e.Action == "WorkspaceRead" && e.ResourceId == resource).ToArrayAsync(Ct);
    }

    [Fact]
    public async Task Context_switch_to_a_granted_vessel_is_verified_and_audited_for_the_actor()
    {
        using var host = Host(manage: false, read: false); using var client = Client(host);
        using var response = await client.GetAsync("/api/v1/workspace/context/" + SeedVessel.Id, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SeedVessel.Id, (await response.Content.ReadFromJsonAsync<WorkspaceContextDto>(Ct))!.Vessel!.Id);
        var audit = Assert.Single(await WorkspaceAudit(SeedVessel.Id));
        Assert.Equal(("Success", seed.Tenants[0].Id, actorId.ToString()), (audit.Result, audit.TenantId, audit.ActorId));
    }

    [Fact]
    public async Task Context_switch_to_a_foreign_tenant_vessel_is_denied_without_leak_and_attributed()
    {
        var foreign = seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id);
        using var host = Host(); using var client = Client(host);
        using var response = await client.GetAsync("/api/v1/workspace/context/" + foreign.Id, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(foreign.Name, body); Assert.DoesNotContain(foreign.Code, body); Assert.DoesNotContain("company", body);
        var audit = Assert.Single(await WorkspaceAudit(foreign.Id));
        Assert.Equal(("NotFound", seed.Tenants[0].Id, actorId.ToString()), (audit.Result, audit.TenantId, audit.ActorId));
    }

    [Fact]
    public async Task Context_switch_to_a_vessel_outside_the_granted_mode_is_denied()
    {
        var tug = seed.Vessels.First(v => v.TenantId == seed.Tenants[0].Id && v.Kind == VesselKind.Tug);
        using var host = Host(tugMode: false, crossMode: false); using var client = Client(host);
        using var response = await client.GetAsync("/api/v1/workspace/context/" + tug.Id, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(tug.Name, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Context_switch_rechecks_a_vessel_grant_withdrawn_during_the_same_session()
    {
        using var host = Host(manage: false, read: false); using var client = Client(host);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/workspace/context/" + SeedVessel.Id, Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        // The actor keeps read access elsewhere, so this is a stale grant for this vessel, not a logout.
        settings.Memberships = [settings.Memberships[0] with { VesselGrants = [new VesselGrant(Guid.NewGuid())] }];
        using var stale = await client.GetAsync("/api/v1/workspace/context/" + SeedVessel.Id, Ct);
        Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
        Assert.DoesNotContain(SeedVessel.Name, await stale.Content.ReadAsStringAsync(Ct));
        Assert.Contains(await WorkspaceAudit(SeedVessel.Id), e => e.Result == "NotFound");
    }

    [Fact]
    public async Task Context_switch_with_a_removed_membership_fails_closed_without_a_fallback_context()
    {
        using var host = Host(manage: false, read: false); using var client = Client(host);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/workspace/context", Ct)).StatusCode);
        host.Services.GetRequiredService<FederationSettings>().Memberships = [];
        foreach (var path in new[] { "/api/v1/workspace/context", "/api/v1/workspace/context/" + SeedVessel.Id }) {
            using var response = await client.GetAsync(path, Ct);
            Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, path + " " + response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("roleContext", body); Assert.DoesNotContain(SeedVessel.Name, body);
        }
    }

    [Fact]
    public async Task Context_roles_are_exactly_the_current_membership_and_never_an_ungranted_catalog_role()
    {
        const string other = "5b0a7c62-4d4e-4f0f-9a43-0e1f7e2bd021";
        var labels = ContextLabels();
        labels["WorkspaceContext:RoleCatalog:Roles:2:TenantId"] = seed.Tenants[0].Id.ToString();
        labels["WorkspaceContext:RoleCatalog:Roles:2:Reference"] = other;
        labels["WorkspaceContext:RoleCatalog:Roles:2:Tr"] = "KURGUSAL atanmamış rol";
        labels["WorkspaceContext:RoleCatalog:Roles:2:En"] = "FICTIONAL ungranted role";
        using var host = Host(manage: false, read: false).WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(labels)));
        using var client = Client(host);
        var first = (await client.GetFromJsonAsync<WorkspaceContextDto>("/api/v1/workspace/context/" + SeedVessel.Id, Ct))!;
        Assert.Equal(Guid.Parse(ContextRole), Assert.Single(first.RoleContext.Roles).Reference);
        // A client cannot pick the other role; only a server-side membership change can.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/workspace/context/" + SeedVessel.Id + "?role=" + other, Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var changed = settings.Memberships[0] with { }; changed.RoleReferences = [other];
        settings.Memberships = [changed];
        var second = (await client.GetFromJsonAsync<WorkspaceContextDto>("/api/v1/workspace/context/" + SeedVessel.Id, Ct))!;
        Assert.Equal("FICTIONAL ungranted role", Assert.Single(second.RoleContext.Roles).En);
        Assert.DoesNotContain(second.RoleContext.Roles, r => r.Reference == Guid.Parse(ContextRole));
    }

    [Fact]
    public async Task Workspace_context_requires_real_authentication()
    {
        using var host = Host(); using var client = Client(host, authenticated: false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/workspace/context", Ct)).StatusCode);
    }
}

public sealed class WorkspaceRoleContextTests
{
    private static readonly Guid Tenant = Guid.Parse("00000001-0000-0000-0000-000000000001");
    private const string Role = "00000001-0000-0000-0000-000000000002";
    private static Dictionary<string, string?> Labels() => new() {
        ["WorkspaceContext:RoleCatalog:Version"] = "synthetic-v1",
        ["WorkspaceContext:RoleCatalog:Roles:0:TenantId"] = Tenant.ToString(),
        ["WorkspaceContext:RoleCatalog:Roles:0:Reference"] = Role,
        ["WorkspaceContext:RoleCatalog:Roles:0:Tr"] = "KURGUSAL rol",
        ["WorkspaceContext:RoleCatalog:Roles:0:En"] = "FICTIONAL role"
    };
    private static WorkspaceRoleContextDto Describe(Dictionary<string, string?> labels, string? role = Role) =>
        WorkspaceRoleContext.Describe(Tenant, role, new ConfigurationBuilder().AddInMemoryCollection(labels).Build());
    [Fact]
    public void Role_catalog_resolves_only_a_provisioned_reference() => Assert.Single(Describe(Labels()).Roles);
    [Fact]
    public void Role_catalog_is_not_a_membership_or_permission_assignment() {
        var value = Describe(Labels(), null); Assert.Equal("Missing", value.State); Assert.Empty(value.Roles);
    }
    [Theory]
    [InlineData("Version", "")]
    [InlineData("Roles:0:Tr", "PRIVATE\nLABEL")]
    [InlineData("Roles:0:En", "")]
    [InlineData("Roles:0:TenantId", "00000002-0000-0000-0000-000000000001")]
    [InlineData("Roles:0:Reference", "00000001-0000-0000-0000-000000000099")]
    public void Missing_or_invalid_role_definitions_never_silently_fall_back(string field, string value) {
        var data = Labels(); data["WorkspaceContext:RoleCatalog:" + field] = value;
        var result = Describe(data); Assert.Equal("Unavailable", result.State); Assert.Empty(result.Roles);
    }
    [Fact]
    public void Ambiguous_duplicate_role_definitions_are_unavailable() {
        var data = Labels(); foreach (var key in new[] { "TenantId", "Reference", "Tr", "En" })
            data["WorkspaceContext:RoleCatalog:Roles:1:" + key] = data["WorkspaceContext:RoleCatalog:Roles:0:" + key];
        Assert.Equal("Unavailable", Describe(data).State);
    }
    [Fact]
    public void Partially_configured_multiple_roles_are_not_presented_as_complete() {
        Assert.Equal("Unavailable", Describe(Labels(), Role + ",00000001-0000-0000-0000-000000000003").State);
    }
}
