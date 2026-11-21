using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// Pilot program kit: week close and CONTINUE/EXPAND/FIX/STOP belong only to an exactly appointed, current, human
// Product Owner holding pilot.program.decide (the DEV-075 Membership.PilotGateAuthorities appointment). Every
// operational record type a checklist item accepts has a server probe bound to a tenant entity.
[Trait("Category", "Unit")]
public sealed class PilotProgramAccessTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Role = Guid.NewGuid(), Grant = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 12, 8, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] Permissions = ["directory.read", "pilot.program.read", "pilot.program.record", "pilot.program.decide"];

    private static (FederationSettings, TrustedActor) Context(string[]? permissions = null, string kind = "Human", bool tenantRead = true, string duty = "ProductOwner")
    {
        var actor = new TrustedActor(Guid.NewGuid(), Tenant, permissions ?? Permissions, tenantRead, false, tenantRead ? [] : [new VesselGrant(Guid.NewGuid())]) { RoleContext = Role.ToString("D") };
        var settings = new FederationSettings {
            Memberships = [new Membership("https://idp.invalid", "fictional", actor.Id, Tenant, actor.Permissions) {
                ActorKind = kind, PilotGateAuthorities = [new PilotGateAuthority(Grant, Role, duty, "FICTIONAL synthetic appointment; not operational", Now.AddDays(-1), Now.AddDays(30))] }]
        };
        return (settings, actor);
    }

    [Fact]
    [Trait("Requirement", "DEV-079")]
    public void Only_an_appointed_human_product_owner_with_the_program_permission_decides()
    {
        var (settings, actor) = Context();
        Assert.Equal(Grant, Assert.Single(PilotProgramAccess.Resolve(settings, actor, Now)).Id);
        foreach (var (s, a) in new[] { Context(kind: "AI"), Context(kind: "Automation"), Context(duty: "Admin"), Context(tenantRead: false),
                     Context(permissions: ["directory.read", "pilot.program.read", "pilot.gate.decide"]) })
            Assert.Empty(PilotProgramAccess.Resolve(s, a, Now));
        Assert.Empty(PilotProgramAccess.Resolve(settings, actor, Now.AddDays(31)));
        Assert.False(PilotProgramAccess.Read(Context(permissions: ["directory.read"]).Item2));
        Assert.True(PilotProgramAccess.Record(actor));
        Assert.False(PilotProgramAccess.Triage(actor));
    }

    [Fact]
    [Trait("Requirement", "DEV-077")]
    [Trait("Requirement", "DEV-095")]
    public void Every_operational_record_type_has_a_tenant_entity_probe_and_program_owned_types_are_explicit()
    {
        var operational = PilotProgramCatalog.RecordTypes.Except(PilotProgramCatalog.ProgramOwnedRecordTypes).ToArray();
        Assert.Equal(operational.Order(StringComparer.Ordinal), PilotRecordProbes.OperationalTypes.Order(StringComparer.Ordinal));
        Assert.All(operational, t => Assert.True(typeof(ITenantEntity).IsAssignableFrom(PilotRecordProbes.EntityType(t))));
        Assert.Null(PilotRecordProbes.EntityType(PilotProgramCatalog.UatRun));
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    public void Feedback_retention_is_unconfigured_by_default_and_needs_a_policy_reference()
    {
        Assert.False(new PilotProgramSettings().Configured);
        Assert.False(new PilotProgramSettings { FeedbackRetentionDays = 30 }.Configured);
        Assert.False(new PilotProgramSettings { FeedbackRetentionDays = 0, RetentionPolicyRef = "RET-1" }.Configured);
        Assert.True(new PilotProgramSettings { FeedbackRetentionDays = 90, RetentionPolicyRef = "FICTIONAL-RET-POLICY/1" }.Configured);
    }
}
