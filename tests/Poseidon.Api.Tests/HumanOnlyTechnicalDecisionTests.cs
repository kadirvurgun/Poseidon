using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// S8 QA B1: postponement decision (DEV-109), defect verification / effectiveness / closure (DEV-055) and
// service report verification (DEV-103) are human records. The capability is resolved from the trusted
// server-provisioned membership ActorKind (exact ordinal "Human"); AI, automation, service, unspecified
// and ambiguous memberships fail closed. Unit level (no database); the HTTP 403 human_actor_required
// path is covered by the PostgreSQL integration tests in PmsCoreTests / TechnicalRecordApiTests.
public sealed class HumanOnlyTechnicalDecisionTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid();
    private static readonly string[] Permissions = ["directory.read", "directory.write", "equipment.read", "maintenance.read", "maintenance.postponement.decide",
        "defect.read", "defect.write", "defect.verify", "defect.effectiveness", "defect.close", "service.report.read", "service.report.verify"];

    private static (HttpContext Http, TrustedActor Actor) Context(params string?[] kinds)
    {
        var actor = new TrustedActor(Guid.NewGuid(), Tenant, Permissions, true, true, []) { RoleContext = Guid.NewGuid().ToString("D") };
        var settings = new FederationSettings
        {
            Memberships = kinds.Select(k => new Membership("https://idp.invalid", "fictional-" + Guid.NewGuid().ToString("N"), actor.Id, Tenant, Permissions)
                { ActorKind = k ?? "Unspecified" }).ToArray()
        };
        var services = new ServiceCollection().AddSingleton(settings).BuildServiceProvider();
        return (new DefaultHttpContext { RequestServices = services }, actor);
    }

    public static TheoryData<string?[]> NonHuman => new() { new[] { "AI" }, new[] { "Unspecified" }, new[] { "Automation" }, new[] { "Service" }, new[] { "human" },
        new string?[] { null }, Array.Empty<string?>(), new[] { "Human", "Human" } };

    [Fact]
    [Trait("Requirement", "DEV-109")]
    [Trait("Requirement", "DEV-055")]
    [Trait("Requirement", "DEV-103")]
    public void A_human_membership_keeps_every_decision_capability()
    {
        var (h, a) = Context("Human");
        Assert.True(MaintenancePostponementEndpoints.CanDecide(h, a, Vessel));
        Assert.True(ServiceReportEndpoints.CanVerify(h, a, Vessel));
        foreach (var kind in new[] { "VERIFY", "EFFECTIVENESS", "CLOSE", "ACTION", "START" }) Assert.True(DefectLifecycleEndpoints.CanRecord(h, a, Vessel, kind), kind);
    }

    [Theory]
    [MemberData(nameof(NonHuman))]
    [Trait("Requirement", "DEV-109")]
    [Trait("Requirement", "DEV-055")]
    [Trait("Requirement", "DEV-103")]
    public void Non_human_or_ambiguous_principals_never_hold_an_approval_verification_or_closure_capability(string?[] kinds)
    {
        var (h, a) = Context(kinds);
        // The permission alone is present: only the human gate refuses.
        Assert.True(MaintenancePostponementEndpoints.CanDecide(a, Vessel));
        Assert.False(MaintenancePostponementEndpoints.CanDecide(h, a, Vessel));
        Assert.False(ServiceReportEndpoints.CanVerify(h, a, Vessel));
        foreach (var kind in new[] { "VERIFY", "EFFECTIVENESS", "CLOSE" })
        {
            Assert.True(DefectLifecycleEndpoints.HumanOnly(kind));
            Assert.False(DefectLifecycleEndpoints.CanRecord(h, a, Vessel, kind), kind);
        }
        // Drafting / corrective action stays an attributed non-decision record.
        Assert.False(DefectLifecycleEndpoints.HumanOnly("ACTION"));
        Assert.True(DefectLifecycleEndpoints.CanRecord(h, a, Vessel, "ACTION"));
    }
}
