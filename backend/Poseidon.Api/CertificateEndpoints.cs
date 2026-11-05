using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record CertificateHistoryDto(CertificateDto[] Items,int Offset,int Limit,bool HasMore);

public static class CertificateEndpoints
{
    private static readonly JsonSerializerOptions Input=new(CertificateReader.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    internal static TrustedActor? Actor(HttpContext h)=>h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static DateTimeOffset Now(HttpContext h)=>h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    internal static async Task<IResult> Fail(HttpContext h,int status,string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,h.Request.Method=="GET"?"CertificateRead":"CertificateCommand",code);
        return Results.Json(new{code},statusCode:status);
    }
    public static void MapCertificates(this IEndpointRouteBuilder app) {
        var g=app.MapGroup("/api/v1/vessels/{vessel:guid}/certificates").RequireAuthorization("DirectoryRead");
        g.MapGet("",(Guid vessel,HttpContext h,PoseidonDbContext db)=>List(vessel,h,db)).WithName("VesselCertificateList");
        g.MapGet("/{id:guid}",(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db)=>Read(vessel,id,false,h,db)).WithName("VesselCertificateRead").Produces<CertificateDto>();
        g.MapGet("/{id:guid}/history",(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db)=>Read(vessel,id,true,h,db)).WithName("VesselCertificateHistory");
        g.MapPost("",(Guid vessel,HttpContext h,PoseidonDbContext db)=>Save(vessel,null,h,db)).WithName("VesselCertificateCreate").Accepts<CertificateSaveCommand>("application/json").Produces<CertificateDto>();
        g.MapPut("/{id:guid}",(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db)=>Save(vessel,id,h,db)).WithName("VesselCertificateUpdate").Accepts<CertificateSaveCommand>("application/json").Produces<CertificateDto>();
        app.MapGet("/api/v1/certificate-command-center",Center).RequireAuthorization("DirectoryRead").WithName("CertificateCommandCenter").Produces<CertificateCenterDto>();
        app.MapGet("/api/v1/certificate-command-center/capabilities",Capabilities).RequireAuthorization("DirectoryRead").WithName("CertificateCapabilities").Produces<CertificateCapabilitiesDto>();
    }
    internal static bool Offset(HttpContext h,IReadOnlySet<string> keys,out int offset) {
        offset=0;return h.Request.Query.All(q=>keys.Contains(q.Key) && q.Value.Count==1) &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static Task<IResult> List(Guid vessel,HttpContext h,PoseidonDbContext db)=>CenterRead(vessel,"Vessel",null,null,h,db);
    private static async Task<IResult> Center(HttpContext h,PoseidonDbContext db) {
        if(!Guid.TryParseExact(h.Request.Query["vesselId"],"D",out var vessel) || vessel==Guid.Empty)return await Fail(h,400,"invalid_query");
        var scope=h.Request.Query["scope"].FirstOrDefault()??"Vessel";var temporal=h.Request.Query["temporalState"].FirstOrDefault();
        if(scope is not("Vessel" or "Crew" or "All") || temporal is not(null or "VALID" or "DUE" or "SURVEY_WINDOW" or "EXPIRED" or "REVIEW_REQUIRED"))return await Fail(h,400,"invalid_query");
        return await CenterRead(vessel,scope,temporal,h.Request.Query["purpose"].FirstOrDefault(),h,db);
    }
    private static async Task<IResult> CenterRead(Guid vessel,string selection,string? temporal,string? purpose,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";
        if(!Offset(h,new HashSet<string>{"vesselId","scope","temporalState","purpose","offset"},out var offset))return await Fail(h,400,"invalid_query");
        var actor=Actor(h);if(actor==null || !CertificateReader.Read(actor))return await Fail(h,403,"permission_denied");
        var rights=CertificateReader.Rights(actor);var date=DateOnly.FromDateTime(Now(h).UtcDateTime);
        if(!await actor.Scope(db.Vessels).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return await Fail(h,404,"resource_not_found");
        var policy=CrewFilePolicy.Current(h);var crewAllowed=CertificateReader.CrewRead(actor,vessel,policy,purpose);
        var policyHash=RegisterStorage.Hash(JsonSerializer.Serialize(policy,CertificateReader.Json));
        if(selection=="Crew" && !crewAllowed)return await Fail(h,403,"field_policy_denied");
        var certificates=CertificateReader.Temporal(CertificateReader.Visible(db,actor,vessel),temporal,date);
        var crew=CertificateReader.CrewTemporal(CertificateReader.CrewVisible(db,actor,vessel,policy,purpose),temporal,date);
        var vesselCount=selection=="Crew"?0:await certificates.CountAsync(h.RequestAborted);
        var rows=new List<CertificateDto>();
        if(selection!="Crew")rows.AddRange((await certificates.OrderBy(r=>r.CertificateId).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted)).Select(r=>CertificateReader.Dto(r,actor,date)));
        if(selection!="Vessel" && crewAllowed && rows.Count<26)rows.AddRange((await crew.OrderBy(r=>r.Id).Skip(Math.Max(0,offset-vesselCount)).Take(26-rows.Count).ToArrayAsync(h.RequestAborted)).Select(r=>CertificateReader.CrewDto(r,date)));
        var returned=rows.Take(25).ToArray();var result=new CertificateCenterDto(returned,offset,25,rows.Count>25,selection,"AUTHORIZED_PAGE_ONLY",
            selection=="Vessel"?"NOT_REQUESTED":crewAllowed?"AVAILABLE":"UNAVAILABLE_POLICY_OR_AUTHORITY",CertificateReader.Counts(returned),null,"REVIEW_REQUIRED",date);
        if(!await CertificateReader.Current(h,vessel,rights,returned,purpose:purpose))return await Fail(h,403,"permission_denied");
        if(selection!="Vessel" && policyHash!=RegisterStorage.Hash(JsonSerializer.Serialize(CrewFilePolicy.Current(h),CertificateReader.Json)))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateRead","Success",resourceId:vessel);
        if(!await CertificateReader.Current(h,vessel,rights,returned,purpose:purpose))return await Fail(h,403,"permission_denied");
        if(selection!="Vessel" && policyHash!=RegisterStorage.Hash(JsonSerializer.Serialize(CrewFilePolicy.Current(h),CertificateReader.Json)))return await Fail(h,403,"permission_denied");
        return Results.Ok(result);
    }
    private static async Task<IResult> Capabilities(HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";
        if(!Offset(h,new HashSet<string>{"vesselId"},out _) || !Guid.TryParseExact(h.Request.Query["vesselId"],"D",out var vessel) || vessel==Guid.Empty)return await Fail(h,400,"invalid_query");
        var a=Actor(h);if(a==null || !CertificateReader.Read(a))return await Fail(h,403,"permission_denied");
        if(!await a.Scope(db.Vessels).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return await Fail(h,404,"resource_not_found");
        var rights=CertificateReader.Rights(a);var policy=CrewFilePolicy.Current(h);var crew=CertificateReader.CrewRead(a,vessel,policy,policy.Purpose);
        var policyHash=RegisterStorage.Hash(JsonSerializer.Serialize(policy,CertificateReader.Json));
        var result=new CertificateCapabilitiesDto(vessel,true,CertificateReader.Write(a,vessel),crew,crew?policy.Purpose:null,VesselCertificatePolicy.SourceVersion,
            crew?["Vessel","Crew","All"]:["Vessel"],"DeclaredUtcCalendarNotLegalValidity",["Available","Missing","Review"],
            ["declaration","documentReference","renewalLeadDaysNumeric","sourceSystem","notes"]);
        async Task<bool> Current()=>await CertificateReader.Current(h,vessel,rights,[]) && policyHash==RegisterStorage.Hash(JsonSerializer.Serialize(CrewFilePolicy.Current(h),CertificateReader.Json));
        if(!await Current())return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateRead","Success",resourceId:vessel);
        if(!await Current())return await Fail(h,403,"permission_denied");return Results.Ok(result);
    }
    private static async Task<IResult> Read(Guid vessel,Guid id,bool history,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";
        if(!Offset(h,history?new HashSet<string>{"offset"}:new HashSet<string>(),out var offset))return await Fail(h,400,"invalid_query");
        var a=Actor(h);if(a==null || !CertificateReader.Read(a))return await Fail(h,403,"permission_denied");var rights=CertificateReader.Rights(a);
        if(!await a.Scope(db.Vessels).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return await Fail(h,404,"resource_not_found");
        var head=await db.VesselCertificates.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==id && c.VesselId==vessel,h.RequestAborted);
        if(head==null)return await Fail(h,404,"resource_not_found");
        var rows=await CertificateReader.Visible(db,a,vessel,!history).Where(r=>r.CertificateId==id).OrderByDescending(r=>r.Revision).Skip(offset).Take(history?26:1).ToArrayAsync(h.RequestAborted);
        if(rows.Length==0)return await Fail(h,404,"resource_not_found");var date=DateOnly.FromDateTime(Now(h).UtcDateTime);
        var returned=rows.Take(25).Select(r=>CertificateReader.Dto(r,a,date,r.Revision==head.Revision)).ToArray();
        if(!await CertificateReader.Current(h,vessel,rights,returned))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateRead","Success",resourceId:id);
        if(!await CertificateReader.Current(h,vessel,rights,returned))return await Fail(h,403,"permission_denied");
        return history?Results.Ok(new CertificateHistoryDto(returned,offset,25,rows.Length>25)):Results.Ok(returned[0]);
    }
    private static async Task<IResult> Save(Guid vessel,Guid? routeId,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var actor=Actor(h);
        if(actor==null || !CertificateReader.Write(actor,vessel))return await Fail(h,403,"permission_denied");
        if(h.Request.Query.Count!=0)return await Fail(h,400,"invalid_query");
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;
        if(key==null || !Guid.TryParseExact(key,"D",out _))return await Fail(h,400,"invalid_idempotency_key");
        CertificateSaveCommand command;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var buffer=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,65536,h.RequestAborted);
            using var document=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=8});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command=document.RootElement.Deserialize<CertificateSaveCommand>(Input)??throw new JsonException();
            if(command.Id==Guid.Empty || routeId!=null && command.Id!=routeId || command.ReasonCode is not("INITIAL_AUTHORING" or "AUTHORING_CORRECTION") || !VesselCertificatePolicy.Valid(command.Content))throw new JsonException();
        }catch(Exception e)when(e is JsonException or InvalidDataException){return await Fail(h,400,"validation_failed");}
        var rights=CertificateReader.Rights(actor);var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,routeId,command},CertificateReader.Json));
        if(!await CertificateReader.Current(h,vessel,rights,[],write:true,pendingFiles:command.Content.Document==null?[]:[command.Content.Document]))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateCommand","Attempted",resourceId:command.Id);
        async Task<IResult?> Replay() {
            if(!await CertificateReader.Current(h,vessel,rights,[],write:true))return await Fail(h,403,"permission_denied");
            var r=await db.CertificateCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,h.RequestAborted);
            if(r==null)return null;
            if(r.Action!="CertificateSave" || r.VesselId!=vessel || r.ResourceId!=command.Id || r.Fingerprint!=fingerprint)return await Fail(h,409,"idempotency_key_reused");
            var saved=JsonSerializer.Deserialize<CertificateDto>(r.OutcomeJson,CertificateReader.Json)??throw new InvalidDataException("certificate_receipt_unavailable");
            return await Outcome(h,vessel,rights,[saved],r.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;
        IDbContextTransaction? tx=null;CertificateDto? savedRecord=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            var result=await CertificateWriter.Apply(db,actor,vessel,command,routeId==null,Now(h),h.RequestAborted);
            if(result.Record==null)return await Fail(h,result.Status,result.Code!);
            savedRecord=result.Record;outcome=JsonSerializer.Serialize(savedRecord,CertificateReader.Json);
            db.CertificateCommandReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,ResourceId=command.Id,CertificateId=command.Id,
                Action="CertificateSave",Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=Now(h)});await db.SaveChangesAsync(h.RequestAborted);
            var old=result.Previous==null?Array.Empty<CertificateDto>():[result.Previous];
            if(!await CertificateReader.Current(h,vessel,rights,old,write:true,pendingFiles:command.Content.Document==null?[]:[command.Content.Document]))return await Fail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await Fail(h,409,"version_conflict");}
        catch(Exception e)when(PostgresFailure.IsDatabase(e)) {
            if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(e)||PostgresFailure.Provider(e)?.SqlState=="23505";
            return await Replay()??await Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");
        }finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(h,vessel,rights,[savedRecord!],outcome!);
    }
    internal static async Task<IResult> Outcome(HttpContext h,Guid vessel,string rights,CertificateDto[] records,string json) {
        try {
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateCommand","Success",resourceId:records.FirstOrDefault()?.Id??vessel);
            if(!await CertificateReader.Current(h,vessel,rights,records,write:true))return await Fail(h,503,"command_outcome_withheld");
            return Results.Content(json,"application/json");
        }catch(Exception e)when(e is AuditUnavailableException || PostgresFailure.IsDatabase(e)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
    }
}
