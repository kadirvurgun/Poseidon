namespace Poseidon.Domain;

// PLAN-1511-5501-SLICE-01 (LEAD-5601-D02, C03/GAP06). 55.01 is the single owner
// of metric identity, version, unit, source projection, small-group policy and
// scalar evaluation semantics (55.01 paragraph:155-157, 543-560, 834, 1397).
// Consumers (61.03 Today/Why, DEV-023/024) read these scalars; they never
// invent a second formula, aggregation or snapshot (61.03 paragraph:16, 178, 1261).
// This slice has no report designer, dashboard, schedule or export.
public sealed record MetricDefinition(
    string Id,
    int Version,
    string OwnerModule,
    string Label,
    string UnitCode,
    string SourceProjectionId,
    string Aggregation,
    int MinCohort,
    string FreshnessRule,
    string[] RequiredPermissions,
    string[] AllowedActions,
    string[] Scopes,
    string Basis,
    string Source);

public static class MetricStates
{
    public const string Value = "VALUE";
    public const string Suppressed = "SUPPRESSED";
    public const string Unknown = "UNKNOWN";
    public const string Unavailable = "UNAVAILABLE";
}

public static class MetricQualities
{
    public const string Complete = "COMPLETE";
    public const string Partial = "PARTIAL";
    public const string Unknown = "UNKNOWN";
}

public static class MetricRegistry
{
    // Code-registered v1 catalog. contracts/metrics.v1.json is the published
    // consumer copy; MetricRegistryContractTests keeps both identical. A formula,
    // unit, source or policy change is a new Version, never an in-place edit.
    public const string OnboardingVesselsReviewRequired = "onboarding.vessels.review_required";
    public const string OnboardingRequiredFieldsUnknown = "onboarding.required_fields.unknown";
    public const string CrewDeclaredPersons = "crew.assignments.declared_persons";
    public const string ScopeTenantVisible = "TENANT_VISIBLE";
    public const string ScopeVessel = "VESSEL";
    // Upper bound of owning-source rows read for one scalar. Above it the scalar
    // is UNAVAILABLE (query_limit_exceeded), never a truncated partial total.
    public const int MaxSourceVessels = 1000;

    public static readonly MetricDefinition[] All =
    [
        new(OnboardingVesselsReviewRequired, 1,
            "59.01/18.01 vessel onboarding (ComplianceOnboardingPolicy)",
            "Vessels whose onboarding declaration requires review",
            "vessel", "projection.compliance-onboarding.v1", "COUNT_DISTINCT", 1,
            "LIVE_READ;55.01 rpt.freshness.online_min",
            ["directory.read"], ["view"], [ScopeTenantVisible, ScopeVessel], "DECLARED_UNVERIFIED",
            "55.01/paragraph:156;55.01/paragraph:1397;18.01/Backlog/row:40;59.01/paragraph:55"),
        new(OnboardingRequiredFieldsUnknown, 1,
            "59.01/18.01 vessel onboarding (ComplianceOnboardingPolicy)",
            "Required onboarding fields declared UNKNOWN",
            "field", "projection.compliance-onboarding.v1", "SUM", 1,
            "LIVE_READ;55.01 rpt.freshness.online_min",
            ["directory.read"], ["view"], [ScopeTenantVisible, ScopeVessel], "DECLARED_UNVERIFIED",
            "55.01/paragraph:263;55.01/paragraph:557;18.01/Backlog/row:40"),
        new(CrewDeclaredPersons, 1,
            "53.01/58.03 crew assignment declarations (CrewAssignments)",
            "Persons with a declared crew assignment",
            "person", "projection.crew-assignment-declarations.v1", "COUNT_DISTINCT", 5,
            "LIVE_READ;55.01 rpt.freshness.online_min",
            ["directory.read", "crew.read"], ["view"], [ScopeTenantVisible, ScopeVessel], "DECLARED_UNVERIFIED",
            "55.01/paragraph:645-647;55.01/paragraph:661;64.01/paragraph:142"),
    ];

    public static MetricDefinition? Find(string? id) => id == null ? null : All.SingleOrDefault(d => d.Id == id);

    // Audience is a server-provisioned capability check only, never a role name.
    public static bool Permits(MetricDefinition definition, IReadOnlyCollection<string> permissions, string action = "view") =>
        definition.AllowedActions.Contains(action, StringComparer.Ordinal) &&
        definition.RequiredPermissions.All(p => permissions.Contains(p, StringComparer.Ordinal));
}

// Raw owning-source observation, computed ONLY over records the actor may
// currently read. ExpectedSources counts visible source subjects (e.g. visible
// vessels); ReadSources those whose source could be read; Withheld = expected - read.
public sealed record MetricObservation(
    int ExpectedSources,
    int ReadSources,
    long? Count,
    int UnknownCount,
    IReadOnlyList<long>? CohortComponents = null,
    string? UnavailableReason = null);

public sealed record MetricScalarOutcome(string State, long? Value, string Quality, int UnknownCount, string[] Reasons);

public static class MetricScalarPolicy
{
    public static MetricScalarOutcome Evaluate(MetricDefinition definition, MetricObservation observation)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(observation);
        if (definition.MinCohort < 1) throw new ArgumentException("Minimum cohort must be at least 1.", nameof(definition));
        if (observation.UnavailableReason != null)
            return new(MetricStates.Unavailable, null, MetricQualities.Unknown, 0, [observation.UnavailableReason]);
        if (observation.ExpectedSources < 0 || observation.ReadSources < 0 || observation.ReadSources > observation.ExpectedSources ||
            observation.Count < 0 || observation.UnknownCount < 0 || observation.CohortComponents?.Any(c => c < 0) == true)
            throw new ArgumentException("Metric observation is inconsistent.", nameof(observation));
        var withheld = observation.ExpectedSources - observation.ReadSources;
        // No visible population or no readable source: UNKNOWN, never a false zero.
        if (observation.ExpectedSources == 0)
            return new(MetricStates.Unknown, null, MetricQualities.Unknown, 0, ["NO_VISIBLE_SOURCE"]);
        if (observation.ReadSources == 0 || observation.Count == null)
            return new(MetricStates.Unknown, null, MetricQualities.Unknown, observation.UnknownCount + withheld, ["SOURCE_WITHHELD_OR_UNREADABLE"]);
        var unknown = observation.UnknownCount + withheld;
        var reasons = new List<string>();
        if (withheld > 0) reasons.Add("PARTIAL_SOURCE_COVERAGE");
        if (observation.UnknownCount > 0) reasons.Add("UNKNOWN_SOURCE_VALUES");
        var quality = unknown > 0 ? MetricQualities.Partial : MetricQualities.Complete;
        if (definition.MinCohort > 1)
        {
            // 55.01 paragraph:646-647: count suppression plus complementary
            // suppression so a narrower scope cannot be differenced out of a wider one.
            if (observation.Count is > 0 && observation.Count < definition.MinCohort)
                return new(MetricStates.Suppressed, null, quality, unknown, [.. reasons, "SMALL_GROUP"]);
            if (observation.CohortComponents?.Any(c => c > 0 && c < definition.MinCohort) == true)
                return new(MetricStates.Suppressed, null, quality, unknown, [.. reasons, "COMPLEMENTARY_SUPPRESSION"]);
        }
        return new(MetricStates.Value, observation.Count, quality, unknown, reasons.ToArray());
    }
}
