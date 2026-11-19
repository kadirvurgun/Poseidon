using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;
namespace Poseidon.Api.Tests;

// DEV045/T-S6-003: LocalTextLayer provider is in-process only. These tests prove it
// never creates an HttpClient, needs no secret/endpoint, binds provenance to the
// actual engine/rule versions, and reports image/scanned input as an explicit
// capability gap. Synthetic fictional PDF bytes only.
public sealed class CertificateLocalExtractionProviderTests
{
    private sealed class EnvironmentFixture:IHostEnvironment {
        public string EnvironmentName{get;set;}="Test";public string ApplicationName{get;set;}="SyntheticLocalExtraction";
        public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    private sealed class NoNetwork:IHttpClientFactory {
        public int Created{get;private set;}
        public HttpClient CreateClient(string name){Created++;throw new InvalidOperationException("local provider must not create an HttpClient");}
    }
    internal static CertificateExtractionSettings LocalPolicy()=>new(){Enabled=true,Provider=CertificateExtractionSettings.LocalTextLayer,
        PolicyVersion="synthetic-local-policy-v1",ProcessingReference="FICTIONAL in-process only",StorageReference="FICTIONAL no copy retained",Region="FICTIONAL local host",
        PrivacyReference="FICTIONAL synthetic data",RetentionReference="FICTIONAL request lifetime",ConfidenceReference="FICTIONAL uncalibrated rule score",
        SourceVersion=CertificateExtractionPolicy.SourceVersion,ModelVersion=CertificateTextLayerEngine.EngineVersion,PromptVersion=CertificateTextLayerEngine.RuleSetVersion,
        LowConfidenceThreshold=0.8m,TimeoutSeconds=5,ReviewSeconds=60,MaxBytes=65536,MaxResponseBytes=4096,Scopes=[new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"Vessel")]};
    // Minimal uncompressed synthetic PDF; offsets are not needed by the reader.
    internal static byte[] Pdf(params string[] lines) {
        var content=new StringBuilder();var y=700;
        foreach(var line in lines){content.Append("BT /F1 11 Tf 72 ").Append(y).Append(" Td (").Append(line.Replace("(","\\(",StringComparison.Ordinal).Replace(")","\\)",StringComparison.Ordinal)).Append(") Tj ET\n");y-=20;}
        var stream=content.ToString();
        return Encoding.Latin1.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n"+
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n"+
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n"+
            $"5 0 obj\n<< /Length {stream.Length} >>\nstream\n{stream}\nendstream\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");
    }
    [Fact]
    public async Task Local_text_layer_extracts_review_candidates_in_process_without_network_secret_or_endpoint() {
        var clients=new NoNetwork();var provider=new CertificateExtractionProvider(clients,new EnvironmentFixture());
        var bytes=Pdf("Certificate No: SYN-LOCAL-1","Date of Expiry: 03/04/2035","Issued by: FICTIONAL MARITIME AUTHORITY A");var checksum=Convert.ToHexString(SHA256.HashData(bytes));
        var policy=LocalPolicy();Assert.True(policy.Allows(new EnvironmentFixture(),policy.Scopes[0].TenantId,policy.Scopes[0].ActorId,policy.Scopes[0].VesselId,"Vessel"));
        var raw=await provider.Extract(policy,bytes,"application/pdf",checksum,TestContext.Current.CancellationToken);
        Assert.Equal(0,clients.Created);Assert.Equal(CertificateTextLayerEngine.EngineVersion,raw.ModelVersion);Assert.Equal(checksum,raw.InputSha256);
        Assert.Equal("SYN-LOCAL-1",raw.Fields["certificate_no"].Value);Assert.Equal("2035-04-03",raw.Fields["expiry_date"].Value);Assert.True(raw.Fields["expiry_date"].Ambiguous);
        Assert.All(raw.Fields.Values,f=>{Assert.Equal(1,f.Page);Assert.True(f.Region!.Valid());});
        var candidates=CertificateExtractionPolicy.Project(raw,checksum,raw.ModelVersion,raw.PromptVersion,0.8m,Guid.NewGuid().ToString("D"),null);
        Assert.All(candidates,c=>Assert.Equal("REVIEW_REQUIRED",c.State));
        // Day/month-ambiguous date is forced to low-confidence review.
        Assert.True(candidates.Single(c=>c.Key=="expiry_date").LowConfidence);Assert.False(candidates.Single(c=>c.Key=="certificate_no").LowConfidence);
    }
    [Theory]
    [InlineData("endpoint")][InlineData("secret")][InlineData("model")][InlineData("prompt")][InlineData("unknown-provider")]
    public async Task Local_provider_with_network_settings_or_mismatched_provenance_is_unconfigured(string fault) {
        var policy=LocalPolicy();
        switch(fault){
            case "endpoint":policy.Endpoint="https://synthetic-extraction.invalid/v1";policy.AllowedHost="synthetic-extraction.invalid";break;
            case "secret":policy.SecretReference="SYNTHETIC_SECRET";break;
            case "model":policy.ModelVersion="synthetic-model-v1";break;
            case "prompt":policy.PromptVersion="synthetic-prompt-v1";break;
            default:policy.Provider="CloudOcr";break;
        }
        var clients=new NoNetwork();var bytes=Pdf("Certificate No: SYN-LOCAL-2");
        await Assert.ThrowsAsync<CertificateExtractionUnavailableException>(()=>new CertificateExtractionProvider(clients,new EnvironmentFixture()).Extract(policy,bytes,"application/pdf",Convert.ToHexString(SHA256.HashData(bytes)),TestContext.Current.CancellationToken));
        Assert.Equal(0,clients.Created);
    }
    [Theory]
    [InlineData("image/png")][InlineData("image/jpeg")][InlineData("scanned")]
    public async Task Image_and_scanned_documents_are_an_explicit_ocr_capability_gap_not_an_empty_success(string kind) {
        var bytes=kind=="scanned"?Encoding.Latin1.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n3 0 obj\n<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>\nendobj\n4 0 obj\n<< /Length 32 >>\nstream\nq 400 0 0 600 72 96 cm /Im1 Do Q\nendstream\nendobj\n%%EOF\n"):[0x89,0x50,0x4E,0x47,1,2,3,4];
        var clients=new NoNetwork();
        var error=await Assert.ThrowsAsync<CertificateOcrCapabilityUnavailableException>(()=>new CertificateExtractionProvider(clients,new EnvironmentFixture()).Extract(LocalPolicy(),bytes,kind=="scanned"?"application/pdf":kind,Convert.ToHexString(SHA256.HashData(bytes)),TestContext.Current.CancellationToken));
        Assert.Equal("certificate_ocr_capability_unavailable",error.Message);Assert.Null(error.InnerException);Assert.Equal(0,clients.Created);
    }
    [Fact]
    public void Security_audit_registry_contains_the_ocr_capability_result_code() {
        Assert.Contains("ocr_capability_unavailable",Poseidon.Persistence.SecurityEventCodes.Results);
    }
}
