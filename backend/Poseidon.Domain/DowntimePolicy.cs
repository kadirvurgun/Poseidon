using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DowntimeFact([property: JsonRequired] string State, [property: JsonRequired] string? Value)
{
    public override string ToString() => nameof(DowntimeFact);
}

// Engineering lineage only, outside unresolved DD reference fields. A pin does
// not adopt evidence, assess an insurance claim or close its source record.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DowntimeOriginPin([property: JsonRequired] string Kind, [property: JsonRequired] Guid Id,
    [property: JsonRequired] long Revision, [property: JsonRequired] uint Version,
    [property: JsonRequired] string Hash, [property: JsonRequired] string SourceVersion)
{
    public override string ToString() => nameof(DowntimeOriginPin);
}

public sealed record DowntimeFieldDefinition(string Key, string Type, bool Required, string[] Options,
    bool ReadOnly, bool ReferenceUnavailable, int SourceRow, string? Unit = null, string? RequiredWhen = null);

// A lexical tick count avoids IEEE-754 loss in JSON consumers. One tick is
// 100 ns; this technical observation never supplies the declared source minutes.
public sealed record DowntimeIntervalObservation(string State, string? ElapsedTicks, string Unit, string Qualification);

// DEV-102 / T-OPS2-006. Dictionary state options are candidate declarations;
// source approval/transition/default cells do not authorize a legal conclusion.
public static class DowntimePolicy
{
    public const string SchemaVersion = "poseidon.operational-downtime.draft.v1";
    public const string SourceVersion = "14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2226-2239:v46.3";
    public const string ConditionalUnresolved = "SOURCE_CONDITIONAL_UNRESOLVED";
    public static readonly string[] OriginKinds = ["Defect", "WorkOrder", "Engine"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, MaxDepth = 4,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static DowntimeFieldDefinition F(string key, string type, int row, bool required = false,
        string options = "", bool readOnly = false, string? unit = null) =>
        new(key, type, required, options.Length == 0 ? [] : options.Split(';'), readOnly,
            type == "Reference" && !readOnly, row, unit, required ? null : ConditionalUnresolved);

    public static DowntimeFieldDefinition[] Schema() => [
        F("vessel_id", "Reference", 2226, required: true, readOnly: true),
        F("voyage_id", "Reference", 2227),
        F("downtime_type", "Code", 2228, true, "Technical Failure;Planned Maintenance;Weather;Port Delay;Crew;Regulatory;Commercial;Other"),
        F("start_at", "Instant", 2229, required: true), F("end_at", "Instant", 2230),
        F("duration_minutes", "Number", 2231, unit: "min"), F("cause", "Narrative", 2232, required: true),
        F("linked_defect", "Reference", 2233), F("linked_work_order", "Reference", 2234), F("linked_event", "Reference", 2235),
        F("operational_impact", "Narrative", 2236, required: true),
        F("loss_of_hire_flag", "Code", 2237, options: "YES;NO;REVIEW_REQUIRED", readOnly: true),
        F("claim_evidence", "Reference", 2238),
        F("closure_state", "Code", 2239, true, "OPEN;VERIFIED;CLOSED", true)
    ];

    public static bool TryNormalize(IReadOnlyDictionary<string, DowntimeFact>? input,
        out SortedDictionary<string, DowntimeFact> fields, out string error)
    {
        fields = new(StringComparer.Ordinal); error = "downtime_fields_invalid";
        if (input == null) return false;
        var schema = Schema();
        if (input.Keys.Any(key => !schema.Any(f => f.Key == key && !f.ReadOnly))) return false;
        foreach (var field in schema.Where(f => !f.ReadOnly)) {
            var fact = input.TryGetValue(field.Key, out var supplied) ? supplied : new DowntimeFact("Missing", null);
            if (fact == null) return false;
            if (fact.State is "Missing" or "Unknown") {
                if (fact.Value != null) return false;
            } else if (fact.State == "Known") {
                var text = fact.Value;
                if (string.IsNullOrWhiteSpace(text) || text.Length > (field.Type == "Narrative" ? 4000 : 512) ||
                    text.Any(c => char.IsControl(c) && !(field.Type == "Narrative" && c is '\r' or '\n' or '\t'))) return false;
                if (field.ReferenceUnavailable) { error = "downtime_lookup_source_unavailable"; return false; }
                if (field.Type == "Code" && !field.Options.Contains(text, StringComparer.Ordinal)) return false;
                if (field.Type == "Number" && !LexicalNumber(text)) return false;
                if (field.Type == "Instant" && !TryInstant(text, out _)) return false;
            } else return false;
            fields.Add(field.Key, fact);
        }
        if (fields["start_at"] is { State: "Known", Value: { } from } && fields["end_at"] is { State: "Known", Value: { } to } &&
            TryInstant(from, out var start) && TryInstant(to, out var end) && end < start) {
            error = "downtime_time_order_invalid"; return false;
        }
        error = ""; return true;
    }

    // Source L is Date/Time although M/N retain text transport. This explicit
    // technical instant boundary follows existing operational logs, retains the
    // operator's lexical offset and never assumes a browser or vessel timezone.
    public static bool TryInstant(string? value, out DateTimeOffset instant) => ShipOperationsPolicy.TryInstant(value, out instant);

    private static bool LexicalNumber(string text)
    {
        if (text.Length is < 1 or > 48) return false;
        var digits = text[0] == '-' ? text[1..] : text;
        var parts = digits.Split('.');
        return parts.Length is 1 or 2 && parts.All(p => p.Length > 0 && p.All(c => c is >= '0' and <= '9'));
    }

    public static SortedDictionary<string, DowntimeFact> ReadFields(Guid vessel, string json)
    {
        if (vessel == Guid.Empty) throw new InvalidDataException("downtime_scope_unavailable");
        try {
            using var doc = JsonDocument.Parse(json, new() { MaxDepth = 4 });
            ApplicabilityEngine.RejectDuplicateKeys(doc.RootElement);
            var input = doc.RootElement.Deserialize<SortedDictionary<string, DowntimeFact>>(Json);
            if (TryNormalize(input, out var fields, out _)) {
                fields["vessel_id"] = new("Known", vessel.ToString("D"));
                // AO NO_DEFAULT_DECLARED + blank approved transition/evidence
                // cells: no implicit OPEN, NO or legal REVIEW_REQUIRED decision.
                fields["loss_of_hire_flag"] = new("Unknown", null);
                fields["closure_state"] = new("Unknown", null);
                return fields;
            }
        } catch (JsonException) { }
        throw new InvalidDataException("downtime_fields_unavailable");
    }

    public static DowntimeIntervalObservation ObserveInterval(IReadOnlyDictionary<string, DowntimeFact> fields)
    {
        if (fields.TryGetValue("start_at", out var from) && fields.TryGetValue("end_at", out var to) &&
            from is { State: "Known", Value: { } fromText } && to is { State: "Known", Value: { } toText } &&
            TryInstant(fromText, out var start) && TryInstant(toText, out var end) && end >= start)
            return new("KNOWN", (end - start).Ticks.ToString(CultureInfo.InvariantCulture), "tick", "UNASSESSED");
        return new("UNAVAILABLE", null, "tick", "UNASSESSED");
    }

    public static string[] Blockers(IReadOnlyDictionary<string, DowntimeFact> fields) => Schema().Where(f => !f.ReadOnly)
        .SelectMany(f => new[] {
            f.Required && (!fields.TryGetValue(f.Key, out var fact) || fact?.State != "Known") ? "REQUIRED_" + f.Key.ToUpperInvariant() : null,
            f.RequiredWhen != null ? "CONDITIONAL_REQUIREMENT_UNRESOLVED_" + f.Key.ToUpperInvariant() : null,
            f.ReferenceUnavailable ? "REFERENCE_SOURCE_UNAVAILABLE_" + f.Key.ToUpperInvariant() : null
        }).OfType<string>().Concat([
            "UNVERIFIED_DECLARATION", "SOURCE_POLICY_REVIEW_REQUIRED", "DURATION_FORMULA_NOT_APPROVED",
            "LOSS_OF_HIRE_NOT_ASSESSED", "CLAIM_EVIDENCE_ADOPTION_UNAVAILABLE", "CLOSURE_DEFAULT_UNAVAILABLE",
            "HUMAN_VERIFICATION_NOT_RECORDED", "VERIFIED_CLOSURE_UNAVAILABLE", "OFFLINE_POLICY_UNAVAILABLE"
        ]).ToArray();
}
