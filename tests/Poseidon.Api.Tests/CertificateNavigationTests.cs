using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-022/043/044. Real current membership and owning scope, authored NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Certificate_navigation_and_search_follow_current_owning_read_permission_without_granting_write_or_foreign_access() {
        await using var host = Host(manage: false, read: false, edit: false, search: true); using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Permissions", "certificate.read certificate.write");
        var denied = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
        Assert.DoesNotContain(denied.Destinations, item => item.Key is "certificate-command-center" or "renewal-workflow");
        CertificatePermissions(host.Services, write: false);
        var allowed = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
        Assert.Contains(allowed.Destinations, item => item.Key == "certificate-command-center" && item.Href == "/app/certificate-command-center");
        Assert.Contains(allowed.Destinations, item => item.Key == "renewal-workflow" && item.Href == "/app/renewal-workflow");
        var matches = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=certificate-command-center", Ct))!;
        Assert.Equal("certificate-command-center", Assert.Single(matches.Items).Key);
        var cap = (await client.GetFromJsonAsync<CertificateCapabilitiesDto>($"/api/v1/certificate-command-center/capabilities?vesselId={SeedVessel.Id}", Ct))!;
        Assert.False(cap.CanWriteVessel); Assert.False(cap.CanVerify); Assert.False(cap.CanCompleteRenewal);
        var foreign = seed.Vessels.First(vessel => vessel.TenantId != SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/certificate-command-center?vesselId={foreign.Id}", Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = member.Permissions.Where(permission => permission != "certificate.read").ToArray() };
        var revoked = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
        Assert.DoesNotContain(revoked.Destinations, item => item.Key is "certificate-command-center" or "renewal-workflow");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/certificate-command-center?vesselId={SeedVessel.Id}", Ct)).StatusCode);
    }
}
