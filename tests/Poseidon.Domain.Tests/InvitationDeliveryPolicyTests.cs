using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// USER-ADMIN-03 / UA3-AC-01..UA3-AC-03: outbox state machine, retry backoff, recipient validation and 54.01
// account.invite_hours bounds.
[Trait("Requirement", "USER-ADMIN-03")]
public sealed class InvitationDeliveryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Wire_names_round_trip_and_unknown_states_are_refused()
    {
        foreach (var state in Enum.GetValues<InvitationDeliveryState>())
            Assert.Equal(state, InvitationDeliveryPolicy.Parse(InvitationDeliveryPolicy.Wire(state)));
        Assert.Equal(["QUEUED", "SENT", "FAILED", "NOT_CONFIGURED", "CANCELLED"], InvitationDeliveryPolicy.States);
        Assert.Null(InvitationDeliveryPolicy.Parse("DELIVERED"));
        Assert.Null(InvitationDeliveryPolicy.Parse("sent"));
        Assert.Null(InvitationDeliveryPolicy.Parse(null));
    }

    [Fact]
    public void Only_a_queued_delivery_moves_and_every_other_state_is_terminal()
    {
        Assert.False(InvitationDeliveryPolicy.Terminal(InvitationDeliveryState.Queued));
        foreach (var from in Enum.GetValues<InvitationDeliveryState>())
            foreach (var to in Enum.GetValues<InvitationDeliveryState>())
                Assert.Equal(from == InvitationDeliveryState.Queued, InvitationDeliveryPolicy.CanTransition(from, to));
        foreach (var state in new[] { InvitationDeliveryState.Sent, InvitationDeliveryState.Failed, InvitationDeliveryState.NotConfigured, InvitationDeliveryState.Cancelled })
            Assert.True(InvitationDeliveryPolicy.Terminal(state));
    }

    [Fact]
    public void Only_mail_server_acceptance_is_sent_and_an_unconfigured_transport_is_never_reported_delivered()
    {
        Assert.Equal(new InvitationDeliveryStep(InvitationDeliveryState.Sent, null, null), InvitationDeliveryPolicy.After(1, InvitationSendOutcome.Accepted, Now));
        Assert.Equal(new InvitationDeliveryStep(InvitationDeliveryState.NotConfigured, null, "not_configured"),
            InvitationDeliveryPolicy.After(1, InvitationSendOutcome.NotConfigured, Now));
        Assert.Equal(new InvitationDeliveryStep(InvitationDeliveryState.Failed, null, "smtp_rejected"),
            InvitationDeliveryPolicy.After(2, InvitationSendOutcome.PermanentFailure, Now));
    }

    [Fact]
    public void Transient_failures_retry_with_exponential_backoff_until_the_terminal_failed_state()
    {
        var expected = new[] { 1, 2, 4, 8 };
        for (var attempt = 1; attempt < InvitationDeliveryPolicy.MaxAttempts; attempt++)
        {
            var step = InvitationDeliveryPolicy.After(attempt, InvitationSendOutcome.TransientFailure, Now);
            Assert.Equal(InvitationDeliveryState.Queued, step.State);
            Assert.Equal(Now.AddMinutes(expected[attempt - 1]), step.NextAttemptAt);
            Assert.Equal("smtp_transient", step.ErrorCode);
        }
        var last = InvitationDeliveryPolicy.After(InvitationDeliveryPolicy.MaxAttempts, InvitationSendOutcome.TransientFailure, Now);
        Assert.Equal(new InvitationDeliveryStep(InvitationDeliveryState.Failed, null, "smtp_retries_exhausted"), last);
        Assert.Throws<ArgumentOutOfRangeException>(() => InvitationDeliveryPolicy.After(0, InvitationSendOutcome.Accepted, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => InvitationDeliveryPolicy.After(InvitationDeliveryPolicy.MaxAttempts + 1, InvitationSendOutcome.TransientFailure, Now));
        Assert.Equal(InvitationDeliveryPolicy.MaxBackoff, InvitationDeliveryPolicy.Backoff(30));
        Assert.Throws<ArgumentOutOfRangeException>(() => InvitationDeliveryPolicy.Backoff(0));
    }

    [Fact]
    public void Error_codes_are_a_closed_set_without_free_text()
    {
        Assert.All(InvitationDeliveryPolicy.ErrorCodes, c => Assert.Matches("^[a-z_]{1,32}$", c));
        Assert.Equal(InvitationDeliveryPolicy.ErrorCodes.Length, InvitationDeliveryPolicy.ErrorCodes.Distinct().Count());
    }

    [Theory]
    [InlineData("invitee@example.invalid", true)]
    [InlineData("first.last+tag@sub.example.invalid", true)]
    [InlineData("", false)]
    [InlineData("no-at-sign.example.invalid", false)]
    [InlineData("two@@example.invalid", false)]
    [InlineData("a@b", false)]
    [InlineData(".lead@example.invalid", false)]
    [InlineData("dou..ble@example.invalid", false)]
    [InlineData("Name <invitee@example.invalid>", false)]
    [InlineData("invitee@example.invalid\r\nBcc: other@example.invalid", false)]
    [InlineData("invitee@example.invalid,other@example.invalid", false)]
    [InlineData(" invitee@example.invalid", false)]
    [InlineData("invitee@[127.0.0.1]", false)]
    [InlineData("invitee@-bad.example.invalid", false)]
    [InlineData("invitee@example.123", false)]
    [InlineData("ünïcode@example.invalid", false)]
    public void Recipient_addresses_are_narrow_and_can_never_inject_a_header(string value, bool valid) =>
        Assert.Equal(valid, InvitationDeliveryPolicy.ValidEmail(value));

    [Fact]
    public void Recipient_length_is_bounded()
    {
        Assert.False(InvitationDeliveryPolicy.ValidEmail(new string('a', 65) + "@example.invalid"));
        Assert.False(InvitationDeliveryPolicy.ValidEmail("a@" + string.Join('.', Enumerable.Repeat(new string('b', 60), 5)) + ".invalid"));
        Assert.False(InvitationDeliveryPolicy.ValidEmail(null));
    }

    [Fact]
    public void Lifetime_is_bounded_to_54_01_account_invite_hours_and_defaults_to_24_hours()
    {
        Assert.Equal((1, 168, 24), (InvitationLifetimePolicy.MinHours, InvitationLifetimePolicy.MaxHours, InvitationLifetimePolicy.DefaultHours));
        Assert.Equal(TimeSpan.FromHours(24), InvitationLifetimePolicy.For(null));
        Assert.Equal(MembershipInvitationPolicy.Lifetime, InvitationLifetimePolicy.For(null));
        Assert.Equal(TimeSpan.FromHours(1), InvitationLifetimePolicy.For(1));
        Assert.Equal(TimeSpan.FromDays(7), InvitationLifetimePolicy.For(168));
        foreach (var bad in new[] { 0, -1, 169, int.MaxValue }) { Assert.False(InvitationLifetimePolicy.ValidHours(bad)); Assert.Throws<ArgumentOutOfRangeException>(() => InvitationLifetimePolicy.For(bad)); }
    }
}
