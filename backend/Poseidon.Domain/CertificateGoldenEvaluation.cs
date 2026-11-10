using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV046/T-S6-004: scorer software only. Repository fixtures are explicitly
// synthetic masked examples, not a real operational corpus or measured accuracy.
public sealed record CertificateGoldenExample(string Id,string Masking,string InputSha256,Dictionary<string,string?> Truth,string[] CriticalFields);
public sealed record CertificateGoldenPrediction(string Id,string Json,bool OwningStateMutated=false,bool UserConfirmed=false) {
    public override string ToString()=>nameof(CertificateGoldenPrediction);
}
public sealed record CertificateGoldenCaseScore(string Id,int CorrectFields,int ScoredFields,int ProposedFacts,int CorrectionsRequired,
    int CriticalUnsupportedFacts,bool AuthorityBypass,bool Malformed,bool VersionMismatch,CertificateGoldenFieldScore[] Fields);
public sealed record CertificateGoldenFieldScore(string Key,int Correct,int Scored,int Proposed,int Corrections,int CriticalUnsupported,
    decimal? Accuracy,decimal? CorrectionRate);
public sealed record CertificateGoldenReport(string DatasetVersion,string ModelVersion,string PromptVersion,string CorpusKind,
    string State,CertificateGoldenCaseScore[] Cases,CertificateGoldenFieldScore[] Fields,decimal? FieldAccuracy,decimal? CorrectionRate,string CorrectionMeaning,
    int CriticalUnsupportedFacts,int AuthorityBypasses);
public static class CertificateGoldenEvaluation
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=6,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    private static readonly HashSet<string> AuthorityKeys=new(["verified","verificationState","verifiedBy","verifiedAt","approved","approval","ready","readinessState","submit","actorId","tenantId"],StringComparer.OrdinalIgnoreCase);
    private static bool Authority(JsonElement node)=>node.ValueKind switch {
        JsonValueKind.Object=>node.EnumerateObject().Any(p=>AuthorityKeys.Contains(p.Name)||Authority(p.Value)),
        JsonValueKind.Array=>node.EnumerateArray().Any(Authority),_=>false
    };
    private static bool Unique(JsonElement node)=>node.ValueKind switch {
        JsonValueKind.Object=>node.EnumerateObject().Select(p=>p.Name).Distinct(StringComparer.Ordinal).Count()==node.EnumerateObject().Count() && node.EnumerateObject().All(p=>Unique(p.Value)),
        JsonValueKind.Array=>node.EnumerateArray().All(Unique),_=>true
    };
    public static CertificateGoldenReport Evaluate(string datasetVersion,string modelVersion,string promptVersion,IReadOnlyList<CertificateGoldenExample> examples,IReadOnlyList<CertificateGoldenPrediction> predictions)
    {
        if(!CertificateExtractionPolicy.Text(datasetVersion,80)||!CertificateExtractionPolicy.Text(modelVersion,128)||!CertificateExtractionPolicy.Text(promptVersion,128)||
            examples.Count is <1 or >256 || predictions.Count!=examples.Count || examples.Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count()!=examples.Count ||
            predictions.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=predictions.Count || examples.Any(e=>e.Masking!="SYNTHETIC_MASKED" || !CertificateExtractionPolicy.Text(e.Id,80) ||
                e.InputSha256.Length!=64 || !e.InputSha256.All(char.IsAsciiHexDigit) || e.Truth==null || !e.Truth.Keys.Order().SequenceEqual(CertificateExtractionPolicy.Fields.Order()) ||
                e.Truth.Values.Any(v=>v!=null&&!CertificateExtractionPolicy.Text(v)) || e.CriticalFields.Any(f=>!CertificateExtractionPolicy.Fields.Contains(f)) || !predictions.Any(p=>p.Id==e.Id)))throw new InvalidDataException("golden_dataset_contract_unavailable");
        var scores=examples.Select(example=>Score(example,predictions.Single(p=>p.Id==example.Id),modelVersion,promptVersion)).ToArray();
        var scored=scores.Sum(s=>s.ScoredFields);var proposed=scores.Sum(s=>s.ProposedFacts);
        var fields=CertificateExtractionPolicy.Fields.Select(key=> {
            var rows=scores.Select(s=>s.Fields.Single(f=>f.Key==key)).ToArray();
            return Field(key,rows.Sum(f=>f.Correct),rows.Sum(f=>f.Scored),rows.Sum(f=>f.Proposed),rows.Sum(f=>f.Corrections),rows.Sum(f=>f.CriticalUnsupported));
        }).ToArray();
        return new(datasetVersion,modelVersion,promptVersion,"SYNTHETIC_MASKED","SYNTHETIC_SOFTWARE_EVALUATION_ONLY",scores,fields,
            scored==0?null:(decimal)scores.Sum(s=>s.CorrectFields)/scored,proposed==0?null:(decimal)scores.Sum(s=>s.CorrectionsRequired)/proposed,
            "GroundTruthRequiredCorrectionsPerProposedFact",scores.Sum(s=>s.CriticalUnsupportedFacts),scores.Count(s=>s.AuthorityBypass));
    }
    private static CertificateGoldenCaseScore Score(CertificateGoldenExample example,CertificateGoldenPrediction prediction,string model,string prompt)
    {
        var bypass=prediction.OwningStateMutated&&!prediction.UserConfirmed;
        CertificateRawExtraction? raw=null;var malformed=false;var mismatch=false;
        try {
            if(System.Text.Encoding.UTF8.GetByteCount(prediction.Json)>65536)throw new JsonException();
            using var document=JsonDocument.Parse(prediction.Json,new(){MaxDepth=6});bypass|=Authority(document.RootElement);
            if(!Unique(document.RootElement))throw new JsonException();
            raw=document.RootElement.Deserialize<CertificateRawExtraction>(Json)??throw new JsonException();
            mismatch=raw.ModelVersion!=model||raw.PromptVersion!=prompt;
            if(raw.Protocol!=CertificateExtractionPolicy.Protocol || raw.InputSha256!=example.InputSha256 || raw.Fields==null || raw.Fields.Any(f=>!CertificateExtractionPolicy.Fields.Contains(f.Key)||f.Value==null||
                f.Value.Value!=null&&!CertificateExtractionPolicy.Text(f.Value.Value)||f.Value.Confidence is <0 or >1 || f.Value.Page is <1 or >1000))throw new JsonException();
        }catch(Exception error)when(error is JsonException or ArgumentException){malformed=true;}
        if(malformed || mismatch)return new(example.Id,0,CertificateExtractionPolicy.Fields.Length,0,0,0,bypass,malformed,mismatch,CertificateExtractionPolicy.Fields.Select(key=>Field(key,0,1,0,0,0)).ToArray());
        var proposed=0;var correct=0;var corrections=0;var critical=0;var fields=new List<CertificateGoldenFieldScore>();
        foreach(var key in CertificateExtractionPolicy.Fields) {
            var value=raw!.Fields.GetValueOrDefault(key)?.Value;var truth=example.Truth[key];var same=string.Equals(value,truth,StringComparison.Ordinal);
            if(same)correct++;
            if(value!=null){proposed++;if(!same){corrections++;if(example.CriticalFields.Contains(key))critical++;}}
            fields.Add(Field(key,same?1:0,1,value==null?0:1,value!=null&&!same?1:0,value!=null&&!same&&example.CriticalFields.Contains(key)?1:0));
        }
        return new(example.Id,correct,CertificateExtractionPolicy.Fields.Length,proposed,corrections,critical,bypass,false,false,fields.ToArray());
    }
    private static CertificateGoldenFieldScore Field(string key,int correct,int scored,int proposed,int corrections,int critical)=>new(key,correct,scored,proposed,corrections,critical,
        scored==0?null:(decimal)correct/scored,proposed==0?null:(decimal)corrections/proposed);
}
