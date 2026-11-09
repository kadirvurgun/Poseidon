using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Poseidon.Api;

// Operator-selected disposable lab CA. Trust never enters a machine/user store
// and cannot authorize another endpoint, environment, redirect or provider.
public static class DevelopmentCertificateExtractionTransport
{
    public static HttpMessageHandler Create(IHostEnvironment environment,string? endpoint,string? allowedHost,string caPath)
    {
        if(!environment.IsDevelopment() || !Uri.TryCreate(endpoint,UriKind.Absolute,out var target) || target.Scheme!="https" ||
            target.Host!="localhost" || allowedHost!="localhost" || target.AbsolutePath=="/" || target.UserInfo.Length!=0 || target.Query.Length!=0 || target.Fragment.Length!=0)
            throw new InvalidOperationException("Local extraction trust requires an explicit Development localhost HTTPS endpoint.");
        var root=X509CertificateLoader.LoadCertificateFromFile(caPath);
        return new LocalExtraction(target,root){InnerHandler=new SocketsHttpHandler {
            AllowAutoRedirect=false,UseCookies=false,UseProxy=false,AutomaticDecompression=DecompressionMethods.None,ConnectTimeout=TimeSpan.FromSeconds(10),
            // Bind the socket to loopback directly, independently of DNS/hosts.
            ConnectCallback=async(context,ct)=> {
                if(context.DnsEndPoint.Host!="localhost" || context.DnsEndPoint.Port!=target.Port)throw new HttpRequestException("Local extraction target rejected.");
                var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
                try {await socket.ConnectAsync(IPAddress.Loopback,target.Port,ct);return new NetworkStream(socket,true);}
                catch {socket.Dispose();throw;}
            },
            SslOptions=new(){RemoteCertificateValidationCallback=(_,certificate,_,errors)=>
                certificate is X509Certificate2 leaf && DevelopmentOidcTransport.Validate(leaf,errors,root)}
        }};
    }

    private sealed class LocalExtraction(Uri endpoint,X509Certificate2 root):DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if(request.Method!=HttpMethod.Post || request.RequestUri is not {} target ||
                !string.Equals(target.AbsoluteUri,endpoint.AbsoluteUri,StringComparison.Ordinal))
                throw new HttpRequestException("Local extraction target rejected.");
            return base.SendAsync(request,cancellationToken);
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);if(disposing)root.Dispose();
        }
    }
}
