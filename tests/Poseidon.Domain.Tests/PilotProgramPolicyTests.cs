using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// Pilot program kit (18.01 rows 77-80, 96, 97, 122; 19.01 §2/§7/§11; 63.05 p142-156). The engine only recommends:
// nothing is auto-satisfied, a missing baseline stays UNKNOWN (never 0), EXPAND is refused unless every condition is
// met, and P0 same-day triage is judged on the program's calendar day without an invented hour SLA.
[Trait("Category", "Unit")]
public sealed class PilotProgramPolicyTests
{
    private static readonly Guid Program = Guid.NewGuid(), Ship = Guid.NewGuid(), Tug = Guid.NewGuid();
    private static readonly PilotProgramVessels Vessels = new(Ship, Tug);
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static PilotRecordProbe Probe(Guid? vessel, bool traced = true, string? provenance = "UserDeclaredUnverified", string? completion = PilotProgramPolicy.Completed) =>
        new(vessel, provenance, traced, null, null, null, completion);

    [Fact]
    [Trait("Requirement", "DEV-076")]
    [Trait("Requirement", "DEV-077")]
    [Trait("Requirement", "DEV-078")]
    [Trait("Requirement", "DEV-079")]
    [Trait("Requirement", "DEV-095")]
    public void Every_week_carries_its_source_items_with_closed_record_types_and_unique_codes()
    {
        Assert.Equal(["W1", "W2", "W3", "W4"], PilotProgramCatalog.WeekDefinitions.Select(w => w.Week));
        Assert.Equal(PilotProgramCatalog.Items.Length, PilotProgramCatalog.Items.Select(i => i.Code).Distinct().Count());
        Assert.All(PilotProgramCatalog.Items, i => {
            Assert.NotEmpty(i.RecordTypes);
            Assert.All(i.RecordTypes, t => Assert.Contains(t, PilotProgramCatalog.RecordTypes));
            Assert.StartsWith(i.Week + "_", i.Code);
            Assert.Contains(i.Area, PilotProgramCatalog.Areas);
        });
        // DEV-077 AC: at least one WO, one defect/safeguard, one departure review, one offline/reconnect flow with audit trace.
        var w2 = PilotProgramCatalog.Week("W2")!.Items;
        foreach (var code in new[] { "W2_WORK_ORDER", "W2_DEFECT_SAFEGUARD", "W2_DEPARTURE_REVIEW", "W2_OFFLINE_RECONNECT" })
            Assert.True(w2.Single(i => i.Code == code).RequiresAuditTrace);
        // DEV-095: ship departure/safety + tug job/towage flows.
        Assert.Contains(w2, i => i.Requirement == "DEV-095" && i.RecordTypes.Contains(PilotProgramCatalog.TowageExecution));
        Assert.Contains(w2, i => i.Requirement == "DEV-095" && i.RecordTypes.Contains(PilotProgramCatalog.TugJob));
        // DEV-121 signed UAT for both modes.
        Assert.Equal(2, PilotProgramCatalog.Week("W4")!.Items.Count(i => i.RecordTypes.SequenceEqual([PilotProgramCatalog.UatRun])));
    }

    [Fact]
    [Trait("Requirement", "DEV-077")]
    public void A_link_is_valid_only_for_an_existing_real_traced_record_of_the_program_vessel()
    {
        var wo = PilotProgramCatalog.Item("W2_WORK_ORDER")!;
        Assert.Null(PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.WorkOrder, Probe(Ship), Vessels, Program));
        Assert.Equal("record_type_not_allowed", PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.Defect, Probe(Ship), Vessels, Program));
        Assert.Equal("record_not_found", PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.WorkOrder, null, Vessels, Program));
        Assert.Equal("synthetic_record_not_linkable", PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.WorkOrder, Probe(Ship, provenance: "SyntheticUnverified"), Vessels, Program));
        Assert.Equal("record_vessel_mismatch", PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.WorkOrder, Probe(Tug), Vessels, Program));
        Assert.Equal("audit_trace_missing", PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.WorkOrder, Probe(Ship, traced: false), Vessels, Program));
        var towage = PilotProgramCatalog.Item("W2_DUAL_TUG_TOWAGE")!;
        Assert.Null(PilotProgramPolicy.LinkRefusal(towage, PilotProgramCatalog.TowageExecution, Probe(Tug), Vessels, Program));
        Assert.Equal("tug_vessel_unset", PilotProgramPolicy.LinkRefusal(towage, PilotProgramCatalog.TowageExecution, Probe(Tug), new(Ship, null), Program));
        var shared = PilotProgramCatalog.Item("W2_DUAL_SHARED_EVIDENCE")!;
        Assert.Null(PilotProgramPolicy.LinkRefusal(shared, PilotProgramCatalog.AuditPack, Probe(Tug), Vessels, Program));
        Assert.Equal("record_vessel_mismatch", PilotProgramPolicy.LinkRefusal(shared, PilotProgramCatalog.AuditPack, Probe(Guid.NewGuid()), Vessels, Program));
    }

    [Fact]
    [Trait("Requirement", "DEV-077")]
    [Trait("Requirement", "DEV-078")]
    public void Completed_flows_need_the_owner_completion_state_and_an_audit_trace_alone_never_completes_an_item()
    {
        // DEV-077 AC says "completed with audit trace" for the WO, defect/safeguard, departure review and offline flow.
        var w2 = PilotProgramCatalog.Week("W2")!.Items;
        Assert.Equal(["W2_WORK_ORDER", "W2_DEFECT_SAFEGUARD", "W2_DEPARTURE_REVIEW", "W2_OFFLINE_RECONNECT"],
            PilotProgramCatalog.Items.Where(i => i.RequiresCompletion).Select(i => i.Code));
        Assert.All(w2.Where(i => i.RequiresCompletion), i => Assert.True(i.RequiresAuditTrace));
        var wo = PilotProgramCatalog.Item("W2_WORK_ORDER")!;
        // A traced (human-written) but open work order is not a completed flow.
        Assert.Equal("record_not_completed", PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.WorkOrder, Probe(Ship, completion: PilotProgramPolicy.NotCompleted), Vessels, Program));
        // A record type with no completion state never satisfies a completion item.
        Assert.Equal("record_not_completed", PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.WorkOrder, Probe(Ship, completion: null), Vessels, Program));
        Assert.Equal("audit_trace_missing", PilotProgramPolicy.LinkRefusal(wo, PilotProgramCatalog.WorkOrder, Probe(Ship, traced: false), Vessels, Program));
        // Items whose AC does not say "completed" (e.g. "at least one event flow") rest on the human-written trace only.
        var events = PilotProgramCatalog.Item("W3_EVENT_FLOW")!;
        Assert.False(events.RequiresCompletion);
        Assert.Null(PilotProgramPolicy.LinkRefusal(events, PilotProgramCatalog.SafetyEvent, Probe(Ship, completion: null), Vessels, Program));
        // Completion is re-evaluated on read: a link to a not-completed record leaves the item LINK_INVALID.
        var week = PilotProgramPolicy.EvaluateWeek(PilotProgramCatalog.Week("W2")!, PilotProgramPolicy.InProgress,
            [new(Guid.NewGuid(), wo.Code, PilotProgramCatalog.WorkOrder, Guid.NewGuid(), Guid.NewGuid(), Probe(Ship, completion: PilotProgramPolicy.NotCompleted))], [], Vessels, Program);
        var item = week.Items.Single(i => i.Code == wo.Code);
        Assert.Equal((PilotProgramPolicy.LinkInvalid, 0), (item.State, item.ValidLinks));
        Assert.Contains("LINK_RECORD_NOT_COMPLETED", item.Reasons);
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    [Trait("Requirement", "DEV-079")]
    [Trait("Requirement", "DEV-121")]
    public void Week_close_and_program_decision_exclude_the_authors_of_the_evidence_they_rest_on()
    {
        Guid po = Guid.NewGuid(), other = Guid.NewGuid();
        var w1 = PilotProgramCatalog.Week("W1")!; var w2 = PilotProgramCatalog.Week("W2")!; var w4 = PilotProgramCatalog.Week("W4")!;
        Assert.Equal([false, false, false, true], PilotProgramCatalog.WeekDefinitions.Select(PilotProgramPolicy.UsesReadout));
        PilotLinkInput Link(string item, string type, Guid record, Guid actor) => new(Guid.NewGuid(), item, type, record, actor, null);
        Assert.Empty(PilotProgramPolicy.WeekCloseDutyConflicts(w1, po, [], [], [], []));
        Assert.Empty(PilotProgramPolicy.DecisionDutyConflicts(po, [], [], []));

        // Link author of the week.
        Assert.Equal([PilotProgramPolicy.LinkAuthor],
            PilotProgramPolicy.WeekCloseDutyConflicts(w1, po, [Link("W1_SHIP_CREW", PilotProgramCatalog.CrewAssignment, Guid.NewGuid(), po)], [], [], []));
        // A link in another week does not bind this week's close, but binds the program decision.
        var w2Link = Link("W2_WORK_ORDER", PilotProgramCatalog.WorkOrder, Guid.NewGuid(), po);
        Assert.Empty(PilotProgramPolicy.WeekCloseDutyConflicts(w1, po, [w2Link], [], [], []));
        Assert.Equal([PilotProgramPolicy.LinkAuthor], PilotProgramPolicy.DecisionDutyConflicts(po, [w2Link], [], []));

        // Feedback recorded for the week, or feedback of another week linked from it.
        var own = new PilotAuthoredRecord(Guid.NewGuid(), "W1", po);
        Assert.Equal([PilotProgramPolicy.FeedbackAuthor], PilotProgramPolicy.WeekCloseDutyConflicts(w1, po, [], [own], [], []));
        Assert.Empty(PilotProgramPolicy.WeekCloseDutyConflicts(w2, po, [], [own], [], []));
        var later = new PilotAuthoredRecord(Guid.NewGuid(), "W2", po);
        Assert.Equal([PilotProgramPolicy.FeedbackAuthor],
            PilotProgramPolicy.WeekCloseDutyConflicts(w1, po, [Link("W1_DAILY_FEEDBACK", PilotProgramCatalog.PilotFeedback, later.Id, other)], [later], [], []));

        // Measurements: W4 uses the readout (latest per metric/mode/phase); other weeks only through a link.
        var latest = new PilotAuthoredRecord(Guid.NewGuid(), null, po); var superseded = new PilotAuthoredRecord(Guid.NewGuid(), null, po);
        Assert.Equal([PilotProgramPolicy.MeasurementAuthor], PilotProgramPolicy.WeekCloseDutyConflicts(w4, po, [], [], [latest], [latest, superseded]));
        Assert.Empty(PilotProgramPolicy.WeekCloseDutyConflicts(w1, po, [], [], [latest], [latest, superseded]));
        Assert.Empty(PilotProgramPolicy.WeekCloseDutyConflicts(w4, po, [], [], [], [superseded]));
        var linked = Link("W4_BEFORE_AFTER_BASELINE", PilotProgramCatalog.PilotMeasurement, superseded.Id, other);
        Assert.Equal([PilotProgramPolicy.MeasurementAuthor], PilotProgramPolicy.WeekCloseDutyConflicts(w4, po, [linked], [], [], [latest, superseded]));
        Assert.Equal([PilotProgramPolicy.MeasurementAuthor], PilotProgramPolicy.DecisionDutyConflicts(po, [], [latest], [latest]));
        Assert.Equal([PilotProgramPolicy.MeasurementAuthor], PilotProgramPolicy.DecisionDutyConflicts(po, [linked], [], [superseded]));
        Assert.Empty(PilotProgramPolicy.DecisionDutyConflicts(po, [], [], [superseded]));
        // Both sets at once; another person's records never bind the Product Owner.
        Assert.Equal([PilotProgramPolicy.LinkAuthor, PilotProgramPolicy.MeasurementAuthor], PilotProgramPolicy.DecisionDutyConflicts(po, [w2Link], [latest], [latest]));
        var log = Link("W4_TUG_DAILY_LOG", PilotProgramCatalog.TugDailyLog, Guid.NewGuid(), po);
        Assert.Equal([PilotProgramPolicy.LinkAuthor, PilotProgramPolicy.FeedbackAuthor, PilotProgramPolicy.MeasurementAuthor],
            PilotProgramPolicy.WeekCloseDutyConflicts(w4, po, [log], [new(Guid.NewGuid(), "W4", po)], [latest], [latest]));
        Assert.Empty(PilotProgramPolicy.WeekCloseDutyConflicts(w4, other, [log], [], [latest], [latest]));
        Assert.Empty(PilotProgramPolicy.DecisionDutyConflicts(other, [log], [latest], [latest]));
    }

    [Fact]
    [Trait("Requirement", "DEV-121")]
    public void Uat_sign_off_needs_an_attested_run_of_the_same_mode_and_program_records_must_belong_to_the_program()
    {
        var ship = PilotProgramCatalog.Item("W4_SHIP_UAT_SIGNOFF")!;
        PilotRecordProbe Run(string scope, string state) => new(null, "UserDeclaredUnverified", false, scope, state, null);
        Assert.Null(PilotProgramPolicy.LinkRefusal(ship, PilotProgramCatalog.UatRun, Run("SHIP", "ATTESTED"), Vessels, Program));
        Assert.Equal("uat_run_not_attested", PilotProgramPolicy.LinkRefusal(ship, PilotProgramCatalog.UatRun, Run("SHIP", "SUBMITTED"), Vessels, Program));
        Assert.Equal("record_scope_mismatch", PilotProgramPolicy.LinkRefusal(ship, PilotProgramCatalog.UatRun, Run("TUG", "ATTESTED"), Vessels, Program));
        var feedback = PilotProgramCatalog.Item("W1_DAILY_FEEDBACK")!;
        Assert.Null(PilotProgramPolicy.LinkRefusal(feedback, PilotProgramCatalog.PilotFeedback, new(null, null, false, null, null, Program), Vessels, Program));
        Assert.Equal("record_scope_mismatch", PilotProgramPolicy.LinkRefusal(feedback, PilotProgramCatalog.PilotFeedback, new(null, null, false, null, null, Guid.NewGuid()), Vessels, Program));
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    public void A_week_without_links_is_never_ready_and_its_hash_changes_with_the_evidence()
    {
        var w1 = PilotProgramCatalog.Week("W1")!;
        var empty = PilotProgramPolicy.EvaluateWeek(w1, PilotProgramPolicy.InProgress, [], [], Vessels, Program);
        Assert.Equal(PilotProgramPolicy.NotReady, empty.Recommendation);
        Assert.All(empty.Items, i => Assert.Equal(PilotProgramPolicy.Missing, i.State));
        Assert.Equal("week_not_ready", PilotProgramPolicy.CloseRefusal(PilotProgramPolicy.Accepted, empty));
        Assert.Null(PilotProgramPolicy.CloseRefusal(PilotProgramPolicy.NotAccepted, empty));
        var notStarted = PilotProgramPolicy.EvaluateWeek(w1, PilotProgramPolicy.NotStarted, [], [], Vessels, Program);
        Assert.Equal("week_not_started", PilotProgramPolicy.CloseRefusal(PilotProgramPolicy.NotAccepted, notStarted));

        var links = w1.Items.Select(i => new PilotLinkInput(Guid.NewGuid(), i.Code, i.RecordTypes[0], Guid.NewGuid(), Guid.NewGuid(),
            i.RecordTypes[0] == PilotProgramCatalog.PilotFeedback ? new PilotRecordProbe(null, null, false, null, null, Program) :
            Probe(i.Area == PilotProgramCatalog.Tug ? Tug : Ship))).ToArray();
        var ready = PilotProgramPolicy.EvaluateWeek(w1, PilotProgramPolicy.InProgress, links, [], Vessels, Program);
        Assert.Equal(PilotProgramPolicy.ReadyForClose, ready.Recommendation);
        Assert.Null(PilotProgramPolicy.CloseRefusal(PilotProgramPolicy.Accepted, ready));
        Assert.NotEqual(empty.Hash, ready.Hash);
        // A deleted record (probe null) turns the item into LINK_INVALID; never silently satisfied.
        var gone = PilotProgramPolicy.EvaluateWeek(w1, PilotProgramPolicy.InProgress, [links[0] with { Probe = null }, .. links[1..]], [], Vessels, Program);
        Assert.Equal(PilotProgramPolicy.LinkInvalid, gone.Items[0].State);
        Assert.Contains("LINK_RECORD_NOT_FOUND", gone.Items[0].Reasons);
        Assert.Equal(PilotProgramPolicy.NotReady, gone.Recommendation);
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    public void P0_same_day_triage_uses_the_program_calendar_day_and_only_source_named_categories()
    {
        var reported = new DateTimeOffset(2026, 12, 1, 20, 30, 0, TimeSpan.Zero); // 23:30 in Istanbul
        Assert.Equal(PilotIssuePolicy.SameDay, PilotIssuePolicy.Timing("P0", "CREW", reported, reported.AddMinutes(20), reported.AddHours(1), Istanbul).State);
        // 21:05 UTC is already the next calendar day in Istanbul.
        Assert.Equal(PilotIssuePolicy.Late, PilotIssuePolicy.Timing("P0", "CREW", reported, reported.AddMinutes(35), reported.AddHours(1), Istanbul).State);
        Assert.Equal(PilotIssuePolicy.DueToday, PilotIssuePolicy.Timing("P0", "CERTIFICATE", reported, null, reported.AddMinutes(10), Istanbul).State);
        Assert.Equal(PilotIssuePolicy.Overdue, PilotIssuePolicy.Timing("P0", "ONBOARDING", reported, null, reported.AddHours(1), Istanbul).State);
        Assert.Equal(PilotIssuePolicy.NoTarget, PilotIssuePolicy.Timing("P1", "CREW", reported, null, reported.AddDays(3), Istanbul).State);
        Assert.Equal(PilotIssuePolicy.NoTarget, PilotIssuePolicy.Timing("P0", "PMS", reported, null, reported.AddDays(3), Istanbul).State);

        var w1 = PilotProgramCatalog.Week("W1")!;
        var overdue = new PilotIssueInput(Guid.NewGuid(), "W1", "P0", "CREW", "OPEN", PilotIssuePolicy.Timing("P0", "CREW", reported, null, reported.AddDays(1), Istanbul));
        var week = PilotProgramPolicy.EvaluateWeek(w1, PilotProgramPolicy.InProgress, [], [overdue], Vessels, Program);
        Assert.Equal((1, 1), (week.OpenP0, week.OverdueP0));
        Assert.Contains("P0_TRIAGE_OVERDUE", week.Reasons);
    }

    [Fact]
    [Trait("Requirement", "DEV-079")]
    [Trait("Requirement", "DEV-096")]
    [Trait("Requirement", "DEV-121")]
    public void Missing_baselines_stay_unknown_with_null_values_and_percent_change_needs_a_positive_baseline()
    {
        var rows = PilotProgramPolicy.Readout([], _ => new PilotTelemetryCell("SUPPRESSED", null, "person"));
        Assert.Equal(PilotProgramCatalog.ValueMetrics.Length, rows.Length);
        Assert.All(rows, r => {
            Assert.Equal(PilotProgramPolicy.Unknown, r.Quality);
            Assert.Null(r.Baseline); Assert.Null(r.Current); Assert.Null(r.Delta); Assert.Null(r.PercentChange);
            Assert.Equal(PilotProgramPolicy.NotComparable, r.Comparison);
        });
        // DEV-072 telemetry is context only: SUPPRESSED stays SUPPRESSED and never fills the measurement.
        var adoption = rows.Single(r => r.MetricId == "TUG_USER_ADOPTION");
        Assert.Equal(("SUPPRESSED", (string?)null), (adoption.Telemetry!.State, adoption.Telemetry.Value));
        Assert.Null(rows.Single(r => r.MetricId == "TUG_JOB_PREP_TIME").Telemetry);

        PilotMeasurementInput M(string id, string phase, decimal v, string unit) => new(id, PilotProgramCatalog.Metric(id)!.Mode, phase, v, unit, 10, Guid.NewGuid(), Guid.NewGuid());
        var measured = PilotProgramPolicy.Readout([M("TUG_JOB_PREP_TIME", "BASELINE", 1800, "SECOND"), M("TUG_JOB_PREP_TIME", "PILOT", 1200, "SECOND"),
            M("SHIP_DUPLICATE_ENTRY", "BASELINE", 0, "COUNT"), M("SHIP_DUPLICATE_ENTRY", "PILOT", 2, "COUNT"), M("SHIP_READINESS_ACCURACY", "PILOT", 90, "PERCENT")], _ => null);
        var prep = measured.Single(r => r.MetricId == "TUG_JOB_PREP_TIME");
        Assert.Equal((PilotProgramPolicy.Complete, -600m, -33.3m, PilotProgramPolicy.Improved), (prep.Quality, prep.Delta, prep.PercentChange, prep.Comparison));
        var duplicate = measured.Single(r => r.MetricId == "SHIP_DUPLICATE_ENTRY");
        Assert.Equal((2m, (decimal?)null, PilotProgramPolicy.Worsened), (duplicate.Delta!.Value, duplicate.PercentChange, duplicate.Comparison));
        Assert.Contains("PERCENT_CHANGE_UNDEFINED_BASELINE_NOT_POSITIVE", duplicate.Reasons);
        var accuracy = measured.Single(r => r.MetricId == "SHIP_READINESS_ACCURACY");
        Assert.Equal((PilotProgramPolicy.Unknown, (decimal?)null), (accuracy.Quality, accuracy.Delta));
        Assert.Contains("BASELINE_MISSING", accuracy.Reasons);
    }

    [Fact]
    [Trait("Requirement", "DEV-079")]
    [Trait("Requirement", "DEV-096")]
    public void Expand_is_refused_unless_every_week_is_accepted_and_values_uat_defects_and_p0_are_clear()
    {
        var unknown = PilotProgramPolicy.Readout([], _ => null);
        var defects = new[] { new PilotDefectSeverity("SHIP", "COMPLETE", 0, 0, 0, 0, 0, 0), new PilotDefectSeverity("TUG", "UNKNOWN", 0, 0, 0, 0, 0, 0) };
        var review = PilotProgramPolicy.EvaluateProgram([], unknown, "UNKNOWN", "FAIL", defects, 1);
        Assert.Equal(PilotProgramPolicy.FixRecommended, review.Recommendation);
        Assert.Contains("WEEK_NOT_CLOSED:W1", review.Reasons);
        Assert.Contains("UAT_TUG:FAIL", review.Reasons);
        Assert.Contains("DEFECTS_UNKNOWN:TUG", review.Reasons);
        Assert.Contains("P0_ISSUES_OPEN", review.Reasons);
        Assert.Equal("expand_not_eligible", PilotProgramPolicy.DecisionRefusal("EXPAND", review));
        foreach (var d in new[] { "CONTINUE", "FIX", "STOP" }) Assert.Null(PilotProgramPolicy.DecisionRefusal(d, review));
        Assert.Equal("validation_failed", PilotProgramPolicy.DecisionRefusal("GO", review));

        var all = PilotProgramCatalog.ValueMetrics.SelectMany(m => new[] {
            new PilotMeasurementInput(m.Id, m.Mode, "BASELINE", 10, m.Unit, 5, Guid.NewGuid(), Guid.NewGuid()),
            new PilotMeasurementInput(m.Id, m.Mode, "PILOT", 8, m.Unit, 5, Guid.NewGuid(), Guid.NewGuid()) }).ToArray();
        var closes = PilotProgramCatalog.Weeks.Select(w => new PilotWeekCloseInput(w, "ACCEPTED", new string('A', 64))).ToArray();
        var clear = new[] { new PilotDefectSeverity("SHIP", "COMPLETE", 0, 1, 0, 0, 0, 2), new PilotDefectSeverity("TUG", "COMPLETE", 0, 0, 0, 0, 0, 0) };
        var eligible = PilotProgramPolicy.EvaluateProgram(closes, PilotProgramPolicy.Readout(all, _ => null), "PASS", "PASS", clear, 0);
        Assert.Equal(PilotProgramPolicy.ExpandEligible, eligible.Recommendation);
        Assert.Null(PilotProgramPolicy.DecisionRefusal("EXPAND", eligible));
        var critical = PilotProgramPolicy.EvaluateProgram(closes, PilotProgramPolicy.Readout(all, _ => null), "PASS", "PASS",
            [new PilotDefectSeverity("SHIP", "COMPLETE", 1, 0, 0, 0, 0, 0), clear[1]], 0);
        Assert.Contains("CRITICAL_DEFECTS_UNRESOLVED:SHIP", critical.Reasons);
        Assert.NotEqual(eligible.Hash, critical.Hash);
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    public void Feedback_free_text_refuses_obvious_personal_data_and_values_are_range_checked()
    {
        Assert.True(PilotFeedbackPolicy.SuspectedPersonalData("contact jane@example.com"));
        Assert.True(PilotFeedbackPolicy.SuspectedPersonalData("call +90 532 123 45 67"));
        Assert.False(PilotFeedbackPolicy.SuspectedPersonalData("Departure screen slow on WO 12 after sync"));
        Assert.True(PilotMeasurementPolicy.ValidValue("PERCENT", 99.5m));
        Assert.False(PilotMeasurementPolicy.ValidValue("PERCENT", 100.01m));
        Assert.False(PilotMeasurementPolicy.ValidValue("PERSON", 2.5m));
        Assert.False(PilotMeasurementPolicy.ValidValue("SECOND", -1m));
        Assert.False(PilotMeasurementPolicy.ValidValue("SECOND", 1.234m));
    }
}
