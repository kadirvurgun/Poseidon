using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class TodayTugPolicyTests
{
    [Theory]
    [InlineData(false, false, true, false, "Unknown", "TugPlanMissing", "Review")]
    [InlineData(true, true, true, true, "HumanReviewRequired", "TugConflictReviewRequired", "Attention")]
    [InlineData(true, true, true, true, "Unknown", "TugReadinessBlocked", "Attention")]
    [InlineData(true, true, true, false, "Unknown", "TugRequestChanged", "Review")]
    [InlineData(true, false, true, false, "BoundaryReviewRequired", "TugBoundaryReviewRequired", "Review")]
    [InlineData(true, false, true, false, "Unknown", "TugRequestIncomplete", "Review")]
    [InlineData(true, false, false, false, "Unknown", "TugPlanningReviewRequired", "Review")]
    [InlineData(true, false, false, false, "Separate", "TugPlanningReviewRequired", "Review")]
    public void Current_exceptions_have_deterministic_priority_without_creating_operational_clearance(bool plan,
        bool changed, bool incomplete, bool blocked, string conflict, string reason, string priority)
    {
        var result = TodayTugPolicy.Observe(new(plan, changed, incomplete, blocked, conflict));
        Assert.Equal(reason, result.Reason);
        Assert.Equal(priority, result.Priority);
    }
}
