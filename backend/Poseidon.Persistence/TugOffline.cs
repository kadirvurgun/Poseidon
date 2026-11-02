using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Device enrollments are created only by authenticated commands, never by seed.
public sealed class TugOfflineEnrollment : ITenantEntity {
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid KeyId { get; set; }
    public string Challenge { get; set; } = "";
    public string? PublicKey { get; set; }
    public string PolicyDigest { get; set; } = "";
    public uint RequestVersion { get; set; }
    public uint BaseVersion { get; set; }
    public long BaseRevision { get; set; }
    public string ProtectedBase { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public uint Version { get; set; }
}
public sealed class TugOfflineOutcome : ITenantEntity {
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid CommandId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public Guid? ParentCommandId { get; set; }
    public string Digest { get; set; } = "";
    public string Result { get; set; } = "";
    public uint BaseVersion { get; set; }
    public uint RequestVersion { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public int OffsetMinutes { get; set; }
    public uint ServerVersion { get; set; }
    public long Revision { get; set; }
    public string ProtectedContext { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}
public sealed class TugOfflineLifecycle : ITenantEntity {
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
}
public static class TugOfflineStorage {
    public static void Configure(ModelBuilder model) {
        model.Entity<TugOfflineEnrollment>(b => {
            b.ToTable("OfflineEnrollments", "tug"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.JobId });
            b.HasOne<TugDraftPlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId, Revision = x.BaseRevision }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Version).IsRowVersion(); b.Property(x => x.RequestVersion).HasConversion<long>(); b.Property(x => x.BaseVersion).HasConversion<long>();
            b.Property(x => x.Challenge).HasMaxLength(64); b.Property(x => x.PublicKey).HasMaxLength(256); b.Property(x => x.PolicyDigest).HasMaxLength(64);
        });
        model.Entity<TugOfflineOutcome>(b => {
            b.ToTable("OfflineOutcomes", "tug"); b.HasKey(x => new { x.TenantId, x.ActorId, x.CommandId });
            b.HasAlternateKey(x => new { x.TenantId, x.ActorId, x.CommandId, x.EnrollmentId });
            b.HasOne<TugOfflineEnrollment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId, x.JobId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.JobId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TugOfflineOutcome>().WithMany().HasForeignKey(x => new { x.TenantId, x.ActorId, x.ParentCommandId, x.EnrollmentId })
                .HasPrincipalKey(x => new { x.TenantId, x.ActorId, x.CommandId, x.EnrollmentId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.ParentCommandId }).IsUnique().HasFilter("\"ParentCommandId\" IS NOT NULL");
            b.Property(x => x.ServerVersion).HasConversion<long>(); b.Property(x => x.Digest).HasMaxLength(64); b.Property(x => x.Result).HasMaxLength(32);
            b.Property(x => x.BaseVersion).HasConversion<long>(); b.Property(x => x.RequestVersion).HasConversion<long>();
            b.HasOne<TugDraftPlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugOfflineLifecycle>(b => {
            b.ToTable("OfflineLifecycle", "tug"); b.HasKey(x => new { x.TenantId, x.Id }); b.Property(x => x.Action).HasMaxLength(16);
            b.HasOne<TugOfflineEnrollment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId, x.JobId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.JobId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
