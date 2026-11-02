using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// A reviewed software policy is a recorded human qualification, never proof of legal truth.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualifiedInputPolicy(string Field, string Basis, int? MaxAgeHours = null);
// POL-DEFAULT-01: omitted/null choices stay unknown in a draft. Do not replace
// these nullable flags with CLR false defaults; Missing gates review/adoption.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualifiedControlPolicy(Guid? OwnerRole, string? Frequency, string? EvidencePolicy, string? VerificationPolicy,
    int? MaxAgeHours, bool? RequireFile, bool? RequireAcknowledgment, string? SourceLocator, Guid? AcknowledgmentRole = null, string? RequiredVerificationMechanism = null, string? VerificationMechanismSourceLocator = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualificationContent(string? SourceBytesBase64 = null, string? Publisher = null, string? Edition = null,
    Guid? OwnerId = null, Guid? OwnerRole = null, string? ValidationReview = null,
    QualifiedInputPolicy[]? Inputs = null, QualifiedControlPolicy? Control = null, string? Trigger = null, string? ImpactReview = null,
    Guid? SupersedingRuleId = null, long? SupersedingRuleRevision = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TugCalculationPolicy? TugCalculation = null);

public static class RegulatoryQualificationPolicy
{
    public const string Version = "recorded-qualification-v1";
    public static readonly string[] ProfileFields = ["vessel.flag", "vessel.type", "vessel.gt", "vessel.buildDate", "vessel.classSociety", "vessel.tradingArea", "cargo.type"];
    public static void Validate(QualificationContent c)
    {
        if (c.TugCalculation != null) TugCapability.Validate(c.TugCalculation);
        foreach (var s in new[] { c.Publisher, c.Edition, c.ValidationReview, c.Trigger, c.ImpactReview, c.Control?.Frequency,
            c.Control?.EvidencePolicy, c.Control?.VerificationPolicy, c.Control?.SourceLocator, c.Control?.RequiredVerificationMechanism, c.Control?.VerificationMechanismSourceLocator })
            if (s != null && (s.Length is < 1 or > 2000 || s != s.Trim() || s.Any(char.IsControl))) throw new ArgumentException("invalid_qualification");
        if (c.OwnerId == Guid.Empty || c.OwnerRole == Guid.Empty || c.Control?.OwnerRole == Guid.Empty || c.Control?.AcknowledgmentRole == Guid.Empty || c.SupersedingRuleId == Guid.Empty) throw new ArgumentException("invalid_qualification");
        if (c.SourceBytesBase64 != null) {
            try { if (Convert.FromBase64String(c.SourceBytesBase64).Length is < 1 or > 2097152) throw new ArgumentException("source_size"); }
            catch (FormatException) { throw new ArgumentException("source_encoding"); }
        }
        if (c.Inputs is { } inputs && (inputs.Length > 23 || inputs.Any(p => p == null || !ApplicabilityEngine.Fields.ContainsKey(p.Field) ||
            p.Basis is not ("Declared" or "HumanAttested" or "ProviderVerified") || p.MaxAgeHours is <= 0 or > 87600) || inputs.Select(p => p.Field).Distinct().Count() != inputs.Length))
            throw new ArgumentException("invalid_input_policy");
        // 09.02 Input Catalog6/8 explicitly requires a verified vessel profile.
        if (c.Inputs?.Any(p => p.Field is "vessel.gt" or "vessel.buildDate" && p.Basis == "Declared") == true) throw new ArgumentException("verified_profile_required");
        if ((c.SupersedingRuleId == null) != (c.SupersedingRuleRevision == null) || c.SupersedingRuleRevision is <= 0) throw new ArgumentException("invalid_superseding_revision");
        if (c.Control?.MaxAgeHours is <= 0 or > 87600) throw new ArgumentException("invalid_freshness_policy");
    }
    public static string[] Missing(QualificationContent c) => new[] {
        c.TugCalculation != null && !TugCapability.Complete(c.TugCalculation) ? "TUG_CALCULATION_POLICY_MISSING" : null,
        c.SourceBytesBase64 == null ? "EXACT_SOURCE_BYTES_MISSING" : null,
        c.Publisher == null ? "PUBLISHER_MISSING" : null, c.Edition == null ? "EDITION_MISSING" : null,
        c.OwnerId == null || c.OwnerRole == null ? "SOURCE_OWNER_MISSING" : null,
        c.ValidationReview == null ? "VALIDATION_REVIEW_MISSING" : null,
        c.Inputs == null ? "INPUT_POLICY_MISSING" : null, c.Control != null && (c.Control.OwnerRole == null || c.Control.OwnerRole == Guid.Empty || c.Control.RequireFile == null || c.Control.RequireAcknowledgment == null || string.IsNullOrWhiteSpace(c.Control.Frequency) || string.IsNullOrWhiteSpace(c.Control.EvidencePolicy) || string.IsNullOrWhiteSpace(c.Control.VerificationPolicy) || string.IsNullOrWhiteSpace(c.Control.SourceLocator) || c.Control.RequireAcknowledgment == true && c.Control.AcknowledgmentRole == null || c.Control.RequiredVerificationMechanism != null && c.Control.VerificationMechanismSourceLocator == null) ? "CONTROL_POLICY_MISSING" : null
    }.Where(s => s != null).Cast<string>().ToArray();
    public static string? Transition(string? prior, string next, QualificationContent c, bool independent, DateOnly today, RuleLifecycle lifecycle)
    {
        if (next == "DRAFT") return null; // Corrections always restart review, including noops.
        if (next == "IN_REVIEW") return prior == "DRAFT" && Missing(c).Length == 0 ? null : "incomplete_qualification";
        if (prior is not ("IN_REVIEW" or "ADOPTED" or "AWAITING" or "FUTURE_EFFECTIVE" or "IN_FORCE")) return "invalid_qualification_transition";
        if (!independent) return "independent_reviewer_required";
        if (next is not ("ADOPTED" or "AWAITING" or "FUTURE_EFFECTIVE" or "IN_FORCE" or "SUPERSEDED")) return "invalid_qualification_transition";
        if (Missing(c).Length != 0) return "incomplete_qualification";
        if (next == "AWAITING" && c.Trigger == null) return "acceptance_trigger_required";
        if (next == "FUTURE_EFFECTIVE" && c.ImpactReview == null) return "impact_review_required";
        if (next == "IN_FORCE" && (!ApplicabilityEngine.Date(lifecycle.EffectiveFrom, out var from) || from > today ||
            lifecycle.EffectiveTo != null && ApplicabilityEngine.Date(lifecycle.EffectiveTo, out var to) && today > to)) return "outside_effective_dates";
        if (next == "SUPERSEDED" && c.SupersedingRuleId == null) return "superseding_rule_required";
        return null;
    }
    public static string SourceHash(QualificationContent c) => c.SourceBytesBase64 == null ? "" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Convert.FromBase64String(c.SourceBytesBase64)));
}
