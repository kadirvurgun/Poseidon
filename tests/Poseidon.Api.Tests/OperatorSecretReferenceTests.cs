using Poseidon.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class OperatorSecretReferenceTests
{
    [Fact]
    public void Only_bounded_operator_environment_references_are_resolved_and_missing_values_stay_missing()
    {
        var called = new List<string>(); var synthetic = string.Concat("synthetic", "-operator-value");
        Assert.Equal(synthetic, OperatorSecretReferences.Resolve("POSEIDON_TEST_REFERENCE", name => { called.Add(name); return synthetic; }));
        Assert.Single(called); Assert.Null(OperatorSecretReferences.Resolve("POSEIDON_TEST_REFERENCE", _ => null));
        foreach (var invalid in new[] { "", "../private", "https://remote", "ENV\n", "lowercase", new string('A', 81) })
        {
            var error = Assert.Throws<InvalidOperationException>(() => OperatorSecretReferences.Resolve(invalid, _ => throw new Exception("Must not resolve")));
            Assert.Equal("Invalid operator secret reference.", error.Message); Assert.DoesNotContain(synthetic, error.Message);
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("DevelopmentCopy")]
    [InlineData("Tests")]
    public void Non_fixture_environments_deny_literal_oidc_and_database_secrets(string environment)
    {
        var host = new HostingEnvironment { EnvironmentName = environment };
        var synthetic = string.Concat("synthetic", "-literal-value");
        var settings = new FederationSettings { ClientSecret = synthetic };
        var error = Assert.Throws<InvalidOperationException>(() => OperatorSecretReferences.Oidc(settings, host, _ => throw new Exception("Must not resolve")));
        Assert.Equal("OIDC client secrets require an operator reference.", error.Message);
        Assert.DoesNotContain(synthetic, error.ToString());
        Assert.Null(OperatorSecretReferences.Database(Configuration(new()
        {
            ["ConnectionStrings:Poseidon"] = synthetic, ["POSEIDON_DATABASE"] = synthetic
        }), host, _ => null));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public void Only_explicit_fixture_environments_allow_legacy_literals_without_a_reference(string environment)
    {
        var host = new HostingEnvironment { EnvironmentName = environment };
        var synthetic = string.Concat("synthetic", "-fixture-value");
        Assert.Equal(synthetic, OperatorSecretReferences.Oidc(new() { ClientSecret = synthetic }, host, _ => throw new Exception("Must not resolve")));
        Assert.Equal(synthetic, OperatorSecretReferences.Database(Configuration(new() { ["ConnectionStrings:Poseidon"] = synthetic }), host, _ => null));
        Assert.Equal(synthetic, OperatorSecretReferences.Database(Configuration(new() { ["POSEIDON_DATABASE"] = synthetic }), host, _ => null));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Test")]
    public void An_explicit_missing_reference_never_falls_back_to_any_literal(string environment)
    {
        var host = new HostingEnvironment { EnvironmentName = environment };
        var synthetic = string.Concat("synthetic", "-fallback-forbidden");
        Assert.Null(OperatorSecretReferences.Database(Configuration(new()
        {
            ["SecretReferences:Database"] = "POSEIDON_EXPLICIT_MISSING",
            ["ConnectionStrings:Poseidon"] = synthetic, ["POSEIDON_DATABASE"] = synthetic
        }), host, _ => null));
        var settings = new FederationSettings { ClientSecretReference = "POSEIDON_EXPLICIT_MISSING" };
        var error = Assert.Throws<InvalidOperationException>(() => OperatorSecretReferences.Oidc(settings, host, _ => " \t"));
        Assert.Equal("Required operator secret is unavailable.", error.Message);
        Assert.DoesNotContain(settings.ClientSecretReference, error.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("../private")]
    [InlineData("https://remote")]
    [InlineData("ENV\n")]
    [InlineData("lowercase")]
    public void An_explicit_malformed_reference_denies_before_any_provider_or_fixture_fallback(string reference)
    {
        var host = new HostingEnvironment { EnvironmentName = "Test" };
        var configuration = Configuration(new()
        {
            ["SecretReferences:Database"] = reference,
            ["ConnectionStrings:Poseidon"] = string.Concat("synthetic", "-fallback-forbidden")
        });
        var database = Assert.Throws<InvalidOperationException>(() => OperatorSecretReferences.Database(configuration, host, _ => throw new Exception("Must not resolve")));
        var oidc = Assert.Throws<InvalidOperationException>(() => OperatorSecretReferences.Oidc(new() { ClientSecretReference = reference }, host, _ => throw new Exception("Must not resolve")));
        Assert.Equal("Invalid operator secret reference.", database.Message);
        Assert.Equal(database.Message, oidc.Message);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Test")]
    public void Oidc_literal_and_reference_ambiguity_is_rejected_before_reading_either_provider(string environment)
    {
        var synthetic = string.Concat("synthetic", "-conflicting-value");
        var settings = new FederationSettings { ClientSecret = synthetic, ClientSecretReference = "POSEIDON_EXPLICIT_REFERENCE" };
        var error = Assert.Throws<InvalidOperationException>(() => OperatorSecretReferences.Oidc(settings,
            new HostingEnvironment { EnvironmentName = environment }, _ => throw new Exception("Must not resolve")));
        Assert.Equal("Ambiguous operator secret configuration.", error.Message);
        Assert.DoesNotContain(synthetic, error.ToString());
        Assert.DoesNotContain(settings.ClientSecretReference, error.ToString());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Test")]
    public void Public_oidc_clients_do_not_implicitly_resolve_credentials(string environment)
    {
        var host = new HostingEnvironment { EnvironmentName = environment };
        Assert.Null(OperatorSecretReferences.Oidc(new(), host, _ => throw new Exception("Must not resolve")));
        Assert.Null(OperatorSecretReferences.Oidc(new() { ClientSecret = "" }, host, _ => throw new Exception("Must not resolve")));
    }

    [Fact]
    public void Only_the_configured_reference_is_read_and_replacement_is_not_hidden_by_cached_or_default_values()
    {
        var host = new HostingEnvironment { EnvironmentName = "Production" };
        var settings = new FederationSettings { ClientSecretReference = "POSEIDON_EXPLICIT_REFERENCE" };
        var configuration = Configuration(new() { ["SecretReferences:Database"] = settings.ClientSecretReference });
        var requested = new List<string>();
        var synthetic = string.Concat("synthetic", "-first-reference-value");
        string? Read(string name) { requested.Add(name); return synthetic; }
        Assert.Equal(synthetic, OperatorSecretReferences.Oidc(settings, host, Read));
        Assert.Equal(synthetic, OperatorSecretReferences.Database(configuration, host, Read));
        synthetic = string.Concat("synthetic", "-replacement-reference-value");
        Assert.Equal(synthetic, OperatorSecretReferences.Oidc(settings, host, Read));
        Assert.Equal(synthetic, OperatorSecretReferences.Database(configuration, host, Read));
        Assert.Equal(4, requested.Count);
        Assert.All(requested, name => Assert.Equal(settings.ClientSecretReference, name));
    }

    [Fact]
    public void Provider_failures_never_retain_reference_secret_or_provider_exception_details()
    {
        var reference = "POSEIDON_TEST_PRIVATE_REFERENCE";
        var synthetic = string.Concat("synthetic", "-provider-private-value");
        var error = Assert.Throws<InvalidOperationException>(() => OperatorSecretReferences.Resolve(reference,
            _ => throw new InvalidOperationException(reference + synthetic)));
        Assert.Equal("Required operator secret is unavailable.", error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(reference, error.ToString());
        Assert.DoesNotContain(synthetic, error.ToString());
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
