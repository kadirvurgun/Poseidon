using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// USER-ADMIN-01 / UA-AC-07: a grant change appends an immutable revision; revisions carry closed bundle codes only.
[Trait("Category", "Unit")]
[Trait("Requirement", "USER-ADMIN-01")]
public sealed class MembershipGrantStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-0000000ad001");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));
    private static MembershipGrantRevision Revision(string bundles = "read-only") => new() { TenantId = Tenant, MembershipId = Guid.NewGuid(), Revision = 1,
        Bundles = bundles, IndividualPermissions = "", Permissions = "directory.read", TenantWideRead = true, GrantedBy = Guid.NewGuid().ToString("D") };

    [Fact]
    public void Grant_revisions_and_their_vessel_scope_cannot_be_rewritten_or_removed()
    {
        using var db = Context();
        var revision = Revision();
        db.Attach(revision); revision.Permissions = "directory.read,membership.manage";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var other = Revision();
        db.Attach(other); db.Remove(other);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var vessel = new MembershipGrantVessel { TenantId = Tenant, MembershipId = Guid.NewGuid(), Revision = 1, VesselId = Guid.NewGuid() };
        db.Attach(vessel); vessel.CanEdit = true;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Theory]
    [InlineData("")]
    [InlineData("captain")]
    [InlineData("read-only,root")]
    public void A_new_revision_needs_closed_bundle_codes(string bundles)
    {
        using var db = Context();
        db.Add(Revision(bundles));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Admin_provisioned_membership_still_starts_active_at_revision_zero()
    {
        using var db = Context();
        db.Add(new AccountMembership { TenantId = Tenant, Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Status = "Suspended", StatusReason = "AdminProvisioning",
            AccessRevision = 0, Source = "AdminProvisioned" });
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        db.Add(new AccountMembership { TenantId = Tenant, Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Status = "Active", StatusReason = "AdminProvisioning",
            AccessRevision = 0, Source = "SelfService" });
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
}
