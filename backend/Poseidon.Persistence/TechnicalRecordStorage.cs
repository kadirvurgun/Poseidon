using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// DEV-055 defect lifecycle and DEV-103 service & engineer reports. Heads are mutable with xmin
// concurrency; lifecycle records, report revisions, verification decisions and command receipts
// are append-only. Synthetic rows may never carry a verification, effectiveness or closure
// (DB CHECKs); seeds never add human decisions.
public sealed class DefectLifecycle : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid DefectId {get;set;}
    public long Revision {get;set;}=1;
    public string Stage {get;set;}="DETECT";
    public string? AssessedCriticality {get;set;}
    public Guid? ClosedBy {get;set;}
    public DateTimeOffset? ClosedAt {get;set;}
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public uint Version {get;set;}
}
// One human lifecycle record. Text (assessment rationale, safeguard, action, verification and
// return-to-service statements) is withheld from audit summaries.
public sealed class DefectLifecycleRecord : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public Guid DefectId {get;set;}
    public long Sequence {get;set;}
    public string Kind {get;set;}="";
    public string StageAfter {get;set;}="";
    public long DefectRevision {get;set;}
    public string? Criticality {get;set;}
    public string? MeasureKind {get;set;}
    public bool Temporary {get;set;}
    public DateTimeOffset? ExpiresAt {get;set;}
    public Guid? MeasureId {get;set;}
    public string? Outcome {get;set;}
    public string Text {get;set;}="";
    public Guid ActorId {get;set;}
    public DateTimeOffset RecordedAt {get;set;}
    public string Provenance {get;set;}="UserDeclaredUnverified";
}
public sealed class ServiceReport : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public string ReportNo {get;set;}="";
    public long Revision {get;set;}=1;
    public string State {get;set;}="DRAFT";
    public string ContentJson {get;set;}="{}";
    public Guid? AssetId {get;set;}
    public long? AssetRevision {get;set;}
    public Guid? WorkOrderId {get;set;}
    public long? WorkOrderRevision {get;set;}
    public string DefectLinksJson {get;set;}="[]";
    public string EvidenceJson {get;set;}="[]";
    public string Hash {get;set;}="";
    public string AuthorsJson {get;set;}="[]";
    public Guid? SubmittedBy {get;set;}
    public long? VerifiedRevision {get;set;}
    public Guid CreatedBy {get;set;}
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public uint Version {get;set;}
}
public sealed class ServiceReportRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ReportId {get;set;}
    public long Revision {get;set;}
    public string State {get;set;}="DRAFT";
    public string ReasonCode {get;set;}="";
    public string ContentJson {get;set;}="{}";
    public Guid? AssetId {get;set;}
    public long? AssetRevision {get;set;}
    public Guid? WorkOrderId {get;set;}
    public long? WorkOrderRevision {get;set;}
    public string DefectLinksJson {get;set;}="[]";
    public string EvidenceJson {get;set;}="[]";
    public string Hash {get;set;}="";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public string Provenance {get;set;}="UserDeclaredUnverified";
}
// Human verification record (VERIFY / RETURN) of an exact submitted revision by a non-author.
public sealed class ServiceReportVerification : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public Guid ReportId {get;set;}
    public long Revision {get;set;}
    public long ResultRevision {get;set;}
    public string Decision {get;set;}="";
    public string ContentHash {get;set;}="";
    public Guid ActorId {get;set;}
    public string Reason {get;set;}="";
    public DateTimeOffset DecidedAt {get;set;}
}
public sealed class TechnicalRecordReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public string Action {get;set;}="";
    public string Fingerprint {get;set;}="";
    public Guid VesselId {get;set;}
    public Guid ResourceId {get;set;}
    public long Revision {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}

internal static class TechnicalRecordStorage
{
    private const string Provenance="\"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')";
    internal static void Configure(ModelBuilder model) {
        model.Entity<DefectLifecycle>(b=> {
            b.ToTable("DefectLifecycles","technical",t=>t.HasCheckConstraint("CK_DefectLifecycle_State",
                "\"Revision\">0 AND \"Stage\" IN ('DETECT','ASSESS','SAFEGUARD','ACTION','VERIFY','EFFECTIVENESS','CLOSED') AND "+Provenance+" AND "+
                "(\"AssessedCriticality\" IS NULL OR \"AssessedCriticality\" IN ('Critical','High','Medium','Low')) AND "+
                "((\"Stage\"='CLOSED')=(\"ClosedBy\" IS NOT NULL AND \"ClosedAt\" IS NOT NULL)) AND "+
                "(\"Provenance\"<>'SyntheticUnverified' OR \"Stage\" IN ('DETECT','ASSESS') AND \"ClosedBy\" IS NULL)"));
            b.HasKey(r=>new{r.TenantId,r.DefectId});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.DefectId});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Defect>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.DefectId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_DefectLifecycle_Defect").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.Stage});
            b.Property(r=>r.Stage).HasMaxLength(16);b.Property(r=>r.AssessedCriticality).HasMaxLength(16);b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<DefectLifecycleRecord>(b=> {
            b.ToTable("DefectLifecycleRecords","technical",t=>t.HasCheckConstraint("CK_DefectLifecycleRecord_Kind",
                "\"Sequence\">0 AND \"DefectRevision\">0 AND "+Provenance+" AND length(\"Text\")<=4000 AND "+
                "\"StageAfter\" IN ('DETECT','ASSESS','SAFEGUARD','ACTION','VERIFY','EFFECTIVENESS','CLOSED') AND ("+
                "\"Kind\"='START' AND \"StageAfter\"='DETECT' OR "+
                "\"Kind\"='ASSESS' AND \"Criticality\" IN ('Critical','High','Medium','Low') AND length(\"Text\")>=10 OR "+
                "\"Kind\"='SAFEGUARD' AND \"MeasureKind\" IN ('SAFEGUARD','RESTRICTION') AND length(\"Text\")>=1 AND (NOT \"Temporary\" OR \"ExpiresAt\" IS NOT NULL) OR "+
                "\"Kind\"='LIFT' AND \"MeasureId\" IS NOT NULL AND length(\"Text\")>=10 OR "+
                "\"Kind\"='ACTION' AND \"StageAfter\"='ACTION' AND length(\"Text\")>=1 OR "+
                "\"Kind\"='VERIFY' AND \"Outcome\" IN ('VERIFIED','NOT_VERIFIED') AND length(\"Text\")>=10 OR "+
                "\"Kind\"='EFFECTIVENESS' AND \"Outcome\" IN ('EFFECTIVE','NOT_EFFECTIVE') AND length(\"Text\")>=10 OR "+
                "\"Kind\"='CLOSE' AND \"StageAfter\"='CLOSED' AND \"Outcome\"='RETURNED_TO_SERVICE' AND length(\"Text\")>=10) AND "+
                "(\"Provenance\"<>'SyntheticUnverified' OR \"Kind\" IN ('START','ASSESS'))"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasIndex(r=>new{r.TenantId,r.DefectId,r.Sequence}).IsUnique();
            b.HasOne<DefectLifecycle>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.DefectId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.DefectId})
                .HasConstraintName("FK_DefectLifecycleRecord_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DefectRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.DefectId,r.DefectRevision}).HasPrincipalKey(r=>new{r.TenantId,r.DefectId,r.Revision})
                .HasConstraintName("FK_DefectLifecycleRecord_DefectRevision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Kind).HasMaxLength(16);b.Property(r=>r.StageAfter).HasMaxLength(16);b.Property(r=>r.Criticality).HasMaxLength(16);
            b.Property(r=>r.MeasureKind).HasMaxLength(16);b.Property(r=>r.Outcome).HasMaxLength(24);b.Property(r=>r.Text).HasColumnType("text");b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<ServiceReport>(b=> {
            b.ToTable("ServiceReports","technical",t=>t.HasCheckConstraint("CK_ServiceReport_State",
                "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','VERIFIED','RETURNED') AND "+Provenance+" AND "+
                "(\"State\"<>'VERIFIED' OR \"VerifiedRevision\"=\"Revision\") AND (\"AssetId\" IS NULL)=(\"AssetRevision\" IS NULL) AND "+
                "(\"WorkOrderId\" IS NULL)=(\"WorkOrderRevision\" IS NULL) AND "+
                "(\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"VerifiedRevision\" IS NULL)"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AssetRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.AssetId,r.AssetRevision})
                .HasConstraintName("FK_ServiceReport_AssetRevision").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.ReportNo}).IsUnique();b.HasIndex(r=>new{r.TenantId,r.VesselId,r.SavedAt,r.Id});
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.AssetId});b.HasIndex(r=>new{r.TenantId,r.VesselId,r.WorkOrderId});
            b.Property(r=>r.ReportNo).HasMaxLength(50);b.Property(r=>r.State).HasMaxLength(16);b.Property(r=>r.ContentJson).HasColumnType("text");
            b.Property(r=>r.DefectLinksJson).HasColumnType("text");b.Property(r=>r.EvidenceJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);
            b.Property(r=>r.AuthorsJson).HasColumnType("text");b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<ServiceReportRevision>(b=> {
            b.ToTable("ServiceReportRevisions","technical",t=>t.HasCheckConstraint("CK_ServiceReportRevision_State",
                "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','VERIFIED','RETURNED') AND \"ReasonCode\" IN ('CREATE','EDIT','SUBMIT','VERIFY','RETURN','AMEND') AND "+Provenance+" AND "+
                "(\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT')"));
            b.HasKey(r=>new{r.TenantId,r.ReportId,r.Revision});
            b.HasOne<ServiceReport>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.ReportId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_ServiceReportRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AssetRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.AssetId,r.AssetRevision})
                .HasConstraintName("FK_ServiceReportRevision_AssetRevision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.State).HasMaxLength(16);b.Property(r=>r.ReasonCode).HasMaxLength(16);b.Property(r=>r.ContentJson).HasColumnType("text");
            b.Property(r=>r.DefectLinksJson).HasColumnType("text");b.Property(r=>r.EvidenceJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<ServiceReportVerification>(b=> {
            b.ToTable("ServiceReportVerifications","technical",t=>t.HasCheckConstraint("CK_ServiceReportVerification_Kind",
                "\"Decision\" IN ('VERIFY','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasIndex(r=>new{r.TenantId,r.ReportId,r.Revision}).IsUnique();
            b.HasOne<ServiceReportRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.ReportId,r.Revision}).HasPrincipalKey(r=>new{r.TenantId,r.ReportId,r.Revision})
                .HasConstraintName("FK_ServiceReportVerification_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Decision).HasMaxLength(16);b.Property(r=>r.ContentHash).HasMaxLength(64);b.Property(r=>r.Reason).HasMaxLength(1000);
        });
        model.Entity<TechnicalRecordReceipt>(b=> {
            b.ToTable("TechnicalRecordReceipts","technical",t=>t.HasCheckConstraint("CK_TechnicalRecordReceipt_Revision","\"Revision\">0"));
            b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Action).HasMaxLength(48);b.Property(r=>r.Fingerprint).HasMaxLength(64);
        });
    }

    private static bool Changed(EntityEntry e,params string[] allowed)=>e.Properties.Any(p=>p.IsModified && !allowed.Contains(p.Metadata.Name));
    private static bool NextRevision(EntityEntry e)=>(long)e.Property("Revision").CurrentValue! == (long)e.Property("Revision").OriginalValue!+1;
    // Called by PoseidonDbContext before audit projection; rejects history rewrites.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if(changes.Any(e=>e.Entity is DefectLifecycleRecord or ServiceReportRevision or ServiceReportVerification or TechnicalRecordReceipt && e.State!=EntityState.Added))
            throw new InvalidOperationException("Defect lifecycle records, service report history, verifications and outcomes are immutable.");
        if(changes.Any(e=>e.Entity is DefectLifecycle && (e.State==EntityState.Deleted || e.State==EntityState.Modified &&
            ((string)e.Property("Stage").OriginalValue! == "CLOSED" || Changed(e,"Revision","Stage","AssessedCriticality","ClosedBy","ClosedAt","ActorId","SavedAt") || !NextRevision(e)))))
            throw new InvalidOperationException("A closed defect lifecycle is immutable; open lifecycles append one record per revision.");
        if(changes.Any(e=>e.Entity is ServiceReport && (e.State==EntityState.Deleted || e.State==EntityState.Modified &&
            (Changed(e,"Revision","State","ContentJson","AssetId","AssetRevision","WorkOrderId","WorkOrderRevision","DefectLinksJson","EvidenceJson","Hash","AuthorsJson","SubmittedBy","VerifiedRevision","ActorId","SavedAt") || !NextRevision(e)))))
            throw new InvalidOperationException("Service report ownership is immutable; append one revision.");
    }
}
