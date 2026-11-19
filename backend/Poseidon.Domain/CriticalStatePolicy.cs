using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-024 / T-S2-005. One shared, server-built explanation for every critical state the UI
// shows (onboarding readiness, profile freshness, compliance applicability, Why panels):
// owning source (record + revision/time), reason codes, freshness and blockers with a
// resolution type. 61.03 p211/p213/p266: a hidden blocker may make the state non-positive,
// but a reader without the owning detail authority never receives its reason, count, source
// owner or revision. 54.02 p153: a withheld field is omitted, never sent as null.
public sealed record CriticalStateSource(string Kind, Guid Id, long? Revision, DateTimeOffset? RecordedAt, string Provenance);
public sealed record CriticalStateFreshness(string State, string ReasonCode, DateTimeOffset? ObservedAt, DateTimeOffset CheckedAt);
public sealed record CriticalStateBlocker(string FieldPath, string ReasonCode, string ResolutionType);

public sealed record CriticalStateExplanation(string Key, Guid SubjectId, string State, string Disclosure, DateTimeOffset AsOf,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Verification = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CriticalStateSource? Source = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? ReasonCodes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CriticalStateFreshness? Freshness = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CriticalStateBlocker[]? Blockers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Href = null);

public static class CriticalStatePolicy
{
    public const string Onboarding = "vessel.onboarding";
    public const string ProfileFreshness = "vessel.profile.freshness";
    public const string Applicability = "vessel.compliance.applicability";
    public const string Full = "FULL";
    public const string StateOnly = "STATE_ONLY";
    public const string NotVerified = "NOT_VERIFIED";

    // Closed state set. There is deliberately no READY/VERIFIED/COMPLIANT member: this
    // explanation never grants a positive operational, legal or verification outcome.
    public static IReadOnlyList<string> States { get; } =
        ["BLOCKED", "REVIEW_REQUIRED", "UNKNOWN", "STALE", "UNAVAILABLE", "DECLARED_UNVERIFIED", "QUALIFIED_CURRENT_UNVERIFIED"];
    // DEV024-DEC-02: a state-only reader learns only that the state is not positive.
    // BLOCKED/STALE/DECLARED/UNKNOWN would reveal a reason class (missing data, source age,
    // undeclared source); all collapse into the non-positive REVIEW_REQUIRED, so UNKNOWN is
    // never converted to a positive state. Only an owner service outage stays distinct.
    public static IReadOnlyList<string> RedactedStates { get; } = ["REVIEW_REQUIRED", "UNAVAILABLE"];
    public static IReadOnlyList<string> ResolutionTypes { get; } =
    [
        "PROFILE_DECLARATION", "REQUIRED_DECLARATION", "POLICY_OR_EVIDENCE", "QUALIFIED_EVIDENCE", "SOURCE_DECLARATION",
        "SOURCE_RENEWAL", "FRESHNESS_POLICY_CONFIGURATION", "SERVICE_RECOVERY", "RE_EVALUATION", "QUALIFIED_EVALUATION",
        "CATALOG_QUALIFICATION", "HUMAN_REVIEW",
    ];

    // QA note 1 (DEV-024 re-QA): the full state UNAVAILABLE is reached only after a field passed
    // every source check, so echoing it would leak one bit of the withheld profile. The coarse
    // UNAVAILABLE therefore comes only from the owner's service status (e.g. the freshness policy
    // resolution), which is independent of withheld content; every full state otherwise collapses.
    public static string Redact(string state, bool serviceUnavailable = false) => States.Contains(state)
        ? serviceUnavailable ? "UNAVAILABLE" : "REVIEW_REQUIRED"
        : throw new ArgumentOutOfRangeException(nameof(state), "Unsupported critical state.");

    // Server-side redaction: keeps only key, subject, coarse state, disclosure and time.
    public static CriticalStateExplanation Redact(CriticalStateExplanation full, bool serviceUnavailable = false) =>
        new(full.Key, full.SubjectId, Redact(full.State, serviceUnavailable), StateOnly, full.AsOf);

    // DEV024-DEC-01 per sub-object: the onboarding explanation embeds the DEV-020 freshness
    // summary, whose owner gate (any construction-file-bound revision withholds it) is
    // stricter than the onboarding gate (current draft only). Without the DEV-020 detail the
    // embedded freshness is omitted (never sent as null); the rest stays FULL.
    public static CriticalStateExplanation WithoutProfileFreshness(CriticalStateExplanation full) =>
        full.Key == Onboarding && full.Disclosure == Full ? full with { Freshness = null }
            : throw new ArgumentException("Only a FULL onboarding explanation embeds a separately owned freshness summary.", nameof(full));

    private static CriticalStateExplanation Checked(CriticalStateExplanation value)
    {
        if (!States.Contains(value.State)) throw new ArgumentOutOfRangeException(nameof(value), "Unsupported critical state.");
        if (value.Blockers!.Any(b => !ResolutionTypes.Contains(b.ResolutionType)))
            throw new ArgumentOutOfRangeException(nameof(value), "Unsupported resolution type.");
        return value;
    }

    private static string[] Codes(IEnumerable<string> codes) => codes.Distinct(StringComparer.Ordinal).ToArray();

    // 18.01 row40 / 59.01 p55 onboarding: missing required data blocks submission (POL-DEFAULT-01),
    // unknown data requires review, complete declarations remain unverified.
    public static CriticalStateExplanation ForOnboarding(Guid vessel, OnboardingAssessment assessment, long? profileRevision,
        DateTimeOffset? savedAt, string provenance, FreshnessSummary freshness, DateTimeOffset asOf, string href)
    {
        var state = assessment.Fields.Any(f => f.State == "Missing") || assessment.Blockers.Contains("PROFILE_MISSING") ? "BLOCKED"
            : assessment.Fields.Any(f => f.State == "Unknown") ? "REVIEW_REQUIRED" : "DECLARED_UNVERIFIED";
        return Checked(new(Onboarding, vessel, state, Full, asOf, NotVerified,
            new("RegulatoryDraft", vessel, profileRevision, savedAt, provenance), Codes(assessment.Blockers),
            new(freshness.State, freshness.ReasonCode, freshness.OldestObservedAt, freshness.CheckedAt),
            [.. assessment.BlockerDetails.Select(b => new CriticalStateBlocker(b.FieldPath, b.ReasonCode, b.ResolutionType))], href));
    }

    public static string FreshnessResolution(string reason) => reason switch
    {
        "VALUE_NOT_RECORDED" => "REQUIRED_DECLARATION",
        "VALUE_UNKNOWN" => "POLICY_OR_EVIDENCE",
        "SOURCE_NOT_DECLARED" or "SOURCE_DECLARED_FOR_PRIOR_VALUE" or "SOURCE_UNKNOWN" or "SOURCE_REFERENCE_NOT_RECORDED"
            or "OBSERVED_AT_UNKNOWN" or "OBSERVED_AT_NOT_RECORDED" or "OBSERVED_AT_AFTER_CHECK" => "SOURCE_DECLARATION",
        "FRESHNESS_POLICY_UNCONFIGURED" => "FRESHNESS_POLICY_CONFIGURATION",
        "POLICY_MAX_AGE_EXCEEDED" => "SOURCE_RENEWAL",
        "FIELD_VERSION_UNAVAILABLE" or "FRESHNESS_POLICY_UNAVAILABLE" => "SERVICE_RECOVERY",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), "Unsupported freshness reason.")
    };

    // DEV-020 server freshness. FRESH is only "within an approved policy age" and never verification.
    public static CriticalStateExplanation ForProfileFreshness(Guid vessel, long profileRevision, DateTimeOffset? savedAt, string provenance,
        IReadOnlyList<(string FieldPath, FieldFreshness Freshness)> fields, FreshnessSummary summary, DateTimeOffset asOf, string href)
    {
        var state = summary.State switch
        {
            "FRESH" => "DECLARED_UNVERIFIED", "STALE" => "STALE", "UNAVAILABLE" => "UNAVAILABLE", _ => "UNKNOWN"
        };
        var open = fields.Where(f => f.Freshness.State != "FRESH").ToArray();
        return Checked(new(ProfileFreshness, vessel, state, Full, asOf, NotVerified,
            new("RegulatoryDraft", vessel, profileRevision == 0 ? null : profileRevision, savedAt, provenance),
            Codes(open.Select(f => f.Freshness.ReasonCode).Prepend(summary.ReasonCode)),
            new(summary.State, summary.ReasonCode, summary.OldestObservedAt, summary.CheckedAt),
            [.. open.Select(f => new CriticalStateBlocker("profile." + f.FieldPath, f.Freshness.ReasonCode, FreshnessResolution(f.Freshness.ReasonCode)))], href));
    }

    public static string ApplicabilityResolution(string code) => code switch
    {
        "QUALIFIED_EVALUATION_UNAVAILABLE" or "EVALUATION_UNAVAILABLE" => "QUALIFIED_EVALUATION",
        "QUALIFIED_CATALOG_UNAVAILABLE" => "CATALOG_QUALIFICATION",
        "QUALIFICATION_LINEAGE_UNAVAILABLE" or "CIRCULAR_INPUT_LINEAGE" => "SERVICE_RECOVERY",
        _ when code.EndsWith("_CHANGED", StringComparison.Ordinal) => "RE_EVALUATION",
        _ => "HUMAN_REVIEW"
    };

    // 60.03 p98/p105: missing, stale, unverified or conflicting input is UNKNOWN and defaults to
    // REVIEW_REQUIRED; a superseded input (*_CHANGED) makes the recorded result STALE, never current.
    public static CriticalStateExplanation ForApplicability(Guid vessel, bool available, bool current, Guid? evaluationId,
        DateTimeOffset? evaluatedAt, string[] blockers, FreshnessSummary inputs, DateTimeOffset asOf, string href)
    {
        if (available != (evaluationId != null) || current && (!available || blockers.Length != 0))
            throw new ArgumentException("Inconsistent applicability review.", nameof(current));
        var changed = blockers.Where(b => b.EndsWith("_CHANGED", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        var state = !available ? "UNAVAILABLE" : changed.Length > 0 ? "STALE" : current ? "QUALIFIED_CURRENT_UNVERIFIED" : "REVIEW_REQUIRED";
        var freshness = !available ? new CriticalStateFreshness("UNKNOWN", blockers.FirstOrDefault() ?? "QUALIFIED_EVALUATION_UNAVAILABLE", null, asOf)
            : changed.Length > 0 ? new CriticalStateFreshness("STALE", changed[0], evaluatedAt, asOf)
            : new CriticalStateFreshness(inputs.State, inputs.ReasonCode, inputs.OldestObservedAt, inputs.CheckedAt);
        // 60.03 p291: an immutable evaluation is result version 1; a recomputation is a new evaluation id.
        return Checked(new(Applicability, vessel, state, Full, asOf, NotVerified,
            evaluationId is { } id ? new("ApplicabilityEvaluation", id, 1, evaluatedAt, "QualifiedEvaluation") : null,
            Codes(blockers), freshness,
            [.. Codes(blockers).Select(code => new CriticalStateBlocker("applicability", code, ApplicabilityResolution(code)))], href));
    }
}
