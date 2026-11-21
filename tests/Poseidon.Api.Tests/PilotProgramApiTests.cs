using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;
namespace Poseidon.Api.Tests;

// Pilot program kit DEV-076/077/078/079/095/096/121 (18.01 rows 77-80, 96, 97, 122; 19.01 §2/§7/§11): real PostgreSQL
// + actual host. Synthetic identities and a FICTIONAL Product Owner appointment only. Every critical write has a
// success path; links are verified against real tenant records (synthetic and missing records are refused), week
// close and the program decision are human records bound to their hash with separation of duties, missing baselines
// stay UNKNOWN and EXPAND is refused. No real pilot result is produced.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid PilotKitGrant = Guid.Parse("5a7b0000-0000-4000-8000-0000000000f6");

    private WebApplicationFactory<Program> PilotKitHost(bool retention = true) {
        var config = new Dictionary<string, string?> {
            ["Authentication:Oidc:Memberships:0:Permissions:80"] = "pilot.program.read",
            ["Authentication:Oidc:Memberships:0:Permissions:81"] = "pilot.program.manage",
            ["Authentication:Oidc:Memberships:0:Permissions:82"] = "pilot.program.record",
            ["Authentication:Oidc:Memberships:0:Permissions:83"] = "pilot.program.triage",
            ["Authentication:Oidc:Memberships:0:Permissions:84"] = "pilot.program.decide",
            ["Authentication:Oidc:Memberships:0:ActorKind"] = "Human",
            ["Authentication:Oidc:Memberships:0:PilotGateAuthorities:0:Id"] = PilotKitGrant.ToString(),
            ["Authentication:Oidc:Memberships:0:PilotGateAuthorities:0:RoleId"] = "91c26eb5-cf16-4553-981d-1c275358c535",
            ["Authentication:Oidc:Memberships:0:PilotGateAuthorities:0:Duty"] = "ProductOwner",
            ["Authentication:Oidc:Memberships:0:PilotGateAuthorities:0:AppointmentSource"] = "Synthetic test appointment; not an operational appointment",
            ["Authentication:Oidc:Memberships:0:PilotGateAuthorities:0:ValidFrom"] = "2026-01-01T00:00:00Z",
            ["Authentication:Oidc:Memberships:0:PilotGateAuthorities:0:ValidUntil"] = "2099-01-01T00:00:00Z",
        };
        if (retention) { config["PilotProgram:FeedbackRetentionDays"] = "90"; config["PilotProgram:RetentionPolicyRef"] = "FICTIONAL-RETENTION-POLICY-1"; }
        return Host().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config)));
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    [Trait("Requirement", "DEV-077")]
    [Trait("Requirement", "DEV-079")]
    [Trait("Requirement", "DEV-095")]
    [Trait("Requirement", "DEV-096")]
    [Trait("Requirement", "DEV-121")]
    public async Task Pilot_program_records_human_evidence_verifies_links_and_binds_week_close_and_decision_to_the_product_owner() {
        using var host = PilotKitHost(); using var c = Client(host); await Token(c);
        var tug = seed.Vessels.First(v => v.TenantId == SeedVessel.TenantId && v.Kind == VesselKind.Tug);
        var ship = seed.Vessels.First(v => v.TenantId == SeedVessel.TenantId && v.Kind == VesselKind.Ship);
        var list = (await c.GetFromJsonAsync<PilotProgramListView>("/api/v1/pilot-programs", Ct))!;
        Assert.True(list.CanCreate); Assert.Empty(list.Programs);
        Assert.Contains(list.Vessels, v => v.Id == tug.Id && v.Kind == "TUG");

        // Program: 1 ship + 1 tug of the tenant, an IANA calendar zone; wrong kinds and zones are refused.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PilotCode(await Send(c, "pilot-programs", new PilotProgramCreateCommand("FICTIONAL pilot", ship.Id, tug.Id, today, "Mars/Olympus")), HttpStatusCode.BadRequest, "invalid_time_zone");
        await PilotCode(await Send(c, "pilot-programs", new PilotProgramCreateCommand("FICTIONAL pilot", tug.Id, null, today, "UTC")), (HttpStatusCode)422, "invalid_vessel");
        var program = await PilotOk<PilotProgramDto>(await Send(c, "pilot-programs", new PilotProgramCreateCommand("FICTIONAL dual-mode pilot", ship.Id, tug.Id, today, "UTC")));
        Assert.Equal(("UserDeclaredUnverified", actorId, PilotProgramCatalog.Version), (program.Provenance, program.CreatedBy, program.CatalogVersion));
        var root = $"pilot-programs/{program.Id}";

        // DEV-076 daily feedback: PII is refused; retention is the operator policy.
        await PilotCode(await Send(c, root + "/feedback", new PilotFeedbackCommand("W1", "SHIP", "USABILITY", "NEGATIVE", "MASTER", "call me on 05321234567", "P-MASTER-01")),
            HttpStatusCode.BadRequest, "pii_suspected");
        var feedback = await PilotOk<PilotFeedbackDto>(await Send(c, root + "/feedback",
            new PilotFeedbackCommand("W1", "SHIP", "USABILITY", "NEUTRAL", "MASTER", "FICTIONAL certificate list took long to load", "P-MASTER-01")));
        Assert.Equal(("FICTIONAL certificate list took long to load", actorId), (feedback.Summary, feedback.ActorId));
        Assert.True(feedback.ExpiresAt > feedback.RecordedAt.AddDays(89));

        // Links: only a server-verified real record of this tenant and program satisfies an item.
        await PilotCode(await Send(c, root + "/links", new PilotLinkCommand("W1_DAILY_FEEDBACK", "WORK_ORDER", feedback.Id, null)), HttpStatusCode.BadRequest, "record_type_not_allowed");
        await PilotCode(await Send(c, root + "/links", new PilotLinkCommand("W1_DAILY_FEEDBACK", "PILOT_FEEDBACK", Guid.NewGuid(), null)), (HttpStatusCode)422, "record_not_found");
        var linkKey = Guid.NewGuid().ToString();
        var linkBody = new PilotLinkCommand("W1_DAILY_FEEDBACK", "PILOT_FEEDBACK", feedback.Id, "FICTIONAL first daily feedback");
        var link = await PilotOk<PilotLinkDto>(await Send(c, root + "/links", linkBody, linkKey));
        Assert.Equal(("W1", actorId, false), (link.Week, link.ActorId, link.AuditTracedAtLink));
        using (var replay = await Send(c, root + "/links", linkBody, linkKey)) {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        }
        await PilotCode(await Send(c, root + "/links", linkBody), HttpStatusCode.Conflict, "link_exists");
        var withdrawn = await PilotOk<PilotLinkDto>(await Send(c, $"{root}/links/{link.Id}/withdraw", new PilotLinkWithdrawCommand("FICTIONAL linked to the wrong item", link.Version)));
        Assert.Equal((actorId, "FICTIONAL linked to the wrong item"), (withdrawn.WithdrawnBy!.Value, withdrawn.WithdrawalReason));
        await PilotCode(await Send(c, $"{root}/links/{link.Id}/withdraw", new PilotLinkWithdrawCommand("FICTIONAL second withdrawal", withdrawn.Version)), HttpStatusCode.Conflict, "link_withdrawn");
        var relink = await PilotOk<PilotLinkDto>(await Send(c, root + "/links", linkBody));
        Assert.NotEqual(link.Id, relink.Id);
        // A synthetic (seeded) work order is never pilot evidence.
        await using (var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        await using (var tx = await admin.Database.BeginTransactionAsync(Ct)) { await WorkOrderFixtures.AddMissingAsync(admin, [ship], Ct); await tx.CommitAsync(Ct); }
        await PilotCode(await Send(c, root + "/links", new PilotLinkCommand("W2_WORK_ORDER", "WORK_ORDER", WorkOrderFixtures.Id(ship.Id), null)),
            (HttpStatusCode)422, "synthetic_record_not_linkable");

        // DEV-076 P0 triage tracked by server timestamps on the program calendar day; OPEN -> TRIAGED -> RESOLVED.
        var issue = await PilotOk<PilotIssueDto>(await Send(c, root + "/issues", new PilotIssueCommand("W1", "SHIP", "CREW", "P0", "FICTIONAL crew list incomplete")));
        Assert.Contains(issue.Timing, new[] { PilotIssuePolicy.DueToday, PilotIssuePolicy.Overdue });
        await PilotCode(await Send(c, $"{root}/issues/{issue.Id}/resolve", new PilotIssueResolveCommand("FICTIONAL fixed", null, issue.Version)), HttpStatusCode.Conflict, "issue_not_triaged");
        var triaged = await PilotOk<PilotIssueDto>(await Send(c, $"{root}/issues/{issue.Id}/triage", new PilotIssueTriageCommand("FICTIONAL import gap triaged", issue.Version)));
        Assert.Equal(("TRIAGED", (Guid?)actorId), (triaged.State, triaged.TriagedBy));
        Assert.Contains(triaged.Timing, new[] { PilotIssuePolicy.SameDay, PilotIssuePolicy.Late });
        await PilotCode(await Send(c, $"{root}/issues/{issue.Id}/triage", new PilotIssueTriageCommand("FICTIONAL again", triaged.Version)), HttpStatusCode.Conflict, "issue_not_open");
        var resolved = await PilotOk<PilotIssueDto>(await Send(c, $"{root}/issues/{issue.Id}/resolve", new PilotIssueResolveCommand("FICTIONAL crew list re-imported", "FICTIONAL-TICKET-1", triaged.Version)));
        Assert.Equal("RESOLVED", resolved.State);

        // DEV-096/121 human baseline with an evidence reference; append-only lineage.
        var baseline = new PilotMeasurementCommand("TUG_JOB_PREP_TIME", "BASELINE", 1800m, 5, today.AddDays(-30), today.AddDays(-1), "FICTIONAL-TIME-STUDY-1", "FICTIONAL stopwatch study of 5 jobs", 0);
        await PilotCode(await Send(c, root + "/measurements", baseline with { MetricId = "SHIP_EVIDENCE_COMPLETENESS", Value = 120m }), HttpStatusCode.BadRequest, "validation_failed");
        var measured = await PilotOk<PilotMeasurementDto>(await Send(c, root + "/measurements", baseline));
        Assert.Equal(("TUG", "SECOND", 1L), (measured.Mode, measured.Unit, measured.Sequence));
        await PilotCode(await Send(c, root + "/measurements", baseline), HttpStatusCode.Conflict, "measurement_sequence_conflict");

        // Read model: W1 not ready (other items missing), later weeks not started, missing pilot measurement -> UNKNOWN.
        var view = (await c.GetFromJsonAsync<PilotProgramView>($"/api/v1/{root}", Ct))!;
        var w1 = view.Weeks.Single(w => w.Definition.Week == "W1");
        Assert.Equal((PilotProgramPolicy.NotReady, PilotProgramPolicy.InProgress), (w1.Outcome.Recommendation, w1.Outcome.Phase));
        Assert.Equal(PilotProgramPolicy.Satisfied, w1.Outcome.Items.Single(i => i.Code == "W1_DAILY_FEEDBACK").State);
        Assert.Equal(PilotProgramPolicy.Missing, w1.Outcome.Items.Single(i => i.Code == "W1_SHIP_CERTIFICATE").State);
        Assert.Equal(new[] { PilotProgramPolicy.NotAccepted }, w1.AllowedOutcomes);
        Assert.Equal(PilotProgramPolicy.NotStarted, view.Weeks.Single(w => w.Definition.Week == "W2").Outcome.Phase);
        var prep = view.Readout.Metrics.Single(m => m.MetricId == "TUG_JOB_PREP_TIME");
        Assert.Equal((PilotProgramPolicy.Unknown, (decimal?)1800m, (decimal?)null, (decimal?)null), (prep.Quality, prep.Baseline, prep.Current, prep.Delta));
        Assert.Equal("NOT_AUTHORIZED", view.Readout.TelemetryState);
        Assert.Equal(PilotProgramPolicy.FixRecommended, view.Review.Recommendation);
        Assert.DoesNotContain("EXPAND", view.AllowedDecisions);
        Assert.True(view.CanDecide); Assert.Equal(PilotKitGrant, Assert.Single(view.Authorities).Id);

        // Separation of duties: the author of the week's links and measurements cannot close or decide.
        PilotWeekCloseCommand Close(string week, string outcome, string hash, long sequence = 0) =>
            new(week, outcome, "FICTIONAL review of the recorded week evidence", hash, sequence, PilotKitGrant);
        await PilotCode(await Send(c, root + "/week-closes", Close("W1", "NOT_ACCEPTED", w1.Outcome.Hash)), HttpStatusCode.Forbidden, "separation_of_duties_required");
        await PilotCode(await Send(c, root + "/decisions", new PilotProgramDecisionCommand("FIX", "FICTIONAL author attempt", view.Review.Hash, 0, PilotKitGrant)),
            HttpStatusCode.Forbidden, "separation_of_duties_required");

        var owner = Guid.NewGuid();
        PilotActor(host.Services, owner); await Token(c);
        await PilotCode(await Send(c, root + "/week-closes", Close("W1", "ACCEPTED", w1.Outcome.Hash)), HttpStatusCode.Conflict, "week_not_ready");
        await PilotCode(await Send(c, root + "/week-closes", Close("W1", "NOT_ACCEPTED", new string('0', 64))), HttpStatusCode.Conflict, "week_stale");
        await PilotCode(await Send(c, root + "/week-closes", Close("W2", "NOT_ACCEPTED", view.Weeks.Single(w => w.Definition.Week == "W2").Outcome.Hash)),
            HttpStatusCode.Conflict, "week_not_started");
        var closed = await PilotOk<PilotWeekCloseDto>(await Send(c, root + "/week-closes", Close("W1", "NOT_ACCEPTED", w1.Outcome.Hash)));
        Assert.Equal(("NOT_ACCEPTED", PilotProgramPolicy.NotReady, 1L, "ProductOwner", "CURRENT", owner),
            (closed.Outcome, closed.Recommendation, closed.Sequence, closed.Duty, closed.Validity, closed.ActorId));
        await PilotCode(await Send(c, root + "/week-closes", Close("W1", "NOT_ACCEPTED", w1.Outcome.Hash)), HttpStatusCode.Conflict, "close_sequence_conflict");

        var review = (await c.GetFromJsonAsync<PilotProgramView>($"/api/v1/{root}", Ct))!;
        PilotProgramDecisionCommand Decision(string d, string? hash = null) => new(d, "FICTIONAL: baselines and UAT incomplete", hash ?? review.Review.Hash, 0, PilotKitGrant);
        await PilotCode(await Send(c, root + "/decisions", Decision("EXPAND")), HttpStatusCode.Conflict, "expand_not_eligible");
        await PilotCode(await Send(c, root + "/decisions", Decision("FIX", new string('0', 64))), HttpStatusCode.Conflict, "program_stale");
        var decision = await PilotOk<PilotProgramDecisionDto>(await Send(c, root + "/decisions", Decision("FIX")));
        Assert.Equal(("FIX", PilotProgramPolicy.FixRecommended, review.Review.Hash, 1L, "CURRENT", owner),
            (decision.Decision, decision.Recommendation, decision.ProgramHash, decision.Sequence, decision.Validity, decision.ActorId));

        // AI/automation principals never record or decide, even with the appointment configured.
        PilotActor(host.Services, owner, "AI"); await Token(c);
        await PilotCode(await Send(c, root + "/links", new PilotLinkCommand("W1_DAILY_FEEDBACK", "PILOT_FEEDBACK", feedback.Id, null)), HttpStatusCode.Forbidden, "human_actor_required");
        await PilotCode(await Send(c, root + "/decisions", new PilotProgramDecisionCommand("STOP", "FICTIONAL AI attempt", review.Review.Hash, 1, PilotKitGrant)),
            HttpStatusCode.Forbidden, "human_actor_required");

        await using (var db = Db()) {
            Assert.Equal(10, await db.PilotProgramReceipts.Select(r => r.Action).Distinct().CountAsync(Ct));
            var saved = await db.PilotProgramDecisions.SingleAsync(Ct);
            Assert.Equal((owner, PilotKitGrant, "ProductOwner"), (saved.ActorId, saved.AuthorityId, saved.Duty));
            Assert.Contains("TUG_JOB_PREP_TIME", saved.SnapshotJson);
            Assert.Equal(2, await db.PilotChecklistLinks.CountAsync(Ct));
            Assert.Equal(1, await db.PilotChecklistLinks.CountAsync(l => l.WithdrawnAt == null, Ct));
            var audit = await db.AuditEvents.Where(a => a.EntityType.StartsWith("Pilot") && a.EntityType != "PilotGateDecision").ToArrayAsync(Ct);
            Assert.Contains(audit, a => a.EntityType == "PilotProgramDecision");
            Assert.All(audit, a => { Assert.DoesNotContain("FICTIONAL", a.ChangeSummary ?? ""); Assert.DoesNotContain("P-MASTER-01", a.ChangeSummary ?? ""); });
        }
        await using (var other = Db(seed.Tenants[1].Id)) {
            Assert.False(await other.PilotPrograms.AnyAsync(Ct));
            Assert.False(await other.PilotChecklistLinks.AnyAsync(Ct));
        }
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    [Trait("Requirement", "DEV-079")]
    public async Task Pilot_program_readers_without_permission_are_denied_feedback_needs_retention_and_the_fixture_is_opt_in_fictional() {
        using (var plain = Host()) {
            using var p = Client(plain); await Token(p);
            await PilotCode(await p.GetAsync("/api/v1/pilot-programs", Ct), HttpStatusCode.Forbidden, "permission_denied");
            await PilotCode(await Send(p, "pilot-programs", new PilotProgramCreateCommand("FICTIONAL pilot", SeedVessel.Id, null, new DateOnly(2026, 12, 1), "UTC")),
                HttpStatusCode.Forbidden, "permission_denied");
        }
        await using (var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        await using (var tx = await admin.Database.BeginTransactionAsync(Ct)) {
            var tenants = seed.Tenants.Select(t => t.Id).ToArray();
            await PilotProgramFixtures.AddMissingAsync(admin, tenants, Ct); await PilotProgramFixtures.AddMissingAsync(admin, tenants, Ct); await tx.CommitAsync(Ct);
        }
        var id = PilotProgramFixtures.ProgramId(seed.Tenants[0].Id);
        await using (var check = Db()) {
            var row = await check.PilotPrograms.SingleAsync(Ct);
            Assert.Equal((id, "SyntheticUnverified"), (row.Id, row.Provenance));
            Assert.False(await check.PilotChecklistLinks.AnyAsync(Ct));
            Assert.False(await check.PilotWeekCloses.AnyAsync(Ct));
            Assert.False(await check.PilotProgramDecisions.AnyAsync(Ct));
            Assert.False(await check.PilotMeasurements.AnyAsync(Ct));
        }
        using var host = PilotKitHost(retention: false); using var c = Client(host); await Token(c);
        await PilotCode(await c.GetAsync($"/api/v1/pilot-programs/{Guid.NewGuid()}", Ct), HttpStatusCode.NotFound, "resource_not_found");
        await PilotCode(await c.GetAsync($"/api/v1/pilot-programs/{id}?x=1", Ct), HttpStatusCode.BadRequest, "invalid_query");
        var view = (await c.GetFromJsonAsync<PilotProgramView>($"/api/v1/pilot-programs/{id}", Ct))!;
        Assert.Equal(("UNCONFIGURED", false), (view.FeedbackRetention, view.CanDecide));
        Assert.Empty(view.AllowedDecisions);
        await PilotCode(await Send(c, $"pilot-programs/{id}/feedback", new PilotFeedbackCommand("W1", "TUG", "OFFLINE", "NEGATIVE", "TUG_MASTER", "FICTIONAL sync slow", "P-TUG-01")),
            HttpStatusCode.ServiceUnavailable, "retention_unconfigured");
        // A synthetic program is never closed or decided.
        await PilotCode(await Send(c, $"pilot-programs/{id}/week-closes", new PilotWeekCloseCommand("W1", "NOT_ACCEPTED", "FICTIONAL synthetic close", new string('A', 64), 0, PilotKitGrant)),
            HttpStatusCode.Conflict, "synthetic_program_not_decidable");
        await PilotCode(await Send(c, $"pilot-programs/{id}/decisions", new PilotProgramDecisionCommand("STOP", "FICTIONAL synthetic decision", new string('A', 64), 0, PilotKitGrant)),
            HttpStatusCode.Conflict, "synthetic_program_not_decidable");
        await using var other = Db(seed.Tenants[1].Id);
        Assert.False(await other.PilotPrograms.AnyAsync(p => p.Id == id, Ct));
    }
}
