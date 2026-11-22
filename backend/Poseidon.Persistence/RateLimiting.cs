using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Poseidon.Persistence;

// RATE-LIMIT-01: shared, PostgreSQL-backed technical abuse guard (closes the USER-ADMIN-02 "process-local" open item).
// Counters are technical throttling state, not business data: they are never written through the audited business
// SaveChanges path (PoseidonDbContext refuses tracked counters) and carry no tenant, actor, subject, IP or token. A row is
// (closed policy name, keyed HMAC of the partition parts, fixed window index, count, expiry). Controlled design per
// AGENTS.md: the single-statement upsert and the bounded expiry delete below are the only raw SQL writers of this table.

public enum RateLimitOutcome { Allowed, Limited, LimitedRepeat }

// Where the decision was counted: Shared = PostgreSQL store; Local = in-process by configuration; Fallback = in-process
// because the configured shared store was unavailable (logged + metered, never silent).
public enum RateLimitSource { Shared, Local, Fallback }

public readonly record struct RateLimitDecision(RateLimitOutcome Outcome, TimeSpan RetryAfter, RateLimitSource Source)
{
    public bool Allowed => Outcome == RateLimitOutcome.Allowed;
    // Delta-seconds for the Retry-After header (RFC 9110 §10.2.3): whole seconds, at least 1.
    public string RetryAfterHeader => Math.Max(1, (long)Math.Ceiling(RetryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
}

// A closed technical policy: name [a-z0-9.-], a permit limit per fixed window. Not an approved product quota.
public sealed partial record RateLimitPolicy
{
    public string Name { get; }
    public int PermitLimit { get; }
    public TimeSpan Window { get; }

    public RateLimitPolicy(string name, int permitLimit, TimeSpan window)
    {
        if (!PolicyName().IsMatch(name)) throw new ArgumentException("Invalid rate-limit policy name.", nameof(name));
        if (permitLimit is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(permitLimit));
        if (window < TimeSpan.FromSeconds(1) || window > TimeSpan.FromDays(1) || window.Ticks % TimeSpan.TicksPerSecond != 0)
            throw new ArgumentOutOfRangeException(nameof(window));
        (Name, PermitLimit, Window) = (name, permitLimit, window);
    }

    internal long WindowSeconds => (long)Window.TotalSeconds;

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,47}$")]
    private static partial Regex PolicyName();
}

// Fixed-window arithmetic shared by the store and the in-process counters (Unix epoch aligned).
public static class RateLimitWindow
{
    public static long Index(DateTimeOffset now, RateLimitPolicy policy) => now.ToUnixTimeSeconds() / policy.WindowSeconds;
    public static DateTimeOffset End(long index, RateLimitPolicy policy) => DateTimeOffset.FromUnixTimeSeconds((index + 1) * policy.WindowSeconds);
    public static TimeSpan RetryAfter(DateTimeOffset now, long index, RateLimitPolicy policy)
    {
        var remaining = End(index, policy) - now;
        return remaining < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : remaining;
    }
    // Exactly one attempt per window and partition observes Limited (the first refusal, audited once); later ones are
    // LimitedRepeat. With the store's atomic increment this holds across instances, too.
    public static RateLimitOutcome Classify(long count, RateLimitPolicy policy) =>
        count <= policy.PermitLimit ? RateLimitOutcome.Allowed : count == policy.PermitLimit + 1L ? RateLimitOutcome.Limited : RateLimitOutcome.LimitedRepeat;
    // Counts stop growing past limit+2: enough to keep the Limited/LimitedRepeat distinction, no overflow.
    public static long Cap(RateLimitPolicy policy) => policy.PermitLimit + 2L;
}

// Partition keys: HMAC-SHA256 over the length-prefixed policy and parts, keyed by an HKDF of operator key material.
// The raw issuer/subject/tenant/actor/IP/token never leaves the process and is never stored. Without operator key material
// a random per-process key is used (Persistent = false), which is only valid for the in-process limiter.
public sealed class RateLimitPartitionKeys
{
    public const int MinimumSecretLength = 32;
    private static readonly byte[] Info = Encoding.ASCII.GetBytes("poseidon.ratelimit.partition.v1");
    private readonly byte[] key;
    public bool Persistent { get; }

    private RateLimitPartitionKeys(byte[] key, bool persistent) { this.key = key; Persistent = persistent; }

    public static RateLimitPartitionKeys FromSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < MinimumSecretLength)
            throw new InvalidOperationException("Rate-limit partition key material is unavailable or too short.");
        return new(HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret), 32, info: Info), true);
    }

    public static RateLimitPartitionKeys Ephemeral() => new(RandomNumberGenerator.GetBytes(32), false);

    public string For(RateLimitPolicy policy, IReadOnlyList<string> parts)
    {
        if (parts.Count == 0) throw new ArgumentException("A partition needs at least one part.", nameof(parts));
        using var buffer = new MemoryStream();
        void Write(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            buffer.Write(length);
            buffer.Write(bytes);
        }
        Write(policy.Name);
        foreach (var part in parts) Write(part ?? throw new ArgumentNullException(nameof(parts)));
        return Convert.ToHexString(HMACSHA256.HashData(key, buffer.ToArray())).ToLowerInvariant();
    }
}

// The counter row. Mapped only so EF migrations own the schema; reads/writes go through PostgresRateLimitStore.
public sealed class RateLimitCounter
{
    public required string Policy { get; set; }
    public required string PartitionHash { get; set; }
    public long WindowIndex { get; set; }
    public long Count { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<RateLimitCounter>(b =>
        {
            b.ToTable("RateLimitCounters", "ratelimit", t =>
            {
                t.HasCheckConstraint("CK_RateLimitCounters_Hash", "\"PartitionHash\" ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("CK_RateLimitCounters_Policy", "\"Policy\" ~ '^[a-z0-9][a-z0-9.-]{0,47}$'");
                t.HasCheckConstraint("CK_RateLimitCounters_Count", "\"Count\" >= 1 AND \"WindowIndex\" >= 0");
            });
            b.HasKey(x => new { x.Policy, x.PartitionHash });
            b.Property(x => x.Policy).HasMaxLength(48);
            b.Property(x => x.PartitionHash).HasMaxLength(64).IsFixedLength();
            b.HasIndex(x => x.ExpiresAt);
        });
    }
}

public interface IRateLimitStore
{
    // Atomically increments the partition's counter for the window and returns (count, window) actually stored.
    Task<(long Count, long WindowIndex)> IncrementAsync(RateLimitPolicy policy, string partitionHash, long windowIndex, CancellationToken cancellationToken);
    // Deletes at most batchSize expired rows; returns the number deleted.
    Task<int> DeleteExpiredAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);
}

// Single-statement atomic upsert (row lock under READ COMMITTED; no SERIALIZABLE transaction, so no 40001 retry loop).
// A row from an older window restarts at 1; a row already in a newer window (another instance's clock ahead) keeps
// counting in that newer window, so a lagging clock can never reset a current counter.
public sealed class PostgresRateLimitStore(NpgsqlDataSource dataSource, int commandTimeoutSeconds = 2) : IRateLimitStore
{
    private const string Upsert = """
        INSERT INTO ratelimit."RateLimitCounters" AS c ("Policy", "PartitionHash", "WindowIndex", "Count", "ExpiresAt")
        VALUES (@policy, @hash, @window, 1, @expires)
        ON CONFLICT ("Policy", "PartitionHash") DO UPDATE SET
          "Count" = CASE WHEN c."WindowIndex" >= EXCLUDED."WindowIndex" THEN LEAST(c."Count" + 1, @cap) ELSE 1 END,
          "WindowIndex" = GREATEST(c."WindowIndex", EXCLUDED."WindowIndex"),
          "ExpiresAt" = GREATEST(c."ExpiresAt", EXCLUDED."ExpiresAt")
        RETURNING "Count", "WindowIndex"
        """;

    // Bounded: SKIP LOCKED never waits on a counter being incremented; the expiry is re-checked under the row lock.
    // Returns the deleted row count as a scalar. This is the second (and last) raw writer; RateLimitRawSqlBoundaryTests pins
    // both statements to this one table, outside the S1 metadata guard that forbids ExecuteNonQuery in business assemblies.
    private const string Cleanup = """
        WITH expired AS (
          SELECT "Policy", "PartitionHash" FROM ratelimit."RateLimitCounters"
          WHERE "ExpiresAt" < @now ORDER BY "ExpiresAt" LIMIT @batch FOR UPDATE SKIP LOCKED),
        deleted AS (
          DELETE FROM ratelimit."RateLimitCounters" d USING expired e
          WHERE d."Policy" = e."Policy" AND d."PartitionHash" = e."PartitionHash" AND d."ExpiresAt" < @now
          RETURNING 1)
        SELECT count(*)::integer FROM deleted
        """;

    public async Task<(long Count, long WindowIndex)> IncrementAsync(RateLimitPolicy policy, string partitionHash, long windowIndex, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(Upsert);
        command.CommandTimeout = commandTimeoutSeconds;
        command.Parameters.AddWithValue("policy", policy.Name);
        command.Parameters.AddWithValue("hash", partitionHash);
        command.Parameters.AddWithValue("window", windowIndex);
        command.Parameters.AddWithValue("expires", RateLimitWindow.End(windowIndex, policy));
        command.Parameters.AddWithValue("cap", RateLimitWindow.Cap(policy));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Rate-limit counter upsert returned no row.");
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(Cleanup);
        command.CommandTimeout = Math.Max(commandTimeoutSeconds, 10);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("batch", Math.Clamp(batchSize, 1, 10_000));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}

// In-process fixed-window counters (Development default and the fail-safe fallback). Bounded and time-bucketed (USER-ADMIN-02
// QA N4): at capacity only entries of past windows are evicted; current-window counters are never reset, and while the table
// is full of current-window partitions a NEW partition is refused (fail closed) until the window rolls over.
public sealed class LocalRateLimitCounters(TimeProvider clock, int capacity = LocalRateLimitCounters.DefaultCapacity)
{
    public const int DefaultCapacity = 10_000;
    private readonly Dictionary<(string Policy, string Hash), (long Window, long Count, DateTimeOffset Expires)> windows = new();
    private readonly Lock gate = new();

    public int Tracked { get { lock (gate) return windows.Count; } }

    public RateLimitDecision Acquire(RateLimitPolicy policy, string partitionHash, RateLimitSource source)
    {
        var now = clock.GetUtcNow();
        var window = RateLimitWindow.Index(now, policy);
        var key = (policy.Name, partitionHash);
        long count;
        lock (gate)
        {
            if (windows.TryGetValue(key, out var entry) && entry.Window >= window)
            {
                window = entry.Window;
                count = Math.Min(entry.Count + 1, RateLimitWindow.Cap(policy));
            }
            else
            {
                if (!windows.ContainsKey(key) && windows.Count >= capacity)
                {
                    foreach (var stale in windows.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray()) windows.Remove(stale);
                    if (windows.Count >= capacity)
                        return new(RateLimitOutcome.LimitedRepeat, RateLimitWindow.RetryAfter(now, window, policy), source);
                }
                count = 1;
            }
            windows[key] = (window, count, RateLimitWindow.End(window, policy));
        }
        return new(RateLimitWindow.Classify(count, policy), RateLimitWindow.RetryAfter(now, window, policy), source);
    }
}

// Closed-label metrics: policy name and source only; never a partition, hash, tenant or actor.
public static class RateLimitTelemetry
{
    public const string MeterName = "Poseidon.RateLimit";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>("poseidon.ratelimit.decisions", "{decision}");
    private static readonly Counter<long> StoreUnavailable = Meter.CreateCounter<long>("poseidon.ratelimit.store.unavailable", "{attempt}");
    internal static void ObserveDecision(RateLimitPolicy policy, RateLimitDecision decision)
    {
        try
        {
            Decisions.Add(1, new KeyValuePair<string, object?>("poseidon.ratelimit.policy", policy.Name),
                new KeyValuePair<string, object?>("poseidon.ratelimit.outcome", decision.Outcome.ToString()),
                new KeyValuePair<string, object?>("poseidon.ratelimit.source", decision.Source.ToString()));
        }
        catch { /* A listener failure never changes the decision. */ }
    }
    internal static void ObserveStoreUnavailable(RateLimitPolicy policy)
    {
        try { StoreUnavailable.Add(1, new KeyValuePair<string, object?>("poseidon.ratelimit.policy", policy.Name)); }
        catch { /* A listener failure never changes the decision. */ }
    }
}

// The limiter consumers call. With a shared store it counts in PostgreSQL; if the store fails (connection, timeout,
// provider error) it falls back to the in-process counters for a short breaker interval, logging a non-PII warning
// (policy name and exception type only, never the message, which can carry host names) and metering every fallback.
// It never fails open: a fallback decision is still enforced locally.
public sealed partial class SharedRateLimiter
{
    private readonly RateLimitPartitionKeys keys;
    private readonly IRateLimitStore? store;
    private readonly LocalRateLimitCounters local;
    private readonly TimeProvider clock;
    private readonly ILogger logger;
    private readonly TimeSpan breaker;
    private long openUntilTicks;

    public SharedRateLimiter(RateLimitPartitionKeys keys, IRateLimitStore? store, LocalRateLimitCounters local, TimeProvider clock, ILogger<SharedRateLimiter> logger,
        TimeSpan? breaker = null)
    {
        if (store != null && !keys.Persistent)
            throw new InvalidOperationException("A shared rate-limit store requires operator partition key material.");
        (this.keys, this.store, this.local, this.clock, this.logger) = (keys, store, local, clock, logger);
        this.breaker = breaker ?? TimeSpan.FromSeconds(10);
    }

    public bool Shared => store != null;

    public async Task<RateLimitDecision> AcquireAsync(RateLimitPolicy policy, IReadOnlyList<string> parts, CancellationToken cancellationToken)
    {
        var hash = keys.For(policy, parts);
        RateLimitDecision decision;
        if (store == null) decision = local.Acquire(policy, hash, RateLimitSource.Local);
        else if (clock.GetUtcNow().UtcTicks < Interlocked.Read(ref openUntilTicks))
        {
            RateLimitTelemetry.ObserveStoreUnavailable(policy);
            decision = local.Acquire(policy, hash, RateLimitSource.Fallback);
        }
        else
        {
            try
            {
                var now = clock.GetUtcNow();
                var (count, window) = await store.IncrementAsync(policy, hash, RateLimitWindow.Index(now, policy), cancellationToken);
                decision = new(RateLimitWindow.Classify(count, policy), RateLimitWindow.RetryAfter(now, window, policy), RateLimitSource.Shared);
            }
            catch (Exception error) when (IsStoreFailure(error, cancellationToken))
            {
                Interlocked.Exchange(ref openUntilTicks, (clock.GetUtcNow() + breaker).UtcTicks);
                RateLimitTelemetry.ObserveStoreUnavailable(policy);
                StoreUnavailable(logger, policy.Name, error.GetType().Name);
                decision = local.Acquire(policy, hash, RateLimitSource.Fallback);
            }
        }
        RateLimitTelemetry.ObserveDecision(policy, decision);
        return decision;
    }

    // Caller cancellation propagates; everything the store can raise for an unreachable/failed database falls back.
    public static bool IsStoreFailure(Exception error, CancellationToken cancellationToken) =>
        !(error is OperationCanceledException && cancellationToken.IsCancellationRequested) &&
        (PostgresFailure.IsDatabase(error) || error is System.Data.Common.DbException or TimeoutException or OperationCanceledException
            or System.Net.Sockets.SocketException or IOException);

    [LoggerMessage(EventId = 4290, Level = LogLevel.Warning,
        Message = "Shared rate-limit store unavailable for policy {Policy} ({ErrorType}); enforcing the in-process limiter.")]
    private static partial void StoreUnavailable(ILogger logger, string policy, string errorType);
}
