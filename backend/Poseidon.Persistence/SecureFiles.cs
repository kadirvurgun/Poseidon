using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Metadata-only staged intent. A quarantined intent is never evidence or a downloadable file.
public sealed class FileStage : ITenantEntity
{
    public string? StorageReference { get; set; }
    public Guid? DocumentId { get; set; }
    public long? DocumentRevision { get; set; }
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Fingerprint { get; set; }
    public required string Name { get; set; }
    public required string ExpectedChecksum { get; set; }
    public long ExpectedLength { get; set; }
    public string State { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
    public string Mime { get; set; } = "image/png";
}

// Each accepted transport has an immutable object ID/revision. Clean transport is NOT verified evidence.
public sealed class StoredFile : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public required string Name { get; set; }
    public required string Checksum { get; set; }
    public long Length { get; set; }
    public long Revision { get; set; } = 1;
    public string Mime { get; set; } = "image/png";
    public string? ProviderVersion { get; set; }
    public string? ProviderETag { get; set; }
    public string? ProviderReference { get; set; }
    public required string RetentionReference { get; set; }
    public required string ScannerReference { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
    public uint Version { get; set; }
}

public static class SecureFileStorage
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<FileStage>(b => {
            b.ToTable("FileStages", "files"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId });
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.Key }).IsUnique();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Key).HasMaxLength(80); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(120); b.Property(x => x.ExpectedChecksum).HasMaxLength(64);
            b.Property(x => x.State).HasMaxLength(24); b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Mime).HasMaxLength(32).HasDefaultValue("image/png");
            b.Property(x => x.StorageReference).HasMaxLength(64);
        });
        model.Entity<StoredFile>(b => {
            b.ToTable("StoredFiles", "files"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<FileStage>().WithOne().HasForeignKey<StoredFile>(x => new { x.TenantId, x.Id, x.VesselId }).HasPrincipalKey<FileStage>(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Name).HasMaxLength(120); b.Property(x => x.Checksum).HasMaxLength(64);
            b.Property(x => x.Mime).HasMaxLength(32); b.Property(x => x.RetentionReference).HasMaxLength(64);
            b.Property(x => x.ScannerReference).HasMaxLength(64); b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.ProviderVersion).HasMaxLength(1024); b.Property(x => x.ProviderETag).HasMaxLength(256);
            b.Property(x => x.ProviderReference).HasMaxLength(64);
        });
    }
}
