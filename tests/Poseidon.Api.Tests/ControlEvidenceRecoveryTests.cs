using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// CEO06: actual host/antiforgery/owned PostgreSQL. Fictional nullable drafts,
// no production policy, human verification or approval is installed.
// AUTHORED ONLY: all execution DEFERRED until the next frozen larger package.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly JsonSerializerOptions ControlRecoveryJson = new(JsonSerializerDefaults.Web);
    private async Task<HttpResponseMessage> ControlRecoveryPost(HttpClient client, string route, string body, string key, bool update)
    {
        using var request = new HttpRequestMessage(update ? HttpMethod.Put : HttpMethod.Post, "/api/v1/" + route)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request, Ct);
    }
    private async Task ControlRecoverySession(HttpClient client, Guid actor, Guid tenant)
    {
        using var response = await client.GetAsync("/api/v1/auth/session", Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = (await response.Content.ReadFromJsonAsync<SessionDto>(Ct))!;
        Assert.True(current.Authenticated); Assert.Equal(actor.ToString("D"), current.ActorId); Assert.Equal(tenant, current.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(current.CsrfToken));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", current.CsrfToken);
    }
    private async Task ControlRecoveryDenied(HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        Assert.Equal(expected, response.StatusCode); var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        foreach (var field in new[] { "content", "requirements", "links", "file", "hash", "revision", "outcomeJson", "items" })
            Assert.False(problem.TryGetProperty(field, out _));
    }
    private async Task<byte[]> ControlRecoveryState(ControlEvidenceDto saved, string key)
    {
        await using var db = Db();
        var head = await db.ControlEvidenceHeads.AsNoTracking().SingleAsync(x => x.Kind == saved.Kind && x.Id == saved.Id, Ct);
        var revisions = await db.ControlEvidenceRevisions.AsNoTracking().Where(x => x.Kind == saved.Kind && x.Id == saved.Id).OrderBy(x => x.Revision).ToArrayAsync(Ct);
        var receipts = await db.ControlEvidenceReceipts.AsNoTracking().Where(x => x.ResourceId == saved.Id).OrderBy(x => x.Key).ToArrayAsync(Ct);
        var receipt = Assert.Single(receipts, x => x.Key == key); Assert.Equal(actorId, receipt.ActorId); Assert.Equal(SeedVessel.TenantId, receipt.TenantId);
        var pins = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.Kind == saved.Kind && x.Id == saved.Id).OrderBy(x => x.Revision).ThenBy(x => x.RequirementId).ToArrayAsync(Ct);
        var types = new[] { nameof(ControlEvidenceHead), nameof(ControlEvidenceRevision), nameof(ControlEvidenceRequirement), nameof(ControlEvidenceEdge), nameof(ControlEvidenceReceipt) };
        var audits = await db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand" && types.Contains(x.EntityType) &&
            (x.ResourceId == saved.Id || x.EntityId.Contains(key))).OrderBy(x => x.Id).ToArrayAsync(Ct);
        var receiptAudit = Assert.Single(audits, x => x.EntityType == nameof(ControlEvidenceReceipt) && x.EntityId.Contains(key));
        Assert.Equal("Added", receiptAudit.Action); Assert.Equal(actorId.ToString("D"), receiptAudit.ActorId);
        Assert.True(head.Revision >= saved.Revision); Assert.Equal(head.Revision, revisions.Last().Revision);
        // Compare in memory: never print private serialized body/receipt/audit text.
        return JsonSerializer.SerializeToUtf8Bytes(new { head, revisions, receipts, pins, audits }, ControlRecoveryJson);
    }

    [Theory]
    [InlineData("controls", false, "read")]
    [InlineData("controls", false, "write")]
    [InlineData("controls", false, "owner")]
    [InlineData("controls", true, "read")]
    [InlineData("controls", true, "write")]
    [InlineData("controls", true, "owner")]
    [InlineData("evidence", false, "read")]
    [InlineData("evidence", false, "write")]
    [InlineData("evidence", false, "owner")]
    [InlineData("evidence", true, "read")]
    [InlineData("evidence", true, "write")]
    [InlineData("evidence", true, "owner")]
    [Trait("Recovery", "ControlEvidence")]
    public async Task Control_or_evidence_new_and_update_commit_then_live_denial_restore_fresh_csrf_exact_replay_has_one_original_effect(string family, bool update, string denial)
    {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var owner = settings.Memberships[0];
        var role = Guid.Parse(owner.RoleReferences!.Single());
        var content = new ControlEvidenceContent(Title: "FICTIONAL owning recovery declaration", OwnerId: owner.ActorId, OwnerRole: role);
        var pin = new RequirementPin(RegulatoryRegisterFixtures.RequirementId(owner.TenantId), 1, RegulatoryRegisterFixtures.SourceId(owner.TenantId), 1);
        var prior = update ? await GraphSave(client, family, GraphCommand(content: content)) : null;
        var command = GraphCommand(prior?.Version ?? 0, content) with { Requirements = [pin] };
        var route = GraphRoute(prior == null ? family : $"{family}/{prior.Id}"); var body = JsonSerializer.Serialize(command, ControlRecoveryJson); var key = Guid.NewGuid().ToString("D");
        await ControlRecoverySession(client, actorId, owner.TenantId);
        using var first = await ControlRecoveryPost(client, route, body, key, update); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadAsByteArrayAsync(Ct); var saved = JsonSerializer.Deserialize<ControlEvidenceDto>(original, ControlRecoveryJson)!;
        Assert.Equal(update ? 2 : 1, saved.Revision); Assert.Equal("DRAFT", saved.Lifecycle); Assert.Equal("UNVERIFIED", saved.Verification); Assert.Equal("UNKNOWN", saved.Effectiveness);
        var snapshot = await ControlRecoveryState(saved, key);
        try {
            var removed = denial == "read" ? "file.read" : denial == "owner" ? "control.owner.eligible" : family == "controls" ? "control.write" : "evidence.write";
            settings.Memberships[0] = owner with { Permissions = owner.Permissions.Where(value => value != removed).ToArray() };
            await ControlRecoverySession(client, actorId, owner.TenantId);
            using var denied = await ControlRecoveryPost(client, route, body, key, update);
            await ControlRecoveryDenied(denied, denial == "owner" ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden,
                denial == "owner" ? "owner_unavailable" : "permission_denied");
        } finally { settings.Memberships[0] = owner; }
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var csrf = await ControlRecoveryPost(client, route, body, key, update); await ControlRecoveryDenied(csrf, HttpStatusCode.Forbidden, "csrf_invalid");
        await ControlRecoverySession(client, actorId, owner.TenantId);
        using var replay = await ControlRecoveryPost(client, route, body, key, update); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBytes = await replay.Content.ReadAsByteArrayAsync(Ct); var replayState = await ControlRecoveryState(saved, key);
        Assert.True(original.SequenceEqual(replayBytes)); Assert.True(snapshot.SequenceEqual(replayState));
    }

    [Theory]
    [InlineData("controls")]
    [InlineData("evidence")]
    [Trait("Recovery", "ControlEvidence")]
    public async Task Control_historical_exact_receipt_after_later_edit_keeps_original_outcome_and_current_versions_head(string family)
    {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var first = await GraphSave(client, family, GraphCommand());
        var command = GraphCommand(first.Version, new(Title: "FICTIONAL original captured update")); var body = JsonSerializer.Serialize(command, ControlRecoveryJson); var key = Guid.NewGuid().ToString("D");
        var route = GraphRoute($"{family}/{first.Id}");
        using var committed = await ControlRecoveryPost(client, route, body, key, true); Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var original = await committed.Content.ReadAsByteArrayAsync(Ct); var saved = JsonSerializer.Deserialize<ControlEvidenceDto>(original, ControlRecoveryJson)!;
        var later = await GraphSave(client, $"{family}/{first.Id}", GraphCommand(saved.Version, new(Title: "FICTIONAL later current update")), update: true);
        var state = await ControlRecoveryState(saved, key);
        var owner = host.Services.GetRequiredService<FederationSettings>().Memberships[0]; await ControlRecoverySession(client, actorId, owner.TenantId);
        using var replay = await ControlRecoveryPost(client, route, body, key, true); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBytes = await replay.Content.ReadAsByteArrayAsync(Ct); var after = await ControlRecoveryState(saved, key);
        Assert.True(original.SequenceEqual(replayBytes)); Assert.True(state.SequenceEqual(after));
        using var versions = await client.GetAsync("/api/v1/" + GraphRoute($"{family}/{first.Id}/versions"), Ct); Assert.Equal(HttpStatusCode.OK, versions.StatusCode);
        var page = await versions.Content.ReadFromJsonAsync<JsonElement>(Ct); var current = page.GetProperty("items")[0];
        Assert.Equal(first.Id, current.GetProperty("id").GetGuid()); Assert.Equal(later.Revision, current.GetProperty("revision").GetInt64());
        Assert.True(later.Revision > saved.Revision); Assert.NotEqual(saved.Version, later.Version);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("asset")]
    [Trait("Recovery", "ControlEvidence")]
    public async Task Control_original_receipt_is_not_disclosed_to_foreign_unentitled_actor_tenant_or_asset(string change)
    {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var command = GraphCommand(); var body = JsonSerializer.Serialize(command, ControlRecoveryJson); var key = Guid.NewGuid().ToString("D"); var route = GraphRoute("controls");
        using var committed = await ControlRecoveryPost(client, route, body, key, false); Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var original = await committed.Content.ReadAsByteArrayAsync(Ct); var saved = JsonSerializer.Deserialize<ControlEvidenceDto>(original, ControlRecoveryJson)!; var state = await ControlRecoveryState(saved, key);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var owner = settings.Memberships[0];
        try {
            // Actor receipts are not globally shared. This replacement actor is
            // deliberately unentitled to author; no cross-actor deduplication is invented.
            var stranger = change == "actor" ? owner with { ActorId = Guid.NewGuid(), Permissions = owner.Permissions.Where(value => value != "control.write").ToArray() } : change == "tenant" ? owner with { TenantId = seed.Tenants[1].Id } : owner;
            settings.Memberships[0] = stranger; await ControlRecoverySession(client, stranger.ActorId, stranger.TenantId);
            using var denied = await ControlRecoveryPost(client, change == "asset" ? GraphRoute("controls", Guid.NewGuid()) : route, body, key, false);
            await ControlRecoveryDenied(denied, change == "actor" ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound, change == "actor" ? "permission_denied" : "resource_not_found");
            if (change is "actor" or "tenant") { await using var foreign = Db(stranger.TenantId); Assert.False(await foreign.ControlEvidenceReceipts.AnyAsync(x => x.ActorId == stranger.ActorId && x.Key == key, Ct)); }
        } finally { settings.Memberships[0] = owner; }
        await ControlRecoverySession(client, actorId, owner.TenantId);
        using var replay = await ControlRecoveryPost(client, route, body, key, false); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBytes = await replay.Content.ReadAsByteArrayAsync(Ct); var after = await ControlRecoveryState(saved, key);
        Assert.True(original.SequenceEqual(replayBytes)); Assert.True(state.SequenceEqual(after));
    }
}
