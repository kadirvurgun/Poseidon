using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV045/T-S6-003. Closed advisory contract. Confidence is neither verification
// nor permission to change certificate ownership, status, approval or readiness.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRawCandidate(string? Value, decimal? Confidence, int? Page, CertificateSourceRegion? Region=null, bool Ambiguous=false) { public override string ToString()=>nameof(CertificateRawCandidate); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRawExtraction(string Protocol, string ModelVersion, string PromptVersion, string InputSha256,
    Dictionary<string,CertificateRawCandidate> Fields) { public override string ToString()=>nameof(CertificateRawExtraction); }
public sealed record CertificateCandidate(string Key, string? Value, decimal? Confidence, int? Page,
    bool LowConfidence, string State, string[] Reasons, CertificateSourceRegion? Region=null, bool Ambiguous=false) { public override string ToString()=>nameof(CertificateCandidate); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateConfirmedFact(string Key, string Value, [property:JsonRequired] bool Confirmed) { public override string ToString()=>nameof(CertificateConfirmedFact); }

public static class CertificateExtractionPolicy
{
    public const string Protocol="poseidon.certificate-extraction.v1";
    public const string SourceVersion="18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:46-47;14.02:1634-1657:v46.3";
    public static readonly string[] MutableFields=["certificate_type","certificate_no","issuer","issue_date","expiry_date"];
    public static readonly string[] Fields=[..MutableFields,"vessel_reference","person_reference"];
    public static bool Text(string? value,int maximum=512)=>value is {Length:>0} && value.Length<=maximum && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    // A region is only meaningful on a stated page; ambiguity needs a value.
    public static bool ValidCandidate(CertificateRawCandidate? c)=>c!=null && (c.Value==null || Text(c.Value)) && c.Confidence is not (<0 or >1) && c.Page is not (<1 or >1000) &&
        (c.Region==null || c.Page!=null && c.Region.Valid()) && (!c.Ambiguous || c.Value!=null);
    public static bool ValidRaw(CertificateRawExtraction? raw,string checksum,string model,string prompt)=>raw!=null && raw.Protocol==Protocol && raw.ModelVersion==model && raw.PromptVersion==prompt && raw.InputSha256==checksum &&
        raw.Fields!=null && raw.Fields.Count<=Fields.Length && raw.Fields.All(f=>Fields.Contains(f.Key,StringComparer.Ordinal) && ValidCandidate(f.Value));
    public static CertificateCandidate[] Project(CertificateRawExtraction raw,string checksum,string model,string prompt,decimal threshold,
        CertificateVesselIdentifiers? vessel,string? personReference)
    {
        if(!ValidRaw(raw,checksum,model,prompt) || threshold is <0 or >1)throw new InvalidDataException("extraction_contract_unavailable");
        return Fields.Select(key=> {
            var field=raw.Fields.GetValueOrDefault(key);var reasons=new List<string>();
            if(field?.Value==null)reasons.Add("NOT_EXTRACTED");
            if(field?.Confidence==null)reasons.Add("CONFIDENCE_UNAVAILABLE");
            var ambiguous=field?.Ambiguous==true;
            var low=field?.Confidence is not {} confidence || confidence<threshold || ambiguous;if(low)reasons.Add("LOW_CONFIDENCE_REVIEW");
            if(ambiguous)reasons.Add("AMBIGUOUS_CANDIDATE_REVIEW");
            if(field?.Page==null)reasons.Add("SOURCE_LOCATOR_UNAVAILABLE");
            else if(field.Region==null)reasons.Add("SOURCE_REGION_UNAVAILABLE");
            if(field?.Value!=null && (key is "issue_date" or "expiry_date") && !DateOnly.TryParseExact(field.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))reasons.Add("DATE_FORMAT_REVIEW");
            if(key=="vessel_reference") {
                // A match is a suggestion that the document names this vessel; it is
                // never an identity decision. The human vessel-context confirmation stays.
                var match=CertificateVesselMatch.Match(field?.Value,vessel);
                if(match==null)reasons.Add("SCOPED_REFERENCE_UNMATCHED");
                else{reasons.Add("SCOPED_REFERENCE_MATCH_SUGGESTED");reasons.Add("SCOPED_REFERENCE_MATCHED_BY_"+match);}
            }
            if(key=="person_reference" && (personReference==null || field?.Value==null || !string.Equals(personReference,field.Value,StringComparison.OrdinalIgnoreCase)))reasons.Add("SCOPED_REFERENCE_UNMATCHED");
            reasons.Add("PROVIDER_CANDIDATE_NOT_VERIFICATION");
            return new CertificateCandidate(key,field?.Value,field?.Confidence,field?.Page,low,"REVIEW_REQUIRED",reasons.ToArray(),field?.Region,ambiguous);
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

/// <summary>Server-owned identifiers of the scoped vessel. Imo is only the
/// user-declared Known value from the regulatory draft (unverified provenance).</summary>
public sealed record CertificateVesselIdentifiers(string? Name,string? Code,string? Imo=null)
{
    public override string ToString()=>nameof(CertificateVesselIdentifiers);
}

public static class CertificateVesselMatch
{
    private static readonly string[] Prefixes=["MV","MT","MS","MY","SS","TUG","M V","M T","M S"];
    /// <summary>Upper-case invariant, letters/digits only, single spaces; leading ship-type prefixes removed.</summary>
    public static string Normalize(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return "";
        var builder=new System.Text.StringBuilder(value.Length);
        foreach(var ch in value.Normalize(System.Text.NormalizationForm.FormD)) {
            if(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)==System.Globalization.UnicodeCategory.NonSpacingMark)continue;
            var c=ch switch{'ı'=>'i','İ'=>'I',_=>ch};
            builder.Append(char.IsLetterOrDigit(c)?char.ToUpperInvariant(c):' ');
        }
        var text=string.Join(' ',builder.ToString().Split(' ',StringSplitOptions.RemoveEmptyEntries));
        foreach(var prefix in Prefixes.OrderByDescending(p=>p.Length))
            if(text.StartsWith(prefix+" ",StringComparison.Ordinal) && text.Length>prefix.Length+1){text=text[(prefix.Length+1)..];break;}
        return text;
    }
    private static string Compact(string value)=>value.Replace(" ","",StringComparison.Ordinal);
    /// <summary>Returns IMO, CODE or NAME when the extracted reference equals that identifier after normalization; otherwise null (unmatched).</summary>
    public static string? Match(string? extracted,CertificateVesselIdentifiers? vessel)
    {
        var value=Normalize(extracted);
        if(value.Length==0 || vessel==null)return null;
        var digits=new string(value.Where(char.IsAsciiDigit).ToArray());
        var imo=vessel.Imo is {Length:7} known && known.All(char.IsAsciiDigit)?known:null;
        if(imo!=null && digits==imo && (Compact(value)==imo || Compact(value)=="IMO"+imo))return "IMO";
        var code=Normalize(vessel.Code);
        if(code.Length>=2 && Compact(value)==Compact(code))return "CODE";
        var name=Normalize(vessel.Name);
        if(name.Length>=2 && value==name)return "NAME";
        return null;
    }
}
