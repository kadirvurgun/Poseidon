using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DowntimeOriginFieldDto(string Key,string Type,DowntimeFact Value,int SourceRow,string? Unit);
public sealed record DowntimeOriginDto(Guid VesselId,DowntimeOriginPin Pin,bool Current,string State,string Qualification,
    string Provenance,DateTimeOffset SavedAt,DowntimeOriginFieldDto[] Fields,string Href);

// Engineering lineage only. Generic DD linked_* references are intentionally not
// promoted to this typed owning context, and no source declaration grants closure.
internal static class DowntimeOriginReader
{
    internal static bool CanRead(TrustedActor? actor,string kind)=>kind switch{
        "Defect"=>DefectEndpoints.CanRead(actor),"WorkOrder"=>WorkOrderEndpoints.CanRead(actor),
        "Engine"=>DailyOperationsEndpoints.CanRead(actor,"Engine"),_=>false};
    internal static string[] Kinds(TrustedActor actor)=>new[]{"Defect","WorkOrder","Engine"}.Where(k=>CanRead(actor,k)).ToArray();
    private static string Source(string kind)=>kind switch{"Defect"=>DefectPolicy.SourceVersion,"WorkOrder"=>WorkOrderPolicy.SourceVersion,"Engine"=>DailyOperationsPolicy.SourceVersion,_=>""};
    internal static bool Valid(DowntimeOriginPin? pin)=>pin is {Kind:"Defect" or "WorkOrder" or "Engine",Revision:>0,Version:>0,Hash.Length:64} &&
        pin.Id!=Guid.Empty && pin.SourceVersion==Source(pin.Kind) && pin.Hash.All(char.IsAsciiHexDigit);
    private sealed class Replace(ParameterExpression from,ParameterExpression to):ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node)=>node==from?to:base.VisitParameter(node);
    }
    private static Expression<Func<T,bool>> And<T>(Expression<Func<T,bool>> a,Expression<Func<T,bool>> b)=>Expression.Lambda<Func<T,bool>>(
        Expression.AndAlso(a.Body,new Replace(b.Parameters[0],a.Parameters[0]).Visit(b.Body)!),a.Parameters);
    private static DefectAssetPin? Asset(Guid? id,long? rev,long? version,string? hash)=>id==null?null:new(id.Value,rev!.Value,checked((uint)version!.Value),hash!);
    private static WorkOrderPlanPin? Plan(Guid? id,long? rev,long? version,string? hash,string? source)=>id==null?null:new(id.Value,rev!.Value,checked((uint)version!.Value),hash!,source!);

    // Observe the actual current equipment metadata as well as the immutable
    // historical equipment snapshot already pinned by the owning source reader.
    // This is authorization composition, never a claim that the old source is current.
    private static async Task<Expression<Func<Vessel,bool>>?> CurrentAssetFence(PoseidonDbContext db,Guid vessel,IEnumerable<Guid> ids,CancellationToken ct) {
        Expression<Func<Vessel,bool>> fence=v=>true;
        foreach(var id in ids.Distinct()) {
            var asset=await db.Assets.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id && a.VesselId==vessel,ct);if(asset==null)return null;
            var raw=await db.AssetRevisions.AsNoTracking().Where(a=>a.AssetId==id && a.VesselId==vessel && a.Revision==asset.Revision).Select(a=>a.ContentJson).SingleOrDefaultAsync(ct);if(raw==null)return null;
            Expression<Func<Vessel,bool>> exact=v=>db.AssetRevisions.Any(a=>a.VesselId==vessel && a.AssetId==id && a.Revision==asset.Revision && a.ContentJson==raw) &&
                db.Assets.Any(a=>a.Id==id && a.VesselId==vessel && a.Revision==asset.Revision && a.Version==asset.Version);
            fence=And(fence,exact);
        }
        return fence;
    }
    private static async Task<Expression<Func<Vessel,bool>>?> CurrentPlanFence(PoseidonDbContext db,Guid vessel,WorkOrderPlanPin[] pins,CancellationToken ct) {
        Expression<Func<Vessel,bool>> fence=v=>true;
        foreach(var id in pins.Select(p=>p.Id).Distinct()) {
            var head=await db.MaintenancePlans.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==id && p.VesselId==vessel,ct);if(head==null)return null;
            var revisions=pins.Where(p=>p.Id==id).Select(p=>p.Revision).Distinct().ToArray();
            var assets=await db.MaintenancePlanRevisions.AsNoTracking().Where(p=>p.PlanId==id && p.VesselId==vessel && revisions.Contains(p.Revision)).Select(p=>p.AssetId).ToArrayAsync(ct);
            var nested=await CurrentAssetFence(db,vessel,assets.Append(head.AssetId),ct);if(nested==null)return null;
            Expression<Func<Vessel,bool>> exact=v=>db.MaintenancePlans.Any(p=>p.Id==id && p.VesselId==vessel && p.Revision==head.Revision && p.Version==head.Version &&
                p.AssetId==head.AssetId && p.ContentJson==head.ContentJson && p.Status==head.Status && p.Provenance==head.Provenance && p.OwnerRoleId==head.OwnerRoleId && p.JobId==head.JobId && p.ObservationId==head.ObservationId);
            fence=And(And(fence,exact),nested);
        }
        return fence;
    }
    // Each requested immutable source AND its freshly captured current source head
    // contributes to one expression. Nested asset/plan contexts from BOTH snapshots
    // are composed into that same final SQL; awaiting two boolean fences is unsafe.
    internal static async Task<Expression<Func<Vessel,bool>>?> CaptureFence(PoseidonDbContext db,TrustedActor actor,Guid vessel,
        DowntimeOriginPin[] pins,bool currentOnly=false,CancellationToken ct=default) {
        Expression<Func<Vessel,bool>> fence=v=>true;
        if(pins.Length>128 || pins.Any(p=>!Valid(p) || !CanRead(actor,p.Kind)))return null;
        foreach(var pin in pins.Distinct()) {
            if(pin.Kind=="Defect") {
                var row=await db.DefectRevisions.AsNoTracking().SingleOrDefaultAsync(r=>r.VesselId==vessel && r.DefectId==pin.Id && r.Revision==pin.Revision && r.RecordVersion==pin.Version && r.Hash==pin.Hash && r.SourceVersion==pin.SourceVersion,ct);
                var head=await db.Defects.AsNoTracking().SingleOrDefaultAsync(r=>r.VesselId==vessel && r.Id==pin.Id,ct);if(row==null || head==null || head.SourceVersion!=Source(pin.Kind))return null;
                var assets=new[]{Asset(row.AssetId,row.AssetRevision,row.AssetVersion,row.AssetHash),Asset(head.AssetId,head.AssetRevision,head.AssetVersion,head.AssetHash)}.Where(p=>p!=null).Cast<DefectAssetPin>().Distinct().ToArray();
                var nested=await DefectAssetContextReader.CaptureFence(db,actor,vessel,assets,false,ct);if(nested==null)return null;
                var currentAssets=await CurrentAssetFence(db,vessel,assets.Select(p=>p.Id),ct);if(currentAssets==null)return null;nested=And(nested,currentAssets);
                Expression<Func<Vessel,bool>> match=v=>db.DefectRevisions.Any(r=>r.VesselId==vessel && r.DefectId==pin.Id && r.Revision==pin.Revision && r.RecordVersion==pin.Version && r.Hash==pin.Hash && r.SourceVersion==pin.SourceVersion) &&
                    db.Defects.Any(h=>h.VesselId==vessel && h.Id==head.Id && h.Revision==head.Revision && h.Version==head.Version && h.Hash==head.Hash && h.SourceVersion==head.SourceVersion &&
                        h.AssetId==head.AssetId && h.AssetRevision==head.AssetRevision && h.AssetVersion==head.AssetVersion && h.AssetHash==head.AssetHash &&
                        (!currentOnly || h.Revision==pin.Revision && h.Version==pin.Version && h.Hash==pin.Hash));
                fence=And(And(fence,match),nested);
            }else if(pin.Kind=="WorkOrder") {
                var row=await db.WorkOrderRevisions.AsNoTracking().SingleOrDefaultAsync(r=>r.VesselId==vessel && r.WorkOrderId==pin.Id && r.Revision==pin.Revision && r.RecordVersion==pin.Version && r.Hash==pin.Hash && r.SourceVersion==pin.SourceVersion,ct);
                var head=await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(r=>r.VesselId==vessel && r.Id==pin.Id,ct);if(row==null || head==null || head.SourceVersion!=Source(pin.Kind))return null;
                var plans=new[]{Plan(row.PlanId,row.PlanRevision,row.PlanVersion,row.PlanHash,row.PlanSourceVersion),Plan(head.PlanId,head.PlanRevision,head.PlanVersion,head.PlanHash,head.PlanSourceVersion)}.Where(p=>p!=null).Cast<WorkOrderPlanPin>().Distinct().ToArray();
                var nested=await WorkOrderPlanOriginReader.CaptureFence(db,actor,vessel,plans,false,ct);if(nested==null)return null;
                var currentPlans=await CurrentPlanFence(db,vessel,plans,ct);if(currentPlans==null)return null;nested=And(nested,currentPlans);
                Expression<Func<Vessel,bool>> match=v=>db.WorkOrderRevisions.Any(r=>r.VesselId==vessel && r.WorkOrderId==pin.Id && r.Revision==pin.Revision && r.RecordVersion==pin.Version && r.Hash==pin.Hash && r.SourceVersion==pin.SourceVersion) &&
                    db.WorkOrders.Any(h=>h.VesselId==vessel && h.Id==head.Id && h.Revision==head.Revision && h.Version==head.Version && h.Hash==head.Hash && h.SourceVersion==head.SourceVersion &&
                        h.PlanId==head.PlanId && h.PlanRevision==head.PlanRevision && h.PlanVersion==head.PlanVersion && h.PlanHash==head.PlanHash && h.PlanSourceVersion==head.PlanSourceVersion &&
                        (!currentOnly || h.Revision==pin.Revision && h.Version==pin.Version && h.Hash==pin.Hash));
                fence=And(And(fence,match),nested);
            }else {
                var visible=DailyOperationOriginReader.ReturnedVisible(db,actor,vessel,[new("Engine",pin.Id,pin.Revision,pin.Version,pin.Hash,pin.SourceVersion)],currentOnly);
                var head=await db.DailyOperations.AsNoTracking().SingleOrDefaultAsync(h=>h.Id==pin.Id && h.VesselId==vessel && h.Kind=="Engine",ct);
                if(head==null || head.SourceVersion!=Source(pin.Kind))return null;
                Expression<Func<Vessel,bool>> match=v=>visible.Any() && db.DailyOperations.Any(h=>h.Id==head.Id && h.VesselId==vessel && h.Kind=="Engine" &&
                    h.Revision==head.Revision && h.Version==head.Version && h.Hash==head.Hash && h.SourceVersion==head.SourceVersion);
                fence=And(fence,match);
            }
        }
        return fence;
    }
    internal static void MapDowntimeOrigins(this IEndpointRouteBuilder app)=>app.MapGet("/api/v1/vessels/{vessel:guid}/downtime-origins/{kind}/{id:guid}",Read)
        .RequireAuthorization("DirectoryRead").WithName("DowntimeOriginRead").Produces<DowntimeOriginDto>();
    private static Task Audit(HttpContext h,string code,Guid? id=null)=>h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"DowntimeOriginRead",code,resourceId:id);
    private static async Task<IResult> Fail(HttpContext h,int status,string code){await Audit(h,code);return Results.Json(new{code},statusCode:status);}
    private static async Task<bool> Current(HttpContext h,Guid vessel,string rights,DowntimeOriginPin pin) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var actor=resolver.Resolve(h.User);
        if(!OperationalDowntimeEndpoints.CanRead(actor) || !CanRead(actor,pin.Kind) || DailyOperationsEndpoints.Rights(actor!)!=rights)return false;
        var db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();if(db.TenantId!=actor!.TenantId)return false;
        var fence=await CaptureFence(db,actor,vessel,[pin],true,h.RequestAborted);if(fence==null)return false;
        var found=await actor.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==vessel && v.Kind==VesselKind.Ship).Where(fence).AnyAsync(h.RequestAborted);
        var live=resolver.Resolve(h.User);return found && OperationalDowntimeEndpoints.CanRead(live) && CanRead(live,pin.Kind) && DailyOperationsEndpoints.Rights(live!)==rights;
    }
    private static async Task<IResult> Read(Guid vessel,string kind,Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";long revision=0;
        if(vessel==Guid.Empty || id==Guid.Empty || kind is not("Defect" or "WorkOrder" or "Engine") || h.Request.Query.Any(q=>q.Key!="revision" || q.Value.Count!=1) ||
            h.Request.Query.ContainsKey("revision") && (!long.TryParse(h.Request.Query["revision"],NumberStyles.None,CultureInfo.InvariantCulture,out revision) || revision<=0))return await Fail(h,400,"invalid_query");
        var actor=h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if(!OperationalDowntimeEndpoints.CanRead(actor) || !CanRead(actor,kind))return await Fail(h,403,"permission_denied");var rights=DailyOperationsEndpoints.Rights(actor!);
        DowntimeOriginDto? result=null;
        if(kind=="Defect") {
            var row=await db.Defects.AsNoTracking().SingleOrDefaultAsync(r=>r.VesselId==vessel && r.Id==id,h.RequestAborted);
            if(row!=null) {
                var fields=DefectPolicy.ReadFields(row.FieldsJson);string[] keys=["record_datetime","criticality","failure_consequence","restriction","temporary_measure"];
                result=new(vessel,new(kind,id,row.Revision,row.Version,row.Hash,row.SourceVersion),true,"DECLARATION","REVIEW_REQUIRED",row.Provenance,row.SavedAt,
                    DefectPolicy.Schema().Where(f=>keys.Contains(f.Key)).Select(f=>new DowntimeOriginFieldDto(f.Key,f.Type,new(fields[f.Key].State,fields[f.Key].Value),int.Parse(f.SourceLocator[(f.SourceLocator.LastIndexOf(':')+1)..],CultureInfo.InvariantCulture),null)).ToArray(),$"/app/defects/{vessel:D}?id={id:D}");
            }
        }else if(kind=="WorkOrder") {
            var row=await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(r=>r.VesselId==vessel && r.Id==id,h.RequestAborted);
            if(row!=null) {
                var fields=WorkOrderPolicy.ReadFields(row.FieldsJson);string[] keys=["work_order_type","priority","planned_start","planned_finish","work_done","findings","execution_stage"];
                result=new(vessel,new(kind,id,row.Revision,row.Version,row.Hash,row.SourceVersion),true,"DRAFT","REVIEW_REQUIRED",row.Provenance,row.SavedAt,
                    WorkOrderPolicy.Schema().Where(f=>keys.Contains(f.Key)).Select(f=>new DowntimeOriginFieldDto(f.Key,f.Type,new(fields[f.Key].State,fields[f.Key].Value),int.Parse(f.SourceLocator[(f.SourceLocator.LastIndexOf(':')+1)..],CultureInfo.InvariantCulture),null)).ToArray(),$"/app/work-order-center/{vessel:D}?id={id:D}");
            }
        }else {
            var row=await db.DailyOperations.AsNoTracking().SingleOrDefaultAsync(r=>r.VesselId==vessel && r.Id==id && r.Kind=="Engine",h.RequestAborted);
            if(row!=null) {
                var fields=DailyOperationsPolicy.ReadFields("Engine",vessel,row.FieldsJson);string[] keys=["log_date","watch_period","defects","alarms","handover_note"];
                result=new(vessel,new(kind,id,row.Revision,row.Version,row.Hash,row.SourceVersion),true,"DRAFT","REVIEW_REQUIRED",row.Provenance,row.SavedAt,
                    DailyOperationsPolicy.Schema("Engine").Where(f=>keys.Contains(f.Key)).Select(f=>new DowntimeOriginFieldDto(f.Key,f.Type,new(fields[f.Key].State,fields[f.Key].Value),f.SourceRow,f.Unit)).ToArray(),$"/app/engine-daily-log/{vessel:D}?id={id:D}");
            }
        }
        if(result==null)return await Fail(h,404,"resource_not_found");if(revision>0 && revision!=result.Pin.Revision)return await Fail(h,409,"origin_context_stale");
        if(!Valid(result.Pin))return await Fail(h,503,"source_contract_changed");if(!await Current(h,vessel,rights,result.Pin))return await Fail(h,403,"permission_denied");
        await Audit(h,"Success",id);if(!await Current(h,vessel,rights,result.Pin))return await Fail(h,403,"permission_denied");return Results.Ok(result);
    }
}
