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

public sealed record WorkOrderOfflineSpareOption(Guid Id,long Revision,string PartNumber,string Name,string Unit);
// Prefetched WO context (DEV-057 AC1): owning WO declaration, execution requirements and catalogued spare
// options needed to record evidence without a connection. Nothing here grants a transition offline.
public sealed record WorkOrderOfflineContextDto(Guid Id,Guid TenantId,Guid ActorId,Guid VesselId,Guid WorkOrderId,Guid DeviceId,Guid KeyId,string Challenge,
    string PolicyVersion,string PolicyDigest,string AuthorityDigest,string SourceVersion,string Operation,string[] AllowedParts,uint BaseVersion,long BaseRevision,
    WorkOrderEvidence BaseEvidence,string State,string[]? RequiredEvidence,SortedDictionary<string,WorkOrderFact> WorkOrderFields,WorkOrderOfflineSpareOption[] SpareOptions,
    DateTimeOffset ServerTime,DateTimeOffset ExpiresAt,DateTimeOffset RetainUntil,int MaxRecords,int MaxEncryptedBytes,int MaxDeviceRecords,int MaxDeviceEncryptedBytes)
{ public override string ToString()=>nameof(WorkOrderOfflineContextDto); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderOfflineCapture(Guid CommandId,Guid WorkOrderId,Guid KeyId,Guid DeviceId,string PolicyVersion,string PolicyDigest,string SourceVersion,string Operation,
    uint BaseVersion,DateTimeOffset CapturedAt,int OffsetMinutes,WorkOrderEvidence? Evidence=null,Guid? ParentCommandId=null,uint? ExpectedServerVersion=null,string? Choice=null,string? ReasonCode=null)
{ public override string ToString()=>nameof(WorkOrderOfflineCapture); }
public sealed record WorkOrderOfflineResult(Guid CommandId,string Result,Guid WorkOrderId,uint BaseVersion,uint ServerVersion,long Revision,Guid? ParentCommandId,DateTimeOffset ReceivedAt);
internal sealed record WorkOrderOfflinePrivate(WorkOrderEvidence Base,WorkOrderEvidence? Local=null,WorkOrderEvidence? Server=null,string? ReasonCode=null)
{ public override string ToString()=>nameof(WorkOrderOfflinePrivate); }
public sealed record WorkOrderOfflineConflictDto(Guid CommandId,Guid EnrollmentId,Guid WorkOrderId,uint CurrentVersion,WorkOrderEvidence Base,WorkOrderEvidence Captured,
    WorkOrderEvidence Server,string[] Choices,WorkOrderOfflineResult? Resolved);

// DEV-057 / T-S8-006 (18.01 sheet:Backlog/row:58): WO context prefetched; evidence queue durable;
// reconnect sync idempotent; material field conflict goes to the conflict center. Same enrollment/key
// proof/replay protocol as the shared offline application (DEV-033..035), in its own resource namespace.
// Only the evidence-recording command (IN_PROGRESS) is offline; every lifecycle transition stays online.
public static class WorkOrderOfflineEndpoints
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=10,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static void MapWorkOrderOffline(this IEndpointRouteBuilder app) {
        var g=app.MapGroup("/api/v1/work-order-offline/{workOrder:guid}").RequireAuthorization("DirectoryRead");
        g.MapGet("",Inventory).WithName("WorkOrderOfflineInventory");
        g.MapPost("",Enroll).WithName("WorkOrderOfflineEnroll").Produces<WorkOrderOfflineContextDto>();
        g.MapPost("/{enrollment:guid}/{action}",Action).WithName("WorkOrderOfflineAction");
        g.MapGet("/{enrollment:guid}/history",History).WithName("WorkOrderOfflineHistory");
        g.MapGet("/{enrollment:guid}/conflicts/{command:guid}",Conflict).WithName("WorkOrderOfflineConflict");
    }
    private static TrustedActor? Actor(HttpContext h)=>WorkOrderExecutionEndpoints.Actor(h);
    private static DateTimeOffset Now(HttpContext h)=>WorkOrderExecutionEndpoints.Now(h);
    private static WorkOrderOfflinePolicy? Policy(HttpContext h,Guid vessel)=>Actor(h) is {} a ? h.RequestServices.GetRequiredService<WorkOrderOfflineSettings>().For(a.TenantId,a.Id,vessel,Now(h)):null;
    internal static WorkOrderOfflineDto Capability(HttpContext h,TrustedActor actor,Guid vessel,string? state)=>
        WorkOrderExecutionEndpoints.Execute(actor,vessel) && Policy(h,vessel)!=null
            ? new(true,state=="IN_PROGRESS"?"CONFIGURED_UNVERIFIED":"CONFIGURED_REQUIRES_IN_PROGRESS"):new(false,"UNAVAILABLE_POLICY");
    private static async Task<IResult> Fail(HttpContext h,int status,string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderOfflineRequest",code);return Results.Json(new{code},statusCode:status);
    }
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        vessel=Guid.Empty;offset=0;
        return h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vesselId" || paging && q.Key=="offset")) && Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static async Task<(TrustedActor? Actor,Guid Vessel,string Rights)> Gate(HttpContext h,PoseidonDbContext db,Guid workOrder,bool paging=false) {
        if(workOrder==Guid.Empty || !Query(h,paging,out var vessel,out _))return(null,Guid.Empty,"");
        var a=Actor(h);if(!WorkOrderExecutionEndpoints.Execute(a,vessel) || !a!.HasWriteAttribution)return(null,Guid.Empty,"");
        if(!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return(null,Guid.Empty,"");
        if(!await db.WorkOrderExecutions.AnyAsync(x=>x.WorkOrderId==workOrder && x.VesselId==vessel,h.RequestAborted))return(null,Guid.Empty,"");
        return(a,vessel,WorkOrderExecutionEndpoints.Rights(a));
    }
    private static bool Human(HttpContext h,WorkOrderOfflineEnrollment e)=>Actor(h) is {} a && a.Id==e.ActorId && a.TenantId==e.TenantId && CertificateRenewalEndpoints.Human(h,a);
    private static bool Matches(HttpContext h,WorkOrderOfflineEnrollment e,WorkOrderOfflinePolicy? p,DateTimeOffset now)=>p!=null && e.RevokedAt==null && now<e.RetainUntil &&
        Actor(h) is {} a && a.Id==e.ActorId && a.TenantId==e.TenantId && e.AuthorityDigest==WorkOrderExecutionEndpoints.Rights(a) && e.PolicyVersion==p.Version &&
        e.PolicyDigest==p.Fingerprint() && e.SourceVersion==p.SourceVersion;
    // DEV-034 reconciliation: receipt disclosure requires the same current policy/authority binding as replay recovery.
    internal static bool EnrollmentCurrent(HttpContext h,WorkOrderOfflineEnrollment e)=>Matches(h,e,Policy(h,e.VesselId),Now(h));
    internal static bool ReplayRights(TrustedActor a,Guid vessel)=>WorkOrderExecutionEndpoints.Execute(a,vessel) && a.HasWriteAttribution;
    // Revocation/key binding observed in a NEW trusted tenant context together with vessel visibility.
    private static async Task<bool> Fresh(HttpContext h,WorkOrderOfflineEnrollment expected,string rights,string? publicKey,bool privateAccess=true,bool captureLease=false) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var a=resolver.Resolve(h.User);
        if(a==null || a.Id!=expected.ActorId || a.TenantId!=expected.TenantId || !ReplayRights(a,expected.VesselId) || WorkOrderExecutionEndpoints.Rights(a)!=rights)return false;
        PoseidonDbContext db;try{db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=expected.TenantId)return false;
        var vessels=a.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==expected.VesselId);
        var fresh=await db.WorkOrderOfflineEnrollments.AsNoTracking().Where(e=>e.Id==expected.Id && e.ActorId==expected.ActorId && e.VesselId==expected.VesselId &&
            e.WorkOrderId==expected.WorkOrderId && vessels.Any()).SingleOrDefaultAsync(h.RequestAborted);
        if(fresh==null || fresh.DeviceId!=expected.DeviceId || fresh.KeyId!=expected.KeyId || fresh.PublicKey!=publicKey || fresh.Challenge!=expected.Challenge ||
            fresh.PolicyVersion!=expected.PolicyVersion || fresh.PolicyDigest!=expected.PolicyDigest || fresh.AuthorityDigest!=expected.AuthorityDigest ||
            fresh.SourceVersion!=expected.SourceVersion || fresh.BaseVersion!=expected.BaseVersion || fresh.BaseRevision!=expected.BaseRevision ||
            fresh.ProtectedBase!=expected.ProtectedBase || fresh.CreatedAt!=expected.CreatedAt || fresh.RetainUntil!=expected.RetainUntil)return false;
        var live=resolver.Resolve(h.User);
        return live!=null && live.Id==expected.ActorId && ReplayRights(live,expected.VesselId) && WorkOrderExecutionEndpoints.Rights(live)==rights &&
            (!privateAccess || Matches(h,fresh,Policy(h,expected.VesselId),Now(h)) && (!captureLease || Now(h)<fresh.ExpiresAt));
    }
    private static async Task<IResult> Committed(HttpContext h,WorkOrderOfflineEnrollment e,string rights,Func<IResult> response,bool privateAccess=true,bool captureLease=false,bool human=false,bool audit=false) {
        try {
            if(audit)await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderOfflineRequest","Success",resourceId:e.WorkOrderId);
            if(!await Fresh(h,e,rights,e.PublicKey,privateAccess,captureLease) || human && !Human(h,e))return await Fail(h,503,"command_outcome_withheld");
            return response();
        }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
    }
    private static async Task<bool> Csrf(HttpContext h) {
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);return true;}catch(AntiforgeryValidationException){return false;}
    }
    private static IDataProtector Protector(HttpContext h,WorkOrderOfflineEnrollment e)=>h.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("Poseidon.WorkOrderOffline.Private.v1",e.TenantId.ToString("D"),e.ActorId.ToString("D"),e.VesselId.ToString("D"),e.WorkOrderId.ToString("D"),
            e.Id.ToString("D"),e.PolicyDigest,e.AuthorityDigest,e.SourceVersion);
    private static string Protect(HttpContext h,WorkOrderOfflineEnrollment e,WorkOrderOfflinePrivate value)=>Protector(h,e).Protect(JsonSerializer.Serialize(value,Json));
    private static WorkOrderOfflinePrivate Private(HttpContext h,WorkOrderOfflineEnrollment e,string value)=>JsonSerializer.Deserialize<WorkOrderOfflinePrivate>(Protector(h,e).Unprotect(value),Json)!;
    private static IQueryable<WorkOrderOfflineEnrollment> Enrollments(PoseidonDbContext db,Guid actor,Guid vessel,Guid workOrder)=>
        db.WorkOrderOfflineEnrollments.Where(e=>e.ActorId==actor && e.VesselId==vessel && e.WorkOrderId==workOrder);
    private static IQueryable<WorkOrderOfflineOutcome> Outcomes(PoseidonDbContext db,WorkOrderOfflineEnrollment e)=>
        db.WorkOrderOfflineOutcomes.Where(o=>o.ActorId==e.ActorId && o.EnrollmentId==e.Id && o.VesselId==e.VesselId && o.WorkOrderId==e.WorkOrderId);
    private static void Event(PoseidonDbContext db,WorkOrderOfflineEnrollment e,string action,DateTimeOffset now)=>db.WorkOrderOfflineLifecycles.Add(new(){
        TenantId=e.TenantId,Id=Guid.NewGuid(),EnrollmentId=e.Id,ActorId=e.ActorId,VesselId=e.VesselId,WorkOrderId=e.WorkOrderId,Action=action,RecordedAt=now});
    private static async Task<WorkOrderOfflineContextDto> Context(HttpContext h,PoseidonDbContext db,WorkOrderOfflineEnrollment e,WorkOrderOfflinePolicy p) {
        var order=await db.WorkOrders.AsNoTracking().SingleAsync(w=>w.Id==e.WorkOrderId && w.VesselId==e.VesselId,h.RequestAborted);
        var current=await WorkOrderExecutionEndpoints.Current(db,e.VesselId).SingleAsync(r=>r.WorkOrderId==e.WorkOrderId,h.RequestAborted);var c=WorkOrderExecutionEndpoints.Content(current);
        var spares=await CriticalSpareEndpoints.CurrentRevisions(db,e.VesselId).OrderBy(r=>r.SpareId).Take(200).ToArrayAsync(h.RequestAborted);
        return new(e.Id,e.TenantId,e.ActorId,e.VesselId,e.WorkOrderId,e.DeviceId,e.KeyId,e.Challenge,e.PolicyVersion,e.PolicyDigest,e.AuthorityDigest,e.SourceVersion,
            WorkOrderOfflinePolicy.Operation,p.Fields.Select(f=>f.Key).ToArray(),e.BaseVersion,e.BaseRevision,Private(h,e,e.ProtectedBase).Base,c.State,c.RequiredEvidence,
            WorkOrderPolicy.ReadFields(order.FieldsJson),spares.Select(s=>{var d=CriticalSpareEndpoints.Content(s).Definition;return new WorkOrderOfflineSpareOption(s.SpareId,s.Revision,d.PartNumber,d.Name,d.Unit);}).ToArray(),
            Now(h),e.ExpiresAt,e.RetainUntil,p.MaxRecords,p.MaxEncryptedBytes,p.MaxDeviceRecords,p.MaxDeviceEncryptedBytes);
    }
    private static async Task<IResult> Inventory(Guid workOrder,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,workOrder);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var rows=await Enrollments(db,gate.Actor.Id,gate.Vessel,workOrder).AsNoTracking().OrderByDescending(e=>e.CreatedAt).ThenBy(e=>e.Id).Take(32)
            .Select(e=>new{e.Id,e.ExpiresAt,e.RetainUntil,e.RevokedAt,e.PolicyVersion}).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderOfflineRead","Success",resourceId:workOrder);
        if(Actor(h) is not {} live || !ReplayRights(live,gate.Vessel) || WorkOrderExecutionEndpoints.Rights(live)!=gate.Rights)return await Fail(h,403,"permission_denied");
        return Results.Ok(new{items=rows});
    }
    private static async Task<IResult> Enroll(Guid workOrder,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,workOrder);if(gate.Actor==null)return await Fail(h,403,"permission_denied");
        if(!await Csrf(h))return await Fail(h,403,"csrf_invalid");
        var p=Policy(h,gate.Vessel);if(p==null)return await Fail(h,403,"offline_policy_denied");
        IDbContextTransaction? tx=null;WorkOrderOfflineEnrollment? committed=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            if(await Enrollments(db,gate.Actor.Id,gate.Vessel,workOrder).CountAsync(e=>e.RevokedAt==null,h.RequestAborted)>=p.MaxEnrollments)return await Fail(h,409,"enrollment_limit");
            var head=await db.WorkOrderExecutions.AsNoTracking().SingleAsync(x=>x.WorkOrderId==workOrder && x.VesselId==gate.Vessel,h.RequestAborted);
            if(head.State!="IN_PROGRESS")return await Fail(h,409,"invalid_transition");
            var current=await db.WorkOrderExecutionRevisions.AsNoTracking().SingleAsync(r=>r.WorkOrderId==workOrder && r.Revision==head.Revision,h.RequestAborted);
            if(current.SourceVersion!=p.SourceVersion)return await Fail(h,409,"source_contract_changed");
            var evidence=WorkOrderExecutionEndpoints.Content(current).Evidence;
            if(!await PreSurveyChecklistEndpoints.FilesVisible(db,gate.Actor,gate.Vessel,WorkOrderExecutionEndpoints.Pins(evidence),h.RequestAborted))return await Fail(h,404,"resource_unavailable");
            var now=Now(h);var e=new WorkOrderOfflineEnrollment{TenantId=gate.Actor.TenantId,ActorId=gate.Actor.Id,VesselId=gate.Vessel,WorkOrderId=workOrder,Id=Guid.NewGuid(),
                DeviceId=Guid.NewGuid(),KeyId=Guid.NewGuid(),Challenge=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),PolicyVersion=p.Version,PolicyDigest=p.Fingerprint(),
                AuthorityDigest=gate.Rights,SourceVersion=p.SourceVersion,BaseVersion=head.Version,BaseRevision=head.Revision,CreatedAt=now,
                ExpiresAt=new[]{now.AddSeconds(p.LeaseSeconds),p.ExpiresAt}.Min(),RetainUntil=now.AddSeconds(p.RetentionSeconds)};
            e.ProtectedBase=Protect(h,e,new(evidence));db.WorkOrderOfflineEnrollments.Add(e);Event(db,e,"Enrolled",now);
            await db.SaveChangesAsync(h.RequestAborted);
            if(Actor(h) is not {} live || !ReplayRights(live,gate.Vessel) || WorkOrderExecutionEndpoints.Rights(live)!=gate.Rights || !Matches(h,e,Policy(h,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);committed=e;
        }catch(Exception ex)when(ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)){return await Failure(h,ex);}
        finally{if(tx!=null)await tx.DisposeAsync();}
        var context=await Context(h,db,committed!,p);
        return await Committed(h,committed!,gate.Rights,()=>Results.Ok(context),captureLease:true);
    }
    private static async Task<IResult> Failure(HttpContext h,Exception error)=>await Fail(h,
        error is DbUpdateConcurrencyException || PostgresFailure.IsSerialization(error) || PostgresFailure.Provider(error)?.SqlState=="23505"?409:503,"offline_retry");
    private static async Task<byte[]> Body(HttpContext h,int maximum) {
        if(!h.Request.HasJsonContentType())throw new JsonException();using var stream=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,stream,maximum,h.RequestAborted);
        using var document=JsonDocument.Parse(stream.ToArray(),new(){MaxDepth=10});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);return stream.ToArray();
    }
    private static async Task<IResult> Action(Guid workOrder,Guid enrollment,string action,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,workOrder);if(gate.Actor==null)return await Fail(h,403,"permission_denied");
        if(!await Csrf(h))return await Fail(h,403,"csrf_invalid");
        if(action is not("renew" or "key" or "revoke" or "lost" or "cleanup" or "replay"))return await Fail(h,404,"resource_unavailable");
        IDbContextTransaction? tx=null;WorkOrderOfflineEnrollment? committed=null;WorkOrderOfflinePolicy? committedPolicy=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,workOrder).SingleOrDefaultAsync(x=>x.Id==enrollment,h.RequestAborted);
            if(e==null)return await Fail(h,404,"resource_unavailable");var now=Now(h);var p=Policy(h,gate.Vessel);var originalKey=e.PublicKey;
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
                    if(key==null || !OfflineApplicationEndpoints.VerifyKey(key.PublicKey,key.Proof,$"work-order/{e.WorkOrderId:D}/{e.Id:D}/{e.KeyId:D}/{e.Challenge}") || e.PublicKey!=null && e.PublicKey!=key.PublicKey)
                        return await Fail(h,403,"invalid_key_enrollment");
                    if(e.PublicKey==null){e.PublicKey=key.PublicKey;Event(db,e,"KeyEnrolled",now);}
                }
            }
            await db.SaveChangesAsync(h.RequestAborted);
            if(!await Fresh(h,e,gate.Rights,originalKey,privateAccess:action is "renew" or "key",captureLease:action=="key"))return await Fail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);committed=e;committedPolicy=p;
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,400,"invalid_request");}
        catch(Exception ex)when(ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)){return await Failure(h,ex);}
        finally{if(tx!=null)await tx.DisposeAsync();}
        var saved=committed!;
        var context=action=="renew"?await Context(h,db,saved,committedPolicy!):null;
        return await Committed(h,saved,gate.Rights,()=>action switch {
            "renew"=>Results.Ok(context),
            "cleanup"=>Results.Ok(new{saved.Id,saved.TenantId,saved.ActorId,saved.VesselId,saved.WorkOrderId,saved.DeviceId,saved.KeyId,saved.PolicyVersion,saved.PolicyDigest,saved.AuthorityDigest,saved.SourceVersion}),
            _=>Results.Ok(new{state=action=="key"?"KeyEnrolled":"Revoked"})
        },privateAccess:action is "renew" or "key",captureLease:action=="renew");
    }
    private static async Task<IResult> Replay(HttpContext h,PoseidonDbContext db,IDbContextTransaction tx,WorkOrderOfflineEnrollment e,WorkOrderOfflinePolicy p,string rights) {
        var bytes=await Body(h,262144);var capture=JsonSerializer.Deserialize<WorkOrderOfflineCapture>(bytes,Json);var now=Now(h);var ct=h.RequestAborted;
        using var parsed=JsonDocument.Parse(bytes);var capturedText=parsed.RootElement.TryGetProperty("capturedAt",out var at)&&at.ValueKind==JsonValueKind.String?at.GetString():null;
        if(capture==null || capture.CommandId==Guid.Empty || capture.ParentCommandId==capture.CommandId || capture.WorkOrderId!=e.WorkOrderId || capture.KeyId!=e.KeyId || capture.DeviceId!=e.DeviceId ||
            capture.PolicyVersion!=e.PolicyVersion || capture.PolicyDigest!=e.PolicyDigest || capture.SourceVersion!=e.SourceVersion || capture.Operation!=WorkOrderOfflinePolicy.Operation ||
            capturedText==null || !(capturedText.EndsWith('Z')||capturedText.EndsWith("+00:00",StringComparison.Ordinal)))
            return await Fail(h,400,"invalid_offline_command");
        var idempotency=h.Request.Headers["Idempotency-Key"];if(idempotency.Count!=1 || idempotency[0]!=capture.CommandId.ToString("D"))return await Fail(h,400,"invalid_idempotency_key");
        var digest=Convert.ToHexString(SHA256.HashData(bytes));
        if(h.Request.Headers["X-Offline-Hash"].Count!=1 || !string.Equals(h.Request.Headers["X-Offline-Hash"],digest,StringComparison.Ordinal))return await Fail(h,400,"offline_hash_mismatch");
        if(h.Request.Headers["X-Offline-Signature"].Count!=1 || !OfflineApplicationEndpoints.VerifyKey(e.PublicKey,h.Request.Headers["X-Offline-Signature"],digest))return await Fail(h,403,"offline_key_proof_denied");
        if(capture.ParentCommandId!=null && !Human(h,e))return await Fail(h,403,"human_authority_required");
        // Idempotent reconnect: the same key and digest returns the immutable first outcome.
        var old=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.CommandId==capture.CommandId,ct);
        if(old!=null) {
            if(old.Digest!=digest)return await Fail(h,409,"offline_command_reused");
            if(!await Fresh(h,e,rights,e.PublicKey))return await Fail(h,403,"permission_denied");
            return await Committed(h,e,rights,()=>Results.Content(old.OutcomeJson,"application/json"),human:capture.ParentCommandId!=null,audit:true);
        }
        if(e.ExpiresAt<=now || !OfflineWatchCapture.ValidCaptureTime(capture.CapturedAt,capture.OffsetMinutes,e.CreatedAt,now,e.ExpiresAt))return await Fail(h,409,"offline_recovery_required");
        var head=await db.WorkOrderExecutions.SingleOrDefaultAsync(x=>x.WorkOrderId==e.WorkOrderId && x.VesselId==e.VesselId,ct);
        if(head==null)return await Fail(h,404,"resource_unavailable");
        var current=await db.WorkOrderExecutionRevisions.AsNoTracking().SingleAsync(r=>r.WorkOrderId==e.WorkOrderId && r.Revision==head.Revision,ct);
        if(current.SourceVersion!=e.SourceVersion)return await Fail(h,409,"source_contract_changed");
        var content=WorkOrderExecutionEndpoints.Content(current);var server=content.Evidence;var before=Private(h,e,e.ProtectedBase).Base;WorkOrderEvidence local;
        if(capture.ParentCommandId==null) {
            if(capture.BaseVersion!=e.BaseVersion || capture.ExpectedServerVersion!=null || capture.Choice!=null || capture.ReasonCode!=null || !WorkOrderExecutionPolicy.ValidEvidence(capture.Evidence))
                return await Fail(h,400,"invalid_offline_command");
            local=capture.Evidence!;
        }else {
            var parent=await Outcomes(db,e).SingleOrDefaultAsync(o=>o.CommandId==capture.ParentCommandId && o.Result=="MaterialConflict",ct);
            if(parent==null || capture.Evidence!=null || capture.ExpectedServerVersion!=head.Version || capture.BaseVersion!=head.Version ||
                capture.Choice is not("keepServer" or "useCaptured") || capture.ReasonCode!=(capture.Choice=="keepServer"?"CURRENT_SERVER_RETAINED":"INPUT_CORRECTED"))return await Fail(h,409,"offline_conflict_changed");
            if(await Outcomes(db,e).AnyAsync(o=>o.ParentCommandId==parent.CommandId,ct))return await Fail(h,409,"offline_resolution_exists");
            local=capture.Choice=="keepServer"?server:Private(h,e,parent.ProtectedContext).Local!;
        }
        // Any concurrent head change (evidence, submit, return, verify) is a material conflict: no silent last-write-wins.
        var result=capture.ParentCommandId!=null && capture.Choice=="keepServer"?"ServerRetained":head.Version==capture.BaseVersion?"Committed":"MaterialConflict";
        uint serverVersion=head.Version;long revision=head.Revision;
        if(result=="Committed") {
            if(Actor(h) is not {} writer || writer.Id!=e.ActorId || writer.TenantId!=e.TenantId)return await Fail(h,403,"permission_denied");
            var (next,problem)=WorkOrderExecutionPolicy.RecordEvidence(content,local,writer.Id);if(problem!=null)return await Fail(h,problem.Status,problem.Code);
            if(await WorkOrderExecutionEndpoints.Linked(db,writer,e.VesselId,local,server,ct) is {} linked)return await Fail(h,403,linked);
            head.Revision++;var row=await WorkOrderExecutionEndpoints.Append(db,writer,e.VesselId,head,current.WorkOrderRevision,current.WorkOrderVersion,current.WorkOrderHash,next!,"EVIDENCE",now,ct);
            serverVersion=head.Version;revision=row.Revision;
        }
        var outcome=new WorkOrderOfflineResult(capture.CommandId,result,e.WorkOrderId,capture.BaseVersion,serverVersion,revision,capture.ParentCommandId,now);
        var json=JsonSerializer.Serialize(outcome,Json);
        db.WorkOrderOfflineOutcomes.Add(new(){TenantId=e.TenantId,ActorId=e.ActorId,CommandId=capture.CommandId,EnrollmentId=e.Id,VesselId=e.VesselId,WorkOrderId=e.WorkOrderId,
            ParentCommandId=capture.ParentCommandId,Digest=digest,Result=result,BaseVersion=capture.BaseVersion,ServerVersion=serverVersion,Revision=revision,OutcomeJson=json,
            CapturedAt=capture.CapturedAt.ToUniversalTime(),ReceivedAt=now,ProtectedContext=Protect(h,e,new(before,local,server,capture.ReasonCode))});
        await db.SaveChangesAsync(ct);
        if(!await Fresh(h,e,rights,e.PublicKey,captureLease:true) || capture.ParentCommandId!=null && !Human(h,e))return await Fail(h,403,"permission_denied");
        await tx.CommitAsync(ct);
        return await Committed(h,e,rights,()=>Results.Content(json,"application/json"),human:capture.ParentCommandId!=null,audit:true);
    }
    private static async Task<IResult> History(Guid workOrder,Guid enrollment,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,workOrder,true);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,workOrder).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==enrollment,h.RequestAborted);
        if(e==null || !Matches(h,e,Policy(h,gate.Vessel),Now(h)))return await Fail(h,403,"offline_policy_denied");
        Query(h,true,out _,out var offset);var rows=await Outcomes(db,e).AsNoTracking().OrderByDescending(o=>o.ReceivedAt).ThenBy(o=>o.CommandId).Skip(offset).Take(26)
            .Select(o=>o.OutcomeJson).ToArrayAsync(h.RequestAborted);
        if(!await Fresh(h,e,gate.Rights,e.PublicKey))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderOfflineRead","Success",resourceId:workOrder);
        if(!await Fresh(h,e,gate.Rights,e.PublicKey))return await Fail(h,403,"permission_denied");
        return Results.Ok(new{items=rows.Take(25).Select(s=>JsonSerializer.Deserialize<WorkOrderOfflineResult>(s,Json)),offset,hasMore=rows.Length>25});
    }
    private static async Task<IResult> Conflict(Guid workOrder,Guid enrollment,Guid command,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,workOrder);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,workOrder).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==enrollment,h.RequestAborted);
        if(e==null || !Matches(h,e,Policy(h,gate.Vessel),Now(h)))return await Fail(h,403,"offline_policy_denied");
        var outcome=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.CommandId==command && o.Result=="MaterialConflict",h.RequestAborted);
        if(outcome==null)return await Fail(h,404,"resource_unavailable");
        var head=await db.WorkOrderExecutions.AsNoTracking().SingleAsync(x=>x.WorkOrderId==workOrder && x.VesselId==gate.Vessel,h.RequestAborted);
        var current=await db.WorkOrderExecutionRevisions.AsNoTracking().SingleAsync(r=>r.WorkOrderId==workOrder && r.Revision==head.Revision,h.RequestAborted);
        if(current.SourceVersion!=e.SourceVersion)return await Fail(h,409,"source_contract_changed");
        var server=WorkOrderExecutionEndpoints.Content(current).Evidence;var retained=Private(h,e,outcome.ProtectedContext);
        if(!await PreSurveyChecklistEndpoints.FilesVisible(db,gate.Actor,gate.Vessel,WorkOrderExecutionEndpoints.Pins(server),h.RequestAborted))return await Fail(h,404,"resource_unavailable");
        var resolved=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.ParentCommandId==command,h.RequestAborted);
        if(!await Fresh(h,e,gate.Rights,e.PublicKey))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderOfflineRead","Success",resourceId:workOrder);
        if(!await Fresh(h,e,gate.Rights,e.PublicKey))return await Fail(h,403,"permission_denied");
        // Resolving a captured value into the current record is only possible while the work is still IN_PROGRESS.
        string[] choices=resolved!=null || !Human(h,e)?[]:head.State=="IN_PROGRESS"?["keepServer","useCaptured"]:["keepServer"];
        return Results.Ok(new WorkOrderOfflineConflictDto(command,enrollment,workOrder,head.Version,retained.Base,retained.Local!,server,choices,
            resolved==null?null:JsonSerializer.Deserialize<WorkOrderOfflineResult>(resolved.OutcomeJson,Json)));
    }
}
