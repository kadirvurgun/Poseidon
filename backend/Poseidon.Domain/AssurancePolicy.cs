using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// S10 WP-A DEV-113 / T-ASS2-001 (18.01 Backlog row:114; industry-assurance 13.02 paragraph:399-400, 15.01 paragraph:755-756,
// 62.04 paragraph:376, 09.01 paragraph:228-229 INDUSTRY_ASSURANCE, 03.01 NOZZLE Kaynakları row:15).
// The OCIMF SIRE 2.0 question set / report format is licensed and NOT in this repository. This policy works on a
// POSEIDON SYNTHETIC column schema pinned by SourceVersion; it is not the OCIMF format and makes no claim to be.
// An import is a controlled draft intake: it never closes, verifies or accepts a finding, never overwrites an accepted
// item value without a recorded human acceptance, and applies only to tankers (UNKNOWN type -> REVIEW_REQUIRED).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssuranceItemContent(string QuestionRef, string? Chapter, string ObservationType, string? ObservationCategory,
    string? ObservationText, string? InspectorComment);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssuranceColumnBinding(string Header, string Field);
public sealed record AssuranceRowIssue(int Row, string Code);
public sealed record AssuranceDiffEntry(string QuestionRef, string Change, string[] Fields, long? CurrentRevision);
public sealed record AssuranceParsed(AssuranceItemContent[] Items, AssuranceRowIssue[] Issues);

public static class AssurancePolicy
{
    public const string Programme = "SIRE_2_0";
    // Pin: synthetic schema, not the licensed OCIMF format (no licensed file is present or reproduced).
    public const string SourceVersion = "POSEIDON-SYNTHETIC-SIRE2-SCHEMA-v1;OCIMF-SIRE-2.0-FORMAT:LICENSED-NOT-PRESENT;18.01/Backlog/row:114";
    public const string ReadPermission = "assurance.inspection.read", ImportPermission = "assurance.inspection.import",
        AcceptPermission = "assurance.import.accept", EvidencePermission = "assurance.evidence.write", LinkPermission = "assurance.finding.link",
        MappingPermission = "assurance.mapping.write";
    public static readonly string[] Permissions = [ReadPermission, ImportPermission, AcceptPermission, EvidencePermission, LinkPermission, MappingPermission];

    public static readonly string[] Fields = ["question_ref", "chapter", "observation_type", "observation_category", "observation_text", "inspector_comment"];
    public static readonly string[] RequiredFields = ["question_ref", "observation_type"];
    public static readonly string[] ObservationTypes = ["NEGATIVE", "POSITIVE", "NONE", "UNKNOWN"];
    // Synthetic categorisation (human / process / hardware / photograph); UNKNOWN is preserved, never defaulted.
    public static readonly string[] ObservationCategories = ["HUMAN", "PROCESS", "HARDWARE", "PHOTOGRAPH", "UNKNOWN"];
    public const string New = "NEW", Unchanged = "UNCHANGED", Changed = "CHANGED", Absent = "ABSENT";
    public static readonly string[] Changes = [New, Unchanged, Changed, Absent];
    public static readonly string[] ImportStates = ["PENDING_REVIEW", "ACCEPTED", "REJECTED"];
    public static readonly string[] InspectionStates = ["DRAFT"];
    public static readonly string[] FindingStates = ["OPEN"];
    public static readonly string[] LinkStates = ["UNLINKED", "LINKED"];
    public static readonly string[] MediaKinds = ["PHOTO", "VIDEO"];
    public static readonly string[] Origins = ["ONLINE", "OFFLINE"];
    public const string Tanker = "TANKER", NotTanker = "NOT_TANKER", Unknown = "UNKNOWN";
    public const string Applicable = "APPLICABLE", NotApplicable = "NOT_APPLICABLE", ReviewRequired = "REVIEW_REQUIRED";
    public const int MaxRows = 500, MaxText = 4000, MaxBindings = 16;
    private static readonly Regex QuestionPattern = new(@"\A[0-9]{1,2}(\.[0-9]{1,3}){1,3}\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Tanker classification from the declared ship type (vessel profile). SIRE 2.0 is a tanker programme (oil, chemical,
    // LPG and LNG carriers). A tug is never a tanker; a missing/unknown declaration is UNKNOWN (never "not a tanker").
    private static readonly string[] TankerTerms = ["TANKER", "GAS CARRIER", "LNG CARRIER", "LPG CARRIER"];
    public static string Classify(bool tug, string typeState, string? typeValue)
    {
        if (tug) return NotTanker;
        if (typeState != "Known" || string.IsNullOrWhiteSpace(typeValue)) return Unknown;
        var normalized = Regex.Replace(typeValue.ToUpperInvariant().Replace('_', ' ').Replace('-', ' '), @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim();
        return TankerTerms.Any(t => Regex.IsMatch(normalized, @"(\A|\s)" + Regex.Escape(t) + @"(\s|\z)", RegexOptions.None, TimeSpan.FromMilliseconds(100))) ? Tanker : NotTanker;
    }
    public static string Applicability(string classification) => classification switch { Tanker => Applicable, NotTanker => NotApplicable, _ => ReviewRequired };

    // Mapping: every header binds once, every target once, required targets must be bound, unknown targets rejected.
    public static string[] MappingIssues(IReadOnlyList<AssuranceColumnBinding>? bindings)
    {
        if (bindings == null || bindings.Count is 0 or > MaxBindings) return ["INVALID_BINDINGS"];
        var issues = new List<string>();
        if (bindings.Any(b => b == null || string.IsNullOrWhiteSpace(b.Header) || b.Header.Length > 120 || b.Header != b.Header.Trim() || b.Header.Any(char.IsControl)))
            issues.Add("INVALID_HEADER");
        if (bindings.Any(b => b != null && !Fields.Contains(b.Field, StringComparer.Ordinal))) issues.Add("UNKNOWN_TARGET");
        if (bindings.Where(b => b != null).GroupBy(b => b.Header, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) issues.Add("DUPLICATE_HEADER");
        if (bindings.Where(b => b != null).GroupBy(b => b.Field, StringComparer.Ordinal).Any(g => g.Count() > 1)) issues.Add("DUPLICATE_TARGET");
        foreach (var required in RequiredFields.Where(r => bindings.All(b => b?.Field != r))) issues.Add("MISSING_REQUIRED_MAPPING:" + required);
        return issues.ToArray();
    }

    public static string MappingHash(int version, IReadOnlyList<AssuranceColumnBinding> bindings) =>
        Hash("ASSURANCE_MAPPING_V1\n" + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + SourceVersion + "\n" +
            string.Join("\n", bindings.OrderBy(b => b.Field, StringComparer.Ordinal).Select(b => b.Field + "=" + b.Header)));

    // Applies a validated mapping to a table. Cell text is kept verbatim (trimmed); nothing is inferred: an empty
    // observation type is UNKNOWN only when the cell explicitly says UNKNOWN, otherwise the row is invalid.
    public static AssuranceParsed Parse(IReadOnlyList<AssuranceColumnBinding> bindings, ImportTable table)
    {
        if (MappingIssues(bindings).Length > 0) throw new ArgumentException("invalid_mapping");
        var issues = new List<AssuranceRowIssue>();
        if (table.Rows.Length is 0 or > MaxRows) return new([], [new(0, "ROW_COUNT_INVALID")]);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var b in bindings)
        {
            var columns = table.Headers.Select((h, i) => (h, i)).Where(x => string.Equals(x.h?.Trim(), b.Header, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (columns.Length != 1) return new([], [new(0, (columns.Length == 0 ? "HEADER_MISSING:" : "HEADER_DUPLICATE:") + b.Field)]);
            index[b.Field] = columns[0].i;
        }
        string? Cell(string[] row, string field) => index.TryGetValue(field, out var i) && i < row.Length && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
        var items = new List<AssuranceItemContent>();
        for (var r = 0; r < table.Rows.Length; r++)
        {
            var row = table.Rows[r] ?? []; var ordinal = r + 1;
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var q = Cell(row, "question_ref"); var type = Cell(row, "observation_type")?.ToUpperInvariant();
            var category = Cell(row, "observation_category")?.ToUpperInvariant();
            var item = new AssuranceItemContent(q ?? "", Cell(row, "chapter"), type ?? "", category, Cell(row, "observation_text"), Cell(row, "inspector_comment"));
            var problems = ItemIssues(item);
            if (problems.Length > 0) { issues.AddRange(problems.Select(p => new AssuranceRowIssue(ordinal, p))); continue; }
            items.Add(item);
        }
        foreach (var dup in items.GroupBy(i => i.QuestionRef, StringComparer.Ordinal).Where(g => g.Count() > 1)) issues.Add(new(0, "DUPLICATE_QUESTION:" + dup.Key));
        if (items.Count == 0 && issues.Count == 0) issues.Add(new(0, "ROW_COUNT_INVALID"));
        return new(items.OrderBy(i => i.QuestionRef, StringComparer.Ordinal).ToArray(), issues.ToArray());
    }

    public static string[] ItemIssues(AssuranceItemContent item)
    {
        var issues = new List<string>();
        if (!QuestionPattern.IsMatch(item.QuestionRef ?? "")) issues.Add("QUESTION_REF_INVALID");
        if (!ObservationTypes.Contains(item.ObservationType, StringComparer.Ordinal)) issues.Add("OBSERVATION_TYPE_INVALID");
        if (item.ObservationCategory != null && !ObservationCategories.Contains(item.ObservationCategory, StringComparer.Ordinal)) issues.Add("OBSERVATION_CATEGORY_INVALID");
        // A negative observation needs its text; its category stays UNKNOWN when the source did not state one.
        if (item.ObservationType == "NEGATIVE" && item.ObservationText == null) issues.Add("OBSERVATION_TEXT_MISSING");
        foreach (var text in new[] { item.Chapter, item.ObservationText, item.InspectorComment })
            if (text != null && (text.Length > MaxText || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))) { issues.Add("TEXT_INVALID"); break; }
        return issues.ToArray();
    }

    // Never a blind overwrite: an existing accepted value that differs is CHANGED and needs an explicit human decision;
    // an existing item missing from the new import is ABSENT and is retained (never deleted).
    public static AssuranceDiffEntry[] Diff(IReadOnlyDictionary<string, (AssuranceItemContent Content, long Revision)> current, IReadOnlyList<AssuranceItemContent> incoming)
    {
        var result = new List<AssuranceDiffEntry>();
        foreach (var item in incoming)
        {
            if (!current.TryGetValue(item.QuestionRef, out var existing)) { result.Add(new(item.QuestionRef, New, [], null)); continue; }
            var fields = ChangedFields(existing.Content, item);
            result.Add(new(item.QuestionRef, fields.Length == 0 ? Unchanged : Changed, fields, existing.Revision));
        }
        foreach (var key in current.Keys.Where(k => incoming.All(i => i.QuestionRef != k)))
            result.Add(new(key, Absent, [], current[key].Revision));
        return result.OrderBy(d => d.QuestionRef, StringComparer.Ordinal).ToArray();
    }

    public static string[] ChangedFields(AssuranceItemContent a, AssuranceItemContent b)
    {
        var fields = new List<string>();
        if (a.Chapter != b.Chapter) fields.Add("chapter");
        if (a.ObservationType != b.ObservationType) fields.Add("observation_type");
        if (a.ObservationCategory != b.ObservationCategory) fields.Add("observation_category");
        if (a.ObservationText != b.ObservationText) fields.Add("observation_text");
        if (a.InspectorComment != b.InspectorComment) fields.Add("inspector_comment");
        return fields.ToArray();
    }

    // Acceptance must decide every CHANGED item exactly once; NEW items are taken with the batch; UNCHANGED/ABSENT
    // items are never touched. Returns an empty array when the decision set is complete.
    public static string[] AcceptanceIssues(IReadOnlyList<AssuranceDiffEntry> diff, IReadOnlyCollection<string> accept, IReadOnlyCollection<string> reject)
    {
        var changed = diff.Where(d => d.Change == Changed).Select(d => d.QuestionRef).ToHashSet(StringComparer.Ordinal);
        var issues = new List<string>();
        if (accept.Distinct(StringComparer.Ordinal).Count() != accept.Count || reject.Distinct(StringComparer.Ordinal).Count() != reject.Count || accept.Intersect(reject, StringComparer.Ordinal).Any())
            issues.Add("DUPLICATE_DECISION");
        foreach (var q in accept.Concat(reject).Where(q => !changed.Contains(q)).Distinct(StringComparer.Ordinal)) issues.Add("NOT_A_CHANGED_ITEM:" + q);
        foreach (var q in changed.Where(q => !accept.Contains(q) && !reject.Contains(q)).Order(StringComparer.Ordinal)) issues.Add("DECISION_MISSING:" + q);
        return issues.ToArray();
    }

    public static bool IsFinding(AssuranceItemContent item) => item.ObservationType == "NEGATIVE";

    public static string ItemHash(AssuranceItemContent item) => Hash("ASSURANCE_ITEM_V1\n" + JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    public static string DiffHash(IEnumerable<AssuranceDiffEntry> diff, IEnumerable<AssuranceItemContent> items) =>
        Hash("ASSURANCE_DIFF_V1\n" + JsonSerializer.Serialize(new { diff, items }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
