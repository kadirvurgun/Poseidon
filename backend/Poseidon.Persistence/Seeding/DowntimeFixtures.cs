using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional, incomplete synthetic declarations only. No safeguard, restriction,
// source plan, deadline, closure, verification or effectiveness is fabricated.
public static class DowntimeFixtures
{
    public static Guid Id(Guid vessel)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.downtime.draft.v1:"+vessel.ToString("D"))).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic downtimes require an administrative audited transaction.");
        foreach(var vessel in vessels.Where(v=>v.Kind==VesselKind.Ship)) {
            var id=Id(vessel.Id);if(await db.Downtimes.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct))continue;
            if(!await db.Vessels.AnyAsync(v=>v.TenantId==vessel.TenantId && v.Id==vessel.Id,ct))throw new InvalidOperationException("Synthetic downtime scope is unavailable.");
            if(!DowntimePolicy.TryNormalize(new Dictionary<string,DowntimeFact>(),out var fields,out _))throw new InvalidOperationException("Synthetic downtime schema unavailable.");
            var json=JsonSerializer.Serialize(fields,new JsonSerializerOptions(JsonSerializerDefaults.Web));var now=DateTimeOffset.UtcNow;
            var row=new Downtime{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id,FieldsJson=json,SourceVersion=DowntimePolicy.SourceVersion,
                Hash=RegisterStorage.Hash("SyntheticUnverified:"+vessel.TenantId+":"+vessel.Id+":"+json),Provenance="SyntheticUnverified",SavedAt=now};db.Downtimes.Add(row);await db.SaveChangesAsync(ct);
            db.DowntimeRevisions.Add(new(){TenantId=row.TenantId,VesselId=row.VesselId,DowntimeId=id,Revision=1,RecordVersion=row.Version,FieldsJson=json,SourceVersion=row.SourceVersion,
                Hash=row.Hash,Provenance=row.Provenance,SavedAt=now});await db.SaveChangesAsync(ct);
        }
        // Caller commits. Existing operator declarations and receipts stay intact.
    }
}
