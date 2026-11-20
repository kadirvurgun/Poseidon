using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

// S9 WP-A: DEV-058 / T-S9-001 (18.01 Backlog row 59) and DEV-059 / T-S9-002 (row 60); 60.01 Rev3 paragraphs 928,
// 936, 952. FICTIONAL codes only; the engine never produces a RELEASE by itself.
public sealed class DepartureReadinessTests
{
    private static readonly DateOnly From = new(2026, 11, 21), To = new(2026, 11, 25);
    private const string E = DepartureReadinessEngine.Current;
    private static DepartureCheck[] AllPass() => DepartureReadinessEngine.Categories.Select(c => new DepartureCheck(c, DepartureReadinessEngine.Pass, "FICTIONAL_PASS")).ToArray();

    [Fact]
    [Trait("Requirement", "DEV-058")]
    public void Every_category_passing_is_ready_and_the_engine_never_releases() {
        var o = DepartureReadinessEngine.Evaluate(AllPass(), From, To, E);
        Assert.Equal(DepartureReadinessEngine.Ready, o.State);
        Assert.Empty(o.Why); Assert.Equal(64, o.Hash.Length);
        Assert.Equal(DepartureReadinessEngine.DecisionAuthority, o.Decision);
        Assert.Equal(new[] { "crew", "technical", "statutory", "emergency", "safety" }, o.Categories.Select(c => c.Category));
    }

    [Fact]
    [Trait("Requirement", "DEV-058")]
    public void A_single_critical_blocker_is_never_averaged_away_by_many_passes() {
        var many = Enumerable.Range(0, 500).Select(i => new DepartureCheck("technical", DepartureReadinessEngine.Pass, "FICTIONAL_PASS_" + i)).ToList();
        many.AddRange(AllPass());
        many.Add(new("technical", DepartureReadinessEngine.Fail, "CONDITION_ALERTS:DECLARED_CRITICAL", "Defect", Guid.NewGuid(), 3));
        var o = DepartureReadinessEngine.Evaluate(many, From, To, E);
        Assert.Equal(DepartureReadinessEngine.NotReady, o.State);
        Assert.Equal(new[] { "technical:CONDITION_ALERTS:DECLARED_CRITICAL" }, o.CriticalBlockers);
        var why = Assert.Single(o.Why);
        Assert.Equal(("technical", DepartureReadinessEngine.Fail, "Defect", 3L), (why.Category, why.Result, why.SourceKind, why.SourceRevision));
        Assert.Equal(DepartureReadinessEngine.Fail, o.Categories.Single(c => c.Category == "technical").Result);
    }

    [Fact]
    [Trait("Requirement", "DEV-058")]
    public void Missing_category_is_unknown_and_review_required_never_ready() {
        var o = DepartureReadinessEngine.Evaluate(AllPass().Where(c => c.Category != "emergency").ToArray(), From, To, E);
        Assert.Equal(DepartureReadinessEngine.ReviewRequired, o.State);
        Assert.Contains("emergency:SOURCE_MISSING", o.ReviewReasons);
        Assert.Equal(DepartureReadinessEngine.Unknown, o.Categories.Single(c => c.Category == "emergency").Result);
        var unavailable = DepartureReadinessEngine.Evaluate([.. AllPass().Where(c => c.Category != "emergency"), new("emergency", "UNKNOWN", "EMERGENCY_SOURCE_UNAVAILABLE")], From, To, E);
        Assert.Equal(DepartureReadinessEngine.ReviewRequired, unavailable.State);
        Assert.Contains("emergency:EMERGENCY_SOURCE_UNAVAILABLE", unavailable.ReviewReasons);
    }

    [Fact]
    [Trait("Requirement", "DEV-058")]
    public void Precedence_is_fail_then_unknown_then_condition_and_why_is_ordered_most_severe_first() {
        var checks = AllPass().Concat([new DepartureCheck("crew", "CONDITIONAL", "SAFE_MANNING_ATTENTION"), new DepartureCheck("statutory", "UNKNOWN", "CERTIFICATE_HUMAN_VERIFICATION_MISSING")]).ToArray();
        var review = DepartureReadinessEngine.Evaluate(checks, From, To, E);
        Assert.Equal(DepartureReadinessEngine.ReviewRequired, review.State);
        Assert.Equal(new[] { "UNKNOWN", "CONDITIONAL" }, review.Why.Select(w => w.Result));
        var conditional = DepartureReadinessEngine.Evaluate(AllPass().Append(new("crew", "CONDITIONAL", "SAFE_MANNING_ATTENTION")).ToArray(), From, To, E);
        Assert.Equal(DepartureReadinessEngine.Conditional, conditional.State);
        Assert.Equal(new[] { "crew:SAFE_MANNING_ATTENTION" }, conditional.Conditions);
        var blocked = DepartureReadinessEngine.Evaluate(checks.Append(new("statutory", "FAIL", "CERTIFICATE_EXPIRES_BEFORE_VOYAGE_END")).ToArray(), From, To, E);
        Assert.Equal(DepartureReadinessEngine.NotReady, blocked.State);
        Assert.Equal("FAIL", blocked.Why[0].Result);
    }

    [Fact]
    [Trait("Requirement", "DEV-059")]
    public void Any_material_input_window_or_mode_change_changes_the_hash_and_order_does_not() {
        var a = AllPass();
        var baseline = DepartureReadinessEngine.Evaluate(a, From, To, E).Hash;
        Assert.Equal(baseline, DepartureReadinessEngine.Evaluate(a.AsEnumerable().Reverse().ToArray(), From, To, E).Hash);
        Assert.NotEqual(baseline, DepartureReadinessEngine.Evaluate([.. a, new("technical", "PASS", "FICTIONAL_PASS", "Defect", Guid.NewGuid(), 2)], From, To, E).Hash);
        var id = Guid.NewGuid();
        Assert.NotEqual(DepartureReadinessEngine.Evaluate([.. a, new("technical", "PASS", "X", "Defect", id, 1)], From, To, E).Hash,
            DepartureReadinessEngine.Evaluate([.. a, new("technical", "PASS", "X", "Defect", id, 2)], From, To, E).Hash);
        Assert.NotEqual(baseline, DepartureReadinessEngine.Evaluate(a, From, To.AddDays(1), E).Hash);
        Assert.NotEqual(baseline, DepartureReadinessEngine.Evaluate(a, From, To, DepartureReadinessEngine.Forecast).Hash);
    }

    [Theory]
    [Trait("Requirement", "DEV-058")]
    [InlineData("crew", "MAYBE", "X")]
    [InlineData("weather", "PASS", "X")]
    [InlineData("crew", "PASS", "lower case")]
    [InlineData("crew", "PASS", "")]
    public void Unknown_categories_results_or_free_text_reasons_are_rejected(string category, string result, string reason) =>
        Assert.Throws<ArgumentException>(() => DepartureReadinessEngine.Evaluate([new(category, result, reason)], From, To, E));

    [Fact]
    [Trait("Requirement", "DEV-058")]
    public void A_future_window_is_forecast_and_a_past_or_reversed_window_is_invalid() {
        var today = new DateOnly(2026, 11, 20);
        Assert.Equal(DepartureReadinessEngine.Forecast, DepartureReadinessEngine.Mode(From, To, today));
        Assert.Equal(DepartureReadinessEngine.Current, DepartureReadinessEngine.Mode(today, To, today));
        Assert.False(DepartureReadinessEngine.ValidWindow(To, From, today));
        Assert.False(DepartureReadinessEngine.ValidWindow(today.AddDays(-5), today.AddDays(-1), today));
        Assert.False(DepartureReadinessEngine.ValidWindow(today, today.AddDays(400), today));
        Assert.True(DepartureReadinessEngine.ValidWindow(today, today, today));
    }

    [Fact]
    public void Owning_codes_are_mapped_to_closed_upper_snake_reasons() {
        Assert.Equal("DECLARED_CRITICAL_DEFECT_OPEN", DepartureReadinessEngine.Code("DeclaredCriticalDefectOpen"));
        Assert.Equal("CRITICAL_OVERDUE_JOBS", DepartureReadinessEngine.Code("critical_overdue_jobs"));
        Assert.True(DepartureReadinessEngine.ValidReasonCode(DepartureReadinessEngine.Code("weird value; with 'quotes'")));
    }

    [Theory]
    [Trait("Requirement", "DEV-059")]
    [InlineData("RELEASE", "READY", "CURRENT", null)]
    [InlineData("RELEASE", "CONDITIONAL", "CURRENT", "departure_release_not_permitted")]
    [InlineData("RELEASE", "NOT_READY", "CURRENT", "departure_release_not_permitted")]
    [InlineData("RELEASE", "REVIEW_REQUIRED", "CURRENT", "departure_release_not_permitted")]
    [InlineData("RELEASE", "READY", "FORECAST", "departure_release_forecast_only")]
    [InlineData("HOLD", "NOT_READY", "CURRENT", null)]
    [InlineData("DENY", "REVIEW_REQUIRED", "FORECAST", null)]
    [InlineData("HOLD", "READY", "CURRENT", null)]
    [InlineData("GO", "READY", "CURRENT", "validation_failed")]
    public void Release_needs_a_current_ready_assessment_while_hold_and_deny_are_always_recordable(string decision, string state, string mode, string? expected) =>
        Assert.Equal(expected, DepartureDecisionPolicy.Refusal(decision, state, mode, "FICTIONAL Master reasoning"));

    [Theory]
    [Trait("Requirement", "DEV-059")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too short")]
    [InlineData(" leading space reason")]
    public void Every_decision_needs_a_reason(string? reason) =>
        Assert.Equal("validation_failed", DepartureDecisionPolicy.Refusal("HOLD", "NOT_READY", "CURRENT", reason));

    [Fact]
    [Trait("Requirement", "DEV-059")]
    public void A_decision_is_current_only_for_the_same_window_and_exact_hash() {
        var h = new string('A', 64);
        Assert.Equal("CURRENT", DepartureDecisionPolicy.Validity(h, From, To, h, From, To));
        Assert.Equal("STALE", DepartureDecisionPolicy.Validity(h, From, To, new string('B', 64), From, To));
        Assert.Equal("STALE", DepartureDecisionPolicy.Validity(h, From, To, h, From, To.AddDays(1)));
    }

    // S9 integration DEV-058 x DEV-060: the safety event blocker snapshot is one more owning-source input.
    private static DepartureCheck[] WithSafety(DepartureCheck[] safety) => [.. AllPass().Where(c => c.Category != "safety"), .. safety];

    [Fact]
    [Trait("Requirement", "DEV-058")]
    [Trait("Requirement", "DEV-060")]
    public void An_open_high_severity_safety_event_is_a_critical_blocker_that_passes_never_mask() {
        var blocking = Guid.NewGuid(); var review = Guid.NewGuid();
        var safety = DepartureReadinessEngine.SafetyChecks("BLOCKERS_PRESENT",
            [new DepartureSafetyItem(blocking, "HIGH_SEVERITY_OPEN", true), new DepartureSafetyItem(review, "UNTRIAGED_EVENT", false)], ["SeverityPolicyUnconfigured"]);
        Assert.Contains(safety, c => c.Result == DepartureReadinessEngine.Fail && c.SourceId == blocking && c.SourceKind == "SafetyEvent" && c.Reason == "SAFETY_EVENT:HIGH_SEVERITY_OPEN");
        Assert.Contains(safety, c => c.Result == DepartureReadinessEngine.Unknown && c.SourceId == review && c.Reason == "SAFETY_EVENT:UNTRIAGED_EVENT");
        var o = DepartureReadinessEngine.Evaluate(WithSafety(safety), From, To, E);
        Assert.Equal(DepartureReadinessEngine.NotReady, o.State);
        Assert.Equal(new[] { "safety:SAFETY_EVENT:HIGH_SEVERITY_OPEN" }, o.CriticalBlockers);
        Assert.Equal(DepartureReadinessEngine.Fail, o.Categories.Single(c => c.Category == "safety").Result);
        Assert.Equal("departure_release_not_permitted", DepartureDecisionPolicy.Refusal("RELEASE", o.State, E, "FICTIONAL reason text"));
        // A BLOCKERS_PRESENT state without item detail still fails closed.
        Assert.Equal(DepartureReadinessEngine.Fail, Assert.Single(DepartureReadinessEngine.SafetyChecks("BLOCKERS_PRESENT", [])).Result);
    }

    [Theory]
    [Trait("Requirement", "DEV-058")]
    [Trait("Requirement", "DEV-060")]
    [InlineData("REVIEW_REQUIRED", "SAFETY_EVENT_REVIEW_REQUIRED")]
    [InlineData("UNAVAILABLE", "SAFETY_EVENT_SOURCE_UNAVAILABLE")]
    [InlineData(null, "SAFETY_EVENT_STATE_UNRECOGNIZED")]
    [InlineData("READY", "SAFETY_EVENT_STATE_UNRECOGNIZED")]
    public void Review_required_or_unavailable_safety_source_is_unknown_never_pass(string? state, string reason) {
        var safety = DepartureReadinessEngine.SafetyChecks(state, state == "REVIEW_REQUIRED" ? [new DepartureSafetyItem(Guid.NewGuid(), "SEVERITY_UNASSESSED", false)] : []);
        Assert.DoesNotContain(safety, c => c.Result == DepartureReadinessEngine.Pass);
        Assert.Contains(safety, c => c.Result == DepartureReadinessEngine.Unknown && c.Reason == reason);
        var o = DepartureReadinessEngine.Evaluate(WithSafety(safety), From, To, E);
        Assert.Equal(DepartureReadinessEngine.ReviewRequired, o.State);
        Assert.Contains("safety:" + reason, o.ReviewReasons);
        Assert.Equal(DepartureReadinessEngine.Unknown, o.Categories.Single(c => c.Category == "safety").Result);
    }

    [Fact]
    [Trait("Requirement", "DEV-058")]
    [Trait("Requirement", "DEV-060")]
    public void Only_no_open_high_severity_event_passes_the_safety_input_and_a_source_limit_stays_unknown() {
        var pass = Assert.Single(DepartureReadinessEngine.SafetyChecks("NO_OPEN_HIGH_SEVERITY_EVENT", [], ["SeverityPolicyUnconfigured"]));
        Assert.Equal(("safety", DepartureReadinessEngine.Pass, "NO_OPEN_HIGH_SEVERITY_EVENT"), (pass.Category, pass.Result, pass.Reason));
        Assert.Equal(DepartureReadinessEngine.Ready, DepartureReadinessEngine.Evaluate(WithSafety([pass]), From, To, E).State);
        // An inconsistent snapshot (items under a "no event" state) or a truncated source is never PASS.
        Assert.All(DepartureReadinessEngine.SafetyChecks("NO_OPEN_HIGH_SEVERITY_EVENT", [new DepartureSafetyItem(Guid.NewGuid(), "SEVERITY_UNASSESSED", false)]),
            c => Assert.NotEqual(DepartureReadinessEngine.Pass, c.Result));
        var limited = DepartureReadinessEngine.SafetyChecks("NO_OPEN_HIGH_SEVERITY_EVENT", [], ["SafetyEventSourceLimitExceeded"]);
        Assert.Equal(DepartureReadinessEngine.ReviewRequired, DepartureReadinessEngine.Evaluate(WithSafety(limited), From, To, E).State);
        // The safety input is bound into the assessment hash: a new blocker makes a prior decision STALE.
        var before = DepartureReadinessEngine.Evaluate(WithSafety([pass]), From, To, E).Hash;
        var after = DepartureReadinessEngine.Evaluate(WithSafety(DepartureReadinessEngine.SafetyChecks("BLOCKERS_PRESENT",
            [new DepartureSafetyItem(Guid.NewGuid(), "HIGH_SEVERITY_OPEN", true)])), From, To, E).Hash;
        Assert.NotEqual(before, after);
    }
}
