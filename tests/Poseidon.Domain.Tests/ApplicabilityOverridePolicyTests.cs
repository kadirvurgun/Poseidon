using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-040 / 18.01 Backlog row 41 + 60.03 §10 (paragraph:154-166), OVERRIDE_AUTHORITY (575-580), REG-033..035.
[Trait("Requirement", "DEV-040")]
public sealed class ApplicabilityOverridePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AuthorId = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly Guid ApproverId = Guid.Parse("20000000-0000-4000-8000-000000000002");
    private const string Rationale = "FICTIONAL rationale long enough for review";
    private static OverrideProposal Proposal(string action = "REQUIRE_ADDITIONAL_CONTROL", string reason = "ADDITIONAL_PRECAUTION", Guid[]? evidence = null,
        DateTimeOffset? from = null, DateTimeOffset? expires = null, DateTimeOffset? review = null) =>
        new(action, reason, "COMPLIANCE_REVIEW", Rationale, evidence ?? [], from ?? Now, expires ?? Now.AddDays(30), review ?? Now.AddDays(7));
    private static OverrideAuthorityGrant Grant(Guid? person = null, string[]? actions = null, long duration = 31536000, DateTimeOffset? until = null) =>
        new(Guid.NewGuid(), person ?? ApproverId, actions ?? ["ACCEPT_INTERPRETATION", "REQUIRE_ADDITIONAL_CONTROL"], duration, duration, Now.AddDays(-1), until ?? Now.AddYears(2));
    private static string? Approve(OverrideAuthorityGrant? grant, Guid? actor = null, bool approve = true, bool human = true, bool bound = true, bool overlap = false,
        string state = "IN_REVIEW", DateTimeOffset? now = null) =>
        ApplicabilityOverridePolicy.Decide(state, "APPROVE", actor ?? ApproverId, AuthorId, human, approve, false, grant, "REQUIRE_ADDITIONAL_CONTROL",
            Now, Now.AddDays(30), Now.AddDays(7), bound, overlap, now ?? Now);

    [Fact]
    public void Controlled_reason_catalog_is_closed_per_action() {
        Assert.Null(ApplicabilityOverridePolicy.ValidateProposal(Proposal(), "APPLICABLE", true, Now));
        Assert.Equal("override_reason_not_allowed", ApplicabilityOverridePolicy.ValidateProposal(Proposal(reason: "VERIFIED_LEGAL_INTERPRETATION"), "APPLICABLE", true, Now));
        Assert.Equal("override_reason_not_allowed", ApplicabilityOverridePolicy.ValidateProposal(Proposal(reason: "ADMIN_SAYS_SO"), "APPLICABLE", true, Now));
        Assert.Equal("invalid_request", ApplicabilityOverridePolicy.ValidateProposal(Proposal(action: "FORCE_NOT_APPLICABLE"), "APPLICABLE", true, Now));
        Assert.Equal("invalid_request", ApplicabilityOverridePolicy.ValidateProposal(Proposal() with { Rationale = "too short" }, "APPLICABLE", true, Now));
        Assert.Equal("invalid_request", ApplicabilityOverridePolicy.ValidateProposal(Proposal() with { Purpose = "OPERATION_RELEASE" }, "APPLICABLE", true, Now));
        Assert.All(ApplicabilityOverridePolicy.ReasonCodes, code => Assert.Matches("^[A-Z_]+$", code));
    }

    [Theory]
    [InlineData("REVIEW_REQUIRED", true)]
    [InlineData("UNKNOWN", false)]
    [InlineData("APPLICABLE", false)]
    public void Interpretation_never_turns_unknown_or_unauthoritative_results_into_a_decision(string result, bool authoritative) {
        var p = Proposal("ACCEPT_INTERPRETATION", "VERIFIED_LEGAL_INTERPRETATION", [Guid.NewGuid()]);
        Assert.Equal("override_unknown_not_acceptable", ApplicabilityOverridePolicy.ValidateProposal(p, result, authoritative, Now));
        // A restriction may still be proposed for an uncertain result: it adds a control, never a permission.
        Assert.Null(ApplicabilityOverridePolicy.ValidateProposal(Proposal(), result, authoritative, Now));
    }

    [Fact]
    public void Interpretation_requires_a_cited_reference_record() {
        Assert.Equal("override_evidence_required", ApplicabilityOverridePolicy.ValidateProposal(Proposal("ACCEPT_INTERPRETATION", "FLAG_STATE_EXEMPTION_REFERENCE"), "NOT_APPLICABLE", true, Now));
        Assert.Null(ApplicabilityOverridePolicy.ValidateProposal(Proposal("ACCEPT_INTERPRETATION", "FLAG_STATE_EXEMPTION_REFERENCE", [Guid.NewGuid()]), "NOT_APPLICABLE", true, Now));
        var duplicate = Guid.NewGuid();
        Assert.Equal("invalid_request", ApplicabilityOverridePolicy.ValidateProposal(Proposal(evidence: [duplicate, duplicate]), "APPLICABLE", true, Now));
    }

    [Fact]
    public void Validity_is_bounded_with_mandatory_review_and_no_open_ended_or_default_duration() {
        Assert.Equal("override_validity_invalid", ApplicabilityOverridePolicy.ValidateProposal(Proposal(review: Now.AddDays(31)), "APPLICABLE", true, Now));
        Assert.Equal("override_validity_invalid", ApplicabilityOverridePolicy.ValidateProposal(Proposal(expires: Now), "APPLICABLE", true, Now));
        Assert.Equal("override_validity_invalid", ApplicabilityOverridePolicy.ValidateProposal(Proposal(expires: Now.AddDays(400), review: Now.AddDays(10)), "APPLICABLE", true, Now));
        Assert.Equal("override_validity_invalid", ApplicabilityOverridePolicy.ValidateProposal(Proposal(from: Now.AddDays(-2)), "APPLICABLE", true, Now));
        Assert.Equal("override_validity_invalid", ApplicabilityOverridePolicy.ValidateProposal(Proposal(review: Now), "APPLICABLE", true, Now));
    }

    [Fact]
    public void Approval_without_a_current_authority_grant_is_unavailable_even_for_an_approver() {
        // REG-034: no grant -> 422. This is the production path: the server resolver supplies no grant.
        Assert.Equal("override_authority_unavailable", Approve(null));
        Assert.Equal("override_authority_unavailable", Approve(Grant(person: Guid.NewGuid())));
        Assert.Equal("override_authority_unavailable", Approve(Grant(actions: ["ACCEPT_INTERPRETATION"])));
        Assert.Equal("override_authority_unavailable", Approve(Grant(duration: 86400)));
        Assert.Equal("override_authority_unavailable", Approve(Grant(until: Now.AddDays(10))));
    }

    [Fact]
    public void Maker_checker_human_permission_binding_and_overlap_gates_hold_with_a_grant() {
        Assert.Equal("permission_denied", Approve(Grant(), approve: false));
        Assert.Equal("separation_of_duties", Approve(Grant(person: AuthorId), actor: AuthorId));
        Assert.Equal("human_actor_required", Approve(Grant(), human: false));
        Assert.Equal("evaluation_stale", Approve(Grant(), bound: false));
        Assert.Equal("override_overlap", Approve(Grant(), overlap: true));
        Assert.Equal("override_validity_invalid", Approve(Grant(), now: Now.AddDays(7)));
        Assert.Equal("invalid_transition", Approve(Grant(), state: "REVOKED"));
        Assert.Null(Approve(Grant()));
        Assert.Equal("ACTIVE", ApplicabilityOverridePolicy.Next("APPROVE"));
    }

    [Fact]
    public void Withdraw_reject_and_revoke_follow_the_closed_state_machine() {
        string? D(string state, string decision, Guid actor, bool approve = false, bool revoke = false) =>
            ApplicabilityOverridePolicy.Decide(state, decision, actor, AuthorId, true, approve, revoke, null, "REQUIRE_ADDITIONAL_CONTROL", Now, Now.AddDays(30), Now.AddDays(7), true, false, Now);
        Assert.Null(D("IN_REVIEW", "WITHDRAW", AuthorId));
        Assert.Equal("override_author_required", D("IN_REVIEW", "WITHDRAW", ApproverId));
        Assert.Equal("invalid_transition", D("REVOKED", "WITHDRAW", AuthorId));
        Assert.Null(D("IN_REVIEW", "REJECT", ApproverId, approve: true));
        Assert.Equal("separation_of_duties", D("IN_REVIEW", "REJECT", AuthorId, approve: true));
        Assert.Equal("permission_denied", D("IN_REVIEW", "REJECT", ApproverId));
        Assert.Null(D("ACTIVE", "REVOKE", ApproverId, revoke: true));
        Assert.Equal("permission_denied", D("ACTIVE", "REVOKE", ApproverId));
        Assert.Equal("invalid_transition", D("IN_REVIEW", "REVOKE", ApproverId, revoke: true));
        Assert.All(new[] { "WITHDRAW", "REJECT", "REVOKE" }, d => Assert.Equal("REVOKED", ApplicabilityOverridePolicy.Next(d)));
    }

    [Fact]
    public void Read_time_validity_expires_at_review_or_expiry_and_requires_review_after_reevaluation() {
        DateTimeOffset from = Now, expires = Now.AddDays(30), review = Now.AddDays(7);
        Assert.Equal("CURRENT", ApplicabilityOverridePolicy.CurrentValidity("ACTIVE", from, expires, review, true, Now));
        // REG-033: equal to reviewAt is already invalid even if no worker ran.
        Assert.Equal("EXPIRED", ApplicabilityOverridePolicy.CurrentValidity("ACTIVE", from, expires, review, true, review));
        // Rule revision / input / evaluation change -> REQUIRES_REVIEW, new use blocked.
        Assert.Equal("REQUIRES_REVIEW", ApplicabilityOverridePolicy.CurrentValidity("ACTIVE", from, expires, review, false, Now));
        Assert.Equal("REQUIRES_REVIEW", ApplicabilityOverridePolicy.CurrentValidity("IN_REVIEW", from, expires, review, false, Now));
        Assert.Equal("NOT_ACTIVE", ApplicabilityOverridePolicy.CurrentValidity("IN_REVIEW", from, expires, review, true, Now));
        Assert.Equal("NOT_YET_VALID", ApplicabilityOverridePolicy.CurrentValidity("ACTIVE", Now.AddDays(1), expires, review, true, Now));
        Assert.Equal("REVOKED", ApplicabilityOverridePolicy.CurrentValidity("REVOKED", from, expires, review, true, Now));
    }

    [Fact]
    public void Overlap_is_half_open_interval_intersection() {
        Assert.True(ApplicabilityOverridePolicy.Overlaps(Now, Now.AddDays(2), Now.AddDays(1), Now.AddDays(3)));
        Assert.False(ApplicabilityOverridePolicy.Overlaps(Now, Now.AddDays(1), Now.AddDays(1), Now.AddDays(2)));
    }
}
