using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S4 WP-D DEV-034 / T-S4-002 (18.01 Backlog row 35, H35 /api/v1/sync/operations).
// Actual host + PostgreSQL + antiforgery + durable crew offline receipts. Execution DEFERRED (Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private static string SyncDigest(OfflineReplayCommand c) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(c, new JsonSerializerOptions(JsonSerializerDefaults.Web))))).ToLowerInvariant();
    private object SyncQuery(params (Guid Id, string Domain, Guid Resource, string Digest)[] ops) => new {
        vesselId = SeedVessel.Id, operations = ops.Select(o => new { operationId = o.Id, domain = o.Domain, resourceId = o.Resource, payloadDigest = o.Digest }).ToArray() };
    private async Task<HttpResponseMessage> Sync(HttpClient client, object body) => await client.PostAsJsonAsync("/api/v1/sync/operations", body, Ct);

    [Fact]
    public async Task Sync_reconcile_recovers_a_lost_replay_response_without_a_second_effect_and_audits_without_values()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client);
        var e = await EnrollOffline(client); var c = Captured(e);
        using var delivered = await Deliver(client, e, c); Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        var original = (await delivered.Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct))!;
        long revision; await using (var before = Db()) revision = (await before.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct)).Revision;
        client.DefaultRequestHeaders.Remove("X-Correlation-ID");
        using var response = await Sync(client, SyncQuery((c.CommandId, SyncOperationReconciliation.CrewWatchGroup, e.AssignmentId, SyncDigest(c))));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(SyncDigest(c), raw, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("SYNTHETIC-A", raw);
        var result = JsonSerializer.Deserialize<SyncOperationsResultDto>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var item = Assert.Single(result.Items);
        Assert.Equal("Recorded", item.State); Assert.Equal(original.Result, item.Result);
        Assert.Equal(original.ServerVersion, item.ServerVersion); Assert.Equal(original.Revision, item.Revision);
        Assert.Equal(original.ReceivedAt, item.ReceivedAt); Assert.Equal(SeedVessel.Id, result.VesselId);
        await using var db = Db();
        Assert.Single(await db.OfflineOutcomes.ToArrayAsync(Ct));
        Assert.Equal(revision, (await db.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct)).Revision);
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        var audit = Assert.Single(await db.Set<SecurityEvent>().Where(x => x.CorrelationId == correlation).ToArrayAsync(Ct));
        Assert.Equal("SyncOperationRead", audit.Action); Assert.Equal("Success", audit.Result);
        Assert.Equal(SeedVessel.Id, audit.ResourceId); Assert.Equal(actorId.ToString(), audit.ActorId);
        // The exact retry of the frozen body still recovers the same immutable outcome.
        using var retry = await Deliver(client, e, c); Assert.Equal(original, await retry.Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct));
    }

    [Fact]
    public async Task Sync_reconcile_distinguishes_pending_payload_mismatch_and_key_reuse_without_disclosing_outcomes()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client);
        var e = await EnrollOffline(client); var c = Captured(e);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, c)).StatusCode);
        var unknown = Guid.NewGuid();
        using var response = await Sync(client, SyncQuery(
            (c.CommandId, SyncOperationReconciliation.CrewWatchGroup, e.AssignmentId, new string('b', 64)),
            (unknown, SyncOperationReconciliation.CrewWatchGroup, e.AssignmentId, new string('c', 64))));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<SyncOperationsResultDto>(Ct))!.Items;
        Assert.Equal(["PayloadMismatch", "NotReceived"], items.Select(x => x.State).ToArray());
        Assert.All(items, x => { Assert.Null(x.Result); Assert.Null(x.ServerVersion); Assert.Null(x.Revision); });
        using var reused = await Sync(client, SyncQuery((c.CommandId, SyncOperationReconciliation.CrewWatchGroup, Guid.NewGuid(), SyncDigest(c))));
        Assert.Equal("KeyReused", Assert.Single((await reused.Content.ReadFromJsonAsync<SyncOperationsResultDto>(Ct))!.Items).State);
        using var domain = await Sync(client, SyncQuery((c.CommandId, SyncOperationReconciliation.DailyNoon, e.AssignmentId, SyncDigest(c))));
        Assert.Contains(Assert.Single((await domain.Content.ReadFromJsonAsync<SyncOperationsResultDto>(Ct))!.Items).State, new[] { "KeyReused", "Unavailable" });
    }

    [Fact]
    public async Task Sync_reconcile_requires_current_rights_vessel_scope_and_tenant_before_receipt_disclosure()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client);
        var e = await EnrollOffline(client); var c = Captured(e);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, c)).StatusCode);
        var query = SyncQuery((c.CommandId, SyncOperationReconciliation.CrewWatchGroup, e.AssignmentId, SyncDigest(c)));
        var settings = host.Services.GetRequiredService<FederationSettings>(); var original = settings.Memberships[0];
        settings.Memberships[0] = original with { Permissions = ["directory.read", "crew.read"] };
        using (var withdrawn = await Sync(client, query)) {
            Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
            var item = Assert.Single((await withdrawn.Content.ReadFromJsonAsync<SyncOperationsResultDto>(Ct))!.Items);
            Assert.Equal("Unavailable", item.State); Assert.Null(item.Result);
        }
        settings.Memberships[0] = original with { TenantId = seed.Tenants.First(t => t.Id != e.TenantId).Id };
        using (var tenant = await Sync(client, query)) {
            Assert.Equal(HttpStatusCode.NotFound, tenant.StatusCode); Assert.DoesNotContain("items", await tenant.Content.ReadAsStringAsync(Ct));
        }
        settings.Memberships[0] = original;
        var other = seed.Vessels.First(v => v.Id != SeedVessel.Id);
        using var foreign = await Sync(client, new { vesselId = other.Id, operations = new[] {
            new { operationId = c.CommandId, domain = SyncOperationReconciliation.CrewWatchGroup, resourceId = e.AssignmentId, payloadDigest = SyncDigest(c) } } });
        if (foreign.StatusCode == HttpStatusCode.OK)
            Assert.NotEqual("Recorded", Assert.Single((await foreign.Content.ReadFromJsonAsync<SyncOperationsResultDto>(Ct))!.Items).State);
        else Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Sync_reconcile_rejects_missing_csrf_and_malformed_or_unbounded_batches_without_business_effects()
    {
        await using var host = OfflineHost(); using var client = Client(host);
        var valid = SyncQuery((Guid.NewGuid(), SyncOperationReconciliation.CrewWatchGroup, Guid.NewGuid(), new string('a', 64)));
        using (var csrf = await Sync(client, valid)) {
            Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
            Assert.Equal("csrf_invalid", (await csrf.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }
        await Token(client);
        var before = await S1BusinessSnapshot();
        var op = new { operationId = Guid.NewGuid(), domain = SyncOperationReconciliation.CrewWatchGroup, resourceId = Guid.NewGuid(), payloadDigest = new string('a', 64) };
        var invalid = new object[] {
            new { vesselId = SeedVessel.Id, operations = Array.Empty<object>() },
            new { vesselId = SeedVessel.Id, operations = new[] { op, op } },
            new { vesselId = SeedVessel.Id, operations = new[] { op with { domain = "Unsupported" } } },
            new { vesselId = SeedVessel.Id, operations = new[] { op with { payloadDigest = "not-a-digest" } } },
            new { vesselId = SeedVessel.Id, operations = new[] { op }, tenantId = Guid.NewGuid() },
            new { vesselId = Guid.Empty, operations = new[] { op } },
            new { vesselId = SeedVessel.Id, operations = Enumerable.Range(0, SyncOperationReconciliation.MaxOperations + 1).Select(_ => op with { operationId = Guid.NewGuid() }).ToArray() },
        };
        foreach (var body in invalid) {
            using var response = await Sync(client, body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }
        using var duplicateJson = await client.PostAsync("/api/v1/sync/operations", new StringContent(
            $"{{\"vesselId\":\"{SeedVessel.Id}\",\"vesselId\":\"{SeedVessel.Id}\",\"operations\":[]}}", Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateJson.StatusCode);
        S1AssertSameBusinessSnapshot(before, await S1BusinessSnapshot());
    }
}
