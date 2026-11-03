using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class FoundationIdentitySecretTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Registered_oidc_options_use_only_the_explicit_operator_reference_and_keep_protocol_guards(string environment)
    {
        using var secret = new SyntheticReference();
        var first = string.Concat("synthetic", "-registered-first-value");
        secret.Set(first);
        var configuration = Configuration(secret.Reference);
        using var provider = Register(configuration, environment).BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(FoundationIdentity.Oidc);
        Assert.Equal(first, options.ClientSecret);
        Assert.Equal(first, provider.GetRequiredService<FederationSettings>().ClientSecret);
        Assert.True(options.UsePkce);
        Assert.True(options.RequireHttpsMetadata);
        Assert.False(options.SaveTokens);
        Assert.False(options.MapInboundClaims);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.True(options.TokenValidationParameters.RequireExpirationTime);
        Assert.True(options.TokenValidationParameters.RequireSignedTokens);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);

        // OIDC options are a startup snapshot; a deliberate new registration consumes an operator rotation.
        var replacement = string.Concat("synthetic", "-registered-replacement-value");
        secret.Set(replacement);
        Assert.Equal(first, options.ClientSecret);
        using var rotated = Register(configuration, environment).BuildServiceProvider();
        Assert.Equal(replacement, rotated.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(FoundationIdentity.Oidc).ClientSecret);
        secret.Set(null);
        var error = Assert.Throws<InvalidOperationException>(() => Register(configuration, environment));
        Assert.Equal("Required operator secret is unavailable.", error.Message);
        Assert.DoesNotContain(secret.Reference, error.ToString());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Registration_rejects_production_literals_even_when_federation_is_not_configured(string environment)
    {
        var synthetic = string.Concat("synthetic", "-registration-literal");
        foreach (var configured in new[] { false, true })
        {
            var configuration = Configuration(literal: synthetic, configured: configured);
            var error = Assert.Throws<InvalidOperationException>(() => Register(configuration, environment));
            Assert.Equal("OIDC client secrets require an operator reference.", error.Message);
            Assert.DoesNotContain(synthetic, error.ToString());
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public void Isolated_fixture_registration_retains_explicit_legacy_literal_support(string environment)
    {
        var synthetic = string.Concat("synthetic", "-fixture-client-value");
        using var provider = Register(Configuration(literal: synthetic), environment).BuildServiceProvider();
        Assert.Equal(synthetic, provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(FoundationIdentity.Oidc).ClientSecret);
    }

    [Fact]
    public async Task Public_client_registration_keeps_code_pkce_without_a_secret_and_unconfigured_identity_has_no_oidc_scheme()
    {
        using var provider = Register(Configuration(), "Production").BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(FoundationIdentity.Oidc);
        Assert.Null(options.ClientSecret);
        Assert.Equal("code", options.ResponseType);
        Assert.True(options.UsePkce);
        using var unconfigured = Register(Configuration(configured: false), "Production").BuildServiceProvider();
        Assert.Null(await unconfigured.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync(FoundationIdentity.Oidc));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../private")]
    [InlineData("lowercase")]
    public void Registration_rejects_malformed_references_in_every_environment(string reference)
    {
        foreach (var environment in new[] { "Production", "Development", "Test" })
        {
            var error = Assert.Throws<InvalidOperationException>(() => Register(Configuration(reference), environment));
            Assert.Equal("Invalid operator secret reference.", error.Message);
        }
    }

    [Fact]
    public void Registration_rejects_missing_and_ambiguous_references_without_exposing_configuration()
    {
        using var secret = new SyntheticReference();
        var missing = Assert.Throws<InvalidOperationException>(() => Register(Configuration(secret.Reference), "Production"));
        Assert.Equal("Required operator secret is unavailable.", missing.Message);
        var synthetic = string.Concat("synthetic", "-ambiguous-client-value");
        secret.Set(synthetic);
        foreach (var environment in new[] { "Production", "Development", "Test" })
        {
            var error = Assert.Throws<InvalidOperationException>(() => Register(Configuration(secret.Reference, synthetic), environment));
            Assert.Equal("Ambiguous operator secret configuration.", error.Message);
            Assert.DoesNotContain(secret.Reference, error.ToString());
            Assert.DoesNotContain(synthetic, error.ToString());
        }
    }

    private static ServiceCollection Register(IConfiguration configuration, string environment)
    {
        var services = new ServiceCollection();
        var host = new HostingEnvironment { EnvironmentName = environment };
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(host);
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddFoundationIdentity(configuration, host);
        return services;
    }

    private static IConfiguration Configuration(string? reference = null, string? literal = null, bool configured = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Oidc:Authority"] = configured ? "https://synthetic.example" : null,
            ["Authentication:Oidc:Issuer"] = configured ? "https://synthetic.example" : null,
            ["Authentication:Oidc:ClientId"] = configured ? "synthetic-public-client" : null,
            ["Authentication:Oidc:ClientSecretReference"] = reference,
            ["Authentication:Oidc:ClientSecret"] = literal
        }).Build();

    // Unique per test: parallel cases cannot replace each other's process environment references.
    private sealed class SyntheticReference : IDisposable
    {
        public string Reference { get; } = "POSEIDON_TEST_OIDC_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        private readonly string? original;
        public SyntheticReference() { original = Environment.GetEnvironmentVariable(Reference); }
        public void Set(string? value) => Environment.SetEnvironmentVariable(Reference, value);
        public void Dispose() => Environment.SetEnvironmentVariable(Reference, original);
    }
}
