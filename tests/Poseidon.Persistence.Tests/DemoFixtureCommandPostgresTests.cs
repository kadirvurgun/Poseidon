using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

// USER-GUIDE-01 UG-2 / D5: the screenshot demo command is idempotent, audited and seeds no decision.
[Trait("Category", "Integration")]
public sealed class DemoFixtureCommandPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18.6").Build();
    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Demo_fixture_command_requires_seed_is_idempotent_attributed_and_seeds_no_decision_or_grant()
    {
        var ct = TestContext.Current.CancellationToken;
        var seed = SeedValidationTests.Demo();
        var connection = container.GetConnectionString();
        await using (var migrate = new PoseidonDbContext(SeedImporter.Options(connection), Guid.Empty, ActorContext.SeedImport(), administrativeImport: true))
            await migrate.Database.MigrateAsync(ct);
        // Without the synthetic seed the command refuses instead of inventing vessels.
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoFixtureCommand.RunAsync(connection, "Development", seed, ct));
        await using (var setup = new PoseidonDbContext(SeedImporter.Options(connection, seed), Guid.Empty, ActorContext.SeedImport(), administrativeImport: true))
            await setup.Database.MigrateAsync(ct);

        await DemoFixtureCommand.RunAsync(connection, "Development", seed, ct);
        await using var first = new PoseidonDbContext(SeedImporter.Options(connection), Guid.Empty, ActorContext.SeedImport(), administrativeImport: true);
        var audits = await first.AuditEvents.CountAsync(a => a.ActorId == "system:synthetic-demo-fixture", ct);
        Assert.True(audits > 0);
        await DemoFixtureCommand.RunAsync(connection, "Test", seed, ct);
        Assert.Equal(audits, await first.AuditEvents.CountAsync(a => a.ActorId == "system:synthetic-demo-fixture", ct));

        var tugs = seed.Vessels.Where(v => v.Kind == VesselKind.Tug).Select(v => v.Id).ToArray();
        Assert.All(await first.TowagePlans.Where(p => tugs.Contains(p.VesselId)).ToArrayAsync(ct), p => Assert.Equal("DRAFT", p.State));
        Assert.All(await first.TowageBriefings.Where(b => tugs.Contains(b.VesselId)).ToArrayAsync(ct), b => Assert.Equal("DRAFT", b.State));
        Assert.Empty(await first.TowageExecutions.Where(x => tugs.Contains(x.VesselId)).ToArrayAsync(ct));
        var memberships = await first.AccountMemberships.Where(m => m.Source == "SyntheticUnverified").ToArrayAsync(ct);
        Assert.Equal(seed.Tenants.Count(), memberships.Length);
        Assert.All(memberships, m => Assert.Equal(0, m.AccessRevision));
        Assert.All(await first.UserAccounts.Where(a => a.Source == "SyntheticUnverified").ToArrayAsync(ct),
            a => Assert.Equal(AccountMembershipFixtures.SyntheticIssuer, a.Issuer));
    }
}
