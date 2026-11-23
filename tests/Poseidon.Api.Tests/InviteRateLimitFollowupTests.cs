using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// INVITE-RATELIMIT-FOLLOWUPS on the actual host with real PostgreSQL. N4: the idempotency fingerprint of invitation
// create/reissue binds the recipient address (keyed digest of the normalized address, never the address). Rate limit:
// invitation create and reissue share one budget per administrator. Synthetic .invalid addresses only.
public sealed partial class DirectoryWorkspaceTests
{
    private const string FirstAddress = "first.invitee@example.invalid";
    private const string OtherAddress = "other.invitee@example.invalid";

    private async Task AssertReplayWithoutToken(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Idempotency-Replayed")));
        Assert.Null((await response.Content.ReadFromJsonAsync<MembershipInvitationIssueDto>(Ct))!.InvitationToken);
    }

    private async Task AssertKeyReused(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        Assert.Equal("idempotency_key_reused", await MembershipCode(response));
    }

    private async Task AssertNoAddressStored(Guid tenant, params string[] addresses)
    {
        await using var db = Db();
        var fingerprints = await db.MembershipCommandReceipts.AsNoTracking().Select(r => r.Fingerprint).ToArrayAsync(Ct);
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.TenantId == tenant && e.ChangeSummary != null).Select(e => e.ChangeSummary!).ToArrayAsync(Ct);
        foreach (var address in addresses)
        {
            Assert.DoesNotContain(fingerprints, f => f.Contains(address, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(audit, a => a.Contains(address, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    [Trait("Requirement", "INVITE-RATELIMIT-FOLLOWUPS")]
    public async Task Create_with_the_same_key_replays_only_for_the_same_recipient_and_conflicts_for_another_address()
    {
        await using var host = InvitationHost();
        using var admin = await Member(host, "synthetic");
        var key = Guid.NewGuid().ToString();
        Guid membership;
        using (var created = await Post(admin, "memberships", InviteByEmail(FirstAddress), key))
        {
            Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync(Ct));
            var issued = (await created.Content.ReadFromJsonAsync<MembershipInvitationIssueDto>(Ct))!;
            Assert.NotNull(issued.InvitationToken);
            membership = issued.Membership.Membership.Id;
        }
        // Lost reply: the same mailbox (case-folded spelling) under the same key replays the committed outcome.
        await AssertReplayWithoutToken(await Post(admin, "memberships", InviteByEmail(FirstAddress), key));
        await AssertReplayWithoutToken(await Post(admin, "memberships", InviteByEmail(FirstAddress.ToUpperInvariant()), key));
        // Another address, or no address, under the same key is a different request: 409, nothing new is issued.
        await AssertKeyReused(await Post(admin, "memberships", InviteByEmail(OtherAddress), key));
        await AssertKeyReused(await Post(admin, "memberships", InviteByEmail(null), key));
        await using (var db = Db())
            Assert.Single(await db.InvitationDeliveries.AsNoTracking().Where(d => d.MembershipId == membership).ToArrayAsync(Ct));
        // Without an e-mail the same key replays only a request without an e-mail.
        var plainKey = Guid.NewGuid().ToString();
        using (var plain = await Post(admin, "memberships", InviteByEmail(null), plainKey)) Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        await AssertReplayWithoutToken(await Post(admin, "memberships", InviteByEmail(null), plainKey));
        await AssertKeyReused(await Post(admin, "memberships", InviteByEmail(FirstAddress), plainKey));
        await AssertNoAddressStored(seed.Tenants[0].Id, FirstAddress, OtherAddress);
    }

    [Fact]
    [Trait("Requirement", "INVITE-RATELIMIT-FOLLOWUPS")]
    public async Task Reissue_with_the_same_key_replays_only_for_the_same_recipient_and_conflicts_for_another_address()
    {
        await using var host = InvitationHost();
        using var admin = await Member(host, "synthetic");
        var first = await Invited(admin, InviteByEmail(null));
        var membership = first.Membership.Membership.Id;
        var version = first.Membership.Membership.Version;
        var key = Guid.NewGuid().ToString();
        using (var reissued = await Post(admin, $"memberships/{membership}/invitation", new { expectedVersion = version, deliveryEmail = FirstAddress }, key))
        {
            Assert.True(reissued.StatusCode == HttpStatusCode.OK, await reissued.Content.ReadAsStringAsync(Ct));
            Assert.NotNull((await reissued.Content.ReadFromJsonAsync<MembershipInvitationIssueDto>(Ct))!.InvitationToken);
        }
        await AssertReplayWithoutToken(await Post(admin, $"memberships/{membership}/invitation", new { expectedVersion = version, deliveryEmail = FirstAddress }, key));
        await AssertKeyReused(await Post(admin, $"memberships/{membership}/invitation", new { expectedVersion = version, deliveryEmail = OtherAddress }, key));
        await AssertKeyReused(await Post(admin, $"memberships/{membership}/invitation", new { expectedVersion = version }, key));
        await using (var db = Db())
        {
            // One reissue only: the original invitation superseded, one new pending one, one delivery row.
            var invitations = await db.MembershipInvitations.AsNoTracking().Where(i => i.MembershipId == membership).ToArrayAsync(Ct);
            Assert.Equal(2, invitations.Length);
            Assert.Single(invitations, i => i.Status == "Pending");
            Assert.Single(await db.InvitationDeliveries.AsNoTracking().Where(d => d.MembershipId == membership).ToArrayAsync(Ct));
        }
        await AssertNoAddressStored(seed.Tenants[0].Id, FirstAddress, OtherAddress);
    }

    [Fact]
    [Trait("Requirement", "INVITE-RATELIMIT-FOLLOWUPS")]
    public async Task Invitation_create_and_reissue_share_a_per_administrator_limit_with_retry_after_and_one_audited_refusal()
    {
        await using var host = InvitationHost();
        using var admin = await Member(host, "synthetic");
        using var foreignAdmin = await Member(host, "other-tenant-admin");
        var issued = await Invited(admin, Invite(null));
        // Invite QA N1 (followups-20261123): cross-site requests without a valid antiforgery token are refused (audited
        // csrf_invalid) and never consume the administrator's budget; otherwise the final reissue below would be 429.
        for (var i = 0; i < 5; i++)
        {
            using var forged = new HttpRequestMessage(HttpMethod.Post, "/api/v1/memberships") { Content = JsonContent.Create(Invite(null)) };
            forged.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            forged.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", "forged-token");
            var saved = admin.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").ToArray();
            admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            try
            {
                using var refused = await admin.SendAsync(forged, Ct);
                Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
                Assert.Equal("csrf_invalid", await MembershipCode(refused));
            }
            finally { admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN", saved); }
        }
        // Refused bodies count too: the limit is taken before parsing or any write (1 create above + 58 + 1 reissue = 60).
        for (var i = 0; i < InvitationIssueRateLimiter.IssuesPerWindow - 2; i++)
            using (var invalid = await Post(admin, "memberships", new { unknownField = i }))
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var reissue = await Post(admin, $"memberships/{issued.Membership.Membership.Id}/invitation", new { expectedVersion = issued.Membership.Membership.Version }))
            Assert.Equal(HttpStatusCode.OK, reissue.StatusCode);
        var before = await MembershipCommandEvents("request_limit");
        foreach (var path in new[] { "memberships", $"memberships/{issued.Membership.Membership.Id}/invitation" })
        {
            using var limited = await Post(admin, path, path == "memberships" ? Invite(null) : new { expectedVersion = issued.Membership.Membership.Version + 1 });
            Assert.Equal((HttpStatusCode)429, limited.StatusCode);
            Assert.Equal("request_limit", await MembershipCode(limited));
            Assert.InRange(limited.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0, 1, 900);
        }
        // Only the first refusal of the window is audited; nothing was issued.
        Assert.Equal(before + 1, await MembershipCommandEvents("request_limit"));
        await using (var db = Db())
            Assert.Equal(2, await db.MembershipInvitations.AsNoTracking().CountAsync(i => i.MembershipId == issued.Membership.Membership.Id, Ct));
        // Another administrator (another tenant) keeps its own budget.
        await Invited(foreignAdmin, Invite(null));
    }

    private async Task<int> MembershipCommandEvents(string result)
    {
        await using var audit = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(postgres.GetConnectionString()).Options);
        return await audit.Set<SecurityEvent>().AsNoTracking().CountAsync(e => e.Action == "MembershipCommand" && e.Result == result && e.TenantId == seed.Tenants[0].Id, Ct);
    }
}
