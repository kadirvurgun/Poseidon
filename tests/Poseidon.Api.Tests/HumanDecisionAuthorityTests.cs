using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence.Seeding;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-031 / T-S3-006 (18.01 Backlog row:32) and DEV-032 / T-S3-007 (row:33).
// Actual host/CSRF/owned PostgreSQL. Synthetic actors exercise software only; no real
// human, pilot or legal authority is represented. PostgreSQL execution DEFERRED (no Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private async Task AssertNoFinalDecisionAndAuditedDenial(int decisionsBefore)
    {
        await using var db = Db();
        Assert.Equal(decisionsBefore, await db.HumanReviewEvents.CountAsync(x => x.Kind == "Decision" && (x.State == "RECORDED" || x.State == "REJECTED"), Ct));
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "HumanReview" && x.Result == "human_authority_required" && x.ActorId == actorId.ToString(), Ct));
    }

    [Theory, Trait("Requirement", "DEV-031")]
    [InlineData("AI")]
    [InlineData("Automation")]
    [InlineData("Service")]
    [InlineData("Unspecified")]
    public async Task Non_human_principal_with_full_appointment_cannot_create_or_finalize_a_decision(string actorKind)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var v = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, v);
        // A pending human request exists, so both the direct and the finalization path are exercised.
        var request = await CurrentReviewCommand(c, host.Services, ev, co, v, "human.review.submit") with { ExpectedVersion = 0, VerificationId = v.Id, VerificationRevision = v.Revision };
        using var pendingResponse = await Send(c, GraphRoute("decisions"), request); Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        var pending = (await pendingResponse.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        // The exact rejection envelope is built while the principal is still the appointed human:
        // the regulatory trace it reads is itself human-only (ActorKind == "Human"), so building it
        // after the switch would fail on the trace read rather than exercise the decision gate.
        var reject = await CurrentReviewCommand(c, host.Services, ev, co, pending, "human.decide");
        var settings = host.Services.GetRequiredService<FederationSettings>(); settings.Memberships[0] = settings.Memberships[0] with { ActorKind = actorKind };
        // The trace is not readable by the non-human principal either.
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(TraceRoute(ev, co), Ct)).StatusCode);
        using var direct = await Send(c, GraphRoute("decisions"), cmd); Assert.Equal(HttpStatusCode.Forbidden, direct.StatusCode);
        using var finalize = await Send(c, GraphRoute($"decisions/{pending.Id}/events"), cmd with { ExpectedVersion = pending.Version });
        Assert.Equal(HttpStatusCode.Forbidden, finalize.StatusCode);
        using var rejected = await Send(c, GraphRoute($"decisions/{pending.Id}/events"), reject with { State = "REJECTED", ReasonCode = "HUMAN_REJECTED" });
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        foreach (var r in new[] { direct, finalize, rejected }) Assert.Contains("human_authority_required", await r.Content.ReadAsStringAsync(Ct));
        await AssertNoFinalDecisionAndAuditedDenial(0);
        await using var db = Db(); Assert.Equal("PENDING", (await db.HumanReviewEvents.Where(x => x.Id == pending.Id).OrderByDescending(x => x.Revision).FirstAsync(Ct)).State);
    }

    [Theory, Trait("Requirement", "DEV-031")]
    [InlineData("no-appointment")]
    [InlineData("submit-only")]
    [InlineData("admin-role")]
    [InlineData("unresolved-policy")]
    public async Task Generic_tenant_admin_without_the_exact_human_decision_appointment_cannot_create_a_final_decision(string variant)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var v = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, v);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        var decide = m.HumanAuthorities.Single(x => x.Capability == "human.decide");
        // Tenant-wide read + manage + every review/decision permission: a generic administrator.
        settings.Memberships[0] = m with { TenantWideRead = true, TenantWideManage = true,
            Permissions = [..m.Permissions, "directory.write", "audit.read", "human.trace.read", "workspace.search"],
            HumanAuthorities = variant switch {
                "no-appointment" => m.HumanAuthorities.Where(x => x.Capability != "human.decide").ToArray(),
                "submit-only" => m.HumanAuthorities.Where(x => x.Capability != "human.decide").ToArray(),
                "admin-role" => m.HumanAuthorities.Select(x => x == decide ? x with { Recording = x.Recording! with { AuthorityRole = "Admin" } } : x).ToArray(),
                _ => m.HumanAuthorities.Select(x => x == decide ? x with { Recording = null } : x).ToArray() } };
        var attempt = variant == "submit-only"
            ? cmd with { AuthorityId = m.HumanAuthorities.Single(x => x.Capability == "human.review.submit").Id }
            : cmd;
        using var response = await Send(c, GraphRoute("decisions"), attempt);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoFinalDecisionAndAuditedDenial(0);
        // The administrator still reads nothing private through the trace without an appointment.
        var body = await (await c.GetAsync(TraceRoute(ev, co), Ct)).Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(cmd.Recording!.HumanDecision, body);
    }

    [Theory, Trait("Requirement", "DEV-031")]
    [InlineData("DPA")]
    [InlineData("Master")]
    public async Task Authorized_human_decision_records_role_context_reason_and_the_exact_chain_and_the_trace_exposes_its_hash(string role)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id, role);
        var v = await PendingReview(c, host.Services, ev, co); var cmd = await RecordingCommand(c, host.Services, ev, co, v);
        var grant = host.Services.GetRequiredService<FederationSettings>().Memberships[0].HumanAuthorities.Single(x => x.Capability == "human.decide");
        using var response = await Send(c, GraphRoute("decisions"), cmd); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        await using (var db = Db()) {
            var row = await db.HumanReviewEvents.SingleAsync(x => x.Id == saved.Id && x.Kind == "Decision", Ct);
            // Authorized role (appointment role id + recorded authority role), server actor and time.
            Assert.Equal(ReviewRole, row.RoleId); Assert.Equal(grant.Id, row.AuthorityId); Assert.Equal(actorId, row.ActorId);
            var recorded = JsonSerializer.Deserialize<RecordedDecision>(row.DecisionJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(role, recorded.Policy.AuthorityRole); Assert.Equal("UNASSESSED", recorded.Qualification);
            // Context and reason.
            Assert.Equal(cmd.Recording!.DecisionContext, recorded.Content.DecisionContext); Assert.Equal(cmd.Recording.TriggerSource, recorded.Content.TriggerSource);
            Assert.Equal("HUMAN_DECIDED", row.ReasonCode); Assert.Equal(cmd.Rationale, row.Rationale);
            // The exact chain it decides on: evidence -> control -> verification revision (+ profile).
            Assert.Equal((ev.Id, ev.Revision, co.Id, co.Revision), (row.EvidenceId, row.EvidenceRevision, row.ControlId, row.ControlRevision));
            Assert.Equal((v.Id, v.Revision), (row.VerificationId!.Value, row.VerificationRevision!.Value));
            Assert.Equal(cmd.ExpectedProfileRevision, recorded.ProfileRevision);
            Assert.Matches("^[A-F0-9]{64}$", row.ContextHash); Assert.NotEqual(v.ContextHash, row.ContextHash);
            Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "HumanReviewCommand" && x.Result == "Attempted" && x.ActorId == actorId.ToString(), Ct));
            // Private rationale/decision text never enters the security audit stream.
            Assert.DoesNotContain(await db.AuditEvents.ToArrayAsync(Ct), x => (x.ChangeSummary ?? "").Contains(cmd.Recording.HumanDecision));
        }
        // The authority holder's trace and an assigned auditor's audit-ready view carry the same context hash and reason.
        var trace = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co), Ct);
        var item = trace.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == saved.Id);
        Assert.Equal(saved.ContextHash, item.GetProperty("contextHash").GetString()); Assert.Equal("HUMAN_DECIDED", item.GetProperty("reasonCode").GetString());
        Assert.Equal(ReviewRole, item.GetProperty("roleId").GetGuid());
        var settings = host.Services.GetRequiredService<FederationSettings>(); var now = DateTimeOffset.UtcNow;
        settings.Memberships[0] = settings.Memberships[0] with {
            Permissions = ["directory.read", "audit.read", "human.trace.read", "regulatory.draft.read", "control.read", "evidence.read", "file.read"],
            TenantWideManage = false, TenantWideRead = false, VesselGrants = [new(SeedVessel.Id)], HumanAuthorities = [],
            HumanTraceReaders = [new(Guid.NewGuid(), SeedVessel.Id, ev.Id, co.Id, ReviewRole, ["Verification", "Decision"], [cmd.Recording!.DecisionType], now.AddHours(-1), now.AddHours(1))] };
        var audit = await c.GetFromJsonAsync<JsonElement>(TraceRoute(ev, co).Replace("regulatory-trace", "audit-ready"), Ct);
        var auditItem = audit.GetProperty("auditItems").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == saved.Id);
        Assert.Equal(saved.ContextHash, auditItem.GetProperty("contextHash").GetString());
        Assert.Equal("HUMAN_DECIDED", auditItem.GetProperty("reasonCode").GetString()); Assert.Equal(role, auditItem.GetProperty("authorityRole").GetString());
        Assert.DoesNotContain(cmd.Rationale, audit.GetRawText()); Assert.DoesNotContain(cmd.Recording.HumanDecision, audit.GetRawText());
        Assert.False(audit.GetProperty("complete").GetBoolean());
    }

    [Fact, Trait("Requirement", "DEV-032")]
    public async Task Audit_ready_chains_list_only_appointed_chains_with_declared_source_locators_and_are_audited()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c);
        var (otherEv, otherCo) = await ReviewInputs(c);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var now = DateTimeOffset.UtcNow;
        var route = $"/api/v1/audit-ready/chains?vesselId={SeedVessel.Id}";
        using (var denied = await c.GetAsync(route, Ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        settings.Memberships[0] = settings.Memberships[0] with {
            ActorKind = "Human", Permissions = ["directory.read", "audit.read", "human.trace.read", "regulatory.draft.read", "control.read", "evidence.read", "file.read"],
            TenantWideManage = false, TenantWideRead = false, VesselGrants = [new(SeedVessel.Id)], HumanAuthorities = [],
            HumanTraceReaders = [new(Guid.NewGuid(), SeedVessel.Id, ev.Id, co.Id, ReviewRole, ["Verification", "Decision"], [], now.AddHours(-1), now.AddHours(1))] };
        var body = await c.GetFromJsonAsync<JsonElement>(route, Ct);
        var chain = Assert.Single(body.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal((ev.Id, ev.Revision, co.Id, co.Revision), (chain.GetProperty("evidenceId").GetGuid(), chain.GetProperty("evidenceRevision").GetInt64(),
            chain.GetProperty("controlId").GetGuid(), chain.GetProperty("controlRevision").GetInt64()));
        Assert.DoesNotContain(otherEv.Id.ToString("D"), body.GetRawText()); Assert.DoesNotContain(otherCo.Id.ToString("D"), body.GetRawText());
        var source = Assert.Single(chain.GetProperty("sources").EnumerateArray().ToArray());
        Assert.Equal(RegulatoryRegisterFixtures.SourceId(seed.Tenants[0].Id), source.GetProperty("sourceId").GetGuid());
        Assert.Equal("Available", source.GetProperty("availability").GetString());
        Assert.Contains(source.GetProperty("provenance").GetString(), new[] { "DeclaredUnverified", "SyntheticUnverified" });
        // A Missing/Unknown locator is passed through as its state with no invented text.
        if (source.GetProperty("locatorState").GetString() != "Known") Assert.Equal(JsonValueKind.Null, source.GetProperty("locator").ValueKind);
        await using (var db = Db()) Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "HumanTraceRead" && x.Result == "Success" && x.ResourceId == SeedVessel.Id, Ct));
        // Selector widening is rejected; a foreign-tenant vessel is indistinguishable from missing.
        using (var widened = await c.GetAsync(route + "&evidenceId=" + otherEv.Id, Ct)) Assert.Equal(HttpStatusCode.BadRequest, widened.StatusCode);
        var foreign = seed.Vessels.First(x => x.TenantId != seed.Tenants[0].Id);
        using (var hidden = await c.GetAsync($"/api/v1/audit-ready/chains?vesselId={foreign.Id}", Ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        // A non-human principal with the same reader appointment sees an empty list, never another chain.
        settings.Memberships[0] = settings.Memberships[0] with { ActorKind = "AI" };
        var ai = await c.GetFromJsonAsync<JsonElement>(route, Ct);
        Assert.Empty(ai.GetProperty("items").EnumerateArray());
    }
}
