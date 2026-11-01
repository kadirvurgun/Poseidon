using System.Security.Claims;
using Poseidon.Api;
using Xunit;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;

namespace Poseidon.Api.Tests;

public sealed class FoundationIdentityTests
{
    [Fact]
    public void Malformed_server_membership_configuration_fails_startup_instead_of_silently_dropping_entries()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Authentication:Oidc:Memberships:0:ActorId"] = "malformed-guid" }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddFoundationIdentity(configuration,
            new HostingEnvironment { EnvironmentName = "Development" }));
    }
    [Fact]
    public void Json_membership_configuration_retains_manager_without_vessel_grants_and_restricted_reader()
    {
        const string issuer = "https://localhost:8443/realms/poseidon-lab";
        var members = new[]
        {
            new Membership(issuer, "manager", Guid.NewGuid(), Guid.NewGuid(), ["directory.read", "directory.write", "workspace.search"], true, true, []),
            new Membership(issuer, "restricted", Guid.NewGuid(), Guid.NewGuid(), ["directory.read"], false, false, [new(Guid.NewGuid())])
        };
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Authentication = new { Oidc = new { Authority = issuer, Issuer = issuer, ClientId = "local", Memberships = members } } })));
        var configuration = new ConfigurationBuilder().AddJsonStream(json).Build();
        var settings = configuration.GetSection("Authentication:Oidc").Get<FederationSettings>()!;
        Assert.Equal(2, settings.Memberships.Length);
        var resolver = new ActorResolver(settings);
        var manager = resolver.Resolve(new(new ClaimsIdentity([new("sub", "manager"), new("iss", issuer)], "oidc")));
        Assert.NotNull(manager); Assert.True(manager.CanManage); Assert.True(manager.CanSearch);
    }
    [Fact]
    public void Search_permission_requires_explicit_trusted_provisioning_and_directory_scope()
    {
        var membership = new Membership("https://synthetic.example", "synthetic", Guid.NewGuid(), Guid.NewGuid(), ["directory.read"], TenantWideRead: true);
        var settings = new FederationSettings { Memberships = [membership] };
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new("sub", membership.Subject), new("iss", membership.Issuer), new("role", "workspace.search"), new("permission", "workspace.search")], "test"));
        var resolver = new ActorResolver(settings);
        Assert.False(resolver.Resolve(principal)!.CanSearch);
        settings.Memberships = [membership with { Permissions = ["directory.read", "workspace.search", "invented.authority"] }];
        Assert.True(resolver.Resolve(principal)!.CanSearch);
        Assert.DoesNotContain("invented.authority", resolver.Resolve(principal)!.Permissions);
        settings.Memberships = [membership with { Permissions = ["workspace.search"] }];
        Assert.False(resolver.Resolve(principal)!.CanSearch);
    }
    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/application")]
    [InlineData("/app\\evil")]
    [InlineData("/app/%2f%2fevil.example")]
    [InlineData("/app/%255cevil")]
    [InlineData("/app/../other")]
    [InlineData("/app/%")]
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
