using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// LA-01..06. Controlled 09.03 bytes, not a fetched schema or an executable language.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApplicabilityRule(string RuleId, int Version, RuleSource Source, RuleLifecycle Lifecycle,
    JsonElement Scope, JsonElement When, JsonElement Then, string UnknownPolicy,
    string? TitleTr = null, string? TitleEn = null, int Priority = 0, bool RequiresHumanReview = false,
    string[]? Tags = null, bool ExampleOnly = false, string PublicationStatus = "DRAFT", string SourceVerificationStatus = "PENDING");
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RuleSource(string SourceId, string Locator, string AuthorityLayer, bool Mandatory = false, string? Url = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RuleLifecycle(string State, string EffectiveFrom, string? EffectiveTo = null, string? Supersedes = null);
public enum RuleTruth { Unknown, False, True }
public enum RuleFieldType { Text, Decimal, Date, Boolean, Collection }
public sealed record ContextFact(RuleFieldType Type, string Knowledge, JsonElement Value, string Provenance,
    string Revision, DateTimeOffset? ObservedAt = null, DateTimeOffset? FreshUntil = null, bool AuthoritativeEmpty = false);
public sealed record PredicateReason(string Path, RuleTruth Truth, string Reason, string? Field, string? Operator,
    PredicateReason[] Children, bool ReviewRequired);
public sealed record RulePrediction(string Result, string CurrentResult, bool Authoritative, string Lifecycle,
    PredicateReason Reason, JsonElement[] PredictedActivations, string[] Blockers);
public sealed record RuleConflict(string Code, string Target, string[] RuleReferences, string Status = "REVIEW_REQUIRED");
public sealed record RuleCandidate(string Reference, ApplicabilityRule Rule, RulePrediction Prediction);

public static class ApplicabilityEngine
{
    public const string Contract = "09.03:309afa07b393fb96556a0ea4ec70098b1e2b8648ab6daec181f5a81ce9ac8825";
    public const string AdapterVersion = "controlled-input-catalog-v1";
    public static readonly string[] States = ["DRAFT", "ADOPTED", "AWAITING", "FUTURE_EFFECTIVE", "IN_FORCE", "SUPERSEDED"];
    // 09.02 Input Catalog rows2..24. Unsupported owning adapters return Unknown;
    // this list grants neither source authority nor a new operational codebook.
    public static readonly IReadOnlyDictionary<string, RuleFieldType> Fields = new Dictionary<string, RuleFieldType> {
        ["context.mode"] = RuleFieldType.Text, ["evaluationDate"] = RuleFieldType.Date,
        ["vessel.flag"] = RuleFieldType.Text, ["vessel.type"] = RuleFieldType.Text,
        ["vessel.gt"] = RuleFieldType.Decimal, ["vessel.mainPropulsionKw"] = RuleFieldType.Decimal,
        ["vessel.buildDate"] = RuleFieldType.Date, ["vessel.contractForConstructionDate"] = RuleFieldType.Date,
        ["vessel.classSociety"] = RuleFieldType.Text, ["vessel.tradingArea"] = RuleFieldType.Text,
        ["vessel.operatingArea"] = RuleFieldType.Text, ["operation.jobType"] = RuleFieldType.Text,
        ["operation.profile"] = RuleFieldType.Text, ["operation.serviceContext"] = RuleFieldType.Text,
        ["cargo.packagedDangerousGoods"] = RuleFieldType.Boolean, ["cargo.type"] = RuleFieldType.Text,
        ["equipment.towingWinchFitted"] = RuleFieldType.Boolean, ["class.rulePack.IACS_M79"] = RuleFieldType.Boolean,
        ["regulatory.ismApplicable"] = RuleFieldType.Boolean, ["regulatory.msmdVerified"] = RuleFieldType.Boolean,
        ["crew.requiredRoleEligible"] = RuleFieldType.Boolean, ["evidence.sourceVerified"] = RuleFieldType.Boolean,
        ["source.lifecycle"] = RuleFieldType.Text
    };
    public static bool Date(string? s, out DateOnly date) => DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static bool Text(string? s, int max = 500) => s is { Length: > 0 } && s.Length <= max && !s.Any(char.IsControl);
    public static ApplicabilityRule Parse(string json)
    {
        if (json.Length > 32768) throw new JsonException("rule_limit");
        using var doc = JsonDocument.Parse(json, new() { MaxDepth = 24 });
        RejectDuplicateKeys(doc.RootElement);
        Shape(doc.RootElement, ["ruleId", "version", "source", "lifecycle", "scope", "when", "then", "unknownPolicy"], ["titleTr", "titleEn", "priority", "requiresHumanReview", "tags", "exampleOnly", "publicationStatus", "sourceVerificationStatus"]);
        var rule = JsonSerializer.Deserialize<ApplicabilityRule>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 24, PropertyNameCaseInsensitive = false }) ?? throw new JsonException("invalid_rule");
        Validate(rule); return rule;
    }
    public static void RejectDuplicateKeys(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object) {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in e.EnumerateObject()) { if (!keys.Add(p.Name)) throw new JsonException("duplicate_key"); RejectDuplicateKeys(p.Value); }
        } else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) RejectDuplicateKeys(x);
    }
    private static void Shape(JsonElement e, string[] required, string[] optional)
    {
        if (e.ValueKind != JsonValueKind.Object || required.Any(k => !e.TryGetProperty(k, out _)) ||
            e.EnumerateObject().Any(p => !required.Contains(p.Name) && !optional.Contains(p.Name))) throw new JsonException("invalid_shape");
    }
    public static void Validate(ApplicabilityRule r)
    {
        if (!Text(r.RuleId, 100) || r.Version < 1 || r.Source == null || r.Lifecycle == null || !Text(r.Source.SourceId, 100) ||
            !Text(r.Source.Locator) || !new[] { "INTERNATIONAL", "FLAG", "CLASS_RO", "COMPANY_SMS", "OEM", "INDUSTRY_ASSURANCE", "PRODUCT_CONTROL" }.Contains(r.Source.AuthorityLayer) ||
            !States.Contains(r.Lifecycle.State) || !Date(r.Lifecycle.EffectiveFrom, out var start) ||
            r.Lifecycle.EffectiveTo != null && (!Date(r.Lifecycle.EffectiveTo, out var end) || end < start) ||
            r.Lifecycle.State == "SUPERSEDED" && !Text(r.Lifecycle.Supersedes, 100) ||
            r.Priority < 0 || r.Tags is { Length: > 32 } || r.Tags?.Any(x => !Text(x, 100)) == true ||
            r.TitleTr?.Length > 500 || r.TitleEn?.Length > 500 || r.Source.Url?.Length > 1000 ||
            !new[] { "DRAFT", "IN_REVIEW", "APPROVED", "PUBLISHED" }.Contains(r.PublicationStatus) ||
            !new[] { "PENDING", "VERIFIED", "REJECTED" }.Contains(r.SourceVerificationStatus) ||
            r.PublicationStatus == "PUBLISHED" && (r.ExampleOnly || r.SourceVerificationStatus != "VERIFIED")) throw new JsonException("invalid_rule");
        // Configuration authoring cannot weaken product unknown policy. Admission of
        // a different controlled policy belongs to a separately qualified control chain.
        if (r.UnknownPolicy != "REVIEW_REQUIRED") throw new JsonException("unknown_policy_not_admitted");
        Shape(r.Scope, [], ["modes", "flags", "classSocieties", "vesselTypes", "operationTypes"]);
        foreach (var p in r.Scope.EnumerateObject()) {
            if (p.Value.ValueKind != JsonValueKind.Array || p.Value.GetArrayLength() is < 1 or > 32 ||
                p.Value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String || !Text(v.GetString(), 100) || p.Name == "modes" && v.GetString() is not ("SHIP" or "TUG"))) throw new JsonException("invalid_scope");
        }
        Shape(r.Then, ["result"], ["activate", "stateEffect", "reasonCode"]);
        if (r.Then.GetProperty("result").ValueKind != JsonValueKind.String || r.Then.GetProperty("result").GetString() is not ("APPLICABLE" or "NOT_APPLICABLE" or "REVIEW_REQUIRED")) throw new JsonException("invalid_result");
        foreach (var name in new[] { "stateEffect", "reasonCode" })
            if (r.Then.TryGetProperty(name, out var value) && !(name == "stateEffect" && value.ValueKind == JsonValueKind.Null) && (value.ValueKind != JsonValueKind.String || !Text(value.GetString()))) throw new JsonException("invalid_effect");
        if (r.Then.TryGetProperty("activate", out var a)) {
            if (a.ValueKind != JsonValueKind.Array || a.GetArrayLength() > 64) throw new JsonException("activation_limit");
            foreach (var x in a.EnumerateArray()) {
                Shape(x, ["type", "id"], []);
                if (x.GetProperty("type").ValueKind != JsonValueKind.String || !new[] { "REQUIREMENT", "CONTROL", "SCREEN", "FIELD", "EVIDENCE_POLICY", "VERIFICATION_GATE", "TEST_CASE", "NOTIFICATION" }.Contains(x.GetProperty("type").GetString()) ||
                    x.GetProperty("id").ValueKind != JsonValueKind.String || !Text(x.GetProperty("id").GetString(), 150)) throw new JsonException("invalid_activation");
            }
        }
        var nodes = 0; ValidateExpression(r.When, 0, ref nodes);
    }
    private static void ValidateExpression(JsonElement e, int depth, ref int nodes)
    {
        if (depth > 8 || ++nodes > 128 || e.ValueKind != JsonValueKind.Object) throw new JsonException("expression_limit");
        foreach (var key in new[] { "all", "any", "not" }) if (e.TryGetProperty(key, out var children)) {
            Shape(e, [key], []);
            if (key == "not") ValidateExpression(children, depth + 1, ref nodes);
            else {
                if (children.ValueKind != JsonValueKind.Array || children.GetArrayLength() is < 1 or > 32) throw new JsonException("list_limit");
                foreach (var child in children.EnumerateArray()) ValidateExpression(child, depth + 1, ref nodes);
            }
            return;
        }
        Shape(e, ["field", "operator"], ["value"]);
        if (e.GetProperty("field").ValueKind != JsonValueKind.String || !Fields.TryGetValue(e.GetProperty("field").GetString()!, out var type) ||
            e.GetProperty("operator").ValueKind != JsonValueKind.String) throw new JsonException("unsupported_field");
        var op = e.GetProperty("operator").GetString()!; var has = e.TryGetProperty("value", out var value);
        if (op is "EXISTS" or "IS_TRUE" or "IS_FALSE") {
            if (has || op != "EXISTS" && type != RuleFieldType.Boolean) throw new JsonException("invalid_unary"); return;
        }
        if (!has) throw new JsonException("value_required");
        var field = e.GetProperty("field").GetString();
        if (field is "context.mode" or "source.lifecycle") {
            var literals = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : new[] { value };
            var allowed = field == "context.mode" ? new[] { "SHIP", "TUG" } : States;
            if (literals.Any(v => v.ValueKind != JsonValueKind.String || !allowed.Contains(v.GetString()))) throw new JsonException("invalid_controlled_enum");
        }
        if (op is "IN" or "NOT_IN") {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 or > 32 || value.EnumerateArray().Any(v => !Literal(v, type, false))) throw new JsonException("invalid_membership");
        } else if (op is "GT" or "GTE" or "LT" or "LTE") {
            if (type != RuleFieldType.Decimal || value.ValueKind != JsonValueKind.Number || !Literal(value, type, false)) throw new JsonException("invalid_numeric");
        } else if (op is "DATE_GTE" or "DATE_LT") {
            if (type != RuleFieldType.Date || !Literal(value, type, false)) throw new JsonException("invalid_date");
        } else if (op is "EQ" or "NE") {
            if (!Literal(value, type, false)) throw new JsonException("invalid_value");
        } else if (op == "CONTAINS") {
            // 09.02 v1 has no admitted multi-value adapter. Never reinterpret text
            // substring matching as a controlled multi-value relation.
            if (type != RuleFieldType.Collection || !Literal(value, RuleFieldType.Text, false)) throw new JsonException("collection_adapter_unavailable");
        } else throw new JsonException("unsupported_operator");
    }
    private static bool Literal(JsonElement value, RuleFieldType type, bool input) => type switch {
        RuleFieldType.Text => value.ValueKind == JsonValueKind.String && Text(value.GetString()),
        RuleFieldType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        RuleFieldType.Date => value.ValueKind == JsonValueKind.String && Date(value.GetString(), out _),
        RuleFieldType.Decimal => (input ? value.ValueKind == JsonValueKind.String : value.ValueKind == JsonValueKind.Number) &&
            InvariantDecimal.TryNormalize(input ? value.GetString() : value.GetRawText(), out _),
        RuleFieldType.Collection => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= 32 && value.EnumerateArray().All(x => Literal(x, RuleFieldType.Text, true)),
        _ => false
    };
    // Strong Kleene truth values, retaining independent review blockers. Evaluate
    // every child: FALSE AND UNKNOWN is FALSE but still requires review (09.01 §56).
    public static RuleTruth Combine(string op, IEnumerable<RuleTruth> values)
    {
        var v = values.ToArray();
        return op switch {
            "all" => v.Contains(RuleTruth.False) ? RuleTruth.False : v.Contains(RuleTruth.Unknown) ? RuleTruth.Unknown : RuleTruth.True,
            "any" => v.Contains(RuleTruth.True) ? RuleTruth.True : v.Contains(RuleTruth.Unknown) ? RuleTruth.Unknown : RuleTruth.False,
            "not" when v.Length == 1 => v[0] == RuleTruth.Unknown ? RuleTruth.Unknown : v[0] == RuleTruth.True ? RuleTruth.False : RuleTruth.True,
            _ => throw new JsonException("invalid_group")
        };
    }
    public static PredicateReason EvaluateExpression(JsonElement e, IReadOnlyDictionary<string, ContextFact> context, DateTimeOffset asOf, string path = "when")
    {
        foreach (var op in new[] { "all", "any", "not" }) if (e.TryGetProperty(op, out var group)) {
            var children = (op == "not" ? new[] { group } : group.EnumerateArray().ToArray()).Select((x, i) => EvaluateExpression(x, context, asOf, $"{path}.{op}[{i}]")).ToArray();
            return new(path, Combine(op, children.Select(x => x.Truth)), "group", null, op, children, children.Any(x => x.ReviewRequired));
        }
        var field = e.GetProperty("field").GetString()!; var operation = e.GetProperty("operator").GetString()!;
        PredicateReason Unknown(string reason) => new(path, RuleTruth.Unknown, reason, field, operation, [], true);
        if (!context.TryGetValue(field, out var fact)) return Unknown("Missing");
        if (fact.Knowledge != "Known") return Unknown(fact.Knowledge);
        if (fact.ObservedAt > asOf) return Unknown("NotYetObserved");
        if (fact.FreshUntil < asOf) return Unknown("Stale");
        if (Fields.TryGetValue(field, out var expected) && fact.Type != expected) return Unknown("InvalidType");
        if (operation == "EXISTS" && fact.AuthoritativeEmpty && (fact.Value.ValueKind == JsonValueKind.Null || fact.Type == RuleFieldType.Collection && fact.Value.ValueKind == JsonValueKind.Array && fact.Value.GetArrayLength() == 0))
            return new(path, RuleTruth.False, "authoritative_empty", field, operation, [], false);
        if (!Literal(fact.Value, fact.Type, true)) return Unknown("InvalidValue");
        var hasValue = e.TryGetProperty("value", out var value);
        int Compare(JsonElement threshold) {
            if (fact.Type == RuleFieldType.Decimal) {
                InvariantDecimal.TryNormalize(fact.Value.GetString(), out var a); InvariantDecimal.TryNormalize(threshold.GetRawText(), out var b);
                return decimal.Parse(a!, CultureInfo.InvariantCulture).CompareTo(decimal.Parse(b!, CultureInfo.InvariantCulture));
            }
            return string.CompareOrdinal(fact.Value.ValueKind is JsonValueKind.True or JsonValueKind.False ? fact.Value.GetRawText() : fact.Value.GetString(),
                threshold.ValueKind is JsonValueKind.True or JsonValueKind.False ? threshold.GetRawText() : threshold.GetString());
        }
        bool result = operation switch {
            "EXISTS" => fact.Type != RuleFieldType.Collection || fact.Value.GetArrayLength() > 0,
            "IS_TRUE" => fact.Value.ValueKind == JsonValueKind.True,
            "IS_FALSE" => fact.Value.ValueKind == JsonValueKind.False,
            "EQ" => Compare(value) == 0, "NE" => Compare(value) != 0,
            "GT" => Compare(value) > 0, "GTE" => Compare(value) >= 0,
            "LT" => Compare(value) < 0, "LTE" => Compare(value) <= 0,
            "DATE_GTE" => Compare(value) >= 0, "DATE_LT" => Compare(value) < 0,
            "IN" => value.EnumerateArray().Any(x => Compare(x) == 0),
            "NOT_IN" => value.EnumerateArray().All(x => Compare(x) != 0),
            "CONTAINS" when hasValue => fact.Value.EnumerateArray().Any(x => x.GetString() == value.GetString()),
            _ => throw new JsonException("unsupported_operator")
        };
        // An unknown collection is never an empty one; empty without authoritative
        // emptiness is also not sufficient to prove EXISTS false.
        if (operation == "EXISTS" && !result && !fact.AuthoritativeEmpty) return Unknown("EmptyNotAuthoritative");
        return new(path, result ? RuleTruth.True : RuleTruth.False, "compared", field, operation, [], false);
    }
    public static RulePrediction Preview(ApplicabilityRule rule, IReadOnlyDictionary<string, ContextFact> context, DateTimeOffset asOf)
    {
        Validate(rule);
        var reasons = new List<PredicateReason>();
        var scopeFields = new Dictionary<string, string> { ["modes"] = "context.mode", ["flags"] = "vessel.flag", ["classSocieties"] = "vessel.classSociety", ["vesselTypes"] = "vessel.type", ["operationTypes"] = "operation.jobType" };
        foreach (var scope in rule.Scope.EnumerateObject()) {
            var predicate = JsonSerializer.SerializeToElement(new { field = scopeFields[scope.Name], @operator = "IN", value = scope.Value });
            reasons.Add(EvaluateExpression(predicate, context, asOf, "scope." + scope.Name));
        }
        reasons.Add(EvaluateExpression(rule.When, context, asOf));
        var reason = new PredicateReason("rule", Combine("all", reasons.Select(x => x.Truth)), "scope_and_predicate", null, "all", reasons.ToArray(), reasons.Any(x => x.ReviewRequired));
        Date(rule.Lifecycle.EffectiveFrom, out var from); var day = DateOnly.FromDateTime(asOf.UtcDateTime);
        var notCurrent = rule.Lifecycle.State is "ADOPTED" or "AWAITING" or "FUTURE_EFFECTIVE" or "SUPERSEDED" || day < from ||
            rule.Lifecycle.EffectiveTo != null && Date(rule.Lifecycle.EffectiveTo, out var to) && day > to;
        var result = notCurrent ? "NOT_CURRENT" : reason.ReviewRequired || reason.Truth == RuleTruth.Unknown ? "REVIEW_REQUIRED" : reason.Truth == RuleTruth.False ? "NOT_APPLICABLE" : rule.Then.GetProperty("result").GetString()!;
        // This package admits no authoritative publication/reviewer proof. Declarations
        // in source JSON cannot cross that boundary, even IN_FORCE/PUBLISHED claims.
        return new(result, "REVIEW_REQUIRED", false, rule.Lifecycle.State, reason,
            result == "APPLICABLE" && rule.Then.TryGetProperty("activate", out var activations) ? activations.EnumerateArray().Select(x => x.Clone()).ToArray() : [],
            ["preview_only", "source_admission_pending", "independent_reviewer_binding_pending"]);
    }

    // Preserve both candidate obligations. Numeric priority is never legal precedence.
    // These are review records in the immutable evaluation, not source corrections.
    public static RuleConflict[] Conflicts(IEnumerable<RuleCandidate> candidates)
    {
        var rows = candidates.ToArray(); var conflicts = new List<RuleConflict>();
        var targets = rows.SelectMany(r => r.Rule.Then.TryGetProperty("activate", out var a)
            ? a.EnumerateArray().Select(x => (Target: x.GetProperty("type").GetString() + ":" + x.GetProperty("id").GetString(), Row: r))
            : Enumerable.Empty<(string Target, RuleCandidate Row)>());
        foreach (var group in targets.GroupBy(x => x.Target, StringComparer.Ordinal)) {
            var applicable = group.Where(x => x.Row.Prediction.Result == "APPLICABLE").Select(x => x.Row).ToArray();
            var excluded = group.Where(x => x.Row.Prediction.Result == "NOT_APPLICABLE" && x.Row.Prediction.Reason.Truth == RuleTruth.True).Select(x => x.Row).ToArray();
            if (applicable.Length > 0 && excluded.Length > 0)
                conflicts.Add(new("CANDIDATE_SOURCE_CONTRADICTION", group.Key, applicable.Concat(excluded).Select(x => x.Reference).Distinct().Order(StringComparer.Ordinal).ToArray()));
        }
        IEnumerable<PredicateReason> Flatten(PredicateReason reason) => new[] { reason }.Concat(reason.Children.SelectMany(Flatten));
        foreach (var group in rows.SelectMany(r => Flatten(r.Prediction.Reason).Where(p => p.Reason == "Conflicting").Select(p => (r.Reference, p.Field))).GroupBy(x => x.Field))
            conflicts.Add(new("INPUT_SOURCE_CONFLICT", group.Key ?? "unavailable", group.Select(x => x.Reference).Distinct().Order(StringComparer.Ordinal).ToArray()));
        return conflicts.OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Target, StringComparer.Ordinal).ToArray();
    }
}
