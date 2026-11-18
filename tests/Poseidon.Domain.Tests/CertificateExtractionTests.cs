using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

// DEV045/046. Test sources only; no operational corpus/provider execution.
public sealed class CertificateExtractionTests
{
    private static CertificateRawExtraction Raw()=>new(CertificateExtractionPolicy.Protocol,"synthetic-model-v1","synthetic-prompt-v1",new string('A',64),new(){
        ["certificate_no"]=new("MASKED-001",0.99m,1),["expiry_date"]=new("2032-01-02",0.2m,1),["vessel_reference"]=new("OTHER-SYNTHETIC-VESSEL",0.99m,1)});
    [Fact]
    public void Confidence_missing_fields_and_reference_mismatch_never_promote_candidates_to_verified_or_owning_state() {
        var fields=CertificateExtractionPolicy.Project(Raw(),new string('A',64),"synthetic-model-v1","synthetic-prompt-v1",0.8m,"SYNTHETIC-VESSEL",null);
        Assert.Equal(7,fields.Length);Assert.All(fields,f=>Assert.Equal("REVIEW_REQUIRED",f.State));
        Assert.False(fields.Single(f=>f.Key=="certificate_no").LowConfidence);Assert.True(fields.Single(f=>f.Key=="expiry_date").LowConfidence);
        Assert.Contains("SCOPED_REFERENCE_UNMATCHED",fields.Single(f=>f.Key=="vessel_reference").Reasons);
        var missing=fields.Single(f=>f.Key=="issue_date");Assert.Null(missing.Value);Assert.Null(missing.Confidence);Assert.Contains("NOT_EXTRACTED",missing.Reasons);
        Assert.All(fields,f=>Assert.Contains("PROVIDER_CANDIDATE_NOT_VERIFICATION",f.Reasons));
    }
    [Fact]
    public void User_confirmed_fact_merge_preserves_every_unselected_existing_declaration_and_never_writes_identity_or_verification() {
        var original=new CertificateMetadata(new("EXISTING-TYPE","EXISTING-NO","MASKED-ISSUER",new(2030,1,1),new(2031,1,1),"LIMITATION"),DocumentReference:Guid.NewGuid(),Notes:"EXISTING NOTE");
        Assert.False(CertificateExtractionPolicy.TryMerge(original,[new("certificate_no","MASKED-NEW",false)],out _));
        Assert.False(CertificateExtractionPolicy.TryMerge(original,[new("verified_by","someone",true)],out _));
        Assert.False(CertificateExtractionPolicy.TryMerge(original,[new("vessel_reference",Guid.NewGuid().ToString(),true)],out _));
        Assert.False(CertificateExtractionPolicy.TryMerge(original,[new("expiry_date","01/02/2032",true)],out _));
        Assert.True(CertificateExtractionPolicy.TryMerge(original,[new("certificate_no","MASKED-NEW",true)],out var next));
        Assert.Equal("MASKED-NEW",next.Declaration.CertificateNumber);Assert.Equal(original.Declaration with{CertificateNumber="MASKED-NEW"},next.Declaration);
        Assert.Equal(original.DocumentReference,next.DocumentReference);Assert.Equal(original.Notes,next.Notes);Assert.Equal("EXISTING-NO",original.Declaration.CertificateNumber);
    }
    [Fact]
    public void Wrong_provider_source_version_or_unbounded_field_is_a_contract_failure_not_a_successful_extraction() {
        var raw=Raw();Assert.False(CertificateExtractionPolicy.ValidRaw(raw with{InputSha256=new string('B',64)},raw.InputSha256,raw.ModelVersion,raw.PromptVersion));
        Assert.False(CertificateExtractionPolicy.ValidRaw(raw with{ModelVersion="different"},raw.InputSha256,raw.ModelVersion,raw.PromptVersion));
        raw.Fields["verified"]=new("true",1m,1);Assert.False(CertificateExtractionPolicy.ValidRaw(raw,raw.InputSha256,raw.ModelVersion,raw.PromptVersion));
    }
    [Fact]
    public void Synthetic_masked_dataset_scorer_reports_explicit_denominators_versions_and_no_operational_accuracy_claim() {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","certificate-extraction.synthetic-masked-v1.json")));
        Assert.Equal("NOT_RUN",fixture.RootElement.GetProperty("executionState").GetString());
        var examples=fixture.RootElement.GetProperty("examples").EnumerateArray().Select(e=>new CertificateGoldenExample(e.GetProperty("id").GetString()!,"SYNTHETIC_MASKED",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(e.GetProperty("maskedInput").GetString()!))),
            e.GetProperty("truth").Deserialize<Dictionary<string,string?>>()!,e.GetProperty("criticalFields").Deserialize<string[]>()!)).ToArray();
        Assert.Equal(3,examples.Length);
        var predictions=examples.Select(e=>new CertificateGoldenPrediction(e.Id,JsonSerializer.Serialize(new CertificateRawExtraction(CertificateExtractionPolicy.Protocol,"synthetic-model-v1","synthetic-prompt-v1",e.InputSha256,
            e.Truth.Where(f=>f.Value!=null).ToDictionary(f=>f.Key,f=>new CertificateRawCandidate(f.Value,0.5m,1))),new JsonSerializerOptions(JsonSerializerDefaults.Web)))).ToArray();
        var report=CertificateGoldenEvaluation.Evaluate("synthetic-masked-v1","synthetic-model-v1","synthetic-prompt-v1",examples,predictions);
        Assert.Equal("SYNTHETIC_SOFTWARE_EVALUATION_ONLY",report.State);Assert.Equal("synthetic-model-v1",report.ModelVersion);Assert.Equal("synthetic-prompt-v1",report.PromptVersion);
        Assert.All(report.Cases,c=>Assert.Equal(7,c.ScoredFields));Assert.Equal("GroundTruthRequiredCorrectionsPerProposedFact",report.CorrectionMeaning);
        Assert.Equal(7,report.Fields.Length);Assert.All(report.Fields,f=>{Assert.Equal(3,f.Scored);Assert.Equal(1m,f.Accuracy);});
        Assert.DoesNotContain("MASKED-ISSUER",JsonSerializer.Serialize(report));
        // Expected arithmetic for scorer software, not a measured model/provider result.
        Assert.Equal(1m,report.FieldAccuracy);Assert.Equal(0,report.CriticalUnsupportedFacts);Assert.Equal(0,report.AuthorityBypasses);
    }
    [Fact]
    public void Unsupported_critical_fact_and_authority_attempt_are_detected_and_zero_proposals_do_not_invent_a_correction_rate() {
        var truth=CertificateExtractionPolicy.Fields.ToDictionary(k=>k,_=>(string?)null);var example=new CertificateGoldenExample("synthetic-absent","SYNTHETIC_MASKED",new string('A',64),truth,["expiry_date"]);
        var invented=new CertificateRawExtraction(CertificateExtractionPolicy.Protocol,"m1","p1",example.InputSha256,new(){["expiry_date"]=new("2032-01-02",0.99m,1)});
        var report=CertificateGoldenEvaluation.Evaluate("synthetic-v1","m1","p1",[example],[new(example.Id,JsonSerializer.Serialize(invented,new JsonSerializerOptions(JsonSerializerDefaults.Web)))]);
        Assert.Equal(1,report.CriticalUnsupportedFacts);Assert.Equal(1m,report.CorrectionRate);
        var expiry=Assert.Single(report.Fields,f=>f.Key=="expiry_date");Assert.Equal(0m,expiry.Accuracy);Assert.Equal(1m,expiry.CorrectionRate);Assert.Equal(1,expiry.CriticalUnsupported);
        var injected=JsonSerializer.Serialize(invented,new JsonSerializerOptions(JsonSerializerDefaults.Web)).Replace("\"fields\":", "\"verified\":true,\"fields\":",StringComparison.Ordinal);
        var denied=CertificateGoldenEvaluation.Evaluate("synthetic-v1","m1","p1",[example],[new(example.Id,injected)]);
        Assert.Equal(1,denied.AuthorityBypasses);Assert.True(Assert.Single(denied.Cases).Malformed);Assert.Null(denied.CorrectionRate);
        var bypass=CertificateGoldenEvaluation.Evaluate("synthetic-v1","m1","p1",[example],[new(example.Id,JsonSerializer.Serialize(invented,new JsonSerializerOptions(JsonSerializerDefaults.Web)),true,false)]);
        Assert.Equal(1,bypass.AuthorityBypasses);
        Assert.Throws<InvalidDataException>(()=>CertificateGoldenEvaluation.Evaluate("synthetic-v1","m1","p1",[example],[]));
    }
}
