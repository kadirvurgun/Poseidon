using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Server-provisioned exact-record appointment. Never request-bound or inferred
// from an administrator, IdP display role, crew name, AI, or a catalog proposal.
public sealed record DailyReviewAuthority(Guid Id,Guid VesselId,Guid RecordId,string Kind,Guid RoleId,string Duty,
    string SourceVersion,string AppointmentSource,DateTimeOffset ValidFrom,DateTimeOffset ValidUntil);
public sealed record DailyReviewerDto(Guid Id,Guid RoleId,string Duty,string[] Decisions);
public sealed record DailyReviewCapabilitiesDto(Guid RecordId,Guid VesselId,string Kind,long RecordRevision,uint RecordVersion,
    uint ReviewVersion,bool CanRecord,DailyReviewerDto[] Authorities,string[] Blockers,string SourceVersion=DailyOperationsPolicy.SourceVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DailyReviewCommand([property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] uint ExpectedReviewVersion,
    [property:JsonRequired] long ReviewedRevision,[property:JsonRequired] Guid AuthorityId,[property:JsonRequired] string Decision,
    [property:JsonRequired] string ReasonCode,[property:JsonRequired] string Rationale);
public sealed record DailyReviewDto(Guid Id,Guid RecordId,Guid VesselId,string Kind,long RecordRevision,long Sequence,uint Version,
    Guid ActorId,Guid RoleId,Guid AuthorityId,string Duty,string Decision,string ReasonCode,string Rationale,string SourceVersion,
    string AuthorityHash,string RecordHash,DateTimeOffset SavedAt,string State="RECORDED_UNASSESSED");

public static class DailyReviewEndpoints
{
    private static readonly (string Kind,string Route)[] Families=[("Noon","noon-reports"),("Deck","deck-daily-logs"),("Engine","engine-daily-logs")];
    public static void MapDailyReviews(this IEndpointRouteBuilder app) {
        foreach(var family in Families) {
            var kind=family.Kind;var group=app.MapGroup("/api/v1/"+family.Route).RequireAuthorization("DirectoryRead");
            group.MapGet("/{id:guid}/review-capabilities",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(kind,id,true,h,db)).WithName(kind+"DailyReviewCapabilities").Produces<DailyReviewCapabilitiesDto>();
            group.MapGet("/{id:guid}/reviews",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(kind,id,false,h,db)).WithName(kind+"DailyReviews");
            group.MapPost("/{id:guid}/reviews",(Guid id,HttpContext h,PoseidonDbContext db)=>Save(kind,id,h,db)).WithName(kind+"DailyReviewRecord").Accepts<DailyReviewCommand>("application/json").Produces<DailyReviewDto>();
        }
    }
    private static TrustedActor? Actor(HttpContext h)=>h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static DailyReviewAuthority[] Authorities(HttpContext h,TrustedActor? actor,Guid vessel,string kind,Guid? record=null) {
        if(actor==null || !DailyOperationsEndpoints.CanWrite(actor,kind,vessel) || !actor.Permissions.Contains("daily."+kind.ToLowerInvariant()+".review"))return [];
        var members=h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m=>m.ActorId==actor.Id && m.TenantId==actor.TenantId).ToArray();
        if(members.Length!=1 || members[0].ActorKind!="Human")return [];
        var now=h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();var grants=members[0].DailyReviewAuthorities??[];
        if(grants.Length>256)return [];
        return grants.Where(g=>g!=null && g.Id!=Guid.Empty && grants.Count(other=>other!=null && other.Id==g.Id)==1 && g.RecordId!=Guid.Empty &&
            g.VesselId==vessel && g.Kind==kind && (record==null || g.RecordId==record) && DailyReviewPolicy.Duty(kind,g.Duty) &&
            g.RoleId!=Guid.Empty && actor.RoleContext!.Split(',').Contains(g.RoleId.ToString("D")) &&
            g.SourceVersion==DailyOperationsPolicy.SourceVersion && g.AppointmentSource is {Length:>0 and <=200} && !string.IsNullOrWhiteSpace(g.AppointmentSource) && !g.AppointmentSource.Any(char.IsControl) &&
            g.ValidUntil>g.ValidFrom && g.ValidFrom<=now && now<g.ValidUntil).ToArray();
    }
    private static string AuthoritySnapshot(DailyReviewAuthority grant,TrustedActor actor)=>JsonSerializer.Serialize(new{grant,actor.Id,actor.TenantId,actorKind="Human"},DailyOperationsEndpoints.Json);
    private static string AuthorityHash(DailyReviewAuthority grant,TrustedActor actor)=>RegisterStorage.Hash(AuthoritySnapshot(grant,actor));
    internal static string CapabilityHash(HttpContext h,TrustedActor actor,Guid vessel,string kind)=>RegisterStorage.Hash(JsonSerializer.Serialize(
        Authorities(h,actor,vessel,kind).OrderBy(g=>g.Id),DailyOperationsEndpoints.Json));
    internal static async Task<DailyRecordDto[]> Decorate(PoseidonDbContext db,Guid vessel,string kind,DailyRecordDto[] records,CancellationToken ct) {
        if(records.Length==0)return records;
        var ids=records.Select(r=>r.Id).Distinct().ToArray();var revisions=records.Select(r=>r.Revision).Distinct().ToArray();
        var reviews=await db.DailyReviewEvents.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind==kind && ids.Contains(r.RecordId) && revisions.Contains(r.RecordRevision))
            .GroupBy(r=>new{r.RecordId,r.RecordRevision}).Select(g=>g.OrderByDescending(r=>r.Sequence).First()).ToArrayAsync(ct);
        return records.Select(r=> {
            var review=reviews.SingleOrDefault(e=>e.RecordId==r.Id && e.RecordRevision==r.Revision);
            return review==null?r:r with{ReviewState="RECORDED_UNASSESSED",ReviewedRevision=review.RecordRevision,ReviewedBy=review.ActorId,ReviewedAt=review.SavedAt,
                Blockers=r.Blockers.Where(b=>b!="HUMAN_REVIEW_NOT_RECORDED").Append("REVIEW_RECORDING_IS_NOT_VERIFICATION").ToArray()};
        }).ToArray();
    }
    private static async Task<bool> Current(HttpContext h,string kind,Guid vessel,string rights,Guid id,string authorityHash,Guid authority) {
        if(!await ShipCurrent(h,kind,vessel,rights,true))return false;
        var actor=Actor(h);var current=Authorities(h,actor,vessel,kind,id).SingleOrDefault(g=>g.Id==authority);
        return actor!=null && current!=null && AuthorityHash(current,actor)==authorityHash;
    }
    private static async Task<bool> ShipCurrent(HttpContext h,string kind,Guid vessel,string rights,bool write=false) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var actor=resolver.Resolve(h.User);
        if(actor==null || !DailyOperationsEndpoints.CanRead(actor,kind) || DailyOperationsEndpoints.Rights(actor)!=rights || write && !DailyOperationsEndpoints.CanWrite(actor,kind,vessel))return false;
        var db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();if(db.TenantId!=actor.TenantId)return false;
        var ship=await actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel && v.Kind==VesselKind.Ship,h.RequestAborted);
        var live=resolver.Resolve(h.User);
        return ship && live!=null && DailyOperationsEndpoints.CanRead(live,kind) && DailyOperationsEndpoints.Rights(live)==rights && (!write || DailyOperationsEndpoints.CanWrite(live,kind,vessel));
    }
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        offset=0;vessel=Guid.Empty;
        return h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vesselId" || paging && q.Key=="offset")) &&
            Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static Task Audit(HttpContext h,string result,Guid? id=null)=>h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,
        h.Request.Method=="GET"?"DailyReviewRead":"DailyReviewCommand",result,resourceId:id);
    private static async Task<IResult> Fail(HttpContext h,int status,string code) {await Audit(h,code);return Results.Json(new{code},statusCode:status);}
    private static IQueryable<DailyOperation> Records(PoseidonDbContext db,TrustedActor actor,Guid vessel,string kind,Guid id) {
        var vessels=actor.Scope(db.Vessels).Where(v=>v.Id==vessel && v.Kind==VesselKind.Ship);
        return db.DailyOperations.Where(r=>r.VesselId==vessel && r.Kind==kind && r.Id==id && vessels.Any());
    }
    private static DailyReviewDto Dto(DailyReviewEvent row)=>new(row.Id,row.RecordId,row.VesselId,row.Kind,row.RecordRevision,row.Sequence,checked((uint)row.ReviewVersion),
        row.ActorId,row.RoleId,row.AuthorityId,row.Duty,row.Decision,row.ReasonCode,row.Rationale,row.SourceVersion,row.AuthorityHash,row.RecordHash,row.SavedAt);
    private static string GrantsHash(HttpContext h,TrustedActor actor,Guid vessel,string kind,Guid id)=>RegisterStorage.Hash(JsonSerializer.Serialize(
        Authorities(h,actor,vessel,kind,id).OrderBy(g=>g.Id),DailyOperationsEndpoints.Json));
    private static async Task<IResult> Read(string kind,Guid id,bool capabilities,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";
        var actor=Actor(h);if(actor==null || !DailyOperationsEndpoints.CanRead(actor,kind))return await Fail(h,403,"permission_denied");
        if(!Query(h,!capabilities,out var vessel,out var offset))return await Fail(h,400,"invalid_query");
        var rights=DailyOperationsEndpoints.Rights(actor!);var grantHash=GrantsHash(h,actor!,vessel,kind,id);
        var row=await Records(db,actor!,vessel,kind,id).AsNoTracking().SingleOrDefaultAsync(h.RequestAborted);
        if(row==null)return await Fail(h,404,"resource_not_found");
        object result;
        if(capabilities) {
            var head=await db.DailyReviewHeads.AsNoTracking().SingleOrDefaultAsync(r=>r.RecordId==id && r.VesselId==vessel && r.Kind==kind,h.RequestAborted);
            var authorities=Authorities(h,actor,vessel,kind,id).Select(g=>new DailyReviewerDto(g.Id,g.RoleId,g.Duty,DailyReviewPolicy.Decisions(kind))).ToArray();
            result=new DailyReviewCapabilitiesDto(id,vessel,kind,row.Revision,row.Version,head?.Version??0,authorities.Length>0,authorities,
                authorities.Length==0?["HUMAN_REVIEW_AUTHORITY_UNAVAILABLE"]:[]);
        } else {
            var rows=await db.DailyReviewEvents.AsNoTracking().Where(r=>r.RecordId==id && r.VesselId==vessel && r.Kind==kind)
                .OrderByDescending(r=>r.Sequence).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
            result=new{items=rows.Take(25).Select(Dto).ToArray(),offset,limit=25,hasMore=rows.Length>25};
        }
        async Task<bool> ReadCurrent()=>await ShipCurrent(h,kind,vessel,rights) &&
            (!capabilities || Actor(h) is {} live && GrantsHash(h,live,vessel,kind,id)==grantHash);
        if(!await ReadCurrent())return await Fail(h,403,"permission_denied");
        await Audit(h,"Success",id);
        if(!await ReadCurrent())return await Fail(h,403,"permission_denied");
        return Results.Ok(result);
    }
    private static async Task<IResult> Save(string kind,Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;var actor=Actor(h);
        if(actor==null || !DailyOperationsEndpoints.CanRead(actor,kind) || !actor.HasWriteAttribution || !actor.Permissions.Contains("daily."+kind.ToLowerInvariant()+".review"))return await Fail(h,403,"human_authority_required");
        if(!Query(h,false,out var vessel,out _))return await Fail(h,400,"invalid_query");
        if(!DailyOperationsEndpoints.CanWrite(actor,kind,vessel))return await Fail(h,403,"human_authority_required");
        var rights=DailyOperationsEndpoints.Rights(actor);
        try {await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}
        catch(AntiforgeryValidationException){return await Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;
        if(key==null || key.Length is <16 or >80 || key.Any(c=>!char.IsAsciiLetterOrDigit(c) && c!='-'))return await Fail(h,400,"invalid_idempotency_key");
        DailyReviewCommand command;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();
            using var buffer=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,16384,ct);
            using var document=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=4});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command=document.RootElement.Deserialize<DailyReviewCommand>(DailyOperationsEndpoints.Json)??throw new JsonException();
            if(command.AuthorityId==Guid.Empty || command.ReviewedRevision<=0 || !DailyReviewPolicy.Valid(kind,command.Decision,command.ReasonCode,command.Rationale))throw new JsonException();
        } catch(Exception e)when(e is JsonException or InvalidDataException){return await Fail(h,400,"invalid_review_request");}
        var authority=Authorities(h,actor,vessel,kind,id).SingleOrDefault(g=>g.Id==command.AuthorityId);
        if(authority==null)return await Fail(h,403,"human_authority_required");
        var authorityHash=AuthorityHash(authority,actor);
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{kind,vessel,id,command,authorityHash},DailyOperationsEndpoints.Json));
        async Task<IResult?> Replay() {
            if(!await Current(h,kind,vessel,rights,id,authorityHash,authority.Id))return await Fail(h,403,"human_authority_required");
            var receipt=await db.DailyReviewReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,ct);
            if(receipt==null)return null;
            if(receipt.Fingerprint!=fingerprint || receipt.RecordId!=id || receipt.VesselId!=vessel || receipt.Kind!=kind)return await Fail(h,409,"idempotency_key_reused");
            await Audit(h,"Success",id);
            if(!await Current(h,kind,vessel,rights,id,authorityHash,authority.Id))return await Fail(h,503,"command_outcome_withheld");
            return Results.Content(receipt.OutcomeJson,"application/json");
        }
        var replay=await Replay();if(replay!=null)return replay;
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var row=await Records(db,actor,vessel,kind,id).SingleOrDefaultAsync(ct);
            if(row==null)return await Fail(h,404,"resource_not_found");
            if(row.Version!=command.ExpectedVersion || row.Revision!=command.ReviewedRevision)return await Fail(h,409,"version_conflict");
            if(row.SourceVersion!=DailyOperationsPolicy.SourceVersion)return await Fail(h,409,"source_contract_changed");
            var head=await db.DailyReviewHeads.SingleOrDefaultAsync(r=>r.RecordId==id && r.VesselId==vessel && r.Kind==kind,ct);
            if((head?.Version??0)!=command.ExpectedReviewVersion)return await Fail(h,409,"version_conflict");
            if(head==null){head=new(){TenantId=actor.TenantId,VesselId=vessel,RecordId=id,Kind=kind};db.DailyReviewHeads.Add(head);}else head.Sequence++;
            await db.SaveChangesAsync(ct);
            var entry=new DailyReviewEvent{TenantId=actor.TenantId,VesselId=vessel,RecordId=id,Kind=kind,Id=Guid.NewGuid(),RecordRevision=row.Revision,RecordVersion=row.Version,
                Sequence=head.Sequence,ReviewVersion=head.Version,ActorId=actor.Id,RoleId=authority.RoleId,AuthorityId=authority.Id,Duty=authority.Duty,Decision=command.Decision,
                ReasonCode=command.ReasonCode,Rationale=command.Rationale,SourceVersion=row.SourceVersion,AuthorityHash=authorityHash,AuthorityJson=AuthoritySnapshot(authority,actor),RecordHash=row.Hash,
                SavedAt=h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow()};
            db.DailyReviewEvents.Add(entry);outcome=JsonSerializer.Serialize(Dto(entry),DailyOperationsEndpoints.Json);
            db.DailyReviewReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,RecordId=id,Kind=kind,EventId=entry.Id,Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=entry.SavedAt});
            await db.SaveChangesAsync(ct);
            if(!await Current(h,kind,vessel,rights,id,authorityHash,authority.Id))return await Fail(h,403,"human_authority_required");
            await tx.CommitAsync(ct);
        } catch(DbUpdateConcurrencyException) {
            if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await Fail(h,409,"version_conflict");
        } catch(Exception e)when(PostgresFailure.IsDatabase(e)) {
            if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(e)||PostgresFailure.Provider(e)?.SqlState=="23505";
            return await Replay()??await Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");
        } finally {if(tx!=null)await tx.DisposeAsync();}
        await Audit(h,"Success",id);
        if(!await Current(h,kind,vessel,rights,id,authorityHash,authority.Id))return await Fail(h,503,"command_outcome_withheld");
        return Results.Content(outcome!,"application/json");
    }
}
