using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class CrewCatalogGovernanceTests
{
    [Fact]
    public void Incomplete_drafts_are_allowed_but_neither_review_nor_adoption_can_default_missing_definition_to_complete()
    {
        Assert.Null(CrewCatalogGovernance.Reject(null, "Draft", false, false, false));
        Assert.Equal("catalog_incomplete", CrewCatalogGovernance.Reject("Draft", "Reviewed", false, true, false));
        Assert.Equal("catalog_incomplete", CrewCatalogGovernance.Reject("Reviewed", "Adopted", false, true, false));
    }
    [Theory]
    [InlineData("Draft", "Adopted")]
    [InlineData(null, "Reviewed")]
    [InlineData("Withdrawn", "Adopted")]
    [InlineData("Adopted", "Reviewed")]
    [InlineData("Reviewed", "ApprovedOperationally")]
    public void Governance_cannot_skip_review_or_create_a_new_authority_state(string? prior, string next) =>
        Assert.Equal("invalid_transition", CrewCatalogGovernance.Reject(prior, next, true, true, false));
    [Fact]
    public void Only_independent_review_and_adoption_progress_and_withdrawal_requires_an_active_catalog()
    {
        Assert.Equal("independent_catalog_authority_required", CrewCatalogGovernance.Reject("Draft", "Reviewed", true, false, false));
        Assert.Equal("independent_catalog_authority_required", CrewCatalogGovernance.Reject("Reviewed", "Adopted", true, false, false));
        Assert.Null(CrewCatalogGovernance.Reject("Draft", "Reviewed", true, true, false));
        Assert.Null(CrewCatalogGovernance.Reject("Reviewed", "Adopted", true, true, false));
        Assert.Null(CrewCatalogGovernance.Reject("Draft", "Withdrawn", false, false, true));
        Assert.Equal("invalid_transition", CrewCatalogGovernance.Reject("Draft", "Withdrawn", true, true, false));
    }
}
