using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

public static class VesselCertificateFixtures
{
    public static Guid Id(Guid vessel)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.certificate.v1:"+vessel.ToString("D"))).AsSpan(0,16));
    // Optional explicit owned-lab import. No real credential, evidence, signature,
    // qualification, expiry, issuer, verification or user receipt is seeded.
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic certificate fixture requires owned administrative transaction.");
        foreach(var vessel in vessels) {
            var id=Id(vessel.Id);if(await db.VesselCertificates.AnyAsync(c=>c.TenantId==vessel.TenantId && c.Id==id,ct))continue;
            if(!await db.Vessels.AnyAsync(v=>v.TenantId==vessel.TenantId && v.Id==vessel.Id,ct))throw new InvalidOperationException("Synthetic certificate vessel unavailable.");
            var head=new VesselCertificate{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id};db.VesselCertificates.Add(head);await db.SaveChangesAsync(ct);
            var json=JsonSerializer.Serialize(new VesselCertificateContent(new(new()),DocumentName:"FICTIONAL incomplete certificate draft"),new JsonSerializerOptions(JsonSerializerDefaults.Web));
            db.VesselCertificateRevisions.Add(new(){TenantId=vessel.TenantId,VesselId=vessel.Id,CertificateId=id,Revision=1,RecordVersion=head.Version,
                ContentJson=json,Hash=RegisterStorage.Hash(json),SourceVersion=VesselCertificatePolicy.SourceVersion,Provenance="SyntheticUnverified",SavedAt=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync(ct);
        }
    }
}
