using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-023 / T-S2-004: 61.03 §33 view states never become a false green.
[Trait("Requirement", "DEV-023")]
public sealed class TodayViewStatePolicyTests
{
    [Theory]
    [InlineData(new[] { "Unavailable", "Unavailable" }, 0, "Unavailable")]
    [InlineData(new string[0], 0, "Unavailable")]
    [InlineData(new[] { "Available", "Unavailable" }, 3, "Partial")]
    [InlineData(new[] { "Missing", "Unavailable" }, 0, "Partial")]
    [InlineData(new[] { "Available", "Missing" }, 0, "Empty")]
    [InlineData(new[] { "Available", "Missing" }, 2, "Ready")]
    public void Classifies_coverage_without_inferring_clearance(string[] coverage, int items, string expected) =>
        Assert.Equal(expected, TodayViewStatePolicy.Classify(coverage, items));

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Ready")]
    [InlineData("")]
    public void Unrecognized_source_status_is_never_promoted_to_readable(string status) =>
        Assert.Equal("Unavailable", TodayViewStatePolicy.Classify(["Available", status], 1));

    [Fact]
    public void Current_today_placeholders_can_never_yield_ready_or_empty()
    {
        // OperationalTasks/SafetyDecisions/Permits/EvidenceVerification are fixed Unavailable until their owners exist.
        string[] coverage = ["Available", "Unavailable", "Unavailable", "Unavailable", "Unavailable", "Available", "Available"];
        Assert.Equal("Partial", TodayViewStatePolicy.Classify(coverage, 0));
        Assert.Equal("Partial", TodayViewStatePolicy.Classify(coverage, 25));
    }

    [Fact]
    public void Rejects_negative_counts() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TodayViewStatePolicy.Classify(["Available"], -1));
}
