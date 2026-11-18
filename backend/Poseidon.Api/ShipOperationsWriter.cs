using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record ShipOperationApply(ShipOpsRecordDto? Record,int Status=200,string? Code=null);
internal static class ShipOperationsWriter
{
    // Caller owns authorization/current origin checks, audited SERIALIZABLE
    // transaction and receipt. This seam never synthesizes an HTTP/system actor.
    internal static async Task<ShipOperationApply> Apply(PoseidonDbContext db,TrustedActor actor,Guid vessel,string kind,ShipOpsDraftCommand command,
        bool create,DateTimeOffset now,CancellationToken ct) {
        if(db.Database.CurrentTransaction==null || actor.Id==Guid.Empty || vessel==Guid.Empty || command.Id==Guid.Empty)
            throw new InvalidOperationException("Ship operation apply requires an attributed scoped transaction.");
        if(db.TenantId!=actor.TenantId || !ShipOperationsEndpoints.CanWrite(actor,kind,vessel))return new(null,403,"permission_denied");
        if(!ShipOperationsPolicy.TryNormalize(kind,command.Fields,out var fields,out _))return new(null,400,"invalid_request");
        if(create && command.ExpectedVersion!=0)return new(null,409,"version_conflict");
        var row=await db.ShipOperations.SingleOrDefaultAsync(r=>r.Id==command.Id,ct);
        if(create ? row!=null : row==null)return new(null,create?409:404,create?"version_conflict":"resource_not_found");
        if(row!=null && (row.VesselId!=vessel || row.Kind!=kind))return new(null,404,"resource_not_found");
        if(row!=null && row.Version!=command.ExpectedVersion)return new(null,409,"version_conflict");
        if(row!=null && row.SourceVersion!=ShipOperationsPolicy.SourceVersion)return new(null,409,"source_contract_changed");
        if(row==null){row=new(){TenantId=actor.TenantId,VesselId=vessel,Id=command.Id,Kind=kind};db.ShipOperations.Add(row);}else row.Revision++;
        var origin=command.Origin==null?null:JsonSerializer.Serialize(command.Origin,ShipOperationsEndpoints.Json);
        row.FieldsJson=JsonSerializer.Serialize(fields,ShipOperationsEndpoints.Json);row.OriginJson=origin;
        row.SourceVersion=ShipOperationsPolicy.SourceVersion;row.Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{kind,vessel,row.Id,row.Revision,fields,command.Origin,sourceVersion=row.SourceVersion},ShipOperationsEndpoints.Json));
        row.Provenance="UserDeclaredUnverified";row.ActorId=actor.Id;row.SavedAt=now;
        row.OccurredAt=Instant(kind=="Fuel"?"record_date":"event_start");row.EndedAt=kind=="Port"?Instant("event_end"):null;
        DateTimeOffset? Instant(string key)=>fields.TryGetValue(key,out var fact) && fact.State=="Known" && ShipOperationsPolicy.TryInstant(fact.Value,out var value)?value:null;
        await db.SaveChangesAsync(ct);
        db.ShipOperationRevisions.Add(new(){TenantId=row.TenantId,VesselId=row.VesselId,RecordId=row.Id,Kind=row.Kind,Revision=row.Revision,RecordVersion=row.Version,
            FieldsJson=row.FieldsJson,OriginJson=row.OriginJson,SourceVersion=row.SourceVersion,Hash=row.Hash,Provenance=row.Provenance,ActorId=row.ActorId,SavedAt=row.SavedAt,OccurredAt=row.OccurredAt,EndedAt=row.EndedAt});
        await db.SaveChangesAsync(ct);return new(ShipOperationsEndpoints.Dto(row,false,false));
    }
}
