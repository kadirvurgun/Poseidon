using System.Text.Json;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// S11 WP-C DEV-073 / DEV-093 (18.01 Backlog rows 74 and 94; 19.01 p124-129). Unit level (no host, no database): only an
// exactly appointed, current, human SME reviewer for the campaign scope holds a review authority; a platform administrator,
// AI or automation never does. The candidate catalog is the DEV-008 plan (contracts/sources/pilot-ownership.v1.json) with
// UNVERIFIED preserved. The HTTP paths are covered by RegulatoryReviewApiTests (PostgreSQL, CI).
[Trait("Category", "Unit")]
public sealed class RegulatoryReviewAuthorityTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Role = Guid.NewGuid(), Grant = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] Permissions = ["directory.read", "regulatory.review.read", "regulatory.review.verdict"];

    private static RegulatoryReviewerAuthority Appointment(string[]? scopes = null, string state = "VERIFIED", DateTimeOffset? until = null, Guid? role = null) =>
        new(Grant, role ?? Role, scopes ?? ["SHIP", "TUG"], ["STATUTORY", "FLAG", "CLASS"], "FICTIONAL-SME-QUALIFICATION-001", state, Now.AddDays(-1), until ?? Now.AddDays(30));

    private static (FederationSettings Settings, TrustedActor Actor) Context(string[]? permissions = null, bool manage = false, RegulatoryReviewerAuthority[]? grants = null,
        params string?[] kinds) {
        var actor = new TrustedActor(Guid.NewGuid(), Tenant, permissions ?? Permissions, true, manage, []) { RoleContext = Role.ToString("D") };
        var settings = new FederationSettings {
            Memberships = (kinds.Length == 0 ? new string?[] { "Human" } : kinds).Select(k => new Membership("https://idp.invalid", "fictional-" + Guid.NewGuid().ToString("N"), actor.Id, Tenant, actor.Permissions)
                { ActorKind = k ?? "Unspecified", RegulatoryReviewerAuthorities = grants ?? [Appointment()] }).ToArray()
        };
        return (settings, actor);
    }

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public void An_appointed_current_human_reviewer_holds_one_authority_per_scope() {
        var (settings, actor) = Context();
        var grant = Assert.Single(RegulatoryReviewAccess.Reviewers(settings, actor, "SHIP", Now));
        Assert.True(RegulatoryReviewAccess.Verified(grant));
        Assert.Single(RegulatoryReviewAccess.Reviewers(settings, actor, "TUG", Now));
        var (shipOnly, a2) = Context(grants: [Appointment(["SHIP"])]);
        Assert.Empty(RegulatoryReviewAccess.Reviewers(shipOnly, a2, "TUG", Now));
    }

    public static TheoryData<string?[]> NonHuman => new() { new[] { "AI" }, new[] { "Automation" }, new[] { "Service" }, new[] { "Unspecified" }, new[] { "human" },
        new string?[] { null }, new[] { "Human", "Human" } };

    [Theory]
    [MemberData(nameof(NonHuman))]
    [Trait("Requirement", "DEV-073")]
    public void Ai_automation_or_ambiguous_principals_never_review_even_with_an_appointment(string?[] kinds) {
        var (settings, actor) = Context(kinds: kinds);
        Assert.Empty(RegulatoryReviewAccess.Reviewers(settings, actor, "SHIP", Now));
    }

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public void A_platform_administrator_without_an_appointment_or_permission_never_reviews() {
        string[] admin = ["directory.read", "directory.write", "membership.manage", "regulatory.review.read", "regulatory.review.manage", "regulatory.draft.write"];
        var (settings, actor) = Context(admin, manage: true, grants: []);
        Assert.Empty(RegulatoryReviewAccess.Reviewers(settings, actor, "SHIP", Now));
        var (withGrant, a2) = Context(admin, manage: true);
        Assert.Empty(RegulatoryReviewAccess.Reviewers(withGrant, a2, "SHIP", Now)); // permission is necessary
        Assert.False(RegulatoryReviewAccess.CanReview(a2));
    }

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public void Expired_foreign_role_or_malformed_appointments_fail_closed_and_unverified_is_kept_unverified() {
        var (s0, a0) = Context(grants: [Appointment(until: Now)]);
        Assert.Empty(RegulatoryReviewAccess.Reviewers(s0, a0, "SHIP", Now));
        var (s1, a1) = Context(grants: [Appointment(role: Guid.NewGuid())]);
        Assert.Empty(RegulatoryReviewAccess.Reviewers(s1, a1, "SHIP", Now));
        var (s2, a2) = Context(grants: [Appointment(["SHIP", "PLATFORM"])]);
        Assert.Empty(RegulatoryReviewAccess.Reviewers(s2, a2, "SHIP", Now));
        var (s3, a3) = Context(grants: [Appointment(), Appointment()]);
        Assert.Empty(RegulatoryReviewAccess.Reviewers(s3, a3, "SHIP", Now));
        var (s4, a4) = Context(grants: [Appointment(state: "UNVERIFIED")]);
        Assert.False(RegulatoryReviewAccess.Verified(Assert.Single(RegulatoryReviewAccess.Reviewers(s4, a4, "SHIP", Now))));
    }

    [Fact]
    [Trait("Requirement", "DEV-093")]
    public void The_candidate_catalog_is_the_dev008_plan_with_unverified_and_activation_blocked_preserved() {
        using var plan = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pilot-ownership.v1.json")));
        var root = plan.RootElement;
        Assert.Equal("AUTHORED_UNVERIFIED", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("activationBlocked").GetBoolean());
        var expected = root.GetProperty("scopes").EnumerateArray().SelectMany(s => {
            Assert.Equal("UNVERIFIED", s.GetProperty("validation").GetProperty("reviewerQualification").GetString());
            return s.GetProperty("candidateSources").EnumerateArray().Select(c => (Scope: s.GetProperty("scope").GetString()!,
                Code: c.GetProperty("sourceId").GetProperty("value").GetString()!, Issuer: c.GetProperty("issuer").GetProperty("value").GetString()!,
                Instrument: c.GetProperty("instrument").GetProperty("value").GetString()!, Locator: c.GetProperty("sourceId").GetProperty("locator").GetString()!));
        }).ToArray();
        Assert.Equal(expected.Select(e => (e.Scope, e.Code, e.Issuer, e.Instrument, e.Locator)),
            RegulatoryReviewPolicy.Candidates.Select(c => (c.PlanScope, c.Code, c.Issuer, c.Instrument, c.Locator)));
        Assert.All(RegulatoryReviewPolicy.Candidates, c => Assert.Equal(c.PlanScope switch { "Flag" => "FLAG", "Class" => "CLASS", _ => "STATUTORY" }, c.Authority));
    }
}
