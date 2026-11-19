using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyCreateCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] Guid CertificateId,
    [property:JsonRequired] long CertificateRevision,[property:JsonRequired] uint CertificateVersion,[property:JsonRequired] string TemplateCode,[property:JsonRequired] PreSurveyPlan Plan);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyUpdateCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] long ExpectedRevision,
    [property:JsonRequired] long CertificateRevision,[property:JsonRequired] uint CertificateVersion,[property:JsonRequired] PreSurveyPlan Plan,[property:JsonRequired] PreSurveyItemInput[] Items);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreSurveyVerifyCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] long ExpectedRevision,
    [property:JsonRequired] PreSurveyVerificationDecision[] Decisions);

public sealed record PreSurveyItemDto(Guid ItemId,string ItemCode,bool Critical,bool Required,bool EvidenceRequired,bool AllowNotApplicable,Guid? OwnerRoleId,DateOnly? DueDate,
    string Answer,CertificateFilePin[] Evidence,string? Finding,string? Comment,string ItemHash,string VerificationState,DateTimeOffset? VerifiedAt,string? VerificationReason,
    bool AnsweredByCurrentActor,bool VerifiedByCurrentActor,string[] Blockers);
public sealed record PreSurveyPortCallDto(Guid Id,long Revision,string Reference,DateOnly? From,DateOnly? To,string Qualification="PORT_CALL_DRAFT_UNVERIFIED_PORT_IDENTITY_UNRESOLVED");
public sealed record PreSurveyChecklistDto(Guid Id,Guid VesselId,Guid CertificateId,long CertificateRevision,uint CertificateVersion,long Revision,uint Version,
    string TemplateCode,string TemplateRevision,string TemplateStatus,string SurveyType,PreSurveyPlan Plan,PreSurveyItemDto[] Items,PreSurveyReadiness Readiness,
    SurveyWindowDto Window,SurveyFitDto VoyageFit,PreSurveyPortCallDto? PortCall,string Transition,string SourceVersion,string Hash,DateTimeOffset SavedAt,string Provenance,
    bool CurrentRevision,bool CanEdit,bool CanVerify,string Qualification,string VoyageReference="UNKNOWN_NO_VOYAGE_MODULE");
public sealed record PreSurveyPageDto(PreSurveyChecklistDto[] Items,int Offset,int Limit,bool HasMore);
public sealed record PreSurveyTemplateDto(string Code,string Revision,string SurveyType,string Status,PreSurveyTemplateItem[] Items);
public sealed record PreSurveyCapabilitiesDto(Guid VesselId,bool CanWrite,bool CanVerify,RenewalOwnerDto[] OwnerOptions,PreSurveyTemplateDto[] Templates,bool CanReadPortCalls,
    string SourceVersion,string[] Answers,string[] RejectReasons,string Qualification);
public sealed record PreSurveyPortCallOptionDto(Guid Id,long Revision,DateOnly? From,DateOnly? To,string Label,string Qualification="PORT_CALL_DRAFT_UNVERIFIED_PORT_IDENTITY_UNRESOLVED");
public sealed record PreSurveyPortCallPageDto(PreSurveyPortCallOptionDto[] Items,bool Truncated);
public sealed record PreSurveyItemErrorDto(Guid ItemId,string Code);

// DEV-104 pre-survey checklist (18.01 row105; 60.04 Rev3 p135-140, p225-226, p245-246, p347,
// p637-639). Every write is SERIALIZABLE, version-fenced, idempotent per actor key and appends one
// immutable revision. Verification is Human-only, separately permissioned and never by the person
// who recorded the answer. Readiness/voyage fit are advisory and never reach READY or FIT.
public static class PreSurveyChecklistEndpoints
{
    private static readonly JsonSerializerOptions Input=new(CertificateReader.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    internal static bool Read(TrustedActor? a)=>CertificateReader.Read(a);
    internal static bool Write(TrustedActor? a,Guid vessel)=>CertificateReader.Read(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("certificate.survey.plan");
    internal static bool Review(HttpContext? h,TrustedActor? a,Guid vessel)=>CertificateReader.Read(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("certificate.survey.review") &&
        h!=null && CertificateRenewalEndpoints.Human(h,a!);
    internal static bool PortCalls(TrustedActor a)=>ShipOperationsEndpoints.CanRead(a,"Port");
    internal static Task<IResult> ReadFail(HttpContext h,int status,string code)=>Fail(h,"PreSurveyRead",status,code);
    internal static Task<IResult> CommandFail(HttpContext h,int status,string code)=>Fail(h,"PreSurveyCommand",status,code);
    private static async Task<IResult> Fail(HttpContext h,string action,int status,string code,object? body=null) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,action,code);
        return Results.Json(body??new{code},statusCode:status);
    }
    internal static Guid[] Owners(HttpContext h,TrustedActor a,Guid vessel)=>h.RequestServices.GetRequiredService<FederationSettings>().Memberships
        .Where(m=>m.TenantId==a.TenantId && m.ActorKind=="Human" && (m.Permissions??[]).Contains("certificate.survey.plan") &&
            (m.TenantWideManage || m.VesselGrants?.Any(v=>v.VesselId==vessel && v.CanEdit)==true))
        .SelectMany(m=>(ActorResolver.TrustedRoleContext(m.RoleReferences)??"").Split(',',StringSplitOptions.RemoveEmptyEntries)).Select(Guid.Parse).Distinct().Order().Take(257).ToArray();
    internal static string OwnerHash(HttpContext h,TrustedActor a,Guid vessel)=>RegisterStorage.Hash(JsonSerializer.Serialize(Owners(h,a,vessel)));

    public static void MapPreSurveyChecklists(this IEndpointRouteBuilder app) {
        var g=app.MapGroup("/api/v1/pre-survey-checklists").RequireAuthorization("DirectoryRead");
        g.MapGet("",(HttpContext h,PoseidonDbContext db)=>List(h,db)).WithName("PreSurveyChecklists").Produces<PreSurveyPageDto>();
        g.MapGet("/capabilities",(HttpContext h,PoseidonDbContext db)=>Capabilities(h,db)).WithName("PreSurveyCapabilities").Produces<PreSurveyCapabilitiesDto>();
        g.MapGet("/port-calls",(HttpContext h,PoseidonDbContext db)=>PortCallOptions(h,db)).WithName("PreSurveyPortCalls").Produces<PreSurveyPortCallPageDto>();
        g.MapGet("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Detail(id,false,h,db)).WithName("PreSurveyChecklistRead").Produces<PreSurveyChecklistDto>();
        g.MapGet("/{id:guid}/history",(Guid id,HttpContext h,PoseidonDbContext db)=>Detail(id,true,h,db)).WithName("PreSurveyChecklistHistory").Produces<PreSurveyPageDto>();
        g.MapPost("",(HttpContext h,PoseidonDbContext db)=>Create(h,db)).WithName("PreSurveyChecklistCreate").Accepts<PreSurveyCreateCommand>("application/json").Produces<PreSurveyChecklistDto>();
        g.MapPut("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Update(id,h,db)).WithName("PreSurveyChecklistUpdate").Accepts<PreSurveyUpdateCommand>("application/json").Produces<PreSurveyChecklistDto>();
        g.MapPost("/{id:guid}/verifications",(Guid id,HttpContext h,PoseidonDbContext db)=>Verify(id,h,db)).WithName("PreSurveyChecklistVerify").Accepts<PreSurveyVerifyCommand>("application/json").Produces<PreSurveyChecklistDto>();
    }

    internal static PreSurveyChecklistContent Content(PreSurveyChecklistRevision row)=>JsonSerializer.Deserialize<PreSurveyChecklistContent>(row.ContentJson,Input)
        ??throw new InvalidDataException("presurvey_content_unavailable");
    internal static IQueryable<PreSurveyChecklistRevision> Visible(PoseidonDbContext db,TrustedActor a,Guid vessel,bool currentOnly=true) {
        var certificates=CertificateReader.Visible(db,a,vessel,false);
        var rows=db.PreSurveyChecklistRevisions.AsNoTracking().Where(r=>r.VesselId==vessel &&
            certificates.Any(c=>c.CertificateId==r.CertificateId && c.Revision==r.CertificateRevision && c.RecordVersion==r.CertificateVersion));
        var heads=db.PreSurveyChecklists.Where(x=>x.VesselId==vessel);
        return currentOnly?rows.Where(r=>heads.Any(x=>x.Id==r.ChecklistId && x.Revision==r.Revision)):
            rows.Where(r=>heads.Any(x=>x.Id==r.ChecklistId && rows.Any(c=>c.ChecklistId==x.Id && c.Revision==x.Revision)));
    }
    // Evidence pins live inside the immutable content; a revision whose pinned files the actor
    // cannot see is withheld as a whole, never partially redacted (same rule as renewal files).
    internal static async Task<bool> FilesVisible(PoseidonDbContext db,TrustedActor a,Guid vessel,IEnumerable<CertificateFilePin> pins,CancellationToken ct) {
        var list=pins.DistinctBy(p=>(p.Id,p.Revision,p.Checksum.ToUpperInvariant())).ToArray();if(list.Length==0)return true;
        if(!a.Permissions.Contains("file.read") || list.Length>500)return false;
        var ids=list.Select(p=>p.Id).Distinct().ToArray();
        var files=await db.StoredFiles.AsNoTracking().Where(f=>ids.Contains(f.Id) && f.VesselId==vessel &&
            db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==vessel && s.PersonId==null && s.DocumentId==null && s.State=="CleanTransport"))
            .Select(f=>new{f.Id,f.Revision,f.Checksum}).ToArrayAsync(ct);
        return list.All(p=>files.Any(f=>f.Id==p.Id && f.Revision==p.Revision && string.Equals(f.Checksum,p.Checksum,StringComparison.OrdinalIgnoreCase)));
    }
    internal static async Task<SurveyPortCallFact?> PortFact(PoseidonDbContext db,TrustedActor a,Guid vessel,Guid? id,long? revision,CancellationToken ct) {
        if(id==null || revision==null)return null;
        var kind=await db.Vessels.AsNoTracking().Where(v=>v.Id==vessel).Select(v=>(VesselKind?)v.Kind).SingleOrDefaultAsync(ct);
        if(!PortCalls(a) || kind!=VesselKind.Ship)return new("NOT_VISIBLE",null,null);
        var row=await db.ShipOperationRevisions.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind=="Port" && r.RecordId==id && r.Revision==revision && r.SourceVersion==ShipOperationsPolicy.SourceVersion)
            .Select(r=>new{r.OccurredAt,r.EndedAt}).SingleOrDefaultAsync(ct);
        var head=await db.ShipOperations.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind=="Port" && r.Id==id).Select(r=>(long?)r.Revision).SingleOrDefaultAsync(ct);
        if(row==null || head==null)return new("NOT_FOUND",null,null);
        DateOnly? Day(DateTimeOffset? t)=>t is {} v?DateOnly.FromDateTime(v.UtcDateTime):null;
        return new(head==revision?"CURRENT":"SUPERSEDED",Day(row.OccurredAt),Day(row.EndedAt));
    }
    internal static PreSurveyChecklistDto Dto(PreSurveyChecklistRevision row,TrustedActor a,bool current,DateOnly today,VesselCertificateRevision certificate,SurveyPortCallFact? port,HttpContext? h) {
        var c=Content(row);var template=PreSurveyChecklistPolicy.Template(c.TemplateCode)!;
        var items=c.Items.Select(i=>new PreSurveyItemDto(i.ItemId,i.ItemCode,i.Critical,i.Required,i.EvidenceRequired,i.AllowNotApplicable,i.OwnerRoleId,i.DueDate,i.Answer,i.Evidence,
            i.Finding,i.Comment,PreSurveyChecklistPolicy.ItemHash(i),PreSurveyChecklistPolicy.VerifiedCurrent(i)?"VERIFIED":
                i.Verification.State=="REJECTED" && i.Verification.ItemHash==PreSurveyChecklistPolicy.ItemHash(i)?"REJECTED":"PENDING",
            i.Verification.State=="PENDING"?null:i.Verification.RecordedAt,i.Verification.ReasonCode,i.AnsweredBy==a.Id,i.Verification.VerifierId==a.Id,
            PreSurveyChecklistPolicy.ItemBlockers(i,today))).ToArray();
        var window=SurveyPlannerPolicy.Window(today,certificate.SurveyWindowDeclared,certificate.SurveyWindowStart,certificate.SurveyWindowEnd);
        var fit=SurveyPlannerPolicy.Fit(today,window,certificate.ExpiryDate,c.Plan,port);
        return new(row.ChecklistId,row.VesselId,row.CertificateId,row.CertificateRevision,checked((uint)row.CertificateVersion),row.Revision,checked((uint)row.RecordVersion),
            c.TemplateCode,c.TemplateRevision,template.Status,c.SurveyType,c.Plan,items,PreSurveyChecklistPolicy.Readiness(c,today),window,fit,
            port==null?null:new(c.Plan.PortCallId!.Value,c.Plan.PortCallRevision!.Value,port.Reference,port.From,port.To),row.Transition,row.SourceVersion,row.Hash,row.SavedAt,row.Provenance,
            current,current && Write(a,row.VesselId),current && Review(h,a,row.VesselId),PreSurveyChecklistPolicy.Qualification);
    }
    internal static async Task<PreSurveyChecklistDto[]> Project(PoseidonDbContext db,TrustedActor a,Guid vessel,PreSurveyChecklistRevision[] rows,Func<PreSurveyChecklistRevision,bool> current,
        DateOnly today,HttpContext h) {
        var result=new List<PreSurveyChecklistDto>();
        foreach(var row in rows) {
            var content=Content(row);
            if(!await FilesVisible(db,a,vessel,content.Items.SelectMany(i=>i.Evidence),h.RequestAborted))continue;
            var certificate=await CertificateReader.Visible(db,a,vessel,false).SingleOrDefaultAsync(c=>c.CertificateId==row.CertificateId && c.Revision==row.CertificateRevision,h.RequestAborted);
            if(certificate==null)continue;
            result.Add(Dto(row,a,current(row),today,certificate,await PortFact(db,a,vessel,content.Plan.PortCallId,content.Plan.PortCallRevision,h.RequestAborted),h));
        }
        return result.ToArray();
    }
    // Pre/post-audit fence: the same actor rights, vessel visibility, exact returned revisions and
    // (for writes) the same write authority and owner set must still hold in a fresh scope.
    internal static async Task<bool> Current(HttpContext h,Guid vessel,string rights,PreSurveyChecklistDto[] records,string? ownerHash=null,bool write=false) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var a=resolver.Resolve(h.User);
        if(a==null || !Read(a) || CertificateReader.Rights(a)!=rights)return false;
        PoseidonDbContext db;try{db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=a.TenantId || !await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return false;
        foreach(var r in records) {
            var row=await Visible(db,a,vessel,false).SingleOrDefaultAsync(x=>x.ChecklistId==r.Id && x.Revision==r.Revision && x.RecordVersion==r.Version && x.Hash==r.Hash,h.RequestAborted);
            if(row==null || r.CurrentRevision && !await db.PreSurveyChecklists.AnyAsync(x=>x.VesselId==vessel && x.Id==r.Id && x.Revision==r.Revision,h.RequestAborted))return false;
            if(!await FilesVisible(db,a,vessel,r.Items.SelectMany(i=>i.Evidence),h.RequestAborted))return false;
        }
        var live=resolver.Resolve(h.User);
        return live!=null && Read(live) && CertificateReader.Rights(live)==rights && (!write || ownerHash!=null && OwnerHash(h,live,vessel)==ownerHash);
    }
    private static async Task<IResult> Ok(HttpContext h,Guid vessel,string rights,PreSurveyChecklistDto[] records,object result,Guid? resource) {
        if(!await Current(h,vessel,rights,records))return await ReadFail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"PreSurveyRead","Success",resourceId:resource??vessel);
        if(!await Current(h,vessel,rights,records))return await ReadFail(h,403,"permission_denied");
        return Results.Ok(result);
    }
    private static async Task<(TrustedActor? Actor,Guid Vessel,int Offset,IResult? Error)> Begin(HttpContext h,PoseidonDbContext db,HashSet<string> keys) {
        h.Response.Headers.CacheControl="no-store";
        if(!CertificateEndpoints.Offset(h,keys,out var offset) || !Guid.TryParseExact(h.Request.Query["vesselId"],"D",out var vessel) || vessel==Guid.Empty)
            return (null,Guid.Empty,0,await ReadFail(h,400,"invalid_query"));
        var a=CertificateEndpoints.Actor(h);if(a==null || !Read(a))return (null,vessel,0,await ReadFail(h,403,"permission_denied"));
        if(!await a.Scope(db.Vessels).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return (null,vessel,0,await ReadFail(h,404,"resource_not_found"));
        return (a,vessel,offset,null);
    }
    private static async Task<IResult> List(HttpContext h,PoseidonDbContext db) {
        var (a,vessel,offset,error)=await Begin(h,db,["vesselId","offset","certificateId"]);if(error!=null)return error;
        Guid? certificate=null;
        if(h.Request.Query.ContainsKey("certificateId")) {if(!Guid.TryParseExact(h.Request.Query["certificateId"],"D",out var c) || c==Guid.Empty)return await ReadFail(h,400,"invalid_query");certificate=c;}
        var rights=CertificateReader.Rights(a!);var today=DateOnly.FromDateTime(CertificateEndpoints.Now(h).UtcDateTime);
        var query=Visible(db,a!,vessel);if(certificate!=null)query=query.Where(r=>r.CertificateId==certificate);
        var rows=await query.OrderByDescending(r=>r.SavedAt).ThenBy(r=>r.ChecklistId).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        var items=await Project(db,a!,vessel,rows.Take(25).ToArray(),_=>true,today,h);
        return await Ok(h,vessel,rights,items,new PreSurveyPageDto(items,offset,25,rows.Length>25),null);
    }
    private static async Task<IResult> Detail(Guid id,bool history,HttpContext h,PoseidonDbContext db) {
        var (a,vessel,offset,error)=await Begin(h,db,history?["vesselId","offset"]:["vesselId"]);if(error!=null)return error;
        var head=await db.PreSurveyChecklists.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && x.VesselId==vessel,h.RequestAborted);
        if(head==null)return await ReadFail(h,404,"resource_not_found");
        var rights=CertificateReader.Rights(a!);var today=DateOnly.FromDateTime(CertificateEndpoints.Now(h).UtcDateTime);
        var rows=await Visible(db,a!,vessel,!history).Where(r=>r.ChecklistId==id).OrderByDescending(r=>r.Revision).Skip(offset).Take(history?26:1).ToArrayAsync(h.RequestAborted);
        var items=await Project(db,a!,vessel,rows.Take(25).ToArray(),r=>r.Revision==head.Revision,today,h);
        if(items.Length==0 && (!history || offset==0))return await ReadFail(h,404,"resource_not_found");
        return await Ok(h,vessel,rights,items,history?new PreSurveyPageDto(items,offset,25,rows.Length>25):items[0],id);
    }
    private static async Task<IResult> Capabilities(HttpContext h,PoseidonDbContext db) {
        var (a,vessel,_,error)=await Begin(h,db,["vesselId"]);if(error!=null)return error;
        var owners=Owners(h,a!,vessel);if(owners.Length>256)owners=[];
        var kind=await db.Vessels.AsNoTracking().Where(v=>v.Id==vessel).Select(v=>v.Kind).SingleAsync(h.RequestAborted);
        var result=new PreSurveyCapabilitiesDto(vessel,Write(a,vessel),Review(h,a,vessel),owners.Select(o=>new RenewalOwnerDto(o)).ToArray(),
            PreSurveyChecklistPolicy.Templates.Select(t=>new PreSurveyTemplateDto(t.Code,t.Revision,t.SurveyType,t.Status,t.Items)).ToArray(),
            PortCalls(a!) && kind==VesselKind.Ship,PreSurveyChecklistPolicy.SourceVersion,PreSurveyChecklistPolicy.Answers,PreSurveyChecklistPolicy.RejectReasons,PreSurveyChecklistPolicy.Qualification);
        return await Ok(h,vessel,CertificateReader.Rights(a!),[],result,null);
    }
    // Voyage-fit inputs: declared Ship Operations port calls (DEV-101 owning ledger). The pin links
    // a draft port call; it does not resolve a port master, UN/LOCODE or a voyage (none exists).
    private static async Task<IResult> PortCallOptions(HttpContext h,PoseidonDbContext db) {
        var (a,vessel,_,error)=await Begin(h,db,["vesselId"]);if(error!=null)return error;
        var kind=await db.Vessels.AsNoTracking().Where(v=>v.Id==vessel).Select(v=>v.Kind).SingleAsync(h.RequestAborted);
        if(!PortCalls(a!) || kind!=VesselKind.Ship)return await ReadFail(h,403,"permission_denied");
        var rows=await db.ShipOperations.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind=="Port" && r.SourceVersion==ShipOperationsPolicy.SourceVersion)
            .OrderByDescending(r=>r.OccurredAt).ThenBy(r=>r.Id).Take(51).ToArrayAsync(h.RequestAborted);
        var items=rows.Take(50).Select(r=>{
            string label;
            try {
                var fields=ShipOperationsPolicy.ReadFields("Port",vessel,r.FieldsJson);
                label=string.Join(" · ",new[]{"activity_type","terminal"}.Select(k=>fields.GetValueOrDefault(k) is {State:"Known",Value:{} v}?v:null).Where(v=>v!=null));
            }catch(InvalidDataException){label="";}
            return new PreSurveyPortCallOptionDto(r.Id,r.Revision,r.OccurredAt is {} o?DateOnly.FromDateTime(o.UtcDateTime):null,r.EndedAt is {} e?DateOnly.FromDateTime(e.UtcDateTime):null,
                label.Length==0?"r"+r.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture):label);
        }).ToArray();
        return await Ok(h,vessel,CertificateReader.Rights(a!),[],new PreSurveyPortCallPageDto(items,rows.Length>50),null);
    }

    private sealed record Envelope(TrustedActor Actor,Guid Vessel,string Key,string Rights,string OwnerHash,Guid[] Owners);
    private static async Task<(Envelope? Env,IResult? Error)> Command(HttpContext h,bool review) {
        h.Response.Headers.CacheControl="no-store";var a=CertificateEndpoints.Actor(h);
        if(a==null || !CertificateRenewalEndpoints.Query(h,false,out var vessel,out _) || !(review?CertificateReader.Read(a) && a.CanEditVessel(vessel):Write(a,vessel)))
            return (null,await CommandFail(h,403,"permission_denied"));
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return (null,await CommandFail(h,403,"csrf_invalid"));}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;
        if(key==null || !Guid.TryParseExact(key,"D",out _))return (null,await CommandFail(h,400,"invalid_idempotency_key"));
        if(review && !CertificateRenewalEndpoints.Human(h,a))return (null,await CommandFail(h,403,"human_actor_required"));
        if(review && !Review(h,a,vessel))return (null,await CommandFail(h,403,"permission_denied"));
        var owners=Owners(h,a,vessel);if(owners.Length>256)return (null,await CommandFail(h,403,"owner_unavailable"));
        return (new(a,vessel,key,CertificateReader.Rights(a),OwnerHash(h,a,vessel),owners),null);
    }
    private static async Task<T> Body<T>(HttpContext h) {
        if(!h.Request.HasJsonContentType())throw new JsonException();using var bytes=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,bytes,262144,h.RequestAborted);
        using var document=JsonDocument.Parse(bytes.ToArray(),new(){MaxDepth=10});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
        return document.RootElement.Deserialize<T>(Input)??throw new JsonException();
    }
    private static async Task<IResult> Outcome(HttpContext h,Envelope e,PreSurveyChecklistDto record,string json) {
        try {
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"PreSurveyCommand","Success",resourceId:record.Id);
            if(!await Current(h,e.Vessel,e.Rights,[record],e.OwnerHash,true))return await CommandFail(h,503,"command_outcome_withheld");
            return Results.Content(json,"application/json");
        }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
    }
    private static async Task<IResult?> Replay(HttpContext h,PoseidonDbContext db,Envelope e,string action,Guid id,string fingerprint) {
        if(!await Current(h,e.Vessel,e.Rights,[],e.OwnerHash,true))return await CommandFail(h,403,"permission_denied");
        var receipt=await db.PreSurveyReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==e.Actor.Id && r.Key==e.Key,h.RequestAborted);if(receipt==null)return null;
        if(receipt.Action!=action || receipt.VesselId!=e.Vessel || receipt.ChecklistId!=id || receipt.Fingerprint!=fingerprint)return await CommandFail(h,409,"idempotency_key_reused");
        var record=JsonSerializer.Deserialize<PreSurveyChecklistDto>(receipt.OutcomeJson,CertificateReader.Json)??throw new InvalidDataException("presurvey_receipt_unavailable");
        return await Outcome(h,e,record,receipt.OutcomeJson);
    }
    // Shared serializable apply: version fence, immutable revision append, receipt, fresh fence.
    private delegate Task<(PreSurveyChecklistContent? Content,VesselCertificateRevision? Certificate,IResult? Error)> Mutation(PreSurveyChecklist? head,PreSurveyChecklistRevision? current);
    private static async Task<IResult> Apply(HttpContext h,PoseidonDbContext db,Envelope e,Guid id,bool create,uint expectedVersion,long expectedRevision,string action,string transition,
        string fingerprint,Mutation mutate) {
        var replay=await Replay(h,db,e,action,id,fingerprint);if(replay!=null)return replay;
        IDbContextTransaction? tx=null;PreSurveyChecklistDto? saved=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            var head=await db.PreSurveyChecklists.SingleOrDefaultAsync(x=>x.Id==id,h.RequestAborted);PreSurveyChecklistRevision? current=null;PreSurveyChecklistDto? previous=null;
            var now=CertificateEndpoints.Now(h);var today=DateOnly.FromDateTime(now.UtcDateTime);
            if(head!=null && head.VesselId!=e.Vessel)return await CommandFail(h,404,"resource_not_found");
            if(create) {
                if(head!=null || expectedVersion!=0)return await Replay(h,db,e,action,id,fingerprint)??await CommandFail(h,409,"version_conflict");
            }else {
                if(head==null)return await CommandFail(h,404,"resource_not_found");
                if(head.Version!=expectedVersion || head.Revision!=expectedRevision)return await Replay(h,db,e,action,id,fingerprint)??await CommandFail(h,409,"version_conflict");
                current=await Visible(db,e.Actor,e.Vessel).SingleOrDefaultAsync(r=>r.ChecklistId==id,h.RequestAborted);
                if(current==null || !await FilesVisible(db,e.Actor,e.Vessel,Content(current).Items.SelectMany(i=>i.Evidence),h.RequestAborted))return await CommandFail(h,404,"resource_not_found");
                var currentCertificate=await CertificateReader.Visible(db,e.Actor,e.Vessel,false).SingleAsync(c=>c.CertificateId==current.CertificateId && c.Revision==current.CertificateRevision,h.RequestAborted);
                var currentContent=Content(current);
                previous=Dto(current,e.Actor,true,today,currentCertificate,await PortFact(db,e.Actor,e.Vessel,currentContent.Plan.PortCallId,currentContent.Plan.PortCallRevision,h.RequestAborted),h);
            }
            var (content,certificate,error)=await mutate(head,current);if(error!=null)return error;
            if(await db.CertificateSupersessions.AnyAsync(s=>s.VesselId==e.Vessel && s.CertificateId==certificate!.CertificateId,h.RequestAborted))
                return await CommandFail(h,409,"certificate_already_superseded");
            if(head==null) {head=new(){TenantId=e.Actor.TenantId,VesselId=e.Vessel,Id=id,CertificateId=certificate!.CertificateId};db.PreSurveyChecklists.Add(head);}
            else {
                if(head.CertificateId!=certificate!.CertificateId)return await CommandFail(h,400,"validation_failed");
                head.Revision++;
            }
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"PreSurveyCommand","Attempted",resourceId:id);
            await db.SaveChangesAsync(h.RequestAborted);
            var json=JsonSerializer.Serialize(content,CertificateReader.Json);var readiness=PreSurveyChecklistPolicy.Readiness(content!,today);
            var row=new PreSurveyChecklistRevision{TenantId=e.Actor.TenantId,VesselId=e.Vessel,ChecklistId=id,CertificateId=certificate!.CertificateId,CertificateRevision=certificate.Revision,
                CertificateVersion=certificate.RecordVersion,Revision=head.Revision,RecordVersion=head.Version,TemplateCode=content!.TemplateCode,ContentJson=json,
                Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,id,revision=head.Revision,certificate.CertificateId,certificateRevision=certificate.Revision,certificate.RecordVersion,transition,json},CertificateReader.Json)),
                SourceVersion=PreSurveyChecklistPolicy.SourceVersion,Transition=transition,Readiness=readiness.State,PlannedStart=content.Plan.PlannedStart,PlannedEnd=content.Plan.PlannedEnd,
                PortCallId=content.Plan.PortCallId,PortCallRevision=content.Plan.PortCallRevision,ActorId=e.Actor.Id,SavedAt=now};
            db.PreSurveyChecklistRevisions.Add(row);
            saved=Dto(row,e.Actor,false,today,certificate,await PortFact(db,e.Actor,e.Vessel,content.Plan.PortCallId,content.Plan.PortCallRevision,h.RequestAborted),h);
            outcome=JsonSerializer.Serialize(saved,CertificateReader.Json);
            db.PreSurveyReceipts.Add(new(){TenantId=e.Actor.TenantId,VesselId=e.Vessel,ActorId=e.Actor.Id,Key=e.Key,ChecklistId=id,Action=action,Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});
            await db.SaveChangesAsync(h.RequestAborted);
            if(!await Current(h,e.Vessel,e.Rights,previous==null?[]:[previous],e.OwnerHash,true))return await CommandFail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay(h,db,e,action,id,fingerprint)??await CommandFail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
            var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";
            return await Replay(h,db,e,action,id,fingerprint)??await CommandFail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(h,e,saved!,outcome!);
    }
    private static async Task<(VesselCertificateRevision? Row,IResult? Error)> Certificate(HttpContext h,PoseidonDbContext db,Envelope e,Guid id,long revision,uint version) {
        var row=await CertificateReader.Visible(db,e.Actor,e.Vessel).SingleOrDefaultAsync(r=>r.CertificateId==id && r.Revision==revision && r.RecordVersion==version,h.RequestAborted);
        return row==null?(null,await CommandFail(h,409,"source_revision_changed")):(row,null);
    }
    private static async Task<IResult?> PlanAllowed(HttpContext h,PoseidonDbContext db,Envelope e,PreSurveyPlan plan,PreSurveyPlan? previous) {
        if(plan.PortCallId==null || previous!=null && previous.PortCallId==plan.PortCallId && previous.PortCallRevision==plan.PortCallRevision)return null;
        var fact=await PortFact(db,e.Actor,e.Vessel,plan.PortCallId,plan.PortCallRevision,h.RequestAborted);
        return fact?.Reference=="CURRENT"?null:await CommandFail(h,409,"port_call_unavailable");
    }
    private static async Task<IResult> Create(HttpContext h,PoseidonDbContext db) {
        var (env,error)=await Command(h,false);if(error!=null)return error;var e=env!;
        PreSurveyCreateCommand command;
        try {
            command=await Body<PreSurveyCreateCommand>(h);
            if(command.Id==Guid.Empty || command.CertificateId==Guid.Empty || command.CertificateRevision<=0 || command.CertificateVersion==0 || command.ExpectedVersion!=0 ||
                !PreSurveyChecklistPolicy.ValidPlan(command.Plan))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await CommandFail(h,400,"validation_failed");}
        if(PreSurveyChecklistPolicy.Template(command.TemplateCode) is not {} template)return await CommandFail(h,422,"template_unavailable");
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,command},CertificateReader.Json));
        return await Apply(h,db,e,command.Id,true,command.ExpectedVersion,0,"PreSurveySave","CREATE",fingerprint,async (_,_)=> {
            var (certificate,fail)=await Certificate(h,db,e,command.CertificateId,command.CertificateRevision,command.CertificateVersion);if(fail!=null)return (null,null,fail);
            if(await PlanAllowed(h,db,e,command.Plan,null) is {} planError)return (null,null,planError);
            return (PreSurveyChecklistPolicy.Instantiate(command.Id,template,command.Plan),certificate,null);
        });
    }
    private static async Task<IResult> Update(Guid id,HttpContext h,PoseidonDbContext db) {
        var (env,error)=await Command(h,false);if(error!=null)return error;var e=env!;
        PreSurveyUpdateCommand command;
        try {
            command=await Body<PreSurveyUpdateCommand>(h);
            if(command.Id!=id || command.ExpectedVersion==0 || command.ExpectedRevision<=0 || command.CertificateRevision<=0 || command.CertificateVersion==0 ||
                !PreSurveyChecklistPolicy.ValidPlan(command.Plan) || command.Items==null || command.Items.Length is 0 or >500 || !command.Items.All(PreSurveyChecklistPolicy.ValidInput))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await CommandFail(h,400,"validation_failed");}
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,id,command},CertificateReader.Json));
        return await Apply(h,db,e,id,false,command.ExpectedVersion,command.ExpectedRevision,"PreSurveySave","UPDATE",fingerprint,async (head,current)=> {
            var (certificate,fail)=await Certificate(h,db,e,head!.CertificateId,command.CertificateRevision,command.CertificateVersion);if(fail!=null)return (null,null,fail);
            var old=Content(current!);
            if(await PlanAllowed(h,db,e,command.Plan,old.Plan) is {} planError)return (null,null,planError);
            var (next,problem)=PreSurveyChecklistPolicy.Apply(old,command.Plan,command.Items,e.Actor.Id);
            if(problem!=null)return (null,null,await CommandFail(h,problem=="validation_failed"?400:422,problem));
            var oldItems=old.Items.ToDictionary(i=>i.ItemId);
            // A newly chosen owner must be a currently eligible planner role; an unchanged owner is kept.
            if(next!.Items.Any(i=>i.OwnerRoleId is {} owner && oldItems[i.ItemId].OwnerRoleId!=owner && !e.Owners.Contains(owner)))return (null,null,await CommandFail(h,403,"owner_unavailable"));
            var known=old.Items.SelectMany(i=>i.Evidence).Select(p=>(p.Id,p.Revision,p.Checksum.ToUpperInvariant())).ToHashSet();
            foreach(var pin in next.Items.SelectMany(i=>i.Evidence).Where(p=>!known.Contains((p.Id,p.Revision,p.Checksum.ToUpperInvariant()))))
                if(!await CertificateReader.FileAllowed(db,e.Actor,e.Vessel,pin,h.RequestAborted))return (null,null,await CommandFail(h,403,"linked_resource_unavailable"));
            return (next,certificate,null);
        });
    }
    private static async Task<IResult> Verify(Guid id,HttpContext h,PoseidonDbContext db) {
        var (env,error)=await Command(h,true);if(error!=null)return error;var e=env!;
        PreSurveyVerifyCommand command;
        try {
            command=await Body<PreSurveyVerifyCommand>(h);
            if(command.Id!=id || command.ExpectedVersion==0 || command.ExpectedRevision<=0 || command.Decisions==null || command.Decisions.Length is 0 or >PreSurveyChecklistPolicy.MaxDecisions ||
                command.Decisions.Any(d=>d==null || d.ItemId==Guid.Empty || d.ExpectedItemHash is not {Length:64} || !d.ExpectedItemHash.All(char.IsAsciiHexDigit)))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await CommandFail(h,400,"validation_failed");}
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,id,command},CertificateReader.Json));
        return await Apply(h,db,e,id,false,command.ExpectedVersion,command.ExpectedRevision,"PreSurveyVerify","VERIFY",fingerprint,async (_,current)=> {
            var certificate=await CertificateReader.Visible(db,e.Actor,e.Vessel,false).SingleOrDefaultAsync(c=>c.CertificateId==current!.CertificateId && c.Revision==current.CertificateRevision,h.RequestAborted);
            if(certificate==null)return (null,null,await CommandFail(h,409,"source_revision_changed"));
            var (next,errors)=PreSurveyChecklistPolicy.Verify(Content(current!),command.Decisions,e.Actor.Id,CertificateEndpoints.Now(h));
            if(errors.Length>0) {
                if(errors.Any(x=>x.Code=="validation_failed"))return (null,null,await CommandFail(h,400,"validation_failed"));
                // Every failing item is returned; one failure never hides another (60.04 p226).
                return (null,null,await Fail(h,"PreSurveyCommand",422,"verification_rejected",new{code="verification_rejected",items=errors.Select(x=>new PreSurveyItemErrorDto(x.ItemId,x.Code)).ToArray()}));
            }
            return (next,certificate,null);
        });
    }
}
