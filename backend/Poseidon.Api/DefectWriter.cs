using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record DefectApply(DefectRecordDto? Record,int Status=200,string? Code=null);
internal static class DefectWriter
{
    internal static async Task<DefectApply> Apply(PoseidonDbContext db,TrustedActor actor,Guid vessel,DefectDraftCommand command,bool create,DateTimeOffset now,CancellationToken ct) {
        if(db.Database.CurrentTransaction==null || actor.Id==Guid.Empty || vessel==Guid.Empty || command.Id==Guid.Empty)throw new InvalidOperationException("Defect apply requires an attributed scoped transaction.");
        if(db.TenantId!=actor.TenantId || !DefectEndpoints.CanWrite(actor,vessel))return new(null,403,"permission_denied");
        if(!DefectPolicy.TryNormalize(command.Fields,out var fields,out _))return new(null,400,"invalid_request");
        if(command.SourceVersion!=DefectPolicy.SourceVersion)return new(null,409,"source_contract_changed");
        var row=await db.Defects.SingleOrDefaultAsync(r=>r.Id==command.Id,ct);
        if(row!=null && row.VesselId!=vessel)return new(null,404,"resource_not_found");
        if(create ? row!=null || command.ExpectedVersion!=0 : row==null)return new(null,create?409:404,create?"version_conflict":"resource_not_found");
        if(row!=null && row.Version!=command.ExpectedVersion)return new(null,409,"version_conflict");
        if(row!=null && row.SourceVersion!=DefectPolicy.SourceVersion)return new(null,409,"source_contract_changed");
        if(row==null){row=new(){TenantId=actor.TenantId,VesselId=vessel,Id=command.Id};db.Defects.Add(row);}else row.Revision++;
        row.FieldsJson=JsonSerializer.Serialize(fields,DefectEndpoints.Json);row.SourceVersion=DefectPolicy.SourceVersion;row.AssetId=command.Asset?.Id;row.AssetRevision=command.Asset?.Revision;
        row.AssetVersion=command.Asset?.Version;row.AssetHash=command.Asset?.Hash;row.Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,row.Id,row.Revision,fields,command.Asset,row.SourceVersion},DefectEndpoints.Json));
        row.Provenance="UserDeclaredUnverified";row.ActorId=actor.Id;row.SavedAt=now;await db.SaveChangesAsync(ct);
        db.DefectRevisions.Add(new(){TenantId=row.TenantId,VesselId=vessel,DefectId=row.Id,Revision=row.Revision,RecordVersion=row.Version,FieldsJson=row.FieldsJson,SourceVersion=row.SourceVersion,
            Hash=row.Hash,AssetId=row.AssetId,AssetRevision=row.AssetRevision,AssetVersion=row.AssetVersion,AssetHash=row.AssetHash,Provenance=row.Provenance,ActorId=row.ActorId,SavedAt=now});
        await db.SaveChangesAsync(ct);return new(DefectEndpoints.Dto(row,false,false,now));
    }
}
