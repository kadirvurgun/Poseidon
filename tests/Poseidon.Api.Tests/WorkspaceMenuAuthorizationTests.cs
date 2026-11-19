using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-022 / T-S2-003: role-first menu, authorized search, no hidden menu/count leakage.
// Sources: 65.03 paragraph 50-51 and 512; 54.02 paragraph 120, 512, 520-521, 803 and
// IAM_READ_V1 effectiveMenu (paragraph 13368); 61.03 paragraph 6, 110, 186 and 355-357.
// Real API host and PostgreSQL; the browser never decides authorization.
public sealed partial class DirectoryWorkspaceTests
{
    private const string Dev022SecondRole = "6f0c1b8e-3d4a-4c5e-9f7a-2b1d0e9c8a71";

    private Dictionary<string, string?> Dev022TwoRoleMenu()
    {
        var c = ContextLabels();
        c["WorkspaceContext:RoleCatalog:Roles:2:TenantId"] = seed.Tenants[0].Id.ToString();
        c["WorkspaceContext:RoleCatalog:Roles:2:Reference"] = Dev022SecondRole;
        c["WorkspaceContext:RoleCatalog:Roles:2:Tr"] = "KURGUSAL ikinci rol";
        c["WorkspaceContext:RoleCatalog:Roles:2:En"] = "FICTIONAL second role";
        c["Authentication:Oidc:Memberships:0:RoleReferences:1"] = Dev022SecondRole;
        c["WorkspaceNavigation:Version"] = "synthetic-menu-v1";
        void Map(int index, string role, string[] keys)
        {
            c[$"WorkspaceNavigation:Mappings:{index}:TenantId"] = seed.Tenants[0].Id.ToString();
            c[$"WorkspaceNavigation:Mappings:{index}:Reference"] = role;
            for (var i = 0; i < keys.Length; i++) c[$"WorkspaceNavigation:Mappings:{index}:Keys:{i}"] = keys[i];
            c[$"WorkspaceNavigation:Mappings:{index}:Modes:0"] = "Fleet";
            c[$"WorkspaceNavigation:Mappings:{index}:Modes:1"] = "Ship";
        }
        Map(0, ContextRole, ["fleet", "today", "audit"]);
        Map(1, Dev022SecondRole, ["crew", "maintenance", "audit"]);
        return c;
    }

    private static IEnumerable<string> PropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nested in PropertyNames(property.Value)) yield return nested;
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                foreach (var nested in PropertyNames(item)) yield return nested;
    }

    // DEV022-AC-02/03: each role lens shows only the role's mapped keys that the current
    // membership can actually open; a role mapping never adds a hidden item.
    [Fact]
    public async Task Dev022_role_lenses_are_role_specific_and_intersect_current_permissions()
    {
        using var host = Host(manage: false, read: false, crewRead: true).WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Dev022TwoRoleMenu())));
        using var client = Client(host);
        var lenses = (await client.GetFromJsonAsync<WorkspaceLenses>("/api/v1/workspace/lenses/" + SeedVessel.Id, Ct))!;
        Assert.Equal("Configured", lenses.State);
        var first = lenses.Lenses.Single(l => l.Reference == Guid.Parse(ContextRole));
        var second = lenses.Lenses.Single(l => l.Reference == Guid.Parse(Dev022SecondRole));
        Assert.Equal(["fleet", "today"], first.Items.Select(i => i.Key).Order().ToArray());
        // maintenance needs equipment.read + maintenance.read, audit needs audit.read: both hidden.
        Assert.Equal(["crew"], second.Items.Select(i => i.Key).ToArray());
        var body = await client.GetStringAsync("/api/v1/workspace/lenses/" + SeedVessel.Id, Ct);
        Assert.DoesNotContain("audit", body); Assert.DoesNotContain("maintenance", body);

        using var auditHost = Host(manage: false, read: false, crewRead: true, auditRead: true, technicalRead: true).WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Dev022TwoRoleMenu())));
        using var auditClient = Client(auditHost);
        var granted = (await auditClient.GetFromJsonAsync<WorkspaceLenses>("/api/v1/workspace/lenses/" + SeedVessel.Id, Ct))!;
        Assert.Equal(["audit", "fleet", "today"], granted.Lenses.Single(l => l.Reference == Guid.Parse(ContextRole)).Items.Select(i => i.Key).Order().ToArray());
        Assert.Equal(["audit", "crew", "maintenance"], granted.Lenses.Single(l => l.Reference == Guid.Parse(Dev022SecondRole)).Items.Select(i => i.Key).Order().ToArray());
    }

    // DEV022-AC-04/05: a hidden destination does not match search and the page is byte-identical
    // to a query that matches nothing, so neither result nor hasMore reveals the hidden item.
    [Fact]
    public async Task Dev022_search_hides_unauthorized_destinations_without_a_distinguishable_response()
    {
        await using var host = Host(manage: false, read: false, search: true); using var client = Client(host);
        var hidden = await client.GetStringAsync("/api/v1/workspace/search?q=audit", Ct);
        var nothing = await client.GetStringAsync("/api/v1/workspace/search?q=zz-no-such-entry", Ct);
        Assert.Equal(nothing, hidden);
        Assert.Empty(JsonSerializer.Deserialize<PageDto<SearchItemDto>>(hidden, JsonSerializerOptions.Web)!.Items);

        await using var auditHost = Host(manage: false, read: false, search: true, auditRead: true); using var auditClient = Client(auditHost);
        var visible = (await auditClient.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=AUDIT", Ct))!;
        var item = Assert.Single(visible.Items);
        Assert.Equal(("audit", "/app/audit", "Destination"), (item.Key, item.Href, item.Kind));
    }

    // DEV022-AC-04/05 (QA follow-up): destinations match by their TR/EN display label as well as the
    // route key, with Turkish-aware folding, but only after the scope projection. A hidden
    // destination's label is byte-identical to a no-match. "Denetim" is not a label the UI shows for
    // audit (TR label is "İşlem geçmişi"), so it matches nothing rather than an invented synonym.
    [Fact]
    public async Task Dev022_search_matches_authorized_destinations_by_tr_and_en_label()
    {
        await using var host = Host(manage: false, read: false, search: true); using var client = Client(host);
        var nothing = await client.GetStringAsync("/api/v1/workspace/search?q=zz-no-such-entry", Ct);
        foreach (var hidden in new[] { "İşlem geçmişi", "Action history", "işlem GEÇMİŞİ", "Denetim", "Şirketler", "Companies" })
            Assert.Equal(nothing, await client.GetStringAsync("/api/v1/workspace/search?q=" + Uri.EscapeDataString(hidden), Ct));
        foreach (var fleet in new[] { "Filo", "FİLO", "filo", "Fleet" })
        {
            var page = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=" + Uri.EscapeDataString(fleet), Ct))!;
            Assert.Equal(("fleet", "/app/fleet", "Destination"), (page.Items[0].Key, page.Items[0].Href, page.Items[0].Kind));
            Assert.DoesNotContain(page.Items, i => i.Kind == "Destination" && i.Key != "fleet");
        }

        await using var auditHost = Host(manage: false, read: false, search: true, auditRead: true); using var auditClient = Client(auditHost);
        foreach (var label in new[] { "İşlem geçmişi", "islem gecmisi", "IŞLEM", "Action history", "action HISTORY" })
        {
            var page = (await auditClient.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=" + Uri.EscapeDataString(label), Ct))!;
            var item = Assert.Single(page.Items);
            Assert.Equal(("audit", "/app/audit", "Destination"), (item.Key, item.Href, item.Kind));
            Assert.False(page.HasMore);
        }
        Assert.Equal(nothing, await auditClient.GetStringAsync("/api/v1/workspace/search?q=Denetim", Ct));
        Assert.Equal(nothing, await auditClient.GetStringAsync("/api/v1/workspace/search?q=" + Uri.EscapeDataString("Şirketler"), Ct));
    }

    // DEV022-AC-04 (QA follow-up): the server TR/EN label map equals the labels the frontend shows
    // for every destination key the server can return (search result and sidebar menu labels).
    [Fact]
    public void Dev022_search_label_map_matches_frontend_tr_en_catalog()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        var src = Path.Combine(root!.FullName, "frontend", "src");
        Dictionary<string, string> Pairs(string locale)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(src, "i18n", "messages", locale + ".json")));
            return doc.RootElement.GetProperty("pairs").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        }
        Dictionary<string, string> Grab(string file, string pattern) =>
            System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(Path.Combine(src, "product", file)), pattern)
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        var tr = Pairs("tr"); var en = Pairs("en");
        var search = Grab("workspace-lens-api.ts", "'?([a-z-]+)'?: \\[sourceText\\(\"tr\", \"pairs\\.(m_[0-9a-f]+)\"\\)");
        foreach (var (key, pair) in Grab("WorkspaceSearch.tsx", "'?([a-z-]+)'?: localizedText\\(en, \"pairs\\.(m_[0-9a-f]+)\"\\)")) search[key] = pair;
        var menu = Grab("EnterpriseChrome.tsx", "'?([a-z-]+)'?: \\{ tr: sourceText\\(\"tr\", \"pairs\\.(m_[0-9a-f]+)\"\\)");
        // Every destination key the server projection can emit (WorkspaceEndpoints.Destinations).
        var serverKeys = System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(Path.Combine(root.FullName, "backend", "Poseidon.Api", "WorkspaceEndpoints.cs")), "new\\s*(?:NavigationDto)?\\(\\s*\"([a-z-]+)\"\\s*,\\s*\"/app/")
            .Select(m => m.Groups[1].Value).Distinct().ToArray();
        Assert.Contains("audit", serverKeys); Assert.Contains("engine-daily-log", serverKeys);
        Assert.Equal(serverKeys.Order(StringComparer.Ordinal), WorkspaceDestinationLabels.Labels.Keys.Order(StringComparer.Ordinal));
        foreach (var key in serverKeys)
        {
            Assert.True(search.ContainsKey(key) && menu.ContainsKey(key), "frontend label missing for " + key);
            string[] expected = [tr[search[key]], en[search[key]], tr[menu[key]], en[menu[key]]];
            Assert.Equal(expected.Distinct().Order(StringComparer.Ordinal).ToArray(),
                WorkspaceDestinationLabels.Labels[key].Order(StringComparer.Ordinal).ToArray());
        }
    }

    [Theory]
    [InlineData("İŞLEM GEÇMİŞİ", "islem gecmisi")]
    [InlineData("Işık", "isik")]
    [InlineData("ıiIİ", "iiii")]
    [InlineData("Öğle  ", "ogle")]
    [InlineData("Şirketler", "sirketler")]
    public void Dev022_search_folding_is_turkish_aware_and_culture_independent(string value, string folded)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "tr-TR", "en-US", "" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                Assert.Equal(folded, WorkspaceDestinationLabels.Fold(value));
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    // DEV022-AC-04/05: vessel search is case-insensitive inside scope; a foreign tenant vessel with
    // the same code and an ungranted same-tenant vessel never match and leave no trace.
    [Fact]
    public async Task Dev022_vessel_search_is_scoped_before_matching_and_hidden_matches_are_indistinguishable()
    {
        await using var host = Host(manage: false, read: false, search: true); using var client = Client(host);
        var page = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=" + Uri.EscapeDataString(SeedVessel.Code.ToLowerInvariant()), Ct))!;
        var hit = Assert.Single(page.Items);
        Assert.Equal(SeedVessel.Id.ToString(), hit.Key); Assert.False(page.HasMore);
        var foreign = seed.Vessels.Single(v => v.TenantId == seed.Tenants[1].Id);
        Assert.Equal(SeedVessel.Code, foreign.Code); // Same code in another tenant: only the in-scope one is returned.
        var nothing = await client.GetStringAsync("/api/v1/workspace/search?q=zz-no-such-vessel", Ct);
        foreach (var other in seed.Vessels.Where(v => v.Id != SeedVessel.Id))
        {
            var body = await client.GetStringAsync("/api/v1/workspace/search?q=" + Uri.EscapeDataString(other.Name.ToUpperInvariant()), Ct);
            Assert.Equal(nothing, body);
            Assert.DoesNotContain(other.Id.ToString(), body);
        }
    }

    // DEV022-AC-06: authority selectors are rejected, never silently ignored or honoured.
    [Theory]
    [InlineData("/api/v1/workspace/search?tenantId=10000000-0000-0000-0000-000000000002")]
    [InlineData("/api/v1/workspace/search?vesselId=10000000-0000-0000-0000-000000000002")]
    [InlineData("/api/v1/workspace/search?role=admin")]
    [InlineData("/api/v1/workspace/search?mode=Tug")]
    [InlineData("/api/v1/workspace/search?permissions=audit.read")]
    [InlineData("/api/v1/workspace/search?q=a&q=b")]
    [InlineData("/api/v1/workspace?role=admin")]
    [InlineData("/api/v1/workspace?tenantId=10000000-0000-0000-0000-000000000002")]
    public async Task Dev022_menu_and_search_reject_client_authority_selectors(string path)
    {
        await using var host = Host(manage: false, read: false, search: true); using var client = Client(host);
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("items", body); Assert.DoesNotContain("destinations", body);
        foreach (var vessel in seed.Vessels) Assert.DoesNotContain(vessel.Name, body);
        await using var db = Db();
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Result == "Invalid" &&
            e.Action == (path.Contains("/search") ? "WorkspaceSearch" : "WorkspaceRead") && e.ActorId == actorId.ToString());
    }

    // DEV022-AC-07: menu, lens and search projections are closed shapes with no count, total,
    // hidden/redacted aggregate or denied-action list (54.02 paragraph 803, 61.03 paragraph 110).
    [Fact]
    public async Task Dev022_menu_lens_and_search_projections_carry_no_hidden_count_or_aggregate()
    {
        using var host = Host(manage: false, read: false, search: true, crewRead: true).WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Dev022TwoRoleMenu())));
        using var client = Client(host);
        string[] allowed = ["destinations", "key", "href", "canSearch", "canWriteRegulatory", "items", "offset", "limit", "hasMore", "label", "kind",
            "tenantId", "actorId", "vesselId", "mode", "state", "version", "lenses", "reference", "tr", "en", "group"];
        foreach (var path in new[] { "/api/v1/workspace", "/api/v1/workspace/lenses", "/api/v1/workspace/lenses/" + SeedVessel.Id, "/api/v1/workspace/search", "/api/v1/workspace/search?q=zz" })
        {
            using var document = JsonDocument.Parse(await client.GetStringAsync(path, Ct));
            var names = PropertyNames(document.RootElement).Distinct().ToArray();
            Assert.All(names, name => Assert.Contains(name, allowed));
            Assert.DoesNotContain(names, n => n.Contains("count", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("total", StringComparison.OrdinalIgnoreCase) || n.Contains("hidden", StringComparison.OrdinalIgnoreCase));
        }
    }

    // DEV-022 / DEV-035 (T-S4-003): the offline conflict center is a server-projected destination only when
    // its list endpoint can return at least one source for the current actor (crew, tug or a daily kind).
    // Without such a right the menu omits it and its TR/EN label search is byte-identical to a no-match.
    [Fact]
    public async Task Dev022_conflict_center_destination_follows_the_conflict_list_permissions()
    {
        const string key = "offline-conflict-center", href = "/app/offline-conflict-center";
        await using (var host = Host(manage: false, read: false, search: true))
        {
            using var client = Client(host);
            var menu = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
            Assert.DoesNotContain(menu.Destinations, d => d.Key == key || d.Href == href);
            var nothing = await client.GetStringAsync("/api/v1/workspace/search?q=zz-no-such-entry", Ct);
            foreach (var hidden in new[] { "offline-conflict", "Material conflict center", "Önemli çakışma merkezi", "ONEMLI CAKISMA" })
                Assert.Equal(nothing, await client.GetStringAsync("/api/v1/workspace/search?q=" + Uri.EscapeDataString(hidden), Ct));
        }
        foreach (var (crew, tug) in new[] { (true, false), (false, true) })
        {
            await using var host = Host(manage: false, read: false, search: true, crewRead: crew, tugRead: tug);
            using var client = Client(host);
            var menu = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
            Assert.Single(menu.Destinations, d => d.Key == key && d.Href == href);
            foreach (var label in new[] { "Material conflict center", "önemli ÇAKIŞMA merkezi", "onemli cakisma" })
            {
                var page = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=" + Uri.EscapeDataString(label), Ct))!;
                var item = Assert.Single(page.Items, i => i.Kind == "Destination");
                Assert.Equal((key, href), (item.Key, item.Href));
            }
            using var list = await client.GetAsync($"/api/v1/sync/conflicts?vesselId={SeedVessel.Id:D}", Ct);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        }
    }

    // DEV022-AC-08: a foreign-tenant, an ungranted same-tenant and a nonexistent vessel produce the
    // same lens denial, so the menu route cannot be used to probe vessel existence.
    [Fact]
    public async Task Dev022_lens_denials_for_foreign_ungranted_and_missing_vessels_are_identical()
    {
        using var host = Host(manage: false, read: false).WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Dev022TwoRoleMenu())));
        using var client = Client(host);
        var targets = seed.Vessels.Where(v => v.Id != SeedVessel.Id).Select(v => v.Id).Append(Guid.NewGuid()).ToArray();
        var bodies = new List<string>();
        foreach (var target in targets)
        {
            using var response = await client.GetAsync("/api/v1/workspace/lenses/" + target, Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            bodies.Add(await response.Content.ReadAsStringAsync(Ct));
        }
        Assert.Single(bodies.Distinct());
        await using var db = Db();
        var events = await db.Set<SecurityEvent>().Where(e => e.Action == "WorkspaceRead" && e.Result == "NotFound").ToArrayAsync(Ct);
        Assert.Equal(targets.Order().ToArray(), events.Select(e => e.ResourceId!.Value).Order().ToArray());
    }
}
