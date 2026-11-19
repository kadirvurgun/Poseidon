namespace Poseidon.Domain;

public sealed record ControlCandidateSource(Guid RuleId, long RuleRevision, Guid RequirementId, long RequirementRevision,
    Guid SourceId, long SourceRevision, string? RequirementTitle, bool AlreadyLinked);
public sealed record ControlCandidate(ControlCandidateSource Source, ControlEvidenceContent Content);

// DEV-041 (18.01 Backlog row 42): applicable requirements are linked to controls with owner/frequency/
// evidence/verification fields. Generation only prepares DRAFT control candidates for a current qualified
// APPLICABLE requirement without a linked control. A candidate never invents an owner, frequency,
// evidence rule, verification rule or criticality: those stay null so the existing submission blockers
// (OWNER_MISSING, FREQUENCY_UNSPECIFIED, EVIDENCE_POLICY_MISSING, VERIFICATION_POLICY_MISSING,
// CRITICALITY_UNKNOWN) keep the draft out of review until a human declares them. Generation is never
// publication, approval, completion, verification or effectiveness (62.01 §6 lifecycle stays DRAFT).
public static class ComplianceControlCandidatePolicy
{
    public const string ReasonCode = "REQUIREMENT_CANDIDATE";
    public const int MaxCandidates = 25;
    public static string Title(string? requirementTitle, Guid requirementId, long requirementRevision)
    {
        var text = requirementTitle?.Trim();
        if (string.IsNullOrEmpty(text) || text.Any(char.IsControl)) return $"Control candidate for requirement {requirementId:D} r{requirementRevision}";
        return text.Length > 1900 ? text[..1900].TrimEnd() : text;
    }
    public static ControlCandidate[] Plan(IEnumerable<ControlCandidateSource> applicable) => applicable
        .Where(s => !s.AlreadyLinked && s.RuleRevision > 0 && s.RequirementRevision > 0 && s.SourceRevision > 0)
        .GroupBy(s => (s.RequirementId, s.RequirementRevision)).Select(g => g.OrderBy(s => s.RuleId).First())
        .OrderBy(s => s.RequirementId).ThenBy(s => s.RuleId).Take(MaxCandidates)
        .Select(s => new ControlCandidate(s, new ControlEvidenceContent(Title: Title(s.RequirementTitle, s.RequirementId, s.RequirementRevision))))
        .ToArray();
}
