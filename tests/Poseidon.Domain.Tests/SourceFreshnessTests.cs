using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public class SourceFreshnessTests
{
    [Theory]
    [InlineData(null, 60, SourceFreshness.Unknown)]
    [InlineData(0, 60, SourceFreshness.Current)]
    [InlineData(-60, 60, SourceFreshness.Current)]
    [InlineData(-61, 60, SourceFreshness.Stale)]
    [InlineData(1, 60, SourceFreshness.Future)]
    [InlineData(0, 0, SourceFreshness.Unavailable)]
    [InlineData(0, -1, SourceFreshness.Unavailable)]
    [InlineData(null, null, SourceFreshness.Unavailable)]
    public void Unknown_future_and_unconfigured_are_not_current(int? offsetSeconds, int? maxAgeSeconds, SourceFreshness expected)
    {
        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, SourceFreshnessPolicy.Assess(offsetSeconds == null ? null : now.AddSeconds(offsetSeconds.Value), now,
            maxAgeSeconds == null ? null : TimeSpan.FromSeconds(maxAgeSeconds.Value)));
    }
}
