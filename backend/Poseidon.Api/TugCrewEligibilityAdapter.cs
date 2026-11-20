using Poseidon.Domain;

namespace Poseidon.Api;

// S7 integration: WP-B DEV-086 tug crew readiness consumes WP-A's shared, advisory crew
// eligibility engine (ICrewEligibilityEvaluator, DEV-050) through ITugCrewCompetenceSource.
// Semantics preserved from both sides:
// - The evaluator authorizes the caller's TrustedActor itself; a denied or out-of-scope call
//   yields REVIEW_REQUIRED, which maps to UNKNOWN here (never reveals crew existence).
// - UNKNOWN / REVIEW_REQUIRED never becomes PASS. A criterion with no configured requirement
//   of its type is UNKNOWN ("RequirementNotConfigured"), never NOT_APPLICABLE or PASS.
// - Training completion is not competence: TowageCompetence reads COMPETENCE requirements only.
// - The result is advisory input to TugReadinessEngine. It is never a GO, an assignment
//   decision or a Safe Manning decision; those stay attributed human records.
// - Only closed codes and position identifiers leave the adapter (no crew PII).
public sealed class CrewEligibilityTugCompetenceSource(ICrewEligibilityEvaluator evaluator) : ITugCrewCompetenceSource
{
    public const string SourceName = "CrewEligibilityEvaluator:S7-WPA";

    public async Task<TugCrewCompetenceObservation> Observe(TugCrewCompetenceQuery query, CancellationToken ct)
    {
        var window = new CrewEligibilityWindow(DateOnly.FromDateTime(query.WindowStart.UtcDateTime), DateOnly.FromDateTime(query.WindowEnd.UtcDateTime));
        if (!window.Valid)
            return new(SourceName, [.. TugCrewCompetencePolicy.Criteria.Select(c => Unknown(c, "EvaluationWindowInvalid"))], null);
        var evaluation = await evaluator.EvaluateVesselAsync(query.Actor, query.TugId, window, ct);
        return new(SourceName, Map(evaluation), null);
    }

    internal static TugCrewCriterion[] Map(SafeManningEvaluation evaluation)
    {
        if (evaluation.Members.Length == 0)
            return [.. TugCrewCompetencePolicy.Criteria.Select(c => Unknown(c, Reason(evaluation.Reasons, "NoCrewEvaluated")))];
        return [
            MinimumManning(evaluation),
            FromGaps(TugCrewCompetencePolicy.Credential, evaluation.Members, CrewRequirementTypes.CredentialBacked),
            FromGaps(TugCrewCompetencePolicy.TowageCompetence, evaluation.Members, t => t == CrewRequirementTypes.Competence),
            FromGaps(TugCrewCompetencePolicy.Familiarization, evaluation.Members, t => t == CrewRequirementTypes.Familiarization),
            WorkRest(evaluation.Members)
        ];
    }

    // The MSMD-based vessel state can only confirm coverage when the shared engine says ELIGIBLE.
    private static TugCrewCriterion MinimumManning(SafeManningEvaluation e) => e.State switch {
        CrewEligibilityStates.Eligible => new(TugCrewCompetencePolicy.MinimumManning, TugReadinessEngine.Pass, "SharedEngineEligible", true),
        CrewEligibilityStates.Attention => new(TugCrewCompetencePolicy.MinimumManning, TugReadinessEngine.Condition, Reason(e.Reasons, "SharedEngineAttention"), true),
        CrewEligibilityStates.NotEligible => new(TugCrewCompetencePolicy.MinimumManning, TugReadinessEngine.Fail, Reason(e.Reasons, "SharedEngineNotEligible"), true),
        _ => Unknown(TugCrewCompetencePolicy.MinimumManning, Reason(e.Reasons, "SharedEngineReviewRequired"))
    };

    private static TugCrewCriterion FromGaps(string criterion, CrewEligibilityResult[] members, Func<string, bool> type)
    {
        var gaps = members.SelectMany(m => m.Gaps.Where(g => type(g.Type)).Select(g => (Member: m, Gap: g)))
            .Where(x => x.Gap.State != CrewGapStates.NotApplicable).ToArray();
        if (gaps.Length == 0) return Unknown(criterion, "RequirementNotConfigured");
        var worst = gaps.FirstOrDefault(x => x.Gap.State == CrewGapStates.Gap);
        if (worst.Gap != null) return new(criterion, TugReadinessEngine.Fail, Code(worst.Gap.Reason, "RequirementGap"), true, worst.Member.PositionId);
        var review = gaps.FirstOrDefault(x => x.Gap.State is not (CrewGapStates.Satisfied or CrewGapStates.Attention));
        if (review.Gap != null) return new(criterion, TugReadinessEngine.Unknown, Code(review.Gap.Reason, "ReviewRequired"), false, review.Member.PositionId);
        var attention = gaps.FirstOrDefault(x => x.Gap.State == CrewGapStates.Attention);
        if (attention.Gap != null) return new(criterion, TugReadinessEngine.Condition, Code(attention.Gap.Reason, "Attention"), true, attention.Member.PositionId);
        // Every configured requirement is SATISFIED (the shared engine satisfies only from verified input);
        // a contributing member whose own evaluation is still REVIEW_REQUIRED keeps the criterion UNKNOWN.
        return members.Any(m => m.State == CrewEligibilityStates.ReviewRequired && gaps.Any(g => g.Member == m))
            ? Unknown(criterion, "MemberReviewRequired")
            : new(criterion, TugReadinessEngine.Pass, "AllConfiguredRequirementsSatisfied", true);
    }

    private static TugCrewCriterion WorkRest(CrewEligibilityResult[] members)
    {
        var reasons = members.SelectMany(m => m.Reasons.Where(r => r.Dimension == "WORK_REST").Select(r => (Member: m, Reason: r))).ToArray();
        var fail = reasons.FirstOrDefault(x => x.Reason.Severity == CrewEligibilityStates.NotEligible);
        if (fail.Reason != null) return new(TugCrewCompetencePolicy.WorkRest, TugReadinessEngine.Fail, Code(fail.Reason.Code, "WorkRestNonCompliant"), true, fail.Member.PositionId);
        var other = reasons.FirstOrDefault();
        if (other.Reason != null)
            return other.Reason.Severity == CrewEligibilityStates.Attention
                ? new(TugCrewCompetencePolicy.WorkRest, TugReadinessEngine.Condition, Code(other.Reason.Code, "WorkRestAttention"), true, other.Member.PositionId)
                : new(TugCrewCompetencePolicy.WorkRest, TugReadinessEngine.Unknown, Code(other.Reason.Code, "WorkRestUnknown"), false, other.Member.PositionId);
        return new(TugCrewCompetencePolicy.WorkRest, TugReadinessEngine.Pass, "WorkRestSourceCompliant", true);
    }

    private static TugCrewCriterion Unknown(string criterion, string reason) => new(criterion, TugReadinessEngine.Unknown, reason, false);
    private static string Reason(CrewEligibilityReason[] reasons, string fallback) => Code(reasons.FirstOrDefault()?.Code, fallback);
    private static string Code(string? code, string fallback) => string.IsNullOrWhiteSpace(code) || code.Length > 120 ? fallback : code;
}
