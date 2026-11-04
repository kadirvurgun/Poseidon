using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DailyOfflineContextDto(Guid Id, Guid TenantId, Guid ActorId, Guid VesselId, string Kind, Guid RecordId,
    Guid DeviceId, Guid KeyId, string Challenge, string PolicyVersion, string PolicyDigest, string AuthorityDigest, string SourceVersion, string SchemaHash,
    string Operation, string[] AllowedFields, uint BaseVersion, long BaseRevision, SortedDictionary<string,DailyValue> BaseFields,
    DateTimeOffset ServerTime, DateTimeOffset ExpiresAt, DateTimeOffset RetainUntil, int MaxRecords, int MaxEncryptedBytes,
    int MaxDeviceRecords, int MaxDeviceEncryptedBytes) { public override string ToString()=>nameof(DailyOfflineContextDto); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DailyOfflineCapture(Guid CommandId, Guid RecordId, string Kind, Guid KeyId, Guid DeviceId, string PolicyVersion, string PolicyDigest,
    string SourceVersion, string SchemaHash, string Operation, uint BaseVersion, DateTimeOffset CapturedAt, int OffsetMinutes,
    [property:JsonRequired] Dictionary<string,DailyValue> Fields, Guid? ParentCommandId=null, uint? ExpectedServerVersion=null,
    string? Choice=null, string? ReasonCode=null) { public override string ToString()=>nameof(DailyOfflineCapture); }
public sealed record DailyOfflineResult(Guid CommandId, string Result, Guid RecordId, string Kind, uint BaseVersion,
    uint ServerVersion, long Revision, Guid? ParentCommandId, DateTimeOffset ReceivedAt);
internal sealed record DailyOfflinePrivate(SortedDictionary<string,DailyValue> Base, SortedDictionary<string,DailyValue>? Local=null,
    SortedDictionary<string,DailyValue>? Server=null, string? ReasonCode=null) { public override string ToString()=>nameof(DailyOfflinePrivate); }
public sealed record DailyOfflineConflictField(string Key, DailyValue Base, DailyValue Captured, DailyValue Server);
public sealed record DailyOfflineConflictDto(Guid CommandId, Guid EnrollmentId, Guid RecordId, string Kind, uint CurrentVersion,
    DailyOfflineConflictField[] Fields, string[] Choices, DailyOfflineResult? Resolved);

public static class DailyOfflineEndpoints
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive=false,MaxDepth=8,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
    public static void MapDailyOffline(this IEndpointRouteBuilder app) {
        var g=app.MapGroup("/api/v1/daily-offline/{kind}/{record:guid}").RequireAuthorization("DirectoryRead");
        g.MapGet("",Inventory).WithName("DailyOfflineInventory");
        g.MapPost("",Enroll).WithName("DailyOfflineEnroll").Produces<DailyOfflineContextDto>();
        g.MapPost("/{enrollment:guid}/{action}",Action).WithName("DailyOfflineAction");
        g.MapGet("/{enrollment:guid}/history",History).WithName("DailyOfflineHistory");
        g.MapGet("/{enrollment:guid}/conflicts/{command:guid}",Conflict).WithName("DailyOfflineConflict");
    }
    private static TrustedActor? Actor(HttpContext h)=>h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static DateTimeOffset Now(HttpContext h) { var now=h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().ToUniversalTime();return now.AddTicks(-(now.Ticks%10)); }
    private static DailyOfflinePolicy? Policy(HttpContext h,string kind,Guid vessel)=>Actor(h) is {} a
        ? h.RequestServices.GetRequiredService<DailyOfflineSettings>().For(a.TenantId,a.Id,vessel,kind,Now(h)):null;
    internal static DailyOfflineDto Capability(HttpContext h,TrustedActor actor,string kind,Guid vessel)=>
        DailyOperationsEndpoints.CanWrite(actor,kind,vessel) && Policy(h,kind,vessel)!=null
            ? new(true,"CONFIGURED_UNVERIFIED",DailyOfflinePolicy.Operation(kind)):new(false,"UNAVAILABLE_POLICY",DailyOfflinePolicy.Operation(kind));
    private static async Task<IResult> Fail(HttpContext h,int status,string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"DailyOfflineRequest",code);
        return Results.Json(new {code},statusCode:status);
    }
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        vessel=Guid.Empty;offset=0;
        return h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vesselId" || paging && q.Key=="offset")) &&
            Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static async Task<(TrustedActor? Actor,Guid Vessel,string Rights)> Gate(HttpContext h,PoseidonDbContext db,string kind,Guid record,bool paging=false) {
        if(!DailyOperationsPolicy.Kinds.Contains(kind) || record==Guid.Empty || !Query(h,paging,out var vessel,out _))return(null,Guid.Empty,"");
        var a=Actor(h); if(!DailyOperationsEndpoints.CanWrite(a,kind,vessel))return(null,Guid.Empty,"");
        if(!await a!.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel && (v.Kind==VesselKind.Ship || a.Permissions.Contains("mode.cross")),h.RequestAborted))return(null,Guid.Empty,"");
        if(await db.DailyOperations.AnyAsync(r=>r.Id==record && (r.VesselId!=vessel || r.Kind!=kind),h.RequestAborted))return(null,Guid.Empty,"");
        return(a,vessel,DailyOperationsEndpoints.Rights(a));
    }
    private static bool Human(HttpContext h,DailyOfflineEnrollment e) {
        var actor=Actor(h);if(actor==null || actor.Id!=e.ActorId || actor.TenantId!=e.TenantId)return false;
        var members=h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m=>m.ActorId==actor.Id && m.TenantId==actor.TenantId).Take(2).ToArray();
        return members.Length==1 && members[0].ActorKind=="Human";
    }
    private static bool CommandPermission(HttpContext h,string kind) => DailyOperationsPolicy.Kinds.Contains(kind) && Actor(h) is {} actor &&
        actor.HasWriteAttribution && DailyOperationsEndpoints.CanRead(actor,kind) && actor.Permissions.Contains("daily."+kind.ToLowerInvariant()+".write");
    private static Task<bool> Current(HttpContext h,string kind,Guid vessel,string rights)=>DailyOperationsEndpoints.Current(h,kind,vessel,rights,true);
    private static async Task<bool> Csrf(HttpContext h) {
        try {await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);return true;}
        catch(AntiforgeryValidationException){return false;}
    }
    private static IDataProtector Protector(HttpContext h,DailyOfflineEnrollment e)=>h.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("Poseidon.DailyOffline.Private.v1",e.TenantId.ToString("D"),e.ActorId.ToString("D"),e.VesselId.ToString("D"),e.Kind,
            e.RecordId.ToString("D"),e.Id.ToString("D"),e.PolicyDigest,e.AuthorityDigest,e.SourceVersion,e.SchemaHash);
    private static string Protect(HttpContext h,DailyOfflineEnrollment e,DailyOfflinePrivate value)=>Protector(h,e).Protect(JsonSerializer.Serialize(value,Json));
    private static DailyOfflinePrivate Private(HttpContext h,DailyOfflineEnrollment e,string value)=>JsonSerializer.Deserialize<DailyOfflinePrivate>(Protector(h,e).Unprotect(value),Json)!;
    private static IQueryable<DailyOfflineEnrollment> Enrollments(PoseidonDbContext db,Guid actor,Guid vessel,string kind,Guid record)=>
        db.DailyOfflineEnrollments.Where(e=>e.ActorId==actor && e.VesselId==vessel && e.Kind==kind && e.RecordId==record);
    private static IQueryable<DailyOfflineOutcome> Outcomes(PoseidonDbContext db,DailyOfflineEnrollment e)=>
        db.DailyOfflineOutcomes.Where(o=>o.ActorId==e.ActorId && o.EnrollmentId==e.Id && o.VesselId==e.VesselId && o.Kind==e.Kind && o.RecordId==e.RecordId);
    private static bool Matches(HttpContext h,DailyOfflineEnrollment e,DailyOfflinePolicy? p,DateTimeOffset now)=>p!=null && e.RevokedAt==null && now<e.RetainUntil &&
        Actor(h) is {} actor && e.AuthorityDigest==DailyOperationsEndpoints.Rights(actor) && e.PolicyVersion==p.Version && e.PolicyDigest==p.Fingerprint() && e.SourceVersion==p.SourceVersion && e.SchemaHash==p.SchemaHash();
    private static SortedDictionary<string,DailyValue> Select(DailyOfflinePolicy p,IReadOnlyDictionary<string,DailyValue> fields)=>
        new(p.Fields.ToDictionary(f=>f.Key,f=>fields.GetValueOrDefault(f.Key)??new DailyValue("Missing",null)),StringComparer.Ordinal);
    private static bool Fields(DailyOfflinePolicy p,Dictionary<string,DailyValue>? fields,out SortedDictionary<string,DailyValue> normalized) {
        normalized=new(StringComparer.Ordinal);
        if(fields==null || fields.Count!=p.Fields.Length || fields.Keys.Any(k=>!p.Fields.Any(f=>f.Key==k)) ||
            !DailyOperationsPolicy.TryNormalize(p.Kind,fields,out var all,out _))return false;
        normalized=Select(p,all);return true;
    }
    private static void Event(PoseidonDbContext db,DailyOfflineEnrollment e,string action,DateTimeOffset now)=>db.DailyOfflineLifecycles.Add(new(){
        TenantId=e.TenantId,Id=Guid.NewGuid(),EnrollmentId=e.Id,ActorId=e.ActorId,VesselId=e.VesselId,Kind=e.Kind,RecordId=e.RecordId,Action=action,RecordedAt=now});
    private static DailyOfflineContextDto Context(HttpContext h,DailyOfflineEnrollment e,DailyOfflinePolicy p)=>new(e.Id,e.TenantId,e.ActorId,e.VesselId,e.Kind,e.RecordId,
        e.DeviceId,e.KeyId,e.Challenge,e.PolicyVersion,e.PolicyDigest,e.AuthorityDigest,e.SourceVersion,e.SchemaHash,DailyOfflinePolicy.Operation(e.Kind),p.Fields.Select(f=>f.Key).ToArray(),
        e.BaseVersion,e.BaseRevision,Private(h,e,e.ProtectedBase).Base,Now(h),e.ExpiresAt,e.RetainUntil,p.MaxRecords,p.MaxEncryptedBytes,p.MaxDeviceRecords,p.MaxDeviceEncryptedBytes);
    private static async Task<IResult> Inventory(string kind,Guid record,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,kind,record);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var rows=await Enrollments(db,gate.Actor.Id,gate.Vessel,kind,record).AsNoTracking().OrderByDescending(e=>e.CreatedAt).ThenBy(e=>e.Id).Take(32)
            .Select(e=>new {e.Id,e.ExpiresAt,e.RetainUntil,e.RevokedAt,e.PolicyVersion}).ToArrayAsync(h.RequestAborted);
        if(!await Current(h,kind,gate.Vessel,gate.Rights))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"DailyOfflineRead","Success",resourceId:record);
        if(!await Current(h,kind,gate.Vessel,gate.Rights))return await Fail(h,403,"permission_denied");
        return Results.Ok(new {items=rows});
    }
    private static async Task<IResult> Enroll(string kind,Guid record,HttpContext h,PoseidonDbContext db) {
        if(!CommandPermission(h,kind))return await Fail(h,403,"permission_denied");
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,kind,record);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        if(!await Csrf(h))return await Fail(h,403,"csrf_invalid");
        var p=Policy(h,kind,gate.Vessel);if(p==null)return await Fail(h,403,"offline_policy_denied");
        IDbContextTransaction? tx=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            if(await Enrollments(db,gate.Actor.Id,gate.Vessel,kind,record).CountAsync(e=>e.RevokedAt==null,h.RequestAborted)>=p.MaxEnrollments)return await Fail(h,409,"enrollment_limit");
            var owner=await db.DailyOperations.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==record,h.RequestAborted);
            if(owner!=null && (owner.VesselId!=gate.Vessel || owner.Kind!=kind || owner.SourceVersion!=p.SourceVersion))return await Fail(h,404,"resource_unavailable");
            var fields=owner==null?new SortedDictionary<string,DailyValue>(StringComparer.Ordinal):JsonSerializer.Deserialize<SortedDictionary<string,DailyValue>>(owner.FieldsJson)!;
            var now=Now(h);var e=new DailyOfflineEnrollment {TenantId=gate.Actor.TenantId,ActorId=gate.Actor.Id,VesselId=gate.Vessel,Kind=kind,RecordId=record,Id=Guid.NewGuid(),
                DeviceId=Guid.NewGuid(),KeyId=Guid.NewGuid(),Challenge=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),PolicyVersion=p.Version,PolicyDigest=p.Fingerprint(),AuthorityDigest=gate.Rights,
                SourceVersion=p.SourceVersion,SchemaHash=p.SchemaHash(),BaseVersion=owner?.Version??0,BaseRevision=owner?.Revision??0,CreatedAt=now,
                ExpiresAt=new[]{now.AddSeconds(p.LeaseSeconds),p.ExpiresAt}.Min(),RetainUntil=now.AddSeconds(p.RetentionSeconds)};
            e.ProtectedBase=Protect(h,e,new(Select(p,fields)));db.DailyOfflineEnrollments.Add(e);Event(db,e,"Enrolled",now);
            await db.SaveChangesAsync(h.RequestAborted);
            if(!await Current(h,kind,gate.Vessel,gate.Rights) || !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);
            if(!await Current(h,kind,gate.Vessel,gate.Rights) || !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
            return Results.Ok(Context(h,e,p));
        }catch(Exception ex)when(ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)){return await Failure(h,ex);}
        finally{if(tx!=null)await tx.DisposeAsync();}
    }
    private static async Task<IResult> Failure(HttpContext h,Exception error)=>await Fail(h,
        error is DbUpdateConcurrencyException || PostgresFailure.IsSerialization(error) || PostgresFailure.Provider(error)?.SqlState=="23505"?409:503,"offline_retry");
    private static async Task<byte[]> Body(HttpContext h,int maximum=65536) {
        if(!h.Request.HasJsonContentType())throw new JsonException();using var stream=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,stream,maximum,h.RequestAborted);
        using var document=JsonDocument.Parse(stream.ToArray(),new(){MaxDepth=8});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);return stream.ToArray();
    }
    private static async Task<IResult> Action(string kind,Guid record,Guid enrollment,string action,HttpContext h,PoseidonDbContext db) {
        if(!CommandPermission(h,kind))return await Fail(h,403,"permission_denied");
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,kind,record);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        if(!await Csrf(h))return await Fail(h,403,"csrf_invalid");
        if(action is not("renew" or "key" or "revoke" or "lost" or "cleanup" or "replay"))return await Fail(h,404,"resource_unavailable");
        IDbContextTransaction? tx=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,kind,record).SingleOrDefaultAsync(e=>e.Id==enrollment,h.RequestAborted);
            if(e==null)return await Fail(h,404,"resource_unavailable");var now=Now(h);var p=Policy(h,kind,gate.Vessel);
            if(action is "revoke" or "lost" or "cleanup") {
                if(action=="cleanup")Event(db,e,"CleanupAllowed",now);
                else if(e.RevokedAt==null){e.RevokedAt=now;Event(db,e,action=="lost"?"KeyLost":"Revoked",now);}
            }else {
                if(!Matches(h,e,p,now))return await Fail(h,403,"offline_policy_denied");
                if(action=="renew"){e.ExpiresAt=new[]{now.AddSeconds(p!.LeaseSeconds),p.ExpiresAt,e.RetainUntil}.Min();Event(db,e,"Renewed",now);}
                else if(action=="replay")return await Replay(h,db,tx,e,p!,gate.Rights);
                else {
                    if(e.ExpiresAt<=now)return await Fail(h,403,"reauthorization_required");
                    var key=JsonSerializer.Deserialize<OfflineKeyEnrollment>(await Body(h,1024),Json);
                    if(key==null || !OfflineApplicationEndpoints.VerifyKey(key.PublicKey,key.Proof,$"daily/{e.Kind}/{e.RecordId:D}/{e.Id:D}/{e.KeyId:D}/{e.Challenge}") || e.PublicKey!=null && e.PublicKey!=key.PublicKey)
                        return await Fail(h,403,"invalid_key_enrollment");
                    if(e.PublicKey==null){e.PublicKey=key.PublicKey;Event(db,e,"KeyEnrolled",now);}
                }
            }
            await db.SaveChangesAsync(h.RequestAborted);
            if(!await Current(h,kind,gate.Vessel,gate.Rights) || (action is "renew" or "key") && !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);
            if(!await Current(h,kind,gate.Vessel,gate.Rights) || (action is "renew" or "key") && !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
            if(action=="renew")return Results.Ok(Context(h,e,p!));
            if(action=="cleanup")return Results.Ok(new {e.Id,e.TenantId,e.ActorId,e.VesselId,e.Kind,e.RecordId,e.DeviceId,e.KeyId,e.PolicyVersion,e.PolicyDigest,e.AuthorityDigest,e.SourceVersion,e.SchemaHash});
            return Results.Ok(new {state=action=="key"?"KeyEnrolled":"Revoked"});
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,400,"invalid_request");}
        catch(Exception ex)when(ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)){return await Failure(h,ex);}
        finally{if(tx!=null)await tx.DisposeAsync();}
    }
    private static async Task<IResult> Replay(HttpContext h,PoseidonDbContext db,IDbContextTransaction tx,DailyOfflineEnrollment e,DailyOfflinePolicy p,string rights) {
        var bytes=await Body(h);var capture=JsonSerializer.Deserialize<DailyOfflineCapture>(bytes,Json);var now=Now(h);var ct=h.RequestAborted;
        using var parsed=JsonDocument.Parse(bytes);var capturedText=parsed.RootElement.TryGetProperty("capturedAt",out var at)&&at.ValueKind==JsonValueKind.String?at.GetString():null;
        if(capture==null || capture.CommandId==Guid.Empty || capture.ParentCommandId==capture.CommandId || capture.RecordId!=e.RecordId || capture.Kind!=e.Kind || capture.KeyId!=e.KeyId || capture.DeviceId!=e.DeviceId ||
            capture.PolicyVersion!=e.PolicyVersion || capture.PolicyDigest!=e.PolicyDigest || capture.SourceVersion!=e.SourceVersion || capture.SchemaHash!=e.SchemaHash ||
            capture.Operation!=DailyOfflinePolicy.Operation(e.Kind) || capturedText==null || !(capturedText.EndsWith('Z')||capturedText.EndsWith("+00:00",StringComparison.Ordinal)))
            return await Fail(h,400,"invalid_offline_command");
        var idempotency=h.Request.Headers["Idempotency-Key"];if(idempotency.Count!=1 || idempotency[0]!=capture.CommandId.ToString("D"))return await Fail(h,400,"invalid_idempotency_key");
        var digest=Convert.ToHexString(SHA256.HashData(bytes));
        if(h.Request.Headers["X-Offline-Hash"].Count!=1 || !string.Equals(h.Request.Headers["X-Offline-Hash"],digest,StringComparison.Ordinal))return await Fail(h,400,"offline_hash_mismatch");
        if(h.Request.Headers["X-Offline-Signature"].Count!=1 || !OfflineApplicationEndpoints.VerifyKey(e.PublicKey,h.Request.Headers["X-Offline-Signature"],digest))return await Fail(h,403,"offline_key_proof_denied");
        if(capture.ParentCommandId!=null && !Human(h,e))return await Fail(h,403,"human_authority_required");
        // Owning current scope, policy, device, exact source and proof precede recovery.
        var old=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.CommandId==capture.CommandId,ct);
        if(old!=null) {
            if(old.Digest!=digest)return await Fail(h,409,"offline_command_reused");
            if(!await Current(h,e.Kind,e.VesselId,rights) || !Matches(h,e,Policy(h,e.Kind,e.VesselId),Now(h)) || capture.ParentCommandId!=null && !Human(h,e))return await Fail(h,403,"permission_denied");
            return Results.Content(old.OutcomeJson,"application/json");
        }
        if(e.ExpiresAt<=now || !OfflineWatchCapture.ValidCaptureTime(capture.CapturedAt,capture.OffsetMinutes,e.CreatedAt,now,e.ExpiresAt))return await Fail(h,409,"offline_recovery_required");
        var row=await db.DailyOperations.SingleOrDefaultAsync(r=>r.Id==e.RecordId,ct);
        if(row!=null && (row.VesselId!=e.VesselId || row.Kind!=e.Kind || row.SourceVersion!=e.SourceVersion))return await Fail(h,409,"source_contract_changed");
        var serverVersion=row?.Version??0;var revision=row?.Revision??0;
        var full=row==null?new SortedDictionary<string,DailyValue>(StringComparer.Ordinal):JsonSerializer.Deserialize<SortedDictionary<string,DailyValue>>(row.FieldsJson)!;
        var server=Select(p,full);var before=Private(h,e,e.ProtectedBase).Base;SortedDictionary<string,DailyValue> local;
        if(capture.ParentCommandId==null) {
            if(capture.BaseVersion!=e.BaseVersion || capture.ExpectedServerVersion!=null || capture.Choice!=null || capture.ReasonCode!=null || !Fields(p,capture.Fields,out local))return await Fail(h,400,"invalid_offline_command");
        }else {
            var parent=await Outcomes(db,e).SingleOrDefaultAsync(o=>o.CommandId==capture.ParentCommandId && o.Result=="MaterialConflict",ct);
            if(parent==null || capture.Fields==null || capture.Fields.Count!=0 || capture.ExpectedServerVersion!=serverVersion || capture.BaseVersion!=serverVersion ||
                capture.Choice is not("keepServer" or "useCaptured") || capture.ReasonCode!=(capture.Choice=="keepServer"?"CURRENT_SERVER_RETAINED":"INPUT_CORRECTED"))return await Fail(h,409,"offline_conflict_changed");
            if(await Outcomes(db,e).AnyAsync(o=>o.ParentCommandId==parent.CommandId,ct))return await Fail(h,409,"offline_resolution_exists");
            var retained=Private(h,e,parent.ProtectedContext);
            local=capture.Choice=="keepServer"?server:retained.Local!;
            if(!Fields(p,local.ToDictionary(),out local))return await Fail(h,403,"offline_policy_denied");
        }
        var result=capture.ParentCommandId!=null && capture.Choice=="keepServer"?"ServerRetained":serverVersion==capture.BaseVersion?"Committed":"MaterialConflict";
        if(result=="Committed") {
            foreach(var field in local)full[field.Key]=field.Value;
            var ordinary=new DailyDraftCommand(e.RecordId,serverVersion,full.ToDictionary(),serverVersion==0?"INITIAL_AUTHORING":"AUTHORING_CORRECTION");
            var applied=await DailyOperationsWriter.Apply(db,e.ActorId,e.VesselId,e.Kind,ordinary,row==null,now,ct);
            if(applied.Record==null)return await Fail(h,applied.Status,applied.Code??"invalid_request");
            serverVersion=applied.Record.Version;revision=applied.Record.Revision;
        }
        var outcome=new DailyOfflineResult(capture.CommandId,result,e.RecordId,e.Kind,capture.BaseVersion,serverVersion,revision,capture.ParentCommandId,now);
        var json=JsonSerializer.Serialize(outcome,Json);
        db.DailyOfflineOutcomes.Add(new(){TenantId=e.TenantId,ActorId=e.ActorId,CommandId=capture.CommandId,EnrollmentId=e.Id,VesselId=e.VesselId,Kind=e.Kind,RecordId=e.RecordId,
            ParentCommandId=capture.ParentCommandId,Digest=digest,Result=result,BaseVersion=capture.BaseVersion,ServerVersion=serverVersion,Revision=revision,OutcomeJson=json,
            ReceivedAt=now,ProtectedContext=Protect(h,e,new(before,local,server,capture.ReasonCode))});
        await db.SaveChangesAsync(ct);
        if(!await Current(h,e.Kind,e.VesselId,rights) || !Matches(h,e,Policy(h,e.Kind,e.VesselId),Now(h)) || capture.ParentCommandId!=null && !Human(h,e))return await Fail(h,403,"permission_denied");
        await tx.CommitAsync(ct);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"DailyOfflineRequest","Success",resourceId:e.RecordId);
        if(!await Current(h,e.Kind,e.VesselId,rights) || !Matches(h,e,Policy(h,e.Kind,e.VesselId),Now(h)) || capture.ParentCommandId!=null && !Human(h,e))return await Fail(h,503,"command_outcome_withheld");
        return Results.Content(json,"application/json");
    }
    private static async Task<IResult> History(string kind,Guid record,Guid enrollment,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,kind,record,true);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,kind,record).AsNoTracking().SingleOrDefaultAsync(e=>e.Id==enrollment,h.RequestAborted);
        if(e==null || !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"offline_policy_denied");
        Query(h,true,out _,out var offset);var rows=await Outcomes(db,e).AsNoTracking().OrderByDescending(o=>o.ReceivedAt).ThenBy(o=>o.CommandId).Skip(offset).Take(26)
            .Select(o=>o.OutcomeJson).ToArrayAsync(h.RequestAborted);
        if(!await Current(h,kind,gate.Vessel,gate.Rights) || !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"DailyOfflineRead","Success",resourceId:record);
        if(!await Current(h,kind,gate.Vessel,gate.Rights) || !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
        return Results.Ok(new {items=rows.Take(25).Select(s=>JsonSerializer.Deserialize<DailyOfflineResult>(s,Json)),offset,hasMore=rows.Length>25});
    }
    private static async Task<IResult> Conflict(string kind,Guid record,Guid enrollment,Guid command,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,kind,record);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,kind,record).AsNoTracking().SingleOrDefaultAsync(e=>e.Id==enrollment,h.RequestAborted);
        var p=Policy(h,kind,gate.Vessel);if(e==null || !Matches(h,e,p,Now(h)))return await Fail(h,403,"offline_policy_denied");
        var outcome=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.CommandId==command && o.Result=="MaterialConflict",h.RequestAborted);
        if(outcome==null)return await Fail(h,404,"resource_unavailable");
        var owner=await db.DailyOperations.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==record && r.Kind==kind && r.VesselId==gate.Vessel,h.RequestAborted);
        if(owner!=null && owner.SourceVersion!=e.SourceVersion)return await Fail(h,409,"source_contract_changed");
        var retained=Private(h,e,outcome.ProtectedContext);var server=Select(p!,owner==null?new Dictionary<string,DailyValue>():JsonSerializer.Deserialize<Dictionary<string,DailyValue>>(owner.FieldsJson)!);
        var resolved=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.ParentCommandId==command,h.RequestAborted);
        var fields=p!.Fields.Select(f=>new DailyOfflineConflictField(f.Key,retained.Base[f.Key],retained.Local![f.Key],server[f.Key])).ToArray();
        if(!await Current(h,kind,gate.Vessel,gate.Rights) || !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"DailyOfflineRead","Success",resourceId:record);
        if(!await Current(h,kind,gate.Vessel,gate.Rights) || !Matches(h,e,Policy(h,kind,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
        return Results.Ok(new DailyOfflineConflictDto(command,enrollment,record,kind,owner?.Version??0,fields,
            resolved==null && Human(h,e)?["keepServer","useCaptured"]:[],resolved==null?null:JsonSerializer.Deserialize<DailyOfflineResult>(resolved.OutcomeJson,Json)));
    }
}
