using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class S3ConfigurationTests
{
    [Fact]
    public void Explicit_references_owned_loopback_and_production_TLS_are_required_without_network_discovery()
    {
        var access = "POSEIDON_FSC_ACCESS_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var secret = "POSEIDON_FSC_SECRET_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var environment = new EnvironmentFixture();
        var settings = new S3ObjectSettings { Endpoint = "http://127.0.0.1:9000", Bucket = "synthetic-owned", OwnedLoopback = true,
            AccessKeyReference = access, SecretKeyReference = secret };
        // Ambient credentials are never needed/resolved; absent selected references are not ready.
        Assert.False(settings.Valid(environment));
        try {
            Environment.SetEnvironmentVariable(access, string.Concat("synthetic-", Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable(secret, string.Concat("synthetic-", Guid.NewGuid().ToString("N")));
            Assert.True(settings.Valid(environment));
            environment.EnvironmentName = Environments.Production; Assert.False(settings.Valid(environment));
            settings.OwnedLoopback = false; Assert.False(settings.Valid(environment));
            settings.Endpoint = "https://storage.example.invalid"; Assert.True(settings.Valid(environment));
            foreach (var url in new[] { "http://storage.example.invalid", "https://storage.example.invalid/private", "https://storage.example.invalid/?token=x", "https://user@storage.example.invalid", "https://127.0.0.1" }) {
                settings.Endpoint = url; Assert.False(settings.Valid(environment));
            }
            settings.Endpoint = "https://storage.example.invalid"; settings.AccessKeyReference = "../unsafe-reference";
            Assert.False(settings.Valid(environment));
        } finally { Environment.SetEnvironmentVariable(access, null); Environment.SetEnvironmentVariable(secret, null); }
    }
    private sealed class EnvironmentFixture : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "configuration-only";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
