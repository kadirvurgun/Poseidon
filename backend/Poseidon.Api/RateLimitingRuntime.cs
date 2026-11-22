using Npgsql;
using Poseidon.Persistence;

namespace Poseidon.Api;

// RATE-LIMIT-01 operator configuration ("RateLimiting" section). Store = "InMemory" (default; per-process counters) or
// "Postgres" (shared counters across API instances). Postgres requires operator key material for the partition HMAC:
// PartitionKeyReference names an environment variable (operator secret reference, never a value); a literal PartitionKey
// is accepted only in Development/Test fixtures. Values are never logged or echoed.
public sealed class RateLimitingSettings
{
    public string? Store { get; set; }
    public string? PartitionKeyReference { get; set; }
    public string? PartitionKey { get; set; }
    public int? LocalCapacity { get; set; }
    public int? FallbackSeconds { get; set; }
    public int? CleanupIntervalSeconds { get; set; }
    public int? CleanupBatchSize { get; set; }

    internal bool UsesPostgres => Store switch
    {
        null or "" or "InMemory" => false,
        "Postgres" => true,
        _ => throw new InvalidOperationException("Unknown rate-limit store.")
    };

    internal string? PartitionSecret(IHostEnvironment environment, Func<string, string?> read)
    {
        if (PartitionKeyReference != null)
        {
            if (!string.IsNullOrEmpty(PartitionKey)) throw new InvalidOperationException("Ambiguous rate-limit key configuration.");
            return OperatorSecretReferences.Resolve(PartitionKeyReference, read) ?? throw new InvalidOperationException("Required operator secret is unavailable.");
        }
        if (string.IsNullOrEmpty(PartitionKey)) return null;
        return environment.IsDevelopment() || environment.IsEnvironment("Test") ? PartitionKey
            : throw new InvalidOperationException("Rate-limit partition keys require an operator reference.");
    }
}

public static partial class RateLimitingRuntime
{
    public static void AddSharedRateLimiting(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment) =>
        services.AddSharedRateLimiting(configuration, environment, Environment.GetEnvironmentVariable);

    internal static void AddSharedRateLimiting(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, Func<string, string?> read)
    {
        var settings = configuration.GetSection("RateLimiting").Get<RateLimitingSettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new();
        var postgres = settings.UsesPostgres;
        var secret = settings.PartitionSecret(environment, read);
        if (postgres && secret == null) throw new InvalidOperationException("Shared rate limiting requires operator partition key material.");
        var keys = secret != null ? RateLimitPartitionKeys.FromSecret(secret) : RateLimitPartitionKeys.Ephemeral();
        services.AddSingleton(settings);
        services.AddSingleton(sp => new LocalRateLimitCounters(sp.GetRequiredService<TimeProvider>(),
            Math.Clamp(settings.LocalCapacity ?? LocalRateLimitCounters.DefaultCapacity, 100, 1_000_000)));
        if (postgres)
        {
            var connection = OperatorSecretReferences.Database(configuration, environment, read);
            if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Shared rate limiting requires the database reference.");
            // A dedicated small pool: throttling must not exhaust the business connection pool.
            var builder = new NpgsqlConnectionStringBuilder(connection) { MaxPoolSize = 10, Timeout = 3 };
            services.AddSingleton(_ => NpgsqlDataSource.Create(builder.ConnectionString));
            services.AddSingleton<IRateLimitStore>(sp => new PostgresRateLimitStore(sp.GetRequiredService<NpgsqlDataSource>()));
            services.AddHostedService<RateLimitCounterCleanup>();
        }
        services.AddSingleton(sp => new SharedRateLimiter(keys, sp.GetService<IRateLimitStore>(), sp.GetRequiredService<LocalRateLimitCounters>(),
            sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<SharedRateLimiter>>(),
            TimeSpan.FromSeconds(Math.Clamp(settings.FallbackSeconds ?? 10, 1, 300))));
        services.AddSingleton<InvitationAttemptLimiter>();
        services.AddSingleton<PilotTelemetryRateLimiter>();
        if (!postgres && !environment.IsDevelopment() && !environment.IsEnvironment("Test"))
            services.AddHostedService<ProcessLocalRateLimitNotice>();
    }

    // 429 with Retry-After (delta-seconds) for every limited outcome; the problem body is unchanged.
    public static void RetryAfter(HttpContext http, RateLimitDecision decision) => http.Response.Headers.RetryAfter = decision.RetryAfterHeader;

    [LoggerMessage(EventId = 4291, Level = LogLevel.Warning,
        Message = "Rate limiting is process-local (RateLimiting:Store is InMemory); limits are not shared across API instances and reset on restart.")]
    internal static partial void ProcessLocal(ILogger logger);

    [LoggerMessage(EventId = 4292, Level = LogLevel.Warning, Message = "Rate-limit counter cleanup failed ({ErrorType}); retrying at the next interval.")]
    internal static partial void CleanupFailed(ILogger logger, string errorType);
}

// A non-Development host without a shared store says so once at startup (non-PII, no configuration values).
internal sealed class ProcessLocalRateLimitNotice(ILogger<ProcessLocalRateLimitNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) { RateLimitingRuntime.ProcessLocal(logger); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// Background expiry cleanup in bounded batches (at most 20 batches per tick). Counters are already ignored once their
// window has passed; cleanup only bounds the table size. A failure is logged with its type only and retried next tick.
internal sealed class RateLimitCounterCleanup(IRateLimitStore store, RateLimitingSettings settings, TimeProvider clock, ILogger<RateLimitCounterCleanup> logger)
    : BackgroundService
{
    internal const int MaxBatchesPerTick = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.CleanupIntervalSeconds ?? 300, 10, 86_400));
        var batch = Math.Clamp(settings.CleanupBatchSize ?? 500, 1, 10_000);
        using var timer = new PeriodicTimer(interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunOnceAsync(store, clock, batch, logger, stoppingToken);
    }

    internal static async Task<int> RunOnceAsync(IRateLimitStore store, TimeProvider clock, int batch, ILogger logger, CancellationToken ct)
    {
        var total = 0;
        try
        {
            for (var i = 0; i < MaxBatchesPerTick; i++)
            {
                var deleted = await store.DeleteExpiredAsync(clock.GetUtcNow(), batch, ct);
                total += deleted;
                if (deleted < batch) break;
            }
        }
        catch (Exception error) when (SharedRateLimiter.IsStoreFailure(error, ct)) { RateLimitingRuntime.CleanupFailed(logger, error.GetType().Name); }
        return total;
    }
}
