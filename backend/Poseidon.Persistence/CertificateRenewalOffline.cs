using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// S10 WP-C DEV-066 (18.01 sheet:Backlog/row:67) certificate renewal offline namespace: separate from
// crew/tug/daily/work-order/safety grants. One enrollment binds one renewal case head revision; only the
// draft metadata authoring command (DRAFT/PREPARING) is offline. No plaintext command snapshot, key
// material or credential is stored; protected values use server data protection.
public sealed class CertificateRenewalOfflineEnrollment : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public Guid ActorId {get;set;}
    public Guid VesselId {get;set;}
    public Guid CertificateId {get;set;}
    public Guid RenewalId {get;set;}
    public Guid DeviceId {get;set;}
    public Guid KeyId {get;set;}
    public string Challenge {get;set;}="";
    public string? PublicKey {get;set;}
    public string PolicyVersion {get;set;}="";
    public string PolicyDigest {get;set;}="";
    public string AuthorityDigest {get;set;}="";
    public string SourceVersion {get;set;}="";
    public uint BaseVersion {get;set;}
    public long BaseRevision {get;set;}
    public string ProtectedBase {get;set;}="";
    public DateTimeOffset CreatedAt {get;set;}
    public DateTimeOffset ExpiresAt {get;set;}
    public DateTimeOffset RetainUntil {get;set;}
    public DateTimeOffset? RevokedAt {get;set;}
    public uint Version {get;set;}
}
public sealed class CertificateRenewalOfflineLifecycle : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public Guid EnrollmentId {get;set;}
    public Guid ActorId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RenewalId {get;set;}
    public string Action {get;set;}="";
    public DateTimeOffset RecordedAt {get;set;}
}
public sealed class CertificateRenewalOfflineOutcome : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid ActorId {get;set;}
    public Guid CommandId {get;set;}
    public Guid EnrollmentId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RenewalId {get;set;}
    public Guid? ParentCommandId {get;set;}
    public string Digest {get;set;}="";
    public string Result {get;set;}="";
    public uint BaseVersion {get;set;}
    public uint ServerVersion {get;set;}
    public long Revision {get;set;}
    public string OutcomeJson {get;set;}="";
    public string ProtectedContext {get;set;}="";
    public DateTimeOffset CapturedAt {get;set;}
    public DateTimeOffset ReceivedAt {get;set;}
}

internal static class CertificateRenewalOfflineStorage
{
    internal static readonly string[] LifecycleActions=["Enrolled","Renewed","KeyEnrolled","Revoked","KeyLost","CleanupAllowed"];
    internal static readonly string[] Results=["Committed","MaterialConflict","ServerRetained"];
    internal static void Configure(ModelBuilder model) {
        model.Entity<CertificateRenewalOfflineEnrollment>(b=> {
            b.ToTable("CertificateRenewalEnrollments","offline",t=>t.HasCheckConstraint("CK_CertificateRenewalOfflineEnrollment_State","\"BaseVersion\" > 0 AND \"BaseRevision\" > 0 AND \"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\""));
            b.HasKey(e=>new{e.TenantId,e.Id});
            b.HasAlternateKey(e=>new{e.TenantId,e.Id,e.ActorId,e.VesselId,e.RenewalId}).HasName("AK_CertificateRenewalOfflineEnrollment_Scope");
            b.HasOne<CertificateRenewal>().WithMany().HasForeignKey(e=>new{e.TenantId,e.VesselId,e.CertificateId,e.RenewalId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.Id})
                .HasConstraintName("FK_CertificateRenewalOffline_Renewal").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e=>new{e.TenantId,e.ActorId,e.VesselId,e.RenewalId,e.CreatedAt}).HasDatabaseName("IX_CertificateRenewalOfflineEnrollment_Owner");
            b.Property(e=>e.Challenge).HasMaxLength(64);b.Property(e=>e.PublicKey).HasMaxLength(256);b.Property(e=>e.PolicyVersion).HasMaxLength(80);
            b.Property(e=>e.PolicyDigest).HasMaxLength(64);b.Property(e=>e.AuthorityDigest).HasMaxLength(64);b.Property(e=>e.SourceVersion).HasMaxLength(192);b.Property(e=>e.Version).IsRowVersion();
        });
        model.Entity<CertificateRenewalOfflineLifecycle>(b=> {
            b.ToTable("CertificateRenewalLifecycle","offline",t=>t.HasCheckConstraint("CK_CertificateRenewalOfflineLifecycle_Action","\"Action\" IN ('Enrolled','Renewed','KeyEnrolled','Revoked','KeyLost','CleanupAllowed')"));
            b.HasKey(e=>new{e.TenantId,e.Id});b.Property(e=>e.Action).HasMaxLength(24);
            b.HasIndex(e=>new{e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.RenewalId}).HasDatabaseName("IX_CertificateRenewalOfflineLifecycle_Scope");
            b.HasOne<CertificateRenewalOfflineEnrollment>().WithMany().HasForeignKey(e=>new{e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.RenewalId})
                .HasPrincipalKey(e=>new{e.TenantId,e.Id,e.ActorId,e.VesselId,e.RenewalId}).HasConstraintName("FK_CertificateRenewalOfflineLifecycle_Enrollment").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CertificateRenewalOfflineOutcome>(b=> {
            b.ToTable("CertificateRenewalOutcomes","offline",t=>t.HasCheckConstraint("CK_CertificateRenewalOfflineOutcome_State","\"BaseVersion\" > 0 AND \"ServerVersion\" > 0 AND \"Revision\" > 0 AND \"Result\" IN ('Committed','MaterialConflict','ServerRetained') AND (\"Result\" <> 'ServerRetained' OR \"ParentCommandId\" IS NOT NULL)"));
            b.HasKey(e=>new{e.TenantId,e.ActorId,e.CommandId});
            b.HasAlternateKey(e=>new{e.TenantId,e.ActorId,e.CommandId,e.EnrollmentId}).HasName("AK_CertificateRenewalOfflineOutcome_ParentScope");
            b.HasIndex(e=>new{e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.RenewalId}).HasDatabaseName("IX_CertificateRenewalOfflineOutcome_Scope");
            b.HasIndex(e=>new{e.TenantId,e.ActorId,e.ParentCommandId,e.EnrollmentId}).HasDatabaseName("IX_CertificateRenewalOfflineOutcome_ParentScope");
            b.HasIndex(e=>new{e.TenantId,e.ActorId,e.ParentCommandId}).HasDatabaseName("UX_CertificateRenewalOfflineOutcome_Resolution").IsUnique().HasFilter("\"ParentCommandId\" IS NOT NULL");
            b.HasOne<CertificateRenewalOfflineEnrollment>().WithMany().HasForeignKey(e=>new{e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.RenewalId})
                .HasPrincipalKey(e=>new{e.TenantId,e.Id,e.ActorId,e.VesselId,e.RenewalId}).HasConstraintName("FK_CertificateRenewalOfflineOutcome_Enrollment").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CertificateRenewalOfflineOutcome>().WithMany().HasForeignKey(e=>new{e.TenantId,e.ActorId,e.ParentCommandId,e.EnrollmentId})
                .HasPrincipalKey(e=>new{e.TenantId,e.ActorId,e.CommandId,e.EnrollmentId}).HasConstraintName("FK_CertificateRenewalOfflineOutcome_Parent").OnDelete(DeleteBehavior.Restrict);
            b.Property(e=>e.Digest).HasMaxLength(64);b.Property(e=>e.Result).HasMaxLength(24);b.Property(e=>e.OutcomeJson).HasColumnType("text");
        });
    }
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if(changes.Any(e=>e.Entity is CertificateRenewalOfflineLifecycle or CertificateRenewalOfflineOutcome && e.State!=EntityState.Added))
            throw new InvalidOperationException("Certificate renewal offline lineage is immutable.");
        if(changes.Any(e=>e.Entity is CertificateRenewalOfflineEnrollment && (e.State==EntityState.Deleted || e.State==EntityState.Modified &&
            (e.Properties.Any(p=>p.IsModified && p.Metadata.Name is not("ExpiresAt" or "RevokedAt" or "PublicKey")) || e.Property("RevokedAt").OriginalValue!=null ||
             (DateTimeOffset)e.Property("ExpiresAt").CurrentValue! < (DateTimeOffset)e.Property("ExpiresAt").OriginalValue! ||
             e.Property("PublicKey").OriginalValue!=null && !Equals(e.Property("PublicKey").OriginalValue,e.Property("PublicKey").CurrentValue)))))
            throw new InvalidOperationException("Certificate renewal offline ownership and key identity are immutable.");
    }
}
