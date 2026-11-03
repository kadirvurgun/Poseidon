namespace Poseidon.Domain;

public sealed record TodayTugContext(bool HasPlan, bool RequestChanged, bool RequestIncomplete,
    bool ReadinessBlocked, string Conflict);
public sealed record TodayTugAdvisory(string Reason, string Priority);

// TT-01: work requiring planning review. This never authorizes operational release.
public static class TodayTugPolicy
{
    public static TodayTugAdvisory Observe(TodayTugContext context) => context switch {
        { Conflict: "HumanReviewRequired" } => new("TugConflictReviewRequired", "Attention"),
        { ReadinessBlocked: true } => new("TugReadinessBlocked", "Attention"),
        { RequestChanged: true } => new("TugRequestChanged", "Review"),
        { HasPlan: false } => new("TugPlanMissing", "Review"),
        { Conflict: "BoundaryReviewRequired" } => new("TugBoundaryReviewRequired", "Review"),
        { RequestIncomplete: true } => new("TugRequestIncomplete", "Review"),
        _ => new("TugPlanningReviewRequired", "Review")
    };
}
