using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Owning technical defect declarations. No closure, service verification,
// effectiveness assessment or safety decision is represented by these rows.
public sealed class Defect : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
    public string FieldsJson {get;set;}="{}";
    public Guid? AssetId {get;set;}
    public long? AssetRevision {get;set;}
    public long? AssetVersion {get;set;}
    public string? AssetHash {get;set;}
    public string SourceVersion {get;set;}="";
    public string Hash {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class DefectRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid DefectId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string FieldsJson {get;set;}="{}";
    public Guid? AssetId {get;set;}
    public long? AssetRevision {get;set;}
    public long? AssetVersion {get;set;}
    public string? AssetHash {get;set;}
    public string SourceVersion {get;set;}="";
    public string Hash {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class DefectReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid DefectId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class DefectStorage
{
    internal const string AssetConstraint="((\"AssetId\" IS NULL AND \"AssetRevision\" IS NULL AND \"AssetVersion\" IS NULL AND \"AssetHash\" IS NULL) OR (\"AssetId\" IS NOT NULL AND \"AssetRevision\" IS NOT NULL AND \"AssetRevision\">0 AND \"AssetVersion\" IS NOT NULL AND \"AssetVersion\">0 AND \"AssetHash\" IS NOT NULL))";
    internal static void Configure(ModelBuilder model) {
        model.Entity<Defect>(b=> {
            b.ToTable("Defects","technical",t=>t.HasCheckConstraint("CK_Defect_Draft","\"Revision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND "+AssetConstraint));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AssetRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.AssetId,r.AssetRevision})
                .HasConstraintName("FK_Defect_AssetRevision").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.SavedAt,r.Id});b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.SourceVersion).HasMaxLength(192);
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.AssetHash).HasMaxLength(64);b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<DefectRevision>(b=> {
            b.ToTable("DefectRevisions","technical",t=>t.HasCheckConstraint("CK_DefectRevision_Draft","\"Revision\">0 AND \"RecordVersion\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND "+AssetConstraint));
            b.HasKey(r=>new{r.TenantId,r.DefectId,r.Revision});
            b.HasAlternateKey(r=>new{r.TenantId,r.DefectId,r.Revision,r.RecordVersion}).HasName("AK_DefectRevision_Version");
            b.HasOne<Defect>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.DefectId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_DefectRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AssetRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.AssetId,r.AssetRevision})
                .HasConstraintName("FK_DefectRevision_AssetRevision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.SourceVersion).HasMaxLength(192);
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.AssetHash).HasMaxLength(64);b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<DefectReceipt>(b=> {
            b.ToTable("DefectReceipts","technical",t=>t.HasCheckConstraint("CK_DefectReceipt_Version","\"Revision\">0 AND \"RecordVersion\">0"));b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<Defect>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.DefectId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_DefectReceipt_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DefectRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.DefectId,r.Revision,r.RecordVersion}).HasPrincipalKey(r=>new{r.TenantId,r.DefectId,r.Revision,r.RecordVersion})
                .HasConstraintName("FK_DefectReceipt_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
        });
    }
}
