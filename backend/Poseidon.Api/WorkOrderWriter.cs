using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record WorkOrderApply(WorkOrderRecordDto? Record,int Status=200,string? Code=null);
internal static class WorkOrderWriter
{
    internal static async Task<WorkOrderApply> Apply(PoseidonDbContext db,TrustedActor actor,Guid vessel,WorkOrderDraftCommand command,bool create,DateTimeOffset now,CancellationToken ct) {
        if(db.Database.CurrentTransaction==null || actor.Id==Guid.Empty || vessel==Guid.Empty || command.Id==Guid.Empty)throw new InvalidOperationException("WorkOrder apply requires an attributed scoped transaction.");
        if(db.TenantId!=actor.TenantId || !WorkOrderEndpoints.CanWrite(actor,vessel))return new(null,403,"permission_denied");
        if(!WorkOrderPolicy.TryNormalize(command.Fields,out var fields,out _))return new(null,400,"invalid_request");
        if(command.SourceVersion!=WorkOrderPolicy.SourceVersion)return new(null,409,"source_contract_changed");
        var row=await db.WorkOrders.SingleOrDefaultAsync(r=>r.Id==command.Id,ct);
        if(row!=null && row.VesselId!=vessel)return new(null,404,"resource_not_found");
        if(create ? row!=null || command.ExpectedVersion!=0 : row==null)return new(null,create?409:404,create?"version_conflict":"resource_not_found");
        if(row!=null && row.Version!=command.ExpectedVersion)return new(null,409,"version_conflict");
        if(row!=null && row.SourceVersion!=WorkOrderPolicy.SourceVersion)return new(null,409,"source_contract_changed");
        if(row==null){row=new(){TenantId=actor.TenantId,VesselId=vessel,Id=command.Id};db.WorkOrders.Add(row);}else row.Revision++;
        row.FieldsJson=JsonSerializer.Serialize(fields,WorkOrderEndpoints.Json);row.SourceVersion=WorkOrderPolicy.SourceVersion;row.PlanId=command.Plan?.Id;row.PlanRevision=command.Plan?.Revision;
        row.PlanVersion=command.Plan?.Version;row.PlanHash=command.Plan?.Hash;row.PlanSourceVersion=command.Plan?.SourceVersion;row.Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,row.Id,row.Revision,fields,command.Plan,row.SourceVersion},WorkOrderEndpoints.Json));
        row.Provenance="UserDeclaredUnverified";row.ActorId=actor.Id;row.SavedAt=now;await db.SaveChangesAsync(ct);
        db.WorkOrderRevisions.Add(new(){TenantId=row.TenantId,VesselId=vessel,WorkOrderId=row.Id,Revision=row.Revision,RecordVersion=row.Version,FieldsJson=row.FieldsJson,SourceVersion=row.SourceVersion,
            Hash=row.Hash,PlanId=row.PlanId,PlanRevision=row.PlanRevision,PlanVersion=row.PlanVersion,PlanHash=row.PlanHash,PlanSourceVersion=row.PlanSourceVersion,Provenance=row.Provenance,ActorId=row.ActorId,SavedAt=now});
        await db.SaveChangesAsync(ct);return new(WorkOrderEndpoints.Dto(row,false,false,now));
    }
}
