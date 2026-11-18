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

public sealed record ShipOpsRecordDto(Guid Id,Guid VesselId,string Kind,long Revision,uint Version,SortedDictionary<string,ShipOpsFact> Fields,
    string State,string Provenance,DateTimeOffset SavedAt,string Hash,string SourceVersion,string[] Blockers,bool CurrentRevision,bool CanEdit,ShipOperationOriginPin? Origin=null);
public sealed record ShipOpsOfflineDto(bool Supported,string State,string Operation);
public sealed record ShipOpsCapabilitiesDto(Guid VesselId,string Kind,bool CanRead,bool CanWrite,bool CanReview,bool CanSubmit,
    ShipOpsFieldDefinition[] Schema,string SourceVersion,ShipOpsOfflineDto Offline);
public sealed record ShipOpsPageDto(ShipOpsRecordDto[] Items,int Offset,int Limit,bool HasMore,bool CanWrite,string SourceVersion,ShipOpsOfflineDto Offline);
public sealed record ShipOpsHistoryDto(ShipOpsRecordDto[] Items,int Offset,int Limit,bool HasMore);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShipOpsDraftCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,
    [property:JsonRequired] Dictionary<string,ShipOpsFact> Fields,[property:JsonRequired] string ReasonCode,ShipOperationOriginPin? Origin=null);

public static class ShipOperationsEndpoints
{
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=8,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    private static readonly ShipOpsOfflineDto Offline=new(false,"UNAVAILABLE","ShipOpsDraft");
    public static void MapShipOperations(this IEndpointRouteBuilder app) {
        foreach(var family in new[]{(Kind:"Fuel",Route:"fuel-bunker-logs"),(Kind:"Port",Route:"port-activity-sof")}) {
            var kind=family.Kind;var group=app.MapGroup("/api/v1/"+family.Route).RequireAuthorization("DirectoryRead");
            group.MapGet("",(HttpContext h,PoseidonDbContext db)=>Read(kind,null,false,false,h,db)).WithName(kind+"ShipOpsList").Produces<ShipOpsPageDto>();
            group.MapGet("/capabilities",(HttpContext h,PoseidonDbContext db)=>Read(kind,null,false,true,h,db)).WithName(kind+"ShipOpsCapabilities").Produces<ShipOpsCapabilitiesDto>();
            group.MapGet("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(kind,id,false,false,h,db)).WithName(kind+"ShipOpsRead").Produces<ShipOpsRecordDto>();
            group.MapGet("/{id:guid}/history",(Guid id,HttpContext h,PoseidonDbContext db)=>Read(kind,id,true,false,h,db)).WithName(kind+"ShipOpsHistory").Produces<ShipOpsHistoryDto>();
            group.MapPost("",(HttpContext h,PoseidonDbContext db)=>Save(kind,null,h,db)).WithName(kind+"ShipOpsCreate").Accepts<ShipOpsDraftCommand>("application/json").Produces<ShipOpsRecordDto>();
            group.MapPut("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Save(kind,id,h,db)).WithName(kind+"ShipOpsUpdate").Accepts<ShipOpsDraftCommand>("application/json").Produces<ShipOpsRecordDto>();
        }
    }
    private static TrustedActor? Actor(HttpContext h)=>h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static string Permission(string kind,string action)=>"operations."+kind.ToLowerInvariant()+"."+action;
    internal static bool CanRead(TrustedActor? actor,string kind)=>kind is "Fuel" or "Port" && actor?.CanRead==true && actor.CanSeeShip && actor.Permissions.Contains(Permission(kind,"read"));
    internal static bool CanWrite(TrustedActor? actor,string kind,Guid vessel)=>CanRead(actor,kind) && actor!.HasWriteAttribution && actor.CanEditVessel(vessel) && actor.Permissions.Contains(Permission(kind,"write"));
    private static IQueryable<Vessel> Vessels(PoseidonDbContext db,TrustedActor actor,Guid vessel)=>actor.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==vessel && v.Kind==VesselKind.Ship);
    private static string Rights(TrustedActor actor)=>RegisterStorage.Hash(JsonSerializer.Serialize(new{actor.Id,actor.TenantId,actor.RoleContext,actor.TenantWideRead,actor.TenantWideManage,
        permissions=actor.Permissions.Order(StringComparer.Ordinal),vessels=actor.VesselGrants.OrderBy(v=>v.VesselId).ThenBy(v=>v.CanEdit)},Json));
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        vessel=Guid.Empty;offset=0;return h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vesselId" || paging && q.Key=="offset")) &&
            Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static Task Audit(HttpContext h,string result,Guid? id=null)=>h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,
        h.Request.Method=="GET"?"ShipOperationsRead":"ShipOperationsCommand",result,resourceId:id);
    private static async Task<IResult> Fail(HttpContext h,int status,string code){await Audit(h,code);return Results.Json(new{code},statusCode:status);}
    internal static ShipOpsRecordDto Dto(ShipOperation row,bool current,bool canWrite)=>Dto(row.Id,row.VesselId,row.Kind,row.Revision,row.Version,row.FieldsJson,row.OriginJson,row.Provenance,row.SavedAt,row.Hash,row.SourceVersion,current,canWrite);
    private static ShipOpsRecordDto Dto(ShipOperationRevision row,bool current,bool canWrite)=>Dto(row.RecordId,row.VesselId,row.Kind,row.Revision,checked((uint)row.RecordVersion),row.FieldsJson,row.OriginJson,row.Provenance,row.SavedAt,row.Hash,row.SourceVersion,current,canWrite);
    private static ShipOpsRecordDto Dto(Guid id,Guid vessel,string kind,long revision,uint version,string json,string? originJson,string provenance,DateTimeOffset saved,string hash,string source,bool current,bool canWrite) {
        var fields=ShipOperationsPolicy.ReadFields(kind,vessel,json);var blockers=ShipOperationsPolicy.Blockers(kind,fields);
        if(source!=ShipOperationsPolicy.SourceVersion)blockers=[..blockers,"SOURCE_VERSION_CHANGED"];
        return new(id,vessel,kind,revision,version,fields,"DRAFT",provenance,saved,hash,source,blockers,current,current && canWrite,
            originJson==null?null:JsonSerializer.Deserialize<ShipOperationOriginPin>(originJson,Json));
    }
    // One fresh trusted database statement observes the vessel, exact returned
    // ledger revisions/current heads and retained origin visibility together.
    private static async Task<bool> Current(HttpContext h,string kind,Guid vessel,string rights,ShipOpsRecordDto[] records,bool write=false,ShipOperationOriginPin? proposedOrigin=null,bool proposedCurrent=true) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var actor=resolver.Resolve(h.User);
        if(!CanRead(actor,kind) || Rights(actor!)!=rights || write && !CanWrite(actor,kind,vessel))return false;
        var db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();if(db.TenantId!=actor!.TenantId)return false;
        var ids=records.Select(r=>r.Id).Distinct().ToArray();
        HeadPin[] headPins=ids.Length==0?[]:await db.ShipOperations.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind==kind && ids.Contains(r.Id))
            .Select(r=>new HeadPin(r.Id,r.Revision,r.Version,r.OriginJson)).ToArrayAsync(h.RequestAborted);
        Expression<Func<ShipOperationRevision,bool>> pairs=r=>false;
        foreach(var record in records) {
            Expression<Func<ShipOperationRevision,bool>> pin=r=>r.RecordId==record.Id && r.VesselId==vessel && r.Kind==kind && r.Revision==record.Revision && r.RecordVersion==record.Version &&
                r.Hash==record.Hash && r.SourceVersion==record.SourceVersion && (!record.CurrentRevision || db.ShipOperations.Any(head=>head.Id==r.RecordId && head.VesselId==vessel && head.Kind==kind && head.Revision==r.Revision && head.Version==record.Version));
            var body=new ReplaceParameter(pin.Parameters[0],pairs.Parameters[0]).Visit(pin.Body)!;pairs=Expression.Lambda<Func<ShipOperationRevision,bool>>(Expression.OrElse(pairs.Body,body),pairs.Parameters);
        }
        var returned=db.ShipOperationRevisions.AsNoTracking().Where(pairs);
        // A new protected current origin also restricts old history/receipts.
        // Pin the freshly loaded current head metadata in the final statement;
        // an intervening restriction change is never treated as a safe snapshot.
        Expression<Func<ShipOperation,bool>> headPairs=r=>false;
        foreach(var head in headPins) {
            Expression<Func<ShipOperation,bool>> pin=r=>r.Id==head.Id && r.VesselId==vessel && r.Kind==kind && r.Revision==head.Revision && r.Version==head.Version && r.OriginJson==head.OriginJson;
            var body=new ReplaceParameter(pin.Parameters[0],headPairs.Parameters[0]).Visit(pin.Body)!;headPairs=Expression.Lambda<Func<ShipOperation,bool>>(Expression.OrElse(headPairs.Body,body),headPairs.Parameters);
        }
        var currentHeads=db.ShipOperations.AsNoTracking().Where(headPairs);
        var origins=records.Where(r=>r.Origin!=null).Select(r=>r.Origin!).Concat(headPins.Where(r=>r.OriginJson!=null)
            .Select(r=>JsonSerializer.Deserialize<ShipOperationOriginPin>(r.OriginJson!,Json)??throw new InvalidDataException("ship_operation_origin_unavailable"))).Distinct().ToArray();
        var visibleOrigins=DailyOperationOriginReader.ReturnedVisible(db,actor,vessel,origins,false);
        var proposed=DailyOperationOriginReader.ReturnedVisible(db,actor,vessel,proposedOrigin==null?[]:[proposedOrigin],proposedCurrent);
        var ok=await Vessels(db,actor,vessel).Select(_=>returned.Count()==records.Length && currentHeads.Count()==ids.Length && visibleOrigins.Count()==origins.Length &&
            (proposedOrigin==null || proposed.Count()==1)).SingleOrDefaultAsync(h.RequestAborted);
        var live=resolver.Resolve(h.User);return ok && CanRead(live,kind) && Rights(live!)==rights && (!write || CanWrite(live,kind,vessel));
    }
    private sealed class ReplaceParameter(ParameterExpression from,ParameterExpression to):ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node)=>node==from?to:base.VisitParameter(node);
    }
    private sealed record HeadPin(Guid Id,long Revision,uint Version,string? OriginJson);
    private static async Task<IResult> Read(string kind,Guid? id,bool history,bool capabilities,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";if(!Query(h,history || id==null && !capabilities,out var vessel,out var offset))return await Fail(h,400,"invalid_query");
        var actor=Actor(h);if(!CanRead(actor,kind))return await Fail(h,403,"permission_denied");var rights=Rights(actor!);
        if(!await Vessels(db,actor!,vessel).AnyAsync(h.RequestAborted))return await Fail(h,404,"resource_not_found");
        var canWrite=CanWrite(actor,kind,vessel);ShipOpsRecordDto[] records=[];object result;
        if(capabilities)result=new ShipOpsCapabilitiesDto(vessel,kind,true,canWrite,false,false,ShipOperationsPolicy.Schema(kind),ShipOperationsPolicy.SourceVersion,Offline);
        else {
            var heads=db.ShipOperations.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind==kind);var head=id==null?null:await heads.SingleOrDefaultAsync(r=>r.Id==id,h.RequestAborted);
            if(id!=null && head==null)return await Fail(h,404,"resource_not_found");
            if(history) {
                var rows=await db.ShipOperationRevisions.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind==kind && r.RecordId==id).OrderByDescending(r=>r.Revision).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
                records=rows.Take(25).Select(r=>Dto(r,r.Revision==head!.Revision,canWrite)).ToArray();result=new ShipOpsHistoryDto(records,offset,25,rows.Length>25);
            }else if(id!=null){records=[Dto(head!,true,canWrite)];result=records[0];}
            else {
                var rows=await heads.OrderByDescending(r=>r.OccurredAt??r.SavedAt).ThenBy(r=>r.Id).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
                records=rows.Take(25).Select(r=>Dto(r,true,canWrite)).ToArray();result=new ShipOpsPageDto(records,offset,25,rows.Length>25,canWrite,ShipOperationsPolicy.SourceVersion,Offline);
            }
        }
        if(!await Current(h,kind,vessel,rights,records))return await Fail(h,403,"permission_denied");await Audit(h,"Success",id??vessel);
        if(!await Current(h,kind,vessel,rights,records))return await Fail(h,403,"permission_denied");return Results.Ok(result);
    }
    private static async Task<IResult> Save(string kind,Guid? routeId,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;var actor=Actor(h);
        if(!CanRead(actor,kind) || !actor!.HasWriteAttribution || !actor.Permissions.Contains(Permission(kind,"write")))return await Fail(h,403,"permission_denied");
        if(!Query(h,false,out var vessel,out _))return await Fail(h,400,"invalid_query");if(!CanWrite(actor,kind,vessel))return await Fail(h,403,"permission_denied");
        if(!await Vessels(db,actor,vessel).AnyAsync(ct))return await Fail(h,404,"resource_not_found");var rights=Rights(actor);
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;if(key==null || !Guid.TryParseExact(key,"D",out _))return await Fail(h,400,"invalid_idempotency_key");
        ShipOpsDraftCommand command;SortedDictionary<string,ShipOpsFact> fields;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var buffer=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,65536,ct);
            using var document=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=8});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);command=document.RootElement.Deserialize<ShipOpsDraftCommand>(Json)??throw new JsonException();
            if(command.Id==Guid.Empty || routeId!=null && routeId!=command.Id || routeId==null && command.ExpectedVersion!=0 || routeId!=null && command.ExpectedVersion==0 ||
                command.ReasonCode is not("INITIAL_AUTHORING" or "AUTHORING_CORRECTION"))throw new JsonException();
            if(command.Origin!=null && (!DailyOperationOriginReader.Valid(command.Origin) || kind=="Port" && command.Origin.Kind!="Noon"))return await Fail(h,400,"linked_resource_unavailable");
            if(!ShipOperationsPolicy.TryNormalize(kind,command.Fields,out fields,out var error))return await Fail(h,400,error=="lookup_source_unavailable"?"linked_resource_unavailable":"invalid_request");
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,400,"invalid_request");}
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{kind,vessel,routeId,command.Id,command.ExpectedVersion,fields,command.ReasonCode,command.Origin},Json));
        async Task<IResult> Outcome(string json) {
            try {
                var record=JsonSerializer.Deserialize<ShipOpsRecordDto>(json,Json)??throw new InvalidDataException("ship_operation_receipt_unavailable");
                await Audit(h,"Success",command.Id);if(!await Current(h,kind,vessel,rights,[record],true))return await Fail(h,503,"command_outcome_withheld");return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
        }
        async Task<IResult?> Replay() {
            if(!await Current(h,kind,vessel,rights,[],true))return await Fail(h,403,"permission_denied");
            var receipt=await db.ShipOperationReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,ct);if(receipt==null)return null;
            if(receipt.Kind!=kind || receipt.VesselId!=vessel || receipt.RecordId!=command.Id || receipt.Fingerprint!=fingerprint)return await Fail(h,409,"idempotency_key_reused");return await Outcome(receipt.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;
        var existing=routeId==null?null:await db.ShipOperations.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==command.Id && r.VesselId==vessel && r.Kind==kind,ct);
        if(routeId!=null && existing==null)return await Fail(h,404,"resource_not_found");
        ShipOpsRecordDto[] previous=existing==null?[]:[Dto(existing,true,true)];
        var originJson=command.Origin==null?null:JsonSerializer.Serialize(command.Origin,Json);
        var unchangedOrigin=existing!=null && existing.Version==command.ExpectedVersion && existing.OriginJson==originJson;
        if(!await Current(h,kind,vessel,rights,previous,true,command.Origin,!unchangedOrigin))return await Fail(h,403,"linked_resource_unavailable");
        await Audit(h,"Attempted",command.Id);IDbContextTransaction? tx=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var now=h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var applied=await ShipOperationsWriter.Apply(db,actor,vessel,kind,command,routeId==null,now,ct);if(applied.Record==null)return await Fail(h,applied.Status,applied.Code!);
            outcome=JsonSerializer.Serialize(applied.Record,Json);db.ShipOperationReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,RecordId=command.Id,Kind=kind,
                OriginJson=command.Origin==null?null:JsonSerializer.Serialize(command.Origin,Json),Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});await db.SaveChangesAsync(ct);
            if(!await Current(h,kind,vessel,rights,previous,true,command.Origin,!unchangedOrigin))return await Fail(h,403,"permission_denied");await tx.CommitAsync(ct);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await Fail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";return await Replay()??await Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(outcome!);
    }
}
