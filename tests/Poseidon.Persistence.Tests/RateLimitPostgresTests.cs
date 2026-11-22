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

    private static RateLimitDataSource Source(NpgsqlDataSource source) => new(source);

    private SharedRateLimiter Instance(NpgsqlDataSource source, TimeProvider clock) =>
        new(RateLimitPartitionKeys.FromSecret(Secret), new PostgresRateLimitStore(Source(source)), new LocalRateLimitCounters(clock), clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SharedRateLimiter>.Instance);

    // Database-clock windows: a one-day window (UTC-midnight boundary) keeps each sequence inside one window in practice.
    private static RateLimitPolicy Policy(int limit = 4) => new("it." + Guid.NewGuid().ToString("N")[..12], limit, TimeSpan.FromDays(1));

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
        Assert.InRange(outcomes[^1].RetryAfter, TimeSpan.FromSeconds(1), policy.Window);

        await using var read = sourceA.CreateCommand("""SELECT "PartitionHash", "Count" FROM ratelimit."RateLimitCounters" WHERE "Policy" = @p""");
        read.Parameters.AddWithValue("p", policy.Name);
        await using var reader = await read.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        var stored = reader.GetString(0);
        Assert.Matches("^[0-9a-f]{64}$", stored);
        Assert.DoesNotContain("4711", stored);
        Assert.Equal(RateLimitWindow.Cap(policy), reader.GetInt64(1)); // capped, never unbounded
        Assert.False(await reader.ReadAsync(Ct));

        // Advancing an instance clock does not open a new window: the database clock owns it (QA M1).
        clock.Now = clock.Now.AddDays(2);
        Assert.Equal(RateLimitOutcome.LimitedRepeat, (await b.AcquireAsync(policy, ["https://idp.example.invalid", "subject-4711"], Ct)).Outcome);
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
    public async Task Instance_clocks_hours_ahead_or_behind_share_the_database_window_and_never_lock_out_beyond_it()
    {
        // QA M1: a fast-clock node can no longer push a partition into a future window.
        await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
        var policy = Policy(limit: 2);
        var fast = Instance(source, new Clock(DateTimeOffset.UtcNow.AddHours(6)));
        var slow = Instance(source, new Clock(DateTimeOffset.UtcNow.AddHours(-6)));
        var results = new[]
        {
            await fast.AcquireAsync(policy, ["p"], Ct), await slow.AcquireAsync(policy, ["p"], Ct),
            await fast.AcquireAsync(policy, ["p"], Ct), await slow.AcquireAsync(policy, ["p"], Ct)
        };
        Assert.Equal([RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Limited, RateLimitOutcome.LimitedRepeat], results.Select(x => x.Outcome));
        Assert.All(results, x => Assert.InRange(x.RetryAfter, TimeSpan.FromSeconds(1), policy.Window));

        await using var read = source.CreateCommand("""
            SELECT "WindowIndex" = floor(extract(epoch FROM now()) / 86400)::bigint, "ExpiresAt" <= now() + interval '1 day'
            FROM ratelimit."RateLimitCounters" WHERE "Policy" = @p
            """);
        read.Parameters.AddWithValue("p", policy.Name);
        await using var reader = await read.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.True(reader.GetBoolean(0) && reader.GetBoolean(1), "The stored window must be the database clock's current window.");
    }

    [Fact]
    public async Task Expired_counters_are_deleted_by_the_database_clock_in_bounded_batches_and_current_ones_stay()
    {
        await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
        var policy = Policy();
        // Synthetic expired rows (test-only raw insert); the instance clock is irrelevant to expiry (QA L1).
        for (var i = 0; i < 7; i++)
        {
            await using var insert = source.CreateCommand("""
                INSERT INTO ratelimit."RateLimitCounters" ("Policy", "PartitionHash", "WindowIndex", "Count", "ExpiresAt")
                VALUES (@p, @h, 1, 1, now() - interval '1 hour')
                """);
            insert.Parameters.AddWithValue("p", policy.Name);
            insert.Parameters.AddWithValue("h", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(BitConverter.GetBytes(i))).ToLowerInvariant());
            await insert.ExecuteNonQueryAsync(Ct);
        }
        await Instance(source, new Clock(DateTimeOffset.UtcNow.AddDays(3))).AcquireAsync(policy, ["current"], Ct);
        var store = new PostgresRateLimitStore(Source(source));
        // Other tests' rows may also be expired; delete until this policy has no expired rows, 3 at most per batch.
        var batches = 0;
        while (await Count(source, policy, expired: true) > 0)
        {
            Assert.InRange(await store.DeleteExpiredAsync(3, Ct), 1, 3);
            Assert.True(++batches < 100);
        }
        Assert.Equal(1, await Count(source, policy, expired: false));
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

    private static async Task<long> Count(NpgsqlDataSource source, RateLimitPolicy policy, bool expired)
    {
        await using var command = source.CreateCommand(expired
            ? """SELECT count(*) FROM ratelimit."RateLimitCounters" WHERE "Policy" = @p AND "ExpiresAt" < now()"""
            : """SELECT count(*) FROM ratelimit."RateLimitCounters" WHERE "Policy" = @p""");
        command.Parameters.AddWithValue("p", policy.Name);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
