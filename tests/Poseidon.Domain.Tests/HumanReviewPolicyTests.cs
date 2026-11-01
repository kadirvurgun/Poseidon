using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class HumanReviewPolicyTests
{
    [Theory]
    [InlineData(null, "VERIFIED", "HUMAN_VERIFIED", "pending_required")]
    [InlineData(null, "PENDING", "REQUEST_REVIEW", null)]
    [InlineData("PENDING", "VERIFIED", "HUMAN_VERIFIED", null)]
    [InlineData("PENDING", "REJECTED", "HUMAN_REJECTED", null)]
    [InlineData("VERIFIED", "REJECTED", "HUMAN_REJECTED", null)]
    [InlineData("REJECTED", "VERIFIED", "HUMAN_VERIFIED", "terminal_review")]
    [InlineData("EXPIRED", "PENDING", "REQUEST_REVIEW", "terminal_review")]
    [InlineData("SUPERSEDED", "VERIFIED", "HUMAN_VERIFIED", "terminal_review")]
    [InlineData("PENDING", "Ready", "HUMAN_VERIFIED", "invalid_state")]
    public void Controlled_transitions_preserve_terminal_lineage(string? prior, string next, string reason, string? error) =>
        Assert.Equal(error, HumanReviewPolicy.Transition("Verification", prior, next, reason, true, true, true, false, false));

    [Theory]
    [InlineData(false, true, true, "separation_of_duties")]
    [InlineData(true, false, true, "input_review_required")]
    [InlineData(true, true, false, "freshness_review_required")]
    public void Verification_needs_human_independence_current_inputs_and_freshness(bool independent, bool current, bool fresh, string error) =>
        Assert.Equal(error, HumanReviewPolicy.Transition("Verification", "PENDING", "VERIFIED", "HUMAN_VERIFIED", independent, current, fresh, false, false));

    [Fact]
    public void Expiry_and_supersession_need_observable_proof_and_never_rewrite_recorded_state()
    {
        Assert.Equal("expiry_not_due", HumanReviewPolicy.Transition("Verification", "VERIFIED", "EXPIRED", "VALIDITY_ELAPSED", true, true, true, false, false));
        Assert.Null(HumanReviewPolicy.Transition("Verification", "VERIFIED", "EXPIRED", "VALIDITY_ELAPSED", true, true, false, true, false));
        Assert.Equal("supersession_not_proven", HumanReviewPolicy.Transition("Verification", "PENDING", "SUPERSEDED", "INPUT_SUPERSEDED", true, true, false, false, false));
        Assert.Null(HumanReviewPolicy.Transition("Verification", "PENDING", "SUPERSEDED", "INPUT_SUPERSEDED", true, false, false, false, true));
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("VERIFIED", HumanReviewPolicy.Current("VERIFIED", now.AddMinutes(1), false, now));
        Assert.Equal("EXPIRED_REVIEW_REQUIRED", HumanReviewPolicy.Current("VERIFIED", now, false, now));
        Assert.Equal("SUPERSEDED_REVIEW_REQUIRED", HumanReviewPolicy.Current("VERIFIED", now.AddDays(1), true, now));
        Assert.Equal("PENDING", HumanReviewPolicy.Current("PENDING", null, false, now));
    }
    [Theory]
    [InlineData("APPROVED")]
    [InlineData("RELEASED")]
    [InlineData("READY")]
    [InlineData("Effective")]
    public void No_evidence_or_administrator_label_fabricates_final_signature(string outcome) =>
        Assert.Equal("source_admission_and_signature_unavailable", HumanReviewPolicy.Transition("Decision", "PENDING", outcome, "HUMAN_VERIFIED", true, true, true, false, false));
    [Fact]
    public void Submission_capability_cannot_create_a_decided_outcome_directly() =>
        Assert.Equal("pending_required", HumanReviewPolicy.Transition("Decision", null, "REJECTED", "HUMAN_REJECTED", true, true, true, false, false));
    [Theory]
    [InlineData("")]
    [InlineData(" reason")]
    [InlineData("reason\nforged audit")]
    public void Rationale_rejects_empty_or_log_injection_text(string reason) => Assert.Throws<ArgumentException>(() => HumanReviewPolicy.Reason(reason));
}
