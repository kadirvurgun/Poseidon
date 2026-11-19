using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record SurveyPlannerChecklistDto(Guid Id,long Revision,string Readiness,int CriticalUnresolved,int RequiredUnresolved,DateOnly? PlannedStart,DateOnly? PlannedEnd,
    string PortCallReference,string Href);
public sealed record SurveyPlannerRowDto(Guid VesselId,string VesselCode,string VesselName,string VesselKind,Guid CertificateId,long CertificateRevision,uint CertificateVersion,
    string? CertificateType,string? CertificateNumber,string? Issuer,string? DeclaredFlag,string? RegulatoryBasis,DateOnly? ExpiryDate,SurveyWindowDto Window,SurveyFitDto Fit,
    SurveyPlannerChecklistDto? Checklist,string Coordination,string Href);
public sealed record SurveyPlannerCountsDto(int WindowUnknown,int WindowUpcoming,int WindowOpen,int WindowOverdue,int FitConditional,int FitNotFit,int FitUnknown,int ChecklistBlocked,int ChecklistMissing);
public sealed record SurveyPlannerDto(DateOnly From,DateOnly To,DateOnly ReferenceDate,SurveyPlannerRowDto[] Items,bool Truncated,bool VesselsTruncated,SurveyPlannerCountsDto Counts,
    string Qualification,string[] FitStates,string SourceVersion);

// DEV-112 survey planner fleet calendar (18.01 row113; 60.04 Rev3 p221-222). Windows come only
// from the certificate's declared dates (UNKNOWN when missing, never filled from plan dates); fit
// is CONDITIONAL/NOT_FIT/UNKNOWN with reasons and never FIT. Class/flag/authority coordination
// is external: only what the certificate itself declares (issuer, flag, basis) is shown.
public static class SurveyPlannerEndpoints
{
    private const int MaxVessels=50, MaxRows=200, MaxSpanDays=731;
    internal static Task<IResult> PlannerFail(HttpContext h,int status,string code)=>FailCore(h,status,code);
    private static async Task<IResult> FailCore(HttpContext h,int status,string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"SurveyPlannerRead",code);
        return Results.Json(new{code},statusCode:status);
    }
    public static void MapSurveyPlanner(this IEndpointRouteBuilder app)=>app.MapGet("/api/v1/survey-planner",Read).RequireAuthorization("DirectoryRead")
        .WithName("SurveyPlannerCalendar").Produces<SurveyPlannerDto>();

    private static bool Date(HttpContext h,string key,out DateOnly? value) {
        value=null;if(!h.Request.Query.ContainsKey(key))return true;
        if(!DateOnly.TryParseExact(h.Request.Query[key],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))return false;
        value=parsed;return true;
    }
    private static async Task<IResult> Read(HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";
        var allowed=new HashSet<string>{"from","to","vesselId"};
        if(!h.Request.Query.All(q=>allowed.Contains(q.Key) && q.Value.Count==1) || !Date(h,"from",out var fromQuery) || !Date(h,"to",out var toQuery))return await PlannerFail(h,400,"invalid_query");
        Guid? only=null;
        if(h.Request.Query.ContainsKey("vesselId")) {if(!Guid.TryParseExact(h.Request.Query["vesselId"],"D",out var v) || v==Guid.Empty)return await PlannerFail(h,400,"invalid_query");only=v;}
        var a=CertificateEndpoints.Actor(h);if(a==null || !PreSurveyChecklistEndpoints.Read(a))return await PlannerFail(h,403,"permission_denied");
        var today=DateOnly.FromDateTime(CertificateEndpoints.Now(h).UtcDateTime);
        var from=fromQuery??today.AddDays(-30);var to=toQuery??today.AddDays(365);
        if(to<from || to.DayNumber-from.DayNumber>MaxSpanDays)return await PlannerFail(h,400,"invalid_query");
        var rights=CertificateReader.Rights(a);
        var scoped=a.Scope(db.Vessels.AsNoTracking());if(only!=null)scoped=scoped.Where(v=>v.Id==only);
        var vessels=await scoped.OrderBy(v=>v.Code).ThenBy(v=>v.Id).Take(MaxVessels+1).ToArrayAsync(h.RequestAborted);
        if(only!=null && vessels.Length==0)return await PlannerFail(h,404,"resource_not_found");
        var rows=new List<SurveyPlannerRowDto>();var truncated=false;
        foreach(var vessel in vessels.Take(MaxVessels)) {
            var certificates=await CertificateReader.Visible(db,a,vessel.Id).OrderBy(r=>r.CertificateId).Take(MaxRows+1).ToArrayAsync(h.RequestAborted);
            if(certificates.Length>MaxRows)truncated=true;
            var superseded=await db.CertificateSupersessions.AsNoTracking().Where(s=>s.VesselId==vessel.Id).Select(s=>s.CertificateId).ToArrayAsync(h.RequestAborted);
            var checklists=await PreSurveyChecklistEndpoints.Visible(db,a,vessel.Id).OrderByDescending(r=>r.SavedAt).Take(500).ToArrayAsync(h.RequestAborted);
            foreach(var certificate in certificates.Take(MaxRows).Where(c=>!superseded.Contains(c.CertificateId))) {
                var content=CertificateReader.Content(certificate);var d=content.Metadata.Declaration;
                var window=SurveyPlannerPolicy.Window(today,certificate.SurveyWindowDeclared,certificate.SurveyWindowStart,certificate.SurveyWindowEnd);
                if(!SurveyPlannerPolicy.Intersects(window,certificate.ExpiryDate,from,to))continue;
                SurveyPlannerChecklistDto? checklist=null;PreSurveyPlan? plan=null;SurveyPortCallFact? port=null;
                // The most recently saved current checklist prepared for this certificate, if its pins are visible.
                foreach(var candidate in checklists.Where(r=>r.CertificateId==certificate.CertificateId)) {
                    var c=PreSurveyChecklistEndpoints.Content(candidate);
                    if(!await PreSurveyChecklistEndpoints.FilesVisible(db,a,vessel.Id,c.Items.SelectMany(i=>i.Evidence),h.RequestAborted))continue;
                    plan=c.Plan;port=await PreSurveyChecklistEndpoints.PortFact(db,a,vessel.Id,c.Plan.PortCallId,c.Plan.PortCallRevision,h.RequestAborted);
                    var readiness=PreSurveyChecklistPolicy.Readiness(c,today);
                    checklist=new(candidate.ChecklistId,candidate.Revision,readiness.State,readiness.CriticalUnresolved,readiness.RequiredUnresolved,c.Plan.PlannedStart,c.Plan.PlannedEnd,
                        port?.Reference??"NOT_LINKED",$"/app/pre-survey-checklist/{vessel.Id:D}?id={candidate.ChecklistId:D}");
                    break;
                }
                var fit=SurveyPlannerPolicy.Fit(today,window,certificate.ExpiryDate,plan,port);
                rows.Add(new(vessel.Id,vessel.Code,vessel.Name,vessel.Kind.ToString(),certificate.CertificateId,certificate.Revision,checked((uint)certificate.RecordVersion),
                    d.CertificateTypeReference??content.DocumentName,d.CertificateNumber,d.Issuer,content.DeclaredFlag,content.RegulatoryBasis,certificate.ExpiryDate,window,fit,checklist,
                    "EXTERNAL_NOT_SIMULATED",$"/app/certificate-command-center/{vessel.Id:D}?id={certificate.CertificateId:D}"));
            }
        }
        var ordered=rows.OrderBy(r=>r.Window.Start??DateOnly.MaxValue).ThenBy(r=>r.ExpiryDate??DateOnly.MaxValue).ThenBy(r=>r.VesselCode,StringComparer.Ordinal).ThenBy(r=>r.CertificateId).ToArray();
        if(ordered.Length>MaxRows){ordered=ordered.Take(MaxRows).ToArray();truncated=true;}
        var counts=new SurveyPlannerCountsDto(ordered.Count(r=>r.Window.State=="UNKNOWN"),ordered.Count(r=>r.Window.State=="UPCOMING"),ordered.Count(r=>r.Window.State=="OPEN"),
            ordered.Count(r=>r.Window.State=="OVERDUE"),ordered.Count(r=>r.Fit.State=="CONDITIONAL"),ordered.Count(r=>r.Fit.State=="NOT_FIT"),ordered.Count(r=>r.Fit.State=="UNKNOWN"),
            ordered.Count(r=>r.Checklist?.Readiness=="BLOCKED"),ordered.Count(r=>r.Checklist==null));
        var result=new SurveyPlannerDto(from,to,today,ordered,truncated,vessels.Length>MaxVessels,counts,"ADVISORY_PLANNING_NOT_AUTHORITY_SCHEDULE",SurveyPlannerPolicy.FitStates,SurveyPlannerPolicy.SourceVersion);
        if(!await Current(h,rights,ordered))return await PlannerFail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"SurveyPlannerRead","Success",resourceId:only);
        if(!await Current(h,rights,ordered))return await PlannerFail(h,403,"permission_denied");
        return Results.Ok(result);
    }
    // Fresh-scope fence: same rights, every returned vessel still in scope and every returned
    // certificate/checklist still the current head revision that was projected.
    private static async Task<bool> Current(HttpContext h,string rights,SurveyPlannerRowDto[] rows) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var a=resolver.Resolve(h.User);
        if(a==null || !PreSurveyChecklistEndpoints.Read(a) || CertificateReader.Rights(a)!=rights)return false;
        PoseidonDbContext db;try{db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=a.TenantId)return false;
        foreach(var group in rows.GroupBy(r=>r.VesselId)) {
            var vessel=group.Key;
            if(!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return false;
            var ids=group.Select(r=>r.CertificateId).ToArray();
            var heads=await CertificateReader.Visible(db,a,vessel).Where(r=>ids.Contains(r.CertificateId)).Select(r=>new{r.CertificateId,r.Revision,r.RecordVersion}).ToArrayAsync(h.RequestAborted);
            if(group.Any(r=>!heads.Any(x=>x.CertificateId==r.CertificateId && x.Revision==r.CertificateRevision && x.RecordVersion==r.CertificateVersion)))return false;
            var lists=group.Where(r=>r.Checklist!=null).Select(r=>r.Checklist!.Id).ToArray();
            if(lists.Length>0) {
                var current=await db.PreSurveyChecklists.AsNoTracking().Where(x=>x.VesselId==vessel && lists.Contains(x.Id)).Select(x=>new{x.Id,x.Revision}).ToArrayAsync(h.RequestAborted);
                if(group.Where(r=>r.Checklist!=null).Any(r=>!current.Any(x=>x.Id==r.Checklist!.Id && x.Revision==r.Checklist!.Revision)))return false;
            }
        }
        var live=resolver.Resolve(h.User);
        return live!=null && PreSurveyChecklistEndpoints.Read(live) && CertificateReader.Rights(live)==rights;
    }
}
