using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// OFFLINE-APPLICATION-01 / TC46-crew-roster-watch_group. AUTHORED, NOT RUN.
public sealed class OfflineWatchCaptureTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("SYNTHETIC-A", true)]
    [InlineData("synthetic-a", false)]
    [InlineData("", false)]
    [InlineData("SYNTHETIC-A\n", false)]
    [InlineData("unconfigured", false)]
    public void Capture_preserves_unknown_and_requires_exact_explicit_controlled_value(string? value, bool allowed) =>
        Assert.Equal(allowed, OfflineWatchCapture.ValidValue(value, ["SYNTHETIC-A"]));

    [Theory]
    [InlineData(-1, 0, false)]
    [InlineData(0, 0, true)]
    [InlineData(1, -840, true)]
    [InlineData(1, 840, true)]
    [InlineData(1, 841, false)]
    [InlineData(61, 0, false)]
    public void Captured_provenance_is_bounded_by_server_enrollment_and_receipt_time(int seconds, int offset, bool allowed)
    {
        var enrolled = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(allowed, OfflineWatchCapture.ValidCaptureTime(enrolled.AddSeconds(seconds), offset, enrolled, enrolled.AddMinutes(1), enrolled.AddMinutes(2)));
        Assert.False(OfflineWatchCapture.ValidCaptureTime(enrolled.AddMinutes(2), offset, enrolled, enrolled.AddMinutes(3), enrolled.AddMinutes(2)));
        Assert.False(OfflineWatchCapture.ValidCaptureTime(enrolled.ToOffset(TimeSpan.FromHours(3)), offset, enrolled, enrolled.AddMinutes(3), enrolled.AddMinutes(4)));
    }
}
