using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TugHistoryExecutionDto(Guid Id, Guid TugId, string State, string VerificationState, DateTimeOffset StartedAt, DateTimeOffset? EndedAt,
    long? DurationMinutes, Guid StartedBy, Guid? EndedBy, Guid? VerifiedBy, long Sequence, int EvidenceEvents, string LastEventHash);
public sealed record TugHistoryDailyLogDto(Guid Id, Guid TugId, DateOnly LogDate, string Watch, string State, bool LinkedToExecution);
public sealed record TugHistoryItemDto(Guid JobId, Guid VesselId, string JobNumber, string JobStatus, string? JobType, string? AssistedVessel, string? OperationArea,
    DateTimeOffset At, DateTimeOffset? CancelledAt, string Outcome, string Evidence, string? PlanState, string? BriefingState, TugHistoryExecutionDto[] Executions,
    TugHistoryDailyLogDto[] DailyLogs, string SafetyEvents);
public sealed record TugHistoryPageDto(TugHistoryItemDto[] Items, string? NextCursor, string[] Outcomes, DateTimeOffset ReadAt,
    string Deletion = "NoSilentDeletion;CancelledJobsRemainVisible", string SourceVersion = TugOperationHistoryPolicy.SourceVersion);

// DEV-091 / T-TUG-012 GET /api/v1/tug-operation-history (06.01 p236; 13.02 screen 185; 15.01 p1609). Read-only projection of
// tug jobs in the actor's tug scope with their executions, Master/actor records, verification, evidence events and linked
// daily logs. Filters: vesselId, tugId, jobId, from, to (UTC dates, inclusive), outcome; keyset paging by activity time.
// Cancelled jobs and aborted/rejected executions remain visible (no silent deletion); nothing is inferred as completed.
public static class TugOperationHistoryEndpoints
{
    private const string Read = "TugOperationHistoryRead";
    private static readonly string[] Keys = ["vesselId", "tugId", "jobId", "from", "to", "outcome", "cursor"];

    public static void MapTugOperationHistory(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/tug-operation-history", List).RequireAuthorization("DirectoryRead").WithName("TugOperationHistoryList").Produces<TugHistoryPageDto>();

    internal sealed record Filter(Guid? Vessel, Guid? Tug, Guid? Job, DateTimeOffset? From, DateTimeOffset? To, string? Outcome, (DateTimeOffset At, Guid Id)? After);

    internal static bool TryFilter(IQueryCollection query, out Filter filter)
    {
        filter = new(null, null, null, null, null, null, null);
        if (query.Any(q => !Keys.Contains(q.Key) || q.Value.Count != 1)) return false;
        Guid? Id(string key, out bool ok) {
            ok = true; if (!query.ContainsKey(key)) return null;
            ok = Guid.TryParseExact(query[key], "D", out var v) && v != Guid.Empty; return v;
        }
        DateTimeOffset? Day(string key, out bool ok) {
            ok = true; if (!query.ContainsKey(key)) return null;
            ok = DateOnly.TryParseExact(query[key], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d);
            return ok ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
        }
        var vessel = Id("vesselId", out var v1); var tug = Id("tugId", out var v2); var job = Id("jobId", out var v3);
        var from = Day("from", out var v4); var to = Day("to", out var v5);
        if (!(v1 && v2 && v3 && v4 && v5)) return false;
        if (from != null && to != null && (to < from || (to.Value - from.Value).TotalDays > TugOperationHistoryPolicy.MaxRangeDays)) return false;
        string? outcome = query.ContainsKey("outcome") ? query["outcome"].ToString() : null;
        if (outcome != null && !TugOperationHistoryPolicy.Outcomes.Contains(outcome)) return false;
        (DateTimeOffset, Guid)? after = null;
        if (query.ContainsKey("cursor")) {
            if (!TugOperationHistoryPolicy.TryCursor(query["cursor"], out var at, out var id)) return false;
            after = (at, id);
        }
        filter = new(vessel, tug, job, from, to?.AddDays(1), outcome, after);
        return true;
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (a == null || !a.CanReadTug || db.TenantId != a.TenantId) return await TowageCommands.Fail(h, Read, 403, "permission_denied");
        if (!TryFilter(h.Request.Query, out var f)) return await TowageCommands.Fail(h, Read, 400, "invalid_query");
        var visible = TugReadinessHttp.Tugs(db, a).Select(v => v.Id);
        if (f.Vessel is { } scoped && !await visible.AnyAsync(v => v == scoped, ct)) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", scoped);
        if (f.Tug is { } tugScoped && !await visible.AnyAsync(v => v == tugScoped, ct)) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", tugScoped);

        var jobs = db.TugJobs.AsNoTracking().Where(j => visible.Contains(j.VesselId));
        if (f.Vessel is { } vessel) jobs = jobs.Where(j => j.VesselId == vessel);
        if (f.Job is { } job) jobs = jobs.Where(j => j.Id == job);
        if (f.Tug is { } tug) jobs = jobs.Where(j => j.VesselId == tug || db.TowageExecutions.Any(x => x.JobId == j.Id && x.VesselId == j.VesselId && x.TugId == tug) ||
            db.TugAssignmentProposals.Any(p => p.JobId == j.Id && p.VesselId == j.VesselId && p.TugId == tug));
        const string active = TowageExecutionPolicy.Active, suspended = TowageExecutionPolicy.Suspended, completed = TowageExecutionPolicy.Completed, aborted = TowageExecutionPolicy.Aborted;
        const string cancelled = TugJobLifecycle.Cancelled;
        var projected = jobs.Select(j => new {
            Job = j,
            At = db.TowageExecutions.Where(x => x.JobId == j.Id && x.VesselId == j.VesselId).Min(x => (DateTimeOffset?)x.StartedAt) ?? j.RequestedStart ?? j.SavedAt,
            Any = db.TowageExecutions.Any(x => x.JobId == j.Id && x.VesselId == j.VesselId),
            Running = db.TowageExecutions.Any(x => x.JobId == j.Id && x.VesselId == j.VesselId && (x.State == active || x.State == suspended)),
            AllCompleted = db.TowageExecutions.Where(x => x.JobId == j.Id && x.VesselId == j.VesselId).All(x => x.State == completed),
            AllAborted = db.TowageExecutions.Where(x => x.JobId == j.Id && x.VesselId == j.VesselId).All(x => x.State == aborted),
        });
        if (f.From is { } from) projected = projected.Where(x => x.At >= from);
        if (f.To is { } to) projected = projected.Where(x => x.At < to);
        projected = f.Outcome switch {
            TugOperationHistoryPolicy.Cancelled => projected.Where(x => x.Job.Status == cancelled),
            TugOperationHistoryPolicy.NotStarted => projected.Where(x => x.Job.Status != cancelled && !x.Any),
            TugOperationHistoryPolicy.InProgress => projected.Where(x => x.Job.Status != cancelled && x.Running),
            TugOperationHistoryPolicy.Completed => projected.Where(x => x.Job.Status != cancelled && x.Any && x.AllCompleted),
            TugOperationHistoryPolicy.Aborted => projected.Where(x => x.Job.Status != cancelled && x.Any && x.AllAborted),
            TugOperationHistoryPolicy.Mixed => projected.Where(x => x.Job.Status != cancelled && x.Any && !x.Running && !x.AllCompleted && !x.AllAborted),
            _ => projected
        };
        if (f.After is { } after) projected = projected.Where(x => x.At < after.At || x.At == after.At && x.Job.Id.CompareTo(after.Id) > 0);
        var page = await projected.OrderByDescending(x => x.At).ThenBy(x => x.Job.Id).Take(TugOperationHistoryPolicy.PageSize + 1)
            .Select(x => new { x.Job, x.At }).ToArrayAsync(ct);
        var shown = page.Take(TugOperationHistoryPolicy.PageSize).ToArray();
        var ids = shown.Select(x => x.Job.Id).ToArray();

        var executions = await db.TowageExecutions.AsNoTracking().Where(x => ids.Contains(x.JobId) && visible.Contains(x.TugId)).OrderBy(x => x.StartedAt).ToArrayAsync(ct);
        var executionIds = executions.Select(x => x.Id).ToArray();
        var evidence = await db.TowageExecutionEvents.AsNoTracking().Where(e => executionIds.Contains(e.ExecutionId) && e.Kind == "EVIDENCE")
            .GroupBy(e => e.ExecutionId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var plans = await db.TowagePlans.AsNoTracking().Where(p => ids.Contains(p.JobId)).Select(p => new { p.JobId, p.VesselId, p.State }).ToArrayAsync(ct);
        var briefings = await db.TowageBriefings.AsNoTracking().Where(b => ids.Contains(b.JobId)).Select(b => new { b.JobId, b.VesselId, b.State }).ToArrayAsync(ct);
        // Daily logs of the executing tugs on the execution dates; linkage is what the log itself records.
        var tugs = executions.Select(x => x.TugId).Distinct().ToArray();
        var minDay = executions.Length == 0 ? DateOnly.MaxValue : DateOnly.FromDateTime(executions.Min(x => x.StartedAt).UtcDateTime).AddDays(-1);
        var maxDay = executions.Length == 0 ? DateOnly.MinValue : DateOnly.FromDateTime(executions.Max(x => x.EndedAt ?? x.StartedAt).UtcDateTime).AddDays(1);
        TugDailyLog[] logs = executions.Length == 0 ? [] : await db.TugDailyLogs.AsNoTracking().Where(l => tugs.Contains(l.VesselId) && l.LogDate >= minDay && l.LogDate <= maxDay)
            .OrderBy(l => l.LogDate).Take(500).ToArrayAsync(ct);
        var logLinks = logs.Select(l => (Log: l, Executions: (JsonSerializer.Deserialize<TugDailyLogContent>(l.ContentJson, FieldOpsJson.Options)?.ExecutionIds ?? []))).ToArray();

        var items = shown.Select(x => {
            var j = x.Job;
            var mine = executions.Where(e => e.JobId == j.Id && e.VesselId == j.VesselId).ToArray();
            var dtos = mine.Select(e => new TugHistoryExecutionDto(e.Id, e.TugId, e.State, e.VerificationState, e.StartedAt, e.EndedAt,
                TugOperationHistoryPolicy.DurationMinutes(e.StartedAt, e.EndedAt), e.StartedBy, e.EndedBy, e.VerifiedBy, e.Sequence,
                evidence.GetValueOrDefault(e.Id), e.LastEventHash)).ToArray();
            var daily = logLinks.Where(l => mine.Any(e => e.TugId == l.Log.VesselId && (l.Executions.Contains(e.Id) ||
                    l.Log.LogDate >= DateOnly.FromDateTime(e.StartedAt.UtcDateTime) && l.Log.LogDate <= DateOnly.FromDateTime((e.EndedAt ?? e.StartedAt).UtcDateTime))))
                .Select(l => new TugHistoryDailyLogDto(l.Log.Id, l.Log.VesselId, l.Log.LogDate, l.Log.Watch, l.Log.State, mine.Any(e => l.Executions.Contains(e.Id)))).ToArray();
            return new TugHistoryItemDto(j.Id, j.VesselId, j.JobNumber, j.Status, j.JobType, j.AssistedVessel, j.OperationArea, x.At, j.CancelledAt,
                TugOperationHistoryPolicy.Outcome(j.Status, mine.Select(e => e.State).ToArray()),
                TugOperationHistoryPolicy.Evidence(dtos.Sum(e => e.EvidenceEvents), mine.Select(e => e.VerificationState).ToArray()),
                plans.FirstOrDefault(p => p.JobId == j.Id && p.VesselId == j.VesselId)?.State, briefings.FirstOrDefault(b => b.JobId == j.Id && b.VesselId == j.VesselId)?.State,
                dtos, daily, "UNKNOWN:SafetyEventJobLinkUnavailable");
        }).ToArray();
        var next = page.Length > TugOperationHistoryPolicy.PageSize ? TugOperationHistoryPolicy.Cursor(shown[^1].At, shown[^1].Job.Id) : null;
        await TugReadinessHttp.Observe(h, Read, "Success", f.Job ?? f.Tug ?? f.Vessel);
        return Results.Json(new TugHistoryPageDto(items, next, TugOperationHistoryPolicy.Outcomes, TugReadinessHttp.Now(h)), FieldOpsJson.Options);
    }
}
