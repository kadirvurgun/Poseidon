using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// These are declarations. Neither a text policy nor a clean transport is admission,
// business completion, human verification or operational effectiveness.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlEvidenceContent(string? Title = null, Guid? OwnerId = null, Guid? OwnerRole = null,
    string? Frequency = null, string? EvidencePolicy = null, string? VerificationPolicy = null, bool? Critical = null,
    string? Provenance = null, DateTimeOffset? ObservedAt = null, DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidUntil = null, DateTimeOffset? FreshUntil = null, SourceAcknowledgment[]? Acknowledgments = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceAcknowledgment(Guid RuleId, string QualificationHash, string SourceHash);

public static class ControlEvidencePolicy
{
    public static void Validate(string kind, ControlEvidenceContent c, DateTimeOffset now)
    {
        if (c.Acknowledgments is { } acknowledgments && (acknowledgments.Length > 32 || acknowledgments.Any(x => x == null || x.RuleId == Guid.Empty ||
            !System.Text.RegularExpressions.Regex.IsMatch(x.QualificationHash ?? "", "\\A[A-F0-9]{64}\\z") ||
            !System.Text.RegularExpressions.Regex.IsMatch(x.SourceHash ?? "", "\\A[A-F0-9]{64}\\z")) || acknowledgments.Select(x => x.RuleId).Distinct().Count() != acknowledgments.Length)) throw new ArgumentException("invalid_acknowledgment");
        if (kind is not ("Control" or "Evidence")) throw new ArgumentException("invalid_kind");
        foreach (var text in new[] { c.Title, c.Frequency, c.EvidencePolicy, c.VerificationPolicy, c.Provenance })
            if (text != null && (text.Length is < 1 or > 2000 || text != text.Trim() || text.Any(char.IsControl))) throw new ArgumentException("invalid_content");
        if ((c.OwnerId == null) != (c.OwnerRole == null) || c.OwnerId == Guid.Empty || c.OwnerRole == Guid.Empty) throw new ArgumentException("invalid_owner");
        if (c.ObservedAt > now || c.ValidFrom != null && c.ValidUntil < c.ValidFrom || c.ObservedAt != null && c.FreshUntil < c.ObservedAt)
            throw new ArgumentException("invalid_chronology");
        if (kind == "Control" && (c.ObservedAt != null || c.ValidFrom != null || c.ValidUntil != null || c.FreshUntil != null)) throw new ArgumentException("invalid_control_dates");
    }
    public static string[] Blockers(string kind, ControlEvidenceContent c, int requirements, bool file, int applicabilityLinks = 0)
    {
        var reasons = new List<string>();
        if (c.Title == null) reasons.Add("TITLE_MISSING");
        if (c.OwnerId == null) reasons.Add("OWNER_MISSING");
        if (requirements == 0) reasons.Add("REQUIREMENT_LINK_MISSING");
        if (requirements > applicabilityLinks) reasons.Add("APPLICABILITY_LINK_MISSING");
        if (kind == "Control") {
            if (c.Frequency == null) reasons.Add("FREQUENCY_UNSPECIFIED");
            if (c.EvidencePolicy == null) reasons.Add("EVIDENCE_POLICY_MISSING");
            if (c.VerificationPolicy == null) reasons.Add("VERIFICATION_POLICY_MISSING");
            if (c.Critical == null) reasons.Add("CRITICALITY_UNKNOWN");
        } else {
            if (!file) reasons.Add("CLEAN_FILE_MISSING");
            if (c.Provenance == null) reasons.Add("PROVENANCE_MISSING");
            if (c.ObservedAt == null) reasons.Add("OBSERVATION_TIME_MISSING");
        }
        reasons.Add("SOURCE_ADMISSION_UNAVAILABLE");
        reasons.Add("VERIFICATION_NOT_PERFORMED");
        return reasons.ToArray();
    }
    // Dates are retained declarations, never an invented qualified freshness policy.
    public static string TimeState(ControlEvidenceContent c, DateTimeOffset asOf, bool superseded) => superseded ? "SUPERSEDED" :
        c.ValidFrom > asOf ? "DECLARED_NOT_YET_VALID" : c.ValidUntil < asOf ? "DECLARED_EXPIRED" :
        c.FreshUntil < asOf ? "DECLARED_STALE" : "UNKNOWN";
}
