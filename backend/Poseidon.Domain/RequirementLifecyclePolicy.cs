namespace Poseidon.Domain;

// DEV-026 / T-S3-001 (18.01 Backlog row 27): draft/adopted/future/in-force/superseded
// distinction for a requirement. The lifecycle is a read projection derived from the
// recorded per-rule source qualification heads; it is never stored a second time and
// it is recorded software qualification, not legal truth or a human sign-off.
public static class RequirementLifecyclePolicy
{
    public const string Draft = "DRAFT", Adopted = "ADOPTED", Future = "FUTURE", InForce = "IN_FORCE", Superseded = "SUPERSEDED", Unavailable = "UNAVAILABLE";
    public const string BasisNone = "NO_QUALIFIED_RULE", BasisRecorded = "RECORDED_QUALIFICATION", BasisUnrecognized = "UNRECOGNIZED_QUALIFICATION_STATE";
    public static readonly string[] Values = [Draft, Adopted, Future, InForce, Superseded, Unavailable];

    // One qualification head state -> requirement lifecycle term. An unrecognized
    // stored value is UNAVAILABLE, never silently a draft or in force.
    public static string Map(string? state) => state switch {
        "DRAFT" or "IN_REVIEW" => Draft,
        "ADOPTED" or "AWAITING" => Adopted,
        "FUTURE_EFFECTIVE" => Future,
        "IN_FORCE" => InForce,
        "SUPERSEDED" => Superseded,
        _ => Unavailable
    };

    // Aggregates the current qualification heads bound to the requirement's current
    // revision. Any unrecognized state poisons the aggregate (UNAVAILABLE). Otherwise the
    // strongest currently-effective state wins; SUPERSEDED only when every bound rule is
    // superseded. No qualified rule means the revision is still a draft.
    public static (string Lifecycle, string Basis) Derive(IEnumerable<string?> currentStates)
    {
        var mapped = currentStates.Select(Map).ToArray();
        if (mapped.Length == 0) return (Draft, BasisNone);
        if (mapped.Contains(Unavailable)) return (Unavailable, BasisUnrecognized);
        foreach (var candidate in new[] { InForce, Future, Adopted, Draft })
            if (mapped.Contains(candidate)) return (candidate, BasisRecorded);
        return (Superseded, BasisRecorded);
    }
}
