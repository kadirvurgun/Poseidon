using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class SecureFileAuthorityTests
{
    [Fact]
    public void Download_capabilities_bind_actor_tenant_vessel_object_revision_and_expiry()
    {
        var service = new FileCapabilities(new EphemeralDataProtectionProvider());
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var vessel = Guid.NewGuid(); var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow; var token = service.Issue(tenant, actor, vessel, id, 1, now.AddSeconds(10));
        Assert.True(service.Valid(token, tenant, actor, vessel, id, 1, now));
        Assert.False(service.Valid(token, Guid.NewGuid(), actor, vessel, id, 1, now));
        Assert.False(service.Valid(token, tenant, Guid.NewGuid(), vessel, id, 1, now));
        Assert.False(service.Valid(token, tenant, actor, Guid.NewGuid(), id, 1, now));
        Assert.False(service.Valid(token, tenant, actor, vessel, Guid.NewGuid(), 1, now));
        Assert.False(service.Valid(token, tenant, actor, vessel, id, 2, now));
        Assert.False(service.Valid(token, tenant, actor, vessel, id, 1, now.AddSeconds(11)));
        Assert.False(service.Valid(token + "x", tenant, actor, vessel, id, 1, now));
    }

    [Fact]
    public void No_production_or_default_permissive_scanner_path_exists()
    {
        var settings = new SecureFileSettings(); Assert.False(settings.Ready(new Env("Development")));
        settings = new() { Adapter = "LocalDevelopment", Root = Path.GetTempPath(), MaxBytes = 1024, MaxPixels = 1024, ScanSeconds = 1, ClamdPort = 3310, CapabilitySeconds = 10, ScannerReference = "operator-scanner", RetentionReference = "operator-retention" };
        Assert.True(settings.Ready(new Env("Development"))); Assert.False(settings.Ready(new Env("Production")));
        settings.ScannerReference = null; Assert.False(settings.Ready(new Env("Development")));
    }

    [Fact]
    public void Roles_are_real_operator_references_never_permission_or_IdP_labels_and_re_resolve_on_revocation()
    {
        var role = Guid.NewGuid().ToString();
        var member = new Membership("https://synthetic.example", "synthetic", Guid.NewGuid(), Guid.NewGuid(), ["directory.read", "file.read"], TenantWideRead: true) { RoleReferences = [role] };
        var settings = new FederationSettings { Memberships = [member] }; var resolver = new ActorResolver(settings);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new("iss", member.Issuer), new("sub", member.Subject), new(ClaimTypes.Role, "Marine-admin"), new("roles", role)], "synthetic"));
        var snapshot = resolver.Resolve(principal)!; Assert.Equal(role, snapshot.RoleContext);
        settings.Memberships[0] = member with { RoleReferences = null, Permissions = ["directory.read"] };
        Assert.Null(resolver.Resolve(principal)!.RoleContext); Assert.DoesNotContain("file.read", resolver.Resolve(principal)!.Permissions);
        Assert.Equal(role, snapshot.RoleContext); Assert.DoesNotContain("Marine", snapshot.RoleContext!);
        foreach (var refs in new[] { new[] { "directory.write" }, new[] { role + "\n" }, new[] { role, role }, new[] { Guid.Empty.ToString() }, new string[] { null! } })
            Assert.Null(ActorResolver.TrustedRoleContext(refs));
        settings.Memberships = []; Assert.Null(resolver.Resolve(principal));
    }

    private sealed class Env(string name) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "synthetic";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "synthetic-content-root");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
