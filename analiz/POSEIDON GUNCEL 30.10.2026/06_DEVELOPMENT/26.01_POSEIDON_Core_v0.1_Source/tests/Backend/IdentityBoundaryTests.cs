using System.Security.Claims;
using Xunit;
namespace Poseidon.Tests;
public class IdentityBoundaryTests
{
    static readonly ExternalIdentity Identity = new("https://issuer.invalid", "subject");
    static readonly Guid Tenant = Guid.NewGuid(), Company = Guid.NewGuid(), Vessel = Guid.NewGuid();
    static IdentityGrant Grant => new(Identity, Tenant, Company, [new(Tenant, Company, Vessel)], true);
    [Theory][InlineData("missing")][InlineData("identity")][InlineData("tenant")][InlineData("company")][InlineData("vessel-tenant")][InlineData("vessel-company")][InlineData("vessel-empty")][InlineData("duplicate")]
    public void Missing_or_inconsistent_server_mapping_is_rejected(string scenario)
    {
        var grant = scenario switch {
            "missing" => null, "identity" => Grant with { Identity = Identity with { Subject = "other" } },
            "tenant" => Grant with { TenantId = Guid.Empty }, "company" => Grant with { CompanyId = Guid.Empty },
            "vessel-tenant" => Grant with { Vessels = [new(Guid.NewGuid(), Company, Vessel)] },
            "vessel-company" => Grant with { Vessels = [new(Tenant, Guid.NewGuid(), Vessel)] },
            "vessel-empty" => Grant with { Vessels = [new(Tenant, Company, Guid.Empty)] },
            _ => Grant with { Vessels = [new(Tenant, Company, Vessel), new(Tenant, Company, Vessel)] }
        };
        Assert.Throws<InvalidOperationException>(() => IdentityBoundary.Map(Identity, grant));
    }
    [Fact] public void Identity_is_issuer_plus_subject_and_server_mapping_controls_scope()
    {
        var principal = IdentityBoundary.Map(Identity, Grant);
        var scope = Contracts.Scope(principal);
        Assert.Equal(Tenant, scope.TenantId); Assert.Equal(Company, scope.CompanyId); Assert.Equal(new[]{Vessel}, scope.VesselIds);
        Assert.NotEqual(scope.Subject, IdentityBoundary.ActorKey(Identity with { Issuer = "https://other.invalid" }));
        Assert.Equal(IdentityBoundary.ActorKey(Identity), scope.Subject);
    }
    [Theory][InlineData("https://evil.invalid")][InlineData("//evil.invalid")][InlineData("/\\evil.invalid")][InlineData("/%2f%2fevil.invalid")][InlineData("/\r\nevil")][InlineData("/bff/logout")][InlineData("")]
    public void Unsafe_returns_rejected(string target) => Assert.False(IdentityBoundary.IsLocalReturnUrl(target));
    [Theory][InlineData("/")][InlineData("/vessels?tab=all")]
    public void Local_returns_accepted(string target) => Assert.True(IdentityBoundary.IsLocalReturnUrl(target));
    [Fact] public void Ambiguous_identity_and_unauthenticated_claims_rejected()
    {
        var claims = new[] { new Claim("iss", Identity.Issuer), new Claim("sub", Identity.Subject) };
        Assert.Throws<InvalidOperationException>(() => IdentityBoundary.Read(new(new ClaimsIdentity(claims)), Identity.Issuer));
        Assert.Throws<InvalidOperationException>(() => IdentityBoundary.Read(new(new ClaimsIdentity(claims.Concat([new Claim("sub", "other")]), "test")), Identity.Issuer));
    }
    [Theory][InlineData("http://issuer.invalid")][InlineData("")][InlineData("https://user:password@issuer.invalid")][InlineData("https://issuer.invalid?x=1")]
    public void Unsafe_authorities_rejected(string authority) => Assert.Throws<InvalidOperationException>(() => IdentityBoundary.RequireHttps(authority));
    [Theory][InlineData("tenant_id", "bad")][InlineData("vessel_id", "bad")][InlineData("vessel_id", "00000000-0000-0000-0000-000000000000")]
    public void Malformed_or_duplicate_scope_is_rejected(string type, string value)
    {
        var principal = IdentityBoundary.Map(Identity, Grant);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new(type, value));
        Assert.Throws<ApiFault>(() => Contracts.Scope(principal));
    }
}
