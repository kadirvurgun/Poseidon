using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;
namespace Poseidon.Api.Tests;

// DEV-081 / DEV-082 (18.01 Backlog rows 82/83, 60.02 §5): real PostgreSQL + actual host.
// Synthetic identities and FICTIONAL request data only.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid LifecycleOwner = Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535");
    private TugSaveCommand CompleteDraft(DateTimeOffset start) => new(Guid.NewGuid(), null, new(RequestSource: "Customer", AssistedVessel: "FICTIONAL assisted vessel",
        JobType: "Harbour Towage", RequestedStart: start, OperationArea: "FICTIONAL-AREA", RequiredTugCount: 2, Priority: "High", RequestOwner: LifecycleOwner));
    private async Task<HttpResponseMessage> Lifecycle(HttpClient c, Guid id, string operation, object body, string? key = null) {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tug-jobs/{id}/{operation}?vessel={Tug.Id}") { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await c.SendAsync(message, Ct);
    }

    [Fact]
    public async Task Incomplete_draft_saves_but_submission_is_blocked_with_missing_keys_and_no_state_change() {
        using var host = Host(tugRead: true, tugWrite: true); using var c = Client(host); await Token(c);
        var draft = (await (await Send(c, TugUrl, Draft())).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.Equal("Draft", draft.State); Assert.Contains("requested_start", draft.Missing);
        var r = await Lifecycle(c, draft.Id, "submit", new TugLifecycleCommand(draft.Version));
        Assert.Equal((HttpStatusCode)422, r.StatusCode);
        var failure = (await r.Content.ReadFromJsonAsync<TugLifecycleFailure>(Ct))!;
        Assert.Equal("tug_request_incomplete", failure.Code); Assert.Equal(draft.Missing.Order(), failure.Missing.Order());
        await using var db = Db();
        var job = await db.TugJobs.SingleAsync(j => j.Id == draft.Id, Ct);
        Assert.Equal("Draft", job.Status); Assert.Null(job.SubmittedAt); Assert.Equal(1, job.Revision);
        await using var security = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(postgres.GetConnectionString()).Options);
        Assert.Contains(await security.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "TugLifecycle" && e.Result == "tug_request_incomplete");
    }

    [Fact]
    public async Task Complete_draft_submits_once_with_receipt_replay_and_submitted_body_is_immutable() {
        using var host = Host(tugRead: true, tugWrite: true, tugOwner: true); using var c = Client(host); await Token(c);
        var cmd = CompleteDraft(DateTimeOffset.UtcNow.AddHours(1));
        var draft = (await (await Send(c, TugUrl, cmd)).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.Empty(draft.Missing);
        var key = Guid.NewGuid().ToString();
        var submitted = (await (await Lifecycle(c, draft.Id, "submit", new TugLifecycleCommand(draft.Version), key)).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.Equal("Submitted", submitted.State); Assert.Equal(2, submitted.Revision); Assert.NotEqual(draft.Version, submitted.Version); Assert.False(submitted.Replayed);
        var replay = (await (await Lifecycle(c, draft.Id, "submit", new TugLifecycleCommand(draft.Version), key)).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.True(replay.Replayed); Assert.Equal(submitted.Revision, replay.Revision); Assert.Equal(submitted.Version, replay.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await Lifecycle(c, draft.Id, "cancel", new TugLifecycleCommand(draft.Version, "FICTIONAL reason"), key)).StatusCode);
        // Stale base version and a second submit are conflicts, not silent successes.
        Assert.Equal(HttpStatusCode.Conflict, (await Lifecycle(c, draft.Id, "submit", new TugLifecycleCommand(draft.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Lifecycle(c, draft.Id, "submit", new TugLifecycleCommand(submitted.Version))).StatusCode);
        var edit = new TugSaveCommand(draft.Id, submitted.Version, cmd.Inputs with { Notes = "FICTIONAL change" });
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, $"tug-jobs/{draft.Id}?vessel={Tug.Id}", edit, update: true)).StatusCode);
        await using var db = Db();
        var revisions = await db.TugJobRevisions.Where(r => r.JobId == draft.Id).OrderBy(r => r.Revision).ToArrayAsync(Ct);
        Assert.Equal(["Draft", "Submitted"], revisions.Select(r => r.JobState));
        Assert.Equal(revisions[0].ContentJson, revisions[1].ContentJson);
        Assert.Single(await db.CommandReceipts.Where(r => r.Key == key && r.Action == "TugSubmit").ToArrayAsync(Ct));
        var audit = await db.AuditEvents.Where(e => e.ResourceId == draft.Id && e.EntityType == nameof(TugJob)).ToArrayAsync(Ct);
        Assert.Contains(audit, e => (e.ChangeSummary ?? "").Contains("\"JobState\"") && e.ChangeSummary!.Contains("Submitted"));
    }

    [Fact]
    public async Task Cancel_requires_reason_withholds_it_from_audit_hides_job_from_default_board_and_blocks_plans() {
        using var host = Host(tugRead: true, tugWrite: true); using var c = Client(host); await Token(c);
        var draft = (await (await Send(c, TugUrl, Draft())).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await Lifecycle(c, draft.Id, "cancel", new TugLifecycleCommand(draft.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Lifecycle(c, draft.Id, "cancel", new TugLifecycleCommand(draft.Version, "  "))).StatusCode);
        var cancelled = (await (await Lifecycle(c, draft.Id, "cancel", new TugLifecycleCommand(draft.Version, "FICTIONAL withdrawn by customer"))).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.Equal("Cancelled", cancelled.State);
        Assert.Equal(HttpStatusCode.Conflict, (await Lifecycle(c, draft.Id, "cancel", new TugLifecycleCommand(cancelled.Version, "FICTIONAL again"))).StatusCode);
        var board = await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}&limit=100", Ct);
        Assert.DoesNotContain(board!.Items, row => row.Request.Id == draft.Id);
        var only = await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}&state=Cancelled&limit=100", Ct);
        var row = Assert.Single(only!.Items, row => row.Request.Id == draft.Id);
        Assert.Equal("NoFurtherActionCancelled", row.NextAction); Assert.Equal("Cancelled", row.Request.State);
        await using var db = Db();
        var audit = await db.AuditEvents.Where(e => e.ResourceId == draft.Id).ToArrayAsync(Ct);
        Assert.DoesNotContain(audit, e => (e.ChangeSummary ?? "").Contains("withdrawn by customer"));
        Assert.Contains(audit, e => (e.ChangeSummary ?? "").Contains("\"CancelReason\"") && e.ChangeSummary!.Contains("ValuesWithheld"));
    }

    [Fact]
    public async Task Lifecycle_commands_deny_read_only_other_tenant_and_missing_csrf_without_effects() {
        using (var writer = Host(tugRead: true, tugWrite: true)) { using var w = Client(writer); await Token(w);
            var draft = (await (await Send(w, TugUrl, Draft())).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
            using var reader = Host(tugRead: true); using var r = Client(reader); await Token(r);
            Assert.Equal(HttpStatusCode.Forbidden, (await Lifecycle(r, draft.Id, "cancel", new TugLifecycleCommand(draft.Version, "FICTIONAL"))).StatusCode);
            using var anonymous = Client(writer);
            Assert.Contains((await Lifecycle(anonymous, draft.Id, "cancel", new TugLifecycleCommand(draft.Version, "FICTIONAL"))).StatusCode,
                new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest });
            using var spoof = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tug-jobs/{draft.Id}/cancel?vessel={Tug.Id}") {
                Content = new StringContent("{\"expectedVersion\":1,\"reason\":\"FICTIONAL\",\"state\":\"Submitted\"}", System.Text.Encoding.UTF8, "application/json") };
            spoof.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.BadRequest, (await w.SendAsync(spoof, Ct)).StatusCode);
            var other = seed.Vessels.First(v => v.TenantId != Tug.TenantId);
            using var cross = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tug-jobs/{draft.Id}/cancel?vessel={other.Id}") { Content = JsonContent.Create(new TugLifecycleCommand(draft.Version, "FICTIONAL")) };
            cross.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await w.SendAsync(cross, Ct)).StatusCode);
            await using var db = Db();
            Assert.Equal("Draft", (await db.TugJobs.SingleAsync(j => j.Id == draft.Id, Ct)).Status);
            Assert.DoesNotContain(await db.CommandReceipts.ToArrayAsync(Ct), x => x.Action is "TugSubmit" or "TugCancel");
        }
    }

    [Fact]
    public async Task Board_filters_by_state_and_server_relative_window_and_keeps_unknown_start_visible() {
        using var host = Host(tugRead: true, tugWrite: true, tugOwner: true); using var c = Client(host); await Token(c);
        var soon = (await (await Send(c, TugUrl, CompleteDraft(DateTimeOffset.UtcNow.AddHours(2)))).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        var later = (await (await Send(c, TugUrl, CompleteDraft(DateTimeOffset.UtcNow.AddHours(30)))).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        var unknown = (await (await Send(c, TugUrl, Draft())).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.Equal(HttpStatusCode.OK, (await Lifecycle(c, soon.Id, "submit", new TugLifecycleCommand(soon.Version))).StatusCode);
        var near = (await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}&window=next4h&limit=100", Ct))!.Items;
        Assert.Contains(near, x => x.Request.Id == soon.Id); Assert.DoesNotContain(near, x => x.Request.Id == later.Id);
        Assert.Contains(near, x => x.Request.Id == unknown.Id && x.WindowState == "MissingStart");
        var submitted = (await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}&state=Submitted&limit=100", Ct))!.Items;
        Assert.All(submitted, x => Assert.Equal("Submitted", x.Request.State)); Assert.Contains(submitted, x => x.Request.Id == soon.Id);
        var drafts = (await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}&state=Draft&limit=100", Ct))!.Items;
        Assert.Contains(drafts, x => x.Request.Id == later.Id && x.NextAction == "SubmitRequest");
        foreach (var bad in new[] { "state=Ready", "window=next1h", "state=Draft&state=Submitted" })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/v1/tug-job-board?vessel={Tug.Id}&{bad}", Ct)).StatusCode);
    }
}
