using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// USER-ADMIN-02 / UA2-AC-03, UA2-AC-04: invitation accounts bind exactly once; invitations close exactly once and keep
// their token hash, expected subject and expiry. These guards run before any database round trip.
[Trait("Category", "Unit")]
[Trait("Requirement", "USER-ADMIN-02")]
public sealed class MembershipInvitationStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-0000000ad002");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));
    private static MembershipInvitation Invitation(string status = "Pending") => new() { TenantId = Tenant, Id = Guid.NewGuid(), MembershipId = Guid.NewGuid(),
        TokenHash = new string('A', 64), Status = status, CreatedAt = DateTimeOffset.UnixEpoch, ExpiresAt = DateTimeOffset.UnixEpoch.AddDays(7), CreatedBy = Guid.NewGuid().ToString("D") };

    [Fact]
    public void Only_an_admin_provisioned_account_starts_without_a_subject()
    {
        using var db = Context();
        db.Add(new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = "https://idp.example", Subject = null, Source = "ConfigurationImport" });
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void A_bound_account_never_changes_and_a_pending_account_changes_only_its_subject()
    {
        using var db = Context();
        var bound = new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = "https://idp.example", Subject = "holder", Source = "AdminProvisioned" };
        db.Attach(bound); bound.Subject = "someone-else";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var pending = new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = "https://idp.example", Subject = null, Source = "AdminProvisioned" };
        db.Attach(pending); pending.Subject = "holder"; pending.Issuer = "https://other.example";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var configured = new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = "https://idp.example", Subject = null, Source = "ConfigurationImport" };
        db.Attach(configured); configured.Subject = "holder";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var removed = new UserAccount { TenantId = Tenant, Id = Guid.NewGuid(), Issuer = "https://idp.example", Subject = null, Source = "AdminProvisioned" };
        db.Attach(removed); db.Remove(removed);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Revoked")]
    [InlineData("Expired")]
    public void An_invitation_starts_pending(string status)
    {
        using var db = Context();
        db.Add(Invitation(status));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void An_invitation_closes_exactly_once_and_keeps_its_token_subject_and_expiry()
    {
        using var db = Context();
        var closed = Invitation("Accepted");
        closed.ClosedAt = DateTimeOffset.UnixEpoch;
        db.Attach(closed); closed.Status = "Revoked";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var pending = Invitation();
        db.Attach(pending); pending.ExpiresAt = pending.ExpiresAt.AddDays(30); pending.Status = "Accepted"; pending.ClosedAt = DateTimeOffset.UnixEpoch;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var noClose = Invitation();
        db.Attach(noClose); noClose.Status = "Revoked";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var removed = Invitation();
        db.Attach(removed); db.Remove(removed);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void An_invited_membership_is_admin_provisioned_and_never_reopened()
    {
        using var db = Context();
        db.Add(new AccountMembership { TenantId = Tenant, Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Status = "Invited", StatusReason = "AdminInvitation",
            AccessRevision = 0, Source = "ConfigurationImport" });
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var suspended = new AccountMembership { TenantId = Tenant, Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Status = "Suspended",
            StatusReason = "AdministrativeSuspension", AccessRevision = 1, Source = "AdminProvisioned" };
        db.Attach(suspended); suspended.Status = "Invited"; suspended.StatusReason = "AdminInvitation"; suspended.AccessRevision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var invited = new AccountMembership { TenantId = Tenant, Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Status = "Invited",
            StatusReason = "AdminInvitation", AccessRevision = 0, Source = "AdminProvisioned" };
        db.Attach(invited); invited.Status = "Suspended"; invited.StatusReason = "AdministrativeSuspension"; invited.AccessRevision = 1;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void The_accepted_binding_index_ignores_unbound_accounts()
    {
        using var db = Context();
        var index = db.Model.FindEntityType(typeof(UserAccount))!.GetIndexes().Single(i => i.GetDatabaseName() == "IX_Accounts_IssuerSubject");
        Assert.True(index.IsUnique);
        Assert.Equal("\"Subject\" IS NOT NULL", index.GetFilter());
        var pending = db.Model.FindEntityType(typeof(MembershipInvitation))!.GetIndexes().Single(i => i.GetDatabaseName() == "IX_MembershipInvitations_Pending");
        Assert.True(pending.IsUnique);
        Assert.Equal("\"Status\" = 'Pending'", pending.GetFilter());
    }
}
