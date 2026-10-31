using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class SecurityEventWriterValidationTests
{
    [Fact]
    public async Task Append_rejects_unrecognized_action_or_result_before_database_access()
    {
        var options = new DbContextOptionsBuilder<SecurityEventWriter>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Timeout=1").Options;
        await using var writer = new SecurityEventWriter(options);

        var actionError = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.AppendAsync(Event("ArbitraryAction", "Denied"), TestContext.Current.CancellationToken));
        var resultError = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.AppendAsync(Event("AccessDenied", "arbitrary_result"), TestContext.Current.CancellationToken));
        Assert.Equal("Unknown security event code.", actionError.Message);
        Assert.Equal("Unknown security event code.", resultError.Message);
    }

    private static SecurityEvent Event(string action, string result) => new()
    {
        Id = Guid.NewGuid(), Action = action, Result = result,
        CorrelationId = "synthetic-correlation", OccurredAt = DateTimeOffset.UtcNow
    };
}
