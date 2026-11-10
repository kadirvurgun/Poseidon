using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional owned-lab synthetic drafts. No credential, human appointment, review,
// sign-off, work order, evidence, physical position or verified reading is seeded.
public static class DailyOperationsFixtures
{
    public static Guid Id(Guid vessel,string kind) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.daily.v1:"+vessel.ToString("D")+":"+kind)).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)
            throw new UnauthorizedAccessException("Synthetic daily drafts require an administrative audited transaction.");
        foreach(var vessel in vessels.Where(v=>v.Kind==VesselKind.Ship)) foreach(var kind in DailyOperationsPolicy.Kinds) {
            var id=Id(vessel.Id,kind);
            // Existing operator data, including previously edited fixtures, is preserved.
            if(await db.DailyOperations.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct)) continue;
            if(!await db.Vessels.AnyAsync(v=>v.TenantId==vessel.TenantId && v.Id==vessel.Id && v.Kind==VesselKind.Ship,ct))
                throw new InvalidOperationException("Synthetic daily vessel is unavailable.");
            DailyOperationsPolicy.TryNormalize(kind,new Dictionary<string,DailyValue>(),out var fields,out _);
            var json=JsonSerializer.Serialize(fields); var now=DateTimeOffset.UtcNow;
            var row=new DailyOperation {TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id,Kind=kind,SourceVersion=DailyOperationsPolicy.SourceVersion,
                FieldsJson=json,Hash=RegisterStorage.Hash("SyntheticUnverified:"+vessel.Id+":"+kind+":"+json),Provenance="SyntheticUnverified",SavedAt=now};
            db.DailyOperations.Add(row); await db.SaveChangesAsync(ct);
            db.DailyOperationRevisions.Add(new(){TenantId=row.TenantId,VesselId=row.VesselId,RecordId=id,Kind=kind,SourceVersion=row.SourceVersion,Revision=1,
                RecordVersion=row.Version,FieldsJson=json,Hash=row.Hash,Provenance="SyntheticUnverified",SavedAt=now});
            await db.SaveChangesAsync(ct);
        }
        // Caller commits. No HTTP idempotency receipt pretends a user command ran.
    }
}
