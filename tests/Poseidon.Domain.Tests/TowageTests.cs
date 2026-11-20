using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

// S8 WP-D: DEV-087 / T-TUG-008 (18.01 row 88), DEV-088 / T-TUG-009 (row 89), DEV-107 / T-RISK2-003 (row 108).
// FICTIONAL values only. No case produces a GO, a permit, an expert limit or a signed decision.
public sealed class TowageTests
{
    private static readonly Guid Tug = Guid.Parse("0e000000-0000-4000-8000-000000000001"), Other = Guid.Parse("0e000000-0000-4000-8000-000000000002");
    private static readonly Guid Author = Guid.NewGuid(), Approver = Guid.NewGuid(), Crew = Guid.NewGuid();

    private static TowagePlanContent EmptyPlan() => new([], null, [], new(null, null, null, null), new(null, null), [], new(null, []));
    private static TowagePlanContent CompletePlan(Guid? release = null) => new(
        [new(Tug, "CENTRE_LEAD_FORWARD", "TOWING_ON_LINE", true, "TUG_LINE", "FICTIONAL centre lead fwd", null, release ?? Guid.NewGuid())],
        "FICTIONAL master/pilot/tug master exchange",
        [new("WIND_SPEED", "25", "FICTIONAL company SMS ref"), new("VISIBILITY", "0.5", "FICTIONAL port guideline ref")],
        new("12", "14", "English", "FICTIONAL lost comms: stop and hold position"),
        new("FICTIONAL release on master order via winch brake", "Tug Master"),
        [new(Guid.NewGuid(), "TOWLINE_PARTING", "FICTIONAL standby tug connects")],
        new(true, [new(Guid.NewGuid(), "FICTIONAL deck clear of bight")]));
    private static TowageBriefingContent CompleteBriefing(Guid risk) => new("FICTIONAL wind SW 10 kn", "FICTIONAL 1 kn ebb", "FICTIONAL > 2 nm",
        [new(Guid.NewGuid(), "FICTIONAL girting")], [new(Guid.NewGuid(), "FICTIONAL tow angle limits")], [new(Guid.NewGuid(), "FICTIONAL emergency release")],
        "FICTIONAL anyone may stop", [new(Guid.NewGuid(), "FICTIONAL line under load near crew")], "FICTIONAL muster and VHF 16", risk,
        [new(Crew, "DECK_CREW", Tug)]);
    private static TowagePlanFacts Approved(TowagePlanContent c, long revision = 4, long job = 2) => new(Guid.NewGuid(), revision, TowagePlanPolicy.Approved, revision, job, c);

    // ---------------- DEV-087 plan ----------------
    [Fact]
    public void Incomplete_draft_is_storable_but_submit_lists_every_missing_section() {
        Assert.True(TowagePlanPolicy.ValidDraft(EmptyPlan()));
        var blockers = TowagePlanPolicy.SubmitBlockers(EmptyPlan(), []);
        foreach (var code in new[] { "tug_roles_missing", "coordinating_tug_required", "information_exchange_missing", "limits_missing", "primary_channel_missing",
            "standby_channel_missing", "working_language_missing", "lost_communication_procedure_missing", "emergency_release_procedure_missing",
            "emergency_release_authority_missing", "contingency_missing", "snap_back_zone_unknown", "snap_back_controls_missing" })
            Assert.Contains(code, blockers);
        Assert.Equal("towage_record_incomplete", TowagePlanPolicy.Submit(TowagePlanPolicy.Draft, blockers).Failure);
    }

    [Fact]
    public void Complete_plan_submits_only_when_every_tug_has_a_current_assignment_proposal() {
        var plan = CompletePlan();
        Assert.Equal(["tug_assignment_missing"], TowagePlanPolicy.SubmitBlockers(plan, []));
        Assert.Empty(TowagePlanPolicy.SubmitBlockers(plan, [Tug]));
        Assert.Equal(TowagePlanPolicy.InReview, TowagePlanPolicy.Submit(TowagePlanPolicy.Draft, []).ToState);
        Assert.Equal("invalid_state", TowagePlanPolicy.Submit(TowagePlanPolicy.Approved, []).Failure);
    }

    [Fact]
    public void Limits_are_inputs_without_defaults_and_need_a_value_and_a_source() {
        var noValue = CompletePlan() with { Limits = [new("WIND_SPEED", null, null)] };
        Assert.True(TowagePlanPolicy.ValidDraft(noValue));
        var b = TowagePlanPolicy.SubmitBlockers(noValue, [Tug]);
        Assert.Contains("limit_value_missing", b); Assert.Contains("limit_source_missing", b);
        Assert.False(TowagePlanPolicy.ValidDraft(CompletePlan() with { Limits = [new("WIND_SPEED", "-3", "x")] }));
        Assert.False(TowagePlanPolicy.ValidDraft(CompletePlan() with { Limits = [new("UNLISTED", "3", "x")] }));
        Assert.False(TowagePlanPolicy.ValidDraft(CompletePlan() with { Limits = [new("WIND_SPEED", "3", "x"), new("WIND_SPEED", "4", "y")] }));
        Assert.Equal("kn", TowagePlanPolicy.LimitUnits["WIND_SPEED"]);
    }

    [Fact]
    public void Towing_on_a_line_requires_line_source_connection_point_and_linked_emergency_release() {
        var t = CompletePlan().Tugs[0] with { LineSource = null, ConnectionPoint = null, EmergencyReleaseItemId = null };
        var b = TowagePlanPolicy.SubmitBlockers(CompletePlan() with { Tugs = [t] }, [Tug]);
        Assert.Contains("towline_source_missing", b); Assert.Contains("connection_point_missing", b); Assert.Contains("emergency_release_equipment_unlinked", b);
        var pushing = t with { Method = "PUSHING_ONLY" };
        Assert.Empty(TowagePlanPolicy.SubmitBlockers(CompletePlan() with { Tugs = [pushing] }, [Tug]));
        var two = CompletePlan() with { Tugs = [CompletePlan().Tugs[0], CompletePlan().Tugs[0] with { TugId = Other }] };
        Assert.Contains("coordinating_tug_required", TowagePlanPolicy.SubmitBlockers(two, [Tug, Other]));
    }

    [Fact]
    public void Approval_is_a_separate_human_record_never_by_an_author_or_the_submitter() {
        Assert.Equal("separation_of_duties", TowagePlanPolicy.Review(TowagePlanPolicy.InReview, "APPROVE", Author, [Author], Author, "FICTIONAL plan reviewed").Failure);
        Assert.Equal("separation_of_duties", TowagePlanPolicy.Review(TowagePlanPolicy.InReview, "APPROVE", Approver, [Author], Approver, "FICTIONAL plan reviewed").Failure);
        Assert.Equal("invalid_reason", TowagePlanPolicy.Review(TowagePlanPolicy.InReview, "APPROVE", Approver, [Author], Author, "short").Failure);
        Assert.Equal("invalid_state", TowagePlanPolicy.Review(TowagePlanPolicy.Draft, "APPROVE", Approver, [Author], Author, "FICTIONAL plan reviewed").Failure);
        Assert.Equal(TowagePlanPolicy.Approved, TowagePlanPolicy.Review(TowagePlanPolicy.InReview, "APPROVE", Approver, [Author], Author, "FICTIONAL plan reviewed").ToState);
        Assert.Equal(TowagePlanPolicy.Returned, TowagePlanPolicy.Review(TowagePlanPolicy.InReview, "RETURN", Approver, [Author], Author, "FICTIONAL needs contingency").ToState);
        Assert.Equal(TowagePlanPolicy.Draft, TowagePlanPolicy.Amend(TowagePlanPolicy.Approved, "FICTIONAL tug change after approval").ToState);
    }

    [Fact]
    public void Communication_input_is_pass_only_for_a_current_approved_plan_covering_the_tug() {
        Assert.Equal(("UNKNOWN", "CommunicationSourceUnavailable"), Pair(TowagePlanPolicy.Communication(null, Tug, 2)));
        var draft = new TowagePlanFacts(Guid.NewGuid(), 3, TowagePlanPolicy.InReview, null, 2, CompletePlan());
        Assert.Equal(("UNKNOWN", "TowagePlanNotApproved"), Pair(TowagePlanPolicy.Communication(draft, Tug, 2)));
        Assert.Equal(("UNKNOWN", "TowagePlanRequestChanged"), Pair(TowagePlanPolicy.Communication(Approved(CompletePlan()), Tug, 3)));
        Assert.Equal(("UNKNOWN", "TugNotInApprovedTowagePlan"), Pair(TowagePlanPolicy.Communication(Approved(CompletePlan()), Other, 2)));
        Assert.Equal(("PASS", "ApprovedTowagePlanCommunication"), Pair(TowagePlanPolicy.Communication(Approved(CompletePlan()), Tug, 2)));
        var stale = Approved(CompletePlan()) with { ApprovedRevision = 3 };
        Assert.Equal("UNKNOWN", TowagePlanPolicy.Communication(stale, Tug, 2).Result);
    }
    private static (string, string) Pair(TugReadinessCheck c) => (c.Result, c.Reason);

    // ---------------- DEV-088 briefing ----------------
    private static readonly TowageRiskFacts ApprovedRisk = new(Guid.NewGuid(), RiskPolicy.Approved, 5, 5);

    [Fact]
    public void Issue_requires_every_section_an_approved_plan_and_an_approved_risk_assessment() {
        var empty = new TowageBriefingContent(null, null, null, [], [], [], null, [], null, null, []);
        Assert.True(TowageBriefingPolicy.ValidDraft(empty));
        var b = TowageBriefingPolicy.IssueBlockers(empty, null, null);
        foreach (var code in new[] { "weather_missing", "current_missing", "visibility_missing", "hazard_missing", "preventive_barrier_missing", "mitigating_barrier_missing",
            "stop_work_authority_missing", "stop_work_trigger_missing", "emergency_arrangements_missing", "participants_missing", "towage_plan_not_approved", "risk_assessment_unlinked" })
            Assert.Contains(code, b);
        var content = CompleteBriefing(ApprovedRisk.Id);
        Assert.Empty(TowageBriefingPolicy.IssueBlockers(content, Approved(CompletePlan()), ApprovedRisk));
        Assert.Equal(["risk_assessment_not_approved"], TowageBriefingPolicy.IssueBlockers(content, Approved(CompletePlan()), ApprovedRisk with { State = RiskPolicy.InReview }));
    }

    [Fact]
    public void Incomplete_briefing_is_fail_and_blocks_ready_in_the_readiness_engine() {
        var plan = Approved(CompletePlan());
        var content = CompleteBriefing(ApprovedRisk.Id);
        var missing = TowageBriefingPolicy.Checks(null, plan, ApprovedRisk, true);
        Assert.Equal(("FAIL", "TowageBriefingMissing"), Pair(Assert.Single(missing)));
        var draft = new TowageBriefingFacts(Guid.NewGuid(), 1, TowageBriefingPolicy.Draft, null, null, content, []);
        Assert.Equal("TowageBriefingNotIssued", Assert.Single(TowageBriefingPolicy.Checks(draft, plan, ApprovedRisk, true)).Reason);
        var issued = new TowageBriefingFacts(Guid.NewGuid(), 2, TowageBriefingPolicy.Issued, plan.Revision, ApprovedRisk.Revision, content, []);
        Assert.Contains(TowageBriefingPolicy.Checks(issued, plan, ApprovedRisk, true), c => c.Reason == "BriefingAcknowledgementPending" && c.Result == "FAIL");
        var all = TugReadinessEngine.Categories.Where(c => c != "briefing").Select(c => new TugReadinessCheck(c, TugReadinessEngine.Pass, "FictionalPass"))
            .Concat(TowageBriefingPolicy.Checks(issued, plan, ApprovedRisk, true)).ToArray();
        Assert.Equal(TugReadinessEngine.NotReady, TugReadinessEngine.Evaluate(all).State);
        var acked = issued with { CurrentAcknowledgements = [(Crew, "ACK")] };
        Assert.Equal(("PASS", "BriefingIssuedAndAcknowledgedByAllParticipants"), Pair(Assert.Single(TowageBriefingPolicy.Checks(acked, plan, ApprovedRisk, true))));
        Assert.Contains(TowageBriefingPolicy.Checks(acked with { CurrentAcknowledgements = [(Crew, "DISPUTE")] }, plan, ApprovedRisk, true), c => c.Reason == "BriefingAcknowledgementDisputed");
        // A changed plan or risk revision after issue makes the briefing stale (re-brief), and hidden risk is UNKNOWN.
        Assert.Contains(TowageBriefingPolicy.Checks(acked, plan with { ApprovedRevision = 9, Revision = 9 }, ApprovedRisk, true), c => c.Reason == "TowageBriefingPlanChanged");
        Assert.Contains(TowageBriefingPolicy.Checks(acked, plan, ApprovedRisk with { Revision = 6, ApprovedRevision = 6 }, true), c => c.Reason == "TowageRiskAssessmentChanged");
        Assert.Contains(TowageBriefingPolicy.Checks(acked, plan, null, false), c => c.Reason == "RiskAccessRequired" && c.Result == "UNKNOWN");
    }

    [Fact]
    public void Each_participant_acknowledges_only_for_themselves_on_the_issued_revision() {
        var content = CompleteBriefing(ApprovedRisk.Id);
        Assert.Null(TowageBriefingPolicy.AcknowledgementFailure(TowageBriefingPolicy.Issued, 2, 2, content, Crew, "ACK", null));
        Assert.Equal("acknowledgment_role_required", TowageBriefingPolicy.AcknowledgementFailure(TowageBriefingPolicy.Issued, 2, 2, content, Author, "ACK", null));
        Assert.Equal("invalid_state", TowageBriefingPolicy.AcknowledgementFailure(TowageBriefingPolicy.Issued, 3, 2, content, Crew, "ACK", null));
        Assert.Equal("invalid_state", TowageBriefingPolicy.AcknowledgementFailure(TowageBriefingPolicy.Draft, 2, 2, content, Crew, "ACK", null));
        Assert.Equal("invalid_reason", TowageBriefingPolicy.AcknowledgementFailure(TowageBriefingPolicy.Issued, 2, 2, content, Crew, "DISPUTE", null));
        Assert.False(TowageBriefingPolicy.ValidDraft(content with { Participants = [new(Crew, "DECK_CREW", Tug), new(Crew, "MATE", Tug)] }));
    }

    [Fact]
    public void Risk_state_maps_the_linked_assessment_without_assuming_ready() {
        Assert.Equal("REVIEW_REQUIRED", TowageBriefingPolicy.RiskState(null, false, null).State);
        Assert.Equal("BLOCKED", TowageBriefingPolicy.RiskState(null, true, null).State);
        Assert.Equal("BLOCKED", TowageBriefingPolicy.RiskState(ApprovedRisk with { State = RiskPolicy.Draft, ApprovedRevision = null }, true, null).State);
        Assert.Equal("REVIEW_REQUIRED", TowageBriefingPolicy.RiskState(ApprovedRisk with { State = RiskPolicy.InReview }, true, null).State);
        Assert.Equal("ATTENTION", TowageBriefingPolicy.RiskState(ApprovedRisk, true, "PENDING").State);
        Assert.Equal("BLOCKED", TowageBriefingPolicy.RiskState(ApprovedRisk, true, "DISPUTED").State);
        Assert.Equal("READY", TowageBriefingPolicy.RiskState(ApprovedRisk, true, "COMPLETE").State);
    }

    // ---------------- DEV-107 high-risk work ----------------
    [Fact]
    public void No_score_hides_a_critical_blocker_and_permit_unknown_never_yields_none() {
        HighRiskDimension[] mostlyFine = [new("risk", "READY", "RiskAssessmentApprovedAndAcknowledged"), new("crew", "READY", "x"), new("equipment", "READY", "x"),
            new("critical_barrier", "HEALTHY", "x"), new("briefing", "BLOCKED", "BriefingAcknowledgementPending")];
        Assert.Equal(0, HighRiskWorkPolicy.Rank(mostlyFine));
        Assert.Equal(1, HighRiskWorkPolicy.Count(mostlyFine, "CRITICAL"));
        Assert.Equal("COMPLETE_REQUIRED", HighRiskWorkPolicy.NextAction(mostlyFine).Kind);
        HighRiskDimension[] permitOnly = [new("risk", "READY", "x"), new("permit", "UNKNOWN", "PermitToWorkSourceUnavailable")];
        Assert.Equal(1, HighRiskWorkPolicy.Rank(permitOnly));
        Assert.Equal("WAIT_SOURCE", HighRiskWorkPolicy.NextAction(permitOnly).Kind);
        Assert.Equal("NONE", HighRiskWorkPolicy.NextAction([new("risk", "READY", "x")]).Kind);
    }

    [Fact]
    public void Next_action_follows_the_fixed_server_priority() {
        HighRiskDimension[] dims = [new("crew", "REVIEW_REQUIRED", "u"), new("briefing", "BLOCKED", "TowageBriefingPlanChanged"),
            new("risk", "BLOCKED", "RiskAssessmentNotApproved"), new("risk", "BLOCKED", "RiskAcknowledgementDisputed")];
        Assert.Equal("RESOLVE_STOP", HighRiskWorkPolicy.NextAction(dims).Kind);
        Assert.Equal("COMPLETE_REQUIRED", HighRiskWorkPolicy.NextAction(dims[..3]).Kind);
        Assert.Equal("REASSESS", HighRiskWorkPolicy.NextAction(dims[..2]).Kind);
        Assert.Equal("WAIT_SOURCE", HighRiskWorkPolicy.NextAction(dims[..1]).Kind);
        Assert.Equal("OPEN_OWNER", HighRiskWorkPolicy.NextAction([new("risk", "ATTENTION", "RiskAcknowledgementPending")]).Kind);
    }

    [Fact]
    public void Work_type_mapping_never_drops_unknown_classification() {
        Assert.Equal("UNCLASSIFIED", HighRiskWorkPolicy.FromRiskWorkType(null));
        Assert.Equal("UNCLASSIFIED", HighRiskWorkPolicy.FromRiskWorkType("MAINTENANCE"));
        Assert.Equal("HOT_WORK", HighRiskWorkPolicy.FromRiskWorkType("HOT_WORK"));
        Assert.Null(HighRiskWorkPolicy.FromRiskWorkType("CARGO"));
        Assert.Equal(TugReadinessEngine.NotReady, HighRiskWorkPolicy.Readiness([TugReadinessEngine.Ready, TugReadinessEngine.NotReady]));
        Assert.Equal(TugReadinessEngine.ReviewRequired, HighRiskWorkPolicy.Readiness([]));
    }
}
