namespace Poseidon.Domain;

// DEV-023 / 61.03 §33 + ScreenEnvelope.viewState: a deterministic composition state.
// It describes which owning sources could be read, never readiness or safety clearance.
// Missing = readable owner with no record; Unavailable = not readable in this context.
public static class TodayViewStatePolicy
{
    public const string Ready = "Ready";
    public const string Empty = "Empty";
    public const string Partial = "Partial";
    public const string Unavailable = "Unavailable";

    public static string Classify(IReadOnlyCollection<string> coverage, int returnedItems)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentOutOfRangeException.ThrowIfNegative(returnedItems);
        if (coverage.Count == 0 || coverage.Any(s => s is not ("Available" or "Missing" or "Unavailable")))
            return Unavailable; // Unknown status is never promoted to a readable source.
        var unreadable = coverage.Count(s => s == "Unavailable");
        if (unreadable == coverage.Count) return Unavailable;
        if (unreadable > 0) return Partial;
        return returnedItems == 0 ? Empty : Ready;
    }
}
