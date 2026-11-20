using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CriticalSpareSaveCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] long ExpectedRevision,
    [property:JsonRequired] CriticalSpareDefinition Definition,CriticalSpareCount? Count=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TechnicalRequisitionProposeCommand([property:JsonRequired] Guid Id,[property:JsonRequired] Guid SpareId,[property:JsonRequired] long SpareRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TechnicalRequisitionDecisionCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] long ExpectedRevision,
    [property:JsonRequired] string Decision,string? ApprovedQuantity=null,string? ReasonCode=null);

public sealed record CriticalSpareDto(Guid Id,Guid VesselId,long Revision,uint Version,CriticalSpareDefinition Definition,CriticalSpareCount? Count,SpareStockProjection Stock,
    string EquipmentCriticality,int OpenWorkOrderDemand,Guid? OpenRequisitionId,string Transition,string SourceVersion,string Hash,DateTimeOffset SavedAt,string Provenance,
    bool CurrentRevision,bool CanEdit);
public sealed record CriticalSparePageDto(CriticalSpareDto[] Items,int Offset,int Limit,bool HasMore,int LowStock,int ReviewRequired,bool CanWrite,bool CanPropose,bool CanApprove,
    string[] Criticalities,string[] RejectReasons,string Qualification,string ErpBoundary,string SourceVersion);
public sealed record TechnicalRequisitionErpReferenceDto(string Kind,Guid ReferenceId,Guid VesselId,string PartNumber,string Unit,string Quantity,string SourceVersion,string Boundary);
public sealed record TechnicalRequisitionDto(Guid Id,Guid VesselId,Guid SpareId,long SpareRevision,long Revision,uint Version,string State,string? ProposedQuantity,string BasisState,
    string? BasisProjected,string? BasisMinimum,string? BasisMaximum,string SpareCriticality,string EquipmentCriticality,int OpenWorkOrderDemand,DateTimeOffset ProposedAt,
    string? ApprovedQuantity,DateTimeOffset? DecidedAt,string? ReasonCode,bool ProposedByCurrentActor,bool DecidedByCurrentActor,string PartNumber,string Name,string Unit,
    TechnicalRequisitionErpReferenceDto? ErpReference,string Transition,string Hash,DateTimeOffset SavedAt,bool CurrentRevision,bool CanDecide,string ErpBoundary);
public sealed record TechnicalRequisitionPageDto(TechnicalRequisitionDto[] Items,int Offset,int Limit,bool HasMore);

// Read-only spare gap contract for other modules (WP-C PMS command center critical_spare_gaps).
public sealed record SpareGapFact(Guid SpareId,long Revision,string PartNumber,string Name,string Criticality,string StockState,string? Projected,string? Minimum,bool OpenRequisition);
public interface ISpareGapSource
{
    Task<SpareGapFact[]> ReadAsync(PoseidonDbContext db,TrustedActor actor,Guid vessel,CancellationToken ct);
}
internal sealed class SpareGapReader:ISpareGapSource
{
    public async Task<SpareGapFact[]> ReadAsync(PoseidonDbContext db,TrustedActor actor,Guid vessel,CancellationToken ct) {
        if(!CriticalSpareEndpoints.CanRead(actor) || db.TenantId!=actor.TenantId || !await actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,ct))return [];
        var rows=await CriticalSpareEndpoints.CurrentRevisions(db,vessel).OrderBy(r=>r.SpareId).Take(500).ToArrayAsync(ct);
        var consumption=await CriticalSpareEndpoints.Consumption(db,vessel,ct);
        var open=await db.TechnicalRequisitions.AsNoTracking().Where(r=>r.VesselId==vessel && r.State=="PROPOSED").Select(r=>r.SpareId).ToArrayAsync(ct);
        return rows.Select(r=>{var c=CriticalSpareEndpoints.Content(r);var p=CriticalSparePolicy.Project(c,consumption==null?null:consumption.GetValueOrDefault(r.SpareId)??[]);
            return new SpareGapFact(r.SpareId,r.Revision,c.Definition.PartNumber,c.Definition.Name,c.Definition.Criticality,p.State,p.Projected,p.Minimum,open.Contains(r.SpareId));})
            .Where(f=>f.StockState!="ABOVE_MINIMUM").ToArray();
    }
}

// DEV-110 / T-SUP2-001 critical spares + technical requisition. SERIALIZABLE, version-fenced, idempotent
// writes appending immutable revisions. The system computes the proposal; a Human approves with
// separation of duties. Approval is an ERP export reference only (no PO/vendor/price/receipt).
public static class CriticalSpareEndpoints
{
    private static readonly JsonSerializerOptions Json=WorkOrderExecutionEndpoints.Json;
    public static void MapCriticalSpares(this IEndpointRouteBuilder app) {
        var s=app.MapGroup("/api/v1/critical-spares").RequireAuthorization("DirectoryRead");
        s.MapGet("",(HttpContext h,PoseidonDbContext db)=>List(h,db)).WithName("CriticalSpareList").Produces<CriticalSparePageDto>();
        s.MapGet("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Detail(id,h,db)).WithName("CriticalSpareRead").Produces<CriticalSpareDto>();
        s.MapPost("",(HttpContext h,PoseidonDbContext db)=>Save(null,h,db)).WithName("CriticalSpareCreate").Accepts<CriticalSpareSaveCommand>("application/json").Produces<CriticalSpareDto>();
        s.MapPut("/{id:guid}",(Guid id,HttpContext h,PoseidonDbContext db)=>Save(id,h,db)).WithName("CriticalSpareUpdate").Accepts<CriticalSpareSaveCommand>("application/json").Produces<CriticalSpareDto>();
        var r=app.MapGroup("/api/v1/technical-requisitions").RequireAuthorization("DirectoryRead");
        r.MapGet("",(HttpContext h,PoseidonDbContext db)=>Requisitions(h,db)).WithName("TechnicalRequisitionList").Produces<TechnicalRequisitionPageDto>();
        r.MapPost("",(HttpContext h,PoseidonDbContext db)=>Propose(h,db)).WithName("TechnicalRequisitionPropose").Accepts<TechnicalRequisitionProposeCommand>("application/json").Produces<TechnicalRequisitionDto>();
        r.MapPost("/{id:guid}/decisions",(Guid id,HttpContext h,PoseidonDbContext db)=>Decide(id,h,db)).WithName("TechnicalRequisitionDecide").Accepts<TechnicalRequisitionDecisionCommand>("application/json").Produces<TechnicalRequisitionDto>();
    }
    internal static bool CanRead(TrustedActor? a)=>a?.CanRead==true && a.Permissions.Contains("spare.read");
    internal static bool CanWrite(TrustedActor? a,Guid vessel)=>CanRead(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("spare.write");
    internal static bool CanPropose(HttpContext? h,TrustedActor? a,Guid vessel)=>CanRead(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("requisition.propose") && h!=null && CertificateRenewalEndpoints.Human(h,a);
    internal static bool CanApprove(HttpContext? h,TrustedActor? a,Guid vessel)=>CanRead(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("requisition.approve") && h!=null && CertificateRenewalEndpoints.Human(h,a);
    private static Task<IResult> ReadFail(HttpContext h,string action,int status,string code)=>Fail(h,action,status,code);
    private static async Task<IResult> Fail(HttpContext h,string action,int status,string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,action,code);return Results.Json(new{code},statusCode:status);
    }
    internal static CriticalSpareContent Content(CriticalSpareRevision row)=>JsonSerializer.Deserialize<CriticalSpareContent>(row.ContentJson,Json)??throw new InvalidDataException("critical_spare_unavailable");
    internal static TechnicalRequisitionContent Content(TechnicalRequisitionRevision row)=>JsonSerializer.Deserialize<TechnicalRequisitionContent>(row.ContentJson,Json)??throw new InvalidDataException("requisition_unavailable");
    internal static IQueryable<CriticalSpareRevision> CurrentRevisions(PoseidonDbContext db,Guid vessel)=>db.CriticalSpareRevisions.AsNoTracking()
        .Where(r=>r.VesselId==vessel && db.CriticalSpares.Any(x=>x.VesselId==vessel && x.Id==r.SpareId && x.Revision==r.Revision));
    private static IQueryable<TechnicalRequisitionRevision> CurrentRequisitions(PoseidonDbContext db,Guid vessel)=>db.TechnicalRequisitionRevisions.AsNoTracking()
        .Where(r=>r.VesselId==vessel && db.TechnicalRequisitions.Any(x=>x.VesselId==vessel && x.Id==r.RequisitionId && x.Revision==r.Revision));
    private static readonly string[] Submitted=["EVIDENCE_SUBMITTED","VERIFIED","CLOSED"];
    private static readonly string[] Open=["RELEASED","IN_PROGRESS","EVIDENCE_SUBMITTED"];
    // Catalogued consumption on current submitted work orders. More rows than the bound -> null (unresolved).
    internal static async Task<Dictionary<Guid,List<SpareConsumption>>?> Consumption(PoseidonDbContext db,Guid vessel,CancellationToken ct) {
        var rows=await WorkOrderExecutionEndpoints.Current(db,vessel).Where(r=>Submitted.Contains(r.State)).Take(CriticalSparePolicy.MaxConsumption+1).ToArrayAsync(ct);
        if(rows.Length>CriticalSparePolicy.MaxConsumption)return null;
        var result=new Dictionary<Guid,List<SpareConsumption>>();
        foreach(var row in rows) {
            var c=WorkOrderExecutionEndpoints.Content(row);if(c.Submission==null)continue;
            foreach(var use in c.Evidence.Spares.Where(s=>s.SpareId!=null)) {
                if(!result.TryGetValue(use.SpareId!.Value,out var list))result[use.SpareId.Value]=list=[];
                list.Add(new(row.WorkOrderId,use.Quantity,c.Submission.RecordedAt));
            }
        }
        return result;
    }
    private static async Task<Dictionary<Guid,int>> Demand(PoseidonDbContext db,Guid vessel,CancellationToken ct) {
        var rows=await WorkOrderExecutionEndpoints.Current(db,vessel).Where(r=>Open.Contains(r.State)).Take(1000).ToArrayAsync(ct);
        return rows.SelectMany(r=>WorkOrderExecutionEndpoints.Content(r).Evidence.Spares.Where(s=>s.SpareId!=null).Select(s=>s.SpareId!.Value).Distinct())
            .GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count());
    }
    private static async Task<string> EquipmentCriticality(PoseidonDbContext db,Guid vessel,Guid? asset,CancellationToken ct) {
        if(asset==null)return "UNKNOWN";
        var value=await db.Assets.AsNoTracking().Where(a=>a.Id==asset && a.VesselId==vessel).Select(a=>a.Criticality).SingleOrDefaultAsync(ct);
        // Free-text declaration from the equipment register; no criticality catalog is inferred.
        return string.IsNullOrWhiteSpace(value) || value=="Unknown" || value.Length>64 ? "UNKNOWN" : value;
    }
    private static async Task<CriticalSpareDto[]> Project(HttpContext h,PoseidonDbContext db,TrustedActor a,Guid vessel,CriticalSpareRevision[] rows,Func<CriticalSpareRevision,bool> current) {
        if(rows.Length==0)return [];var ct=h.RequestAborted;
        var consumption=await Consumption(db,vessel,ct);var demand=await Demand(db,vessel,ct);
        var ids=rows.Select(r=>r.SpareId).Distinct().ToArray();
        var open=await db.TechnicalRequisitions.AsNoTracking().Where(r=>r.VesselId==vessel && r.State=="PROPOSED" && ids.Contains(r.SpareId)).ToDictionaryAsync(r=>r.SpareId,r=>r.Id,ct);
        var result=new List<CriticalSpareDto>();
        foreach(var row in rows) {
            var c=Content(row);var stock=CriticalSparePolicy.Project(c,consumption==null?null:consumption.GetValueOrDefault(row.SpareId)??[]);
            result.Add(new(row.SpareId,row.VesselId,row.Revision,checked((uint)row.RecordVersion),c.Definition,c.Count,stock,await EquipmentCriticality(db,vessel,c.Definition.AssetId,ct),
                demand.GetValueOrDefault(row.SpareId),open.TryGetValue(row.SpareId,out var o)?o:null,row.Transition,row.SourceVersion,row.Hash,row.SavedAt,row.Provenance,
                current(row),current(row) && CanWrite(a,vessel)));
        }
        return result.ToArray();
    }
    private static TechnicalRequisitionDto Dto(TechnicalRequisitionRevision row,CriticalSpareContent spare,TrustedActor a,bool current,HttpContext? h) {
        var c=Content(row);var d=spare.Definition;
        return new(row.RequisitionId,row.VesselId,row.SpareId,row.SpareRevision,row.Revision,checked((uint)row.RecordVersion),c.State,c.ProposedQuantity,c.BasisState,c.BasisProjected,
            c.BasisMinimum,c.BasisMaximum,c.SpareCriticality,c.EquipmentCriticality,c.OpenWorkOrderDemand,c.ProposedAt,c.ApprovedQuantity,c.DecidedAt,c.ReasonCode,
            c.ProposedBy==a.Id,c.DecidedBy==a.Id,d.PartNumber,d.Name,d.Unit,
            c.State=="APPROVED_FOR_ERP_EXPORT"?new("TechnicalRequisition",row.RequisitionId,row.VesselId,d.PartNumber,d.Unit,c.ApprovedQuantity!,ErpReferenceBoundary.SourceVersion,CriticalSparePolicy.ErpBoundary):null,
            row.Transition,row.Hash,row.SavedAt,current,current && c.State=="PROPOSED" && CanApprove(h,a,row.VesselId),CriticalSparePolicy.ErpBoundary);
    }
    private static async Task<bool> Fence(HttpContext h,Guid vessel,string rights,Func<PoseidonDbContext,Task<bool>> exact) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();
        var a=resolver.Resolve(h.User);if(!CanRead(a) || WorkOrderExecutionEndpoints.Rights(a!)!=rights)return false;
        PoseidonDbContext db;try{db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=a!.TenantId || !await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted) || !await exact(db))return false;
        var live=resolver.Resolve(h.User);return CanRead(live) && WorkOrderExecutionEndpoints.Rights(live!)==rights;
    }
    private static Func<PoseidonDbContext,Task<bool>> Spares(HttpContext h,Guid vessel,CriticalSpareDto[] rows)=>async db=>{
        foreach(var r in rows)if(!await db.CriticalSpareRevisions.AsNoTracking().AnyAsync(x=>x.VesselId==vessel && x.SpareId==r.Id && x.Revision==r.Revision && x.Hash==r.Hash,h.RequestAborted) ||
            r.CurrentRevision && !await db.CriticalSpares.AnyAsync(x=>x.VesselId==vessel && x.Id==r.Id && x.Revision==r.Revision,h.RequestAborted))return false;
        return true;};
    private static Func<PoseidonDbContext,Task<bool>> Requests(HttpContext h,Guid vessel,TechnicalRequisitionDto[] rows)=>async db=>{
        foreach(var r in rows)if(!await db.TechnicalRequisitionRevisions.AsNoTracking().AnyAsync(x=>x.VesselId==vessel && x.RequisitionId==r.Id && x.Revision==r.Revision && x.Hash==r.Hash,h.RequestAborted) ||
            r.CurrentRevision && !await db.TechnicalRequisitions.AnyAsync(x=>x.VesselId==vessel && x.Id==r.Id && x.Revision==r.Revision,h.RequestAborted))return false;
        return true;};
    private static async Task<(TrustedActor? Actor,Guid Vessel,int Offset,IResult? Error)> Begin(HttpContext h,PoseidonDbContext db,string action,bool paging) {
        h.Response.Headers.CacheControl="no-store";
        Guid vessel=Guid.Empty;var offset=0;
        if(!h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vesselId" || paging && q.Key=="offset")) || !Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) || vessel==Guid.Empty ||
            h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) || offset>100000))
            return (null,vessel,0,await ReadFail(h,action,400,"invalid_query"));
        var a=WorkOrderExecutionEndpoints.Actor(h);if(!CanRead(a))return (null,vessel,0,await ReadFail(h,action,403,"permission_denied"));
        if(!await a!.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return (null,vessel,0,await ReadFail(h,action,404,"resource_not_found"));
        return (a,vessel,offset,null);
    }
    private static async Task<IResult> Ok(HttpContext h,string action,Guid vessel,string rights,Func<PoseidonDbContext,Task<bool>> exact,object result,Guid? resource) {
        if(!await Fence(h,vessel,rights,exact))return await ReadFail(h,action,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,action,"Success",resourceId:resource??vessel);
        if(!await Fence(h,vessel,rights,exact))return await ReadFail(h,action,403,"permission_denied");
        return Results.Ok(result);
    }
    private static async Task<IResult> List(HttpContext h,PoseidonDbContext db) {
        var (a,vessel,offset,error)=await Begin(h,db,"CriticalSpareRead",true);if(error!=null)return error;
        var rows=await CurrentRevisions(db,vessel).OrderBy(r=>r.SpareId).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        var items=await Project(h,db,a!,vessel,rows.Take(25).ToArray(),_=>true);
        // Alert counts summarize the returned page only; UNKNOWN/UNCONFIGURED are review-required, never healthy.
        var page=new CriticalSparePageDto(items,offset,25,rows.Length>25,items.Count(i=>i.Stock.State=="LOW_STOCK"),items.Count(i=>i.Stock.State is not("LOW_STOCK" or "ABOVE_MINIMUM")),
            CanWrite(a,vessel),CanPropose(h,a,vessel),CanApprove(h,a,vessel),CriticalSparePolicy.Criticalities,CriticalSparePolicy.RejectReasons,CriticalSparePolicy.Qualification,
            CriticalSparePolicy.ErpBoundary,CriticalSparePolicy.SourceVersion);
        return await Ok(h,"CriticalSpareRead",vessel,WorkOrderExecutionEndpoints.Rights(a!),Spares(h,vessel,items),page,null);
    }
    private static async Task<IResult> Detail(Guid id,HttpContext h,PoseidonDbContext db) {
        var (a,vessel,_,error)=await Begin(h,db,"CriticalSpareRead",false);if(error!=null)return error;
        var row=await CurrentRevisions(db,vessel).SingleOrDefaultAsync(r=>r.SpareId==id,h.RequestAborted);if(row==null)return await ReadFail(h,"CriticalSpareRead",404,"resource_not_found");
        var items=await Project(h,db,a!,vessel,[row],_=>true);
        return await Ok(h,"CriticalSpareRead",vessel,WorkOrderExecutionEndpoints.Rights(a!),Spares(h,vessel,items),items[0],id);
    }
    private static async Task<IResult> Requisitions(HttpContext h,PoseidonDbContext db) {
        var (a,vessel,offset,error)=await Begin(h,db,"TechnicalRequisitionRead",true);if(error!=null)return error;
        var rows=await CurrentRequisitions(db,vessel).OrderByDescending(r=>r.SavedAt).ThenBy(r=>r.RequisitionId).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        var items=new List<TechnicalRequisitionDto>();
        foreach(var row in rows.Take(25)) {
            var spare=await db.CriticalSpareRevisions.AsNoTracking().SingleAsync(s=>s.VesselId==vessel && s.SpareId==row.SpareId && s.Revision==row.SpareRevision,h.RequestAborted);
            items.Add(Dto(row,Content(spare),a!,true,h));
        }
        var records=items.ToArray();
        return await Ok(h,"TechnicalRequisitionRead",vessel,WorkOrderExecutionEndpoints.Rights(a!),Requests(h,vessel,records),new TechnicalRequisitionPageDto(records,offset,25,rows.Length>25),null);
    }

    private sealed record Envelope(TrustedActor Actor,Guid Vessel,string Key,string Rights,string AuditAction);
    private static async Task<(Envelope? Env,IResult? Error)> Command(HttpContext h,string audit,Func<TrustedActor,Guid,bool> allowed,bool human) {
        h.Response.Headers.CacheControl="no-store";var a=WorkOrderExecutionEndpoints.Actor(h);Guid vessel=Guid.Empty;
        if(!CanRead(a) || !a!.HasWriteAttribution || !h.Request.Query.All(q=>q.Value.Count==1 && q.Key=="vesselId") || !Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) || vessel==Guid.Empty)
            return (null,await Fail(h,audit,403,"permission_denied"));
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return (null,await Fail(h,audit,403,"csrf_invalid"));}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;
        if(key==null || !Guid.TryParseExact(key,"D",out _))return (null,await Fail(h,audit,400,"invalid_idempotency_key"));
        if(human && !CertificateRenewalEndpoints.Human(h,a))return (null,await Fail(h,audit,403,"human_actor_required"));
        if(!allowed(a,vessel))return (null,await Fail(h,audit,403,"permission_denied"));
        if(!await a.Scope(h.RequestServices.GetRequiredService<PoseidonDbContext>().Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return (null,await Fail(h,audit,404,"resource_not_found"));
        return (new(a,vessel,key,WorkOrderExecutionEndpoints.Rights(a),audit),null);
    }
    private static async Task<IResult> Outcome(HttpContext h,Envelope e,Guid resource,string json,Func<PoseidonDbContext,Task<bool>> exact) {
        try {
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,e.AuditAction,"Success",resourceId:resource);
            if(!await Fence(h,e.Vessel,e.Rights,exact))return await Fail(h,e.AuditAction,503,"command_outcome_withheld");
            return Results.Content(json,"application/json");
        }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
    }
    private static async Task<IResult?> Replay(HttpContext h,PoseidonDbContext db,Envelope e,string action,Guid id,string fingerprint) {
        if(!await Fence(h,e.Vessel,e.Rights,_=>Task.FromResult(true)))return await Fail(h,e.AuditAction,403,"permission_denied");
        var receipt=await db.TechnicalSupplyReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==e.Actor.Id && r.Key==e.Key,h.RequestAborted);if(receipt==null)return null;
        if(receipt.Action!=action || receipt.VesselId!=e.Vessel || receipt.ResourceId!=id || receipt.Fingerprint!=fingerprint)return await Fail(h,e.AuditAction,409,"idempotency_key_reused");
        return await Outcome(h,e,id,receipt.OutcomeJson,action.StartsWith("CriticalSpare",StringComparison.Ordinal)
            ? Spares(h,e.Vessel,[JsonSerializer.Deserialize<CriticalSpareDto>(receipt.OutcomeJson,Json)!])
            : Requests(h,e.Vessel,[JsonSerializer.Deserialize<TechnicalRequisitionDto>(receipt.OutcomeJson,Json)!]));
    }
    // One serializable apply for both aggregates. The mutation adds the head/revision and returns the outcome.
    private delegate Task<(string? Json,long Revision,long RecordVersion,Func<PoseidonDbContext,Task<bool>>? Exact,IResult? Error)> Mutation(DateTimeOffset now);
    private static async Task<IResult> Apply(HttpContext h,PoseidonDbContext db,Envelope e,string action,Guid id,string fingerprint,Mutation mutate) {
        var replay=await Replay(h,db,e,action,id,fingerprint);if(replay!=null)return replay;
        IDbContextTransaction? tx=null;string? outcome=null;Func<PoseidonDbContext,Task<bool>>? exact=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);var now=WorkOrderExecutionEndpoints.Now(h);
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,e.AuditAction,"Attempted",resourceId:id);
            var (json,revision,version,check,error)=await mutate(now);
            if(error!=null){await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return (error is IStatusCodeHttpResult{StatusCode:409}?await Replay(h,db,e,action,id,fingerprint):null)??error;}
            outcome=json;exact=check;
            db.TechnicalSupplyReceipts.Add(new(){TenantId=e.Actor.TenantId,VesselId=e.Vessel,ActorId=e.Actor.Id,Key=e.Key,ResourceId=id,Action=action,Revision=revision,RecordVersion=version,
                Fingerprint=fingerprint,OutcomeJson=json!,SavedAt=now});
            await db.SaveChangesAsync(h.RequestAborted);
            if(!await Fence(h,e.Vessel,e.Rights,_=>Task.FromResult(true)))return await Fail(h,e.AuditAction,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay(h,db,e,action,id,fingerprint)??await Fail(h,e.AuditAction,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
            var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";
            return await Replay(h,db,e,action,id,fingerprint)??await Fail(h,e.AuditAction,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(h,e,id,outcome!,exact!);
    }

    private static async Task<IResult> Save(Guid? routeId,HttpContext h,PoseidonDbContext db) {
        var (env,error)=await Command(h,"CriticalSpareCommand",CanWrite,false);if(error!=null)return error;var e=env!;
        CriticalSpareSaveCommand command;
        try {
            command=await WorkOrderExecutionEndpoints.Body<CriticalSpareSaveCommand>(h,32768);
            if(command.Id==Guid.Empty || routeId!=null && routeId!=command.Id || (routeId==null ? command.ExpectedVersion!=0 || command.ExpectedRevision!=0 : command.ExpectedVersion==0 || command.ExpectedRevision<=0) ||
                !CriticalSparePolicy.ValidDefinition(command.Definition) || !CriticalSparePolicy.ValidCount(command.Count,WorkOrderExecutionEndpoints.Now(h)))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,e.AuditAction,400,"validation_failed");}
        var action=routeId==null?"CriticalSpareCreate":"CriticalSpareUpdate";
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,routeId,command},Json));
        return await Apply(h,db,e,action,command.Id,fingerprint,async now=> {
            var head=await db.CriticalSpares.SingleOrDefaultAsync(x=>x.Id==command.Id,h.RequestAborted);
            if(head!=null && head.VesselId!=e.Vessel)return (null,0,0,null,await Fail(h,e.AuditAction,404,"resource_not_found"));
            if(routeId==null ? head!=null : head==null)return (null,0,0,null,await Fail(h,e.AuditAction,routeId==null?409:404,routeId==null?"version_conflict":"resource_not_found"));
            if(head!=null && (head.Version!=command.ExpectedVersion || head.Revision!=command.ExpectedRevision))return (null,0,0,null,await Fail(h,e.AuditAction,409,"version_conflict"));
            if(command.Definition.AssetId is {} asset && !(e.Actor.Permissions.Contains("equipment.read") &&
                await db.Assets.AsNoTracking().AnyAsync(x=>x.Id==asset && x.VesselId==e.Vessel,h.RequestAborted)))return (null,0,0,null,await Fail(h,e.AuditAction,403,"linked_resource_unavailable"));
            if(head==null){head=new(){TenantId=e.Actor.TenantId,VesselId=e.Vessel,Id=command.Id};db.CriticalSpares.Add(head);}else head.Revision++;
            await db.SaveChangesAsync(h.RequestAborted);
            var json=JsonSerializer.Serialize(new CriticalSpareContent(command.Definition,command.Count),Json);var transition=routeId==null?"CREATE":"UPDATE";
            var row=new CriticalSpareRevision{TenantId=e.Actor.TenantId,VesselId=e.Vessel,SpareId=head.Id,Revision=head.Revision,RecordVersion=head.Version,Transition=transition,
                AssetId=command.Definition.AssetId,ContentJson=json,SourceVersion=CriticalSparePolicy.SourceVersion,ActorId=e.Actor.Id,SavedAt=now,
                Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,head.Id,head.Revision,transition,json},Json))};
            db.CriticalSpareRevisions.Add(row);await db.SaveChangesAsync(h.RequestAborted);
            var dto=(await Project(h,db,e.Actor,e.Vessel,[row],_=>false))[0];
            return (JsonSerializer.Serialize(dto,Json),row.Revision,row.RecordVersion,Spares(h,e.Vessel,[dto]),null);
        });
    }
    private static async Task<IResult> Propose(HttpContext h,PoseidonDbContext db) {
        var (env,error)=await Command(h,"TechnicalRequisitionCommand",(a,v)=>CanPropose(h,a,v),true);if(error!=null)return error;var e=env!;
        TechnicalRequisitionProposeCommand command;
        try {
            command=await WorkOrderExecutionEndpoints.Body<TechnicalRequisitionProposeCommand>(h,4096);
            if(command.Id==Guid.Empty || command.SpareId==Guid.Empty || command.SpareRevision<=0)throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,e.AuditAction,400,"validation_failed");}
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,command},Json));
        return await Apply(h,db,e,"TechnicalRequisitionPropose",command.Id,fingerprint,async now=> {
            if(await db.TechnicalRequisitions.AnyAsync(x=>x.Id==command.Id,h.RequestAborted))return (null,0,0,null,await Fail(h,e.AuditAction,409,"version_conflict"));
            var spare=await CurrentRevisions(db,e.Vessel).SingleOrDefaultAsync(r=>r.SpareId==command.SpareId,h.RequestAborted);
            if(spare==null || spare.Revision!=command.SpareRevision)return (null,0,0,null,await Fail(h,e.AuditAction,409,"source_revision_changed"));
            if(await db.TechnicalRequisitions.AnyAsync(x=>x.VesselId==e.Vessel && x.SpareId==command.SpareId && x.State=="PROPOSED",h.RequestAborted))
                return (null,0,0,null,await Fail(h,e.AuditAction,409,"requisition_already_open"));
            var content=Content(spare);var consumption=await Consumption(db,e.Vessel,h.RequestAborted);
            var projection=CriticalSparePolicy.Project(content,consumption==null?null:consumption.GetValueOrDefault(command.SpareId)??[]);
            var (proposal,problem)=CriticalSparePolicy.Propose(command.SpareId,spare.Revision,content,projection,await EquipmentCriticality(db,e.Vessel,content.Definition.AssetId,h.RequestAborted),
                (await Demand(db,e.Vessel,h.RequestAborted)).GetValueOrDefault(command.SpareId),e.Actor.Id,now);
            if(problem!=null)return (null,0,0,null,await Fail(h,e.AuditAction,422,problem));
            var head=new TechnicalRequisition{TenantId=e.Actor.TenantId,VesselId=e.Vessel,Id=command.Id,SpareId=command.SpareId,State="PROPOSED"};db.TechnicalRequisitions.Add(head);
            await db.SaveChangesAsync(h.RequestAborted);
            var row=Revision(e,head,proposal!,spare.Revision,"PROPOSE",now);db.TechnicalRequisitionRevisions.Add(row);
            var dto=Dto(row,content,e.Actor,false,h);
            return (JsonSerializer.Serialize(dto,Json),row.Revision,row.RecordVersion,Requests(h,e.Vessel,[dto]),null);
        });
    }
    private static TechnicalRequisitionRevision Revision(Envelope e,TechnicalRequisition head,TechnicalRequisitionContent content,long spareRevision,string transition,DateTimeOffset now) {
        var json=JsonSerializer.Serialize(content,Json);
        return new(){TenantId=e.Actor.TenantId,VesselId=e.Vessel,RequisitionId=head.Id,SpareId=head.SpareId,SpareRevision=spareRevision,Revision=head.Revision,RecordVersion=head.Version,
            Transition=transition,State=content.State,ContentJson=json,SourceVersion=CriticalSparePolicy.SourceVersion,ActorId=e.Actor.Id,SavedAt=now,
            Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,head.Id,head.Revision,transition,json},Json))};
    }
    private static async Task<IResult> Decide(Guid id,HttpContext h,PoseidonDbContext db) {
        var (env,error)=await Command(h,"TechnicalRequisitionCommand",(a,v)=>CanApprove(h,a,v),true);if(error!=null)return error;var e=env!;
        TechnicalRequisitionDecisionCommand command;
        try {
            command=await WorkOrderExecutionEndpoints.Body<TechnicalRequisitionDecisionCommand>(h,4096);
            if(command.Id!=id || command.ExpectedVersion==0 || command.ExpectedRevision<=0)throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,e.AuditAction,400,"validation_failed");}
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,id,command},Json));
        return await Apply(h,db,e,"TechnicalRequisitionDecide",id,fingerprint,async now=> {
            var head=await db.TechnicalRequisitions.SingleOrDefaultAsync(x=>x.Id==id,h.RequestAborted);
            if(head==null || head.VesselId!=e.Vessel)return (null,0,0,null,await Fail(h,e.AuditAction,404,"resource_not_found"));
            if(head.Version!=command.ExpectedVersion || head.Revision!=command.ExpectedRevision)return (null,0,0,null,await Fail(h,e.AuditAction,409,"version_conflict"));
            var current=await db.TechnicalRequisitionRevisions.AsNoTracking().SingleAsync(r=>r.RequisitionId==id && r.Revision==head.Revision,h.RequestAborted);
            var (next,status,problem)=CriticalSparePolicy.Decide(Content(current),command.Decision,e.Actor.Id,now,command.ApprovedQuantity,command.ReasonCode);
            if(problem!=null)return (null,0,0,null,await Fail(h,e.AuditAction,status,problem));
            head.Revision++;head.State=next!.State;await db.SaveChangesAsync(h.RequestAborted);
            var row=Revision(e,head,next,current.SpareRevision,command.Decision,now);db.TechnicalRequisitionRevisions.Add(row);
            var spare=await db.CriticalSpareRevisions.AsNoTracking().SingleAsync(s=>s.VesselId==e.Vessel && s.SpareId==head.SpareId && s.Revision==current.SpareRevision,h.RequestAborted);
            var dto=Dto(row,Content(spare),e.Actor,false,h);
            return (JsonSerializer.Serialize(dto,Json),row.Revision,row.RecordVersion,Requests(h,e.Vessel,[dto]),null);
        });
    }
}
