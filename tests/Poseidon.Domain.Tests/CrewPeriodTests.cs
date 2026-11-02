using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class CrewPeriodTests
{
    [Fact]
    public void Incomplete_draft_does_not_fabricate_a_period()
    {
        Assert.True(CrewPeriod.Valid(null, null));
        Assert.False(CrewPeriod.Valid(null, new(2026, 11, 2)));
        Assert.False(CrewPeriod.Overlaps(null, null, new(2026, 11, 1), null));
    }
    [Fact]
    public void End_is_exclusive_and_chronology_is_strict()
    {
        var first = new DateOnly(2026, 11, 1); var next = first.AddDays(1);
        Assert.False(CrewPeriod.Valid(first, first)); Assert.False(CrewPeriod.Valid(next, first));
        Assert.True(CrewPeriod.Valid(first, next));
        Assert.False(CrewPeriod.Overlaps(first, next, next, null));
        Assert.True(CrewPeriod.Overlaps(first, null, next, null));
        Assert.True(CrewPeriod.Overlaps(first, next.AddDays(1), next, next.AddDays(2)));
    }
    [Theory]
    [InlineData("FullName")]
    [InlineData("EmployeeNo")]
    [InlineData("WatchGroup")]
    [InlineData("Rank")]
    [InlineData("Capacity")]
    public void Crew_text_is_excluded_from_audit_value_snapshots(string field) => Assert.False(AuditValuePolicy.Includes(field));
}
