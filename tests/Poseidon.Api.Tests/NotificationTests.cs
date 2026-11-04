using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-036/T-S4-004 + DEV-038/T-S4-006. Actual owned PostgreSQL/HTTP source;
// no outbound delivery, production routing policy or real human sign-off. NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid NotificationEscalationRole = Guid.Parse("71345931-1216-4b61-a95c-9c29a346cbaf");
    private sealed class NotificationClock : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Current;
    }
    private NotificationSettings NotificationTestSettings() => new() { Rules = [
        new(seed.Tenants[0].Id, SeedVessel.Id, "due-critical", "fictional-v1", "FICTIONAL maintenance routing", "Due", "Critical", false, 60, NotificationEscalationRole),
        new(seed.Tenants[0].Id, SeedVessel.Id, "due-advisory", "fictional-v1", "FICTIONAL advisory routing", "Due", "Warning", false, null, null)
    ] };
    private WebApplicationFactory<Program> NotificationHost(NotificationClock? clock = null, DbCommandInterceptor? interceptor = null) =>
        Host(technicalRead: true, technicalWrite: true, interceptor: interceptor).WithWebHostBuilder(b => b.ConfigureTestServices(s => {
            s.RemoveAll<NotificationSettings>(); s.AddSingleton(NotificationTestSettings());
            s.RemoveAll<NotificationSchedulerSettings>(); s.AddSingleton(new NotificationSchedulerSettings());
            s.RemoveAll<MaintenanceObservationSettings>(); s.AddSingleton(new MaintenanceObservationSettings { Sources = [
                new(seed.Tenants[0].Id, SeedVessel.Id, SeedVessel.Id, "synthetic-meter", "synthetic-v1", "Meter", "hours", 3600, 4)
            ] });
            if (clock != null) { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); }
        }));
    private void NotificationPermissions(IServiceProvider services)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Issuer ??= member.Issuer;
        settings.Memberships = [member with { Permissions = [..member.Permissions, "maintenance.observation.write", "notification.read", "notification.manage", "notification.acknowledge", "notification.preference.write"] },
            new(member.Issuer, "fictional-notification-escalation-owner", Guid.NewGuid(), member.TenantId,
                ["directory.read", "maintenance.owner.eligible"], TenantWideRead: true) { RoleReferences = [NotificationEscalationRole.ToString("D")] }];
    }
    private string NotificationPath(string suffix = "") => $"notification-escalation{suffix}?vesselId={SeedVessel.Id:D}";
    private async Task<NotificationInboxDto> Inbox(HttpClient c) => (await c.GetFromJsonAsync<NotificationInboxDto>("/api/v1/" + NotificationPath(), Ct))!;
    private async Task RefreshNotifications(HttpClient c) {
        using var refreshed = await Send(c, NotificationPath("/refresh"), new { });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }
    [Fact]
    public async Task Persistent_notifications_dedupe_real_due_sources_escalate_once_and_critical_route_survives_preference_and_exact_ack_retry()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c);
        var plan = CurrentCalendar(); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode);
        clock.Current = DateTimeOffset.UtcNow;
        await RefreshNotifications(c); await RefreshNotifications(c);
        var first = await Inbox(c); Assert.Equal(2, first.Items.Length); Assert.Equal("InAppOnly", first.Delivery);
        var critical = Assert.Single(first.Items, n => n.Severity == "Critical"); Assert.True(critical.Mandatory);
        Assert.Equal(plan.Id, critical.SourceId); Assert.Equal(1, critical.SourceRevision); Assert.Equal("ConfiguredUnverified", critical.Provenance);
        using var preference = await Send(c, NotificationPath("/preference"), new NotificationPreferenceCommand(0, true), update: true);
        Assert.Equal(HttpStatusCode.OK, preference.StatusCode);
        var setting = (await preference.Content.ReadFromJsonAsync<NotificationPreferenceDto>(Ct))!;
        Assert.True(setting.MutedAdvisory); Assert.False(setting.MandatoryRouteMutable);
        Assert.Equal(critical.Id, Assert.Single((await Inbox(c)).Items).Id);
        clock.Current = clock.Current.AddSeconds(60);
        await RefreshNotifications(c); await RefreshNotifications(c);
        critical = Assert.Single((await Inbox(c)).Items); Assert.Equal("Escalated", critical.State);
        var key = Guid.NewGuid().ToString(); var ack = new NotificationAckCommand(critical.Version);
        using var acknowledged = await Send(c, NotificationPath($"/{critical.Id}/acknowledge"), ack, key);
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode); var original = await acknowledged.Content.ReadAsStringAsync(Ct);
        using var replay = await Send(c, NotificationPath($"/{critical.Id}/acknowledge"), ack, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal(original, await replay.Content.ReadAsStringAsync(Ct));
        using var stale = await Send(c, NotificationPath($"/{critical.Id}/acknowledge"), ack);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var db = Db();
        Assert.Equal(2, await db.Notifications.CountAsync(Ct));
        Assert.Equal(2, await db.NotificationEvents.CountAsync(e => e.Kind == "Raised", Ct));
        Assert.Single(await db.NotificationEvents.Where(e => e.Kind == "Escalated").ToArrayAsync(Ct));
        Assert.Single(await db.NotificationEvents.Where(e => e.Kind == "Acknowledged").ToArrayAsync(Ct));
        Assert.Equal(2, await db.NotificationReceipts.CountAsync(Ct));
        Assert.Contains(await db.AuditEvents.Where(a => a.EntityType == nameof(NotificationEvent)).ToArrayAsync(Ct), a =>
            a.ActorId == actorId.ToString() && a.VesselId == SeedVessel.Id && a.ChangeSummary!.Contains("NotificationAcknowledge"));
        using var history = await c.GetAsync("/api/v1/" + NotificationPath($"/{critical.Id}/history"), Ct);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.DoesNotContain("actorId", await history.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Concurrent_refresh_and_duplicate_acknowledgement_have_one_source_trigger_and_one_acknowledgement_event()
    {
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        var plan = CurrentCalendar(); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode);
        var refreshes = await Task.WhenAll(Send(c, NotificationPath("/refresh"), new { }), Send(c, NotificationPath("/refresh"), new { }));
        foreach (var r in refreshes) { Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict); r.Dispose(); }
        await RefreshNotifications(c); var inbox = await Inbox(c); Assert.Equal(2, inbox.Items.Length);
        var notice = inbox.Items[0]; var key = Guid.NewGuid().ToString(); var command = new NotificationAckCommand(notice.Version);
        var replies = await Task.WhenAll(Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key), Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key));
        var body = await replies[0].Content.ReadAsStringAsync(Ct);
        foreach (var r in replies) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(body, await r.Content.ReadAsStringAsync(Ct)); r.Dispose(); }
        await using var db = Db(); Assert.Single(await db.NotificationEvents.Where(e => e.NotificationId == notice.Id && e.Kind == "Acknowledged").ToArrayAsync(Ct));
        Assert.Single(await db.NotificationReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Notification_scope_and_current_permissions_precede_ids_history_receipts_and_preference_mutation()
    {
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode); await RefreshNotifications(c);
        var notice = (await Inbox(c)).Items[0]; var key = Guid.NewGuid().ToString(); var command = new NotificationAckCommand(notice.Version);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key)).StatusCode);
        foreach (var vessel in seed.Vessels.Where(v => v.TenantId != SeedVessel.TenantId)) {
            using var other = await c.GetAsync($"/api/v1/notification-escalation?vesselId={vessel.Id}", Ct);
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode); Assert.DoesNotContain(notice.Id.ToString(), await other.Content.ReadAsStringAsync(Ct));
        }
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => !p.StartsWith("notification.", StringComparison.Ordinal)).ToArray() };
        using var revoked = await Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        using var hidden = await c.GetAsync("/api/v1/" + NotificationPath($"/{notice.Id}/history"), Ct); Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        using var pref = await Send(c, NotificationPath("/preference"), new NotificationPreferenceCommand(0, true), update: true); Assert.Equal(HttpStatusCode.Forbidden, pref.StatusCode);
        await using var db = Db(); Assert.Single(await db.NotificationReceipts.ToArrayAsync(Ct)); Assert.Empty(await db.NotificationPreferences.ToArrayAsync(Ct));
    }
    private sealed class RejectNotificationReceipt : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("INSERT INTO engagement.\"NotificationReceipts\"", StringComparison.Ordinal)) throw new Npgsql.NpgsqlException("Fictional owned receipt fault");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Notification_receipt_fault_rolls_back_acknowledgement_event_head_and_business_audit_then_retry_recovers()
    {
        var fault = new RejectNotificationReceipt(); await using var host = NotificationHost(interceptor: fault); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode); await RefreshNotifications(c);
        var notice = (await Inbox(c)).Items[0]; var key = Guid.NewGuid().ToString(); var command = new NotificationAckCommand(notice.Version);
        long audits; await using (var db = Db()) audits = await db.AuditEvents.LongCountAsync(Ct);
        fault.Armed = true; using var failure = await Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        await using (var db = Db()) {
            Assert.Equal(notice.Version, (await db.Notifications.SingleAsync(n => n.Id == notice.Id, Ct)).Version);
            Assert.Empty(await db.NotificationEvents.Where(e => e.Kind == "Acknowledged").ToArrayAsync(Ct));
            Assert.Empty(await db.NotificationReceipts.ToArrayAsync(Ct)); Assert.Equal(audits, await db.AuditEvents.LongCountAsync(Ct));
        }
        fault.Armed = false; using var retry = await Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }
    [Fact]
    public async Task Missing_or_changed_routing_policy_is_explicit_and_requests_cannot_supply_authority_severity_or_mandatory_route()
    {
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode);
        using var spoof = await Send(c, NotificationPath("/refresh"), new { severity = "Critical", mandatory = false, actorId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
        host.Services.GetRequiredService<NotificationSettings>().Rules = [];
        using var unavailable = await Send(c, NotificationPath("/refresh"), new { }); Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("Unavailable", (await Inbox(c)).PolicyState);
        using var mandatory = await Send(c, NotificationPath("/preference"), new { expectedVersion = 0, mutedAdvisory = true, mandatoryRouteMutable = true }, update: true);
        Assert.Equal(HttpStatusCode.BadRequest, mandatory.StatusCode);
        await using var db = Db(); Assert.Empty(await db.Notifications.ToArrayAsync(Ct)); Assert.Empty(await db.NotificationPreferences.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Optional_notification_fixture_is_idempotent_unverified_hidden_and_preserves_real_notifications_events_and_receipts()
    {
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode); await RefreshNotifications(c);
        var before = await Inbox(c);
        await using (var seedContext = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true)) {
            await NotificationFixtures.AddMissingAsync(seedContext, seed.Vessels, Ct); await seedContext.SaveChangesAsync(Ct);
            var count = await seedContext.AuditEvents.LongCountAsync(Ct);
            await NotificationFixtures.AddMissingAsync(seedContext, seed.Vessels, Ct); await seedContext.SaveChangesAsync(Ct);
            Assert.Equal(count, await seedContext.AuditEvents.LongCountAsync(Ct));
        }
        Assert.Equal(before.Items, (await Inbox(c)).Items);
        await using var db = Db(); var synthetic = await db.Notifications.SingleAsync(n => n.Id == SeedVessel.Id, Ct);
        Assert.Equal("SyntheticUnverified", synthetic.State); Assert.Equal("Unknown", synthetic.Severity); Assert.Null(synthetic.DueDate);
        Assert.Empty(await db.NotificationEvents.Where(e => e.NotificationId == synthetic.Id && e.Kind != "FixturePrepared").ToArrayAsync(Ct));
        var immutable = await db.NotificationEvents.FirstAsync(Ct); immutable.Kind = "Forged";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Escalation_recipient_can_acknowledge_and_recover_the_same_receipt_after_the_alert_is_acknowledged()
    {
        var clock = new NotificationClock(); await using var host = NotificationHost(clock); using var c = Client(host);
        NotificationPermissions(host.Services); await Token(c); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode);
        await RefreshNotifications(c); clock.Current = clock.Current.AddSeconds(61); await RefreshNotifications(c);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        // Keep an actual eligible owner for the original plan while the current
        // user changes to the escalation role. Role transfer is not source loss.
        settings.Memberships = [..settings.Memberships, new(member.Issuer, "fictional-original-plan-owner", Guid.NewGuid(), member.TenantId,
            ["directory.read", "maintenance.owner.eligible"], VesselGrants: [new(SeedVessel.Id)]) { RoleReferences = [ReviewRole.ToString("D")] }];
        settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "notification.manage").ToArray(),
            RoleReferences = [NotificationEscalationRole.ToString("D")] };
        var notice = Assert.Single((await Inbox(c)).Items); Assert.Equal("Escalated", notice.State);
        var key = Guid.NewGuid().ToString(); var command = new NotificationAckCommand(notice.Version);
        using var first = await Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var retry = await Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(Ct), await retry.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Acknowledged", Assert.Single((await Inbox(c)).Items).State);
        await using var db = Db(); Assert.Single(await db.NotificationEvents.Where(e => e.NotificationId == notice.Id && e.Kind == "Acknowledged").ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Notification_source_foreign_key_and_database_guards_reject_cross_tenant_links_and_history_or_routing_mutation()
    {
        long before;
        await using (var db = Db()) {
            before = await db.AuditEvents.LongCountAsync(Ct);
            var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
            db.Notifications.Add(new() { TenantId = SeedVessel.TenantId, VesselId = foreign.Id, Id = Guid.NewGuid(),
                PlanId = foreign.Id, PlanRevision = 1, RuleKey = "fictional-cross-scope", PolicyVersion = "fictional-v1",
                PolicyDigest = RegisterStorage.Hash("fictional"), SourceState = "Due", Severity = "Warning", RecipientRoleId = ReviewRole, CreatedAt = DateTimeOffset.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }
        await using (var db = Db()) { Assert.Empty(await db.Notifications.ToArrayAsync(Ct)); Assert.Equal(before, await db.AuditEvents.LongCountAsync(Ct)); }
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode); await RefreshNotifications(c);
        await using var verify = Db(); var critical = await verify.Notifications.SingleAsync(n => n.Severity == "Critical", Ct);
        critical.Mandatory = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => verify.SaveChangesAsync(Ct)); verify.ChangeTracker.Clear();
        // Negative database-guard probes only: no successful raw business write.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => verify.Database.ExecuteSqlRawAsync(
            """UPDATE engagement."NotificationEvents" SET "Kind"='Forged'""", Ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => verify.Database.ExecuteSqlRawAsync(
            """DELETE FROM engagement."Notifications""", Ct));
        Assert.True((await verify.Notifications.SingleAsync(n => n.Severity == "Critical", Ct)).Mandatory);
    }
    [Fact]
    public async Task Notification_context_identity_keeps_the_exact_selected_reading_and_marks_unknown_due_assessment_unavailable_without_overwriting_its_basis()
    {
        await using var host = NotificationHost(); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        var selected = Reading("21"); Assert.Equal(HttpStatusCode.OK, (await Send(c, ObservationPath, selected)).StatusCode);
        var plan = CurrentCalendar() with { Inputs = CurrentCalendar().Inputs with { TriggerType = "Meter", IntervalValue = "10", IntervalUnit = "hours",
            LastDoneMeter = "10", ObservationId = selected.Id } };
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, plan)).StatusCode); await RefreshNotifications(c);
        var initial = await Inbox(c); Assert.Equal(2, initial.Items.Length); Assert.All(initial.Items, n => Assert.Equal("Due", n.SourceState));
        string originalContext;
        await using (var db = Db()) originalContext = (await db.Notifications.FirstAsync(Ct)).SourceContextHash;
        // Append another actual reading that is also over the threshold. The
        // owning plan pins the old immutable observation; it does not follow latest.
        var later = Reading("25"); Assert.Equal(HttpStatusCode.OK, (await Send(c, ObservationPath, later)).StatusCode);
        await RefreshNotifications(c); await RefreshNotifications(c);
        Assert.Equal(initial.Items, (await Inbox(c)).Items);
        await using (var db = Db()) {
            var head = await db.MaintenancePlans.SingleAsync(p => p.Id == plan.Id, Ct); Assert.Equal(1, head.Revision); Assert.Equal(selected.Id, head.ObservationId);
            Assert.All(await db.Notifications.ToArrayAsync(Ct), n => Assert.Equal(originalContext, n.SourceContextHash));
        }
        var settings = host.Services.GetRequiredService<NotificationSettings>();
        settings.Rules = [..settings.Rules, new(seed.Tenants[0].Id, SeedVessel.Id, "review-warning", "fictional-v1", "FICTIONAL changed-input route", "ReviewRequired", "Warning", false, null, null)];
        Assert.Equal(HttpStatusCode.OK, (await Send(c, ObservationPath + $"/{selected.Id}/revoke", new { })).StatusCode);
        await RefreshNotifications(c); await RefreshNotifications(c);
        var changed = await Inbox(c); Assert.Equal(3, changed.Items.Length);
        Assert.All(changed.Items.Where(n => initial.Items.Any(old => old.Id == n.Id)), n => Assert.Equal("SourceUnavailable", n.State));
        var replacement = Assert.Single(changed.Items, n => n.State == "Open"); Assert.Equal("ReviewRequired", replacement.SourceState);
        Assert.Equal(1, replacement.SourceRevision);
        await using var verify = Db(); var current = await verify.Notifications.SingleAsync(n => n.Id == replacement.Id, Ct);
        Assert.NotEqual(originalContext, current.SourceContextHash);
        Assert.All(await verify.Notifications.Where(n => n.Id != replacement.Id).ToArrayAsync(Ct), n => Assert.Equal(originalContext, n.SourceContextHash));
        Assert.Empty(await verify.NotificationEvents.Where(e => e.Kind == "SourceChanged").ToArrayAsync(Ct));
        Assert.All(await verify.NotificationEpisodes.Where(e => e.RuleKey != "review-warning").ToArrayAsync(Ct), e => Assert.Null(e.Active));
        Assert.Equal(1, (await verify.MaintenancePlans.SingleAsync(p => p.Id == plan.Id, Ct)).Revision);
    }
}
