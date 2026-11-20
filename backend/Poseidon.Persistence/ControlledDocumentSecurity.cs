using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// DEV-114: append-only reader acknowledgement ("I read revision X and could access its content").
// It is a business record, distinct from the idempotency ControlledDocumentReceipt; opening,
// comparing or downloading never creates one.
public sealed class ControlledDocumentAcknowledgement : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid DocumentId { get; set; }
    public long Revision { get; set; }
    public Guid ActorId { get; set; }
    public required string ContentSha256 { get; set; }
    public string Statement { get; set; } = "READ_AND_ACCESSED_V1";
    public DateTimeOffset AcknowledgedAt { get; set; }
}

// DEV-114: append-only content access record (download / in-app view) with the classification
// basis and the attribution watermark text bound to the issued short-lived capability.
public sealed class ControlledDocumentAccessRecord : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid DocumentId { get; set; }
    public long Revision { get; set; }
    public Guid FileId { get; set; }
    public Guid ActorId { get; set; }
    public required string Classification { get; set; }
    public required string Purpose { get; set; }
    public required string PolicyBasis { get; set; }
    public required string Attribution { get; set; }
    public required string ContentSha256 { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
}

internal static class ControlledDocumentSecurityStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<ControlledDocumentAcknowledgement>(b => {
            b.ToTable("ControlledDocumentAcknowledgements", "files", t => t.HasCheckConstraint("CK_ControlledDocumentAcknowledgements",
                "\"Revision\" > 0 AND \"ContentSha256\" ~ '^[0-9A-F]{64}$' AND \"Statement\" = 'READ_AND_ACCESSED_V1'"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.Revision, x.ActorId }).IsUnique().HasDatabaseName("IX_ControlledDocumentAcknowledgements_Reader");
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentAcknowledgements_Document");
            b.HasOne<ControlledDocumentRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.Revision })
                .HasConstraintName("FK_ControlledDocumentAcknowledgements_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentAcknowledgements_Document").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ContentSha256).HasMaxLength(64); b.Property(x => x.Statement).HasMaxLength(32);
        });
        model.Entity<ControlledDocumentAccessRecord>(b => {
            b.ToTable("ControlledDocumentAccessRecords", "files", t => t.HasCheckConstraint("CK_ControlledDocumentAccessRecords",
                "\"Revision\" > 0 AND \"ContentSha256\" ~ '^[0-9A-F]{64}$' AND \"Purpose\" IN ('VIEW','DOWNLOAD') " +
                "AND \"Classification\" IN ('INTERNAL','CONFIDENTIAL','RESTRICTED') " +
                "AND \"PolicyBasis\" IN ('INTERNAL_DEFAULT','CONFIDENTIAL_ATTRIBUTED','RESTRICTED_EXPLICIT_PERMISSION') AND length(\"Attribution\") > 0"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentAccessRecords_Document");
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.IssuedAt }).HasDatabaseName("IX_ControlledDocumentAccessRecords_Actor");
            b.HasOne<ControlledDocumentRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.Revision })
                .HasConstraintName("FK_ControlledDocumentAccessRecords_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentAccessRecords_Document").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Classification).HasMaxLength(16); b.Property(x => x.Purpose).HasMaxLength(16); b.Property(x => x.PolicyBasis).HasMaxLength(40);
            b.Property(x => x.Attribution).HasMaxLength(300); b.Property(x => x.ContentSha256).HasMaxLength(64);
        });
    }

    internal static void Guard(IReadOnlyCollection<EntityEntry> changes)
    {
        if (changes.Any(e => e.Entity is ControlledDocumentAcknowledgement or ControlledDocumentAccessRecord && e.State != EntityState.Added))
            throw new InvalidOperationException("Controlled document acknowledgements and access records are append-only.");
    }
}
