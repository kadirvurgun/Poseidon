using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class Notification : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid PlanId { get; set; }
    public long PlanRevision { get; set; }
    public string RuleKey { get; set; } = "";
    public string PolicyVersion { get; set; } = "";
    public string PolicyDigest { get; set; } = "";
    public string SourceContextHash { get; set; } = "";
    public long Episode { get; set; } = 1;
    public string SourceState { get; set; } = "";
    public string Severity { get; set; } = "Unknown";
    public bool Mandatory { get; set; }
    public Guid RecipientRoleId { get; set; }
    public Guid? EscalationRoleId { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? EscalateAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string State { get; set; } = "Open";
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}
public sealed class NotificationEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid NotificationId { get; set; }
    public long Sequence { get; set; }
    public string Kind { get; set; } = "";
    public Guid? ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
public sealed class NotificationPreference : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public bool MutedAdvisory { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}
public sealed class NotificationReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public string Key { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid ResourceId { get; set; }
    public Guid? NotificationId { get; set; }
    public Guid? PreferenceActorId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string OutcomeJson { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; }
}
// Control-plane lease/cursor only. No credentials, human appointment or delivery proof.
public sealed class NotificationDispatchState : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset? LastCompletedAt { get; set; }
    public int SourceOffset { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}
// A real evaluated state/basis boundary advances Episode. Null means unobserved,
// never a fabricated negative predicate or permission to clear an operational gate.
public sealed class NotificationEpisode : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid PlanId { get; set; }
    public string RuleKey { get; set; } = "";
    public string PolicyDigest { get; set; } = "";
    public long Episode { get; set; }
    public bool? Active { get; set; }
    // Current unavailability must not erase an observed inactive/active boundary.
    public bool? LastKnownActive { get; set; }
    public string? SourceContextHash { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}

internal static class NotificationStorage
{
    internal const string StateConstraint = "\"State\" IN ('Open','Acknowledged','Escalated','SourceChanged','SyntheticUnverified') AND \"Revision\" > 0 AND \"PlanRevision\" > 0 AND \"Severity\" IN ('Info','Warning','Critical','Unknown') AND (\"Severity\" <> 'Critical' OR \"Mandatory\")";
    internal const string ReceiptConstraint = "(\"Action\" = 'Acknowledge' AND \"NotificationId\" IS NOT NULL AND \"ResourceId\" = \"NotificationId\" AND \"PreferenceActorId\" IS NULL) OR (\"Action\" = 'Preference' AND \"NotificationId\" IS NULL AND \"PreferenceActorId\" IS NOT NULL AND \"PreferenceActorId\" = \"ActorId\" AND \"ResourceId\" = \"ActorId\")";
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Notification>(b => {
            b.ToTable("Notifications", "engagement", t => t.HasCheckConstraint("CK_Notification_State", StateConstraint));
            b.HasKey(n => new { n.TenantId, n.Id }); b.HasAlternateKey(n => new { n.TenantId, n.VesselId, n.Id });
            b.HasOne<MaintenancePlanRevision>().WithMany().HasForeignKey(n => new { n.TenantId, n.VesselId, n.PlanId, n.PlanRevision })
                .HasPrincipalKey(p => new { p.TenantId, p.VesselId, p.PlanId, p.Revision }).HasConstraintName("FK_Notification_SourceRevision").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(n => new { n.TenantId, n.VesselId, n.RuleKey, n.PolicyDigest, n.PlanId, n.PlanRevision, n.SourceContextHash, n.Episode }).IsUnique().HasDatabaseName("UX_Notification_SourceTrigger");
            b.HasIndex(n => new { n.TenantId, n.VesselId, n.PlanId, n.PlanRevision }).HasDatabaseName("IX_Notification_SourceRevision");
            b.Property(n => n.RuleKey).HasMaxLength(64); b.Property(n => n.PolicyVersion).HasMaxLength(80);
            b.Property(n => n.PolicyDigest).HasMaxLength(64); b.Property(n => n.Severity).HasMaxLength(16);
            b.Property(n => n.SourceContextHash).HasMaxLength(64);
            b.Property(n => n.SourceState).HasMaxLength(24); b.Property(n => n.State).HasMaxLength(24);
            b.Property(n => n.Version).IsRowVersion();
        });
        model.Entity<NotificationEvent>(b => {
            b.ToTable("NotificationEvents", "engagement"); b.HasKey(e => new { e.TenantId, e.Id });
            b.HasIndex(e => new { e.TenantId, e.NotificationId, e.Sequence }).IsUnique().HasDatabaseName("UX_NotificationEvent_Sequence"); b.Property(e => e.Kind).HasMaxLength(24);
            b.HasIndex(e => new { e.TenantId, e.VesselId, e.NotificationId }).HasDatabaseName("IX_NotificationEvent_Notification");
            b.HasOne<Notification>().WithMany().HasForeignKey(e => new { e.TenantId, e.VesselId, e.NotificationId })
                .HasPrincipalKey(n => new { n.TenantId, n.VesselId, n.Id }).HasConstraintName("FK_NotificationEvent_Notification").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<NotificationPreference>(b => {
            b.ToTable("NotificationPreferences", "engagement"); b.HasKey(p => new { p.TenantId, p.VesselId, p.ActorId });
            b.HasOne<Vessel>().WithMany().HasForeignKey(p => new { p.TenantId, p.VesselId }).HasConstraintName("FK_NotificationPreference_Vessel").OnDelete(DeleteBehavior.Restrict);
            b.Property(p => p.Version).IsRowVersion();
        });
        model.Entity<NotificationReceipt>(b => {
            b.ToTable("NotificationReceipts", "engagement", t => t.HasCheckConstraint("CK_Notification_Receipt", ReceiptConstraint));
            b.HasKey(r => new { r.TenantId, r.ActorId, r.Key }); b.Property(r => r.Key).HasMaxLength(80);
            b.Property(r => r.Action).HasMaxLength(24); b.Property(r => r.Fingerprint).HasMaxLength(64); b.Property(r => r.OutcomeJson).HasColumnType("text");
            b.HasOne<Notification>().WithMany().HasForeignKey(r => new { r.TenantId, r.VesselId, r.NotificationId })
                .HasPrincipalKey(n => new { n.TenantId, n.VesselId, n.Id }).HasConstraintName("FK_NotificationReceipt_Notification").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<NotificationPreference>().WithMany().HasForeignKey(r => new { r.TenantId, r.VesselId, r.PreferenceActorId })
                .HasPrincipalKey(p => new { p.TenantId, p.VesselId, p.ActorId }).HasConstraintName("FK_NotificationReceipt_Preference").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r => new { r.TenantId, r.VesselId, r.NotificationId }).HasDatabaseName("IX_NotificationReceipt_Notification");
            b.HasIndex(r => new { r.TenantId, r.VesselId, r.PreferenceActorId }).HasDatabaseName("IX_NotificationReceipt_Preference");
        });
        model.Entity<NotificationDispatchState>(b => {
            b.ToTable("NotificationDispatchStates", "engagement", t => t.HasCheckConstraint("CK_NotificationDispatch_State",
                "\"Revision\" > 0 AND \"SourceOffset\" >= 0 AND ((\"LeaseOwner\" IS NULL AND \"LeaseUntil\" IS NULL) OR (\"LeaseOwner\" IS NOT NULL AND \"LeaseUntil\" IS NOT NULL))"));
            b.HasKey(s => new { s.TenantId, s.VesselId }); b.Property(s => s.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(s => new { s.TenantId, s.VesselId }).HasConstraintName("FK_NotificationDispatch_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<NotificationEpisode>(b => {
            b.ToTable("NotificationEpisodes", "engagement", t => t.HasCheckConstraint("CK_NotificationEpisode_State",
                "\"Revision\" > 0 AND \"Episode\" >= 0 AND (\"Active\" IS NULL OR \"Active\" IS NOT DISTINCT FROM \"LastKnownActive\") AND (\"Active\" IS DISTINCT FROM TRUE OR (\"Episode\" > 0 AND \"SourceContextHash\" IS NOT NULL))"));
            b.HasKey(e => new { e.TenantId, e.VesselId, e.PlanId, e.RuleKey, e.PolicyDigest }); b.Property(e => e.Version).IsRowVersion();
            b.Property(e => e.RuleKey).HasMaxLength(64); b.Property(e => e.PolicyDigest).HasMaxLength(64); b.Property(e => e.SourceContextHash).HasMaxLength(64);
            b.HasOne<MaintenancePlan>().WithMany().HasForeignKey(e => new { e.TenantId, e.VesselId, e.PlanId })
                .HasPrincipalKey(p => new { p.TenantId, p.VesselId, p.Id }).HasConstraintName("FK_NotificationEpisode_Plan").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
