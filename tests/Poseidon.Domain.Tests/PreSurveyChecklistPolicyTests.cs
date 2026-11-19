using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-104 (18.01 row105; 60.04 Rev3 p135-140, p226, p637-639) and DEV-112 (row113; p92, p126, p221-222).
[Trait("Requirement", "DEV-104")]
public sealed class PreSurveyChecklistPolicyTests
{
    private static readonly Guid Checklist = Guid.Parse("10000000-0000-4000-8000-000000000001"), Author = Guid.NewGuid(), Reviewer = Guid.NewGuid(), Role = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static readonly CertificateFilePin Pin = new(Guid.NewGuid(), 1, new string('a', 64));
    private static PreSurveyChecklistContent Fresh(string template = "STARTER-ANNUAL") => PreSurveyChecklistPolicy.Instantiate(Checklist, PreSurveyChecklistPolicy.Template(template)!, new());
    private static PreSurveyItemInput[] All(PreSurveyChecklistContent c, Func<PreSurveyItem, string> answer, bool evidence = true) =>
        c.Items.Select(i => new PreSurveyItemInput(i.ItemId, Role, Today.AddDays(5), answer(i), evidence && i.EvidenceRequired && answer(i) == "READY" ? [Pin] : [])).ToArray();
    private static PreSurveyChecklistContent Answered(PreSurveyChecklistContent c) =>
        PreSurveyChecklistPolicy.Apply(c, new(), All(c, i => i.AllowNotApplicable ? "NOT_APPLICABLE" : "READY"), Author).Content!;
    private static PreSurveyVerificationDecision[] VerifyAll(PreSurveyChecklistContent c) =>
        c.Items.Select(i => new PreSurveyVerificationDecision(i.ItemId, PreSurveyChecklistPolicy.ItemHash(i), "VERIFIED")).ToArray();

    [Fact]
    public void Templates_are_disclosed_starters_and_instantiate_deterministic_open_items()
    {
        Assert.All(PreSurveyChecklistPolicy.Templates, t => Assert.Equal("STARTER_UNAPPROVED", t.Status));
        var c = Fresh();
        Assert.Equal(Fresh().Items.Select(i => i.ItemId), c.Items.Select(i => i.ItemId));
        Assert.All(c.Items, i => { Assert.Equal("OPEN", i.Answer); Assert.Equal("PENDING", i.Verification.State); Assert.Null(i.AnsweredBy); });
        Assert.True(PreSurveyChecklistPolicy.ValidContent(c));
        Assert.Contains(Fresh("STARTER-RENEWAL").Items, i => i.ItemCode == "RENEWAL_APPLICATION_REFERENCE_RECORDED" && i.Critical);
        Assert.Null(PreSurveyChecklistPolicy.Template("APPROVED-CLASS-CATALOG"));
    }

    [Fact]
    public void Unresolved_critical_item_blocks_and_readiness_never_becomes_ready()
    {
        var fresh = PreSurveyChecklistPolicy.Readiness(Fresh(), Today);
        Assert.Equal("BLOCKED", fresh.State);
        Assert.Contains("CRITICAL_ITEM_UNRESOLVED", fresh.Blockers); Assert.Contains("TEMPLATE_NOT_APPROVED", fresh.Blockers);
        // Answered but not independently verified still blocks.
        var answered = Answered(Fresh());
        Assert.Equal("BLOCKED", PreSurveyChecklistPolicy.Readiness(answered, Today).State);
        var verified = PreSurveyChecklistPolicy.Verify(answered, VerifyAll(answered), Reviewer, DateTimeOffset.UnixEpoch).Content!;
        var best = PreSurveyChecklistPolicy.Readiness(verified, Today);
        Assert.Equal("PREPARED_PENDING_HUMAN_CONFIRMATION", best.State);
        Assert.Equal("ADVISORY_NOT_SURVEY_APPROVAL", best.Qualification);
        Assert.DoesNotContain("READY", PreSurveyChecklistPolicy.ReadinessStates);
        Assert.Contains("SURVEY_BOOKING_AND_AUTHORITY_EXTERNAL", best.Blockers);
        // One critical item marked BLOCKED blocks again; a required non-critical one yields INCOMPLETE.
        var critical = verified.Items.First(i => i.Critical);
        var blocked = PreSurveyChecklistPolicy.Apply(verified, new(), verified.Items.Select(i => new PreSurveyItemInput(i.ItemId, i.OwnerRoleId, i.DueDate, i.ItemId == critical.ItemId ? "BLOCKED" : i.Answer, i.Evidence)).ToArray(), Author).Content!;
        Assert.Equal("BLOCKED", PreSurveyChecklistPolicy.Readiness(blocked, Today).State);
        var required = verified.Items.First(i => i.Required && !i.Critical);
        var incomplete = PreSurveyChecklistPolicy.Apply(verified, new(), verified.Items.Select(i => new PreSurveyItemInput(i.ItemId, i.OwnerRoleId, i.DueDate, i.ItemId == required.ItemId ? "ATTENTION" : i.Answer, i.Evidence, i.ItemId == required.ItemId ? "Fictional finding" : null)).ToArray(), Author).Content!;
        var state = PreSurveyChecklistPolicy.Readiness(incomplete, Today);
        Assert.Equal("INCOMPLETE", state.State); Assert.Contains("OPEN_FINDING", state.Blockers);
    }

    [Fact]
    public void Authoring_requires_every_item_refuses_unauthorised_na_and_resets_changed_verification()
    {
        var c = Fresh();
        Assert.Equal("checklist_items_mismatch", PreSurveyChecklistPolicy.Apply(c, new(), [.. All(c, _ => "READY").Skip(1), new(Guid.NewGuid(), null, null, "OPEN", [])], Author).Error);
        Assert.Equal("validation_failed", PreSurveyChecklistPolicy.Apply(c, new(), All(c, _ => "READY").Skip(1).ToArray(), Author).Error);
        Assert.Equal("na_not_authorized", PreSurveyChecklistPolicy.Apply(c, new(), All(c, _ => "NOT_APPLICABLE"), Author).Error);
        Assert.Equal("validation_failed", PreSurveyChecklistPolicy.Apply(c, new(PlannedStart: Today, PlannedEnd: Today.AddDays(-1)), All(c, _ => "READY"), Author).Error);
        Assert.Equal("validation_failed", PreSurveyChecklistPolicy.Apply(c, new(PortCallId: Guid.NewGuid()), All(c, _ => "READY"), Author).Error);
        var answered = Answered(c);
        var verified = PreSurveyChecklistPolicy.Verify(answered, VerifyAll(answered), Reviewer, DateTimeOffset.UnixEpoch).Content!;
        var target = verified.Items[0];
        var edited = PreSurveyChecklistPolicy.Apply(verified, new(), verified.Items.Select(i => new PreSurveyItemInput(i.ItemId, i.OwnerRoleId, i.DueDate, i.Answer, i.Evidence, null, i.ItemId == target.ItemId ? "changed" : null)).ToArray(), Author).Content!;
        Assert.Equal("PENDING", edited.Items[0].Verification.State);
        Assert.All(edited.Items.Skip(1), i => Assert.True(PreSurveyChecklistPolicy.VerifiedCurrent(i)));
    }

    [Fact]
    public void Verification_is_independent_bound_to_the_seen_item_and_reports_every_failure()
    {
        var answered = Answered(Fresh());
        var own = PreSurveyChecklistPolicy.Verify(answered, VerifyAll(answered), Author, DateTimeOffset.UnixEpoch);
        Assert.Null(own.Content); Assert.Equal(answered.Items.Length, own.Errors.Length); Assert.All(own.Errors, e => Assert.Equal("separation_of_duties", e.Code));
        var stale = PreSurveyChecklistPolicy.Verify(answered, [new(answered.Items[0].ItemId, new string('f', 64), "VERIFIED")], Reviewer, DateTimeOffset.UnixEpoch);
        Assert.Equal("item_changed", Assert.Single(stale.Errors).Code);
        var noEvidence = PreSurveyChecklistPolicy.Apply(Fresh(), new(), All(Fresh(), i => i.AllowNotApplicable ? "OPEN" : "READY", evidence: false), Author).Content!;
        var missing = PreSurveyChecklistPolicy.Verify(noEvidence, VerifyAll(noEvidence).Where(d => noEvidence.Items.Single(i => i.ItemId == d.ItemId).Answer != "OPEN").ToArray(), Reviewer, DateTimeOffset.UnixEpoch);
        Assert.Equal(noEvidence.Items.Count(i => i.EvidenceRequired), missing.Errors.Count(e => e.Code == "evidence_required"));
        var open = PreSurveyChecklistPolicy.Verify(Fresh(), VerifyAll(Fresh()).Take(1).ToArray(), Reviewer, DateTimeOffset.UnixEpoch);
        Assert.Equal("item_not_answered", Assert.Single(open.Errors).Code);
        Assert.Equal("validation_failed", PreSurveyChecklistPolicy.Verify(answered, [new(answered.Items[0].ItemId, PreSurveyChecklistPolicy.ItemHash(answered.Items[0]), "REJECTED")], Reviewer, DateTimeOffset.UnixEpoch).Errors.Single().Code);
        var rejected = PreSurveyChecklistPolicy.Verify(answered, [new(answered.Items[0].ItemId, PreSurveyChecklistPolicy.ItemHash(answered.Items[0]), "REJECTED", "EVIDENCE_INSUFFICIENT")], Reviewer, DateTimeOffset.UnixEpoch).Content!;
        Assert.Contains("VERIFICATION_REJECTED", PreSurveyChecklistPolicy.ItemBlockers(rejected.Items[0], Today));
        Assert.Equal("already_recorded", PreSurveyChecklistPolicy.Verify(rejected, [new(rejected.Items[0].ItemId, PreSurveyChecklistPolicy.ItemHash(rejected.Items[0]), "REJECTED", "EVIDENCE_INSUFFICIENT")], Reviewer, DateTimeOffset.UnixEpoch).Errors.Single().Code);
    }

    [Fact]
    public void Overdue_unresolved_item_and_missing_owner_or_due_are_flagged_not_defaulted()
    {
        var c = Fresh();
        var late = PreSurveyChecklistPolicy.Apply(c, new(), c.Items.Select(i => new PreSurveyItemInput(i.ItemId, null, Today.AddDays(-1), "ATTENTION", [])).ToArray(), Author).Content!;
        Assert.Contains("ITEM_OVERDUE", PreSurveyChecklistPolicy.ItemBlockers(late.Items[0], Today));
        Assert.Contains("OWNER_UNASSIGNED", PreSurveyChecklistPolicy.ItemBlockers(late.Items[0], Today));
        Assert.Contains("DUE_DATE_UNKNOWN", PreSurveyChecklistPolicy.ItemBlockers(c.Items[0], Today));
    }
}

[Trait("Requirement", "DEV-112")]
public sealed class SurveyPlannerPolicyTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static SurveyWindowDto Open() => SurveyPlannerPolicy.Window(Today, true, Today.AddDays(-10), Today.AddDays(60));

    [Theory]
    [InlineData(false, null, null, "UNKNOWN", "WINDOW_NOT_DECLARED")]
    [InlineData(true, -5, null, "UNKNOWN", "WINDOW_INVALID")]
    [InlineData(true, 10, 5, "UNKNOWN", "WINDOW_INVALID")]
    [InlineData(true, 5, 30, "UPCOMING", "SURVEY_SATISFACTION_NOT_RECORDED")]
    [InlineData(true, -5, 30, "OPEN", "DECLARED_WINDOW_NOT_SOURCE_POLICY")]
    [InlineData(true, -60, -1, "OVERDUE", "SURVEY_SATISFACTION_NOT_RECORDED")]
    public void Window_state_is_computed_from_declared_dates_only(bool declared, int? start, int? end, string state, string qualifier)
    {
        var w = SurveyPlannerPolicy.Window(Today, declared, start is { } s ? Today.AddDays(s) : null, end is { } e ? Today.AddDays(e) : null);
        Assert.Equal(state, w.State); Assert.Contains(qualifier, w.Qualifiers);
        if (state == "OVERDUE") Assert.Equal(1, w.DaysOverdue);
    }

    [Fact]
    public void Fit_is_never_fit_and_reports_unknown_inputs_and_known_conflicts()
    {
        Assert.DoesNotContain("FIT", SurveyPlannerPolicy.FitStates);
        var unknown = SurveyPlannerPolicy.Fit(Today, SurveyPlannerPolicy.Window(Today, false, null, null), null, null, null);
        Assert.Equal("UNKNOWN", unknown.State); Assert.Contains("WINDOW_UNKNOWN", unknown.Reasons); Assert.Contains("NO_PREPARATION_PLAN", unknown.Reasons); Assert.Contains("EXPIRY_UNKNOWN", unknown.Reasons);
        var plan = new PreSurveyPlan(Today.AddDays(10), Today.AddDays(11), Guid.NewGuid(), 2);
        var noPort = SurveyPlannerPolicy.Fit(Today, Open(), Today.AddDays(200), plan with { PortCallId = null, PortCallRevision = null }, null);
        Assert.Equal("UNKNOWN", noPort.State); Assert.Contains("PORT_CALL_NOT_LINKED", noPort.Reasons);
        var stalePort = SurveyPlannerPolicy.Fit(Today, Open(), Today.AddDays(200), plan, new("SUPERSEDED", Today.AddDays(10), Today.AddDays(12)));
        Assert.Contains("PORT_CALL_REFERENCE_NOT_CURRENT", stalePort.Reasons);
        var conditional = SurveyPlannerPolicy.Fit(Today, Open(), Today.AddDays(200), plan, new("CURRENT", Today.AddDays(9), Today.AddDays(12)));
        Assert.Equal("CONDITIONAL", conditional.State); Assert.Contains("VOYAGE_UNKNOWN", conditional.Reasons); Assert.Contains("AUTHORITY_COORDINATION_EXTERNAL", conditional.Reasons);
        Assert.Equal("NOT_FIT", SurveyPlannerPolicy.Fit(Today, Open(), Today.AddDays(200), plan, new("CURRENT", Today.AddDays(20), Today.AddDays(22))).State);
        var outside = SurveyPlannerPolicy.Fit(Today, Open(), Today.AddDays(200), plan with { PlannedStart = Today.AddDays(70), PlannedEnd = Today.AddDays(71) }, new("CURRENT", Today.AddDays(70), Today.AddDays(71)));
        Assert.Equal("NOT_FIT", outside.State); Assert.Contains("PLANNED_OUTSIDE_SURVEY_WINDOW", outside.Reasons);
        Assert.Contains("PLANNED_AFTER_DECLARED_EXPIRY", SurveyPlannerPolicy.Fit(Today, Open(), Today.AddDays(10), plan, new("CURRENT", Today.AddDays(9), Today.AddDays(12))).Reasons);
        var closed = SurveyPlannerPolicy.Fit(Today, SurveyPlannerPolicy.Window(Today, true, Today.AddDays(-60), Today.AddDays(-1)), Today.AddDays(200), plan, new("CURRENT", Today.AddDays(9), Today.AddDays(12)));
        Assert.Equal("NOT_FIT", closed.State); Assert.Contains("WINDOW_CLOSED", closed.Reasons);
        Assert.Contains("PLANNED_DATES_IN_PAST", SurveyPlannerPolicy.Fit(Today, Open(), Today.AddDays(200), plan with { PlannedStart = Today.AddDays(-2) }, null).Reasons);
    }

    [Fact]
    public void Calendar_range_keeps_unknown_windows_visible()
    {
        Assert.True(SurveyPlannerPolicy.Intersects(SurveyPlannerPolicy.Window(Today, false, null, null), null, Today, Today.AddDays(1)));
        Assert.False(SurveyPlannerPolicy.Intersects(SurveyPlannerPolicy.Window(Today, true, Today.AddDays(400), Today.AddDays(500)), Today.AddDays(900), Today, Today.AddDays(30)));
        Assert.True(SurveyPlannerPolicy.Intersects(SurveyPlannerPolicy.Window(Today, true, Today.AddDays(400), Today.AddDays(500)), Today.AddDays(20), Today, Today.AddDays(30)));
    }
}
