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
    // S11 WP-B DEV-072: pilot metrics are evaluated only by GET /api/v1/pilot-metrics/summary over a
    // bounded pilot window; the generic scalar endpoint refuses this scope (invalid_metric_scope).
    public const string ScopePilotWindow = "PILOT_WINDOW";
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
        // v2: a vessel with no profile declaration is a not-collected source
        // (55.01 paragraph:263, no zero-fill paragraph:652), never 0 unknown fields.
        new(OnboardingRequiredFieldsUnknown, 2,
            "59.01/18.01 vessel onboarding (ComplianceOnboardingPolicy)",
            "Required onboarding fields declared UNKNOWN (declared profiles only)",
            "field", "projection.compliance-onboarding.v1", "SUM", 1,
            "LIVE_READ;55.01 rpt.freshness.online_min",
            ["directory.read"], ["view"], [ScopeTenantVisible, ScopeVessel], "DECLARED_UNVERIFIED",
            "55.01/paragraph:263;55.01/paragraph:557;55.01/paragraph:652;18.01/Backlog/row:40"),
        // v2: explicit that the count is declared assignments regardless of
        // status or dates; it is not current/onboard crew and Today/Why must not
        // present it as such. MinCohort 5 is an unapproved default (64.01 paragraph:1398).
        new(CrewDeclaredPersons, 2,
            "53.01/58.03 crew assignment declarations (CrewAssignments)",
            "Persons with a declared crew assignment (any status or dates; not current crew)",
            "person", "projection.crew-assignment-declarations.v1", "COUNT_DISTINCT", 5,
            "LIVE_READ;55.01 rpt.freshness.online_min",
            ["directory.read", "crew.read"], ["view"], [ScopeTenantVisible, ScopeVessel], "DECLARED_UNVERIFIED",
            "55.01/paragraph:645-647;55.01/paragraph:661;64.01/paragraph:142;64.01/paragraph:1398"),
        // S11 WP-B DEV-072 (18.01 Backlog row 73, 19.01 paragraph:137-161): privacy-safe pilot event
        // observations. Client-instrumented, so DECLARED_UNVERIFIED; MinCohort 5 distinct pseudonymous
        // contributors is the same unapproved default (64.01 paragraph:1398).
        Pilot(PilotMetricsPolicy.JourneyCompletion, "Pilot journeys completed out of completed or abandoned (permille)", "permille", "RATIO",
            "19.01/paragraph:141-143;18.01/Backlog/row:73"),
        Pilot(PilotMetricsPolicy.RoleAdoption, "Distinct pseudonymous pilot contributors active in the window", "person", "COUNT_DISTINCT",
            "19.01/paragraph:159-161;18.01/Backlog/row:73"),
        Pilot(PilotMetricsPolicy.AiCorrectionRate, "AI suggestions corrected out of accepted, corrected or rejected (permille)", "permille", "RATIO",
            "19.01/paragraph:156-158;18.01/Backlog/row:73"),
        Pilot(PilotMetricsPolicy.SyncConflicts, "Offline sync conflicts observed by pilot clients", "conflict", "COUNT",
            "19.01/paragraph:153-155;18.01/Backlog/row:73"),
        Pilot(PilotMetricsPolicy.EvidenceSearchP95, "Evidence search duration, 95th percentile (nearest rank)", "millisecond", "P95",
            "19.01/paragraph:150-152;18.01/Backlog/row:73"),
    ];

    private static MetricDefinition Pilot(string id, string label, string unit, string aggregation, string source) =>
        new(id, PilotMetricsPolicy.Version(id), "18.01 DEV-072 pilot telemetry (PilotMetricsPolicy)", label, unit, "projection.pilot-metric-events.v1",
            aggregation, PilotMetricsPolicy.MinContributors, "LIVE_READ;55.01 rpt.freshness.online_min",
            ["directory.read", PilotMetricsPolicy.ReadPermission], ["view"], [ScopePilotWindow], "DECLARED_UNVERIFIED", source + ";55.01/paragraph:646-647");

    public static MetricDefinition? Find(string? id) => id == null ? null : All.SingleOrDefault(d => d.Id == id);

    // Audience is a server-provisioned capability check only, never a role name.
    public static bool Permits(MetricDefinition definition, IReadOnlyCollection<string> permissions, string action = "view") =>
        definition.AllowedActions.Contains(action, StringComparer.Ordinal) &&
        definition.RequiredPermissions.All(p => permissions.Contains(p, StringComparer.Ordinal));
}

// Raw owning-source observation, computed ONLY over records the actor may
// currently read. ExpectedSources counts visible source subjects (e.g. visible
// vessels); ReadSources those whose source could be read. Unread sources split
// into Unreadable (stored source cannot be decoded), NotCollected (no source
// declaration exists) and Withheld (current access hides the source) =
// expected - read - unreadable - notCollected.
public sealed record MetricObservation(
    int ExpectedSources,
    int ReadSources,
    long? Count,
    int UnknownCount,
    IReadOnlyList<long>? CohortComponents = null,
    string? UnavailableReason = null,
    int UnreadableSources = 0,
    int NotCollectedSources = 0)
{
    public int WithheldSources => ExpectedSources - ReadSources - UnreadableSources - NotCollectedSources;
}

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
            observation.UnreadableSources < 0 || observation.NotCollectedSources < 0 || observation.WithheldSources < 0 ||
            observation.Count < 0 || observation.UnknownCount < 0 || observation.CohortComponents?.Any(c => c < 0) == true)
            throw new ArgumentException("Metric observation is inconsistent.", nameof(observation));
        // No visible population or no readable source: UNKNOWN, never a false zero.
        if (observation.ExpectedSources == 0)
            return new(MetricStates.Unknown, null, MetricQualities.Unknown, 0, ["NO_VISIBLE_SOURCE"]);
        var unread = observation.ExpectedSources - observation.ReadSources;
        var unknown = observation.UnknownCount + unread;
        // Cause codes stay separate: access withholding is not corruption, and a
        // never-collected source is neither (55.01 paragraph:263, 652).
        var causes = new List<string>();
        if (observation.WithheldSources > 0) causes.Add("SOURCE_WITHHELD");
        if (observation.UnreadableSources > 0) causes.Add("SOURCE_UNREADABLE");
        if (observation.NotCollectedSources > 0) causes.Add("SOURCE_NOT_COLLECTED");
        if (observation.ReadSources == 0 || observation.Count == null)
            return new(MetricStates.Unknown, null, MetricQualities.Unknown, unknown, causes.Count == 0 ? ["SOURCE_UNREADABLE"] : causes.ToArray());
        var reasons = new List<string>();
        if (unread > 0) reasons.AddRange(["PARTIAL_SOURCE_COVERAGE", .. causes]);
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
            // Overlap differential: sum(per-scope components) - distinct total is
            // itself a group (subjects declared in several scopes). A small
            // non-zero overlap would be disclosed by differencing, so suppress.
            if (observation.CohortComponents is { Count: > 1 } components)
            {
                var overlap = components.Sum() - observation.Count.Value;
                if (overlap > 0 && overlap < definition.MinCohort)
                    return new(MetricStates.Suppressed, null, quality, unknown, [.. reasons, "COMPLEMENTARY_SUPPRESSION"]);
            }
        }
        return new(MetricStates.Value, observation.Count, quality, unknown, reasons.ToArray());
    }
}
