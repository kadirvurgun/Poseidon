using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DowntimeLifecycleCommand([property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] string Action,
    [property:JsonRequired] string ReasonCode,Guid? AuthorityId=null,DowntimeEvidencePin? Evidence=null);
public sealed record DowntimeLifecycleEventDto(Guid Id,long FromRevision,long ToRevision,string Action,string? FromState,string? ToState,Guid ActorId,
    Guid? RoleId,string? Duty,string ReasonCode,DowntimeEvidencePin? Evidence,DateTimeOffset SavedAt);
public sealed record DowntimeEvidenceOptionDto(Guid Id,long Revision,string Label);
public sealed record DowntimeLifecycleViewDto(Guid RecordId,Guid VesselId,long Revision,uint Version,string State,string LossOfHireCandidate,
    DowntimeEvidencePin[] ClaimEvidence,string[] AllowedActions,ShipOpsAuthorityDto[] Authorities,bool ActorIsRecorder,string[] MissingRequired,
    DowntimeEvidenceOptionDto[] EvidenceOptions,DowntimeLifecycleEventDto[] Events,string[] Qualifications);

// DEV-102 / T-OPS2-006 lifecycle (60.01 paragraphs 1680-1688, T65-T67; 11.01 row 2239).
// Submit and claim-evidence pins are writer commands. Return, verify and close
// are human review commands with a provisioned duty appointment and separation
// of duties: a recorder of the declared content never verifies or closes it.
// No financial claim, entitlement, amount or safety release is ever produced.
public static partial class OperationalDowntimeEndpoints
{
    public const string LifecycleReason="HUMAN_DOWNTIME_LIFECYCLE";
    internal static readonly string[] LifecycleQualifications=["LOSS_OF_HIRE_CANDIDATE_ONLY_NO_ENTITLEMENT_OR_AMOUNT","CLAIM_EVIDENCE_LINK_IS_NOT_VERIFICATION",
        "VERIFIED_CLOSURE_IS_NOT_SAFETY_RELEASE","SEPARATION_OF_DUTIES_RECORDER_NEVER_VERIFIES_OR_CLOSES","EXTERNAL_SHARING_NOT_AUTOMATIC"];
    private static void MapLifecycle(RouteGroupBuilder group) {
        group.MapGet("/{id:guid}/lifecycle",(Guid id,HttpContext h,PoseidonDbContext db)=>Lifecycle(id,h,db)).WithName("DowntimeLifecycle").Produces<DowntimeLifecycleViewDto>();
        group.MapPost("/{id:guid}/lifecycle",(Guid id,HttpContext h,PoseidonDbContext db)=>LifecycleCommand(id,h,db)).WithName("DowntimeLifecycleCommand")
            .Accepts<DowntimeLifecycleCommand>("application/json").Produces<DowntimeRecordDto>();
    }
    internal static bool CanReview(TrustedActor? a,Guid vessel)=>CanWrite(a,vessel) && a!.Permissions.Contains("operations.downtime.review");
    // Recorders are the authors of declared content revisions, not of lifecycle-only revisions.
    private static async Task<Guid[]> Recorders(PoseidonDbContext db,Guid vessel,Guid id,CancellationToken ct) {
        var lifecycle=db.ShipOpsLifecycleEvents.Where(e=>e.VesselId==vessel && e.Module=="Downtime" && e.RecordId==id).Select(e=>e.ToRevision);
        return await db.DowntimeRevisions.Where(r=>r.VesselId==vessel && r.DowntimeId==id && !lifecycle.Contains(r.Revision)).Select(r=>r.ActorId).Distinct().ToArrayAsync(ct);
    }
    private static IQueryable<ControlEvidenceRevision> CurrentEvidence(PoseidonDbContext db,TrustedActor actor,Guid vessel)=>
        OperationEquipmentCommands.Visible(db,actor,db.ControlEvidenceRevisions.AsNoTracking()).Where(r=>r.VesselId==vessel && r.Kind=="Evidence" &&
            db.ControlEvidenceHeads.Any(h=>h.VesselId==r.VesselId && h.Kind==r.Kind && h.Id==r.Id && h.Revision==r.Revision));
    private static DowntimeLifecycleEventDto EventDto(ShipOpsLifecycleEvent e)=>new(e.Id,e.FromRevision,e.ToRevision,e.Action,e.FromState,e.ToState,e.ActorId,e.RoleId,e.Duty,e.ReasonCode,
        e.DetailJson=="{}"?null:JsonSerializer.Deserialize<DowntimeEvidencePin>(e.DetailJson,Json),e.SavedAt);

    private static async Task<IResult> Lifecycle(Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;if(!Query(h,false,out var vessel,out _))return await Fail(h,400,"invalid_query");
        var actor=Actor(h);if(!CanRead(actor))return await Fail(h,403,"permission_denied");var rights=Rights(actor!);
        if(!await Vessels(db,actor!,vessel).AnyAsync(ct))return await Fail(h,404,"resource_not_found");
        var head=await db.Downtimes.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==id && r.VesselId==vessel,ct);if(head==null)return await Fail(h,404,"resource_not_found");
        var fields=DowntimePolicy.ReadFields(vessel,head.FieldsJson);var canWrite=CanWrite(actor,vessel);
        var authorities=CanReview(actor,vessel)?ShipOpsLifecycleAuthority.Resolve(h,actor,vessel,ShipOperationsLifecycle.DowntimeScope):[];
        var recorder=(await Recorders(db,vessel,id,ct)).Contains(actor!.Id);
        var allowed=ShipOperationsLifecycle.Allowed(head.ClosureState).Where(a=>ShipOperationsLifecycle.ReviewActions.Contains(a)
            ? authorities.Length>0 && (a==ShipOperationsLifecycle.Return || !recorder) : canWrite).ToArray();
        DowntimeEvidenceOptionDto[] options=[];
        if(canWrite && ControlEvidenceEndpoints.Read(actor) && ShipOperationsLifecycle.Editable(head.ClosureState))
            options=(await CurrentEvidence(db,actor,vessel).OrderByDescending(r=>r.SavedAt).ThenBy(r=>r.Id).Select(r=>new{r.Id,r.Revision,r.Lifecycle,r.SavedAt}).Take(25).ToArrayAsync(ct))
                .Select(r=>new DowntimeEvidenceOptionDto(r.Id,r.Revision,r.Lifecycle+" · r"+r.Revision+" · "+r.SavedAt.UtcDateTime.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var events=await db.ShipOpsLifecycleEvents.AsNoTracking().Where(e=>e.VesselId==vessel && e.Module=="Downtime" && e.RecordId==id).OrderBy(e=>e.ToRevision).Take(100).ToArrayAsync(ct);
        var result=new DowntimeLifecycleViewDto(id,vessel,head.Revision,head.Version,ShipOperationsLifecycle.State(head.ClosureState),ShipOperationsLifecycle.LossOfHireCandidate(fields),
            Evidence(head.ClaimEvidenceJson),allowed,ShipOpsLifecycleAuthority.Dto(authorities),recorder,ShipOperationsLifecycle.DowntimeMissingRequired(fields),options,
            events.Select(EventDto).ToArray(),LifecycleQualifications);
        DowntimeRecordDto[] records=[Dto(head,true,false,Now(h))];
        if(!await Current(h,vessel,rights,records))return await Fail(h,403,"permission_denied");await Audit(h,"Success",id);
        if(!await Current(h,vessel,rights,records))return await Fail(h,403,"permission_denied");return Results.Ok(result);
    }

    private static async Task<IResult> LifecycleCommand(Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;var actor=Actor(h);
        if(!CanRead(actor) || !actor!.HasWriteAttribution || !actor.Permissions.Contains("operations.downtime.write"))return await Fail(h,403,"permission_denied");
        if(!Query(h,false,out var vessel,out _))return await Fail(h,400,"invalid_query");if(!CanWrite(actor,vessel))return await Fail(h,403,"permission_denied");
        if(!await Vessels(db,actor,vessel).AnyAsync(ct))return await Fail(h,404,"resource_not_found");var rights=Rights(actor);
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;if(key==null || !Guid.TryParseExact(key,"D",out _))return await Fail(h,400,"invalid_idempotency_key");
        DowntimeLifecycleCommand command;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var buffer=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,16384,ct);
            using var document=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=4});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command=document.RootElement.Deserialize<DowntimeLifecycleCommand>(Json)??throw new JsonException();
            var review=ShipOperationsLifecycle.ReviewActions.Contains(command.Action);var evidence=command.Action is ShipOperationsLifecycle.LinkEvidence or ShipOperationsLifecycle.UnlinkEvidence;
            if(command.ExpectedVersion==0 || command.ReasonCode!=LifecycleReason || !ShipOperationsLifecycle.DowntimeActions.Contains(command.Action) ||
                review!=(command.AuthorityId!=null) || command.AuthorityId==Guid.Empty || evidence!=(command.Evidence!=null) ||
                command.Evidence!=null && (command.Evidence.Id==Guid.Empty || command.Evidence.Revision<1))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,400,"invalid_request");}
        var isReview=command.AuthorityId!=null;
        if(isReview && !CanReview(actor,vessel))return await Fail(h,403,"permission_denied");
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,id,command.ExpectedVersion,command.Action,command.ReasonCode,command.AuthorityId,command.Evidence},Json));
        async Task<IResult> Outcome(string json) {
            try {
                var saved=JsonSerializer.Deserialize<DowntimeRecordDto>(json,Json)??throw new InvalidDataException("downtime_receipt_unavailable");
                await Audit(h,"Success",id);if(!await Current(h,vessel,rights,[saved],true))return await Fail(h,503,"command_outcome_withheld");return Results.Json(saved,Json);
            }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
        }
        async Task<IResult?> Replay() {
            if(!await Current(h,vessel,rights,[],true))return await Fail(h,403,"permission_denied");
            var receipt=await db.DowntimeReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,ct);if(receipt==null)return null;
            if(receipt.VesselId!=vessel || receipt.DowntimeId!=id || receipt.Fingerprint!=fingerprint)return await Fail(h,409,"idempotency_key_reused");return await Outcome(receipt.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;
        var existing=await db.Downtimes.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==id && r.VesselId==vessel,ct);if(existing==null)return await Fail(h,404,"resource_not_found");
        ShipOperationsAuthority? authority=null;string? authorityHash=null;
        if(isReview) {
            authority=ShipOpsLifecycleAuthority.Resolve(h,actor,vessel,ShipOperationsLifecycle.DowntimeScope).SingleOrDefault(g=>g.Id==command.AuthorityId);
            if(authority==null)return await Fail(h,403,"human_authority_required");authorityHash=ShipOpsLifecycleAuthority.Hash(authority,actor);
        }
        DowntimeRecordDto[] previous=[Dto(existing,true,true,Now(h))];
        async Task<bool> Fence()=>await Current(h,vessel,rights,previous,true) && (authority==null || CanReview(Actor(h),vessel) &&
            ShipOpsLifecycleAuthority.Resolve(h,Actor(h),vessel,ShipOperationsLifecycle.DowntimeScope).Any(g=>g.Id==authority.Id && ShipOpsLifecycleAuthority.Hash(g,actor)==authorityHash));
        if(!await Fence())return await Replay()??await Fail(h,existing.Version!=command.ExpectedVersion?409:403,existing.Version!=command.ExpectedVersion?"version_conflict":"permission_denied");
        await Audit(h,"Attempted",id);IDbContextTransaction? tx=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var now=Now(h);
            if(db.TenantId!=actor.TenantId)return await Fail(h,403,"permission_denied");
            var row=await db.Downtimes.SingleOrDefaultAsync(r=>r.Id==id && r.VesselId==vessel,ct);if(row==null)return await Fail(h,404,"resource_not_found");
            if(row.Version!=command.ExpectedVersion)return await Replay()??await Fail(h,409,"version_conflict");
            if(row.SourceVersion!=DowntimePolicy.SourceVersion)return await Fail(h,409,"source_contract_changed");
            if(!ShipOperationsLifecycle.Valid(row.ClosureState,command.Action))return await Fail(h,409,"invalid_transition");
            var fields=DowntimePolicy.ReadFields(vessel,row.FieldsJson);
            if(command.Action is ShipOperationsLifecycle.Submit or ShipOperationsLifecycle.Verify or ShipOperationsLifecycle.Close && ShipOperationsLifecycle.DowntimeMissingRequired(fields).Length>0)
                return await Fail(h,422,"downtime_record_incomplete");
            if(command.Action is ShipOperationsLifecycle.Verify or ShipOperationsLifecycle.Close && (await Recorders(db,vessel,id,ct)).Contains(actor.Id))
                return await Fail(h,403,"separation_of_duties");
            var pins=Evidence(row.ClaimEvidenceJson).ToList();
            if(command.Action==ShipOperationsLifecycle.LinkEvidence) {
                if(!ControlEvidenceEndpoints.Read(actor) || !await CurrentEvidence(db,actor,vessel).AnyAsync(r=>r.Id==command.Evidence!.Id && r.Revision==command.Evidence.Revision,ct))
                    return await Fail(h,400,"linked_resource_unavailable");
                pins.RemoveAll(p=>p.Id==command.Evidence!.Id);if(pins.Count>=ShipOperationsLifecycle.MaxClaimEvidence)return await Fail(h,400,"invalid_request");pins.Add(command.Evidence!);
            }else if(command.Action==ShipOperationsLifecycle.UnlinkEvidence && pins.RemoveAll(p=>p==command.Evidence)==0)return await Fail(h,400,"invalid_request");
            var fromState=row.ClosureState;var from=row.Revision;var origin=Origin(row.OriginJson);
            row.Revision++;row.ClosureState=ShipOperationsLifecycle.Next(fromState,command.Action);
            row.ClaimEvidenceJson=pins.Count==0?null:JsonSerializer.Serialize(pins.OrderBy(p=>p.Id).ToArray(),Json);
            row.ActorId=actor.Id;row.SavedAt=now;row.Hash=DowntimeWriter.Hash(vessel,row,fields,origin);await db.SaveChangesAsync(ct);
            db.DowntimeRevisions.Add(new(){TenantId=row.TenantId,VesselId=vessel,DowntimeId=row.Id,Revision=row.Revision,RecordVersion=row.Version,FieldsJson=row.FieldsJson,SourceVersion=row.SourceVersion,
                Hash=row.Hash,OriginJson=row.OriginJson,Provenance=row.Provenance,ActorId=actor.Id,SavedAt=now,ClosureState=row.ClosureState,ClaimEvidenceJson=row.ClaimEvidenceJson});
            db.ShipOpsLifecycleEvents.Add(new(){TenantId=actor.TenantId,VesselId=vessel,Id=Guid.NewGuid(),Module="Downtime",RecordId=id,FromRevision=from,ToRevision=row.Revision,Action=command.Action,
                FromState=ShipOperationsLifecycle.State(fromState),ToState=ShipOperationsLifecycle.State(row.ClosureState),ActorId=actor.Id,RoleId=authority?.RoleId,AuthorityId=authority?.Id,
                Duty=authority?.Duty,AuthorityHash=authorityHash,ReasonCode=command.ReasonCode,DetailJson=command.Evidence==null?"{}":JsonSerializer.Serialize(command.Evidence,Json),RecordHash=row.Hash,SavedAt=now});
            var record=Dto(row,false,false,now);outcome=JsonSerializer.Serialize(record,Json);
            db.DowntimeReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,DowntimeId=id,Revision=row.Revision,RecordVersion=row.Version,Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});
            await db.SaveChangesAsync(ct);
            if(!await Fence())return await Fail(h,403,"permission_denied");await tx.CommitAsync(ct);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await Fail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";return await Replay()??await Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(outcome!);
    }
}
