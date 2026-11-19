namespace Poseidon.Domain;

// DEV-026 / T-S3-001 (18.01 Backlog row 27): draft/adopted/future/in-force/superseded
// distinction for a requirement. The lifecycle is a read projection derived from the
// recorded per-rule source qualification heads; it is never stored a second time and
// it is recorded software qualification, not legal truth or a human sign-off.
public static class RequirementLifecyclePolicy
{
    public const string Draft = "DRAFT", Adopted = "ADOPTED", Future = "FUTURE", InForce = "IN_FORCE", Superseded = "SUPERSEDED", Unavailable = "UNAVAILABLE";
    public const string BasisNone = "NO_QUALIFIED_RULE", BasisRecorded = "RECORDED_QUALIFICATION", BasisUnrecognized = "UNRECOGNIZED_QUALIFICATION_STATE";
    // MIXED: the bound rules disagree. The derived value below is a software precedence choice
    // without a source (18.01 row 27 names the states, not a multi-rule aggregation rule); it is
    // published with this review basis and recorded as an open expert decision (S3-WPA report).
    // LIMIT: more bound rules than one projection carries; nothing is derived from a partial set.
    public const string BasisMixed = "MIXED_QUALIFICATION_STATES", BasisLimitExceeded = "QUALIFICATION_LIMIT_EXCEEDED";
    public static readonly string[] Bases = [BasisNone, BasisRecorded, BasisUnrecognized, BasisMixed, BasisLimitExceeded];
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
    // superseded. No qualified rule means the revision is still a draft. When the rules map
    // to more than one lifecycle term the value carries the MIXED review basis, never a
    // plain recorded basis.
    public static (string Lifecycle, string Basis) Derive(IEnumerable<string?> currentStates)
    {
        var mapped = currentStates.Select(Map).ToArray();
        if (mapped.Length == 0) return (Draft, BasisNone);
        if (mapped.Contains(Unavailable)) return (Unavailable, BasisUnrecognized);
        var basis = mapped.Distinct(StringComparer.Ordinal).Count() > 1 ? BasisMixed : BasisRecorded;
        foreach (var candidate in new[] { InForce, Future, Adopted, Draft })
            if (mapped.Contains(candidate)) return (candidate, basis);
        return (Superseded, basis);
    }

    // The projection could not carry every bound rule: never derive from a partial set.
    public static (string Lifecycle, string Basis) LimitExceeded() => (Unavailable, BasisLimitExceeded);
}
