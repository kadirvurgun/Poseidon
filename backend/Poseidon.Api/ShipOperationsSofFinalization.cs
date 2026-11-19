using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PortSofFinalizeCommand([property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] Guid AuthorityId,
    [property:JsonRequired] string ReasonCode,[property:JsonRequired] string[] AcknowledgedUnresolved);
public sealed record PortSofFinalizationEventDto(Guid Id,long FromRevision,long ToRevision,Guid ActorId,Guid? RoleId,string? Duty,string ReasonCode,
    string[] AcknowledgedUnresolved,string SignatureState,string RecordHash,DateTimeOffset SavedAt);
public sealed record PortSofFinalizationDto(Guid RecordId,Guid VesselId,long Revision,uint Version,string SofState,long? FinalRevision,bool CanFinalize,
    ShipOpsAuthorityDto[] Authorities,string[] MissingRequired,string[] UnresolvedReferences,PortSofFinalizationEventDto[] Events,string[] Qualifications);

// DEV-101 / T-OPS2-005 finalization (60.01 paragraphs 1636-1638, T63/T64).
// An explicit attributed human command with a provisioned duty appointment
// produces one immutable FINAL revision. It records who, when, duty and reason;
// it never records or fabricates a signature and is not an external agreement.
public static partial class ShipOperationsEndpoints
{
    internal static readonly string[] SofQualifications=["FINAL_IS_INTERNAL_NOT_EXTERNAL_AGREEMENT",ShipOperationsLifecycle.SofSignature,
        "VOYAGE_ENTITY_UNAVAILABLE_VOYAGE_UNKNOWN","LINEAGE_VIA_NOON_ORIGIN_PIN_ONLY","AMENDMENT_CREATES_NEW_REVISION"];
    private static void MapSofFinalization(RouteGroupBuilder group) {
        group.MapGet("/{id:guid}/finalization",(Guid id,HttpContext h,PoseidonDbContext db)=>Finalization(id,h,db)).WithName("PortSofFinalization").Produces<PortSofFinalizationDto>();
        group.MapPost("/{id:guid}/finalize",(Guid id,HttpContext h,PoseidonDbContext db)=>Finalize(id,h,db)).WithName("PortSofFinalize")
            .Accepts<PortSofFinalizeCommand>("application/json").Produces<ShipOpsRecordDto>();
    }
    internal static bool CanFinalize(TrustedActor? actor,Guid vessel)=>CanWrite(actor,"Port",vessel) && actor!.Permissions.Contains("operations.port.finalize");
    private static PortSofFinalizationEventDto EventDto(ShipOpsLifecycleEvent e)=>new(e.Id,e.FromRevision,e.ToRevision,e.ActorId,e.RoleId,e.Duty,e.ReasonCode,
        JsonSerializer.Deserialize<string[]>(e.DetailJson,Json)??[],e.SignatureState,e.RecordHash,e.SavedAt);

    private static async Task<IResult> Finalization(Guid id,HttpContext h,PoseidonDbContext db) {
        const string kind="Port";h.Response.Headers.CacheControl="no-store";if(!Query(h,false,out var vessel,out _))return await Fail(h,400,"invalid_query");
        var actor=Actor(h);if(!CanRead(actor,kind))return await Fail(h,403,"permission_denied");var rights=Rights(actor!);
        if(!await Vessels(db,actor!,vessel).AnyAsync(h.RequestAborted))return await Fail(h,404,"resource_not_found");
        var head=await db.ShipOperations.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==id && r.VesselId==vessel && r.Kind==kind,h.RequestAborted);
        if(head==null)return await Fail(h,404,"resource_not_found");
        var fields=ShipOperationsPolicy.ReadFields(kind,vessel,head.FieldsJson,head.SofState);
        var events=await db.ShipOpsLifecycleEvents.AsNoTracking().Where(e=>e.VesselId==vessel && e.Module=="PortSof" && e.RecordId==id).OrderBy(e=>e.ToRevision).Take(100).ToArrayAsync(h.RequestAborted);
        var authorities=CanFinalize(actor,vessel)?ShipOpsLifecycleAuthority.Resolve(h,actor,vessel,ShipOperationsLifecycle.SofScope):[];
        var missing=ShipOperationsLifecycle.SofMissingRequired(fields);
        var result=new PortSofFinalizationDto(id,vessel,head.Revision,head.Version,head.SofState??ShipOperationsLifecycle.SofDraft,head.FinalRevision,
            authorities.Length>0 && missing.Length==0 && head.SofState!=ShipOperationsLifecycle.SofFinal,ShipOpsLifecycleAuthority.Dto(authorities),missing,
            ShipOperationsLifecycle.SofUnresolvedReferences(fields),events.Select(EventDto).ToArray(),SofQualifications);
        ShipOpsRecordDto[] records=[Dto(head,true,false)];
        if(!await Current(h,kind,vessel,rights,records))return await Fail(h,403,"permission_denied");await Audit(h,"Success",id);
        if(!await Current(h,kind,vessel,rights,records))return await Fail(h,403,"permission_denied");return Results.Ok(result);
    }

    private static async Task<IResult> Finalize(Guid id,HttpContext h,PoseidonDbContext db) {
        const string kind="Port";h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;var actor=Actor(h);
        if(!CanRead(actor,kind) || !actor!.HasWriteAttribution || !actor.Permissions.Contains("operations.port.finalize"))return await Fail(h,403,"permission_denied");
        if(!Query(h,false,out var vessel,out _))return await Fail(h,400,"invalid_query");if(!CanFinalize(actor,vessel))return await Fail(h,403,"permission_denied");
        if(!await Vessels(db,actor,vessel).AnyAsync(ct))return await Fail(h,404,"resource_not_found");var rights=Rights(actor);
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;if(key==null || !Guid.TryParseExact(key,"D",out _))return await Fail(h,400,"invalid_idempotency_key");
        PortSofFinalizeCommand command;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var buffer=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,16384,ct);
            using var document=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=4});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command=document.RootElement.Deserialize<PortSofFinalizeCommand>(Json)??throw new JsonException();
            if(command.ExpectedVersion==0 || command.AuthorityId==Guid.Empty || command.ReasonCode!=ShipOperationsLifecycle.SofReason || command.AcknowledgedUnresolved==null ||
                command.AcknowledgedUnresolved.Length>8 || command.AcknowledgedUnresolved.Any(k=>k==null || !ShipOperationsPolicy.Schema(kind).Any(f=>f.Key==k)))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await Fail(h,400,"invalid_request");}
        var acknowledged=command.AcknowledgedUnresolved.Order(StringComparer.Ordinal).ToArray();
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{action="FINALIZE",kind,vessel,id,command.ExpectedVersion,command.AuthorityId,command.ReasonCode,acknowledged},Json));
        async Task<IResult> Outcome(string json) {
            try {
                var record=JsonSerializer.Deserialize<ShipOpsRecordDto>(json,Json)??throw new InvalidDataException("ship_operation_receipt_unavailable");
                await Audit(h,"Success",id);if(!await Current(h,kind,vessel,rights,[record],true))return await Fail(h,503,"command_outcome_withheld");return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
        }
        async Task<IResult?> Replay() {
            if(!await Current(h,kind,vessel,rights,[],true))return await Fail(h,403,"permission_denied");
            var receipt=await db.ShipOperationReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,ct);if(receipt==null)return null;
            if(receipt.Kind!=kind || receipt.VesselId!=vessel || receipt.RecordId!=id || receipt.Fingerprint!=fingerprint)return await Fail(h,409,"idempotency_key_reused");return await Outcome(receipt.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;
        var existing=await db.ShipOperations.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==id && r.VesselId==vessel && r.Kind==kind,ct);
        if(existing==null)return await Fail(h,404,"resource_not_found");
        var authority=ShipOpsLifecycleAuthority.Resolve(h,actor,vessel,ShipOperationsLifecycle.SofScope).SingleOrDefault(g=>g.Id==command.AuthorityId);
        if(authority==null)return await Fail(h,403,"human_authority_required");var authorityHash=ShipOpsLifecycleAuthority.Hash(authority,actor);
        ShipOpsRecordDto[] previous=[Dto(existing,true,true)];
        async Task<bool> Fence()=>await Current(h,kind,vessel,rights,previous,true) &&
            ShipOpsLifecycleAuthority.Resolve(h,Actor(h),vessel,ShipOperationsLifecycle.SofScope).Any(g=>g.Id==authority.Id && ShipOpsLifecycleAuthority.Hash(g,actor)==authorityHash);
        if(!await Fence())return await Replay()??await Fail(h,existing.Version!=command.ExpectedVersion?409:403,existing.Version!=command.ExpectedVersion?"version_conflict":"permission_denied");
        await Audit(h,"Attempted",id);IDbContextTransaction? tx=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var now=h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            if(db.TenantId!=actor.TenantId)return await Fail(h,403,"permission_denied");
            var row=await db.ShipOperations.SingleOrDefaultAsync(r=>r.Id==id && r.VesselId==vessel && r.Kind==kind,ct);
            if(row==null)return await Fail(h,404,"resource_not_found");
            if(row.Version!=command.ExpectedVersion)return await Replay()??await Fail(h,409,"version_conflict");
            if(row.SourceVersion!=ShipOperationsPolicy.SourceVersion)return await Fail(h,409,"source_contract_changed");
            if(row.SofState==ShipOperationsLifecycle.SofFinal)return await Fail(h,409,"sof_finalized");
            var fields=ShipOperationsPolicy.ReadFields(kind,vessel,row.FieldsJson,row.SofState);
            if(ShipOperationsLifecycle.SofMissingRequired(fields).Length>0)return await Fail(h,422,"sof_record_incomplete");
            if(!ShipOperationsLifecycle.SofAcknowledged(fields,acknowledged))return await Fail(h,422,"sof_references_not_acknowledged");
            var from=row.Revision;row.Revision++;row.SofState=ShipOperationsLifecycle.SofFinal;row.FinalRevision=row.Revision;row.ActorId=actor.Id;row.SavedAt=now;
            var origin=row.OriginJson==null?null:JsonSerializer.Deserialize<ShipOperationOriginPin>(row.OriginJson,Json);
            row.Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{kind,vessel,row.Id,row.Revision,fields,origin,sourceVersion=row.SourceVersion,sofState=row.SofState,
                finalizedBy=actor.Id,authority=authority.Id,acknowledged},Json));
            await db.SaveChangesAsync(ct);
            db.ShipOperationRevisions.Add(new(){TenantId=row.TenantId,VesselId=row.VesselId,RecordId=row.Id,Kind=row.Kind,Revision=row.Revision,RecordVersion=row.Version,FieldsJson=row.FieldsJson,
                OriginJson=row.OriginJson,SourceVersion=row.SourceVersion,Hash=row.Hash,Provenance=row.Provenance,ActorId=actor.Id,SavedAt=now,OccurredAt=row.OccurredAt,EndedAt=row.EndedAt,
                SofState=row.SofState,FinalRevision=row.FinalRevision});
            db.ShipOpsLifecycleEvents.Add(new(){TenantId=actor.TenantId,VesselId=vessel,Id=Guid.NewGuid(),Module="PortSof",RecordId=id,FromRevision=from,ToRevision=row.Revision,Action="FINALIZE",
                FromState=ShipOperationsLifecycle.SofDraft,ToState=ShipOperationsLifecycle.SofFinal,ActorId=actor.Id,RoleId=authority.RoleId,AuthorityId=authority.Id,Duty=authority.Duty,
                AuthorityHash=authorityHash,ReasonCode=command.ReasonCode,DetailJson=JsonSerializer.Serialize(acknowledged,Json),RecordHash=row.Hash,SavedAt=now});
            outcome=JsonSerializer.Serialize(Dto(row,false,false),Json);
            db.ShipOperationReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,RecordId=id,Kind=kind,OriginJson=row.OriginJson,Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});
            await db.SaveChangesAsync(ct);
            if(!await Fence())return await Fail(h,403,"permission_denied");await tx.CommitAsync(ct);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await Fail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";return await Replay()??await Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(outcome!);
    }
}
