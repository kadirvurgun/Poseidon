using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// USER-ADMIN-01 / UA-AC-02..05: least privilege (grant ≤ grantor), closed bundles, no decision authority in bundles,
// self-modification and the administrator predicate used by the last-admin guard (13.03 §9 paragraph:102-104).
[Trait("Requirement", "USER-ADMIN-01")]
public sealed class MembershipGrantPolicyTests
{
    private static readonly HashSet<string> Known = new(MembershipRoleBundles.All.SelectMany(b => b.Permissions)
        .Concat(["membership.security.suspend", "departure.decide", "crew.write"]), StringComparer.Ordinal);
    private static readonly Guid Admin = Guid.NewGuid();
    private static Grantor Holding(params string[] permissions) => new(Admin, permissions, true, true);
    private static Grantor All() => Holding([.. Known]);
    private static GrantRequest Request(string[]? bundles, string[]? permissions = null, bool read = true, bool manage = false, VesselAccess[]? vessels = null) =>
        new(bundles, permissions, read, manage, vessels);

    [Fact]
    public void Bundles_are_closed_distinct_and_carry_no_decision_authority()
    {
        Assert.Equal(9, MembershipRoleBundles.All.Count);
        Assert.Equal(MembershipRoleBundles.Codes.Length, MembershipRoleBundles.Codes.Distinct().Count());
        Assert.Equal(MembershipRoleBundles.All.Count, MembershipRoleBundles.All.Select(b => b.RoleId).Distinct().Count());
        foreach (var bundle in MembershipRoleBundles.All)
        {
            Assert.Contains("directory.read", bundle.Permissions);
            Assert.Equal(bundle.Permissions.Length, bundle.Permissions.Distinct().Count());
            Assert.StartsWith("13.03", bundle.Locator);
            Assert.DoesNotContain(bundle.Permissions, MembershipRoleBundles.IsDecisionAuthority);
        }
        Assert.True(MembershipRoleBundles.IsDecisionAuthority("departure.decide"));
        Assert.True(MembershipRoleBundles.IsDecisionAuthority("membership.security.suspend"));
        Assert.True(MembershipRoleBundles.IsDecisionAuthority("human.verify"));
        Assert.False(MembershipRoleBundles.IsDecisionAuthority("human.review.read"));
        Assert.Contains("membership.manage", MembershipRoleBundles.Find("tenant-admin")!.Permissions);
        Assert.DoesNotContain(MembershipRoleBundles.All.Where(b => b.Code != "tenant-admin"), b => b.Permissions.Any(p => p.StartsWith("membership.", StringComparison.Ordinal)));
        Assert.Equal("membership", MembershipRoleBundles.Module("membership.manage"));
    }

    [Fact]
    public void A_grant_never_exceeds_the_grantor_authority()
    {
        var limited = Holding([.. MembershipRoleBundles.Find("read-only")!.Permissions]);
        var bundle = MembershipGrantPolicy.Validate(Request(["master"]), limited, Known);
        Assert.Equal([MembershipGrantPolicy.ExceedsGrantorAuthority], bundle.Errors["bundles"]);
        var individual = MembershipGrantPolicy.Validate(Request(["read-only"], ["crew.write"]), limited, Known);
        Assert.Equal([MembershipGrantPolicy.ExceedsGrantorAuthority], individual.Errors["permissions"]);
        var scope = MembershipGrantPolicy.Validate(Request(["read-only"], manage: true), new Grantor(Admin, [.. Known], true, false), Known);
        Assert.Equal([MembershipGrantPolicy.ExceedsGrantorAuthority], scope.Errors["tenantWideManage"]);
        var ok = MembershipGrantPolicy.Validate(Request(["read-only"]), limited, Known);
        Assert.True(ok.Valid);
        Assert.False(ok.Administrator);
    }

    [Fact]
    public void Bundles_and_individual_permissions_are_closed_and_canonical()
    {
        Assert.Equal(["required"], MembershipGrantPolicy.Validate(Request([]), All(), Known).Errors["bundles"]);
        Assert.Equal(["unknown_bundle"], MembershipGrantPolicy.Validate(Request(["captain"]), All(), Known).Errors["bundles"]);
        Assert.Equal(["duplicate"], MembershipGrantPolicy.Validate(Request(["master", "master"]), All(), Known).Errors["bundles"]);
        Assert.Equal(["unknown_permission"], MembershipGrantPolicy.Validate(Request(["master"], ["root.all"]), All(), Known).Errors["permissions"]);
        Assert.Equal(["duplicate"], MembershipGrantPolicy.Validate(Request(["master"], ["crew.write", "crew.write"]), All(), Known).Errors["permissions"]);
        var outcome = MembershipGrantPolicy.Validate(Request(["tug-master", "master"], ["departure.decide"]), All(), Known);
        Assert.True(outcome.Valid);
        Assert.Equal(["master", "tug-master"], outcome.Bundles);
        Assert.Equal(["departure.decide"], outcome.IndividualPermissions);
        Assert.Equal(outcome.EffectivePermissions.Order(StringComparer.Ordinal), outcome.EffectivePermissions);
        Assert.Contains("mode.tug", outcome.EffectivePermissions);
        Assert.Contains("departure.decide", outcome.EffectivePermissions);
    }

    [Fact]
    public void Scope_is_required_and_vessel_grants_are_distinct()
    {
        Assert.Equal(["required"], MembershipGrantPolicy.Validate(Request(["read-only"], read: false), All(), Known).Errors["scope"]);
        var vessel = Guid.NewGuid();
        Assert.True(MembershipGrantPolicy.Validate(Request(["read-only"], read: false, vessels: [new(vessel, false)]), All(), Known).Valid);
        Assert.Equal(["duplicate"], MembershipGrantPolicy.Validate(Request(["read-only"], vessels: [new(vessel, false), new(vessel, true)]), All(), Known).Errors["vesselGrants"]);
        Assert.Equal(["invalid"], MembershipGrantPolicy.Validate(Request(["read-only"], vessels: [new(Guid.Empty, false)]), All(), Known).Errors["vesselGrants"]);
    }

    [Fact]
    public void Administrator_predicate_requires_manage_scope_permission_and_bundle()
    {
        var admin = MembershipGrantPolicy.Validate(Request(["tenant-admin"], manage: true), All(), Known);
        Assert.True(admin.Valid);
        Assert.True(admin.Administrator);
        Assert.False(MembershipGrantPolicy.Validate(Request(["tenant-admin"], manage: false), All(), Known).Administrator);
        Assert.False(MembershipGrantPolicy.Validate(Request(["read-only"], manage: true), All(), Known).Administrator);
        Assert.False(MembershipGrantPolicy.IsAdministrator(["membership.manage", "directory.read"], true, 0));
    }

    [Fact]
    public void Own_membership_is_never_self_editable()
    {
        Assert.True(MembershipGrantPolicy.IsSelf(Admin, Admin));
        Assert.False(MembershipGrantPolicy.IsSelf(Admin, Guid.NewGuid()));
    }
}
