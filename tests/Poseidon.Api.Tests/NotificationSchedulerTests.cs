using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-036/T-S4-004: configured product worker + real owned PostgreSQL.
// Source authored, NOT RUN. Fictional service actors are not human appointments.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid NotificationServiceRole = Guid.Parse("c5ea9435-bc0c-4c73-beb7-a297b5dd4237");
    private async Task<NotificationSchedule> ScheduleNotifications(IServiceProvider services, string actorKind = "Service", int batch = 32)
    {
        // Ensure the explicitly disabled test host's hosted task has exited
        // before advancing deterministic ticks by calling RunBatchAsync directly.
        var hosted = services.GetRequiredService<NotificationScheduler>();
        if (hosted.ExecuteTask is { } stopped) await stopped.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var federation = services.GetRequiredService<FederationSettings>(); var actor = Guid.NewGuid();
        federation.Memberships = [..federation.Memberships, new(federation.Issuer!, "fictional-notification-worker", actor, SeedVessel.TenantId,
            ["directory.read", "maintenance.read", "equipment.read", "notification.evaluate"], VesselGrants: [new(SeedVessel.Id)]) {
                ActorKind = actorKind, RoleReferences = [NotificationServiceRole.ToString("D")]
            }];
        var settings = services.GetRequiredService<NotificationSchedulerSettings>();
        var target = new NotificationSchedule(SeedVessel.TenantId, SeedVessel.Id, actor, NotificationServiceRole);
        settings.Enabled = true; settings.IntervalSeconds = 5; settings.LeaseSeconds = 30; settings.BatchTimeoutSeconds = 5;
        settings.SourceBatchSize = batch; settings.Scopes = [target];
        return target;
    }
    private static NotificationScheduler AnotherNotificationWorker(IServiceProvider services) => new(
        services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<NotificationSchedulerSettings>(),
        services.GetRequiredService<NotificationServiceContexts>(), services.GetRequiredService<FederationSettings>(),
        services.GetRequiredService<NotificationSettings>(), services.GetRequiredService<TimeProvider>(), NullLogger<NotificationScheduler>.Instance);

    [Fact]
    public async Task Missing_explicit_scheduler_configuration_performs_no_scheduled_writes_or_service_identity_fallback()
    {
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        var worker = host.Services.GetRequiredService<NotificationScheduler>();
        Assert.Equal("Disabled", (await worker.RunBatchAsync(Ct)).Status);
        if (worker.ExecuteTask is { } stopped) await stopped.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        host.Services.GetRequiredService<NotificationSchedulerSettings>().Enabled = true;
        Assert.Equal("Unavailable", (await worker.RunBatchAsync(Ct)).Status);
        await using var db = Db(); Assert.Empty(await db.NotificationDispatchStates.ToArrayAsync(Ct));
        Assert.Empty(await db.Notifications.ToArrayAsync(Ct)); Assert.Empty(await db.NotificationEpisodes.ToArrayAsync(Ct));
    }
    [Theory]
    [InlineData("Human")]
    [InlineData("AI")]
    [InlineData("Unspecified")]
    public async Task Human_AI_and_unspecified_memberships_cannot_be_borrowed_as_the_scheduled_service_actor(string kind)
    {
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        var target = await ScheduleNotifications(host.Services, kind);
        Assert.Null(host.Services.GetRequiredService<NotificationServiceContexts>().Resolve(target));
        var result = await host.Services.GetRequiredService<NotificationScheduler>().RunBatchAsync(Ct);
        Assert.Equal(0, result.Completed); Assert.Equal(1, result.Unavailable);
        await using var db = Db(); Assert.Empty(await db.NotificationDispatchStates.ToArrayAsync(Ct));
        Assert.Empty(await db.Notifications.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Configured_worker_uses_real_source_time_schedules_once_and_attributes_all_effects_to_the_service_role()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode);
        clock.Current = DateTimeOffset.UtcNow; var target = await ScheduleNotifications(host.Services);
        var worker = host.Services.GetRequiredService<NotificationScheduler>();
        Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        Assert.Equal(0, (await worker.RunBatchAsync(Ct)).Completed);
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using (var db = Db()) {
            Assert.Equal(2, await db.Notifications.CountAsync(Ct)); Assert.Equal(2, await db.NotificationEvents.CountAsync(e => e.Kind == "Raised", Ct));
            var audits = await db.AuditEvents.Where(a => a.Source == "NotificationScheduler").ToArrayAsync(Ct);
            Assert.NotEmpty(audits); Assert.All(audits, a => { Assert.Equal("System", a.ActorType); Assert.Equal(target.ServiceActorId.ToString("D"), a.ActorId);
                Assert.Equal(target.RoleId.ToString("D"), a.RoleContext); Assert.Equal(target.TenantId, a.TenantId); Assert.Equal(target.VesselId, a.VesselId); });
        }
        clock.Current = clock.Current.AddSeconds(55); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using var verify = Db(); Assert.Single(await verify.NotificationEvents.Where(e => e.Kind == "Escalated").ToArrayAsync(Ct));
        var state = await verify.NotificationDispatchStates.SingleAsync(Ct); Assert.Null(state.LeaseOwner); Assert.Null(state.LeaseUntil);
        Assert.NotNull(state.LastCompletedAt); Assert.Equal(0, state.SourceOffset);
    }
    [Fact]
    public async Task Observed_inactive_then_unknown_then_same_active_basis_creates_one_new_episode_without_reopening_the_prior_acknowledged_record()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); var plan = CurrentCalendar(); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode);
        clock.Current = DateTimeOffset.UtcNow; await ScheduleNotifications(host.Services); var worker = host.Services.GetRequiredService<NotificationScheduler>();
        Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        var first = await Inbox(c); var critical = Assert.Single(first.Items, n => n.Severity == "Critical");
        var key = Guid.NewGuid().ToString(); var ack = new NotificationAckCommand(critical.Version);
        using var acknowledged = await Send(c, NotificationPath($"/{critical.Id}/acknowledge"), ack, key); Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        var receipt = await acknowledged.Content.ReadAsStringAsync(Ct);
        var routing = host.Services.GetRequiredService<NotificationSettings>(); var original = routing.Rules;
        // An observed withdrawal from an otherwise valid, nonempty routing set
        // is a known policy boundary. Missing source/owner state is not.
        routing.Rules = original.Where(r => r.Key != "due-critical").ToArray();
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using (var db = Db()) {
            Assert.Equal("SourceChanged", (await db.Notifications.SingleAsync(n => n.Id == critical.Id, Ct)).State);
            Assert.False((await db.NotificationEpisodes.SingleAsync(e => e.PlanId == plan.Id && e.RuleKey == "due-critical", Ct)).Active);
            Assert.True((await db.NotificationEpisodes.SingleAsync(e => e.PlanId == plan.Id && e.RuleKey == "due-advisory", Ct)).Active);
        }
        routing.Rules = original;
        var federation = host.Services.GetRequiredService<FederationSettings>(); var owner = federation.Memberships[0];
        federation.Memberships[0] = owner with { Permissions = owner.Permissions.Where(p => p != "maintenance.owner.eligible").ToArray() };
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        long unknownRevision;
        await using (var db = Db()) {
            var inactive = await db.NotificationEpisodes.SingleAsync(e => e.PlanId == plan.Id && e.RuleKey == "due-critical", Ct);
            Assert.Null(inactive.Active); Assert.False(inactive.LastKnownActive); Assert.Equal(1, inactive.Episode);
            unknownRevision = inactive.Revision;
            var advisory = await db.NotificationEpisodes.SingleAsync(e => e.PlanId == plan.Id && e.RuleKey == "due-advisory", Ct);
            Assert.Null(advisory.Active); Assert.True(advisory.LastKnownActive); Assert.Equal(1, advisory.Episode);
            Assert.Equal(2, await db.NotificationEvents.CountAsync(e => e.Kind == "Raised", Ct));
        }
        // A repeated unknown poll is neither a new boundary nor a reason to
        // erase the already observed inactive state of the critical route.
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using (var db = Db()) {
            var unchanged = await db.NotificationEpisodes.SingleAsync(e => e.PlanId == plan.Id && e.RuleKey == "due-critical", Ct);
            Assert.Null(unchanged.Active); Assert.False(unchanged.LastKnownActive); Assert.Equal(unknownRevision, unchanged.Revision);
            Assert.Equal(2, await db.Notifications.CountAsync(Ct));
        }
        federation.Memberships[0] = owner;
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using (var db = Db()) {
            var all = await db.Notifications.Where(n => n.PlanId == plan.Id).ToArrayAsync(Ct); Assert.Equal(3, all.Length);
            var old = all.Single(n => n.Id == critical.Id); var next = all.Single(n => n.Severity == "Critical" && n.State == "Open");
            Assert.Equal("SourceChanged", old.State); Assert.Equal(1, old.Episode); Assert.Equal(2, next.Episode);
            Assert.Equal(old.SourceContextHash, next.SourceContextHash); Assert.Equal(old.PlanRevision, next.PlanRevision);
            Assert.NotEqual(old.Id, next.Id); Assert.True(next.Mandatory);
            Assert.Single(await db.NotificationEvents.Where(e => e.NotificationId == old.Id && e.Kind == "Acknowledged").ToArrayAsync(Ct));
            Assert.Equal(3, await db.NotificationEvents.CountAsync(e => e.Kind == "Raised", Ct));
            Assert.Equal(1, (await db.MaintenancePlans.SingleAsync(p => p.Id == plan.Id, Ct)).Revision);
        }
        using var retry = await Send(c, NotificationPath($"/{critical.Id}/acknowledge"), ack, key);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode); Assert.Equal(receipt, await retry.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Observed_policy_withdrawal_while_source_already_unknown_preserves_a_real_inactive_boundary()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); var plan = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode);
        clock.Current = DateTimeOffset.UtcNow; await ScheduleNotifications(host.Services);
        var worker = host.Services.GetRequiredService<NotificationScheduler>(); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        var federation = host.Services.GetRequiredService<FederationSettings>(); var owner = federation.Memberships[0];
        federation.Memberships[0] = owner with { Permissions = owner.Permissions.Where(p => p != "maintenance.owner.eligible").ToArray() };
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        var routing = host.Services.GetRequiredService<NotificationSettings>(); var original = routing.Rules;
        routing.Rules = original.Where(r => r.Key != "due-critical").ToArray();
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using (var db = Db()) {
            var retired = await db.NotificationEpisodes.SingleAsync(e => e.PlanId == plan.Id && e.RuleKey == "due-critical", Ct);
            Assert.False(retired.Active); Assert.False(retired.LastKnownActive); Assert.Equal(1, retired.Episode);
            Assert.Equal("SourceChanged", (await db.Notifications.SingleAsync(n => n.PlanId == plan.Id && n.RuleKey == "due-critical", Ct)).State);
        }
        routing.Rules = original;
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using (var db = Db()) {
            var unknown = await db.NotificationEpisodes.SingleAsync(e => e.PlanId == plan.Id && e.RuleKey == "due-critical", Ct);
            Assert.Null(unknown.Active); Assert.False(unknown.LastKnownActive); Assert.Equal(2, await db.Notifications.CountAsync(Ct));
        }
        federation.Memberships[0] = owner;
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using var verify = Db(); Assert.Equal(3, await verify.Notifications.CountAsync(Ct));
        Assert.Equal(2, (await verify.NotificationEpisodes.SingleAsync(e => e.PlanId == plan.Id && e.RuleKey == "due-critical", Ct)).Episode);
    }
    [Fact]
    public async Task Retained_retired_header_bootstraps_a_closed_episode_even_when_the_source_hash_is_identical()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); var plan = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode);
        clock.Current = DateTimeOffset.UtcNow; await ScheduleNotifications(host.Services);
        var worker = host.Services.GetRequiredService<NotificationScheduler>(); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        var routing = host.Services.GetRequiredService<NotificationSettings>();
        var rule = routing.Rules.Single(r => r.Key == "due-advisory") with { Key = "fictional-retired-bootstrap" };
        var retiredId = Guid.NewGuid(); string basis;
        await using (var db = Db()) {
            var current = await db.Notifications.SingleAsync(n => n.PlanId == plan.Id && n.RuleKey == "due-advisory", Ct);
            basis = current.SourceContextHash;
            // Explicit historical unverified fixture against the real current
            // owning revision, not a delivered message or verified safety fact.
            db.Notifications.Add(new() { TenantId = current.TenantId, VesselId = current.VesselId, Id = retiredId,
                PlanId = current.PlanId, PlanRevision = current.PlanRevision, RuleKey = rule.Key, PolicyVersion = rule.Version,
                PolicyDigest = NotificationEngine.Digest(rule), SourceContextHash = basis, Episode = 1, SourceState = current.SourceState,
                Severity = rule.Severity, RecipientRoleId = current.RecipientRoleId, DueDate = current.DueDate,
                State = "SourceChanged", CreatedAt = clock.Current });
            db.NotificationEvents.Add(new() { TenantId = current.TenantId, VesselId = current.VesselId, Id = Guid.NewGuid(),
                NotificationId = retiredId, Sequence = 1, Kind = "SourceChanged", RecordedAt = clock.Current });
            await db.SaveChangesAsync(Ct);
        }
        routing.Rules = [..routing.Rules, rule];
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using var verify = Db(); var rows = await verify.Notifications.Where(n => n.RuleKey == rule.Key).ToArrayAsync(Ct);
        Assert.Equal(2, rows.Length); Assert.Equal("SourceChanged", rows.Single(n => n.Id == retiredId).State);
        var fresh = Assert.Single(rows, n => n.State == "Open"); Assert.Equal(2, fresh.Episode); Assert.Equal(basis, fresh.SourceContextHash);
        Assert.Single(await verify.NotificationEvents.Where(e => e.NotificationId == fresh.Id && e.Kind == "Raised").ToArrayAsync(Ct));
        Assert.Single(await verify.NotificationEvents.Where(e => e.NotificationId == retiredId).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Unresolved_owner_is_unavailable_not_a_false_boundary_and_restoration_keeps_the_same_episode()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); var plan = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode);
        clock.Current = DateTimeOffset.UtcNow; await ScheduleNotifications(host.Services);
        var worker = host.Services.GetRequiredService<NotificationScheduler>(); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        var before = await Inbox(c); var notice = before.Items[0];
        var federation = host.Services.GetRequiredService<FederationSettings>(); var original = federation.Memberships[0];
        federation.Memberships[0] = original with { Permissions = original.Permissions.Where(p => p != "maintenance.owner.eligible").ToArray() };
        // Even before the worker reaches this plan, the read projection cannot
        // advertise an old Open header as a confirmed current exception.
        Assert.All((await Inbox(c)).Items, n => Assert.Equal("SourceUnavailable", n.State));
        using var blocked = await Send(c, NotificationPath($"/{notice.Id}/acknowledge"), new NotificationAckCommand(notice.Version));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using (var db = Db()) {
            Assert.All(await db.NotificationEpisodes.Where(e => e.PlanId == plan.Id).ToArrayAsync(Ct), e => { Assert.Null(e.Active); Assert.True(e.LastKnownActive); Assert.Equal(1, e.Episode); });
            Assert.All(await db.Notifications.ToArrayAsync(Ct), n => Assert.Equal("Open", n.State));
            Assert.Empty(await db.NotificationEvents.Where(e => e.Kind != "Raised").ToArrayAsync(Ct));
            Assert.Empty(await db.NotificationReceipts.ToArrayAsync(Ct));
        }
        federation.Memberships[0] = original; clock.Current = clock.Current.AddSeconds(5);
        Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        var restored = await Inbox(c); Assert.Equal(before.Items.Select(n => n.Id).Order(), restored.Items.Select(n => n.Id).Order());
        Assert.All(restored.Items, n => Assert.Equal("Open", n.State));
        await using var verify = Db(); Assert.Equal(2, await verify.Notifications.CountAsync(Ct));
        Assert.All(await verify.NotificationEpisodes.Where(e => e.PlanId == plan.Id).ToArrayAsync(Ct), e => { Assert.True(e.Active); Assert.Equal(1, e.Episode); });
    }
    [Fact]
    public async Task Missing_current_revision_outside_the_source_page_does_not_leave_prior_notices_current_or_rearm_them()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); var plan = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode);
        clock.Current = DateTimeOffset.UtcNow; await ScheduleNotifications(host.Services, batch: 2);
        var worker = host.Services.GetRequiredService<NotificationScheduler>(); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        // Explicit owned fixture fault: a head without its matching snapshot.
        // Ordinary audited EF write, no SQL/FK/tenant guard bypass or verified data.
        await using (var db = Db()) { var head = await db.MaintenancePlans.SingleAsync(p => p.Id == plan.Id, Ct); head.Revision++; await db.SaveChangesAsync(Ct); }
        Assert.All((await Inbox(c)).Items, n => Assert.Equal("SourceUnavailable", n.State));
        clock.Current = clock.Current.AddSeconds(5); Assert.Equal(1, (await worker.RunBatchAsync(Ct)).Completed);
        await using var verify = Db();
        Assert.All(await verify.NotificationEpisodes.Where(e => e.PlanId == plan.Id).ToArrayAsync(Ct), e => { Assert.Null(e.Active); Assert.Equal(1, e.Episode); });
        Assert.All(await verify.Notifications.ToArrayAsync(Ct), n => Assert.Equal("Open", n.State));
        Assert.Equal(2, await verify.NotificationEvents.CountAsync(e => e.Kind == "Raised", Ct));
        Assert.Empty(await verify.NotificationEvents.Where(e => e.Kind == "SourceChanged").ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Independent_worker_instances_share_a_durable_lease_and_bounded_cursor_without_duplicate_alerts()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c);
        var plans = new List<Guid>();
        for (var i = 0; i < 4; i++) { var plan = CurrentCalendar(); plans.Add(plan.Id); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode); }
        clock.Current = DateTimeOffset.UtcNow; await ScheduleNotifications(host.Services, batch: 2);
        var worker1 = host.Services.GetRequiredService<NotificationScheduler>(); var worker2 = AnotherNotificationWorker(host.Services);
        var simultaneous = await Task.WhenAll(worker1.RunBatchAsync(Ct), worker2.RunBatchAsync(Ct));
        Assert.True(simultaneous.Sum(r => r.Completed) >= 1);
        // Each pass can cover at most two source rows. Immediate continuation is
        // persisted when more remain; a completed sweep then observes its interval.
        for (var i = 0; i < 5; i++) { clock.Current = clock.Current.AddSeconds(5); await worker1.RunBatchAsync(Ct); }
        await using var db = Db(); var notices = await db.Notifications.Where(n => plans.Contains(n.PlanId)).ToArrayAsync(Ct);
        Assert.Equal(8, notices.Length); Assert.All(notices.GroupBy(n => (n.PlanId, n.RuleKey, n.Episode)), group => Assert.Single(group));
        Assert.Equal(8, await db.NotificationEvents.CountAsync(e => e.Kind == "Raised", Ct));
        Assert.All(await db.NotificationDispatchStates.ToArrayAsync(Ct), s => { Assert.Null(s.LeaseOwner); Assert.True(s.SourceOffset >= 0); });
    }
    private sealed class ScheduledSourceBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Wait(CancellationToken ct) => reached.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        public void Release() => released.TrySetResult();
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("FROM technical.\"Plans\"", StringComparison.Ordinal)) {
                reached.TrySetResult(); await released.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
    [Fact]
    public async Task Cancelled_source_batch_rolls_back_alerts_and_episodes_and_an_expired_lease_is_recoverable()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode);
        clock.Current = DateTimeOffset.UtcNow; var target = await ScheduleNotifications(host.Services);
        var identity = host.Services.GetRequiredService<NotificationServiceContexts>().Resolve(target)!;
        var barrier = new ScheduledSourceBarrier(); using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        // Test-only interception of the normal tenant-scoped service DB context;
        // no admin import, fake principal or human authority is constructed.
        await using var db = new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(postgres.GetConnectionString()).AddInterceptors(barrier).Options,
            target.TenantId, new(target.ServiceActorId.ToString("D"), "System", Guid.NewGuid().ToString("N"),
                "directory.read,equipment.read,maintenance.read,notification.evaluate", "NotificationScheduler", target.RoleId.ToString("D")));
        await using var services = host.Services.CreateAsyncScope(); var engine = services.ServiceProvider.GetRequiredService<NotificationEngine>();
        var pending = engine.EvaluateAsync(db, target.VesselId, target.ServiceActorId,
            () => TechnicalEndpoints.EligibleOwners(host.Services.GetRequiredService<FederationSettings>(), target.TenantId, target.VesselId),
            () => host.Services.GetRequiredService<NotificationServiceContexts>().Resolve(target)?.Fingerprint == identity.Fingerprint,
            cancelled.Token, leaseSeconds: 30, intervalSeconds: 5);
        try { await barrier.Wait(Ct); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); }
        finally { barrier.Release(); }
        await using (var check = Db()) {
            Assert.Empty(await check.Notifications.ToArrayAsync(Ct)); Assert.Empty(await check.NotificationEpisodes.ToArrayAsync(Ct));
            var lease = await check.NotificationDispatchStates.SingleAsync(Ct); Assert.NotNull(lease.LeaseOwner); Assert.Null(lease.LastCompletedAt);
        }
        clock.Current = clock.Current.AddSeconds(31);
        Assert.Equal(1, (await host.Services.GetRequiredService<NotificationScheduler>().RunBatchAsync(Ct)).Completed);
        await using var recovered = Db(); Assert.Equal(2, await recovered.Notifications.CountAsync(Ct));
        Assert.Null((await recovered.NotificationDispatchStates.SingleAsync(Ct)).LeaseOwner);
    }
    [Fact]
    public async Task Service_role_revoked_after_private_source_read_rolls_back_all_notification_effects()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode);
        clock.Current = DateTimeOffset.UtcNow; var target = await ScheduleNotifications(host.Services);
        var contexts = host.Services.GetRequiredService<NotificationServiceContexts>(); var identity = contexts.Resolve(target)!;
        var federation = host.Services.GetRequiredService<FederationSettings>(); var barrier = new ScheduledSourceBarrier();
        await using var db = new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(postgres.GetConnectionString()).AddInterceptors(barrier).Options,
            target.TenantId, new(target.ServiceActorId.ToString("D"), "System", Guid.NewGuid().ToString("N"),
                "directory.read,equipment.read,maintenance.read,notification.evaluate", "NotificationScheduler", target.RoleId.ToString("D")));
        await using var services = host.Services.CreateAsyncScope();
        var pending = services.ServiceProvider.GetRequiredService<NotificationEngine>().EvaluateAsync(db, target.VesselId, target.ServiceActorId,
            () => TechnicalEndpoints.EligibleOwners(federation, target.TenantId, target.VesselId),
            () => contexts.Resolve(target)?.Fingerprint == identity.Fingerprint, Ct, leaseSeconds: 30);
        try {
            await barrier.Wait(Ct);
            federation.Memberships.Single(m => m.ActorId == target.ServiceActorId).RoleReferences = [];
        } finally { barrier.Release(); }
        Assert.Equal("Denied", (await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct)).Status);
        await using var verify = Db(); Assert.Empty(await verify.Notifications.ToArrayAsync(Ct));
        Assert.Empty(await verify.NotificationEpisodes.ToArrayAsync(Ct)); Assert.Empty(await verify.NotificationEvents.ToArrayAsync(Ct));
        Assert.Null((await verify.NotificationDispatchStates.SingleAsync(Ct)).LastCompletedAt);
        Assert.Null(contexts.Resolve(target));
    }
    [Fact]
    public async Task Wrong_tenant_or_removed_service_role_never_creates_a_lease_or_reads_a_foreign_vessel()
    {
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        var target = await ScheduleNotifications(host.Services); var contexts = host.Services.GetRequiredService<NotificationServiceContexts>();
        var federation = host.Services.GetRequiredService<FederationSettings>();
        var member = federation.Memberships.Single(m => m.ActorId == target.ServiceActorId);
        member.RoleReferences = [Guid.NewGuid().ToString("D")]; Assert.Null(contexts.Resolve(target));
        Assert.Equal(0, (await host.Services.GetRequiredService<NotificationScheduler>().RunBatchAsync(Ct)).Completed);
        member.RoleReferences = [target.RoleId.ToString("D")];
        var foreign = seed.Vessels.First(v => v.TenantId != target.TenantId);
        var wrong = target with { VesselId = foreign.Id };
        federation.Memberships = federation.Memberships.Select(m => m.ActorId == target.ServiceActorId ? m with { VesselGrants = [new(foreign.Id)] } : m).ToArray();
        host.Services.GetRequiredService<NotificationSchedulerSettings>().Scopes = [wrong];
        var rules = host.Services.GetRequiredService<NotificationSettings>(); rules.Rules = [rules.Rules[0] with { VesselId = foreign.Id }];
        Assert.Equal(0, (await host.Services.GetRequiredService<NotificationScheduler>().RunBatchAsync(Ct)).Completed);
        await using var db = Db(); Assert.Empty(await db.NotificationDispatchStates.ToArrayAsync(Ct)); Assert.Empty(await db.Notifications.ToArrayAsync(Ct));
        await using var other = Db(foreign.TenantId); Assert.Empty(await other.NotificationDispatchStates.ToArrayAsync(Ct));
    }
}
