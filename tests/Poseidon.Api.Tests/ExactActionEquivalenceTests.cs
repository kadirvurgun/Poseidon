using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5402 P5402-AC-09: every migrated exact action is EQUAL to the legacy predicate it
// replaces across the full synthetic membership space — never wider, never silently narrower.
[Trait("Requirement", "PLAN-1511-5402")]
public sealed class ExactActionEquivalenceTests
{
    private static readonly Guid Tenant = Guid.Parse("00000054-0002-0000-0000-0000000000a1");
    private static readonly Guid Vessel = Guid.Parse("00000054-0002-0000-0000-0000000000a2");
    private static readonly Guid Other = Guid.Parse("00000054-0002-0000-0000-0000000000a3");
    private static readonly string[] Keys = ["directory.read", "directory.write", "regulatory.draft.read", "regulatory.draft.write", "notification.manage"];

    private static IEnumerable<TrustedActor> Space()
    {
        VesselGrant[][] grants = [[], [new(Vessel, false)], [new(Vessel, true)], [new(Other, true)], [new(Other, false), new(Vessel, true)]];
        for (var mask = 0; mask < 1 << Keys.Length; mask++)
            foreach (var wideRead in new[] { false, true })
            foreach (var wideManage in new[] { false, true })
            foreach (var g in grants)
            foreach (var role in new[] { null, "00000054-0002-0000-0000-0000000000a4" })
                yield return new TrustedActor(Guid.Parse("00000054-0002-0000-0000-0000000000a5"), Tenant,
                    Keys.Where((_, i) => (mask & 1 << i) != 0).ToArray(), wideRead, wideManage, g) { RoleContext = role };
    }

    [Fact]
    public void Vessel_actions_equal_CanEditVessel_for_every_membership()
    {
        var count = 0;
        foreach (var actor in Space())
        foreach (var vessel in new[] { Vessel, Other })
        foreach (var key in new[] { ExactActionCatalog.VesselRegulatoryProfileDraftSave, ExactActionCatalog.VesselDirectoryUpdate })
        {
            var purpose = ExactActionCatalog.Actions[key].Purposes[0];
            Assert.Equal(actor.CanEditVessel(vessel), ExactActionAccess.Evaluate(actor, new(key, purpose, Tenant, vessel)).Allowed);
            count++;
        }
        Assert.Equal(1280 * 4, count);
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
