using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DefectRecordDto(Guid Id,Guid VesselId,long Revision,uint Version,SortedDictionary<string,DefectFact> Fields,DefectAssetPin? Asset,
    string SourceVersion,string Hash,string Provenance,DateTimeOffset SavedAt,string Stage,string Qualification,string[] Blockers,bool CurrentRevision,bool CanEdit,DefectTemporalAssessment Temporal);
public sealed record DefectOfflineDto(bool Supported,string State);
public sealed record DefectCapabilitiesDto(Guid VesselId,bool CanRead,bool CanWrite,bool CanClose,bool CanVerify,bool CanAssessEffectiveness,
    DefectFieldDefinition[] Schema,string SourceVersion,DefectOfflineDto Offline);
public sealed record DefectPageDto(DefectRecordDto[] Items,int Offset,int Limit,bool HasMore,bool CanWrite,string SourceVersion);
public sealed record DefectHistoryDto(DefectRecordDto[] Items,int Offset,int Limit,bool HasMore);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DefectDraftCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,
    [property:JsonRequired] Dictionary<string,DefectFact> Fields,[property:JsonRequired] string SourceVersion,[property:JsonRequired] DefectAssetPin? Asset=null);

public static class DefectEndpoints
{
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=8,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static void MapDefects(this IEndpointRouteBuilder app) {
        var group=app.MapGroup("/api/v1/defects").RequireAuthorization("DirectoryRead");
        group.MapGet("",(HttpContext h,PoseidonDbContext db)=>Read(null,false,false,h,db)).WithName("DefectList").Produces<DefectPageDto>();
        group.MapGet("/capabilities",(HttpContext h,PoseidonDbContext db)=>Read(null,false,true,h,db)).WithName("DefectCapabilities").Produces<DefectCapabilitiesDto>();
        group.MapGet("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(id,false,false,h,db)).WithName("DefectRead").Produces<DefectRecordDto>();
        group.MapGet("/{id:guid}/history",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(id,true,false,h,db)).WithName("DefectHistory").Produces<DefectHistoryDto>();
        group.MapPost("",(HttpContext h,PoseidonDbContext db)=>Save(null,h,db)).WithName("DefectCreate").Accepts<DefectDraftCommand>("application/json").Produces<DefectRecordDto>();
        group.MapPut("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Save(id,h,db)).WithName("DefectUpdate").Accepts<DefectDraftCommand>("application/json").Produces<DefectRecordDto>();
    }
    private static TrustedActor? Actor(HttpContext h)=>h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static bool CanRead(TrustedActor? a)=>a?.CanRead==true && a.Permissions.Contains("defect.read") && a.Permissions.Contains("equipment.read");
    internal static bool CanWrite(TrustedActor? a,Guid vessel)=>CanRead(a) && a!.HasWriteAttribution && a.CanEditVessel(vessel) && a.Permissions.Contains("defect.write");
    private static IQueryable<Vessel> Vessels(PoseidonDbContext db,TrustedActor actor,Guid vessel)=>actor.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==vessel);
    private static string Rights(TrustedActor actor)=>RegisterStorage.Hash(JsonSerializer.Serialize(new{actor.Id,actor.TenantId,actor.RoleContext,actor.TenantWideRead,actor.TenantWideManage,
        permissions=actor.Permissions.Order(StringComparer.Ordinal),vessels=actor.VesselGrants.OrderBy(v=>v.VesselId).ThenBy(v=>v.CanEdit)},Json));
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        vessel=Guid.Empty;offset=0;return h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vessel" || paging && q.Key=="offset")) &&
            Guid.TryParseExact(h.Request.Query["vessel"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static Task Audit(HttpContext h,string result,Guid? id=null)=>h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,h.Request.Method=="GET"?"DefectRead":"DefectCommand",result,resourceId:id);
    private static async Task<IResult> Fail(HttpContext h,int status,string code){await Audit(h,code);return Results.Json(new{code},statusCode:status);}
    private static DefectAssetPin? Asset(Guid? id,long? revision,long? version,string? hash)=>id==null?null:
        new(id.Value,revision??throw new InvalidDataException("defect_asset_unavailable"),checked((uint)(version??throw new InvalidDataException("defect_asset_unavailable"))),hash??throw new InvalidDataException("defect_asset_unavailable"));
    internal static DefectRecordDto Dto(Defect row,bool current,bool canWrite,DateTimeOffset now)=>Dto(row.Id,row.VesselId,row.Revision,row.Version,row.FieldsJson,
        Asset(row.AssetId,row.AssetRevision,row.AssetVersion,row.AssetHash),row.SourceVersion,row.Hash,row.Provenance,row.SavedAt,current,canWrite,now);
    private static DefectRecordDto Dto(DefectRevision row,bool current,bool canWrite,DateTimeOffset now)=>Dto(row.DefectId,row.VesselId,row.Revision,checked((uint)row.RecordVersion),row.FieldsJson,
        Asset(row.AssetId,row.AssetRevision,row.AssetVersion,row.AssetHash),row.SourceVersion,row.Hash,row.Provenance,row.SavedAt,current,canWrite,now);
    private static DefectRecordDto Dto(Guid id,Guid vessel,long revision,uint version,string json,DefectAssetPin? asset,string source,string hash,string provenance,DateTimeOffset saved,bool current,bool canWrite,DateTimeOffset now) {
        var fields=DefectPolicy.ReadFields(json);var blockers=DefectPolicy.Blockers(fields);if(source!=DefectPolicy.SourceVersion)blockers=[..blockers,"SOURCE_VERSION_CHANGED"];
        return new(id,vessel,revision,version,fields,asset,source,hash,provenance,saved,"DECLARATION","REVIEW_REQUIRED",blockers,current,current && canWrite,DefectPolicy.Assess(fields,now));
    }
    private sealed record HeadPin(Guid Id,long Revision,uint Version,Guid? AssetId,long? AssetRevision,long? AssetVersion,string? AssetHash);
    private sealed class ReplaceParameter(ParameterExpression from,ParameterExpression to):ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node)=>node==from?to:base.VisitParameter(node);
    }
    private static Expression<Func<T,bool>> Or<T>(Expression<Func<T,bool>> left,Expression<Func<T,bool>> right)=>Expression.Lambda<Func<T,bool>>(
        Expression.OrElse(left.Body,new ReplaceParameter(right.Parameters[0],left.Parameters[0]).Visit(right.Body)!),left.Parameters);
    private static async Task<bool> Current(HttpContext h,Guid vessel,string rights,DefectRecordDto[] records,bool write=false,DefectAssetPin? proposed=null,bool proposedCurrent=true) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var actor=resolver.Resolve(h.User);
        if(!CanRead(actor) || Rights(actor!)!=rights || write && !CanWrite(actor,vessel))return false;
        var db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();if(db.TenantId!=actor!.TenantId)return false;
        var ids=records.Select(r=>r.Id).Distinct().ToArray();HeadPin[] heads=ids.Length==0?[]:await db.Defects.AsNoTracking().Where(r=>r.VesselId==vessel && ids.Contains(r.Id))
            .Select(r=>new HeadPin(r.Id,r.Revision,r.Version,r.AssetId,r.AssetRevision,r.AssetVersion,r.AssetHash)).ToArrayAsync(h.RequestAborted);
        Expression<Func<DefectRevision,bool>> pairs=r=>false;
        foreach(var row in records)pairs=Or(pairs,r=>r.VesselId==vessel && r.DefectId==row.Id && r.Revision==row.Revision && r.RecordVersion==row.Version && r.Hash==row.Hash && r.SourceVersion==row.SourceVersion &&
            (!row.CurrentRevision || db.Defects.Any(head=>head.Id==r.DefectId && head.VesselId==vessel && head.Revision==row.Revision && head.Version==row.Version)));
        Expression<Func<Defect,bool>> headPairs=r=>false;
        foreach(var head in heads)headPairs=Or(headPairs,r=>r.Id==head.Id && r.VesselId==vessel && r.Revision==head.Revision && r.Version==head.Version &&
            r.AssetId==head.AssetId && r.AssetRevision==head.AssetRevision && r.AssetVersion==head.AssetVersion && r.AssetHash==head.AssetHash);
        var pins=records.Where(r=>r.Asset!=null).Select(r=>r.Asset!).Concat(heads.Where(r=>r.AssetId!=null).Select(r=>Asset(r.AssetId,r.AssetRevision,r.AssetVersion,r.AssetHash)!)).Distinct().ToArray();
        var assetFence=await DefectAssetContextReader.CaptureFence(db,actor,vessel,pins,false,h.RequestAborted);if(assetFence==null)return false;
        var proposedFence=await DefectAssetContextReader.CaptureFence(db,actor,vessel,proposed==null?[]:[proposed],proposedCurrent,h.RequestAborted);if(proposedFence==null)return false;
        var returned=db.DefectRevisions.AsNoTracking().Where(pairs);var currentHeads=db.Defects.AsNoTracking().Where(headPairs);
        // Captured asset JSON matches and actual current xmin, if required, are
        // observed with every defect/head pin in ONE final fresh SQL statement.
        var found=await Vessels(db,actor,vessel).Where(assetFence).Where(proposedFence).Select(_=>returned.Count()==records.Length && currentHeads.Count()==ids.Length).SingleOrDefaultAsync(h.RequestAborted);
        var live=resolver.Resolve(h.User);return found && CanRead(live) && Rights(live!)==rights && (!write || CanWrite(live,vessel));
    }
    private static DateTimeOffset Now(HttpContext h)=>h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    private static DefectRecordDto Fresh(DefectRecordDto row,DateTimeOffset now)=>row with{Temporal=DefectPolicy.Assess(row.Fields,now)};
    private static async Task<IResult> Read(Guid? id,bool history,bool capabilities,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";if(!Query(h,history || id==null && !capabilities,out var vessel,out var offset))return await Fail(h,400,"invalid_query");
        var actor=Actor(h);if(!CanRead(actor))return await Fail(h,403,"permission_denied");var rights=Rights(actor!);
        if(!await Vessels(db,actor!,vessel).AnyAsync(h.RequestAborted))return await Fail(h,404,"resource_not_found");var canWrite=CanWrite(actor,vessel);
        DefectRecordDto[] records=[];var more=false;
        if(!capabilities) {
            var query=db.Defects.AsNoTracking().Where(r=>r.VesselId==vessel);var head=id==null?null:await query.SingleOrDefaultAsync(r=>r.Id==id,h.RequestAborted);
            if(id!=null && head==null)return await Fail(h,404,"resource_not_found");
            if(history) {
                var rows=await db.DefectRevisions.AsNoTracking().Where(r=>r.VesselId==vessel && r.DefectId==id).OrderByDescending(r=>r.Revision).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
                records=rows.Take(25).Select(r=>Dto(r,r.Revision==head!.Revision,false,Now(h))).ToArray();more=rows.Length>25;
            }else if(id!=null)records=[Dto(head!,true,canWrite,Now(h))];
            else {
                var rows=await query.OrderByDescending(r=>r.SavedAt).ThenBy(r=>r.Id).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
                records=rows.Take(25).Select(r=>Dto(r,true,canWrite,Now(h))).ToArray();more=rows.Length>25;
            }
        }
        if(!await Current(h,vessel,rights,records))return await Fail(h,403,"permission_denied");await Audit(h,"Success",id??vessel);
        if(!await Current(h,vessel,rights,records))return await Fail(h,403,"permission_denied");records=records.Select(r=>Fresh(r,Now(h))).ToArray();
        if(capabilities)return Results.Ok(new DefectCapabilitiesDto(vessel,true,canWrite,false,false,false,DefectPolicy.Schema(),DefectPolicy.SourceVersion,new(false,"UNAVAILABLE")));
        if(history)return Results.Ok(new DefectHistoryDto(records,offset,25,more));if(id!=null)return Results.Ok(records[0]);return Results.Ok(new DefectPageDto(records,offset,25,more,canWrite,DefectPolicy.SourceVersion));
    }
    private static async Task<IResult> Save(Guid? routeId,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;var actor=Actor(h);
        if(!CanRead(actor) || !actor!.HasWriteAttribution || !actor.Permissions.Contains("defect.write"))return await Fail(h,403,"permission_denied");
        if(!Query(h,false,out var vessel,out _))return await Fail(h,400,"invalid_query");if(!CanWrite(actor,vessel))return await Fail(h,403,"permission_denied");
        if(!await Vessels(db,actor,vessel).AnyAsync(ct))return await Fail(h,404,"resource_not_found");var rights=Rights(actor);
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;if(key==null || !Guid.TryParseExact(key,"D",out _))return await Fail(h,400,"invalid_idempotency_key");
        DefectDraftCommand command;SortedDictionary<string,DefectFact> fields;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var buffer=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,65536,ct);
            using var document=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=8});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);command=document.RootElement.Deserialize<DefectDraftCommand>(Json)??throw new JsonException();
            if(command.Id==Guid.Empty || routeId!=null && routeId!=command.Id || routeId==null && command.ExpectedVersion!=0 || routeId!=null && command.ExpectedVersion==0 ||
                !DefectPolicy.TryNormalize(command.Fields,out fields,out _))throw new JsonException();
            if(command.SourceVersion!=DefectPolicy.SourceVersion)return await Fail(h,409,"source_contract_changed");
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,400,"invalid_request");}
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,routeId,command.Id,command.ExpectedVersion,fields,command.Asset,command.SourceVersion},Json));
        async Task<IResult> Outcome(string json) {
            try {
                var saved=JsonSerializer.Deserialize<DefectRecordDto>(json,Json)??throw new InvalidDataException("defect_receipt_unavailable");
                await Audit(h,"Success",command.Id);if(!await Current(h,vessel,rights,[saved],true))return await Fail(h,503,"command_outcome_withheld");
                // The durable outcome/facts/hash are immutable. Only the explicitly
                // assessed-at temporal observation is recomputed for this delivery.
                return Results.Json(Fresh(saved,Now(h)),Json);
            }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
        }
        async Task<IResult?> Replay() {
            if(!await Current(h,vessel,rights,[],true))return await Fail(h,403,"permission_denied");
            var receipt=await db.DefectReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,ct);if(receipt==null)return null;
            if(receipt.VesselId!=vessel || receipt.DefectId!=command.Id || receipt.Fingerprint!=fingerprint)return await Fail(h,409,"idempotency_key_reused");return await Outcome(receipt.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;
        var existing=routeId==null?null:await db.Defects.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==command.Id && r.VesselId==vessel,ct);
        if(routeId!=null && existing==null)return await Fail(h,404,"resource_not_found");DefectRecordDto[] previous=existing==null?[]:[Dto(existing,true,true,Now(h))];
        var unchangedAsset=existing!=null && Asset(existing.AssetId,existing.AssetRevision,existing.AssetVersion,existing.AssetHash)==command.Asset;
        if(!await Current(h,vessel,rights,previous,true,command.Asset,!unchangedAsset))return await Fail(h,403,"linked_resource_unavailable");
        await Audit(h,"Attempted",command.Id);IDbContextTransaction? tx=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var now=Now(h);
            var applied=await DefectWriter.Apply(db,actor,vessel,command,routeId==null,now,ct);if(applied.Record==null)return await Fail(h,applied.Status,applied.Code!);
            outcome=JsonSerializer.Serialize(applied.Record,Json);db.DefectReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,DefectId=command.Id,Revision=applied.Record.Revision,
                RecordVersion=applied.Record.Version,Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});await db.SaveChangesAsync(ct);
            if(!await Current(h,vessel,rights,previous,true,command.Asset,!unchangedAsset))return await Fail(h,403,"permission_denied");await tx.CommitAsync(ct);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await Fail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";return await Replay()??await Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(outcome!);
    }
}
