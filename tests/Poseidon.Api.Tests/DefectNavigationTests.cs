using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class DefectNavigationTests
{
    private static readonly Guid Vessel = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static TrustedActor Reader(params string[] permissions) => new(Guid.NewGuid(), Guid.NewGuid(),
        ["directory.read", .. permissions], true, false, []);

    [Theory]
    [InlineData(VesselKind.Ship)]
    [InlineData(VesselKind.Tug)]
    public void Common_technical_navigation_respects_the_actual_supported_context_and_never_grants_defect_writes(VesselKind kind)
    {
        var reader = kind == VesselKind.Tug ? Reader("mode.tug", "defect.read", "equipment.read") : Reader("defect.read", "equipment.read");
        Assert.Contains(WorkspaceEndpoints.Destinations(reader), item => item.Key == "defects" && item.Href == "/app/defects");
        var destination = Assert.Single(WorkspaceLensPolicy.Available(reader, Vessel, kind), item => item.Key == "defects");
        Assert.Equal($"/app/defects/{Vessel}", destination.Href); Assert.Equal("daily", destination.Group);
        Assert.False(DefectEndpoints.CanWrite(reader, Vessel));
        Assert.DoesNotContain(WorkspaceLensPolicy.Available(Reader("equipment.read"), Vessel, kind), item => item.Key == "defects");
        Assert.DoesNotContain(WorkspaceLensPolicy.Available(Reader("defect.read"), Vessel, kind), item => item.Key == "defects");
    }
}
