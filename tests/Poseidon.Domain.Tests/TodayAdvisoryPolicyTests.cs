using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;
public sealed class TodayAdvisoryPolicyTests
{
    [Fact]
    public void Absent_is_distinct_from_persisted_missing_inputs()
    {
        Assert.Equal(new DraftObservation(0, 0, 0, "DraftAbsent"), TodayAdvisoryPolicy.Observe(null));
        Assert.Equal(new DraftObservation(0, 17, 0, "DraftUnverified"), TodayAdvisoryPolicy.Observe(new()));
    }
    [Fact]
    public void Zero_and_unknown_are_preserved_without_readiness_or_mandatory_policy()
    {
        var content = new RegulatoryDraftContent { Gt = new("Known", "0"), Flag = new("Unknown"), SourceObservedAt = new("Unknown") };
        Assert.Equal(new DraftObservation(1, 14, 2, "DraftUnverified"), TodayAdvisoryPolicy.Observe(content));
        Assert.Equal("0", content.Gt.Value);
    }

    [Fact]
    public void Every_input_declared_still_has_no_verification_or_safety_clearance()
    {
        var declared = new TextInput("Known", "synthetic");
        var measurement = new NumberInput("Known", "0.00000000000000000001");
        var content = new RegulatoryDraftContent
        {
            Imo = new("Known", "1234567"), Flag = declared, ShipType = declared, ClassSociety = declared, ClassNumber = declared,
            Gt = measurement, Nt = measurement, Dwt = measurement, Loa = measurement, Breadth = measurement, Draft = measurement,
            BuildDate = new("Known", "2000-01-01"), TradingArea = declared, CargoContext = declared,
            Declaration = declared, SourceReference = declared, SourceObservedAt = new("Known", "2001-01-01T00:00:00Z")
        };
        Assert.Equal(new DraftObservation(17, 0, 0, "DraftUnverified"), TodayAdvisoryPolicy.Observe(content));
        Assert.Equal(measurement.Value, content.Gt.Value);
    }
}
