using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// RATE-LIMIT-01: operator configuration of the shared limiter. No host and no database (registration only).
[Trait("Category", "Unit")]
[Trait("Requirement", "RATE-LIMIT-01")]
public sealed class RateLimitingConfigurationTests
{
    private const string Key = "synthetic-only-rate-limit-key-material-0001";

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Poseidon.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider Register(string environment, Dictionary<string, string?> values, Dictionary<string, string?>? secrets = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        services.AddSharedRateLimiting(configuration, new Environment(environment), name => secrets != null && secrets.TryGetValue(name, out var v) ? v : null);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void The_default_is_in_process_and_registers_no_store()
    {
        using var provider = Register("Development", []);
        Assert.False(provider.GetRequiredService<SharedRateLimiter>().Shared);
        Assert.Null(provider.GetService<IRateLimitStore>());
        Assert.NotNull(provider.GetRequiredService<InvitationAttemptLimiter>());
        Assert.NotNull(provider.GetRequiredService<PilotTelemetryRateLimiter>());
    }

    [Fact]
    public void Postgres_uses_the_operator_key_reference_and_the_database_reference()
    {
        using var provider = Register("Production", new() { ["RateLimiting:Store"] = "Postgres", ["RateLimiting:PartitionKeyReference"] = "POSEIDON_RATE_LIMIT_KEY" },
            new() { ["POSEIDON_RATE_LIMIT_KEY"] = Key, ["POSEIDON_DATABASE"] = "Host=127.0.0.1;Database=synthetic_rate_limit;Username=synthetic" });
        Assert.True(provider.GetRequiredService<SharedRateLimiter>().Shared);
        Assert.IsType<PostgresRateLimitStore>(provider.GetRequiredService<IRateLimitStore>());
    }

    [Theory]
    [InlineData("Production", "Postgres", null, null)] // no key material: fail closed at startup
    [InlineData("Production", "Postgres", null, Key)] // literal key outside Development/Test
    [InlineData("Production", "Postgres", "POSEIDON_MISSING_KEY", null)] // reference that resolves to nothing
    [InlineData("Test", "Postgres", "POSEIDON_RATE_LIMIT_KEY", Key)] // ambiguous reference + literal
    [InlineData("Test", "Redis", null, null)] // unknown store
    [InlineData("Test", "Postgres", null, "short")] // weak key material
    public void Invalid_configuration_fails_closed(string environment, string store, string? reference, string? literal)
    {
        var values = new Dictionary<string, string?> { ["RateLimiting:Store"] = store, ["RateLimiting:PartitionKeyReference"] = reference, ["RateLimiting:PartitionKey"] = literal };
        var error = Assert.Throws<InvalidOperationException>(() => Register(environment, values, new() { ["POSEIDON_RATE_LIMIT_KEY"] = Key,
            ["POSEIDON_DATABASE"] = "Host=127.0.0.1;Database=synthetic_rate_limit;Username=synthetic" }));
        Assert.DoesNotContain(Key, error.Message);
    }

    [Fact]
    public void Unknown_settings_are_refused() =>
        Assert.Throws<InvalidOperationException>(() => Register("Test", new() { ["RateLimiting:Unknown"] = "x" }));
}
