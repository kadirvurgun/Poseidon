using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class WorkOrderNavigationTests
{
    [Theory]
    [InlineData(VesselKind.Ship)]
    [InlineData(VesselKind.Tug)]
    public void Work_order_discovery_requires_every_owning_read_permission_without_creating_write_authority(VesselKind kind)
    {
        var vessel = Guid.NewGuid();
        var permissions = new[] { "directory.read", "workorder.read", "equipment.read", "maintenance.read" };
        if (kind == VesselKind.Tug) permissions = [.. permissions, "mode.tug"];
        var reader = new TrustedActor(Guid.NewGuid(), Guid.NewGuid(), permissions, true, false, []);
        Assert.Contains(WorkspaceEndpoints.Destinations(reader), item => item.Key == "work-order-center" && item.Href == "/app/work-order-center");
        Assert.Contains(WorkspaceLensPolicy.Available(reader, vessel, kind), item => item.Key == "work-order-center" && item.Href == $"/app/work-order-center/{vessel}");
        Assert.False(WorkOrderEndpoints.CanWrite(reader, vessel));
        foreach (var denied in new[] { "workorder.read", "equipment.read", "maintenance.read" }) {
            var changed = reader with { Permissions = permissions.Where(p => p != denied).ToArray() };
            Assert.DoesNotContain(WorkspaceEndpoints.Destinations(changed), item => item.Key == "work-order-center");
            Assert.DoesNotContain(WorkspaceLensPolicy.Available(changed, vessel, kind), item => item.Key == "work-order-center");
        }
    }
}
