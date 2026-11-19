using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// DEV-104 pre-survey checklist. The head carries only the optimistic revision; every
// authoring or verification command appends one immutable revision pinned to the exact
// certificate revision it prepares for. Verification is a human record inside the
// revision content, never derived from upload, OCR or seed.
public sealed class PreSurveyChecklist : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public Guid CertificateId {get;set;}
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
}
public sealed class PreSurveyChecklistRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ChecklistId {get;set;}
    public Guid CertificateId {get;set;}
    public long CertificateRevision {get;set;}
    public long CertificateVersion {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string TemplateCode {get;set;}="";
    public string ContentJson {get;set;}="{}";
    public string Hash {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public string Transition {get;set;}="CREATE";
    public string Readiness {get;set;}="BLOCKED";
    public DateOnly? PlannedStart {get;set;}
    public DateOnly? PlannedEnd {get;set;}
    public Guid? PortCallId {get;set;}
    public long? PortCallRevision {get;set;}
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class PreSurveyReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid ChecklistId {get;set;}
    public string Action {get;set;}="";
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class PreSurveyStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<PreSurveyChecklist>(b=> {
            b.ToTable("PreSurveyChecklists","assurance",t=>t.HasCheckConstraint("CK_PreSurveyChecklist_Revision","\"Revision\" > 0"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.CertificateId}).HasDatabaseName("IX_PreSurveyChecklist_Certificate");
            b.HasOne<VesselCertificate>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId})
                .HasPrincipalKey(c=>new{c.TenantId,c.VesselId,c.Id}).HasConstraintName("FK_PreSurveyChecklist_Certificate").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PreSurveyChecklistRevision>(b=> {
            b.ToTable("PreSurveyChecklistRevisions","assurance",t=>{
                t.HasCheckConstraint("CK_PreSurveyRevision_Binding","\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"CertificateRevision\" > 0 AND \"CertificateVersion\" > 0 AND ((\"PortCallId\" IS NULL AND \"PortCallRevision\" IS NULL) OR (\"PortCallId\" IS NOT NULL AND \"PortCallRevision\" > 0)) AND (\"PlannedStart\" IS NULL OR \"PlannedEnd\" IS NULL OR \"PlannedEnd\" >= \"PlannedStart\")");
                t.HasCheckConstraint("CK_PreSurveyRevision_State","\"Transition\" IN ('CREATE','UPDATE','VERIFY') AND \"Readiness\" IN ('BLOCKED','INCOMPLETE','PREPARED_PENDING_HUMAN_CONFIRMATION') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')");
            });
            b.HasKey(r=>new{r.TenantId,r.ChecklistId,r.Revision});
            b.HasOne<PreSurveyChecklist>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.ChecklistId})
                .HasPrincipalKey(h=>new{h.TenantId,h.VesselId,h.Id}).HasConstraintName("FK_PreSurveyRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<VesselCertificateRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.CertificateRevision})
                .HasPrincipalKey(c=>new{c.TenantId,c.VesselId,c.CertificateId,c.Revision}).HasConstraintName("FK_PreSurveyRevision_Certificate").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.TemplateCode).HasMaxLength(64);b.Property(r=>r.ContentJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);
            b.Property(r=>r.SourceVersion).HasMaxLength(192);b.Property(r=>r.Provenance).HasMaxLength(32);b.Property(r=>r.Transition).HasMaxLength(16);b.Property(r=>r.Readiness).HasMaxLength(48);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.ChecklistId}).HasDatabaseName("IX_PreSurveyRevision_Head");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.CertificateRevision}).HasDatabaseName("IX_PreSurveyRevision_Certificate");
        });
        model.Entity<PreSurveyReceipt>(b=> {
            b.ToTable("PreSurveyReceipts","assurance",t=>t.HasCheckConstraint("CK_PreSurveyReceipt_Action","\"Action\" IN ('PreSurveySave','PreSurveyVerify')"));
            b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<PreSurveyChecklist>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.ChecklistId})
                .HasPrincipalKey(h=>new{h.TenantId,h.VesselId,h.Id}).HasConstraintName("FK_PreSurveyReceipt_Checklist").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Action).HasMaxLength(32);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.ChecklistId}).HasDatabaseName("IX_PreSurveyReceipt_Checklist");
        });
    }
}
