using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class DevelopmentCertificateExtractionTransportTests
{
    [Theory]
    [InlineData("Production","https://localhost:18443/v1/extract","localhost")]
    [InlineData("Test","https://localhost:18443/v1/extract","localhost")]
    [InlineData("Development","http://localhost:18443/v1/extract","localhost")]
    [InlineData("Development","https://127.0.0.1:18443/v1/extract","127.0.0.1")]
    [InlineData("Development","https://foreign.invalid/v1/extract","foreign.invalid")]
    [InlineData("Development","https://localhost:18443/v1/extract","foreign.invalid")]
    [InlineData("Development","https://localhost:18443/","localhost")]
    [InlineData("Development","https://localhost:18443/v1/extract?private=value","localhost")]
    [InlineData("Development","https://localhost:18443/v1/extract#fragment","localhost")]
    [InlineData("Development","https://operator@localhost:18443/v1/extract","localhost")]
    public void Explicit_CA_is_rejected_before_loading_outside_the_exact_development_localhost_scope(string environment,string endpoint,string allowedHost)
    {
        Assert.Throws<InvalidOperationException>(()=>DevelopmentCertificateExtractionTransport.Create(
            new HostingEnvironment{EnvironmentName=environment},endpoint,allowedHost,"nonexistent.pem"));
    }

    [Theory]
    [InlineData("https://localhost:18444/v1/extract")]
    [InlineData("https://localhost:18443/another")]
    [InlineData("https://foreign.invalid:18443/v1/extract")]
    [InlineData("http://localhost:18443/v1/extract")]
    [InlineData("https://localhost:18443/v1/extract?extra=1")]
    public async Task A_scoped_handler_cannot_send_to_a_changed_target(string target)
    {
        using var key=RSA.Create(2048);using var root=Root(key);var path=Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(path,root.ExportCertificatePem(),TestContext.Current.CancellationToken);
            using var client=new HttpClient(DevelopmentCertificateExtractionTransport.Create(new HostingEnvironment{EnvironmentName="Development"},
                "https://localhost:18443/v1/extract","localhost",path));
            var failure=await Assert.ThrowsAsync<HttpRequestException>(()=>client.PostAsync(target,new StringContent("synthetic"),TestContext.Current.CancellationToken));
            Assert.Equal("Local extraction target rejected.",failure.Message);
        }finally{File.Delete(path);}
    }

    private static X509Certificate2 Root(RSA key)
    {
        var request=new CertificateRequest("CN=Synthetic extraction lab",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign,true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-3),DateTimeOffset.UtcNow.AddDays(3));
    }

    [Theory]
    [InlineData("valid")][InlineData("wrong-name")][InlineData("unrelated-root")][InlineData("client-only")][InlineData("expired")][InlineData("redirect")][InlineData("no-explicit-ca")]
    public async Task Actual_TLS_named_client_preserves_hostname_EKU_validity_root_and_redirect_boundaries(string scenario)
    {
        using var rootKey=RSA.Create(2048);using var root=Root(rootKey);
        using var otherKey=RSA.Create(2048);using var other=Root(otherKey);using var key=RSA.Create(2048);
        var request=new CertificateRequest("CN=Synthetic extraction peer",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder();san.AddDnsName(scenario=="wrong-name"?"foreign.invalid":"localhost");request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection{new(scenario=="client-only"?"1.3.6.1.5.5.7.3.2":"1.3.6.1.5.5.7.3.1")},true));
        using var issued=request.Create(scenario=="unrelated-root"?other:root,DateTimeOffset.UtcNow.AddDays(-2),
            scenario=="expired"?DateTimeOffset.UtcNow.AddDays(-1):DateTimeOffset.UtcNow.AddDays(1),RandomNumberGenerator.GetBytes(16));
        using var withKey=issued.CopyWithPrivateKey(key);
        // Schannel cannot use CopyWithPrivateKey's ephemeral key directly.
        // Round-trip a disposable key container; never install a trusted root.
        using var certificate=X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.DefaultKeySet);
        var path=Path.GetTempFileName();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var endpoint=$"https://localhost:{port}/v1/extract";
        var peer=Serve(listener,certificate,scenario=="redirect",scenario is not ("valid" or "redirect"),deadline.Token);
        try {
            await File.WriteAllTextAsync(path,root.ExportCertificatePem(),deadline.Token);
            var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["CertificateExtraction:Endpoint"]=endpoint,["CertificateExtraction:AllowedHost"]="localhost",
                ["DevelopmentLab:CertificateExtractionCaPath"]=scenario=="no-explicit-ca"?null:path
            }).Build();
            var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IHostEnvironment>(new HostingEnvironment{EnvironmentName="Development"});
            services.AddCertificateExtraction(configuration);await using var provider=services.BuildServiceProvider();
            using var client=provider.GetRequiredService<IHttpClientFactory>().CreateClient("CertificateExtraction");
            if(scenario is "valid" or "redirect") {
                using var response=await client.PostAsync(endpoint,new StringContent("synthetic protocol only"),deadline.Token);
                Assert.Equal(scenario=="redirect"?HttpStatusCode.Found:HttpStatusCode.OK,response.StatusCode);
                if(scenario=="valid")Assert.Equal("synthetic protocol only",await response.Content.ReadAsStringAsync(deadline.Token));
            }else await Assert.ThrowsAsync<HttpRequestException>(()=>client.PostAsync(endpoint,new StringContent("synthetic protocol only"),deadline.Token));
            await peer;
        }finally{deadline.Cancel();listener.Stop();try{await peer;}catch(OperationCanceledException){}File.Delete(path);}
    }

    private static async Task Serve(TcpListener listener,X509Certificate2 certificate,bool redirect,bool expectedRejection,CancellationToken ct)
    {
        using var connection=await listener.AcceptTcpClientAsync(ct);await using var stream=new SslStream(connection.GetStream());
        try {
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13},ct);
            using var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true);var size=0;
            while(await reader.ReadLineAsync(ct) is {} line && line.Length!=0){size+=line.Length;if(size>4096)throw new InvalidDataException("synthetic_headers_too_large");}
            var body="synthetic protocol only";
            var headers=redirect?"HTTP/1.1 302 Found\r\nLocation: https://foreign.invalid/v1/extract\r\nContent-Length: 0\r\nConnection: close\r\n\r\n":
                $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers),ct);await stream.FlushAsync(ct);
        }catch(Exception error)when(expectedRejection && error is AuthenticationException or IOException) { /* Expected rejected TLS handshake. */ }
    }
}
