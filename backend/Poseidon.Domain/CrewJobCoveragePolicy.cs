namespace Poseidon.Domain;

public sealed record ManningCandidate(Guid PositionId, Guid PersonId, bool? Qualified);
public sealed record ManningRoleCoverage(Guid PositionId, int Required, int Declared, int Qualified, int Unknown,
    string State, string Reason);
public sealed record ManningCoverage(string State, string Reason, ManningRoleCoverage[] Roles);

// Minimum source-defined role coverage only. This neither allocates people to
// duties nor establishes fitness, work/rest, job briefing or a Master decision.
public static class CrewJobCoveragePolicy
{
    public static ManningCoverage Observe(MsmdRoleRequirement[] roles, ManningCandidate[] candidates)
    {
        if (roles.Length == 0 || roles.Length > 128 || roles.Any(r => r.PositionId == Guid.Empty || r.Count is < 1 or > 1000) ||
            roles.Select(r => r.PositionId).Distinct().Count() != roles.Length ||
            candidates.Any(c => c.PositionId == Guid.Empty || c.PersonId == Guid.Empty))
            return new("ReviewRequired", "SourceRoleProfileInvalid", []);
        var result = new List<ManningRoleCoverage>();
        foreach (var role in roles.OrderBy(r => r.PositionId)) {
            var people = candidates.Where(c => c.PositionId == role.PositionId).GroupBy(c => c.PersonId)
                .Select(group => {
                    var states = group.Select(c => c.Qualified).Distinct().ToArray();
                    return states.Length == 1 ? states[0] : null;
                }).ToArray();
            var qualified = people.Count(x => x == true); var unknown = people.Count(x => x == null);
            var state = qualified >= role.Count ? "Current" : unknown > 0 ? "ReviewRequired" : "Blocked";
            result.Add(new(role.PositionId, role.Count, people.Length, qualified, unknown, state,
                state == "Current" ? "SourceQualifiedRoleCountMet" : state == "Blocked" ? "SourceQualifiedRoleCountNotMet" : "CurrentRoleQualificationIncomplete"));
        }
        var positions = roles.Select(r => r.PositionId).ToHashSet();
        if (candidates.Where(c => positions.Contains(c.PositionId)).GroupBy(c => c.PersonId)
            .Any(g => g.Select(c => c.PositionId).Distinct().Count() > 1))
            return new("ReviewRequired", "CrossRoleAllocationReviewRequired", result.ToArray());
        return new(result.Any(r => r.State == "Blocked") ? "Blocked" : result.Any(r => r.State == "ReviewRequired") ? "ReviewRequired" : "CurrentMinimumRoleCoverage",
            result.Any(r => r.State == "Blocked") ? "SourceQualifiedRoleCountNotMet" : result.Any(r => r.State == "ReviewRequired") ? "CurrentRoleQualificationIncomplete" : "SourceQualifiedMinimumRolesOnly", result.ToArray());
    }
}
