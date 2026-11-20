using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-109 / T-PMS2-003 and DEV-052 criticality; DEV-053 deterministic due calculation.
public sealed class MaintenancePostponementPolicyTests
{
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Requester = Guid.Parse("60000000-0000-0000-0000-000000000006");
    private static readonly Guid Decider = Guid.Parse("70000000-0000-0000-0000-000000000007");
    private static readonly DateOnly Due = new(2026, 12, 1);
    private static PostponementPolicyRule Rule(int max = 30) => new(Tenant, "FICTIONAL-DEFERRAL-POLICY", "synthetic-v1", max);
    private const string Reason = "FICTIONAL spare parts arrive next port call";

    [Theory]
    [InlineData("CRITICAL", "CRITICAL")]
    [InlineData("IMPORTANT", "IMPORTANT")]
    [InlineData("STANDARD", "STANDARD")]
    [InlineData(null, "UNKNOWN")]
    [InlineData("Critical", "UNKNOWN")]
    [InlineData("DeclaredCritical", "UNKNOWN")]
    public void Criticality_is_a_closed_vocabulary_and_anything_else_is_unknown(string? input, string expected)
    {
        Assert.Equal(expected, EquipmentCriticality.Of(input));
        Assert.Equal(input is null or "CRITICAL" or "IMPORTANT" or "STANDARD", EquipmentCriticality.ValidInput(input));
        Assert.Equal(expected is "CRITICAL" or "UNKNOWN", EquipmentCriticality.RequiresReview(expected));
    }

    [Fact]
    public void Requests_need_reason_known_original_due_and_a_later_deadline_but_not_a_configured_policy()
    {
        Assert.Null(MaintenancePostponementPolicy.ValidateRequest(Reason, Due, Due.AddDays(10), null, null, false));
        Assert.Equal("invalid_reason", MaintenancePostponementPolicy.ValidateRequest("short", Due, Due.AddDays(10), null, null, false));
        Assert.Equal("original_due_unavailable", MaintenancePostponementPolicy.ValidateRequest(Reason, null, Due.AddDays(10), null, null, false));
        Assert.Equal("postponement_deadline_not_after_due", MaintenancePostponementPolicy.ValidateRequest(Reason, Due, Due, null, null, false));
        Assert.Equal("postponement_not_allowed", MaintenancePostponementPolicy.ValidateRequest(Reason, Due, Due.AddDays(10), null, null, true));
        Assert.Equal("postponement_extension_exceeds_policy", MaintenancePostponementPolicy.ValidateRequest(Reason, Due, Due.AddDays(31), null, Rule(), false));
    }

    [Fact]
    public void A_chained_postponement_is_measured_from_the_first_original_due()
    {
        // AT-034: first approved to day 20; a second request to day 35 exceeds 30 from the original due.
        Assert.Equal("postponement_deadline_not_after_previous", MaintenancePostponementPolicy.ValidateRequest(Reason, Due, Due.AddDays(20), Due.AddDays(20), Rule(), false));
        Assert.Equal("postponement_extension_exceeds_policy", MaintenancePostponementPolicy.ValidateRequest(Reason, Due, Due.AddDays(35), Due.AddDays(20), Rule(), false));
        Assert.Null(MaintenancePostponementPolicy.ValidateRequest(Reason, Due, Due.AddDays(30), Due.AddDays(20), Rule(), false));
    }

    [Fact]
    public void Approval_requires_a_different_person_current_plan_configured_policy_and_risk_review_for_critical()
    {
        Assert.Equal("separation_of_duties", MaintenancePostponementPolicy.ValidateApproval(Requester, Requester, "STANDARD", false, Due, Due.AddDays(5), Rule(), true));
        Assert.Equal("plan_context_stale", MaintenancePostponementPolicy.ValidateApproval(Requester, Decider, "STANDARD", false, Due, Due.AddDays(5), Rule(), false));
        Assert.Equal("postponement_policy_unconfigured", MaintenancePostponementPolicy.ValidateApproval(Requester, Decider, "STANDARD", false, Due, Due.AddDays(5), null, true));
        Assert.Equal("postponement_policy_unconfigured", MaintenancePostponementPolicy.ValidateApproval(Requester, Decider, "STANDARD", false, Due, Due.AddDays(5), Rule(0), true));
        Assert.Equal("postponement_extension_exceeds_policy", MaintenancePostponementPolicy.ValidateApproval(Requester, Decider, "STANDARD", false, Due, Due.AddDays(31), Rule(), true));
        Assert.Equal("risk_review_required", MaintenancePostponementPolicy.ValidateApproval(Requester, Decider, "CRITICAL", false, Due, Due.AddDays(5), Rule(), true));
        Assert.Equal("risk_review_required", MaintenancePostponementPolicy.ValidateApproval(Requester, Decider, "UNKNOWN", false, Due, Due.AddDays(5), Rule(), true));
        Assert.Null(MaintenancePostponementPolicy.ValidateApproval(Requester, Decider, "CRITICAL", true, Due, Due.AddDays(30), Rule(), true));
        Assert.Null(MaintenancePostponementPolicy.ValidateApproval(Requester, Decider, "STANDARD", false, Due, Due.AddDays(30), Rule(), true));
    }

    [Theory]
    [InlineData("CRITICAL", "REQUESTED", "REVIEW_REQUIRED")]
    [InlineData("UNKNOWN", "APPROVED", "REVIEW_REQUIRED")]
    [InlineData("CRITICAL", "APPROVED", "REVIEW_REQUIRED")]
    [InlineData("STANDARD", "APPROVED", "CONDITIONAL_DEFERRED")]
    [InlineData("IMPORTANT", "REQUESTED", "PENDING_DECISION")]
    [InlineData("CRITICAL", "REJECTED", "UNCHANGED")]
    [InlineData("CRITICAL", "EXPIRED", "UNCHANGED")]
    public void A_postponement_never_produces_readiness_and_critical_equipment_needs_review(string criticality, string state, string impact) =>
        Assert.Equal(impact, MaintenancePostponementPolicy.ReadinessImpact(criticality, state));

    [Fact]
    public void An_approved_postponement_expires_after_its_deadline()
    {
        Assert.Equal("APPROVED", MaintenancePostponementPolicy.EffectiveState("APPROVED", Due, Due));
        Assert.Equal("EXPIRED", MaintenancePostponementPolicy.EffectiveState("APPROVED", Due, Due.AddDays(1)));
        Assert.Equal("REQUESTED", MaintenancePostponementPolicy.EffectiveState("REQUESTED", Due, Due.AddDays(1)));
    }

    // DEV-053 determinism: identical inputs at the same instant give byte-identical results
    // regardless of process culture, and calendar edges follow DateOnly arithmetic exactly.
    [Theory]
    [InlineData(2026, 1, 31, "1", "month", "2026-02-28")]
    [InlineData(2028, 2, 29, "1", "year", "2029-02-28")]
    [InlineData(2026, 12, 31, "1", "day", "2027-01-01")]
    [InlineData(2026, 3, 1, "12", "month", "2027-03-01")]
    public void Calendar_due_calculation_is_deterministic_across_cultures(int y, int m, int d, string interval, string unit, string expected)
    {
        var inputs = new MaintenanceInputs(TriggerType: "Calendar", IntervalValue: interval, IntervalUnit: unit, LastDoneDate: new(y, m, d),
            TimeZone: "UTC", Provenance: "SyntheticUnverified", Criticality: "DeclaredCritical");
        var at = new DateTimeOffset(2029, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            var tr = MaintenanceCalculation.Evaluate(inputs, at);
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            var ar = MaintenanceCalculation.Evaluate(inputs, at);
            Assert.Equal(tr, ar);
            Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), tr.NextDueDate);
            Assert.Equal("Due", tr.State);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void Due_boundary_is_inclusive_and_evaluated_in_the_declared_time_zone()
    {
        var inputs = new MaintenanceInputs(TriggerType: "Calendar", IntervalValue: "10", IntervalUnit: "day", LastDoneDate: new(2026, 11, 10),
            TimeZone: "Europe/Istanbul", Provenance: "SyntheticUnverified", Criticality: "DeclaredCritical");
        // 2026-11-19T21:30Z is 2026-11-20 00:30 in Istanbul (UTC+3): the due day has begun there.
        Assert.Equal("Due", MaintenanceCalculation.Evaluate(inputs, new(2026, 11, 19, 21, 30, 0, TimeSpan.Zero)).State);
        Assert.Equal("NotDue", MaintenanceCalculation.Evaluate(inputs, new(2026, 11, 19, 20, 30, 0, TimeSpan.Zero)).State);
    }
}
