using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class DailyOperation : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    public string FieldsJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public sealed class DailyOperationRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid RecordId { get; set; }
    public string Kind { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public long Revision { get; set; }
    public long RecordVersion { get; set; }
    public string FieldsJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public sealed class DailyOperationReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public string Key { get; set; } = "";
    public Guid RecordId { get; set; }
    public string Kind { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string OutcomeJson { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; }
}
internal static class DailyOperationsStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<DailyOperation>(b => {
            b.ToTable("DailyOperations","operations",t => t.HasCheckConstraint("CK_DailyOperation_State",
                "\"Kind\" IN ('Noon','Deck','Engine') AND \"Revision\" > 0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(r => new { r.TenantId, r.Id }); b.HasAlternateKey(r => new {r.TenantId,r.VesselId,r.Kind,r.Id});
            b.HasOne<Vessel>().WithMany().HasForeignKey(r => new {r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r => new {r.TenantId,r.VesselId,r.Kind,r.SavedAt,r.Id});
            b.Property(r => r.Kind).HasMaxLength(12); b.Property(r => r.SourceVersion).HasMaxLength(160); b.Property(r => r.Hash).HasMaxLength(64); b.Property(r => r.Provenance).HasMaxLength(32);
            b.Property(r => r.FieldsJson).HasColumnType("text"); b.Property(r => r.Version).IsRowVersion();
        });
        model.Entity<DailyOperationRevision>(b => {
            b.ToTable("DailyOperationRevisions","operations",t => t.HasCheckConstraint("CK_DailyOperationRevision_State",
                "\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(r => new {r.TenantId,r.RecordId,r.Revision});
            b.HasOne<DailyOperation>().WithMany().HasForeignKey(r => new {r.TenantId,r.VesselId,r.Kind,r.RecordId})
                .HasPrincipalKey(r => new {r.TenantId,r.VesselId,r.Kind,r.Id}).HasConstraintName("FK_DailyRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r => new {r.TenantId,r.VesselId,r.Kind,r.RecordId});
            b.Property(r => r.Kind).HasMaxLength(12); b.Property(r => r.SourceVersion).HasMaxLength(160); b.Property(r => r.Hash).HasMaxLength(64); b.Property(r => r.Provenance).HasMaxLength(32); b.Property(r => r.FieldsJson).HasColumnType("text");
        });
        model.Entity<DailyOperationReceipt>(b => {
            b.ToTable("DailyOperationReceipts","operations"); b.HasKey(r => new {r.TenantId,r.ActorId,r.Key});
            b.HasOne<DailyOperation>().WithMany().HasForeignKey(r => new {r.TenantId,r.VesselId,r.Kind,r.RecordId})
                .HasPrincipalKey(r => new {r.TenantId,r.VesselId,r.Kind,r.Id}).HasConstraintName("FK_DailyReceipt_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r => new {r.TenantId,r.VesselId,r.Kind,r.RecordId});
            b.Property(r => r.Key).HasMaxLength(80); b.Property(r => r.Kind).HasMaxLength(12); b.Property(r => r.Fingerprint).HasMaxLength(64); b.Property(r => r.OutcomeJson).HasColumnType("text");
        });
    }
}
