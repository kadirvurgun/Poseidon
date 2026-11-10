using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional, incomplete synthetic declarations only. No safeguard, restriction,
// source plan, deadline, closure, verification or effectiveness is fabricated.
public static class WorkOrderFixtures
{
    public static Guid Id(Guid vessel)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.work-order.draft.v1:"+vessel.ToString("D"))).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic work_orders require an administrative audited transaction.");
        foreach(var vessel in vessels) {
            var id=Id(vessel.Id);if(await db.WorkOrders.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct))continue;
            if(!await db.Vessels.AnyAsync(v=>v.TenantId==vessel.TenantId && v.Id==vessel.Id,ct))throw new InvalidOperationException("Synthetic work_order scope is unavailable.");
            if(!WorkOrderPolicy.TryNormalize(new Dictionary<string,WorkOrderFact>(),out var fields,out _))throw new InvalidOperationException("Synthetic work_order schema unavailable.");
            var json=JsonSerializer.Serialize(fields,new JsonSerializerOptions(JsonSerializerDefaults.Web));var now=DateTimeOffset.UtcNow;
            var row=new WorkOrder{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id,FieldsJson=json,SourceVersion=WorkOrderPolicy.SourceVersion,
                Hash=RegisterStorage.Hash("SyntheticUnverified:"+vessel.TenantId+":"+vessel.Id+":"+json),Provenance="SyntheticUnverified",SavedAt=now};db.WorkOrders.Add(row);await db.SaveChangesAsync(ct);
            db.WorkOrderRevisions.Add(new(){TenantId=row.TenantId,VesselId=row.VesselId,WorkOrderId=id,Revision=1,RecordVersion=row.Version,FieldsJson=json,SourceVersion=row.SourceVersion,
                Hash=row.Hash,Provenance=row.Provenance,SavedAt=now});await db.SaveChangesAsync(ct);
        }
        // Caller commits. Existing operator declarations and receipts stay intact.
    }
}
