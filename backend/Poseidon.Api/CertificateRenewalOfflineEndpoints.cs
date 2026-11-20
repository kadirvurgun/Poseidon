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

// Prefetched renewal context (DEV-066 flow 1): the bound renewal head revision, its draft content and the
// owner role options needed to author the draft without a connection. Nothing here grants a transition offline.
public sealed record CertificateRenewalOfflineContextDto(Guid Id,Guid TenantId,Guid ActorId,Guid VesselId,Guid CertificateId,Guid RenewalId,Guid DeviceId,Guid KeyId,
    string Challenge,string PolicyVersion,string PolicyDigest,string AuthorityDigest,string SourceVersion,string Operation,string[] AllowedParts,uint BaseVersion,long BaseRevision,
    CertificateRenewalContent BaseContent,string State,Guid[] OwnerOptions,DateTimeOffset ServerTime,DateTimeOffset ExpiresAt,DateTimeOffset RetainUntil,
    int MaxRecords,int MaxEncryptedBytes,int MaxDeviceRecords,int MaxDeviceEncryptedBytes)
{ public override string ToString()=>nameof(CertificateRenewalOfflineContextDto); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRenewalOfflineCapture(Guid CommandId,Guid RenewalId,Guid KeyId,Guid DeviceId,string PolicyVersion,string PolicyDigest,string SourceVersion,
    string Operation,uint BaseVersion,DateTimeOffset CapturedAt,int OffsetMinutes,CertificateRenewalContent? Content=null,Guid? ParentCommandId=null,uint? ExpectedServerVersion=null,
    string? Choice=null,string? ReasonCode=null)
{ public override string ToString()=>nameof(CertificateRenewalOfflineCapture); }
public sealed record CertificateRenewalOfflineResult(Guid CommandId,string Result,Guid RenewalId,uint BaseVersion,uint ServerVersion,long Revision,Guid? ParentCommandId,DateTimeOffset ReceivedAt);
internal sealed record CertificateRenewalOfflinePrivate(CertificateRenewalContent Base,CertificateRenewalContent? Local=null,CertificateRenewalContent? Server=null,string? ReasonCode=null)
{ public override string ToString()=>nameof(CertificateRenewalOfflinePrivate); }
public sealed record CertificateRenewalOfflineConflictDto(Guid CommandId,Guid EnrollmentId,Guid RenewalId,uint CurrentVersion,string State,CertificateRenewalContent Base,
    CertificateRenewalContent Captured,CertificateRenewalContent Server,string[] Choices,CertificateRenewalOfflineResult? Resolved);

// S10 WP-C DEV-066 (18.01 sheet:Backlog/row:67) pilot offline journey flow 1: certificate renewal draft.
// Same enrollment/key proof/signed idempotent replay protocol as the shared offline application
// (DEV-033..035) and the DEV-057 work-order namespace. A replay appends exactly one DRAFT/PREPARING
// revision; any concurrent head change is a MaterialConflict recorded for the conflict center and never a
// silent last-write-wins. Conflict resolution is a reasoned human command. Transitions stay online.
public static class CertificateRenewalOfflineEndpoints
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=10,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    private const string Read="CertificateRenewalOfflineRead",Request="CertificateRenewalOfflineRequest";
    public static void MapCertificateRenewalOffline(this IEndpointRouteBuilder app) {
        var g=app.MapGroup("/api/v1/certificate-renewal-offline/{renewal:guid}").RequireAuthorization("DirectoryRead");
        g.MapGet("",Inventory).WithName("CertificateRenewalOfflineInventory");
        g.MapPost("",Enroll).WithName("CertificateRenewalOfflineEnroll").Produces<CertificateRenewalOfflineContextDto>();
        g.MapPost("/{enrollment:guid}/{action}",Action).WithName("CertificateRenewalOfflineAction");
        g.MapGet("/{enrollment:guid}/history",History).WithName("CertificateRenewalOfflineHistory");
        g.MapGet("/{enrollment:guid}/conflicts/{command:guid}",Conflict).WithName("CertificateRenewalOfflineConflict");
    }
    private static TrustedActor? Actor(HttpContext h)=>CertificateEndpoints.Actor(h);
    private static DateTimeOffset Now(HttpContext h){var now=CertificateEndpoints.Now(h).ToUniversalTime();return now.AddTicks(-(now.Ticks%10));}
    private static CertificateRenewalOfflinePolicy? Policy(HttpContext h,Guid vessel)=>Actor(h) is {} a?h.RequestServices.GetRequiredService<CertificateRenewalOfflineSettings>().For(a.TenantId,a.Id,vessel,Now(h)):null;
    internal static bool ReplayRights(TrustedActor? a,Guid vessel)=>CertificateRenewalEndpoints.Write(a,vessel) && a!.HasWriteAttribution;
    private static string Rights(TrustedActor a)=>CertificateReader.Rights(a);
    // Navigation/capability projection only: CONFIGURED_UNVERIFIED never means a pilot-approved offline mode.
    internal static string Capability(HttpContext h,TrustedActor a,Guid vessel)=>ReplayRights(a,vessel) && Policy(h,vessel)!=null?"CONFIGURED_UNVERIFIED":"UNAVAILABLE_POLICY";
    private static async Task<IResult> Fail(HttpContext h,int status,string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,Request,code);return Results.Json(new{code},statusCode:status);
    }
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        vessel=Guid.Empty;offset=0;
        return h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vesselId" || paging && q.Key=="offset")) && Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static async Task<(TrustedActor? Actor,Guid Vessel,string Rights)> Gate(HttpContext h,PoseidonDbContext db,Guid renewal,bool paging=false) {
        if(renewal==Guid.Empty || !Query(h,paging,out var vessel,out _))return(null,Guid.Empty,"");
        var a=Actor(h);if(!ReplayRights(a,vessel) || db.TenantId!=a!.TenantId)return(null,Guid.Empty,"");
        if(!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return(null,Guid.Empty,"");
        // The renewal case must be currently visible through the ordinary renewal read path.
        if(!await CertificateRenewalEndpoints.Visible(db,a,vessel).AnyAsync(r=>r.RenewalId==renewal,h.RequestAborted))return(null,Guid.Empty,"");
        return(a,vessel,Rights(a));
    }
    private static bool Human(HttpContext h,CertificateRenewalOfflineEnrollment e)=>Actor(h) is {} a && a.Id==e.ActorId && a.TenantId==e.TenantId && CertificateRenewalEndpoints.Human(h,a);
    private static bool Matches(HttpContext h,CertificateRenewalOfflineEnrollment e,CertificateRenewalOfflinePolicy? p,DateTimeOffset now)=>p!=null && e.RevokedAt==null && now<e.RetainUntil &&
        Actor(h) is {} a && a.Id==e.ActorId && a.TenantId==e.TenantId && e.AuthorityDigest==Rights(a) && e.PolicyVersion==p.Version &&
        e.PolicyDigest==p.Fingerprint() && e.SourceVersion==p.SourceVersion;
    internal static bool EnrollmentCurrent(HttpContext h,CertificateRenewalOfflineEnrollment e)=>Matches(h,e,Policy(h,e.VesselId),Now(h));
    // Revocation/key binding observed in a NEW trusted tenant context together with vessel visibility.
    private static async Task<bool> Fresh(HttpContext h,CertificateRenewalOfflineEnrollment expected,string rights,string? publicKey,bool privateAccess=true,bool captureLease=false) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var a=resolver.Resolve(h.User);
        if(a==null || a.Id!=expected.ActorId || a.TenantId!=expected.TenantId || !ReplayRights(a,expected.VesselId) || Rights(a)!=rights)return false;
        PoseidonDbContext db;try{db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=expected.TenantId)return false;
        var vessels=a.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==expected.VesselId);
        var fresh=await db.CertificateRenewalOfflineEnrollments.AsNoTracking().Where(e=>e.Id==expected.Id && e.ActorId==expected.ActorId && e.VesselId==expected.VesselId &&
            e.RenewalId==expected.RenewalId && vessels.Any()).SingleOrDefaultAsync(h.RequestAborted);
        if(fresh==null || fresh.DeviceId!=expected.DeviceId || fresh.KeyId!=expected.KeyId || fresh.PublicKey!=publicKey || fresh.Challenge!=expected.Challenge ||
            fresh.PolicyVersion!=expected.PolicyVersion || fresh.PolicyDigest!=expected.PolicyDigest || fresh.AuthorityDigest!=expected.AuthorityDigest ||
            fresh.SourceVersion!=expected.SourceVersion || fresh.BaseVersion!=expected.BaseVersion || fresh.BaseRevision!=expected.BaseRevision ||
            fresh.ProtectedBase!=expected.ProtectedBase || fresh.CreatedAt!=expected.CreatedAt || fresh.RetainUntil!=expected.RetainUntil)return false;
        var live=resolver.Resolve(h.User);
        return live!=null && live.Id==expected.ActorId && ReplayRights(live,expected.VesselId) && Rights(live)==rights &&
            (!privateAccess || Matches(h,fresh,Policy(h,expected.VesselId),Now(h)) && (!captureLease || Now(h)<fresh.ExpiresAt));
    }
    private static async Task<IResult> Committed(HttpContext h,CertificateRenewalOfflineEnrollment e,string rights,Func<IResult> response,bool privateAccess=true,bool captureLease=false,bool human=false,bool audit=false) {
        try {
            if(audit)await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,Request,"Success",resourceId:e.RenewalId);
            if(!await Fresh(h,e,rights,e.PublicKey,privateAccess,captureLease) || human && !Human(h,e))return await Fail(h,503,"command_outcome_withheld");
            return response();
        }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
    }
    private static async Task<bool> Csrf(HttpContext h) {
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);return true;}catch(AntiforgeryValidationException){return false;}
    }
    private static IDataProtector Protector(HttpContext h,CertificateRenewalOfflineEnrollment e)=>h.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("Poseidon.CertificateRenewalOffline.Private.v1",e.TenantId.ToString("D"),e.ActorId.ToString("D"),e.VesselId.ToString("D"),e.RenewalId.ToString("D"),
            e.Id.ToString("D"),e.PolicyDigest,e.AuthorityDigest,e.SourceVersion);
    private static string Protect(HttpContext h,CertificateRenewalOfflineEnrollment e,CertificateRenewalOfflinePrivate value)=>Protector(h,e).Protect(JsonSerializer.Serialize(value,Json));
    private static CertificateRenewalOfflinePrivate Private(HttpContext h,CertificateRenewalOfflineEnrollment e,string value)=>JsonSerializer.Deserialize<CertificateRenewalOfflinePrivate>(Protector(h,e).Unprotect(value),Json)!;
    private static IQueryable<CertificateRenewalOfflineEnrollment> Enrollments(PoseidonDbContext db,Guid actor,Guid vessel,Guid renewal)=>
        db.CertificateRenewalOfflineEnrollments.Where(e=>e.ActorId==actor && e.VesselId==vessel && e.RenewalId==renewal);
    private static IQueryable<CertificateRenewalOfflineOutcome> Outcomes(PoseidonDbContext db,CertificateRenewalOfflineEnrollment e)=>
        db.CertificateRenewalOfflineOutcomes.Where(o=>o.ActorId==e.ActorId && o.EnrollmentId==e.Id && o.VesselId==e.VesselId && o.RenewalId==e.RenewalId);
    private static void Event(PoseidonDbContext db,CertificateRenewalOfflineEnrollment e,string action,DateTimeOffset now)=>db.CertificateRenewalOfflineLifecycles.Add(new(){
        TenantId=e.TenantId,Id=Guid.NewGuid(),EnrollmentId=e.Id,ActorId=e.ActorId,VesselId=e.VesselId,RenewalId=e.RenewalId,Action=action,RecordedAt=now});
    private static async Task<(CertificateRenewal Head,CertificateRenewalRevision Current)?> Head(PoseidonDbContext db,TrustedActor a,Guid vessel,Guid renewal,bool tracked,CancellationToken ct) {
        var heads=tracked?db.CertificateRenewals:db.CertificateRenewals.AsNoTracking();
        var head=await heads.SingleOrDefaultAsync(r=>r.Id==renewal && r.VesselId==vessel,ct);if(head==null)return null;
        var current=await CertificateRenewalEndpoints.Visible(db,a,vessel).SingleOrDefaultAsync(r=>r.RenewalId==renewal && r.Revision==head.Revision,ct);
        return current==null?null:(head,current);
    }
    private static CertificateRenewalOfflineContextDto Context(HttpContext h,TrustedActor a,CertificateRenewalOfflineEnrollment e,CertificateRenewalOfflinePolicy p,CertificateRenewalRevision current)=>new(
        e.Id,e.TenantId,e.ActorId,e.VesselId,e.CertificateId,e.RenewalId,e.DeviceId,e.KeyId,e.Challenge,e.PolicyVersion,e.PolicyDigest,e.AuthorityDigest,e.SourceVersion,
        CertificateRenewalOfflinePolicy.Operation,p.Fields.Select(f=>f.Key).ToArray(),e.BaseVersion,e.BaseRevision,Private(h,e,e.ProtectedBase).Base,current.State,
        CertificateRenewalEndpoints.Owners(h,a,e.VesselId).Take(256).ToArray(),Now(h),e.ExpiresAt,e.RetainUntil,p.MaxRecords,p.MaxEncryptedBytes,p.MaxDeviceRecords,p.MaxDeviceEncryptedBytes);

    private static async Task<IResult> Inventory(Guid renewal,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,renewal);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var rows=await Enrollments(db,gate.Actor.Id,gate.Vessel,renewal).AsNoTracking().OrderByDescending(e=>e.CreatedAt).ThenBy(e=>e.Id).Take(32)
            .Select(e=>new{e.Id,e.ExpiresAt,e.RetainUntil,e.RevokedAt,e.PolicyVersion}).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,Read,"Success",resourceId:renewal);
        if(Actor(h) is not {} live || !ReplayRights(live,gate.Vessel) || Rights(live)!=gate.Rights)return await Fail(h,403,"permission_denied");
        return Results.Ok(new{items=rows});
    }
    private static async Task<IResult> Failure(HttpContext h,Exception error)=>await Fail(h,
        error is DbUpdateConcurrencyException || PostgresFailure.IsSerialization(error) || PostgresFailure.Provider(error)?.SqlState=="23505"?409:503,"offline_retry");
    private static async Task<IResult> Enroll(Guid renewal,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,renewal);if(gate.Actor==null)return await Fail(h,403,"permission_denied");
        if(!await Csrf(h))return await Fail(h,403,"csrf_invalid");
        var p=Policy(h,gate.Vessel);if(p==null)return await Fail(h,403,"offline_policy_denied");
        IDbContextTransaction? tx=null;CertificateRenewalOfflineEnrollment? committed=null;CertificateRenewalRevision? row=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            if(await Enrollments(db,gate.Actor.Id,gate.Vessel,renewal).CountAsync(e=>e.RevokedAt==null,h.RequestAborted)>=p.MaxEnrollments)return await Fail(h,409,"enrollment_limit");
            if(await Head(db,gate.Actor,gate.Vessel,renewal,false,h.RequestAborted) is not {} bound)return await Fail(h,404,"resource_unavailable");
            // Only the editable draft stages can be authored offline (DEV-044 freezes validated content).
            if(!CertificateRenewalWorkflow.Editable(bound.Current.State))return await Fail(h,409,"renewal_state_locked");
            if(bound.Current.SourceVersion!=p.SourceVersion)return await Fail(h,409,"source_contract_changed");
            var now=Now(h);var e=new CertificateRenewalOfflineEnrollment{TenantId=gate.Actor.TenantId,ActorId=gate.Actor.Id,VesselId=gate.Vessel,CertificateId=bound.Head.CertificateId,
                RenewalId=renewal,Id=Guid.NewGuid(),DeviceId=Guid.NewGuid(),KeyId=Guid.NewGuid(),Challenge=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),PolicyVersion=p.Version,
                PolicyDigest=p.Fingerprint(),AuthorityDigest=gate.Rights,SourceVersion=p.SourceVersion,BaseVersion=bound.Head.Version,BaseRevision=bound.Head.Revision,CreatedAt=now,
                ExpiresAt=new[]{now.AddSeconds(p.LeaseSeconds),p.ExpiresAt}.Min(),RetainUntil=now.AddSeconds(p.RetentionSeconds)};
            e.ProtectedBase=Protect(h,e,new(CertificateRenewalEndpoints.Content(bound.Current)));db.CertificateRenewalOfflineEnrollments.Add(e);Event(db,e,"Enrolled",now);
            await db.SaveChangesAsync(h.RequestAborted);
            if(Actor(h) is not {} live || !ReplayRights(live,gate.Vessel) || Rights(live)!=gate.Rights || !Matches(h,e,Policy(h,gate.Vessel),Now(h)))return await Fail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);committed=e;row=bound.Current;
        }catch(Exception ex)when(ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)){return await Failure(h,ex);}
        finally{if(tx!=null)await tx.DisposeAsync();}
        var context=Context(h,gate.Actor,committed!,p,row!);
        return await Committed(h,committed!,gate.Rights,()=>Results.Ok(context),captureLease:true);
    }
    private static async Task<byte[]> Body(HttpContext h,int maximum) {
        if(!h.Request.HasJsonContentType())throw new JsonException();using var stream=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,stream,maximum,h.RequestAborted);
        using var document=JsonDocument.Parse(stream.ToArray(),new(){MaxDepth=10});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);return stream.ToArray();
    }
    private static async Task<IResult> Action(Guid renewal,Guid enrollment,string action,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,renewal);if(gate.Actor==null)return await Fail(h,403,"permission_denied");
        if(!await Csrf(h))return await Fail(h,403,"csrf_invalid");
        if(action is not("renew" or "key" or "revoke" or "lost" or "cleanup" or "replay"))return await Fail(h,404,"resource_unavailable");
        IDbContextTransaction? tx=null;CertificateRenewalOfflineEnrollment? committed=null;CertificateRenewalOfflinePolicy? committedPolicy=null;CertificateRenewalRevision? current=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,renewal).SingleOrDefaultAsync(x=>x.Id==enrollment,h.RequestAborted);
            if(e==null)return await Fail(h,404,"resource_unavailable");var now=Now(h);var p=Policy(h,gate.Vessel);var originalKey=e.PublicKey;
            if(action is "revoke" or "lost" or "cleanup") {
                if(action=="cleanup")Event(db,e,"CleanupAllowed",now);
                else if(e.RevokedAt==null){e.RevokedAt=now;Event(db,e,action=="lost"?"KeyLost":"Revoked",now);}
            }else {
                if(!Matches(h,e,p,now))return await Fail(h,403,"offline_policy_denied");
                if(action=="renew") {
                    if(await Head(db,gate.Actor,gate.Vessel,renewal,false,h.RequestAborted) is not {} bound)return await Fail(h,404,"resource_unavailable");
                    current=bound.Current;e.ExpiresAt=new[]{now.AddSeconds(p!.LeaseSeconds),p.ExpiresAt,e.RetainUntil}.Min();Event(db,e,"Renewed",now);
                }
                else if(action=="replay")return await Replay(h,db,tx,e,gate.Rights);
                else {
                    if(e.ExpiresAt<=now)return await Fail(h,403,"reauthorization_required");
                    var key=JsonSerializer.Deserialize<OfflineKeyEnrollment>(await Body(h,1024),Json);
                    if(key==null || !OfflineApplicationEndpoints.VerifyKey(key.PublicKey,key.Proof,$"certificate-renewal/{e.RenewalId:D}/{e.Id:D}/{e.KeyId:D}/{e.Challenge}") || e.PublicKey!=null && e.PublicKey!=key.PublicKey)
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
        var context=action=="renew"?Context(h,gate.Actor,saved,committedPolicy!,current!):null;
        return await Committed(h,saved,gate.Rights,()=>action switch {
            "renew"=>Results.Ok(context),
            "cleanup"=>Results.Ok(new{saved.Id,saved.TenantId,saved.ActorId,saved.VesselId,saved.RenewalId,saved.DeviceId,saved.KeyId,saved.PolicyVersion,saved.PolicyDigest,saved.AuthorityDigest,saved.SourceVersion}),
            _=>Results.Ok(new{state=action=="key"?"KeyEnrolled":"Revoked"})
        },privateAccess:action is "renew" or "key",captureLease:action=="renew");
    }
    private static async Task<IResult> Replay(HttpContext h,PoseidonDbContext db,IDbContextTransaction tx,CertificateRenewalOfflineEnrollment e,string rights) {
        var bytes=await Body(h,131072);var capture=JsonSerializer.Deserialize<CertificateRenewalOfflineCapture>(bytes,Json);var now=Now(h);var ct=h.RequestAborted;
        using var parsed=JsonDocument.Parse(bytes);var capturedText=parsed.RootElement.TryGetProperty("capturedAt",out var at)&&at.ValueKind==JsonValueKind.String?at.GetString():null;
        if(capture==null || capture.CommandId==Guid.Empty || capture.ParentCommandId==capture.CommandId || capture.RenewalId!=e.RenewalId || capture.KeyId!=e.KeyId || capture.DeviceId!=e.DeviceId ||
            capture.PolicyVersion!=e.PolicyVersion || capture.PolicyDigest!=e.PolicyDigest || capture.SourceVersion!=e.SourceVersion || capture.Operation!=CertificateRenewalOfflinePolicy.Operation ||
            capturedText==null || !(capturedText.EndsWith('Z')||capturedText.EndsWith("+00:00",StringComparison.Ordinal)))
            return await Fail(h,400,"invalid_offline_command");
        var idempotency=h.Request.Headers["Idempotency-Key"];if(idempotency.Count!=1 || idempotency[0]!=capture.CommandId.ToString("D"))return await Fail(h,400,"invalid_idempotency_key");
        var digest=Convert.ToHexString(SHA256.HashData(bytes));
        if(h.Request.Headers["X-Offline-Hash"].Count!=1 || !string.Equals(h.Request.Headers["X-Offline-Hash"],digest,StringComparison.Ordinal))return await Fail(h,400,"offline_hash_mismatch");
        if(h.Request.Headers["X-Offline-Signature"].Count!=1 || !OfflineApplicationEndpoints.VerifyKey(e.PublicKey,h.Request.Headers["X-Offline-Signature"],digest))return await Fail(h,403,"offline_key_proof_denied");
        if(capture.ParentCommandId!=null && !Human(h,e))return await Fail(h,403,"human_authority_required");
        // Idempotent reconnect: the same key and digest returns the immutable first outcome (no duplicate revision).
        var old=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.CommandId==capture.CommandId,ct);
        if(old!=null) {
            if(old.Digest!=digest)return await Fail(h,409,"offline_command_reused");
            if(!await Fresh(h,e,rights,e.PublicKey))return await Fail(h,403,"permission_denied");
            return await Committed(h,e,rights,()=>Results.Content(old.OutcomeJson,"application/json"),human:capture.ParentCommandId!=null,audit:true);
        }
        if(e.ExpiresAt<=now || !OfflineWatchCapture.ValidCaptureTime(capture.CapturedAt,capture.OffsetMinutes,e.CreatedAt,now,e.ExpiresAt))return await Fail(h,409,"offline_recovery_required");
        if(Actor(h) is not {} writer || writer.Id!=e.ActorId || writer.TenantId!=e.TenantId)return await Fail(h,403,"permission_denied");
        if(await Head(db,writer,e.VesselId,e.RenewalId,true,ct) is not {} bound)return await Fail(h,404,"resource_unavailable");
        var (head,current)=bound;
        if(current.SourceVersion!=e.SourceVersion)return await Fail(h,409,"source_contract_changed");
        var server=CertificateRenewalEndpoints.Content(current);var before=Private(h,e,e.ProtectedBase).Base;CertificateRenewalContent local;
        if(capture.ParentCommandId==null) {
            if(capture.BaseVersion!=e.BaseVersion || capture.ExpectedServerVersion!=null || capture.Choice!=null || capture.ReasonCode!=null || capture.Content==null ||
                !CertificateRenewalOfflinePolicy.OfflineEditable(capture.Content,before))return await Fail(h,400,"invalid_offline_command");
            local=capture.Content;
        }else {
            var parent=await Outcomes(db,e).SingleOrDefaultAsync(o=>o.CommandId==capture.ParentCommandId && o.Result=="MaterialConflict",ct);
            if(parent==null || capture.Content!=null || capture.ExpectedServerVersion!=head.Version || capture.BaseVersion!=head.Version ||
                capture.Choice is not("keepServer" or "useCaptured") || capture.ReasonCode!=(capture.Choice=="keepServer"?"CURRENT_SERVER_RETAINED":"INPUT_CORRECTED"))return await Fail(h,409,"offline_conflict_changed");
            if(await Outcomes(db,e).AnyAsync(o=>o.ParentCommandId==parent.CommandId,ct))return await Fail(h,409,"offline_resolution_exists");
            var retained=Private(h,e,parent.ProtectedContext).Local!;
            // A captured value can only be applied when its links still match the current server links.
            if(capture.Choice=="useCaptured" && !CertificateRenewalOfflinePolicy.OfflineEditable(retained,server))return await Fail(h,409,"offline_conflict_changed");
            local=capture.Choice=="keepServer"?server:retained;
        }
        // Any concurrent head change (draft edit, transition) is a material conflict: no silent last-write-wins.
        var result=capture.ParentCommandId!=null && capture.Choice=="keepServer"?"ServerRetained":head.Version==capture.BaseVersion?"Committed":"MaterialConflict";
        uint serverVersion=head.Version;long revision=head.Revision;
        if(result=="Committed") {
            if(!CertificateRenewalWorkflow.Editable(current.State))return await Fail(h,409,"renewal_state_locked");
            var owners=CertificateRenewalEndpoints.Owners(h,writer,e.VesselId);
            if(owners.Length>256 || local.OwnerRoleId!=null && !owners.Contains(local.OwnerRoleId.Value))return await Fail(h,403,"owner_unavailable");
            head.Revision++;await db.SaveChangesAsync(ct);
            var json=JsonSerializer.Serialize(local,CertificateReader.Json);
            var row=new CertificateRenewalRevision{TenantId=writer.TenantId,VesselId=e.VesselId,RenewalId=head.Id,CertificateId=current.CertificateId,CertificateRevision=current.CertificateRevision,
                CertificateVersion=current.CertificateVersion,Revision=head.Revision,RecordVersion=head.Version,ContentJson=json,
                Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel=e.VesselId,head.Id,head.Revision,current.CertificateId,current.CertificateRevision,current.CertificateVersion,json},CertificateReader.Json)),
                SourceVersion=VesselCertificatePolicy.SourceVersion,NewCertificateId=local.NewCertificateId,NewCertificateRevision=local.NewCertificateRevision,NewCertificateVersion=local.NewCertificateVersion,
                FileId=local.NewDocument?.Id,FileRevision=local.NewDocument?.Revision,Checksum=local.NewDocument?.Checksum,State=current.State,ActorId=writer.Id,SavedAt=now};
            db.CertificateRenewalRevisions.Add(row);serverVersion=head.Version;revision=row.Revision;
        }
        var outcome=new CertificateRenewalOfflineResult(capture.CommandId,result,e.RenewalId,capture.BaseVersion,serverVersion,revision,capture.ParentCommandId,now);
        var outcomeJson=JsonSerializer.Serialize(outcome,Json);
        db.CertificateRenewalOfflineOutcomes.Add(new(){TenantId=e.TenantId,ActorId=e.ActorId,CommandId=capture.CommandId,EnrollmentId=e.Id,VesselId=e.VesselId,RenewalId=e.RenewalId,
            ParentCommandId=capture.ParentCommandId,Digest=digest,Result=result,BaseVersion=capture.BaseVersion,ServerVersion=serverVersion,Revision=revision,OutcomeJson=outcomeJson,
            CapturedAt=capture.CapturedAt.ToUniversalTime(),ReceivedAt=now,ProtectedContext=Protect(h,e,new(before,local,server,capture.ReasonCode))});
        await db.SaveChangesAsync(ct);
        if(!await Fresh(h,e,rights,e.PublicKey,captureLease:true) || capture.ParentCommandId!=null && !Human(h,e))return await Fail(h,403,"permission_denied");
        await tx.CommitAsync(ct);
        return await Committed(h,e,rights,()=>Results.Content(outcomeJson,"application/json"),human:capture.ParentCommandId!=null,audit:true);
    }
    private static async Task<IResult> History(Guid renewal,Guid enrollment,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,renewal,true);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,renewal).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==enrollment,h.RequestAborted);
        if(e==null || !Matches(h,e,Policy(h,gate.Vessel),Now(h)))return await Fail(h,403,"offline_policy_denied");
        Query(h,true,out _,out var offset);var rows=await Outcomes(db,e).AsNoTracking().OrderByDescending(o=>o.ReceivedAt).ThenBy(o=>o.CommandId).Skip(offset).Take(26)
            .Select(o=>o.OutcomeJson).ToArrayAsync(h.RequestAborted);
        if(!await Fresh(h,e,gate.Rights,e.PublicKey))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,Read,"Success",resourceId:renewal);
        if(!await Fresh(h,e,gate.Rights,e.PublicKey))return await Fail(h,403,"permission_denied");
        return Results.Ok(new{items=rows.Take(25).Select(s=>JsonSerializer.Deserialize<CertificateRenewalOfflineResult>(s,Json)),offset,hasMore=rows.Length>25});
    }
    private static async Task<IResult> Conflict(Guid renewal,Guid enrollment,Guid command,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var gate=await Gate(h,db,renewal);if(gate.Actor==null)return await Fail(h,404,"resource_unavailable");
        var e=await Enrollments(db,gate.Actor.Id,gate.Vessel,renewal).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==enrollment,h.RequestAborted);
        if(e==null || !Matches(h,e,Policy(h,gate.Vessel),Now(h)))return await Fail(h,403,"offline_policy_denied");
        var outcome=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.CommandId==command && o.Result=="MaterialConflict",h.RequestAborted);
        if(outcome==null)return await Fail(h,404,"resource_unavailable");
        if(await Head(db,gate.Actor,gate.Vessel,renewal,false,h.RequestAborted) is not {} bound)return await Fail(h,404,"resource_unavailable");
        if(bound.Current.SourceVersion!=e.SourceVersion)return await Fail(h,409,"source_contract_changed");
        var server=CertificateRenewalEndpoints.Content(bound.Current);var retained=Private(h,e,outcome.ProtectedContext);
        var resolved=await Outcomes(db,e).AsNoTracking().SingleOrDefaultAsync(o=>o.ParentCommandId==command,h.RequestAborted);
        if(!await Fresh(h,e,gate.Rights,e.PublicKey))return await Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,Read,"Success",resourceId:renewal);
        if(!await Fresh(h,e,gate.Rights,e.PublicKey))return await Fail(h,403,"permission_denied");
        // Applying a captured value is only possible while the case is still an editable draft and its links match.
        string[] choices=resolved!=null || !Human(h,e)?[]:CertificateRenewalWorkflow.Editable(bound.Current.State) && CertificateRenewalOfflinePolicy.OfflineEditable(retained.Local!,server)
            ?["keepServer","useCaptured"]:["keepServer"];
        return Results.Ok(new CertificateRenewalOfflineConflictDto(command,enrollment,renewal,bound.Head.Version,bound.Current.State,retained.Base,retained.Local!,server,choices,
            resolved==null?null:JsonSerializer.Deserialize<CertificateRenewalOfflineResult>(resolved.OutcomeJson,Json)));
    }
}
