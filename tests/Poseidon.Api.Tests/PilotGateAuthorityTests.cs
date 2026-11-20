using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// S11 WP-D DEV-075 (18.01 row 76; brief "Product Owner exact authority; AI/sistem karar veremez") and DEV-074/094
// (UAT outcomes are human observations). Unit level: only an exactly appointed, current, human Product Owner holds a
// go/no-go authority; AI/automation/service/ambiguous principals and administrators without an appointment never do.
[Trait("Category", "Unit")]
public sealed class PilotGateAuthorityTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Role = Guid.NewGuid(), Grant = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] Permissions = ["directory.read", "directory.write", "pilot.gate.read", "pilot.gate.decide", "uat.read", "uat.record"];

    private static PilotGateAuthority Appointment(string duty = "ProductOwner", Guid? role = null, DateTimeOffset? from = null, DateTimeOffset? until = null) =>
        new(Grant, role ?? Role, duty, "FICTIONAL synthetic appointment; not operational", from ?? Now.AddDays(-1), until ?? Now.AddDays(30));

    private static (FederationSettings Settings, TrustedActor Actor) Context(string[]? permissions = null, bool tenantRead = true, string? roleContext = null,
        PilotGateAuthority[]? grants = null, params string?[] kinds)
    {
        var actor = new TrustedActor(Guid.NewGuid(), Tenant, permissions ?? Permissions, tenantRead, false, []) { RoleContext = roleContext ?? Role.ToString("D") };
        var settings = new FederationSettings {
            Memberships = (kinds.Length == 0 ? new string?[] { "Human" } : kinds).Select(k => new Membership("https://idp.invalid", "fictional-" + Guid.NewGuid().ToString("N"), actor.Id, Tenant, actor.Permissions)
                { ActorKind = k ?? "Unspecified", PilotGateAuthorities = grants ?? [Appointment()] }).ToArray()
        };
        return (settings, actor);
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void An_exactly_appointed_current_human_product_owner_holds_one_authority()
    {
        var (settings, actor) = Context();
        var grant = Assert.Single(PilotAccess.Resolve(settings, actor, Now));
        Assert.Equal((Grant, "ProductOwner"), (grant.Id, grant.Duty));
    }

    public static TheoryData<string?[]> NonHuman => new() { new[] { "AI" }, new[] { "Automation" }, new[] { "Service" }, new[] { "Unspecified" }, new[] { "human" },
        new string?[] { null }, new[] { "Human", "Human" } };

    [Theory]
    [MemberData(nameof(NonHuman))]
    [Trait("Requirement", "DEV-075")]
    [Trait("Requirement", "DEV-074")]
    public void Ai_automation_service_or_ambiguous_principals_never_decide_or_record_uat_evidence(string?[] kinds)
    {
        var (settings, actor) = Context(kinds: kinds);
        Assert.Empty(PilotAccess.Resolve(settings, actor, Now));
        Assert.False(PilotAccess.Human(settings, actor));
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Wrong_duty_role_period_missing_permission_vessel_only_scope_or_duplicate_grant_is_refused()
    {
        foreach (var grant in new[] { Appointment(duty: "Admin"), Appointment(duty: "productowner"), Appointment(role: Guid.NewGuid()),
            Appointment(from: Now.AddDays(1)), Appointment(until: Now), Appointment(from: Now.AddDays(-2), until: Now.AddDays(-3)) }) {
            var (settings, actor) = Context(grants: [grant]);
            Assert.Empty(PilotAccess.Resolve(settings, actor, Now));
        }
        var (s1, noDecide) = Context(permissions: ["directory.read", "directory.write", "pilot.gate.read"]);
        Assert.Empty(PilotAccess.Resolve(s1, noDecide, Now));
        var (s2, vesselOnly) = Context(tenantRead: false);
        Assert.False(PilotAccess.GateRead(vesselOnly));
        Assert.Empty(PilotAccess.Resolve(s2, vesselOnly, Now));
        var (s3, duplicate) = Context(grants: [Appointment(), Appointment()]);
        Assert.Empty(PilotAccess.Resolve(s3, duplicate, Now));
        var (s4, otherRole) = Context(roleContext: Guid.NewGuid().ToString("D"));
        Assert.Empty(PilotAccess.Resolve(s4, otherRole, Now));
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public async Task Not_integrated_gate_owners_are_deferred_never_pass()
    {
        var gates = DeferredPilotGateSource.Defaults();
        Assert.Equal(new[] { "SECURITY", "REGULATORY", "OFFLINE", "DR", "DATA", "TRACE" }, gates.Select(g => g.Gate));
        foreach (var g in gates) {
            var input = await g.ReadAsync(null!, Now, TestContext.Current.CancellationToken);
            Assert.Equal(PilotGatePolicy.Deferred, input.State);
            Assert.Contains("NOT_INTEGRATED", Assert.Single(input.Reasons));
        }
    }

    [Fact]
    public void Uat_and_pilot_gate_permissions_survive_the_trusted_allowlist() =>
        Assert.Equal(new[] { "uat.read", "uat.record", "uat.threshold.record", "uat.attest", "pilot.gate.read", "pilot.gate.support.record", "pilot.gate.decide" },
            ActorResolver.TrustedPermissions(["uat.read", "uat.record", "uat.threshold.record", "uat.attest", "pilot.gate.read", "pilot.gate.support.record", "pilot.gate.decide", "pilot.gate.admin", "uat.approve"]));
}
