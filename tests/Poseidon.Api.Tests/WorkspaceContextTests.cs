using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Poseidon.Api;
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
        Assert.Equal("synthetic-v1", dto.RoleContext.CatalogVersion);
        Assert.Equal("FICTIONAL local role", Assert.Single(dto.RoleContext.Roles).En);
        Assert.DoesNotContain("FOREIGN", await response.Content.ReadAsStringAsync(Ct));
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
        Assert.Null(dto.RoleContext.CatalogVersion); Assert.Null(dto.Vessel);
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
