using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// DEV-104 optional administrative fixture: one FICTIONAL, SyntheticUnverified pre-survey checklist
// per supplied vessel, instantiated from the STARTER_UNAPPROVED annual template against the
// synthetic certificate. Every item stays OPEN with no owner, due date, evidence, finding, plan,
// port call or verification. Pending/persisted repeats are skipped; operator revisions are kept.
public static class PreSurveyChecklistFixtures
{
    public static Guid Id(Guid vessel)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.presurvey.v1:"+vessel.ToString("D"))).AsSpan(0,16));
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic pre-survey checklist requires an owned audited administrative transaction.");
        var template=PreSurveyChecklistPolicy.Template("STARTER-ANNUAL")!;
        foreach(var vessel in vessels) {
            var id=Id(vessel.Id);
            if(db.ChangeTracker.Entries<PreSurveyChecklist>().Any(e=>e.Entity.TenantId==vessel.TenantId && e.Entity.Id==id) ||
                await db.PreSurveyChecklists.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct))continue;
            var certificateId=VesselCertificateFixtures.Id(vessel.Id);
            var source=await db.VesselCertificateRevisions.Where(c=>c.TenantId==vessel.TenantId && c.VesselId==vessel.Id && c.CertificateId==certificateId)
                .OrderByDescending(c=>c.Revision).FirstOrDefaultAsync(ct)??throw new InvalidOperationException("Incomplete synthetic certificate prerequisite unavailable.");
            var head=new PreSurveyChecklist{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id,CertificateId=certificateId};db.PreSurveyChecklists.Add(head);await db.SaveChangesAsync(ct);
            var content=PreSurveyChecklistPolicy.Instantiate(id,template,new PreSurveyPlan());
            var json=JsonSerializer.Serialize(content,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var now=db.Clock.GetUtcNow();
            db.PreSurveyChecklistRevisions.Add(new(){TenantId=vessel.TenantId,VesselId=vessel.Id,ChecklistId=id,CertificateId=certificateId,CertificateRevision=source.Revision,
                CertificateVersion=source.RecordVersion,Revision=1,RecordVersion=head.Version,TemplateCode=template.Code,ContentJson=json,Hash=RegisterStorage.Hash(json),
                SourceVersion=PreSurveyChecklistPolicy.SourceVersion,Provenance="SyntheticUnverified",Transition="CREATE",
                Readiness=PreSurveyChecklistPolicy.Readiness(content,DateOnly.FromDateTime(now.UtcDateTime)).State,SavedAt=now});
            await db.SaveChangesAsync(ct);
        }
        // No answer, owner, evidence, verification, finding, plan date, port call or receipt is seeded.
    }
}
