using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class RateLimitPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    public string ConnectionString => postgres.GetConnectionString();
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        await using var db = new PoseidonDbContext(SeedImporter.Options(ConnectionString), Guid.Empty, ActorContext.SeedImport(), true);
        await db.Database.MigrateAsync();
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}

// RATE-LIMIT-01: two limiter instances (separate data sources = separate API processes) on one real PostgreSQL database
// share counters; the atomic upsert yields exactly one first refusal under concurrency; expiry cleanup is bounded.
[Trait("Category", "Integration")]
[Trait("Requirement", "RATE-LIMIT-01")]
public sealed class RateLimitPostgresTests(RateLimitPostgresFixture fixture) : IClassFixture<RateLimitPostgresFixture>
{
    private const string Secret = "synthetic-only-rate-limit-key-material-0001";
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 7, 30, TimeSpan.Zero);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private SharedRateLimiter Instance(NpgsqlDataSource source, TimeProvider clock) =>
        new(RateLimitPartitionKeys.FromSecret(Secret), new PostgresRateLimitStore(source), new LocalRateLimitCounters(clock), clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SharedRateLimiter>.Instance);

    private static RateLimitPolicy Policy(int limit = 4) => new("it." + Guid.NewGuid().ToString("N")[..12], limit, TimeSpan.FromMinutes(15));

    [Fact]
    public async Task Two_instances_share_one_counter_and_store_only_the_keyed_hash()
    {
        await using var sourceA = NpgsqlDataSource.Create(fixture.ConnectionString);
        await using var sourceB = NpgsqlDataSource.Create(fixture.ConnectionString);
        var clock = new Clock(Now);
        var (a, b) = (Instance(sourceA, clock), Instance(sourceB, clock));
        var policy = Policy();
        var outcomes = new List<RateLimitDecision>();
        for (var i = 0; i < 6; i++) outcomes.Add(await (i % 2 == 0 ? a : b).AcquireAsync(policy, ["https://idp.example.invalid", "subject-4711"], Ct));
        Assert.Equal([RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Limited,
            RateLimitOutcome.LimitedRepeat], outcomes.Select(x => x.Outcome));
        Assert.All(outcomes, x => Assert.Equal(RateLimitSource.Shared, x.Source));
        Assert.Equal(TimeSpan.FromSeconds(450), outcomes[^1].RetryAfter);

        await using var read = sourceA.CreateCommand("""SELECT "PartitionHash", "Count" FROM ratelimit."RateLimitCounters" WHERE "Policy" = @p""");
        read.Parameters.AddWithValue("p", policy.Name);
        await using var reader = await read.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        var stored = reader.GetString(0);
        Assert.Matches("^[0-9a-f]{64}$", stored);
        Assert.DoesNotContain("4711", stored);
        Assert.Equal(RateLimitWindow.Cap(policy), reader.GetInt64(1)); // capped, never unbounded
        Assert.False(await reader.ReadAsync(Ct));

        // A new window restarts the shared counter for both instances.
        clock.Now = clock.Now.AddMinutes(15);
        Assert.Equal(RateLimitOutcome.Allowed, (await b.AcquireAsync(policy, ["https://idp.example.invalid", "subject-4711"], Ct)).Outcome);
    }

    [Fact]
    public async Task Concurrent_attempts_across_instances_see_exactly_one_first_refusal()
    {
        await using var sourceA = NpgsqlDataSource.Create(fixture.ConnectionString);
        await using var sourceB = NpgsqlDataSource.Create(fixture.ConnectionString);
        var clock = new Clock(Now);
        var (a, b) = (Instance(sourceA, clock), Instance(sourceB, clock));
        var policy = Policy(limit: 10);
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i => (i % 2 == 0 ? a : b).AcquireAsync(policy, ["shared-partition"], Ct)));
        Assert.Equal(10, results.Count(x => x.Outcome == RateLimitOutcome.Allowed));
        Assert.Equal(1, results.Count(x => x.Outcome == RateLimitOutcome.Limited));
        Assert.Equal(29, results.Count(x => x.Outcome == RateLimitOutcome.LimitedRepeat));
        Assert.All(results, x => Assert.Equal(RateLimitSource.Shared, x.Source));
    }

    [Fact]
    public async Task A_lagging_instance_clock_never_resets_a_newer_window()
    {
        await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
        var ahead = new Clock(Now.AddMinutes(15));
        var behind = new Clock(Now);
        var policy = Policy(limit: 2);
        var fast = Instance(source, ahead);
        var slow = Instance(source, behind);
        await fast.AcquireAsync(policy, ["p"], Ct);
        await fast.AcquireAsync(policy, ["p"], Ct);
        var lagging = await slow.AcquireAsync(policy, ["p"], Ct);
        Assert.Equal(RateLimitOutcome.Limited, lagging.Outcome);
        // Retry-After follows the stored (newer) window, not the lagging clock's.
        Assert.Equal(RateLimitWindow.End(RateLimitWindow.Index(ahead.Now, policy), policy) - behind.Now, lagging.RetryAfter);
    }

    [Fact]
    public async Task Expired_counters_are_deleted_in_bounded_batches_and_current_ones_stay()
    {
        await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
        var clock = new Clock(Now);
        var limiter = Instance(source, clock);
        var policy = Policy();
        for (var i = 0; i < 7; i++) await limiter.AcquireAsync(policy, ["old-" + i], Ct);
        clock.Now = clock.Now.AddMinutes(15);
        await limiter.AcquireAsync(policy, ["current"], Ct);
        var store = new PostgresRateLimitStore(source);
        // Other tests' rows may also be expired; delete until this policy has no expired rows, 3 at most per batch.
        var batches = 0;
        while (await Count(source, policy, expiredAt: clock.Now) > 0)
        {
            Assert.InRange(await store.DeleteExpiredAsync(clock.Now, 3, Ct), 1, 3);
            Assert.True(++batches < 100);
        }
        Assert.Equal(1, await Count(source, policy, expiredAt: null));
    }

    [Fact]
    public async Task Counters_violating_the_closed_shape_are_refused_by_the_database()
    {
        await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
        await using var insert = source.CreateCommand("""
            INSERT INTO ratelimit."RateLimitCounters" ("Policy", "PartitionHash", "WindowIndex", "Count", "ExpiresAt") VALUES ('it.raw', @h, 1, 1, now())
            """);
        insert.Parameters.AddWithValue("h", "subject-4711".PadRight(64, 'x'));
        var error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(Ct));
        Assert.Equal(("23514", "CK_RateLimitCounters_Hash"), (error.SqlState, error.ConstraintName));
    }

    private static async Task<long> Count(NpgsqlDataSource source, RateLimitPolicy policy, DateTimeOffset? expiredAt)
    {
        await using var command = source.CreateCommand(expiredAt == null
            ? """SELECT count(*) FROM ratelimit."RateLimitCounters" WHERE "Policy" = @p"""
            : """SELECT count(*) FROM ratelimit."RateLimitCounters" WHERE "Policy" = @p AND "ExpiresAt" < @now""");
        command.Parameters.AddWithValue("p", policy.Name);
        if (expiredAt != null) command.Parameters.AddWithValue("now", expiredAt.Value);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
