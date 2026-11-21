using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;
namespace Poseidon.Api.Tests;

// Final-gate QA follow-up for the pilot program kit (N3b separation of duties, N3c link coverage, N3d trace vs
// completion). Real PostgreSQL + actual host; synthetic identities, a FICTIONAL Product Owner appointment and
// FICTIONAL content only. No real pilot result, sign-off or GO is produced.
public sealed partial class DirectoryWorkspaceTests
{
    private async Task<(HttpStatusCode Status, string? Code)> PilotRefusal(HttpResponseMessage response) {
        using (response) return (response.StatusCode, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    [Trait("Requirement", "DEV-077")]
    [Trait("Requirement", "DEV-078")]
    [Trait("Requirement", "DEV-079")]
    [Trait("Requirement", "DEV-121")]
    public async Task Pilot_links_verify_api_records_hide_other_tenants_turn_invalid_on_re_read_and_closers_exclude_evidence_authors() {
        using var host = PilotKitHost(); using var c = Client(host);
        Guid recorder = Guid.NewGuid(), commenter = Guid.NewGuid(), measurer = Guid.NewGuid(), owner = Guid.NewGuid();
        var ship = SeedVessel; Assert.Equal(VesselKind.Ship, ship.Kind);
        var tug = seed.Vessels.First(v => v.TenantId == ship.TenantId && v.Kind == VesselKind.Tug);
        var otherShip = seed.Vessels.First(v => v.TenantId == seed.Tenants[1].Id && v.Kind == VesselKind.Ship);
        SafetyActor(host.Services, recorder, SafetyEventPolicy.ReportPermission); await Token(c);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var program = await PilotOk<PilotProgramDto>(await Send(c, "pilot-programs", new PilotProgramCreateCommand("FICTIONAL follow-up pilot", ship.Id, tug.Id, today, "UTC")));
        var root = $"pilot-programs/{program.Id}";

        // A real operational record created through the API by a human (safety event draft on the program ship) links
        // and satisfies an item that needs a human-written trace; the trace is not a completion claim.
        var safetyEvent = await PilotOk<SafetyEventDto>(await Send(c, SafetyPath(),
            new SafetyEventCreateCommand(Guid.NewGuid(), SafetyEventPolicy.Named, new SafetyEventDraftFields(Category: "NEAR_MISS"))));
        var eventLink = await PilotOk<PilotLinkDto>(await Send(c, root + "/links", new PilotLinkCommand("W3_EVENT_FLOW", "SAFETY_EVENT", safetyEvent.Id, null)));
        Assert.Equal(("W3", true, recorder), (eventLink.Week, eventLink.AuditTracedAtLink, eventLink.ActorId));
        // Type mismatch: the same identifier claimed as another allowed type is not that record.
        await PilotCode(await Send(c, root + "/links", new PilotLinkCommand("W2_DUAL_SHIP_SAFETY", "RISK_ASSESSMENT", safetyEvent.Id, null)), (HttpStatusCode)422, "record_not_found");

        // Another tenant's record answers exactly like a missing record (no existence oracle); this tenant's synthetic
        // record is distinguishable only because it is visible here.
        await using (var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        await using (var tx = await admin.Database.BeginTransactionAsync(Ct)) { await WorkOrderFixtures.AddMissingAsync(admin, [ship, otherShip], Ct); await tx.CommitAsync(Ct); }
        var missing = await PilotRefusal(await Send(c, root + "/links", new PilotLinkCommand("W2_WORK_ORDER", "WORK_ORDER", Guid.NewGuid(), null)));
        var foreign = await PilotRefusal(await Send(c, root + "/links", new PilotLinkCommand("W2_WORK_ORDER", "WORK_ORDER", WorkOrderFixtures.Id(otherShip.Id), null)));
        Assert.Equal(((HttpStatusCode)422, "record_not_found"), missing);
        Assert.Equal(missing, foreign);
        await PilotCode(await Send(c, root + "/links", new PilotLinkCommand("W2_WORK_ORDER", "WORK_ORDER", WorkOrderFixtures.Id(ship.Id), null)),
            (HttpStatusCode)422, "synthetic_record_not_linkable");

        // DEV-077 "completed with audit trace": a human-written but only RELEASED work order is not a completed flow.
        var (order, released) = await ReleasedWorkOrder(c, host.Services, recorder, null);
        Assert.Equal("RELEASED", released.State);
        await PilotCode(await Send(c, root + "/links", new PilotLinkCommand("W2_WORK_ORDER", "WORK_ORDER", order.Id, null)), (HttpStatusCode)422, "record_not_completed");

        // W1 evidence by the recorder: feedback linked to the daily feedback item.
        var feedback = await PilotOk<PilotFeedbackDto>(await Send(c, root + "/feedback",
            new PilotFeedbackCommand("W1", "SHIP", "USABILITY", "NEUTRAL", "MASTER", "FICTIONAL certificate list loads slowly", "P-MASTER-02")));
        var feedbackLink = await PilotOk<PilotLinkDto>(await Send(c, root + "/links", new PilotLinkCommand("W1_DAILY_FEEDBACK", "PILOT_FEEDBACK", feedback.Id, null)));
        // A commenter records W1 feedback only; a measurer records a baseline only.
        PilotActor(host.Services, commenter); await Token(c);
        await PilotOk<PilotFeedbackDto>(await Send(c, root + "/feedback",
            new PilotFeedbackCommand("W1", "TUG", "OFFLINE", "NEGATIVE", "TUG_MASTER", "FICTIONAL sync took long after reconnect", "P-TUG-02")));
        PilotActor(host.Services, measurer); await Token(c);
        await PilotOk<PilotMeasurementDto>(await Send(c, root + "/measurements", new PilotMeasurementCommand("TUG_JOB_PREP_TIME", "BASELINE", 1800m, 5,
            today.AddDays(-30), today.AddDays(-1), "FICTIONAL-TIME-STUDY-2", "FICTIONAL stopwatch study of 5 jobs", 0)));

        async Task<PilotProgramView> View() => (await c.GetFromJsonAsync<PilotProgramView>($"/api/v1/{root}", Ct))!;
        PilotWeekCloseCommand Close(string week, string hash) => new(week, "NOT_ACCEPTED", "FICTIONAL review of the recorded week evidence", hash, 0, PilotKitGrant);
        PilotProgramDecisionCommand Decide(string hash) => new("FIX", "FICTIONAL: baselines and UAT incomplete", hash, 0, PilotKitGrant);

        // Separation of duties, server side (re-read inside the SERIALIZABLE transaction).
        PilotActor(host.Services, recorder); await Token(c);
        var view = await View();
        var w1 = view.Weeks.Single(w => w.Definition.Week == "W1");
        Assert.Equal(new[] { PilotProgramPolicy.LinkAuthor, PilotProgramPolicy.FeedbackAuthor }, w1.CloseDutyConflicts);
        Assert.Equal(new[] { PilotProgramPolicy.LinkAuthor, PilotProgramPolicy.FeedbackAuthor }, view.DecisionDutyConflicts);
        await PilotCode(await Send(c, root + "/week-closes", Close("W1", w1.Outcome.Hash)), HttpStatusCode.Forbidden, "separation_of_duties_required");
        // A link and feedback author (no measurement) cannot record the program decision either.
        await PilotCode(await Send(c, root + "/decisions", Decide(view.Review.Hash)), HttpStatusCode.Forbidden, "separation_of_duties_required");
        PilotActor(host.Services, commenter); await Token(c);
        Assert.Equal(new[] { PilotProgramPolicy.FeedbackAuthor }, (await View()).Weeks.Single(w => w.Definition.Week == "W1").CloseDutyConflicts);
        await PilotCode(await Send(c, root + "/week-closes", Close("W1", w1.Outcome.Hash)), HttpStatusCode.Forbidden, "separation_of_duties_required");
        PilotActor(host.Services, measurer); await Token(c);
        view = await View();
        Assert.Equal(new[] { PilotProgramPolicy.MeasurementAuthor }, view.Weeks.Single(w => w.Definition.Week == "W4").CloseDutyConflicts);
        Assert.Equal(new[] { PilotProgramPolicy.MeasurementAuthor }, view.DecisionDutyConflicts);
        await PilotCode(await Send(c, root + "/week-closes", Close("W4", view.Weeks.Single(w => w.Definition.Week == "W4").Outcome.Hash)),
            HttpStatusCode.Forbidden, "separation_of_duties_required");
        await PilotCode(await Send(c, root + "/decisions", Decide(view.Review.Hash)), HttpStatusCode.Forbidden, "separation_of_duties_required");
        // The measurer authored nothing W1 rests on, so may close W1 (an honest NOT_ACCEPTED).
        Assert.Empty(view.Weeks.Single(w => w.Definition.Week == "W1").CloseDutyConflicts);
        var closed = await PilotOk<PilotWeekCloseDto>(await Send(c, root + "/week-closes", Close("W1", w1.Outcome.Hash)));
        Assert.Equal((measurer, "CURRENT"), (closed.ActorId, closed.Validity));
        // Independent QA of 2177c023: with W1 closed by someone else, the commenter (author of W1 feedback only) still
        // cannot record the program decision that rests on that feedback.
        PilotActor(host.Services, commenter); await Token(c);
        view = await View();
        Assert.Equal(new[] { PilotProgramPolicy.FeedbackAuthor }, view.DecisionDutyConflicts);
        await PilotCode(await Send(c, root + "/decisions", Decide(view.Review.Hash)), HttpStatusCode.Forbidden, "separation_of_duties_required");

        // Database level: the same rule holds for a direct insert that bypasses the API.
        var tenant = ship.TenantId.ToString("D");
        var closeInsert = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"""
            INSERT INTO pilot."PilotWeekCloses" ("TenantId","Id","ProgramId","Week","Sequence","PreviousCloseId","Outcome","Recommendation","WeekHash","SnapshotJson",
              "Rationale","ActorId","AuthorityId","RoleId","Duty","AuthorityHash","RecordedAt")
            VALUES ('{tenant}', gen_random_uuid(), '{program.Id:D}', 'W3', 1, NULL, 'NOT_ACCEPTED', 'NOT_READY', repeat('A', 64), '{"{}"}',
              'FICTIONAL direct insert', '{recorder:D}', gen_random_uuid(), gen_random_uuid(), 'ProductOwner', repeat('A', 64), now())
            """));
        Assert.Equal(("23514", "Pilot week close requires separation of duties"), (closeInsert.SqlState, closeInsert.MessageText));
        var decisionInsert = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"""
            INSERT INTO pilot."PilotProgramDecisions" ("TenantId","Id","ProgramId","Sequence","PreviousDecisionId","Decision","Recommendation","ProgramHash","SnapshotJson",
              "Rationale","ActorId","AuthorityId","RoleId","Duty","AuthorityHash","RecordedAt")
            VALUES ('{tenant}', gen_random_uuid(), '{program.Id:D}', 1, NULL, 'FIX', 'FIX_RECOMMENDED', repeat('A', 64), '{"{}"}',
              'FICTIONAL direct insert', '{measurer:D}', gen_random_uuid(), gen_random_uuid(), 'ProductOwner', repeat('A', 64), now())
            """));
        Assert.Equal(("23514", "Pilot program decision requires separation of duties"), (decisionInsert.SqlState, decisionInsert.MessageText));
        // Migration 20261124100000_PilotDecisionFeedbackSeparation: a feedback-only author is refused by the trigger too.
        var feedbackDecisionInsert = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"""
            INSERT INTO pilot."PilotProgramDecisions" ("TenantId","Id","ProgramId","Sequence","PreviousDecisionId","Decision","Recommendation","ProgramHash","SnapshotJson",
              "Rationale","ActorId","AuthorityId","RoleId","Duty","AuthorityHash","RecordedAt")
            VALUES ('{tenant}', gen_random_uuid(), '{program.Id:D}', 1, NULL, 'CONTINUE', 'FIX_RECOMMENDED', repeat('A', 64), '{"{}"}',
              'FICTIONAL direct insert', '{commenter:D}', gen_random_uuid(), gen_random_uuid(), 'ProductOwner', repeat('A', 64), now())
            """));
        Assert.Equal(("23514", "Pilot program decision requires separation of duties"), (feedbackDecisionInsert.SqlState, feedbackDecisionInsert.MessageText));

        // An independent Product Owner (no authorship) records the decision.
        PilotActor(host.Services, owner); await Token(c);
        view = await View();
        Assert.Empty(view.DecisionDutyConflicts);
        var decision = await PilotOk<PilotProgramDecisionDto>(await Send(c, root + "/decisions", Decide(view.Review.Hash)));
        Assert.Equal(("FIX", owner), (decision.Decision, decision.ActorId));

        // The linked feedback disappears (simulating the retention purge): on re-read the link is INVALID, the item is no
        // longer satisfied and the W1 close bound to the old week hash is STALE. The API-created event stays verified.
        await Sql($"""DELETE FROM pilot."PilotFeedback" WHERE "TenantId" = '{tenant}' AND "Id" = '{feedback.Id:D}'""");
        view = await View();
        w1 = view.Weeks.Single(w => w.Definition.Week == "W1");
        var link = w1.Links.Single(l => l.Id == feedbackLink.Id);
        Assert.Equal(("INVALID", "record_not_found"), (link.Validity, link.Refusal));
        var item = w1.Outcome.Items.Single(i => i.Code == "W1_DAILY_FEEDBACK");
        Assert.Equal((PilotProgramPolicy.LinkInvalid, 0), (item.State, item.ValidLinks));
        Assert.Contains("LINK_RECORD_NOT_FOUND", item.Reasons);
        Assert.Equal("STALE", Assert.Single(w1.Closes).Validity);
        var w3 = view.Weeks.Single(w => w.Definition.Week == "W3");
        Assert.Equal("VALID", w3.Links.Single(l => l.Id == eventLink.Id).Validity);
        Assert.Equal(PilotProgramPolicy.Satisfied, w3.Outcome.Items.Single(i => i.Code == "W3_EVENT_FLOW").State);

        await using var db = Db();
        Assert.Equal(1, await db.PilotWeekCloses.CountAsync(Ct));
        Assert.Equal(1, await db.PilotProgramDecisions.CountAsync(Ct));
        await using var other = Db(seed.Tenants[1].Id);
        Assert.False(await other.PilotChecklistLinks.AnyAsync(Ct));
    }
}
