using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-050 owning-input observation for one assignment. S7 WP-A correction: a source
// rule fact ("crew.requiredRoleEligible") alone never yields a positive state any more.
// The state is the advisory CrewEligibilityEvaluator outcome, which needs verified,
// human-backed credential/competence inputs and a known work/rest source. A Known
// negative rule fact can only make the outcome more restrictive.
internal static class CrewEligibilityObservation
{
    internal static async Task<CrewOwningInput> Read(PoseidonDbContext db, TrustedActor actor, FederationSettings settings, CrewAssignment assignment,
        DateTimeOffset now, CancellationToken ct, CrewEligibilityEvaluator? evaluator = null) {
        if (evaluator == null || !CrewEligibilityEvaluator.CanEvaluate(actor)) return new("eligibility_state", "Withheld", "DEV-050", "private_input_access_required");
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        var result = await evaluator.CoreAsync(assignment, new(date, date), now, ct);
        if (await RuleNegative(db, actor, settings, assignment, now, ct))
            return new("eligibility_state", CrewEligibilityStates.NotEligible, "DEV-050", "source_criteria_not_met");
        var reason = result.Reasons.FirstOrDefault(r => r.Severity == result.State)?.Code ?? "human_backed_inputs_complete_advisory_only";
        return new("eligibility_state", result.State, "DEV-050", reason.ToLowerInvariant());
    }

    // Exact assignment/position consumer of a current source-qualified role evaluation.
    private static async Task<bool> RuleNegative(PoseidonDbContext db, TrustedActor actor, FederationSettings settings, CrewAssignment assignment, DateTimeOffset now, CancellationToken ct) {
        if (!ComplianceInputContext.CrewRead(actor) || !OperationEquipmentCommands.Read(actor)) return false;
        var candidates = await OperationEquipmentCommands.Evaluations(db, actor).Where(e => e.VesselId == assignment.VesselId &&
            db.EvaluationOwningReferences.Any(p => p.EvaluationId == e.Id && db.OperationEquipmentBindings.Any(b => b.VesselId == p.VesselId && b.Id == p.Id && b.Revision == p.Revision && b.AssignmentId == assignment.Id && b.AssignmentRevision == assignment.Revision)))
            .OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).Take(2).ToArrayAsync(ct);
        if (candidates.Length == 0) return false;
        var row = candidates[0];
        if ((await CurrentRegulatoryContext.Stale(db, row.Id, assignment.VesselId, now, ct, settings)).Length != 0) return false;
        using var snapshot = JsonDocument.Parse(row.SnapshotJson);
        var facts = snapshot.RootElement.GetProperty("qualifications").EnumerateArray().Select(q => q.GetProperty("facts").GetProperty("crew.requiredRoleEligible").Deserialize<ContextFact>(RegulatoryQualificationEndpoints.Json)!).ToArray();
        return facts.Length > 0 && facts.All(f => f.Knowledge == "Known") && facts.All(f => !f.Value.GetBoolean());
    }
}
