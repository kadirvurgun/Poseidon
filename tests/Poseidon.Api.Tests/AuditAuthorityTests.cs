using System.Security.Claims;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class AuditAuthorityTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Audit_requires_operator_permission_and_real_scope_regardless_of_IdP_role_claims(bool permission, bool scope, bool expected)
    {
        var membership = new Membership("https://synthetic.example", "synthetic", Guid.NewGuid(), Guid.NewGuid(),
            permission ? ["directory.read", "directory.write", "audit.read"] : ["directory.read", "directory.write"], TenantWideManage: scope);
        var resolver = new ActorResolver(new() { Memberships = [membership] });
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new("iss", membership.Issuer), new("sub", membership.Subject),
            new(ClaimTypes.Role, "audit.read"), new("permissions", "audit.read")
        ], "synthetic"));
        var actor = resolver.Resolve(principal)!;
        Assert.Equal(expected, actor.CanReadAudit);
        Assert.Equal(permission, actor.Permissions.Contains("audit.read"));
        Assert.DoesNotContain("external-admin", actor.AuditCapabilities);
    }

    [Fact]
    public void Vessel_auditor_has_no_catalog_read_or_tenant_management_authority()
    {
        var actor = new TrustedActor(Guid.NewGuid(), Guid.NewGuid(), ["directory.read", "audit.read"], false, false, [new(Guid.NewGuid())]);
        Assert.True(actor.CanReadAudit); Assert.False(actor.CanReadRegulatory); Assert.False(actor.CanManage);
    }
}
