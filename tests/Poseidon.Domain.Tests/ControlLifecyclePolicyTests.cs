using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-028 / 18.01 Backlog row 29 "state"; transitions from 62.01 §6 paragraph:174-199.
[Trait("Requirement", "DEV-028")]
public sealed class ControlLifecyclePolicyTests
{
    [Theory]
    [InlineData("DRAFT", "SUBMIT_FOR_REVIEW")]
    [InlineData("IN_REVIEW", "RETURN_FOR_CORRECTION")]
    [InlineData("IN_REVIEW", "APPROVE")]
    [InlineData("APPROVED", "PUBLISH")]
    [InlineData("PUBLISHED", "RETIRE")]
    public void Source_transitions_succeed_only_with_every_gate_satisfied(string prior, string reason) =>
        Assert.Null(ControlLifecyclePolicy.Transition(prior, reason, true, true, true, true));

    [Theory]
    [InlineData("DRAFT", "APPROVE")]
    [InlineData("DRAFT", "PUBLISH")]
    [InlineData("DRAFT", "RETIRE")]
    [InlineData("IN_REVIEW", "PUBLISH")]
    [InlineData("APPROVED", "APPROVE")]
    [InlineData("APPROVED", "RETIRE")]
    [InlineData("PUBLISHED", "APPROVE")]
    [InlineData("PUBLISHED", "SUBMIT_FOR_REVIEW")]
    public void Skipped_or_backward_transitions_are_rejected(string prior, string reason) =>
        Assert.Equal("invalid_transition", ControlLifecyclePolicy.Transition(prior, reason, true, true, true, true));

    [Theory]
    [InlineData("SUBMIT_FOR_REVIEW")]
    [InlineData("APPROVE")]
    [InlineData("PUBLISH")]
    public void Retired_is_terminal_and_accepts_no_revision(string reason)
    {
        Assert.Equal("control_retired", ControlLifecyclePolicy.Transition("RETIRED", reason, true, true, true, true));
        Assert.False(ControlLifecyclePolicy.AcceptsRevision("RETIRED"));
    }

    [Fact]
    public void Review_requires_complete_record_reviewer_independence_and_qualified_sources()
    {
        Assert.Equal("control_record_incomplete", ControlLifecyclePolicy.Transition("DRAFT", "SUBMIT_FOR_REVIEW", false, true, true, true));
        Assert.Equal("control_reviewer_required", ControlLifecyclePolicy.Transition("IN_REVIEW", "APPROVE", true, false, true, true));
        Assert.Equal("separation_of_duties", ControlLifecyclePolicy.Transition("IN_REVIEW", "APPROVE", true, true, false, true));
        Assert.Equal("separation_of_duties", ControlLifecyclePolicy.Transition("IN_REVIEW", "RETURN_FOR_CORRECTION", true, true, false, true));
        Assert.Equal("qualified_source_required", ControlLifecyclePolicy.Transition("IN_REVIEW", "APPROVE", true, true, true, false));
        Assert.Equal("control_reviewer_required", ControlLifecyclePolicy.Transition("APPROVED", "PUBLISH", true, false, true, true));
        Assert.Equal("control_reviewer_required", ControlLifecyclePolicy.Transition("PUBLISHED", "RETIRE", true, false, true, true));
    }

    [Theory]
    [InlineData("ACTIVE", "APPROVE", "invalid_state")]
    [InlineData("DRAFT", "CONTENT_REVISED", "invalid_reason")]
    [InlineData("DRAFT", "Ready", "invalid_reason")]
    public void Unknown_states_and_system_reasons_are_not_commands(string prior, string reason, string error) =>
        Assert.Equal(error, ControlLifecyclePolicy.Transition(prior, reason, true, true, true, true));

    [Theory]
    [InlineData("DRAFT", false)]
    [InlineData("IN_REVIEW", true)]
    [InlineData("APPROVED", true)]
    [InlineData("PUBLISHED", true)]
    public void A_new_revision_makes_prior_review_and_approval_not_current(string prior, bool resets) =>
        Assert.Equal(resets, ControlLifecyclePolicy.RevisionResets(prior));

    // DEV-029: declared stale/expired/superseded stay distinct; no freshness policy registry
    // (54.01) exists, so absent declarations stay UNKNOWN rather than fresh/current.
    [Fact]
    public void Evidence_time_states_are_declared_overlays_and_supersession_wins()
    {
        var now = DateTimeOffset.Parse("2026-11-19T09:00:00Z");
        Assert.Equal("DECLARED_STALE", ControlEvidencePolicy.TimeState(new(ObservedAt: now.AddDays(-2), FreshUntil: now.AddSeconds(-1)), now, false));
        Assert.Equal("DECLARED_EXPIRED", ControlEvidencePolicy.TimeState(new(ValidUntil: now.AddSeconds(-1), FreshUntil: now.AddSeconds(-1)), now, false));
        Assert.Equal("SUPERSEDED", ControlEvidencePolicy.TimeState(new(ValidUntil: now.AddSeconds(-1)), now, true));
        Assert.Equal("DECLARED_NOT_YET_VALID", ControlEvidencePolicy.TimeState(new(ValidFrom: now.AddDays(1)), now, false));
        Assert.Equal("UNKNOWN", ControlEvidencePolicy.TimeState(new(), now, false));
        Assert.Equal("UNKNOWN", ControlEvidencePolicy.TimeState(new(FreshUntil: now.AddDays(1)), now, false));
    }

    [Theory]
    [InlineData(true, false, true, new[] { "REQUIREMENT_LINK_MISSING" })]
    [InlineData(true, true, false, new[] { "OWNER_MISSING" })]
    [InlineData(true, false, false, new[] { "REQUIREMENT_LINK_MISSING", "OWNER_MISSING" })]
    [InlineData(null, false, true, new[] { "CRITICALITY_UNKNOWN", "REQUIREMENT_LINK_MISSING" })]
    [InlineData(true, true, true, new string[0])]
    [InlineData(false, false, false, new string[0])]
    public void Orphan_report_keeps_unknown_criticality_visible(bool? critical, bool linked, bool owner, string[] expected) =>
        Assert.Equal(expected, ControlEvidencePolicy.OrphanReasons(critical, linked, owner));
}
