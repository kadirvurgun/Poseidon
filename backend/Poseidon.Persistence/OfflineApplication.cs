using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// No keys, credentials or plaintext commands are stored here. Context IDs are not authentication.
public sealed class OfflineEnrollment : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid KeyId { get; set; }
    public string KeyChallenge { get; set; } = "";
    public string? PublicSigningKey { get; set; }
    public string PolicyVersion { get; set; } = "";
    public string PolicyDigest { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public uint BaseVersion { get; set; }
    public long BaseRevision { get; set; }
    public string ProtectedBase { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public uint Version { get; set; }
}

public sealed class OfflineLifecycle : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public Guid AssignmentId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
}

// Outcome is immutable, including conflict outcomes. Resolution is a new command with parent lineage.
public sealed class OfflineOutcome : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CommandId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid? ParentCommandId { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public Guid AssignmentId { get; set; }
    public string Digest { get; set; } = "";
    public string Result { get; set; } = "";
    public uint BaseVersion { get; set; }
    public uint ServerVersion { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public int OffsetMinutes { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string ProtectedContext { get; set; } = "";
}

public static class OfflineApplicationStorage
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<OfflineEnrollment>(b => {
            b.ToTable("Enrollments", "offline"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.AssignmentId });
            b.HasOne<CrewAssignment>().WithMany().HasForeignKey(x => new { x.TenantId, x.AssignmentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_OfflineEnrollment_Assignment").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.PolicyVersion).HasMaxLength(80); b.Property(x => x.SourceVersion).HasMaxLength(64);
            b.Property(x => x.PolicyDigest).HasMaxLength(64);
            b.Property(x => x.KeyChallenge).HasMaxLength(64); b.Property(x => x.PublicSigningKey).HasMaxLength(256);
            b.Property(x => x.Version).IsRowVersion();
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.VesselId, x.AssignmentId });
        });
        model.Entity<OfflineLifecycle>(b => {
            b.ToTable("Lifecycle", "offline"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<OfflineEnrollment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.AssignmentId }).HasConstraintName("FK_OfflineLifecycle_Enrollment").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Action).HasMaxLength(16);
        });
        model.Entity<OfflineOutcome>(b => {
            b.ToTable("Outcomes", "offline"); b.HasKey(x => new { x.TenantId, x.ActorId, x.CommandId });
            b.HasAlternateKey(x => new { x.TenantId, x.ActorId, x.CommandId, x.EnrollmentId });
            b.HasOne<OfflineEnrollment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.AssignmentId }).HasConstraintName("FK_OfflineOutcome_Enrollment").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<OfflineOutcome>().WithMany().HasForeignKey(x => new { x.TenantId, x.ActorId, x.ParentCommandId, x.EnrollmentId })
                .HasPrincipalKey(x => new { x.TenantId, x.ActorId, x.CommandId, x.EnrollmentId }).HasConstraintName("FK_OfflineOutcome_Parent").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Digest).HasMaxLength(64); b.Property(x => x.Result).HasMaxLength(24);
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.EnrollmentId, x.ReceivedAt });
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.ParentCommandId }).IsUnique().HasFilter("\"ParentCommandId\" IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.ParentCommandId, x.EnrollmentId }).HasDatabaseName("IX_OfflineOutcome_ParentScope");
        });
    }
}
