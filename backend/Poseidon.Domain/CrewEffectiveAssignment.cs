namespace Poseidon.Domain;
// Source DEV-047/14.01 §6: status + actual period + provenance. A Draft is never inferred to be current.
public sealed record CrewAssignmentObservation(Guid Id, Guid VesselId, string Status, string Provenance, DateOnly? From, DateOnly? To, DateTimeOffset? SourceObservedAt);
public sealed record CrewEffectiveSelection(string State, Guid? AssignmentId, Guid? VesselId);
public static class CrewEffectiveAssignment
{
    public static CrewEffectiveSelection Select(IEnumerable<CrewAssignmentObservation> scoped, DateOnly date, DateTimeOffset receivedAt,
        IReadOnlyCollection<string> effectiveStates, IReadOnlyCollection<string> provenances, bool requireSourceObservation) {
        var candidates = scoped.Where(x => x.Status != "Draft" && effectiveStates.Contains(x.Status, StringComparer.Ordinal) && provenances.Contains(x.Provenance, StringComparer.Ordinal) &&
            x.From != null && x.From <= date && (x.To == null || date < x.To) && (!requireSourceObservation || x.SourceObservedAt != null && x.SourceObservedAt <= receivedAt)).Take(2).ToArray();
        return candidates.Length switch { 0 => new("Missing", null, null), 1 => new("ConfiguredEffectiveDeclaration", candidates[0].Id, candidates[0].VesselId), _ => new("Ambiguous", null, null) };
    }
}
