using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-037 / T-S4-005 notification job health and DEV-036 / T-S4-004 quiet-hours projection.
// Pure computations with fixed instants (fake clock); no host or database.
public sealed class NotificationHealthUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static NotificationSchedulerSettings Enabled() => new() {
        Enabled = true, IntervalSeconds = 30, LeaseSeconds = 120, BatchTimeoutSeconds = 30,
        Scopes = [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())]
    };

    [Fact]
    public void Missing_dispatch_row_is_not_observed_rather_than_healthy() =>
        Assert.Equal("NotObserved", SystemHealthEndpoints.DispatchState(null, Enabled(), Now));

    [Fact]
    public void Held_lease_is_running_and_an_expired_unreleased_lease_is_reported_as_an_abandoned_run()
    {
        var owner = Guid.NewGuid();
        Assert.Equal("Running", SystemHealthEndpoints.DispatchState(new() { LeaseOwner = owner, LeaseUntil = Now.AddSeconds(1) }, Enabled(), Now));
        Assert.Equal("LeaseExpired", SystemHealthEndpoints.DispatchState(new() { LeaseOwner = owner, LeaseUntil = Now }, Enabled(), Now));
        Assert.Equal("LeaseExpired", SystemHealthEndpoints.DispatchState(new() { LeaseOwner = owner, LeaseUntil = Now.AddMinutes(-5) }, new(), Now));
    }

    [Fact]
    public void Disabled_scheduler_is_explicit_and_never_reported_current()
    {
        var row = new NotificationDispatchState { LastCompletedAt = Now, NextRunAt = Now.AddSeconds(30) };
        Assert.Equal("SchedulerDisabled", SystemHealthEndpoints.DispatchState(row, new(), Now));
        var invalid = Enabled(); invalid.Scopes = [];
        Assert.Equal("SchedulerDisabled", SystemHealthEndpoints.DispatchState(row, invalid, Now));
    }

    [Fact]
    public void Next_run_beyond_the_interval_and_lease_grace_is_overdue()
    {
        // grace = max(60, 2*30 + 120) = 180 s
        Assert.Equal("Current", SystemHealthEndpoints.DispatchState(new() { NextRunAt = Now.AddSeconds(-180) }, Enabled(), Now));
        Assert.Equal("Overdue", SystemHealthEndpoints.DispatchState(new() { NextRunAt = Now.AddSeconds(-181) }, Enabled(), Now));
        Assert.Equal("Overdue", SystemHealthEndpoints.DispatchState(new() { NextRunAt = null }, Enabled(), Now));
    }

    [Fact]
    public void Quiet_projection_uses_the_stored_zone_and_server_clock()
    {
        Assert.Equal("NotConfigured", NotificationEndpoints.Quiet(null, Now).State);
        Assert.Equal("NotConfigured", NotificationEndpoints.Quiet(new() { MutedAdvisory = true }, Now).State);
        var istanbul = new NotificationPreference { QuietStart = new(14, 0), QuietEnd = new(16, 0), QuietZoneId = "Europe/Istanbul" };
        var (state, window) = NotificationEndpoints.Quiet(istanbul, Now); // 15:00 local
        Assert.Equal("Active", state);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 13, 0, 0, TimeSpan.Zero), window!.EndsAt);
        Assert.Equal("Inactive", NotificationEndpoints.Quiet(istanbul, Now.AddHours(2)).State);
    }

    [Fact]
    public void Unresolvable_stored_zone_fails_open_to_immediate_visibility()
    {
        var (state, window) = NotificationEndpoints.Quiet(new() { QuietStart = new(0, 0), QuietEnd = new(23, 59), QuietZoneId = "Mars/Olympus" }, Now);
        Assert.Equal("Unavailable", state); Assert.Null(window);
    }
}
