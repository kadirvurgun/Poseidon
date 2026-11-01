using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    // NAV-01/02, DEV-022. Real PostgreSQL/API, not client-side menu authorization.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restricted_navigation_search_and_session_reveal_only_granted_vessel(bool edit)
    {
        await using var host = Host(manage: false, read: false, edit: edit, search: true);
        using var client = Client(host);
        var session = (await client.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", Ct))!;
        Assert.Null(session.TenantName); Assert.False(session.CanReadCompanies); Assert.True(session.CanSearch);
        var workspace = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
        Assert.Equal(["today", "fleet"], workspace.Destinations.Select(x => x.Key).ToArray());
        var page = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search", Ct))!;
        Assert.Equal(["today", "fleet", SeedVessel.Id.ToString()], page.Items.Select(x => x.Key).ToArray());
        Assert.False(page.HasMore);
        var body = await client.GetStringAsync("/api/v1/workspace/search?tenantId=" + seed.Tenants[1].Id, Ct);
        Assert.DoesNotContain(SeedVessel.CompanyId.ToString(), body);
        Assert.DoesNotContain(seed.Companies[0].Name, body);
        foreach (var vessel in seed.Vessels.Where(v => v.Id != SeedVessel.Id))
        {
            Assert.DoesNotContain(vessel.Id.ToString(), body);
            using var denied = await client.GetAsync($"/api/v1/vessels/{vessel.Id}", Ct);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            var empty = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=" + Uri.EscapeDataString(vessel.Name), Ct))!;
            Assert.Empty(empty.Items); Assert.False(empty.HasMore);
        }
        using var company = await client.GetAsync($"/api/v1/companies/{SeedVessel.CompanyId}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, company.StatusCode);
    }

    [Fact]
    public async Task Search_is_explicit_default_deny_and_request_claims_do_not_grant_it()
    {
        await using var host = Host(); using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Permissions", "workspace.search");
        var workspace = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
        Assert.False(workspace.CanSearch);
        using var denied = await client.GetAsync("/api/v1/workspace/search?permissions=workspace.search&role=admin", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var db = Db();
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "WorkspaceSearch" && e.Result == "Denied" && e.ActorId == actorId.ToString());
    }

    [Fact]
    public async Task Search_pagination_is_stable_bounded_and_audited_without_query_text()
    {
        await using var host = Host(search: true); using var client = Client(host);
        var all = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search", Ct))!;
        var collected = new List<SearchItemDto>();
        for (var offset = 0; offset < all.Items.Length; offset++)
        {
            var page = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>($"/api/v1/workspace/search?offset={offset}&limit=1", Ct))!;
            collected.Add(Assert.Single(page.Items));
            Assert.Equal(offset < all.Items.Length - 1, page.HasMore);
        }
        Assert.Equal(all.Items, collected.ToArray());
        Assert.Equal(collected.Count, collected.Select(x => x.Key).Distinct().Count());
        foreach (var query in new[] { "limit=26", "limit=0", "offset=-1", "offset=100001", "q=%00", "q=" + new string('a', 101) })
        {
            using var invalid = await client.GetAsync("/api/v1/workspace/search?" + query, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        var missing = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=synthetic-private-query", Ct))!;
        Assert.Empty(missing.Items);
        await using var db = Db();
        var events = await db.Set<SecurityEvent>().Where(e => e.Action == "WorkspaceSearch").ToArrayAsync(Ct);
        Assert.Contains(events, e => e.Result == "Success"); Assert.Contains(events, e => e.Result == "Invalid");
        Assert.All(events, e => { Assert.Equal(seed.Tenants[0].Id, e.TenantId); Assert.Null(e.ResourceId); Assert.Contains(e.Result, new[] { "Success", "Invalid" }); });
    }

    [Fact]
    public async Task Navigation_and_search_revalidate_revoked_scope_and_anonymous_access()
    {
        await using var host = Host(manage: false, read: false, search: true);
        using var anonymous = Client(host, false);
        foreach (var path in new[] { "/api/v1/workspace", "/api/v1/workspace/search" })
        {
            using var denied = await anonymous.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using var client = Client(host);
        Assert.True((await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!.CanSearch);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = [settings.Memberships[0] with { VesselGrants = [] }];
        foreach (var path in new[] { "/api/v1/workspace", "/api/v1/workspace/search", $"/api/v1/vessels/{SeedVessel.Id}" })
        {
            using var denied = await client.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
    }

    [Fact]
    public async Task Search_does_not_deliver_data_when_mandatory_audit_fails()
    {
        await using var host = Host(search: true); using var client = Client(host);
        await Sql("""CREATE TRIGGER nav_reject BEFORE INSERT ON audit."SecurityEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();""");
        using var response = await client.GetAsync("/api/v1/workspace/search", Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(SeedVessel.Name, await response.Content.ReadAsStringAsync(Ct));
    }
}
