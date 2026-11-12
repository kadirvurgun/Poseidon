using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Poseidon.Api;

// Explicit local lab trust, never installed in a machine/user root store.
public static class DevelopmentOidcTransport
{
    public static int ConfiguredPort(IConfiguration configuration)
    {
        var supplied = configuration.GetSection("DevelopmentLab").GetChildren()
            .Any(section => string.Equals(section.Key, "OidcPort", StringComparison.OrdinalIgnoreCase));
        if (!supplied) return 8443;
        var value = configuration["DevelopmentLab:OidcPort"];
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            port is < 1024 or > 65535 || value != port.ToString(CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Local OIDC port selection requires a canonical port from 1024 through 65535.");
        return port;
    }

    public static HttpMessageHandler Create(IHostEnvironment environment, string authority, string caPath, int selectedPort = 8443)
    {
        if (!environment.IsDevelopment() || selectedPort is < 1024 or > 65535 ||
            !Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "localhost" || uri.Port != selectedPort || uri.AbsolutePath != "/realms/poseidon-lab" ||
            !string.Equals(authority, $"https://localhost:{selectedPort}/realms/poseidon-lab", StringComparison.Ordinal))
            throw new InvalidOperationException("Local OIDC trust requires the explicit Development lab authority.");
        var root = X509CertificateLoader.LoadCertificateFromFile(caPath);
        return new LocalBackchannel(root, selectedPort) { InnerHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(10),
            // Bind to loopback independently of DNS, hosts-file or proxy settings.
            // This handler belongs only to the explicitly selected local realm.
            ConnectCallback = async (context, ct) =>
            {
                if (context.DnsEndPoint.Host != "localhost" || context.DnsEndPoint.Port != selectedPort)
                    throw new HttpRequestException("Local OIDC backchannel target rejected.");
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(IPAddress.Loopback, selectedPort, ct); return new NetworkStream(socket, true); }
                catch { socket.Dispose(); throw; }
            },
            SslOptions = new() { RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                certificate is X509Certificate2 leaf && Validate(leaf, errors, root) }
        }};
    }

    private sealed class LocalBackchannel(X509Certificate2 root, int selectedPort) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            if (uri == null || uri.Scheme != "https" || uri.Host != "localhost" || uri.Port != selectedPort ||
                !uri.AbsolutePath.StartsWith("/realms/poseidon-lab/", StringComparison.Ordinal) ||
                uri.AbsolutePath.Contains('%') || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                request.Headers.Host is { } host && !string.Equals(host, $"localhost:{selectedPort}", StringComparison.Ordinal))
                throw new HttpRequestException("Local OIDC backchannel target rejected.");
            return base.SendAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) root.Dispose();
        }
    }

    public static bool Validate(X509Certificate2? certificate, SslPolicyErrors errors, X509Certificate2 root)
    {
        // Hostname and certificate presence are always checked by the platform first.
        if (certificate == null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0) return false;
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Generated local CA has no revocation service.
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.ApplicationPolicy.Add(new("1.3.6.1.5.5.7.3.1"));
        return chain.Build(certificate);
    }
}
