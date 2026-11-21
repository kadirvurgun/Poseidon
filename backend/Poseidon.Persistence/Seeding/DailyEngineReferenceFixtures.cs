using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional owned-lab synthetic lineage only: links each ship's synthetic engine draft
// to its synthetic work-order draft at that draft's current exact revision. No review,
// verification, closure, receipt or human decision is fabricated. Requires both the
// DailyOperationsFixtures and WorkOrderFixtures rows; absent sources are skipped.
public static class DailyEngineReferenceFixtures
{
    public static Guid EventId(Guid vessel) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.engine-reference.v1:"+vessel.ToString("D"))).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)
            throw new UnauthorizedAccessException("Synthetic engine log references require an administrative audited transaction.");
        foreach(var vessel in vessels.Where(v=>v.Kind==VesselKind.Ship)) {
            var id=EventId(vessel.Id);
            // Existing operator links (including a later UNLINK of this fixture) are preserved.
            if(await db.DailyEngineReferenceEvents.AnyAsync(e=>e.TenantId==vessel.TenantId && e.Id==id,ct)) continue;
            var recordId=DailyOperationsFixtures.Id(vessel.Id,"Engine");var workOrderId=WorkOrderFixtures.Id(vessel.Id);
            var record=await db.DailyOperations.SingleOrDefaultAsync(r=>r.TenantId==vessel.TenantId && r.VesselId==vessel.Id && r.Kind=="Engine" && r.Id==recordId,ct);
            var target=await db.WorkOrders.SingleOrDefaultAsync(r=>r.TenantId==vessel.TenantId && r.VesselId==vessel.Id && r.Id==workOrderId,ct);
            if(record==null || target==null || target.SourceVersion!=WorkOrderPolicy.SourceVersion) continue;
            if(await db.DailyEngineReferenceHeads.AnyAsync(h=>h.TenantId==vessel.TenantId && h.RecordId==recordId,ct)) continue;
            var now=db.Clock.GetUtcNow();
            db.DailyEngineReferenceHeads.Add(new(){TenantId=vessel.TenantId,VesselId=vessel.Id,RecordId=recordId,Sequence=1});
            await db.SaveChangesAsync(ct);
            db.DailyEngineReferenceEvents.Add(new(){TenantId=vessel.TenantId,VesselId=vessel.Id,RecordId=recordId,Id=id,Sequence=1,
                RecordRevision=record.Revision,RecordVersion=record.Version,Operation="LINK",TargetKind="WorkOrder",WorkOrderId=target.Id,
                WorkOrderRevision=target.Revision,TargetVersion=target.Version,TargetHash=target.Hash,TargetSourceVersion=target.SourceVersion,
                SourceVersion=DailyEngineReferencePolicy.SourceVersion,ActorId=SystemActor,SavedAt=now});
            await db.SaveChangesAsync(ct);
        }
        // Caller commits. No HTTP idempotency receipt pretends a user command ran.
    }
    // Deterministic synthetic system actor reference; never a human or appointment.
    public static readonly Guid SystemActor=new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.system-seed-import.v1")).AsSpan(0,16));
}
