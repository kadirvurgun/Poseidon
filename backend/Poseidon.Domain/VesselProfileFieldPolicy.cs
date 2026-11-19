using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// DEV-020 / T-S2-001. Per-field provenance and freshness of the single controlled vessel
// profile. 59.01 §5 measurementSources {fieldPath, source, observedAt, unit} and §35
// SourceState {FRESH|STALE|UNAVAILABLE|UNKNOWN, observedAt, checkedAt}. A declared source
// is never verification; freshness without an approved policy (54.01 ops.source.
// freshness_policy_ref has no default) is UNKNOWN with an explicit reason, never FRESH.
public sealed record ProfileFieldDefinition(string Path, string Kind, string? Unit);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeclareProfileFieldSource(string? FieldPath, long? ExpectedValueRevision, long? ExpectedSequence,
    string? SourceKind, TextInput? Reference, TextInput? ObservedAt);

public sealed record ProfileFieldSourceFacts(string SourceKind, string ReferenceState, string? Reference,
    string ObservedAtState, DateTimeOffset? ObservedAt);

public sealed record FreshnessPolicyResolution(string Status, string? PolicyRef = null,
    IReadOnlyDictionary<string, TimeSpan>? MaxAge = null)
{
    public static FreshnessPolicyResolution Unconfigured { get; } = new("UNCONFIGURED");
    public static FreshnessPolicyResolution Unavailable { get; } = new("UNAVAILABLE");
}

public sealed record FieldFreshness(string State, string ReasonCode, DateTimeOffset? ObservedAt, DateTimeOffset CheckedAt, long? AgeSeconds);

public sealed record FreshnessSummary(string State, string ReasonCode, DateTimeOffset? OldestObservedAt, DateTimeOffset CheckedAt,
    int Fresh, int Stale, int Unknown, int Unavailable);

public static class VesselProfileFieldPolicy
{
    public const int ReferenceLimit = 500;
    // Fixed source-backed profile value fields (59.01 §5, 60.03 VesselRegulatoryProfileView,
    // 09.02 input catalog). Profile-level declaration/source/observation metadata is not a
    // field value and carries its own draft provenance.
    public static IReadOnlyList<ProfileFieldDefinition> Fields { get; } =
    [
        new("imo", "text", null), new("flag", "text", null), new("shipType", "text", null),
        new("classSociety", "text", null), new("classNumber", "text", null),
        new("gt", "number", "TONNAGE_INDEX"), new("nt", "number", "TONNAGE_INDEX"), new("dwt", "number", "TONNE"),
        new("loa", "number", "METRE"), new("breadth", "number", "METRE"), new("draft", "number", "METRE"),
        new("buildDate", "date", null), new("mainPropulsionKw", "number", "KILOWATT"),
        new("contractForConstructionDate", "date", null), new("constructionRecordReference", "text", null),
        new("tradingArea", "text", null), new("cargoContext", "text", null),
    ];
    public static IReadOnlySet<string> Paths { get; } = Fields.Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
    // Declared provenance kinds only. None of them is verification, authority or a sign-off.
    public static IReadOnlyList<string> SourceKinds { get; } =
        ["DOCUMENT", "REGISTRY_RECORD", "CLASS_RECORD", "OWNER_DECLARATION", "SURVEY_OR_MEASUREMENT", "OTHER", "UNKNOWN"];

    public static (string State, string? Value) Value(RegulatoryDraftContent content, string path) => path switch
    {
        "imo" => (content.Imo.State, content.Imo.Value), "flag" => (content.Flag.State, content.Flag.Value),
        "shipType" => (content.ShipType.State, content.ShipType.Value),
        "classSociety" => (content.ClassSociety.State, content.ClassSociety.Value),
        "classNumber" => (content.ClassNumber.State, content.ClassNumber.Value),
        "gt" => (content.Gt.State, content.Gt.Value), "nt" => (content.Nt.State, content.Nt.Value),
        "dwt" => (content.Dwt.State, content.Dwt.Value), "loa" => (content.Loa.State, content.Loa.Value),
        "breadth" => (content.Breadth.State, content.Breadth.Value), "draft" => (content.Draft.State, content.Draft.Value),
        "buildDate" => (content.BuildDate.State, content.BuildDate.Value),
        "mainPropulsionKw" => (content.MainPropulsionKw.State, content.MainPropulsionKw.Value),
        "contractForConstructionDate" => (content.ContractForConstructionDate.State, content.ContractForConstructionDate.Value),
        "constructionRecordReference" => (content.ConstructionRecordReference.State, content.ConstructionRecordReference.Value),
        "tradingArea" => (content.TradingArea.State, content.TradingArea.Value),
        "cargoContext" => (content.CargoContext.State, content.CargoContext.Value),
        _ => throw new ArgumentOutOfRangeException(nameof(path), "Unsupported profile field.")
    };

    // Pins the exact declared value; the value revision alone does not survive history rewrites.
    public static string ValueHash(string state, string? value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"PROFILE_FIELD_VALUE_V1\n{state}\n{value}"))).ToLowerInvariant();

    public static (DeclareProfileFieldSource Command, Dictionary<string, string[]> Errors) Normalize(DeclareProfileFieldSource input, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.FieldPath is null || !Paths.Contains(input.FieldPath)) errors["fieldPath"] = ["unknown_field"];
        if (input.ExpectedValueRevision is not > 0) errors["expectedValueRevision"] = ["positive_revision_required"];
        if (input.ExpectedSequence is not >= 0) errors["expectedSequence"] = ["non_negative_sequence_required"];
        if (input.SourceKind is null || !SourceKinds.Contains(input.SourceKind)) errors["sourceKind"] = ["invalid_source_kind"];
        TextInput reference = new();
        if (input.Reference is null || input.Reference.State is not ("Missing" or "Unknown" or "Known") ||
            (input.Reference.State != "Known" && input.Reference.Value != null)) errors["reference"] = ["invalid_state_value"];
        else if (input.Reference.State == "Known")
        {
            var trimmed = input.Reference.Value?.Trim() ?? "";
            if (trimmed.Length == 0) reference = new();
            else if (trimmed.Length > ReferenceLimit || trimmed.Any(char.IsControl)) errors["reference"] = ["invalid_text"];
            else reference = new("Known", trimmed.Normalize(NormalizationForm.FormC));
        }
        else reference = input.Reference;
        TextInput observed = new();
        if (input.ObservedAt is null || input.ObservedAt.State is not ("Missing" or "Unknown" or "Known") ||
            (input.ObservedAt.State == "Known" ? input.ObservedAt.Value == null : input.ObservedAt.Value != null)) errors["observedAt"] = ["invalid_state_value"];
        else if (input.ObservedAt.State == "Known")
        {
            if (!TryTimestamp(input.ObservedAt.Value!, out var parsed)) errors["observedAt"] = ["offset_timestamp_required"];
            // Observation time is a business fact; a future observation cannot be current evidence.
            else if (parsed > now) errors["observedAt"] = ["observed_at_in_future"];
            else observed = new("Known", Truncate(parsed).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }
        else observed = input.ObservedAt;
        return (input with { Reference = reference, ObservedAt = observed }, errors);
    }

    // PostgreSQL timestamptz keeps microseconds; persist and return the same instant.
    public static DateTimeOffset Truncate(DateTimeOffset value) => value.AddTicks(-(value.Ticks % 10));

    public static bool TryTimestamp(string value, out DateTimeOffset parsed)
    {
        parsed = default;
        return Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.ECMAScript) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }

    // Ordered, deterministic reasons. Only a resolved approved policy can produce FRESH/STALE.
    public static FieldFreshness Evaluate(string path, string valueState, long? valueRevision, ProfileFieldSourceFacts? source,
        bool sourceForPriorValue, FreshnessPolicyResolution policy, DateTimeOffset checkedAt)
    {
        if (!Paths.Contains(path)) throw new ArgumentOutOfRangeException(nameof(path), "Unsupported profile field.");
        FieldFreshness Unknown(string reason, DateTimeOffset? observed = null) => new("UNKNOWN", reason, observed, checkedAt, null);
        if (valueState == "Missing") return Unknown("VALUE_NOT_RECORDED");
        if (valueState == "Unknown") return Unknown("VALUE_UNKNOWN");
        if (valueState != "Known") throw new ArgumentOutOfRangeException(nameof(valueState));
        if (valueRevision is not > 0) return Unknown("FIELD_VERSION_UNAVAILABLE");
        if (source == null) return Unknown(sourceForPriorValue ? "SOURCE_DECLARED_FOR_PRIOR_VALUE" : "SOURCE_NOT_DECLARED");
        if (source.SourceKind == "UNKNOWN") return Unknown("SOURCE_UNKNOWN");
        if (source.ReferenceState != "Known") return Unknown("SOURCE_REFERENCE_NOT_RECORDED");
        if (source.ObservedAtState == "Unknown") return Unknown("OBSERVED_AT_UNKNOWN");
        if (source.ObservedAtState != "Known" || source.ObservedAt is not { } observedAt) return Unknown("OBSERVED_AT_NOT_RECORDED");
        if (observedAt > checkedAt) return Unknown("OBSERVED_AT_AFTER_CHECK", observedAt);
        if (policy.Status == "UNAVAILABLE") return new("UNAVAILABLE", "FRESHNESS_POLICY_UNAVAILABLE", observedAt, checkedAt, null);
        var age = (long)Math.Floor((checkedAt - observedAt).TotalSeconds);
        if (policy.Status != "RESOLVED" || policy.MaxAge is null || !policy.MaxAge.TryGetValue(path, out var maxAge) || maxAge <= TimeSpan.Zero)
            return new("UNKNOWN", "FRESHNESS_POLICY_UNCONFIGURED", observedAt, checkedAt, age);
        return checkedAt - observedAt <= maxAge
            ? new("FRESH", "WITHIN_POLICY_MAX_AGE", observedAt, checkedAt, age)
            : new("STALE", "POLICY_MAX_AGE_EXCEEDED", observedAt, checkedAt, age);
    }

    // Missing/stale never yields a positive current result (59.01 §35). Every tracked field counts.
    public static FreshnessSummary Summarize(IReadOnlyCollection<FieldFreshness> fields, DateTimeOffset checkedAt)
    {
        int Count(string state) => fields.Count(x => x.State == state);
        var (fresh, stale, unknown, unavailable) = (Count("FRESH"), Count("STALE"), Count("UNKNOWN"), Count("UNAVAILABLE"));
        var oldest = fields.Where(x => x.ObservedAt != null).Select(x => x.ObservedAt).Min();
        var (state, reason) = fields.Count == 0 ? ("UNKNOWN", "NO_PROFILE_FIELDS") :
            unavailable > 0 ? ("UNAVAILABLE", "FIELDS_UNAVAILABLE") : stale > 0 ? ("STALE", "FIELDS_STALE") :
            unknown > 0 ? ("UNKNOWN", "FIELDS_UNKNOWN") : ("FRESH", "ALL_FIELDS_WITHIN_POLICY");
        return new(state, reason, oldest, checkedAt, fresh, stale, unknown, unavailable);
    }
}
