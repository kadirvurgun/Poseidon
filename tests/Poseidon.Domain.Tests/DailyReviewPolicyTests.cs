using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class DailyReviewPolicyTests
{
    [Fact]
    public void Source_workflow_review_labels_never_include_verification_submission_or_permit_states() {
        Assert.Equal(["REVIEWED"],DailyReviewPolicy.Decisions("Noon"));
        Assert.Equal(["REVIEWED","RETURNED"],DailyReviewPolicy.Decisions("Engine"));
        Assert.False(DailyReviewPolicy.Valid("Noon","RETURNED","RETURNED_FOR_CORRECTION","FICTIONAL"));
        foreach(var state in new[]{"VERIFIED","SUBMITTED","APPROVED","READY"})
            Assert.False(DailyReviewPolicy.Valid("Deck",state,"REVIEW_RECORDED","FICTIONAL"));
    }
    [Fact]
    public void Review_recording_requires_its_source_role_reason_and_bounded_human_rationale() {
        Assert.True(DailyReviewPolicy.Duty("Noon","Master"));Assert.False(DailyReviewPolicy.Duty("Noon","Administrator"));
        Assert.False(DailyReviewPolicy.Duty("Engine","Master"));Assert.True(DailyReviewPolicy.Duty("Engine","ChiefEngineer"));
        Assert.True(DailyReviewPolicy.Valid("Deck","RETURNED","RETURNED_FOR_CORRECTION","FICTIONAL\nmissing handover"));
        Assert.False(DailyReviewPolicy.Valid("Deck","RETURNED","REVIEW_RECORDED","FICTIONAL"));
        Assert.False(DailyReviewPolicy.Valid("Deck","REVIEWED","REVIEW_RECORDED"," "));
        Assert.False(DailyReviewPolicy.Valid("Deck","REVIEWED","REVIEW_RECORDED","hidden\0value"));
    }
}
