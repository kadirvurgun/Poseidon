using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional (not in the default seed), idempotent FICTIONAL critical spare declaration. Criticality is
// UNKNOWN, no stock count and no min/max are declared, so the projection is STOCK_UNKNOWN (review
// required). No requisition, approval, work order release or execution is fabricated: a release is an
// attributed human record and is never seeded.
public static class CriticalSpareFixtures
{
    public static Guid Id(Guid vessel)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.critical-spare.v1:"+vessel.ToString("D"))).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic critical spares require an administrative audited transaction.");
        foreach(var vessel in vessels) {
            var id=Id(vessel.Id);if(await db.CriticalSpares.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct))continue;
            if(!await db.Vessels.AnyAsync(v=>v.TenantId==vessel.TenantId && v.Id==vessel.Id,ct))throw new InvalidOperationException("Synthetic critical spare scope is unavailable.");
            var content=new CriticalSpareContent(new("FICTIONAL-SP-0001","FICTIONAL spare declaration (not a real part)","pcs","UNKNOWN"),null);
            if(!CriticalSparePolicy.ValidDefinition(content.Definition))throw new InvalidOperationException("Synthetic critical spare definition unavailable.");
            var json=JsonSerializer.Serialize(content,new JsonSerializerOptions(JsonSerializerDefaults.Web));var now=DateTimeOffset.UtcNow;
            var head=new CriticalSpare{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id};db.CriticalSpares.Add(head);await db.SaveChangesAsync(ct);
            db.CriticalSpareRevisions.Add(new(){TenantId=vessel.TenantId,VesselId=vessel.Id,SpareId=id,Revision=1,RecordVersion=head.Version,Transition="CREATE",ContentJson=json,
                SourceVersion=CriticalSparePolicy.SourceVersion,Provenance="SyntheticUnverified",SavedAt=now,
                Hash=RegisterStorage.Hash("SyntheticUnverified:"+vessel.TenantId+":"+vessel.Id+":"+json)});
            await db.SaveChangesAsync(ct);
        }
        // Caller commits. Operator declarations are never overwritten.
    }
}
