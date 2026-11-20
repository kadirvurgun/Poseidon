using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record DowntimeApply(DowntimeRecordDto? Record,int Status=200,string? Code=null);
internal static class DowntimeWriter
{
    // Draft revisions keep the original hash shape; lifecycle state and claim
    // evidence pins join the hash once present so each revision stays distinct.
    internal static string Hash(Guid vessel,Downtime row,SortedDictionary<string,DowntimeFact> fields,DowntimeOriginPin? origin)=>RegisterStorage.Hash(
        row.ClosureState==null && row.ClaimEvidenceJson==null
            ? JsonSerializer.Serialize(new{vessel,row.Id,row.Revision,fields,origin,row.SourceVersion},OperationalDowntimeEndpoints.Json)
            : JsonSerializer.Serialize(new{vessel,row.Id,row.Revision,fields,origin,row.SourceVersion,closure=row.ClosureState,evidence=row.ClaimEvidenceJson},OperationalDowntimeEndpoints.Json));
    internal static async Task<DowntimeApply> Apply(PoseidonDbContext db,TrustedActor actor,Guid vessel,DowntimeDraftCommand command,bool create,DateTimeOffset now,CancellationToken ct) {
        if(db.Database.CurrentTransaction==null || actor.Id==Guid.Empty || vessel==Guid.Empty || command.Id==Guid.Empty)throw new InvalidOperationException("Downtime apply requires an attributed scoped transaction.");
        if(db.TenantId!=actor.TenantId || !OperationalDowntimeEndpoints.CanWrite(actor,vessel))return new(null,403,"permission_denied");
        if(!await actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel && v.Kind==VesselKind.Ship,ct))return new(null,404,"resource_not_found");
        if(!DowntimePolicy.TryNormalize(command.Fields,out var fields,out _))return new(null,400,"invalid_request");
        if(command.SourceVersion!=DowntimePolicy.SourceVersion)return new(null,409,"source_contract_changed");
        var row=await db.Downtimes.SingleOrDefaultAsync(r=>r.Id==command.Id,ct);
        if(row!=null && row.VesselId!=vessel)return new(null,404,"resource_not_found");
        if(create ? row!=null || command.ExpectedVersion!=0 : row==null)return new(null,create?409:404,create?"version_conflict":"resource_not_found");
        if(row!=null && row.Version!=command.ExpectedVersion)return new(null,409,"version_conflict");
        if(row!=null && row.SourceVersion!=DowntimePolicy.SourceVersion)return new(null,409,"source_contract_changed");
        // Submitted, verified or closed content is immutable ("read-only after
        // controlled finalization"); a reviewer returns it before any correction.
        if(row!=null && !ShipOperationsLifecycle.Editable(row.ClosureState))return new(null,409,"downtime_lifecycle_locked");
        if(row==null){row=new(){TenantId=actor.TenantId,VesselId=vessel,Id=command.Id};db.Downtimes.Add(row);}else row.Revision++;
        row.FieldsJson=JsonSerializer.Serialize(fields,OperationalDowntimeEndpoints.Json);row.SourceVersion=DowntimePolicy.SourceVersion;
        row.OriginJson=command.Origin==null?null:JsonSerializer.Serialize(command.Origin,OperationalDowntimeEndpoints.Json);
        row.StartedAt=fields.TryGetValue("start_at",out var start) && start.State=="Known" && DowntimePolicy.TryInstant(start.Value,out var at)?at:null;
        row.Hash=Hash(vessel,row,fields,command.Origin);
        row.Provenance="UserDeclaredUnverified";row.ActorId=actor.Id;row.SavedAt=now;await db.SaveChangesAsync(ct);
        db.DowntimeRevisions.Add(new(){TenantId=row.TenantId,VesselId=vessel,DowntimeId=row.Id,Revision=row.Revision,RecordVersion=row.Version,FieldsJson=row.FieldsJson,SourceVersion=row.SourceVersion,
            Hash=row.Hash,OriginJson=row.OriginJson,Provenance=row.Provenance,ActorId=row.ActorId,SavedAt=now,ClosureState=row.ClosureState,ClaimEvidenceJson=row.ClaimEvidenceJson});
        await db.SaveChangesAsync(ct);return new(OperationalDowntimeEndpoints.Dto(row,false,false,now));
    }
}
