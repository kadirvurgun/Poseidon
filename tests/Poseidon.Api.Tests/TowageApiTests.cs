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

// S8 WP-D DEV-087 (18.01 row 88, T-TUG-008), DEV-088 (row 89, T-TUG-009), DEV-107 (row 108, T-RISK2-003):
// real PostgreSQL + actual host. Synthetic identities, appointments and FICTIONAL content only. A planner authors;
// a second synthetic person holding the server-provisioned Tug Master appointment approves/issues; a third is the
// briefed crew member. Nothing here records a GO, a permit or an expert-approved limit.
// AUTHORED; execution DEFERRED to CI (no local Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private string TowagePath(string root, string suffix = "") => $"{root}{suffix}?vessel={Tug.Id}";

    private TowagePlanContent TowagePlan(Guid release, string exchange = "FICTIONAL master/pilot/tug master exchange") => new(
        [new(Tug.Id, "CENTRE_LEAD_FORWARD", "TOWING_ON_LINE", true, "TUG_LINE", "FICTIONAL centre lead forward", null, release)],
        exchange, [new("WIND_SPEED", "25", "FICTIONAL company SMS reference")],
        new("12", "14", "English", "FICTIONAL lost communication: stop and hold"),
        new("FICTIONAL release on Tug Master order", "Tug Master"),
        [new(Guid.NewGuid(), "TOWLINE_PARTING", "FICTIONAL standby tug connects")],
        new(true, [new(Guid.NewGuid(), "FICTIONAL crew clear of the bight")]));

    private TowageBriefingContent TowageBriefing(Guid risk, Guid crew) => new("FICTIONAL SW 10 kn", "FICTIONAL 1 kn ebb", "FICTIONAL over 2 nm",
        [new(Guid.NewGuid(), "FICTIONAL girting")], [new(Guid.NewGuid(), "FICTIONAL tow angle limits")], [new(Guid.NewGuid(), "FICTIONAL emergency release")],
        "FICTIONAL every participant may stop the job", [new(Guid.NewGuid(), "FICTIONAL line under load near crew")], "FICTIONAL muster and VHF 16", risk,
        [new(crew, "DECK_CREW", Tug.Id)]);

    private async Task<TugReadinessView> TowageReadiness(HttpClient c, Guid assignment) =>
        (await c.GetFromJsonAsync<TugReadinessView>($"/api/v1/tug-readiness?vessel={Tug.Id}&assignment={assignment}", Ct))!;

    [Fact]
    [Trait("Requirement", "DEV-087")]
    public async Task Towage_plan_and_briefing_are_human_records_that_feed_readiness_and_the_fleet_high_risk_view()
    {
        using var host = ReadinessHost(true); using var c = Client(host);
        var planner = Guid.NewGuid(); var master = Guid.NewGuid(); var crew = Guid.NewGuid();
        RiskActor(host.Services, planner, RiskPermissions); await Token(c);
        var job = await SubmittedJob(c);
        var proposal = await RiskOk<TugAssignmentDto>(await Send(c, $"tug-assignments?vessel={Tug.Id}", Proposal(job)));
        var release = await RiskOk<TowEquipmentDto>(await Send(c, $"tug-tow-equipment?vessel={Tug.Id}", new TowEquipmentSaveCommand(Guid.NewGuid(), null,
            new TowEquipmentInputs("EmergencyRelease", "FICTIONAL release", null, null, null, "Serviceable", true, null, null, null))));

        // DEV-087: no plan yet; readiness communication stays UNKNOWN and the missing briefing is a FAIL.
        var empty = (await c.GetFromJsonAsync<TowagePlanView>($"/api/v1/towage-plans?vessel={Tug.Id}&job={job.Id}", Ct))!;
        Assert.Null(empty.Plan); Assert.True(empty.CanEdit); Assert.Contains(Tug.Id, empty.AssignedTugs);
        var before = await TowageReadiness(c, proposal.Id);
        Assert.Contains("communication:CommunicationSourceUnavailable", before.ReviewReasons);
        Assert.Contains("briefing:TowageBriefingMissing", before.CriticalBlockers); Assert.Equal(TugReadinessEngine.NotReady, before.State);

        var key = Guid.NewGuid().ToString();
        var create = new TowagePlanCreateCommand(Guid.NewGuid(), job.Id, TowagePlan(release.Id));
        var created = await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans"), create, key));
        Assert.Equal(("DRAFT", 1L, "UserDeclaredUnverified"), (created.State, created.Revision, created.Provenance));
        Assert.Equal(created.Id, (await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans"), create, key))).Id);
        await AssertCode(await Send(c, TowagePath("towage-plans"), create with { Id = Guid.NewGuid() }, key), HttpStatusCode.Conflict, "idempotency_key_reused");
        await AssertCode(await Send(c, TowagePath("towage-plans"), create with { Id = Guid.NewGuid() }), HttpStatusCode.Conflict, "version_conflict");
        var foreignRelease = TowagePlan(Guid.NewGuid());
        await AssertCode(await Send(c, TowagePath("towage-plans", $"/{created.Id}"), new TowagePlanUpdateCommand(created.Version, foreignRelease), update: true),
            (HttpStatusCode)422, "linked_resource_unavailable");
        var updated = await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans", $"/{created.Id}"),
            new TowagePlanUpdateCommand(created.Version, TowagePlan(release.Id, "FICTIONAL updated exchange")), update: true));
        Assert.Equal(2L, updated.Revision);
        var submitted = await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans", $"/{created.Id}/submit"), new TowageSubmitCommand(updated.Version, updated.Hash)));
        Assert.Equal("IN_REVIEW", submitted.State);
        // The planner holds the same synthetic appointment but authored the plan: separation of duties.
        var decide = new TowagePlanDecisionCommand(submitted.Version, submitted.Hash, "APPROVE", "FICTIONAL Tug Master plan review", TugMasterGrant);
        await AssertCode(await Send(c, TowagePath("towage-plans", $"/{created.Id}/decisions"), decide), HttpStatusCode.Forbidden, "separation_of_duties");

        RiskActor(host.Services, master, RiskPermissions); await Token(c);
        var approved = await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans", $"/{created.Id}/decisions"), decide));
        Assert.Equal(("APPROVED", approved.Revision), (approved.State, approved.ApprovedRevision!.Value));
        var afterPlan = await TowageReadiness(c, proposal.Id);
        Assert.DoesNotContain(afterPlan.Checks, x => x.Category == "communication" && x.Result != TugReadinessEngine.Pass);
        Assert.Contains(afterPlan.Checks, x => x.Category == "communication" && x.Reason == "ApprovedTowagePlanCommunication" && x.SourceId == created.Id);

        // DEV-088: risk link to an approved DEV-105 assessment (method publication and approval are separate people).
        var method = await PublishedRiskMethod(c, host.Services, planner, master);
        RiskActor(host.Services, planner, RiskPermissions); await Token(c);
        var hazard = new RiskHazard(Guid.NewGuid(), "Connect towline", "FICTIONAL girting", ["Capsize"], new("L2", "S2", "FICTIONAL rationale"),
            [new(Guid.NewGuid(), "FICTIONAL tow angle limit", "EXISTING", "Tug Master", true, ["BAR-1"])], ["BAR-1"], new("L1", "S2", "FICTIONAL rationale"));
        var riskContent = new RiskAssessmentContent("FICTIONAL towage JSA", "FICTIONAL harbour towage", "TOWAGE", "FICTIONAL scope", method.Id, [hazard], true,
            [new(Guid.NewGuid(), "FICTIONAL limits exceeded")], [new(planner, "PREPARER")]);
        var ra = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath(vessel: Tug.Id), new RiskAssessmentCreateCommand(Guid.NewGuid(), "FICTIONAL-TOW-" + Guid.NewGuid().ToString("N")[..6],
            riskContent, [new("TugJob", job.Id)])));
        var raSubmitted = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{ra.Id}/submit", Tug.Id), new RiskAssessmentSubmitCommand(ra.Version, ra.Hash)));
        RiskActor(host.Services, master, RiskPermissions); await Token(c);
        await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{ra.Id}/review", Tug.Id), new RiskAssessmentReviewCommand(raSubmitted.Version, raSubmitted.Hash, "APPROVE", "FICTIONAL HSE review record")));

        RiskActor(host.Services, planner, RiskPermissions); await Token(c);
        var briefingCreate = new TowageBriefingCreateCommand(Guid.NewGuid(), job.Id, TowageBriefing(ra.Id, crew) with { Weather = null });
        var draft = await RiskOk<TowageBriefingDto>(await Send(c, TowagePath("towage-risk-briefings"), briefingCreate));
        Assert.Equal(("DRAFT", 1L), (draft.State, draft.Revision));
        var view = (await c.GetFromJsonAsync<TowageBriefingView>($"/api/v1/towage-risk-briefings?vessel={Tug.Id}&job={job.Id}", Ct))!;
        Assert.Contains("weather_missing", view.Blockers); Assert.Equal("ATTENTION", view.Risk.State);
        var briefingUpdated = await RiskOk<TowageBriefingDto>(await Send(c, TowagePath("towage-risk-briefings", $"/{draft.Id}"),
            new TowageBriefingUpdateCommand(draft.Version, TowageBriefing(ra.Id, crew)), update: true));
        Assert.Equal(2L, briefingUpdated.Revision);

        RiskActor(host.Services, master, RiskPermissions); await Token(c);
        var issued = await RiskOk<TowageBriefingDto>(await Send(c, TowagePath("towage-risk-briefings", $"/{draft.Id}/issue"),
            new TowageBriefingIssueCommand(briefingUpdated.Version, briefingUpdated.Hash, TugMasterGrant)));
        Assert.Equal(("ISSUED", approved.Revision), (issued.State, issued.PlanRevision!.Value));
        Assert.Contains("briefing:BriefingAcknowledgementPending", (await TowageReadiness(c, proposal.Id)).CriticalBlockers);
        // The Tug Master is not a listed participant and cannot acknowledge for the crew member.
        var ack = new TowageBriefingAcknowledgeCommand(issued.Revision, issued.Hash, "ACK");
        await AssertCode(await Send(c, TowagePath("towage-risk-briefings", $"/{draft.Id}/acknowledgements"), ack), HttpStatusCode.Forbidden, "acknowledgment_role_required");
        RiskActor(host.Services, crew, "hse.risk.read"); await Token(c);
        await RiskOk<TowageBriefingDto>(await Send(c, TowagePath("towage-risk-briefings", $"/{draft.Id}/acknowledgements"), ack));
        await AssertCode(await Send(c, TowagePath("towage-risk-briefings", $"/{draft.Id}/acknowledgements"), ack), HttpStatusCode.Conflict, "version_conflict");
        var acknowledged = await TowageReadiness(c, proposal.Id);
        Assert.Contains(acknowledged.Checks, x => x.Category == "briefing" && x.Result == TugReadinessEngine.Pass);
        Assert.NotEqual(TugReadinessEngine.Ready, acknowledged.State); // weather stays UNKNOWN; the engine never issues GO

        // DEV-107 fleet projection: the job row carries every dimension, permit UNKNOWN, and the linked RA is not duplicated.
        RiskActor(host.Services, planner, [.. RiskPermissions, "hse.high_risk_work.read"]); await Token(c);
        var page = (await c.GetFromJsonAsync<HighRiskWorkPage>($"/api/v1/hse/high-risk-work?vesselId={Tug.Id}&workType=TOWAGE", Ct))!;
        var row = Assert.Single(page.Items, i => i.WorkId == job.Id);
        Assert.Equal(("TugJob", "UNKNOWN", "TOWAGE"), (row.WorkKind, row.PermitState, row.WorkType));
        Assert.NotEqual("NONE", row.NextAction.Kind);
        Assert.Contains(row.Why, d => d.Dimension == "permit" && d.Reason == "PermitToWorkSourceUnavailable");
        Assert.DoesNotContain(page.Items, i => i.WorkId == ra.Id);
        Assert.Contains("PermitToWorkSourceUnavailable", page.Coverage);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/hse/high-risk-work?score=1", Ct)).StatusCode);
        // Fleet (company) filter: only the company's vessels; ranking is worst-first over the complete dimension set
        // (live crew/equipment readiness included); an unknown company is indistinguishable from an empty one.
        var fleet = (await c.GetFromJsonAsync<HighRiskWorkPage>($"/api/v1/hse/high-risk-work?companyId={Tug.CompanyId}", Ct))!;
        Assert.Contains(fleet.Items, i => i.WorkId == job.Id);
        Assert.All(fleet.Items, i => Assert.Equal(Tug.CompanyId, fleet.Vessels.Single(v => v.Id == i.VesselId).CompanyId));
        var ranks = fleet.Items.Select(i => HighRiskWorkPolicy.Rank(i.Why)).ToArray();
        Assert.Equal(ranks.Order(), ranks);
        Assert.All(fleet.Items, i => Assert.Equal(i.CriticalBlockers > 0, HighRiskWorkPolicy.Rank(i.Why) == 0));
        var otherFleet = (await c.GetFromJsonAsync<HighRiskWorkPage>($"/api/v1/hse/high-risk-work?companyId={Guid.NewGuid()}", Ct))!;
        Assert.Empty(otherFleet.Items); Assert.False(otherFleet.HasMore);

        // Amendments open new revisions: the re-briefing needs new acknowledgements and the plan needs re-approval.
        var briefingAfterAck = (await c.GetFromJsonAsync<TowageBriefingView>($"/api/v1/towage-risk-briefings?vessel={Tug.Id}&job={job.Id}", Ct))!.Briefing!;
        var rebrief = await RiskOk<TowageBriefingDto>(await Send(c, TowagePath("towage-risk-briefings", $"/{draft.Id}/amend"), new TowageAmendCommand(briefingAfterAck.Version, "FICTIONAL weather changed")));
        Assert.Equal("DRAFT", rebrief.State);
        var planAmended = await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans", $"/{created.Id}/amend"), new TowageAmendCommand(approved.Version, "FICTIONAL tug change after approval")));
        Assert.Equal(("DRAFT", (long?)null), (planAmended.State, planAmended.ApprovedRevision));
        var reopened = await TowageReadiness(c, proposal.Id);
        Assert.Contains("communication:TowagePlanNotApproved", reopened.ReviewReasons);
        Assert.Contains("briefing:TowageBriefingNotIssued", reopened.CriticalBlockers);

        await using var db = Db();
        var decision = Assert.Single(await db.TowagePlanDecisions.Where(d => d.PlanId == created.Id).ToArrayAsync(Ct));
        Assert.Equal((master, TugMasterGrant, "TugMaster", "APPROVE"), (decision.ActorId, decision.AuthorityId, decision.Duty, decision.Decision));
        var stored = Assert.Single(await db.TowageBriefingAcknowledgements.Where(k => k.BriefingId == draft.Id).ToArrayAsync(Ct));
        Assert.Equal((crew, "ACK", issued.Revision), (stored.ActorId, stored.Choice, stored.Revision));
        Assert.Empty(await db.TugReadinessDecisions.Where(d => d.AssignmentId == proposal.Id).ToArrayAsync(Ct));
        var audit = await db.AuditEvents.Where(e => e.ResourceId == created.Id || e.ResourceId == draft.Id).ToArrayAsync(Ct);
        Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        Assert.Contains(audit, e => e.EntityType == nameof(TowagePlanDecision) && e.ActorId == master.ToString());
        Assert.Contains(audit, e => e.EntityType == nameof(TowageBriefingAcknowledgement) && e.ActorId == crew.ToString() && e.VesselId == Tug.Id);
        var history = await db.TowagePlanRevisions.FirstAsync(r => r.PlanId == created.Id && r.State == "APPROVED", Ct);
        history.ContentJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-107")]
    public async Task High_risk_work_requires_its_permission_and_never_leaks_out_of_scope_filters()
    {
        using var host = ReadinessHost(false); using var c = Client(host);
        RiskActor(host.Services, Guid.NewGuid(), RiskPermissions); await Token(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/hse/high-risk-work", Ct)).StatusCode);
        RiskActor(host.Services, Guid.NewGuid(), [.. RiskPermissions, "hse.high_risk_work.read"]); await Token(c);
        var hidden = (await c.GetFromJsonAsync<HighRiskWorkPage>($"/api/v1/hse/high-risk-work?vesselId={Guid.NewGuid()}", Ct))!;
        Assert.Empty(hidden.Items); Assert.False(hidden.HasMore);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/hse/high-risk-work?limit=500", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/hse/high-risk-work?workType=PAINTING", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/towage-plans?vessel={Tug.Id}&job={Guid.NewGuid()}", Ct)).StatusCode);
    }
    [Fact]
    [Trait("Requirement", "DEV-088")]
    public async Task Towage_fixture_is_opt_in_idempotent_fictional_and_seeds_no_approval_issue_or_acknowledgement()
    {
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            await TowageFixtures.AddMissingAsync(db, seed.Vessels, Ct); await TowageFixtures.AddMissingAsync(db, seed.Vessels, Ct); await tx.CommitAsync(Ct);
        }
        var jobs = seed.Vessels.Where(v => v.Kind == VesselKind.Tug).Select(v => v.Id).ToArray();
        var plans = jobs.Select(TowageFixtures.PlanId).ToArray(); var briefings = jobs.Select(TowageFixtures.BriefingId).ToArray();
        var seededPlans = await db.TowagePlans.Where(p => plans.Contains(p.Id)).ToArrayAsync(Ct);
        var seededBriefings = await db.TowageBriefings.Where(b => briefings.Contains(b.Id)).ToArrayAsync(Ct);
        Assert.Equal(jobs.Length, seededPlans.Length); Assert.Equal(jobs.Length, seededBriefings.Length);
        Assert.All(seededPlans, p => Assert.Equal(("DRAFT", "SyntheticUnverified", (long?)null, 1L), (p.State, p.Provenance, p.ApprovedRevision, p.Revision)));
        Assert.All(seededBriefings, b => Assert.Equal(("DRAFT", (Guid?)null, 1L), (b.State, b.IssuedBy, b.Revision)));
        Assert.Equal(jobs.Length, await db.TowagePlanRevisions.CountAsync(r => plans.Contains(r.PlanId), Ct));
        Assert.Empty(await db.TowagePlanDecisions.Where(d => plans.Contains(d.PlanId)).ToArrayAsync(Ct));
        Assert.Empty(await db.TowageBriefingAcknowledgements.Where(k => briefings.Contains(k.BriefingId)).ToArrayAsync(Ct));
        Assert.Empty(await db.TowageReceipts.Where(r => plans.Contains(r.ResourceId) || briefings.Contains(r.ResourceId)).ToArrayAsync(Ct));
    }
}
