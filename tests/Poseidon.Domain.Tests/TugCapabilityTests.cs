using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class TugCapabilityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-02T00:00:00Z");
    private static readonly Guid Asset = Guid.Parse("ceca1000-0000-4000-8000-000000000001");
    private static TugCapabilityStatement Test => new("BollardPullTest", Asset, 2, 3, "70", "67.123456", Now.AddDays(-1), Now.AddYears(1), SourceLocator: "FICTIONAL/certificate", AssetVersion: 4);
    private static TugCapabilityStatement Condition => new("Condition", Asset, 2, 3, ConditionFactor: "0.987654", SourceLocator: "FICTIONAL/condition", AssetVersion: 4);
    private static TugCalculationPolicy Policy => new("MeasuredTimesVerifiedCondition", "SumForPlanning", "FICTIONAL/source/formula-only", [TugRequestPolicy.JobTypes[0]], ["FICTIONAL-AREA"], 3, "Floor", 365, "10");
    private static TugCapacityResult Calculate(TugCapabilityStatement? test = null, TugCapabilityStatement? condition = null, TugCalculationPolicy? policy = null) =>
        TugCapability.Calculate(test ?? Test, condition ?? Condition, policy ?? Policy, TugRequestPolicy.JobTypes[0], "FICTIONAL-AREA", Now.AddHours(1), Now.AddHours(2), Now);
    [Fact]
    public void Exact_decimal_formula_rounds_down_only_at_the_explicit_qualified_scale() {
        var result = Calculate(); Assert.Equal("CurrentPlanningCapacity", result.State);
        Assert.Equal("66.294", result.Tonnes); // 67.123456 * .987654 = 66.294749812224
        Assert.Contains("MasterDecisionSeparate", result.Why);
        Assert.Equal("0", Calculate(condition: Condition with { ConditionFactor = "0" }).Tonnes);
    }
    [Theory]
    [InlineData("1e2")][InlineData("1,2")][InlineData("+1")][InlineData("-1")][InlineData("1.0000001")][InlineData("01")][InlineData("1\n")]
    public void Wire_numbers_are_lossless_closed_base_ten(string raw) => Assert.Throws<ArgumentException>(() => Calculate(Test with { MeasuredTonnes = raw }));
    [Fact]
    public void Incomplete_policy_and_evidence_remain_unknown_and_cannot_become_no_or_ready() {
        TugCapability.Validate(new TugCalculationPolicy()); TugCapability.Validate(new TugCapabilityStatement("BollardPullTest"));
        Assert.Equal("Unknown", Calculate(policy: new()).State);
        Assert.Equal("Unknown", Calculate(test: Test with { MeasuredTonnes = null }).State);
        Assert.Contains("TUG_CALCULATION_POLICY_MISSING", RegulatoryQualificationPolicy.Missing(new(TugCalculation: new())));
    }
    [Fact]
    public void Window_expiry_changed_equipment_and_future_test_never_produce_capacity() {
        Assert.Equal("Expired", Calculate(test: Test with { ReviewAt = Now.AddHours(2) }).State);
        Assert.Equal("Expired", Calculate(policy: Policy with { MaxTestAgeDays = 1 }).State);
        Assert.Equal("Future", Calculate(test: Test with { TestAt = Now.AddMinutes(1) }).State);
        Assert.Equal("Unknown", Calculate(condition: Condition with { AssetVersion = 5 }).State);
        Assert.Equal("Unknown", Calculate(condition: Condition with { ProfileVersion = 4 }).State);
    }
    [Fact]
    public void Formula_context_and_loss_restrictions_are_explicit_without_escort_or_derating_defaults() {
        Assert.Equal("UnsupportedContext", Calculate(policy: Policy with { Areas = ["OTHER"] }).State);
        Assert.Equal("ReviewRequired", Calculate(Test with { MeasuredTonnes = "62.999999" }).State);
        Assert.Equal("CurrentPlanningCapacity", Calculate(Test with { MeasuredTonnes = "63" }).State);
        Assert.Equal("ReviewRequired", Calculate(Test with { MeasuredTonnes = "70.000001" }).State);
        Assert.Throws<ArgumentException>(() => Calculate(policy: Policy with { Formula = "InventedEscortNorm" }));
        Assert.Throws<ArgumentException>(() => Calculate(condition: Condition with { ConditionFactor = "1.000001" }));
    }
}
