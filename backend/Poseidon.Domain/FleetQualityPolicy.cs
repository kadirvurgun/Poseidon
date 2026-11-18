namespace Poseidon.Domain;

// 59.01 paragraphs 1856–1884. Internal producer inputs, never request DTOs.
// The future registered owner adapter must select the complete catalog/native schema
// set and observations in one authorized source cut before calling this policy.
public enum FleetQualitySourceKind { ROOT, IDENTITY, PROFILE, TUG_PROFILE }
public enum FleetQualityObservationState { KNOWN_VALUE, KNOWN_NOT_APPLICABLE, MISSING, DISPUTED, UNAVAILABLE }
public enum FleetQualityResult { COMPLETE, PARTIAL, UNKNOWN, DISPUTED }

public sealed record FleetQualityRequirementV1(FleetQualitySourceKind SourceKind, string Path, bool NullableAllowed)
{
    public bool Required => true;
}

public sealed record FleetQualityObservationV1(FleetQualityRequirementV1 Requirement, FleetQualityObservationState State,
    Guid? SourceRevisionId, long? SourceVersion);

public static class FleetQualityPolicy
{
    public const int MaximumRequirements = 100;
    private static readonly string[] RootPaths = ["/recordCode", "/displayName", "/operatingMode"];

    // Only an accepted catalog's requiredInputPaths, bound to native writer schemas,
    // may be supplied here. Optional fields/steward/operator are not extra roots.
    public static FleetQualityRequirementV1[] Requirements(IEnumerable<FleetQualityRequirementV1> catalogRequirements) =>
        Unique(RootPaths.Select(path => new FleetQualityRequirementV1(FleetQualitySourceKind.ROOT, path, false))
            .Concat(catalogRequirements));

    public static FleetQualityResult Evaluate(IReadOnlyCollection<FleetQualityRequirementV1> requirements,
        IReadOnlyCollection<FleetQualityObservationV1> observations, Guid? catalogRevisionId, bool enumerationComplete)
    {
        var required = Unique(requirements);
        var keys = required.Select(Key).ToHashSet();
        // A disagreement in a required source has priority over missing catalog or
        // unavailable sources. An unrelated optional observation cannot dispute it.
        if (observations.Any(x => keys.Contains(Key(x.Requirement)) && x.State == FleetQualityObservationState.DISPUTED))
            return FleetQualityResult.DISPUTED;
        if (!enumerationComplete || catalogRevisionId is null || catalogRevisionId == Guid.Empty || required.Length == 0 ||
            required.Any(x => !Valid(x)) || RootPaths.Any(path => !required.Contains(new(FleetQualitySourceKind.ROOT, path, false))) ||
            required.Any(x => x.SourceKind == FleetQualitySourceKind.ROOT && !RootPaths.Contains(x.Path)))
            return FleetQualityResult.UNKNOWN;

        var missing = false;
        foreach (var requirement in required)
        {
            var matches = observations.Where(x => Key(x.Requirement) == Key(requirement)).ToArray();
            // Omission is unknown source coverage, not proof that a field is missing.
            if (matches.Length != 1 || matches[0].Requirement != requirement) return FleetQualityResult.UNKNOWN;
            var observation = matches[0];
            if (!Enum.IsDefined(observation.State) || observation.State == FleetQualityObservationState.UNAVAILABLE ||
                !ValidLineage(observation)) return FleetQualityResult.UNKNOWN;
            if (observation.State == FleetQualityObservationState.KNOWN_NOT_APPLICABLE && !requirement.NullableAllowed)
                return FleetQualityResult.UNKNOWN;
            missing |= observation.State == FleetQualityObservationState.MISSING;
        }
        return missing ? FleetQualityResult.PARTIAL : FleetQualityResult.COMPLETE;
    }

    private static (FleetQualitySourceKind, string) Key(FleetQualityRequirementV1 x) => (x.SourceKind, x.Path);

    private static FleetQualityRequirementV1[] Unique(IEnumerable<FleetQualityRequirementV1> requirements)
    {
        // Same path in different native sources is intentionally distinct. Identical
        // repeated declarations are one requirement; conflicting bindings stay visible.
        var result = requirements.Distinct().ToArray();
        if (result.Select(Key).Distinct().Count() > MaximumRequirements)
            throw new ArgumentException("QUALITY_REQUIREMENT_SET_TOO_LARGE", nameof(requirements));
        return result;
    }

    private static bool Valid(FleetQualityRequirementV1 x) => Enum.IsDefined(x.SourceKind) &&
        !string.IsNullOrEmpty(x.Path) && x.Path.Length <= 200 && x.Path.StartsWith('/') &&
        !x.Path.Any(char.IsControl);

    private static bool ValidLineage(FleetQualityObservationV1 x)
    {
        if (x.SourceVersion is <= 0 || x.SourceRevisionId == Guid.Empty) return false;
        var known = x.State is FleetQualityObservationState.KNOWN_VALUE or FleetQualityObservationState.KNOWN_NOT_APPLICABLE;
        if (x.Requirement.SourceKind == FleetQualitySourceKind.ROOT)
            return x.SourceRevisionId is null && x.SourceVersion is not null;
        // A missing revision has both null. A missing field in a retained revision
        // can still carry that exact revision/version; partial pins are never valid.
        return known ? x.SourceRevisionId is not null && x.SourceVersion is not null :
            (x.SourceRevisionId is null) == (x.SourceVersion is null);
    }
}
