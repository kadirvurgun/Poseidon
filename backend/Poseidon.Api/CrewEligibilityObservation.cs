using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class CrewEligibilityObservation
{
    internal static async Task<CrewOwningInput> Read(PoseidonDbContext db, TrustedActor actor, FederationSettings settings, CrewAssignment assignment, DateTimeOffset now, CancellationToken ct) {
        if (!ComplianceInputContext.CrewRead(actor) || !OperationEquipmentCommands.Read(actor)) return new("eligibility_state", "Withheld", "DEV-050", "private_input_access_required");
        // Exact assignment/position consumer. No global Person eligibility badge.
        var candidates = await OperationEquipmentCommands.Evaluations(db, actor).Where(e => e.VesselId == assignment.VesselId &&
            db.EvaluationOwningReferences.Any(p => p.EvaluationId == e.Id && db.OperationEquipmentBindings.Any(b => b.VesselId == p.VesselId && b.Id == p.Id && b.Revision == p.Revision && b.AssignmentId == assignment.Id && b.AssignmentRevision == assignment.Revision)))
            .OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).Take(2).ToArrayAsync(ct);
        if (candidates.Length == 0) return new("eligibility_state", "EvaluationUnavailable", "DEV-050", "current_source_qualified_role_evaluation_required");
        var row = candidates[0];
        if ((await CurrentRegulatoryContext.Stale(db, row.Id, assignment.VesselId, now, ct, settings)).Length != 0) return new("eligibility_state", "Unknown", "DEV-050", "role_evaluation_inputs_changed");
        using var snapshot = JsonDocument.Parse(row.SnapshotJson);
        var facts = snapshot.RootElement.GetProperty("qualifications").EnumerateArray().Select(q => q.GetProperty("facts").GetProperty("crew.requiredRoleEligible").Deserialize<ContextFact>(RegulatoryQualificationEndpoints.Json)!).ToArray();
        if (facts.Length == 0 || facts.Any(f => f.Knowledge != "Known") || facts.Select(f => f.Value.GetBoolean()).Distinct().Count() != 1)
            return new("eligibility_state", "Unknown", "DEV-050", "source_criteria_incomplete_or_conflicting");
        return new("eligibility_state", facts[0].Value.GetBoolean() ? "QualifiedEligible" : "QualifiedIneligible", "DEV-050", "exact_source_criteria_assignment_position_period_only");
    }
}
