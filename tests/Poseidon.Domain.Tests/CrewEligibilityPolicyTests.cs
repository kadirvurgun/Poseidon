using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S7 WP-A DEV-048/049/050 (18.01 rows 49-51). UNKNOWN never positive; advisory only.
[Trait("Requirement", "DEV-050")]
public sealed class CrewEligibilityPolicyTests
{
    private static readonly Guid Assignment = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid Position = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid CertReq = Guid.Parse("30000000-0000-0000-0000-000000000010");
    private static readonly Guid CompReq = Guid.Parse("30000000-0000-0000-0000-000000000011");
    private static readonly Guid FamReq = Guid.Parse("30000000-0000-0000-0000-000000000012");
    private static readonly CrewEligibilityWindow Window = new(new(2026, 12, 1), new(2026, 12, 31));
    private static readonly CrewWorkRestFact Rest = new(CrewWorkRestStates.Compliant, "FICTIONAL_SOURCE");

    private static CrewRequirementFact Cert(bool mandatory = true, string? vesselScope = null) =>
        new(CertReq, CrewRequirementTypes.Certificate, "FICTIONAL-COC", mandatory, null, vesselScope, null, null, null);
    private static CrewRequirementFact Comp(int? months = null) => new(CompReq, CrewRequirementTypes.Competence, "FICTIONAL-ECDIS", true, months, null, null, null, null);
    private static CrewRequirementFact Fam() => new(FamReq, CrewRequirementTypes.Familiarization, "FICTIONAL-SHIP-FAM", true, null, null, null, null, null);
    private static CrewCredentialFact Credential(string? assertion = "Verified", bool current = true, DateOnly? expiry = null, bool? endorsement = false) =>
        new(Guid.Parse("30000000-0000-0000-0000-000000000020"), "FICTIONAL-COC", expiry ?? new DateOnly(2028, 1, 1), endorsement, null, assertion, current);
    private static CrewCompetenceFact Record(Guid requirement, string kind, string? signOff = "CONFIRMED", bool current = true, DateOnly? completed = null) =>
        new(Guid.NewGuid(), requirement, kind, completed ?? new DateOnly(2026, 6, 1), null, signOff, current);
    private static CrewEligibilityInputs Inputs(CrewRequirementFact[] requirements, CrewCredentialFact[] credentials, CrewCompetenceFact[] competence,
        CrewWorkRestFact? rest = null, string presence = "OnboardDeclaration", string? vesselType = null) =>
        new(Assignment, Position, presence, Window, new(2026, 11, 1), null, requirements, credentials, competence, rest ?? Rest, vesselType);

    private static CrewEligibilityInputs Complete(CrewWorkRestFact? rest = null) => Inputs([Cert(), Comp(), Fam()], [Credential()],
        [Record(CompReq, CrewCompetenceKinds.CompetenceAssessment), Record(FamReq, CrewCompetenceKinds.Familiarization)], rest);

    [Fact]
    public void Verified_human_backed_inputs_with_known_work_rest_are_eligible_but_advisory_only()
    {
        var result = CrewEligibilityPolicy.Evaluate(Complete());
        Assert.Equal(CrewEligibilityStates.Eligible, result.State);
        Assert.Empty(result.Reasons);
        Assert.Equal(CrewEligibilityPolicy.Authority, result.Authority);
        Assert.Contains("SAFE_MANNING_DECISION_IS_A_HUMAN_RECORD", result.Qualifications);
    }

    [Fact]
    public void Unknown_work_rest_source_is_review_required_never_eligible()
    {
        var result = CrewEligibilityPolicy.Evaluate(Complete(new(CrewWorkRestStates.Unknown, "WORK_REST_SOURCE_UNAVAILABLE")));
        Assert.Equal(CrewEligibilityStates.ReviewRequired, result.State);
        Assert.Contains(result.Reasons, r => r.Code == "WORK_REST_SOURCE_UNAVAILABLE" && r.Dimension == "WORK_REST");
    }

    [Fact]
    public void Non_compliant_work_rest_is_not_eligible()
    {
        Assert.Equal(CrewEligibilityStates.NotEligible, CrewEligibilityPolicy.Evaluate(Complete(new(CrewWorkRestStates.NonCompliant, "X"))).State);
    }

    [Fact]
    public void Unverified_credential_is_review_required_and_expired_is_not_eligible()
    {
        var unverified = CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [Credential(assertion: null)], []));
        Assert.Equal(CrewEligibilityStates.ReviewRequired, unverified.State);
        Assert.Contains(unverified.Reasons, r => r.Code == "CREDENTIAL_UNVERIFIED");
        var expired = CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [Credential(expiry: new(2026, 11, 30))], []));
        Assert.Equal(CrewEligibilityStates.NotEligible, expired.State);
        Assert.Contains(expired.Reasons, r => r.Code == "CREDENTIAL_EXPIRED");
        var within = CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [Credential(expiry: new(2026, 12, 15))], []));
        Assert.Contains(within.Reasons, r => r.Code == "CREDENTIAL_EXPIRES_WITHIN_WINDOW" && r.Severity == CrewEligibilityStates.NotEligible);
    }

    [Theory]
    [InlineData("Not Verified")]
    [InlineData("Suspicious")]
    [InlineData("Suspended / Revoked")]
    public void Negative_human_assertion_is_not_eligible(string state)
    {
        Assert.Equal(CrewEligibilityStates.NotEligible, CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [Credential(assertion: state)], [])).State);
    }

    [Fact]
    public void Stale_assertion_and_unknown_expiry_or_endorsement_are_review_required()
    {
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [Credential(current: false)], [])).Reasons, r => r.Code == "CREDENTIAL_VERIFICATION_NOT_CURRENT");
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [Credential() with { ExpiryDate = null }], [])).Reasons, r => r.Code == "CREDENTIAL_EXPIRY_UNKNOWN");
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [Credential(endorsement: null)], [])).Reasons, r => r.Code == "ENDORSEMENT_REQUIREMENT_UNKNOWN");
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [Credential(endorsement: true)], [])).Reasons, r => r.Code == "ENDORSEMENT_EXPIRY_UNKNOWN");
    }

    [Fact]
    public void Training_completion_never_satisfies_a_competence_requirement()
    {
        var result = CrewEligibilityPolicy.Evaluate(Inputs([Comp()], [], [Record(CompReq, CrewCompetenceKinds.TrainingCompletion)]));
        Assert.Equal(CrewEligibilityStates.NotEligible, result.State);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal(CrewGapStates.Gap, gap.State);
        Assert.Equal("TRAINING_COMPLETION_IS_NOT_COMPETENCE", gap.Reason);
    }

    [Fact]
    public void Missing_or_negative_or_stale_sign_off_is_never_satisfied()
    {
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([Comp()], [], [Record(CompReq, CrewCompetenceKinds.CompetenceAssessment, signOff: null)])).Gaps,
            g => g.Reason == "HUMAN_SIGN_OFF_MISSING" && g.State == CrewGapStates.ReviewRequired);
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([Comp()], [], [Record(CompReq, CrewCompetenceKinds.CompetenceAssessment, signOff: "NOT_CONFIRMED")])).Gaps,
            g => g.Reason == "HUMAN_SIGN_OFF_NEGATIVE" && g.State == CrewGapStates.Gap);
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([Comp()], [], [Record(CompReq, CrewCompetenceKinds.CompetenceAssessment, current: false)])).Gaps,
            g => g.Reason == "HUMAN_SIGN_OFF_NOT_CURRENT");
    }

    [Fact]
    public void Matrix_validity_months_expire_a_completed_record()
    {
        var result = CrewEligibilityPolicy.Evaluate(Inputs([Comp(months: 6)], [], [Record(CompReq, CrewCompetenceKinds.CompetenceAssessment, completed: new(2026, 6, 10))]));
        Assert.Contains(result.Gaps, g => g.Reason == "VALIDITY_EXPIRED_OR_EXPIRES_WITHIN_WINDOW");
    }

    [Fact]
    public void Non_mandatory_gap_is_attention_and_missing_mandatory_is_not_eligible()
    {
        var optional = CrewEligibilityPolicy.Evaluate(Inputs([Cert(mandatory: false)], [], []));
        Assert.Equal(CrewEligibilityStates.Attention, optional.State);
        var mandatory = CrewEligibilityPolicy.Evaluate(Inputs([Cert()], [], []));
        Assert.Equal(CrewEligibilityStates.NotEligible, mandatory.State);
        Assert.Contains(mandatory.Reasons, r => r.Code == "CREDENTIAL_MISSING" && r.RequirementId == CertReq);
    }

    [Fact]
    public void Scoped_requirement_with_unknown_vessel_context_is_review_required_and_other_scope_not_applicable()
    {
        var unknown = CrewEligibilityPolicy.Evaluate(Inputs([Cert(vesselScope: "TANKER")], [Credential()], []));
        Assert.Contains(unknown.Gaps, g => g.Reason == "VESSEL_TYPE_SCOPE_CONTEXT_UNKNOWN" && g.State == CrewGapStates.ReviewRequired);
        var other = CrewEligibilityPolicy.Evaluate(Inputs([Cert(vesselScope: "TANKER"), Comp()], [], [Record(CompReq, CrewCompetenceKinds.CompetenceAssessment)], vesselType: "TUG"));
        Assert.Contains(other.Gaps, g => g.RequirementId == CertReq && g.State == CrewGapStates.NotApplicable);
    }

    [Fact]
    public void Empty_matrix_missing_position_and_draft_presence_are_review_required()
    {
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([], [], [])).Reasons, r => r.Code == "REQUIREMENT_MATRIX_EMPTY_FOR_POSITION");
        Assert.Equal(CrewEligibilityStates.ReviewRequired, CrewEligibilityPolicy.Evaluate(Complete() with { PositionId = null }).State);
        Assert.Equal(CrewEligibilityStates.ReviewRequired, CrewEligibilityPolicy.Evaluate(Complete() with { PresenceState = "Draft" }).State);
    }

    [Fact]
    public void Link_to_absent_requirement_is_review_required()
    {
        var linked = Cert() with { CompetenceLinkId = Guid.Parse("30000000-0000-0000-0000-000000000099") };
        Assert.Contains(CrewEligibilityPolicy.Evaluate(Inputs([linked], [Credential()], [])).Gaps, g => g.Reason == "LINKED_REQUIREMENT_UNAVAILABLE");
    }

    [Fact]
    public void Credential_register_status_never_shows_expired_or_unverified_as_positive()
    {
        var reference = new DateOnly(2026, 12, 1);
        Assert.Equal("VERIFIED_CURRENT", CrewEligibilityPolicy.CredentialStatus(Credential(), reference));
        Assert.Equal("UNVERIFIED", CrewEligibilityPolicy.CredentialStatus(Credential(assertion: null), reference));
        Assert.Equal("EXPIRED", CrewEligibilityPolicy.CredentialStatus(Credential(expiry: reference), reference));
        Assert.Equal("EXPIRY_UNKNOWN", CrewEligibilityPolicy.CredentialStatus(Credential() with { ExpiryDate = null }, reference));
        Assert.Equal("VERIFICATION_NOT_CURRENT", CrewEligibilityPolicy.CredentialStatus(Credential(current: false), reference));
        Assert.Equal("VERIFICATION_NEGATIVE", CrewEligibilityPolicy.CredentialStatus(Credential(assertion: "Suspicious"), reference));
    }

    private static CrewEligibilityResult Member(string state) => new(Guid.NewGuid(), Position, state, [], [], []);

    [Fact]
    public void Safe_manning_without_current_msmd_is_review_required()
    {
        var (state, reasons, roles) = SafeManningPolicy.Evaluate("SOURCE_QUALIFIED_MSMD_REQUIRED", null, [Member(CrewEligibilityStates.Eligible)]);
        Assert.Equal(CrewEligibilityStates.ReviewRequired, state);
        Assert.Contains(reasons, r => r.Code == "MSMD_SOURCE_QUALIFIED_MSMD_REQUIRED");
        Assert.Empty(roles);
    }

    [Fact]
    public void Safe_manning_counts_only_eligible_or_attention_members()
    {
        MsmdRoleRequirement[] msmd = [new(Position, 2, "FICTIONAL/msmd")];
        Assert.Equal(CrewEligibilityStates.Eligible, SafeManningPolicy.Evaluate(SafeManningPolicy.MsmdCurrent, msmd,
            [Member(CrewEligibilityStates.Eligible), Member(CrewEligibilityStates.Eligible)]).State);
        Assert.Equal(CrewEligibilityStates.Attention, SafeManningPolicy.Evaluate(SafeManningPolicy.MsmdCurrent, msmd,
            [Member(CrewEligibilityStates.Eligible), Member(CrewEligibilityStates.Attention)]).State);
        Assert.Equal(CrewEligibilityStates.ReviewRequired, SafeManningPolicy.Evaluate(SafeManningPolicy.MsmdCurrent, msmd,
            [Member(CrewEligibilityStates.Eligible), Member(CrewEligibilityStates.ReviewRequired)]).State);
        var blocked = SafeManningPolicy.Evaluate(SafeManningPolicy.MsmdCurrent, msmd, [Member(CrewEligibilityStates.Eligible), Member(CrewEligibilityStates.NotEligible)]);
        Assert.Equal(CrewEligibilityStates.NotEligible, blocked.State);
        Assert.Contains(blocked.Reasons, r => r.Code == "MSMD_ROLE_COUNT_NOT_MET");
    }

    [Fact]
    public void Safe_manning_with_no_crew_is_review_required()
    {
        Assert.Equal(CrewEligibilityStates.ReviewRequired, SafeManningPolicy.Evaluate("SOURCE_ACCESS_REQUIRED", null, []).State);
    }

    [Theory]
    [InlineData("OnboardDeclaration", "unverified_onboard_declaration", "ONBOARD")]
    [InlineData("PlannedDeclaration", "unverified_planned_declaration", "PLANNED")]
    [InlineData("OutsidePeriod", "period_not_started", "PLANNED")]
    [InlineData("OutsidePeriod", "period_ended", "ENDED")]
    [InlineData("Draft", "incomplete_declaration", "DRAFT")]
    [InlineData("Unknown", "assignment_policy_unavailable", "REVIEW_REQUIRED")]
    [InlineData("Stale", "assignment_policy_changed", "REVIEW_REQUIRED")]
    [Trait("Requirement", "DEV-047")]
    public void Derived_profile_status_projects_declared_presence_only(string state, string reason, string expected)
    {
        Assert.Equal(expected, CrewRosterPresencePolicy.ProfileStatus(new(state, reason)));
    }
}
