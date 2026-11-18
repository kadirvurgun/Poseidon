using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Owning operational downtime draft declarations. No financial claim, closure or verification,
// effectiveness assessment or safety decision is represented by these rows.
public sealed class Downtime : ITenantEntity
{
    public DateTimeOffset? StartedAt {get;set;}
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
    public string FieldsJson {get;set;}="{}";
    public string? OriginJson {get;set;}
    public string SourceVersion {get;set;}="";
    public string Hash {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class DowntimeRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid DowntimeId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string FieldsJson {get;set;}="{}";
    public string? OriginJson {get;set;}
    public string SourceVersion {get;set;}="";
    public string Hash {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class DowntimeReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid DowntimeId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class DowntimeStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<Downtime>(b=> {
            b.ToTable("Downtimes","operations",t=>t.HasCheckConstraint("CK_Downtime_Draft","\"Revision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.SavedAt,r.Id});b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.SourceVersion).HasMaxLength(192);
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.OriginJson).HasColumnType("text");b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<DowntimeRevision>(b=> {
            b.ToTable("DowntimeRevisions","operations",t=>t.HasCheckConstraint("CK_DowntimeRevision_Draft","\"Revision\">0 AND \"RecordVersion\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(r=>new{r.TenantId,r.DowntimeId,r.Revision});
            b.HasAlternateKey(r=>new{r.TenantId,r.DowntimeId,r.Revision,r.RecordVersion}).HasName("AK_DowntimeRevision_Version");
            b.HasOne<Downtime>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.DowntimeId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_DowntimeRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.SourceVersion).HasMaxLength(192);
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.OriginJson).HasColumnType("text");b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<DowntimeReceipt>(b=> {
            b.ToTable("DowntimeReceipts","operations",t=>t.HasCheckConstraint("CK_DowntimeReceipt_Version","\"Revision\">0 AND \"RecordVersion\">0"));b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<Downtime>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.DowntimeId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_DowntimeReceipt_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DowntimeRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.DowntimeId,r.Revision,r.RecordVersion}).HasPrincipalKey(r=>new{r.TenantId,r.DowntimeId,r.Revision,r.RecordVersion})
                .HasConstraintName("FK_DowntimeReceipt_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
        });
    }
}
