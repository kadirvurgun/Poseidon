using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class PostgresFailureTests
{
    [Theory]
    [InlineData("40001", true)]
    [InlineData("40P01", true)]
    [InlineData("23503", false)]
    [InlineData("22001", false)]
    public void Execution_strategy_wrappers_preserve_exact_provider_classification(string state, bool serialization)
    {
        var provider = new PostgresException("synthetic provider failure", "ERROR", "ERROR", state);
        var wrapped = new InvalidOperationException("synthetic strategy wrapper", new DbUpdateException("synthetic save", provider));
        Assert.Same(provider, PostgresFailure.Provider(wrapped));
        Assert.Equal(serialization, PostgresFailure.IsSerialization(wrapped));
        Assert.Equal(serialization, PostgresFailure.IsDatabase(wrapped));
        Assert.True(PostgresFailure.IsDatabase(wrapped.InnerException!));
        Assert.False(PostgresFailure.IsDatabase(new InvalidOperationException("40001 in arbitrary text is not a provider failure")));
    }
}
