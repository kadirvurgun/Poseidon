using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Poseidon.Api;

// Explicit local lab trust, never installed in a machine/user root store.
public static class DevelopmentOidcTransport
{
    public static HttpMessageHandler Create(IHostEnvironment environment, string authority, string caPath)
    {
        if (!environment.IsDevelopment() || !Uri.TryCreate(authority, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.Host != "localhost" || uri.Port != 8443 ||
            uri.AbsolutePath != "/realms/poseidon-lab" || uri.Query.Length != 0 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Local OIDC trust requires the explicit Development lab authority.");
        var root = X509CertificateLoader.LoadCertificateFromFile(caPath);
        return new LocalBackchannel(root) { InnerHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
                request.RequestUri?.Scheme == "https" && request.RequestUri.Host == "localhost" && request.RequestUri.Port == 8443 &&
                Validate(certificate, errors, root)
        }};
    }

    private sealed class LocalBackchannel(X509Certificate2 root) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            if (uri == null || uri.Scheme != "https" || uri.Host != "localhost" || uri.Port != 8443 ||
                !uri.AbsolutePath.StartsWith("/realms/poseidon-lab/", StringComparison.Ordinal) || uri.UserInfo.Length != 0)
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
