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
}
