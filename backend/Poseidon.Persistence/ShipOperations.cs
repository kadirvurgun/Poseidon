using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// One module ledger for fuel movements and port events; the canonical field
// schema remains domain-owned. Neither record kind is a verified/signed decision.
public sealed class ShipOperation : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public string Kind {get;set;}="";
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
    public string FieldsJson {get;set;}="{}";
    public string? OriginJson {get;set;}
    public string SourceVersion {get;set;}="";
    public string Hash {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public DateTimeOffset? OccurredAt {get;set;}
    public DateTimeOffset? EndedAt {get;set;}
}
public sealed class ShipOperationRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RecordId {get;set;}
    public string Kind {get;set;}="";
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string FieldsJson {get;set;}="{}";
    public string? OriginJson {get;set;}
    public string SourceVersion {get;set;}="";
    public string Hash {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public DateTimeOffset? OccurredAt {get;set;}
    public DateTimeOffset? EndedAt {get;set;}
}
public sealed class ShipOperationReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid RecordId {get;set;}
    public string Kind {get;set;}="";
    public string? OriginJson {get;set;}
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class ShipOperationsStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<ShipOperation>(b=> {
            b.ToTable("ShipOperations","operations",t=>t.HasCheckConstraint("CK_ShipOperation_State","\"Kind\" IN ('Fuel','Port') AND \"Revision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Kind,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.Kind,r.OccurredAt,r.Id});b.Property(r=>r.Kind).HasMaxLength(8);b.Property(r=>r.SourceVersion).HasMaxLength(160);
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.Provenance).HasMaxLength(32);b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.OriginJson).HasColumnType("text");
        });
        model.Entity<ShipOperationRevision>(b=> {
            b.ToTable("ShipOperationRevisions","operations",t=>t.HasCheckConstraint("CK_ShipOperationRevision_State","\"Revision\">0 AND \"RecordVersion\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(r=>new{r.TenantId,r.RecordId,r.Revision});
            b.HasOne<ShipOperation>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.Kind,r.RecordId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Kind,r.Id})
                .HasConstraintName("FK_ShipOperationRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.Kind,r.RecordId});b.Property(r=>r.Kind).HasMaxLength(8);b.Property(r=>r.SourceVersion).HasMaxLength(160);
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.Provenance).HasMaxLength(32);b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.OriginJson).HasColumnType("text");
        });
        model.Entity<ShipOperationReceipt>(b=> {
            b.ToTable("ShipOperationReceipts","operations");b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<ShipOperation>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.Kind,r.RecordId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Kind,r.Id})
                .HasConstraintName("FK_ShipOperationReceipt_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.Kind,r.RecordId});b.Property(r=>r.Kind).HasMaxLength(8);b.Property(r=>r.Key).HasMaxLength(80);
            b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");b.Property(r=>r.OriginJson).HasColumnType("text");
        });
    }
}
