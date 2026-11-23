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
    // INVITE-RATELIMIT-FOLLOWUPS: sign-in starts per client network per 5 minutes (default 60, 10-100000). Operators behind
    // one shared egress (a vessel's satellite NAT) or a reverse proxy without forwarded-header processing raise it.
    public int? LoginPermitLimit { get; set; }

    // QA L4: out-of-range numeric settings stop startup instead of being clamped silently.
    internal int Capacity => Checked(LocalCapacity, LocalRateLimitCounters.DefaultCapacity, 100, 1_000_000);
    internal TimeSpan Fallback => TimeSpan.FromSeconds(Checked(FallbackSeconds, 10, 1, 300));
    internal TimeSpan CleanupInterval => TimeSpan.FromSeconds(Checked(CleanupIntervalSeconds, 300, 10, 86_400));
    internal int CleanupBatch => Checked(CleanupBatchSize, 500, 1, 10_000);
    internal int LoginLimit => Checked(LoginPermitLimit, LoginRateLimiter.DefaultPermitLimit, 10, 100_000);

    private static int Checked(int? value, int fallback, int min, int max) =>
        value is null ? fallback : value >= min && value <= max ? value.Value : throw new InvalidOperationException("Rate-limit setting out of range.");

    internal void Validate() { _ = Capacity; _ = Fallback; _ = CleanupInterval; _ = CleanupBatch; _ = LoginLimit; _ = UsesPostgres; }

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
        settings.Validate();
        var postgres = settings.UsesPostgres;
        var secret = settings.PartitionSecret(environment, read);
        if (postgres && secret == null) throw new InvalidOperationException("Shared rate limiting requires operator partition key material.");
        var keys = secret != null ? RateLimitPartitionKeys.FromSecret(secret) : RateLimitPartitionKeys.Ephemeral();
        services.AddSingleton(settings);
        services.AddSingleton(sp => new LocalRateLimitCounters(sp.GetRequiredService<TimeProvider>(), settings.Capacity));
        if (postgres)
        {
            var connection = OperatorSecretReferences.Database(configuration, environment, read);
            if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Shared rate limiting requires the database reference.");
            // A dedicated small pool: throttling must not exhaust the business connection pool.
            var builder = new NpgsqlConnectionStringBuilder(connection) { MaxPoolSize = 10, Timeout = 3 };
            // Own wrapper type, never a bare NpgsqlDataSource registration (QA N1).
            services.AddSingleton(_ => new RateLimitDataSource(NpgsqlDataSource.Create(builder.ConnectionString)));
            services.AddSingleton<IRateLimitStore>(sp => new PostgresRateLimitStore(sp.GetRequiredService<RateLimitDataSource>()));
            services.AddHostedService<RateLimitCounterCleanup>();
        }
        services.AddSingleton(sp => new SharedRateLimiter(keys, sp.GetService<IRateLimitStore>(), sp.GetRequiredService<LocalRateLimitCounters>(),
            sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<SharedRateLimiter>>(),
            settings.Fallback));
        services.AddSingleton<InvitationAttemptLimiter>();
        services.AddSingleton<PilotTelemetryRateLimiter>();
        services.AddSingleton<InvitationIssueRateLimiter>();
        services.AddSingleton(sp => new LoginRateLimiter(sp.GetRequiredService<SharedRateLimiter>(), settings.LoginLimit));
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

// INVITE-RATELIMIT-FOLLOWUPS: invitation create and reissue share one technical budget per (tenant, actor): each issues a
// live single-use token and may queue an e-mail, so a compromised or scripted administrator session cannot mint tokens or
// mail without bound. Not an approved quota. The partition is a keyed HMAC of the ids (RATE-LIMIT-01), never the ids.
public sealed class InvitationIssueRateLimiter(SharedRateLimiter limiter)
{
    public const int IssuesPerWindow = 60;
    public static readonly RateLimitPolicy Policy = new("invitation.issue", IssuesPerWindow, TimeSpan.FromMinutes(15));

    public Task<RateLimitDecision> AcquireAsync(Guid tenant, Guid actor, CancellationToken cancellationToken) =>
        limiter.AcquireAsync(Policy, [tenant.ToString("D"), actor.ToString("D")], cancellationToken);
}

// INVITE-RATELIMIT-FOLLOWUPS: GET /api/v1/auth/login (anonymous OIDC challenge: correlation/nonce cookies, a SecurityEvent
// and an IdP redirect per call) is bounded per client network over a fixed 5-minute window. Credentials are checked by
// the IdP, which owns password brute-force protection and account lockout; this guard bounds challenge/audit
// amplification. Partition: IPv4 address, or the IPv6 /64 (one subscriber prefix), keyed-HMAC'd like every partition
// (the address is personal data and is never stored). The client address is the connection's remote address; behind a
// reverse proxy the operator must enable forwarded-header processing (ASPNETCORE_FORWARDEDHEADERS_ENABLED or equivalent
// trusted-proxy configuration), otherwise all callers share the proxy's partition.
public sealed class LoginRateLimiter(SharedRateLimiter limiter, int permitLimit)
{
    public const int DefaultPermitLimit = 60;
    public RateLimitPolicy Policy { get; } = new("auth.login", permitLimit, TimeSpan.FromMinutes(5));

    public Task<RateLimitDecision> AcquireAsync(System.Net.IPAddress? remote, CancellationToken cancellationToken) =>
        limiter.AcquireAsync(Policy, [Network(remote)], cancellationToken);

    internal static string Network(System.Net.IPAddress? address)
    {
        if (address == null) return "none";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) return "v4:" + address;
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return "v6:" + new System.Net.IPAddress(bytes) + "/64";
    }
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
        using var timer = new PeriodicTimer(settings.CleanupInterval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunOnceAsync(store, settings.CleanupBatch, logger, stoppingToken);
    }

    // Expiry is judged by the database clock inside the store (QA L1); the timer clock only paces the ticks.
    internal static async Task<int> RunOnceAsync(IRateLimitStore store, int batch, ILogger logger, CancellationToken ct)
    {
        var total = 0;
        try
        {
            for (var i = 0; i < MaxBatchesPerTick; i++)
            {
                var deleted = await store.DeleteExpiredAsync(batch, ct);
                total += deleted;
                if (deleted < batch) break;
            }
        }
        catch (Exception error) when (SharedRateLimiter.IsStoreFailure(error, ct)) { RateLimitingRuntime.CleanupFailed(logger, error.GetType().Name); }
        return total;
    }
}
