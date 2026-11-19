using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateRenewalTransitionCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,
    [property:JsonRequired] long ExpectedRevision,[property:JsonRequired] string Action,[property:JsonRequired] CertificateRenewalDecision Decision);

// DEV-044 renewal workflow v1 transitions (60.04 VC-012 p180-200, SoD p196/p252-253).
// Each transition appends one immutable renewal revision under the head row version.
// VERIFY/COMPLETE are Human-only, separately permissioned and separated from the
// candidate recorder/replacement author/verifier. COMPLETE writes one immutable
// supersession; the original certificate revisions and history are never modified.
public static class CertificateRenewalTransitions
{
    private static readonly JsonSerializerOptions Input=new(CertificateReader.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    private static int Status(string code)=>code is "invalid_transition" or "terminal_review" or "renewal_cancellation_receipt_required" or "linked_version_conflict"
        or "source_revision_changed" or "separation_of_duties" or "certificate_already_superseded"?409:422;
    internal static async Task<IResult> Transition(Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var actor=CertificateEndpoints.Actor(h);
        if(actor==null || !CertificateRenewalEndpoints.Query(h,false,out var vessel,out _) || !CertificateRenewalEndpoints.Write(actor,vessel))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await CertificateEndpoints.Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;if(key==null || !Guid.TryParseExact(key,"D",out _))return await CertificateEndpoints.Fail(h,400,"invalid_idempotency_key");
        CertificateRenewalTransitionCommand command;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var bytes=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,bytes,65536,h.RequestAborted);
            using var document=JsonDocument.Parse(bytes.ToArray(),new(){MaxDepth=8});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command=document.RootElement.Deserialize<CertificateRenewalTransitionCommand>(Input)??throw new JsonException();
            if(command.Id!=id || command.ExpectedRevision<=0 || command.ExpectedVersion==0 || command.Action==null || !CertificateRenewalWorkflow.Transitions.ContainsKey(command.Action) ||
                command.Decision==null || !CertificateRenewalWorkflow.DecisionShape(command.Action,command.Decision))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await CertificateEndpoints.Fail(h,400,"validation_failed");}
        if(CertificateRenewalWorkflow.HumanOnly(command.Action) && !CertificateRenewalEndpoints.Human(h,actor))return await CertificateEndpoints.Fail(h,403,"human_actor_required");
        if(!CertificateRenewalEndpoints.TransitionPermitted(h,actor,vessel,command.Action))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        var rights=CertificateReader.Rights(actor);var ownerHash=CertificateRenewalEndpoints.OwnerHash(h,actor,vessel);var owners=CertificateRenewalEndpoints.Owners(h,actor,vessel);
        if(owners.Length>256)return await CertificateEndpoints.Fail(h,403,"owner_unavailable");
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,id,command},CertificateReader.Json));
        async Task<IResult> Outcome(CertificateRenewalDto record,string json) {
            try {
                await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateCommand","Success",resourceId:record.Id);
                if(!await CertificateRenewalEndpoints.Current(h,vessel,rights,[record],ownerHash,true))return await CertificateEndpoints.Fail(h,503,"command_outcome_withheld");
                return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
        }
        async Task<IResult?> Replay() {
            if(!await CertificateRenewalEndpoints.Current(h,vessel,rights,[],ownerHash,true))return await CertificateEndpoints.Fail(h,403,"permission_denied");
            var receipt=await db.CertificateCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,h.RequestAborted);if(receipt==null)return null;
            if(receipt.Action!="RenewalTransition" || receipt.VesselId!=vessel || receipt.ResourceId!=id || receipt.Fingerprint!=fingerprint)return await CertificateEndpoints.Fail(h,409,"idempotency_key_reused");
            var record=JsonSerializer.Deserialize<CertificateRenewalDto>(receipt.OutcomeJson,CertificateReader.Json)??throw new InvalidDataException("renewal_receipt_unavailable");
            return await Outcome(record,receipt.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;
        IDbContextTransaction? tx=null;CertificateRenewalDto? saved=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            var head=await db.CertificateRenewals.SingleOrDefaultAsync(r=>r.Id==id && r.VesselId==vessel,h.RequestAborted);
            if(head==null)return await CertificateEndpoints.Fail(h,404,"resource_not_found");
            if(head.Version!=command.ExpectedVersion || head.Revision!=command.ExpectedRevision)return await Replay()??await CertificateEndpoints.Fail(h,409,"version_conflict");
            var current=await CertificateRenewalEndpoints.Visible(db,actor,vessel).SingleOrDefaultAsync(r=>r.RenewalId==id,h.RequestAborted);
            if(current==null)return await CertificateEndpoints.Fail(h,404,"resource_not_found");
            var content=CertificateRenewalEndpoints.Content(current);var decision=command.Decision;var now=CertificateEndpoints.Now(h);var today=DateOnly.FromDateTime(now.UtcDateTime);
            var original=await CertificateReader.Visible(db,actor,vessel,false).SingleOrDefaultAsync(r=>r.CertificateId==current.CertificateId && r.Revision==current.CertificateRevision,h.RequestAborted);
            if(original==null)return await CertificateEndpoints.Fail(h,409,"source_revision_changed");
            // Replacement facts always come from the exact pinned owning revision.
            var candidate=command.Action=="RECORD_ISSUED_CANDIDATE";
            var pinId=candidate?decision.NewCertificateId:content.NewCertificateId;var pinRevision=candidate?decision.NewCertificateRevision:content.NewCertificateRevision;
            var pinVersion=candidate?decision.NewCertificateVersion:content.NewCertificateVersion;
            if(pinId==current.CertificateId)return await CertificateEndpoints.Fail(h,400,"validation_failed");
            VesselCertificateRevision? replacementRow=null;RenewalReplacementFacts? replacement=null;
            if(pinId is {} rid && pinRevision is {} rrev && pinVersion is {} rver) {
                replacementRow=await CertificateReader.Visible(db,actor,vessel,false).SingleOrDefaultAsync(r=>r.CertificateId==rid && r.Revision==rrev && r.RecordVersion==rver,h.RequestAborted);
                if(replacementRow==null)return await CertificateEndpoints.Fail(h,409,"linked_version_conflict");
                var currentHead=await db.VesselCertificates.AnyAsync(c=>c.VesselId==vessel && c.Id==rid && c.Revision==rrev,h.RequestAborted);
                replacement=new(rid,rrev,replacementRow.IssueDate,replacementRow.ExpiryDate,
                    replacementRow.FileId is {} file?new CertificateFilePin(file,replacementRow.FileRevision!.Value,replacementRow.Checksum!):null,currentHead);
            }
            foreach(var pin in new[]{decision.NewDocument,decision.Evidence})
                if(!await CertificateReader.FileAllowed(db,actor,vessel,pin,h.RequestAborted))return await CertificateEndpoints.Fail(h,403,"linked_resource_unavailable");
            var eligible=content.OwnerRoleId is {} owner && owners.Contains(owner);
            var error=CertificateRenewalWorkflow.Check(command.Action,current.State,content,decision,eligible,original.ExpiryDate,today,replacement);
            if(error!=null)return await CertificateEndpoints.Fail(h,Status(error),error);
            if(command.Action is "VERIFY" or "COMPLETE") {
                var history=await db.CertificateRenewalRevisions.AsNoTracking().Where(r=>r.VesselId==vessel && r.RenewalId==id && r.Transition!=null)
                    .OrderByDescending(r=>r.Revision).Select(r=>new{r.Transition,r.ActorId}).ToArrayAsync(h.RequestAborted);
                var recorder=history.FirstOrDefault(r=>r.Transition=="RECORD_ISSUED_CANDIDATE")?.ActorId;var verifier=history.FirstOrDefault(r=>r.Transition=="VERIFY")?.ActorId;
                if(!CertificateRenewalWorkflow.Separated(command.Action,actor.Id,recorder,replacementRow?.ActorId,verifier))return await CertificateEndpoints.Fail(h,409,"separation_of_duties");
            }
            if(command.Action=="COMPLETE") {
                // The original head must still be the pinned revision; a later correction needs a new case.
                if(!await db.VesselCertificates.AnyAsync(c=>c.VesselId==vessel && c.Id==current.CertificateId && c.Revision==current.CertificateRevision,h.RequestAborted))
                    return await CertificateEndpoints.Fail(h,409,"source_revision_changed");
                if(await db.CertificateSupersessions.AnyAsync(s=>s.CertificateId==current.CertificateId || s.ReplacementCertificateId==replacement!.CertificateId,h.RequestAborted))
                    return await CertificateEndpoints.Fail(h,409,"certificate_already_superseded");
            }
            var previous=CertificateRenewalEndpoints.Dto(current,actor,true,owners,h);var to=CertificateRenewalWorkflow.Transitions[command.Action].To;
            head.Revision++;
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateCommand","Attempted",resourceId:id);
            await db.SaveChangesAsync(h.RequestAborted);
            var next=CertificateRenewalWorkflow.Apply(command.Action,content,decision,replacement);
            var json=JsonSerializer.Serialize(next,CertificateReader.Json);var decisionJson=JsonSerializer.Serialize(decision,CertificateReader.Json);
            var row=new CertificateRenewalRevision{TenantId=actor.TenantId,VesselId=vessel,RenewalId=id,CertificateId=current.CertificateId,CertificateRevision=current.CertificateRevision,
                CertificateVersion=current.CertificateVersion,Revision=head.Revision,RecordVersion=head.Version,ContentJson=json,
                Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,id,head.Revision,current.CertificateId,current.CertificateRevision,current.CertificateVersion,json,to,command.Action,decisionJson},CertificateReader.Json)),
                SourceVersion=VesselCertificatePolicy.SourceVersion,NewCertificateId=next.NewCertificateId,NewCertificateRevision=next.NewCertificateRevision,NewCertificateVersion=next.NewCertificateVersion,
                FileId=next.NewDocument?.Id,FileRevision=next.NewDocument?.Revision,Checksum=next.NewDocument?.Checksum,State=to,Transition=command.Action,DecisionJson=decisionJson,ActorId=actor.Id,SavedAt=now};
            db.CertificateRenewalRevisions.Add(row);
            if(command.Action=="COMPLETE")
                db.CertificateSupersessions.Add(new(){TenantId=actor.TenantId,VesselId=vessel,CertificateId=current.CertificateId,CertificateRevision=current.CertificateRevision,
                    ReplacementCertificateId=replacement!.CertificateId,ReplacementRevision=replacement.Revision,RenewalId=id,RenewalRevision=head.Revision,
                    EffectiveDate=replacement.IssueDate!.Value,ActorId=actor.Id,CompletedAt=now});
            saved=CertificateRenewalEndpoints.Dto(row,actor,false,owners,h);outcome=JsonSerializer.Serialize(saved,CertificateReader.Json);
            db.CertificateCommandReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,ResourceId=id,CertificateId=current.CertificateId,RenewalId=id,
                Action="RenewalTransition",Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});
            await db.SaveChangesAsync(h.RequestAborted);
            var refs=new List<CertificateDto>{CertificateReader.Dto(original,actor,today,false)};if(replacementRow!=null)refs.Add(CertificateReader.Dto(replacementRow,actor,today,false));
            var files=new[]{decision.NewDocument,decision.Evidence}.OfType<CertificateFilePin>().ToArray();
            if(!await CertificateRenewalEndpoints.Current(h,vessel,rights,[previous],ownerHash,true,refs.ToArray(),files))return await CertificateEndpoints.Fail(h,403,"permission_denied");
            await tx.CommitAsync(h.RequestAborted);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await CertificateEndpoints.Fail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";return await Replay()??await CertificateEndpoints.Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(saved!,outcome!);
    }
}
