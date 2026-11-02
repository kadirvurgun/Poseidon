using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// Actual host/CSRF/owned PostgreSQL. Synthetic actors exercise software only.
public sealed partial class DirectoryWorkspaceTests
{
    private void RecordingAppointment(IServiceProvider services, Guid evidence, Guid control, string role = "DPA")
    {
        ProvisionSyntheticHuman(services, evidence, control);
        var m = services.GetRequiredService<FederationSettings>().Memberships[0];
        var i = Array.FindIndex(m.HumanAuthorities, x => x.Capability == "human.decide");
        m.HumanAuthorities[i] = m.HumanAuthorities[i] with { Recording = new("FICTIONAL_ASSIGNED_DECISION", "Control", control,
            role, "HumanRecording", "15.01/paragraph:99;18.01/Backlog/row:32") };
    }
    private async Task<HumanReviewCommand> RecordingCommand(HttpClient c, IServiceProvider services, ControlEvidenceDto ev, ControlEvidenceDto co, HumanReviewDto verification)
    {
        var cmd = await CurrentReviewCommand(c, services, ev, co, verification, "human.decide");
        return cmd with { ExpectedVersion = 0, VerificationId = verification.Id, VerificationRevision = verification.Revision,
            ExpectedProfileRevision = (await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct)).GetProperty("profileRevision").GetInt64(),
            State = "RECORDED", ReasonCode = "HUMAN_DECIDED", Rationale = "FICTIONAL private rationale, software test only",
            Recording = new("FICTIONAL_ASSIGNED_DECISION", "Control", co.Id, "FICTIONAL human decision text", "FICTIONAL trigger", "Incomplete unqualified context explicitly retained") };
    }
    [Theory]
    [InlineData("Master")]
    [InlineData("DPA")]
    public async Task Appointed_human_records_text_without_legal_signature_or_promotion_and_replay_is_exact(string role)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id, role);
        var verification = await PendingReview(c, host.Services, ev, co);
        var cmd = await RecordingCommand(c, host.Services, ev, co, verification); var key = Guid.NewGuid().ToString();
        using var first = await Send(c, GraphRoute("decisions"), cmd, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var bytes = await first.Content.ReadAsStringAsync(Ct); var saved = JsonSerializer.Deserialize<HumanReviewDto>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("RECORDED", saved.State); Assert.Equal(cmd.Recording, saved.Recording!.Content);
        Assert.Equal("UNASSESSED", saved.Recording.Qualification); Assert.Equal(role, saved.Recording.Policy.AuthorityRole);
        Assert.Equal(verification.Id, saved.VerificationId); Assert.Equal(verification.Revision, saved.VerificationRevision);
        using var replay = await Send(c, GraphRoute("decisions"), cmd, key); Assert.Equal(bytes, await replay.Content.ReadAsStringAsync(Ct));
        var trace = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct);
        Assert.False(trace.GetProperty("complete").GetBoolean()); Assert.Equal("REVIEW_REQUIRED", trace.GetProperty("disposition").GetString());
        Assert.Contains("RECORDED", trace.GetRawText()); Assert.Contains("PENDING", trace.GetRawText());
        // Same human may make an assigned decision; verifier independence remains
        // enforced separately. No universal decision-maker SoD is invented.
        var terminal = await CurrentReviewCommand(c, host.Services, ev, co, saved, "human.decide");
        using var overwrite = await Send(c, GraphRoute($"decisions/{saved.Id}/events"), terminal with { State = "RECORDED", ReasonCode = "HUMAN_DECIDED", Recording = cmd.Recording });
        Assert.Equal(HttpStatusCode.Conflict, overwrite.StatusCode);
        await using var db = Db(); Assert.Single(await db.HumanReviewEvents.Where(x => x.Kind == "Decision").ToArrayAsync(Ct));
        Assert.DoesNotContain(await db.AuditEvents.ToArrayAsync(Ct), x => x.ChangeSummary.Contains(cmd.Rationale) || x.ChangeSummary.Contains(cmd.Recording!.HumanDecision));
        Assert.Equal("PENDING", (await db.HumanReviewEvents.SingleAsync(x => x.Id == verification.Id, Ct)).State);
    }
    [Theory]
    [InlineData("AI")]
    [InlineData("Service")]
    [InlineData("Unspecified")]
    [InlineData("admin")]
    [InlineData("tug-role")]
    [InlineData("role")]
    [InlineData("type")]
    [InlineData("subject")]
    [InlineData("operation")]
    [InlineData("vessel")]
    [InlineData("tenant")]
    [InlineData("mode")]
    [InlineData("revoked")]
    [InlineData("unresolved")]
    public async Task Recording_requires_actual_human_role_type_subject_operation_and_current_scope(string denial)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var verification = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, verification);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        if (denial is "AI" or "Service" or "Unspecified") settings.Memberships[0] = m with { ActorKind = denial };
        var i = Array.FindIndex(m.HumanAuthorities, x => x.Capability == "human.decide"); var g = m.HumanAuthorities[i];
        m.HumanAuthorities[i] = denial switch {
            "tug-role" => g with { Recording = g.Recording! with { AuthorityRole = "Tug Master" } },
            "admin" => g with { Recording = g.Recording! with { AuthorityRole = "Admin" } },
            "role" => g with { RoleId = Guid.NewGuid() }, "operation" => g with { OperationId = Guid.NewGuid() },
            "vessel" => g with { VesselId = Guid.NewGuid() }, "revoked" => g with { ValidUntil = DateTimeOffset.UtcNow.AddMinutes(-1) },
            "unresolved" => g with { Recording = null }, _ => g };
        if (denial == "type") cmd = cmd with { Recording = cmd.Recording! with { DecisionType = "UNRESOLVED" } };
        if (denial == "subject") cmd = cmd with { Recording = cmd.Recording! with { SubjectId = Guid.NewGuid() } };
        if (denial == "tenant") settings.Memberships[0] = m with { TenantId = seed.Tenants[1].Id };
        if (denial == "mode") settings.Memberships[0] = m with { Permissions = [..m.Permissions.Where(p => p != "mode.cross"), "mode.tug"] };
        using var response = await Send(c, GraphRoute("decisions"), cmd);
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        await using var db = Db(); Assert.Empty(await db.HumanReviewEvents.Where(x => x.Kind == "Decision").ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Pending_decision_can_be_finalized_as_a_human_record_without_rewriting_its_request()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var v = await PendingReview(c, host.Services, ev, co);
        var request = await CurrentReviewCommand(c, host.Services, ev, co, v, "human.review.submit");
        request = request with { ExpectedVersion = 0, VerificationId = v.Id, VerificationRevision = v.Revision };
        using var pendingResponse = await Send(c, GraphRoute("decisions"), request); Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        var pending = (await pendingResponse.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        var cmd = await RecordingCommand(c, host.Services, ev, co, v);
        using var final = await Send(c, GraphRoute($"decisions/{pending.Id}/events"), cmd with { ExpectedVersion = pending.Version });
        Assert.Equal(HttpStatusCode.OK, final.StatusCode);
        await using var db = Db(); var history = await db.HumanReviewEvents.Where(x => x.Id == pending.Id).OrderBy(x => x.Revision).ToArrayAsync(Ct);
        Assert.Equal(2, history.Length); Assert.Equal("PENDING", history[0].State); Assert.Null(history[0].DecisionJson); Assert.Equal("RECORDED", history[1].State);
        Assert.NotEqual(history[0].ContextHash, history[1].ContextHash);
    }
    [Theory]
    [InlineData("Evidence")]
    [InlineData("Control")]
    [InlineData("Profile")]
    public async Task Changed_material_input_after_recording_rejects_receipt_replay_and_preserves_history(string input)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var v = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, v); var key = Guid.NewGuid().ToString();
        using var first = await Send(c, GraphRoute("decisions"), cmd, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await using (var db = Db()) {
            if (input == "Profile") {
                var profile = await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct); profile.Revision++; profile.SavedAt = DateTimeOffset.UtcNow;
                db.RegulatoryDraftRevisions.Add(profile.Snapshot());
            } else {
                var id = input == "Evidence" ? ev.Id : co.Id;
                var head = await db.ControlEvidenceHeads.SingleAsync(x => x.Id == id && x.Kind == input, Ct); head.Revision++;
                var old = await db.ControlEvidenceRevisions.AsNoTracking().SingleAsync(x => x.Id == id && x.Kind == input && x.Revision == head.Revision - 1, Ct);
                old.Revision = head.Revision; old.SavedAt = DateTimeOffset.UtcNow; old.ReasonCode = "AUTHORING_CORRECTION"; db.ControlEvidenceRevisions.Add(old);
            }
            await db.SaveChangesAsync(Ct);
        }
        using var replay = await Send(c, GraphRoute("decisions"), cmd, key); Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        var trace = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct); Assert.Contains("SUPERSEDED_REVIEW_REQUIRED", trace.GetRawText());
        await using var check = Db(); Assert.Single(await check.HumanReviewEvents.Where(x => x.Kind == "Decision" && x.State == "RECORDED").ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Stale_profile_source_and_evidence_context_conflict_and_cannot_replay_as_current()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var v = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, v);
        using var wrongProfile = await Send(c, GraphRoute("decisions"), cmd with { ExpectedProfileRevision = cmd.ExpectedProfileRevision + 1 });
        Assert.Equal(HttpStatusCode.Conflict, wrongProfile.StatusCode);
        var key = Guid.NewGuid().ToString(); using var first = await Send(c, GraphRoute("decisions"), cmd, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var saved = (await first.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        // Change the source through audited EF and retain the old immutable revision.
        await using (var db = Db()) {
            var pin = await db.ControlEvidenceRequirements.SingleAsync(x => x.Kind == "Control" && x.Id == co.Id && x.Revision == co.Revision, Ct);
            var source = await db.RegisterDrafts.SingleAsync(x => x.Kind == "Source" && x.Id == pin.SourceId, Ct);
            source.Revision++; source.SavedAt = DateTimeOffset.UtcNow; db.RegisterRevisions.Add(source.Snapshot(null, null)); await db.SaveChangesAsync(Ct);
        }
        using var replay = await Send(c, GraphRoute("decisions"), cmd, key); Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        var current = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct); Assert.Contains("SUPERSEDED_REVIEW_REQUIRED", current.GetRawText());
        var historical = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co) + "&asOf=" + Uri.EscapeDataString(saved.AsOf.ToString("O")), Ct);
        Assert.Contains("RECORDED", historical.GetRawText()); Assert.DoesNotContain("SUPERSEDED_REVIEW_REQUIRED", historical.GetRawText());
        await using var check = Db(); Assert.Equal(saved.ContextHash, (await check.HumanReviewEvents.SingleAsync(x => x.Id == saved.Id, Ct)).ContextHash);
    }
    [Fact]
    public async Task Client_cannot_supply_actor_kind_role_signature_policy_or_decision_time()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var v = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, v);
        var json = JsonSerializer.Serialize(cmd, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var extra in new[] { "\"actorKind\":\"Human\"", "\"actorRole\":\"Master\"", "\"actorId\":\"fictional\"", "\"signature\":\"fake\"", "\"policy\":{}", "\"decidedAt\":\"2026-11-01T00:00:00Z\"" }) {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + GraphRoute("decisions"));
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); req.Content = new StringContent(json[..^1] + "," + extra + "}", System.Text.Encoding.UTF8, "application/json");
            using var response = await c.SendAsync(req, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        await using var db = Db(); Assert.Empty(await db.HumanReviewEvents.Where(x => x.Kind == "Decision").ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Type_specific_gate_reports_exact_source_and_never_falls_back_to_recording()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var v = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, v);
        var m = host.Services.GetRequiredService<FederationSettings>().Memberships[0];
        var i = Array.FindIndex(m.HumanAuthorities, x => x.Capability == "human.decide");
        m.HumanAuthorities[i] = m.HumanAuthorities[i] with { Recording = m.HumanAuthorities[i].Recording! with {
            RequiredGate = "QualifiedSignature", GateSourceLocator = "07.01/Master Register/row:71/TR-013 (conditional qualified-signature type)" } };
        using var response = await Send(c, GraphRoute("decisions"), cmd); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("07.01/Master Register/row:71", await response.Content.ReadAsStringAsync(Ct));
        await using var db = Db(); Assert.Empty(await db.HumanReviewEvents.Where(x => x.Kind == "Decision").ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Recording_race_rollback_revoked_history_and_stale_verification_replay_are_bound()
    {
        var fault = new RejectHumanReceipt(); await using var host = Host(interceptor: fault); using var c = Client(host);
        GraphPermissionsForTest(host.Services); await Token(c); var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var v = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, v);
        await using var before = Db(); var audits = await before.AuditEvents.CountAsync(Ct); var receipts = await before.HumanReviewReceipts.CountAsync(Ct);
        fault.Armed = true; using var failed = await Send(c, GraphRoute("decisions"), cmd); Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        await using (var db = Db()) { Assert.Empty(await db.HumanReviewEvents.Where(x => x.Kind == "Decision").ToArrayAsync(Ct)); Assert.Equal(audits, await db.AuditEvents.CountAsync(Ct)); Assert.Equal(receipts, await db.HumanReviewReceipts.CountAsync(Ct)); }
        fault.Armed = false; var key = Guid.NewGuid().ToString();
        var races = await Task.WhenAll(Send(c, GraphRoute("decisions"), cmd, key), Send(c, GraphRoute("decisions"), cmd, key));
        var original = await races[0].Content.ReadAsStringAsync(Ct);
        foreach (var r in races) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(original, await r.Content.ReadAsStringAsync(Ct)); r.Dispose(); }
        using var reused = await Send(c, GraphRoute("decisions"), cmd with { Recording = cmd.Recording! with { HumanDecision = "Changed text" } }, key); Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        var update = await CurrentReviewCommand(c, host.Services, ev, co, v, "human.verify");
        // Recording intentionally touches the verification head xmin even with
        // unchanged revision. A stale editor must reload that live head token.
        using var staleNoop = await Send(c, GraphRoute($"verifications/{v.Id}/events"), update); Assert.Equal(HttpStatusCode.Conflict, staleNoop.StatusCode);
        var liveTrace = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct);
        update = update with { ExpectedVersion = liveTrace.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == v.Id).GetProperty("version").GetUInt32() };
        using var noop = await Send(c, GraphRoute($"verifications/{v.Id}/events"), update); Assert.Equal(HttpStatusCode.OK, noop.StatusCode);
        using var stale = await Send(c, GraphRoute("decisions"), cmd, key); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var trace = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct); Assert.Contains("SUPERSEDED_REVIEW_REQUIRED", trace.GetRawText());
        host.Services.GetRequiredService<FederationSettings>().Memberships[0].HumanAuthorities = [];
        using var revoked = await Send(c, GraphRoute("decisions"), cmd, key); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        using var hidden = await c.GetAsync(TraceRoute(ev, co), Ct); Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        var hiddenBody = await hidden.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("FICTIONAL human decision text", hiddenBody); Assert.DoesNotContain("RECORDED", hiddenBody);
    }
}
