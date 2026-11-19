using Poseidon.Domain;

namespace Poseidon.Api;

// STABLE CONSUMER CONTRACT (S7 WP-A, DEV-050). WP-B tug assignment/readiness and any
// other module consume crew eligibility only through this interface (resolve it from
// DI: scoped). Results are ADVISORY: State is one of CrewEligibilityStates
// (ELIGIBLE / ATTENTION / NOT_ELIGIBLE / REVIEW_REQUIRED) with machine reason codes.
// - Never a GO, assignment decision, release or Safe Manning decision (that is the
//   human CrewSafeManningDecision record of an appointed Master/DPA).
// - UNKNOWN, unverified, expired or unavailable inputs never yield ELIGIBLE.
// - The caller's TrustedActor is authorized inside the evaluator (vessel scope plus
//   crew.read and crew.eligibility.read or crew.safe_manning.read); a denied or
//   out-of-scope call returns REVIEW_REQUIRED / PROTECTED_CREW_SOURCE_ACCESS_REQUIRED
//   and never reveals whether the assignment exists.
// - Results carry identifiers and codes only: no names, employee numbers,
//   certificate numbers or other crew PII.
public interface ICrewEligibilityEvaluator
{
    Task<CrewEligibilityResult> EvaluateAssignmentAsync(TrustedActor actor, Guid vesselId, Guid assignmentId,
        CrewEligibilityWindow window, CancellationToken ct);
    Task<SafeManningEvaluation> EvaluateVesselAsync(TrustedActor actor, Guid vesselId, CrewEligibilityWindow window, CancellationToken ct);
}

// Work/rest hours (58.04) have no owning source module yet. The default source
// reports UNKNOWN, which makes every evaluation REVIEW_REQUIRED, never ELIGIBLE.
public interface ICrewWorkRestSource
{
    Task<CrewWorkRestFact> ReadAsync(Guid vesselId, Guid assignmentId, CrewEligibilityWindow window, CancellationToken ct);
}

public sealed class UnavailableCrewWorkRestSource : ICrewWorkRestSource
{
    public Task<CrewWorkRestFact> ReadAsync(Guid vesselId, Guid assignmentId, CrewEligibilityWindow window, CancellationToken ct) =>
        Task.FromResult(new CrewWorkRestFact(CrewWorkRestStates.Unknown, "WORK_REST_SOURCE_UNAVAILABLE"));
}
