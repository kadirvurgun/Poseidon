using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// PLAN-1511-5402 / 54.02 Rev4 A02-004/A02-006/A03-002/A03-007/A03-008. Synthetic identities only.
[Trait("Requirement", "PLAN-1511-5402")]
public sealed class ExactActionAuthorizationTests
{
    private static readonly Guid Tenant = Guid.Parse("00000054-0002-0000-0000-000000000001");
    private static readonly Guid Actor = Guid.Parse("00000054-0002-0000-0000-000000000002");
    private static readonly Guid Vessel = Guid.Parse("00000054-0002-0000-0000-000000000003");
    private static readonly Guid OtherVessel = Guid.Parse("00000054-0002-0000-0000-000000000004");
    private const string Role = "00000054-0002-0000-0000-000000000005";

    private static AccessSubject Subject(string[]? permissions = null, bool wideRead = false, bool wideManage = false,
        AccessGrant[]? grants = null, string? role = Role, Guid? tenant = null) =>
        new(Actor, tenant ?? Tenant, null, permissions ?? ["directory.read", "directory.write", "regulatory.draft.read", "regulatory.draft.write"],
            wideRead, wideManage, grants ?? [new(Vessel, true)], role);

    private static ExactAccessRequest ProfileSave(Guid? vessel = null, string purpose = ExactActionCatalog.PurposeRegulatoryProfileDraft, Guid? tenant = null) =>
        new(ExactActionCatalog.VesselRegulatoryProfileDraftSave, purpose, tenant ?? Tenant, vessel ?? Vessel);

    // P5402-AC-01
    [Fact]
    public void Vessel_edit_grant_allows_exact_profile_save_on_that_vessel_only()
    {
        var allowed = ExactActionResolver.Evaluate(Subject(), ProfileSave());
        Assert.True(allowed.Allowed); Assert.Equal(ExactAccessReasons.Allow, allowed.ReasonCode); Assert.NotNull(allowed.Epoch);
        var other = ExactActionResolver.Evaluate(Subject(), ProfileSave(OtherVessel));
        Assert.False(other.Allowed); Assert.Equal(ExactAccessReasons.NoGrant, other.ReasonCode);
    }

    // P5402-AC-02
    [Fact]
    public void Unknown_wildcard_or_unregistered_action_is_not_enabled()
    {
        foreach (var key in new[] { "*", "vessel.*", "directory.write", "regulatory.source.approve", "vessel.regulatory_profile.draft.save.extra" })
        {
            var decision = ExactActionResolver.Evaluate(Subject(wideManage: true), new(key, ExactActionCatalog.PurposeRegulatoryProfileDraft, Tenant, Vessel));
            Assert.False(decision.Allowed); Assert.Equal(ExactAccessReasons.ActionNotEnabled, decision.ReasonCode);
        }
    }

    // P5402-AC-03
    [Theory]
    [InlineData("")]
    [InlineData("vessel.directory.maintenance")]
    [InlineData("regulatory.register.draft")]
    [InlineData("VESSEL.REGULATORY_PROFILE.DRAFT")]
    [InlineData("export")]
    public void Out_of_scope_purpose_is_denied_even_with_full_grant(string purpose)
    {
        var decision = ExactActionResolver.Evaluate(Subject(wideRead: true, wideManage: true), ProfileSave(purpose: purpose));
        Assert.False(decision.Allowed); Assert.Equal(ExactAccessReasons.PurposeNotPermitted, decision.ReasonCode);
    }

    // P5402-AC-04
    [Fact]
    public void Tenant_and_scope_kind_mismatch_are_denied()
    {
        Assert.Equal(ExactAccessReasons.ScopeMismatch, ExactActionResolver.Evaluate(Subject(wideManage: true), ProfileSave(tenant: Guid.NewGuid())).ReasonCode);
        Assert.Equal(ExactAccessReasons.ScopeMismatch, ExactActionResolver.Evaluate(Subject(wideManage: true),
            new(ExactActionCatalog.VesselRegulatoryProfileDraftSave, ExactActionCatalog.PurposeRegulatoryProfileDraft, Tenant, null)).ReasonCode);
        Assert.Equal(ExactAccessReasons.ScopeMismatch, ExactActionResolver.Evaluate(Subject(wideRead: true),
            new(ExactActionCatalog.RegulatorySourceCreate, ExactActionCatalog.PurposeRegisterDraft, Tenant, Vessel)).ReasonCode);
        Assert.Equal(ExactAccessReasons.MembershipInactive, ExactActionResolver.Evaluate(null, ProfileSave()).ReasonCode);
        Assert.Equal(ExactAccessReasons.MembershipInactive, ExactActionResolver.Evaluate(Subject() with { ActorId = Guid.Empty }, ProfileSave()).ReasonCode);
    }

    // P5402-AC-05
    [Fact]
    public void Write_attribution_is_required_and_read_does_not_produce_write()
    {
        Assert.Equal(ExactAccessReasons.WriteAttributionRequired, ExactActionResolver.Evaluate(Subject(role: null), ProfileSave()).ReasonCode);
        Assert.Equal(ExactAccessReasons.NoGrant, ExactActionResolver.Evaluate(Subject(["directory.read"], wideManage: true), ProfileSave()).ReasonCode);
        Assert.Equal(ExactAccessReasons.NoGrant, ExactActionResolver.Evaluate(Subject(grants: [new(Vessel, false)]), ProfileSave()).ReasonCode);
        // Regulatory register requires tenant-wide read; vessel-only grants confer no catalog authority.
        var register = new ExactAccessRequest(ExactActionCatalog.RegulatorySourceEdit, ExactActionCatalog.PurposeRegisterDraft, Tenant, null);
        Assert.Equal(ExactAccessReasons.NoGrant, ExactActionResolver.Evaluate(Subject(), register).ReasonCode);
        Assert.True(ExactActionResolver.Evaluate(Subject(wideRead: true), register).Allowed);
        // directory.write never produces regulatory register authority.
        Assert.False(ExactActionResolver.Evaluate(Subject(["directory.read", "directory.write"], wideRead: true, wideManage: true), register).Allowed);
    }

    // P5402-AC-06: legacy broad write/manage never derives a checker action.
    [Fact]
    public void Legacy_mapping_never_derives_checker_or_release_actions()
    {
        foreach (var action in ExactActionCatalog.Actions.Keys)
            Assert.DoesNotContain(action.Split('.'), segment => ExactActionCatalog.CheckerSegments.Contains(segment));
        foreach (var row in ExactActionCatalog.Compatibility)
        {
            Assert.All(row.ExactActionKeys, key => Assert.True(ExactActionCatalog.Actions.ContainsKey(key)));
            if (row.Status == "MIGRATION_REVIEW_REQUIRED") Assert.Empty(row.ExactActionKeys);
            Assert.DoesNotContain("*", row.LegacyPermission);
        }
        Assert.All(ExactActionCatalog.Actions.Values, a => Assert.Matches("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*){1,7}$", a.Key));
    }

    // P5402-AC-07: commit-time revalidation.
    [Fact]
    public void Revocation_between_check_and_commit_denies_and_reports_epoch_change()
    {
        var prior = ExactActionResolver.Evaluate(Subject(), ProfileSave());
        var revoked = ExactActionResolver.Revalidate(prior, Subject(grants: [new(Vessel, false)]), ProfileSave(), Actor);
        Assert.False(revoked.Allowed); Assert.True(revoked.EpochChanged); Assert.Equal(ExactAccessReasons.NoGrant, revoked.ReasonCode);
        var gone = ExactActionResolver.Revalidate(prior, null, ProfileSave(), Actor);
        Assert.False(gone.Allowed); Assert.Equal(ExactAccessReasons.AccessContextChanged, gone.ReasonCode);
        var rebound = ExactActionResolver.Revalidate(prior, Subject() with { ActorId = Guid.NewGuid() }, ProfileSave(), Actor);
        Assert.False(rebound.Allowed); Assert.Equal(ExactAccessReasons.AccessContextChanged, rebound.ReasonCode);
    }

    [Fact]
    public void Unrelated_epoch_change_is_recomputed_not_blindly_rejected_and_denied_prior_stays_denied()
    {
        var prior = ExactActionResolver.Evaluate(Subject(), ProfileSave());
        var unchanged = ExactActionResolver.Revalidate(prior, Subject(), ProfileSave(), Actor);
        Assert.True(unchanged.Allowed); Assert.False(unchanged.EpochChanged);
        var widened = ExactActionResolver.Revalidate(prior, Subject(["directory.read", "directory.write", "workspace.search"]), ProfileSave(), Actor);
        Assert.True(widened.Allowed); Assert.True(widened.EpochChanged);
        var denied = ExactActionResolver.Evaluate(Subject(role: null), ProfileSave());
        Assert.False(ExactActionResolver.Revalidate(denied, Subject(), ProfileSave(), Actor).Allowed);
    }

    [Fact]
    public void Epoch_is_order_insensitive_and_changes_with_every_authority_input()
    {
        var a = Subject(["directory.write", "directory.read"]).Epoch();
        Assert.Equal(a, Subject(["directory.read", "directory.write"]).Epoch());
        Assert.NotEqual(a, Subject(["directory.read", "directory.write"], wideRead: true).Epoch());
        Assert.NotEqual(a, Subject(["directory.read", "directory.write"], grants: [new(Vessel, false)]).Epoch());
        Assert.NotEqual(a, Subject(["directory.read", "directory.write"], role: null).Epoch());
        Assert.NotEqual(a, (Subject(["directory.read", "directory.write"]) with { ActiveCompanyId = Guid.NewGuid() }).Epoch());
    }

    // P5402-AC-08: dynamic SoD on the same record.
    [Fact]
    public void Separation_of_duties_rejects_self_approval_and_unknown_makers()
    {
        var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        Assert.Null(SeparationOfDuties.Evaluate(checker, maker, Guid.NewGuid()));
        Assert.Equal(ExactAccessReasons.SodViolation, SeparationOfDuties.Evaluate(maker, maker));
        Assert.Equal(ExactAccessReasons.SodViolation, SeparationOfDuties.Evaluate(maker, Guid.NewGuid(), maker));
        Assert.Equal(ExactAccessReasons.SodMakerUnknown, SeparationOfDuties.Evaluate(checker, maker, null));
        Assert.Equal(ExactAccessReasons.SodMakerUnknown, SeparationOfDuties.Evaluate(checker, Guid.Empty));
        Assert.Equal(ExactAccessReasons.SodMakerUnknown, SeparationOfDuties.Evaluate(checker));
        Assert.Equal(ExactAccessReasons.SodMakerUnknown, SeparationOfDuties.Evaluate(Guid.Empty, maker));
        Assert.False(SeparationOfDuties.Independent(maker, maker)); Assert.True(SeparationOfDuties.Independent(checker, maker));
    }
}
