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

    // PLAN-1511-5802 QA F5: guard-trigger state rules (55000/23514) are conflicts, not outages.
    [Theory]
    [InlineData("55000", true, false)]
    [InlineData("23514", true, false)]
    [InlineData("23505", false, true)]
    [InlineData("40001", false, true)]
    [InlineData("40P01", false, true)]
    [InlineData("08006", false, false)]
    public void State_rule_and_conflict_classification_reads_wrapped_provider_codes(string state, bool stateRule, bool conflict)
    {
        var wrapped = new InvalidOperationException("synthetic strategy wrapper",
            new DbUpdateException("synthetic save", new PostgresException("synthetic provider failure", "ERROR", "ERROR", state)));
        Assert.Equal(stateRule, PostgresFailure.IsStateRule(wrapped));
        Assert.Equal(conflict, PostgresFailure.IsConflict(wrapped));
        Assert.False(PostgresFailure.IsStateRule(new InvalidOperationException("55000 in arbitrary text")));
    }
}
