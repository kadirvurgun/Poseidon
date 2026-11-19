using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record HealthApiDto(string State, string Scope, long? Requests, long? Errors, double? AverageLatencyMs);
public sealed record HealthAuditDto(string State, string Scope, long? Persisted, long? Unavailable, double? AverageLatencyMs);
public sealed record HealthFreshnessDto(string State, string Scope, long? Current, long? Stale, long? Future, long? Unknown, long? Unavailable);
public sealed record HealthSyncDto(string State, string Scope, long Committed, long UnresolvedConflicts, long? LocalBacklog,
    DateTimeOffset? LastCommittedAt = null);
// Process liveness only: this API process answered and reports its own OS start time.
public sealed record HealthServiceDto(string State, DateTimeOffset? StartedAt, long? UptimeSeconds);
// Durable selected-vessel notification job/queue state. Dispatch: NotObserved | Running |
// LeaseExpired | SchedulerDisabled | Overdue | Current. Never a delivery or safety claim.
public sealed record HealthNotificationsDto(string State, string Scope, long OpenUnacknowledged, long OverdueEscalations,
    string Dispatch, DateTimeOffset? LastCompletedAt, DateTimeOffset? NextRunAt);
public sealed record HealthFilesDto(string State, string Adapter, string ScanState);
public sealed record SystemHealthDto(Guid VesselId, DateTimeOffset ReadAt, HealthApiDto Api, HealthAuditDto Audit,
    HealthSyncDto Sync, HealthFilesDto Files, HealthFreshnessDto Freshness, HealthServiceDto Service, HealthNotificationsDto Notifications);

public static class SystemHealthEndpoints
{
    internal static bool Permitted(TrustedActor? actor) => actor != null && actor.CanReadAudit && (actor.TenantWideRead || actor.TenantWideManage) &&
        actor.Permissions.Contains("system.health.read");

    public static void MapSystemHealth(this IEndpointRouteBuilder app) => app.MapGet("/api/v1/system-health", Read)
        .RequireAuthorization("DirectoryRead").WithName("SystemHealthRead").Produces<SystemHealthDto>()
        .Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);

    private static async Task<IResult> Read(HttpContext h, PoseidonDbContext db, ActorResolver actors, SecurityAudit audit,
        SecureFileSettings files, IWebHostEnvironment environment, TimeProvider time, NotificationSchedulerSettings scheduler)
    {
        async Task<IResult> Fail(int status, string result) {
            await audit.WriteAsync(h, "SystemHealthRead", result); return Results.StatusCode(status);
        }
        var actor = actors.Resolve(h.User)!;
        if (!Permitted(actor)) return await Fail(403, "Denied");
        if (h.Request.Query.Count != 1 || h.Request.Query["vesselId"].Count != 1 ||
            !Guid.TryParseExact(h.Request.Query["vesselId"], "D", out var vessel) || vessel == Guid.Empty) return await Fail(400, "Invalid");
        if (!await actor.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, h.RequestAborted)) return await Fail(404, "NotFound");
        var committed = await db.OfflineOutcomes.LongCountAsync(o => o.VesselId == vessel && o.Result == "Committed", h.RequestAborted) +
            await db.TugOfflineOutcomes.LongCountAsync(o => o.VesselId == vessel && o.Result == "Committed", h.RequestAborted) +
            await db.DailyOfflineOutcomes.LongCountAsync(o => o.VesselId == vessel && o.Result == "Committed", h.RequestAborted);
        var conflicts = await db.OfflineOutcomes.LongCountAsync(o => o.VesselId == vessel && o.Result == "MaterialConflict" &&
            !db.OfflineOutcomes.Any(resolved => resolved.ActorId == o.ActorId && resolved.EnrollmentId == o.EnrollmentId && resolved.ParentCommandId == o.CommandId && resolved.Result == "Committed"), h.RequestAborted) +
            await db.TugOfflineOutcomes.LongCountAsync(o => o.VesselId == vessel && o.Result == "MaterialConflict" &&
            !db.TugOfflineOutcomes.Any(resolved => resolved.ActorId == o.ActorId && resolved.EnrollmentId == o.EnrollmentId && resolved.ParentCommandId == o.CommandId && resolved.Result == "Committed"), h.RequestAborted) +
            await db.DailyOfflineOutcomes.LongCountAsync(o => o.VesselId == vessel && o.Result == "MaterialConflict" &&
            !db.DailyOfflineOutcomes.Any(resolved => resolved.ActorId == o.ActorId && resolved.EnrollmentId == o.EnrollmentId && resolved.ParentCommandId == o.CommandId &&
                (resolved.Result == "Committed" || resolved.Result == "ServerRetained")), h.RequestAborted);
        var lastCommitted = new[] {
            await db.OfflineOutcomes.Where(o => o.VesselId == vessel && o.Result == "Committed").MaxAsync(o => (DateTimeOffset?)o.ReceivedAt, h.RequestAborted),
            await db.TugOfflineOutcomes.Where(o => o.VesselId == vessel && o.Result == "Committed").MaxAsync(o => (DateTimeOffset?)o.ReceivedAt, h.RequestAborted),
            await db.DailyOfflineOutcomes.Where(o => o.VesselId == vessel && o.Result == "Committed").MaxAsync(o => (DateTimeOffset?)o.ReceivedAt, h.RequestAborted)
        }.Max();
        var now = time.GetUtcNow();
        var open = await db.Notifications.LongCountAsync(n => n.VesselId == vessel && (n.State == "Open" || n.State == "Escalated"), h.RequestAborted);
        var overdue = await db.Notifications.LongCountAsync(n => n.VesselId == vessel && n.State == "Open" && n.EscalateAt != null && n.EscalateAt <= now, h.RequestAborted);
        var dispatch = await db.NotificationDispatchStates.AsNoTracking().SingleOrDefaultAsync(d => d.VesselId == vessel, h.RequestAborted);
        var notifications = new HealthNotificationsDto("Observed", "SelectedVesselDurable", open, overdue,
            DispatchState(dispatch, scheduler, now), dispatch?.LastCompletedAt, dispatch?.NextRunAt);
        var observed = OperationalHealthObservations.Read(actor.TenantId);
        var configured = files.Ready(environment);
        var fileState = new HealthFilesDto(configured ? "ConfiguredUnprobed" : "Unavailable",
            configured && files.Adapter is "LocalDevelopment" or "S3" ? files.Adapter : "Unavailable",
            configured ? "ConfiguredUnprobed" : "Unavailable");
        bool Current() => actors.Resolve(h.User) is { } live && live.Id == actor.Id && live.TenantId == actor.TenantId &&
            Permitted(live) && live.AuditCapabilities == actor.AuditCapabilities && live.RoleContext == actor.RoleContext;
        if (!Current()) return await Fail(403, "Denied");
        await audit.WriteAsync(h, "SystemHealthRead", "Success", resourceId: vessel);
        if (!Current()) return await Fail(403, "Denied");
        return Results.Ok(new SystemHealthDto(vessel, time.GetUtcNow(), observed.Api, observed.Audit,
            new("Observed", "SelectedVesselServerOutcomes", committed, conflicts, null, lastCommitted), fileState, observed.Freshness,
            Liveness(time.GetUtcNow()), notifications));
    }
    internal static string DispatchState(NotificationDispatchState? dispatch, NotificationSchedulerSettings scheduler, DateTimeOffset now)
    {
        if (dispatch == null) return "NotObserved";
        if (dispatch.LeaseOwner != null) return dispatch.LeaseUntil > now ? "Running" : "LeaseExpired"; // abandoned/failed run
        if (!scheduler.Enabled || scheduler.Snapshot() == null) return "SchedulerDisabled";
        var grace = TimeSpan.FromSeconds(Math.Max(60, 2L * scheduler.IntervalSeconds + scheduler.LeaseSeconds));
        return dispatch.NextRunAt == null || dispatch.NextRunAt.Value + grace < now ? "Overdue" : "Current";
    }
    private static HealthServiceDto Liveness(DateTimeOffset now)
    {
        try {
            var started = new DateTimeOffset(System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero);
            return started <= now ? new("Observed", started, (long)(now - started).TotalSeconds) : new("Unavailable", null, null);
        } catch (Exception e) when (e is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception) {
            return new("Unavailable", null, null);
        }
    }
}
