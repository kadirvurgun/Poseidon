using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class CrewJobCoveragePolicyTests
{
    private static readonly Guid First = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Person = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public void Duplicate_assignments_of_one_person_do_not_supply_two_required_people()
    {
        var result = CrewJobCoveragePolicy.Observe([new(First, 2, "FICTIONAL/msmd/role")], [new(First, Person, true), new(First, Person, true)]);
        Assert.Equal("Blocked", result.State); var role = Assert.Single(result.Roles);
        Assert.Equal(1, role.Declared); Assert.Equal(1, role.Qualified); Assert.Equal(0, role.Unknown);
    }

    [Fact]
    public void Unknown_and_conflicting_person_qualifications_stay_review_required()
    {
        foreach (var values in new bool?[][] { [null], [true, false], [true, null] }) {
            var result = CrewJobCoveragePolicy.Observe([new(First, 1, "FICTIONAL/msmd/role")], values.Select(v => new ManningCandidate(First, Person, v)).ToArray());
            Assert.Equal("ReviewRequired", result.State); Assert.Equal(1, Assert.Single(result.Roles).Unknown);
        }
    }

    [Fact]
    public void One_person_is_never_automatically_allocated_to_two_simultaneous_required_roles()
    {
        var result = CrewJobCoveragePolicy.Observe([new(First, 1, "FICTIONAL/first"), new(Second, 1, "FICTIONAL/second")],
            [new(First, Person, true), new(Second, Person, true)]);
        Assert.Equal("ReviewRequired", result.State); Assert.Equal("CrossRoleAllocationReviewRequired", result.Reason);
    }

    [Fact]
    public void Source_qualified_minimum_counts_are_distinct_from_full_operational_readiness()
    {
        var result = CrewJobCoveragePolicy.Observe([new(First, 1, "FICTIONAL/first"), new(Second, 1, "FICTIONAL/second")],
            [new(First, Person, true), new(Second, Guid.NewGuid(), true)]);
        Assert.Equal("CurrentMinimumRoleCoverage", result.State); Assert.Equal("SourceQualifiedMinimumRolesOnly", result.Reason);
        Assert.All(result.Roles, r => Assert.Equal("Current", r.State));
    }

    [Theory]
    [InlineData(true, "CurrentMinimumRoleCoverage")]
    [InlineData(false, "Blocked")]
    [InlineData(null, "ReviewRequired")]
    public void False_and_unknown_remain_separate_under_the_issued_role_count(bool? qualified, string state) =>
        Assert.Equal(state, CrewJobCoveragePolicy.Observe([new(First, 1, "FICTIONAL/role")], [new(First, Person, qualified)]).State);

    [Fact]
    public void Missing_or_duplicate_source_role_profiles_cannot_yield_coverage()
    {
        Assert.Equal("SourceRoleProfileInvalid", CrewJobCoveragePolicy.Observe([], []).Reason);
        Assert.Equal("SourceRoleProfileInvalid", CrewJobCoveragePolicy.Observe([new(First, 1, "x"), new(First, 2, "y")], []).Reason);
    }
}
