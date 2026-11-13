using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// HUMAN-REVIEW-OWNER-RECOVERY-01 HRO05. Actual host, real antiforgery and owned
// PostgreSQL. Test-only fictional appointments; no production grant or approval.
// AUTHORED / execution DEFERRED until the next larger frozen bundle gate.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly JsonSerializerOptions HumanRecoveryJson = new(JsonSerializerDefaults.Web);
    private async Task<HttpResponseMessage> HumanRecoveryPost(HttpClient client, string route, string body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + route)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }
    private async Task HumanRecoverySession(HttpClient client, Guid actor, Guid tenant)
    {
        using var response = await client.GetAsync("/api/v1/auth/session", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = (await response.Content.ReadFromJsonAsync<SessionDto>(Ct))!;
        Assert.True(current.Authenticated); Assert.Equal(actor.ToString("D"), current.ActorId); Assert.Equal(tenant, current.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(current.CsrfToken));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", current.CsrfToken);
    }
    private async Task HumanRecoveryDenied(HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        Assert.Equal(expected, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        foreach (var field in new[] { "rationale", "recording", "actorId", "authorityHash", "contextHash", "items", "outcomeJson" })
            Assert.False(problem.TryGetProperty(field, out _));
    }
    private async Task<byte[]> HumanRecoveryState(HumanReviewDto saved, string key)
    {
        await using var db = Db();
        var head = await db.HumanReviewHeads.AsNoTracking().SingleAsync(x => x.Id == saved.Id, Ct);
        var entry = Assert.Single(await db.HumanReviewEvents.AsNoTracking().Where(x => x.Id == saved.Id).ToArrayAsync(Ct));
        var receipt = Assert.Single(await db.HumanReviewReceipts.AsNoTracking().Where(x => x.ResourceId == saved.Id).ToArrayAsync(Ct));
        Assert.Equal(saved.Revision, head.Revision); Assert.Equal(saved.Version, head.Version);
        Assert.Equal(saved.State, entry.State); Assert.Equal(saved.ContextHash, entry.ContextHash);
        Assert.Equal(actorId, entry.ActorId); Assert.Equal(actorId, receipt.ActorId); Assert.Equal(key, receipt.Key);
        Assert.Equal(SeedVessel.TenantId, receipt.TenantId);
        var id = saved.Id.ToString("D");
        var types = new[] { nameof(HumanReviewHead), nameof(HumanReviewEvent), nameof(HumanReviewReceipt) };
        var audits = await db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand" && types.Contains(x.EntityType) &&
            (x.EntityId.Contains(id) || x.EntityId.Contains(key))).OrderBy(x => x.Id).ToArrayAsync(Ct);
        foreach (var type in types) {
            var audit = Assert.Single(audits, x => x.EntityType == type);
            Assert.Equal("Added", audit.Action); Assert.Equal(actorId.ToString("D"), audit.ActorId);
            Assert.Equal(SeedVessel.TenantId, audit.TenantId);
            Assert.False((audit.ChangeSummary ?? "").Contains(entry.Rationale, StringComparison.Ordinal));
        }
        // Snapshot comparison stays in memory; failure prints no private text/body.
        var counts = new { Heads = await db.HumanReviewHeads.CountAsync(Ct), Events = await db.HumanReviewEvents.CountAsync(Ct), Receipts = await db.HumanReviewReceipts.CountAsync(Ct) };
        return JsonSerializer.SerializeToUtf8Bytes(new { head, entry, receipt, audits, counts }, HumanRecoveryJson);
    }

    [Theory]
    [InlineData(false, "read")]
    [InlineData(false, "grant")]
    [InlineData(false, "expired")]
    [InlineData(false, "capability")]
    [InlineData(true, "read")]
    [InlineData(true, "grant")]
    [InlineData(true, "expired")]
    [InlineData(true, "capability")]
    [Trait("Recovery", "HumanReview")]
    public async Task Human_committed_pending_or_recorded_command_denies_revoked_retry_then_same_owner_fresh_csrf_replays_exactly_once(bool recording, string denial)
    {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var (ev, co) = await ReviewInputs(client);
        if (recording) RecordingAppointment(host.Services, ev.Id, co.Id); else ProvisionSyntheticHuman(host.Services, ev.Id, co.Id);
        var command = recording ? await RecordingCommand(client, host.Services, ev, co, await PendingReview(client, host.Services, ev, co)) : ReviewCommand(host.Services, ev, co);
        var route = GraphRoute(recording ? "decisions" : "verifications");
        var body = JsonSerializer.Serialize(command, HumanRecoveryJson); var key = Guid.NewGuid().ToString("D");
        var settings = host.Services.GetRequiredService<FederationSettings>(); var owner = settings.Memberships[0];
        await HumanRecoverySession(client, actorId, owner.TenantId);
        using var first = await HumanRecoveryPost(client, route, body, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadAsByteArrayAsync(Ct); var saved = JsonSerializer.Deserialize<HumanReviewDto>(original, HumanRecoveryJson)!;
        var snapshot = await HumanRecoveryState(saved, key);
        try {
            settings.Memberships[0] = denial switch {
                "read" => owner with { Permissions = owner.Permissions.Where(x => x != "human.review.read").ToArray() },
                "grant" => owner with { HumanAuthorities = [] },
                "expired" => owner with { HumanAuthorities = owner.HumanAuthorities.Select(g => g.Id == command.AuthorityId ? g with { ValidUntil = DateTimeOffset.UtcNow.AddSeconds(-1) } : g).ToArray() },
                "capability" => owner with { Permissions = owner.Permissions.Where(x => x != (recording ? "human.decide" : "human.review.submit")).ToArray() },
                _ => throw new InvalidOperationException("Unknown test denial")
            };
            await HumanRecoverySession(client, actorId, owner.TenantId);
            using var denied = await HumanRecoveryPost(client, route, body, key);
            await HumanRecoveryDenied(denied, HttpStatusCode.Forbidden, denial == "read" ? "permission_denied" : "human_authority_required");
        } finally { settings.Memberships[0] = owner; }
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var csrf = await HumanRecoveryPost(client, route, body, key);
        await HumanRecoveryDenied(csrf, HttpStatusCode.Forbidden, "csrf_invalid");
        await HumanRecoverySession(client, actorId, owner.TenantId);
        using var recovered = await HumanRecoveryPost(client, route, body, key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.True(original.SequenceEqual(await recovered.Content.ReadAsByteArrayAsync(Ct)));
        Assert.True(snapshot.SequenceEqual(await HumanRecoveryState(saved, key)));
        Assert.Equal(recording ? "RECORDED" : "PENDING", saved.State);
        if (recording) Assert.Equal("UNASSESSED", saved.Recording!.Qualification);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("asset")]
    [Trait("Recovery", "HumanReview")]
    public async Task Human_committed_receipt_is_not_exposed_by_foreign_owner_tenant_or_asset_and_original_attempt_stays_recoverable(string change)
    {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var (ev, co) = await ReviewInputs(client); ProvisionSyntheticHuman(host.Services, ev.Id, co.Id);
        var command = ReviewCommand(host.Services, ev, co); var body = JsonSerializer.Serialize(command, HumanRecoveryJson); var key = Guid.NewGuid().ToString("D");
        var route = GraphRoute("verifications"); var settings = host.Services.GetRequiredService<FederationSettings>(); var owner = settings.Memberships[0];
        using var first = await HumanRecoveryPost(client, route, body, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadAsByteArrayAsync(Ct); var saved = JsonSerializer.Deserialize<HumanReviewDto>(original, HumanRecoveryJson)!;
        var snapshot = await HumanRecoveryState(saved, key);
        try {
            // Actor receipts are scoped. A differently authorized actor is not
            // promised global idempotence; this foreign actor has no appointment.
            var stranger = change == "actor" ? owner with { ActorId = Guid.NewGuid(), HumanAuthorities = [] } :
                change == "tenant" ? owner with { TenantId = seed.Tenants[1].Id } : owner;
            settings.Memberships[0] = stranger;
            await HumanRecoverySession(client, stranger.ActorId, stranger.TenantId);
            var target = change == "asset" ? route.Replace(SeedVessel.Id.ToString(), Guid.NewGuid().ToString(), StringComparison.Ordinal) : route;
            using var denied = await HumanRecoveryPost(client, target, body, key);
            await HumanRecoveryDenied(denied, change == "actor" ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound,
                change == "actor" ? "human_authority_required" : "resource_not_found");
            if (change is "actor" or "tenant") {
                await using var foreign = Db(stranger.TenantId);
                Assert.False(await foreign.HumanReviewReceipts.AnyAsync(x => x.ActorId == stranger.ActorId && x.Key == key, Ct));
            }
        } finally { settings.Memberships[0] = owner; }
        await HumanRecoverySession(client, actorId, owner.TenantId);
        using var recovered = await HumanRecoveryPost(client, route, body, key); Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.True(original.SequenceEqual(await recovered.Content.ReadAsByteArrayAsync(Ct)));
        Assert.True(snapshot.SequenceEqual(await HumanRecoveryState(saved, key)));
    }
}
