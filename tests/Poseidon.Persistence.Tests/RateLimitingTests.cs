using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Poseidon.Persistence.Tests;

// RATE-LIMIT-01 unit coverage: keyed partition hashing (no raw values), fixed-window math, the shared/fallback decision path
// and the exclusion of counters from the audited business SaveChanges path. No database.
[Trait("Category", "Unit")]
[Trait("Requirement", "RATE-LIMIT-01")]
public sealed class RateLimitingTests
{
    private const string Secret = "synthetic-only-rate-limit-key-material-0001";
    private static readonly RateLimitPolicy Policy = new("test.policy", 3, TimeSpan.FromMinutes(15));
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 7, 30, TimeSpan.Zero);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Shared in-memory stand-in for the PostgreSQL table: same upsert semantics on its OWN clock (the database clock).
    private sealed class MemoryStore(TimeProvider databaseClock) : IRateLimitStore
    {
        private readonly Dictionary<(string, string), (long Window, long Count)> rows = new();
        public List<string> Hashes { get; } = [];
        public Task<(long Count, TimeSpan Remaining)> IncrementAsync(RateLimitPolicy policy, string partitionHash, CancellationToken cancellationToken)
        {
            lock (rows)
            {
                Hashes.Add(partitionHash);
                var now = databaseClock.GetUtcNow();
                var window = RateLimitWindow.Index(now, policy);
                var next = rows.TryGetValue((policy.Name, partitionHash), out var row) && row.Window == window
                    ? (window, Math.Min(row.Count + 1, RateLimitWindow.Cap(policy))) : (window, 1L);
                rows[(policy.Name, partitionHash)] = next;
                return Task.FromResult((next.Item2, RateLimitWindow.End(window, policy) - now));
            }
        }
        public Task<int> DeleteExpiredAsync(int batchSize, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    // A store reporting an absurd remaining time (corrupt row or skewed store clock).
    private sealed class SkewedStore(TimeSpan remaining) : IRateLimitStore
    {
        public Task<(long Count, TimeSpan Remaining)> IncrementAsync(RateLimitPolicy policy, string partitionHash, CancellationToken cancellationToken) =>
            Task.FromResult((RateLimitWindow.Cap(policy), remaining));
        public Task<int> DeleteExpiredAsync(int batchSize, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class FailingStore(Exception error) : IRateLimitStore
    {
        public int Calls { get; private set; }
        public Task<(long Count, TimeSpan Remaining)> IncrementAsync(RateLimitPolicy policy, string partitionHash, CancellationToken cancellationToken)
        { Calls++; throw error; }
        public Task<int> DeleteExpiredAsync(int batchSize, CancellationToken cancellationToken) => throw error;
    }

    private sealed class CapturingLogger : ILogger<SharedRateLimiter>
    {
        public List<(LogLevel Level, EventId Id, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception); // the provider exception (host names, SQL) is never attached
            Entries.Add((logLevel, eventId, formatter(state, exception)));
        }
    }

    private static SharedRateLimiter Limiter(IRateLimitStore? store, TimeProvider clock, ILogger<SharedRateLimiter>? logger = null, TimeSpan? breaker = null) =>
        new(store == null ? RateLimitPartitionKeys.Ephemeral() : RateLimitPartitionKeys.FromSecret(Secret), store, new LocalRateLimitCounters(clock), clock,
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SharedRateLimiter>.Instance, breaker);

    [Fact]
    public void Partition_hashes_are_keyed_stable_and_never_contain_the_raw_values()
    {
        var keys = RateLimitPartitionKeys.FromSecret(Secret);
        var hash = keys.For(Policy, ["https://idp.example.invalid", "subject-4711"]);
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.DoesNotContain("4711", hash);
        Assert.DoesNotContain("idp", hash);
        Assert.Equal(hash, RateLimitPartitionKeys.FromSecret(Secret).For(Policy, ["https://idp.example.invalid", "subject-4711"]));
        Assert.NotEqual(hash, RateLimitPartitionKeys.FromSecret(Secret + "x").For(Policy, ["https://idp.example.invalid", "subject-4711"]));
        Assert.NotEqual(hash, keys.For(new RateLimitPolicy("other.policy", 3, TimeSpan.FromMinutes(15)), ["https://idp.example.invalid", "subject-4711"]));
        // Length prefixing: moving the separator between parts yields another partition.
        Assert.NotEqual(keys.For(Policy, ["ab", "c"]), keys.For(Policy, ["a", "bc"]));
        Assert.NotEqual(keys.For(Policy, ["a\nb"]), keys.For(Policy, ["a", "b"]));
        Assert.True(keys.Persistent);
        Assert.False(RateLimitPartitionKeys.Ephemeral().Persistent);
        Assert.NotEqual(RateLimitPartitionKeys.Ephemeral().For(Policy, ["x"]), RateLimitPartitionKeys.Ephemeral().For(Policy, ["x"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("                                        ")]
    public void Missing_or_weak_key_material_is_refused(string secret) =>
        Assert.Throws<InvalidOperationException>(() => RateLimitPartitionKeys.FromSecret(secret));

    [Fact]
    public void A_shared_store_cannot_be_used_with_a_per_process_key() =>
        Assert.Throws<InvalidOperationException>(() => new SharedRateLimiter(RateLimitPartitionKeys.Ephemeral(), new MemoryStore(TimeProvider.System),
            new LocalRateLimitCounters(TimeProvider.System), TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<SharedRateLimiter>.Instance));

    [Theory]
    [InlineData("Upper")]
    [InlineData("has space")]
    [InlineData("")]
    [InlineData("a-name-that-is-far-too-long-for-the-counter-policy-column")]
    public void Policy_names_are_closed(string name) => Assert.Throws<ArgumentException>(() => new RateLimitPolicy(name, 1, TimeSpan.FromMinutes(1)));

    [Fact]
    public void Fixed_window_math_is_epoch_aligned_and_retry_after_counts_to_the_window_end()
    {
        var index = RateLimitWindow.Index(Now, Policy);
        Assert.Equal(Now.ToUnixTimeSeconds() / 900, index);
        Assert.Equal(new DateTimeOffset(2026, 11, 22, 9, 15, 0, TimeSpan.Zero), RateLimitWindow.End(index, Policy));
        Assert.Equal(TimeSpan.FromSeconds(450), RateLimitWindow.RetryAfter(Now, index, Policy));
        // Never zero or negative (a lagging instance clock reports the stored, newer window).
        Assert.Equal(TimeSpan.FromSeconds(1), RateLimitWindow.RetryAfter(RateLimitWindow.End(index, Policy), index, Policy));
        // QA M1: never more than one policy window, whatever the stored window says.
        Assert.Equal(Policy.Window, RateLimitWindow.RetryAfter(Now, index + 40, Policy));
        Assert.Equal(Policy.Window, RateLimitWindow.Bound(TimeSpan.FromHours(6), Policy));
        Assert.Equal([RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Limited, RateLimitOutcome.LimitedRepeat],
            Enumerable.Range(1, 5).Select(c => RateLimitWindow.Classify(c, Policy)));
        Assert.Equal(5, RateLimitWindow.Cap(Policy));
        Assert.Equal("1", new RateLimitDecision(RateLimitOutcome.Limited, TimeSpan.FromMilliseconds(10), RateLimitSource.Local).RetryAfterHeader);
        Assert.Equal("451", new RateLimitDecision(RateLimitOutcome.Limited, TimeSpan.FromSeconds(450.2), RateLimitSource.Local).RetryAfterHeader);
    }

    [Fact]
    public async Task Two_limiters_on_one_store_share_a_window_and_only_one_attempt_sees_the_first_refusal()
    {
        var clock = new Clock(Now);
        var store = new MemoryStore(clock);
        var a = Limiter(store, clock);
        var b = Limiter(store, clock);
        var outcomes = new List<RateLimitOutcome>();
        for (var i = 0; i < 6; i++) outcomes.Add((await (i % 2 == 0 ? a : b).AcquireAsync(Policy, ["issuer", "subject"], Ct)).Outcome);
        Assert.Equal([RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Limited,
            RateLimitOutcome.LimitedRepeat, RateLimitOutcome.LimitedRepeat], outcomes);
        Assert.All(store.Hashes, h => Assert.Matches("^[0-9a-f]{64}$", h));
        Assert.Single(store.Hashes.Distinct());
        var limited = await a.AcquireAsync(Policy, ["issuer", "subject"], Ct);
        Assert.Equal((RateLimitSource.Shared, TimeSpan.FromSeconds(450)), (limited.Source, limited.RetryAfter));
        clock.Now = clock.Now.AddMinutes(15);
        Assert.Equal(RateLimitOutcome.Allowed, (await b.AcquireAsync(Policy, ["issuer", "subject"], Ct)).Outcome);
    }

    [Fact]
    public async Task Instance_clocks_ahead_or_behind_cannot_move_a_shared_partition_into_another_window()
    {
        // QA M1: the store owns the window (database clock); API instance clocks hours apart share one window.
        var database = new Clock(Now);
        var store = new MemoryStore(database);
        var fast = Limiter(store, new Clock(Now.AddHours(6)));
        var slow = Limiter(store, new Clock(Now.AddHours(-6)));
        var outcomes = new List<RateLimitDecision>();
        for (var i = 0; i < 5; i++) outcomes.Add(await (i % 2 == 0 ? fast : slow).AcquireAsync(Policy, ["p"], Ct));
        Assert.Equal([RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Limited, RateLimitOutcome.LimitedRepeat],
            outcomes.Select(x => x.Outcome));
        Assert.All(outcomes, x => Assert.Equal(TimeSpan.FromSeconds(450), x.RetryAfter));
        // When the database window ends, the partition is free again for every instance.
        database.Now = database.Now.AddMinutes(8);
        Assert.Equal(RateLimitOutcome.Allowed, (await fast.AcquireAsync(Policy, ["p"], Ct)).Outcome);
        // A store reporting an hours-long remaining time is capped at the policy window.
        var capped = await Limiter(new SkewedStore(TimeSpan.FromHours(9)), database).AcquireAsync(Policy, ["p"], Ct);
        Assert.Equal((RateLimitOutcome.LimitedRepeat, Policy.Window, "900"), (capped.Outcome, capped.RetryAfter, capped.RetryAfterHeader));
    }

    [Fact]
    public async Task An_unavailable_store_falls_back_to_enforced_local_limits_with_a_non_pii_warning_and_a_breaker()
    {
        var clock = new Clock(Now);
        var store = new FailingStore(new NpgsqlException("Failed to connect to db.internal.example:5432 for subject-4711"));
        var logger = new CapturingLogger();
        var limiter = Limiter(store, clock, logger, TimeSpan.FromSeconds(10));
        var outcomes = new List<RateLimitDecision>();
        for (var i = 0; i < 5; i++) outcomes.Add(await limiter.AcquireAsync(Policy, ["issuer", "subject-4711"], Ct));
        // Never fail open: the fallback still enforces the policy.
        Assert.Equal([RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Allowed, RateLimitOutcome.Limited, RateLimitOutcome.LimitedRepeat],
            outcomes.Select(x => x.Outcome));
        Assert.All(outcomes, x => Assert.Equal(RateLimitSource.Fallback, x.Source));
        // Breaker: one store attempt and one warning while open; the warning names only the policy and the error type.
        Assert.Equal(1, store.Calls);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal((LogLevel.Warning, 4290), (warning.Level, warning.Id.Id));
        Assert.Contains("test.policy", warning.Message);
        Assert.Contains(nameof(NpgsqlException), warning.Message);
        Assert.DoesNotContain("db.internal", warning.Message);
        Assert.DoesNotContain("4711", warning.Message);
        clock.Now = clock.Now.AddSeconds(11);
        await limiter.AcquireAsync(Policy, ["issuer", "subject-4711"], Ct);
        Assert.Equal(2, store.Calls);
    }

    [Fact]
    public async Task Timeouts_fall_back_but_caller_cancellation_and_programming_errors_propagate()
    {
        var clock = new Clock(Now);
        Assert.Equal(RateLimitSource.Fallback, (await Limiter(new FailingStore(new TimeoutException()), clock).AcquireAsync(Policy, ["x"], Ct)).Source);
        Assert.Equal(RateLimitSource.Fallback, (await Limiter(new FailingStore(new OperationCanceledException()), clock).AcquireAsync(Policy, ["x"], Ct)).Source);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Limiter(new FailingStore(new OperationCanceledException(cancelled.Token)), clock).AcquireAsync(Policy, ["x"], cancelled.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => Limiter(new FailingStore(new ArgumentException()), clock).AcquireAsync(Policy, ["x"], Ct));
    }

    [Fact]
    public async Task Without_a_store_the_decision_is_local_by_configuration()
    {
        var decision = await Limiter(null, new Clock(Now)).AcquireAsync(Policy, ["x"], Ct);
        Assert.Equal((RateLimitOutcome.Allowed, RateLimitSource.Local), (decision.Outcome, decision.Source));
    }

    [Fact]
    public void Counters_are_refused_on_the_audited_business_save_path()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql("Host=127.0.0.1;Database=synthetic_rate_limit_model;Username=synthetic").Options;
        using var db = new PoseidonDbContext(options, Guid.NewGuid(), new ActorContext("synthetic-rate-limit", "User", "synthetic-rate-limit"));
        db.Add(new RateLimitCounter { Policy = "test.policy", PartitionHash = new string('a', 64), WindowIndex = 1, Count = 1, ExpiresAt = Now });
        var error = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Contains("Rate-limit counters", error.Message);
        var entity = db.Model.FindEntityType(typeof(RateLimitCounter))!;
        Assert.Equal(("ratelimit", "RateLimitCounters"), (entity.GetSchema(), entity.GetTableName()));
        Assert.DoesNotContain(entity.GetProperties(), p => p.Name.Contains("Tenant") || p.Name.Contains("Actor") || p.Name.Contains("Subject"));
    }
}
