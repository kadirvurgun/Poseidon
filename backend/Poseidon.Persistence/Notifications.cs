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
            b.HasIndex(n => new { n.TenantId, n.VesselId, n.RuleKey, n.PolicyDigest, n.PlanId, n.PlanRevision, n.SourceContextHash }).IsUnique().HasDatabaseName("UX_Notification_SourceTrigger");
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
    }
}
