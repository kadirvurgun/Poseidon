using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record CertificateExtractionBinding(Guid TenantId,Guid ActorId,Guid VesselId,Guid CertificateId,string Scope,Guid? AssignmentId,Guid? PersonId,
    uint CertificateVersion,long CertificateRevision,string? CertificateHash,Guid FileId,long FileRevision,string Checksum,uint StageVersion,uint StoredVersion,
    string Rights,string StorageHash,string? PersonalPolicyHash)
{
    public override string ToString()=>nameof(CertificateExtractionBinding);
}
internal sealed record CertificateExtractionSource(CertificateExtractionBinding Binding,CertificateDto Record,StoredFile File) { public override string ToString()=>nameof(CertificateExtractionSource); }
internal static class CertificateExtractionAccess
{
    internal static TrustedActor? Actor(HttpContext h)=>h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static bool Permission(TrustedActor? a)=>a?.HasWriteAttribution==true && CertificateReader.Read(a) && a.Permissions.Contains("file.read") && a.Permissions.Contains("certificate.extract");
    private static string Storage(HttpContext h)=>RegisterStorage.Hash(JsonSerializer.Serialize(h.RequestServices.GetRequiredService<SecureFileSettings>().Snapshot()));
    private static string Personal(HttpContext h)=>RegisterStorage.Hash(JsonSerializer.Serialize(CrewFilePolicy.Current(h)));
    internal static bool Human(HttpContext h,TrustedActor actor) {
        var members=h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m=>m.TenantId==actor.TenantId && m.ActorId==actor.Id).Take(2).ToArray();
        return members.Length==1 && members[0].ActorKind=="Human";
    }
    internal static async Task<CertificateExtractionSource?> Read(PoseidonDbContext db,HttpContext h,Guid vessel,Guid certificate,Guid? assignment,uint expected,string? purpose,CancellationToken ct)
    {
        var a=Actor(h);if(!Permission(a) || db.TenantId!=a!.TenantId || !await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==vessel,ct))return null;
        var date=DateOnly.FromDateTime(h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);CertificateDto record;Guid? person=null;Guid fileId;
        if(assignment==null) {
            var row=await CertificateReader.Visible(db,a,vessel).SingleOrDefaultAsync(r=>r.CertificateId==certificate,ct);
            if(row==null || row.RecordVersion!=expected || row.SourceVersion!=VesselCertificatePolicy.SourceVersion || row.FileId==null)return null;
            record=CertificateReader.Dto(row,a,date);fileId=row.FileId.Value;
        }else {
            var policy=CrewFilePolicy.Current(h);if(!CertificateReader.CrewRead(a,vessel,policy,purpose))return null;
            var row=await CertificateReader.CrewVisible(db,a,vessel,policy,purpose).SingleOrDefaultAsync(r=>r.Id==certificate && r.AssignmentId==assignment,ct);
            if(row==null || row.Version!=expected || row.DocumentReference==null)return null;
            record=CertificateReader.CrewDto(row,date);person=row.PersonId;fileId=row.DocumentReference.Value;
        }
        var file=await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f=>f.Id==fileId && f.VesselId==vessel,ct);
        var stage=await db.FileStages.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==fileId && s.VesselId==vessel && s.State=="CleanTransport",ct);
        if(file==null || stage==null || file.Mime is not("image/png" or "image/jpeg" or "application/pdf") ||
            assignment==null && (stage.PersonId!=null || stage.DocumentId!=null || record.Content.Document?.Revision!=file.Revision || record.Content.Document?.Checksum!=file.Checksum) ||
            assignment!=null && (stage.PersonId!=person || stage.PersonalKind!="CertificateDocument" || stage.PersonalRevision is not >0 || stage.PersonalPolicyVersion!=CrewFilePolicy.Current(h).Version ||
                stage.PersonalPurpose!=purpose || stage.PersonalSourceVersion!=CrewFilePolicy.Current(h).SourceVersion))return null;
        var hash=record.Hash??RegisterStorage.Hash(JsonSerializer.Serialize(record.Content,CertificateReader.Json));
        return new(new(a.TenantId,a.Id,vessel,certificate,assignment==null?"Vessel":"Crew",assignment,person,record.Version,record.Revision,hash,file.Id,file.Revision,file.Checksum,
            stage.Version,file.Version,CertificateReader.Rights(a),Storage(h),assignment==null?null:Personal(h)),record,file);
    }
    internal static async Task<bool> Current(HttpContext h,CertificateExtractionBinding original,string? purpose,bool sameCertificate=true,bool write=false)
    {
        await using var services=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver=services.ServiceProvider.GetRequiredService<ActorResolver>();var a=resolver.Resolve(h.User);
        if(!Permission(a) || a!.Id!=original.ActorId || a.TenantId!=original.TenantId || CertificateReader.Rights(a)!=original.Rights || Storage(h)!=original.StorageHash ||
            original.Scope=="Crew" && Personal(h)!=original.PersonalPolicyHash || write && (!CertificateReader.Write(a,original.VesselId)||!Human(h,a)))return false;
        PoseidonDbContext db;try{db=services.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=original.TenantId)return false;
        bool found;
        if(sameCertificate) {
            var current=await Read(db,h,original.VesselId,original.CertificateId,original.AssignmentId,original.CertificateVersion,purpose,h.RequestAborted);
            if(current?.Binding!=original)return false;
            // Read captures owning content before asynchronous transport reads.
            // Observe that same owning head and its exact transport together in
            // one final statement; an intervening writer must invalidate it.
            var policy=CrewFilePolicy.Current(h);
            var vesselCertificates=CertificateReader.Visible(db,a,original.VesselId).Where(r=>
                r.CertificateId==original.CertificateId && r.Revision==original.CertificateRevision && r.RecordVersion==original.CertificateVersion &&
                r.Hash==original.CertificateHash && r.SourceVersion==VesselCertificatePolicy.SourceVersion && r.FileId==original.FileId &&
                r.FileRevision==original.FileRevision && r.Checksum==original.Checksum &&
                db.VesselCertificates.Any(head=>head.Id==r.CertificateId && head.VesselId==r.VesselId && head.Revision==r.Revision && head.Version==original.CertificateVersion));
            var credentials=CertificateReader.CrewVisible(db,a,original.VesselId,policy,purpose).Where(r=>
                r.Id==original.CertificateId && r.AssignmentId==original.AssignmentId && r.PersonId==original.PersonId &&
                r.Revision==original.CertificateRevision && r.Version==original.CertificateVersion && r.DocumentReference==original.FileId);
            var crew=original.Scope=="Crew" && original.AssignmentId!=null && original.PersonId!=null;
            var vesselCertificate=original.Scope=="Vessel" && original.AssignmentId==null && original.PersonId==null;
            found=await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==original.VesselId &&
                (vesselCertificate && vesselCertificates.Any() || crew && credentials.Any()) &&
                db.StoredFiles.Any(f=>f.Id==original.FileId && f.VesselId==v.Id && f.Revision==original.FileRevision && f.Checksum==original.Checksum &&
                    f.Version==original.StoredVersion && (f.Mime=="image/png" || f.Mime=="image/jpeg" || f.Mime=="application/pdf") &&
                    db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==v.Id && s.Version==original.StageVersion && s.State=="CleanTransport" &&
                        (vesselCertificate && s.PersonId==null && s.DocumentId==null || crew && s.PersonId==original.PersonId &&
                            s.PersonalKind=="CertificateDocument" && s.PersonalRevision>0 && s.PersonalPolicyVersion==policy.Version &&
                            s.PersonalPurpose==purpose && s.PersonalSourceVersion==policy.SourceVersion))),h.RequestAborted);
        }else {
            // Receipt/just-applied response fence: the original current version
            // legitimately changed, but source transport and access must remain.
            found=await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v=>v.Id==original.VesselId &&
                db.StoredFiles.Any(f=>f.Id==original.FileId && f.VesselId==v.Id && f.Revision==original.FileRevision && f.Checksum==original.Checksum && f.Version==original.StoredVersion &&
                    db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==v.Id && s.Version==original.StageVersion && s.State=="CleanTransport" && s.PersonId==null && s.DocumentId==null)),h.RequestAborted);
        }
        var after=resolver.Resolve(h.User);
        return found && Permission(after) && after!.Id==original.ActorId && after.TenantId==original.TenantId && CertificateReader.Rights(after)==original.Rights &&
            Storage(h)==original.StorageHash && (original.Scope!="Crew" || Personal(h)==original.PersonalPolicyHash) && (!write || CertificateReader.Write(after,original.VesselId)&&Human(h,after));
    }
}
