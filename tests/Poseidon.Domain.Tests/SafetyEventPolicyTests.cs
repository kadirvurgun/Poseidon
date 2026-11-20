using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

// S9 WP-B: DEV-060 / T-S9-003 (18.01 row 61) and DEV-061 / T-S9-004 (row 62); 61.01 Rev3 §4-§9.
// FICTIONAL values only. No case derives a root cause, a severity, an effectiveness result or a closure automatically.
public sealed class SafetyEventPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid(), Verifier = Guid.NewGuid(), Reviewer = Guid.NewGuid();

    private static SafetyEventDraftFields Complete() => new("NEAR_MISS", "EXACT", Now.AddHours(-2), null, null, "Europe/Istanbul", "MOORING",
        "FICTIONAL forward station", "FICTIONAL mooring line parted near crew", "FICTIONAL the head line parted while heaving; nobody was in the bight.",
        "YES", "FICTIONAL area cleared and line replaced", "HIGH", null);

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public void Draft_may_be_incomplete_but_submission_lists_every_missing_required_field()
    {
        var empty = new SafetyEventDraftFields();
        Assert.True(SafetyEventPolicy.ValidDraft(empty));
        Assert.Equal(["category", "occurrence_precision", "operation_context", "summary", "narrative", "immediate_action_taken"], SafetyEventPolicy.Missing(empty));
        var decision = SafetyEventPolicy.Submit(SafetyEventPolicy.Draft, true, empty, Now);
        Assert.Equal((422, "safety_event_incomplete"), (decision.Status, decision.Code));
        Assert.Empty(SafetyEventPolicy.Missing(Complete()));
        Assert.True(SafetyEventPolicy.Submit(SafetyEventPolicy.Draft, true, Complete(), Now).Ok);
        Assert.Equal(["immediate_action_text"], SafetyEventPolicy.Missing(Complete() with { ImmediateActionText = null }));
    }

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public void Unknown_stays_unknown_with_a_reason_and_is_never_turned_into_no()
    {
        var unknown = Complete() with { OccurrencePrecision = "UNKNOWN", OccurredAt = null, ImmediateActionTaken = "UNKNOWN", ImmediateActionText = null };
        Assert.Equal(["occurrence_unknown_reason"], SafetyEventPolicy.Missing(unknown));
        Assert.Empty(SafetyEventPolicy.Missing(unknown with { OccurrenceUnknownReason = "FICTIONAL reporter was asleep and woke to the alarm" }));
        // Contradictions are rejected, not silently resolved.
        Assert.False(SafetyEventPolicy.ValidDraft(unknown with { OccurredAt = Now }));
        Assert.False(SafetyEventPolicy.ValidDraft(Complete() with { ImmediateActionTaken = "NO" }));
        Assert.False(SafetyEventPolicy.ValidDraft(Complete() with { OccurrencePrecision = "DATE_ONLY" }));
        Assert.False(SafetyEventPolicy.ValidDraft(Complete() with { Category = "MAYBE" }));
        Assert.False(SafetyEventPolicy.ValidDraft(Complete() with { TimeZone = "../etc" }));
    }

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public void Future_occurrence_is_rejected_but_a_local_date_ahead_of_utc_is_not()
    {
        Assert.Equal("occurrence_in_future", SafetyEventPolicy.Submit(SafetyEventPolicy.Draft, true, Complete() with { OccurredAt = Now.AddMinutes(5) }, Now).Code);
        var tomorrowLocal = Complete() with { OccurrencePrecision = "DATE_ONLY", OccurredAt = null, OccurrenceDate = new DateOnly(2026, 11, 21) };
        Assert.True(SafetyEventPolicy.Submit(SafetyEventPolicy.Draft, true, tomorrowLocal, Now).Ok);
        Assert.True(SafetyEventPolicy.InFuture(tomorrowLocal with { OccurrenceDate = new DateOnly(2026, 11, 23) }, Now));
    }

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public void Only_the_reporter_edits_a_draft_and_a_submitted_report_cannot_be_withdrawn()
    {
        Assert.Equal(403, SafetyEventPolicy.Edit(SafetyEventPolicy.Draft, false).Status);
        Assert.True(SafetyEventPolicy.Edit(SafetyEventPolicy.Returned, true).Ok);
        Assert.Equal(409, SafetyEventPolicy.Edit(SafetyEventPolicy.Submitted, true).Status);
        Assert.Equal(409, SafetyEventPolicy.Withdraw(SafetyEventPolicy.Submitted, true).Status);
        Assert.True(SafetyEventPolicy.Withdraw(SafetyEventPolicy.Draft, true).Ok);
    }

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public void Triage_is_a_human_classification_never_by_the_reporter()
    {
        Assert.Equal("separation_of_duties", SafetyEventPolicy.Triage(SafetyEventPolicy.Submitted, "TRIAGE", "HIGH", "YES", true).Code);
        Assert.True(SafetyEventPolicy.Triage(SafetyEventPolicy.Submitted, "TRIAGE", "UNASSESSED", "UNKNOWN", false).Ok);
        Assert.Equal(400, SafetyEventPolicy.Triage(SafetyEventPolicy.Submitted, "TRIAGE", null, "YES", false).Status);
        Assert.Equal(400, SafetyEventPolicy.Triage(SafetyEventPolicy.Submitted, "RETURN", "LOW", null, false).Status);
        Assert.Equal(409, SafetyEventPolicy.Triage(SafetyEventPolicy.Triaged, "RETURN", null, null, false).Status);
        Assert.True(SafetyEventPolicy.Triage(SafetyEventPolicy.Triaged, "TRIAGE", "CRITICAL", "YES", false).Ok);
    }

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public void Blocker_source_marks_high_severity_and_untriaged_events_and_never_ready()
    {
        Assert.Equal("UNTRIAGED_EVENT", SafetyEventPolicy.BlockerReason(SafetyEventPolicy.Submitted, null));
        Assert.Equal("HIGH_SEVERITY_OPEN", SafetyEventPolicy.BlockerReason(SafetyEventPolicy.ActionTracking, "CRITICAL"));
        Assert.Equal("SEVERITY_UNASSESSED", SafetyEventPolicy.BlockerReason(SafetyEventPolicy.Triaged, "UNASSESSED"));
        Assert.Null(SafetyEventPolicy.BlockerReason(SafetyEventPolicy.Triaged, "LOW"));
        Assert.Null(SafetyEventPolicy.BlockerReason(SafetyEventPolicy.Closed, "CRITICAL"));
        Assert.Null(SafetyEventPolicy.BlockerReason(SafetyEventPolicy.Draft, "HIGH"));
        Assert.True(SafetyEventPolicy.Blocking("HIGH_SEVERITY_OPEN")); Assert.False(SafetyEventPolicy.Blocking("UNTRIAGED_EVENT"));
    }

    private static SafetyInvestigationContent Investigation(out Guid cause, out Guid barrier)
    {
        var factor = new SafetyFactor(Guid.NewGuid(), "FACT", "EQUIPMENT", "FICTIONAL line was past its retirement criteria");
        cause = Guid.NewGuid(); barrier = Guid.NewGuid();
        return new("FICTIONAL scope: mooring line failure at forward station", [factor],
            [new(cause, "FICTIONAL line inspection interval not followed", [factor.Id], "SUPPORTED")],
            [new(barrier, "FICTIONAL line inspection", "PREVENTIVE", "FAILED", "BAR-1"), new(Guid.NewGuid(), "FICTIONAL snap-back zone marking", "MITIGATING", "WORKED")],
            [], []);
    }

    [Fact]
    [Trait("Requirement", "DEV-061")]
    public void Investigation_needs_factors_barriers_and_a_human_root_cause_or_explicit_uncertainty()
    {
        var empty = new SafetyInvestigationContent(null, [], [], [], [], []);
        Assert.True(SafetyEventPolicy.ValidInvestigation(empty));
        Assert.Equal(["scope", "contributing_factors", "barriers", "root_cause_or_uncertainty"], SafetyEventPolicy.InvestigationMissing(empty));
        var full = Investigation(out _, out _);
        Assert.Empty(SafetyEventPolicy.InvestigationMissing(full));
        Assert.Empty(SafetyEventPolicy.InvestigationMissing(full with { RootCauses = [], Uncertainties = ["FICTIONAL root cause could not be determined: line destroyed"] }));
        // A root cause must cite the analysed factors; the server never invents one.
        Assert.False(SafetyEventPolicy.ValidInvestigation(full with { RootCauses = [new(Guid.NewGuid(), "x", [Guid.NewGuid()], "SUPPORTED")] }));
        Assert.False(SafetyEventPolicy.ValidInvestigation(full with { Barriers = [new(Guid.NewGuid(), "x", "PREVENTIVE", "OK")] }));
    }

    [Fact]
    [Trait("Requirement", "DEV-061")]
    public void Investigation_reviewer_is_independent_of_authors_submitter_and_reporter()
    {
        var author = Guid.NewGuid();
        Assert.Equal("separation_of_duties", SafetyEventPolicy.ReviewInvestigation("SUBMITTED", "APPROVE", author, [author], author, false).Code);
        Assert.Equal("separation_of_duties", SafetyEventPolicy.ReviewInvestigation("SUBMITTED", "APPROVE", Reviewer, [author], author, true).Code);
        Assert.True(SafetyEventPolicy.ReviewInvestigation("SUBMITTED", "REJECT", Reviewer, [author], author, false).Ok);
        Assert.Equal(409, SafetyEventPolicy.ReviewInvestigation("DRAFT", "APPROVE", Reviewer, [author], null, false).Status);
    }

    private static SafetyActionView Action(string state = "OPEN", string effectiveness = "PENDING", Guid? completed = null, Guid? verified = null, Guid? followUp = null,
        Guid? id = null, Guid? cause = null, Guid? barrier = null) =>
        new(id ?? Guid.NewGuid(), state, effectiveness, Owner, completed, verified, effectiveness == "PENDING" ? null : Reviewer, followUp,
            cause == null ? [] : [cause.Value], barrier == null ? [] : [barrier.Value]);

    [Fact]
    [Trait("Requirement", "DEV-061")]
    public void Action_steps_keep_owner_verifier_and_effectiveness_reviewer_separate()
    {
        var all = new HashSet<string> { SafetyEventPolicy.ActionVerifyPermission, SafetyEventPolicy.ActionEffectivenessPermission };
        Assert.Equal("separation_of_duties", SafetyEventPolicy.Step(Action(), "COMPLETE", null, Verifier, all).Code);
        Assert.True(SafetyEventPolicy.Step(Action(), "COMPLETE", null, Owner, all).Ok);
        var completed = Action("COMPLETED", completed: Owner);
        Assert.Equal("separation_of_duties", SafetyEventPolicy.Step(completed, "VERIFY", "VERIFIED", Owner, all).Code);
        Assert.Equal("permission_denied", SafetyEventPolicy.Step(completed, "VERIFY", "VERIFIED", Verifier, new HashSet<string>()).Code);
        Assert.True(SafetyEventPolicy.Step(completed, "VERIFY", "VERIFIED", Verifier, all).Ok);
        // Effectiveness only after verification and never by owner/completer/verifier.
        Assert.Equal(409, SafetyEventPolicy.Step(completed, "EFFECTIVENESS", "EFFECTIVE", Reviewer, all).Status);
        var verified = Action("VERIFIED", completed: Owner, verified: Verifier);
        Assert.Equal("separation_of_duties", SafetyEventPolicy.Step(verified, "EFFECTIVENESS", "EFFECTIVE", Verifier, all).Code);
        Assert.True(SafetyEventPolicy.Step(verified, "EFFECTIVENESS", "INCONCLUSIVE", Reviewer, all).Ok);
        Assert.Equal(409, SafetyEventPolicy.Step(verified with { Effectiveness = "EFFECTIVE" }, "EFFECTIVENESS", "NOT_EFFECTIVE", Reviewer, all).Status);
        Assert.True(SafetyEventPolicy.Step(verified with { Effectiveness = "INCONCLUSIVE" }, "EFFECTIVENESS", "EFFECTIVE", Reviewer, all).Ok);
    }

    [Fact]
    [Trait("Requirement", "DEV-061")]
    public void Closure_is_blocked_until_every_failed_barrier_and_cause_has_a_verified_effective_action()
    {
        var content = Investigation(out var cause, out var barrier);
        var blockers = SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.ActionTracking, "YES", "APPROVED", content, []);
        Assert.Contains("BARRIER_WITHOUT_ACTION:" + barrier.ToString("D"), blockers);
        Assert.Contains("ROOT_CAUSE_WITHOUT_ACTION:" + cause.ToString("D"), blockers);
        var open = Action(cause: cause, barrier: barrier);
        Assert.Equal(["ACTION_NOT_VERIFIED:" + open.Id.ToString("D")], SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.ActionTracking, "YES", "APPROVED", content, [open]));
        var verified = open with { State = "VERIFIED", CompletedBy = Owner, VerifiedBy = Verifier };
        Assert.Equal(["EFFECTIVENESS_NOT_REVIEWED:" + open.Id.ToString("D")], SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.ActionTracking, "YES", "APPROVED", content, [verified]));
        var failed = verified with { Effectiveness = "NOT_EFFECTIVE" };
        Assert.Equal(["NOT_EFFECTIVE_WITHOUT_FOLLOW_UP:" + open.Id.ToString("D")], SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.ActionTracking, "YES", "APPROVED", content, [failed]));
        var followUp = Action("VERIFIED", "EFFECTIVE", Owner, Verifier, open.Id, cause: cause);
        Assert.Empty(SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.ActionTracking, "YES", "APPROVED", content, [failed, followUp]));
        Assert.Equal("EFFECTIVE", SafetyEventPolicy.Effectiveness([failed, followUp]));
        Assert.Equal("NOT_EFFECTIVE", SafetyEventPolicy.Effectiveness([failed]));
        Assert.Equal(["INVESTIGATION_NOT_APPROVED"], SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.ActionTracking, "YES", "SUBMITTED", null, []));
    }

    [Fact]
    [Trait("Requirement", "DEV-061")]
    public void Closed_is_not_effective_and_unknown_investigation_need_blocks_closure()
    {
        Assert.Equal("NO_ACTIONS", SafetyEventPolicy.Effectiveness([]));
        Assert.Equal(["INVESTIGATION_NEED_UNKNOWN"], SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.Triaged, "UNKNOWN", null, null, []));
        Assert.Equal(["INVESTIGATION_REQUIRED"], SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.Triaged, "YES", null, null, []));
        Assert.Empty(SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.Triaged, "NO", null, null, []));
        Assert.Equal(["INVALID_STATE"], SafetyEventPolicy.ClosureBlockers(SafetyEventPolicy.Submitted, "NO", null, null, []));
    }
}
