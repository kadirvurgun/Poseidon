using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-021/022/025; T-S2-002/003/006. Pure presentation policy: no operational role inference.
public sealed class WorkspaceLensPolicyTests
{
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Vessel = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private const string Role = "20000000-0000-0000-0000-000000000001";
    private static TrustedActor Actor(params string[] extra) => new(Guid.NewGuid(), Tenant,
        ["directory.read", .. extra], true, false, []) { RoleContext = Role };
    private static Dictionary<string, string?> Config() => new() {
        ["WorkspaceContext:RoleCatalog:Version"] = "synthetic-label-v1",
        ["WorkspaceContext:RoleCatalog:Roles:0:TenantId"] = Tenant.ToString(),
        ["WorkspaceContext:RoleCatalog:Roles:0:Reference"] = Role,
        ["WorkspaceContext:RoleCatalog:Roles:0:Tr"] = "KURGUSAL yerel rol",
        ["WorkspaceContext:RoleCatalog:Roles:0:En"] = "FICTIONAL local role",
        ["WorkspaceNavigation:Version"] = "synthetic-menu-v1",
        ["WorkspaceNavigation:Mappings:0:TenantId"] = Tenant.ToString(),
        ["WorkspaceNavigation:Mappings:0:Reference"] = Role,
        ["WorkspaceNavigation:Mappings:0:Keys:0"] = "crew",
        ["WorkspaceNavigation:Mappings:0:Keys:1"] = "audit",
        ["WorkspaceNavigation:Mappings:0:Keys:2"] = "tug-command",
        ["WorkspaceNavigation:Mappings:0:Modes:0"] = "Fleet",
        ["WorkspaceNavigation:Mappings:0:Modes:1"] = "Ship",
        ["WorkspaceNavigation:Mappings:0:Modes:2"] = "Tug"
    };
    private static WorkspaceLenses Describe(TrustedActor actor, Dictionary<string, string?>? config = null, VesselKind kind = VesselKind.Ship) =>
        WorkspaceLensPolicy.Describe(actor, Vessel, kind, new ConfigurationBuilder().AddInMemoryCollection(config ?? Config()).Build());

    [Fact]
    public void Configured_lens_simplifies_menu_without_granting_any_endpoint_or_search() {
        var actor = Actor("crew.read"); var result = Describe(actor);
        Assert.Equal("Configured", result.State); Assert.False(result.CanSearch);
        var item = Assert.Single(Assert.Single(result.Lenses).Items);
        Assert.Equal("crew", item.Key); Assert.Equal($"/app/crew-roster/{Vessel}", item.Href);
        Assert.False(actor.CanReadAudit); Assert.False(actor.CanReadTug);
    }
    [Fact]
    public void Same_trusted_cross_mode_role_survives_both_modes_but_tug_destinations_are_hidden_on_ship() {
        var actor = Actor("mode.tug", "mode.cross", "tug.read", "crew.read");
        Assert.DoesNotContain(Assert.Single(Describe(actor).Lenses).Items, x => x.Key.StartsWith("tug-"));
        Assert.Contains(Assert.Single(Describe(actor, kind: VesselKind.Tug).Lenses).Items, x => x.Key == "tug-command" && x.Href.EndsWith("?vessel=" + Vessel));
    }
    [Theory]
    [InlineData("Version", "")]
    [InlineData("Mappings:0:Keys:0", "admin")]
    [InlineData("Mappings:0:Modes:0", "Master")]
    [InlineData("Mappings:0:TenantId", "10000000-0000-0000-0000-000000000099")]
    public void Invalid_or_foreign_mapping_is_unavailable_without_fallback(string field, string value) {
        var c = Config(); c["WorkspaceNavigation:" + field] = value;
        var result = Describe(Actor(), c); Assert.Equal("Unavailable", result.State); Assert.Empty(result.Lenses); Assert.Null(result.Version);
    }
    [Fact]
    public void Absent_assignment_stays_missing_even_with_configured_catalog() =>
        Assert.Equal("Missing", Describe(Actor() with { RoleContext = null }).State);
    [Fact]
    public void Partial_multirole_mapping_and_ambiguous_duplicates_fail_closed() {
        var c = Config();
        foreach (var entry in c.Where(x => x.Key.StartsWith("WorkspaceNavigation:Mappings:0:")).ToArray())
            c[entry.Key.Replace("Mappings:0:", "Mappings:1:")] = entry.Value;
        Assert.Equal("Unavailable", Describe(Actor(), c).State);
        var partial = Config();
        foreach (var entry in partial.Where(x => x.Key.StartsWith("WorkspaceContext:RoleCatalog:Roles:0:")).ToArray())
            partial[entry.Key.Replace("Roles:0:", "Roles:1:")] = entry.Value;
        partial["WorkspaceContext:RoleCatalog:Roles:1:Reference"] = "20000000-0000-0000-0000-000000000002";
        Assert.Equal("Unavailable", Describe(Actor() with { RoleContext = Role + ",20000000-0000-0000-0000-000000000002" }, partial).State);
    }
    [Fact]
    public void Mode_exclusion_is_configured_empty_and_does_not_select_a_default_role() {
        var c = Config(); c["WorkspaceNavigation:Mappings:0:Modes:0"] = "Tug";
        c.Remove("WorkspaceNavigation:Mappings:0:Modes:1"); c.Remove("WorkspaceNavigation:Mappings:0:Modes:2");
        var result = Describe(Actor(), c); Assert.Equal("Configured", result.State); Assert.Empty(result.Lenses);
    }
    [Fact]
    public void Maintenance_requires_both_endpoints_and_tug_intake_requires_current_write_scope() {
        Assert.DoesNotContain(WorkspaceLensPolicy.Available(Actor("maintenance.read"), Vessel, VesselKind.Ship), i => i.Key == "maintenance");
        Assert.Contains(WorkspaceLensPolicy.Available(Actor("maintenance.read", "equipment.read"), Vessel, VesselKind.Ship), i => i.Key == "maintenance");
        Assert.DoesNotContain(WorkspaceLensPolicy.Available(Actor("mode.tug", "tug.read", "tug.write"), Vessel, VesselKind.Tug), i => i.Key == "tug-job-intake");
    }
}

public sealed partial class DirectoryWorkspaceTests
{
    private Dictionary<string, string?> LensConfig() {
        var c = ContextLabels(); c["WorkspaceNavigation:Version"] = "synthetic-menu-v1";
        c["WorkspaceNavigation:Mappings:0:TenantId"] = seed.Tenants[0].Id.ToString();
        c["WorkspaceNavigation:Mappings:0:Reference"] = ContextRole;
        foreach (var (key, index) in new[] { "fleet", "crew", "equipment", "maintenance", "import", "audit", "tug-command" }.Select((k, i) => (k, i)))
            c[$"WorkspaceNavigation:Mappings:0:Keys:{index}"] = key;
        foreach (var (mode, index) in new[] { "Fleet", "Ship", "Tug" }.Select((m, i) => (m, i)))
            c[$"WorkspaceNavigation:Mappings:0:Modes:{index}"] = mode;
        return c;
    }
    [Fact]
    public async Task Lens_endpoint_intersects_real_scope_and_preserves_explicit_power_search() {
        using var host = Host(manage: false, read: false, crewRead: true, search: true).WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(LensConfig())));
        using var client = Client(host);
        using var response = await client.GetAsync("/api/v1/workspace/lenses/" + SeedVessel.Id, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var dto = (await response.Content.ReadFromJsonAsync<WorkspaceLenses>(Ct))!;
        Assert.Equal(actorId, dto.ActorId); Assert.Equal(seed.Tenants[0].Id, dto.TenantId); Assert.Equal(SeedVessel.Id, dto.VesselId);
        Assert.True(dto.CanSearch); Assert.Equal(new[] { "fleet", "crew" }, Assert.Single(dto.Lenses).Items.Select(x => x.Key));
        Assert.DoesNotContain("FOREIGN", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/equipment-hierarchy/{SeedVessel.Id}", Ct)).StatusCode);
    }
    [Fact]
    public async Task Lens_current_session_rechecks_role_capability_mode_and_grant_revocation() {
        using var host = Host(manage: false, read: false, crewRead: true).WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(LensConfig())));
        using var client = Client(host);
        var path = "/api/v1/workspace/lenses/" + SeedVessel.Id;
        Assert.Contains(Assert.Single((await client.GetFromJsonAsync<WorkspaceLenses>(path, Ct))!.Lenses).Items, i => i.Key == "crew");
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var member = settings.Memberships[0];
        var permissions = member.Permissions; settings.Memberships[0] = member with { Permissions = permissions.Where(x => x != "crew.read").ToArray() };
        Assert.DoesNotContain(Assert.Single((await client.GetFromJsonAsync<WorkspaceLenses>(path, Ct))!.Lenses).Items, i => i.Key == "crew");
        settings.Memberships[0] = member with { RoleReferences = null };
        var missing = (await client.GetFromJsonAsync<WorkspaceLenses>(path, Ct))!;
        Assert.Equal("Missing", missing.State); Assert.Empty(missing.Lenses);
        settings.Memberships[0] = member with { Permissions = [.. permissions.Where(x => x is not ("mode.cross" or "mode.tug")), "mode.tug"] };
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, Ct)).StatusCode); // Ship no longer visible.
        settings.Memberships[0] = member with { VesselGrants = [] };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path, Ct)).StatusCode);
    }
    [Fact]
    public async Task Lens_does_not_reveal_cross_tenant_or_ungranted_scope() {
        using var host = Host(manage: false, read: false).WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(LensConfig())));
        using var client = Client(host);
        foreach (var vessel in seed.Vessels.Where(v => v.Id != SeedVessel.Id)) {
            var response = await client.GetAsync("/api/v1/workspace/lenses/" + vessel.Id, Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain(vessel.Name, body); Assert.DoesNotContain("lenses", body); Assert.DoesNotContain("count", body);
        }
    }
    [Theory]
    [InlineData("role")]
    [InlineData("tenantId")]
    [InlineData("mode")]
    [InlineData("capability")]
    public async Task Lens_rejects_all_query_authority_selectors(string key) {
        using var host = Host(); using var client = Client(host);
        var response = await client.GetAsync("/api/v1/workspace/lenses?" + key + "=admin", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.DoesNotContain("lenses", await response.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Lens_anonymous_denial_and_configuration_failure_never_fabricate_role() {
        using var host = Host(); using var anonymous = Client(host, false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/workspace/lenses", Ct)).StatusCode);
        using var client = Client(host);
        var dto = (await client.GetFromJsonAsync<WorkspaceLenses>("/api/v1/workspace/lenses", Ct))!;
        Assert.Equal("Unavailable", dto.State); Assert.Empty(dto.Lenses); Assert.False(dto.CanSearch);
    }
}
