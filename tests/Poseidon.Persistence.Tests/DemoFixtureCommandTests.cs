using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// USER-GUIDE-01 UG-2 / UG-DOC-03: the screenshot demo fixture command is Development/Test only.
public sealed class DemoFixtureCommandTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("development")]
    [InlineData("test")]
    [InlineData("")]
    public async Task Demo_fixture_command_rejects_every_environment_but_exact_Development_or_Test(string environment)
    {
        Assert.Throws<InvalidOperationException>(() => DemoFixtureCommand.EnsureAllowed(environment));
        // The guard runs before any connection is opened, so an unusable connection string proves ordering.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DemoFixtureCommand.RunAsync("not-used-before-environment-guard", environment, SeedValidationTests.Demo(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public void Demo_fixture_command_allows_only_the_two_synthetic_environments(string environment) =>
        DemoFixtureCommand.EnsureAllowed(environment);
}
