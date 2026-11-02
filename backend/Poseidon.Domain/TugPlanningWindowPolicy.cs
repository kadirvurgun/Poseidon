namespace Poseidon.Domain;

public readonly record struct TugPlanningWindow(DateTimeOffset? RequestedStart, DateTimeOffset? RequestedEnd);

public enum TugPlanningWindowState { MissingStart, EndNotSupplied, InvalidOrder, Defined }
public enum TugPlanningWindowRelation { Unknown, InvalidWindow, Separate, BoundaryTouch, Intersecting }

// DEV-081/T-TUG-002 and DEV-082/T-TUG-003. Source14.02 rows1920/1921:
// requested_start is required, requested_end is conditional. No inferred end,
// current-clock substitution, turnaround buffer, assignment or dispatch authority.
public static class TugPlanningWindowPolicy
{
    public static TugPlanningWindowState Assess(TugPlanningWindow window)
    {
        if (window.RequestedStart is null)
            return TugPlanningWindowState.MissingStart;
        if (window.RequestedEnd is null)
            return TugPlanningWindowState.EndNotSupplied;
        return window.RequestedEnd < window.RequestedStart
            ? TugPlanningWindowState.InvalidOrder : TugPlanningWindowState.Defined;
    }

    // Comparison concerns only the entered instants, never operational feasibility.
    // Touching boundaries are distinct because the source has not supplied an approved
    // turnaround/buffer rule. Separate windows do not mean an assigned tug is available.
    public static TugPlanningWindowRelation CompareDeclared(TugPlanningWindow first, TugPlanningWindow second)
    {
        var firstState = Assess(first);
        var secondState = Assess(second);
        if (firstState == TugPlanningWindowState.InvalidOrder || secondState == TugPlanningWindowState.InvalidOrder)
            return TugPlanningWindowRelation.InvalidWindow;
        if (firstState != TugPlanningWindowState.Defined || secondState != TugPlanningWindowState.Defined)
            return TugPlanningWindowRelation.Unknown;

        // DateTimeOffset comparison uses instants, including differing UTC offsets.
        if (first.RequestedEnd < second.RequestedStart || second.RequestedEnd < first.RequestedStart)
            return TugPlanningWindowRelation.Separate;
        if (first.RequestedEnd == second.RequestedStart || second.RequestedEnd == first.RequestedStart)
            return TugPlanningWindowRelation.BoundaryTouch;
        return TugPlanningWindowRelation.Intersecting;
    }
}
