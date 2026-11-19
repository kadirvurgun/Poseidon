using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-099 / T-OPS2-003: engine log WO/defect links pin exact owning revisions.
public sealed class DailyEngineReferencePolicyTests
{
    private static DailyEngineReferencePin Pin(string kind = "WorkOrder", long revision = 1, uint version = 7, Guid? id = null) =>
        new(kind, id ?? Guid.Parse("40000000-0000-4000-8000-000000000001"), revision, version, new string('a', 64),
            DailyEngineReferencePolicy.OwningSource(kind) ?? "");

    [Theory]
    [InlineData("WorkOrder")] [InlineData("Defect")]
    public void Owning_kinds_with_exact_source_contract_are_valid(string kind) => Assert.True(DailyEngineReferencePolicy.Valid(Pin(kind)));

    [Fact]
    public void Free_text_unknown_kind_foreign_source_or_unpinned_versions_are_rejected()
    {
        Assert.False(DailyEngineReferencePolicy.Valid(null));
        Assert.False(DailyEngineReferencePolicy.Valid(Pin() with { Kind = "Engine" }));
        Assert.False(DailyEngineReferencePolicy.Valid(Pin() with { SourceVersion = DefectPolicy.SourceVersion }));
        Assert.False(DailyEngineReferencePolicy.Valid(Pin() with { Revision = 0 }));
        Assert.False(DailyEngineReferencePolicy.Valid(Pin() with { Version = 0 }));
        Assert.False(DailyEngineReferencePolicy.Valid(Pin() with { Hash = "not-a-hash" }));
        Assert.False(DailyEngineReferencePolicy.Valid(Pin() with { Id = Guid.Empty }));
    }

    [Fact]
    public void Latest_event_per_target_decides_the_active_set()
    {
        var a = Pin(); var b = Pin("Defect", id: Guid.Parse("40000000-0000-4000-8000-000000000002"));
        var active = DailyEngineReferencePolicy.Active([("LINK", a, 1), ("LINK", b, 2), ("UNLINK", a, 3)]);
        Assert.Equal(b, Assert.Single(active).Pin);
        var relinked = DailyEngineReferencePolicy.Active([("LINK", a, 1), ("UNLINK", a, 2), ("LINK", a with { Revision = 2, Version = 9 }, 3)]);
        Assert.Equal(2, Assert.Single(relinked).Pin.Revision);
    }

    [Fact]
    public void Transitions_reject_duplicates_downgrades_and_unlinking_an_unlinked_or_different_pin()
    {
        var a = Pin(); var active = DailyEngineReferencePolicy.Active([("LINK", a, 1)]);
        Assert.Null(DailyEngineReferencePolicy.Transition([], "LINK", a));
        Assert.Equal("invalid_transition", DailyEngineReferencePolicy.Transition(active, "LINK", a));
        Assert.Null(DailyEngineReferencePolicy.Transition(active, "LINK", a with { Revision = 2, Version = 8 }));
        Assert.Equal("linked_version_conflict", DailyEngineReferencePolicy.Transition(
            DailyEngineReferencePolicy.Active([("LINK", a with { Revision = 3 }, 1)]), "LINK", a));
        Assert.Null(DailyEngineReferencePolicy.Transition(active, "UNLINK", a));
        Assert.Equal("invalid_transition", DailyEngineReferencePolicy.Transition([], "UNLINK", a));
        Assert.Equal("invalid_transition", DailyEngineReferencePolicy.Transition(active, "UNLINK", a with { Version = 99 }));
        Assert.Equal("invalid_request", DailyEngineReferencePolicy.Transition(active, "APPROVE", a));
    }

    [Fact]
    public void Active_link_count_is_bounded()
    {
        var full = Enumerable.Range(1, DailyEngineReferencePolicy.MaxActiveLinks)
            .Select(i => ("LINK", Pin(id: Guid.NewGuid()), (long)i)).ToArray();
        Assert.Equal("invalid_request", DailyEngineReferencePolicy.Transition(DailyEngineReferencePolicy.Active(full), "LINK", Pin(id: Guid.NewGuid())));
    }

    [Fact]
    public void Chief_engineer_review_of_engine_log_needs_the_exact_engine_duty()
    {
        Assert.True(DailyReviewPolicy.Duty("Engine", "ChiefEngineer"));
        foreach (var duty in new[] { "Master", "ChiefOfficer", "Administrator", "AI", "" })
            Assert.False(DailyReviewPolicy.Duty("Engine", duty));
    }
}
