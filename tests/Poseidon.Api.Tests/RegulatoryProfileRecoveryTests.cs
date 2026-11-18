using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PROFILE-OWNER-RECOVERY-01 / DEV-020/T-S2-001 / 19.01 paragraphs261..268.
// Actual host+owned PostgreSQL+real antiforgery. AUTHORED, execution DEFERRED.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly JsonSerializerOptions ProfileRecoveryJson = new(JsonSerializerDefaults.Web);
    private async Task<HttpResponseMessage> ProfileRecoveryPut(HttpClient client, Guid vessel, string body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/" + ProfilePath(vessel))
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }
    private async Task ProfileRecoverySession(HttpClient client, Guid actor, Guid tenant)
    {
        using var response = await client.GetAsync("/api/v1/auth/session", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>(Ct))!;
        Assert.True(session.Authenticated); Assert.Equal(actor.ToString("D"), session.ActorId); Assert.Equal(tenant, session.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(session.CsrfToken));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
    }
    private async Task<byte[]> ProfileRecoveryState(Guid vessel, string key)
    {
        await using var db = Db();
        var head = await db.RegulatoryDrafts.AsNoTracking().SingleAsync(x => x.VesselId == vessel, Ct);
        var revisions = await db.RegulatoryDraftRevisions.AsNoTracking().Where(x => x.VesselId == vessel).OrderBy(x => x.Revision).ToArrayAsync(Ct);
        var receipts = await db.CommandReceipts.AsNoTracking().Where(x => x.Action == "ProfileSave" && x.ResourceId == vessel).OrderBy(x => x.ActorId).ThenBy(x => x.Key).ToArrayAsync(Ct);
        var receipt = Assert.Single(receipts, x => x.ActorId == actorId && x.Key == key);
        Assert.Equal(seed.Tenants[0].Id, receipt.TenantId); Assert.Equal(vessel, receipt.ProfileVesselId);
        Assert.Equal(head.Revision, revisions.Last().Revision);
        var types = new[] { nameof(RegulatoryDraft), nameof(RegulatoryDraftRevision), nameof(CommandReceipt) };
        var audits = await db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand" && types.Contains(x.EntityType) &&
            x.ResourceId == vessel).OrderBy(x => x.Id).ToArrayAsync(Ct);
        static string? Field(AuditEvent audit, string name) {
            using var fields = JsonDocument.Parse(audit.ChangeSummary!);
            return Assert.Single(fields.RootElement.EnumerateArray(), value => value.GetProperty("field").GetString() == name)
                .GetProperty("after").GetString();
        }
        // Generic CommandReceipt also audits VesselCreate/VesselUpdate on this
        // same resource. Use its real allowlisted endpoint context to distinguish
        // ProfileSave without inventing a key-based audit identity.
        var receiptAudits = audits.Where(value => value.EntityType == nameof(CommandReceipt) &&
            Field(value, "CommandName") == "SaveRegulatoryDraft").ToArray();
        Assert.Equal(receipts.Length, receiptAudits.Length);
        var matchedReceiptAudits = new HashSet<Guid>();
        foreach (var committed in receipts) {
            Assert.Equal("ProfileSave", committed.Action); Assert.Equal(vessel, committed.ProfileVesselId);
            var outcome = JsonSerializer.Deserialize<RegulatoryDraftDto>(committed.ProfileOutcomeJson!, ProfileRecoveryJson)!;
            Assert.Equal(vessel, outcome.Vessel.Id); Assert.Equal(committed.TenantId, outcome.TenantId);
            Assert.Equal(committed.RegisterRevision, outcome.Revision); Assert.Equal(committed.RegisterVersion, outcome.Version);
            var audit = Assert.Single(receiptAudits, value => value.EntityId == committed.ResourceId.ToString("D") &&
                value.TenantId == committed.TenantId && value.ActorId == committed.ActorId.ToString("D") &&
                value.AfterRevision == committed.RegisterRevision && value.AfterVersion == committed.RegisterVersion);
            Assert.True(matchedReceiptAudits.Add(audit.Id));
            Assert.Equal("Added", audit.Action);
            var revisionAudit = Assert.Single(audits, value => value.EntityType == nameof(RegulatoryDraftRevision) &&
                value.AfterRevision == outcome.Revision && value.CorrelationId == audit.CorrelationId);
            var headAudit = Assert.Single(audits, value => value.EntityType == nameof(RegulatoryDraft) &&
                value.AfterRevision == outcome.Revision && value.CorrelationId == audit.CorrelationId);
            Assert.Equal("Added", revisionAudit.Action); Assert.Equal(outcome.Revision == 1 ? "Added" : "Modified", headAudit.Action);
            foreach (var item in new[] { audit, revisionAudit, headAudit }) {
                Assert.Equal(committed.TenantId, item.TenantId); Assert.Equal(committed.ActorId.ToString("D"), item.ActorId);
                Assert.Equal("User", item.ActorType); Assert.Equal("WebCommand", item.Source);
                Assert.Equal(vessel, item.ResourceId); Assert.Equal(vessel, item.VesselId);
                Assert.False(string.IsNullOrWhiteSpace(item.CorrelationId)); Assert.Equal(audit.CorrelationId, item.CorrelationId);
                Assert.False(string.IsNullOrWhiteSpace(item.RoleContext)); Assert.Equal(audit.RoleContext, item.RoleContext);
                Assert.False(string.IsNullOrWhiteSpace(item.CapabilityContext)); Assert.Equal(audit.CapabilityContext, item.CapabilityContext);
                Assert.Equal("RecordedControlledCode", item.ReasonStatus);
                Assert.Equal("SaveRegulatoryDraft", Field(item, "CommandName"));
                Assert.Equal("Vessel", Field(item, "ScopeKind")); Assert.Equal("PREPARATION_COMMAND", Field(item, "TechnicalReasonCode"));
                Assert.False(item.EntityId.Contains(committed.Key, StringComparison.Ordinal));
                Assert.False((item.ChangeSummary ?? "").Contains(committed.Key, StringComparison.Ordinal));
            }
        }
        Assert.Equal(receiptAudits.Length, matchedReceiptAudits.Count);
        // Closed in-memory comparison; never emit receipt/content/private values.
        return JsonSerializer.SerializeToUtf8Bytes(new { head, revisions, receipts, audits }, ProfileRecoveryJson);
    }
    private async Task ProfileRecoveryProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        if (bytes.Length == 0) return; // Authorization middleware can return an empty403.
        var value = JsonSerializer.Deserialize<JsonElement>(bytes);
        foreach (var field in new[] { "content", "vessel", "revision", "savedAt", "provenance", "profileOutcomeJson", "items" })
            Assert.False(value.TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData(false, "read")]
    [InlineData(false, "write")]
    [InlineData(false, "scope")]
    [InlineData(true, "read")]
    [InlineData(true, "write")]
    [InlineData(true, "scope")]
    [Trait("Recovery", "RegulatoryProfile")]
    public async Task Profile_commit_then_live_denial_then_original_owner_fresh_csrf_exact_replay_has_one_revision_and_business_effect(bool absent, string denial)
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var vessel = SeedVessel.Id;
        if (absent) {
            using var created = await Send(client, "vessels", new { code = "FICTIONAL-PROFILE-RECOVERY", name = "FICTIONAL disposable recovery vessel", kind = "Ship", companyId = SeedVessel.CompanyId });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode); vessel = (await created.Content.ReadFromJsonAsync<VesselDto>(Ct))!.Id;
        }
        var before = await Profile(client, vessel);
        var command = new SaveRegulatoryDraft(before.Version, new RegulatoryDraftContent { Flag = new("Unknown"), Gt = new("Known", "9007199254740993"), Nt = new("Known", "0.1234567890123456789012345678") });
        var body = JsonSerializer.Serialize(command, ProfileRecoveryJson); var key = Guid.NewGuid().ToString("D");
        var settings = host.Services.GetRequiredService<FederationSettings>(); var owner = settings.Memberships[0];
        await ProfileRecoverySession(client, actorId, owner.TenantId);
        using var committed = await ProfileRecoveryPut(client, vessel, body, key); Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var original = await committed.Content.ReadAsByteArrayAsync(Ct);
        var saved = JsonSerializer.Deserialize<RegulatoryDraftDto>(original, ProfileRecoveryJson)!;
        Assert.Equal(before.Revision+1, saved.Revision); Assert.Equal("Unknown", saved.Content.Flag.State); Assert.Equal("Missing", saved.Provenance);
        var snapshot = await ProfileRecoveryState(vessel, key);
        try {
            settings.Memberships[0] = denial == "scope" ? owner with { TenantWideRead = false, TenantWideManage = false, VesselGrants = [new(Guid.NewGuid(), true)] } :
                owner with { Permissions = owner.Permissions.Where(permission => permission != (denial == "read" ? "directory.read" : "directory.write")).ToArray() };
            await ProfileRecoverySession(client, actorId, owner.TenantId);
            using var denied = await ProfileRecoveryPut(client, vessel, body, key);
            await ProfileRecoveryProblem(denied, denial == "scope" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden);
            if (denial != "write") {
                using var read = await client.GetAsync("/api/v1/"+ProfilePath(vessel), Ct);
                await ProfileRecoveryProblem(read, denial == "scope" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden);
                using var history = await client.GetAsync("/api/v1/"+ProfilePath(vessel)+"/history", Ct);
                await ProfileRecoveryProblem(history, denial == "scope" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden);
            }
        } finally { settings.Memberships[0] = owner; }
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var noCsrf = await ProfileRecoveryPut(client, vessel, body, key); await ProfileRecoveryProblem(noCsrf, HttpStatusCode.Forbidden);
        await ProfileRecoverySession(client, actorId, owner.TenantId);
        using var replay = await ProfileRecoveryPut(client, vessel, body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        var replayBytes = await replay.Content.ReadAsByteArrayAsync(Ct); var after = await ProfileRecoveryState(vessel, key);
        Assert.True(original.SequenceEqual(replayBytes)); Assert.True(snapshot.SequenceEqual(after));
    }

    [Fact]
    [Trait("Recovery", "RegulatoryProfile")]
    public async Task Profile_original_historical_receipt_after_newer_edit_keeps_original_bytes_and_latest_head_unchanged()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client); var command = new SaveRegulatoryDraft(before.Version, before.Content with { Flag = new("Unknown") });
        var body = JsonSerializer.Serialize(command, ProfileRecoveryJson); var key = Guid.NewGuid().ToString("D");
        using var committed = await ProfileRecoveryPut(client, SeedVessel.Id, body, key); Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var original = await committed.Content.ReadAsByteArrayAsync(Ct); var saved = JsonSerializer.Deserialize<RegulatoryDraftDto>(original, ProfileRecoveryJson)!;
        var later = await SaveProfile(client, saved, saved.Content with { Flag = new("Known", "FICTIONAL later declaration") });
        var snapshot = await ProfileRecoveryState(SeedVessel.Id, key);
        await ProfileRecoverySession(client, actorId, before.TenantId);
        using var replay = await ProfileRecoveryPut(client, SeedVessel.Id, body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBytes = await replay.Content.ReadAsByteArrayAsync(Ct); var after = await ProfileRecoveryState(SeedVessel.Id, key);
        Assert.True(original.SequenceEqual(replayBytes)); Assert.True(snapshot.SequenceEqual(after));
        var current = await Profile(client); Assert.Equal(later.Revision, current.Revision); Assert.Equal(later.Version, current.Version); Assert.Equal(later.Content, current.Content);
        var history = (await client.GetFromJsonAsync<PageDto<RegulatoryRevisionDto>>("/api/v1/"+ProfilePath(SeedVessel.Id)+"/history", Ct))!;
        Assert.Equal(later.Content, history.Items[0].Content); Assert.Equal(saved.Content, history.Items[1].Content);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("vessel")]
    [Trait("Recovery", "RegulatoryProfile")]
    public async Task Profile_receipt_cannot_leak_to_unentitled_actor_tenant_or_different_route(string change)
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client); var body = JsonSerializer.Serialize(new SaveRegulatoryDraft(before.Version, new()), ProfileRecoveryJson); var key = Guid.NewGuid().ToString("D");
        using var committed = await ProfileRecoveryPut(client, SeedVessel.Id, body, key); Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var original = await committed.Content.ReadAsByteArrayAsync(Ct); var snapshot = await ProfileRecoveryState(SeedVessel.Id, key);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var owner = settings.Memberships[0];
        try {
            var stranger = change == "actor" ? owner with { ActorId = Guid.NewGuid(), Permissions = owner.Permissions.Where(permission => permission != "directory.write").ToArray() } : change == "tenant" ? owner with { TenantId = seed.Tenants[1].Id } : owner;
            settings.Memberships[0] = stranger; await ProfileRecoverySession(client, stranger.ActorId, stranger.TenantId);
            using var denied = await ProfileRecoveryPut(client, change == "vessel" ? Guid.NewGuid() : SeedVessel.Id, body, key);
            await ProfileRecoveryProblem(denied, change == "actor" ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound);
            if (change is "actor" or "tenant") { await using var db = Db(stranger.TenantId); Assert.False(await db.CommandReceipts.AnyAsync(x => x.ActorId == stranger.ActorId && x.Key == key, Ct)); }
        } finally { settings.Memberships[0] = owner; }
        await ProfileRecoverySession(client, actorId, owner.TenantId);
        using var replay = await ProfileRecoveryPut(client, SeedVessel.Id, body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBytes = await replay.Content.ReadAsByteArrayAsync(Ct); var after = await ProfileRecoveryState(SeedVessel.Id, key);
        Assert.True(original.SequenceEqual(replayBytes)); Assert.True(snapshot.SequenceEqual(after));
    }
}
