using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-104 / T-CERT2-001 (18.01 sheet:Backlog/row:105): template/items/owner/due/evidence/
// verification/finding/voyage-fit; an unresolved critical item blocks survey readiness.
// Field contract: 60.04 Rev3 PreSurveyChecklistRevision p135-136, VC-010 p140 (NA only with
// allowNA and an independent reviewer), screen p225-226, permissions p245-246, commands p347.
// Readiness is an advisory preparation state. It is never survey approval, a booking,
// a performed survey or certificate validity, and it is never computed as READY.
public sealed record PreSurveyTemplateItem(string ItemCode,bool Critical,bool Required,bool EvidenceRequired,bool AllowNotApplicable);
public sealed record PreSurveyTemplate(string Code,string Revision,string SurveyType,string Status,PreSurveyTemplateItem[] Items);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyPlan(DateOnly? PlannedStart=null,DateOnly? PlannedEnd=null,Guid? PortCallId=null,long? PortCallRevision=null,string? LocationText=null)
{
    public override string ToString()=>nameof(PreSurveyPlan);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyItemInput(Guid ItemId,Guid? OwnerRoleId,DateOnly? DueDate,string Answer,CertificateFilePin[] Evidence,string? Finding=null,string? Comment=null)
{
    public override string ToString()=>nameof(PreSurveyItemInput);
}

// Human verification record. VerifierId/AnsweredBy stay server-side; DTOs project booleans only.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyVerification(string State,Guid? VerifierId=null,DateTimeOffset? RecordedAt=null,string? ItemHash=null,string? ReasonCode=null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyItem(Guid ItemId,string ItemCode,bool Critical,bool Required,bool EvidenceRequired,bool AllowNotApplicable,
    Guid? OwnerRoleId,DateOnly? DueDate,string Answer,CertificateFilePin[] Evidence,string? Finding,string? Comment,Guid? AnsweredBy,PreSurveyVerification Verification);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyChecklistContent(string TemplateCode,string TemplateRevision,string SurveyType,PreSurveyPlan Plan,PreSurveyItem[] Items)
{
    public override string ToString()=>nameof(PreSurveyChecklistContent);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyVerificationDecision(Guid ItemId,string ExpectedItemHash,string Decision,string? ReasonCode=null);

public sealed record PreSurveyItemError(Guid ItemId,string Code);
public sealed record PreSurveyReadiness(string State,string Qualification,string[] Blockers,int CriticalUnresolved,int RequiredUnresolved,int Verified,int Total);

public static class PreSurveyChecklistPolicy
{
    public const string SourceVersion="18.01:row105+60.04Rev3:p135-140,p225-226,p245-246,p347,p637-639:v46.3";
    public const string Qualification="ADVISORY_NOT_SURVEY_APPROVAL";
    public static readonly string[] Answers=["OPEN","READY","ATTENTION","BLOCKED","NOT_APPLICABLE"];
    public static readonly string[] VerificationStates=["PENDING","VERIFIED","REJECTED"];
    public static readonly string[] RejectReasons=["EVIDENCE_INSUFFICIENT","ANSWER_NOT_SUPPORTED","NOT_APPLICABLE_NOT_JUSTIFIED","OTHER_SEE_COMMENT"];
    public static readonly string[] ReadinessStates=["BLOCKED","INCOMPLETE","PREPARED_PENDING_HUMAN_CONFIRMATION"];
    public static readonly string[] Transitions=["CREATE","UPDATE","VERIFY"];
    public const int MaxEvidence=10, MaxText=4000, MaxLocation=200, MaxDecisions=100;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);

    // Product starter scaffolding, not an approved regulatory/class/flag catalog. 60.04 requires
    // template create/submit/approve/publish (cert.survey.template.manage/approve); that governance
    // is not built here, so every template stays STARTER_UNAPPROVED and is disclosed as such.
    private static readonly PreSurveyTemplateItem[] Common=[
        new("CERTIFICATE_ORIGINALS_AVAILABLE",true,true,true,false),
        new("PREVIOUS_FINDINGS_REVIEWED",true,true,true,false),
        new("OPEN_CONDITIONS_REVIEWED",true,true,true,false),
        new("PMS_DUE_JOBS_REVIEWED",false,true,true,false),
        new("SURVEYOR_ACCESS_ARRANGED",false,true,false,false),
        new("AUTHORITY_COORDINATION_RECORDED",false,true,true,false),
        new("SPARES_AND_TOOLS_PREPARED",false,false,false,true)];
    public static readonly PreSurveyTemplate[] Templates=[
        new("STARTER-ANNUAL","1","ANNUAL","STARTER_UNAPPROVED",Common),
        new("STARTER-INTERMEDIATE","1","INTERMEDIATE","STARTER_UNAPPROVED",Common),
        new("STARTER-RENEWAL","1","RENEWAL","STARTER_UNAPPROVED",[..Common,new("RENEWAL_APPLICATION_REFERENCE_RECORDED",true,true,true,false)])];
    public static PreSurveyTemplate? Template(string? code)=>Templates.SingleOrDefault(t=>t.Code==code);

    public static Guid ItemId(Guid checklist,string itemCode)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.presurvey.item.v1:"+checklist.ToString("D")+":"+itemCode)).AsSpan(0,16));
    public static PreSurveyChecklistContent Instantiate(Guid checklist,PreSurveyTemplate template,PreSurveyPlan plan)=>new(template.Code,template.Revision,template.SurveyType,plan,
        template.Items.Select(i=>new PreSurveyItem(ItemId(checklist,i.ItemCode),i.ItemCode,i.Critical,i.Required,i.EvidenceRequired,i.AllowNotApplicable,
            null,null,"OPEN",[],null,null,null,new("PENDING"))).ToArray());

    // The hash binds a human verification to the exact answer/evidence/owner/due/finding it saw.
    public static string ItemHash(PreSurveyItem item)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{
        item.ItemId,item.ItemCode,item.OwnerRoleId,item.DueDate,item.Answer,evidence=item.Evidence.Select(e=>new{e.Id,e.Revision,checksum=e.Checksum.ToUpperInvariant()}),item.Finding,item.Comment},Json)));

    private static bool Text(string? value,int max)=>value==null || value.Length>0 && value.Length<=max && !value.Any(c=>char.IsControl(c) && c is not('\r' or '\n' or '\t'));
    public static bool ValidPin(CertificateFilePin? pin)=>pin!=null && pin.Id!=Guid.Empty && pin.Revision>0 && pin.Checksum is {Length:64} && pin.Checksum.All(char.IsAsciiHexDigit);
    public static bool ValidPlan(PreSurveyPlan? plan)=>plan!=null && (plan.PortCallId==null)==(plan.PortCallRevision==null) && plan.PortCallId!=Guid.Empty &&
        (plan.PortCallRevision==null || plan.PortCallRevision>0) && (plan.PlannedStart==null || plan.PlannedEnd==null || plan.PlannedEnd>=plan.PlannedStart) &&
        Text(plan.LocationText,MaxLocation) && !(plan.LocationText?.Any(c=>c is '\r' or '\n' or '\t')??false);
    public static bool ValidInput(PreSurveyItemInput? item)=>item!=null && item.ItemId!=Guid.Empty && item.OwnerRoleId!=Guid.Empty && Answers.Contains(item.Answer) &&
        item.Evidence is {Length:<=MaxEvidence} evidence && evidence.All(ValidPin) && evidence.Select(e=>e.Id).Distinct().Count()==evidence.Length &&
        Text(item.Finding,MaxText) && Text(item.Comment,MaxText);
    public static bool ValidContent(PreSurveyChecklistContent? c)=>c!=null && Template(c.TemplateCode) is {} t && t.Revision==c.TemplateRevision && t.SurveyType==c.SurveyType &&
        ValidPlan(c.Plan) && c.Items.Length==t.Items.Length && c.Items.Zip(t.Items).All(p=>p.First.ItemCode==p.Second.ItemCode && p.First.Critical==p.Second.Critical &&
            p.First.Required==p.Second.Required && p.First.EvidenceRequired==p.Second.EvidenceRequired && p.First.AllowNotApplicable==p.Second.AllowNotApplicable &&
            VerificationStates.Contains(p.First.Verification.State) && ValidInput(new(p.First.ItemId,p.First.OwnerRoleId,p.First.DueDate,p.First.Answer,p.First.Evidence,p.First.Finding,p.First.Comment)));

    // Authoring: every template item must be present exactly once (a required item cannot be
    // dropped). A changed item records its author and resets its verification to PENDING.
    public static (PreSurveyChecklistContent? Content,string? Error) Apply(PreSurveyChecklistContent current,PreSurveyPlan plan,PreSurveyItemInput[] inputs,Guid actor) {
        if(!ValidPlan(plan) || inputs.Length!=current.Items.Length || inputs.Select(i=>i.ItemId).Distinct().Count()!=inputs.Length || !inputs.All(ValidInput))return (null,"validation_failed");
        var byId=inputs.ToDictionary(i=>i.ItemId);
        if(current.Items.Any(i=>!byId.ContainsKey(i.ItemId)))return (null,"checklist_items_mismatch");
        var items=new List<PreSurveyItem>();
        foreach(var old in current.Items) {
            var input=byId[old.ItemId];
            if(input.Answer=="NOT_APPLICABLE" && !old.AllowNotApplicable)return (null,"na_not_authorized");
            var next=old with{OwnerRoleId=input.OwnerRoleId,DueDate=input.DueDate,Answer=input.Answer,Evidence=input.Evidence,Finding=input.Finding,Comment=input.Comment};
            items.Add(ItemHash(next)==ItemHash(old)?old:next with{AnsweredBy=actor,Verification=new("PENDING")});
        }
        return (current with{Plan=plan,Items=items.ToArray()},null);
    }

    // Human verification of answered items. Every failing item is reported (one failure never
    // hides another) and nothing is recorded unless all decisions are admissible.
    public static (PreSurveyChecklistContent? Content,PreSurveyItemError[] Errors) Verify(PreSurveyChecklistContent current,PreSurveyVerificationDecision[] decisions,Guid verifier,DateTimeOffset now) {
        var errors=new List<PreSurveyItemError>();
        if(decisions.Length is 0 or >MaxDecisions || decisions.Select(d=>d.ItemId).Distinct().Count()!=decisions.Length)return (null,[new(Guid.Empty,"validation_failed")]);
        var items=current.Items.ToDictionary(i=>i.ItemId);
        foreach(var d in decisions) {
            if(d.Decision is not("VERIFIED" or "REJECTED") || d.Decision=="VERIFIED" && d.ReasonCode!=null || d.Decision=="REJECTED" && !RejectReasons.Contains(d.ReasonCode)) {errors.Add(new(d.ItemId,"validation_failed"));continue;}
            if(!items.TryGetValue(d.ItemId,out var item)) {errors.Add(new(d.ItemId,"item_not_found"));continue;}
            var hash=ItemHash(item);
            if(!string.Equals(hash,d.ExpectedItemHash,StringComparison.OrdinalIgnoreCase)) {errors.Add(new(d.ItemId,"item_changed"));continue;}
            if(item.Answer=="OPEN" || item.AnsweredBy==null) {errors.Add(new(d.ItemId,"item_not_answered"));continue;}
            // Independent review: the person who recorded the answer cannot verify it (60.04 p140/p246).
            if(item.AnsweredBy==verifier) {errors.Add(new(d.ItemId,"separation_of_duties"));continue;}
            if(item.Answer=="NOT_APPLICABLE" && !item.AllowNotApplicable) {errors.Add(new(d.ItemId,"na_not_authorized"));continue;}
            if(d.Decision=="VERIFIED" && item.EvidenceRequired && item.Answer=="READY" && item.Evidence.Length==0) {errors.Add(new(d.ItemId,"evidence_required"));continue;}
            if(d.Decision=="VERIFIED" && item.Answer is "ATTENTION" or "BLOCKED") {errors.Add(new(d.ItemId,"item_not_resolved"));continue;}
            if(item.Verification.State!="PENDING" && item.Verification.ItemHash==hash) {errors.Add(new(d.ItemId,"already_recorded"));continue;}
        }
        if(errors.Count>0)return (null,errors.ToArray());
        var byId=decisions.ToDictionary(d=>d.ItemId);
        return (current with{Items=current.Items.Select(i=>byId.TryGetValue(i.ItemId,out var d)?i with{Verification=new(d.Decision,verifier,now,ItemHash(i),d.ReasonCode)}:i).ToArray()},[]);
    }

    public static bool VerifiedCurrent(PreSurveyItem item)=>item.Verification.State=="VERIFIED" && item.Verification.ItemHash==ItemHash(item);
    public static bool Unresolved(PreSurveyItem item)=>item.Answer is "OPEN" or "ATTENTION" or "BLOCKED" || !VerifiedCurrent(item);
    public static string[] ItemBlockers(PreSurveyItem item,DateOnly today) {
        var b=new List<string>();
        if(item.Answer=="OPEN")b.Add("ITEM_NOT_ANSWERED");
        if(item.Answer=="BLOCKED")b.Add("ITEM_BLOCKED");
        if(item.Answer=="ATTENTION")b.Add("ITEM_NEEDS_ATTENTION");
        if(item.Finding!=null && item.Answer is "ATTENTION" or "BLOCKED")b.Add("OPEN_FINDING");
        if(item.Answer is "READY" or "NOT_APPLICABLE" && !VerifiedCurrent(item))b.Add(item.Verification.State=="REJECTED" && item.Verification.ItemHash==ItemHash(item)?"VERIFICATION_REJECTED":"VERIFICATION_PENDING");
        if(item.EvidenceRequired && item.Answer=="READY" && item.Evidence.Length==0)b.Add("EVIDENCE_REQUIRED");
        if(item.OwnerRoleId==null)b.Add("OWNER_UNASSIGNED");
        if(item.DueDate==null)b.Add("DUE_DATE_UNKNOWN");
        else if(item.DueDate<today && Unresolved(item))b.Add("ITEM_OVERDUE");
        return b.ToArray();
    }
    // Advisory only: BLOCKED when any critical item is unresolved; INCOMPLETE when any required
    // item is unresolved; otherwise PREPARED_PENDING_HUMAN_CONFIRMATION. There is no READY state.
    public static PreSurveyReadiness Readiness(PreSurveyChecklistContent c,DateOnly today) {
        var critical=c.Items.Count(i=>i.Critical && Unresolved(i));var required=c.Items.Count(i=>i.Required && !i.Critical && Unresolved(i));
        var blockers=new List<string>();
        if(critical>0)blockers.Add("CRITICAL_ITEM_UNRESOLVED");
        if(required>0)blockers.Add("REQUIRED_ITEM_UNRESOLVED");
        blockers.AddRange(c.Items.Where(i=>i.Required || i.Answer!="OPEN").SelectMany(i=>ItemBlockers(i,today)).Distinct().Order(StringComparer.Ordinal));
        blockers.Add("TEMPLATE_NOT_APPROVED");blockers.Add("SURVEY_BOOKING_AND_AUTHORITY_EXTERNAL");
        var state=critical>0?"BLOCKED":required>0?"INCOMPLETE":"PREPARED_PENDING_HUMAN_CONFIRMATION";
        return new(state,Qualification,blockers.Distinct().ToArray(),critical,required,c.Items.Count(VerifiedCurrent),c.Items.Length);
    }
}
