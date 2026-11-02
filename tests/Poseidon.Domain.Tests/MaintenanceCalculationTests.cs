using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

public sealed class MaintenanceCalculationTests
{
    private static readonly DateTimeOffset Clock = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static MaintenanceInputs Calendar => new(TriggerType: "Calendar", IntervalValue: "1", IntervalUnit: "month",
        LastDoneDate: new(2026, 10, 1), TimeZone: "Europe/Istanbul", Provenance: "SyntheticDeclared");
    [Fact]
    public void Calendar_is_due_on_local_boundary_without_changing_reference_clock()
    {
        var before = MaintenanceCalculation.Evaluate(Calendar, Clock.AddHours(-3).AddTicks(-1));
        var boundary = MaintenanceCalculation.Evaluate(Calendar, Clock.AddHours(-3));
        Assert.Equal("NotDue", before.State); Assert.Equal("Due", boundary.State);
        Assert.Equal(new DateOnly(2026, 11, 1), boundary.NextDueDate);
        Assert.Equal(boundary, MaintenanceCalculation.Evaluate(Calendar, Clock.AddHours(-3)));
    }
    [Theory]
    [InlineData(2024, 1, 31, 2024, 2, 29)]
    [InlineData(2025, 1, 31, 2025, 2, 28)]
    public void Calendar_month_end_is_deterministic(int y, int m, int d, int ey, int em, int ed)
    {
        var due = MaintenanceCalculation.Evaluate(Calendar with { LastDoneDate = new(y, m, d) }, Clock);
        Assert.Equal(new DateOnly(ey, em, ed), due.NextDueDate);
    }
    [Theory]
    [InlineData(null)] [InlineData("0")] [InlineData("-1")] [InlineData("1.5")] [InlineData("1e3")]
    public void Missing_or_invalid_calendar_interval_never_becomes_zero_or_not_due(string? interval) =>
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(Calendar with { IntervalValue = interval }, Clock).State);
    [Fact]
    public void Exact_meter_decimal_and_unit_are_preserved()
    {
        var input = new MaintenanceInputs(TriggerType: "Meter", IntervalValue: "0.000000000000000001", IntervalUnit: "declared-hours",
            MeterSource: "synthetic-manual", LastDoneMeter: "0.100000000000000001", CurrentMeter: "0.100000000000000002",
            ObservedAt: Clock, ObservationRevision: "synthetic/v1", TimeZone: "UTC", Provenance: "DeclaredUnverified");
        var result = MaintenanceCalculation.Evaluate(input, Clock);
        Assert.Equal("Due", result.State); Assert.Equal("0.100000000000000002", result.NextDueMeter);
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(input with { CurrentMeter = null }, Clock).State);
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(input with { CurrentMeter = "0.01" }, Clock).State);
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(input with { ObservedAt = Clock.AddSeconds(-1) }, Clock).State);
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(input with { MeterSource = null }, Clock).State);
    }
    [Theory]
    [InlineData(null, "ReviewRequired")]
    [InlineData("Unknown", "ReviewRequired")]
    [InlineData("False", "NotDue")]
    [InlineData("True", "Due")]
    public void Condition_unknown_is_never_false(string? condition, string expected) => Assert.Equal(expected,
        MaintenanceCalculation.Evaluate(new(TriggerType: "Condition", Condition: condition, ObservedAt: Clock,
            ObservationRevision: "synthetic/v1", TimeZone: "UTC", Provenance: "DeclaredUnverified"), Clock).State);
    [Fact]
    public void Numeric_parser_rejects_precision_loss_culture_exponents_and_extremes()
    {
        foreach (var input in new[] { "1,5", "1e3", "-1", " 1", "1.", ".", "999999999999999999999999999999", "0.00000000000000000000000000001" })
            Assert.False(MaintenanceCalculation.Number(input, out _));
        Assert.True(MaintenanceCalculation.Number("0.0000000000000000000000000001", out var exact));
        Assert.Equal(0.0000000000000000000000000001m, exact);
    }
    [Fact]
    public void Unresolved_inputs_and_calendar_range_never_infer_operational_fitness()
    {
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(new(), Clock).State);
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(Calendar with { TimeZone = null }, Clock).State);
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(Calendar with { Provenance = null }, Clock).State);
        Assert.Equal("ReviewRequired", MaintenanceCalculation.Evaluate(Calendar with { IntervalValue = "2147483647" }, Clock).State);
    }
    [Fact]
    public void Dst_fallback_uses_explicit_local_date_and_never_machine_local_time()
    {
        var input = Calendar with { TimeZone = "America/New_York" };
        var midnight = new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero);
        Assert.Equal("NotDue", MaintenanceCalculation.Evaluate(input, midnight.AddTicks(-1)).State);
        Assert.Equal("Due", MaintenanceCalculation.Evaluate(input, midnight).State);
        Assert.Equal("Due", MaintenanceCalculation.Evaluate(input, midnight.AddHours(2)).State);
    }
}
