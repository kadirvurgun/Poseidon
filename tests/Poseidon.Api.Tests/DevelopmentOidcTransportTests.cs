using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting.Internal;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class DevelopmentOidcTransportTests
{
    [Theory]
    [InlineData("http://localhost:8443/realms/poseidon-lab/token")]
    [InlineData("https://example.com:8443/realms/poseidon-lab/token")]
    [InlineData("https://localhost:8444/realms/poseidon-lab/token")]
    [InlineData("https://localhost:8443/realms/foreign/token")]
    public async Task Metadata_cannot_send_backchannel_requests_outside_the_local_realm(string target)
    {
        using var key = RSA.Create(2048);
        using var root = Root(key);
        var file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, root.ExportCertificatePem(), TestContext.Current.CancellationToken);
            using var client = new HttpClient(DevelopmentOidcTransport.Create(
                new HostingEnvironment { EnvironmentName = "Development" }, "https://localhost:8443/realms/poseidon-lab", file));
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(target, TestContext.Current.CancellationToken));
            Assert.Equal("Local OIDC backchannel target rejected.", exception.Message);
        }
        finally { File.Delete(file); }
    }

    private static X509Certificate2 Root(RSA key)
    {
        var request = new CertificateRequest("CN=Synthetic lab", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(3));
    }

    [Fact]
    public void Explicit_root_does_not_accept_wrong_names_expired_certificates_or_unrelated_roots()
    {
        using var rootKey = RSA.Create(2048);
        using var root = Root(rootKey);
        using var otherKey = RSA.Create(2048);
        using var other = Root(otherKey);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var leaf = request.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
        using var expired = request.Create(root, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1), RandomNumberGenerator.GetBytes(16));
        Assert.True(DevelopmentOidcTransport.Validate(leaf, SslPolicyErrors.RemoteCertificateChainErrors, root));
        Assert.False(DevelopmentOidcTransport.Validate(leaf, SslPolicyErrors.RemoteCertificateNameMismatch, root));
        Assert.False(DevelopmentOidcTransport.Validate(null, SslPolicyErrors.RemoteCertificateNotAvailable, root));
        Assert.False(DevelopmentOidcTransport.Validate(expired, SslPolicyErrors.RemoteCertificateChainErrors, root));
        Assert.False(DevelopmentOidcTransport.Validate(leaf, SslPolicyErrors.RemoteCertificateChainErrors, other));
    }

    [Theory]
    [InlineData("Production", "https://localhost:8443/realms/poseidon-lab")]
    [InlineData("Test", "https://localhost:8443/realms/poseidon-lab")]
    [InlineData("Development", "http://localhost:8443/realms/poseidon-lab")]
    [InlineData("Development", "https://example.com:8443/realms/poseidon-lab")]
    [InlineData("Development", "https://localhost:8444/realms/poseidon-lab")]
    [InlineData("Development", "https://localhost:8443/realms/foreign")]
    public void Scoped_trust_cannot_be_enabled_outside_explicit_local_development(string environment, string authority)
    {
        Assert.Throws<InvalidOperationException>(() => DevelopmentOidcTransport.Create(
            new HostingEnvironment { EnvironmentName = environment }, authority, "nonexistent.pem"));
    }
}
