using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional, incomplete synthetic declarations only. No safeguard, restriction,
// source asset, deadline, closure, verification or effectiveness is fabricated.
public static class DefectFixtures
{
    public static Guid Id(Guid vessel)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.defect.draft.v1:"+vessel.ToString("D"))).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic defects require an administrative audited transaction.");
        foreach(var vessel in vessels) {
            var id=Id(vessel.Id);if(await db.Defects.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct))continue;
            if(!await db.Vessels.AnyAsync(v=>v.TenantId==vessel.TenantId && v.Id==vessel.Id,ct))throw new InvalidOperationException("Synthetic defect scope is unavailable.");
            if(!DefectPolicy.TryNormalize(new Dictionary<string,DefectFact>(),out var fields,out _))throw new InvalidOperationException("Synthetic defect schema unavailable.");
            var json=JsonSerializer.Serialize(fields,new JsonSerializerOptions(JsonSerializerDefaults.Web));var now=DateTimeOffset.UtcNow;
            var row=new Defect{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id,FieldsJson=json,SourceVersion=DefectPolicy.SourceVersion,
                Hash=RegisterStorage.Hash("SyntheticUnverified:"+vessel.TenantId+":"+vessel.Id+":"+json),Provenance="SyntheticUnverified",SavedAt=now};db.Defects.Add(row);await db.SaveChangesAsync(ct);
            db.DefectRevisions.Add(new(){TenantId=row.TenantId,VesselId=row.VesselId,DefectId=id,Revision=1,RecordVersion=row.Version,FieldsJson=json,SourceVersion=row.SourceVersion,
                Hash=row.Hash,Provenance=row.Provenance,SavedAt=now});await db.SaveChangesAsync(ct);
        }
        // Caller commits. Existing operator declarations and receipts stay intact.
    }
}
