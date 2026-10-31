using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed class SecurityAudit(IConfiguration configuration, ActorResolver actors, ILogger<SecurityAudit> logger)
{
    public async Task WriteAsync(HttpContext http, string action, string result, ClaimsPrincipal? principal = null, Guid? resourceId = null)
    {
        var connection = configuration.GetConnectionString("Poseidon") ?? configuration["POSEIDON_DATABASE"];
        if (string.IsNullOrWhiteSpace(connection)) throw new AuditUnavailableException();
        var actor = actors.Resolve(principal ?? http.User);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var writer = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(connection).Options);
            await writer.AppendAsync(new SecurityEvent
            {
                Id = Guid.NewGuid(), TenantId = actor?.TenantId, ActorId = actor?.Id.ToString(),
                ResourceId = resourceId,
                Action = action, Result = result, CorrelationId = http.TraceIdentifier, OccurredAt = DateTimeOffset.UtcNow
            }, deadline.Token);
        }
        catch (Exception) { throw new AuditUnavailableException(); }
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
