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
}
