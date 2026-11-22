using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// USER-ADMIN-03: transactional outbox row for one invitation e-mail. Written in the same audited SaveChanges transaction
// as the invitation it delivers. The recipient address and the one-time token are needed only to send; they travel in
// ProtectedPayload, an ASP.NET Core Data Protection ciphertext bound to tenant + delivery id, and the payload is erased
// when the row reaches a terminal state. Nothing else on the row is personal data.
public sealed class InvitationDelivery : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid InvitationId { get; set; }
    public Guid MembershipId { get; set; }
    public required string State { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    // Claim fence so two dispatcher instances never send one row concurrently (optimistic, xmin-checked).
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? ProtectedPayload { get; set; }
    public required string CreatedBy { get; set; }
    public uint Version { get; set; }
}

// USER-ADMIN-03: 54.01 account.invite_hours tenant value (1-168). No row = the 24 h source default.
public sealed class TenantInvitationSettings : ITenantEntity
{
    public Guid TenantId { get; set; }
    public int LifetimeHours { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public required string UpdatedBy { get; set; }
    public uint Version { get; set; }
}

public static class InvitationDeliveryStorage
{
    internal static readonly string[] MutableColumns = [nameof(InvitationDelivery.State), nameof(InvitationDelivery.Attempts),
        nameof(InvitationDelivery.NextAttemptAt), nameof(InvitationDelivery.LeaseUntil), nameof(InvitationDelivery.CompletedAt),
        nameof(InvitationDelivery.LastErrorCode), nameof(InvitationDelivery.ProtectedPayload), nameof(InvitationDelivery.Version)];
    public const int PayloadMaxLength = 4096;

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<InvitationDelivery>(b =>
        {
            b.ToTable("InvitationDeliveries", AccountMembershipStorage.IdentitySchema, t => t.HasCheckConstraint("CK_InvitationDeliveries_Closed",
                "\"State\" IN ('QUEUED','SENT','FAILED','NOT_CONFIGURED','CANCELLED') AND \"Attempts\" >= 0 AND \"Attempts\" <= " + InvitationDeliveryPolicy.MaxAttempts + " AND " +
                "length(\"CreatedBy\") > 0 AND (\"LastErrorCode\" IS NULL OR \"LastErrorCode\" IN ('smtp_transient','smtp_rejected','smtp_retries_exhausted','not_configured'," +
                "'payload_unavailable','invitation_closed','invitation_expired')) AND " +
                // A queued row carries its payload and a due time; a terminal row never keeps the payload.
                "((\"State\" = 'QUEUED' AND \"ProtectedPayload\" IS NOT NULL AND \"NextAttemptAt\" IS NOT NULL AND \"CompletedAt\" IS NULL) OR " +
                "(\"State\" <> 'QUEUED' AND \"ProtectedPayload\" IS NULL AND \"CompletedAt\" IS NOT NULL AND \"LeaseUntil\" IS NULL))"));
            b.HasKey(x => new { x.TenantId, x.Id });
            // One delivery per invitation; a reissue creates a new invitation and therefore a new delivery.
            b.HasIndex(x => new { x.TenantId, x.InvitationId }).IsUnique().HasDatabaseName("IX_InvitationDeliveries_Invitation");
            b.HasIndex(x => new { x.TenantId, x.MembershipId }).HasDatabaseName("IX_InvitationDeliveries_Membership");
            b.HasIndex(x => x.NextAttemptAt).HasFilter("\"State\" = 'QUEUED'").HasDatabaseName("IX_InvitationDeliveries_Due");
            b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.LastErrorCode).HasMaxLength(32);
            b.Property(x => x.ProtectedPayload).HasMaxLength(PayloadMaxLength);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<MembershipInvitation>().WithMany().HasForeignKey(x => new { x.TenantId, x.InvitationId })
                .HasConstraintName("FK_InvitationDeliveries_Invitation").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TenantInvitationSettings>(b =>
        {
            b.ToTable("TenantInvitationSettings", AccountMembershipStorage.IdentitySchema, t => t.HasCheckConstraint("CK_TenantInvitationSettings_Lifetime",
                "\"LifetimeHours\" >= " + InvitationLifetimePolicy.MinHours + " AND \"LifetimeHours\" <= " + InvitationLifetimePolicy.MaxHours + " AND length(\"UpdatedBy\") > 0"));
            b.HasKey(x => x.TenantId);
            b.Property(x => x.UpdatedBy).HasMaxLength(200);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).HasConstraintName("FK_TenantInvitationSettings_Tenant").OnDelete(DeleteBehavior.Restrict);
        });
    }

    // Application-level state machine; the CHECK constraints above are the second line.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes)
    {
        foreach (var entry in changes)
        {
            switch (entry.Entity)
            {
                case InvitationDelivery d:
                    if (InvitationDeliveryPolicy.Parse(d.State) is not { } state || d.Attempts is < 0 or > InvitationDeliveryPolicy.MaxAttempts ||
                        d.LastErrorCode != null && !InvitationDeliveryPolicy.ErrorCodes.Contains(d.LastErrorCode))
                        throw new InvalidOperationException("Invitation delivery state is outside its closed contract.");
                    if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Invitation delivery history cannot be removed.");
                    var terminal = InvitationDeliveryPolicy.Terminal(state);
                    if (terminal ? d.ProtectedPayload != null || d.CompletedAt == null || d.LeaseUntil != null
                                 : d.ProtectedPayload is not { Length: > 0 } || d.NextAttemptAt == null || d.CompletedAt != null)
                        throw new InvalidOperationException("A queued delivery holds its protected payload; a terminal delivery never does.");
                    if (entry.State == EntityState.Added && (d.Attempts != 0 || !(state == InvitationDeliveryState.Queued || state == InvitationDeliveryState.NotConfigured)))
                        throw new InvalidOperationException("A delivery starts queued (or not configured) without attempts.");
                    if (entry.State == EntityState.Modified)
                    {
                        var before = InvitationDeliveryPolicy.Parse((string?)entry.Property(nameof(InvitationDelivery.State)).OriginalValue);
                        if (before is not { } from || !InvitationDeliveryPolicy.CanTransition(from, state) ||
                            entry.Properties.Any(p => p.IsModified && !MutableColumns.Contains(p.Metadata.Name)) ||
                            d.Attempts < (int)entry.Property(nameof(InvitationDelivery.Attempts)).OriginalValue! ||
                            !terminal && !string.Equals(d.ProtectedPayload, (string?)entry.Property(nameof(InvitationDelivery.ProtectedPayload)).OriginalValue, StringComparison.Ordinal))
                            throw new InvalidOperationException("Only a queued delivery changes; its payload, invitation and attempt history are immutable.");
                    }
                    break;
                case TenantInvitationSettings s:
                    if (entry.State == EntityState.Deleted) throw new InvalidOperationException("The tenant invitation setting is changed, never removed.");
                    if (!InvitationLifetimePolicy.ValidHours(s.LifetimeHours) || string.IsNullOrWhiteSpace(s.UpdatedBy))
                        throw new InvalidOperationException("The invitation lifetime is outside 54.01 account.invite_hours (1-168).");
                    if (entry.State == EntityState.Modified && entry.Property(nameof(TenantInvitationSettings.TenantId)).IsModified)
                        throw new InvalidOperationException("Tenant ownership cannot be changed.");
                    break;
            }
        }
    }
}
