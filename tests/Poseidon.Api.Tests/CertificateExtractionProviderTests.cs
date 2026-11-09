using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;
namespace Poseidon.Api.Tests;

// Real HttpClient protocol boundary with a deterministic in-memory handler.
// Not an activated/provider OCR run or accuracy evidence. Sources authored only.
public sealed class CertificateExtractionProviderTests
{
    private sealed class EnvironmentFixture:IHostEnvironment {
        public string EnvironmentName{get;set;}="Test";public string ApplicationName{get;set;}="SyntheticExtractionFixture";
        public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    private sealed class ProtocolPeer(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> response):HttpMessageHandler {
        public int Requests{get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Requests++;return response(request,ct);}
    }
    private sealed class Clients(ProtocolPeer peer):IHttpClientFactory {public HttpClient CreateClient(string name){Assert.Equal("CertificateExtraction",name);return new(peer,false);}}
    private static CertificateExtractionSettings Policy(string reference)=>new(){Enabled=true,Endpoint="http://127.0.0.1:18081/extract",AllowedHost="127.0.0.1",SecretReference=reference,
        PolicyVersion="synthetic-processing-v1",ProcessingReference="FICTIONAL test processing; not approved",StorageReference="FICTIONAL no provider retention test",Region="FICTIONAL test region",PrivacyReference="FICTIONAL masked only",
        RetentionReference="FICTIONAL fixture only",ConfidenceReference="FICTIONAL review threshold",SourceVersion=CertificateExtractionPolicy.SourceVersion,ModelVersion="synthetic-model-v1",PromptVersion="synthetic-prompt-v1",
        LowConfidenceThreshold=0.8m,TimeoutSeconds=1,ReviewSeconds=60,MaxBytes=4096,MaxResponseBytes=4096,Scopes=[new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"Vessel")]};
    private static HttpResponseMessage Reply(CertificateExtractionSettings p,string checksum)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new CertificateRawExtraction(CertificateExtractionPolicy.Protocol,p.ModelVersion!,p.PromptVersion!,checksum,new(){["certificate_no"]=new("MASKED-ONLY",0.4m,1)}),new JsonSerializerOptions(JsonSerializerDefaults.Web)),Encoding.UTF8,"application/json")};
    private static string Reference() {
        var reference="POSEIDON_TEST_EXTRACTION_"+Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(reference,Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));return reference;
    }
    [Fact]
    public async Task Default_or_incomplete_processing_configuration_performs_no_http_request() {
        var peer=new ProtocolPeer((_,_)=>throw new InvalidOperationException("must not contact provider"));var provider=new CertificateExtractionProvider(new Clients(peer),new EnvironmentFixture(),new CertificateExtractionDeadlineClock());
        await Assert.ThrowsAsync<CertificateExtractionUnavailableException>(()=>provider.Extract(new(),[1],"image/png",new string('A',64),CancellationToken.None));Assert.Equal(0,peer.Requests);
        var policy=Policy("SYNTHETIC_UNSET_EXTRACTION_SECRET");policy.PrivacyReference="";
        await Assert.ThrowsAsync<CertificateExtractionUnavailableException>(()=>provider.Extract(policy,[1],"image/png",new string('A',64),CancellationToken.None));Assert.Equal(0,peer.Requests);
    }
    [Fact]
    public async Task Explicit_protocol_sends_only_fixed_contract_and_owned_bytes_and_requires_exact_provider_versions_and_input_binding() {
        var reference=Reference();
        try {
            var policy=Policy(reference);var bytes=Encoding.UTF8.GetBytes("SYNTHETIC MASKED DOCUMENT ONLY");var checksum=Convert.ToHexString(SHA256.HashData(bytes));
            var peer=new ProtocolPeer(async(request,ct)=>{
                Assert.Equal(policy.Endpoint,request.RequestUri!.AbsoluteUri);Assert.Equal("",request.RequestUri.Query);
                Assert.Equal("Bearer",request.Headers.Authorization?.Scheme);Assert.DoesNotContain("MASKED",request.Headers.ToString());
                var multipart=Assert.IsType<MultipartFormDataContent>(request.Content);Assert.Equal(2,multipart.Count());
                var document=multipart.Single(c=>c.Headers.ContentDisposition?.Name?.Trim('"')=="document");Assert.Equal("source.document",document.Headers.ContentDisposition!.FileName!.Trim('"'));
                Assert.Equal(bytes,await document.ReadAsByteArrayAsync(ct));var contract=await multipart.Single(c=>c!=document).ReadAsStringAsync(ct);
                Assert.Contains(policy.ModelVersion!,contract);Assert.Contains(policy.PromptVersion!,contract);Assert.DoesNotContain("actorId",contract);Assert.DoesNotContain("tenantId",contract);
                return Reply(policy,checksum);
            });
            var provider=new CertificateExtractionProvider(new Clients(peer),new EnvironmentFixture(),new CertificateExtractionDeadlineClock());var candidate=await provider.Extract(policy,bytes,"application/pdf",checksum,CancellationToken.None);
            Assert.Equal("MASKED-ONLY",candidate.Fields["certificate_no"].Value);Assert.Equal(1,peer.Requests);
        }finally{Environment.SetEnvironmentVariable(reference,null);}
    }
    [Theory]
    [InlineData("redirect")] [InlineData("wrong-model")] [InlineData("wrong-source")] [InlineData("authority")] [InlineData("oversize")]
    public async Task Redirects_wrong_provenance_extra_authority_and_unbounded_responses_are_unavailable_without_private_exception_content(string fault) {
        var reference=Reference();
        try {
            var p=Policy(reference);var checksum=new string('A',64);var peer=new ProtocolPeer(async(_,_)=>{
                if(fault=="redirect")return new(HttpStatusCode.Redirect){Headers={Location=new("https://untrusted.invalid/")}};
                var response=Reply(p,fault=="wrong-source"?new string('B',64):checksum);var json=await response.Content.ReadAsStringAsync();
                if(fault=="wrong-model")json=json.Replace("synthetic-model-v1","unmatched-model",StringComparison.Ordinal);
                if(fault=="authority")json=json.Replace("\"fields\":","\"verified\":true,\"fields\":",StringComparison.Ordinal);
                if(fault=="oversize")json=new string('X',5000);response.Content=new StringContent(json,Encoding.UTF8,"application/json");return response;
            });
            var error=await Assert.ThrowsAsync<CertificateExtractionUnavailableException>(()=>new CertificateExtractionProvider(new Clients(peer),new EnvironmentFixture(),new CertificateExtractionDeadlineClock()).Extract(p,[1],"image/png",checksum,CancellationToken.None));
            Assert.Equal("certificate_extraction_unavailable",error.Message);Assert.Null(error.InnerException);Assert.Equal(1,peer.Requests);
        }finally{Environment.SetEnvironmentVariable(reference,null);}
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Logical_deadline_is_exact_and_unavailable_while_caller_cancellation_propagates(bool cancelCaller) {
        var reference=Reference();var clock=new CertificateExtractionDeadlineClock();using var caller=new CancellationTokenSource();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer=new ProtocolPeer(async(_,ct)=>{entered.TrySetResult();await Task.Delay(Timeout.InfiniteTimeSpan,ct);throw new InvalidOperationException("cancelled_peer_resumed");});
        try {
            var policy=Policy(reference);Assert.Equal(1,policy.TimeoutSeconds);
            var provider=new CertificateExtractionProvider(new Clients(peer),new EnvironmentFixture(),clock);
            var pending=provider.Extract(policy,[1],"image/png",new string('A',64),caller.Token);await entered.Task;
            Assert.Equal(1,clock.ActiveTimers);Assert.False(pending.IsCompleted);
            if(cancelCaller) {
                caller.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);Assert.True(caller.IsCancellationRequested);
            }else {
                clock.Advance(TimeSpan.FromMilliseconds(999));Assert.False(pending.IsCompleted);
                clock.Advance(TimeSpan.FromMilliseconds(1));
                var error=await Assert.ThrowsAsync<CertificateExtractionUnavailableException>(()=>pending);
                Assert.Equal("certificate_extraction_unavailable",error.Message);Assert.Null(error.InnerException);Assert.False(caller.IsCancellationRequested);
            }
            Assert.Equal(1,peer.Requests);Assert.Equal(0,clock.ActiveTimers);
        }finally{caller.Cancel();Environment.SetEnvironmentVariable(reference,null);}
    }
}
