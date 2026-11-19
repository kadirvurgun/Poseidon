using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

public sealed record ParameterFieldError(string Path, string Code);
// RequestedEffectiveFrom keeps the caller's explicit boundary (null = "now at acceptance") for retry identity.
public sealed record ParameterProposal(string ScopeKind, string SelectorJson, string SelectorHash, string ValueMode, string? ValueJson,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, string Reason, DateTimeOffset? RequestedEffectiveFrom);

// PLAN-1511-5401 typed validation (54.01 paragraph:13-17, :25, :77, :170-183). Closed types only:
// no free JSON, script, regex or caller-supplied validator. Errors keep the draft untouched.
public static partial class ParameterValuePolicy
{
    public const int ReasonMin = 10, ReasonMax = 2000; // ParameterRevision.reason 10-2000 (paragraph:15).
    // Shared by proposal and decision reasons: 10-2000 characters, no control characters except line breaks/tab.
    public static bool ValidReason(string reason) => reason.Trim().Length >= ReasonMin && reason.Length <= ReasonMax &&
        !reason.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
    private static readonly string[] ScopeOrder = ["VESSEL", "COMPANY", "TENANT", "GLOBAL"];
    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9._:\-]{0,127}\z", RegexOptions.CultureInvariant)] private static partial Regex SelectorValue();
    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9_.]{0,63}\z", RegexOptions.CultureInvariant)] private static partial Regex ObjectType();
    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)] private static partial Regex Sha256();
    [GeneratedRegex(@"\A-?(0|[1-9][0-9]{0,18})\z", RegexOptions.CultureInvariant)] private static partial Regex IntegerLiteral();

    public static int ScopeRank(string kind) => Array.IndexOf(ScopeOrder, kind);

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    // Owner fields, invariants, aliases and family references never accept a generic value write.
    public static string? WriteRejection(ParameterDefinition definition) => definition.Classification switch
    {
        ParameterClassifications.OwnerField => "owner_field_not_writable",
        ParameterClassifications.Invariant => "invariant_not_writable",
        ParameterClassifications.Alias => "alias_read_only",
        ParameterClassifications.FamilyReference => "family_not_writable",
        _ => definition.Writable ? null : "owner_field_not_writable"
    };

    // EXACT_ONLY selectors (paragraph:2326 precedenceContract): every declared selector, nothing else.
    // Shape is checked here; owner catalogue membership needs the owner adapter (not registered).
    public static (string Json, string Hash)? Selectors(ParameterDefinition definition, IReadOnlyDictionary<string, string>? values)
    {
        values ??= new Dictionary<string, string>();
        if (values.Count != definition.RequiredSelectors.Length || definition.RequiredSelectors.Any(name =>
                !values.TryGetValue(name, out var value) || value == null || !SelectorValue().IsMatch(value)))
            return null;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var name in definition.RequiredSelectors.Order(StringComparer.Ordinal)) writer.WriteString(name, values[name]);
            writer.WriteEndObject();
        }
        var json = Encoding.UTF8.GetString(stream.ToArray());
        return (json, Hash(json));
    }

    public static (string? Json, string? Error) Value(ParameterDefinition definition, JsonElement? input)
    {
        if (input is not { } value || value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            // REVISION_REF is nullable in source, but an absent policy is UNCONFIGURED, never a proposable value.
            return (null, "type_mismatch");
        var type = definition.Type;
        switch (type.Type)
        {
            case "INTEGER":
                if (value.ValueKind != JsonValueKind.Number || !IntegerLiteral().IsMatch(value.GetRawText()) || !value.TryGetInt64(out var number))
                    return (null, "type_mismatch");
                if (type.Minimum is { } min && number < min || type.Maximum is { } max && number > max) return (null, "out_of_range");
                return (number.ToString(CultureInfo.InvariantCulture), null);
            case "BOOLEAN":
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? (value.ValueKind == JsonValueKind.True ? "true" : "false", null) : (null, "type_mismatch");
            case "REVISION_REF":
                return RevisionRef(definition, value);
            default:
                return (null, "type_mismatch");
        }
    }

    // RevisionRef {ownerDomain,objectType,objectId,revisionId,contentHash} (paragraph:19). A policy
    // reference may only point to its own owner domain; it cannot overwrite another module's record.
    private static (string? Json, string? Error) RevisionRef(ParameterDefinition definition, JsonElement value)
    {
        string[] names = ["ownerDomain", "objectType", "objectId", "revisionId", "contentHash"];
        if (value.ValueKind != JsonValueKind.Object) return (null, "type_mismatch");
        var properties = value.EnumerateObject().ToArray();
        if (properties.Length != names.Length || properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != names.Length ||
            properties.Any(p => !names.Contains(p.Name, StringComparer.Ordinal) || p.Value.ValueKind != JsonValueKind.String))
            return (null, "type_mismatch");
        var owner = value.GetProperty("ownerDomain").GetString()!;
        var objectType = value.GetProperty("objectType").GetString()!;
        var hash = value.GetProperty("contentHash").GetString()!;
        if (!Guid.TryParseExact(value.GetProperty("objectId").GetString(), "D", out var objectId) || objectId == Guid.Empty ||
            !Guid.TryParseExact(value.GetProperty("revisionId").GetString(), "D", out var revisionId) || revisionId == Guid.Empty ||
            !ObjectType().IsMatch(objectType) || !Sha256().IsMatch(hash))
            return (null, "type_mismatch");
        if (!string.Equals(owner, definition.OwnerDomain, StringComparison.Ordinal)) return (null, "unresolved_reference");
        var json = JsonSerializer.Serialize(new { ownerDomain = owner, objectType, objectId = objectId.ToString("D"), revisionId = revisionId.ToString("D"), contentHash = hash });
        return (json, null);
    }

    public static (ParameterProposal? Proposal, List<ParameterFieldError> Errors) Propose(ParameterDefinition definition, int definitionVersion,
        string? scopeKind, IReadOnlyDictionary<string, string>? selectors, string? valueMode, JsonElement? value,
        DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo, string? reason, DateTimeOffset now)
    {
        var errors = new List<ParameterFieldError>();
        if (WriteRejection(definition) is { } rejected) return (null, [new("/key", rejected)]);
        if (definitionVersion != definition.DefinitionVersion) errors.Add(new("/definitionVersion", "definition_version_mismatch"));
        // GLOBAL values are product release content, never a tenant write.
        if (scopeKind is not ("TENANT" or "COMPANY" or "VESSEL") || !definition.AllowedScopes.Contains(scopeKind, StringComparer.Ordinal))
            errors.Add(new("/scopeKind", "scope_not_allowed"));
        var selector = Selectors(definition, selectors);
        if (selector == null) errors.Add(new("/applicability", "selector_invalid"));
        string? json = null;
        if (valueMode == "VALUE")
        {
            var (normalized, error) = Value(definition, value);
            if (error != null) errors.Add(new("/value", error)); else json = normalized;
        }
        else if (valueMode == "INHERIT")
        {
            // INHERIT carries no value and needs an allowed, less specific scope to climb to.
            if (value is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) }) errors.Add(new("/value", "type_mismatch"));
            if (scopeKind != null && !definition.AllowedScopes.Any(s => ScopeRank(s) > ScopeRank(scopeKind))) errors.Add(new("/valueMode", "scope_not_allowed"));
        }
        else errors.Add(new("/valueMode", "type_mismatch"));
        var from = effectiveFrom ?? now;
        if (from < now) errors.Add(new("/effectiveFrom", "effective_from_in_past"));
        if (effectiveTo is { } to && to <= from) errors.Add(new("/effectiveTo", "outside_effective_dates"));
        if (reason == null || !ValidReason(reason))
            errors.Add(new("/reason", "invalid_reason"));
        if (errors.Count > 0) return (null, errors);
        return (new(scopeKind!, selector!.Value.Json, selector.Value.Hash, valueMode!, json, Micro(from), effectiveTo is { } end ? Micro(end) : null, reason!,
            effectiveFrom is { } requested ? Micro(requested) : null), errors);
    }

    // PostgreSQL timestamptz has microsecond precision; keep responses equal to later reads.
    public static DateTimeOffset Micro(DateTimeOffset value) => value.ToUniversalTime().AddTicks(-(value.UtcTicks % 10));
}

// Maker-checker for every mutable CFG3 value (paragraph:81, :219, :941). Author and approver are
// the server-resolved actor identities; an unknown author or approver fails closed. A synthetic
// fixture proposal can never become an approved value.
public static class ParameterGovernance
{
    public static string? DecisionViolation(Guid authorId, Guid deciderId, string provenance, bool approve) =>
        authorId == Guid.Empty && provenance != "SyntheticUnverified" || deciderId == Guid.Empty ? "separation_of_duties" :
        authorId == deciderId ? "separation_of_duties" :
        approve && provenance != "DeclaredUnverified" ? "synthetic_not_approvable" : null;

    // Selection is append-only and never backdated: an approval takes effect no earlier than its decision.
    public static DateTimeOffset SelectedFrom(DateTimeOffset effectiveFrom, DateTimeOffset decidedAt) => effectiveFrom > decidedAt ? effectiveFrom : decidedAt;
}

public sealed record ParameterScopeLevel(string Kind, Guid? Id);
public sealed record ParameterCandidate(Guid RevisionId, Guid RootId, long Sequence, string ScopeKind, Guid ScopeId, string SelectorHash,
    string ValueMode, string? ValueJson, DateTimeOffset SelectedFrom, DateTimeOffset? EffectiveTo, DateTimeOffset RecordedAt, string ContentHash);
public sealed record ParameterResolution(string Key, int DefinitionVersion, string Status, string ValueClass, string? ValueJson,
    string? ValueMode, Guid? SourceRevisionId, string? SourceScope, Guid? SourceScopeId, long? SourceVersion, DateTimeOffset? SelectedFrom,
    string? ContentHash, bool FallbackUsed, string OwnerVerification, bool PositiveUseAllowed, string[] ReasonCodes);

// CFG3 resolver (paragraph:28-33). Deterministic: the most specific allowed scope is evaluated
// first; a less specific scope is consulted only through an explicit INHERIT value or a
// definition fallback of INHERIT. A missing, expired or ambiguous value is reported, never
// replaced with a guess. Product defaults require a deployment acceptance record, which none has.
public static class ParameterResolver
{
    public static ParameterResolution Resolve(ParameterDefinition definition, IReadOnlyList<ParameterScopeLevel> chain, string selectorHash,
        IEnumerable<ParameterCandidate> candidates, DateTimeOffset effectiveAt, DateTimeOffset knownAt)
    {
        var reasons = new List<string>();
        ParameterResolution Result(string status, ParameterCandidate? source = null, string? value = null, string? scope = null, Guid? scopeId = null, bool fallback = false)
        {
            // 54.01 paragraph:81-85: an APPROVED stored value is not ACTIVE until a system activate
            // (schedule, quorum, approval lifetime, configEpoch) exists. Until then a value selected
            // from a stored decision is reported but never allowed for positive use (P5401-OPEN-01).
            var approvedNotActivated = status == "RESOLVED" && source != null;
            var positive = status == "RESOLVED" && definition.OwnerVerification == "NOT_REQUIRED" && !approvedNotActivated;
            if (approvedNotActivated) reasons.Add("APPROVED_NOT_ACTIVATED");
            if (status == "RESOLVED" && definition.OwnerVerification != "NOT_REQUIRED") reasons.Add("OWNER_VERIFICATION_REQUIRED_FOR_POSITIVE_USE");
            return new(definition.Key, definition.DefinitionVersion, status, definition.ValueClass, value ?? source?.ValueJson, source?.ValueMode ?? (value != null ? "VALUE" : null),
                source?.RevisionId, scope ?? source?.ScopeKind, scopeId ?? source?.ScopeId, source?.Sequence, source?.SelectedFrom, source?.ContentHash,
                fallback, definition.OwnerVerification, positive, [.. reasons]);
        }
        switch (definition.Classification)
        {
            case ParameterClassifications.OwnerField: reasons.Add("OWNER_FIELD_RESOLVED_BY_OWNER_RECORD"); return Result("BLOCKED");
            case ParameterClassifications.Alias: reasons.Add("ALIAS_READ_THROUGH_CANONICAL"); return Result("BLOCKED");
            case ParameterClassifications.FamilyReference: reasons.Add("FAMILY_OWNER_PURPOSE_CONTEXT_REQUIRED"); return Result("UNKNOWN");
            case ParameterClassifications.Invariant:
                if (definition.ApprovedProductDefault == null) { reasons.Add("INVARIANT_VALUE_UNAVAILABLE"); return Result("BLOCKED"); }
                reasons.Add("PRODUCT_INVARIANT"); return Result("RESOLVED", value: definition.ApprovedProductDefault, scope: "GLOBAL");
        }
        var levels = chain.Where(x => definition.AllowedScopes.Contains(x.Kind, StringComparer.Ordinal)).ToArray();
        if (levels.Length == 0) { reasons.Add("SCOPE_NOT_ALLOWED"); return Result("BLOCKED"); }
        var known = candidates.Where(c => c.SelectorHash == selectorHash && c.RecordedAt <= knownAt && c.SelectedFrom <= effectiveAt).ToArray();
        foreach (var level in levels)
        {
            if (level.Kind == "GLOBAL") return ProductDefault();
            var selections = known.Where(c => c.ScopeKind == level.Kind && c.ScopeId == level.Id).ToArray();
            if (selections.Length > 0)
            {
                // Append-only cutover: the latest selection of the root is current; an earlier one never resumes.
                var latest = selections.Max(c => c.SelectedFrom);
                var current = selections.Where(c => c.SelectedFrom == latest).ToArray();
                if (current.Length != 1)
                { reasons.Add("EQUAL_PRECEDENCE_SELECTIONS"); return Result("AMBIGUOUS"); }
                var selected = current[0];
                if (selected.EffectiveTo is { } end && effectiveAt >= end)
                { reasons.Add("SELECTED_REVISION_EXPIRED"); return Result("UNCONFIGURED"); }
                if (selected.ValueMode == "VALUE") return Result("RESOLVED", selected);
                reasons.Add("EXPLICIT_INHERIT");
                continue;
            }
            if (definition.Fallback == "INHERIT") { reasons.Add("FALLBACK_INHERIT"); continue; }
            reasons.Add("NO_APPROVED_VALUE_AT_SCOPE");
            if (definition.Fallback == "APPROVED_DEFAULT") return ProductDefault();
            reasons.Add("FALLBACK_BLOCK");
            return Result("UNCONFIGURED");
        }
        if (definition.AllowedScopes.Contains("GLOBAL", StringComparer.Ordinal)) return ProductDefault();
        reasons.Add("NO_PARENT_SCOPE");
        return Result("UNCONFIGURED");

        ParameterResolution ProductDefault()
        {
            if (definition.ApprovedProductDefault == null) { reasons.Add("NO_PRODUCT_DEFAULT"); return Result("UNCONFIGURED"); }
            if (!definition.ProductDefaultDeploymentAccepted) { reasons.Add("PRODUCT_DEFAULT_NOT_DEPLOYMENT_ACCEPTED"); return Result("UNCONFIGURED"); }
            reasons.Add("APPROVED_PRODUCT_DEFAULT");
            return Result("RESOLVED", value: definition.ApprovedProductDefault, scope: "GLOBAL", fallback: true);
        }
    }
}
