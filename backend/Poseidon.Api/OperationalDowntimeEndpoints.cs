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

public sealed record DowntimeRecordDto(Guid Id,Guid VesselId,long Revision,uint Version,SortedDictionary<string,DowntimeFact> Fields,DowntimeOriginPin? Origin,
    string SourceVersion,string Hash,string Provenance,DateTimeOffset SavedAt,string State,string Qualification,string[] Blockers,bool CurrentRevision,bool CanEdit,DowntimeIntervalObservation EngineeringInterval);
public sealed record DowntimeOfflineDto(bool Supported,string State);
public sealed record DowntimeCapabilitiesDto(Guid VesselId,bool CanRead,bool CanWrite,bool CanVerify,bool CanClose,bool CanFinalize,string[] OriginKinds,
    DowntimeFieldDefinition[] Schema,string SourceVersion,DowntimeOfflineDto Offline);
public sealed record DowntimePageDto(DowntimeRecordDto[] Items,int Offset,int Limit,bool HasMore,bool CanWrite,string SourceVersion);
public sealed record DowntimeHistoryDto(DowntimeRecordDto[] Items,int Offset,int Limit,bool HasMore);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DowntimeDraftCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,
    [property:JsonRequired] Dictionary<string,DowntimeFact> Fields,[property:JsonRequired] string SourceVersion,[property:JsonRequired] DowntimeOriginPin? Origin=null);

public static class OperationalDowntimeEndpoints
{
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=8,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static void MapOperationalDowntime(this IEndpointRouteBuilder app) {
        var group=app.MapGroup("/api/v1/operational-downtime").RequireAuthorization("DirectoryRead");
        group.MapGet("",(HttpContext h,PoseidonDbContext db)=>Read(null,false,false,h,db)).WithName("DowntimeList").Produces<DowntimePageDto>();
        group.MapGet("/capabilities",(HttpContext h,PoseidonDbContext db)=>Read(null,false,true,h,db)).WithName("DowntimeCapabilities").Produces<DowntimeCapabilitiesDto>();
        group.MapGet("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(id,false,false,h,db)).WithName("DowntimeRead").Produces<DowntimeRecordDto>();
        group.MapGet("/{id:guid}/history",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(id,true,false,h,db)).WithName("DowntimeHistory").Produces<DowntimeHistoryDto>();
        group.MapPost("",(HttpContext h,PoseidonDbContext db)=>Save(null,h,db)).WithName("DowntimeCreate").Accepts<DowntimeDraftCommand>("application/json").Produces<DowntimeRecordDto>();
        group.MapPut("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Save(id,h,db)).WithName("DowntimeUpdate").Accepts<DowntimeDraftCommand>("application/json").Produces<DowntimeRecordDto>();
    }
    private static TrustedActor? Actor(HttpContext h)=>h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static bool CanRead(TrustedActor? a)=>a?.CanRead==true && a.CanSeeShip && a.Permissions.Contains("operations.downtime.read");
    internal static bool CanWrite(TrustedActor? a,Guid vessel)=>CanRead(a) && a!.HasWriteAttribution && a.CanEditVessel(vessel) && a.Permissions.Contains("operations.downtime.write");
    private static IQueryable<Vessel> Vessels(PoseidonDbContext db,TrustedActor actor,Guid vessel)=>actor.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==vessel && v.Kind==VesselKind.Ship);
    private static string Rights(TrustedActor actor)=>RegisterStorage.Hash(JsonSerializer.Serialize(new{actor.Id,actor.TenantId,actor.RoleContext,actor.TenantWideRead,actor.TenantWideManage,
        permissions=actor.Permissions.Order(StringComparer.Ordinal),vessels=actor.VesselGrants.OrderBy(v=>v.VesselId).ThenBy(v=>v.CanEdit)},Json));
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        vessel=Guid.Empty;offset=0;return h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vessel" || paging && q.Key=="offset")) &&
            Guid.TryParseExact(h.Request.Query["vessel"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static Task Audit(HttpContext h,string result,Guid? id=null)=>h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,h.Request.Method=="GET"?"DowntimeRead":"DowntimeCommand",result,resourceId:id);
    private static async Task<IResult> Fail(HttpContext h,int status,string code){await Audit(h,code);return Results.Json(new{code},statusCode:status);}
    private static DowntimeOriginPin? Origin(string? json)=>json==null?null:JsonSerializer.Deserialize<DowntimeOriginPin>(json,Json)??throw new InvalidDataException("downtime_origin_unavailable");
    internal static DowntimeRecordDto Dto(Downtime row,bool current,bool canWrite,DateTimeOffset now)=>Dto(row.Id,row.VesselId,row.Revision,row.Version,row.FieldsJson,
        Origin(row.OriginJson),row.SourceVersion,row.Hash,row.Provenance,row.SavedAt,current,canWrite,now);
    private static DowntimeRecordDto Dto(DowntimeRevision row,bool current,bool canWrite,DateTimeOffset now)=>Dto(row.DowntimeId,row.VesselId,row.Revision,checked((uint)row.RecordVersion),row.FieldsJson,
        Origin(row.OriginJson),row.SourceVersion,row.Hash,row.Provenance,row.SavedAt,current,canWrite,now);
    private static DowntimeRecordDto Dto(Guid id,Guid vessel,long revision,uint version,string json,DowntimeOriginPin? origin,string source,string hash,string provenance,DateTimeOffset saved,bool current,bool canWrite,DateTimeOffset now) {
        var fields=DowntimePolicy.ReadFields(vessel,json);var blockers=DowntimePolicy.Blockers(fields);if(source!=DowntimePolicy.SourceVersion)blockers=[..blockers,"SOURCE_VERSION_CHANGED"];
        return new(id,vessel,revision,version,fields,origin,source,hash,provenance,saved,"DRAFT","REVIEW_REQUIRED",blockers,current,current && canWrite,DowntimePolicy.ObserveInterval(fields));
    }
    private sealed record HeadPin(Guid Id,long Revision,uint Version,string? OriginJson);
    private sealed class ReplaceParameter(ParameterExpression from,ParameterExpression to):ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node)=>node==from?to:base.VisitParameter(node);
    }
    private static Expression<Func<T,bool>> Or<T>(Expression<Func<T,bool>> left,Expression<Func<T,bool>> right)=>Expression.Lambda<Func<T,bool>>(
        Expression.OrElse(left.Body,new ReplaceParameter(right.Parameters[0],left.Parameters[0]).Visit(right.Body)!),left.Parameters);
    private static async Task<bool> Current(HttpContext h,Guid vessel,string rights,DowntimeRecordDto[] records,bool write=false,DowntimeOriginPin? proposed=null,bool proposedCurrent=true) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var actor=resolver.Resolve(h.User);
        if(!CanRead(actor) || Rights(actor!)!=rights || write && !CanWrite(actor,vessel))return false;
        var db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();if(db.TenantId!=actor!.TenantId)return false;
        var ids=records.Select(r=>r.Id).Distinct().ToArray();HeadPin[] heads=ids.Length==0?[]:await db.Downtimes.AsNoTracking().Where(r=>r.VesselId==vessel && ids.Contains(r.Id))
            .Select(r=>new HeadPin(r.Id,r.Revision,r.Version,r.OriginJson)).ToArrayAsync(h.RequestAborted);
        Expression<Func<DowntimeRevision,bool>> pairs=r=>false;
        foreach(var row in records)pairs=Or(pairs,r=>r.VesselId==vessel && r.DowntimeId==row.Id && r.Revision==row.Revision && r.RecordVersion==row.Version && r.Hash==row.Hash && r.SourceVersion==row.SourceVersion &&
            (!row.CurrentRevision || db.Downtimes.Any(head=>head.Id==r.DowntimeId && head.VesselId==vessel && head.Revision==row.Revision && head.Version==row.Version)));
        Expression<Func<Downtime,bool>> headPairs=r=>false;
        foreach(var head in heads)headPairs=Or(headPairs,r=>r.Id==head.Id && r.VesselId==vessel && r.Revision==head.Revision && r.Version==head.Version &&
            r.OriginJson==head.OriginJson);
        var pins=records.Where(r=>r.Origin!=null).Select(r=>r.Origin!).Concat(heads.Where(r=>r.OriginJson!=null).Select(r=>Origin(r.OriginJson)!)).Distinct().ToArray();
        var planFence=await DowntimeOriginReader.CaptureFence(db,actor,vessel,pins,false,h.RequestAborted);if(planFence==null)return false;
        var proposedFence=await DowntimeOriginReader.CaptureFence(db,actor,vessel,proposed==null?[]:[proposed],proposedCurrent,h.RequestAborted);if(proposedFence==null)return false;
        var returned=db.DowntimeRevisions.AsNoTracking().Where(pairs);var currentHeads=db.Downtimes.AsNoTracking().Where(headPairs);
        // Exact source, nested context and actual current xmin, if required, are
        // observed with every downtime/head pin in ONE final fresh SQL statement.
        var found=await Vessels(db,actor,vessel).Where(planFence).Where(proposedFence).Select(_=>returned.Count()==records.Length && currentHeads.Count()==ids.Length).SingleOrDefaultAsync(h.RequestAborted);
        var live=resolver.Resolve(h.User);return found && CanRead(live) && Rights(live!)==rights && (!write || CanWrite(live,vessel));
    }
    private static DateTimeOffset Now(HttpContext h)=>h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    private static async Task<IResult> Read(Guid? id,bool history,bool capabilities,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";if(!Query(h,history || id==null && !capabilities,out var vessel,out var offset))return await Fail(h,400,"invalid_query");
        var actor=Actor(h);if(!CanRead(actor))return await Fail(h,403,"permission_denied");var rights=Rights(actor!);
        if(!await Vessels(db,actor!,vessel).AnyAsync(h.RequestAborted))return await Fail(h,404,"resource_not_found");var canWrite=CanWrite(actor,vessel);
        DowntimeRecordDto[] records=[];var more=false;
        if(!capabilities) {
            var query=db.Downtimes.AsNoTracking().Where(r=>r.VesselId==vessel);var head=id==null?null:await query.SingleOrDefaultAsync(r=>r.Id==id,h.RequestAborted);
            if(id!=null && head==null)return await Fail(h,404,"resource_not_found");
            if(history) {
                var rows=await db.DowntimeRevisions.AsNoTracking().Where(r=>r.VesselId==vessel && r.DowntimeId==id).OrderByDescending(r=>r.Revision).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
                records=rows.Take(25).Select(r=>Dto(r,r.Revision==head!.Revision,false,Now(h))).ToArray();more=rows.Length>25;
            }else if(id!=null)records=[Dto(head!,true,canWrite,Now(h))];
            else {
                var rows=await query.OrderByDescending(r=>r.StartedAt??r.SavedAt).ThenBy(r=>r.Id).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
                records=rows.Take(25).Select(r=>Dto(r,true,canWrite,Now(h))).ToArray();more=rows.Length>25;
            }
        }
        if(!await Current(h,vessel,rights,records))return await Fail(h,403,"permission_denied");await Audit(h,"Success",id??vessel);
        if(!await Current(h,vessel,rights,records))return await Fail(h,403,"permission_denied");
        if(capabilities)return Results.Ok(new DowntimeCapabilitiesDto(vessel,true,canWrite,false,false,false,DowntimeOriginReader.Kinds(actor!),DowntimePolicy.Schema(),DowntimePolicy.SourceVersion,new(false,"UNAVAILABLE")));
        if(history)return Results.Ok(new DowntimeHistoryDto(records,offset,25,more));if(id!=null)return Results.Ok(records[0]);return Results.Ok(new DowntimePageDto(records,offset,25,more,canWrite,DowntimePolicy.SourceVersion));
    }
    private static async Task<IResult> Save(Guid? routeId,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;var actor=Actor(h);
        if(!CanRead(actor) || !actor!.HasWriteAttribution || !actor.Permissions.Contains("operations.downtime.write"))return await Fail(h,403,"permission_denied");
        if(!Query(h,false,out var vessel,out _))return await Fail(h,400,"invalid_query");if(!CanWrite(actor,vessel))return await Fail(h,403,"permission_denied");
        if(!await Vessels(db,actor,vessel).AnyAsync(ct))return await Fail(h,404,"resource_not_found");var rights=Rights(actor);
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;if(key==null || !Guid.TryParseExact(key,"D",out _))return await Fail(h,400,"invalid_idempotency_key");
        DowntimeDraftCommand command;SortedDictionary<string,DowntimeFact> fields;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var buffer=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,65536,ct);
            using var document=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=8});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);command=document.RootElement.Deserialize<DowntimeDraftCommand>(Json)??throw new JsonException();
            if(command.Id==Guid.Empty || routeId!=null && routeId!=command.Id || routeId==null && command.ExpectedVersion!=0 || routeId!=null && command.ExpectedVersion==0 ||
                !DowntimePolicy.TryNormalize(command.Fields,out fields,out _))throw new JsonException();
            if(command.Origin!=null && !DowntimeOriginReader.Valid(command.Origin))return await Fail(h,400,"linked_resource_unavailable");
            if(command.SourceVersion!=DowntimePolicy.SourceVersion)return await Fail(h,409,"source_contract_changed");
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,400,"invalid_request");}
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,routeId,command.Id,command.ExpectedVersion,fields,command.Origin,command.SourceVersion},Json));
        async Task<IResult> Outcome(string json) {
            try {
                var saved=JsonSerializer.Deserialize<DowntimeRecordDto>(json,Json)??throw new InvalidDataException("downtime_receipt_unavailable");
                await Audit(h,"Success",command.Id);if(!await Current(h,vessel,rights,[saved],true))return await Fail(h,503,"command_outcome_withheld");
                // Receipt remains immutable; current editing rights require a later owning GET.
                return Results.Json(saved,Json);
            }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
        }
        async Task<IResult?> Replay() {
            if(!await Current(h,vessel,rights,[],true))return await Fail(h,403,"permission_denied");
            var receipt=await db.DowntimeReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,ct);if(receipt==null)return null;
            if(receipt.VesselId!=vessel || receipt.DowntimeId!=command.Id || receipt.Fingerprint!=fingerprint)return await Fail(h,409,"idempotency_key_reused");return await Outcome(receipt.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;
        var existing=routeId==null?null:await db.Downtimes.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==command.Id && r.VesselId==vessel,ct);
        if(routeId!=null && existing==null)return await Fail(h,404,"resource_not_found");DowntimeRecordDto[] previous=existing==null?[]:[Dto(existing,true,true,Now(h))];
        var unchangedOrigin=existing!=null && existing.Version==command.ExpectedVersion && Origin(existing.OriginJson)==command.Origin;
        if(!await Current(h,vessel,rights,previous,true,command.Origin,!unchangedOrigin))return await Fail(h,403,"linked_resource_unavailable");
        await Audit(h,"Attempted",command.Id);IDbContextTransaction? tx=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var now=Now(h);
            var applied=await DowntimeWriter.Apply(db,actor,vessel,command,routeId==null,now,ct);if(applied.Record==null)return await Fail(h,applied.Status,applied.Code!);
            outcome=JsonSerializer.Serialize(applied.Record,Json);db.DowntimeReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,DowntimeId=command.Id,Revision=applied.Record.Revision,
                RecordVersion=applied.Record.Version,Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});await db.SaveChangesAsync(ct);
            if(!await Current(h,vessel,rights,previous,true,command.Origin,!unchangedOrigin))return await Fail(h,403,"permission_denied");await tx.CommitAsync(ct);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await Fail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";return await Replay()??await Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(outcome!);
    }
}
