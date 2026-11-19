namespace Poseidon.Domain;

// DEV-047: a period/status declaration is not a qualification or operational decision.
public sealed record CrewRosterPresence(string State, string Reason);
public static class CrewRosterPresencePolicy
{
    public static CrewRosterPresence Evaluate(string status, string provenance, DateOnly? from, DateOnly? to,
        DateTimeOffset? observedAt, DateOnly date, DateTimeOffset now, bool configured, bool currentPolicy,
        IReadOnlyCollection<string> onboard, IReadOnlyCollection<string> planned, IReadOnlyCollection<string> provenances,
        bool requireObservation)
    {
        if (status == "Draft") return new("Draft", "incomplete_declaration");
        if (!configured) return new("Unknown", "assignment_policy_unavailable");
        if (!currentPolicy) return new("Stale", "assignment_policy_changed");
        if (!provenances.Contains(provenance, StringComparer.Ordinal)) return new("Unknown", "source_unrecognized");
        if (from == null || !CrewPeriod.Valid(from, to)) return new("Unknown", "period_missing_or_invalid");
        if (observedAt > now || requireObservation && observedAt == null) return new("Unknown", "source_observation_missing_or_future");
        if (to <= date) return new("OutsidePeriod", "period_ended");
        if (planned.Contains(status, StringComparer.Ordinal)) return new("PlannedDeclaration", "unverified_planned_declaration");
        if (onboard.Contains(status, StringComparer.Ordinal)) return from <= date
            ? new("OnboardDeclaration", "unverified_onboard_declaration") : new("OutsidePeriod", "period_not_started");
        return new("Unknown", "status_unrecognized");
    }

    // DEV-047 derived profile status (14.02 row 1615 is SYSTEM_DERIVED; BA definition pending).
    // A projection of the declared, unverified presence only: never fitness, eligibility or GO.
    public static readonly string[] ProfileStatuses = ["ONBOARD", "PLANNED", "ENDED", "DRAFT", "REVIEW_REQUIRED"];
    public static string ProfileStatus(CrewRosterPresence presence) => presence.State switch {
        "OnboardDeclaration" => "ONBOARD",
        "PlannedDeclaration" => "PLANNED",
        "OutsidePeriod" => presence.Reason == "period_not_started" ? "PLANNED" : "ENDED",
        "Draft" => "DRAFT",
        _ => "REVIEW_REQUIRED"
    };
}
