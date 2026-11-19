using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-031 / T-S3-006 (18.01 Backlog row:32) and DEV-032 / T-S3-007 (row:33). Pure policy;
// no database. Synthetic identifiers exercise software only, never a real human authority.
[Trait("Requirement", "DEV-031")]
public sealed class HumanDecisionAuthorityPolicyTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), ActorId = Guid.NewGuid(), Role = Guid.NewGuid(), Vessel = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static HumanAuthority Grant(string role = "DPA", string capability = "human.decide") =>
        new(Guid.NewGuid(), Vessel, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Role, capability, Now.AddDays(-1), Now.AddDays(1),
            new("FICTIONAL_TYPE", "Control", Guid.NewGuid(), role, "HumanRecording", "15.01/paragraph:99;18.01/Backlog/row:32"));
    private static Membership Member(string kind, params HumanAuthority[] grants) =>
        new("https://issuer.invalid", "subject", ActorId, Tenant, ["directory.read", "human.decide"]) { ActorKind = kind, HumanAuthorities = grants };
    private static TrustedActor Actor(bool tenantAdmin = false, string[]? permissions = null) =>
        new(ActorId, Tenant, permissions ?? ["directory.read", "directory.write", "human.review.read", "human.decide"], tenantAdmin, tenantAdmin, []) { RoleContext = Role.ToString("D") };

    [Theory]
    [InlineData("Human", "DPA", VesselKind.Ship)]
    [InlineData("Human", "Master", VesselKind.Ship)]
    [InlineData("Human", "Tug Master", VesselKind.Tug)]
    [InlineData("Human", "DPA", VesselKind.Tug)]
    public void Exactly_appointed_human_role_may_record_a_final_decision(string kind, string role, VesselKind vessel)
    {
        var grant = Grant(role);
        Assert.Null(HumanDecisionAuthority.FinalDecisionDenial([Member(kind, grant)], Actor(), grant, vessel, "RECORDED", Now));
    }

    [Theory]
    [InlineData("AI")]
    [InlineData("Automation")]
    [InlineData("Service")]
    [InlineData("Unspecified")]
    [InlineData("human")]
    [InlineData("")]
    public void AI_automation_and_service_principals_never_create_a_final_decision_even_with_an_appointment(string kind)
    {
        var grant = Grant();
        foreach (var state in new[] { "RECORDED", "REJECTED" })
            Assert.Equal("non_human_principal", HumanDecisionAuthority.FinalDecisionDenial([Member(kind, grant)], Actor(tenantAdmin: true), grant, VesselKind.Ship, state, Now));
    }

    [Fact]
    public void Generic_tenant_admin_without_the_exact_appointment_is_denied()
    {
        var admin = Actor(tenantAdmin: true);
        Assert.True(admin.CanManage);
        // Tenant-wide manage + human.decide permission, but no provisioned appointment.
        Assert.Equal("human_authority_required", HumanDecisionAuthority.FinalDecisionDenial([Member("Human")], admin, null, VesselKind.Ship, "RECORDED", Now));
        // A forged/derived grant that is not one of the membership's own appointments.
        Assert.Equal("human_authority_required", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", Grant())], admin, Grant(), VesselKind.Ship, "RECORDED", Now));
        // An "Admin" recording role is not a decision authority role.
        var adminRole = Grant("Admin");
        Assert.Equal("decision_role_required", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", adminRole)], admin, adminRole, VesselKind.Ship, "RECORDED", Now));
        // A submit-only appointment never finalizes.
        var submit = Grant(capability: "human.review.submit");
        Assert.Equal("decision_capability_required", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", submit)], admin, submit, VesselKind.Ship, "REJECTED", Now));
    }

    [Fact]
    public void Role_context_permission_validity_mode_and_membership_are_all_required()
    {
        var grant = Grant("Master");
        Assert.Equal("decision_role_mode_mismatch", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", grant)], Actor(), grant, VesselKind.Tug, "RECORDED", Now));
        var tug = Grant("Tug Master");
        Assert.Equal("decision_role_mode_mismatch", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", tug)], Actor(), tug, VesselKind.Ship, "RECORDED", Now));
        Assert.Equal("decision_capability_required", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", grant)], Actor(permissions: ["directory.read"]), grant, VesselKind.Ship, "RECORDED", Now));
        Assert.Equal("decision_role_context_required", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", grant)], Actor() with { RoleContext = Guid.NewGuid().ToString("D") }, grant, VesselKind.Ship, "RECORDED", Now));
        Assert.Equal("human_authority_required", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", grant)], Actor(), grant, VesselKind.Ship, "RECORDED", Now.AddDays(2)));
        Assert.Equal("membership_unresolved", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", grant), Member("Human", grant)], Actor(), grant, VesselKind.Ship, "RECORDED", Now));
        Assert.Equal("membership_unresolved", HumanDecisionAuthority.FinalDecisionDenial([], Actor(), grant, VesselKind.Ship, "RECORDED", Now));
        var unresolved = grant with { Recording = null };
        Assert.Equal("decision_role_required", HumanDecisionAuthority.FinalDecisionDenial([Member("Human", unresolved)], Actor(), unresolved, VesselKind.Ship, "RECORDED", Now));
        Assert.True(HumanDecisionAuthority.IsFinal("Decision", "human.decide"));
        Assert.False(HumanDecisionAuthority.IsFinal("Verification", "human.verify"));
    }

    [Fact, Trait("Requirement", "DEV-032")]
    public void Audit_ready_menu_and_chain_readers_require_human_trace_reader_appointment()
    {
        var evidence = Guid.NewGuid(); var control = Guid.NewGuid();
        var auditor = new TrustedActor(ActorId, Tenant, ["directory.read", "audit.read", "human.trace.read"], true, false, []) { RoleContext = Role.ToString("D") };
        Assert.Contains(WorkspaceEndpoints.Destinations(auditor), d => d.Key == "audit-ready" && d.Href == "/app/audit-ready");
        Assert.Contains(WorkspaceLensPolicy.Available(auditor, Vessel, VesselKind.Ship), d => d.Key == "audit-ready" && d.Href == $"/app/audit-ready/{Vessel}" && d.Group == "review");
        Assert.DoesNotContain(WorkspaceEndpoints.Destinations(auditor with { Permissions = ["directory.read", "audit.read"] }), d => d.Key == "audit-ready");
        Assert.DoesNotContain(WorkspaceEndpoints.Destinations(auditor with { Permissions = ["directory.read", "human.trace.read"] }), d => d.Key == "audit-ready");
        Assert.DoesNotContain(WorkspaceEndpoints.Destinations(auditor with { RoleContext = null }), d => d.Key == "audit-ready");
        // A tenant admin without the trace capability does not see it either.
        Assert.DoesNotContain(WorkspaceEndpoints.Destinations(new TrustedActor(ActorId, Tenant, ["directory.read", "directory.write", "audit.read"], true, true, []) { RoleContext = Role.ToString("D") }), d => d.Key == "audit-ready");

        HumanTraceReader Reader(Guid? vessel = null, DateTimeOffset? until = null, Guid? role = null) =>
            new(Guid.NewGuid(), vessel ?? Vessel, evidence, control, role ?? Role, ["Verification", "Decision"], [], Now.AddDays(-1), until ?? Now.AddDays(1));
        Membership With(string kind, params HumanTraceReader[] readers) =>
            new("https://issuer.invalid", "subject", ActorId, Tenant, auditor.Permissions) { ActorKind = kind, HumanTraceReaders = readers };
        var valid = Reader();
        Assert.Equal([valid], AuditReadyEndpoints.Readers([With("Human", valid, Reader(vessel: Guid.NewGuid()), Reader(until: Now.AddMinutes(-1)), Reader(role: Guid.NewGuid()))], auditor, Vessel, Now));
        Assert.Empty(AuditReadyEndpoints.Readers([With("AI", valid)], auditor, Vessel, Now));
        Assert.Empty(AuditReadyEndpoints.Readers([With("Service", valid)], auditor, Vessel, Now));
        Assert.Empty(AuditReadyEndpoints.Readers([With("Human", valid)], auditor with { Permissions = ["directory.read", "audit.read"] }, Vessel, Now));
        var duplicate = valid with { };
        Assert.Empty(AuditReadyEndpoints.Readers([With("Human", valid, duplicate)], auditor, Vessel, Now));
    }
}
