using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// AUTHORED / NOT RUN. Real API host, antiforgery, owned PostgreSQL and signed device commands.
public sealed partial class DirectoryWorkspaceTests
{
    private TugPlanningPolicy OfflinePlanningPolicy() {
        var p = SyntheticPlanning();
        p.Offline = new() { Version = "fictional-window-v1", SourceVersion = TugOfflinePolicy.Source,
            Purpose = "FICTIONAL software test", FieldClassification = "NonPersonalControlledDraftDeclaration",
            DevicePolicyReference = "FICTIONAL-device", KeyPolicyReference = "FICTIONAL-key",
            StoragePolicyReference = "FICTIONAL-encrypted-idb", RetentionPolicyReference = "FICTIONAL-ephemeral",
            Fields = ["planned_start", "planned_end"], LeaseSeconds = 3600 };
        return p;
    }
    private async Task<TugOfflineContext> EnrollTug(HttpClient c, Guid job, ECDsa key) {
        using var response = await Send(c, PlanningUrl(job, "/offline"), new { }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var e = (await response.Content.ReadFromJsonAsync<TugOfflineContext>(Ct))!;
        using var enrolled = await Send(c, PlanningUrl(job, $"/offline/{e.Id}/key"), new OfflineKeyEnrollment(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes($"{e.Id:D}/{e.KeyId:D}/{e.Challenge}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))));
        Assert.Equal(HttpStatusCode.OK, enrolled.StatusCode); return e;
    }
    private static TugOfflineCapture TugCaptured(TugOfflineContext e) => new(Guid.NewGuid(), e.KeyId, e.DeviceId, e.PolicyVersion, e.SourceVersion,
        TugOfflinePolicy.Operation, e.BaseVersion, e.RequestVersion, DateTimeOffset.UtcNow, 180, DateTimeOffset.UtcNow.AddHours(1), null);
    private async Task<HttpResponseMessage> DeliverTug(HttpClient c, TugOfflineContext e, TugOfflineCapture cmd, ECDsa key, string? raw = null) {
        var body = raw ?? JsonSerializer.Serialize(cmd, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + PlanningUrl(e.JobId, $"/offline/{e.Id}/replay")) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Offline-Proof", Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes($"{e.Id:D}/{cmd.CommandId:D}/{digest}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        return await c.SendAsync(request, Ct);
    }
    [Fact]
    public async Task Tug_device_command_commits_atomic_plan_receipt_outcome_audit_then_recovers_pinned_outcome_after_restart() {
        var p = OfflinePlanningPolicy(); TugRequestDto job; TugOfflineContext e; TugOfflineResult result; TugOfflineCapture cmd;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using (var host = PlanningHost(p)) {
            using var c = Client(host); await Token(c); job = await PlanningJob(c);
            var plan = await ChangePlan(c, job, null, new("Window")); e = await EnrollTug(c, job.Id, key); cmd = TugCaptured(e);
            using var committed = await DeliverTug(c, e, cmd, key); Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
            result = (await committed.Content.ReadFromJsonAsync<TugOfflineResult>(Ct))!; Assert.Equal("Committed", result.Result);
            Assert.Equal(plan.Revision + 1, result.Revision); Assert.NotEqual(plan.Version, result.ServerVersion);
            using var retry = await DeliverTug(c, e, cmd, key); Assert.Equal(result, await retry.Content.ReadFromJsonAsync<TugOfflineResult>(Ct));
            var noOp = cmd with { CommandId = Guid.NewGuid(), BaseVersion = result.ServerVersion, ParentCommandId = null };
            // A new capture cannot silently rebase this device's frozen context.
            Assert.Equal(HttpStatusCode.BadRequest, (await DeliverTug(c, e, noOp, key)).StatusCode);
        }
        using (var restarted = PlanningHost(p)) {
            using var c = Client(restarted); await Token(c);
            using var renew = await Send(c, PlanningUrl(job.Id, $"/offline/{e.Id}/renew"), new { }); Assert.Equal(HttpStatusCode.OK, renew.StatusCode);
            Assert.Equal(e.BaseVersion, (await renew.Content.ReadFromJsonAsync<TugOfflineContext>(Ct))!.BaseVersion);
            using var recovered = await DeliverTug(c, e, cmd, key); Assert.Equal(result, await recovered.Content.ReadFromJsonAsync<TugOfflineResult>(Ct));
            using var history = await c.GetAsync("/api/v1/" + PlanningUrl(job.Id, $"/offline/{e.Id}/history"), Ct);
            Assert.Equal(HttpStatusCode.OK, history.StatusCode); Assert.DoesNotContain("protectedContext", await history.Content.ReadAsStringAsync(Ct));
        }
        await using var db = Db(); Assert.Single(await db.TugOfflineOutcomes.Where(o => o.JobId == job.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(r => r.Key == cmd.CommandId.ToString()).ToArrayAsync(Ct));
        var audit = await db.AuditEvents.Where(a => a.EntityType == nameof(TugOfflineOutcome) && a.ResourceId == job.Id).ToArrayAsync(Ct);
        Assert.Single(audit); Assert.Equal(Tug.Id, audit[0].VesselId); Assert.DoesNotContain("Protected", audit[0].ChangeSummary);
    }
    [Fact]
    public async Task Changed_plan_creates_material_conflict_and_reasoned_resolution_increments_xmin_even_for_keep_server_noop() {
        using var host = PlanningHost(OfflinePlanningPolicy()); using var c = Client(host); await Token(c);
        var job = await PlanningJob(c); var plan = await ChangePlan(c, job, null, new("Window"));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var e = await EnrollTug(c, job.Id, key); var cmd = TugCaptured(e);
        // Match PostgreSQL's exact persisted microsecond precision before the
        // command, retaining strict equality after keep-server resolution.
        plan = await ChangePlan(c, job, plan, new("Window", null, PostgresInstant(DateTimeOffset.UtcNow.AddHours(2))));
        using var delivered = await DeliverTug(c, e, cmd, key); var conflict = (await delivered.Content.ReadFromJsonAsync<TugOfflineResult>(Ct))!;
        Assert.Equal("MaterialConflict", conflict.Result); Assert.Equal(plan.Version, conflict.ServerVersion);
        var detail = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id, $"/offline/{e.Id}/conflicts/{cmd.CommandId}"), Ct);
        Assert.Equal(2, detail.GetProperty("choices").GetArrayLength()); Assert.Equal(JsonValueKind.Null, detail.GetProperty("base").GetProperty("plannedStart").ValueKind);
        var resolution = cmd with { CommandId = Guid.NewGuid(), BaseVersion = plan.Version!.Value, ParentCommandId = cmd.CommandId,
            ExpectedServerVersion = plan.Version, Choice = "keepServer", Reason = "CURRENT_SERVER_RETAINED", CapturedAt = DateTimeOffset.UtcNow };
        using var resolved = await DeliverTug(c, e, resolution, key); Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var outcome = (await resolved.Content.ReadFromJsonAsync<TugOfflineResult>(Ct))!; Assert.Equal("Committed", outcome.Result);
        Assert.NotEqual(plan.Version, outcome.ServerVersion); Assert.Equal(plan.Revision + 1, outcome.Revision);
        using var duplicate = await DeliverTug(c, e, resolution, key); Assert.Equal(outcome, await duplicate.Content.ReadFromJsonAsync<TugOfflineResult>(Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await DeliverTug(c, e, resolution with { CommandId = Guid.NewGuid() }, key)).StatusCode);
        await using var db = Db(); var current = await db.TugDraftPlans.SingleAsync(p => p.JobId == job.Id, Ct);
        Assert.Null(current.PlannedStart); Assert.Equal(plan.Inputs.PlannedEnd, current.PlannedEnd);
        Assert.Equal(2, await db.TugOfflineOutcomes.CountAsync(o => o.JobId == job.Id, Ct));
    }
    [Theory]
    [InlineData("scope")][InlineData("mode")][InlineData("policy")][InlineData("revoke")][InlineData("device")][InlineData("key")][InlineData("source")][InlineData("body")]
    public async Task Current_rights_and_device_policy_precede_recovery_and_no_unsupported_capture_can_mutate(string change) {
        var p = OfflinePlanningPolicy(); using var host = PlanningHost(p); using var c = Client(host); await Token(c);
        var job = await PlanningJob(c); await ChangePlan(c, job, null, new("Window"));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var e = await EnrollTug(c, job.Id, key); var cmd = TugCaptured(e);
        Assert.Equal(HttpStatusCode.OK, (await DeliverTug(c, e, cmd, key)).StatusCode);
        if (change == "scope") p.Scopes = [];
        if (change == "policy") p.Offline.Version = "changed";
        if (change == "mode") {
            var s = host.Services.GetRequiredService<FederationSettings>(); s.Memberships[0] = s.Memberships[0] with { Permissions = s.Memberships[0].Permissions.Where(x => x != "mode.tug").ToArray() };
        }
        if (change == "revoke") Assert.Equal(HttpStatusCode.OK, (await Send(c, PlanningUrl(job.Id, $"/offline/{e.Id}/revoke"), new { })).StatusCode);
        if (change == "device") cmd = cmd with { DeviceId = Guid.NewGuid() };
        if (change == "key") cmd = cmd with { KeyId = Guid.NewGuid() };
        if (change == "source") cmd = cmd with { SourceVersion = "unsupported" };
        var body = change == "body" ? JsonSerializer.Serialize(cmd, new JsonSerializerOptions(JsonSerializerDefaults.Web)).TrimEnd('}') + ",\"ready\":true}" : null;
        using var denied = await DeliverTug(c, e, cmd, key, body); Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        if (change is "scope" or "mode" or "policy" or "revoke") {
            using var history = await c.GetAsync("/api/v1/" + PlanningUrl(job.Id, $"/offline/{e.Id}/history"), Ct);
            Assert.NotEqual(HttpStatusCode.OK, history.StatusCode); Assert.DoesNotContain("serverVersion", await history.Content.ReadAsStringAsync(Ct));
        }
        await using var db = Db(); Assert.Single(await db.TugOfflineOutcomes.Where(o => o.JobId == job.Id).ToArrayAsync(Ct));
        Assert.Equal(2, await db.TugDraftPlanRevisions.CountAsync(r => r.JobId == job.Id, Ct));
    }
    [Fact]
    public async Task Simultaneous_device_replays_have_one_atomic_effect_and_storage_lineage_is_immutable() {
        var p = OfflinePlanningPolicy(); using var host = PlanningHost(p); using var c = Client(host); await Token(c);
        var job = await PlanningJob(c); await ChangePlan(c, job, null, new("Window"));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var e = await EnrollTug(c, job.Id, key); var cmd = TugCaptured(e);
        var race = await Task.WhenAll(DeliverTug(c, e, cmd, key), DeliverTug(c, e, cmd, key));
        Assert.Contains(race, r => r.StatusCode == HttpStatusCode.OK); Assert.All(race, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        foreach (var response in race) response.Dispose();
        await using var db = Db(); Assert.Single(await db.TugOfflineOutcomes.Where(o => o.JobId == job.Id).ToArrayAsync(Ct));
        Assert.Equal(2, await db.TugDraftPlanRevisions.CountAsync(r => r.JobId == job.Id, Ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql("UPDATE tug.\"OfflineOutcomes\" SET \"Result\"='Committed'"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql("TRUNCATE tug.\"OfflineLifecycle\""));
    }
    [Fact]
    public async Task Replay_outcome_storage_failure_rolls_back_already_saved_plan_receipt_and_audit_then_exact_retry_commits() {
        using var host = PlanningHost(OfflinePlanningPolicy()); using var c = Client(host); await Token(c);
        var job = await PlanningJob(c); var plan = await ChangePlan(c, job, null, new("Window"));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var e = await EnrollTug(c, job.Id, key); var cmd = TugCaptured(e);
        int before; await using (var db = Db()) before = await db.AuditEvents.CountAsync(a => a.ResourceId == job.Id, Ct);
        await Sql("CREATE TRIGGER reject_tug_outcome BEFORE INSERT ON tug.\"OfflineOutcomes\" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
        using var failed = await DeliverTug(c, e, cmd, key); Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        await using (var db = Db()) {
            Assert.Equal(plan.Revision, (await db.TugDraftPlans.SingleAsync(p => p.JobId == job.Id, Ct)).Revision);
            Assert.False(await db.CommandReceipts.AnyAsync(r => r.Key == cmd.CommandId.ToString(), Ct));
            Assert.False(await db.TugOfflineOutcomes.AnyAsync(o => o.JobId == job.Id, Ct));
            Assert.Equal(before, await db.AuditEvents.CountAsync(a => a.ResourceId == job.Id, Ct));
        }
        await Sql("DROP TRIGGER reject_tug_outcome ON tug.\"OfflineOutcomes\";");
        Assert.Equal(HttpStatusCode.OK, (await DeliverTug(c, e, cmd, key)).StatusCode);
    }
    [Fact]
    public async Task Server_expiry_requires_current_online_renewal_and_does_not_rebase_the_original_capture() {
        var clock = new OfflineTestClock(); var p = OfflinePlanningPolicy();
        using var host = PlanningHost(p).WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); }));
        using var c = Client(host); await Token(c); var job = await PlanningJob(c); await ChangePlan(c, job, null, new("Window"));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var e = await EnrollTug(c, job.Id, key);
        var cmd = TugCaptured(e) with { CapturedAt = clock.GetUtcNow() };
        clock.Utc = e.ExpiresAt;
        Assert.Equal(HttpStatusCode.Forbidden, (await DeliverTug(c, e, cmd, key)).StatusCode);
        using var renewal = await Send(c, PlanningUrl(job.Id, $"/offline/{e.Id}/renew"), new { }); Assert.Equal(HttpStatusCode.OK, renewal.StatusCode);
        Assert.Equal(e.BaseVersion, (await renewal.Content.ReadFromJsonAsync<TugOfflineContext>(Ct))!.BaseVersion);
        Assert.Equal(HttpStatusCode.OK, (await DeliverTug(c, e, cmd, key)).StatusCode);
        await using var db = Db(); var row = await db.TugOfflineOutcomes.SingleAsync(o => o.JobId == job.Id, Ct);
        Assert.Equal(cmd.CapturedAt, row.CapturedAt); Assert.Equal(180, row.OffsetMinutes); Assert.Equal(clock.Utc, row.ReceivedAt);
    }
    [Fact]
    public async Task Missing_or_foreign_device_proof_and_cross_tenant_route_are_denied_before_any_domain_effect() {
        using var host = PlanningHost(OfflinePlanningPolicy()); using var c = Client(host); await Token(c);
        var job = await PlanningJob(c); await ChangePlan(c, job, null, new("Window"));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var foreign = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var e = await EnrollTug(c, job.Id, key); var cmd = TugCaptured(e);
        Assert.Equal(HttpStatusCode.Forbidden, (await DeliverTug(c, e, cmd, foreign)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, PlanningUrl(job.Id, $"/offline/{e.Id}/replay"), cmd)).StatusCode);
        var other = seed.Vessels.First(v => v.TenantId != Tug.TenantId);
        using var hidden = await c.GetAsync($"/api/v1/tug-jobs/{job.Id}/plan/offline/{e.Id}/history?vessel={other.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); Assert.DoesNotContain("serverVersion", await hidden.Content.ReadAsStringAsync(Ct));
        await using var db = Db(); Assert.Empty(await db.TugOfflineOutcomes.Where(o => o.JobId == job.Id).ToArrayAsync(Ct));
        Assert.Single(await db.TugDraftPlanRevisions.Where(r => r.JobId == job.Id).ToArrayAsync(Ct));
    }
}
