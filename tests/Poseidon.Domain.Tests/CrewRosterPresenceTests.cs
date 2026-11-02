using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class CrewRosterPresenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static CrewRosterPresence Evaluate(string status = "SyntheticOnboard", DateOnly? from = null, DateOnly? to = null,
        bool current = true, DateTimeOffset? observed = null) => CrewRosterPresencePolicy.Evaluate(status, "UserDeclaredUnverified",
        from ?? new(2026, 11, 1), to, observed ?? Now, new(2026, 11, 2), Now, true, current,
        ["SyntheticOnboard"], ["SyntheticPlanned"], ["UserDeclaredUnverified"], true);

    [Fact] public void Onboard_declaration_is_period_scoped_and_never_qualification()
    {
        Assert.Equal("OnboardDeclaration", Evaluate().State);
        Assert.Equal("OutsidePeriod", Evaluate(from: new(2026, 11, 3)).State);
        Assert.Equal("OutsidePeriod", Evaluate(to: new(2026, 11, 2)).State);
        Assert.Equal("OnboardDeclaration", Evaluate(from: new(2026, 11, 2), to: new(2026, 11, 3)).State);
    }
    [Fact] public void Planned_and_draft_remain_distinct_from_onboard()
    {
        Assert.Equal("PlannedDeclaration", Evaluate("SyntheticPlanned", from: new(2026, 11, 3)).State);
        Assert.Equal("Draft", Evaluate("Draft", current: false).State);
        Assert.Equal("Unknown", Evaluate("unrecognized").State);
    }
    [Fact] public void Changed_policy_and_future_observation_withdraw_currentness()
    {
        Assert.Equal("Stale", Evaluate(current: false).State);
        Assert.Equal("Unknown", Evaluate(observed: Now.AddSeconds(1)).State);
        var missing = CrewRosterPresencePolicy.Evaluate("SyntheticOnboard", "UserDeclaredUnverified", null, null, null,
            new(2026, 11, 2), Now, true, true, ["SyntheticOnboard"], [], ["UserDeclaredUnverified"], true);
        Assert.Equal("period_missing_or_invalid", missing.Reason);
    }
}
