using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-041 / 18.01 Backlog row 42: candidates are DRAFT preparation only; owner/frequency/evidence/verification
// and criticality are never invented, so the existing submission blockers keep them out of review.
[Trait("Requirement", "DEV-041")]
public sealed class ComplianceControlCandidatePolicyTests
{
    private static ControlCandidateSource Source(Guid requirement, bool linked = false, string? title = "FICTIONAL requirement title", Guid? rule = null) =>
        new(rule ?? Guid.NewGuid(), 1, requirement, 2, Guid.NewGuid(), 3, title, linked);

    [Fact]
    public void Candidates_skip_linked_requirements_and_never_declare_owner_frequency_evidence_verification_or_criticality() {
        var open = Source(Guid.NewGuid()); var linked = Source(Guid.NewGuid(), linked: true);
        var candidate = Assert.Single(ComplianceControlCandidatePolicy.Plan([open, linked]));
        Assert.Equal(open, candidate.Source);
        Assert.Equal("FICTIONAL requirement title", candidate.Content.Title);
        Assert.Null(candidate.Content.OwnerId); Assert.Null(candidate.Content.Frequency); Assert.Null(candidate.Content.EvidencePolicy);
        Assert.Null(candidate.Content.VerificationPolicy); Assert.Null(candidate.Content.Critical);
        var blockers = ControlEvidencePolicy.Blockers("Control", candidate.Content, 1, false, 1);
        Assert.Equal(new[] { "OWNER_MISSING", "FREQUENCY_UNSPECIFIED", "EVIDENCE_POLICY_MISSING", "VERIFICATION_POLICY_MISSING", "CRITICALITY_UNKNOWN",
            "SOURCE_ADMISSION_UNAVAILABLE", "VERIFICATION_NOT_PERFORMED" }, blockers);
        ControlEvidencePolicy.Validate("Control", candidate.Content, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void One_candidate_per_requirement_revision_bounded_and_deterministic() {
        var requirement = Guid.NewGuid();
        var a = Source(requirement, rule: Guid.Parse("00000000-0000-4000-8000-000000000002"));
        var b = Source(requirement, rule: Guid.Parse("00000000-0000-4000-8000-000000000001"));
        Assert.Equal(b.RuleId, Assert.Single(ComplianceControlCandidatePolicy.Plan([a, b])).Source.RuleId);
        var many = Enumerable.Range(0, 40).Select(_ => Source(Guid.NewGuid())).ToArray();
        Assert.Equal(ComplianceControlCandidatePolicy.MaxCandidates, ComplianceControlCandidatePolicy.Plan(many).Length);
        Assert.Equal(ComplianceControlCandidatePolicy.Plan(many).Select(c => c.Source.RequirementId), ComplianceControlCandidatePolicy.Plan(many.Reverse()).Select(c => c.Source.RequirementId));
        Assert.Empty(ComplianceControlCandidatePolicy.Plan([Source(Guid.NewGuid(), linked: true)]));
    }

    [Fact]
    public void Missing_or_unsafe_requirement_title_uses_a_neutral_identifier_title() {
        var id = Guid.NewGuid();
        Assert.Equal($"Control candidate for requirement {id:D} r2", ComplianceControlCandidatePolicy.Title(null, id, 2));
        Assert.Equal($"Control candidate for requirement {id:D} r2", ComplianceControlCandidatePolicy.Title("bad\u0007title", id, 2));
        Assert.Equal(1900, ComplianceControlCandidatePolicy.Title(new string('x', 3000), id, 2).Length);
    }
}
