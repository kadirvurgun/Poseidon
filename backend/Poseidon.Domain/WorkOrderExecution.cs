using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-054 / T-S8-003 (18.01 sheet:Backlog/row:55): RELEASED -> IN_PROGRESS -> EVIDENCE_SUBMITTED with
// readings, spares, photo and document evidence; verification/closure are impossible without the
// required evidence. 13.04 paragraph:91 names the full state set PLANNED / RELEASED / IN_PROGRESS /
// EVIDENCE_SUBMITTED / VERIFIED / CLOSED; PLANNED is the existing owning WorkOrder draft.
// VERIFIED and CLOSED are attributed human records by a person who did not execute the work
// (ISM Code 10.2 independent verification; 11.01 rts_verification "Controlled Decision").
// The required-evidence set is an attributed human release declaration. When nobody declared it the
// gate is REVIEW_REQUIRED and verification stays blocked: an unknown policy is never "nothing required".
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderReading(string Code,string Value,string? Unit=null,DateTimeOffset? TakenAt=null,string? Note=null)
{
    public override string ToString()=>nameof(WorkOrderReading);
}
// A catalogued spare (SpareId+SpareRevision) feeds the DEV-110 stock projection; a free-text spare
// (Description only) is recorded evidence but never changes any projected stock.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderSpareUse(Guid? SpareId,long? SpareRevision,string Quantity,string? Description=null)
{
    public override string ToString()=>nameof(WorkOrderSpareUse);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderEvidenceFile(string Kind,CertificateFilePin File,string? Caption=null)
{
    public override string ToString()=>nameof(WorkOrderEvidenceFile);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderEvidence(WorkOrderReading[] Readings,WorkOrderSpareUse[] Spares,WorkOrderEvidenceFile[] Files,string? WorkDone=null,string? Findings=null)
{
    public static WorkOrderEvidence Empty { get; }=new([],[],[]);
    public override string ToString()=>nameof(WorkOrderEvidence);
}
// Actor identities stay server-side; DTOs project only booleans and timestamps.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderTransitionRecord(Guid ActorId,DateTimeOffset RecordedAt,string? Hash=null,string? ReasonCode=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderExecutionContent(string State,string[]? RequiredEvidence,WorkOrderEvidence Evidence,Guid[] Executors,
    WorkOrderTransitionRecord Release,WorkOrderTransitionRecord? Requirements=null,WorkOrderTransitionRecord? Start=null,WorkOrderTransitionRecord? Submission=null,
    WorkOrderTransitionRecord? Verification=null,WorkOrderTransitionRecord? Closure=null,WorkOrderTransitionRecord? LastReturn=null)
{
    public override string ToString()=>nameof(WorkOrderExecutionContent);
}
public sealed record WorkOrderEvidenceGate(string State,string[] Required,string[] Present,string[] Missing);
public sealed record WorkOrderExecutionError(int Status,string Code);

public static class WorkOrderExecutionPolicy
{
    public const string SourceVersion="18.01:row55+13.04:p91+14.02:1449-1462:v46.3";
    public const string Qualification="HUMAN_RECORDED_NOT_RETURN_TO_SERVICE";
    public static readonly string[] States=["RELEASED","IN_PROGRESS","EVIDENCE_SUBMITTED","VERIFIED","CLOSED"];
    public static readonly string[] Transitions=["RELEASE","REQUIREMENTS","START","EVIDENCE","SUBMIT","VERIFY","RETURN","CLOSE"];
    public static readonly string[] Actions=["RELEASE","REQUIREMENTS","START","SUBMIT","VERIFY","RETURN","CLOSE"];
    public static readonly string[] EvidenceKinds=["READING","SPARES_RECORD","PHOTO","DOCUMENT","WORK_DONE"];
    public static readonly string[] FileKinds=["PHOTO","DOCUMENT"];
    public static readonly string[] ReturnReasons=["EVIDENCE_INSUFFICIENT","READING_NOT_ACCEPTED","WORK_INCOMPLETE","OTHER_SEE_FINDINGS"];
    public static readonly string[] GateStates=["REVIEW_REQUIRED","MISSING","PRESENT_PENDING_HUMAN_VERIFICATION","HUMAN_VERIFIED"];
    public const int MaxReadings=50,MaxSpares=50,MaxFiles=20,MaxText=4000;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);

    private static bool Text(string? value,int max,bool lines=false)=>value==null || value.Length>0 && value.Length<=max && !string.IsNullOrWhiteSpace(value) &&
        !value.Any(c=>char.IsControl(c) && !(lines && c is '\r' or '\n' or '\t'));
    // Readings keep the declared lexical value: no unit conversion, tolerance or threshold is inferred.
    public static bool LexicalNumber(string? text) {
        if(text is not {Length:>=1 and <=48})return false;
        var digits=text[0]=='-'?text[1..]:text;var parts=digits.Split('.');
        return parts.Length is 1 or 2 && parts.All(p=>p.Length>0 && p.All(char.IsAsciiDigit));
    }
    public static bool PositiveQuantity(string? text,out decimal value) {
        value=0;
        return InvariantDecimal.TryNormalize(text,out var canonical) && canonical==text &&
            decimal.TryParse(canonical,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value) && value>0;
    }
    public static bool ValidPin(CertificateFilePin? pin)=>pin!=null && pin.Id!=Guid.Empty && pin.Revision>0 && pin.Checksum is {Length:64} && pin.Checksum.All(char.IsAsciiHexDigit);
    public static bool ValidEvidence(WorkOrderEvidence? e)=>e!=null && e.Readings is {Length:<=MaxReadings} readings && e.Spares is {Length:<=MaxSpares} spares && e.Files is {Length:<=MaxFiles} files &&
        readings.All(r=>r!=null && Text(r.Code,64) && LexicalNumber(r.Value) && Text(r.Unit,32) && Text(r.Note,500,true) && (r.TakenAt==null || r.TakenAt.Value.Offset==TimeSpan.Zero)) &&
        readings.Select(r=>r.Code).Distinct(StringComparer.Ordinal).Count()==readings.Length &&
        spares.All(s=>s!=null && PositiveQuantity(s.Quantity,out _) && Text(s.Description,200) &&
            (s.SpareId==null ? s.SpareRevision==null && s.Description!=null : s.SpareId!=Guid.Empty && s.SpareRevision>0)) &&
        spares.Where(s=>s.SpareId!=null).Select(s=>s.SpareId).Distinct().Count()==spares.Count(s=>s.SpareId!=null) &&
        files.All(f=>f!=null && FileKinds.Contains(f.Kind) && ValidPin(f.File) && Text(f.Caption,200)) &&
        files.Select(f=>f.File.Id).Distinct().Count()==files.Length && Text(e.WorkDone,MaxText,true) && Text(e.Findings,MaxText,true);
    public static bool ValidRequired(string[]? required)=>required==null || required.Length is >0 and <=5 && required.All(EvidenceKinds.Contains) &&
        required.Distinct(StringComparer.Ordinal).Count()==required.Length;
    public static string[]? Normalize(string[]? required)=>required?.OrderBy(k=>Array.IndexOf(EvidenceKinds,k)).ToArray();

    // Binds human verification/closure to the exact evidence it saw (checksums are case-normalized).
    public static string EvidenceHash(WorkOrderEvidence e)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{
        readings=e.Readings.Select(r=>new{r.Code,r.Value,r.Unit,r.TakenAt,r.Note}),spares=e.Spares.Select(s=>new{s.SpareId,s.SpareRevision,s.Quantity,s.Description}),
        files=e.Files.Select(f=>new{f.Kind,f.File.Id,f.File.Revision,checksum=f.File.Checksum.ToUpperInvariant(),f.Caption}),e.WorkDone,e.Findings},Json)));

    public static string[] PresentKinds(WorkOrderEvidence e)=>EvidenceKinds.Where(k=>k switch {
        "READING"=>e.Readings.Length>0, "SPARES_RECORD"=>e.Spares.Length>0, "PHOTO"=>e.Files.Any(f=>f.Kind=="PHOTO"),
        "DOCUMENT"=>e.Files.Any(f=>f.Kind=="DOCUMENT"), _=>e.WorkDone!=null }).ToArray();
    public static WorkOrderEvidenceGate Gate(WorkOrderExecutionContent c) {
        var present=PresentKinds(c.Evidence);
        if(c.RequiredEvidence==null)return new("REVIEW_REQUIRED",[],present,[]);
        var missing=c.RequiredEvidence.Where(k=>!present.Contains(k)).ToArray();
        if(missing.Length>0)return new("MISSING",c.RequiredEvidence,present,missing);
        var verified=c.State is "VERIFIED" or "CLOSED" && c.Verification?.Hash==EvidenceHash(c.Evidence);
        return new(verified?"HUMAN_VERIFIED":"PRESENT_PENDING_HUMAN_VERIFICATION",c.RequiredEvidence,present,[]);
    }

    public static WorkOrderExecutionContent Released(Guid actor,DateTimeOffset now,string workOrderHash,string[]? required)=>
        new("RELEASED",Normalize(required),WorkOrderEvidence.Empty,[],new(actor,now,workOrderHash));

    private static WorkOrderExecutionError E(int status,string code)=>new(status,code);
    private static Guid[] With(Guid[] executors,Guid actor)=>executors.Contains(actor)?executors:[..executors,actor];

    // Execution authoring: only while IN_PROGRESS. Every author becomes an executor for separation of duties.
    public static (WorkOrderExecutionContent? Content,WorkOrderExecutionError? Error) RecordEvidence(WorkOrderExecutionContent c,WorkOrderEvidence evidence,Guid actor) {
        if(!ValidEvidence(evidence))return (null,E(400,"validation_failed"));
        if(c.State!="IN_PROGRESS")return (null,E(409,"invalid_transition"));
        return (c with{Evidence=evidence,Executors=With(c.Executors,actor)},null);
    }

    public static (WorkOrderExecutionContent? Content,WorkOrderExecutionError? Error) Apply(WorkOrderExecutionContent c,string action,Guid actor,DateTimeOffset now,
        string[]? required=null,string? expectedEvidenceHash=null,string? reason=null) {
        var hash=EvidenceHash(c.Evidence);var gate=Gate(c);
        bool Executor()=>c.Executors.Contains(actor) || c.Start?.ActorId==actor || c.Submission?.ActorId==actor;
        switch(action) {
        case "REQUIREMENTS":
            if(!ValidRequired(required) || required==null || reason!=null || expectedEvidenceHash!=null)return (null,E(400,"validation_failed"));
            if(c.State is "VERIFIED" or "CLOSED")return (null,E(409,"invalid_transition"));
            // Policy owner must not be the executor: otherwise the executor could shrink what is required.
            if(Executor())return (null,E(409,"separation_of_duties"));
            return (c with{RequiredEvidence=Normalize(required),Requirements=new(actor,now)},null);
        case "START":
            if(required!=null || reason!=null || expectedEvidenceHash!=null)return (null,E(400,"validation_failed"));
            if(c.State!="RELEASED")return (null,E(409,"invalid_transition"));
            return (c with{State="IN_PROGRESS",Start=new(actor,now),Executors=With(c.Executors,actor)},null);
        case "SUBMIT":
            if(required!=null || reason!=null || expectedEvidenceHash is not {Length:64})return (null,E(400,"validation_failed"));
            if(c.State!="IN_PROGRESS")return (null,E(409,"invalid_transition"));
            if(!string.Equals(expectedEvidenceHash,hash,StringComparison.OrdinalIgnoreCase))return (null,E(409,"evidence_changed"));
            // Declared requirements must be present to submit; an unconfigured policy is disclosed as
            // REVIEW_REQUIRED and is resolved before verification, never treated as satisfied.
            if(gate.State=="MISSING")return (null,E(422,"required_evidence_missing"));
            if(PresentKinds(c.Evidence).Length==0)return (null,E(422,"required_evidence_missing"));
            return (c with{State="EVIDENCE_SUBMITTED",Submission=new(actor,now,hash),Executors=With(c.Executors,actor)},null);
        case "VERIFY" or "RETURN":
            if(required!=null || expectedEvidenceHash is not {Length:64})return (null,E(400,"validation_failed"));
            if(action=="VERIFY" ? reason!=null : !ReturnReasons.Contains(reason))return (null,E(400,action=="RETURN"?"invalid_reason":"validation_failed"));
            if(c.State!="EVIDENCE_SUBMITTED")return (null,E(409,"invalid_transition"));
            if(!string.Equals(expectedEvidenceHash,hash,StringComparison.OrdinalIgnoreCase) || c.Submission?.Hash!=hash)return (null,E(409,"evidence_changed"));
            if(Executor())return (null,E(409,"separation_of_duties"));
            if(action=="RETURN")return (c with{State="IN_PROGRESS",Submission=null,LastReturn=new(actor,now,hash,reason)},null);
            if(gate.State=="REVIEW_REQUIRED")return (null,E(422,"required_evidence_unconfigured"));
            if(gate.State=="MISSING")return (null,E(422,"required_evidence_missing"));
            return (c with{State="VERIFIED",Verification=new(actor,now,hash)},null);
        case "CLOSE":
            if(required!=null || reason!=null || expectedEvidenceHash is not {Length:64})return (null,E(400,"validation_failed"));
            if(c.State!="VERIFIED")return (null,E(409,"invalid_transition"));
            if(!string.Equals(expectedEvidenceHash,hash,StringComparison.OrdinalIgnoreCase) || c.Verification?.Hash!=hash)return (null,E(409,"evidence_changed"));
            if(Executor())return (null,E(409,"separation_of_duties"));
            if(gate.State!="HUMAN_VERIFIED")return (null,E(422,gate.State=="REVIEW_REQUIRED"?"required_evidence_unconfigured":"required_evidence_missing"));
            return (c with{State="CLOSED",Closure=new(actor,now,hash)},null);
        default:
            return (null,E(400,"validation_failed"));
        }
    }

    public static string[] Blockers(WorkOrderExecutionContent c) {
        var gate=Gate(c);var b=new List<string>();
        if(gate.State=="REVIEW_REQUIRED")b.Add("REQUIRED_EVIDENCE_POLICY_UNCONFIGURED");
        b.AddRange(gate.Missing.Select(k=>"REQUIRED_EVIDENCE_MISSING_"+k));
        if(c.State is "RELEASED" or "IN_PROGRESS" or "EVIDENCE_SUBMITTED")b.Add("HUMAN_VERIFICATION_NOT_RECORDED");
        if(c.State!="CLOSED")b.Add("CLOSURE_NOT_RECORDED");
        if(c.LastReturn!=null && c.State=="IN_PROGRESS")b.Add("RETURNED_BY_VERIFIER");
        // Outside this aggregate: permit/risk authorization and return-to-service remain separate decisions.
        b.Add("PERMIT_AND_RISK_NOT_AUTHORIZED");b.Add("RETURN_TO_SERVICE_NOT_RECORDED");
        return b.ToArray();
    }

    public static bool ValidContent(WorkOrderExecutionContent? c)=>c!=null && States.Contains(c.State) && ValidRequired(c.RequiredEvidence) && ValidEvidence(c.Evidence) &&
        c.Executors!=null && c.Executors.All(x=>x!=Guid.Empty) && c.Release!=null && c.Release.ActorId!=Guid.Empty;
}

// DEV-110 / T-SUP2-001 (18.01 sheet:Backlog/row:111): min/max, current stock, WO/equipment criticality,
// low-stock alert, proposed requisition, human approval and no full procurement workflow (ERP boundary).
// 11.01 rows 1869/1879: min_stock is a conditional non-negative number; approval_status is a controlled
// state machine owned by Chief Engineer/Technical Manager/Purchasing. No threshold, lead time, vendor,
// price or criticality catalog is invented: an undeclared value stays UNKNOWN/UNCONFIGURED.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CriticalSpareDefinition(string PartNumber,string Name,string Unit,string Criticality,Guid? AssetId=null,
    string? MinQuantity=null,string? MaxQuantity=null,string? Location=null)
{
    public override string ToString()=>nameof(CriticalSpareDefinition);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CriticalSpareCount(string Quantity,DateTimeOffset CountedAt)
{
    public override string ToString()=>nameof(CriticalSpareCount);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CriticalSpareContent(CriticalSpareDefinition Definition,CriticalSpareCount? Count)
{
    public override string ToString()=>nameof(CriticalSpareContent);
}
public sealed record SpareConsumption(Guid WorkOrderId,string Quantity,DateTimeOffset SubmittedAt);
public sealed record SpareStockProjection(string State,string? Counted,DateTimeOffset? CountedAt,string? ConsumedSinceCount,string? Projected,
    string? Minimum,string? Maximum,string? ProposedQuantity,string[] Reasons,string Qualification);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TechnicalRequisitionContent(Guid SpareId,long SpareRevision,string State,string? ProposedQuantity,string BasisState,string? BasisProjected,
    string? BasisMinimum,string? BasisMaximum,string SpareCriticality,string EquipmentCriticality,int OpenWorkOrderDemand,Guid ProposedBy,DateTimeOffset ProposedAt,
    string? ApprovedQuantity=null,Guid? DecidedBy=null,DateTimeOffset? DecidedAt=null,string? ReasonCode=null)
{
    public override string ToString()=>nameof(TechnicalRequisitionContent);
}

public static class CriticalSparePolicy
{
    public const string SourceVersion="18.01:row111+11.01:1869,1879+15.01:p1672:v46.3";
    public const string Qualification="DECLARED_COUNT_UNVERIFIED_ADVISORY";
    public const string ErpBoundary="EXPORT_REFERENCE_ONLY_NO_PURCHASE_ORDER";
    public static readonly string[] Criticalities=["CRITICAL_DECLARED","NON_CRITICAL_DECLARED","UNKNOWN"];
    public static readonly string[] StockStates=["STOCK_UNKNOWN","MINIMUM_UNCONFIGURED","CONSUMPTION_UNRESOLVED","LOW_STOCK","ABOVE_MINIMUM"];
    public static readonly string[] RequisitionStates=["PROPOSED","APPROVED_FOR_ERP_EXPORT","REJECTED"];
    public static readonly string[] RejectReasons=["STOCK_COUNT_DISPUTED","DEMAND_COVERED_ELSEWHERE","QUANTITY_NOT_ACCEPTED","OTHER_SEE_ERP"];
    public static readonly string[] SpareTransitions=["CREATE","UPDATE"];
    public static readonly string[] RequisitionTransitions=["PROPOSE","APPROVE","REJECT"];
    public const int MaxConsumption=500;

    private static bool Text(string? value,int max)=>value is {Length:>0} && value.Length<=max && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    public static bool NonNegative(string? text,out decimal value) {
        value=0;
        return InvariantDecimal.TryNormalize(text,out var canonical) && canonical==text && decimal.TryParse(canonical,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value);
    }
    public static bool ValidDefinition(CriticalSpareDefinition? d)=>d!=null && Text(d.PartNumber,64) && Text(d.Name,200) && Text(d.Unit,32) && Criticalities.Contains(d.Criticality) &&
        d.AssetId!=Guid.Empty && (d.Location==null || Text(d.Location,120)) &&
        (d.MinQuantity==null || NonNegative(d.MinQuantity,out _)) && (d.MaxQuantity==null || NonNegative(d.MaxQuantity,out _)) &&
        (d.MinQuantity==null || d.MaxQuantity==null || Dec(d.MaxQuantity)>=Dec(d.MinQuantity));
    public static bool ValidCount(CriticalSpareCount? c,DateTimeOffset now)=>c==null || NonNegative(c.Quantity,out _) && c.CountedAt.Offset==TimeSpan.Zero && c.CountedAt<=now;
    private static decimal Dec(string text)=>decimal.Parse(text,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
    private static string Str(decimal value)=>value.ToString("0.############################",CultureInfo.InvariantCulture);

    // Projected stock = declared count minus catalogued consumption on work orders submitted after the count.
    // A missing count, a missing minimum or an unbounded consumption set is never reported as healthy.
    public static SpareStockProjection Project(CriticalSpareContent c,IReadOnlyCollection<SpareConsumption>? consumption) {
        var d=c.Definition;var reasons=new List<string>{"COUNT_DECLARED_UNVERIFIED"};
        if(d.Criticality=="UNKNOWN")reasons.Add("SPARE_CRITICALITY_UNKNOWN");
        if(d.MaxQuantity==null)reasons.Add("MAXIMUM_UNCONFIGURED");
        if(c.Count==null)return new("STOCK_UNKNOWN",null,null,null,null,d.MinQuantity,d.MaxQuantity,null,[..reasons,"STOCK_COUNT_MISSING"],Qualification);
        if(consumption==null)return new("CONSUMPTION_UNRESOLVED",c.Count.Quantity,c.Count.CountedAt,null,null,d.MinQuantity,d.MaxQuantity,null,[..reasons,"CONSUMPTION_SET_UNBOUNDED"],Qualification);
        var used=consumption.Where(x=>x.SubmittedAt>c.Count.CountedAt).Sum(x=>Dec(x.Quantity));var projected=Dec(c.Count.Quantity)-used;
        if(used>0)reasons.Add("CONSUMPTION_FROM_SUBMITTED_WORK_ORDERS");
        if(projected<0)reasons.Add("PROJECTED_BELOW_ZERO_RECOUNT_REQUIRED");
        if(d.MinQuantity==null)return new("MINIMUM_UNCONFIGURED",c.Count.Quantity,c.Count.CountedAt,Str(used),Str(projected),null,d.MaxQuantity,null,[..reasons,"MINIMUM_UNCONFIGURED"],Qualification);
        var low=projected<=Dec(d.MinQuantity);string? proposed=null;
        if(low && d.MaxQuantity!=null && Dec(d.MaxQuantity)-projected>0)proposed=Str(Dec(d.MaxQuantity)-projected);
        if(low && proposed==null)reasons.Add("PROPOSED_QUANTITY_REVIEW_REQUIRED");
        return new(low?"LOW_STOCK":"ABOVE_MINIMUM",c.Count.Quantity,c.Count.CountedAt,Str(used),Str(projected),d.MinQuantity,d.MaxQuantity,proposed,reasons.ToArray(),Qualification);
    }

    // The system proposes; a human actor triggers and is recorded as the proposer.
    public static (TechnicalRequisitionContent? Content,string? Error) Propose(Guid spare,long revision,CriticalSpareContent c,SpareStockProjection p,string equipmentCriticality,
        int demand,Guid actor,DateTimeOffset now)=>p.State!="LOW_STOCK" ? (null,"stock_not_low") :
        (new(spare,revision,"PROPOSED",p.ProposedQuantity,p.State,p.Projected,p.Minimum,p.Maximum,c.Definition.Criticality,equipmentCriticality,demand,actor,now),null);

    // Human approval with separation of duties. Approval only authorizes an ERP export reference;
    // there is no purchase order, vendor, price, receipt or invoice in this boundary.
    public static (TechnicalRequisitionContent? Content,int Status,string? Error) Decide(TechnicalRequisitionContent c,string decision,Guid actor,DateTimeOffset now,
        string? approvedQuantity,string? reason) {
        if(decision is not("APPROVE" or "REJECT"))return (null,400,"validation_failed");
        if(c.State!="PROPOSED")return (null,409,"invalid_transition");
        if(c.ProposedBy==actor)return (null,409,"separation_of_duties");
        if(decision=="REJECT") {
            if(approvedQuantity!=null || !RejectReasons.Contains(reason))return (null,400,"invalid_reason");
            return (c with{State="REJECTED",DecidedBy=actor,DecidedAt=now,ReasonCode=reason},200,null);
        }
        if(reason!=null)return (null,400,"validation_failed");
        var quantity=approvedQuantity??c.ProposedQuantity;
        if(quantity==null)return (null,422,"quantity_required");
        if(!WorkOrderExecutionPolicy.PositiveQuantity(quantity,out _))return (null,400,"validation_failed");
        return (c with{State="APPROVED_FOR_ERP_EXPORT",ApprovedQuantity=quantity,DecidedBy=actor,DecidedAt=now},200,null);
    }
}
