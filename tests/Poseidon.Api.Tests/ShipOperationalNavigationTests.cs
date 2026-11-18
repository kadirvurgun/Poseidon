using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// Current server authority controls both global discovery and saved-source
// authoring launches. Actual API source tests, execution DEFERRED.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Daily_origin_launches_require_actual_target_write_while_global_operational_navigation_allows_read_only() {
        await using var host = Host(); using var client = Client(host);
        DailyPermissions(host.Services, write: false); ShipOpsPermissions(host.Services, write: false);
        var workspace = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
        Assert.Contains(workspace.Destinations, item => item.Key == "fuel-bunker-log");
        Assert.Contains(workspace.Destinations, item => item.Key == "port-activity-sof");
        var readOnly = (await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath("noon-reports", suffix: "/capabilities"), Ct))!;
        Assert.DoesNotContain(readOnly.Destinations, item => item.Key is "fuel-bunker-log" or "port-activity-sof");
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = [.. member.Permissions, "operations.fuel.write", "operations.port.write"] };
        var noon = (await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath("noon-reports", suffix: "/capabilities"), Ct))!;
        Assert.False(noon.CanWrite); // Read-only source can still be an authorized source for a different writer.
        Assert.Contains(noon.Destinations, item => item.Key == "fuel-bunker-log" && item.Href == $"/app/fuel-bunker-log/{SeedVessel.Id}");
        Assert.Contains(noon.Destinations, item => item.Key == "port-activity-sof");
        var engine = (await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath("engine-daily-logs", suffix: "/capabilities"), Ct))!;
        Assert.Contains(engine.Destinations, item => item.Key == "fuel-bunker-log");
        Assert.DoesNotContain(engine.Destinations, item => item.Key == "port-activity-sof");
        var deck = (await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath("deck-daily-logs", suffix: "/capabilities"), Ct))!;
        Assert.DoesNotContain(deck.Destinations, item => item.Key is "fuel-bunker-log" or "port-activity-sof");
    }
}
