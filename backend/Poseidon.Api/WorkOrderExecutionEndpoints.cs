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
public sealed record WorkOrderExecutionTransitionCommand([property:JsonRequired] Guid WorkOrderId,[property:JsonRequired] string Action,[property:JsonRequired] uint ExpectedVersion,
    [property:JsonRequired] long ExpectedRevision,long? WorkOrderRevision=null,uint? WorkOrderVersion=null,string[]? RequiredEvidence=null,string? ExpectedEvidenceHash=null,string? ReasonCode=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkOrderExecutionEvidenceCommand([property:JsonRequired] Guid WorkOrderId,[property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] long ExpectedRevision,
    [property:JsonRequired] WorkOrderEvidence Evidence);

public sealed record WorkOrderExecutionDto(Guid WorkOrderId,Guid VesselId,long Revision,uint Version,long WorkOrderRevision,uint WorkOrderVersion,string State,string[]? RequiredEvidence,
    WorkOrderEvidence Evidence,string EvidenceHash,WorkOrderEvidenceGate Gate,string[] Blockers,string Transition,DateTimeOffset ReleasedAt,DateTimeOffset? StartedAt,
    DateTimeOffset? SubmittedAt,DateTimeOffset? VerifiedAt,DateTimeOffset? ClosedAt,string? ReturnReason,DateTimeOffset? ReturnedAt,bool ReleasedByCurrentActor,
    bool ExecutedByCurrentActor,string SourceVersion,string Hash,string Provenance,DateTimeOffset SavedAt,bool CurrentRevision,string Qualification);
public sealed record WorkOrderExecutionCapabilitiesDto(bool CanRelease,bool CanExecute,bool CanVerify,bool CanClose,string[] EvidenceKinds,string[] FileKinds,string[] ReturnReasons,
    WorkOrderOfflineDto Offline,string SourceVersion,string Qualification);
public sealed record WorkOrderExecutionViewDto(Guid WorkOrderId,Guid VesselId,long WorkOrderRevision,uint WorkOrderVersion,WorkOrderExecutionCapabilitiesDto Capabilities,WorkOrderExecutionDto? Current);
public sealed record WorkOrderExecutionHistoryDto(WorkOrderExecutionDto[] Items,int Offset,int Limit,bool HasMore);

// Read-only contracts for other modules (WP-C PMS command center). Each implementation enforces the
// caller's current work order / spare read rights and vessel scope and returns an empty set otherwise.
// Facts are advisory projections: no field here is a verification, readiness or procurement decision.
public sealed record WorkOrderStateFact(Guid WorkOrderId,long ExecutionRevision,string State,string EvidenceGate,string[] MissingEvidence,bool HumanVerified,bool Closed,DateTimeOffset UpdatedAt);
public interface IWorkOrderStateSource
{
    Task<WorkOrderStateFact[]> ReadAsync(PoseidonDbContext db,TrustedActor actor,Guid vessel,CancellationToken ct);
}
internal sealed class WorkOrderStateReader:IWorkOrderStateSource
{
    public async Task<WorkOrderStateFact[]> ReadAsync(PoseidonDbContext db,TrustedActor actor,Guid vessel,CancellationToken ct) {
        if(!WorkOrderEndpoints.CanRead(actor) || db.TenantId!=actor.TenantId || !await actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,ct))return [];
        var rows=await WorkOrderExecutionEndpoints.Current(db,vessel).OrderByDescending(r=>r.SavedAt).Take(500).ToArrayAsync(ct);
        return rows.Select(r=>{var c=WorkOrderExecutionEndpoints.Content(r);var gate=WorkOrderExecutionPolicy.Gate(c);
            return new WorkOrderStateFact(r.WorkOrderId,r.Revision,r.State,gate.State,gate.Missing,gate.State=="HUMAN_VERIFIED",r.State=="CLOSED",r.SavedAt);}).ToArray();
    }
}

// DEV-054 / T-S8-003 execution API. Every write is SERIALIZABLE, version-fenced, idempotent per actor
// key and appends one immutable revision. Release, verification, return and closure are Human-only and
// separately permissioned; verifier/closer must not be an executor (author of start/evidence/submit).
public static class WorkOrderExecutionEndpoints
{
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=10,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static void MapWorkOrderExecution(this IEndpointRouteBuilder app) {
        var g=app.MapGroup("/api/v1/work-orders/{id:guid}/execution").RequireAuthorization("DirectoryRead");
        g.MapGet("",(Guid id,HttpContext h,PoseidonDbContext db)=>View(id,false,h,db)).WithName("WorkOrderExecutionRead").Produces<WorkOrderExecutionViewDto>();
        g.MapGet("/history",(Guid id,HttpContext h,PoseidonDbContext db)=>View(id,true,h,db)).WithName("WorkOrderExecutionHistory").Produces<WorkOrderExecutionHistoryDto>();
        g.MapPost("/transitions",(Guid id,HttpContext h,PoseidonDbContext db)=>Transition(id,h,db)).WithName("WorkOrderExecutionTransition")
            .Accepts<WorkOrderExecutionTransitionCommand>("application/json").Produces<WorkOrderExecutionDto>();
        g.MapPut("/evidence",(Guid id,HttpContext h,PoseidonDbContext db)=>Evidence(id,h,db)).WithName("WorkOrderExecutionEvidence")
            .Accepts<WorkOrderExecutionEvidenceCommand>("application/json").Produces<WorkOrderExecutionDto>();
    }
    internal static TrustedActor? Actor(HttpContext h)=>h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static DateTimeOffset Now(HttpContext h){var now=h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().ToUniversalTime();return now.AddTicks(-(now.Ticks%10));}
    internal static bool Execute(TrustedActor? a,Guid vessel)=>WorkOrderEndpoints.CanRead(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("workorder.execute");
    internal static bool Release(HttpContext? h,TrustedActor? a,Guid vessel)=>WorkOrderEndpoints.CanRead(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("workorder.release") && h!=null && CertificateRenewalEndpoints.Human(h,a);
    internal static bool Verify(HttpContext? h,TrustedActor? a,Guid vessel)=>WorkOrderEndpoints.CanRead(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("workorder.verify") && h!=null && CertificateRenewalEndpoints.Human(h,a);
    internal static bool Close(HttpContext? h,TrustedActor? a,Guid vessel)=>WorkOrderEndpoints.CanRead(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("workorder.close") && h!=null && CertificateRenewalEndpoints.Human(h,a);
    internal static bool Permitted(HttpContext h,TrustedActor a,Guid vessel,string action)=>action switch {
        "RELEASE" or "REQUIREMENTS"=>Release(h,a,vessel), "START" or "SUBMIT"=>Execute(a,vessel), "VERIFY" or "RETURN"=>Verify(h,a,vessel), "CLOSE"=>Close(h,a,vessel), _=>false };
    internal static string Rights(TrustedActor a)=>RegisterStorage.Hash(JsonSerializer.Serialize(new{a.Id,a.TenantId,a.RoleContext,a.TenantWideRead,a.TenantWideManage,
        permissions=a.Permissions.Order(StringComparer.Ordinal),vessels=a.VesselGrants.OrderBy(v=>v.VesselId).ThenBy(v=>v.CanEdit)},Json));
    internal static Task<IResult> ReadFail(HttpContext h,int status,string code)=>Fail(h,"WorkOrderExecutionRead",status,code);
    internal static Task<IResult> CommandFail(HttpContext h,int status,string code)=>Fail(h,"WorkOrderExecutionCommand",status,code);
    private static async Task<IResult> Fail(HttpContext h,string action,int status,string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,action,code);return Results.Json(new{code},statusCode:status);
    }
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        vessel=Guid.Empty;offset=0;return h.Request.Query.All(q=>q.Value.Count==1 && (q.Key=="vessel" || paging && q.Key=="offset")) &&
            Guid.TryParseExact(h.Request.Query["vessel"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    internal static WorkOrderExecutionContent Content(WorkOrderExecutionRevision row)=>JsonSerializer.Deserialize<WorkOrderExecutionContent>(row.ContentJson,Json)
        ??throw new InvalidDataException("work_order_execution_unavailable");
    internal static IQueryable<WorkOrderExecutionRevision> Current(PoseidonDbContext db,Guid vessel)=>db.WorkOrderExecutionRevisions.AsNoTracking()
        .Where(r=>r.VesselId==vessel && db.WorkOrderExecutions.Any(x=>x.VesselId==vessel && x.WorkOrderId==r.WorkOrderId && x.Revision==r.Revision));
    internal static IEnumerable<CertificateFilePin> Pins(WorkOrderEvidence e)=>e.Files.Select(f=>f.File);

    internal static WorkOrderExecutionDto Dto(WorkOrderExecutionRevision row,TrustedActor a,bool current,long? workOrderHeadRevision) {
        var c=Content(row);var blockers=WorkOrderExecutionPolicy.Blockers(c).ToList();
        if(workOrderHeadRevision!=null && workOrderHeadRevision!=row.WorkOrderRevision)blockers.Add("WORK_ORDER_REVISED_AFTER_RELEASE");
        if(row.SourceVersion!=WorkOrderExecutionPolicy.SourceVersion)blockers.Add("SOURCE_VERSION_CHANGED");
        return new(row.WorkOrderId,row.VesselId,row.Revision,checked((uint)row.RecordVersion),row.WorkOrderRevision,checked((uint)row.WorkOrderVersion),c.State,c.RequiredEvidence,c.Evidence,
            WorkOrderExecutionPolicy.EvidenceHash(c.Evidence),WorkOrderExecutionPolicy.Gate(c),blockers.ToArray(),row.Transition,c.Release.RecordedAt,c.Start?.RecordedAt,c.Submission?.RecordedAt,
            c.Verification?.RecordedAt,c.Closure?.RecordedAt,c.LastReturn?.ReasonCode,c.LastReturn?.RecordedAt,c.Release.ActorId==a.Id,c.Executors.Contains(a.Id),
            row.SourceVersion,row.Hash,row.Provenance,row.SavedAt,current,WorkOrderExecutionPolicy.Qualification);
    }
    internal static WorkOrderExecutionCapabilitiesDto Capabilities(HttpContext h,TrustedActor a,Guid vessel,Guid workOrder,string? state)=>new(Release(h,a,vessel),Execute(a,vessel),
        Verify(h,a,vessel),Close(h,a,vessel),WorkOrderExecutionPolicy.EvidenceKinds,WorkOrderExecutionPolicy.FileKinds,WorkOrderExecutionPolicy.ReturnReasons,
        WorkOrderOfflineEndpoints.Capability(h,a,vessel,state),WorkOrderExecutionPolicy.SourceVersion,WorkOrderExecutionPolicy.Qualification);

    // Pre/post-audit fence in a fresh scope: same rights, vessel, exact returned revisions, current head and pinned files.
    internal static async Task<bool> Fence(HttpContext h,Guid vessel,string rights,WorkOrderExecutionDto[] records) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();
        var a=resolver.Resolve(h.User);if(!WorkOrderEndpoints.CanRead(a) || Rights(a!)!=rights)return false;
        PoseidonDbContext db;try{db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=a!.TenantId || !await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return false;
        foreach(var r in records) {
            if(!await db.WorkOrderExecutionRevisions.AsNoTracking().AnyAsync(x=>x.VesselId==vessel && x.WorkOrderId==r.WorkOrderId && x.Revision==r.Revision && x.RecordVersion==r.Version && x.Hash==r.Hash,h.RequestAborted))return false;
            if(r.CurrentRevision && !await db.WorkOrderExecutions.AnyAsync(x=>x.VesselId==vessel && x.WorkOrderId==r.WorkOrderId && x.Revision==r.Revision,h.RequestAborted))return false;
            if(!await PreSurveyChecklistEndpoints.FilesVisible(db,a,vessel,Pins(r.Evidence),h.RequestAborted))return false;
        }
        var live=resolver.Resolve(h.User);return WorkOrderEndpoints.CanRead(live) && Rights(live!)==rights;
    }
    private static async Task<IResult> View(Guid id,bool history,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";if(!Query(h,history,out var vessel,out var offset))return await ReadFail(h,400,"invalid_query");
        var a=Actor(h);if(!WorkOrderEndpoints.CanRead(a))return await ReadFail(h,403,"permission_denied");var rights=Rights(a!);
        if(!await a!.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,h.RequestAborted))return await ReadFail(h,404,"resource_not_found");
        var order=await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(w=>w.Id==id && w.VesselId==vessel,h.RequestAborted);
        if(order==null)return await ReadFail(h,404,"resource_not_found");
        var head=await db.WorkOrderExecutions.AsNoTracking().SingleOrDefaultAsync(x=>x.WorkOrderId==id && x.VesselId==vessel,h.RequestAborted);
        var rows=head==null?[]:await db.WorkOrderExecutionRevisions.AsNoTracking().Where(r=>r.VesselId==vessel && r.WorkOrderId==id && (history || r.Revision==head.Revision))
            .OrderByDescending(r=>r.Revision).Skip(history?offset:0).Take(history?26:1).ToArrayAsync(h.RequestAborted);
        var items=new List<WorkOrderExecutionDto>();
        // A revision whose pinned evidence files the actor cannot see is withheld as a whole.
        foreach(var row in rows.Take(25))
            if(await PreSurveyChecklistEndpoints.FilesVisible(db,a,vessel,Pins(Content(row).Evidence),h.RequestAborted))items.Add(Dto(row,a,row.Revision==head!.Revision,order.Revision));
        if(head!=null && !history && items.Count==0)return await ReadFail(h,404,"resource_not_found");
        var records=items.ToArray();
        if(!await Fence(h,vessel,rights,records))return await ReadFail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderExecutionRead","Success",resourceId:id);
        if(!await Fence(h,vessel,rights,records))return await ReadFail(h,403,"permission_denied");
        if(history)return Results.Ok(new WorkOrderExecutionHistoryDto(records,offset,25,rows.Length>25));
        return Results.Ok(new WorkOrderExecutionViewDto(id,vessel,order.Revision,order.Version,Capabilities(h,a,vessel,id,head?.State),records.FirstOrDefault()));
    }

    private sealed record Envelope(TrustedActor Actor,Guid Vessel,string Key,string Rights);
    private static async Task<(Envelope? Env,IResult? Error)> Command(HttpContext h) {
        h.Response.Headers.CacheControl="no-store";var a=Actor(h);
        if(!WorkOrderEndpoints.CanRead(a) || !a!.HasWriteAttribution || !Query(h,false,out var vessel,out _) || !a.CanEditVessel(vessel))return (null,await CommandFail(h,403,"permission_denied"));
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return (null,await CommandFail(h,403,"csrf_invalid"));}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;
        if(key==null || !Guid.TryParseExact(key,"D",out _))return (null,await CommandFail(h,400,"invalid_idempotency_key"));
        return (new(a,vessel,key,Rights(a)),null);
    }
    internal static async Task<T> Body<T>(HttpContext h,int max=262144) {
        if(!h.Request.HasJsonContentType())throw new JsonException();using var bytes=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,bytes,max,h.RequestAborted);
        using var document=JsonDocument.Parse(bytes.ToArray(),new(){MaxDepth=10});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
        return document.RootElement.Deserialize<T>(Json)??throw new JsonException();
    }
    private static async Task<IResult> Outcome(HttpContext h,Envelope e,WorkOrderExecutionDto record,string json) {
        try {
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderExecutionCommand","Success",resourceId:record.WorkOrderId);
            if(!await Fence(h,e.Vessel,e.Rights,[record]))return await CommandFail(h,503,"command_outcome_withheld");
            return Results.Content(json,"application/json");
        }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
    }
    private static async Task<IResult?> Replay(HttpContext h,PoseidonDbContext db,Envelope e,string action,Guid id,string fingerprint) {
        if(!await Fence(h,e.Vessel,e.Rights,[]))return await CommandFail(h,403,"permission_denied");
        var receipt=await db.WorkOrderExecutionReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==e.Actor.Id && r.Key==e.Key,h.RequestAborted);if(receipt==null)return null;
        if(receipt.Action!=action || receipt.VesselId!=e.Vessel || receipt.WorkOrderId!=id || receipt.Fingerprint!=fingerprint)return await CommandFail(h,409,"idempotency_key_reused");
        var record=JsonSerializer.Deserialize<WorkOrderExecutionDto>(receipt.OutcomeJson,Json)??throw new InvalidDataException("work_order_execution_receipt_unavailable");
        return await Outcome(h,e,record,receipt.OutcomeJson);
    }

    // Shared head advance + immutable revision append. Caller owns the transaction and receipt.
    internal static async Task<WorkOrderExecutionRevision> Append(PoseidonDbContext db,TrustedActor actor,Guid vessel,WorkOrderExecution head,long workOrderRevision,long workOrderVersion,
        string workOrderHash,WorkOrderExecutionContent content,string transition,DateTimeOffset now,CancellationToken ct) {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("Work order execution writes require a scoped transaction.");
        head.State=content.State;await db.SaveChangesAsync(ct);
        var json=JsonSerializer.Serialize(content,Json);
        var row=new WorkOrderExecutionRevision{TenantId=actor.TenantId,VesselId=vessel,WorkOrderId=head.WorkOrderId,Revision=head.Revision,RecordVersion=head.Version,
            WorkOrderRevision=workOrderRevision,WorkOrderVersion=workOrderVersion,WorkOrderHash=workOrderHash,Transition=transition,State=content.State,
            EvidenceGate=WorkOrderExecutionPolicy.Gate(content).State,ContentJson=json,SourceVersion=WorkOrderExecutionPolicy.SourceVersion,ActorId=actor.Id,SavedAt=now,
            Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,head.WorkOrderId,head.Revision,workOrderRevision,workOrderVersion,transition,json},Json))};
        db.WorkOrderExecutionRevisions.Add(row);return row;
    }
    // New file pins must be clean vessel transport the actor can read; new catalogued spares must exist.
    internal static async Task<string?> Linked(PoseidonDbContext db,TrustedActor a,Guid vessel,WorkOrderEvidence next,WorkOrderEvidence previous,CancellationToken ct) {
        var known=previous.Files.Select(f=>(f.File.Id,f.File.Revision,f.File.Checksum.ToUpperInvariant())).ToHashSet();
        foreach(var pin in next.Files.Select(f=>f.File).Where(p=>!known.Contains((p.Id,p.Revision,p.Checksum.ToUpperInvariant()))))
            if(!await CertificateReader.FileAllowed(db,a,vessel,pin,ct))return "linked_resource_unavailable";
        var knownSpares=previous.Spares.Where(s=>s.SpareId!=null).Select(s=>(s.SpareId,s.SpareRevision)).ToHashSet();
        foreach(var spare in next.Spares.Where(s=>s.SpareId!=null && !knownSpares.Contains((s.SpareId,s.SpareRevision))))
            if(!await db.CriticalSpareRevisions.AsNoTracking().AnyAsync(r=>r.VesselId==vessel && r.SpareId==spare.SpareId && r.Revision==spare.SpareRevision,ct))return "linked_resource_unavailable";
        return null;
    }

    private delegate Task<(WorkOrderExecutionContent? Content,long WorkOrderRevision,long WorkOrderVersion,string WorkOrderHash,IResult? Error)> Mutation(WorkOrderExecution? head,WorkOrderExecutionRevision? current);
    private static async Task<IResult> Apply(HttpContext h,PoseidonDbContext db,Envelope e,Guid id,uint expectedVersion,long expectedRevision,string action,string transition,string fingerprint,Mutation mutate) {
        var replay=await Replay(h,db,e,action,id,fingerprint);if(replay!=null)return replay;
        IDbContextTransaction? tx=null;WorkOrderExecutionDto? saved=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);var now=Now(h);
            var order=await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(w=>w.Id==id,h.RequestAborted);
            if(order==null || order.VesselId!=e.Vessel)return await CommandFail(h,404,"resource_not_found");
            var head=await db.WorkOrderExecutions.SingleOrDefaultAsync(x=>x.WorkOrderId==id,h.RequestAborted);WorkOrderExecutionRevision? current=null;
            if(transition=="RELEASE") {
                if(head!=null || expectedVersion!=0 || expectedRevision!=0)return await Replay(h,db,e,action,id,fingerprint)??await CommandFail(h,409,"version_conflict");
            }else {
                if(head==null)return await CommandFail(h,404,"resource_not_found");
                if(head.Version!=expectedVersion || head.Revision!=expectedRevision)return await Replay(h,db,e,action,id,fingerprint)??await CommandFail(h,409,"version_conflict");
                current=await db.WorkOrderExecutionRevisions.AsNoTracking().SingleAsync(r=>r.WorkOrderId==id && r.Revision==head.Revision,h.RequestAborted);
                if(current.SourceVersion!=WorkOrderExecutionPolicy.SourceVersion)return await CommandFail(h,409,"source_contract_changed");
                if(!await PreSurveyChecklistEndpoints.FilesVisible(db,e.Actor,e.Vessel,Pins(Content(current).Evidence),h.RequestAborted))return await CommandFail(h,404,"resource_not_found");
            }
            var (content,woRevision,woVersion,woHash,error)=await mutate(head,current);if(error!=null)return error;
            if(head==null){head=new(){TenantId=e.Actor.TenantId,VesselId=e.Vessel,WorkOrderId=id};db.WorkOrderExecutions.Add(head);}else head.Revision++;
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"WorkOrderExecutionCommand","Attempted",resourceId:id);
            var row=await Append(db,e.Actor,e.Vessel,head,woRevision,woVersion,woHash,content!,transition,now,h.RequestAborted);
            saved=Dto(row,e.Actor,false,order.Revision);outcome=JsonSerializer.Serialize(saved,Json);
            db.WorkOrderExecutionReceipts.Add(new(){TenantId=e.Actor.TenantId,VesselId=e.Vessel,ActorId=e.Actor.Id,Key=e.Key,WorkOrderId=id,Action=action,Revision=row.Revision,
                RecordVersion=row.RecordVersion,Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});
            await db.SaveChangesAsync(h.RequestAborted);
            if(!await Fence(h,e.Vessel,e.Rights,[]) || !Permitted(h,Actor(h)!,e.Vessel,transition=="EVIDENCE"?"START":transition))return await CommandFail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay(h,db,e,action,id,fingerprint)??await CommandFail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
            var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";
            return await Replay(h,db,e,action,id,fingerprint)??await CommandFail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(h,e,saved!,outcome!);
    }

    private static async Task<IResult> Transition(Guid id,HttpContext h,PoseidonDbContext db) {
        var (env,error)=await Command(h);if(error!=null)return error;var e=env!;
        WorkOrderExecutionTransitionCommand command;
        try {
            command=await Body<WorkOrderExecutionTransitionCommand>(h,16384);
            if(command.WorkOrderId!=id || !WorkOrderExecutionPolicy.Actions.Contains(command.Action) ||
                (command.Action=="RELEASE" ? command.ExpectedVersion!=0 || command.ExpectedRevision!=0 || command.WorkOrderRevision is not >0 || command.WorkOrderVersion is not >0 ||
                    command.ExpectedEvidenceHash!=null || command.ReasonCode!=null || !WorkOrderExecutionPolicy.ValidRequired(command.RequiredEvidence)
                : command.ExpectedVersion==0 || command.ExpectedRevision<=0 || command.WorkOrderRevision!=null || command.WorkOrderVersion!=null))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await CommandFail(h,400,"validation_failed");}
        // Human-only release/verification/closure: an AI or service principal is rejected explicitly.
        if(command.Action is not("START" or "SUBMIT") && !CertificateRenewalEndpoints.Human(h,e.Actor))return await CommandFail(h,403,"human_actor_required");
        if(!Permitted(h,e.Actor,e.Vessel,command.Action))return await CommandFail(h,403,"permission_denied");
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,id,command},Json));
        return await Apply(h,db,e,id,command.ExpectedVersion,command.ExpectedRevision,"WorkOrderExecutionTransition",command.Action,fingerprint,async (_,current)=> {
            var now=Now(h);
            if(command.Action=="RELEASE") {
                var revision=await db.WorkOrderRevisions.AsNoTracking().SingleOrDefaultAsync(r=>r.VesselId==e.Vessel && r.WorkOrderId==id && r.Revision==command.WorkOrderRevision &&
                    r.RecordVersion==command.WorkOrderVersion && r.SourceVersion==WorkOrderPolicy.SourceVersion &&
                    db.WorkOrders.Any(w=>w.Id==id && w.VesselId==e.Vessel && w.Revision==r.Revision && w.Version==command.WorkOrderVersion),h.RequestAborted);
                if(revision==null)return (null,0,0,"",await CommandFail(h,409,"source_revision_changed"));
                return (WorkOrderExecutionPolicy.Released(e.Actor.Id,now,revision.Hash,command.RequiredEvidence),revision.Revision,revision.RecordVersion,revision.Hash,null);
            }
            var (next,problem)=WorkOrderExecutionPolicy.Apply(Content(current!),command.Action,e.Actor.Id,now,command.RequiredEvidence,command.ExpectedEvidenceHash,command.ReasonCode);
            if(problem!=null)return (null,0,0,"",await CommandFail(h,problem.Status,problem.Code));
            return (next,current!.WorkOrderRevision,current.WorkOrderVersion,current.WorkOrderHash,null);
        });
    }
    private static async Task<IResult> Evidence(Guid id,HttpContext h,PoseidonDbContext db) {
        var (env,error)=await Command(h);if(error!=null)return error;var e=env!;
        if(!Execute(e.Actor,e.Vessel))return await CommandFail(h,403,"permission_denied");
        WorkOrderExecutionEvidenceCommand command;
        try {
            command=await Body<WorkOrderExecutionEvidenceCommand>(h);
            if(command.WorkOrderId!=id || command.ExpectedVersion==0 || command.ExpectedRevision<=0 || !WorkOrderExecutionPolicy.ValidEvidence(command.Evidence))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await CommandFail(h,400,"validation_failed");}
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{e.Vessel,id,command},Json));
        return await Apply(h,db,e,id,command.ExpectedVersion,command.ExpectedRevision,"WorkOrderExecutionEvidence","EVIDENCE",fingerprint,async (_,current)=> {
            var old=Content(current!);
            var (next,problem)=WorkOrderExecutionPolicy.RecordEvidence(old,command.Evidence,e.Actor.Id);
            if(problem!=null)return (null,0,0,"",await CommandFail(h,problem.Status,problem.Code));
            if(await Linked(db,e.Actor,e.Vessel,command.Evidence,old.Evidence,h.RequestAborted) is {} linked)return (null,0,0,"",await CommandFail(h,403,linked));
            return (next,current!.WorkOrderRevision,current.WorkOrderVersion,current.WorkOrderHash,null);
        });
    }
}
