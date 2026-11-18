using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class NotificationPolicyTests
{
    private static NotificationRule Rule => new(Guid.NewGuid(), Guid.NewGuid(), "maintenance-due", "fictional-v1",
        "FICTIONAL routing, not an approved operational catalog", "Due", "Critical", false, 60, Guid.NewGuid());
    [Theory]
    [InlineData("Critical", false, true)]
    [InlineData("Warning", true, true)]
    [InlineData("Info", false, false)]
    public void Advisory_preference_never_silences_a_critical_or_explicitly_mandatory_route(string severity, bool mandatory, bool visible) =>
        Assert.Equal(visible, NotificationPolicy.Visible(severity, mandatory, mutedAdvisory: true));
    [Theory]
    [InlineData("Due", true, true)]
    [InlineData("ReviewRequired", true, false)]
    [InlineData("Unknown", true, false)]
    [InlineData("NotDue", true, false)]
    [InlineData("Due", false, false)]
    public void Trigger_requires_the_exact_declared_state_and_a_real_current_owner(string state, bool owner, bool triggered) =>
        Assert.Equal(triggered, NotificationPolicy.Trigger(Rule, state, owner));
    [Fact]
    public void Escalation_has_an_exact_inclusive_boundary_and_never_repeats_after_acknowledgement_or_escalation()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.False(NotificationPolicy.EscalationDue("Open", now.AddTicks(1), now));
        Assert.True(NotificationPolicy.EscalationDue("Open", now, now));
        Assert.False(NotificationPolicy.EscalationDue("Acknowledged", now.AddSeconds(-1), now));
        Assert.False(NotificationPolicy.EscalationDue("Escalated", now.AddSeconds(-1), now));
        Assert.False(NotificationPolicy.EscalationDue("Open", null, now));
    }
    [Fact]
    public void Missing_source_role_or_trigger_policy_is_not_a_default_operational_route()
    {
        Assert.False(NotificationPolicy.Valid(Rule with { SourceReference = "" }));
        Assert.False(NotificationPolicy.Valid(Rule with { EscalationRoleId = null }));
        Assert.False(NotificationPolicy.Valid(Rule with { EscalateAfterSeconds = 0 }));
        Assert.False(NotificationPolicy.Valid(Rule with { TriggerState = "Ready" }));
        Assert.False(NotificationPolicy.Valid(Rule with { Severity = "Unknown" }));
    }
}
