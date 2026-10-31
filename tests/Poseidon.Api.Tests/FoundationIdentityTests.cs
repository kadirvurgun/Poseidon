using System.Security.Claims;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class FoundationIdentityTests
{
    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/application")]
    [InlineData("/app\\evil")]
    [InlineData("/app\r\nLocation: evil")]
    [InlineData(null)]
    public void Unsafe_return_urls_fall_back_to_product(string? url) => Assert.Equal("/app", FoundationIdentity.LocalReturn(url));

    [Theory]
    [InlineData("/app")]
    [InlineData("/app/fleet")]
    [InlineData("/app?tab=fleet")]
    public void Local_product_returns_are_allowed(string url) => Assert.Equal(url, FoundationIdentity.LocalReturn(url));

    [Fact]
    public void Only_unique_authenticated_server_membership_supplies_actor_tenant_and_permissions()
    {
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid();
        var settings = new FederationSettings { Memberships = [new("https://idp.example", "synthetic-sub", actor, tenant, ["directory.read"])] };
        var resolver = new ActorResolver(settings);
        var claims = new[] { new Claim("sub", "synthetic-sub"), new Claim("iss", "https://idp.example"), new Claim("tenant", Guid.NewGuid().ToString()), new Claim("role", "admin") };
        Assert.Null(resolver.Resolve(new ClaimsPrincipal(new ClaimsIdentity(claims))));
        var identity = new ClaimsIdentity(claims, "test");
        var result = resolver.Resolve(new(identity));
        Assert.Equal(tenant, result!.TenantId);
        Assert.Equal(actor, result.Id);
        Assert.Equal(["directory.read"], result.Permissions);
        identity.AddClaim(new Claim("sub", "ambiguous"));
        Assert.Null(resolver.Resolve(new(identity)));
        settings.Memberships = [settings.Memberships[0], settings.Memberships[0]];
        Assert.Null(resolver.Resolve(new(new ClaimsIdentity(claims, "test"))));
    }

    [Fact]
    public void Scope_is_explicit_and_revalidated_without_trusting_claims()
    {
        var vessel = Guid.NewGuid();
        var membership = new Membership("https://idp.example", "synthetic", Guid.NewGuid(), Guid.NewGuid(), ["directory.read", "directory.write"]);
        var settings = new FederationSettings { Memberships = [membership] };
        var resolver = new ActorResolver(settings);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "synthetic"), new Claim("iss", membership.Issuer), new Claim("role", "admin"), new Claim("vessel", vessel.ToString())], "test"));
        Assert.False(resolver.Resolve(principal)!.CanRead);
        settings.Memberships = [membership with { VesselGrants = [new(vessel, true)] }];
        var restricted = resolver.Resolve(principal)!;
        Assert.True(restricted.CanRead); Assert.False(restricted.CanManage);
        Assert.True(restricted.CanEditVessel(vessel)); Assert.False(restricted.CanEditVessel(Guid.NewGuid()));
        settings.Memberships = [membership with { TenantWideRead = true }];
        Assert.False(resolver.Resolve(principal)!.CanManage);
        Assert.False(resolver.Resolve(principal)!.CanEditVessel(vessel));
        settings.Memberships = [membership with { TenantWideManage = true }];
        Assert.True(resolver.Resolve(principal)!.CanManage);
        settings.Memberships = [membership with { VesselGrants = [new(vessel, false)] }];
        Assert.False(resolver.Resolve(principal)!.CanEditVessel(vessel));
    }
}
