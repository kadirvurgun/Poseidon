using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

// S7 WP-B: DEV-083 (18.01 row 84), DEV-084 (row 85), DEV-085 (row 86), DEV-086 (row 87); 60.02 Rev2
// paragraphs 108-128, 199, 201, 520. FICTIONAL values only; no case produces a GO by itself.
public sealed class TugReadinessTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);
    private static TugReadinessCheck[] AllPass() => TugReadinessEngine.Categories.Select(c => new TugReadinessCheck(c, TugReadinessEngine.Pass, "FictionalPass")).ToArray();

    // ---------------- DEV-083 assignment workflow ----------------
    [Theory]
    [InlineData(null, "2026-11-21T10:00:00Z", "Lead", "planned_window_required")]
    [InlineData("2026-11-21T10:00:00Z", "2026-11-21T10:00:00Z", "Lead", "planned_window_invalid")]
    [InlineData("2026-11-21T10:00:00Z", "2026-11-21T09:00:00Z", "Lead", "planned_window_invalid")]
    [InlineData("2026-11-21T08:00:00Z", "2026-12-30T08:00:00Z", "Lead", "planned_window_invalid")]
    [InlineData("2026-11-21T08:00:00Z", "2026-11-21T10:00:00Z", "Pilot", "assignment_role_invalid")]
    [InlineData("2026-11-21T08:00:00Z", "2026-11-21T10:00:00Z", null, "assignment_role_invalid")]
    [InlineData("2026-11-21T08:00:00Z", "2026-11-21T10:00:00Z", "Escort", null)]
    public void Proposal_requires_an_explicit_ordered_bounded_window_and_closed_role(string? start, string? end, string? role, string? expected) =>
        Assert.Equal(expected, TugAssignmentWorkflow.InvalidProposal(start == null ? null : DateTimeOffset.Parse(start), end == null ? null : DateTimeOffset.Parse(end), role));

    [Fact]
    public void Master_accept_keeps_the_proposal_proposed_and_never_confirms() {
        var t = TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Pending, TugAssignmentCommand.Respond, TugAssignmentWorkflow.Accept);
        Assert.Equal(TugAssignmentWorkflow.Proposed, t.ToState); Assert.Equal(TugAssignmentWorkflow.Accepted, t.ToAck);
        Assert.DoesNotContain("Confirmed", TugAssignmentWorkflow.States);
    }

    [Fact]
    public void Reject_requires_a_reason_and_acknowledgement_happens_once() {
        Assert.Equal("validation_failed", TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Pending, TugAssignmentCommand.Respond, TugAssignmentWorkflow.Reject).Failure);
        Assert.Equal("validation_failed", TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Pending, TugAssignmentCommand.Respond, TugAssignmentWorkflow.Reject, "  ").Failure);
        var rejected = TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Pending, TugAssignmentCommand.Respond, TugAssignmentWorkflow.Reject, "FICTIONAL crew rest concern");
        Assert.Equal(TugAssignmentWorkflow.Rejected, rejected.ToAck);
        Assert.Equal("invalid_state", TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Rejected, TugAssignmentCommand.Respond, TugAssignmentWorkflow.Accept).Failure);
        Assert.Equal("invalid_state", TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Cancelled, TugAssignmentWorkflow.Pending, TugAssignmentCommand.Respond, TugAssignmentWorkflow.Accept).Failure);
        Assert.Equal("validation_failed", TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Pending, TugAssignmentCommand.Respond, "MAYBE").Failure);
    }

    [Fact]
    public void Withdraw_keeps_the_master_acknowledgement_and_is_terminal() {
        var t = TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Rejected, TugAssignmentCommand.Withdraw, reason: "FICTIONAL re-plan");
        Assert.Equal(TugAssignmentWorkflow.Cancelled, t.ToState); Assert.Equal(TugAssignmentWorkflow.Rejected, t.ToAck);
        Assert.Equal("invalid_state", TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Cancelled, TugAssignmentWorkflow.Rejected, TugAssignmentCommand.Withdraw, reason: "again").Failure);
        Assert.Equal("validation_failed", TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Pending, TugAssignmentCommand.Withdraw).Failure);
    }

    [Fact]
    public void Scope_hash_changes_with_job_revision_tug_window_or_role() {
        var job = Guid.Parse("10000000-0000-0000-0000-000000000001"); var tug = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var baseScope = new TugAssignmentScope(job, 2, tug, Start, Start.AddHours(2), "Lead");
        var h = TugAssignmentWorkflow.ScopeHash(baseScope);
        Assert.Equal(64, h.Length); Assert.Equal(h, TugAssignmentWorkflow.ScopeHash(baseScope with { PlannedStart = Start.ToOffset(TimeSpan.FromHours(3)) }));
        Assert.NotEqual(h, TugAssignmentWorkflow.ScopeHash(baseScope with { JobRevision = 3 }));
        Assert.NotEqual(h, TugAssignmentWorkflow.ScopeHash(baseScope with { TugId = job }));
        Assert.NotEqual(h, TugAssignmentWorkflow.ScopeHash(baseScope with { PlannedEnd = Start.AddHours(3) }));
        Assert.NotEqual(h, TugAssignmentWorkflow.ScopeHash(baseScope with { Role = "Support" }));
    }

    [Fact]
    public void Time_overlap_is_strict_intersection_on_the_same_tug_and_active_proposals_only() {
        var tug = Guid.NewGuid(); var subject = new TugAssignmentWindow(Guid.NewGuid(), tug, Start, Start.AddHours(2), TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Pending);
        var overlapping = subject with { Id = Guid.NewGuid(), PlannedStart = Start.AddHours(1), PlannedEnd = Start.AddHours(3) };
        var touching = subject with { Id = Guid.NewGuid(), PlannedStart = Start.AddHours(2), PlannedEnd = Start.AddHours(4) };
        var otherTug = overlapping with { Id = Guid.NewGuid(), TugId = Guid.NewGuid() };
        var cancelled = overlapping with { Id = Guid.NewGuid(), State = TugAssignmentWorkflow.Cancelled };
        Assert.Equal([overlapping.Id], TugAssignmentWorkflow.TimeOverlaps(subject, [overlapping, touching, otherTug, cancelled, subject]).Select(x => x.Id));
    }

    // ---------------- DEV-084 readiness engine ----------------
    [Fact]
    public void Everything_pass_is_ready_but_carries_no_go() {
        var o = TugReadinessEngine.Evaluate(AllPass());
        Assert.Equal(TugReadinessEngine.Ready, o.State); Assert.Empty(o.CriticalBlockers);
        Assert.Equal(TugReadinessEngine.DecisionAuthority, o.Decision); Assert.DoesNotContain("GO", TugReadinessEngine.States);
    }

    [Fact]
    public void A_single_critical_failure_forces_not_ready_and_cannot_be_averaged_away() {
        var checks = Enumerable.Range(0, 50).Select(_ => new TugReadinessCheck("equipment", TugReadinessEngine.Pass, "FictionalPass")).Concat(AllPass())
            .Append(new("tow_equipment", TugReadinessEngine.Fail, "Winch:CriticalEquipmentFailed")).Append(new("weather", TugReadinessEngine.Unknown, "WholeWindowWeatherSourceUnavailable")).ToArray();
        var o = TugReadinessEngine.Evaluate(checks);
        Assert.Equal(TugReadinessEngine.NotReady, o.State);
        Assert.Equal(["tow_equipment:Winch:CriticalEquipmentFailed"], o.CriticalBlockers);
        Assert.Equal(TugReadinessEngine.Fail, o.Categories.Single(c => c.Category == "tow_equipment").Result);
    }

    [Fact]
    public void Unknown_or_missing_categories_are_review_required_never_ready() {
        Assert.Equal(TugReadinessEngine.ReviewRequired, TugReadinessEngine.Evaluate([]).State);
        var missingWeather = AllPass().Where(c => c.Category != "weather").ToArray();
        var o = TugReadinessEngine.Evaluate(missingWeather);
        Assert.Equal(TugReadinessEngine.ReviewRequired, o.State); Assert.Contains("weather:SourceMissing", o.ReviewReasons);
        Assert.Equal(TugReadinessEngine.ReviewRequired, TugReadinessEngine.Evaluate([.. AllPass(), new("communication", TugReadinessEngine.Unknown, "CommunicationSourceUnavailable")]).State);
    }

    [Fact]
    public void A_condition_without_unknowns_is_conditional() {
        var o = TugReadinessEngine.Evaluate([.. AllPass(), new("pms", TugReadinessEngine.Condition, "DeclaredHighDefectOpen", false)]);
        Assert.Equal(TugReadinessEngine.Conditional, o.State); Assert.Equal(["pms:DeclaredHighDefectOpen"], o.Conditions);
    }

    [Fact]
    public void Invalid_checks_are_refused_and_the_hash_binds_the_exact_inputs() {
        Assert.Throws<ArgumentException>(() => TugReadinessEngine.Evaluate([new("fuel", TugReadinessEngine.Pass, "x")]));
        Assert.Throws<ArgumentException>(() => TugReadinessEngine.Evaluate([new("crew", "MAYBE", "x")]));
        var a = TugReadinessEngine.Evaluate(AllPass()); var b = TugReadinessEngine.Evaluate(AllPass().Reverse().ToArray());
        Assert.Equal(a.Hash, b.Hash);
        Assert.NotEqual(a.Hash, TugReadinessEngine.Evaluate([.. AllPass(), new("pms", TugReadinessEngine.Pass, "Other")]).Hash);
    }

    [Theory]
    [InlineData(null, "40", null, TugReadinessEngine.Unknown, "RequiredBollardPullSourceMissing")]
    [InlineData("40", null, null, TugReadinessEngine.Unknown, "VerifiedAvailableBollardPullMissing")]
    [InlineData("40", "38.5", "-1.5", TugReadinessEngine.Fail, "AvailableBollardPullBelowRequired")]
    [InlineData("40", "40", "0", TugReadinessEngine.Pass, "VerifiedMarginNonNegative")]
    public void Bollard_pull_margin_is_null_when_either_side_is_unknown(string? required, string? available, string? margin, string result, string reason) {
        var (m, check) = TugReadinessEngine.BollardPullMargin(required == null ? null : decimal.Parse(required, System.Globalization.CultureInfo.InvariantCulture),
            available == null ? null : decimal.Parse(available, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(margin, m); Assert.Equal(result, check.Result); Assert.Equal(reason, check.Reason);
    }

    [Theory]
    [InlineData("GO", TugReadinessEngine.Ready, null)]
    [InlineData("GO", TugReadinessEngine.Conditional, null)]
    [InlineData("GO", TugReadinessEngine.ReviewRequired, "tug_go_not_permitted")]
    [InlineData("GO", TugReadinessEngine.NotReady, "tug_go_not_permitted")]
    [InlineData("NO_GO", TugReadinessEngine.NotReady, null)]
    [InlineData("NO_GO", TugReadinessEngine.ReviewRequired, null)]
    [InlineData("LATER", TugReadinessEngine.Ready, "validation_failed")]
    public void Master_go_is_refused_unless_ready_or_conditional_and_no_go_is_always_recordable(string decision, string state, string? expected) {
        Assert.Equal(expected, TugReadinessDecisionPolicy.Refusal(decision, state, "FICTIONAL master reasoning"));
        Assert.Equal("validation_failed", TugReadinessDecisionPolicy.Refusal(TugReadinessDecisionPolicy.NoGo, state, null));
    }

    // ---------------- DEV-085 tow equipment ----------------
    private static readonly Guid Asset = Guid.Parse("30000000-0000-0000-0000-000000000003"), Cert = Guid.Parse("40000000-0000-0000-0000-000000000004");
    private static TowEquipmentInputs Winch(string condition = TowEquipmentPolicy.Serviceable, bool? critical = true) => new(TowEquipmentPolicy.Winch, "FICTIONAL winch", "45",
        new DateOnly(2026, 1, 10), new DateOnly(2027, 1, 10), condition, critical, Asset, Cert, null);
    private static readonly TowEquipmentEvidence Verified = new(TowEquipmentPolicy.Verified, "NotDue", true, new DateOnly(2027, 6, 1));
    private static readonly DateOnly AsOf = new(2026, 11, 21);

    [Fact]
    public void Verified_serviceable_equipment_with_links_passes() =>
        Assert.Equal(TugReadinessEngine.Pass, TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch(), Verified, AsOf).Result);

    [Fact]
    public void Critical_failure_is_fail_even_when_verified() {
        var c = TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch(TowEquipmentPolicy.Failed), Verified, AsOf);
        Assert.Equal(TugReadinessEngine.Fail, c.Result); Assert.Equal("Winch:CriticalEquipmentFailed", c.Reason); Assert.True(c.Critical);
        // Unassessed criticality is treated as critical for a failure.
        Assert.Equal(TugReadinessEngine.Fail, TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch(TowEquipmentPolicy.Failed, null), Verified, AsOf).Result);
        Assert.Equal(TugReadinessEngine.Condition, TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch(TowEquipmentPolicy.Failed, false), Verified, AsOf).Result);
    }

    [Theory]
    [InlineData("ChiefEngineerVerificationMissing")]
    [InlineData("ChiefEngineerVerificationRefused")]
    public void Missing_or_refused_chief_engineer_verification_is_never_positive(string reason) {
        var evidence = Verified with { Verification = reason.EndsWith("Missing") ? null : TowEquipmentPolicy.NotVerified };
        var c = TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch(), evidence, AsOf);
        Assert.Equal(TugReadinessEngine.Unknown, c.Result); Assert.Equal("Winch:" + reason, c.Reason);
    }

    [Fact]
    public void Overdue_test_expired_certificate_and_due_pms_block_critical_equipment() {
        Assert.Equal("Winch:TestOverdue", TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch() with { NextTestDue = new DateOnly(2026, 11, 1) }, Verified, AsOf).Reason);
        Assert.Equal("Winch:CertificateExpired", TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch(), Verified with { CertificateExpiry = new DateOnly(2026, 1, 1) }, AsOf).Reason);
        Assert.Equal("Winch:LinkedPmsDue", TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch(), Verified with { PmsDue = "Due" }, AsOf).Reason);
    }

    [Fact]
    public void Missing_swl_test_dates_links_or_unknown_condition_stay_unknown() {
        foreach (var x in new[] { Winch() with { SwlTonnes = null }, Winch() with { LastTestDate = null }, Winch() with { AssetId = null }, Winch() with { CertificateId = null }, Winch(TowEquipmentPolicy.ConditionUnknown) })
            Assert.Equal(TugReadinessEngine.Unknown, TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, x, Verified, AsOf).Result);
        Assert.Equal(TugReadinessEngine.Unknown, TowEquipmentPolicy.Assess(Guid.NewGuid(), 1, Winch(), Verified with { PmsDue = null }, AsOf).Result);
    }

    [Fact]
    public void A_required_kind_with_no_record_is_unknown_not_not_applicable() {
        var missing = TowEquipmentPolicy.Missing([TowEquipmentPolicy.Winch, TowEquipmentPolicy.TowHook]);
        Assert.Equal(["Towline:EquipmentRoleMissing", "EmergencyRelease:EquipmentRoleMissing", "FiFi:EquipmentRoleMissing"], missing.Select(m => m.Reason));
        Assert.All(missing, m => Assert.Equal(TugReadinessEngine.Unknown, m.Result));
    }

    [Theory]
    [InlineData("0", false)] [InlineData("-1", false)] [InlineData("1e3", false)] [InlineData("12.345", false)] [InlineData("12.34", true)] [InlineData("250", true)]
    public void Swl_is_a_positive_bounded_decimal(string text, bool valid) => Assert.Equal(valid, TowEquipmentPolicy.TrySwl(text, out _));

    [Fact]
    public void Invalid_tow_declarations_are_refused() {
        Assert.Contains("kind", TowEquipmentPolicy.Invalid(Winch() with { Kind = "Fender" }));
        Assert.Contains("condition", TowEquipmentPolicy.Invalid(Winch("Fine")));
        Assert.Contains("next_test_due", TowEquipmentPolicy.Invalid(Winch() with { NextTestDue = new DateOnly(2025, 1, 1) }));
        Assert.Empty(TowEquipmentPolicy.Invalid(Winch()));
    }

    // ---------------- DEV-086 crew / towage competence ----------------
    private static TugCrewCriterion[] AllVerified() => TugCrewCompetencePolicy.Criteria.Select(c => new TugCrewCriterion(c, TugReadinessEngine.Pass, "FictionalVerified", true)).ToArray();

    [Fact]
    public void All_verified_criteria_pass_and_minimum_manning_maps_to_shared_crew_category() {
        var checks = TugCrewCompetencePolicy.Assess(AllVerified());
        Assert.Equal(TugReadinessEngine.Ready, TugReadinessEngine.StateOf(checks));
        Assert.Equal("crew", checks.Single(c => c.Reason.StartsWith("MinimumManning")).Category);
    }

    [Fact]
    public void Unverified_towage_competence_blocks_positive_readiness() {
        var rows = AllVerified().Select(c => c.Criterion == TugCrewCompetencePolicy.TowageCompetence ? c with { Verified = false } : c).ToArray();
        var checks = TugCrewCompetencePolicy.Assess(rows);
        Assert.Equal(TugReadinessEngine.ReviewRequired, TugReadinessEngine.StateOf(checks));
        Assert.Contains(checks, c => c.Reason == "TowageCompetence:Unverified" && c.Result == TugReadinessEngine.Unknown);
    }

    [Fact]
    public void Missing_criteria_and_missing_evaluator_stay_unknown_and_a_failure_is_not_ready() {
        var checks = TugCrewCompetencePolicy.Assess([]);
        Assert.Equal(5, checks.Length); Assert.All(checks, c => Assert.Equal(TugReadinessEngine.Unknown, c.Result));
        var failed = TugCrewCompetencePolicy.Assess(AllVerified().Append(new(TugCrewCompetencePolicy.WorkRest, TugReadinessEngine.Fail, "RestHoursInsufficient", true)));
        Assert.Equal(TugReadinessEngine.NotReady, TugReadinessEngine.StateOf(failed));
    }
}
