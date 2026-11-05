using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class VesselCertificate : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
}
public sealed class VesselCertificateRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid CertificateId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string ContentJson {get;set;}="{}";
    public string Hash {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid? FileId {get;set;}
    public long? FileRevision {get;set;}
    public string? Checksum {get;set;}
    public DateOnly? IssueDate {get;set;}
    public DateOnly? ExpiryDate {get;set;}
    public DateOnly? SurveyWindowStart {get;set;}
    public DateOnly? SurveyWindowEnd {get;set;}
    public bool SurveyWindowDeclared {get;set;}
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class CertificateRenewal : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public Guid CertificateId {get;set;}
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
}
public sealed class CertificateRenewalRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RenewalId {get;set;}
    public Guid CertificateId {get;set;}
    public long CertificateRevision {get;set;}
    public long CertificateVersion {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string ContentJson {get;set;}="{}";
    public string Hash {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid? NewCertificateId {get;set;}
    public long? NewCertificateRevision {get;set;}
    public long? NewCertificateVersion {get;set;}
    public Guid? FileId {get;set;}
    public long? FileRevision {get;set;}
    public string? Checksum {get;set;}
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class CertificateCommandReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid ResourceId {get;set;}
    public Guid CertificateId {get;set;}
    public Guid? RenewalId {get;set;}
    public string Action {get;set;}="";
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class VesselCertificateStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<VesselCertificate>(b=> {
            b.ToTable("Certificates","assurance",t=>t.HasCheckConstraint("CK_VesselCertificate_Revision","\"Revision\" > 0"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasIndex(r=>new{r.TenantId,r.VesselId});
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<VesselCertificateRevision>(b=> {
            b.ToTable("CertificateRevisions","assurance",t=>t.HasCheckConstraint("CK_VesselCertificateRevision_Binding",
                "\"Revision\" > 0 AND \"RecordVersion\" > 0 AND ((\"FileId\" IS NULL AND \"FileRevision\" IS NULL AND \"Checksum\" IS NULL) OR (\"FileId\" IS NOT NULL AND \"FileRevision\" > 0 AND \"Checksum\" IS NOT NULL))"));
            b.HasKey(r=>new{r.TenantId,r.CertificateId,r.Revision});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.Revision}).HasName("AK_CertificateRevision_Scope");
            b.HasOne<VesselCertificate>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId})
                .HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id}).HasConstraintName("FK_CertificateRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StoredFile>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.FileId,r.FileRevision,r.Checksum})
                .HasPrincipalKey(f=>new{f.TenantId,f.VesselId,f.Id,f.Revision,f.Checksum}).HasConstraintName("FK_CertificateRevision_File").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.ContentJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.SourceVersion).HasMaxLength(192);
            b.Property(r=>r.Provenance).HasMaxLength(32);b.Property(r=>r.Checksum).HasMaxLength(64);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.FileId,r.FileRevision,r.Checksum}).HasDatabaseName("IX_CertificateRevision_FilePin");
        });
        model.Entity<CertificateRenewal>(b=> {
            b.ToTable("Renewals","assurance",t=>t.HasCheckConstraint("CK_CertificateRenewal_Revision","\"Revision\" > 0"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<VesselCertificate>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId})
                .HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id}).HasConstraintName("FK_Renewal_Certificate").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CertificateRenewalRevision>(b=> {
            b.ToTable("RenewalRevisions","assurance",t=>t.HasCheckConstraint("CK_RenewalRevision_Binding",
                "\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"CertificateRevision\" > 0 AND \"CertificateVersion\" > 0 AND ((\"NewCertificateId\" IS NULL AND \"NewCertificateRevision\" IS NULL AND \"NewCertificateVersion\" IS NULL) OR (\"NewCertificateId\" IS NOT NULL AND \"NewCertificateRevision\" > 0 AND \"NewCertificateVersion\" > 0)) AND ((\"FileId\" IS NULL AND \"FileRevision\" IS NULL AND \"Checksum\" IS NULL) OR (\"FileId\" IS NOT NULL AND \"FileRevision\" > 0 AND \"Checksum\" IS NOT NULL))"));b.HasKey(r=>new{r.TenantId,r.RenewalId,r.Revision});
            b.HasOne<CertificateRenewal>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.RenewalId})
                .HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.Id}).HasConstraintName("FK_RenewalRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<VesselCertificateRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.CertificateRevision})
                .HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.Revision}).HasConstraintName("FK_RenewalRevision_Original").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<VesselCertificateRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.NewCertificateId,r.NewCertificateRevision})
                .HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.Revision}).HasConstraintName("FK_RenewalRevision_Replacement").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StoredFile>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.FileId,r.FileRevision,r.Checksum})
                .HasPrincipalKey(f=>new{f.TenantId,f.VesselId,f.Id,f.Revision,f.Checksum}).HasConstraintName("FK_RenewalRevision_File").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.ContentJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.SourceVersion).HasMaxLength(192);b.Property(r=>r.Provenance).HasMaxLength(32);b.Property(r=>r.Checksum).HasMaxLength(64);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.RenewalId}).HasDatabaseName("IX_RenewalRevision_Head");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.CertificateRevision}).HasDatabaseName("IX_RenewalRevision_Original");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.NewCertificateId,r.NewCertificateRevision}).HasDatabaseName("IX_RenewalRevision_Replacement");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.FileId,r.FileRevision,r.Checksum}).HasDatabaseName("IX_RenewalRevision_FilePin");
        });
        model.Entity<CertificateCommandReceipt>(b=> {
            b.ToTable("CertificateReceipts","assurance",t=>t.HasCheckConstraint("CK_CertificateReceipt_Resource",
                "(\"Action\"='CertificateSave' AND \"RenewalId\" IS NULL AND \"ResourceId\"=\"CertificateId\") OR (\"Action\"='RenewalSave' AND \"RenewalId\" IS NOT NULL AND \"ResourceId\"=\"RenewalId\")"));b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<VesselCertificate>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId})
                .HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id}).HasConstraintName("FK_CertificateReceipt_Certificate").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CertificateRenewal>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.RenewalId})
                .HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.Id}).HasConstraintName("FK_CertificateReceipt_Renewal").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Action).HasMaxLength(32);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.RenewalId}).HasDatabaseName("IX_CertificateReceipt_Owner");
        });
    }
}
