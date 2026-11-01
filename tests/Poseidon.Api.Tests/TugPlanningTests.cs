using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// AUTHORED ONLY: actual API/antiforgery/owned PostgreSQL; no provider execution here.
public sealed partial class DirectoryWorkspaceTests
{
    private TugPlanningPolicy SyntheticPlanning(params Guid[] tugs) => new() { Version = "synthetic-plan-v1", SourceVersion = TugDraftPlanning.SourceVersion,
        NotBefore = DateTimeOffset.UnixEpoch, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
        Scopes = [new(Tug.TenantId, actorId, Tug.Id, tugs.Length == 0 ? [Tug.Id] : tugs)],
        Types = [new("SYNTHETIC-TYPE", "Kurgusal tip", "Fictional type")],
        Contexts = [new(Tug.TenantId, Tug.Id, "SYNTHETIC-TYPE", "synthetic-source", 1, DateTimeOffset.UtcNow.AddMinutes(-1))] };
    private WebApplicationFactory<Program> PlanningHost(TugPlanningPolicy? policy = null, bool write = true, bool scoped = false) =>
        Host(tugRead: true, tugWrite: true, technicalRead: true, technicalWrite: true, manage: !scoped, read: !scoped)
        .WithWebHostBuilder(b => {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Authentication:Oidc:Memberships:0:Permissions:35"] = write ? "tug.plan.write" : "unused",
                ["Authentication:Oidc:Memberships:0:Permissions:36"] = "maintenance.observation.write",
                ["Authentication:Oidc:Memberships:0:VesselGrants:0:VesselId"] = Tug.Id.ToString(),
                ["Authentication:Oidc:Memberships:0:VesselGrants:0:CanEdit"] = "true"
            }));
            b.ConfigureTestServices(s => s.AddSingleton(policy ?? SyntheticPlanning()));
        });
    private string PlanningUrl(Guid id, string suffix = "") => $"tug-jobs/{id}/plan{suffix}?vessel={Tug.Id}";
    private async Task<TugRequestDto> PlanningJob(HttpClient c) {
        var response = await Send(c, TugUrl, Draft()); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
    }
    private async Task<TugPlanDto> ChangePlan(HttpClient c, TugRequestDto j, TugPlanDto? p, TugPlanChange change, string? key = null) {
        var response = await Send(c, PlanningUrl(j.Id), new TugPlanCommand(p?.Version, j.Version, "synthetic-plan-v1", change), key, update: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<TugPlanDto>(Ct))!;
    }
    [Fact]
    public async Task Draft_window_assign_remove_history_recovery_and_board_use_actual_plan_without_changing_request() {
        using var host = PlanningHost(); using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        var start = DateTimeOffset.UtcNow.AddHours(1); var key = Guid.NewGuid().ToString();
        var cmd = new TugPlanCommand(null, job.Version, "synthetic-plan-v1", new("Window", start, null));
        var response = await Send(c, PlanningUrl(job.Id), cmd, key, update: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var first = (await response.Content.ReadFromJsonAsync<TugPlanDto>(Ct))!;
        var assigned = await ChangePlan(c, job, first, new("Assign", TugId: Tug.Id));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"UPDATE core.\"Vessels\" SET \"Kind\"='Ship' WHERE \"TenantId\"='{Tug.TenantId}' AND \"Id\"='{Tug.Id}'"));
        var removed = await ChangePlan(c, job, assigned, new("Remove", TugId: Tug.Id)); Assert.Empty(removed.Inputs.AssignedTugs);
        var replay = (await (await Send(c, PlanningUrl(job.Id), cmd, key, update: true)).Content.ReadFromJsonAsync<TugPlanDto>(Ct))!;
        Assert.True(replay.Replayed); Assert.Equal(first.Version, replay.Version); Assert.Equal(1, replay.Revision); Assert.Null(replay.Inputs.PlannedEnd);
        var historical = await c.GetFromJsonAsync<TugPlanDto>("/api/v1/" + PlanningUrl(job.Id, "/history/2"), Ct);
        Assert.Equal(assigned.Version, historical!.Version); Assert.Equal(new[] { Tug.Id }, historical.Inputs.AssignedTugs);
        var board = await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}", Ct);
        var actual = board!.Items.Single(r => r.Request.Id == job.Id); Assert.Equal(3, actual.DraftPlan!.Revision);
        Assert.Null(actual.Request.Inputs.RequestedStart); Assert.Equal(start, actual.DraftPlan.Inputs.PlannedStart);
        Assert.Null(actual.AvailableBollardPull); Assert.Equal("Unknown", actual.Readiness); Assert.Contains("NoOperationalApproval", actual.PlannedTimeSource);
        await using var db = Db(); Assert.Equal(1, (await db.TugJobs.SingleAsync(j => j.Id == job.Id, Ct)).Revision);
        Assert.Equal(3, await db.TugDraftPlanRevisions.CountAsync(p => p.JobId == job.Id, Ct));
        Assert.Contains(await db.AuditEvents.ToArrayAsync(Ct), a => a.EntityType == nameof(TugDraftPlan) && a.ResourceId == job.Id && a.VesselId == Tug.Id && a.RoleContext != null);
    }
    [Fact]
    public async Task Explicit_reassignment_creates_new_lineage_and_current_read_revocation_withholds_prior_references() {
        var other = Guid.NewGuid();
        await using (var db = Db()) { db.Vessels.Add(new() { TenantId = Tug.TenantId, Id = other, CompanyId = Tug.CompanyId, Code = "SYN-TUG-2", Name = "FICTIONAL tug", Kind = VesselKind.Tug }); await db.SaveChangesAsync(Ct); }
        var policy = SyntheticPlanning(Tug.Id, other); TugRequestDto job;
        using (var host = PlanningHost(policy)) {
            using var c = Client(host); await Token(c); job = await PlanningJob(c);
            var assigned = await ChangePlan(c, job, null, new("Assign", TugId: Tug.Id));
            var reassigned = await ChangePlan(c, job, assigned, new("Reassign", TugId: Tug.Id, ReplacementTugId: other));
            Assert.Equal(new[] { other }, reassigned.Inputs.AssignedTugs);
            Assert.Equal(new[] { Tug.Id }, (await c.GetFromJsonAsync<TugPlanDto>("/api/v1/" + PlanningUrl(job.Id, "/history/1"), Ct))!.Inputs.AssignedTugs);
        }
        using var scoped = PlanningHost(policy, scoped: true); using var restricted = Client(scoped);
        Assert.Equal(HttpStatusCode.Forbidden, (await restricted.GetAsync("/api/v1/" + PlanningUrl(job.Id), Ct)).StatusCode);
        var history = await restricted.GetStringAsync("/api/v1/" + PlanningUrl(job.Id, "/history"), Ct);
        Assert.DoesNotContain(other.ToString(), history); Assert.DoesNotContain("\"revision\":2", history);
        Assert.Equal(HttpStatusCode.NotFound, (await restricted.GetAsync("/api/v1/" + PlanningUrl(job.Id, "/history/2"), Ct)).StatusCode);
    }
    [Fact]
    public async Task Current_policy_and_membership_are_checked_before_exact_ordinary_receipt_recovery_in_same_session() {
        var policy = SyntheticPlanning(); using var host = PlanningHost(policy); using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        var key = Guid.NewGuid().ToString(); var cmd = new TugPlanCommand(null, job.Version, policy.Version, new("Assign", TugId: Tug.Id));
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlanningUrl(job.Id), cmd, key, update: true)).StatusCode);
        policy.Scopes = [];
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, PlanningUrl(job.Id), cmd, key, update: true)).StatusCode);
        policy.Scopes = SyntheticPlanning().Scopes;
        var membership = host.Services.GetRequiredService<FederationSettings>().Memberships[0];
        membership.Permissions[Array.IndexOf(membership.Permissions, "tug.plan.write")] = "unused";
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, PlanningUrl(job.Id), cmd, key, update: true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/" + PlanningUrl(job.Id), Ct)).StatusCode);
    }
    [Fact]
    public async Task Concurrent_different_commands_have_one_effect_and_losing_command_cannot_overwrite() {
        using var host = PlanningHost(); using var a = Client(host); using var b = Client(host); await Token(a); await Token(b); var job = await PlanningJob(a);
        var time = DateTimeOffset.UtcNow.AddHours(2);
        var commands = new[] { new TugPlanCommand(null, job.Version, "synthetic-plan-v1", new("Window", time)), new TugPlanCommand(null, job.Version, "synthetic-plan-v1", new("Assign", TugId: Tug.Id)) };
        var results = await Task.WhenAll(Send(a, PlanningUrl(job.Id), commands[0], update: true), Send(b, PlanningUrl(job.Id), commands[1], update: true));
        Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.OK)); Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = Db(); Assert.Single(await db.TugDraftPlanRevisions.Where(p => p.JobId == job.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Duplicate_race_and_restart_recover_original_xmin_and_single_revision() {
        var key = Guid.NewGuid().ToString(); TugRequestDto job; TugPlanCommand cmd; TugPlanDto saved;
        using (var host = PlanningHost()) {
            using var a = Client(host); using var b = Client(host); await Token(a); await Token(b); job = await PlanningJob(a);
            cmd = new(null, job.Version, "synthetic-plan-v1", new("Assign", TugId: Tug.Id));
            var replies = await Task.WhenAll(Send(a, PlanningUrl(job.Id), cmd, key, update: true), Send(b, PlanningUrl(job.Id), cmd, key, update: true));
            Assert.Contains(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.All(replies, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
            saved = (await (await Send(a, PlanningUrl(job.Id), cmd, key, update: true)).Content.ReadFromJsonAsync<TugPlanDto>(Ct))!;
        }
        using var restart = PlanningHost(); using var retry = Client(restart); await Token(retry);
        var recovered = (await (await Send(retry, PlanningUrl(job.Id), cmd, key, update: true)).Content.ReadFromJsonAsync<TugPlanDto>(Ct))!;
        Assert.True(recovered.Replayed); Assert.Equal(saved.Version, recovered.Version);
        await using var db = Db(); Assert.Single(await db.TugDraftPlanRevisions.Where(p => p.JobId == job.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Receipt_failure_rolls_back_plan_links_revision_and_attributed_write_audit() {
        using var host = PlanningHost(); using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        int before; await using (var db = Db()) before = await db.AuditEvents.CountAsync(a => a.ResourceId == job.Id, Ct);
        await Sql("CREATE TRIGGER reject_planning_receipt BEFORE INSERT ON core.\"CommandReceipts\" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
        var key = Guid.NewGuid().ToString(); var cmd = new TugPlanCommand(null, job.Version, "synthetic-plan-v1", new("Assign", TugId: Tug.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, PlanningUrl(job.Id), cmd, key, update: true)).StatusCode);
        await using (var db = Db()) {
            Assert.False(await db.TugDraftPlans.AnyAsync(p => p.JobId == job.Id, Ct)); Assert.False(await db.TugDraftPlanRevisions.AnyAsync(p => p.JobId == job.Id, Ct));
            Assert.False(await db.TugDraftPlanTugs.AnyAsync(p => p.JobId == job.Id, Ct)); Assert.Equal(before, await db.AuditEvents.CountAsync(a => a.ResourceId == job.Id, Ct));
        }
        await Sql("DROP TRIGGER reject_planning_receipt ON core.\"CommandReceipts\";");
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlanningUrl(job.Id), cmd, key, update: true)).StatusCode);
    }
    [Fact]
    public async Task Overlap_adapter_reads_beyond_board_page_and_never_turns_geometry_into_clearance() {
        using var host = PlanningHost(); using var c = Client(host); await Token(c);
        var time = DateTimeOffset.UtcNow.AddHours(2); var first = await PlanningJob(c); var second = await PlanningJob(c);
        var p = await ChangePlan(c, first, null, new("Window", time, time.AddHours(2))); await ChangePlan(c, first, p, new("Assign", TugId: Tug.Id));
        var q = await ChangePlan(c, second, null, new("Window", time.AddHours(1), time.AddHours(3))); await ChangePlan(c, second, q, new("Assign", TugId: Tug.Id));
        var board = await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}&limit=1", Ct);
        var row = Assert.Single(board!.Items); Assert.Equal("HumanReviewRequired", row.PlanningEvidence!.ConflictState);
        Assert.Single(row.PlanningEvidence.Conflicts); Assert.Equal("Intersecting", row.PlanningEvidence.Conflicts[0].Relation);
        Assert.Null(row.AvailableBollardPull); Assert.Equal("Unknown", row.Readiness);
        var detail = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(first.Id), Ct);
        Assert.Contains("OperationalEvidenceUnavailable", detail.GetProperty("evidence").GetProperty("coverage").GetString());
    }
    [Fact]
    public async Task Bp_adapter_consumes_actual_normalized_observation_rechecks_revocation_and_never_sums_unverified_capacity() {
        var policy = SyntheticPlanning(); policy.BpSources = [new(Tug.TenantId, Tug.Id, Tug.Id, "synthetic-bp", "bp-v1", 3600)];
        using var host = PlanningHost(policy).WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(new MaintenanceObservationSettings {
            Sources = [new(Tug.TenantId, Tug.Id, Tug.Id, "synthetic-bp", "bp-v1", "Meter", "tonnes", 3600, 2)] })));
        using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        var reading = new MaintenanceObservationCommand(Guid.NewGuid(), "synthetic-bp", "bp-v1", 1, "Meter", "tonnes", "45.01", DateTimeOffset.UtcNow.AddMinutes(-1));
        var path = $"equipment-hierarchy/{Tug.Id}/{Tug.Id}/observations";
        Assert.Equal(HttpStatusCode.OK, (await Send(c, path, reading)).StatusCode); await ChangePlan(c, job, null, new("Assign", TugId: Tug.Id));
        var detail = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id), Ct); var bp = detail.GetProperty("evidence").GetProperty("bp")[0];
        Assert.Equal("DeclaredUnverified", bp.GetProperty("state").GetString()); Assert.Equal("45.01", bp.GetProperty("declaredValue").GetString());
        Assert.Equal(reading.Id, bp.GetProperty("observationId").GetGuid()); Assert.Equal(JsonValueKind.Null, detail.GetProperty("evidence").GetProperty("availableBollardPull").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, path + $"/{reading.Id}/revoke", new { })).StatusCode);
        var revoked = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id), Ct);
        Assert.Equal("Revoked", revoked.GetProperty("evidence").GetProperty("bp")[0].GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, revoked.GetProperty("evidence").GetProperty("bp")[0].GetProperty("declaredValue").ValueKind);
    }
    [Theory]
    [InlineData("Missing")]
    [InlineData("Stale")]
    [InlineData("Future")]
    [InlineData("Unknown")]
    [InlineData("PolicyChanged")]
    public async Task Capacity_adapter_keeps_missing_stale_future_unknown_and_changed_source_distinct(string expected) {
        var policy = SyntheticPlanning(); policy.BpSources = [new(Tug.TenantId, Tug.Id, Tug.Id, "synthetic-bp", "bp-v1", 3600)];
        var sources = new MaintenanceObservationSettings { Sources = [new(Tug.TenantId, Tug.Id, Tug.Id, "synthetic-bp", "bp-v1", "Meter", "tonnes", 3600, 4)] };
        using var host = PlanningHost(policy).WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(sources)));
        using var c = Client(host); await Token(c); var job = await PlanningJob(c); await ChangePlan(c, job, null, new("Assign", TugId: Tug.Id));
        if (expected != "Missing") {
            var observed = DateTimeOffset.UtcNow.AddSeconds(expected == "Stale" ? -7200 : expected == "Future" ? 3600 : -60);
            var reading = new MaintenanceObservationCommand(Guid.NewGuid(), "synthetic-bp", "bp-v1", 1, "Meter", "tonnes", expected == "Unknown" ? "0.001" : "45.01", observed);
            Assert.Equal(HttpStatusCode.OK, (await Send(c, $"equipment-hierarchy/{Tug.Id}/{Tug.Id}/observations", reading)).StatusCode);
        }
        if (expected == "PolicyChanged") {
            policy.BpSources = [policy.BpSources[0] with { PolicyVersion = "bp-v2" }];
            sources.Sources = [sources.Sources[0] with { Version = "bp-v2" }];
        }
        var detail = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id), Ct);
        Assert.Equal(expected, detail.GetProperty("evidence").GetProperty("bp")[0].GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("evidence").GetProperty("availableBollardPull").ValueKind);
    }
    [Fact]
    public async Task Strict_command_shape_csrf_and_idempotency_binding_prevent_unaudited_partial_writes() {
        using var host = PlanningHost(); using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        foreach (var extra in new[] { ",\"actorId\":\"caller-owned\"", ",\"baseVersion\":null" }) {
            var body = $"{{\"baseVersion\":null,\"requestVersion\":{job.Version},\"policyVersion\":\"synthetic-plan-v1\",\"change\":{{\"operation\":\"Window\"}}{extra}}}";
            using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/" + PlanningUrl(job.Id)) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); Assert.Equal(HttpStatusCode.BadRequest, (await c.SendAsync(request, Ct)).StatusCode);
        }
        var key = Guid.NewGuid().ToString(); var cmd = new TugPlanCommand(null, job.Version, "synthetic-plan-v1", new("Window"));
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, PlanningUrl(job.Id), cmd, key, update: true)).StatusCode);
        await using (var db = Db()) Assert.False(await db.TugDraftPlans.AnyAsync(p => p.JobId == job.Id, Ct));
        await Token(c); Assert.Equal(HttpStatusCode.OK, (await Send(c, PlanningUrl(job.Id), cmd, key, update: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, PlanningUrl(job.Id), cmd with { Change = new("Assign", TugId: Tug.Id) }, key, update: true)).StatusCode);
        await using var verify = Db(); Assert.Single(await verify.TugDraftPlanRevisions.Where(p => p.JobId == job.Id).ToArrayAsync(Ct));
        Assert.DoesNotContain(await verify.AuditEvents.ToArrayAsync(Ct), a => (a.ChangeSummary ?? "").Contains(key) || (a.ChangeSummary ?? "").Contains("caller-owned"));
    }
    [Theory]
    [InlineData("2026-11-01T12:00:00")]
    [InlineData("2026-11-01T12:00:00Z\n")]
    public async Task Zone_free_or_trailing_input_is_rejected_without_mutation(string instant) {
        using var host = PlanningHost(); using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        var body = JsonSerializer.Serialize(new { baseVersion = (uint?)null, requestVersion = job.Version, policyVersion = "synthetic-plan-v1", change = new { operation = "Window", plannedStart = instant } });
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/" + PlanningUrl(job.Id)) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.SendAsync(request, Ct)).StatusCode);
        await using var db = Db(); Assert.False(await db.TugDraftPlans.AnyAsync(p => p.JobId == job.Id, Ct));
    }
    [Fact]
    public async Task Type_context_is_source_versioned_scoped_and_removed_when_operator_policy_expires() {
        var policy = SyntheticPlanning(); using var host = PlanningHost(policy); using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        var context = await c.GetFromJsonAsync<JsonElement>($"/api/v1/tug-command?vessel={Tug.Id}", Ct);
        Assert.Equal("SYNTHETIC-TYPE", context.GetProperty("tugType").GetString());
        Assert.Equal("synthetic-plan-v1", context.GetProperty("tugTypePolicyVersion").GetString());
        Assert.Equal("synthetic-source", context.GetProperty("tugTypeContext").GetProperty("sourceReference").GetString());
        var options = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id, "/options"), Ct);
        Assert.True(options.GetProperty("canWrite").GetBoolean()); Assert.Single(options.GetProperty("tugs").EnumerateArray());
        policy.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var revoked = await c.GetFromJsonAsync<JsonElement>($"/api/v1/tug-command?vessel={Tug.Id}", Ct);
        Assert.Equal("Unavailable", revoked.GetProperty("tugType").GetString()); Assert.Empty(revoked.GetProperty("tugTypeOptions").EnumerateArray());
        var unavailable = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id, "/options"), Ct);
        Assert.False(unavailable.GetProperty("canWrite").GetBoolean()); Assert.Empty(unavailable.GetProperty("tugs").EnumerateArray());
    }
    [Fact]
    public async Task Changed_intake_requires_review_and_explicit_plan_rebase_without_rewriting_old_snapshot() {
        using var host = PlanningHost(); using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        var plan = await ChangePlan(c, job, null, new("Window", DateTimeOffset.UnixEpoch, null));
        var intake = new TugSaveCommand(job.Id, job.Version, job.Inputs with { RequiredBollardPull = "46.02" });
        var response = await Send(c, $"tug-jobs/{job.Id}?vessel={Tug.Id}", intake, update: true); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changed = (await response.Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        var current = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id), Ct); Assert.Equal("RequestChangedSincePlan", current.GetProperty("requestState").GetString());
        var stale = new TugPlanCommand(plan.Version, job.Version, "synthetic-plan-v1", new("Assign", TugId: Tug.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, PlanningUrl(job.Id), stale, update: true)).StatusCode);
        var next = await ChangePlan(c, changed, plan, new("Assign", TugId: Tug.Id)); Assert.Equal(changed.Revision, next.RequestRevision);
        var old = await c.GetFromJsonAsync<TugPlanDto>("/api/v1/" + PlanningUrl(job.Id, "/history/1"), Ct);
        Assert.Equal(job.Revision, old!.RequestRevision); Assert.Empty(old.Inputs.AssignedTugs); Assert.Equal(plan.Version, old.Version);
    }
    [Fact]
    public async Task Missing_configuration_generic_read_spoofed_refs_and_changed_request_version_fail_closed() {
        using var host = PlanningHost(new()); using var c = Client(host); await Token(c); var job = await PlanningJob(c);
        var cmd = new TugPlanCommand(null, job.Version, "synthetic-plan-v1", new("Assign", TugId: Tug.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, PlanningUrl(job.Id), cmd, update: true)).StatusCode);
        using var configured = PlanningHost(); using var writer = Client(configured); await Token(writer);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(writer, PlanningUrl(job.Id), cmd with { Change = new("Assign", TugId: SeedVessel.Id) }, update: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(writer, PlanningUrl(job.Id), cmd with { RequestVersion = job.Version + 1 }, update: true)).StatusCode);
        using var viewer = PlanningHost(write: false); using var read = Client(viewer); await Token(read);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(read, PlanningUrl(job.Id), cmd, update: true)).StatusCode);
        var foreign = seed.Vessels.First(v => v.TenantId != Tug.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await writer.GetAsync($"/api/v1/tug-jobs/{job.Id}/plan?vessel={foreign.Id}", Ct)).StatusCode);
    }
}
