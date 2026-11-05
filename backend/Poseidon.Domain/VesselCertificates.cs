using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

public sealed record CertificateFilePin(Guid Id,long Revision,string Checksum);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VesselCertificateContent(CertificateMetadata Metadata,string? DocumentName=null,string? RegulatoryBasis=null,
    string? DeclaredFlag=null,string? Availability=null,DateOnly? SurveyWindowStart=null,string? SurveyWindowEnd=null,CertificateFilePin? Document=null)
{
    public override string ToString()=>nameof(VesselCertificateContent);
}
public sealed record CertificateDateFactsDto(string Issue,string Expiry,string Order);
public sealed record CertificateCalendarDto(string State,DateOnly ReferenceDate,int? DaysToDeclaredExpiry,
    string SurveyState,CertificateDateFactsDto Dates,string Semantics="DeclaredUtcCalendarNotLegalValidity");

// Source 14.02 rows33-40,1634-1657,1708-1755. These are declarations. Neither
// a date calculation nor clean file transport qualifies a certificate/issuer.
public static class VesselCertificatePolicy
{
    public const string SourceVersion="14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:33-40,1634-1657,1708-1755:v46.3";
    public static bool Valid(VesselCertificateContent? content) {
        if(content?.Metadata is not {} metadata || !CertificateMetadataPolicy.Valid(metadata))return false;
        // Keep source-specific crew fields in the actual Credential aggregate.
        if(metadata.IssuingCountryNumeric!=null || metadata.EndorsementRequired!=null || metadata.EndorsementNumber!=null ||
            metadata.EndorsementExpiry!=null || metadata.StcwReference!=null || metadata.TankerRelevance!=null || metadata.VesselTypeScope!=null)return false;
        if(new[]{content.DocumentName,content.RegulatoryBasis,content.DeclaredFlag,content.SurveyWindowEnd}.Any(v=>v!=null &&
            (v.Length>CertificateMetadataPolicy.MaxText || v.Any(c=>char.IsControl(c)&&c is not ('\r' or '\n' or '\t')))))return false;
        if(content.Availability is not(null or "Available" or "Missing" or "Review"))return false;
        if(content.Document is {} file && (file.Id==Guid.Empty || file.Revision<=0 || file.Checksum is not {Length:64} || !file.Checksum.All(char.IsAsciiHexDigit)))return false;
        return metadata.DocumentReference==content.Document?.Id;
    }
    public static CertificateCalendarDto Calendar(CredentialDeclaration declaration,DateOnly reference,DateOnly? surveyStart=null,string? surveyEnd=null) {
        var facts=CredentialDeclarationDates.Evaluate(declaration,reference);
        var days=declaration.ExpiryDate is {} expiry?expiry.DayNumber-reference.DayNumber:(int?)null;
        var end=DateOnly.TryParseExact(surveyEnd,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)?parsed:(DateOnly?)null;
        var survey=surveyStart==null && surveyEnd==null?"NOT_DECLARED":surveyStart==null || end==null || end<surveyStart?"REVIEW_REQUIRED":
            reference<surveyStart?"BEFORE_WINDOW":reference>end?"AFTER_WINDOW":"WINDOW_OPEN";
        var state=declaration.IssueDate==null || declaration.ExpiryDate==null || declaration.IssueDate>reference ||
            facts.Order is DeclaredCredentialDateOrder.ExpiryBeforeIssue || survey=="REVIEW_REQUIRED"?"REVIEW_REQUIRED":
            days<0?"EXPIRED":survey=="WINDOW_OPEN"?"SURVEY_WINDOW":days<90?"DUE":"VALID";
        return new(state,reference,days,survey,new(facts.Issue.ToString(),facts.Expiry.ToString(),facts.Order.ToString()));
    }
    public static string[] Blockers(VesselCertificateContent content) {
        var d=content.Metadata.Declaration;var missing=new List<string>();
        if(string.IsNullOrWhiteSpace(content.DocumentName) && string.IsNullOrWhiteSpace(d.CertificateTypeReference))missing.Add("CERTIFICATE_TYPE_UNRESOLVED");
        if(string.IsNullOrWhiteSpace(d.Issuer))missing.Add("ISSUER_NOT_DECLARED");
        if(content.Document==null)missing.Add("CERTIFICATE_EVIDENCE_NOT_LINKED");
        missing.Add("SOURCE_QUALIFICATION_REQUIRED");missing.Add("HUMAN_VERIFICATION_REQUIRED");
        return missing.ToArray();
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRenewalContent(string? RenewalType=null,DateOnly? TriggerDate=null,string? LeadDaysNumeric=null,Guid? OwnerRoleId=null,
    bool? RequiredSurvey=null,bool? RequiredDocuments=null,bool? RequiredActions=null,string? PlannedLocation=null,DateOnly? ApplicationDate=null,
    string? AuthorityReference=null,DateOnly? NewIssueDate=null,DateOnly? NewExpiryDate=null,Guid? NewCertificateId=null,
    long? NewCertificateRevision=null,uint? NewCertificateVersion=null,CertificateFilePin? NewDocument=null,
    DateOnly? DueDate=null,DateOnly? SurveyWindowStart=null,string? SurveyWindowEnd=null)
{
    public override string ToString()=>nameof(CertificateRenewalContent);
}
public static class CertificateRenewalPolicy
{
    public static bool Valid(CertificateRenewalContent? c)=>c!=null && CertificateMetadataPolicy.ExactNumeric(c.LeadDaysNumeric) &&
        new[]{c.RenewalType,c.PlannedLocation,c.AuthorityReference,c.SurveyWindowEnd}.All(s=>s==null || s.Length<=4096 && !s.Any(char.IsControl)) && c.OwnerRoleId!=Guid.Empty && c.NewCertificateId!=Guid.Empty &&
        (c.NewCertificateId==null && c.NewCertificateRevision==null && c.NewCertificateVersion==null || c.NewCertificateId!=null && c.NewCertificateRevision>0 && c.NewCertificateVersion>0) &&
        (c.NewDocument==null || c.NewDocument.Id!=Guid.Empty && c.NewDocument.Revision>0 && c.NewDocument.Checksum is {Length:64} && c.NewDocument.Checksum.All(char.IsAsciiHexDigit));
}
