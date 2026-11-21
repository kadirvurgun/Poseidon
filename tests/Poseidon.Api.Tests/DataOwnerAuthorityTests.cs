using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// S11 WP-A DEV-070 / T-S11-002 (18.01 Backlog row 71 "owner sign-off"; 18.03 p24, p102). Unit level (no host, no
// database): only an exactly appointed, current, human data owner of this vessel holds a sign-off authority; a platform
// or membership administrator never does. The HTTP 403 paths are covered by ImportReconciliationApiTests (PostgreSQL, CI).
[Trait("Category", "Unit")]
public sealed class DataOwnerAuthorityTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid(), Role = Guid.NewGuid(), Grant = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] Permissions = ["directory.read", "import.reconciliation.sign"];

    private static DataOwnerAuthority Appointment(string duty = "DataOwner", Guid? vessel = null, Guid? role = null, DateTimeOffset? from = null, DateTimeOffset? until = null,
        string source = "FICTIONAL synthetic data owner appointment; not operational") =>
        new(Grant, vessel ?? Vessel, role ?? Role, duty, source, from ?? Now.AddDays(-1), until ?? Now.AddDays(30));

    private static (FederationSettings Settings, TrustedActor Actor) Context(string[]? permissions = null, bool manage = false, string? roleContext = "role",
        DataOwnerAuthority[]? grants = null, params string?[] kinds)
    {
        var actor = new TrustedActor(Guid.NewGuid(), Tenant, permissions ?? Permissions, true, manage, [new(Vessel, false)])
            { RoleContext = roleContext == "role" ? Role.ToString("D") : roleContext };
        var settings = new FederationSettings {
            Memberships = (kinds.Length == 0 ? new string?[] { "Human" } : kinds).Select(k => new Membership("https://idp.invalid", "fictional-" + Guid.NewGuid().ToString("N"), actor.Id, Tenant, actor.Permissions)
                { ActorKind = k ?? "Unspecified", DataOwnerAuthorities = grants ?? [Appointment()] }).ToArray()
        };
        return (settings, actor);
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void An_exactly_appointed_current_human_data_owner_holds_one_authority() {
        var (settings, actor) = Context();
        var grant = Assert.Single(DataOwnerAuthorities.Resolve(settings, actor, Vessel, Now));
        Assert.Equal((Grant, "DataOwner"), (grant.Id, grant.Duty));
        Assert.True(ImportReconciliationEndpoints.CanRead(actor));
        Assert.False(ImportReconciliationEndpoints.CanCreate(actor, Vessel)); // signing confers no report authoring
    }

    public static TheoryData<string?[]> NonHuman => new() { new[] { "AI" }, new[] { "Automation" }, new[] { "Service" }, new[] { "Unspecified" }, new[] { "human" },
        new string?[] { null }, new[] { "Human", "Human" } };

    [Theory]
    [MemberData(nameof(NonHuman))]
    [Trait("Requirement", "DEV-070")]
    public void Ai_automation_service_or_ambiguous_principals_never_sign(string?[] kinds) {
        var (settings, actor) = Context(kinds: kinds);
        Assert.Empty(DataOwnerAuthorities.Resolve(settings, actor, Vessel, Now));
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void Platform_and_membership_administrators_cannot_sign() {
        var (unappointed, admin) = Context(permissions: [.. Permissions, "directory.write", "import.read", "import.write"], manage: true, grants: []);
        Assert.Empty(DataOwnerAuthorities.Resolve(unappointed, admin, Vessel, Now));
        // Even a (mis)configured appointment does not let a membership administrator sign data acceptance.
        var (appointed, membershipAdmin) = Context(permissions: [.. Permissions, "membership.manage"]);
        Assert.Empty(DataOwnerAuthorities.Resolve(appointed, membershipAdmin, Vessel, Now));
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void Wrong_duty_vessel_role_period_source_permission_or_missing_role_context_is_refused() {
        foreach (var grant in new[] { Appointment(duty: "Master"), Appointment(duty: "dataowner"), Appointment(vessel: Guid.NewGuid()), Appointment(role: Guid.NewGuid()),
            Appointment(from: Now.AddDays(1)), Appointment(until: Now), Appointment(source: ""), Appointment(source: " padded "), Appointment(source: new string('x', 201)) }) {
            var (settings, actor) = Context(grants: [grant]);
            Assert.Empty(DataOwnerAuthorities.Resolve(settings, actor, Vessel, Now));
        }
        var (s1, noPermission) = Context(permissions: ["directory.read"]);
        Assert.Empty(DataOwnerAuthorities.Resolve(s1, noPermission, Vessel, Now));
        var (s2, noRole) = Context(roleContext: null);
        Assert.Empty(DataOwnerAuthorities.Resolve(s2, noRole, Vessel, Now));
        var (s3, duplicated) = Context(grants: [Appointment(), Appointment()]);
        Assert.Empty(DataOwnerAuthorities.Resolve(s3, duplicated, Vessel, Now));
        Assert.Empty(DataOwnerAuthorities.Resolve(s1, null, Vessel, Now));
    }

    // DEV-069 "domain validation bypass yok": the import and reconciliation paths never use the set-based or raw SQL
    // APIs that skip the audited SaveChanges path and the owning domain handlers.
    [Fact]
    [Trait("Requirement", "DEV-069")]
    public void Import_commit_and_reconciliation_sources_never_bypass_the_audited_save_path() {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        foreach (var file in new[] { "ImportEndpoints.cs", "ImportReconciliationEndpoints.cs", "ImportTemplates.cs" }) {
            var text = File.ReadAllText(Path.Combine(root!.FullName, "backend", "Poseidon.Api", file));
            foreach (var forbidden in new[] { "ExecuteUpdate", "ExecuteDelete", "FromSql", "ExecuteSql", "IgnoreQueryFilters", "SqlQuery" })
                Assert.False(text.Contains(forbidden, StringComparison.Ordinal), $"{file} uses {forbidden}");
        }
        var commit = File.ReadAllText(Path.Combine(root!.FullName, "backend", "Poseidon.Api", "ImportEndpoints.cs"));
        // Each category commits through its ordinary owning handler, never a direct entity write.
        foreach (var handler in new[] { "DirectoryCommands.ApplyImport(", "TechnicalEndpoints.ApplyImport(", "CredentialEndpoints.Apply(", "CrewEndpoints.ApplyImport(" })
            Assert.Contains(handler, commit, StringComparison.Ordinal);
        Assert.DoesNotContain("db.Assets.Add(", commit, StringComparison.Ordinal);
        Assert.DoesNotContain("db.Credentials.Add(", commit, StringComparison.Ordinal);
        Assert.DoesNotContain("db.CrewAssignments.Add(", commit, StringComparison.Ordinal);
    }
}
