using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence.Tests;

public sealed class SecurityEventWriterValidationTests
{
    [Fact]
    public async Task Append_rejects_unrecognized_action_or_result_before_database_access()
    {
        var options = new DbContextOptionsBuilder<SecurityEventWriter>().Options;
        await using var writer = new SecurityEventWriter(options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.AppendAsync(Event("ArbitraryAction", "Denied")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.AppendAsync(Event("AccessDenied", "arbitrary_result")));
    }

    private static SecurityEvent Event(string action, string result) => new()
    {
        Id = Guid.NewGuid(), Action = action, Result = result,
        CorrelationId = "synthetic-correlation", OccurredAt = DateTimeOffset.UtcNow
    };
}
