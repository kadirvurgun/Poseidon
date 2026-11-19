using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S3-WPA / DEV-026 / T-S3-001 (18.01 Backlog row 27): draft/adopted/future/in-force/superseded
// is derived from recorded qualification heads; unknown stored values are never guessed.
public sealed class RequirementLifecyclePolicyTests
{
    [Theory]
    [InlineData("DRAFT", "DRAFT")]
    [InlineData("IN_REVIEW", "DRAFT")]
    [InlineData("ADOPTED", "ADOPTED")]
    [InlineData("AWAITING", "ADOPTED")]
    [InlineData("FUTURE_EFFECTIVE", "FUTURE")]
    [InlineData("IN_FORCE", "IN_FORCE")]
    [InlineData("SUPERSEDED", "SUPERSEDED")]
    [InlineData("in_force", "UNAVAILABLE")]
    [InlineData("UNKNOWN", "UNAVAILABLE")]
    [InlineData("", "UNAVAILABLE")]
    [InlineData(null, "UNAVAILABLE")]
    public void Each_qualification_state_maps_to_one_lifecycle_term(string? state, string expected) =>
        Assert.Equal(expected, RequirementLifecyclePolicy.Map(state));

    [Fact]
    public void No_current_qualification_is_a_draft_not_an_in_force_requirement() =>
        Assert.Equal(("DRAFT", "NO_QUALIFIED_RULE"), RequirementLifecyclePolicy.Derive([]));

    [Theory]
    [InlineData("IN_FORCE", new[] { "SUPERSEDED", "IN_FORCE", "DRAFT" })]
    [InlineData("FUTURE", new[] { "FUTURE_EFFECTIVE", "ADOPTED", "SUPERSEDED" })]
    [InlineData("ADOPTED", new[] { "AWAITING", "IN_REVIEW" })]
    [InlineData("DRAFT", new[] { "IN_REVIEW", "SUPERSEDED" })]
    [InlineData("SUPERSEDED", new[] { "SUPERSEDED", "SUPERSEDED" })]
    public void Strongest_current_state_wins_and_superseded_needs_every_rule_superseded(string expected, string[] states) =>
        Assert.Equal((expected, "RECORDED_QUALIFICATION"), RequirementLifecyclePolicy.Derive(states));

    [Fact]
    public void An_unrecognized_state_poisons_the_aggregate_instead_of_defaulting() =>
        Assert.Equal(("UNAVAILABLE", "UNRECOGNIZED_QUALIFICATION_STATE"), RequirementLifecyclePolicy.Derive(["IN_FORCE", "LEGACY_STATE"]));

    [Fact]
    public void Published_values_are_the_complete_closed_vocabulary() =>
        Assert.Equal(new[] { "DRAFT", "ADOPTED", "FUTURE", "IN_FORCE", "SUPERSEDED", "UNAVAILABLE" }, RequirementLifecyclePolicy.Values);
}
