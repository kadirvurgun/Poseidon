using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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

// OFFLINE-APPLICATION-01 TC-OA-API01..16: actual host/PG/antiforgery/audited transactions.
// Explicit fictional field/device policy, not deployment configuration. Execution DEFERRED.
public sealed partial class DirectoryWorkspaceTests
{
    private readonly Dictionary<Guid, ECDsa> offlineTestKeys = [];
    private string OfflinePath => $"vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}/offline";
    private OfflineApplicationPolicy SyntheticOfflinePolicy() => new() {
        Version = "synthetic-offline-v1", SourceVersion = OfflineWatchCapture.Source,
        Purpose = "Fictional authoring test only", FieldClassification = "NonPersonalControlledDraftDeclaration",
        DevicePolicyReference = "synthetic-device", KeyPolicyReference = "synthetic-browser-key",
        StoragePolicyReference = "synthetic-encrypted-idb", RetentionPolicyReference = "synthetic-disposable-test",
        Operation = OfflineWatchCapture.Operation, Fields = ["watch_group"], WatchGroups = ["SYNTHETIC-A", "SYNTHETIC-B"],
        Scopes = [new(seed.Tenants[0].Id, actorId, SeedVessel.Id)], NotBefore = DateTimeOffset.UtcNow.AddDays(-1), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
        LeaseSeconds = 3600, MaxRecords = 8, MaxEncryptedBytes = 32768, MaxDeviceRecords = 16, MaxDeviceEncryptedBytes = 65536, MaxEnrollments = 8
    };
    private WebApplicationFactory<Program> OfflineHost(DbCommandInterceptor? interceptor = null) => Host(crewRead: true, crewWrite: true, interceptor: interceptor)
        .WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.RemoveAll<OfflineApplicationPolicy>(); s.AddSingleton(SyntheticOfflinePolicy()); }));
    private async Task<OfflineContextDto> EnrollOffline(HttpClient client) {
        using var response = await Send(client, OfflinePath, new { }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var e = (await response.Content.ReadFromJsonAsync<OfflineContextDto>(Ct))!;
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); offlineTestKeys.Add(e.Id, key);
        var proof = Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes($"{e.Id:D}/{e.KeyId:D}/{e.KeyChallenge}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        using var enrolled = await Send(client, $"{OfflinePath}/{e.Id}/key", new { publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), proof });
        Assert.Equal(HttpStatusCode.OK, enrolled.StatusCode); return e;
    }
    private static OfflineReplayCommand Captured(OfflineContextDto e, string? watch = "SYNTHETIC-A") => new(Guid.NewGuid(), e.KeyId, e.DeviceId,
        e.PolicyVersion, e.SourceVersion, OfflineWatchCapture.Operation, e.BaseVersion, DateTimeOffset.UtcNow, 180, watch);
    private async Task<HttpResponseMessage> Deliver(HttpClient client, OfflineContextDto e, OfflineReplayCommand command, string? bodyOverride = null, bool badHash = false) {
        var body = bodyOverride ?? JsonSerializer.Serialize(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/{OfflinePath}/{e.Id}/replay") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        message.Headers.Add("X-Offline-Hash", badHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))));
        message.Headers.Add("X-Offline-Signature", Convert.ToBase64String(offlineTestKeys[e.Id].SignData(
            Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)))), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        return await client.SendAsync(message, Ct);
    }
    [Fact]
    public async Task Offline_real_command_and_both_receipts_audit_commit_once_and_recover_exact_original_outcome()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client);
        var e = await EnrollOffline(client); var c = Captured(e);
        DateTimeOffset? originalSource;
        await using (var before = Db()) originalSource = (await before.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct)).SourceObservedAt;
        using var response = await Deliver(client, e, c); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = (await response.Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct))!; Assert.Equal("Committed", outcome.Result);
        using var retry = await Deliver(client, e, c); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(outcome, await retry.Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct));
        await using var db = Db(); var assignment = await db.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct);
        Assert.Equal("SYNTHETIC-A", assignment.WatchGroup); Assert.Equal(e.BaseRevision + 1, assignment.Revision);
        Assert.Equal(originalSource, assignment.SourceObservedAt); Assert.Equal("Draft", assignment.Status);
        Assert.Single(await db.OfflineOutcomes.ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(r => r.Key == c.CommandId.ToString()).ToArrayAsync(Ct));
        var events = await db.AuditEvents.Where(a => a.EntityType == nameof(OfflineOutcome)).ToArrayAsync(Ct);
        Assert.Single(events); Assert.Equal(actorId.ToString(), events[0].ActorId); Assert.Equal(e.VesselId, events[0].VesselId);
        Assert.DoesNotContain("SYNTHETIC-A", events[0].ChangeSummary); Assert.DoesNotContain(c.CommandId.ToString(), events[0].ChangeSummary);
    }
    [Fact]
    public async Task Changed_body_key_device_source_and_command_reuse_never_recover_or_overwrite()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client); var c = Captured(e);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, c)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Deliver(client, e, c with { WatchGroup = "SYNTHETIC-B" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Deliver(client, e, c with { KeyId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Deliver(client, e, c with { DeviceId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Deliver(client, e, c with { SourceVersion = new string('0', 64) })).StatusCode);
        await using var db = Db(); Assert.Single(await db.OfflineOutcomes.ToArrayAsync(Ct));
        Assert.Equal("SYNTHETIC-A", (await db.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct)).WatchGroup);
    }
    [Fact]
    public async Task Material_conflict_exposes_authorized_base_local_current_and_requires_new_reasoned_resolution_lineage()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client);
        var old = await EnrollOffline(client); var other = await EnrollOffline(client);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, other, Captured(other, "SYNTHETIC-B"))).StatusCode);
        var local = Captured(old); var conflictResponse = await Deliver(client, old, local);
        var conflict = (await conflictResponse.Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct))!; Assert.Equal("MaterialConflict", conflict.Result);
        using var detail = await client.GetAsync($"/api/v1/{OfflinePath}/{old.Id}/conflicts/{local.CommandId}", Ct);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(Ct)); var root = json.RootElement;
        Assert.Equal("unknown", root.GetProperty("base").GetProperty("kind").GetString());
        Assert.Equal("SYNTHETIC-A", root.GetProperty("fields")[0].GetProperty("captured").GetProperty("text").GetString());
        Assert.Equal("SYNTHETIC-B", root.GetProperty("fields")[0].GetProperty("server").GetProperty("text").GetString());
        var resolution = Captured(old, null) with { ParentCommandId = local.CommandId, BaseVersion = conflict.ServerVersion,
            ExpectedServerVersion = conflict.ServerVersion, Choice = "useCaptured", Reason = "Explicit fictional draft reconciliation" };
        Assert.Equal(HttpStatusCode.BadRequest, (await Deliver(client, old, resolution with { Reason = " " })).StatusCode);
        var result = await Deliver(client, old, resolution); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var resolved = (await result.Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct))!; Assert.Equal("Committed", resolved.Result); Assert.Equal(local.CommandId, resolved.ParentCommandId);
        Assert.Equal(resolved, await (await Deliver(client, old, resolution)).Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await Deliver(client, old, resolution with { CommandId = Guid.NewGuid() })).StatusCode);
        await using var db = Db(); Assert.Equal(3, await db.OfflineOutcomes.CountAsync(Ct));
        Assert.Equal("MaterialConflict", (await db.OfflineOutcomes.SingleAsync(o => o.CommandId == local.CommandId, Ct)).Result);
        Assert.Equal("SYNTHETIC-A", (await db.CrewAssignments.SingleAsync(x => x.Id == old.AssignmentId, Ct)).WatchGroup);
        Assert.All(await db.OfflineOutcomes.ToArrayAsync(Ct), o => Assert.DoesNotContain("SYNTHETIC-", o.ProtectedContext));
    }
    [Fact]
    public async Task Current_permission_revocation_precedes_receipt_conflict_history_inventory_and_cleanup()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client); var c = Captured(e);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, c)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read", "crew.read"] };
        Assert.Equal(HttpStatusCode.NotFound, (await Deliver(client, e, c)).StatusCode);
        foreach (var suffix in new[] { "", $"/{e.Id}/history", $"/{e.Id}/conflicts/{c.CommandId}" }) {
            var response = await client.GetAsync($"/api/v1/{OfflinePath}{suffix}", Ct); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("items", await response.Content.ReadAsStringAsync(Ct));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, $"{OfflinePath}/{e.Id}/cleanup", new { })).StatusCode);
    }
    [Fact]
    public async Task Missing_or_changed_policy_denies_capture_and_replay_without_fabricated_success_but_allows_scoped_cleanup()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client);
        var policy = host.Services.GetRequiredService<OfflineApplicationPolicy>(); policy.Fields = ["full_name"];
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, OfflinePath, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Deliver(client, e, Captured(e))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{OfflinePath}/{e.Id}/cleanup", new { })).StatusCode);
        policy.Fields = ["watch_group"]; policy.WatchGroups = ["SYNTHETIC-A", "SYNTHETIC-B", "CHANGED-WITHOUT-VERSION"];
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, $"{OfflinePath}/{e.Id}/renew", new { })).StatusCode);
        await using var db = Db(); Assert.Empty(await db.OfflineOutcomes.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Explicit_renewal_preserves_capture_while_revocation_and_loss_retain_immutable_data()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client); var c = Captured(e);
        // Author the real lifecycle with clock-independent server context. No client clock grants authority.
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{OfflinePath}/{e.Id}/renew", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, c)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{OfflinePath}/{e.Id}/lost", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Deliver(client, e, c)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, $"{OfflinePath}/{e.Id}/renew", new { })).StatusCode);
        var cleanup = await Send(client, $"{OfflinePath}/{e.Id}/cleanup", new { }); Assert.Equal(HttpStatusCode.OK, cleanup.StatusCode);
        Assert.DoesNotContain("baseWatchGroup", await cleanup.Content.ReadAsStringAsync(Ct));
        await using var db = Db(); Assert.Single(await db.OfflineOutcomes.ToArrayAsync(Ct));
        Assert.Contains(await db.OfflineLifecycles.ToArrayAsync(Ct), x => x.Action == "KeyLost");
    }
    [Fact]
    public async Task Unrecognized_sensitive_fields_duplicate_json_hash_and_capture_time_are_rejected_without_writes()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client); var c = Captured(e);
        var body = JsonSerializer.Serialize(c, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(HttpStatusCode.BadRequest, (await Deliver(client, e, c, body[..^1] + ",\"fullName\":\"PRIVATE-SENTINEL\"}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Deliver(client, e, c, body[..^1] + ",\"watchGroup\":null}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Deliver(client, e, c, badHash: true)).StatusCode);
        foreach (var invalid in new[] { c with { CapturedAt = DateTimeOffset.UtcNow.AddDays(1) }, c with { CapturedAt = e.ServerTime.AddDays(-1) },
            c with { WatchGroup = "PRIVATE-SENTINEL" }, c with { OffsetMinutes = 841 } })
            Assert.Equal(HttpStatusCode.Conflict, (await Deliver(client, e, invalid)).StatusCode);
        await using var db = Db(); Assert.Empty(await db.OfflineOutcomes.ToArrayAsync(Ct));
        Assert.All(await db.AuditEvents.ToArrayAsync(Ct), x => Assert.DoesNotContain("PRIVATE-SENTINEL", x.ChangeSummary));
    }
    [Fact]
    public async Task Other_vessel_tenant_actor_and_role_context_cannot_probe_or_replay_owned_capture()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client);
        var other = seed.Vessels.First(v => v.Id != SeedVessel.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{other.Id}/crew/{e.AssignmentId}/offline", Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var original = settings.Memberships[0];
        settings.Memberships[0] = original with { TenantId = seed.Tenants.First(t => t.Id != e.TenantId).Id };
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, $"{OfflinePath}/{e.Id}/cleanup", new { })).StatusCode);
        settings.Memberships[0] = original with { ActorId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, $"{OfflinePath}/{e.Id}/cleanup", new { })).StatusCode);
        settings.Memberships[0] = original with { RoleReferences = [] };
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, OfflinePath, new { })).StatusCode);
    }
    [Fact]
    public async Task Failure_in_offline_receipt_save_rolls_back_ordinary_mutation_receipt_and_audit()
    {
        await using var host = OfflineHost(new RejectOfflineOutcome()); using var client = Client(host); await Token(client); var e = await EnrollOffline(client);
        var response = await Deliver(client, e, Captured(e)); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var db = Db(); Assert.Empty(await db.OfflineOutcomes.ToArrayAsync(Ct)); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        Assert.Equal(e.BaseRevision, (await db.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct)).Revision);
        Assert.Empty(await db.AuditEvents.Where(x => x.EntityType == nameof(OfflineOutcome) || x.EntityType == nameof(CommandReceipt)).ToArrayAsync(Ct));
    }
    private sealed class RejectOfflineOutcome : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("INSERT INTO offline.\"Outcomes\"", StringComparison.Ordinal)) throw new DbUpdateException("synthetic receipt failure");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
    [Fact]
    public async Task Concurrent_duplicate_delivery_never_creates_two_domain_mutations_and_retry_recovers_original()
    {
        await using var host = OfflineHost(); using var first = Client(host); using var second = Client(host); await Token(first); await Token(second);
        var e = await EnrollOffline(first); var c = Captured(e);
        var results = await Task.WhenAll(Deliver(first, e, c), Deliver(second, e, c));
        Assert.Contains(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(results, x => Assert.True(x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.OK, (await Deliver(first, e, c)).StatusCode);
        await using var db = Db(); Assert.Single(await db.OfflineOutcomes.ToArrayAsync(Ct));
        Assert.Equal(e.BaseRevision + 1, (await db.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct)).Revision);
    }
    [Fact]
    public async Task Seed_is_idempotent_preserves_real_synthetic_session_work_and_does_not_manufacture_enrollment_authority()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client);
        await using (var empty = Db()) Assert.Empty(await empty.OfflineEnrollments.ToArrayAsync(Ct));
        var e = await EnrollOffline(client); Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, Captured(e))).StatusCode);
        await using (var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true)) {
            await SeedImporter.ImportAsync(admin, seed, Ct); await SeedImporter.ImportAsync(admin, seed, Ct);
        }
        await using var db = Db(); Assert.Single(await db.OfflineEnrollments.ToArrayAsync(Ct)); Assert.Single(await db.OfflineOutcomes.ToArrayAsync(Ct));
        Assert.Equal("SYNTHETIC-A", (await db.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct)).WatchGroup);
        await using var otherTenant = Db(seed.Tenants.First(t => t.Id != e.TenantId).Id);
        Assert.Empty(await otherTenant.OfflineEnrollments.ToArrayAsync(Ct)); Assert.Empty(await otherTenant.OfflineOutcomes.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Offline_lineage_and_context_scope_have_real_database_and_EF_immutability_guards()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, Captured(e))).StatusCode);
        await using var db = Db(); var outcome = await db.OfflineOutcomes.SingleAsync(Ct); outcome.Result = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql("UPDATE offline.\"Outcomes\" SET \"Result\" = 'Changed'"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql("TRUNCATE offline.\"Lifecycle\""));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql("UPDATE offline.\"Enrollments\" SET \"ActorId\" = gen_random_uuid()"));
    }
    [Fact]
    public async Task Expired_server_lease_blocks_new_replay_until_explicit_authenticated_renewal_without_losing_capture()
    {
        var clock = new OfflineTestClock();
        await using var host = OfflineHost().WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); }));
        using var client = Client(host); await Token(client); var e = await EnrollOffline(client);
        var c = Captured(e) with { CapturedAt = clock.GetUtcNow() };
        clock.Utc = e.ExpiresAt; // exact exclusive server expiry, never a client authorization clock
        Assert.Equal(HttpStatusCode.Conflict, (await Deliver(client, e, c)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{OfflinePath}/{e.Id}/renew", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, c)).StatusCode);
        await using var db = Db(); var outcome = await db.OfflineOutcomes.SingleAsync(Ct);
        Assert.Equal(clock.GetUtcNow(), outcome.ReceivedAt); Assert.Equal(c.CapturedAt, outcome.CapturedAt);
        Assert.Equal(180, outcome.OffsetMinutes); Assert.NotEqual(outcome.CapturedAt, outcome.ReceivedAt);
    }
    private sealed class OfflineTestClock : TimeProvider
    {
        // Browser capture uses millisecond ISO timestamps; keep the deterministic provider clock at that precision.
        public DateTimeOffset Utc { get; set; } = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        public override DateTimeOffset GetUtcNow() => Utc;
    }
    [Fact]
    public async Task Enrollment_proof_cannot_be_replaced_and_missing_or_wrong_replay_signing_key_denies_before_receipt()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client); var c = Captured(e);
        using var wrong = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var challengeProof = Convert.ToBase64String(wrong.SignData(Encoding.UTF8.GetBytes($"{e.Id:D}/{e.KeyId:D}/{e.KeyChallenge}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, $"{OfflinePath}/{e.Id}/key", new { publicKey = Convert.ToBase64String(wrong.ExportSubjectPublicKeyInfo()), proof = challengeProof })).StatusCode);
        var body = JsonSerializer.Serialize(c, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var signed in new[] { false, true }) {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/{OfflinePath}/{e.Id}/replay") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))); message.Headers.Add("X-Offline-Hash", digest);
            if (signed) message.Headers.Add("X-Offline-Signature", Convert.ToBase64String(wrong.SignData(Encoding.UTF8.GetBytes(digest), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(message, Ct)).StatusCode);
        }
        await using var db = Db(); Assert.Empty(await db.OfflineOutcomes.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Composite_database_FKs_reject_a_receipt_for_another_actor_or_assignment_even_with_valid_tenant()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client); var e = await EnrollOffline(client);
        await using var db = Db();
        db.OfflineOutcomes.Add(new() { TenantId = e.TenantId, ActorId = Guid.NewGuid(), CommandId = Guid.NewGuid(), EnrollmentId = e.Id,
            VesselId = e.VesselId, AssignmentId = e.AssignmentId, Digest = new string('A', 64), Result = "Committed", ProtectedContext = "synthetic-invalid-FK-test",
            BaseVersion = e.BaseVersion, ServerVersion = e.BaseVersion, Revision = e.BaseRevision, CapturedAt = DateTimeOffset.UtcNow, ReceivedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Resolution_rejects_a_server_change_after_review_and_requires_explicit_current_version_choice()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client);
        var e = await EnrollOffline(client); var local = Captured(e);
        var first = await EnrollOffline(client); await Deliver(client, first, Captured(first, "SYNTHETIC-B"));
        var conflict = (await (await Deliver(client, e, local)).Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct))!;
        var next = await EnrollOffline(client);
        var changed = (await (await Deliver(client, next, Captured(next, null))).Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct))!;
        var resolution = Captured(e, null) with { ParentCommandId = local.CommandId, BaseVersion = conflict.ServerVersion,
            ExpectedServerVersion = conflict.ServerVersion, Choice = "keepServer", Reason = "Explicit fictional current server choice" };
        Assert.Equal(HttpStatusCode.Conflict, (await Deliver(client, e, resolution)).StatusCode);
        var currentResolution = resolution with { CommandId = Guid.NewGuid(), BaseVersion = changed.ServerVersion, ExpectedServerVersion = changed.ServerVersion };
        Assert.Equal(HttpStatusCode.OK, (await Deliver(client, e, currentResolution)).StatusCode);
        await using var db = Db(); Assert.Null((await db.CrewAssignments.SingleAsync(x => x.Id == e.AssignmentId, Ct)).WatchGroup);
        Assert.False(await db.OfflineOutcomes.AnyAsync(o => o.CommandId == resolution.CommandId, Ct));
    }
}
