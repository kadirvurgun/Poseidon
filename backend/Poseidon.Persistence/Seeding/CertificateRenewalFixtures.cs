using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

public static class CertificateRenewalFixtures
{
    public static Guid Id(Guid vessel)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.renewal.v1:"+vessel.ToString("D"))).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic renewal requires an owned audited administrative transaction.");
        foreach(var vessel in vessels) {
            var id=Id(vessel.Id);if(await db.CertificateRenewals.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct))continue;
            var certificateId=VesselCertificateFixtures.Id(vessel.Id);
            var source=await db.VesselCertificates.SingleOrDefaultAsync(c=>c.TenantId==vessel.TenantId && c.VesselId==vessel.Id && c.Id==certificateId,ct)
                ??throw new InvalidOperationException("Incomplete synthetic certificate prerequisite unavailable.");
            var head=new CertificateRenewal{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id,CertificateId=certificateId};db.CertificateRenewals.Add(head);await db.SaveChangesAsync(ct);
            var json=JsonSerializer.Serialize(new CertificateRenewalContent(),new JsonSerializerOptions(JsonSerializerDefaults.Web));
            db.CertificateRenewalRevisions.Add(new(){TenantId=vessel.TenantId,VesselId=vessel.Id,RenewalId=id,CertificateId=certificateId,CertificateRevision=source.Revision,
                CertificateVersion=source.Version,Revision=1,RecordVersion=head.Version,ContentJson=json,Hash=RegisterStorage.Hash(json),SourceVersion=VesselCertificatePolicy.SourceVersion,
                Provenance="SyntheticUnverified",SavedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync(ct);
        }
        // No owner, survey completion, verified file, replacement or receipt is seeded.
    }
}
