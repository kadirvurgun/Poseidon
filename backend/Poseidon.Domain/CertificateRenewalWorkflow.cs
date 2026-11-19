using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-044 / T-S6-002 (18.01 Backlog row45) renewal workflow v1. State names and the
// command order follow 60.04 Rev3 VC-012 paragraphs180-200 and the SoD rows252-253:
// openRenewal→PREPARING, validateRenewal→READY_TO_APPLY, recordApplication→APPLICATION_RECORDED,
// awaitAuthority→AWAITING_AUTHORITY, recordIssuedCandidate→ISSUED_UNVERIFIED,
// verifyRenewal→VERIFIED, completeRenewal→COMPLETED, reject/cancel.
// Every human verification here is an internal attributable review of the pinned
// replacement revision; it is not an issuer/authority verification or legal validity.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRenewalDecision(string? SurveyStatus=null,string? DocumentsStatus=null,string? ActionsStatus=null,
    DateOnly? ApplicationDate=null,string? AuthorityReference=null,
    Guid? NewCertificateId=null,long? NewCertificateRevision=null,uint? NewCertificateVersion=null,CertificateFilePin? NewDocument=null,
    string? IssuerMatch=null,string? NumberMatch=null,string? SubjectMatch=null,string? DateMatch=null,string? ScopeMatch=null,
    CertificateFilePin? Evidence=null)
{
    public override string ToString()=>nameof(CertificateRenewalDecision);
}

// Declared dates of the exact pinned replacement revision (never derived from the old expiry).
public sealed record RenewalReplacementFacts(Guid CertificateId,long Revision,DateOnly? IssueDate,DateOnly? ExpiryDate,CertificateFilePin? Document,bool CurrentHead);

public static class CertificateRenewalWorkflow
{
    public const string Draft="DRAFT",Preparing="PREPARING",ReadyToApply="READY_TO_APPLY",ApplicationRecorded="APPLICATION_RECORDED",
        AwaitingAuthority="AWAITING_AUTHORITY",IssuedUnverified="ISSUED_UNVERIFIED",Verified="VERIFIED",Completed="COMPLETED",Cancelled="CANCELLED",Rejected="REJECTED";
    public static readonly string[] States=[Draft,Preparing,ReadyToApply,ApplicationRecorded,AwaitingAuthority,IssuedUnverified,Verified,Completed,Cancelled,Rejected];
    public static readonly IReadOnlyDictionary<string,(string[] From,string To)> Transitions=new Dictionary<string,(string[],string)>(StringComparer.Ordinal) {
        ["OPEN"]=([Draft],Preparing),
        ["VALIDATE"]=([Preparing],ReadyToApply),
        ["RECORD_APPLICATION"]=([ReadyToApply],ApplicationRecorded),
        ["AWAIT_AUTHORITY"]=([ApplicationRecorded],AwaitingAuthority),
        ["RECORD_ISSUED_CANDIDATE"]=([ApplicationRecorded,AwaitingAuthority],IssuedUnverified),
        ["VERIFY"]=([IssuedUnverified],Verified),
        ["COMPLETE"]=([Verified],Completed),
        ["REJECT"]=([ApplicationRecorded,AwaitingAuthority,IssuedUnverified],Rejected),
        // Own preparation cancel only. After an application was recorded a cancellation
        // receipt is required, which this version does not model (60.04 p198).
        ["CANCEL"]=([Draft,Preparing,ReadyToApply],Cancelled),
    };
    public static bool Known(string? state)=>state!=null && States.Contains(state);
    public static bool Terminal(string state)=>state is Completed or Cancelled or Rejected;
    public static bool Editable(string state)=>state is Draft or Preparing;
    public static bool HumanOnly(string action)=>action is "VERIFY" or "COMPLETE";
    public static string? Permission(string action)=>action switch{"VERIFY"=>"certificate.renewal.verify","COMPLETE"=>"certificate.renewal.complete",_=>null};
    public static string[] Allowed(string state)=>Transitions.Where(t=>t.Value.From.Contains(state)).Select(t=>t.Key).Order(StringComparer.Ordinal).ToArray();

    private static bool Status(string? value,bool? required)=>required switch {
        true=>value=="COMPLETE", false=>value is "COMPLETE" or "NOT_REQUIRED", null=>false };

    // Pure guard: returns a closed error code or null. UNKNOWN is never accepted as done.
    public static string? Check(string action,string state,CertificateRenewalContent content,CertificateRenewalDecision decision,
        bool ownerEligible,DateOnly? originalExpiry,DateOnly today,RenewalReplacementFacts? replacement) {
        if(!Transitions.TryGetValue(action,out var t))return "invalid_transition";
        if(!t.From.Contains(state))return Terminal(state)?"terminal_review":action=="CANCEL"?"renewal_cancellation_receipt_required":"invalid_transition";
        switch(action) {
            case "OPEN":
                if(content.OwnerRoleId==null || !ownerEligible)return "renewal_owner_required";
                if(content.DueDate==null)return "renewal_due_required";
                if(originalExpiry==null)return "renewal_expiry_unknown";
                return null;
            case "VALIDATE":
                if(content.RequiredSurvey==null || content.RequiredDocuments==null || content.RequiredActions==null)return "renewal_requirements_unknown";
                if(!ownerEligible)return "renewal_owner_required";
                return Status(decision.SurveyStatus,content.RequiredSurvey) && Status(decision.DocumentsStatus,content.RequiredDocuments) &&
                    Status(decision.ActionsStatus,content.RequiredActions)?null:"renewal_requirements_incomplete";
            case "RECORD_APPLICATION":
                if(decision.ApplicationDate is not {} applied || applied>today || string.IsNullOrWhiteSpace(decision.AuthorityReference))return "renewal_application_incomplete";
                return null;
            case "RECORD_ISSUED_CANDIDATE":
                if(replacement==null || decision.NewCertificateId!=replacement.CertificateId || decision.NewCertificateRevision!=replacement.Revision)return "renewal_replacement_required";
                if(!replacement.CurrentHead)return "linked_version_conflict";
                if(replacement.IssueDate==null || replacement.ExpiryDate==null || replacement.ExpiryDate<replacement.IssueDate)return "renewal_replacement_dates_unknown";
                if(decision.NewDocument==null || replacement.Document!=decision.NewDocument)return "renewal_evidence_required";
                return null;
            case "VERIFY":
                if(replacement==null || content.NewCertificateId!=replacement.CertificateId || content.NewCertificateRevision!=replacement.Revision || content.NewDocument==null)return "renewal_replacement_required";
                if(!replacement.CurrentHead)return "linked_version_conflict";
                return new[]{decision.IssuerMatch,decision.NumberMatch,decision.SubjectMatch,decision.DateMatch,decision.ScopeMatch}.All(m=>m=="MATCH")?null:"renewal_verification_not_matched";
            case "COMPLETE":
                if(replacement==null || content.NewCertificateId!=replacement.CertificateId || content.NewCertificateRevision!=replacement.Revision)return "renewal_replacement_required";
                if(!replacement.CurrentHead)return "linked_version_conflict";
                // Effective boundary: a future-dated replacement never supersedes today's revision early.
                if(replacement.IssueDate is not {} issued || issued>today)return "renewal_replacement_not_effective";
                return null;
            case "REJECT":
                return decision.Evidence==null?"renewal_evidence_required":null;
            default:
                return null;
        }
    }
    public static bool DecisionShape(string action,CertificateRenewalDecision d) {
        static bool Match(string? v)=>v is null or "MATCH" or "MISMATCH" or "UNKNOWN";
        static bool Pin(CertificateFilePin? p)=>p==null || p.Id!=Guid.Empty && p.Revision>0 && p.Checksum is {Length:64} && p.Checksum.All(char.IsAsciiHexDigit);
        if(d.AuthorityReference is {} r && (r.Length>4096 || r.Any(char.IsControl)))return false;
        if(new[]{d.SurveyStatus,d.DocumentsStatus,d.ActionsStatus}.Any(s=>s is not(null or "COMPLETE" or "NOT_REQUIRED" or "UNKNOWN")))return false;
        if(!new[]{d.IssuerMatch,d.NumberMatch,d.SubjectMatch,d.DateMatch,d.ScopeMatch}.All(Match) || !Pin(d.NewDocument) || !Pin(d.Evidence))return false;
        if(d.NewCertificateId==Guid.Empty || (d.NewCertificateId==null)!=(d.NewCertificateRevision==null) || (d.NewCertificateId==null)!=(d.NewCertificateVersion==null) ||
            d.NewCertificateRevision<=0 || d.NewCertificateVersion==0)return false;
        // Facts belong only to the transition that owns them.
        var application=d.ApplicationDate!=null || d.AuthorityReference!=null;
        var requirements=d.SurveyStatus!=null || d.DocumentsStatus!=null || d.ActionsStatus!=null;
        var candidate=d.NewCertificateId!=null || d.NewDocument!=null;
        var verification=d.IssuerMatch!=null || d.NumberMatch!=null || d.SubjectMatch!=null || d.DateMatch!=null || d.ScopeMatch!=null;
        return (application?action=="RECORD_APPLICATION":true) && (requirements?action=="VALIDATE":true) &&
            (candidate?action=="RECORD_ISSUED_CANDIDATE":true) && (verification?action=="VERIFY":true) && (d.Evidence==null || action=="REJECT");
    }
    // Facts recorded by a transition are carried into the immutable revision content.
    public static CertificateRenewalContent Apply(string action,CertificateRenewalContent content,CertificateRenewalDecision d,RenewalReplacementFacts? replacement)=>action switch {
        "RECORD_APPLICATION"=>content with{ApplicationDate=d.ApplicationDate,AuthorityReference=d.AuthorityReference},
        "RECORD_ISSUED_CANDIDATE"=>content with{NewCertificateId=d.NewCertificateId,NewCertificateRevision=d.NewCertificateRevision,NewCertificateVersion=d.NewCertificateVersion,
            NewDocument=d.NewDocument,NewIssueDate=replacement?.IssueDate,NewExpiryDate=replacement?.ExpiryDate},
        _=>content,
    };
    // Separation of duties (60.04 p196, p252-253): verifier is not the candidate recorder or
    // replacement author; the completing reviewer is a second actor distinct from all of them.
    public static bool Separated(string action,Guid actor,Guid? candidateRecorder,Guid? replacementAuthor,Guid? verifier)=>action switch {
        "VERIFY"=>actor!=candidateRecorder && actor!=replacementAuthor,
        "COMPLETE"=>verifier!=null && actor!=verifier && actor!=candidateRecorder && actor!=replacementAuthor,
        _=>true,
    };
    public static string[] Blockers(string state,CertificateRenewalContent content,bool ownerEligible) {
        var blockers=new List<string>{"SOURCE_QUALIFICATION_REQUIRED"};
        if(state is not(Verified or Completed))blockers.Add("HUMAN_VERIFICATION_REQUIRED");
        if(state is Cancelled or Rejected)return [..blockers,"RENEWAL_CLOSED_WITHOUT_REPLACEMENT"];
        if(state!=Completed)blockers.Add("COMPLETION_PREREQUISITES_REQUIRED");
        if(content.OwnerRoleId==null || !ownerEligible)blockers.Add("RENEWAL_OWNER_UNAVAILABLE");
        if(content.NewCertificateId==null)blockers.Add("REPLACEMENT_CERTIFICATE_NOT_PINNED");
        if(content.NewDocument==null)blockers.Add("RENEWAL_EVIDENCE_NOT_LINKED");
        if(content.DueDate==null)blockers.Add("RENEWAL_DUE_NOT_DECLARED");
        if(content.RequiredSurvey==null || content.RequiredDocuments==null || content.RequiredActions==null)blockers.Add("RENEWAL_REQUIREMENTS_UNKNOWN");
        return blockers.ToArray();
    }
}
