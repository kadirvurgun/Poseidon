using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// Real API host/antiforgery/owned PostgreSQL; all authored, never executed here.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid ReviewOperation = Guid.Parse("ab310000-0000-4000-8000-000000000001");
    private static readonly Guid ReviewRole = Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535");
    private void ProvisionSyntheticHuman(IServiceProvider services, Guid evidence, Guid control, string actorKind = "Human", Guid? newActor = null)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        var now = DateTimeOffset.UtcNow;
        settings.Memberships[0] = m with { ActorId = newActor ?? m.ActorId, ActorKind = actorKind,
            Permissions = [..GraphPermissions, "human.review.read", "human.review.submit", "human.verify", "human.decide"],
            HumanAuthorities = new[] { "human.review.submit", "human.verify", "human.decide" }.Select(cap =>
                new HumanAuthority(Guid.NewGuid(), SeedVessel.Id, evidence, control, ReviewOperation, ReviewRole, cap, now.AddDays(-1), now.AddDays(1))).ToArray() };
    }
    private async Task<(ControlEvidenceDto Evidence, ControlEvidenceDto Control)> ReviewInputs(HttpClient c)
    {
        var pin = new RequirementPin(RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id), 1, RegulatoryRegisterFixtures.SourceId(seed.Tenants[0].Id), 1);
        var control = await GraphSave(c, "controls", GraphCommand() with { Requirements = [pin] });
        var evidence = await GraphSave(c, "evidence", GraphCommand() with { Requirements = [pin], Links = [new("Control", control.Id, control.Revision, control.Version)] });
        return (evidence, control);
    }
    private HumanReviewCommand ReviewCommand(IServiceProvider services, ControlEvidenceDto ev, ControlEvidenceDto co, string capability = "human.review.submit")
    {
        var grant = services.GetRequiredService<FederationSettings>().Memberships[0].HumanAuthorities.Single(g => g.Capability == capability);
        return new(0, grant.Id, ReviewOperation, ev.Id, ev.Revision, ev.Version, co.Id, co.Revision, co.Version, null, null, null, "PENDING", "REQUEST_REVIEW", "FICTIONAL incomplete review request; no safety approval");
    }
    private string TraceRoute(ControlEvidenceDto ev, ControlEvidenceDto co) => $"/api/v1/regulatory-trace?vesselId={SeedVessel.Id}&evidenceId={ev.Id}&evidenceRevision={ev.Revision}&controlId={co.Id}&controlRevision={co.Revision}";
    private async Task<HumanReviewDto> PendingReview(HttpClient c, IServiceProvider services, ControlEvidenceDto ev, ControlEvidenceDto co, string? key = null)
    {
        using var result = await Send(c, GraphRoute("verifications"), ReviewCommand(services, ev, co), key);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode); return (await result.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
    }
    private async Task<HumanReviewCommand> CurrentReviewCommand(HttpClient c, IServiceProvider services, ControlEvidenceDto ev, ControlEvidenceDto co, HumanReviewDto prior, string cap)
    {
        var trace = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct);
        return ReviewCommand(services, ev, co, cap) with { ExpectedVersion = prior.Version, EvaluationId = prior.EvaluationId, VerificationId = prior.VerificationId, VerificationRevision = prior.VerificationRevision, EvidenceVersion = trace.GetProperty("evidenceVersion").GetUInt32(), ControlVersion = trace.GetProperty("controlVersion").GetUInt32() };
    }
    [Fact]
    public async Task Human_context_is_default_denied_and_server_appointment_not_claim_or_admin_is_required()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(TraceRoute(ev, co), Ct)).StatusCode);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id, "Service");
        using var service = await Send(c, GraphRoute("verifications"), ReviewCommand(host.Services, ev, co)); Assert.Equal(HttpStatusCode.Forbidden, service.StatusCode);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id, "AI");
        using var ai = await Send(c, GraphRoute("verifications"), ReviewCommand(host.Services, ev, co)); Assert.Equal(HttpStatusCode.Forbidden, ai.StatusCode);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id, "Unspecified");
        using var admin = await Send(c, GraphRoute("verifications"), ReviewCommand(host.Services, ev, co)); Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        await using var db = Db(); Assert.Empty(await db.HumanReviewEvents.ToArrayAsync(Ct));
    }
    [Theory]
    [InlineData("role")]
    [InlineData("operation")]
    [InlineData("vessel")]
    [InlineData("object")]
    [InlineData("expired")]
    [InlineData("duplicate")]
    public async Task Exact_role_assignment_object_operation_and_live_expiry_are_mandatory(string mismatch)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c); var (ev, co) = await ReviewInputs(c);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id); var command = ReviewCommand(host.Services, ev, co);
        var m = host.Services.GetRequiredService<FederationSettings>().Memberships[0]; var grant = m.HumanAuthorities[0];
        m.HumanAuthorities[0] = mismatch switch { "role" => grant with { RoleId = Guid.NewGuid() }, "operation" => grant with { OperationId = Guid.NewGuid() },
            "vessel" => grant with { VesselId = Guid.NewGuid() }, "object" => grant with { EvidenceId = Guid.NewGuid() }, "expired" => grant with { ValidUntil = DateTimeOffset.UtcNow.AddSeconds(-1) }, _ => grant };
        if (mismatch == "duplicate") m.HumanAuthorities = [..m.HumanAuthorities, grant];
        using var result = await Send(c, GraphRoute("verifications"), command); Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }
    [Fact]
    public async Task Pending_is_not_verified_and_submitter_or_evidence_author_cannot_self_verify()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c); var (ev, co) = await ReviewInputs(c);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id); var pending = await PendingReview(c, host.Services, ev, co);
        Assert.Equal("PENDING", pending.State); Assert.Null(pending.ValidUntil);
        var command = await CurrentReviewCommand(c, host.Services, ev, co, pending, "human.verify");
        using var denied = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), command with { State = "VERIFIED", ReasonCode = "HUMAN_VERIFIED" });
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode); Assert.Contains("separation_of_duties", await denied.Content.ReadAsStringAsync(Ct));
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id, newActor: Guid.NewGuid());
        command = await CurrentReviewCommand(c, host.Services, ev, co, pending, "human.verify");
        using var missing = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), command with { State = "VERIFIED", ReasonCode = "HUMAN_VERIFIED" });
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode); Assert.Contains("authoritative_evaluation_unavailable", await missing.Content.ReadAsStringAsync(Ct));
        using var rejected = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), command with { State = "REJECTED", ReasonCode = "HUMAN_REJECTED" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var trace = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct); Assert.False(trace.GetProperty("complete").GetBoolean()); Assert.Equal("REVIEW_REQUIRED", trace.GetProperty("disposition").GetString());
        Assert.Equal(2, trace.GetProperty("items").GetArrayLength()); Assert.Contains("SOURCE_ADMISSION_UNAVAILABLE", trace.GetRawText());
    }
    [Fact]
    public async Task Retry_keeps_exact_original_envelope_and_revocation_precedes_receipt_lookup()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c); var (ev, co) = await ReviewInputs(c);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id); var cmd = ReviewCommand(host.Services, ev, co); var key = Guid.NewGuid().ToString();
        using var first = await Send(c, GraphRoute("verifications"), cmd, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode); var bytes = await first.Content.ReadAsStringAsync(Ct);
        using var replay = await Send(c, GraphRoute("verifications"), cmd, key); Assert.Equal(bytes, await replay.Content.ReadAsStringAsync(Ct));
        using var reuse = await Send(c, GraphRoute("verifications"), cmd with { Rationale = "different fictional request" }, key); Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
        host.Services.GetRequiredService<FederationSettings>().Memberships[0].HumanAuthorities = [];
        using var revoked = await Send(c, GraphRoute("verifications"), cmd, key); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        await using var db = Db(); Assert.Single(await db.HumanReviewEvents.ToArrayAsync(Ct)); Assert.Single(await db.HumanReviewReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Real_csrf_and_strict_body_deny_actor_and_signature_spoofing_without_writes()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c); var (ev, co) = await ReviewInputs(c);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id);
        using var noToken = Client(host); using var csrf = await Send(noToken, GraphRoute("verifications"), ReviewCommand(host.Services, ev, co)); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        var json = JsonSerializer.Serialize(ReviewCommand(host.Services, ev, co), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var extra in new[] { "\"actorContext\":{}", "\"signature\":\"fabricated\"", "\"signatoryName\":\"synthetic\"", "\"expectedVersion\":0" }) {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + GraphRoute("verifications"));
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); req.Content = new StringContent(json[..^1] + "," + extra + "}", System.Text.Encoding.UTF8, "application/json");
            using var response = await c.SendAsync(req, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        await using var db = Db(); Assert.Empty(await db.HumanReviewEvents.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Decision_is_context_bound_and_final_human_signature_remains_unavailable()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c); var (ev, co) = await ReviewInputs(c);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id); var verification = await PendingReview(c, host.Services, ev, co);
        var cmd = await CurrentReviewCommand(c, host.Services, ev, co, verification, "human.review.submit");
        cmd = cmd with { ExpectedVersion = 0, VerificationId = verification.Id, VerificationRevision = verification.Revision };
        using var request = await Send(c, GraphRoute("decisions"), cmd); Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var decision = (await request.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        var final = await CurrentReviewCommand(c, host.Services, ev, co, decision, "human.decide");
        final = final with { VerificationId = verification.Id, VerificationRevision = verification.Revision, State = "APPROVED", ReasonCode = "HUMAN_VERIFIED" };
        using var blocked = await Send(c, GraphRoute($"decisions/{decision.Id}/events"), final); Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("source_admission_and_signature_unavailable", await blocked.Content.ReadAsStringAsync(Ct));
        using var wrong = await Send(c, GraphRoute("decisions"), cmd with { VerificationId = Guid.NewGuid() }); Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
    }
    [Fact]
    public async Task Trace_scope_before_events_hides_wrong_tenant_vessel_protected_anchor_and_historical_authority()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c); var (ev, co) = await ReviewInputs(c);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id); var pending = await PendingReview(c, host.Services, ev, co);
        var other = seed.Vessels.First(x => x.TenantId != seed.Tenants[0].Id);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(TraceRoute(ev, co).Replace(SeedVessel.Id.ToString(), other.Id.ToString()), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(TraceRoute(ev with { Id = Guid.NewGuid() }, co), Ct)).StatusCode);
        var history = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co) + "&asOf=" + Uri.EscapeDataString(pending.AsOf.ToString("O")), Ct);
        Assert.Empty(history.GetProperty("authorities").EnumerateArray()); Assert.True(history.GetProperty("historical").GetBoolean());
        Assert.DoesNotContain("snapshotJson", history.GetRawText()); Assert.DoesNotContain("subject", history.GetRawText());
        using var invalid = await c.GetAsync(TraceRoute(ev, co) + "&offset=0&offset=1", Ct); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
    [Fact]
    public async Task Noop_stale_race_and_receipt_failure_do_not_leave_partial_review_or_write_audit()
    {
        var fault = new RejectHumanReceipt(); await using var host = Host(interceptor: fault); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); ProvisionSyntheticHuman(host.Services, ev.Id, co.Id); var cmd = ReviewCommand(host.Services, ev, co);
        await using var before = Db(); var audits = await before.AuditEvents.CountAsync(Ct); fault.Armed = true;
        using var failure = await Send(c, GraphRoute("verifications"), cmd); Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        await using (var check = Db()) { Assert.Empty(await check.HumanReviewEvents.ToArrayAsync(Ct)); Assert.Empty(await check.HumanReviewReceipts.ToArrayAsync(Ct)); Assert.Equal(audits, await check.AuditEvents.CountAsync(Ct)); }
        fault.Armed = false;
        var key = Guid.NewGuid().ToString(); var races = await Task.WhenAll(Send(c, GraphRoute("verifications"), cmd, key), Send(c, GraphRoute("verifications"), cmd, key));
        foreach (var r in races) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); r.Dispose(); }
        using var stale = await Send(c, GraphRoute("verifications"), cmd); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var after = Db(); Assert.Single(await after.HumanReviewEvents.ToArrayAsync(Ct)); Assert.Single(await after.HumanReviewReceipts.ToArrayAsync(Ct));
    }
    private sealed class RejectHumanReceipt : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("INSERT INTO core.\"HumanReviewReceipts\"", StringComparison.Ordinal)) throw new Npgsql.NpgsqlException("Synthetic review receipt failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Independent_human_verification_with_synthetic_pinned_evaluation_never_issues_permit_and_source_edit_requires_review()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-human-review-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, scanner.Port); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
            var staged = await StagePng(c); using var upload = await PutPng(c, staged.Id); Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            var file = (await upload.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            Guid ruleId; long ruleRevision; Guid evaluationId;
            var source = RegulatoryRegisterFixtures.SourceId(seed.Tenants[0].Id); var requirement = RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id);
            // Explicit test-only immutable storage fixture, not source adoption, runtime
            // simulation promotion, real legal evaluation, human approval or seed data.
            await using (var db = Db()) {
                var head = await db.RuleDrafts.SingleAsync(x => x.Id == ApplicabilityFixtures.Id(db.TenantId), Ct); head.Revision++; ruleId = head.Id; ruleRevision = head.Revision;
                db.RuleRevisions.Add(new() { TenantId = db.TenantId, Id = ruleId, Revision = ruleRevision, Contract = "FICTIONAL_TEST_ONLY", Hash = RegisterStorage.Hash("fictional-rule"),
                    SourceKind = "Source", SourceId = source, SourceRevision = 1, RequirementKind = "Requirement", RequirementId = requirement, RequirementRevision = 1,
                    ActorId = actorId, RoleContext = ReviewRole.ToString(), ReasonCode = "AUTHORING_CORRECTION", RequestedLifecycle = "DRAFT", SavedAt = DateTimeOffset.UtcNow });
                evaluationId = Guid.NewGuid();
                db.ApplicabilityEvaluations.Add(new() { TenantId = db.TenantId, Id = evaluationId, VesselId = SeedVessel.Id, ProfileRevision = 1, ActorId = actorId,
                    SavedAt = DateTimeOffset.UtcNow, AsOf = DateTimeOffset.UtcNow, AdapterVersion = "FICTIONAL_TEST_ONLY", SnapshotJson = "{}", ResultJson = "{}",
                    SnapshotHash = RegisterStorage.Hash("synthetic-input"), RuleSetHash = RegisterStorage.Hash("synthetic-set") });
                db.EvaluationRules.Add(new() { TenantId = db.TenantId, EvaluationId = evaluationId, RuleId = ruleId, RuleRevision = ruleRevision }); await db.SaveChangesAsync(Ct);
            }
            var pin = new RequirementPin(requirement, 1, source, 1, ruleId, ruleRevision);
            var co = await GraphSave(c, "controls", GraphCommand() with { Requirements = [pin] }); var now = DateTimeOffset.UtcNow;
            var ev = await GraphSave(c, "evidence", GraphCommand(content: new(Title: "FICTIONAL verifier transport", Provenance: "Synthetic test evidence only", ObservedAt: now.AddMinutes(-5), ValidFrom: now.AddMinutes(-5), ValidUntil: now.AddHours(1), FreshUntil: now.AddMinutes(30))) with {
                File = new(file.Id, file.Revision!.Value, file.Checksum!), Requirements = [pin], Links = [new("Control", co.Id, co.Revision, co.Version)] });
            ProvisionSyntheticHuman(host.Services, ev.Id, co.Id);
            using var pendingResponse = await Send(c, GraphRoute("verifications"), ReviewCommand(host.Services, ev, co) with { EvaluationId = evaluationId }); Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
            var pending = (await pendingResponse.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
            ProvisionSyntheticHuman(host.Services, ev.Id, co.Id, newActor: Guid.NewGuid());
            var verify = await CurrentReviewCommand(c, host.Services, ev, co, pending, "human.verify");
            using var verified = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), verify with { State = "VERIFIED", ReasonCode = "HUMAN_VERIFIED", Rationale = "FICTIONAL independent fixture review, no legal sign-off" });
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode); var result = (await verified.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
            Assert.Equal("VERIFIED", result.State); Assert.Equal(evaluationId, result.EvaluationId); Assert.NotNull(result.ValidUntil);
            await using (var db = Db()) {
                var s = await db.RegisterDrafts.SingleAsync(x => x.Id == source && x.Kind == "Source", Ct); s.Revision++; s.SavedAt = DateTimeOffset.UtcNow;
                db.RegisterRevisions.Add(s.Snapshot(null, null)); await db.SaveChangesAsync(Ct);
            }
            var current = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct);
            Assert.False(current.GetProperty("complete").GetBoolean()); Assert.Contains("SUPERSEDED_REVIEW_REQUIRED", current.GetRawText());
            var history = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co) + "&asOf=" + Uri.EscapeDataString(result.AsOf.ToString("O")), Ct);
            Assert.Contains("VERIFIED", history.GetRawText()); Assert.DoesNotContain("SUPERSEDED_REVIEW_REQUIRED", history.GetRawText());
            var supersede = await CurrentReviewCommand(c, host.Services, ev, co, result, "human.verify");
            using var newer = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), supersede with { State = "SUPERSEDED", ReasonCode = "INPUT_SUPERSEDED" }); Assert.Equal(HttpStatusCode.OK, newer.StatusCode);
            await using var check = Db(); Assert.Equal(3, await check.HumanReviewEvents.CountAsync(x => x.Id == pending.Id, Ct));
            Assert.Equal("VERIFIED", (await check.HumanReviewEvents.SingleAsync(x => x.Id == pending.Id && x.Revision == 2, Ct)).State);
            Assert.DoesNotContain(await check.AuditEvents.ToArrayAsync(Ct), a => a.ChangeSummary.Contains("FICTIONAL independent fixture review", StringComparison.Ordinal));
        } finally { foreach (var file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root, false); }
    }
}
