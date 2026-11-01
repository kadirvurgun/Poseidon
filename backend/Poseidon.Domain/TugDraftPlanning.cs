using System.Text.Json.Serialization;

namespace Poseidon.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugPlanChange(string Operation, DateTimeOffset? PlannedStart = null, DateTimeOffset? PlannedEnd = null,
    Guid? TugId = null, Guid? ReplacementTugId = null);
public sealed record TugPlanInputs(DateTimeOffset? PlannedStart, DateTimeOffset? PlannedEnd, Guid[] AssignedTugs);

// FR/TC-tug-job-board-planned_start/planned_end/assigned_tugs; detail rows1955..1957.
// An ordinary preparation command: never an appointment, release, permit or safety decision.
public static class TugDraftPlanning
{
    public const string SourceVersion = "14.02:2316:1932-1957:v46.3";
    public static TugPlanInputs Apply(TugPlanInputs current, TugPlanChange change) {
        if (current.AssignedTugs.Length > 1000 || current.AssignedTugs.Distinct().Count() != current.AssignedTugs.Length)
            throw new ArgumentException("invalid_plan");
        var tugs = current.AssignedTugs.ToList();
        switch (change.Operation) {
            case "Window" when change.TugId == null && change.ReplacementTugId == null:
                if (TugPlanningWindowPolicy.Assess(new(change.PlannedStart, change.PlannedEnd)) == TugPlanningWindowState.InvalidOrder)
                    throw new ArgumentException("invalid_window");
                // Missing inputs are legal in drafts; no requested-time fallback.
                return current with { PlannedStart = change.PlannedStart, PlannedEnd = change.PlannedEnd };
            case "Assign" when change.TugId is Guid id && id != Guid.Empty && change.ReplacementTugId == null && change.PlannedStart == null && change.PlannedEnd == null:
                if (tugs.Contains(id) || tugs.Count == 1000) throw new ArgumentException("assignment_conflict");
                tugs.Add(id); break;
            case "Remove" when change.TugId is Guid id && change.ReplacementTugId == null && change.PlannedStart == null && change.PlannedEnd == null:
                if (!tugs.Remove(id)) throw new ArgumentException("assignment_conflict"); break;
            case "Reassign" when change.TugId is Guid id && change.ReplacementTugId is Guid replacement && replacement != Guid.Empty && change.PlannedStart == null && change.PlannedEnd == null:
                if (!tugs.Remove(id) || tugs.Contains(replacement) || replacement == id) throw new ArgumentException("assignment_conflict");
                tugs.Add(replacement); break;
            default: throw new ArgumentException("invalid_operation");
        }
        return current with { AssignedTugs = tugs.Order().ToArray() };
    }
}
