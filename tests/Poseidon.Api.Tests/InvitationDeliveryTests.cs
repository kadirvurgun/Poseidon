using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// USER-ADMIN-03 / UA3-AC-04..UA3-AC-10. Invitation e-mail outbox and the tenant invitation lifetime on the actual host
// with real PostgreSQL. A recording mailer stands in for the SMTP server; synthetic .invalid addresses only.
public sealed partial class DirectoryWorkspaceTests
{
    private const string InviteeEmail = "invitee@example.invalid";

    private sealed class RecordingMailer : IInvitationMailer
    {
        public ConcurrentQueue<InvitationMailMessage> Sent { get; } = new();
        public ConcurrentQueue<InvitationSendOutcome> Script { get; } = new();
        public bool Configured { get; set; } = true;
        public Task<InvitationSendOutcome> SendAsync(InvitationMailMessage message, CancellationToken ct)
        {
            var outcome = Script.TryDequeue(out var next) ? next : InvitationSendOutcome.Accepted;
            if (outcome == InvitationSendOutcome.Accepted) Sent.Enqueue(message);
            return Task.FromResult(outcome);
        }
    }

    // Transport configured (PollSeconds 3600: the background loop runs once at start; the test drives RunOnceAsync).
    private WebApplicationFactory<Program> DeliveryHost(RecordingMailer? mailer, MembershipClock? clock = null) =>
        MembershipHost(clock, s => { if (mailer != null) { s.RemoveAll<IInvitationMailer>(); s.AddSingleton<IInvitationMailer>(mailer); } })
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(AdminExtra.Select((p, i) =>
                    new KeyValuePair<string, string?>($"Authentication:Oidc:Memberships:0:Permissions:{50 + i}", p))
                .Concat(AdminExtra.Select((p, i) => new KeyValuePair<string, string?>($"Authentication:Oidc:Memberships:4:Permissions:{10 + i}", p)))
                .Concat(mailer == null ? [] : new Dictionary<string, string?>
                {
                    ["InvitationDelivery:Enabled"] = "true", ["InvitationDelivery:PublicBaseUrl"] = "https://poseidon.example.invalid",
                    ["InvitationDelivery:SmtpHost"] = "127.0.0.1", ["InvitationDelivery:SmtpPort"] = "1", ["InvitationDelivery:Security"] = "None",
                    ["InvitationDelivery:From"] = "noreply@example.invalid", ["InvitationDelivery:PollSeconds"] = "3600",
                }))));

    private static object InviteByEmail(string? email) => new
    {
        issuer = MemberIssuer, subject = (string?)null, bundles = new[] { "read-only" }, permissions = Array.Empty<string>(), tenantWideRead = true,
        tenantWideManage = false, vesselGrants = Array.Empty<object>(), deliveryEmail = email
    };

    private async Task<InvitationDelivery[]> Deliveries(Guid membership)
    {
        await using var db = Db();
        return await db.InvitationDeliveries.AsNoTracking().Where(d => d.MembershipId == membership).OrderBy(d => d.CreatedAt).ThenBy(d => d.Id).ToArrayAsync(Ct);
    }

    private static async Task Dispatch(WebApplicationFactory<Program> host)
    {
        var dispatcher = host.Services.GetRequiredService<InvitationDeliveryDispatcher>();
        for (var i = 0; i < 50; i++)
        {
            if ((await dispatcher.RunOnceAsync(TestContext.Current.CancellationToken)).Status != "Busy") return;
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("dispatcher busy");
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-03")]
    public async Task An_invitation_email_is_queued_with_the_invitation_and_sent_by_the_attributed_system_dispatcher()
    {
        var mailer = new RecordingMailer();
        await using var host = DeliveryHost(mailer);
        using var admin = await Member(host, "synthetic");
        var issued = await Invited(admin, InviteByEmail(InviteeEmail));
        var membership = issued.Membership.Membership.Id;
        // The copy link stays available: the token is still returned once.
        Assert.NotNull(issued.InvitationToken);
        Assert.Equal("QUEUED", issued.Membership.Membership.InvitationDeliveryState);
        var queued = Assert.Single(await Deliveries(membership));
        Assert.Equal(("QUEUED", 0), (queued.State, queued.Attempts));
        Assert.DoesNotContain(InviteeEmail, queued.ProtectedPayload!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(issued.InvitationToken!, queued.ProtectedPayload!);

        await Dispatch(host);
        var message = Assert.Single(mailer.Sent);
        Assert.Equal(InviteeEmail, message.To);
        Assert.Equal("https://poseidon.example.invalid/app/accept-invitation#token=" + Uri.EscapeDataString(issued.InvitationToken!), message.Link);
        Assert.Equal(issued.ExpiresAt, message.ExpiresAt);
        var sent = Assert.Single(await Deliveries(membership));
        Assert.Equal(("SENT", 1, (string?)null, (string?)null), (sent.State, sent.Attempts, sent.ProtectedPayload, sent.LastErrorCode));
        Assert.NotNull(sent.CompletedAt);
        // A second run sends nothing more (terminal).
        await Dispatch(host);
        Assert.Single(mailer.Sent);

        var view = (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{membership}/access", Ct))!;
        Assert.Equal(("Invited", "Pending", "SENT"), (view.Membership.Status, view.Membership.InvitationState, view.Membership.InvitationDeliveryState));

        await using var db = Db();
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.EntityType == nameof(InvitationDelivery)).OrderBy(e => e.OccurredAt).ToArrayAsync(Ct);
        Assert.Contains(audit, e => e.Action == "Added" && e.ActorType == "User");
        Assert.Contains(audit, e => e.Action == "Modified" && e.ActorType == "System" && e.ActorId == InvitationDeliveryDispatcher.ActorId &&
            e.ReasonStatus == "SystemOperation" && e.ChangeSummary!.Contains("\"SENT\"", StringComparison.Ordinal));
        var summaries = await db.AuditEvents.AsNoTracking().Where(e => e.ChangeSummary != null).Select(e => e.ChangeSummary!).ToArrayAsync(Ct);
        Assert.All(summaries, s => { Assert.DoesNotContain(InviteeEmail, s, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(issued.InvitationToken!, s); });
        // Security events never carry the address either.
        Assert.All(await SecurityEvents(), e => Assert.DoesNotContain(InviteeEmail, System.Text.Json.JsonSerializer.Serialize(e), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-03")]
    public async Task Transient_failures_retry_with_backoff_and_end_in_a_terminal_failed_state()
    {
        var clock = new MembershipClock(DateTimeOffset.UtcNow);
        var mailer = new RecordingMailer();
        mailer.Script.Enqueue(InvitationSendOutcome.TransientFailure);
        mailer.Script.Enqueue(InvitationSendOutcome.PermanentFailure);
        await using var host = DeliveryHost(mailer, clock);
        using var admin = await Member(host, "synthetic");
        var membership = (await Invited(admin, InviteByEmail(InviteeEmail))).Membership.Membership.Id;
        await Dispatch(host);
        var retry = Assert.Single(await Deliveries(membership));
        Assert.Equal(("QUEUED", 1, "smtp_transient"), (retry.State, retry.Attempts, retry.LastErrorCode));
        Assert.Equal(clock.Now.AddMinutes(1), retry.NextAttemptAt);
        Assert.NotNull(retry.ProtectedPayload);
        // Not due yet: nothing is attempted.
        await Dispatch(host);
        Assert.Equal(1, Assert.Single(await Deliveries(membership)).Attempts);
        clock.Now = clock.Now.AddMinutes(2);
        await Dispatch(host);
        var failed = Assert.Single(await Deliveries(membership));
        Assert.Equal(("FAILED", 2, "smtp_rejected", (string?)null), (failed.State, failed.Attempts, failed.LastErrorCode, failed.ProtectedPayload));
        Assert.Empty(mailer.Sent);
        var view = (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{membership}/access", Ct))!;
        Assert.Equal(("Pending", "FAILED"), (view.Membership.InvitationState, view.Membership.InvitationDeliveryState));
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-03")]
    public async Task Without_a_transport_the_email_is_not_configured_and_the_copy_link_still_works()
    {
        await using var host = DeliveryHost(null);
        using var admin = await Member(host, "synthetic");
        var settings = (await admin.GetFromJsonAsync<InvitationSettingsDto>("/api/v1/memberships/invitation-settings", Ct))!;
        Assert.False(settings.DeliveryConfigured);
        var issued = await Invited(admin, InviteByEmail(InviteeEmail));
        Assert.NotNull(issued.InvitationToken);
        Assert.Equal("NOT_CONFIGURED", issued.Membership.Membership.InvitationDeliveryState);
        var row = Assert.Single(await Deliveries(issued.Membership.Membership.Id));
        Assert.Equal(("NOT_CONFIGURED", "not_configured", (string?)null), (row.State, row.LastErrorCode, row.ProtectedPayload));
        // No e-mail requested: no delivery row and no delivery state.
        var plain = await Invited(admin, InviteByEmail(null));
        Assert.Null(plain.Membership.Membership.InvitationDeliveryState);
        Assert.Empty(await Deliveries(plain.Membership.Membership.Id));
        // The token from the not-configured invitation is accepted normally (copy-link flow).
        using var holder = await Invitee(host, "email-fallback-holder");
        using var accept = await Post(holder, "invitations/accept", new { token = issued.InvitationToken });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        // Invalid recipient: field error, nothing written.
        using var bad = await Post(admin, "memberships", InviteByEmail("Name <invitee@example.invalid>"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("invalid", await FieldError(bad, "deliveryEmail"));
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-03")]
    public async Task Reissue_and_revoke_cancel_a_queued_email_and_erase_its_payload()
    {
        var mailer = new RecordingMailer();
        await using var host = DeliveryHost(mailer);
        using var admin = await Member(host, "synthetic");
        var first = await Invited(admin, InviteByEmail(InviteeEmail));
        var membership = first.Membership.Membership.Id;
        using (var reissue = await Post(admin, $"memberships/{membership}/invitation", new { expectedVersion = first.Membership.Membership.Version, deliveryEmail = "second@example.invalid" }))
            Assert.Equal(HttpStatusCode.OK, reissue.StatusCode);
        var afterReissue = await Deliveries(membership);
        Assert.Equal(2, afterReissue.Length);
        Assert.Equal(("CANCELLED", "invitation_closed", (string?)null), (afterReissue[0].State, afterReissue[0].LastErrorCode, afterReissue[0].ProtectedPayload));
        Assert.Equal("QUEUED", afterReissue[1].State);
        var current = (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{membership}/access", Ct))!;
        using (var revoke = await Post(admin, $"memberships/{membership}/revoke", new { expectedVersion = current.Membership.Version }))
            Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.All(await Deliveries(membership), d => Assert.Equal(("CANCELLED", (string?)null), (d.State, d.ProtectedPayload)));
        await Dispatch(host);
        Assert.Empty(mailer.Sent); // a closed invitation is never e-mailed
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-03")]
    public async Task The_tenant_invitation_lifetime_is_bounded_permission_gated_audited_and_used_on_create()
    {
        var clock = new MembershipClock(new DateTimeOffset(2026, 11, 22, 8, 0, 0, TimeSpan.Zero));
        await using var host = DeliveryHost(null, clock);
        using var admin = await Member(host, "synthetic");
        using var foreignAdmin = await Member(host, "other-tenant-admin");
        using var reader = await Member(host, "member-b");
        var initial = (await admin.GetFromJsonAsync<InvitationSettingsDto>("/api/v1/memberships/invitation-settings", Ct))!;
        Assert.Equal((24, 1, 168, 24, false, 0u, true), (initial.LifetimeHours, initial.MinHours, initial.MaxHours, initial.DefaultHours, initial.TenantValue, initial.Version, initial.CanManage));
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/v1/memberships/invitation-settings", Ct)).StatusCode);
        using (var denied = await Post(reader, "memberships/invitation-settings", new { expectedVersion = 0, lifetimeHours = 48 }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        foreach (var hours in new[] { 0, 169, -1 })
        {
            using var bad = await Post(admin, "memberships/invitation-settings", new { expectedVersion = 0, lifetimeHours = hours });
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            Assert.Equal("out_of_range", await FieldError(bad, "lifetimeHours"));
        }
        InvitationSettingsDto changed;
        using (var ok = await Post(admin, "memberships/invitation-settings", new { expectedVersion = 0, lifetimeHours = 48 }))
        {
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            changed = (await ok.Content.ReadFromJsonAsync<InvitationSettingsDto>(Ct))!;
        }
        Assert.Equal((48, true), (changed.LifetimeHours, changed.TenantValue));
        Assert.True(changed.Version > 0);
        using (var stale = await Post(admin, "memberships/invitation-settings", new { expectedVersion = 0, lifetimeHours = 72 }))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("version_conflict", await MembershipCode(stale));
        }
        // Tenant isolation: the other tenant still uses the source default.
        var foreign = (await foreignAdmin.GetFromJsonAsync<InvitationSettingsDto>("/api/v1/memberships/invitation-settings", Ct))!;
        Assert.Equal((24, false), (foreign.LifetimeHours, foreign.TenantValue));
        var issued = await Invited(admin, InviteByEmail(null));
        Assert.Equal(clock.Now.AddHours(48), issued.ExpiresAt);
        var foreignIssued = await Invited(foreignAdmin, InviteByEmail(null));
        Assert.Equal(clock.Now.AddHours(24), foreignIssued.ExpiresAt);

        await using var db = Db();
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.EntityType == nameof(TenantInvitationSettings)).ToArrayAsync(Ct);
        var added = Assert.Single(audit);
        Assert.Equal(("Added", "User"), (added.Action, added.ActorType));
        Assert.Contains("\"48\"", added.ChangeSummary!, StringComparison.Ordinal);
        Assert.Contains(await db.MembershipCommandReceipts.AsNoTracking().ToArrayAsync(Ct), r => r.Action == "InvitationLifetimeUpdate");
    }
}
