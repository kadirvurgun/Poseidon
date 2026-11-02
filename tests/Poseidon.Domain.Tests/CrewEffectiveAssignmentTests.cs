using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;
public sealed class CrewEffectiveAssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026,11,1,0,0,0,TimeSpan.Zero);
    private static CrewAssignmentObservation Row(string status = "SyntheticOnboard", DateOnly? to = null, string provenance = "UserDeclaredUnverified") => new(Guid.NewGuid(), Guid.NewGuid(), status, provenance, new(2026,10,1), to, Now);
    private static CrewEffectiveSelection Select(params CrewAssignmentObservation[] rows) => CrewEffectiveAssignment.Select(rows, new(2026,11,1), Now, ["SyntheticOnboard", "Draft"], ["UserDeclaredUnverified"], true);
    [Fact] public void Draft_is_never_current_even_if_misconfigured() => Assert.Equal("Missing", Select(Row("Draft")).State);
    [Fact] public void End_is_exclusive_and_future_source_observations_do_not_establish_current_period() {
        Assert.Equal("Missing", Select(Row(to: new(2026,11,1))).State);
        Assert.Equal("Missing", Select(Row() with { SourceObservedAt = Now.AddSeconds(1) }).State);
    }
    [Fact] public void Provenance_and_missing_periods_are_required() {
        Assert.Equal("Missing", Select(Row(provenance: "Unknown")).State);
        Assert.Equal("Missing", Select(Row() with { From = null }).State);
    }
    [Fact] public void Multiple_qualifying_assignments_are_ambiguous_without_arbitrary_selection() => Assert.Equal("Ambiguous", Select(Row(), Row()).State);
    [Fact] public void Current_selection_preserves_actual_scoped_reference() {
        var row = Row(); var selected = Select(row); Assert.Equal(row.Id, selected.AssignmentId); Assert.Equal(row.VesselId, selected.VesselId);
    }
}
