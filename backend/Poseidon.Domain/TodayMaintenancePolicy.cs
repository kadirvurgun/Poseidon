namespace Poseidon.Domain;

public sealed record TodayMaintenanceAdvisory(string Reason, string Priority);

// TM-01: local draft-planning exception categories, not safety priority or readiness.
public static class TodayMaintenancePolicy
{
    public static TodayMaintenanceAdvisory? Observe(MaintenanceDue due) => due.State switch {
        "NotDue" => null,
        "Due" => new("MaintenanceDue", "Attention"),
        _ => new("MaintenanceReviewRequired", "Review")
    };
}
