using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record WorkOrderPlanSummaryDto(Guid AssetId,long AssetRevision,Guid? JobId,string Status);
public sealed record WorkOrderAssetSummaryDto(string? Code,string? Name,string Level,string? Criticality,string? Maker,string? Model,string? Location);
public sealed record WorkOrderPlanContextDto(Guid VesselId,WorkOrderPlanPin Pin,bool Current,string Qualification,
    string Provenance,DateTimeOffset SavedAt,WorkOrderPlanSummaryDto Plan,WorkOrderAssetSummaryDto Asset,string Href);

// The established plan revision owns its asset linkage. No inferred completion,
// private owner identities, calculation inputs, evidence or legal authority escape.
internal static class WorkOrderPlanOriginReader
{
    internal const string SourceVersion="poseidon.maintenance-plan-context.v1";
    internal static bool CanRead(TrustedActor? actor)=>WorkOrderEndpoints.CanRead(actor);
    internal static bool Valid(WorkOrderPlanPin? pin)=>pin is {Revision:>0,Version:>0,Hash.Length:64} && pin.Id!=Guid.Empty &&
        pin.SourceVersion==SourceVersion && pin.Hash.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    // Plan snapshots were captured before commit and contain no committed xmin.
    // Fingerprint the exact immutable plan/asset contents, never a fabricated old xmin.
    private static string Hash(MaintenancePlanRevision plan,string asset)=>RegisterStorage.Hash(JsonSerializer.Serialize(new{
        plan.TenantId,plan.VesselId,plan.PlanId,plan.Revision,plan.AssetId,plan.AssetRevision,plan.JobId,plan.OwnerRoleId,
        plan.ObservationId,plan.ContentJson,plan.CalculationJson,plan.ReferenceAt,plan.SavedAt,plan.Provenance,asset},WorkOrderEndpoints.Json));
    internal static IQueryable<MaintenancePlanRevision> Visible(PoseidonDbContext db,TrustedActor actor,Guid vessel) {
        var allowed=CanRead(actor);var vessels=actor.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==vessel);
        return db.MaintenancePlanRevisions.AsNoTracking().Where(r=>allowed && r.VesselId==vessel && vessels.Any() &&
            db.MaintenancePlans.Any(h=>h.Id==r.PlanId && h.VesselId==vessel) &&
            db.Assets.Any(a=>a.Id==r.AssetId && a.VesselId==vessel) &&
            db.AssetRevisions.Any(a=>a.AssetId==r.AssetId && a.VesselId==vessel && a.Revision==r.AssetRevision));
    }
    private sealed class Replace(ParameterExpression from,ParameterExpression to):ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node)=>node==from?to:base.VisitParameter(node);
    }
    private static Expression<Func<T,bool>> And<T>(Expression<Func<T,bool>> left,Expression<Func<T,bool>> right)=>Expression.Lambda<Func<T,bool>>(
        Expression.AndAlso(left.Body,new Replace(right.Parameters[0],left.Parameters[0]).Visit(right.Body)!),left.Parameters);
    private static Expression<Func<T,bool>> Or<T>(Expression<Func<T,bool>> left,Expression<Func<T,bool>> right)=>Expression.Lambda<Func<T,bool>>(
        Expression.OrElse(left.Body,new Replace(right.Parameters[0],left.Parameters[0]).Visit(right.Body)!),left.Parameters);
    internal static async Task<Expression<Func<Vessel,bool>>?> CaptureFence(PoseidonDbContext db,TrustedActor actor,Guid vessel,
        WorkOrderPlanPin[] pins,bool currentOnly=false,CancellationToken ct=default) {
        if(!CanRead(actor) || pins.Length>256 || pins.Any(p=>!Valid(p)))return null;
        Expression<Func<Vessel,bool>> fence=v=>true;if(pins.Length==0)return fence;
        var unique=pins.Distinct().ToArray();Expression<Func<MaintenancePlanRevision,bool>> requested=r=>false;
        foreach(var pin in unique)requested=Or(requested,r=>r.PlanId==pin.Id && r.Revision==pin.Revision);
        var visible=Visible(db,actor,vessel);var captured=await visible.Where(requested).ToArrayAsync(ct);
        foreach(var pin in unique) {
            var row=captured.SingleOrDefault(r=>r.PlanId==pin.Id && r.Revision==pin.Revision);if(row==null)return null;
            var asset=await db.AssetRevisions.AsNoTracking().Where(a=>a.VesselId==vessel && a.AssetId==row.AssetId && a.Revision==row.AssetRevision)
                .Select(a=>a.ContentJson).SingleOrDefaultAsync(ct);if(asset==null || Hash(row,asset)!=pin.Hash)return null;
            var content=row.ContentJson;var calculation=row.CalculationJson;var assetId=row.AssetId;var assetRevision=row.AssetRevision;
            var job=row.JobId;var owner=row.OwnerRoleId;var observation=row.ObservationId;var provenance=row.Provenance;var saved=row.SavedAt;var reference=row.ReferenceAt;
            Expression<Func<Vessel,bool>> match=v=>visible.Any(source=>source.PlanId==pin.Id && source.Revision==pin.Revision &&
                source.ContentJson==content && source.CalculationJson==calculation && source.AssetId==assetId && source.AssetRevision==assetRevision &&
                source.JobId==job && source.OwnerRoleId==owner && source.ObservationId==observation && source.Provenance==provenance && source.SavedAt==saved && source.ReferenceAt==reference &&
                db.AssetRevisions.Any(a=>a.VesselId==vessel && a.AssetId==assetId && a.Revision==assetRevision && a.ContentJson==asset) &&
                (!currentOnly || db.MaintenancePlans.Any(head=>head.Id==pin.Id && head.VesselId==vessel && head.Revision==pin.Revision && head.Version==pin.Version)));
            fence=And(fence,match);
        }
        return fence;
    }
    internal static void MapWorkOrderPlanOrigins(this IEndpointRouteBuilder app)=>app
        .MapGet("/api/v1/vessels/{vessel:guid}/work-order-plan-origins/{id:guid}",Read)
        .RequireAuthorization("DirectoryRead").WithName("WorkOrderPlanOriginRead").Produces<WorkOrderPlanContextDto>();
    private static async Task<IResult> Failure(HttpContext h,int status,string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderPlanOriginRead",code);
        return Results.Json(new{code},statusCode:status);
    }
    private static async Task<bool> Current(HttpContext h,Guid vessel,WorkOrderPlanPin pin,string rights) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var actor=resolver.Resolve(h.User);
        if(!CanRead(actor) || DailyOperationsEndpoints.Rights(actor!)!=rights)return false;
        var db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();if(db.TenantId!=actor!.TenantId)return false;
        var fence=await CaptureFence(db,actor,vessel,[pin],true,h.RequestAborted);if(fence==null)return false;
        var found=await actor.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==vessel).Where(fence).AnyAsync(h.RequestAborted);
        var live=resolver.Resolve(h.User);return found && CanRead(live) && DailyOperationsEndpoints.Rights(live!)==rights;
    }
    private static async Task<IResult> Read(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";long revision=0;
        if(vessel==Guid.Empty || id==Guid.Empty || h.Request.Query.Any(q=>q.Key!="revision" || q.Value.Count!=1) ||
            h.Request.Query.ContainsKey("revision") && (!long.TryParse(h.Request.Query["revision"],NumberStyles.None,CultureInfo.InvariantCulture,out revision) || revision<=0))
            return await Failure(h,400,"invalid_query");
        var actor=h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);if(!CanRead(actor))return await Failure(h,403,"permission_denied");
        var rights=DailyOperationsEndpoints.Rights(actor!);
        var current=await Visible(db,actor!,vessel).Where(r=>r.PlanId==id).Join(db.MaintenancePlans.AsNoTracking().Where(p=>p.VesselId==vessel && p.Id==id),
            r=>new{r.PlanId,r.Revision},p=>new{PlanId=p.Id,p.Revision},(r,p)=>new{Row=r,p.Version}).SingleOrDefaultAsync(h.RequestAborted);
        if(current==null)return await Failure(h,404,"resource_not_found");
        if(revision>0 && revision!=current.Row.Revision)return await Failure(h,409,"plan_context_stale");
        var row=current.Row;var raw=await db.AssetRevisions.AsNoTracking().Where(a=>a.VesselId==vessel && a.AssetId==row.AssetId && a.Revision==row.AssetRevision)
            .Select(a=>a.ContentJson).SingleOrDefaultAsync(h.RequestAborted);if(raw==null)return await Failure(h,404,"resource_not_found");
        Asset? asset;try{asset=JsonSerializer.Deserialize<Asset>(raw);}catch(JsonException){return await Failure(h,503,"source_contract_changed");}
        if(asset==null || asset.Id!=row.AssetId || asset.VesselId!=vessel || asset.TenantId!=actor!.TenantId || asset.Revision!=row.AssetRevision ||
            asset.Level is not("System" or "Equipment" or "Component"))return await Failure(h,503,"source_contract_changed");
        var pin=new WorkOrderPlanPin(id,row.Revision,current.Version,Hash(row,raw),SourceVersion);
        if(!Valid(pin))return await Failure(h,503,"source_contract_changed");
        var result=new WorkOrderPlanContextDto(vessel,pin,true,"REVIEW_REQUIRED",row.Provenance,row.SavedAt,
            new(row.AssetId,row.AssetRevision,row.JobId,"Draft"),new(asset.Code,asset.Name,asset.Level,asset.Criticality,asset.Maker,asset.Model,asset.Location),
            $"/app/maintenance-plan/{vessel:D}?id={id:D}");
        if(!await Current(h,vessel,pin,rights))return await Failure(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderPlanOriginRead","Success",resourceId:id);
        if(!await Current(h,vessel,pin,rights))return await Failure(h,403,"permission_denied");return Results.Ok(result);
    }
}
