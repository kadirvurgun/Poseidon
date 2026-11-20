using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// S9 WP-A DEV-059 / T-S9-002 (18.01 Backlog row 60: "Only authorized Master ... AI/admin cannot sign"; 60.01 Rev3
// paragraph 952). Unit level (no host, no database): only an exactly appointed, current, human Master of this ship
// holds a decision authority. The HTTP 403 path is covered by DepartureReadinessApiTests (PostgreSQL, CI).
[Trait("Category", "Unit")]
public sealed class DepartureAuthorityTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid(), Role = Guid.NewGuid(), Grant = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] Permissions = ["directory.read", "directory.write", "departure.readiness.read", "departure.decide"];

    private static DepartureAuthority Appointment(string duty = "Master", Guid? vessel = null, Guid? role = null, DateTimeOffset? from = null, DateTimeOffset? until = null) =>
        new(Grant, vessel ?? Vessel, role ?? Role, duty, "FICTIONAL synthetic appointment; not operational", from ?? Now.AddDays(-1), until ?? Now.AddDays(30));

    private static (FederationSettings Settings, TrustedActor Actor) Context(string[]? permissions = null, bool manage = false, string? roleContext = null,
        DepartureAuthority[]? grants = null, params string?[] kinds)
    {
        var actor = new TrustedActor(Guid.NewGuid(), Tenant, permissions ?? Permissions, true, manage, [new(Vessel, true)]) { RoleContext = roleContext ?? Role.ToString("D") };
        var settings = new FederationSettings {
            Memberships = (kinds.Length == 0 ? new string?[] { "Human" } : kinds).Select(k => new Membership("https://idp.invalid", "fictional-" + Guid.NewGuid().ToString("N"), actor.Id, Tenant, actor.Permissions)
                { ActorKind = k ?? "Unspecified", DepartureAuthorities = grants ?? [Appointment()] }).ToArray()
        };
        return (settings, actor);
    }

    [Fact]
    [Trait("Requirement", "DEV-059")]
    public void An_exactly_appointed_current_human_master_holds_one_authority() {
        var (settings, actor) = Context();
        var grant = Assert.Single(DepartureAuthorities.Resolve(settings, actor, Vessel, Now));
        Assert.Equal((Grant, "Master"), (grant.Id, grant.Duty));
        Assert.True(DepartureAuthorities.CanRead(actor));
    }

    public static TheoryData<string?[]> NonHuman => new() { new[] { "AI" }, new[] { "Automation" }, new[] { "Service" }, new[] { "Unspecified" }, new[] { "human" },
        new string?[] { null }, new[] { "Human", "Human" } };

    [Theory]
    [MemberData(nameof(NonHuman))]
    [Trait("Requirement", "DEV-059")]
    public void Ai_automation_service_or_ambiguous_principals_never_sign_even_with_an_appointment(string?[] kinds) {
        var (settings, actor) = Context(kinds: kinds);
        Assert.Empty(DepartureAuthorities.Resolve(settings, actor, Vessel, Now));
    }

    [Fact]
    [Trait("Requirement", "DEV-059")]
    public void A_tenant_wide_administrator_without_an_appointment_cannot_sign() {
        var (settings, actor) = Context(manage: true, grants: []);
        Assert.True(DepartureAuthorities.CanDecide(actor, Vessel)); // the permission alone is present
        Assert.Empty(DepartureAuthorities.Resolve(settings, actor, Vessel, Now));
    }

    [Fact]
    [Trait("Requirement", "DEV-059")]
    public void Wrong_duty_vessel_role_period_permission_or_missing_role_context_is_refused() {
        foreach (var grant in new[] { Appointment(duty: "ChiefEngineer"), Appointment(duty: "master"), Appointment(vessel: Guid.NewGuid()), Appointment(role: Guid.NewGuid()),
            Appointment(from: Now.AddDays(1)), Appointment(until: Now), Appointment(from: Now.AddDays(-2), until: Now.AddDays(-3)) }) {
            var (settings, actor) = Context(grants: [grant]);
            Assert.Empty(DepartureAuthorities.Resolve(settings, actor, Vessel, Now));
        }
        var (s1, noDecide) = Context(permissions: ["directory.read", "directory.write", "departure.readiness.read"]);
        Assert.Empty(DepartureAuthorities.Resolve(s1, noDecide, Vessel, Now));
        var (s2, noWrite) = Context(permissions: ["directory.read", "departure.readiness.read", "departure.decide"]);
        Assert.Empty(DepartureAuthorities.Resolve(s2, noWrite, Vessel, Now));
        var (s3, other) = Context(roleContext: Guid.NewGuid().ToString("D"));
        Assert.Empty(DepartureAuthorities.Resolve(s3, other, Vessel, Now));
        var (s4, duplicate) = Context(grants: [Appointment(), Appointment()]);
        Assert.Empty(DepartureAuthorities.Resolve(s4, duplicate, Vessel, Now));
    }

    [Fact]
    public void Departure_permissions_survive_the_trusted_allowlist() =>
        Assert.Equal(new[] { "departure.readiness.read", "departure.decide" }, ActorResolver.TrustedPermissions(["departure.readiness.read", "departure.decide", "departure.admin"]));
}
