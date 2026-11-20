using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;
namespace Poseidon.Api.Tests;

// S11 WP-D DEV-074 / DEV-094 (18.01 rows 75/95; 19.01 §3-4) and DEV-075 (18.01 row 76; DoR DoD Gates row 4; 23.03 §5):
// real PostgreSQL + actual host. Synthetic identities and a FICTIONAL Product Owner appointment only. The flow records
// human UAT evidence, proves separation of duties, keeps the gate NO-GO (deferred owners, incomplete UAT) and records
// the only permitted human decision (NO_GO) bound to the exact review hash. No UAT acceptance or GO is produced.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid PilotOwnerGrant = Guid.Parse("5a7b0000-0000-4000-8000-0000000000e5");

    private WebApplicationFactory<Program> PilotHost(bool appointed = true) {
        var config = new Dictionary<string, string?> {
            ["Authentication:Oidc:Memberships:0:Permissions:70"] = "uat.read",
            ["Authentication:Oidc:Memberships:0:Permissions:71"] = "uat.record",
            ["Authentication:Oidc:Memberships:0:Permissions:72"] = "uat.threshold.record",
            ["Authentication:Oidc:Memberships:0:Permissions:73"] = "uat.attest",
            ["Authentication:Oidc:Memberships:0:Permissions:74"] = "pilot.gate.read",
            ["Authentication:Oidc:Memberships:0:Permissions:75"] = "pilot.gate.support.record",
            ["Authentication:Oidc:Memberships:0:Permissions:76"] = "pilot.gate.decide",
            ["Authentication:Oidc:Memberships:0:ActorKind"] = "Human",
        };
        if (appointed) {
            const string prefix = "Authentication:Oidc:Memberships:0:PilotGateAuthorities:0:";
            config[prefix + "Id"] = PilotOwnerGrant.ToString(); config[prefix + "RoleId"] = "91c26eb5-cf16-4553-981d-1c275358c535";
            config[prefix + "Duty"] = "ProductOwner"; config[prefix + "AppointmentSource"] = "Synthetic test appointment; not an operational appointment";
            config[prefix + "ValidFrom"] = "2026-01-01T00:00:00Z"; config[prefix + "ValidUntil"] = "2099-01-01T00:00:00Z";
        }
        return Host().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config)));
    }

    private static void PilotActor(IServiceProvider services, Guid actor, string kind = "Human") {
        var settings = services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { ActorId = actor, ActorKind = kind };
    }

    private async Task PilotCode(HttpResponseMessage response, HttpStatusCode status, string code) {
        using (response) {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }
    }

    private async Task<T> PilotOk<T>(HttpResponseMessage response) {
        using (response) {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
        }
    }

    [Fact]
    [Trait("Requirement", "DEV-074")]
    [Trait("Requirement", "DEV-094")]
    [Trait("Requirement", "DEV-075")]
    public async Task Uat_evidence_is_human_recorded_and_attested_by_another_human_and_the_pilot_gate_records_only_a_bound_human_no_go() {
        using var host = PilotHost(); using var c = Client(host); await Token(c);

        // DEV-074/094: source catalog, separate ship/tug scopes; no threshold -> THRESHOLD_UNSET, never PASS.
        var catalog = (await c.GetFromJsonAsync<UatCatalogView>("/api/v1/uat/scenarios", Ct))!;
        Assert.Equal((6, 10), (catalog.Scenarios.Count(s => s.Scope == "SHIP"), catalog.Scenarios.Count(s => s.Scope == "TUG")));
        var empty = (await c.GetFromJsonAsync<UatSummaryView>("/api/v1/uat/summary?scope=TUG", Ct))!;
        Assert.Equal((UatPolicy.ThresholdUnset, "UNKNOWN", (UatThresholdDto?)null), (empty.Gate.State, empty.Gate.GateState, empty.Threshold));

        // Human-agreed threshold (append-only lineage, idempotent replay, stale sequence 409).
        var thresholdKey = Guid.NewGuid().ToString();
        var thresholdBody = new UatThresholdCommand("SHIP", 100, 0, true, "FICTIONAL-STEERING-01", "FICTIONAL agreed pilot UAT threshold", 0);
        var threshold = await PilotOk<UatThresholdDto>(await Send(c, "uat/thresholds", thresholdBody, thresholdKey));
        Assert.Equal((1L, (Guid?)null, actorId), (threshold.Sequence, threshold.PreviousThresholdId, threshold.ActorId));
        using (var replay = await Send(c, "uat/thresholds", thresholdBody, thresholdKey)) {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        }
        await PilotCode(await Send(c, "uat/thresholds", thresholdBody), HttpStatusCode.Conflict, "threshold_sequence_conflict");
        await PilotCode(await Send(c, "uat/thresholds", thresholdBody with { MinScenarioPassPercent = 0, ExpectedSequence = 1 }), HttpStatusCode.BadRequest, "validation_failed");

        // Facilitator records a run, outcomes and a blocker; scope and pseudonym are enforced.
        await PilotCode(await Send(c, "uat/runs", new UatRunCreateCommand("SHIP", "TUG_MASTER", "UAT", "f1c2d51", "P-MASTER-01")), HttpStatusCode.BadRequest, "validation_failed");
        await PilotCode(await Send(c, "uat/runs", new UatRunCreateCommand("SHIP", "MASTER", "UAT", "f1c2d51", "jane.doe@example.com")), HttpStatusCode.BadRequest, "validation_failed");
        var run = await PilotOk<UatRunDto>(await Send(c, "uat/runs", new UatRunCreateCommand("SHIP", "MASTER", "UAT", "f1c2d51", "P-MASTER-01")));
        Assert.Equal(("OPEN", actorId, (Guid?)null, UatScenarioCatalog.Version), (run.State, run.FacilitatorActorId, run.AttestedBy, run.CatalogVersion));
        var result = await PilotOk<UatResultDto>(await Send(c, $"uat/runs/{run.Id}/results",
            new UatResultCommand("S-UAT-04", "PASS", 420, false, "FICTIONAL-PLAYWRIGHT-RUN-1", null)));
        Assert.Equal((1, "PASS", "FICTIONAL-PLAYWRIGHT-RUN-1", "AutomatedEvidenceIsAttachmentOnly"), (result.Attempt, result.Outcome, result.AutomatedEvidenceReference, result.EvidenceRule));
        var retry = await PilotOk<UatResultDto>(await Send(c, $"uat/runs/{run.Id}/results", new UatResultCommand("S-UAT-04", "FAIL", 300, true, null, "FICTIONAL retest")));
        Assert.Equal(2, retry.Attempt);
        await PilotCode(await Send(c, $"uat/runs/{run.Id}/results", new UatResultCommand("T-UAT-01", "PASS", null, false, null, null)), (HttpStatusCode)422, "scenario_out_of_scope");
        var blocker = await PilotOk<UatFindingDto>(await Send(c, $"uat/runs/{run.Id}/findings",
            new UatFindingCommand("S-UAT-04", "BLOCKER", true, false, "FICTIONAL blocker hidden before sign-off")));
        Assert.Equal(("OPEN", "SHIP", actorId), (blocker.State, blocker.Scope, blocker.ReportedBy));
        var submitted = await PilotOk<UatRunDto>(await Send(c, $"uat/runs/{run.Id}/submit", new UatRunSubmitCommand(run.Version)));
        Assert.Equal("SUBMITTED", submitted.State);
        await PilotCode(await Send(c, $"uat/runs/{run.Id}/results", new UatResultCommand("S-UAT-01", "PASS", null, false, null, null)), HttpStatusCode.Conflict, "uat_run_not_open");

        // Separation of duties: the facilitator cannot attest the run nor resolve the finding it reported.
        await PilotCode(await Send(c, $"uat/runs/{run.Id}/attest", new UatRunAttestCommand("FICTIONAL self attestation", submitted.Version)), HttpStatusCode.Forbidden, "separation_of_duties_required");
        await PilotCode(await Send(c, $"uat/findings/{blocker.Id}/resolve", new UatFindingResolveCommand("RESOLVED", "FICTIONAL self resolution", null, blocker.Version)),
            HttpStatusCode.Forbidden, "separation_of_duties_required");

        // A second human attests and resolves; a blocker can never be accepted as a risk.
        var witness = Guid.NewGuid();
        PilotActor(host.Services, witness); await Token(c);
        await PilotCode(await Send(c, $"uat/runs/{run.Id}/attest", new UatRunAttestCommand("FICTIONAL attestation", submitted.Version + 1)), HttpStatusCode.Conflict, "version_conflict");
        var attested = await PilotOk<UatRunDto>(await Send(c, $"uat/runs/{run.Id}/attest", new UatRunAttestCommand("FICTIONAL witness attests the recorded outcomes", submitted.Version)));
        Assert.Equal(("ATTESTED", (Guid?)witness), (attested.State, attested.AttestedBy));
        await PilotCode(await Send(c, $"uat/findings/{blocker.Id}/resolve", new UatFindingResolveCommand("ACCEPTED_RISK", "FICTIONAL risk acceptance attempt", null, blocker.Version)),
            (HttpStatusCode)422, "blocker_risk_acceptance_not_permitted");
        var resolved = await PilotOk<UatFindingDto>(await Send(c, $"uat/findings/{blocker.Id}/resolve",
            new UatFindingResolveCommand("RESOLVED", "FICTIONAL fix retested in build f1c2d52", "FICTIONAL-ISSUE-1", blocker.Version)));
        Assert.Equal(("RESOLVED", (Guid?)witness), (resolved.State, resolved.ResolvedBy));

        // The latest attempt (FAIL) counts; DPA/C/E/Crew are not covered -> FAIL, never PASS.
        var summary = (await c.GetFromJsonAsync<UatSummaryView>("/api/v1/uat/summary?scope=SHIP", Ct))!;
        Assert.Equal(("FAIL", 1, 0), (summary.Gate.GateState, summary.Gate.AttestedRuns, summary.Gate.OpenBlockers));
        Assert.Contains("SCENARIO_BELOW_THRESHOLD:S-UAT-04", summary.Gate.Reasons);
        Assert.Contains("ROLE_NOT_COVERED:DPA", summary.Gate.Reasons);

        // DEV-075 support readiness: READY needs an evidence reference.
        await PilotCode(await Send(c, "pilot-gate/support-checks", new PilotSupportCheckCommand("ONCALL_ROTA", "READY", null, "FICTIONAL rota", 0)), HttpStatusCode.BadRequest, "validation_failed");
        var check = await PilotOk<PilotSupportCheckDto>(await Send(c, "pilot-gate/support-checks",
            new PilotSupportCheckCommand("ONCALL_ROTA", "READY", "FICTIONAL-ROTA-DOC-1", "FICTIONAL rota reviewed", 0)));
        Assert.Equal((1L, witness), (check.Sequence, check.ActorId));

        // Review: deferred owners and a failed UAT gate -> NO_GO_RECOMMENDED; only NO_GO is offered.
        var gate = (await c.GetFromJsonAsync<PilotGateView>("/api/v1/pilot-gate", Ct))!;
        Assert.Equal(PilotGatePolicy.NoGoRecommended, gate.Review.Recommendation);
        Assert.Equal(PilotGatePolicy.Gates, gate.Review.Gates.Select(g => g.Gate));
        Assert.Equal("DEFERRED", gate.Review.Gates.Single(g => g.Gate == "SECURITY").State);
        Assert.Equal("FAIL", gate.Review.Gates.Single(g => g.Gate == "UAT_SHIP").State);
        Assert.Equal("UNKNOWN", gate.Review.Gates.Single(g => g.Gate == "SUPPORT").State);
        Assert.Equal(0, gate.Review.OpenP0);
        Assert.Equal(new[] { "NO_GO" }, gate.AllowedDecisions);
        Assert.True(gate.CanDecide); Assert.Equal(PilotOwnerGrant, Assert.Single(gate.Authorities).Id);

        PilotGateDecisionCommand Decision(string decision, string? hash = null, long? sequence = null, string[]? conditions = null) =>
            new(decision, "FICTIONAL review: owners deferred, UAT incomplete", conditions ?? [], hash ?? gate.Review.Hash, sequence ?? gate.Sequence, PilotOwnerGrant);
        await PilotCode(await Send(c, "pilot-gate/decisions", Decision("GO")), HttpStatusCode.Conflict, "pilot_gate_failed");
        await PilotCode(await Send(c, "pilot-gate/decisions", Decision("CONDITIONAL_GO", conditions: ["FICTIONAL condition"])), HttpStatusCode.Conflict, "pilot_gate_failed");
        await PilotCode(await Send(c, "pilot-gate/decisions", Decision("NO_GO", hash: new string('0', 64))), HttpStatusCode.Conflict, "review_stale");
        await PilotCode(await Send(c, "pilot-gate/decisions", Decision("NO_GO", sequence: 7)), HttpStatusCode.Conflict, "decision_sequence_conflict");
        var decisionKey = Guid.NewGuid().ToString();
        var noGo = await PilotOk<PilotGateDecisionDto>(await Send(c, "pilot-gate/decisions", Decision("NO_GO"), decisionKey));
        Assert.Equal(("NO_GO", PilotGatePolicy.NoGoRecommended, gate.Review.Hash, 1L, "ProductOwner", "CURRENT", witness),
            (noGo.Decision, noGo.Recommendation, noGo.ReviewHash, noGo.Sequence, noGo.Duty, noGo.Validity, noGo.ActorId));
        await PilotCode(await Send(c, "pilot-gate/decisions", Decision("GO"), decisionKey), HttpStatusCode.Conflict, "idempotency_key_reused");

        // AI/automation principals never decide nor record UAT evidence, even with the appointment configured.
        PilotActor(host.Services, witness, "AI"); await Token(c);
        await PilotCode(await Send(c, "pilot-gate/decisions", Decision("NO_GO", sequence: 1)), HttpStatusCode.Forbidden, "pilot_gate_authority_required");
        await PilotCode(await Send(c, "uat/runs", new UatRunCreateCommand("TUG", "TUG_MASTER", "UAT", "f1c2d51", "P-TUG-01")), HttpStatusCode.Forbidden, "human_actor_required");

        await using (var db = Db()) {
            var saved = Assert.Single(await db.PilotGateDecisions.Where(d => d.Id == noGo.Id).ToArrayAsync(Ct));
            Assert.Equal((witness, PilotOwnerGrant, "ProductOwner", (Guid?)null), (saved.ActorId, saved.AuthorityId, saved.Duty, saved.PreviousDecisionId));
            Assert.Contains("SECURITY", saved.SnapshotJson);
            Assert.Equal(1, await db.PilotGateDecisions.CountAsync(Ct));
            Assert.False(await db.PilotGateDecisions.AnyAsync(d => d.Decision != "NO_GO", Ct));
            Assert.Equal(9, await db.PilotReceipts.Select(r => r.Action).Distinct().CountAsync(Ct));
            var row = await db.UatRuns.SingleAsync(r => r.Id == run.Id, Ct);
            Assert.Equal((actorId, (Guid?)witness), (row.FacilitatorActorId, row.AttestedBy));
            // Audit projections carry closed codes only.
            var audit = await db.AuditEvents.Where(a => a.EntityType == "PilotGateDecision" || a.EntityType == "UatFinding" || a.EntityType == "UatRun").ToArrayAsync(Ct);
            Assert.NotEmpty(audit);
            Assert.All(audit, a => { Assert.DoesNotContain("FICTIONAL", a.ChangeSummary ?? ""); Assert.DoesNotContain("P-MASTER-01", a.ChangeSummary ?? ""); });
        }
        await using (var other = Db(seed.Tenants[1].Id)) {
            Assert.False(await other.UatRuns.AnyAsync(r => r.Id == run.Id, Ct));
            Assert.False(await other.PilotGateDecisions.AnyAsync(Ct));
        }
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    [Trait("Requirement", "DEV-074")]
    public async Task Readers_without_permission_are_denied_unknown_runs_are_not_found_and_the_uat_fixture_is_opt_in_fictional_and_open() {
        using (var plain = Host()) {
            using var p = Client(plain); await Token(p);
            await PilotCode(await p.GetAsync("/api/v1/pilot-gate", Ct), HttpStatusCode.Forbidden, "permission_denied");
            await PilotCode(await p.GetAsync("/api/v1/uat/summary?scope=SHIP", Ct), HttpStatusCode.Forbidden, "permission_denied");
            await PilotCode(await Send(p, "pilot-gate/decisions", new PilotGateDecisionCommand("NO_GO", "FICTIONAL unauthorized attempt", [], new string('A', 64), 0, PilotOwnerGrant)),
                HttpStatusCode.Forbidden, "permission_denied");
        }
        using var host = PilotHost(appointed: false); using var c = Client(host); await Token(c);
        await PilotCode(await c.GetAsync($"/api/v1/uat/runs/{Guid.NewGuid()}", Ct), HttpStatusCode.NotFound, "resource_not_found");
        await PilotCode(await c.GetAsync("/api/v1/uat/summary?scope=BOTH", Ct), HttpStatusCode.BadRequest, "invalid_query");
        var gate = (await c.GetFromJsonAsync<PilotGateView>("/api/v1/pilot-gate", Ct))!;
        Assert.False(gate.CanDecide); Assert.Empty(gate.Authorities);
        // A tenant-wide manager without an appointment cannot decide.
        await PilotCode(await Send(c, "pilot-gate/decisions", new PilotGateDecisionCommand("NO_GO", "FICTIONAL manager attempt", [], gate.Review.Hash, gate.Sequence, PilotOwnerGrant)),
            HttpStatusCode.Forbidden, "pilot_gate_authority_required");

        await using (var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            var tenants = seed.Tenants.Select(t => t.Id).ToArray();
            await UatFixtures.AddMissingAsync(db, tenants, Ct); await UatFixtures.AddMissingAsync(db, tenants, Ct); await tx.CommitAsync(Ct);
        }
        await using var check = Db();
        var ids = new[] { UatFixtures.RunId(seed.Tenants[0].Id, "SHIP"), UatFixtures.RunId(seed.Tenants[0].Id, "TUG") };
        var rows = await check.UatRuns.Where(r => ids.Contains(r.Id)).ToArrayAsync(Ct);
        Assert.Equal(2, rows.Length);
        Assert.All(rows, r => Assert.Equal(("OPEN", "SyntheticUnverified", (Guid?)null, (DateTimeOffset?)null), (r.State, r.Provenance, r.AttestedBy, r.SubmittedAt)));
        Assert.False(await check.UatStepResults.AnyAsync(x => ids.Contains(x.RunId), Ct));
        Assert.False(await check.UatThresholds.AnyAsync(Ct));
        Assert.False(await check.PilotGateDecisions.AnyAsync(Ct));
        // DB CHECK: a synthetic run never leaves OPEN, even through a direct write.
        await Assert.ThrowsAnyAsync<Exception>(() => Sql($"UPDATE pilot.\"UatRuns\" SET \"State\" = 'SUBMITTED', \"SubmittedAt\" = now() WHERE \"Id\" = '{ids[0]}'"));
    }
}
