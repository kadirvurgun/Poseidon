using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class TodayMaintenancePolicyTests
{
    [Theory]
    [InlineData("Due", "MaintenanceDue", "Attention")]
    [InlineData("ReviewRequired", "MaintenanceReviewRequired", "Review")]
    [InlineData("Unknown", "MaintenanceReviewRequired", "Review")]
    public void Draft_exceptions_keep_unknown_separate_from_not_due(string state, string reason, string priority)
    {
        var result = TodayMaintenancePolicy.Observe(new(state, "SyntheticUnverified"));
        Assert.NotNull(result); Assert.Equal(reason, result.Reason); Assert.Equal(priority, result.Priority);
    }

    [Fact]
    public void Not_due_is_not_an_exception_or_an_operational_ready_assertion() =>
        Assert.Null(TodayMaintenancePolicy.Observe(new("NotDue", "CalendarBoundary")));
}
