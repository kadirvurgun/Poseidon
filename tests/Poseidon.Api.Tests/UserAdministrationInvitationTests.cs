using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// USER-ADMIN-02 / UA2-AC-05..UA2-AC-14. Invitation + acceptance by the identity holder on the actual host with real
// PostgreSQL. Synthetic identities only; the invitation session is what the OIDC login stamps for an identity without
// membership (X-Membership-Invitee in the synthetic handler).
public sealed partial class DirectoryWorkspaceTests
{
    // Both tenant administrators (synthetic in tenant 0, other-tenant-admin in tenant 1) can grant the read-only bundle.
    private WebApplicationFactory<Program> InvitationHost(MembershipClock? clock = null) => MembershipHost(clock).WithWebHostBuilder(b =>
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(AdminExtra.Select((p, i) =>
                new KeyValuePair<string, string?>($"Authentication:Oidc:Memberships:0:Permissions:{50 + i}", p))
            .Concat(AdminExtra.Select((p, i) => new KeyValuePair<string, string?>($"Authentication:Oidc:Memberships:4:Permissions:{10 + i}", p))))));

    private static async Task<HttpClient> Invitee(WebApplicationFactory<Program> host, string subject)
    {
        var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Membership-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Membership-Invitee", "true");
        var session = await client.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", TestContext.Current.CancellationToken);
        Assert.True(session is { Authenticated: true, InvitationSession: true, ActorId: null, CanRead: false });
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
        return client;
    }

    private async Task<MembershipInvitationIssueDto> Invited(HttpClient admin, object body)
    {
        using var response = await Post(admin, "memberships", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var issued = (await response.Content.ReadFromJsonAsync<MembershipInvitationIssueDto>(Ct))!;
        Assert.Equal(("Invited", "Pending"), (issued.Membership.Membership.Status, issued.Membership.Membership.InvitationState));
        Assert.NotNull(issued.InvitationToken);
        return issued;
    }

    private static object Invite(string? expectedSubject, string[]? bundles = null) => new
    {
        issuer = MemberIssuer, subject = expectedSubject, bundles = bundles ?? ["read-only"], permissions = Array.Empty<string>(), tenantWideRead = true,
        tenantWideManage = false, vesselGrants = Array.Empty<object>()
    };

    // Fixture-owned audit connection: invitation-session events carry no tenant until the invitation is identified.
    private async Task<SecurityEvent[]> SecurityEvents()
    {
        await using var audit = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(postgres.GetConnectionString()).Options);
        return await audit.Set<SecurityEvent>().AsNoTracking().Where(e => e.Action == "MembershipInvitation").ToArrayAsync(Ct);
    }

    private async Task AssertNeutral(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        Assert.Equal("invitation_unavailable", await MembershipCode(response));
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-02")]
    public async Task A_foreign_tenant_invitation_cannot_squat_an_identity_and_grants_nothing_until_its_holder_accepts()
    {
        await using var host = InvitationHost();
        using var admin = await Member(host, "synthetic");
        using var foreignAdmin = await Member(host, "other-tenant-admin");
        var t1 = seed.Tenants[0].Id;

        // The tenant-2 administrator invites a tenant-1 user's subject first: nothing is reserved globally.
        var squat = await Invited(foreignAdmin, Invite("victim"));
        var invited = await Invited(admin, Invite("victim"));
        Assert.NotEqual(squat.Membership.Membership.AccountId, invited.Membership.Membership.AccountId);
        // Invited grants nothing: even a forged provisioned binding for the invited account is refused.
        using (var early = await Provisioned(host, "victim", t1, invited.Membership.Membership.AccountId))
            Assert.Equal(HttpStatusCode.Unauthorized, (await early.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        // The locator (login lookup) finds no binding before acceptance.
        Assert.Null(await host.Services.GetRequiredService<ProvisionedIdentityLocator>().FindAsync(MemberIssuer, "victim", "test", Ct));

        using var victim = await Invitee(host, "victim");
        var preview = (await (await Post(victim, "invitations/preview", new { token = invited.InvitationToken })).Content.ReadFromJsonAsync<InvitationGrantDto>(Ct))!;
        Assert.Equal(["read-only"], preview.Bundles);
        Assert.Equal((true, false), (preview.TenantWideRead, preview.TenantWideManage));
        Assert.Equal(seed.Tenants[0].Name, preview.TenantName);
        var key = Guid.NewGuid().ToString();
        using (var accept = await Post(victim, "invitations/accept", new { token = invited.InvitationToken }, key))
        {
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            var body = await accept.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("victim", body); Assert.DoesNotContain(invited.InvitationToken!, body);
            var accepted = (await accept.Content.ReadFromJsonAsync<InvitationAcceptedDto>(Ct))!;
            Assert.Equal((invited.Membership.Membership.Id, true), (accepted.MembershipId, accepted.SignInRequired));
        }
        // Lost-reply retry with the same key replays the committed outcome; a second accept is neutral.
        using (var replay = await Post(victim, "invitations/accept", new { token = invited.InvitationToken }, key))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        }
        await AssertNeutral(await Post(victim, "invitations/accept", new { token = invited.InvitationToken }));

        // The holder now logs into tenant 1 only; the tenant-2 invitation can no longer bind the identity (strict: one
        // accepted membership per identity), refused with the same neutral code as an unknown token.
        var located = await host.Services.GetRequiredService<ProvisionedIdentityLocator>().FindAsync(MemberIssuer, "victim", "test", Ct);
        Assert.Equal((t1, invited.Membership.Membership.AccountId), (located!.TenantId, located.ActorId));
        using (var member = await Provisioned(host, "victim", t1, invited.Membership.Membership.AccountId))
            Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        await AssertNeutral(await Post(victim, "invitations/preview", new { token = squat.InvitationToken }));
        await AssertNeutral(await Post(victim, "invitations/accept", new { token = squat.InvitationToken }));
        var foreignView = (await foreignAdmin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{squat.Membership.Membership.Id}/access", Ct))!;
        Assert.Equal(("Invited", "Pending"), (foreignView.Membership.Status, foreignView.Membership.InvitationState));

        // Tenant isolation: each administrator sees only their own tenant's invitation; no cross-tenant reissue.
        Assert.Equal(HttpStatusCode.NotFound, (await foreignAdmin.GetAsync($"/api/v1/memberships/{invited.Membership.Membership.Id}/access", Ct)).StatusCode);
        using (var crossReissue = await Post(foreignAdmin, $"memberships/{invited.Membership.Membership.Id}/invitation", new { expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.NotFound, crossReissue.StatusCode);

        // Members (configured or provisioned sessions) cannot preview or accept invitations: 403, nothing written.
        using var configured = await Member(host, "member-b");
        using (var denied = await Post(configured, "invitations/accept", new { token = squat.InvitationToken }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // No token, token hash or subject in the security or EF audit trail.
        await using var db = Db();
        var invitations = await db.MembershipInvitations.AsNoTracking().Where(i => i.MembershipId == invited.Membership.Membership.Id).ToArrayAsync(Ct);
        var hash = Assert.Single(invitations).TokenHash;
        Assert.Equal("Accepted", invitations[0].Status);
        var secret = invited.InvitationToken!.Split('.')[2];
        Assert.NotEqual(secret, hash);
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.ChangeSummary != null).Select(e => e.ChangeSummary!).ToArrayAsync(Ct);
        Assert.DoesNotContain(audit, x => x.Contains(hash, StringComparison.Ordinal) || x.Contains(secret, StringComparison.Ordinal) || x.Contains("\"victim\"", StringComparison.Ordinal));
        var events = await SecurityEvents();
        Assert.Contains(events, e => e.Result == "Success" && e.TenantId == t1 && e.ActorId == invited.Membership.Membership.AccountId.ToString());
        Assert.Contains(events, e => e.Result == "identity_unavailable");
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-02")]
    public async Task Expired_reissued_revoked_used_and_wrong_subject_invitations_are_refused_neutrally()
    {
        var clock = new MembershipClock(new DateTimeOffset(2026, 11, 22, 8, 0, 0, TimeSpan.Zero));
        await using var host = InvitationHost(clock);
        using var admin = await Member(host, "synthetic");

        // Wrong subject: an expected subject binds the invitation to exactly that identity.
        var expected = await Invited(admin, Invite("expected-holder"));
        using (var intruder = await Invitee(host, "intruder"))
        {
            await AssertNeutral(await Post(intruder, "invitations/preview", new { token = expected.InvitationToken }));
            await AssertNeutral(await Post(intruder, "invitations/accept", new { token = expected.InvitationToken }));
            // Malformed and unknown tokens look exactly the same.
            await AssertNeutral(await Post(intruder, "invitations/accept", new { token = "not-a-token" }));
            await AssertNeutral(await Post(intruder, "invitations/accept", new { token = expected.InvitationToken![..^4] + "AAAA" }));
            using var unknownField = await Post(intruder, "invitations/accept", new { token = expected.InvitationToken, tenantId = seed.Tenants[1].Id });
            Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        }
        using (var duplicate = await Post(admin, "memberships", Invite("expected-holder")))
            Assert.Equal("identity_unavailable", await MembershipCode(duplicate));

        // Expiry (24 hours, server time): the admin sees Expired, the link is refused, a reissue issues a working link and
        // the old link stays dead.
        var open = await Invited(admin, Invite(null));
        clock.Now = clock.Now.AddDays(7).AddMinutes(1);
        using (var late = await Invitee(host, "late-holder"))
            await AssertNeutral(await Post(late, "invitations/accept", new { token = open.InvitationToken }));
        var expiredView = (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{open.Membership.Membership.Id}/access", Ct))!;
        Assert.Equal(("Invited", "Expired"), (expiredView.Membership.Status, expiredView.Membership.InvitationState));
        using (var stale = await Post(admin, $"memberships/{open.Membership.Membership.Id}/invitation", new { expectedVersion = expiredView.Membership.Version + 1 }))
            Assert.Equal("version_conflict", await MembershipCode(stale));
        using var reissue = await Post(admin, $"memberships/{open.Membership.Membership.Id}/invitation", new { expectedVersion = expiredView.Membership.Version });
        Assert.Equal(HttpStatusCode.OK, reissue.StatusCode);
        var fresh = (await reissue.Content.ReadFromJsonAsync<MembershipInvitationIssueDto>(Ct))!;
        Assert.NotEqual(open.InvitationToken, fresh.InvitationToken);
        Assert.Equal("Pending", fresh.Membership.Membership.InvitationState);
        using (var late = await Invitee(host, "late-holder"))
        {
            await AssertNeutral(await Post(late, "invitations/accept", new { token = open.InvitationToken }));
            using var accepted = await Post(late, "invitations/accept", new { token = fresh.InvitationToken });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            // Used: the same link again, by the holder or anyone else, is refused.
            await AssertNeutral(await Post(late, "invitations/accept", new { token = fresh.InvitationToken }));
        }
        var acceptedView = (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{open.Membership.Membership.Id}/access", Ct))!;
        using (var noReissue = await Post(admin, $"memberships/{open.Membership.Membership.Id}/invitation", new { expectedVersion = acceptedView.Membership.Version }))
        { Assert.Equal(HttpStatusCode.Conflict, noReissue.StatusCode); Assert.Equal("invalid_transition", await MembershipCode(noReissue)); }

        // Revoked: revoking the Invited membership closes its link.
        var revoked = await Invited(admin, Invite(null));
        using (var revoke = await Post(admin, $"memberships/{revoked.Membership.Membership.Id}/revoke", new { expectedVersion = revoked.Membership.Membership.Version }))
            Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        using (var suspendInvited = await Post(admin, $"memberships/{expected.Membership.Membership.Id}/suspend", new { expectedVersion = expected.Membership.Membership.Version }))
            Assert.Equal("invalid_transition", await MembershipCode(suspendInvited));
        using (var holder = await Invitee(host, "revoked-holder"))
            await AssertNeutral(await Post(holder, "invitations/accept", new { token = revoked.InvitationToken }));
        var revokedView = (await admin.GetFromJsonAsync<MembershipAccessDto>($"/api/v1/memberships/{revoked.Membership.Membership.Id}/access", Ct))!;
        Assert.Equal(("Revoked", "Revoked"), (revokedView.Membership.Status, revokedView.Membership.InvitationState));

        await using var db = Db();
        var results = (await SecurityEvents()).Select(e => e.Result).ToArray();
        Assert.Contains("invitation_subject_mismatch", results);
        Assert.Contains("invitation_expired", results);
        Assert.Contains("invitation_token_invalid", results);
        Assert.Contains("invitation_closed", results);
        Assert.Equal(3, await db.MembershipInvitations.AsNoTracking().CountAsync(i => i.MembershipId == open.Membership.Membership.Id ||
            i.MembershipId == revoked.Membership.Membership.Id, Ct));
        Assert.Equal(1, await db.MembershipInvitations.AsNoTracking().CountAsync(i => i.MembershipId == open.Membership.Membership.Id && i.Status == "Superseded", Ct));
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-02")]
    public async Task Concurrent_acceptance_binds_exactly_one_identity_and_an_identity_binds_at_most_one_tenant()
    {
        await using var host = InvitationHost();
        using var admin = await Member(host, "synthetic");
        using var foreignAdmin = await Member(host, "other-tenant-admin");

        // Four identities race for one open invitation: exactly one binding wins, the others are refused neutrally.
        var open = await Invited(admin, Invite(null));
        var racers = new List<HttpClient>();
        for (var i = 0; i < 4; i++) racers.Add(await Invitee(host, $"racer-{i}"));
        var responses = await Task.WhenAll(racers.Select(r => Post(r, "invitations/accept", new { token = open.InvitationToken })));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        await using (var db = Db())
        {
            var account = await db.UserAccounts.AsNoTracking().SingleAsync(a => a.Id == open.Membership.Membership.AccountId, Ct);
            Assert.StartsWith("racer-", account.Subject);
            Assert.Equal("Active", (await db.AccountMemberships.AsNoTracking().SingleAsync(m => m.Id == open.Membership.Membership.Id, Ct)).Status);
        }

        // One identity races to accept invitations of two tenants: it ends bound to exactly one of them.
        var t1 = await Invited(admin, Invite("traveller"));
        var t2 = await Invited(foreignAdmin, Invite("traveller"));
        using var a = await Invitee(host, "traveller");
        using var b = await Invitee(host, "traveller");
        var both = await Task.WhenAll(Post(a, "invitations/accept", new { token = t1.InvitationToken }), Post(b, "invitations/accept", new { token = t2.InvitationToken }));
        Assert.Equal(1, both.Count(r => r.StatusCode == HttpStatusCode.OK));
        var winner = both[0].StatusCode == HttpStatusCode.OK ? seed.Tenants[0].Id : seed.Tenants[1].Id;
        var located = await host.Services.GetRequiredService<ProvisionedIdentityLocator>().FindAsync(MemberIssuer, "traveller", "test", Ct);
        Assert.Equal(winner, located!.TenantId);
        foreach (var r in responses.Concat(both)) r.Dispose();
        foreach (var r in racers) r.Dispose();
    }

    [Fact]
    [Trait("Requirement", "USER-ADMIN-02")]
    public async Task Invitation_attempts_are_rate_limited_per_identity_and_need_antiforgery_and_a_key()
    {
        await using var host = InvitationHost();
        using var admin = await Member(host, "synthetic");
        var issued = await Invited(admin, Invite("guarded-holder"));
        using var guarded = await Invitee(host, "guarded-holder");
        using (var noKey = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations/accept") { Content = JsonContent.Create(new { token = issued.InvitationToken }) })
            Assert.Equal(HttpStatusCode.BadRequest, (await guarded.SendAsync(noKey, Ct)).StatusCode);
        using (var noCsrf = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false }))
        {
            noCsrf.DefaultRequestHeaders.Add("X-Membership-Subject", "guarded-holder");
            noCsrf.DefaultRequestHeaders.Add("X-Membership-Invitee", "true");
            using var response = await Post(noCsrf, "invitations/accept", new { token = issued.InvitationToken });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var prober = await Invitee(host, "prober");
        for (var i = 0; i < InvitationAttemptLimiter.AttemptsPerWindow; i++)
            await AssertNeutral(await Post(prober, "invitations/preview", new { token = issued.InvitationToken }));
        using (var limited = await Post(prober, "invitations/preview", new { token = issued.InvitationToken }))
        { Assert.Equal((HttpStatusCode)429, limited.StatusCode); Assert.Equal("request_limit", await MembershipCode(limited)); }
        // Another identity is unaffected; the holder can still accept.
        using (var accept = await Post(guarded, "invitations/accept", new { token = issued.InvitationToken }))
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
    }
}
