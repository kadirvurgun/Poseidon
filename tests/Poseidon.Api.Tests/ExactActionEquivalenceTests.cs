using System.Security.Claims;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5402 P5402-AC-09: every migrated exact action is EQUAL to the legacy predicate it
// replaces across the full synthetic membership space — never wider, never silently narrower.
// The only intentional narrowing is a Guid.Empty vessel request (SCOPE_MISMATCH), asserted explicitly.
[Trait("Requirement", "PLAN-1511-5402")]
public sealed class ExactActionEquivalenceTests
{
    private static readonly Guid Tenant = Guid.Parse("00000054-0002-0000-0000-0000000000a1");
    private static readonly Guid Vessel = Guid.Parse("00000054-0002-0000-0000-0000000000a2");
    private static readonly Guid Other = Guid.Parse("00000054-0002-0000-0000-0000000000a3");
    private static readonly string[] Keys = ["directory.read", "directory.write", "regulatory.draft.read", "regulatory.draft.write", "notification.manage"];

    private static readonly Guid ActorId = Guid.Parse("00000054-0002-0000-0000-0000000000a5");
    private const string RoleA = "00000054-0002-0000-0000-0000000000a4";
    private const string RoleB = "00000054-0002-0000-0000-0000000000a6";

    // Grant sets include Guid.Empty grants and duplicate/conflicting grants for the same vessel.
    private static readonly VesselGrant[][] Grants =
    [
        [], [new(Vessel, false)], [new(Vessel, true)], [new(Other, true)], [new(Other, false), new(Vessel, true)],
        [new(Guid.Empty, true)], [new(Guid.Empty, true), new(Vessel, false)],
        [new(Vessel, false), new(Vessel, true)], [new(Vessel, true), new(Vessel, false)], [new(Vessel, true), new(Vessel, true)],
    ];

    private static IEnumerable<(string[] Permissions, bool WideRead, bool WideManage, VesselGrant[] Grants)> Memberships()
    {
        for (var mask = 0; mask < 1 << Keys.Length; mask++)
            foreach (var wideRead in new[] { false, true })
            foreach (var wideManage in new[] { false, true })
            foreach (var g in Grants)
                yield return (Keys.Where((_, i) => (mask & 1 << i) != 0).ToArray(), wideRead, wideManage, g);
    }

    // Directly constructed subjects (multiple role-context values, incl. a combined multi-role context).
    private static IEnumerable<TrustedActor> Direct() =>
        from m in Memberships()
        from role in new[] { null, RoleA, RoleA + "," + RoleB, "" }
        select new TrustedActor(ActorId, Tenant, m.Permissions, m.WideRead, m.WideManage, m.Grants) { RoleContext = role };

    // Subjects built through the real ActorResolver from server-provisioned memberships
    // (filters Guid.Empty grants, normalizes/rejects role references).
    private static IEnumerable<TrustedActor> Resolved()
    {
        string[]?[] roleRefs = [null, [RoleA], [RoleA, RoleB], [RoleA, RoleA], ["not-a-guid"], [Guid.Empty.ToString("D")]];
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "synthetic-5402"), new Claim("iss", "https://exact-5402.example")], "synthetic"));
        foreach (var m in Memberships())
        foreach (var refs in roleRefs)
        {
            var membership = new Membership("https://exact-5402.example", "synthetic-5402", ActorId, Tenant, m.Permissions, m.WideRead, m.WideManage, m.Grants) { RoleReferences = refs };
            var actor = new ActorResolver(new FederationSettings { Memberships = [membership] }).Resolve(principal);
            Assert.NotNull(actor);
            yield return actor!;
        }
    }

    private static IEnumerable<TrustedActor> Space() => Direct().Concat(Resolved());

    [Fact]
    public void Vessel_actions_equal_CanEditVessel_for_every_membership()
    {
        var count = 0;
        foreach (var actor in Space())
        foreach (var key in new[] { ExactActionCatalog.VesselRegulatoryProfileDraftSave, ExactActionCatalog.VesselDirectoryUpdate })
        {
            var purpose = ExactActionCatalog.Actions[key].Purposes[0];
            foreach (var vessel in new[] { Vessel, Other })
            {
                Assert.Equal(actor.CanEditVessel(vessel), ExactActionAccess.Evaluate(actor, new(key, purpose, Tenant, vessel)).Allowed);
                count++;
            }
            // Guid.Empty vessel request: exact is never wider (always denies), even with a Guid.Empty grant.
            var empty = ExactActionAccess.Evaluate(actor, new(key, purpose, Tenant, Guid.Empty));
            // Legacy CanEditVessel(Guid.Empty) may be true for a directly built Guid.Empty grant; exact is narrower.
            Assert.False(empty.Allowed);
            Assert.Equal(ExactAccessReasons.ScopeMismatch, empty.ReasonCode);
        }
        // 32 permission masks x 2 x 2 x 10 grant sets x (4 direct roles + 6 resolver role refs) x 2 vessels x 2 actions.
        Assert.Equal(1280 * 10 * 4, count);
    }
    [Fact]
    public void Register_actions_equal_CanWriteRegulatory_for_every_membership()
    {
        foreach (var actor in Space())
        foreach (var key in new[] { ExactActionCatalog.RegulatorySourceCreate, ExactActionCatalog.RegulatorySourceEdit,
                     ExactActionCatalog.RegulatoryRequirementCreate, ExactActionCatalog.RegulatoryRequirementEdit })
            Assert.Equal(actor.CanWriteRegulatory,
                ExactActionAccess.Evaluate(actor, new(key, ExactActionCatalog.PurposeRegisterDraft, Tenant, null)).Allowed);
    }

    [Fact]
    public void Legacy_human_review_independence_is_preserved_or_narrowed_by_the_SoD_helper()
    {
        var checker = Guid.NewGuid(); var maker = Guid.NewGuid();
        Guid?[] values = [null, Guid.Empty, checker, maker];
        foreach (var submitter in new[] { checker, maker, Guid.Empty })
        foreach (var evidence in values)
        foreach (var control in values)
        {
            var legacy = submitter != checker && evidence != checker && control != checker && evidence != null && control != null;
            var exact = SeparationOfDuties.Independent(checker, submitter, evidence, control);
            Assert.True(!exact || legacy); // never wider
            if (submitter != Guid.Empty && evidence != Guid.Empty && control != Guid.Empty) Assert.Equal(legacy, exact);
        }
    }
}
