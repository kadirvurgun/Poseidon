using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class MaintenancePlanningPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly MaintenancePlanningContext Context = new(true, true, true);
    private static MaintenanceInputs Calendar => new(TriggerType: "Calendar", IntervalValue: "1", IntervalUnit: "day",
        LastDoneDate: new(2026, 10, 31), TimeZone: "UTC", Provenance: "SyntheticUnverified", Criticality: "DeclaredCritical");

    [Fact]
    public void A_previously_not_due_plan_is_due_when_the_current_clock_crosses_its_boundary()
    {
        var historical = MaintenanceCalculation.Evaluate(Calendar, Now.AddDays(-2));
        var current = MaintenancePlanningPolicy.Evaluate(Calendar, Now, Context);
        Assert.Equal("NotDue", historical.State);
        Assert.Equal("Due", current.State);
        Assert.Equal(historical.NextDueDate, current.NextDueDate);
        Assert.Equal("planning-current-v1", current.Algorithm);
    }

    [Theory]
    [InlineData(false, true, true, "MaintenanceJobUnavailable")]
    [InlineData(true, false, true, "MaintenanceOwnerUnavailable")]
    [InlineData(true, true, false, "EquipmentRevisionChanged")]
    public void Current_context_changes_never_reuse_a_previously_resolved_result(bool job, bool owner, bool asset, string reason)
    {
        var current = MaintenancePlanningPolicy.Evaluate(Calendar, Now, new(job, owner, asset));
        Assert.Equal("ReviewRequired", current.State);
        Assert.Equal(reason, current.Reason);
        if (!asset) Assert.Null(current.NextDueDate);
    }

    [Theory]
    [InlineData("ObservationRevoked")]
    [InlineData("ObservationStale")]
    [InlineData("ObservationPolicyChanged")]
    public void Unavailable_observations_clear_previously_computed_meter_boundaries(string state)
    {
        var inputs = Calendar with { TriggerType = "Meter", ObservationId = Guid.NewGuid() };
        var current = MaintenancePlanningPolicy.Evaluate(inputs, Now, Context, new(state, "test-v1", inputs));
        Assert.Equal("ReviewRequired", current.State);
        Assert.Equal(state, current.Reason);
        Assert.Null(current.NextDueMeter);
    }

    [Fact]
    public void An_observation_reference_without_a_server_assessment_is_not_a_manual_reading()
    {
        var current = MaintenancePlanningPolicy.Evaluate(Calendar with { ObservationId = Guid.NewGuid() }, Now, Context);
        Assert.Equal("ObservationAssessmentUnavailable", current.Reason);
        Assert.Equal("ReviewRequired", current.State);
    }

    [Fact]
    public void Configured_observation_values_replace_client_echo_before_current_calculation()
    {
        var input = new MaintenanceInputs(TriggerType: "Meter", IntervalValue: "10", IntervalUnit: "hours", MeterSource: "synthetic",
            LastDoneMeter: "10", CurrentMeter: "999", ObservedAt: Now.AddMinutes(-1), ObservationRevision: "test-v1", TimeZone: "UTC",
            Provenance: "DeclaredUnverified", Criticality: "DeclaredCritical", ObservationId: Guid.NewGuid());
        var assessment = new MaintenanceObservationAssessment("ConfiguredDeclaredObservation", "test-v1", input with { CurrentMeter = "19.5" });
        var current = MaintenancePlanningPolicy.Evaluate(input, Now, Context, assessment);
        Assert.Equal("NotDue", current.State);
        Assert.Equal("20", current.NextDueMeter);
        Assert.Equal("planning-current-v1/test-v1", current.Algorithm);
    }

    [Fact]
    public void Unknown_criticality_and_missing_calendar_inputs_remain_unresolved()
    {
        Assert.Equal("ReviewRequired", MaintenancePlanningPolicy.Evaluate(Calendar with { Criticality = "Unknown" }, Now, Context).State);
        Assert.Equal("MissingCalendarInputs", MaintenancePlanningPolicy.Evaluate(Calendar with { LastDoneDate = null }, Now, Context).Reason);
    }
}
