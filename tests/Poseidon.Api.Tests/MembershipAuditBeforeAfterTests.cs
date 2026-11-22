using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5202 CI gate regression (run 44486086531): every membership/delegation command audit row must prove its
// before/after through the unchanged CriticalWriteCoverageCapture.BeforeAfter rule. Projection-only EF tracking:
// no connection opens, no SaveChanges, no persisted audit rows or runtime coverage evidence.
[Trait("Requirement", "PLAN-1511-5202")]
public sealed class MembershipAuditBeforeAfterTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Correlation = "synthetic-membership-correlation";
    private const string Issuer = "https://synthetic-issuer.example";
    private const string Subject = "synthetic-subject-4711";

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>();
        options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }

    // Mirrors the audited SaveChanges row construction. Membership receipts carry no committed xmin, so no
    // receipt-version proof is offered: each row must stand on its own revision or closed binding/state.
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command)
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [entry, null, new AuditCommandContext(command, "PREPARATION_COMMAND"), true])!;
        T? Get<T>(string name) => (T?)type.GetProperty(name)!.GetValue(projection);
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ActorId = "synthetic", ActorType = "User", Action = entry.State.ToString(),
            EntityType = entry.Metadata.ClrType.Name, EntityId = "synthetic", CorrelationId = Correlation,
            ResourceId = Get<Guid?>("ResourceId"), VesselId = Get<Guid?>("VesselId"),
            BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
            BeforeRevision = Get<long?>("BeforeRevision"), AfterRevision = Get<long?>("AfterRevision"), ChangeSummary = Get<string>("Summary")
        };
        Assert.True(row.ChangeSummary!.Length <= 2048);
        Assert.DoesNotContain(Issuer, row.ChangeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(Subject, row.ChangeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("membership.manage", row.ChangeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-approver", row.ChangeSummary, StringComparison.Ordinal);
        Assert.NotNull(row.ResourceId);
        using var summary = JsonDocument.Parse(row.ChangeSummary);
        return (row, summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!, x => x.Clone(), StringComparer.Ordinal));
    }

    private static bool Proven(EntityEntry entry, string command)
    {
        var (row, fields) = Row(entry, command);
        return CriticalWriteCoverageCapture.BeforeAfter(row, new HashSet<(string, Guid, Guid?)>(), fields);
    }

    private static EntityEntry Added(PoseidonDbContext db, object entity) { var e = db.Entry(entity); e.State = EntityState.Added; return e; }

    private static AccountMembership Membership(Guid account) => new()
    {
        TenantId = Tenant, Id = Guid.NewGuid(), AccountId = account, Status = "Active", StatusReason = "ConfigurationImport",
        StatusChangedAt = T0, AccessRevision = 0, Source = "SyntheticUnverified", CreatedAt = T0, Version = 11
    };

    private static MembershipDelegation Delegation() => new()
    {
        TenantId = Tenant, Id = Guid.NewGuid(), DelegatorMembershipId = Guid.NewGuid(), DelegateMembershipId = Guid.NewGuid(),
        Permissions = "membership.manage", CompanyId = Guid.NewGuid(), ReasonCode = "PlannedAbsence", ValidFrom = T0, ValidUntil = T0.AddDays(3),
        Status = "Active", ApprovedBy = "synthetic-approver", CreatedAt = T0, Version = 13
    };

    private static TenantAccessRecovery Recovery() => new()
    {
        TenantId = Tenant, Id = Guid.NewGuid(), MembershipId = Guid.NewGuid(), Cause = "SecuritySuspension", RaisedAt = T0
    };

    private static MembershipCommandReceipt Receipt(string action) => new()
    {
        TenantId = Tenant, ActorId = Guid.NewGuid(), Key = "synthetic-key-" + action, Action = action, Fingerprint = new string('a', 64),
        ResourceId = Guid.NewGuid(), CommittedAt = T0
    };

    [Fact]
    public void Membership_import_rows_are_proven_without_identity_values()
    {
        using var db = Db();
        var account = new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = Issuer, Subject = Subject, Source = "SyntheticUnverified", CreatedAt = T0 };
        Assert.True(Proven(Added(db, account), "MembershipImport"));
        var membership = Membership(account.Id);
        Assert.True(Proven(Added(db, membership), "MembershipImport"));
        Assert.True(Proven(Added(db, new AccountMembershipVesselGrant { TenantId = Tenant, MembershipId = membership.Id, VesselId = Guid.NewGuid(), CanEdit = true }), "MembershipImport"));
        Assert.True(Proven(Added(db, Receipt("MembershipImport")), "MembershipImport"));
    }

    [Theory]
    [InlineData("MembershipSuspend", "Suspended", "AdministrativeSuspension")]
    [InlineData("MembershipSecuritySuspend", "Suspended", "SecuritySuspension")]
    [InlineData("MembershipRevoke", "Revoked", "AdministrativeRevocation")]
    public void Membership_lifecycle_changes_are_proven_by_access_revision(string command, string status, string reason)
    {
        using var db = Db();
        var membership = Membership(Guid.NewGuid());
        var entry = db.Attach(membership);
        membership.Status = status; membership.StatusReason = reason; membership.StatusChangedAt = T0.AddHours(1); membership.AccessRevision = 1;
        entry.DetectChanges();
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.True(Proven(entry, command));
        Assert.True(Proven(Added(db, Receipt(command)), command));
    }

    [Fact]
    public void Membership_resume_and_recovery_resolution_are_proven()
    {
        using var db = Db();
        var membership = Membership(Guid.NewGuid());
        membership.Status = "Suspended"; membership.StatusReason = "SecuritySuspension"; membership.AccessRevision = 1;
        var entry = db.Attach(membership);
        membership.Status = "Active"; membership.StatusReason = "SecurityResume"; membership.AccessRevision = 2;
        entry.DetectChanges();
        Assert.True(Proven(entry, "MembershipResume"));

        Assert.True(Proven(Added(db, Recovery()), "MembershipSecuritySuspend"));
        var recovery = Recovery();
        var recoveryEntry = db.Attach(recovery);
        recovery.ResolvedAt = T0.AddHours(2); recovery.Resolution = "SecuritySuspensionResumed";
        recoveryEntry.DetectChanges();
        Assert.Equal(EntityState.Modified, recoveryEntry.State);
        Assert.True(Proven(recoveryEntry, "MembershipResume"));
    }

    [Fact]
    public void Delegation_create_and_revoke_rows_are_proven_without_permissions_or_approver()
    {
        using var db = Db();
        var delegation = Delegation();
        Assert.True(Proven(Added(db, delegation), "DelegationCreate"));
        Assert.True(Proven(Added(db, new DelegationVesselScope { TenantId = Tenant, DelegationId = delegation.Id, VesselId = Guid.NewGuid(), CanEdit = false }), "DelegationCreate"));
        Assert.True(Proven(Added(db, Receipt("DelegationCreate")), "DelegationCreate"));

        var active = Delegation();
        var entry = db.Attach(active);
        active.Status = "Revoked"; active.RevocationReason = "AdministrativeRevocation"; active.RevokedAt = T0.AddHours(1);
        entry.DetectChanges();
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.True(Proven(entry, "DelegationRevoke"));
        Assert.True(Proven(Added(db, Receipt("DelegationRevoke")), "DelegationRevoke"));
    }

    // USER-ADMIN-01 CI gate regression (run 44939813106, AUDIT_BEFORE_AFTER_UNPROVEN): a grant change appends revision N
    // as an Added row, which must not claim a before revision; create and grant-update rows all prove themselves.
    [Fact]
    [Trait("Requirement", "USER-ADMIN-01")]
    public void Membership_create_and_grant_revision_rows_are_proven_without_permission_lists()
    {
        using var db = Db();
        var membership = Guid.NewGuid();
        Assert.True(Proven(Added(db, new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = Issuer, Subject = Subject, Source = "AdminProvisioned", CreatedAt = T0 }), "MembershipCreate"));
        Assert.True(Proven(Added(db, new AccountMembership { TenantId = Tenant, Id = membership, AccountId = Guid.NewGuid(), Status = "Active",
            StatusReason = "AdminProvisioning", StatusChangedAt = T0, AccessRevision = 0, Source = "AdminProvisioned", CreatedAt = T0 }), "MembershipCreate"));
        foreach (var (revision, command) in new[] { (1, "MembershipCreate"), (2, "MembershipGrantUpdate"), (3, "MembershipGrantUpdate") })
        {
            Assert.True(Proven(Added(db, new MembershipGrantRevision { TenantId = Tenant, MembershipId = membership, Revision = revision, Bundles = "read-only,tenant-admin",
                IndividualPermissions = "crew.read", Permissions = "crew.read,directory.read,membership.manage", TenantWideManage = true, GrantedBy = Guid.NewGuid().ToString("D"), CreatedAt = T0 }), command));
            Assert.True(Proven(Added(db, new MembershipGrantVessel { TenantId = Tenant, MembershipId = membership, Revision = revision, VesselId = Guid.NewGuid(), CanEdit = true }), command));
            Assert.True(Proven(Added(db, Receipt(command)), command));
        }
    }
    // USER-ADMIN-02: invitation create, reissue, acceptance and revocation rows prove themselves; the token hash and the
    // expected/accepted subject never enter an audit summary.
    [Fact]
    [Trait("Requirement", "USER-ADMIN-02")]
    public void Invitation_rows_are_proven_without_token_or_subject()
    {
        using var db = Db();
        var hash = new string('B', 64);
        MembershipInvitation Invitation(Guid membership) => new() { TenantId = Tenant, Id = Guid.NewGuid(), MembershipId = membership, TokenHash = hash,
            ExpectedSubject = Subject, Status = "Pending", CreatedAt = T0, ExpiresAt = T0.AddDays(7), CreatedBy = Guid.NewGuid().ToString("D"), Version = 21 };
        var membershipId = Guid.NewGuid();
        Assert.True(Proven(Added(db, new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = Issuer, Subject = null, Source = "AdminProvisioned", CreatedAt = T0 }), "MembershipCreate"));
        Assert.True(Proven(Added(db, new AccountMembership { TenantId = Tenant, Id = membershipId, AccountId = Guid.NewGuid(), Status = "Invited",
            StatusReason = "AdminInvitation", StatusChangedAt = T0, AccessRevision = 0, Source = "AdminProvisioned", CreatedAt = T0 }), "MembershipCreate"));
        foreach (var command in new[] { "MembershipCreate", "MembershipInvitationReissue" })
        {
            var (row, _) = Row(Added(db, Invitation(membershipId)), command);
            Assert.DoesNotContain(hash, row.ChangeSummary, StringComparison.Ordinal);
            Assert.True(Proven(Added(db, Invitation(membershipId)), command));
        }
        foreach (var (status, command) in new[] { ("Superseded", "MembershipInvitationReissue"), ("Accepted", "MembershipInvitationAccept"), ("Revoked", "MembershipRevoke") })
        {
            var pending = Invitation(membershipId);
            var entry = db.Attach(pending);
            pending.Status = status; pending.ClosedAt = T0.AddHours(1);
            entry.DetectChanges();
            Assert.Equal(EntityState.Modified, entry.State);
            Assert.True(Proven(entry, command));
        }
        // Acceptance: the account binds (Pending -> Bound) and the membership becomes Active at access revision 1.
        var account = new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = Issuer, Subject = null, Source = "AdminProvisioned", CreatedAt = T0, Version = 31 };
        var accountEntry = db.Attach(account);
        account.Subject = Subject;
        accountEntry.DetectChanges();
        Assert.True(Proven(accountEntry, "MembershipInvitationAccept"));
        var invited = Membership(account.Id);
        invited.Status = "Invited"; invited.StatusReason = "AdminInvitation"; invited.Source = "AdminProvisioned";
        var membershipEntry = db.Attach(invited);
        invited.Status = "Active"; invited.StatusReason = "InvitationAccepted"; invited.AccessRevision = 1;
        membershipEntry.DetectChanges();
        Assert.True(Proven(membershipEntry, "MembershipInvitationAccept"));
        Assert.True(Proven(Added(db, Receipt("MembershipInvitationAccept")), "MembershipInvitationAccept"));
        Assert.True(Proven(Added(db, Receipt("MembershipInvitationReissue")), "MembershipInvitationReissue"));
    }
}
