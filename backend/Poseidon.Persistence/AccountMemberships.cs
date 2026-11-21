using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// PLAN-1511-5202 / 52.02. Federated account identity: issuer+subject only. No password,
// hash, token, e-mail or display name is stored for a federated account.
public sealed class UserAccount : ITenantEntity
{
    public Guid TenantId { get; set; }
    // Equal to the trusted actor id so historical audit attribution stays stable.
    public Guid Id { get; set; }
    public required string Issuer { get; set; }
    public required string Subject { get; set; }
    public string AccountType { get; set; } = "Human";
    public required string Source { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}

// Current tenant membership lifecycle. The persisted state narrows the operator-provisioned
// bootstrap grant (FederationSettings.Memberships); it never widens permissions.
public sealed class AccountMembership : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public required string Status { get; set; }
    public required string StatusReason { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; }
    // Monotonic deny fence: every lifecycle change increments it and existing sessions
    // stamped with an older revision are rejected on their next request.
    public long AccessRevision { get; set; }
    public string? RoleContext { get; set; }
    public Guid? ActiveCompanyId { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}

// Imported snapshot of server-provisioned vessel scope. Effective scope is the
// intersection with the current operator grant; rows are immutable.
public sealed class AccountMembershipVesselGrant : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid VesselId { get; set; }
    public bool CanEdit { get; set; }
}

// 52.02 §6 vekalet. A record of an approved, time-boxed authority transfer. It is
// exposed to the exact action resolver (54.02) as current membership context; it does
// not itself add permissions to the generic trusted actor.
public sealed class MembershipDelegation : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid DelegatorMembershipId { get; set; }
    public Guid DelegateMembershipId { get; set; }
    public required string Permissions { get; set; }
    // 52.02 paragraph:102 scope snapshot: company (optional) plus DelegationVesselScope rows.
    // Always a subset of the source's scope at creation; narrowed again at request time.
    public Guid? CompanyId { get; set; }
    // Closed reason code (no free text); LegacyUnscoped only for rows predating the snapshot.
    public required string ReasonCode { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public required string Status { get; set; }
    public string? RevocationReason { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public required string ApprovedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}

// Immutable vessel part of a delegation scope snapshot.
public sealed class DelegationVesselScope : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DelegationId { get; set; }
    public Guid VesselId { get; set; }
    public bool CanEdit { get; set; }
}

// RECOVERY_REQUIRED (54.02 Rev4 paragraph:807): a forced security suspension removed
// the last active identity administrator. No hidden administrator is created.
public sealed class TenantAccessRecovery : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid MembershipId { get; set; }
    public required string Cause { get; set; }
    public DateTimeOffset RaisedAt { get; set; }
    // Closed once, with a closed reason, when the security authority resumes the membership.
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}

// USER-ADMIN-01: immutable grant revision of an admin-provisioned membership (Source = AdminProvisioned). The latest
// revision is the current grant; earlier ones are the reviewable grant history. Bundles are closed role-bundle codes,
// Permissions is the effective snapshot bounded at grant time by the granting administrator's own authority.
public sealed class MembershipGrantRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public int Revision { get; set; }
    public required string Bundles { get; set; }
    public required string IndividualPermissions { get; set; }
    public required string Permissions { get; set; }
    public bool TenantWideRead { get; set; }
    public bool TenantWideManage { get; set; }
    public Guid? ActiveCompanyId { get; set; }
    public required string GrantedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// Immutable vessel scope of one grant revision.
public sealed class MembershipGrantVessel : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public int Revision { get; set; }
    public Guid VesselId { get; set; }
    public bool CanEdit { get; set; }
}

public sealed class MembershipCommandReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid ResourceId { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
    // Import only: the original result (counts and closed skip codes, no identity values) for replay.
    public string? Result { get; set; }
}

public static class AccountMembershipStorage
{
    public const string IdentitySchema = "identity";
    internal static readonly string[] Statuses = ["Active", "Suspended", "Revoked"];
    internal static readonly string[] Reasons = ["ConfigurationImport", "SyntheticFixture", "AdministrativeSuspension", "AdministrativeResume",
        "AdministrativeRevocation", "SecuritySuspension", "SecurityResume", "AdminProvisioning"];
    internal static readonly string[] DelegationReasons = ["PlannedAbsence", "UnplannedAbsence", "WorkloadCover", "Handover"];
    internal static readonly string[] RecoveryResolutions = ["SecuritySuspensionResumed"];
    internal static readonly string[] Sources = ["ConfigurationImport", "SyntheticUnverified", "AdminProvisioned"];
    internal static readonly string[] Actions = ["MembershipImport", "MembershipSuspend", "MembershipResume", "MembershipRevoke",
        "MembershipSecuritySuspend", "DelegationCreate", "DelegationRevoke", "MembershipCreate", "MembershipGrantUpdate"];

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<UserAccount>(b =>
        {
            b.ToTable("Accounts", IdentitySchema, t => t.HasCheckConstraint("CK_Accounts_Closed",
                "\"AccountType\" = 'Human' AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified','AdminProvisioned') AND length(\"Issuer\") > 0 AND length(\"Subject\") > 0"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => x.Id).HasName("AK_Accounts_Id");
            // One federated identity maps to exactly one account across all tenants.
            b.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique().HasDatabaseName("IX_Accounts_IssuerSubject");
            b.Property(x => x.Issuer).HasMaxLength(1024);
            b.Property(x => x.Subject).HasMaxLength(255);
            b.Property(x => x.AccountType).HasMaxLength(16);
            b.Property(x => x.Source).HasMaxLength(32);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).HasConstraintName("FK_Accounts_Tenant").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountMembership>(b =>
        {
            b.ToTable("Memberships", IdentitySchema, t => t.HasCheckConstraint("CK_Memberships_Closed",
                "\"Status\" IN ('Active','Suspended','Revoked') AND \"AccessRevision\" >= 0 AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified','AdminProvisioned') AND " +
                "\"StatusReason\" IN ('ConfigurationImport','SyntheticFixture','AdministrativeSuspension','AdministrativeResume','AdministrativeRevocation','SecuritySuspension','SecurityResume','AdminProvisioning')"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.AccountId }).IsUnique().HasDatabaseName("IX_Memberships_Account");
            b.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("IX_Memberships_Status");
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.StatusReason).HasMaxLength(32);
            b.Property(x => x.Source).HasMaxLength(32);
            b.Property(x => x.RoleContext).HasMaxLength(600);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => new { x.TenantId, x.AccountId }).HasConstraintName("FK_Memberships_Account").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.ActiveCompanyId }).HasConstraintName("FK_Memberships_ActiveCompany").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountMembershipVesselGrant>(b =>
        {
            b.ToTable("MembershipVesselGrants", IdentitySchema);
            b.HasKey(x => new { x.TenantId, x.MembershipId, x.VesselId });
            b.HasOne<AccountMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId }).HasConstraintName("FK_MembershipVesselGrants_Membership").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_MembershipVesselGrants_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MembershipDelegation>(b =>
        {
            b.ToTable("Delegations", IdentitySchema, t => t.HasCheckConstraint("CK_Delegations_Closed",
                "\"Status\" IN ('Active','Revoked') AND \"ValidUntil\" > \"ValidFrom\" AND \"DelegatorMembershipId\" <> \"DelegateMembershipId\" AND " +
                "length(\"Permissions\") > 0 AND \"ReasonCode\" IN ('PlannedAbsence','UnplannedAbsence','WorkloadCover','Handover','LegacyUnscoped') AND " +
                "((\"Status\" = 'Active' AND \"RevokedAt\" IS NULL AND \"RevocationReason\" IS NULL) OR " +
                "(\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL AND \"RevocationReason\" IS NOT NULL AND \"RevocationReason\" IN ('AdministrativeRevocation','MembershipSuspended','MembershipRevoked')))"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.DelegateMembershipId, x.Status }).HasDatabaseName("IX_Delegations_Delegate");
            b.HasIndex(x => new { x.TenantId, x.DelegatorMembershipId, x.Status }).HasDatabaseName("IX_Delegations_Delegator");
            b.Property(x => x.Permissions).HasMaxLength(2048);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.RevocationReason).HasMaxLength(32);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.ReasonCode).HasMaxLength(32);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId }).HasConstraintName("FK_Delegations_Company").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AccountMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.DelegatorMembershipId }).HasConstraintName("FK_Delegations_Delegator").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AccountMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.DelegateMembershipId }).HasConstraintName("FK_Delegations_Delegate").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DelegationVesselScope>(b =>
        {
            b.ToTable("DelegationVesselScopes", IdentitySchema);
            b.HasKey(x => new { x.TenantId, x.DelegationId, x.VesselId });
            b.HasOne<MembershipDelegation>().WithMany().HasForeignKey(x => new { x.TenantId, x.DelegationId }).HasConstraintName("FK_DelegationVesselScopes_Delegation").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_DelegationVesselScopes_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TenantAccessRecovery>(b =>
        {
            b.ToTable("AccessRecoveries", IdentitySchema, t =>
            {
                t.HasCheckConstraint("CK_AccessRecoveries_Cause", "\"Cause\" IN ('SecuritySuspension')");
                t.HasCheckConstraint("CK_AccessRecoveries_Resolution", "(\"ResolvedAt\" IS NULL AND \"Resolution\" IS NULL) OR " +
                    "(\"ResolvedAt\" IS NOT NULL AND \"Resolution\" IS NOT NULL AND \"Resolution\" IN ('SecuritySuspensionResumed'))");
            });
            b.Property(x => x.Resolution).HasMaxLength(32);
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.RaisedAt }).HasDatabaseName("IX_AccessRecoveries_Raised");
            b.Property(x => x.Cause).HasMaxLength(32);
            b.HasOne<AccountMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId }).HasConstraintName("FK_AccessRecoveries_Membership").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MembershipGrantRevision>(b =>
        {
            b.ToTable("MembershipGrantRevisions", IdentitySchema, t => t.HasCheckConstraint("CK_MembershipGrantRevisions_Closed",
                "\"Revision\" >= 1 AND length(\"Bundles\") > 0 AND length(\"Permissions\") > 0 AND length(\"GrantedBy\") > 0"));
            b.HasKey(x => new { x.TenantId, x.MembershipId, x.Revision });
            b.Property(x => x.Bundles).HasMaxLength(512);
            b.Property(x => x.IndividualPermissions).HasMaxLength(8192);
            b.Property(x => x.Permissions).HasMaxLength(8192);
            b.Property(x => x.GrantedBy).HasMaxLength(200);
            b.HasOne<AccountMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId }).HasConstraintName("FK_MembershipGrantRevisions_Membership").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.ActiveCompanyId }).HasConstraintName("FK_MembershipGrantRevisions_Company").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MembershipGrantVessel>(b =>
        {
            b.ToTable("MembershipGrantVessels", IdentitySchema);
            b.HasKey(x => new { x.TenantId, x.MembershipId, x.Revision, x.VesselId });
            b.HasOne<MembershipGrantRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId, x.Revision }).HasConstraintName("FK_MembershipGrantVessels_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_MembershipGrantVessels_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MembershipCommandReceipt>(b =>
        {
            b.ToTable("MembershipCommandReceipts", IdentitySchema, t => t.HasCheckConstraint("CK_MembershipCommandReceipts_Action",
                "\"Action\" IN ('MembershipImport','MembershipSuspend','MembershipResume','MembershipRevoke','MembershipSecuritySuspend','DelegationCreate','DelegationRevoke','MembershipCreate','MembershipGrantUpdate')"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(80);
            b.Property(x => x.Action).HasMaxLength(32);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.Result).HasMaxLength(8192);
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).HasConstraintName("FK_MembershipCommandReceipts_Tenant").OnDelete(DeleteBehavior.Restrict);
        });
    }

    // Application-level immutability: the database constraints above are the second line.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes)
    {
        foreach (var entry in changes)
        {
            switch (entry.Entity)
            {
                case UserAccount:
                    if (entry.State != EntityState.Added) throw new InvalidOperationException("Federated account identity is immutable.");
                    break;
                case MembershipGrantRevision r:
                    if (entry.State != EntityState.Added) throw new InvalidOperationException("Membership grant revisions are immutable; a change appends a revision.");
                    if (r.Revision < 1 || Poseidon.Domain.MembershipGrantPolicy.Split(r.Bundles) is not { Length: > 0 } codes || codes.Any(c => Poseidon.Domain.MembershipRoleBundles.Find(c) == null))
                        throw new InvalidOperationException("A membership grant revision needs closed role bundle codes.");
                    break;
                case MembershipGrantVessel:
                    if (entry.State != EntityState.Added) throw new InvalidOperationException("Membership grant revisions are immutable; a change appends a revision.");
                    break;
                case AccountMembershipVesselGrant or DelegationVesselScope or MembershipCommandReceipt:
                    if (entry.State != EntityState.Added) throw new InvalidOperationException("Membership grant snapshots, delegation scopes and receipts are immutable.");
                    break;
                case TenantAccessRecovery r:
                    if (entry.State == EntityState.Added && (r.ResolvedAt != null || r.Resolution != null))
                        throw new InvalidOperationException("A recovery case starts open.");
                    if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified &&
                        (entry.Property(nameof(TenantAccessRecovery.ResolvedAt)).OriginalValue != null || r.ResolvedAt == null || !RecoveryResolutions.Contains(r.Resolution) ||
                         entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(TenantAccessRecovery.ResolvedAt) or nameof(TenantAccessRecovery.Resolution)))))
                        throw new InvalidOperationException("A recovery case can only be closed once with a closed resolution.");
                    break;
                case AccountMembership m:
                    if (!Statuses.Contains(m.Status) || !Reasons.Contains(m.StatusReason) || !Sources.Contains(m.Source))
                        throw new InvalidOperationException("Membership state is outside its closed contract.");
                    if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Membership history cannot be removed.");
                    if (entry.State == EntityState.Added && (m.Status != "Active" || m.AccessRevision != 0))
                        throw new InvalidOperationException("A membership starts active at access revision zero.");
                    if (entry.State == EntityState.Modified)
                    {
                        if (entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(AccountMembership.Status) or nameof(AccountMembership.StatusReason) or
                                nameof(AccountMembership.StatusChangedAt) or nameof(AccountMembership.AccessRevision))))
                            throw new InvalidOperationException("Membership ownership and grant snapshot are immutable.");
                        var before = (string)entry.Property(nameof(AccountMembership.Status)).OriginalValue!;
                        if (before == "Revoked") throw new InvalidOperationException("A revoked membership is terminal.");
                        if (before == m.Status || m.AccessRevision != (long)entry.Property(nameof(AccountMembership.AccessRevision)).OriginalValue! + 1)
                            throw new InvalidOperationException("A membership change advances exactly one access revision.");
                    }
                    break;
                case MembershipDelegation d:
                    if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Delegation history cannot be removed.");
                    if (entry.State == EntityState.Added && d.Status != "Active") throw new InvalidOperationException("A delegation starts active.");
                    if (entry.State == EntityState.Added && !DelegationReasons.Contains(d.ReasonCode)) throw new InvalidOperationException("A delegation needs a closed reason code.");
                    if (entry.State == EntityState.Modified && ((string)entry.Property(nameof(MembershipDelegation.Status)).OriginalValue! != "Active" || d.Status != "Revoked" ||
                        entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(MembershipDelegation.Status) or
                            nameof(MembershipDelegation.RevokedAt) or nameof(MembershipDelegation.RevocationReason)))))
                        throw new InvalidOperationException("A delegation can only be revoked once; its scope and window are immutable.");
                    break;
            }
        }
    }
}
