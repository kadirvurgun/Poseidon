using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderFact([property: JsonRequired] string State, [property: JsonRequired] string? Value)
{
    public override string ToString() => nameof(WorkOrderFact);
}

// Optional engineering lineage, outside the dictionary's unresolved job_id lookup.
// A plan reference is never a work release, permit, evidence or human decision.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderPlanPin([property: JsonRequired] Guid Id, [property: JsonRequired] long Revision,
    [property: JsonRequired] uint Version, [property: JsonRequired] string Hash,
    [property: JsonRequired] string SourceVersion)
{
    public override string ToString() => nameof(WorkOrderPlanPin);
}

public sealed record WorkOrderFieldDefinition(string Key, string Type, bool Required, string[] Options,
    string SourceLocator, string? RequiredWhen = null, bool KnownUnavailable = false);

// DEV-054 / T-S8-003. Owning incomplete declaration only. The backlog's three
// execution literals do not supply an authorized transition/verification matrix.
public static class WorkOrderPolicy
{
    public const string SchemaVersion = "poseidon.work-order.draft.v1";
    public const string SourceVersion = "18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:55;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:1426-1463:v46.3";
    public const string ConditionalUnresolved = "SOURCE_CONDITIONAL_UNRESOLVED";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, MaxDepth = 4,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static WorkOrderFieldDefinition F(string key, string type, int row, bool required = false, bool unavailable = false) =>
        new(key, type, required, [], $"14.02/sheet:Data Dictionary 2316/row:{row}", required ? null : ConditionalUnresolved, unavailable);

    // DD has 38 screen occurrences / 29 unique keys. Four unique system-state
    // fields and the trusted vessel context are excluded from client mutations.
    // The 24 remaining facts retain conflicts and unavailable lookups explicitly.
    public static WorkOrderFieldDefinition[] Schema() => [
        F("work_order_no", "Text", 1427, required: true),
        F("equipment_id", "Reference", 1428, required: true, unavailable: true),
        F("job_id", "Reference", 1429, unavailable: true),
        F("work_order_type", "Code", 1430, unavailable: true),
        F("priority", "Code", 1431, unavailable: true),
        F("criticality", "Code", 1432, unavailable: true),
        F("planned_start", "Date", 1433), F("due_date", "Date", 1434),
        // The source labels a person but specifies input/date, Date and date.
        // Neither a person catalog nor a date interpretation is approved here.
        F("assigned_to", "SourceConflict", 1435, unavailable: true),
        F("description", "Narrative", 1444), F("planned_finish", "Date", 1448),
        F("actual_start", "Date", 1449), F("actual_finish", "Date", 1450),
        F("executed_by", "Reference", 1452, unavailable: true),
        F("meter_before", "Number", 1453), F("meter_after", "Number", 1454),
        F("findings", "Narrative", 1455), F("work_done", "Narrative", 1456),
        F("spares_used", "Text", 1457), F("measurements", "Text", 1458),
        F("photo_evidence", "Reference", 1459, unavailable: true),
        F("document_evidence", "Reference", 1460, unavailable: true),
        F("verification_result", "Text", 1461),
        F("verified_by", "Reference", 1462, unavailable: true),
        new("execution_stage", "Code", false, ["RELEASED", "IN_PROGRESS", "EVIDENCE_SUBMITTED"], "18.01/sheet:Backlog/row:55")
    ];

    public static bool TryNormalize(IReadOnlyDictionary<string, WorkOrderFact>? input,
        out SortedDictionary<string, WorkOrderFact> fields, out string error)
    {
        fields = new(StringComparer.Ordinal); error = "work_order_fields_invalid";
        if (input == null) return false;
        var schema = Schema();
        if (input.Count > schema.Length || input.Keys.Any(key => !schema.Any(f => f.Key == key))) return false;
        foreach (var field in schema) {
            var fact = input.TryGetValue(field.Key, out var supplied) ? supplied : new WorkOrderFact("Missing", null);
            if (fact == null) return false;
            if (fact.State is "Missing" or "Unknown") {
                if (fact.Value != null) return false;
            } else if (fact.State == "Known") {
                var text = fact.Value;
                if (string.IsNullOrWhiteSpace(text) || text.Length > (field.Type == "Narrative" ? 4000 : 256) ||
                    text.Any(c => char.IsControl(c) && !(field.Type == "Narrative" && c is '\r' or '\n' or '\t'))) return false;
                if (field.KnownUnavailable) { error = "work_order_source_unavailable"; return false; }
                if (field.Type == "Code" && !field.Options.Contains(text, StringComparer.Ordinal)) return false;
                if (field.Type == "Date" && !TryDate(text, out _)) return false;
                if (field.Type == "Number" && !LexicalNumber(text)) return false;
            } else return false;
            fields.Add(field.Key, fact);
        }
        foreach (var (start, end) in new[] { ("planned_start", "planned_finish"), ("actual_start", "actual_finish") }) {
            if (fields[start] is { State: "Known", Value: { } from } && fields[end] is { State: "Known", Value: { } to } &&
                TryDate(from, out var first) && TryDate(to, out var last) && last < first) {
                error = "work_order_date_order_invalid"; return false;
            }
        }
        error = ""; return true;
    }

    // Source M=Date / N=date: preserve calendar dates without timezone conversion.
    public static bool TryDate(string? value, out DateOnly date)
    {
        date = default;
        return value is { Length: 10 } && value.Where((_, i) => i is not 4 and not 7).All(c => c is >= '0' and <= '9') &&
            value[4] == '-' && value[7] == '-' && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    // Technical transport budget only: no declared unit, precision, sign rule,
    // meter arithmetic, stock consumption or operational threshold is invented.
    private static bool LexicalNumber(string text)
    {
        if (text.Length is < 1 or > 48) return false;
        var digits = text[0] == '-' ? text[1..] : text;
        var parts = digits.Split('.');
        return parts.Length is 1 or 2 && parts.All(p => p.Length > 0 && p.All(c => c is >= '0' and <= '9'));
    }

    public static SortedDictionary<string, WorkOrderFact> ReadFields(string json)
    {
        try {
            using var doc = JsonDocument.Parse(json, new() { MaxDepth = 4 });
            ApplicabilityEngine.RejectDuplicateKeys(doc.RootElement);
            var input = doc.RootElement.Deserialize<SortedDictionary<string, WorkOrderFact>>(Json);
            if (TryNormalize(input, out var fields, out _)) return fields;
        } catch (JsonException) { }
        throw new InvalidDataException("work_order_fields_unavailable");
    }

    public static string[] Blockers(IReadOnlyDictionary<string, WorkOrderFact> fields) => Schema()
        .SelectMany(f => new[] {
            f.Required && (!fields.TryGetValue(f.Key, out var fact) || fact.State != "Known") ? "REQUIRED_" + f.Key.ToUpperInvariant() : null,
            f.RequiredWhen != null ? "CONDITIONAL_REQUIREMENT_UNRESOLVED_" + f.Key.ToUpperInvariant() : null,
            f.KnownUnavailable ? "SOURCE_UNAVAILABLE_" + f.Key.ToUpperInvariant() : null
        }).OfType<string>().Concat([
            "UNVERIFIED_DECLARATION", "EXECUTION_STAGE_UNASSESSED", "RELEASE_AUTHORITY_UNAVAILABLE",
            "PERMIT_AND_RISK_NOT_AUTHORIZED", "REQUIRED_EVIDENCE_POLICY_UNAVAILABLE", "EVIDENCE_ADOPTION_UNAVAILABLE",
            "HUMAN_VERIFICATION_NOT_RECORDED", "VERIFICATION_AND_CLOSURE_UNAVAILABLE", "OFFLINE_POLICY_UNAVAILABLE",
            "SOURCE_POLICY_REVIEW_REQUIRED"
        ]).ToArray();
}
