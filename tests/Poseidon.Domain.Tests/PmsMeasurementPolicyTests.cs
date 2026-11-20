using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-108 / T-PMS2-002: definition, reading admission, counter propagation, threshold band and trigger.
public sealed class PmsMeasurementPolicyTests
{
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Vessel = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Asset = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid PointId = Guid.Parse("40000000-0000-0000-0000-000000000004");
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static MeasurementPointDefinition Temp => new("ME-EXH-T1", PmsMeasurementPolicy.Temperature, "CEL", PmsMeasurementPolicy.Manual, 1, "-50", "700", null);
    private static MeasurementPointDefinition Hours => new("ME-RH", PmsMeasurementPolicy.RunningHours, "HOUR", PmsMeasurementPolicy.Manual, 1, null, null, null);
    private static MeasurementPolicyRule Rule(string? wmax = "450", string? amax = "500") =>
        new(Tenant, Vessel, "ME-EXH-T1", "synthetic-threshold-v1", "FICTIONAL OEM manual ref", 60, NormalMin: "200", NormalMax: "420", WarningMax: wmax, AlarmMax: amax);

    [Fact]
    public void Definitions_require_a_closed_kind_matching_unit_source_precision_and_ordered_range()
    {
        Assert.Null(PmsMeasurementPolicy.ValidateDefinition(Temp, PointId));
        Assert.Null(PmsMeasurementPolicy.ValidateDefinition(Hours with { ParentPointId = Guid.NewGuid() }, PointId));
        Assert.Equal("measurement_definition_invalid", PmsMeasurementPolicy.ValidateDefinition(Temp with { Unit = "BAR" }, PointId));
        Assert.Equal("measurement_definition_invalid", PmsMeasurementPolicy.ValidateDefinition(Temp with { Kind = "FLOW" }, PointId));
        Assert.Equal("measurement_definition_invalid", PmsMeasurementPolicy.ValidateDefinition(Temp with { Source = "PROPAGATED" }, PointId));
        Assert.Equal("measurement_definition_invalid", PmsMeasurementPolicy.ValidateDefinition(Temp with { Precision = 7 }, PointId));
        Assert.Equal("measurement_definition_invalid", PmsMeasurementPolicy.ValidateDefinition(Temp with { RangeMin = "700", RangeMax = "700" }, PointId));
        Assert.Equal("measurement_definition_invalid", PmsMeasurementPolicy.ValidateDefinition(Temp with { RangeMin = "1e3" }, PointId));
        // Only running-hours counters may be sub counters; never their own parent.
        Assert.Equal("measurement_definition_invalid", PmsMeasurementPolicy.ValidateDefinition(Temp with { ParentPointId = Guid.NewGuid() }, PointId));
        Assert.Equal("measurement_definition_invalid", PmsMeasurementPolicy.ValidateDefinition(Hours with { ParentPointId = PointId }, PointId));
    }

    [Theory]
    [InlineData("12.5", true)]
    [InlineData("-3.2", true)]
    [InlineData("0", true)]
    [InlineData("-0", false)]
    [InlineData("01", false)]
    [InlineData("1e3", false)]
    [InlineData("+1", false)]
    [InlineData("1,5", false)]
    [InlineData("1.", false)]
    [InlineData("1.25", false)]
    public void Reading_values_are_exact_lexical_decimals_within_the_declared_precision(string value, bool accepted) =>
        Assert.Equal(accepted, PmsMeasurementPolicy.Decimal(value, 1, out _));

    [Fact]
    public void Counter_readings_are_chronological_and_never_decrease_without_a_reviewed_reset()
    {
        var previous = new MeasurementLatest(Guid.NewGuid(), PointId, "1000.0", Now.AddHours(-2));
        Assert.Null(PmsMeasurementPolicy.ValidateReading(Hours, "1010.5", Now.AddHours(-1), Now, previous, out var parsed));
        Assert.Equal(1010.5m, parsed);
        Assert.Equal("counter_decreasing", PmsMeasurementPolicy.ValidateReading(Hours, "999.0", Now.AddHours(-1), Now, previous, out _));
        Assert.Equal("reading_out_of_order", PmsMeasurementPolicy.ValidateReading(Hours, "1020.0", Now.AddHours(-3), Now, previous, out _));
        Assert.Equal("reading_out_of_range", PmsMeasurementPolicy.ValidateReading(Hours, "-1.0", Now, Now, null, out _));
        Assert.Equal("invalid_request", PmsMeasurementPolicy.ValidateReading(Hours, "1.0", Now.AddMinutes(1), Now, null, out _));
        Assert.Equal("reading_out_of_range", PmsMeasurementPolicy.ValidateReading(Temp, "700.1", Now, Now, null, out _));
        Assert.Equal("reading_precision_unsupported", PmsMeasurementPolicy.ValidateReading(Temp, "450.25", Now, Now, null, out _));
    }

    [Fact]
    public void Parent_counter_delta_propagates_to_sub_counters_with_a_baseline_and_skips_unknown_baselines()
    {
        var parent = Guid.NewGuid(); var turbo = Guid.NewGuid(); var turboBearing = Guid.NewGuid(); var fresh = Guid.NewGuid(); var temp = Guid.NewGuid();
        var nodes = new MeasurementCounterNode[] {
            new(parent, null, PmsMeasurementPolicy.RunningHours, "1000", Now.AddDays(-1)),
            new(turbo, parent, PmsMeasurementPolicy.RunningHours, "250.5", Now.AddDays(-1)),
            new(turboBearing, turbo, PmsMeasurementPolicy.RunningHours, "40", Now.AddDays(-1)),
            new(fresh, parent, PmsMeasurementPolicy.RunningHours, null, null),
            new(temp, parent, PmsMeasurementPolicy.Temperature, "300", Now.AddDays(-1)),
        };
        var (derived, skipped) = PmsMeasurementPolicy.Propagate(parent, 12.5m, Now, nodes);
        Assert.Equal(new[] { (turbo, "263.0"), (turboBearing, "52.5") }.OrderBy(x => x.Item1),
            derived.Select(d => (d.PointId, d.Value)).OrderBy(x => x.Item1));
        Assert.All(derived, d => Assert.Equal("12.5", d.Delta));
        Assert.Equal(new[] { new MeasurementPropagationSkip(fresh, "SubCounterBaselineMissing") }, skipped);
        Assert.DoesNotContain(derived, d => d.PointId == temp);
        Assert.Empty(PmsMeasurementPolicy.Propagate(parent, 0m, Now, nodes).Derived);
    }

    [Theory]
    [InlineData("300.0", "NORMAL")]
    [InlineData("420.0", "NORMAL")]
    [InlineData("430.0", "CAUTION")]
    [InlineData("450.0", "WARNING")]
    [InlineData("499.9", "WARNING")]
    [InlineData("500.0", "ALARM")]
    [InlineData("150.0", "CAUTION")]
    public void Threshold_band_uses_inclusive_boundaries_and_the_heavier_result(string value, string band) =>
        Assert.Equal(band, PmsMeasurementPolicy.Band(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), Rule()));

    [Fact]
    public void Policy_entries_must_be_ordered_and_name_a_warning_or_alarm_side()
    {
        Assert.True(PmsMeasurementPolicy.ValidPolicy(Rule(), PmsMeasurementPolicy.Temperature, 1));
        Assert.False(PmsMeasurementPolicy.ValidPolicy(Rule(wmax: "550"), PmsMeasurementPolicy.Temperature, 1));
        Assert.False(PmsMeasurementPolicy.ValidPolicy(Rule(wmax: null, amax: null), PmsMeasurementPolicy.Temperature, 1));
        Assert.False(PmsMeasurementPolicy.ValidPolicy(Rule() with { FreshnessMinutes = 0 }, PmsMeasurementPolicy.Temperature, 1));
        Assert.False(PmsMeasurementPolicy.ValidPolicy(null, PmsMeasurementPolicy.Temperature, 1));
        // A running-hours counter has no band: a band on a counter is an invalid configuration.
        Assert.False(PmsMeasurementPolicy.ValidPolicy(Rule() with { PointCode = "ME-RH" }, PmsMeasurementPolicy.RunningHours, 1));
        Assert.True(PmsMeasurementPolicy.ValidPolicy(new(Tenant, Vessel, "ME-RH", "v1", "FICTIONAL", 1440), PmsMeasurementPolicy.RunningHours, 1));
    }

    private static MeasurementPointRef TempRef => new(Tenant, Vessel, Asset, PointId, "ME-EXH-T1", PmsMeasurementPolicy.Temperature, "CEL");
    private static MaintenanceInputs Condition(string? on = "WARNING") => new(TriggerType: "Condition", TimeZone: "UTC", Provenance: "SyntheticUnverified",
        Criticality: "DeclaredCritical", MeasurementPointId: PointId, TriggerOn: on);

    [Fact]
    public void Unconfigured_threshold_stale_missing_or_unspecified_trigger_is_review_required_never_not_due()
    {
        var reading = new MeasurementLatest(Guid.NewGuid(), PointId, "460.0", Now.AddMinutes(-5));
        Assert.Equal("MeasurementPolicyUnconfigured", PmsMeasurementPolicy.AssessTrigger(Condition(), TempRef, 1, reading, null, Tenant, Vessel, Asset, Now).State);
        Assert.Equal("MeasurementMissing", PmsMeasurementPolicy.AssessTrigger(Condition(), TempRef, 1, null, Rule(), Tenant, Vessel, Asset, Now).State);
        Assert.Equal("MeasurementStale", PmsMeasurementPolicy.AssessTrigger(Condition(), TempRef, 1, reading with { MeasuredAt = Now.AddHours(-2) }, Rule(), Tenant, Vessel, Asset, Now).State);
        Assert.Equal("ThresholdTriggerUnspecified", PmsMeasurementPolicy.AssessTrigger(Condition(null), TempRef, 1, reading, Rule(), Tenant, Vessel, Asset, Now).State);
        Assert.Equal("MeasurementPointScopeMismatch", PmsMeasurementPolicy.AssessTrigger(Condition(), TempRef, 1, reading, Rule(), Tenant, Guid.NewGuid(), Asset, Now).State);
        Assert.Equal("MeasurementKindMismatch", PmsMeasurementPolicy.AssessTrigger(Condition() with { TriggerType = "Meter" }, TempRef, 1, reading, Rule(), Tenant, Vessel, Asset, Now).State);
        var planned = MaintenancePlanningPolicy.Evaluate(Condition(), Now, new(true, true, true),
            PmsMeasurementPolicy.AssessTrigger(Condition(), TempRef, 1, reading, null, Tenant, Vessel, Asset, Now));
        Assert.Equal(("ReviewRequired", "MeasurementPolicyUnconfigured"), (planned.State, planned.Reason));
    }

    [Theory]
    [InlineData("460.0", "WARNING", "Due", "WARNING")]
    [InlineData("460.0", "ALARM", "NotDue", "WARNING")]
    [InlineData("505.0", "ALARM", "Due", "ALARM")]
    [InlineData("300.0", "WARNING", "NotDue", "NORMAL")]
    public void Configured_threshold_band_drives_the_condition_plan_deterministically(string value, string triggerOn, string state, string band)
    {
        var reading = new MeasurementLatest(Guid.Parse("50000000-0000-0000-0000-000000000005"), PointId, value, Now.AddMinutes(-5));
        var inputs = Condition(triggerOn);
        var first = MaintenancePlanningPolicy.Evaluate(inputs, Now, new(true, true, true), PmsMeasurementPolicy.AssessTrigger(inputs, TempRef, 1, reading, Rule(), Tenant, Vessel, Asset, Now));
        var second = MaintenancePlanningPolicy.Evaluate(inputs, Now, new(true, true, true), PmsMeasurementPolicy.AssessTrigger(inputs, TempRef, 1, reading, Rule(), Tenant, Vessel, Asset, Now));
        Assert.Equal(first, second);
        Assert.Equal(state, first.State);
        Assert.Equal("planning-current-v1/synthetic-threshold-v1/" + band, first.Algorithm);
    }

    [Fact]
    public void Running_hours_point_drives_a_meter_interval_plan()
    {
        var hours = new MeasurementPointRef(Tenant, Vessel, Asset, PointId, "ME-RH", PmsMeasurementPolicy.RunningHours, "HOUR");
        var rule = new MeasurementPolicyRule(Tenant, Vessel, "ME-RH", "rh-v1", "FICTIONAL", 1440);
        var inputs = new MaintenanceInputs(TriggerType: "Meter", IntervalValue: "500", IntervalUnit: "HOUR", LastDoneMeter: "1000", TimeZone: "UTC",
            Provenance: "SyntheticUnverified", Criticality: "DeclaredCritical", MeasurementPointId: PointId);
        var reading = new MeasurementLatest(Guid.NewGuid(), PointId, "1500.0", Now.AddHours(-1));
        var due = MaintenancePlanningPolicy.Evaluate(inputs, Now, new(true, true, true), PmsMeasurementPolicy.AssessTrigger(inputs, hours, 1, reading, rule, Tenant, Vessel, Asset, Now));
        Assert.Equal(("Due", "1500"), (due.State, due.NextDueMeter));
        var below = MaintenancePlanningPolicy.Evaluate(inputs, Now, new(true, true, true),
            PmsMeasurementPolicy.AssessTrigger(inputs, hours, 1, reading with { Value = "1499.9" }, rule, Tenant, Vessel, Asset, Now));
        Assert.Equal("NotDue", below.State);
        var wrongUnit = PmsMeasurementPolicy.AssessTrigger(inputs with { IntervalUnit = "CYCLE" }, hours, 1, reading, rule, Tenant, Vessel, Asset, Now);
        Assert.Equal("MeasurementUnitMismatch", wrongUnit.State);
    }
}
