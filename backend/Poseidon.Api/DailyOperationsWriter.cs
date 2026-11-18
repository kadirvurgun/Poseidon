using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record DailyApplyResult(DailyRecordDto? Record, int Status = 200, string? Code = null);

// Transaction-owning ordinary/offline adapters validate live trusted actor,
// vessel, mode and command authority before and after this awaited apply. This
// seam neither creates actors nor commits a transaction or manufactures receipts.
internal static class DailyOperationsWriter
{
    internal static async Task<DailyApplyResult> Apply(PoseidonDbContext db, Guid actorId, Guid vessel,
        string kind, DailyDraftCommand command, bool create, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || actorId == Guid.Empty || vessel == Guid.Empty || command.Id == Guid.Empty)
            throw new InvalidOperationException("Daily apply requires attributed, scoped transaction ownership.");
        if (!DailyOperationsPolicy.TryNormalize(kind,command.Fields,out var fields,out _)) return new(null,400,"invalid_request");
        var row=await db.DailyOperations.SingleOrDefaultAsync(r=>r.Id==command.Id,ct);
        if (create && (row!=null || command.ExpectedVersion!=0)) return new(null,409,"version_conflict");
        if (!create && (row==null || row.VesselId!=vessel || row.Kind!=kind)) return new(null,404,"resource_not_found");
        if (row!=null && row.Version!=command.ExpectedVersion) return new(null,409,"version_conflict");
        if (row!=null && row.SourceVersion!=DailyOperationsPolicy.SourceVersion) return new(null,409,"source_contract_changed");
        if (row==null) { row=new(){TenantId=db.TenantId,VesselId=vessel,Id=command.Id,Kind=kind,SourceVersion=DailyOperationsPolicy.SourceVersion}; db.DailyOperations.Add(row); }
        else row.Revision++;
        var fieldsJson=JsonSerializer.Serialize(fields);
        row.FieldsJson=fieldsJson;
        row.Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new {kind,vessel,row.Id,row.Revision,source=DailyOperationsPolicy.SourceVersion,fieldsJson},DailyOperationsEndpoints.Json));
        row.Provenance="UserDeclaredUnverified"; row.ActorId=actorId; row.SavedAt=now;
        await db.SaveChangesAsync(ct);
        db.DailyOperationRevisions.Add(new(){TenantId=row.TenantId,VesselId=vessel,RecordId=row.Id,Kind=kind,SourceVersion=row.SourceVersion,Revision=row.Revision,RecordVersion=row.Version,
            FieldsJson=fieldsJson,Hash=row.Hash,Provenance=row.Provenance,ActorId=actorId,SavedAt=now});
        await db.SaveChangesAsync(ct);
        return new(DailyOperationsEndpoints.Dto(row));
    }
}
