using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed class SecurityAudit(IConfiguration configuration, ActorResolver actors, ILogger<SecurityAudit> logger, IHostEnvironment environment)
{
    // attributedTenant/attributedActor (USER-ADMIN-02): server-derived attribution for a caller who has no trusted actor yet
    // (an invitation accepted by its identity holder). Used only when the principal resolves to no actor; never request input.
    public async Task WriteAsync(HttpContext http, string action, string result, ClaimsPrincipal? principal = null, Guid? resourceId = null,
        Guid? attributedTenant = null, Guid? attributedActor = null)
    {
        var connection = OperatorSecretReferences.Database(configuration, environment);
        if (string.IsNullOrWhiteSpace(connection)) { AuditTelemetry.Record(false, TimeSpan.Zero); throw new AuditUnavailableException(); }
        var actor = actors.Resolve(principal ?? http.User);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var persisted = false;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var writer = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(connection).Options);
            await writer.AppendAsync(new SecurityEvent
            {
                Id = Guid.NewGuid(), TenantId = actor?.TenantId ?? attributedTenant, ActorId = actor?.Id.ToString() ?? attributedActor?.ToString(),
                ResourceId = resourceId,
                Action = action, Result = result, CorrelationId = http.TraceIdentifier, OccurredAt = PoseidonDbContext.AuditRecordingClock.GetUtcNow()
            }, deadline.Token);
            persisted = true;
        }
        catch (Exception) { throw new AuditUnavailableException(); }
        finally { AuditTelemetry.Record(persisted, System.Diagnostics.Stopwatch.GetElapsedTime(started), actor?.TenantId); }
    }

    public async Task TryWriteAsync(HttpContext http, string action, string result)
    {
        try { await WriteAsync(http, action, result); }
        catch (AuditUnavailableException)
        {
            // Fixed message only: never include DB errors, request URL or identity data.
            logger.LogError("Security event persistence unavailable. Correlation {CorrelationId}", http.TraceIdentifier);
        }
    }
}

public sealed class AuditUnavailableException : Exception;
