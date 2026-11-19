using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// S7 integration (DEV-086 <- DEV-050): CrewEligibilityTugCompetenceSource maps the advisory WP-A
// engine onto WP-B tug crew criteria. Pure unit test (no host, no PostgreSQL).
public sealed class TugCrewEligibilityAdapterTests
{
    private static readonly Guid Tug = Guid.Parse("7e000000-0000-0000-0000-000000000001");
    private static readonly Guid Position = Guid.Parse("7e000000-0000-0000-0000-000000000002");
    private static readonly TrustedActor Actor = new(Guid.NewGuid(), Guid.NewGuid(), ["crew.read"], true, false, []);
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 6, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeEvaluator(SafeManningEvaluation result) : ICrewEligibilityEvaluator
    {
        public CrewEligibilityWindow? Window;
        public Task<CrewEligibilityResult> EvaluateAssignmentAsync(TrustedActor actor, Guid vesselId, Guid assignmentId, CrewEligibilityWindow window, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<SafeManningEvaluation> EvaluateVesselAsync(TrustedActor actor, Guid vesselId, CrewEligibilityWindow window, CancellationToken ct)
        { Window = window; return Task.FromResult(result); }
    }

    private static CrewRequirementGap Gap(string type, string state, string reason = "R") => new(Guid.NewGuid(), type, true, state, reason, null);
    private static CrewEligibilityResult Member(string state, CrewRequirementGap[] gaps, params CrewEligibilityReason[] reasons) =>
        new(Guid.NewGuid(), Position, state, reasons, gaps, CrewEligibilityPolicy.Qualifications);
    private static SafeManningEvaluation Vessel(string state, CrewEligibilityResult[] members, params CrewEligibilityReason[] reasons) =>
        new(Tug, state, reasons, SafeManningPolicy.MsmdCurrent, null, null, [], members, new(new(2026, 11, 20), new(2026, 11, 20)),
            Start, new string('0', 64), SafeManningPolicy.Qualifications);

    private static async Task<TugCrewCriterion[]> Observe(SafeManningEvaluation evaluation, DateTimeOffset? end = null)
    {
        var observed = await new CrewEligibilityTugCompetenceSource(new FakeEvaluator(evaluation))
            .Observe(new(Guid.NewGuid(), Tug, Guid.NewGuid(), Start, end ?? Start.AddHours(4), Actor), Ct);
        Assert.Equal(CrewEligibilityTugCompetenceSource.SourceName, observed.Source);
        Assert.Null(observed.ValidUntil);
        return observed.Criteria;
    }

    [Fact]
    public async Task Review_required_or_denied_evaluation_without_members_is_unknown_for_every_criterion()
    {
        var criteria = await Observe(Vessel(CrewEligibilityStates.ReviewRequired, [],
            new CrewEligibilityReason("PROTECTED_CREW_SOURCE_ACCESS_REQUIRED", "ACCESS", CrewEligibilityStates.ReviewRequired)));
        Assert.Equal(TugCrewCompetencePolicy.Criteria, criteria.Select(c => c.Criterion));
        Assert.All(criteria, c => { Assert.Equal(TugReadinessEngine.Unknown, c.Result); Assert.False(c.Verified); });
        Assert.All(criteria, c => Assert.Equal("PROTECTED_CREW_SOURCE_ACCESS_REQUIRED", c.Reason));
        var checks = TugCrewCompetencePolicy.Assess(criteria);
        Assert.Equal(TugReadinessEngine.ReviewRequired, TugReadinessEngine.StateOf(checks));
    }

    [Fact]
    public async Task Unconfigured_requirement_types_and_unavailable_work_rest_never_become_pass()
    {
        var member = Member(CrewEligibilityStates.ReviewRequired, [Gap(CrewRequirementTypes.Certificate, CrewGapStates.Satisfied)],
            new CrewEligibilityReason("WORK_REST_SOURCE_UNAVAILABLE", "WORK_REST", CrewEligibilityStates.ReviewRequired));
        var criteria = (await Observe(Vessel(CrewEligibilityStates.ReviewRequired, [member]))).ToDictionary(c => c.Criterion);
        Assert.Equal(TugReadinessEngine.Unknown, criteria[TugCrewCompetencePolicy.MinimumManning].Result);
        // Satisfied certificate gap but the member is still REVIEW_REQUIRED overall.
        Assert.Equal(TugReadinessEngine.Unknown, criteria[TugCrewCompetencePolicy.Credential].Result);
        Assert.Equal("RequirementNotConfigured", criteria[TugCrewCompetencePolicy.TowageCompetence].Reason);
        Assert.Equal(TugReadinessEngine.Unknown, criteria[TugCrewCompetencePolicy.Familiarization].Result);
        Assert.Equal(TugReadinessEngine.Unknown, criteria[TugCrewCompetencePolicy.WorkRest].Result);
        Assert.DoesNotContain(criteria.Values, c => c.Result == TugReadinessEngine.Pass);
    }

    [Fact]
    public async Task Training_completion_is_not_towage_competence_and_known_gaps_fail()
    {
        var member = Member(CrewEligibilityStates.NotEligible, [
            Gap(CrewRequirementTypes.Training, CrewGapStates.Satisfied),
            Gap(CrewRequirementTypes.Medical, CrewGapStates.Gap, "MEDICAL_EXPIRED"),
            Gap(CrewRequirementTypes.Familiarization, CrewGapStates.ReviewRequired, "FAMILIARIZATION_UNVERIFIED")]);
        var criteria = (await Observe(Vessel(CrewEligibilityStates.NotEligible, [member],
            new CrewEligibilityReason("ROLE_UNDER_MINIMUM", "CREW", CrewEligibilityStates.NotEligible)))).ToDictionary(c => c.Criterion);
        Assert.Equal(TugReadinessEngine.Fail, criteria[TugCrewCompetencePolicy.MinimumManning].Result);
        Assert.Equal((TugReadinessEngine.Fail, "MEDICAL_EXPIRED", (Guid?)Position),
            (criteria[TugCrewCompetencePolicy.Credential].Result, criteria[TugCrewCompetencePolicy.Credential].Reason, criteria[TugCrewCompetencePolicy.Credential].PositionId));
        Assert.Equal("RequirementNotConfigured", criteria[TugCrewCompetencePolicy.TowageCompetence].Reason);
        Assert.Equal(TugReadinessEngine.Unknown, criteria[TugCrewCompetencePolicy.Familiarization].Result);
        Assert.Equal(TugReadinessEngine.NotReady, TugReadinessEngine.StateOf(TugCrewCompetencePolicy.Assess(criteria.Values)));
    }

    [Fact]
    public async Task Fully_satisfied_engine_result_is_crew_evidence_only_and_never_a_go()
    {
        var gaps = new[] { CrewRequirementTypes.Certificate, CrewRequirementTypes.Competence, CrewRequirementTypes.Familiarization }
            .Select(t => Gap(t, CrewGapStates.Satisfied)).ToArray();
        var criteria = await Observe(Vessel(CrewEligibilityStates.Eligible, [Member(CrewEligibilityStates.Eligible, gaps)]));
        Assert.All(criteria, c => { Assert.Equal(TugReadinessEngine.Pass, c.Result); Assert.True(c.Verified); });
        // Crew criteria are evidence for the crew categories only; the GO/NO_GO stays a separate human record.
        var crewChecks = TugCrewCompetencePolicy.Assess(criteria);
        Assert.All(crewChecks, c => Assert.Contains(c.Category, new[] { "crew", "crew_competence" }));
        Assert.Equal("MasterOrTugMasterHumanDecisionRequired;EngineNeverIssuesGo", TugReadinessEngine.DecisionAuthority);
    }

    [Fact]
    public async Task Attention_lowers_to_conditional_and_invalid_window_is_not_evaluated()
    {
        var member = Member(CrewEligibilityStates.Attention, [Gap(CrewRequirementTypes.Certificate, CrewGapStates.Attention, "EXPIRES_IN_WINDOW")]);
        var criteria = (await Observe(Vessel(CrewEligibilityStates.Attention, [member]))).ToDictionary(c => c.Criterion);
        Assert.Equal(TugReadinessEngine.Condition, criteria[TugCrewCompetencePolicy.Credential].Result);

        var evaluator = new FakeEvaluator(Vessel(CrewEligibilityStates.Eligible, []));
        var observed = await new CrewEligibilityTugCompetenceSource(evaluator).Observe(new(Guid.NewGuid(), Tug, Guid.NewGuid(), Start, Start.AddDays(400), Actor), Ct);
        Assert.Null(evaluator.Window);
        Assert.All(observed.Criteria, c => Assert.Equal((TugReadinessEngine.Unknown, "EvaluationWindowInvalid"), (c.Result, c.Reason)));
    }
}
