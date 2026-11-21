using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// USER-ADMIN-01 / UA-AC-01: every bundle permission survives the server trusted allow-list (no silently dropped grant),
// and the public allow-list keeps TrustedPermissions semantics (unknown values dropped, input order kept).
[Trait("Category", "Unit")]
[Trait("Requirement", "USER-ADMIN-01")]
public sealed class MembershipRoleBundleTests
{
    [Fact]
    public void Every_bundle_is_a_subset_of_the_trusted_permissions()
    {
        foreach (var bundle in MembershipRoleBundles.All)
            Assert.Equal(bundle.Permissions, ActorResolver.TrustedPermissions(bundle.Permissions));
    }

    [Fact]
    public void Known_permissions_are_the_trusted_allow_list()
    {
        Assert.Equal(ActorResolver.KnownPermissions.Length, ActorResolver.KnownPermissions.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ActorResolver.KnownPermissions, ActorResolver.TrustedPermissions(ActorResolver.KnownPermissions));
        Assert.Equal(["membership.manage", "directory.read"], ActorResolver.TrustedPermissions(["membership.manage", "root.all", "directory.read", "membership.manage", null!]));
        Assert.Contains("membership.security.suspend", ActorResolver.KnownPermissions);
    }

    [Fact]
    public void Bundle_role_ids_form_a_trusted_role_context()
    {
        var refs = MembershipRoleBundles.All.Select(b => b.RoleId.ToString("D")).ToArray();
        Assert.NotNull(ActorResolver.TrustedRoleContext(refs));
        var grant = new ProvisionedGrant(1, ["read-only"], [], ["directory.read"], true, false, null, []);
        Assert.Equal(MembershipRoleBundles.Find("read-only")!.RoleId.ToString("D"), grant.RoleContext);
        Assert.Null(new ProvisionedGrant(1, [], [], ["directory.read"], true, false, null, []).RoleContext);
    }

    [Theory]
    [InlineData("https://idp.example/realms/poseidon", null, true)]
    [InlineData("http://idp.example", null, false)]
    [InlineData("https://idp.example/realms/poseidon", "https://idp.example/realms/other", false)]
    [InlineData("", null, false)]
    public void Provisioned_issuer_must_be_https_and_the_trusted_issuer(string issuer, string? trusted, bool valid) =>
        Assert.Equal(valid, MembershipEndpoints.ValidIssuer(issuer, new FederationSettings { Issuer = trusted }));

    [Theory]
    [InlineData("f81d4fae-7dec-11d0-a765-00a0c91e6bf6", true)]
    [InlineData(" padded", false)]
    [InlineData("tab\tsubject", false)]
    [InlineData("", false)]
    public void Provisioned_subject_is_bounded_and_control_free(string subject, bool valid) =>
        Assert.Equal(valid, MembershipEndpoints.ValidSubject(subject));
}
