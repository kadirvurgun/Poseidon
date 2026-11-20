using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S9 WP-D: DEV-089 (18.01 row 90, T-TUG-010), DEV-090 (row 91, T-TUG-011), DEV-111 (row 112, T-PMS2-004).
// Synthetic values only; no rule here is an approved operational limit.
public sealed class FieldOperationsPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static TowageStartFacts Ready() => new("Submitted", false, true, true, true, true,
        [new("briefing", TugReadinessEngine.Pass, "BriefingIssuedAndAcknowledgedByAllParticipants")], true, "GO", true, true, true);

    [Fact]
    [Trait("Requirement", "DEV-089")]
    public void Start_needs_approved_plan_complete_briefing_and_a_current_go_and_never_invents_one()
    {
        Assert.Empty(TowageExecutionPolicy.StartBlockers(Ready()));
        Assert.Contains("towage_plan_not_approved", TowageExecutionPolicy.StartBlockers(Ready() with { PlanApprovedCurrent = false }));
        Assert.Contains("briefing:BriefingAcknowledgementPending", TowageExecutionPolicy.StartBlockers(Ready() with {
            BriefingChecks = [new("briefing", TugReadinessEngine.Fail, "BriefingAcknowledgementPending")] }));
        Assert.Contains("briefing:TowageBriefingMissing", TowageExecutionPolicy.StartBlockers(Ready() with { BriefingChecks = [] }));
        Assert.Contains("readiness_go_missing", TowageExecutionPolicy.StartBlockers(Ready() with { Decision = null }));
        Assert.Contains("readiness_no_go_recorded", TowageExecutionPolicy.StartBlockers(Ready() with { Decision = "NO_GO" }));
        Assert.Contains("readiness_go_stale", TowageExecutionPolicy.StartBlockers(Ready() with { DecisionHashCurrent = false }));
        // QA N2: a current-hash GO over a non-READY (CONDITIONAL/REVIEW_REQUIRED/NOT_READY) assessment never starts.
        Assert.Equal(["readiness_not_ready"], TowageExecutionPolicy.StartBlockers(Ready() with { AssessmentReady = false }));
        Assert.Contains("tug_not_in_approved_plan", TowageExecutionPolicy.StartBlockers(Ready() with { TugInPlan = false }));
        Assert.Contains("execution_already_started", TowageExecutionPolicy.StartBlockers(Ready() with { ExecutionExists = true }));
    }

    [Theory]
    [Trait("Requirement", "DEV-089")]
    [InlineData("ACTIVE", "SUSPEND", "SUSPENDED")]
    [InlineData("SUSPENDED", "RESUME", "ACTIVE")]
    [InlineData("ACTIVE", "ABORT", "ABORTED")]
    [InlineData("SUSPENDED", "ABORT", "ABORTED")]
    [InlineData("ACTIVE", "COMPLETE", "COMPLETED")]
    public void State_machine_allows_only_onboard_transitions(string from, string action, string to)
    {
        Assert.Equal((to, (string?)null), TowageExecutionPolicy.Transition(from, action, "FICTIONAL reason with more than twenty characters"));
    }

    [Fact]
    [Trait("Requirement", "DEV-089")]
    public void Terminal_states_refuse_operating_actions_and_reasons_are_required()
    {
        Assert.Equal("invalid_state", TowageExecutionPolicy.Transition("COMPLETED", "ABORT", "FICTIONAL reason with more than twenty").Failure);
        Assert.Equal("invalid_state", TowageExecutionPolicy.Transition("SUSPENDED", "COMPLETE", "FICTIONAL reason with more than twenty").Failure);
        Assert.Equal("invalid_reason", TowageExecutionPolicy.Transition("ACTIVE", "ABORT", "too short").Failure);
        Assert.Equal("invalid_reason", TowageExecutionPolicy.Transition("ACTIVE", "SUSPEND", "short").Failure);
        Assert.Equal("validation_failed", TowageExecutionPolicy.Transition("ACTIVE", "GO", "FICTIONAL reason with more than twenty").Failure);
    }

    [Fact]
    [Trait("Requirement", "DEV-089")]
    public void Events_carry_recorded_values_with_units_and_no_defaults()
    {
        TowageEventInput E(string kind, string? value = null, string? unit = null, string? result = null, string? text = null, string? reference = null, TowageWeather? w = null) =>
            new(kind, T0, value, unit, result, text, reference, w);
        Assert.True(TowageExecutionPolicy.ValidEvent(E("LOAD_OBSERVED", "42.5", "t")));
        Assert.False(TowageExecutionPolicy.ValidEvent(E("LOAD_OBSERVED", "42.5")));
        Assert.False(TowageExecutionPolicy.ValidEvent(E("LOAD_OBSERVED", "-1", "t")));
        Assert.False(TowageExecutionPolicy.ValidEvent(E("LOAD_OBSERVED", "42", "lbs")));
        Assert.True(TowageExecutionPolicy.ValidEvent(E("COMMS_CHECK", result: "GOOD", text: "VHF 12")));
        Assert.False(TowageExecutionPolicy.ValidEvent(E("COMMS_CHECK", result: "OK")));
        Assert.False(TowageExecutionPolicy.ValidEvent(E("WEATHER_OBSERVED", w: new(null, null, null, null, null))));
        Assert.True(TowageExecutionPolicy.ValidEvent(E("WEATHER_OBSERVED", w: new("12", "SW", null, null, null))));
        Assert.False(TowageExecutionPolicy.ValidEvent(E("TOWLINE_CONNECTED", value: "1")));
        Assert.False(TowageExecutionPolicy.ValidEvent(E("EVIDENCE", result: "PHOTO")));
        Assert.True(TowageExecutionPolicy.ValidEvent(E("EVIDENCE", result: "AIS_OR_TIME", reference: "FICTIONAL-AIS-1")));
        Assert.False(TowageExecutionPolicy.ValidEvent(E("STARTED")));
        Assert.False(TowageExecutionPolicy.Chronological(T0.AddMinutes(10), T0, T0));
        Assert.False(TowageExecutionPolicy.Chronological(T0.AddMinutes(-1), T0, T0.AddHours(1)));
    }

    [Fact]
    [Trait("Requirement", "DEV-089")]
    public void Ledger_projects_first_connection_last_release_max_load_per_unit_and_active_time()
    {
        TowageLedgerEvent L(long s, string kind, int minutes, string? value = null, string? unit = null, string? result = null) => new(s, kind, T0.AddMinutes(minutes), value, unit, result);
        var ledger = TowageExecutionPolicy.Ledger([L(1, "STARTED", 0), L(2, "TOWLINE_CONNECTED", 5), L(3, "LOAD_OBSERVED", 6, "30", "t"), L(4, "LOAD_OBSERVED", 7, "45.5", "t"),
            L(5, "LOAD_OBSERVED", 8, "300", "kN"), L(6, "SUSPENDED", 10), L(7, "RESUMED", 20), L(8, "TOWLINE_RELEASED", 25), L(9, "TOWLINE_CONNECTED", 26),
            L(10, "COMMS_CHECK", 27, result: "ATTENTION")], T0.AddMinutes(30));
        Assert.Equal(T0.AddMinutes(5), ledger.TowlineConnectedAt);
        Assert.True(ledger.TowlineConnected); Assert.Null(ledger.TowlineReleasedAt);
        Assert.Equal(["300:kN:1", "45.5:t:2"], ledger.MaxTowLoad.Select(x => $"{x.Value}:{x.Unit}:{x.Samples}"));
        Assert.Equal(("ATTENTION", "UNKNOWN", "UNKNOWN"), (ledger.CommunicationState, ledger.WinchState, ledger.EmergencyReleaseTested));
        Assert.Equal(20 * 60, ledger.ActiveSeconds);
        Assert.Contains("towline_still_connected", TowageExecutionPolicy.CompleteBlockers("FICTIONAL berth 4", ledger));
        Assert.Contains("end_position_missing", TowageExecutionPolicy.CompleteBlockers(null, ledger));
    }

    [Fact]
    [Trait("Requirement", "DEV-089")]
    public void Verification_is_a_different_person_after_the_operation_ended()
    {
        Guid starter = Guid.NewGuid(), ender = Guid.NewGuid(), verifier = Guid.NewGuid();
        Assert.Null(TowageExecutionPolicy.VerificationFailure("COMPLETED", "PENDING", verifier, starter, ender, "VERIFIED", "FICTIONAL record checked"));
        Assert.Equal("separation_of_duties", TowageExecutionPolicy.VerificationFailure("COMPLETED", "PENDING", ender, starter, ender, "VERIFIED", "FICTIONAL record checked"));
        Assert.Equal("separation_of_duties", TowageExecutionPolicy.VerificationFailure("ABORTED", "PENDING", starter, starter, ender, "REJECTED", "FICTIONAL record checked"));
        Assert.Equal("invalid_state", TowageExecutionPolicy.VerificationFailure("ACTIVE", "PENDING", verifier, starter, null, "VERIFIED", "FICTIONAL record checked"));
        Assert.Equal("invalid_state", TowageExecutionPolicy.VerificationFailure("COMPLETED", "VERIFIED", verifier, starter, ender, "VERIFIED", "FICTIONAL record checked"));
    }

    private static TugDailyLogContent Log(params TugDailyMeter[] meters) =>
        new("Europe/Istanbul", meters, new(null, null, null), [], [], "FICTIONAL day summary for the synthetic tug");

    [Fact]
    [Trait("Requirement", "DEV-090")]
    public void Meter_sequence_conflict_needs_reset_evidence_and_submit_lists_missing_fields()
    {
        var m = new TugDailyMeter(Guid.NewGuid(), "ME-PORT", "100", "90", null);
        Assert.True(TugDailyLogPolicy.MeterSequenceConflict(Log(m)));
        Assert.False(TugDailyLogPolicy.MeterSequenceConflict(Log(m with { ResetEvidenceRef = "FICTIONAL rollover photo" })));
        var blockers = TugDailyLogPolicy.SubmitBlockers(new(null, [m with { Closing = null }], new("10", null, null), [], [], "short"), false);
        Assert.Equal(["daily_summary_too_short", "fuel_source_missing", "fuel_unit_missing", "meter_reading_missing", "timezone_missing"], blockers);
        Assert.False(TugDailyLogPolicy.ValidDraft(Log(m, m with { Id = Guid.NewGuid() })));
    }

    [Fact]
    [Trait("Requirement", "DEV-090")]
    public void Reconciliation_marks_gaps_regressions_and_unlinked_executions_for_review_and_never_fills_values()
    {
        var zone = TimeZoneInfo.Utc;
        var interval = TugDailyLogPolicy.Interval(new DateOnly(2026, 11, 20), "08-16", zone);
        Assert.Equal((T0, T0.AddHours(8)), interval);
        var meter = new TugDailyMeter(Guid.NewGuid(), "ME-PORT", "100", "106", null);
        var ok = TugDailyLogPolicy.Reconcile(Log(meter), new Dictionary<string, string> { ["ME-PORT"] = "100" }, interval, [], T0.AddHours(9));
        Assert.Equal(TugDailyLogPolicy.Reconciled, ok.State); Assert.Equal("6", ok.MeterHours);
        var gap = TugDailyLogPolicy.Reconcile(Log(meter), new Dictionary<string, string> { ["ME-PORT"] = "98" }, interval, [], T0.AddHours(9));
        Assert.Contains(gap.Reasons, r => r.Code == "meter_gap"); Assert.Equal(TugDailyLogPolicy.ReviewRequired, gap.State);
        var regression = TugDailyLogPolicy.Reconcile(Log(meter), new Dictionary<string, string> { ["ME-PORT"] = "101" }, interval, [], T0.AddHours(9));
        Assert.Contains(regression.Reasons, r => r.Code == "meter_regression");
        var first = TugDailyLogPolicy.Reconcile(Log(meter), new Dictionary<string, string>(), interval, [], T0.AddHours(9));
        Assert.Contains(first.Reasons, r => r.Code == "meter_previous_unknown");
        var execution = new TugDailyExecutionFact(Guid.NewGuid(), "COMPLETED", T0.AddHours(1), T0.AddHours(3), [new(T0.AddHours(1), T0.AddHours(3))]);
        var unlinked = TugDailyLogPolicy.Reconcile(Log(meter), new Dictionary<string, string> { ["ME-PORT"] = "100" }, interval, [execution], T0.AddHours(9));
        Assert.Contains(unlinked.Reasons, r => r.Code == "execution_unlinked"); Assert.Equal(0, unlinked.JobsCompleted);
        var linked = TugDailyLogPolicy.Reconcile(Log(meter) with { ExecutionIds = [execution.Id] }, new Dictionary<string, string> { ["ME-PORT"] = "100" }, interval, [execution], T0.AddHours(9));
        Assert.Equal((TugDailyLogPolicy.Reconciled, 1, "2"), (linked.State, linked.JobsCompleted, linked.OperatingHours));
        var noZone = TugDailyLogPolicy.Reconcile(Log(meter), new Dictionary<string, string>(), null, [], T0);
        Assert.Equal(TugDailyLogPolicy.Incomplete, noZone.State);
    }

    [Fact]
    [Trait("Requirement", "DEV-090")]
    public void Master_review_is_never_by_an_author_or_submitter()
    {
        Guid author = Guid.NewGuid(), master = Guid.NewGuid();
        Assert.Null(TugDailyLogPolicy.ReviewFailure("SUBMITTED", "VERIFY", master, [author], author, "FICTIONAL reviewed log"));
        Assert.Equal("separation_of_duties", TugDailyLogPolicy.ReviewFailure("SUBMITTED", "VERIFY", author, [author], author, "FICTIONAL reviewed log"));
        Assert.Equal("invalid_state", TugDailyLogPolicy.ReviewFailure("DRAFT", "VERIFY", master, [author], null, "FICTIONAL reviewed log"));
        Assert.Equal("ATTENTION", TugDailyLogPolicy.MasterReview("SUBMITTED", TugDailyLogPolicy.ReviewRequired));
        Assert.Equal("REVIEWED", TugDailyLogPolicy.MasterReview("VERIFIED", TugDailyLogPolicy.ReviewRequired));
    }

    private static DrydockContent Dock(params DrydockScopeItem[] items) => new("FICTIONAL-DD-1", "FICTIONAL yard", new(2027, 1, 10), new(2027, 1, 30),
        new("125000.00", "EUR", "FICTIONAL vendor", null), items, []);

    [Fact]
    [Trait("Requirement", "DEV-111")]
    public void Drydock_readiness_shows_unlinked_overdue_and_stale_links_and_is_never_a_survey_result()
    {
        var today = new DateOnly(2026, 11, 20);
        var wo = Guid.NewGuid(); var cert = Guid.NewGuid();
        var content = Dock(new(Guid.NewGuid(), "WORK_ORDER", "FICTIONAL hull cleaning", null, null, null, null, "HIGH", [new("WORK_ORDER", wo, 3)]),
            new(Guid.NewGuid(), "CLASS_SURVEY", "FICTIONAL special survey", null, null, null, null, null, [new("CERTIFICATE", cert, 2)]),
            new(Guid.NewGuid(), "OTHER", "FICTIONAL paint", null, null, null, null, null, []));
        var facts = new Dictionary<(string, Guid), DrydockTargetFact> {
            [("WORK_ORDER", wo)] = new("WORK_ORDER", wo, true, 3, today.AddDays(-1), false, "WorkOrderDuePassed"),
            [("CERTIFICATE", cert)] = new("CERTIFICATE", cert, true, 2, today.AddDays(90), false) };
        var r = DrydockPolicy.Readiness(content, facts, today);
        Assert.Equal((DrydockPolicy.Attention, 1, 1, 0), (r.State, r.Unlinked, r.Overdue, r.Stale));
        Assert.Equal("LinkageCompletenessOnly;NotASurveyOrClassResult", r.Meaning);
        facts[("CERTIFICATE", cert)] = new("CERTIFICATE", cert, true, 3, today.AddDays(90), false);
        var stale = DrydockPolicy.Readiness(content, facts, today);
        Assert.Equal(DrydockPolicy.ReviewRequired, stale.State);
        Assert.Contains("link_stale", DrydockPolicy.Blockers("PLAN", content, stale));
        facts.Remove(("WORK_ORDER", wo));
        Assert.Contains(DrydockPolicy.Readiness(content, facts, today).Items.SelectMany(i => i.Links), l => l.State == "UNAVAILABLE");
        Assert.Equal(DrydockPolicy.Incomplete, DrydockPolicy.Readiness(Dock(), facts, today).State);
    }

    [Fact]
    [Trait("Requirement", "DEV-111")]
    public void Drydock_lifecycle_needs_plan_data_and_completion_evidence_and_an_independent_verifier()
    {
        Assert.Equal(("PLANNED", (string?)null), DrydockPolicy.Transition("DRAFT", "PLAN"));
        Assert.Equal("invalid_state", DrydockPolicy.Transition("DRAFT", "COMPLETE").Failure);
        var empty = new DrydockContent(null, null, null, null, new(null, null, null, null), [], []);
        var readiness = DrydockPolicy.Readiness(empty, new Dictionary<(string, Guid), DrydockTargetFact>(), new DateOnly(2026, 11, 20));
        Assert.Equal(["planned_finish_missing", "planned_start_missing", "project_reference_missing", "scope_items_missing", "yard_missing"], DrydockPolicy.Blockers("PLAN", empty, readiness));
        Assert.Equal(["completion_evidence_missing"], DrydockPolicy.Blockers("COMPLETE", empty, readiness));
        Assert.False(DrydockPolicy.ValidDraft(empty with { External = new("1,000", "EUR", null, null) }));
        Assert.False(DrydockPolicy.ValidDraft(empty with { External = new(null, "euro", null, null) }));
        var completer = Guid.NewGuid();
        Assert.Equal("separation_of_duties", DrydockPolicy.VerificationFailure("COMPLETED", "VERIFY", completer, completer, "FICTIONAL verified scope"));
        Assert.Null(DrydockPolicy.VerificationFailure("COMPLETED", "VERIFY", Guid.NewGuid(), completer, "FICTIONAL verified scope"));
    }
}
