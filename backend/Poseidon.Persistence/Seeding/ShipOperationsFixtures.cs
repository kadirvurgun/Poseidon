using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional incomplete fictional module drafts. Nothing is activated at startup.
// No BDN, position, supplier, evidence, verification or user receipt is seeded.
public static class ShipOperationsFixtures
{
    public static Guid Id(Guid vessel,string kind)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.ship-operations.v1:"+vessel.ToString("D")+":"+kind)).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic ship operations require an administrative audited transaction.");
        foreach(var vessel in vessels.Where(v=>v.Kind==VesselKind.Ship))foreach(var kind in ShipOperationsPolicy.Kinds) {
            var id=Id(vessel.Id,kind);if(await db.ShipOperations.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct))continue;
            if(!await db.Vessels.AnyAsync(v=>v.TenantId==vessel.TenantId && v.Id==vessel.Id && v.Kind==VesselKind.Ship,ct))throw new InvalidOperationException("Synthetic ship scope is unavailable.");
            if(!ShipOperationsPolicy.TryNormalize(kind,new Dictionary<string,ShipOpsFact>(),out var fields,out _))throw new InvalidOperationException("Synthetic draft schema unavailable.");
            var json=JsonSerializer.Serialize(fields,new JsonSerializerOptions(JsonSerializerDefaults.Web));var now=DateTimeOffset.UtcNow;
            var row=new ShipOperation{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id,Kind=kind,FieldsJson=json,SourceVersion=ShipOperationsPolicy.SourceVersion,
                Hash=RegisterStorage.Hash("SyntheticUnverified:"+vessel.TenantId+":"+vessel.Id+":"+kind+":"+json),Provenance="SyntheticUnverified",SavedAt=now};
            db.ShipOperations.Add(row);await db.SaveChangesAsync(ct);
            db.ShipOperationRevisions.Add(new(){TenantId=row.TenantId,VesselId=row.VesselId,RecordId=id,Kind=kind,SourceVersion=row.SourceVersion,Revision=1,RecordVersion=row.Version,
                FieldsJson=json,Hash=row.Hash,Provenance=row.Provenance,SavedAt=now});await db.SaveChangesAsync(ct);
        }
        // The caller commits; existing operator state is never updated or deleted.
    }
}
