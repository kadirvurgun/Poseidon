using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
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

    // BUNDLE-PORT-ISOLATION-01; authored for the next coherent bundle gate.
    // No provider, current preview or system certificate trust is altered.
    [Fact]
    public void Absent_selected_port_preserves_the_default_and_canonical_explicit_ports_are_supported()
    {
        Assert.Equal(8443, DevelopmentOidcTransport.ConfiguredPort(new ConfigurationBuilder().Build()));
        foreach (var port in new[] { 1024, 8443, 18443, 65535 })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["DevelopmentLab:OidcPort"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture) }).Build();
            Assert.Equal(port, DevelopmentOidcTransport.ConfiguredPort(configuration));
        }
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("0")] [InlineData("1023")] [InlineData("65536")]
    [InlineData("-18443")] [InlineData("+18443")] [InlineData("018443")]
    [InlineData("18443.0")] [InlineData(" 18443")] [InlineData("18443 ")] [InlineData("18443\n")]
    [InlineData("true")] [InlineData("١٨٤٤٣")]
    public void A_supplied_malformed_port_never_falls_back_to_the_default(string? value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["DevelopmentLab:OidcPort"] = value }).Build();
        var failure = Assert.Throws<InvalidOperationException>(() => DevelopmentOidcTransport.ConfiguredPort(configuration));
        Assert.Equal("Local OIDC port selection requires a canonical port from 1024 through 65535.", failure.Message);
    }

    [Theory]
    [InlineData("Production", "https://localhost:18443/realms/poseidon-lab", 18443)]
    [InlineData("Test", "https://localhost:18443/realms/poseidon-lab", 18443)]
    [InlineData("Development", "https://localhost:18443/realms/poseidon-lab", 8443)]
    [InlineData("Development", "https://localhost:8443/realms/poseidon-lab", 18443)]
    [InlineData("Development", "https://localhost:18443/realms/poseidon-lab", 0)]
    [InlineData("Development", "https://localhost:18443/realms/poseidon-lab", 1023)]
    [InlineData("Development", "https://localhost:18443/realms/poseidon-lab", 65536)]
    [InlineData("Development", "http://localhost:18443/realms/poseidon-lab", 18443)]
    [InlineData("Development", "https://127.0.0.1:18443/realms/poseidon-lab", 18443)]
    [InlineData("Development", "https://[::1]:18443/realms/poseidon-lab", 18443)]
    [InlineData("Development", "https://foreign.invalid:18443/realms/poseidon-lab", 18443)]
    [InlineData("Development", "https://localhost:18443/realms/other", 18443)]
    [InlineData("Development", "https://localhost:18443/realms/poseidon-lab?value=1", 18443)]
    [InlineData("Development", "https://localhost:18443/realms/poseidon-lab#value", 18443)]
    [InlineData("Development", "https://operator@localhost:18443/realms/poseidon-lab", 18443)]
    [InlineData("Development", "https://localhost:18443/realms/other/../poseidon-lab", 18443)]
    [InlineData("Development", "https://localhost:18443/realms/%70oseidon-lab", 18443)]
    [InlineData("Development", "https://localhost:18443/realms/poseidon-lab/", 18443)]
    public void Selected_trust_rejects_noncanonical_or_mismatched_authorities_before_loading_a_CA(string environment, string authority, int port)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => DevelopmentOidcTransport.Create(
            new HostingEnvironment { EnvironmentName = environment }, authority, "nonexistent.pem", port));
        Assert.Equal("Local OIDC trust requires the explicit Development lab authority.", failure.Message);
    }

    [Theory]
    [InlineData("https://localhost:8443/realms/poseidon-lab/protocol/openid-connect/token")]
    [InlineData("https://localhost:18444/realms/poseidon-lab/protocol/openid-connect/token")]
    [InlineData("https://127.0.0.1:18443/realms/poseidon-lab/protocol/openid-connect/token")]
    [InlineData("http://localhost:18443/realms/poseidon-lab/protocol/openid-connect/token")]
    [InlineData("https://foreign.invalid:18443/realms/poseidon-lab/protocol/openid-connect/token")]
    [InlineData("https://localhost:18443/realms/other/protocol/openid-connect/token")]
    [InlineData("https://localhost:18443/realms/poseidon-lab/protocol/openid-connect/token?extra=1")]
    [InlineData("https://localhost:18443/realms/poseidon-lab/protocol/openid-connect/token#fragment")]
    [InlineData("https://operator@localhost:18443/realms/poseidon-lab/protocol/openid-connect/token")]
    [InlineData("https://localhost:18443/realms/poseidon-lab/%252e%252e/other/token")]
    public async Task A_selected_handler_cannot_be_reused_for_another_port_or_target(string target)
    {
        using var key = RSA.Create(2048); using var root = Root(key);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, root.ExportCertificatePem(), TestContext.Current.CancellationToken);
            using var client = new HttpClient(DevelopmentOidcTransport.Create(
                new HostingEnvironment { EnvironmentName = "Development" }, "https://localhost:18443/realms/poseidon-lab", path, 18443));
            var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(target, TestContext.Current.CancellationToken));
            Assert.Equal("Local OIDC backchannel target rejected.", failure.Message);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("foreign.invalid")] [InlineData("localhost:8443")] [InlineData("localhost")]
    public async Task A_host_header_cannot_replace_the_selected_TLS_authority(string host)
    {
        using var key = RSA.Create(2048); using var root = Root(key);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, root.ExportCertificatePem(), TestContext.Current.CancellationToken);
            using var client = new HttpClient(DevelopmentOidcTransport.Create(new HostingEnvironment { EnvironmentName = "Development" },
                "https://localhost:18443/realms/poseidon-lab", path, 18443));
            using var message = new HttpRequestMessage(HttpMethod.Get, "https://localhost:18443/realms/poseidon-lab/protocol/openid-connect/certs");
            message.Headers.Host = host;
            var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(message, TestContext.Current.CancellationToken));
            Assert.Equal("Local OIDC backchannel target rejected.", failure.Message);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("valid")] [InlineData("redirect")] [InlineData("wrong-name")]
    [InlineData("unrelated-root")] [InlineData("client-only")] [InlineData("expired")]
    public async Task Selected_loopback_TLS_retains_certificate_and_redirect_boundaries(string scenario)
    {
        using var rootKey = RSA.Create(2048); using var root = Root(rootKey);
        using var otherKey = RSA.Create(2048); using var other = Root(otherKey);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Synthetic OIDC transport", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName(scenario == "wrong-name" ? "foreign.invalid" : "localhost");
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
            { new(scenario == "client-only" ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1") }, true));
        using var issued = request.Create(scenario == "unrelated-root" ? other : root, DateTimeOffset.UtcNow.AddDays(-2),
            scenario == "expired" ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
        using var withKey = issued.CopyWithPrivateKey(key);
        using var certificate = X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.DefaultKeySet);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Assert.InRange(port, 1024, 65535);
        var endpoint = $"https://localhost:{port}/realms/poseidon-lab/.well-known/openid-configuration";
        var peer = Serve(listener, certificate, scenario == "redirect", scenario is not ("valid" or "redirect"), deadline.Token);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, root.ExportCertificatePem(), deadline.Token);
            using var client = new HttpClient(DevelopmentOidcTransport.Create(new HostingEnvironment { EnvironmentName = "Development" },
                $"https://localhost:{port}/realms/poseidon-lab", path, port));
            if (scenario is "valid" or "redirect")
            {
                using var response = await client.GetAsync(endpoint, deadline.Token);
                Assert.Equal(scenario == "redirect" ? HttpStatusCode.Found : HttpStatusCode.OK, response.StatusCode);
                if (scenario == "valid") Assert.Equal("synthetic OIDC transport", await response.Content.ReadAsStringAsync(deadline.Token));
            }
            else await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(endpoint, deadline.Token));
            await peer;
        }
        finally
        {
            deadline.Cancel(); listener.Stop();
            try { await peer; } catch (OperationCanceledException) { }
            File.Delete(path);
        }
    }

    private static async Task Serve(TcpListener listener, X509Certificate2 certificate, bool redirect, bool expectedRejection, CancellationToken ct)
    {
        using var connection = await listener.AcceptTcpClientAsync(ct);
        await using var stream = new SslStream(connection.GetStream());
        try
        {
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                { ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, ct);
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            var size = 0;
            while (await reader.ReadLineAsync(ct) is { } line && line.Length != 0)
            {
                size += line.Length;
                if (size > 4096) throw new InvalidDataException("synthetic_headers_too_large");
            }
            const string body = "synthetic OIDC transport";
            var response = redirect ? "HTTP/1.1 302 Found\r\nLocation: https://foreign.invalid/realms/poseidon-lab/token\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                : $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), ct); await stream.FlushAsync(ct);
        }
        catch (Exception error) when (expectedRejection && error is AuthenticationException or IOException) { }
    }

    private static ServiceProvider Identity(IConfiguration configuration, string environment = "Development")
    {
        var services = new ServiceCollection();
        var host = new HostingEnvironment { EnvironmentName = environment };
        services.AddSingleton<IConfiguration>(configuration); services.AddSingleton<IHostEnvironment>(host);
        services.AddLogging(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddFoundationIdentity(configuration, host);
        return services.BuildServiceProvider();
    }

    private static IConfiguration IdentityConfiguration(string authority, string issuer, string? port, string? ca, bool includePort = true)
    {
        var values = new Dictionary<string, string?> { ["Authentication:Oidc:Authority"] = authority,
            ["Authentication:Oidc:Issuer"] = issuer, ["Authentication:Oidc:ClientId"] = "poseidon-local",
            ["DevelopmentLab:OidcCaPath"] = ca };
        if (includePort) values["DevelopmentLab:OidcPort"] = port;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Theory]
    [InlineData("Production", "18443", "nonexistent.pem")]
    [InlineData("Test", "18443", "nonexistent.pem")]
    [InlineData("Development", "18443", null)]
    [InlineData("Development", "8443", "")]
    [InlineData("Development", "+18443", "nonexistent.pem")]
    public void Identity_registration_cannot_select_a_port_without_explicit_development_CA_trust(string environment, string port, string? ca)
    {
        Assert.Throws<InvalidOperationException>(() => Identity(IdentityConfiguration(
            "https://localhost:18443/realms/poseidon-lab", "https://localhost:18443/realms/poseidon-lab", port, ca), environment));
    }

    [Fact]
    public void Identity_registration_rejects_a_local_authority_and_issuer_mismatch_before_loading_a_CA()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => Identity(IdentityConfiguration(
            "https://localhost:18443/realms/poseidon-lab", "https://localhost:8443/realms/poseidon-lab", "18443", "nonexistent.pem")));
        Assert.Equal("Local OIDC trust requires a matching configured authority and issuer.", failure.Message);
    }

    [Theory]
    [InlineData(8443, false)] [InlineData(18443, true)]
    public async Task Identity_options_keep_the_selected_issuer_code_PKCE_and_full_token_validation(int port, bool includePort)
    {
        using var key = RSA.Create(2048); using var root = Root(key);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, root.ExportCertificatePem(), TestContext.Current.CancellationToken);
            var authority = $"https://localhost:{port}/realms/poseidon-lab";
            using var provider = Identity(IdentityConfiguration(authority, authority, port.ToString(System.Globalization.CultureInfo.InvariantCulture), path, includePort));
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(FoundationIdentity.Oidc);
            try
            {
                Assert.Equal(authority, options.Authority); Assert.Equal(authority, options.TokenValidationParameters.ValidIssuer);
                Assert.NotNull(options.BackchannelHttpHandler); Assert.Equal("code", options.ResponseType);
                Assert.True(options.UsePkce); Assert.True(options.RequireHttpsMetadata); Assert.False(options.SaveTokens);
                Assert.True(options.TokenValidationParameters.ValidateIssuer); Assert.True(options.TokenValidationParameters.ValidateAudience);
                Assert.True(options.TokenValidationParameters.ValidateLifetime); Assert.True(options.TokenValidationParameters.RequireExpirationTime);
                Assert.True(options.TokenValidationParameters.RequireSignedTokens); Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
                Assert.Equal("/signin-oidc", options.CallbackPath.Value);
                Assert.Equal("/signout-callback-oidc", options.SignedOutCallbackPath.Value);
            }
            finally { options.Backchannel.Dispose(); }
        }
        finally { File.Delete(path); }
    }
}
