using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-036 / T-S4-004 with 58.01 PL-035/PL-036: quiet hours defer only optional in-app
// visibility; mandatory/Critical routes are never silenced. Fixed instants act as the fake clock.
public sealed class NotificationQuietHoursTests
{
    private static QuietHours Hours(string start, string end, string zone)
    {
        Assert.Null(NotificationQuietHours.Validate(start, end, zone, out var hours));
        return hours!;
    }
    private static TimeZoneInfo Zone(string id) => NotificationQuietHours.Zone(id)!;

    [Fact]
    public void Empty_setting_means_no_quiet_hours()
    {
        Assert.Null(NotificationQuietHours.Validate(null, null, null, out var hours));
        Assert.Null(hours);
    }

    [Theory]
    [InlineData("22:00", null, "Europe/Istanbul", "quiet_hours_incomplete")]
    [InlineData(null, "07:00", "Europe/Istanbul", "quiet_hours_incomplete")]
    [InlineData("22:00", "07:00", null, "quiet_hours_incomplete")]
    [InlineData("22:00", "22:00", "Europe/Istanbul", "quiet_hours_invalid")]
    [InlineData("7:00", "22:00", "Europe/Istanbul", "quiet_hours_invalid")]
    [InlineData("24:00", "07:00", "Europe/Istanbul", "quiet_hours_invalid")]
    [InlineData("22:00:00", "07:00", "Europe/Istanbul", "quiet_hours_invalid")]
    [InlineData("22:00", "07:00", "Mars/Olympus", "quiet_hours_zone_unknown")]
    [InlineData("22:00", "07:00", "Turkey Standard Time", "quiet_hours_zone_unknown")]
    [InlineData("22:00", "07:00", " Europe/Istanbul", "quiet_hours_zone_unknown")]
    [InlineData("22:00", "07:00", "+03:00", "quiet_hours_zone_unknown")]
    public void Partial_equal_malformed_or_non_iana_settings_are_rejected(string? start, string? end, string? zone, string code) =>
        Assert.Equal(code, NotificationQuietHours.Validate(start, end, zone, out _));

    [Fact]
    public void Overnight_window_is_resolved_in_the_recipient_zone_not_server_utc()
    {
        var hours = Hours("22:00", "07:00", "Europe/Istanbul"); // UTC+3, no DST
        var zone = Zone("Europe/Istanbul");
        // 2026-11-19 20:30Z = 23:30 local: inside the window that began 19:00Z.
        var window = NotificationQuietHours.Active(hours, zone, new DateTimeOffset(2026, 11, 19, 20, 30, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 19, 0, 0, TimeSpan.Zero), window!.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 4, 0, 0, TimeSpan.Zero), window.EndsAt);
        // 03:59Z next day is still inside; 04:00Z is the first visible instant (half-open interval).
        Assert.NotNull(NotificationQuietHours.Active(hours, zone, new DateTimeOffset(2026, 11, 20, 3, 59, 59, TimeSpan.Zero)));
        Assert.Null(NotificationQuietHours.Active(hours, zone, new DateTimeOffset(2026, 11, 20, 4, 0, 0, TimeSpan.Zero)));
        // 18:59Z (21:59 local) is before the window.
        Assert.Null(NotificationQuietHours.Active(hours, zone, new DateTimeOffset(2026, 11, 19, 18, 59, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Daytime_window_does_not_wrap_midnight()
    {
        var hours = Hours("12:00", "13:00", "UTC"); var zone = Zone("UTC");
        Assert.NotNull(NotificationQuietHours.Active(hours, zone, new DateTimeOffset(2026, 11, 19, 12, 30, 0, TimeSpan.Zero)));
        Assert.Null(NotificationQuietHours.Active(hours, zone, new DateTimeOffset(2026, 11, 19, 23, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Dst_gap_start_uses_the_first_valid_local_instant()
    {
        // Europe/Berlin 2026-03-29 02:00 CET -> 03:00 CEST. 02:30 does not exist locally.
        var zone = Zone("Europe/Berlin");
        var resolved = NotificationQuietHours.Resolve(new DateTime(2026, 3, 29, 2, 30, 0), zone);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero), resolved); // 03:00 CEST
    }

    [Fact]
    public void Dst_overlap_uses_the_later_occurrence()
    {
        // Europe/Berlin 2026-10-25 03:00 CEST -> 02:00 CET. 02:30 occurs twice; later is CET (+1).
        var zone = Zone("Europe/Berlin");
        var resolved = NotificationQuietHours.Resolve(new DateTime(2026, 10, 25, 2, 30, 0), zone);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), resolved);
    }

    [Theory]
    [InlineData("Critical", false, false)]
    [InlineData("Warning", true, false)]
    [InlineData("Info", false, true)]
    [InlineData("Warning", false, true)]
    public void Mandatory_and_critical_notices_bypass_quiet_hours_while_optional_ones_are_deferred(string severity, bool mandatory, bool deferred)
    {
        var window = new QuietWindow(new DateTimeOffset(2026, 11, 19, 19, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 20, 4, 0, 0, TimeSpan.Zero));
        Assert.Equal(deferred, NotificationQuietHours.Deferred(severity, mandatory, window.StartsAt.AddMinutes(5), window));
    }

    [Fact]
    public void Optional_notice_raised_before_the_window_stays_visible_and_no_window_defers_nothing()
    {
        var window = new QuietWindow(new DateTimeOffset(2026, 11, 19, 19, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 20, 4, 0, 0, TimeSpan.Zero));
        Assert.False(NotificationQuietHours.Deferred("Info", false, window.StartsAt.AddTicks(-1), window));
        Assert.True(NotificationQuietHours.Deferred("Info", false, window.StartsAt, window));
        Assert.False(NotificationQuietHours.Deferred("Info", false, window.StartsAt, null));
    }
}
