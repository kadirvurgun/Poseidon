using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;
public sealed class MaintenanceObservationPolicyTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid(), Asset = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026,11,1,12,0,0,TimeSpan.Zero);
    private static MaintenanceObservationValue Observation() => new(Guid.NewGuid(), Tenant, Vessel, Asset, "synthetic-meter", "synthetic-v1", 1, "Meter", "hours", "19.5000", Now.AddMinutes(-1), false);
    private static MaintenanceObservationRule Rule() => new(Tenant,Vessel,Asset,"synthetic-meter","synthetic-v1","Meter","hours",3600,4);
    private static readonly MaintenanceInputs Input = new(TriggerType: "Meter", IntervalValue: "10", IntervalUnit: "hours", LastDoneMeter: "10", TimeZone: "UTC");
    private static MaintenanceObservationAssessment Assess(MaintenanceObservationValue? observation, MaintenanceObservationRule? rule) => MaintenanceObservationPolicy.Assess(observation, rule, Tenant,Vessel,Asset,Input,Now);
    [Fact] public void Real_observation_overrides_client_reading_and_uses_explicit_current_clock_policy() {
        var assessment = Assess(Observation(), Rule()); Assert.True(assessment.Ready); Assert.Equal("19.5000", assessment.Inputs.CurrentMeter);
        Assert.Equal("20", MaintenanceCalculation.Evaluate(assessment.Inputs, Now, assessment).NextDueMeter);
        Assert.Equal("ReadingFreshnessPolicyUnresolved", MaintenanceCalculation.Evaluate(assessment.Inputs, Now).Reason);
    }
    [Fact] public void Missing_and_unconfigured_are_distinct() {
        Assert.Equal("ObservationMissing", Assess(null, Rule()).State);
        Assert.Equal("ObservationPolicyUnavailable", Assess(Observation(), null).State);
        Assert.Equal("ObservationPolicyUnavailable", Assess(Observation(), Rule() with { MaxScale = null }).State);
    }
    [Fact] public void Scope_revocation_policy_change_and_wrong_unit_fail_explicitly() {
        Assert.Equal("ObservationScopeMismatch", Assess(Observation() with { TenantId = Guid.NewGuid() }, Rule()).State);
        Assert.Equal("ObservationRevoked", Assess(Observation() with { Revoked = true }, Rule()).State);
        Assert.Equal("ObservationPolicyChanged", Assess(Observation(), Rule() with { Version = "new-version" }).State);
        Assert.Equal("ObservationUnitOrKindMismatch", Assess(Observation() with { Unit = "cycles" }, Rule()).State);
    }
    [Fact] public void Trusted_current_clock_checks_freshness_even_if_a_client_selects_a_historic_reference() {
        Assert.Equal("ObservationStale", Assess(Observation() with { ObservedAt = Now.AddHours(-2) }, Rule()).State);
        Assert.Equal("ObservationFuture", Assess(Observation() with { ObservedAt = Now.AddTicks(1) }, Rule()).State);
        Assert.True(Assess(Observation() with { ObservedAt = Now.AddHours(-1) }, Rule()).Ready);
    }
    [Theory][InlineData("19.50001")][InlineData("123456789012345678901234567890.0001")][InlineData("1\n")]
    public void Excess_precision_or_noncanonical_lexical_value_is_not_rounded(string value) => Assert.Equal("ObservationPrecisionUnsupported", Assess(Observation() with { Value = value }, Rule()).State);
    [Fact] public void Condition_sources_use_their_own_kind_unit_and_policy() {
        var row = Observation() with { Kind = "Condition", Unit = "predicate", Value = "False" };
        var rule = Rule() with { Kind = "Condition", Unit = "predicate" };
        var result = MaintenanceObservationPolicy.Assess(row, rule, Tenant,Vessel,Asset,Input with { TriggerType = "Condition", IntervalUnit = "predicate" }, Now);
        Assert.True(result.Ready); Assert.Equal("NotDue", MaintenanceCalculation.Evaluate(result.Inputs,Now,result).State);
        Assert.Equal("ObservationConditionUnknown", MaintenanceObservationPolicy.Assess(row with { Value = "Unknown" },rule,Tenant,Vessel,Asset,Input with { TriggerType = "Condition", IntervalUnit = "predicate" },Now).State);
    }
}
