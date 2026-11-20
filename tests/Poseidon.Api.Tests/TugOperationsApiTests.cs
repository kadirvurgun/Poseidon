using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;
namespace Poseidon.Api.Tests;

// S10 WP-B DEV-091 (18.01 row 92, T-TUG-012), DEV-092 (row 93, T-TUG-013), DEV-119 (row 120, T-V463-005): real PostgreSQL +
// actual host. Synthetic identities and FICTIONAL content only; the offline policy is a FICTIONAL test configuration and no
// approved classification. AUTHORED; execution DEFERRED to CI (no local Docker in this delivery).
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly JsonSerializerOptions OpsJson = new(JsonSerializerDefaults.Web);
    private WebApplicationFactory<Program> TugOpsHost() => ReadinessHost(true).WithWebHostBuilder(b => b.ConfigureTestServices(s => {
        s.RemoveAll<TugOperationsOfflineSettings>();
        s.AddSingleton(new TugOperationsOfflineSettings { Policy = new() { Version = "fictional-tug-ops-offline-v1", SourceVersion = TugOperationsOfflineRules.SourceVersion,
            Classification = TugOperationsOfflinePolicy.RequiredClassification, StoragePolicyReference = "FICTIONAL encrypted IndexedDB test only",
            KeyPolicyReference = "FICTIONAL nonextractable key test only", RetentionPolicyReference = "FICTIONAL fixture retention; no approval", LeaseSeconds = 3600, MaxActiveSnapshots = 4 } });
    }));
    private async Task<HttpResponseMessage> OfflineReplay(HttpClient c, TugOperationsOfflineCommand command)
    {
        await Token(c);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tug-operations-offline/replay?vessel={Tug.Id}")
            { Content = new StringContent(JsonSerializer.Serialize(command, OpsJson), Encoding.UTF8, "application/json") };
        message.Headers.Add("Idempotency-Key", command.CommandId.ToString("D"));
        return await c.SendAsync(message, Ct);
    }

    [Fact]
    [Trait("Requirement", "DEV-092")]
    public async Task Tug_offline_snapshot_replays_daily_entries_idempotently_and_keeps_material_conflicts_for_a_person()
    {
        using var host = TugOpsHost(); using var c = Client(host);
        Guid author = Guid.NewGuid(), other = Guid.NewGuid();
        FieldActor(host.Services, author); await Token(c);
        var job = await SubmittedJob(c);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        TugDailyLogContent Content(string summary) => new("UTC", [], new(null, null, null), [], [], summary);
        // A free tug/date/watch slot inside the snapshot window (other tests share the synthetic tug).
        DateOnly date = today; string watch = "OTHER";
        await using (var db = Db()) {
            var taken = await db.TugDailyLogs.Where(l => l.VesselId == Tug.Id && l.LogDate >= today.AddDays(-6)).Select(l => new { l.LogDate, l.Watch }).ToArrayAsync(Ct);
            (date, watch) = Enumerable.Range(0, 7).SelectMany(d => TugDailyLogPolicy.Watches.Select(w => (today.AddDays(-d), w))).First(x => !taken.Any(t => t.LogDate == x.Item1 && t.Watch == x.Item2));
        }
        var created = await RiskOk<TugDailyLogView>(await Send(c, $"tug-daily-logs?vessel={Tug.Id}", new TugDailyLogCreateCommand(Guid.NewGuid(), date, watch, null, Content("FICTIONAL online summary"))));

        var capability = (await c.GetFromJsonAsync<TugOperationsOfflineCapabilityDto>($"/api/v1/tug-operations-offline?vessel={Tug.Id}", Ct))!;
        Assert.Equal("CONFIGURED_UNVERIFIED", capability.State);
        Assert.Contains("MASTER_RELEASE", capability.OnlineOnly); Assert.Contains("EXECUTION_START", capability.OnlineOnly);
        // Prefetch: strict body, CSRF, recorded lease + hash.
        await AssertCode(await Send(c, $"tug-operations-offline/snapshots?vessel={Tug.Id}&job={job.Id}", new { extra = 1 }, ""), HttpStatusCode.BadRequest, "invalid_request");
        var snapshot = await RiskOk<TugOperationsSnapshotDto>(await Send(c, $"tug-operations-offline/snapshots?vessel={Tug.Id}&job={job.Id}", new { }, ""));
        Assert.Equal((job.Id, Tug.Id), (snapshot.JobId, snapshot.VesselId));
        var snapLog = Assert.Single(snapshot.DailyLogs, l => l.Id == created.Log.Id);
        Assert.Contains(Tug.Id, snapshot.WritableTugs);
        Assert.Null(snapshot.Briefing); Assert.Empty(snapshot.Executions);

        // Capture on the snapshot base: committed once; the same bytes return the first outcome; other bytes are refused.
        TugOperationsOfflineCommand Entry(uint? baseVersion, string summary, DateTimeOffset? at = null, Guid? parent = null, string? choice = null, string? reason = null) =>
            new(Guid.NewGuid(), snapshot.Id, TugOperationsOfflineRules.DailyEntry, at ?? DateTimeOffset.UtcNow, 180, null, null,
                new(created.Log.Id, Tug.Id, baseVersion, date, watch, Content(summary)), parent, choice, reason);
        var first = Entry(snapLog.Version, "FICTIONAL offline summary one");
        using var committed = await OfflineReplay(c, first); Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var body = await committed.Content.ReadAsStringAsync(Ct);
        var outcome = JsonSerializer.Deserialize<TugOperationsOfflineResult>(body, OpsJson)!;
        Assert.Equal((TugOperationsOfflineRules.Committed, created.Log.Id, 2u), (outcome.Result, outcome.ResourceId, outcome.ServerVersion));
        using (var retry = await OfflineReplay(c, first)) Assert.Equal(body, await retry.Content.ReadAsStringAsync(Ct));
        await AssertCode(await OfflineReplay(c, first with { DailyEntry = first.DailyEntry! with { Content = Content("FICTIONAL different") } }), HttpStatusCode.Conflict, "offline_command_reused");
        await AssertCode(await OfflineReplay(c, Entry(snapLog.Version, "FICTIONAL before lease", snapshot.IssuedAt.AddHours(-1))), HttpStatusCode.Conflict, "offline_recovery_required");
        await AssertCode(await OfflineReplay(c, first with { CommandId = Guid.NewGuid(), Choice = "keepServer" }), HttpStatusCode.BadRequest, "invalid_offline_command");

        // A second capture on the same (now stale) base is an explicit material conflict, never applied silently.
        var stale = Entry(snapLog.Version, "FICTIONAL offline summary two");
        var conflict = await RiskOk<TugOperationsOfflineResult>(await OfflineReplay(c, stale));
        Assert.Equal(TugOperationsOfflineRules.MaterialConflict, conflict.Result); Assert.Equal(["daily_version"], conflict.ConflictFields);
        Assert.Equal([TugOperationsOfflineRules.KeepServer, TugOperationsOfflineRules.UseCaptured], conflict.Choices);
        var current = await FieldGet<TugDailyLogView>(c, $"tug-daily-logs/{created.Log.Id}?vessel={Tug.Id}");
        Assert.Equal("FICTIONAL offline summary one", current.Log.Content.DailySummary);
        var center = (await c.GetFromJsonAsync<SyncConflictPageDto>($"/api/v1/sync/conflicts?vesselId={Tug.Id}", Ct))!;
        Assert.Contains(center.Items, i => i.Source == "tugOperations" && i.CommandId == stale.CommandId && i.State == "Open" && i.ResourceKind == TugOperationsOfflineRules.DailyEntry);
        Assert.Contains(center.Sources, s => s.Source == "tugOperations" && s.State == "Included");
        var detail = (await c.GetFromJsonAsync<TugOperationsConflictDto>($"/api/v1/tug-operations-offline/conflicts/{stale.CommandId}?vessel={Tug.Id}", Ct))!;
        Assert.False(detail.Resolved); Assert.Equal(conflict.Choices, detail.Choices);

        // Another actor never sees or uses this snapshot.
        FieldActor(host.Services, other); await Token(c);
        await AssertCode(await OfflineReplay(c, Entry(snapLog.Version, "FICTIONAL foreign")), HttpStatusCode.NotFound, "offline_snapshot_unavailable");
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/tug-operations-offline/conflicts/{stale.CommandId}?vessel={Tug.Id}", Ct)).StatusCode);

        // Human resolution: same captured payload with a reason; applied on the CURRENT base exactly once.
        FieldActor(host.Services, author); await Token(c);
        await AssertCode(await OfflineReplay(c, stale with { CommandId = Guid.NewGuid(), DailyEntry = stale.DailyEntry! with { Content = Content("FICTIONAL edited") },
            ParentCommandId = stale.CommandId, Choice = "useCaptured", Reason = "INPUT_CORRECTED" }), HttpStatusCode.Conflict, "offline_conflict_changed");
        var resolution = stale with { CommandId = Guid.NewGuid(), ParentCommandId = stale.CommandId, Choice = "useCaptured", Reason = "INPUT_CORRECTED" };
        var resolved = await RiskOk<TugOperationsOfflineResult>(await OfflineReplay(c, resolution));
        Assert.Equal((TugOperationsOfflineRules.Committed, stale.CommandId), (resolved.Result, resolved.ParentCommandId!.Value));
        await AssertCode(await OfflineReplay(c, resolution with { CommandId = Guid.NewGuid(), Choice = "keepServer", Reason = "CURRENT_SERVER_RETAINED" }),
            HttpStatusCode.Conflict, "offline_conflict_changed");
        Assert.Equal("FICTIONAL offline summary two", (await FieldGet<TugDailyLogView>(c, $"tug-daily-logs/{created.Log.Id}?vessel={Tug.Id}")).Log.Content.DailySummary);
        center = (await c.GetFromJsonAsync<SyncConflictPageDto>($"/api/v1/sync/conflicts?vesselId={Tug.Id}", Ct))!;
        Assert.Contains(center.Items, i => i.CommandId == stale.CommandId && i.State == "Resolved" && i.ResolutionCommandId == resolution.CommandId);

        await using var check = Db();
        var rows = await check.TugOperationOfflineOutcomes.Where(o => o.SnapshotId == snapshot.Id).ToArrayAsync(Ct);
        Assert.Equal(3, rows.Length);
        Assert.All(rows, r => Assert.DoesNotContain("FICTIONAL", r.ProtectedContext));
        var audit = await check.AuditEvents.Where(e => e.EntityType == nameof(TugOperationOfflineOutcome) && e.ResourceId == created.Log.Id).ToArrayAsync(Ct);
        Assert.Equal(3, audit.Length); Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        Assert.Contains(await check.AuditEvents.Where(e => e.EntityType == nameof(TugOperationOfflineSnapshot) && e.ResourceId == job.Id).ToArrayAsync(Ct), e => e.ActorId == author.ToString());
        Assert.Equal(["CREATE", "EDIT", "EDIT"], await check.TugDailyLogRevisions.Where(r => r.LogId == created.Log.Id).OrderBy(r => r.Revision).Select(r => r.ReasonCode).ToArrayAsync(Ct));
        rows[0].Result = "ServerRetained"; await Assert.ThrowsAsync<InvalidOperationException>(() => check.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-092")]
    public async Task Tug_offline_operations_are_unavailable_without_an_operator_policy()
    {
        using var host = ReadinessHost(true); using var c = Client(host);
        FieldActor(host.Services, Guid.NewGuid()); await Token(c);
        var job = await SubmittedJob(c);
        Assert.Equal("UNAVAILABLE_POLICY", (await c.GetFromJsonAsync<TugOperationsOfflineCapabilityDto>($"/api/v1/tug-operations-offline?vessel={Tug.Id}", Ct))!.State);
        await AssertCode(await Send(c, $"tug-operations-offline/snapshots?vessel={Tug.Id}&job={job.Id}", new { }, ""), HttpStatusCode.Forbidden, "offline_policy_denied");
        await AssertCode(await Send(c, $"tug-operations-offline/snapshots?vessel={Guid.NewGuid()}&job={job.Id}", new { }, ""), HttpStatusCode.NotFound, "resource_not_found");
    }

    [Fact]
    [Trait("Requirement", "DEV-091")]
    public async Task Tug_operation_history_filters_by_scope_and_outcome_and_keeps_cancelled_jobs_visible()
    {
        using var host = ReadinessHost(true); using var c = Client(host);
        FieldActor(host.Services, Guid.NewGuid()); await Token(c);
        var open = await SubmittedJob(c); var cancelled = await SubmittedJob(c);
        using (var r = await Lifecycle(c, cancelled.Id, "cancel", new TugLifecycleCommand(cancelled.Version, "FICTIONAL customer cancelled the request"))) Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        TugHistoryPageDto Page(HttpResponseMessage m) => m.Content.ReadFromJsonAsync<TugHistoryPageDto>(OpsJson, Ct).GetAwaiter().GetResult()!;
        using (var r = await c.GetAsync($"/api/v1/tug-operation-history?vesselId={Tug.Id}&jobId={open.Id}", Ct)) {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var item = Assert.Single(Page(r).Items);
            Assert.Equal((open.Id, "NOT_STARTED", "UNKNOWN"), (item.JobId, item.Outcome, item.Evidence)); Assert.Empty(item.Executions);
        }
        using (var r = await c.GetAsync($"/api/v1/tug-operation-history?vesselId={Tug.Id}&outcome=CANCELLED", Ct)) {
            var page = Page(r);
            Assert.Contains(page.Items, i => i.JobId == cancelled.Id && i.JobStatus == "Cancelled" && i.CancelledAt != null);
            Assert.DoesNotContain(page.Items, i => i.JobId == open.Id);
        }
        using (var r = await c.GetAsync($"/api/v1/tug-operation-history?vesselId={Tug.Id}&outcome=NOT_STARTED", Ct))
            Assert.DoesNotContain(Page(r).Items, i => i.JobId == cancelled.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/v1/tug-operation-history?vesselId={Tug.Id}&actorId={Guid.NewGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/tug-operation-history?vesselId={Guid.NewGuid()}", Ct)).StatusCode);
        // Keyset paging: pages never repeat a job.
        using (var r = await c.GetAsync($"/api/v1/tug-operation-history?vesselId={Tug.Id}", Ct)) {
            var page = Page(r);
            if (page.NextCursor != null) {
                using var next = await c.GetAsync($"/api/v1/tug-operation-history?vesselId={Tug.Id}&cursor={page.NextCursor}", Ct);
                Assert.Empty(Page(next).Items.Select(i => i.JobId).Intersect(page.Items.Select(i => i.JobId)));
            }
        }
    }

    [Fact]
    [Trait("Requirement", "DEV-119")]
    public async Task Tug_demo_fixture_is_opt_in_idempotent_fictional_and_seeds_no_decision_execution_or_acknowledgement()
    {
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            await TugDemoFixtures.AddMissingAsync(db, seed.Vessels, Ct); await TugDemoFixtures.AddMissingAsync(db, seed.Vessels, Ct); await tx.CommitAsync(Ct);
        }
        var tugs = seed.Vessels.Where(v => v.Kind == VesselKind.Tug).Select(v => v.Id).ToArray();
        var plans = await db.TowagePlans.Where(p => tugs.Contains(p.VesselId) && tugs.Contains(p.JobId)).ToArrayAsync(Ct);
        var briefings = await db.TowageBriefings.Where(b => tugs.Contains(b.VesselId) && tugs.Contains(b.JobId)).ToArrayAsync(Ct);
        var jobs = await db.TugJobs.CountAsync(j => tugs.Contains(j.Id) && tugs.Contains(j.VesselId) && j.Provenance == "SyntheticUnverified", Ct);
        Assert.Equal(jobs, plans.Length); Assert.Equal(jobs, briefings.Length);
        Assert.All(plans, p => Assert.Equal("DRAFT", p.State)); Assert.All(briefings, b => Assert.Equal("DRAFT", b.State));
        Assert.Equal(tugs.Length, await db.TugDailyLogs.CountAsync(l => tugs.Select(FieldOperationsFixtures.DailyLogId).Contains(l.Id), Ct));
        Assert.Empty(await db.TowageExecutions.Where(x => tugs.Contains(x.JobId)).ToArrayAsync(Ct));
        Assert.Empty(await db.TugReadinessDecisions.Where(d => tugs.Contains(d.VesselId) && d.Reason.StartsWith("SYNTHETIC")).ToArrayAsync(Ct));
        Assert.Empty(await db.TowageBriefingAcknowledgements.Where(k => briefings.Select(b => b.Id).Contains(k.BriefingId)).ToArrayAsync(Ct));
        Assert.Empty(await db.TugOperationOfflineSnapshots.Where(s => tugs.Contains(s.JobId)).ToArrayAsync(Ct));
    }
}
