using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRenewalCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,
    [property:JsonRequired] Guid CertificateId,[property:JsonRequired] long CertificateRevision,[property:JsonRequired] uint CertificateVersion,
    [property:JsonRequired] CertificateRenewalContent Content,[property:JsonRequired] string ReasonCode);
public sealed record CertificateRenewalDto(Guid Id,Guid VesselId,Guid CertificateId,long CertificateRevision,uint CertificateVersion,long Revision,uint Version,
    CertificateRenewalContent Content,string SourceVersion,string Hash,DateTimeOffset SavedAt,string State,string Qualification,bool CanEdit,bool CanComplete,
    string[] Blockers,bool CurrentRevision,string Provenance="UserDeclaredUnverified");
public sealed record RenewalOwnerDto(Guid RoleId);
public sealed record RenewalCapabilitiesDto(Guid VesselId,bool CanWrite,bool CanComplete,RenewalOwnerDto[] OwnerOptions,string SourceVersion);
public sealed record RenewalPageDto(CertificateRenewalDto[] Items,int Offset,int Limit,bool HasMore);

// Renewal plans reference exact owning revisions. A draft link never supersedes
// the old certificate or stands in for completed survey/evidence/verification.
public static class CertificateRenewalEndpoints
{
    private static readonly JsonSerializerOptions Input=new(CertificateReader.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    internal static bool Write(TrustedActor? a,Guid vessel)=>CertificateReader.Write(a,vessel) && a!.Permissions.Contains("certificate.renewal.write");
    internal static Guid[] Owners(HttpContext h,TrustedActor a,Guid vessel)=>h.RequestServices.GetRequiredService<FederationSettings>().Memberships
        .Where(m=>m.TenantId==a.TenantId && m.ActorKind=="Human" && (m.Permissions??[]).Contains("certificate.renewal.owner") &&
            (m.TenantWideManage || m.VesselGrants?.Any(v=>v.VesselId==vessel && v.CanEdit)==true))
        .SelectMany(m=>(ActorResolver.TrustedRoleContext(m.RoleReferences)??"").Split(',',StringSplitOptions.RemoveEmptyEntries)).Select(Guid.Parse).Distinct().Order().Take(257).ToArray();
    private static string OwnerHash(HttpContext h,TrustedActor a,Guid vessel)=>RegisterStorage.Hash(JsonSerializer.Serialize(Owners(h,a,vessel)));
    public static void MapCertificateRenewals(this IEndpointRouteBuilder app) {
        var g=app.MapGroup("/api/v1/renewal-workflow").RequireAuthorization("DirectoryRead");
        g.MapGet("",(HttpContext h,PoseidonDbContext db)=>Read(null,false,false,h,db)).WithName("CertificateRenewals").Produces<RenewalPageDto>();
        g.MapGet("/capabilities",(HttpContext h,PoseidonDbContext db)=>Read(null,false,true,h,db)).WithName("CertificateRenewalCapabilities").Produces<RenewalCapabilitiesDto>();
        g.MapGet("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(id,false,false,h,db)).WithName("CertificateRenewalRead").Produces<CertificateRenewalDto>();
        g.MapGet("/{id:guid}/history",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(id,true,false,h,db)).WithName("CertificateRenewalHistory").Produces<RenewalPageDto>();
        g.MapPost("",(HttpContext h,PoseidonDbContext db)=>Save(null,h,db)).WithName("CertificateRenewalCreate").Accepts<CertificateRenewalCommand>("application/json");
        g.MapPut("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Save(id,h,db)).WithName("CertificateRenewalUpdate").Accepts<CertificateRenewalCommand>("application/json");
    }
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        vessel=Guid.Empty;
        return CertificateEndpoints.Offset(h,paging?new HashSet<string>{"vesselId","offset"}:new HashSet<string>{"vesselId"},out offset) &&
            Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) && vessel!=Guid.Empty;
    }
    internal static IQueryable<CertificateRenewalRevision> Visible(PoseidonDbContext db,TrustedActor actor,Guid vessel,bool currentOnly=true) {
        var certificates=CertificateReader.Visible(db,actor,vessel,false);var files=actor.Permissions.Contains("file.read");
        var rows=db.CertificateRenewalRevisions.AsNoTracking().Where(r=>r.VesselId==vessel &&
            certificates.Any(c=>c.CertificateId==r.CertificateId && c.Revision==r.CertificateRevision && c.RecordVersion==r.CertificateVersion) &&
            (r.NewCertificateId==null || certificates.Any(c=>c.CertificateId==r.NewCertificateId && c.Revision==r.NewCertificateRevision && c.RecordVersion==r.NewCertificateVersion)) &&
            (r.FileId==null || files && db.StoredFiles.Any(f=>f.Id==r.FileId && f.VesselId==vessel && f.Revision==r.FileRevision && f.Checksum==r.Checksum &&
                db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==vessel && s.PersonId==null && s.DocumentId==null && s.State=="CleanTransport"))));
        var heads=db.CertificateRenewals.Where(r=>r.VesselId==vessel);
        return currentOnly?rows.Where(r=>heads.Any(head=>head.Id==r.RenewalId && head.Revision==r.Revision)):
            rows.Where(r=>heads.Any(head=>head.Id==r.RenewalId && rows.Any(current=>current.RenewalId==head.Id && current.Revision==head.Revision)));
    }
    private static CertificateRenewalDto Dto(CertificateRenewalRevision row,TrustedActor actor,bool current,Guid[] owners) {
        var content=JsonSerializer.Deserialize<CertificateRenewalContent>(row.ContentJson,CertificateReader.Json)??throw new InvalidDataException("renewal_content_unavailable");
        var blockers=new List<string>{"SOURCE_QUALIFICATION_REQUIRED","HUMAN_VERIFICATION_REQUIRED","COMPLETION_PREREQUISITES_REQUIRED"};
        if(content.OwnerRoleId==null || !owners.Contains(content.OwnerRoleId.Value))blockers.Add("RENEWAL_OWNER_UNAVAILABLE");
        if(content.NewCertificateId==null)blockers.Add("REPLACEMENT_CERTIFICATE_NOT_PINNED");
        if(content.NewDocument==null)blockers.Add("RENEWAL_EVIDENCE_NOT_LINKED");
        if(content.DueDate==null)blockers.Add("RENEWAL_DUE_NOT_DECLARED");
        if(content.RequiredSurvey==null || content.RequiredDocuments==null || content.RequiredActions==null)blockers.Add("RENEWAL_REQUIREMENTS_UNKNOWN");
        return new(row.RenewalId,row.VesselId,row.CertificateId,row.CertificateRevision,checked((uint)row.CertificateVersion),row.Revision,checked((uint)row.RecordVersion),content,
            row.SourceVersion,row.Hash,row.SavedAt,"DRAFT","REVIEW_REQUIRED",current && Write(actor,row.VesselId),false,blockers.ToArray(),current,row.Provenance);
    }
    private sealed class ParameterSwap(ParameterExpression from,ParameterExpression to):ExpressionVisitor { protected override Expression VisitParameter(ParameterExpression p)=>p==from?to:base.VisitParameter(p); }
    private static Expression<Func<T,bool>> Or<T>(Expression<Func<T,bool>> left,Expression<Func<T,bool>> right)=>Expression.Lambda<Func<T,bool>>(
        Expression.OrElse(left.Body,new ParameterSwap(right.Parameters[0],left.Parameters[0]).Visit(right.Body)!),left.Parameters);
    private static async Task<bool> Current(HttpContext h,Guid vessel,string rights,CertificateRenewalDto[] records,string ownerHash,bool write=false,
        CertificateDto[]? sources=null,CertificateFilePin[]? files=null) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var actor=resolver.Resolve(h.User);
        if(actor==null || !CertificateReader.Read(actor) || CertificateReader.Rights(actor)!=rights || write && !Write(actor,vessel))return false;
        PoseidonDbContext db;try{db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=actor.TenantId)return false;
        var heads=db.CertificateRenewals.Where(r=>r.VesselId==vessel);Expression<Func<CertificateRenewalRevision,bool>> pairs=r=>false;
        foreach(var dto in records)pairs=Or(pairs,r=>r.RenewalId==dto.Id && r.Revision==dto.Revision && r.RecordVersion==dto.Version && r.Hash==dto.Hash &&
            (!dto.CurrentRevision || heads.Any(head=>head.Id==r.RenewalId && head.Revision==r.Revision)));
        var returned=Visible(db,actor,vessel,false).Where(pairs);var requested=sources??[];var sourceRows=CertificateReader.ReturnedVisible(db,actor,vessel,requested);
        var pins=files??[];var fileIds=pins.Select(f=>f.Id).Distinct().ToArray();Expression<Func<StoredFile,bool>> filePairs=f=>false;
        foreach(var pin in pins)filePairs=Or(filePairs,f=>f.Id==pin.Id && f.Revision==pin.Revision && f.Checksum==pin.Checksum);
        var allowedFiles=db.StoredFiles.Where(filePairs).Where(f=>f.VesselId==vessel && db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==vessel && s.PersonId==null && s.DocumentId==null && s.State=="CleanTransport"));
        var fileAccess=actor.Permissions.Contains("file.read");
        var visible=await actor.Scope(db.Vessels).Where(v=>v.Id==vessel).Select(v=>returned.Count()==records.Length && sourceRows.Count()==requested.Length &&
            (fileIds.Length==0 || fileAccess && allowedFiles.Select(f=>f.Id).Distinct().Count()==fileIds.Length)).SingleOrDefaultAsync(h.RequestAborted);
        var live=resolver.Resolve(h.User);
        return visible && live!=null && CertificateReader.Read(live) && CertificateReader.Rights(live)==rights && (!write || Write(live,vessel)) && OwnerHash(h,live,vessel)==ownerHash;
    }
    private static async Task<IResult> Read(Guid? id,bool history,bool caps,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";if(!Query(h,!caps && (history || id==null),out var vessel,out var offset))return await CertificateEndpoints.Fail(h,400,"invalid_query");
        var actor=CertificateEndpoints.Actor(h);if(actor==null || !CertificateReader.Read(actor))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        if(!await actor.Scope(db.Vessels).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return await CertificateEndpoints.Fail(h,404,"resource_not_found");
        var rights=CertificateReader.Rights(actor);var ownerHash=OwnerHash(h,actor,vessel);var owners=Owners(h,actor,vessel);if(owners.Length>256)owners=[];
        CertificateRenewalDto[] returned=[];object result;
        if(caps)result=new RenewalCapabilitiesDto(vessel,Write(actor,vessel),false,owners.Select(r=>new RenewalOwnerDto(r)).ToArray(),VesselCertificatePolicy.SourceVersion);
        else {
            var head=id==null?null:await db.CertificateRenewals.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==id && r.VesselId==vessel,h.RequestAborted);
            if(id!=null && head==null)return await CertificateEndpoints.Fail(h,404,"resource_not_found");
            var query=Visible(db,actor,vessel,!history);if(id!=null)query=query.Where(r=>r.RenewalId==id);
            var rows=await query.OrderBy(r=>r.RenewalId).ThenByDescending(r=>r.Revision).Skip(offset).Take(id!=null && !history?1:26).ToArrayAsync(h.RequestAborted);
            if(id!=null && rows.Length==0)return await CertificateEndpoints.Fail(h,404,"resource_not_found");
            returned=rows.Take(25).Select(r=>Dto(r,actor,head==null || head.Revision==r.Revision,owners)).ToArray();
            result=id!=null && !history?returned[0]:new RenewalPageDto(returned,offset,25,rows.Length>25);
        }
        if(!await Current(h,vessel,rights,returned,ownerHash))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateRead","Success",resourceId:id??vessel);
        if(!await Current(h,vessel,rights,returned,ownerHash))return await CertificateEndpoints.Fail(h,403,"permission_denied");return Results.Ok(result);
    }
    private static async Task<IResult> Save(Guid? routeId,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var actor=CertificateEndpoints.Actor(h);
        if(actor==null || !Query(h,false,out var vessel,out _) || !Write(actor,vessel))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await CertificateEndpoints.Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;if(key==null || !Guid.TryParseExact(key,"D",out _))return await CertificateEndpoints.Fail(h,400,"invalid_idempotency_key");
        CertificateRenewalCommand command;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var bytes=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,bytes,65536,h.RequestAborted);
            using var document=JsonDocument.Parse(bytes.ToArray(),new(){MaxDepth=8});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);command=document.RootElement.Deserialize<CertificateRenewalCommand>(Input)??throw new JsonException();
            if(command.Id==Guid.Empty || command.CertificateId==Guid.Empty || command.CertificateRevision<=0 || command.CertificateVersion==0 || routeId!=null && routeId!=command.Id ||
                command.ReasonCode is not("INITIAL_AUTHORING" or "AUTHORING_CORRECTION") || !CertificateRenewalPolicy.Valid(command.Content) || command.Content.NewCertificateId==command.CertificateId)throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await CertificateEndpoints.Fail(h,400,"validation_failed");}
        var rights=CertificateReader.Rights(actor);var ownerHash=OwnerHash(h,actor,vessel);var owners=Owners(h,actor,vessel);
        if(owners.Length>256 || command.Content.OwnerRoleId!=null && !owners.Contains(command.Content.OwnerRoleId.Value))return await CertificateEndpoints.Fail(h,403,"owner_unavailable");
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,routeId,command},CertificateReader.Json));
        async Task<IResult> Outcome(CertificateRenewalDto record,string json) {
            try {
                await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateCommand","Success",resourceId:record.Id);
                if(!await Current(h,vessel,rights,[record],ownerHash,true))return await CertificateEndpoints.Fail(h,503,"command_outcome_withheld");
                return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
        }
        async Task<IResult?> Replay() {
            if(!await Current(h,vessel,rights,[],ownerHash,true))return await CertificateEndpoints.Fail(h,403,"permission_denied");
            var receipt=await db.CertificateCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,h.RequestAborted);if(receipt==null)return null;
            if(receipt.Action!="RenewalSave" || receipt.VesselId!=vessel || receipt.ResourceId!=command.Id || receipt.Fingerprint!=fingerprint)return await CertificateEndpoints.Fail(h,409,"idempotency_key_reused");
            var record=JsonSerializer.Deserialize<CertificateRenewalDto>(receipt.OutcomeJson,CertificateReader.Json)??throw new InvalidDataException("renewal_receipt_unavailable");
            return await Outcome(record,receipt.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;
        IDbContextTransaction? tx=null;CertificateRenewalDto? saved=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            var source=await CertificateReader.Visible(db,actor,vessel).SingleOrDefaultAsync(r=>r.CertificateId==command.CertificateId && r.Revision==command.CertificateRevision && r.RecordVersion==command.CertificateVersion,h.RequestAborted);
            if(source==null)return await CertificateEndpoints.Fail(h,409,"source_revision_changed");
            VesselCertificateRevision? replacement=null;
            if(command.Content.NewCertificateId is {} replacementId) {
                replacement=await CertificateReader.Visible(db,actor,vessel).SingleOrDefaultAsync(r=>r.CertificateId==replacementId && r.Revision==command.Content.NewCertificateRevision && r.RecordVersion==command.Content.NewCertificateVersion,h.RequestAborted);
                if(replacement==null)return await CertificateEndpoints.Fail(h,409,"linked_version_conflict");
                if(command.Content.NewDocument!=null && (replacement.FileId!=command.Content.NewDocument.Id || replacement.FileRevision!=command.Content.NewDocument.Revision || replacement.Checksum!=command.Content.NewDocument.Checksum))return await CertificateEndpoints.Fail(h,409,"linked_version_conflict");
            }
            if(!await CertificateReader.FileAllowed(db,actor,vessel,command.Content.NewDocument,h.RequestAborted))return await CertificateEndpoints.Fail(h,403,"linked_resource_unavailable");
            var head=await db.CertificateRenewals.SingleOrDefaultAsync(r=>r.Id==command.Id,h.RequestAborted);CertificateRenewalDto? previous=null;
            if(head!=null && (head.VesselId!=vessel || head.CertificateId!=command.CertificateId))return await CertificateEndpoints.Fail(h,404,"resource_not_found");
            if(routeId==null && (head!=null || command.ExpectedVersion!=0) || head!=null && head.Version!=command.ExpectedVersion)return await CertificateEndpoints.Fail(h,409,"version_conflict");
            if(routeId!=null && head==null)return await CertificateEndpoints.Fail(h,404,"resource_not_found");
            if(head!=null) {
                var old=await Visible(db,actor,vessel).SingleOrDefaultAsync(r=>r.RenewalId==head.Id,h.RequestAborted);if(old==null)return await CertificateEndpoints.Fail(h,404,"resource_not_found");
                previous=Dto(old,actor,true,owners);head.Revision++;
            }else{head=new(){TenantId=actor.TenantId,VesselId=vessel,Id=command.Id,CertificateId=command.CertificateId};db.CertificateRenewals.Add(head);}
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateCommand","Attempted",resourceId:command.Id);
            await db.SaveChangesAsync(h.RequestAborted);var json=JsonSerializer.Serialize(command.Content,CertificateReader.Json);var now=CertificateEndpoints.Now(h);
            var row=new CertificateRenewalRevision{TenantId=actor.TenantId,VesselId=vessel,RenewalId=head.Id,CertificateId=source.CertificateId,CertificateRevision=source.Revision,CertificateVersion=source.RecordVersion,
                Revision=head.Revision,RecordVersion=head.Version,ContentJson=json,Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,head.Id,head.Revision,command.CertificateId,command.CertificateRevision,command.CertificateVersion,json},CertificateReader.Json)),
                SourceVersion=VesselCertificatePolicy.SourceVersion,NewCertificateId=command.Content.NewCertificateId,NewCertificateRevision=command.Content.NewCertificateRevision,NewCertificateVersion=command.Content.NewCertificateVersion,
                FileId=command.Content.NewDocument?.Id,FileRevision=command.Content.NewDocument?.Revision,Checksum=command.Content.NewDocument?.Checksum,ActorId=actor.Id,SavedAt=now};
            db.CertificateRenewalRevisions.Add(row);saved=Dto(row,actor,false,owners);outcome=JsonSerializer.Serialize(saved,CertificateReader.Json);
            db.CertificateCommandReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,ResourceId=head.Id,CertificateId=source.CertificateId,RenewalId=head.Id,Action="RenewalSave",Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});
            await db.SaveChangesAsync(h.RequestAborted);
            var refs=new List<CertificateDto>{CertificateReader.Dto(source,actor,DateOnly.FromDateTime(now.UtcDateTime))};if(replacement!=null)refs.Add(CertificateReader.Dto(replacement,actor,DateOnly.FromDateTime(now.UtcDateTime)));
            if(!await Current(h,vessel,rights,previous==null?[]:[previous],ownerHash,true,refs.ToArray(),command.Content.NewDocument==null?[]:[command.Content.NewDocument]))return await CertificateEndpoints.Fail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await CertificateEndpoints.Fail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";return await Replay()??await CertificateEndpoints.Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(saved!,outcome!);
    }
}
