using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DefectFact([property: JsonRequired] string State, [property: JsonRequired] string? Value)
{
    public override string ToString() => nameof(DefectFact);
}

// This optional engineering link does not assert the unresolved logical
// asset-reliability.asset_name field is a physical FK or an approved lookup.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DefectAssetPin([property: JsonRequired] Guid Id, [property: JsonRequired] long Revision,
    [property: JsonRequired] uint Version, [property: JsonRequired] string Hash)
{
    public override string ToString() => nameof(DefectAssetPin);
}

public sealed record DefectFieldDefinition(string Key, string Type, bool Required, string[] Options,
    string SourceLocator, string? RequiredWhen = null);
public sealed record DefectTemporalAssessment(string TemporaryMeasureState, DateTimeOffset AssessedAt,
    bool ClosureAvailable = false);

// DEV-055 / T-S8-004: incomplete critical-defect declarations. The source gives
// a lifecycle, not an approved transition/decision matrix. No amount of draft
// text establishes an effective safeguard, verification or return to service.
public static class DefectPolicy
{
    public const string SchemaVersion = "poseidon.defect.draft.v1";
    public const string SourceVersion = "18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:56;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:443-465:v46.3";
    private const string Backlog = "18.01/sheet:Backlog/row:56";
    private const string Conditional = "SOURCE_CONDITIONAL_UNRESOLVED";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, MaxDepth = 4,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static DefectFieldDefinition[] Schema() => [
        new("defect_no", "Text", false, [], "14.02/sheet:Data Dictionary 2316/row:461", Conditional),
        new("record_datetime", "Instant", false, [], "14.02/sheet:Data Dictionary 2316/row:447"),
        new("criticality", "Code", false, ["Critical", "High", "Medium", "Low"], "14.02/sheet:Data Dictionary 2316/row:457", Conditional),
        new("failure_consequence", "Narrative", false, [], "14.02/sheet:Data Dictionary 2316/row:462", Conditional),
        new("safeguard", "Narrative", false, [], "14.02/sheet:Data Dictionary 2316/row:463", Conditional),
        new("restriction", "Narrative", false, [], Backlog),
        new("temporary_measure", "Narrative", false, [], Backlog),
        new("temporary_measure_expires_at", "Instant", false, [], Backlog),
        new("action", "Narrative", false, [], Backlog),
        new("lifecycle_stage", "Code", false, ["Detect", "Assess", "Safeguard", "Action", "Verify", "Effectiveness"], Backlog),
        new("notes", "Narrative", false, [], "14.02/sheet:Data Dictionary 2316/row:465")
    ];

    public static bool TryNormalize(IReadOnlyDictionary<string, DefectFact>? input,
        out SortedDictionary<string, DefectFact> fields, out string error)
    {
        fields = new(StringComparer.Ordinal); error = "defect_fields_invalid";
        if (input == null) return false;
        var schema = Schema();
        if (input.Count > schema.Length || input.Keys.Any(key => !schema.Any(f => f.Key == key))) return false;
        foreach (var field in schema) {
            var fact = input.TryGetValue(field.Key, out var supplied) ? supplied : new DefectFact("Missing", null);
            if (fact == null) return false;
            if (fact.State is "Missing" or "Unknown") {
                if (fact.Value != null) return false;
            } else if (fact.State == "Known") {
                var text = fact.Value;
                if (string.IsNullOrWhiteSpace(text) || text.Length > (field.Type == "Narrative" ? 4000 : 256) ||
                    text.Any(c => char.IsControl(c) && !(field.Type == "Narrative" && c is '\r' or '\n' or '\t'))) return false;
                if (field.Type == "Code" && !field.Options.Contains(text, StringComparer.Ordinal)) return false;
                if (field.Type == "Instant" && !TryInstant(text, out _)) return false;
            } else return false;
            fields.Add(field.Key, fact);
        }
        error = ""; return true;
    }

    public static bool TryInstant(string? value, out DateTimeOffset instant) => ShipOperationsPolicy.TryInstant(value, out instant);

    public static SortedDictionary<string, DefectFact> ReadFields(string json)
    {
        try {
            using var doc = JsonDocument.Parse(json, new() { MaxDepth = 4 });
            ApplicabilityEngine.RejectDuplicateKeys(doc.RootElement);
            var input = doc.RootElement.Deserialize<SortedDictionary<string, DefectFact>>(Json);
            if (TryNormalize(input, out var result, out _)) return result;
        } catch (JsonException) { }
        throw new InvalidDataException("defect_fields_unavailable");
    }

    public static string[] Blockers(IReadOnlyDictionary<string, DefectFact> fields) =>
        Schema().Where(f => f.RequiredWhen != null).Select(f => "CONDITIONAL_REQUIREMENT_UNRESOLVED_" + f.Key.ToUpperInvariant())
        .Concat(new[] { "UNVERIFIED_DECLARATION", "CRITICALITY_NOT_ASSESSED", "SAFEGUARD_NOT_VERIFIED",
            "RESTRICTION_NOT_AUTHORIZED", "EVIDENCE_ADOPTION_UNAVAILABLE", "HUMAN_VERIFICATION_NOT_RECORDED",
            "EFFECTIVENESS_NOT_ASSESSED", "CLOSURE_POLICY_UNAVAILABLE", "OFFLINE_POLICY_UNAVAILABLE" }).ToArray();

    public static DefectTemporalAssessment Assess(IReadOnlyDictionary<string, DefectFact> fields, DateTimeOffset serverNow)
    {
        var now = serverNow.ToUniversalTime();
        fields.TryGetValue("temporary_measure", out var measure);
        fields.TryGetValue("temporary_measure_expires_at", out var expiry);
        var state = measure?.State switch {
            "Known" => expiry?.State switch {
                "Known" when TryInstant(expiry.Value, out var due) => due <= now ? "EXPIRED_DECLARED" : "NOT_DUE_DECLARED",
                "Unknown" => "DEADLINE_UNKNOWN",
                _ => "DEADLINE_MISSING"
            },
            "Unknown" => "MEASURE_UNKNOWN",
            _ => "MEASURE_MISSING"
        };
        return new(state, now);
    }
}
