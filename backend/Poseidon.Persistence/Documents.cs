using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Preparation roots only. A draft is not a controlled copy, acknowledgement or published version.
public sealed class Document : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}

public sealed class DocumentVersion : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid VesselId { get; set; }
    public long Revision { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = "DraftUnverified";
    public string Provenance { get; set; } = "DeclaredUnverified";
    public DateTimeOffset CreatedAt { get; set; }
}

public static class DocumentStorage
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<Document>(b => {
            b.ToTable("Documents", "files"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId });
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<DocumentVersion>(b => {
            b.ToTable("DocumentVersions", "files"); b.HasKey(x => new { x.TenantId, x.DocumentId, x.Revision });
            b.HasAlternateKey(x => new { x.TenantId, x.DocumentId, x.Revision, x.VesselId });
            b.HasOne<Document>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Status).HasMaxLength(32); b.Property(x => x.Provenance).HasMaxLength(32);
        });
        model.Entity<FileStage>(b => {
            b.HasOne<DocumentVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.DocumentRevision, x.VesselId }).HasPrincipalKey(x => new { x.TenantId, x.DocumentId, x.Revision, x.VesselId }).HasConstraintName("FK_FileStages_DocumentVersion").OnDelete(DeleteBehavior.Restrict);
            b.ToTable("FileStages", "files", t => t.HasCheckConstraint("CK_FileStages_DocumentOwner", "(\"DocumentId\" IS NULL AND \"DocumentRevision\" IS NULL) OR (\"DocumentId\" IS NOT NULL AND \"DocumentRevision\" IS NOT NULL AND \"DocumentRevision\" > 0)"));
        });
    }
}
