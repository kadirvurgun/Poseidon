using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record QualifiedRoleCriteria(bool Value, CrewRoleAssessmentReference Subject, string[] Hashes,
    DateTimeOffset ObservedAt, DateTimeOffset Until);

// Source-defined personal criteria are evaluated separately from vessel manning
// counts. Counting a person whose criteria pass must not depend on already having
// enough other people; aggregate role counts remain a separate consumer.
internal static class QualifiedRoleCriteriaReader
{
    internal static async Task<QualifiedRoleCriteria?> Read(PoseidonDbContext db, FederationSettings settings, Guid vessel,
        QualifiedEligibilityPolicy? policy, OwningInputReferences refs, DateTimeOffset at, CancellationToken ct, HashSet<Guid>? ancestors = null)
    {
        if (policy == null || policy.Criteria is not { Length: > 0 } criteria) return null;
        var observations = new List<bool?>(); var hashes = new List<string>(); var times = new List<DateTimeOffset>(); var expiries = new List<DateTimeOffset>();
        CrewRoleAssessmentReference? subject = null;
        foreach (var criterion in criteria) {
            var matching = new List<(ControlEvidenceRevision Row, ControlEvidenceContent Content, string Hash, DateTimeOffset Until)>();
            foreach (var id in refs.RoleAssessmentIds ?? []) {
                var record = await QualifiedOwningDocumentReader.Read(db, settings, vessel, id, "CrewRoleAssessment", at, ct, ancestors);
                if (record is { } rr && rr.Content.CrewRoleAssessment!.Criterion == criterion.Code && rr.Content.CrewRoleAssessment.PositionId == policy.PositionId &&
                    criterion.RequireCredential != null && (criterion.RequireCredential == false || rr.Content.CrewRoleAssessment.Credential != null) &&
                    await db.ControlEvidenceRequirements.AnyAsync(p => p.VesselId == vessel && p.Kind == "Evidence" && p.Id == id && p.Revision == rr.Row.Revision && p.RuleId == criterion.RuleId && p.RuleRevision == criterion.RuleRevision, ct)) matching.Add(rr);
            }
            if (matching.Count != 1) return null;
            var found = matching[0]; var assessment = found.Content.CrewRoleAssessment!;
            if (subject != null && (subject.AssignmentId != assessment.AssignmentId || subject.AssignmentRevision != assessment.AssignmentRevision ||
                subject.AssignmentVersion != assessment.AssignmentVersion || subject.PositionId != assessment.PositionId || subject.PositionVersion != assessment.PositionVersion ||
                subject.From != assessment.From || subject.To != assessment.To)) return null;
            subject = assessment; observations.Add(assessment.Satisfied); hashes.Add(found.Hash); times.Add(found.Content.ObservedAt!.Value); expiries.Add(found.Until);
        }
        return ComplianceInputs.Eligibility(observations) is { } value && subject != null ? new(value, subject, hashes.ToArray(), times.Min(), expiries.Min()) : null;
    }
}
