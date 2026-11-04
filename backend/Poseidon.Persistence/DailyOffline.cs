using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Separate daily resource namespace. No crew/tug grants, keys, plaintext command
// snapshots or credentials. Resource may be a newly reserved draft UUID.
public sealed class DailyOfflineEnrollment : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public string Kind { get; set; } = "";
    public Guid RecordId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid KeyId { get; set; }
    public string Challenge { get; set; } = "";
    public string? PublicKey { get; set; }
    public string PolicyVersion { get; set; } = "";
    public string PolicyDigest { get; set; } = "";
    public string AuthorityDigest { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string SchemaHash { get; set; } = "";
    public uint BaseVersion { get; set; }
    public long BaseRevision { get; set; }
    public string ProtectedBase { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset RetainUntil { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public uint Version { get; set; }
}
public sealed class DailyOfflineLifecycle : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public string Kind { get; set; } = "";
    public Guid RecordId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
}
public sealed class DailyOfflineOutcome : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid CommandId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid VesselId { get; set; }
    public string Kind { get; set; } = "";
    public Guid RecordId { get; set; }
    public Guid? ParentCommandId { get; set; }
    public string Digest { get; set; } = "";
    public string Result { get; set; } = "";
    public uint BaseVersion { get; set; }
    public uint ServerVersion { get; set; }
    public long Revision { get; set; }
    public string OutcomeJson { get; set; } = "";
    public string ProtectedContext { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}
internal static class DailyOfflineStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<DailyOfflineEnrollment>(b=> {
            b.ToTable("DailyEnrollments","offline",t=>t.HasCheckConstraint("CK_DailyOfflineEnrollment_State","\"Kind\" IN ('Noon','Deck','Engine') AND \"BaseVersion\" >= 0 AND \"BaseRevision\" >= 0 AND ((\"BaseVersion\" = 0) = (\"BaseRevision\" = 0)) AND \"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\"")); b.HasKey(e=>new {e.TenantId,e.Id});
            b.HasAlternateKey(e=>new {e.TenantId,e.Id,e.ActorId,e.VesselId,e.Kind,e.RecordId}).HasName("AK_DailyOfflineEnrollment_Scope");
            b.HasOne<Vessel>().WithMany().HasForeignKey(e=>new {e.TenantId,e.VesselId}).HasConstraintName("FK_DailyOffline_Vessel").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e=>new {e.TenantId,e.ActorId,e.VesselId,e.Kind,e.RecordId,e.CreatedAt}).HasDatabaseName("IX_DailyOfflineEnrollment_Owner");
            b.HasIndex(e=>new {e.TenantId,e.VesselId}).HasDatabaseName("IX_DailyOfflineEnrollment_Vessel");
            b.Property(e=>e.Kind).HasMaxLength(12); b.Property(e=>e.Challenge).HasMaxLength(64); b.Property(e=>e.PublicKey).HasMaxLength(256);
            b.Property(e=>e.PolicyVersion).HasMaxLength(80); b.Property(e=>e.PolicyDigest).HasMaxLength(64); b.Property(e=>e.AuthorityDigest).HasMaxLength(64);
            b.Property(e=>e.SourceVersion).HasMaxLength(192); b.Property(e=>e.SchemaHash).HasMaxLength(64); b.Property(e=>e.Version).IsRowVersion();
        });
        model.Entity<DailyOfflineLifecycle>(b=> {
            b.ToTable("DailyLifecycle","offline");
            b.HasIndex(e=>new {e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.Kind,e.RecordId}).HasDatabaseName("IX_DailyOfflineLifecycle_Scope"); b.HasKey(e=>new {e.TenantId,e.Id}); b.Property(e=>e.Kind).HasMaxLength(12); b.Property(e=>e.Action).HasMaxLength(24);
            b.HasOne<DailyOfflineEnrollment>().WithMany().HasForeignKey(e=>new {e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.Kind,e.RecordId})
                .HasPrincipalKey(e=>new {e.TenantId,e.Id,e.ActorId,e.VesselId,e.Kind,e.RecordId}).HasConstraintName("FK_DailyOfflineLifecycle_Enrollment").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DailyOfflineOutcome>(b=> {
            b.ToTable("DailyOutcomes","offline",t=>t.HasCheckConstraint("CK_DailyOfflineOutcome_State","\"Kind\" IN ('Noon','Deck','Engine') AND \"BaseVersion\" >= 0 AND \"ServerVersion\" >= 0 AND \"Revision\" >= 0 AND \"Result\" IN ('Committed','MaterialConflict') AND (\"Result\" <> 'Committed' OR (\"ServerVersion\" > 0 AND \"Revision\" > 0))")); b.HasKey(e=>new {e.TenantId,e.ActorId,e.CommandId});
            b.HasAlternateKey(e=>new {e.TenantId,e.ActorId,e.CommandId,e.EnrollmentId}).HasName("AK_DailyOfflineOutcome_ParentScope");
            b.HasIndex(e=>new {e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.Kind,e.RecordId}).HasDatabaseName("IX_DailyOfflineOutcome_Scope");
            b.HasIndex(e=>new {e.TenantId,e.ActorId,e.ParentCommandId,e.EnrollmentId}).HasDatabaseName("IX_DailyOfflineOutcome_ParentScope");
            b.HasOne<DailyOfflineEnrollment>().WithMany().HasForeignKey(e=>new {e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.Kind,e.RecordId})
                .HasPrincipalKey(e=>new {e.TenantId,e.Id,e.ActorId,e.VesselId,e.Kind,e.RecordId}).HasConstraintName("FK_DailyOfflineOutcome_Enrollment").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DailyOfflineOutcome>().WithMany().HasForeignKey(e=>new {e.TenantId,e.ActorId,e.ParentCommandId,e.EnrollmentId})
                .HasPrincipalKey(e=>new {e.TenantId,e.ActorId,e.CommandId,e.EnrollmentId}).HasConstraintName("FK_DailyOfflineOutcome_Parent").OnDelete(DeleteBehavior.Restrict);
            b.Property(e=>e.Kind).HasMaxLength(12); b.Property(e=>e.Digest).HasMaxLength(64); b.Property(e=>e.Result).HasMaxLength(24); b.Property(e=>e.OutcomeJson).HasColumnType("text");
            b.HasIndex(e=>new {e.TenantId,e.ActorId,e.EnrollmentId,e.ReceivedAt,e.CommandId}).HasDatabaseName("IX_DailyOfflineOutcome_History");
            b.HasIndex(e=>new {e.TenantId,e.ActorId,e.ParentCommandId}).HasDatabaseName("UX_DailyOfflineOutcome_Resolution").IsUnique().HasFilter("\"ParentCommandId\" IS NOT NULL");
        });
    }
}
