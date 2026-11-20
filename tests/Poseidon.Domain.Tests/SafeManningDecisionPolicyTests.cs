using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-050 acceptance rule: SOLAS V/14, IMO A.1047(27), STCW Art. VIII. The system records and
// links human-supplied dispensations/acknowledgements; it never verifies or waives them.
[Trait("Requirement", "DEV-050")]
public sealed class SafeManningDecisionPolicyTests
{
    private static readonly Guid Vessel = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid Officer = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid Assignment = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private static readonly Guid Other = Guid.Parse("50000000-0000-0000-0000-000000000004");
    private static readonly Guid Evidence = Guid.Parse("50000000-0000-0000-0000-000000000005");
    private static readonly CrewEligibilityWindow Window = new(new(2026, 12, 1), new(2026, 12, 10));

    private static CrewEligibilityResult Member(Guid assignment, string state, params CrewEligibilityReason[] reasons) =>
        new(assignment, Officer, state, reasons, [], CrewEligibilityPolicy.Qualifications);
    private static SafeManningEvaluation Evaluation(string state, CrewEligibilityReason[] reasons, params CrewEligibilityResult[] members) =>
        new(Vessel, state, reasons, SafeManningPolicy.MsmdCurrent, null, null, [], members, Window, DateTimeOffset.UnixEpoch, new string('a', 64), []);
    private static readonly CrewEligibilityReason WorkRest = new("WORK_REST_SOURCE_UNAVAILABLE", "WORK_REST", CrewEligibilityStates.ReviewRequired);
    private static readonly CrewEligibilityReason Expired = new("CREDENTIAL_EXPIRED", "CREDENTIAL", CrewEligibilityStates.NotEligible);
    private static ManningDispensation Dispensation(string post = "OFFICER", bool below = true, bool force = false, Guid? assignment = null, DateOnly? to = null) =>
        new("EVIDENCE", Evidence, 1, assignment ?? Assignment, Officer, post, Window.From, to ?? Window.To, below, force);
    private static string? Refusal(SafeManningEvaluation e, string decision, ManningReviewAcknowledgement[]? acks = null, ManningDispensation[]? dispensations = null, string? positionText = "FICTIONAL 2ND OFFICER") =>
        SafeManningDecisionPolicy.Refusal(e, decision, acks ?? [], dispensations ?? [], _ => positionText);
    private const string Accept = SafeManningDecisionPolicy.Accepted;

    [Fact]
    public void Not_eligible_acceptance_without_dispensation_is_refused()
    {
        var e = Evaluation(CrewEligibilityStates.NotEligible, [], Member(Assignment, CrewEligibilityStates.NotEligible, Expired));
        Assert.Equal(SafeManningDecisionPolicy.DispensationRequired, Refusal(e, Accept));
    }

    [Fact]
    public void Not_eligible_acceptance_with_covering_dispensation_is_recordable()
    {
        var e = Evaluation(CrewEligibilityStates.NotEligible, [], Member(Assignment, CrewEligibilityStates.NotEligible, Expired));
        Assert.Null(Refusal(e, Accept, dispensations: [Dispensation()]));
    }

    [Theory]
    [InlineData("MASTER", "FICTIONAL 2ND OFFICER")]
    [InlineData("OFFICER", "FICTIONAL MASTER")]
    [InlineData("OTHER", "FICTIONAL CHIEF ENGINEER")]
    public void Master_or_chief_engineer_dispensation_needs_force_majeure(string post, string position)
    {
        var e = Evaluation(CrewEligibilityStates.NotEligible, [], Member(Assignment, CrewEligibilityStates.NotEligible, Expired));
        Assert.Equal(SafeManningDecisionPolicy.DispensationRequired, Refusal(e, Accept, dispensations: [Dispensation(post)], positionText: position));
        Assert.Null(Refusal(e, Accept, dispensations: [Dispensation(post, force: true)], positionText: position));
    }

    [Fact]
    public void Dispensation_must_be_for_post_below_cover_window_and_every_negative()
    {
        var e = Evaluation(CrewEligibilityStates.NotEligible, [], Member(Assignment, CrewEligibilityStates.NotEligible, Expired),
            Member(Other, CrewEligibilityStates.NotEligible, Expired));
        Assert.Equal(SafeManningDecisionPolicy.DispensationRequired, Refusal(e, Accept, dispensations: [Dispensation()]));
        Assert.Equal(SafeManningDecisionPolicy.DispensationRequired, Refusal(e, Accept, dispensations: [Dispensation(below: false), Dispensation(assignment: Other)]));
        Assert.Equal(SafeManningDecisionPolicy.DispensationRequired, Refusal(e, Accept, dispensations: [Dispensation(to: new(2026, 12, 9)), Dispensation(assignment: Other)]));
        Assert.Null(Refusal(e, Accept, dispensations: [Dispensation(), Dispensation(assignment: Other)]));
    }

    [Fact]
    public void Msmd_count_shortfall_is_covered_only_when_dispensed_members_complete_it()
    {
        var shortfall = new CrewEligibilityReason("MSMD_ROLE_COUNT_NOT_MET", "MSMD", CrewEligibilityStates.NotEligible);
        var member = Member(Assignment, CrewEligibilityStates.NotEligible, Expired);
        var two = Evaluation(CrewEligibilityStates.NotEligible, [shortfall], member) with { Roles = [new(Officer, 2, 1, 0, 0, "Blocked", "x")] };
        Assert.Equal(SafeManningDecisionPolicy.DispensationRequired, Refusal(two, Accept, dispensations: [Dispensation()]));
        var one = two with { Roles = [new(Officer, 1, 1, 0, 0, "Blocked", "x")] };
        Assert.Null(Refusal(one, Accept, dispensations: [Dispensation()]));
    }

    [Fact]
    public void Member_not_eligible_vessel_reason_is_covered_only_by_the_member_dispensation()
    {
        var reason = new CrewEligibilityReason(SafeManningPolicy.MemberNotEligible, "CREW", CrewEligibilityStates.NotEligible);
        var e = Evaluation(CrewEligibilityStates.NotEligible, [reason], Member(Assignment, CrewEligibilityStates.NotEligible, Expired));
        Assert.Equal(SafeManningDecisionPolicy.DispensationRequired, Refusal(e, Accept));
        Assert.Null(Refusal(e, Accept, dispensations: [Dispensation()]));
    }

    // STCW Art. VIII: a dispensation covers certificate gaps only.
    private static CrewEligibilityResult Gapped(string type, string reason, string dimension)
    {
        var requirement = Guid.NewGuid();
        return new(Assignment, Officer, CrewEligibilityStates.NotEligible, [new(reason, dimension, CrewEligibilityStates.NotEligible, requirement)],
            [new(requirement, type, true, CrewGapStates.Gap, reason, null)], CrewEligibilityPolicy.Qualifications);
    }

    [Fact]
    public void Dispensation_covers_a_credential_only_certificate_gap()
    {
        var e = Evaluation(CrewEligibilityStates.NotEligible, [], Gapped(CrewRequirementTypes.Certificate, "CREDENTIAL_MISSING", "CREDENTIAL"));
        Assert.Null(Refusal(e, Accept, dispensations: [Dispensation()]));
        var endorsement = Evaluation(CrewEligibilityStates.NotEligible, [], Gapped(CrewRequirementTypes.Endorsement, "ENDORSEMENT_EXPIRED_OR_EXPIRES_WITHIN_WINDOW", "CREDENTIAL"));
        Assert.Null(Refusal(endorsement, Accept, dispensations: [Dispensation()]));
    }

    [Fact]
    public void Dispensation_never_covers_work_rest_non_compliance()
    {
        var e = Evaluation(CrewEligibilityStates.NotEligible, [], Member(Assignment, CrewEligibilityStates.NotEligible, Expired,
            new CrewEligibilityReason("WORK_REST_NON_COMPLIANT", "WORK_REST", CrewEligibilityStates.NotEligible)));
        Assert.Equal(SafeManningDecisionPolicy.DispensationNotApplicable, Refusal(e, Accept, dispensations: [Dispensation()]));
    }

    [Theory]
    [InlineData(CrewRequirementTypes.Competence, "HUMAN_SIGN_OFF_NEGATIVE", "COMPETENCE")]
    [InlineData(CrewRequirementTypes.Competence, "COMPETENCE_RECORD_MISSING", "COMPETENCE")]
    [InlineData(CrewRequirementTypes.Familiarization, "VALIDITY_EXPIRED_OR_EXPIRES_WITHIN_WINDOW", "FAMILIARIZATION")]
    [InlineData(CrewRequirementTypes.Medical, "CREDENTIAL_EXPIRED", "CREDENTIAL")]
    public void Dispensation_never_covers_competence_familiarization_or_medical_negatives(string type, string reason, string dimension)
    {
        var e = Evaluation(CrewEligibilityStates.NotEligible, [], Gapped(type, reason, dimension));
        Assert.Equal(SafeManningDecisionPolicy.DispensationNotApplicable, Refusal(e, Accept, dispensations: [Dispensation()]));
    }

    [Fact]
    public void Review_required_acceptance_needs_one_acknowledgement_per_reason()
    {
        var other = new CrewEligibilityReason("CREDENTIAL_UNVERIFIED", "CREDENTIAL", CrewEligibilityStates.ReviewRequired);
        var e = Evaluation(CrewEligibilityStates.ReviewRequired, [], Member(Assignment, CrewEligibilityStates.ReviewRequired, WorkRest, other));
        Assert.Equal(SafeManningDecisionPolicy.AcknowledgementRequired, Refusal(e, Accept));
        Assert.Equal(SafeManningDecisionPolicy.AcknowledgementRequired, Refusal(e, Accept, [new("WORK_REST_SOURCE_UNAVAILABLE", "Verified onboard: FICTIONAL rest log")]));
        Assert.Equal(SafeManningDecisionPolicy.AcknowledgementRequired, Refusal(e, Accept, [new("ALL", "all reviewed")]));
        Assert.Equal(SafeManningDecisionPolicy.AcknowledgementRequired, Refusal(e, Accept,
            [new("WORK_REST_SOURCE_UNAVAILABLE", "ok"), new("CREDENTIAL_UNVERIFIED", " ")]));
        Assert.Null(Refusal(e, Accept, [new("WORK_REST_SOURCE_UNAVAILABLE", "Verified onboard: FICTIONAL rest log"),
            new("CREDENTIAL_UNVERIFIED", "Verified onboard: FICTIONAL original sighted")]));
    }

    [Fact]
    public void Not_eligible_with_open_review_reasons_also_needs_acknowledgements()
    {
        var e = Evaluation(CrewEligibilityStates.NotEligible, [], Member(Assignment, CrewEligibilityStates.NotEligible, Expired, WorkRest));
        Assert.Equal(SafeManningDecisionPolicy.AcknowledgementRequired, Refusal(e, Accept, dispensations: [Dispensation()]));
        Assert.Null(Refusal(e, Accept, [new("WORK_REST_SOURCE_UNAVAILABLE", "Verified onboard")], [Dispensation()]));
    }

    [Theory]
    [InlineData(SafeManningDecisionPolicy.NotAccepted)]
    [InlineData(SafeManningDecisionPolicy.Hold)]
    public void Not_accepted_or_hold_is_always_recordable(string decision)
    {
        Assert.Null(Refusal(Evaluation(CrewEligibilityStates.NotEligible, [], Member(Assignment, CrewEligibilityStates.NotEligible, Expired)), decision));
        Assert.Null(Refusal(Evaluation(CrewEligibilityStates.ReviewRequired, [WorkRest]), decision));
    }

    [Fact]
    public void Eligible_or_attention_acceptance_is_unchanged_and_rejects_stray_inputs()
    {
        Assert.Null(Refusal(Evaluation(CrewEligibilityStates.Eligible, []), Accept));
        Assert.Null(Refusal(Evaluation(CrewEligibilityStates.Attention, []), Accept));
        Assert.Equal("validation_failed", Refusal(Evaluation(CrewEligibilityStates.Eligible, []), Accept, dispensations: [Dispensation()]));
    }
}
