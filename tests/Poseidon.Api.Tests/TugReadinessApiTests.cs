using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;
namespace Poseidon.Api.Tests;

// S7 WP-B DEV-083..086 (18.01 Backlog rows 84-87; 60.02 Rev2 paragraphs 49, 61, 108-128, 199, 201, 520):
// real PostgreSQL + actual host. Synthetic identities, appointments and FICTIONAL data only. The single
// test actor is both proposer and appointed Tug Master/Chief Engineer, which proves separation of duties
// rather than a successful human decision; nothing here records an acknowledgement, verification or GO.
public sealed partial class DirectoryWorkspaceTests
{
    private const string SyntheticRole = "91c26eb5-cf16-4553-981d-1c275358c535";
    private static readonly Guid TugMasterGrant = Guid.Parse("5a7b0000-0000-4000-8000-0000000000a1"), ChiefEngineerGrant = Guid.Parse("5a7b0000-0000-4000-8000-0000000000c1");

    private WebApplicationFactory<Program> ReadinessHost(bool appointed) {
        var config = new Dictionary<string, string?> {
            ["Authentication:Oidc:Memberships:0:Permissions:50"] = "tug.plan.write",
            ["Authentication:Oidc:Memberships:0:ActorKind"] = "Human",
        };
        if (appointed)
            foreach (var (index, id, duty) in new[] { (0, TugMasterGrant, "TugMaster"), (1, ChiefEngineerGrant, "ChiefEngineer") }) {
                var prefix = $"Authentication:Oidc:Memberships:0:TugOperationsAuthorities:{index}:";
                config[prefix + "Id"] = id.ToString(); config[prefix + "VesselId"] = Tug.Id.ToString(); config[prefix + "RoleId"] = SyntheticRole;
                config[prefix + "Duty"] = duty; config[prefix + "AppointmentSource"] = "Synthetic test appointment; not an operational appointment";
                config[prefix + "ValidFrom"] = "2026-01-01T00:00:00Z"; config[prefix + "ValidUntil"] = "2099-01-01T00:00:00Z";
            }
        return Host(tugRead: true, tugWrite: true, tugOwner: true, technicalRead: true, technicalWrite: true)
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config)));
    }

    private async Task<TugRequestDto> SubmittedJob(HttpClient c) {
        var draft = (await (await Send(c, TugUrl, CompleteDraft(DateTimeOffset.UtcNow.AddHours(2)))).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        return (await (await Lifecycle(c, draft.Id, "submit", new TugLifecycleCommand(draft.Version))).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
    }
    private TugAssignmentProposeCommand Proposal(TugRequestDto job, Guid? id = null, int fromHours = 2) => new(id ?? Guid.NewGuid(), job.Id, job.Version, Tug.Id,
        DateTimeOffset.UtcNow.AddHours(fromHours).ToString("yyyy-MM-ddTHH:mm:ssZ"), DateTimeOffset.UtcNow.AddHours(fromHours + 2).ToString("yyyy-MM-ddTHH:mm:ssZ"), "Lead");

    [Fact]
    public async Task Shore_proposal_is_idempotent_one_active_per_job_and_tug_and_only_for_submitted_requests() {
        using var host = ReadinessHost(false); using var c = Client(host); await Token(c);
        var draft = (await (await Send(c, TugUrl, CompleteDraft(DateTimeOffset.UtcNow.AddHours(2)))).Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, $"tug-assignments?vessel={Tug.Id}", Proposal(draft))).StatusCode);
        var job = await SubmittedJob(c);
        var command = Proposal(job); var key = Guid.NewGuid().ToString();
        var first = (await (await Send(c, $"tug-assignments?vessel={Tug.Id}", command, key)).Content.ReadFromJsonAsync<TugAssignmentDto>(Ct))!;
        Assert.Equal("Proposed", first.State); Assert.Equal("Pending", first.MasterAck); Assert.Equal(64, first.ScopeHash.Length); Assert.False(first.Replayed);
        var replay = (await (await Send(c, $"tug-assignments?vessel={Tug.Id}", command, key)).Content.ReadFromJsonAsync<TugAssignmentDto>(Ct))!;
        Assert.True(replay.Replayed); Assert.Equal(first.Id, replay.Id);
        var duplicate = await Send(c, $"tug-assignments?vessel={Tug.Id}", Proposal(job));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var list = (await c.GetFromJsonAsync<TugAssignmentList>($"/api/v1/tug-assignments?vessel={Tug.Id}&job={job.Id}", Ct))!;
        var item = Assert.Single(list.Items);
        Assert.False(item.CanRespond); Assert.Empty(item.Authorities); Assert.Null(item.Response);
        await using var db = Db();
        Assert.Equal(1, await db.TugAssignmentProposals.CountAsync(p => p.JobId == job.Id, Ct));
        Assert.Empty(await db.TugAssignmentResponses.Where(r => r.AssignmentId == first.Id).ToArrayAsync(Ct));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/v1/tug-assignments?vessel={Tug.Id}&job={job.Id}&extra=1", Ct)).StatusCode);
    }

    [Fact]
    public async Task Acknowledgement_needs_a_server_appointment_and_the_proposer_cannot_acknowledge_their_own_proposal() {
        using var host = ReadinessHost(false); using var c = Client(host); await Token(c);
        var job = await SubmittedJob(c);
        var proposal = (await (await Send(c, $"tug-assignments?vessel={Tug.Id}", Proposal(job))).Content.ReadFromJsonAsync<TugAssignmentDto>(Ct))!;
        var accept = new TugAssignmentRespondCommand(proposal.Version, proposal.ScopeHash, "ACCEPT", TugMasterGrant);
        var denied = await Send(c, $"tug-assignments/{proposal.Id}/respond?vessel={Tug.Id}", accept);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var appointed = ReadinessHost(true); using var m = Client(appointed); await Token(m);
        var own = await Send(m, $"tug-assignments/{proposal.Id}/respond?vessel={Tug.Id}", accept);
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode); // separation_of_duties: same person proposed
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(m, $"tug-assignments/{proposal.Id}/respond?vessel={Tug.Id}", accept with { Decision = "REJECT" })).StatusCode);
        await using var db = Db();
        Assert.Equal("Pending", (await db.TugAssignmentProposals.SingleAsync(p => p.Id == proposal.Id, Ct)).MasterAck);
        Assert.False(await db.TugAssignmentResponses.AnyAsync(r => r.AssignmentId == proposal.Id, Ct));
    }

    [Fact]
    public async Task Withdraw_cancels_with_reason_and_frees_the_tug_for_a_new_proposal() {
        using var host = ReadinessHost(false); using var c = Client(host); await Token(c);
        var job = await SubmittedJob(c);
        var proposal = (await (await Send(c, $"tug-assignments?vessel={Tug.Id}", Proposal(job))).Content.ReadFromJsonAsync<TugAssignmentDto>(Ct))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, $"tug-assignments/{proposal.Id}/withdraw?vessel={Tug.Id}", new { expectedVersion = proposal.Version })).StatusCode);
        var withdrawn = (await (await Send(c, $"tug-assignments/{proposal.Id}/withdraw?vessel={Tug.Id}", new TugAssignmentWithdrawCommand(proposal.Version, "FICTIONAL re-plan"))).Content.ReadFromJsonAsync<TugAssignmentDto>(Ct))!;
        Assert.Equal("Cancelled", withdrawn.State);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, $"tug-assignments/{proposal.Id}/withdraw?vessel={Tug.Id}", new TugAssignmentWithdrawCommand(withdrawn.Version, "again"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, $"tug-assignments?vessel={Tug.Id}", Proposal(job))).StatusCode);
    }

    [Fact]
    public async Task Readiness_is_advisory_unknown_sources_require_review_and_go_is_refused_while_no_go_is_recorded() {
        using var host = ReadinessHost(true); using var c = Client(host); await Token(c);
        var job = await SubmittedJob(c);
        var proposal = (await (await Send(c, $"tug-assignments?vessel={Tug.Id}", Proposal(job))).Content.ReadFromJsonAsync<TugAssignmentDto>(Ct))!;
        var view = (await c.GetFromJsonAsync<TugReadinessView>($"/api/v1/tug-readiness?vessel={Tug.Id}&assignment={proposal.Id}", Ct))!;
        Assert.NotEqual(TugReadinessEngine.Ready, view.State);
        Assert.Contains("communication:CommunicationSourceUnavailable", view.ReviewReasons);
        Assert.Contains("weather:WholeWindowWeatherSourceUnavailable", view.ReviewReasons);
        Assert.Contains("assignment:MasterAcknowledgementPending", view.ReviewReasons);
        Assert.Equal(TugReadinessEngine.DecisionAuthority, view.DecisionAuthority); Assert.True(view.CanDecide);
        var go = await Send(c, $"tug-readiness/decisions?vessel={Tug.Id}", new TugReadinessDecisionCommand(proposal.Id, view.AssessmentHash, "GO", "FICTIONAL reason", TugMasterGrant));
        Assert.Equal((HttpStatusCode)422, go.StatusCode);
        var stale = await Send(c, $"tug-readiness/decisions?vessel={Tug.Id}", new TugReadinessDecisionCommand(proposal.Id, new string('0', 64), "NO_GO", "FICTIONAL reason", TugMasterGrant));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var key = Guid.NewGuid().ToString();
        var noGo = new TugReadinessDecisionCommand(proposal.Id, view.AssessmentHash, "NO_GO", "FICTIONAL weather and comms unconfirmed", TugMasterGrant);
        var recorded = (await (await Send(c, $"tug-readiness/decisions?vessel={Tug.Id}", noGo, key)).Content.ReadFromJsonAsync<TugReadinessDecisionDto>(Ct))!;
        Assert.Equal("NO_GO", recorded.Decision); Assert.Equal(view.State, recorded.AssessedState); Assert.Equal("TugMaster", recorded.Duty);
        Assert.True((await (await Send(c, $"tug-readiness/decisions?vessel={Tug.Id}", noGo, key)).Content.ReadFromJsonAsync<TugReadinessDecisionDto>(Ct))!.Replayed);
        await using var db = Db();
        Assert.Single(await db.TugReadinessDecisions.Where(d => d.AssignmentId == proposal.Id).ToArrayAsync(Ct));
        Assert.Equal("Proposed", (await db.TugAssignmentProposals.SingleAsync(p => p.Id == proposal.Id, Ct)).State);
        var crew = (await c.GetFromJsonAsync<TugCrewReadinessView>($"/api/v1/tug-crew-readiness?vessel={Tug.Id}&assignment={proposal.Id}", Ct))!;
        Assert.Equal("Unavailable", crew.EvaluatorState); Assert.NotEqual(TugReadinessEngine.Ready, crew.State);
        Assert.Contains(crew.Checks, x => x.Reason == "TowageCompetence:CrewEligibilityEvaluatorUnavailable");
    }

    [Fact]
    public async Task Tow_equipment_is_declared_unverified_seed_stays_unknown_and_the_declarer_cannot_verify() {
        using var host = ReadinessHost(true); using var c = Client(host); await Token(c);
        var view = (await c.GetFromJsonAsync<TowEquipmentView>($"/api/v1/tug-equipment-readiness?vessel={Tug.Id}", Ct))!;
        Assert.NotEqual(TugReadinessEngine.Ready, view.Assessment.State);
        Assert.All(view.Assessment.Items.Where(i => i.Provenance == "SyntheticUnverified"), i => Assert.Empty(i.Verifications));
        var inputs = new TowEquipmentInputs("Winch", "FICTIONAL winch", "45", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Failed", true, null, null, null);
        var saved = (await (await Send(c, $"tug-tow-equipment?vessel={Tug.Id}", new TowEquipmentSaveCommand(Guid.NewGuid(), null, inputs))).Content.ReadFromJsonAsync<TowEquipmentDto>(Ct))!;
        Assert.Equal("UserDeclaredUnverified", saved.Provenance);
        var after = (await c.GetFromJsonAsync<TowEquipmentView>($"/api/v1/tug-equipment-readiness?vessel={Tug.Id}", Ct))!;
        Assert.Equal(TugReadinessEngine.NotReady, after.Assessment.State);
        Assert.Contains(after.Assessment.Checks, x => x.Reason == "Winch:CriticalEquipmentFailed");
        var verify = await Send(c, $"tug-tow-equipment/{saved.Id}/verifications?vessel={Tug.Id}", new TowEquipmentVerifyCommand(saved.Version, saved.Revision, "VERIFIED", ChiefEngineerGrant));
        Assert.Equal(HttpStatusCode.Forbidden, verify.StatusCode);
        var stale = await Send(c, $"tug-tow-equipment/{saved.Id}?vessel={Tug.Id}", new TowEquipmentSaveCommand(saved.Id, saved.Version + 1, inputs), update: true);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var foreign = await Send(c, $"tug-tow-equipment?vessel={Tug.Id}", new TowEquipmentSaveCommand(Guid.NewGuid(), null, inputs with { AssetId = Guid.NewGuid() }));
        Assert.Equal((HttpStatusCode)422, foreign.StatusCode);
        await using var db = Db();
        Assert.False(await db.TowEquipmentVerifications.AnyAsync(v => v.ItemId == saved.Id, Ct));
    }
}
