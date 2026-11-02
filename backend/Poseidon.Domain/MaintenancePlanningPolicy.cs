namespace Poseidon.Domain;

// DEV-053 / DEV-023: a current planning projection, never work completion or fitness.
// Context comes from scoped server reads, not from request declarations.
public sealed record MaintenancePlanningContext(bool JobAvailable, bool OwnerEligible, bool AssetRevisionCurrent);

public static class MaintenancePlanningPolicy
{
    public static MaintenanceDue Evaluate(MaintenanceInputs inputs, DateTimeOffset serverNow,
        MaintenancePlanningContext context, MaintenanceObservationAssessment? observation = null)
    {
        const string algorithm = "planning-current-v1";
        if (!context.AssetRevisionCurrent) return new("ReviewRequired", "EquipmentRevisionChanged", Algorithm: algorithm);
        if (inputs.ObservationId != null && observation?.Ready != true)
            return new("ReviewRequired", observation?.State ?? "ObservationAssessmentUnavailable", Algorithm: algorithm);
        var due = MaintenanceCalculation.Evaluate(observation?.Inputs ?? inputs, serverNow, observation)
            with { Algorithm = observation == null ? algorithm : algorithm + "/" + observation.PolicyVersion };
        // Preserve an already unresolved calculation and any exact computed boundaries. Context
        // cannot promote an unknown observation, missing baseline or unapproved policy to NotDue.
        if (due.State == "ReviewRequired") return due;
        if (!context.JobAvailable) return due with { State = "ReviewRequired", Reason = "MaintenanceJobUnavailable" };
        if (!context.OwnerEligible) return due with { State = "ReviewRequired", Reason = "MaintenanceOwnerUnavailable" };
        if (string.IsNullOrWhiteSpace(inputs.Criticality) || inputs.Criticality == "Unknown")
            return due with { State = "ReviewRequired", Reason = "IncompletePlanContext" };
        return due;
    }
}
