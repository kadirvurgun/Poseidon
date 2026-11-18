using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Operator configuration only. No HTTP command accepts these service identities.
public sealed record NotificationSchedule(Guid TenantId, Guid VesselId, Guid ServiceActorId, Guid RoleId)
{
    public NotificationSchedule() : this(Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty) { }
}
public sealed class NotificationSchedulerSettings
{
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 30;
    public int LeaseSeconds { get; set; } = 120;
    public int BatchTimeoutSeconds { get; set; } = 30;
    public int SourceBatchSize { get; set; } = 32;
    public int MaxScopesPerTick { get; set; } = 4;
    public NotificationSchedule[] Scopes { get; set; } = [];
    internal NotificationSchedule[]? Snapshot()
    {
        var rows = Scopes?.Take(257).ToArray();
        if (!Enabled || IntervalSeconds is < 1 or > 3600 || LeaseSeconds is < 10 or > 600 ||
            BatchTimeoutSeconds is < 1 or > 120 || LeaseSeconds <= BatchTimeoutSeconds + 5 ||
            SourceBatchSize is < 1 or > 64 || MaxScopesPerTick is < 1 or > 16 || rows is not { Length: > 0 and <= 256 } ||
            rows.Any(s => s == null || s.TenantId == Guid.Empty || s.VesselId == Guid.Empty || s.ServiceActorId == Guid.Empty || s.RoleId == Guid.Empty) ||
            rows.Select(s => (s.TenantId, s.VesselId)).Distinct().Count() != rows.Length) return null;
        return rows.Select(s => s with { }).OrderBy(s => s.TenantId).ThenBy(s => s.VesselId).ToArray();
    }
}
internal sealed record NotificationServiceIdentity(NotificationSchedule Scope, string Fingerprint, bool CanSeeShip, bool CanSeeTug);
internal sealed record NotificationScheduleResult(string Status, int Completed, int Busy, int Unavailable);

// This factory never sets administrativeImport, creates a ClaimsPrincipal, or
// borrows a human account. Normal tenant filters and FK/audit guards still apply.
public sealed class NotificationServiceContexts(IConfiguration configuration, IHostEnvironment environment,
    FederationSettings federation, NotificationSchedulerSettings scheduler)
{
    internal NotificationServiceIdentity? Resolve(NotificationSchedule scope)
    {
        var scopes = scheduler.Snapshot();
        if (scopes == null || !scopes.Contains(scope)) return null;
        var matches = federation.Memberships.Where(m => m.ActorId == scope.ServiceActorId && m.TenantId == scope.TenantId).Take(2).ToArray();
        if (matches.Length != 1) return null;
        var m = matches[0]; var permissions = m.Permissions ?? [];
        if (m.ActorKind != "Service" || string.IsNullOrWhiteSpace(federation.Issuer) || m.Issuer != federation.Issuer || string.IsNullOrWhiteSpace(m.Subject) ||
            federation.Memberships.Count(other => other.Issuer == m.Issuer && other.Subject == m.Subject) != 1 ||
            ActorResolver.TrustedRoleContext(m.RoleReferences)?.Split(',').Contains(scope.RoleId.ToString("D")) != true ||
            !new[] { "directory.read", "maintenance.read", "equipment.read", "notification.evaluate" }.All(permissions.Contains) ||
            !(m.TenantWideRead || m.TenantWideManage || (m.VesselGrants ?? []).Any(g => g.VesselId == scope.VesselId))) return null;
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new {
            scope, m.ActorKind, m.Issuer, m.Subject, m.TenantWideRead, m.TenantWideManage,
            permissions = permissions.Order(StringComparer.Ordinal), roles = ActorResolver.TrustedRoleContext(m.RoleReferences),
            vessels = (m.VesselGrants ?? []).OrderBy(g => g.VesselId).ThenBy(g => g.CanEdit),
            scheduler.IntervalSeconds, scheduler.LeaseSeconds, scheduler.BatchTimeoutSeconds, scheduler.SourceBatchSize
        }));
        return new(scope, fingerprint, !permissions.Contains("mode.tug") || permissions.Contains("mode.cross"), permissions.Contains("mode.tug"));
    }
    internal PoseidonDbContext? Open(NotificationServiceIdentity identity)
    {
        if (Resolve(identity.Scope)?.Fingerprint != identity.Fingerprint) return null;
        var connection = OperatorSecretReferences.Database(configuration, environment);
        if (string.IsNullOrWhiteSpace(connection)) return null;
        var actor = new ActorContext(identity.Scope.ServiceActorId.ToString("D"), "System", Guid.NewGuid().ToString("N"),
            "directory.read,equipment.read,maintenance.read,notification.evaluate", "NotificationScheduler", identity.Scope.RoleId.ToString("D"));
        return new(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connection).Options, identity.Scope.TenantId, actor);
    }
}

public sealed class NotificationScheduler(IServiceScopeFactory scopeFactory, NotificationSchedulerSettings settings,
    NotificationServiceContexts contexts, FederationSettings federation, NotificationSettings routing, TimeProvider time,
    ILogger<NotificationScheduler> logger) : BackgroundService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private int cursor;
    internal async Task<NotificationScheduleResult> RunBatchAsync(CancellationToken ct)
    {
        if (!settings.Enabled) return new("Disabled", 0, 0, 0);
        var configured = settings.Snapshot();
        if (configured == null) return new("Unavailable", 0, 0, 1);
        if (!await gate.WaitAsync(0, ct)) return new("Busy", 0, 1, 0);
        try {
            var completed = 0; var busy = 0; var unavailable = 0;
            var take = Math.Min(settings.MaxScopesPerTick, configured.Length);
            var start = cursor % configured.Length; cursor = (start + take) % configured.Length;
            for (var index = 0; index < take; index++) {
                ct.ThrowIfCancellationRequested();
                var target = configured[(start + index) % configured.Length];
                var identity = contexts.Resolve(target);
                if (identity == null || routing.For(target.TenantId, target.VesselId) is not { Length: > 0 }) { unavailable++; continue; }
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(settings.BatchTimeoutSeconds), time);
                using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
                try {
                    await using var services = scopeFactory.CreateAsyncScope();
                    await using var db = contexts.Open(identity);
                    if (db == null || !await db.Vessels.AsNoTracking().AnyAsync(v => v.Id == target.VesselId &&
                        (v.Kind == VesselKind.Ship ? identity.CanSeeShip : identity.CanSeeTug), bounded.Token)) { unavailable++; continue; }
                    var engine = services.ServiceProvider.GetRequiredService<NotificationEngine>();
                    var result = await engine.EvaluateAsync(db, target.VesselId, target.ServiceActorId,
                        () => TechnicalEndpoints.EligibleOwners(federation, target.TenantId, target.VesselId),
                        () => contexts.Resolve(target)?.Fingerprint == identity.Fingerprint, bounded.Token, honorSchedule: true,
                        batchSize: settings.SourceBatchSize, leaseSeconds: settings.LeaseSeconds, intervalSeconds: settings.IntervalSeconds);
                    if (result.Status == "Completed") completed++;
                    else if (result.Status is "LeaseBusy" or "LeaseLost" or "NotDue" or "Conflict") busy++;
                    else unavailable++;
                } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { unavailable++; }
                catch (Exception e) when (PostgresFailure.IsDatabase(e)) { unavailable++; }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException) { unavailable++; }
            }
            return new(unavailable > 0 ? "Unavailable" : completed > 0 ? "Evaluated" : "Idle", completed, busy, unavailable);
        } finally { gate.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Missing explicit product configuration performs no DB access, migration,
        // secret lookup, OS registration or scheduled action.
        if (!settings.Enabled) return;
        while (!stoppingToken.IsCancellationRequested && settings.Enabled) {
            try {
                var result = await RunBatchAsync(stoppingToken);
                if (result.Unavailable > 0) logger.LogWarning("Configured notification scheduling is unavailable for one or more scopes.");
                var seconds = settings.IntervalSeconds is >= 1 and <= 3600 ? settings.IntervalSeconds : 30;
                await Task.Delay(TimeSpan.FromSeconds(seconds), time, stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
