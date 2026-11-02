using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class SecurityEventTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    public async ValueTask InitializeAsync() => await postgres.StartAsync(TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task Fixed_endpoint_vocabulary_persists_with_attribution_and_without_payloads()
    {
        var ct = TestContext.Current.CancellationToken;
        var seed = SeedValidationTests.Demo();
        await using var setup = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await setup.Database.MigrateAsync(ct);
        await using var writer = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(postgres.GetConnectionString()).Options);
        foreach (var action in SecurityEventCodes.Actions) {
            var entry = Event(seed.Tenants[0].Id); entry.Action = action; entry.Result = "Attempted";
            await writer.AppendAsync(entry, ct);
        }
        foreach (var result in SecurityEventCodes.Results) {
            var entry = Event(seed.Tenants[0].Id); entry.Result = result;
            await writer.AppendAsync(entry, ct);
        }
        await using var inspect = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), seed.Tenants[0].Id, new("synthetic-inspector", "User", "catalog-inspection"));
        var rows = await inspect.Set<SecurityEvent>().AsNoTracking().ToArrayAsync(ct);
        Assert.Equal(SecurityEventCodes.Actions.Count + SecurityEventCodes.Results.Count, rows.Length);
        Assert.All(rows, row => { Assert.Equal(seed.Tenants[0].Id, row.TenantId); Assert.Equal("synthetic-actor", row.ActorId); });
        Assert.Contains(rows, row => row.Action == "ControlEvidenceCommand");
        Assert.Contains(rows, row => row.Result == "owning_verification_requires_independent_evaluation");
    }

    [Fact]
    public async Task Observed_denials_survive_business_rollback_and_are_immutable_and_scoped()
    {
        var seed = SeedValidationTests.Demo();
        await using (var setup = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), seed.Tenants[0].Id, new("synthetic-actor", "User", "synthetic-correlation"));
        await using (var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            (await db.Companies.FirstAsync(TestContext.Current.CancellationToken)).Name = "Rolled back";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await using var writer = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(postgres.GetConnectionString()).Options);
            await writer.AppendAsync(Event(seed.Tenants[0].Id), TestContext.Current.CancellationToken);
            await writer.AppendAsync(Event(null), TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
        db.ChangeTracker.Clear();
        Assert.DoesNotContain(await db.Companies.ToArrayAsync(TestContext.Current.CancellationToken), c => c.Name == "Rolled back");
        Assert.Single(await db.Set<SecurityEvent>().ToArrayAsync(TestContext.Current.CancellationToken));
        await using var other = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), seed.Tenants[1].Id, new("other", "User", "other"));
        Assert.Empty(await other.Set<SecurityEvent>().ToArrayAsync(TestContext.Current.CancellationToken));
        var entry = await db.Set<SecurityEvent>().SingleAsync(TestContext.Current.CancellationToken);
        entry.Result = "Success";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        foreach (var sql in new[] { "UPDATE audit.\"SecurityEvents\" SET \"Result\"='Success'", "DELETE FROM audit.\"SecurityEvents\"", "TRUNCATE audit.\"SecurityEvents\"" })
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken));
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var count = new NpgsqlCommand("SELECT count(*) FROM audit.\"SecurityEvents\" WHERE \"TenantId\" IS NULL AND \"ActorId\" IS NULL", connection);
        Assert.Equal(1L, await count.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    private static SecurityEvent Event(Guid? tenant) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, ActorId = tenant == null ? null : "synthetic-actor",
        Action = "AccessDenied", Result = "Denied", CorrelationId = "synthetic-correlation", OccurredAt = DateTimeOffset.UtcNow
    };
}
