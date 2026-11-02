using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TugOperationalConflict(Guid TugId, string Kind, Guid ResourceId, long Revision,
    string State, string Why, string Provenance, DateTimeOffset? ObservedAt);

internal static class TugOperationalConflicts
{
    internal static async Task<TugOperationalConflict[]> Read(PoseidonDbContext db, TrustedActor actor, Guid jobId,
        Guid[] tugs, DateTimeOffset? start, DateTimeOffset? end, CancellationToken ct) {
        var result = new List<TugOperationalConflict>();
        if (start == null || end == null || start >= end) return [];
        var visible = actor.Scope(db.Vessels).Select(v => v.Id);
        // Existing recorded operational assignment observations are separate from draft
        // plan links. A decision-reference string alone never confers Master authority.
        var assigned = await db.TugAssignments.AsNoTracking().Where(a => a.JobId != jobId && tugs.Contains(a.TugId) && visible.Contains(a.TugId) && visible.Contains(a.VesselId) &&
            db.TugJobs.Any(j => j.Id == a.JobId && j.VesselId == a.VesselId && j.Revision == a.JobRevision)).ToArrayAsync(ct);
        foreach (var a in assigned) {
            var job = await db.TugJobs.AsNoTracking().SingleAsync(j => j.Id == a.JobId && j.VesselId == a.VesselId, ct);
            // Requested times are retained as requested observations, never relabeled actual.
            if (job.RequestedStart == null || job.RequestedEnd == null || job.RequestedStart < end && start < job.RequestedEnd)
                result.Add(new(a.TugId, "RecordedTugAssignment", a.Id, a.JobRevision, "HumanReviewRequired",
                    job.RequestedStart == null || job.RequestedEnd == null ? "RecordedAssignmentWindowUnknown" : "RecordedAssignmentRequestedWindowOverlap",
                    "RecordedReference;AuthorityAndActualWindowUnverified", a.RecordedAt));
        }
        if (!actor.Permissions.Contains("crew.read") || !actor.Permissions.Contains("crew.identity.read")) return result.ToArray();
        var from = DateOnly.FromDateTime(start.Value.UtcDateTime);
        // Date-only crew declarations cannot establish sub-day separation.
        var until = DateOnly.FromDateTime(end.Value.UtcDateTime);
        var own = await db.CrewAssignments.AsNoTracking().Where(a => tugs.Contains(a.VesselId) && visible.Contains(a.VesselId) && a.From != null && a.From <= until && (a.To == null || a.To > from)).ToArrayAsync(ct);
        foreach (var a in own) {
            var others = await db.CrewAssignments.AsNoTracking().Where(b => b.Id != a.Id && b.PersonId == a.PersonId && b.VesselId != a.VesselId && visible.Contains(b.VesselId) &&
                b.From != null && b.From <= until && (b.To == null || b.To > from) && (a.To == null || b.From < a.To) && (b.To == null || a.From < b.To)).ToArrayAsync(ct);
            foreach (var b in others) result.Add(new(a.VesselId, "CrewAssignment", b.Id, b.Revision, "HumanReviewRequired",
                a.To == null || b.To == null ? "RecordedCrewOverlapOpenEndReviewRequired" : "RecordedCrewAssignmentOverlap",
                b.Provenance, b.SourceObservedAt));
        }
        return result.ToArray();
    }
}
