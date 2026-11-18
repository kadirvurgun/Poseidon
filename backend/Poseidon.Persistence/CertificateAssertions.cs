using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// This ledger records an assigned Human's assertion. It never changes legal
// certificate lifecycle, source adoption, file transport state or supersession.
public sealed class CertificateAssertionHead : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid CertificateId {get;set;}
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
}
public sealed class CertificateAssertionEvent : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid CertificateId {get;set;}
    public Guid Id {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public long CertificateRevision {get;set;}
    public long CertificateVersion {get;set;}
    public Guid FileId {get;set;}
    public long FileRevision {get;set;}
    public string Checksum {get;set;}="";
    public string State {get;set;}="";
    public Guid ActorId {get;set;}
    public Guid RoleId {get;set;}
    public Guid AuthorityId {get;set;}
    public string AuthorityHash {get;set;}="";
    public string AuthorityBindingHash {get;set;}="";
    public string AuthorityJson {get;set;}="{}";
    public string SourceVersion {get;set;}="";
    public string Rationale {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class CertificateAssertionStorage
{
    internal const string StateConstraint="\"Revision\">0 AND \"RecordVersion\">0 AND \"CertificateRevision\">0 AND \"CertificateVersion\">0 AND \"State\" IN ('Verified','Not Verified','Suspicious','Suspended / Revoked')";
    internal static void Configure(ModelBuilder model) {
        model.Entity<CertificateAssertionHead>(b=> {
            b.ToTable("CertificateAssertionHeads","assurance",t=>t.HasCheckConstraint("CK_CertificateAssertionHead_Revision","\"Revision\">0"));
            b.HasKey(r=>new{r.TenantId,r.CertificateId});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.CertificateId}).HasName("AK_CertificateAssertionHead_Scope");b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<VesselCertificate>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_CertificateAssertionHead_Certificate").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CertificateAssertionEvent>(b=> {
            b.ToTable("CertificateAssertions","assurance",t=>t.HasCheckConstraint("CK_CertificateAssertion_State",StateConstraint));b.HasKey(r=>new{r.TenantId,r.Id});
            b.HasIndex(r=>new{r.TenantId,r.CertificateId,r.Revision}).IsUnique().HasDatabaseName("UX_CertificateAssertion_Revision");
            b.HasOne<CertificateAssertionHead>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.CertificateId})
                .HasConstraintName("FK_CertificateAssertion_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<VesselCertificateRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.CertificateRevision}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.Revision})
                .HasConstraintName("FK_CertificateAssertion_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StoredFile>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.FileId,r.FileRevision,r.Checksum}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id,r.Revision,r.Checksum})
                .HasConstraintName("FK_CertificateAssertion_File").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.State).HasMaxLength(32);b.Property(r=>r.Checksum).HasMaxLength(64);b.Property(r=>r.AuthorityHash).HasMaxLength(64);b.Property(r=>r.AuthorityBindingHash).HasMaxLength(64);
            b.Property(r=>r.SourceVersion).HasMaxLength(192);b.Property(r=>r.AuthorityJson).HasColumnType("text");b.Property(r=>r.Rationale).HasMaxLength(2000);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.CertificateId,r.CertificateRevision}).HasDatabaseName("IX_CertificateAssertion_Certificate");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.FileId,r.FileRevision,r.Checksum}).HasDatabaseName("IX_CertificateAssertion_File");
        });
    }
}
