using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class DowntimeNavigationPolicyTests
{
    [Fact]
    public void Cross_mode_scope_never_turns_a_Tug_into_a_supported_Ship_downtime_context()
    {
        var vessel = Guid.NewGuid();
        var reader = new TrustedActor(Guid.NewGuid(), Guid.NewGuid(), ["directory.read", "operations.downtime.read", "mode.tug", "mode.cross"], true, false, []);
        Assert.Contains(WorkspaceEndpoints.Destinations(reader), item => item.Key == "operational-downtime");
        Assert.Contains(WorkspaceLensPolicy.Available(reader, vessel, VesselKind.Ship), item => item.Key == "operational-downtime" && item.Href == $"/app/operational-downtime/{vessel}");
        Assert.DoesNotContain(WorkspaceLensPolicy.Available(reader, vessel, VesselKind.Tug), item => item.Key == "operational-downtime");
        Assert.False(OperationalDowntimeEndpoints.CanWrite(reader, vessel));
        Assert.DoesNotContain(WorkspaceEndpoints.Destinations(reader with { Permissions = ["directory.read", "operations.downtime.read", "mode.tug"] }), item => item.Key == "operational-downtime");
    }
}

public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Engine_source_launch_requires_the_actual_target_write_grant_and_never_appears_on_other_daily_families()
    {
        await using var host = Host(); using var client = Client(host); DailyPermissions(host.Services, write: false);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = [.. member.Permissions, "operations.downtime.read"] };
        var readOnly = (await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath("engine-daily-logs", suffix: "/capabilities"), Ct))!;
        Assert.DoesNotContain(readOnly.Destinations, item => item.Key == "operational-downtime");
        member = settings.Memberships[0]; settings.Memberships[0] = member with { Permissions = [.. member.Permissions, "operations.downtime.write"] };
        var engine = (await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath("engine-daily-logs", suffix: "/capabilities"), Ct))!;
        Assert.Contains(engine.Destinations, item => item.Key == "operational-downtime" && item.Href == $"/app/operational-downtime/{SeedVessel.Id}");
        foreach (var resource in new[] { "noon-reports", "deck-daily-logs" }) {
            var other = (await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath(resource, suffix: "/capabilities"), Ct))!;
            Assert.DoesNotContain(other.Destinations, item => item.Key == "operational-downtime");
        }
    }
}
