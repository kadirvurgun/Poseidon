using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S0 signed-browser cohort isolation; original legacy capture authority stays unknown.
public sealed class LegacyCrewBrowserFixtureTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-fixture-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("CEEA0001-0000-4000-8000-000000000001")]
    [InlineData("{ceea0001-0000-4000-8000-000000000001}")]
    [InlineData("ceea0001000040008000000000000001")]
    [InlineData(" ceea0001-0000-4000-8000-000000000001")]
    public void Cli_fixture_identity_rejects_empty_ambiguous_and_noncanonical_input(string? value) =>
        Assert.Throws<ArgumentException>(() => LegacyCrewBrowserFixture.ParseBatchId(value));

    [Fact]
    public void Canonical_cohort_identity_retains_original_default_and_accepts_a_distinct_owned_uuid()
    {
        Assert.Equal(LegacyCrewBrowserFixture.BatchId,
            LegacyCrewBrowserFixture.ParseBatchId("ceea0001-0000-4000-8000-000000000001"));
        var cohort = Guid.NewGuid();
        Assert.Equal(cohort, LegacyCrewBrowserFixture.ParseBatchId(cohort.ToString("D")));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("test")]
    [InlineData("")]
    public async Task Explicit_fixture_id_never_bypasses_exact_Test_environment_guard(string environment) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LegacyCrewBrowserFixture.AddAsync("not-used-before-environment-guard", environment,
                SeedValidationTests.Demo(), Guid.NewGuid()));

    [Fact]
    public async Task Typed_empty_fixture_id_is_rejected_before_any_database_access() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            LegacyCrewBrowserFixture.AddAsync("not-used-before-identity-guard", "Test",
                SeedValidationTests.Demo(), Guid.Empty));
}
