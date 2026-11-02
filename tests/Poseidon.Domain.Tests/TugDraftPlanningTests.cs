using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class TugDraftPlanningTests
{
    [Fact]
    public void Incomplete_window_is_allowed_and_is_not_replaced_by_requested_or_current_time() {
        var original = new TugPlanInputs(null, null, []);
        var changed = TugDraftPlanning.Apply(original, new("Window", PlannedEnd: DateTimeOffset.UnixEpoch));
        Assert.Null(changed.PlannedStart); Assert.Equal(DateTimeOffset.UnixEpoch, changed.PlannedEnd); Assert.Null(original.PlannedEnd);
    }
    [Fact]
    public void Explicit_assignment_reassignment_and_removal_preserve_previous_revision_inputs() {
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var empty = new TugPlanInputs(null, null, []);
        var assigned = TugDraftPlanning.Apply(empty, new("Assign", TugId: first));
        var reassigned = TugDraftPlanning.Apply(assigned, new("Reassign", TugId: first, ReplacementTugId: second));
        var removed = TugDraftPlanning.Apply(reassigned, new("Remove", TugId: second));
        Assert.Empty(empty.AssignedTugs); Assert.Equal(new[] { first }, assigned.AssignedTugs);
        Assert.Equal(new[] { second }, reassigned.AssignedTugs); Assert.Empty(removed.AssignedTugs);
    }
    [Fact]
    public void Duplicate_or_missing_references_do_not_silently_merge() {
        var tug = Guid.NewGuid(); var plan = new TugPlanInputs(null, null, [tug]);
        Assert.Throws<ArgumentException>(() => TugDraftPlanning.Apply(plan, new("Assign", TugId: tug)));
        Assert.Throws<ArgumentException>(() => TugDraftPlanning.Apply(plan, new("Remove", TugId: Guid.NewGuid())));
        Assert.Throws<ArgumentException>(() => TugDraftPlanning.Apply(plan, new("Reassign", TugId: tug, ReplacementTugId: tug)));
    }
    [Fact]
    public void Reassignment_to_an_already_assigned_tug_is_a_conflict_without_mutation() {
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var original = new TugPlanInputs(null, null, [first, second]);
        Assert.Throws<ArgumentException>(() => TugDraftPlanning.Apply(original, new("Reassign", TugId: first, ReplacementTugId: second)));
        Assert.Equal(new[] { first, second }, original.AssignedTugs);
    }
    [Theory]
    [InlineData("Ready")]
    [InlineData("Approve")]
    [InlineData("Dispatch")]
    [InlineData("Submit")]
    [InlineData("Permit")]
    public void Operational_transitions_are_not_preparation_commands(string operation) =>
        Assert.Throws<ArgumentException>(() => TugDraftPlanning.Apply(new(null, null, []), new(operation)));
    [Fact]
    public void Window_rejects_reverse_order_and_reference_smuggling_but_keeps_boundary_touch_distinct() {
        var t = DateTimeOffset.UnixEpoch; var plan = new TugPlanInputs(t, t.AddHours(1), []);
        Assert.Throws<ArgumentException>(() => TugDraftPlanning.Apply(plan, new("Window", t, t.AddTicks(-1))));
        Assert.Throws<ArgumentException>(() => TugDraftPlanning.Apply(plan, new("Window", t, t, Guid.NewGuid())));
        var next = TugDraftPlanning.Apply(plan, new("Window", t.AddHours(1), t.AddHours(2)));
        Assert.Equal(TugPlanningWindowRelation.BoundaryTouch, TugPlanningWindowPolicy.CompareDeclared(new(plan.PlannedStart, plan.PlannedEnd), new(next.PlannedStart, next.PlannedEnd)));
    }
}
