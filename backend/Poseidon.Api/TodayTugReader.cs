using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TodayTugEvidenceDto(Guid JobId, long RequestRevision, bool RequestIncomplete,
    long? PlanRevision, long? PlannedRequestRevision, DateTimeOffset? PlannedStart, DateTimeOffset? PlannedEnd,
    Guid[] AssignedTugs, Guid? CurrentOwnerRoleId, TugPlanningEvidence? Evidence);
internal sealed record TodayTugProjection(TodayTugEvidenceDto Detail, TodayTugAdvisory Advice,
    DateTimeOffset SavedAt, string Provenance);

internal static class TodayTugReader
{
    internal static async Task<TodayTugProjection[]?> Read(Guid vessel, HttpContext http, PoseidonDbContext db,
        TrustedActor actor, DateTimeOffset readAt, Guid? jobId = null)
    {
        // Module + mode + vessel gates precede jobs, counts, plans and linked identifiers.
        if (!actor.CanReadTug || !await actor.Scope(db.Vessels).AnyAsync(v => v.Id == vessel && v.Kind == VesselKind.Tug, http.RequestAborted)) return null;
        var visible = await actor.Scope(db.Vessels).Where(v => v.Kind == VesselKind.Tug).Select(v => v.Id).ToArrayAsync(http.RequestAborted);
        var jobs = await db.TugJobs.AsNoTracking().Where(j => j.VesselId == vessel && (jobId == null || j.Id == jobId))
            .OrderBy(j => j.Id).ToArrayAsync(http.RequestAborted);
        var owners = TugEndpoints.Owners(http, vessel).ToHashSet();
        var result = new List<TodayTugProjection>();
        foreach (var job in jobs) {
            var head = await db.TugDraftPlans.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.JobId == job.Id, http.RequestAborted);
            TugDraftPlanRevision? revision = null;
            TugPlanningEvidence? evidence = null;
            TugPlanInputs? inputs = null;
            if (head != null) {
                revision = await db.TugDraftPlanRevisions.AsNoTracking().SingleAsync(r => r.VesselId == vessel && r.JobId == job.Id && r.Revision == head.Revision, http.RequestAborted);
                inputs = revision.Inputs();
                // A hidden linked tug suppresses the whole projection, before pagination.
                // It must not be disclosed as an empty/missing plan or a partial assignment.
                if (inputs.AssignedTugs.Any(t => !visible.Contains(t))) continue;
                evidence = await TugPlanningEvidenceAdapter.Read(db, actor, http.RequestServices.GetRequiredService<TugPlanningPolicy>(),
                    http.RequestServices.GetRequiredService<MaintenanceObservationSettings>(), job.Id, inputs, http.RequestAborted,
                    http.RequestServices.GetRequiredService<FederationSettings>(), CrewFilePolicy.Current(http), http, readAt);
            }
            var request = job.Inputs();
            var owner = request.RequestOwner is Guid role && owners.Contains(role) ? (Guid?)role : null;
            var incomplete = TugRequestPolicy.Missing(request).Length != 0;
            var advice = TodayTugPolicy.Observe(new(head != null, revision != null && revision.RequestRevision != job.Revision,
                incomplete, evidence?.Readiness == "Blocked", evidence?.ConflictState ?? "Unknown"));
            result.Add(new(new(job.Id, job.Revision, incomplete, head?.Revision, revision?.RequestRevision,
                inputs?.PlannedStart, inputs?.PlannedEnd, inputs?.AssignedTugs ?? [], owner, evidence), advice,
                revision?.SavedAt ?? job.SavedAt, revision?.Provenance ?? job.Provenance));
        }
        return result.ToArray();
    }
}
