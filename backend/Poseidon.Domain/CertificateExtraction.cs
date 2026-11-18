using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV045/T-S6-003. Closed advisory contract. Confidence is neither verification
// nor permission to change certificate ownership, status, approval or readiness.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRawCandidate(string? Value, decimal? Confidence, int? Page) { public override string ToString()=>nameof(CertificateRawCandidate); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRawExtraction(string Protocol, string ModelVersion, string PromptVersion, string InputSha256,
    Dictionary<string,CertificateRawCandidate> Fields) { public override string ToString()=>nameof(CertificateRawExtraction); }
public sealed record CertificateCandidate(string Key, string? Value, decimal? Confidence, int? Page,
    bool LowConfidence, string State, string[] Reasons) { public override string ToString()=>nameof(CertificateCandidate); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateConfirmedFact(string Key, string Value, [property:JsonRequired] bool Confirmed) { public override string ToString()=>nameof(CertificateConfirmedFact); }

public static class CertificateExtractionPolicy
{
    public const string Protocol="poseidon.certificate-extraction.v1";
    public const string SourceVersion="18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:46-47;14.02:1634-1657:v46.3";
    public static readonly string[] MutableFields=["certificate_type","certificate_no","issuer","issue_date","expiry_date"];
    public static readonly string[] Fields=[..MutableFields,"vessel_reference","person_reference"];
    public static bool Text(string? value,int maximum=512)=>value is {Length:>0} && value.Length<=maximum && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    public static bool ValidRaw(CertificateRawExtraction? raw,string checksum,string model,string prompt)=>raw!=null && raw.Protocol==Protocol && raw.ModelVersion==model && raw.PromptVersion==prompt && raw.InputSha256==checksum &&
        raw.Fields!=null && raw.Fields.Count<=Fields.Length && raw.Fields.All(f=>Fields.Contains(f.Key,StringComparer.Ordinal) && f.Value!=null &&
            (f.Value.Value==null || Text(f.Value.Value)) && f.Value.Confidence is not (<0 or >1) && f.Value.Page is not (<1 or >1000));
    public static CertificateCandidate[] Project(CertificateRawExtraction raw,string checksum,string model,string prompt,decimal threshold,
        string vesselReference,string? personReference)
    {
        if(!ValidRaw(raw,checksum,model,prompt) || threshold is <0 or >1)throw new InvalidDataException("extraction_contract_unavailable");
        return Fields.Select(key=> {
            var field=raw.Fields.GetValueOrDefault(key);var reasons=new List<string>();
            if(field?.Value==null)reasons.Add("NOT_EXTRACTED");
            if(field?.Confidence==null)reasons.Add("CONFIDENCE_UNAVAILABLE");
            var low=field?.Confidence is not {} confidence || confidence<threshold;if(low)reasons.Add("LOW_CONFIDENCE_REVIEW");
            if(field?.Page==null)reasons.Add("SOURCE_LOCATOR_UNAVAILABLE");
            if(field?.Value!=null && (key is "issue_date" or "expiry_date") && !DateOnly.TryParseExact(field.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))reasons.Add("DATE_FORMAT_REVIEW");
            if(key is "vessel_reference" or "person_reference") {
                var expected=key=="vessel_reference"?vesselReference:personReference;
                if(expected==null || field?.Value==null || !string.Equals(expected,field.Value,StringComparison.OrdinalIgnoreCase))reasons.Add("SCOPED_REFERENCE_UNMATCHED");
            }
            reasons.Add("PROVIDER_CANDIDATE_NOT_VERIFICATION");
            return new CertificateCandidate(key,field?.Value,field?.Confidence,field?.Page,low,"REVIEW_REQUIRED",reasons.ToArray());
        }).ToArray();
    }
    public static bool TryMerge(CertificateMetadata current,IReadOnlyList<CertificateConfirmedFact>? facts,out CertificateMetadata merged)
    {
        merged=current;
        if(facts is not {Count:>0 and <=5} || facts.Any(f=>f==null || !f.Confirmed || !MutableFields.Contains(f.Key,StringComparer.Ordinal) || !Text(f.Value)) || facts.Select(f=>f.Key).Distinct(StringComparer.Ordinal).Count()!=facts.Count)return false;
        var declaration=current.Declaration;
        foreach(var fact in facts) {
            DateOnly date;
            switch(fact.Key) {
                case "certificate_type":declaration=declaration with{CertificateTypeReference=fact.Value};break;
                case "certificate_no":declaration=declaration with{CertificateNumber=fact.Value};break;
                case "issuer":declaration=declaration with{Issuer=fact.Value};break;
                case "issue_date":if(!DateOnly.TryParseExact(fact.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date))return false;declaration=declaration with{IssueDate=date};break;
                case "expiry_date":if(!DateOnly.TryParseExact(fact.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date))return false;declaration=declaration with{ExpiryDate=date};break;
                default:return false;
            }
        }
        merged=current with{Declaration=declaration};return CertificateMetadataPolicy.Valid(merged);
    }
}
