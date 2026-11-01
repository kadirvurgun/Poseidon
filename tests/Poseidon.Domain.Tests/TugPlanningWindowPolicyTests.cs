using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// Authored for the larger gate. These cases establish no real tug availability,
// operational conflict resolution, BP sufficiency or Master/dispatcher approval.
public sealed class TugPlanningWindowPolicyTests
{
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 11, 1, hour, minute, 0, TimeSpan.Zero);
    private static TugPlanningWindow Window(int start, int end) => new(At(start), At(end));

    [Fact]
    public void Missing_start_is_explicit_and_an_end_cannot_supply_it()
    {
        Assert.Equal(TugPlanningWindowState.MissingStart, TugPlanningWindowPolicy.Assess(new(null, null)));
        Assert.Equal(TugPlanningWindowState.MissingStart, TugPlanningWindowPolicy.Assess(new(null, At(12))));
    }

    [Fact]
    public void Conditional_end_is_not_fabricated_or_automatically_made_a_mandatory_field()
    {
        var draft = new TugPlanningWindow(At(10), null);
        Assert.Equal(TugPlanningWindowState.EndNotSupplied, TugPlanningWindowPolicy.Assess(draft));
        Assert.Null(draft.RequestedEnd);
        Assert.Equal(TugPlanningWindowRelation.Unknown, TugPlanningWindowPolicy.CompareDeclared(draft, Window(12, 13)));
        Assert.Equal(TugPlanningWindowRelation.Unknown, TugPlanningWindowPolicy.CompareDeclared(Window(12, 13), draft));
    }

    [Fact]
    public void Reversed_window_is_invalid_even_when_another_window_is_unknown()
    {
        Assert.Equal(TugPlanningWindowState.InvalidOrder, TugPlanningWindowPolicy.Assess(Window(12, 10)));
        Assert.Equal(TugPlanningWindowRelation.InvalidWindow,
            TugPlanningWindowPolicy.CompareDeclared(Window(12, 10), new(null, null)));
        Assert.Equal(TugPlanningWindowRelation.InvalidWindow,
            TugPlanningWindowPolicy.CompareDeclared(new(null, null), Window(12, 10)));
    }

    [Fact]
    public void Strictly_separate_entered_windows_compare_symmetrically()
    {
        Assert.Equal(TugPlanningWindowRelation.Separate, TugPlanningWindowPolicy.CompareDeclared(Window(10, 11), Window(12, 13)));
        Assert.Equal(TugPlanningWindowRelation.Separate, TugPlanningWindowPolicy.CompareDeclared(Window(12, 13), Window(10, 11)));
    }

    [Fact]
    public void Partial_overlap_and_containment_are_intersections()
    {
        Assert.Equal(TugPlanningWindowRelation.Intersecting, TugPlanningWindowPolicy.CompareDeclared(Window(10, 12), Window(11, 13)));
        Assert.Equal(TugPlanningWindowRelation.Intersecting, TugPlanningWindowPolicy.CompareDeclared(Window(10, 14), Window(11, 12)));
        Assert.Equal(TugPlanningWindowRelation.Intersecting, TugPlanningWindowPolicy.CompareDeclared(Window(11, 12), Window(10, 14)));
    }

    [Fact]
    public void Touching_boundaries_do_not_invent_zero_turnaround_or_dispatch_permission()
    {
        Assert.Equal(TugPlanningWindowRelation.BoundaryTouch, TugPlanningWindowPolicy.CompareDeclared(Window(10, 11), Window(11, 12)));
        Assert.Equal(TugPlanningWindowRelation.BoundaryTouch, TugPlanningWindowPolicy.CompareDeclared(Window(11, 12), Window(10, 11)));
    }

    [Fact]
    public void Equal_instants_preserve_the_declaration_without_inventing_a_minimum_duration()
    {
        Assert.Equal(TugPlanningWindowState.Defined, TugPlanningWindowPolicy.Assess(Window(11, 11)));
        Assert.Equal(TugPlanningWindowRelation.BoundaryTouch, TugPlanningWindowPolicy.CompareDeclared(Window(11, 11), Window(11, 11)));
        Assert.Equal(TugPlanningWindowRelation.Intersecting, TugPlanningWindowPolicy.CompareDeclared(Window(11, 11), Window(10, 12)));
    }

    [Fact]
    public void Offset_changes_compare_utc_instants_instead_of_local_clock_strings()
    {
        var first = new TugPlanningWindow(
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 11, 1, 13, 0, 0, TimeSpan.FromHours(3)));
        Assert.Equal(TugPlanningWindowRelation.BoundaryTouch,
            TugPlanningWindowPolicy.CompareDeclared(first, Window(10, 11)));
        var reversed = new TugPlanningWindow(
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 11, 1, 12, 30, 0, TimeSpan.FromHours(3)));
        Assert.Equal(TugPlanningWindowState.InvalidOrder, TugPlanningWindowPolicy.Assess(reversed));
    }

    [Fact]
    public void Historical_or_future_windows_are_not_rewritten_using_the_current_clock()
    {
        var past = new TugPlanningWindow(At(10).AddYears(-20), At(11).AddYears(-20));
        var future = new TugPlanningWindow(At(10).AddYears(20), At(11).AddYears(20));
        Assert.Equal(TugPlanningWindowState.Defined, TugPlanningWindowPolicy.Assess(past));
        Assert.Equal(TugPlanningWindowState.Defined, TugPlanningWindowPolicy.Assess(future));
        Assert.Equal(TugPlanningWindowRelation.Separate, TugPlanningWindowPolicy.CompareDeclared(past, future));
    }

    [Fact]
    public void Extreme_supported_instants_do_not_overflow_buffer_arithmetic()
    {
        var first = new TugPlanningWindow(DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        var last = new TugPlanningWindow(DateTimeOffset.MaxValue, DateTimeOffset.MaxValue);
        Assert.Equal(TugPlanningWindowRelation.Separate, TugPlanningWindowPolicy.CompareDeclared(first, last));
    }
}
