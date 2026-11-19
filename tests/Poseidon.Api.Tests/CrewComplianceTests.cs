using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// S7 WP-A DEV-047..051 (18.01 Backlog rows 48-52) on the actual host and real PostgreSQL
// (Integration via DirectoryWorkspaceTests). AUTHORED; execution DEFERRED (no Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid CrewSyntheticRole = Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535");
    private static void CrewCompliancePermissions(IServiceProvider services, params CrewAuthority[] authorities) {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorKind = "Human", CrewAuthorities = authorities, Permissions = [.. m.Permissions, "crew.competence.read", "crew.competence.write",
            "crew.competence.assess", "crew.eligibility.read", "crew.safe_manning.read", "crew.safe_manning.decide", "crew.credential.verify", "file.read"] };
    }
    private CrewAuthority CrewGrant(string scope, string duty) =>
        new(Guid.NewGuid(), SeedVessel.Id, scope, CrewSyntheticRole, duty, "FICTIONAL synthetic appointment", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
    private string CrewBase => $"vessels/{SeedVessel.Id}/crew";
    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    [Fact]
    [Trait("Requirement", "DEV-049")]
    public async Task Competence_matrix_records_gap_reasons_and_training_completion_is_never_competence()
    {
        await using var host = Host(crewRead: true, crewWrite: true); using var client = Client(host); CrewCompliancePermissions(host.Services); await Token(client);
        var competence = new CrewCompetenceRequirementCommand(Guid.NewGuid(), null, SeedVessel.Id, CrewRequirementTypes.Competence, "FICTIONAL-ECDIS", true, 60, null, null, null, null, "ACTIVE");
        using var saved = await Send(client, CrewBase + "/competence-matrix", competence); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var matrix = (await client.GetFromJsonAsync<CrewCompetenceMatrix>("/api/v1/" + CrewBase + "/competence-matrix", Ct))!;
        var row = Assert.Single(matrix.Items, r => r.Id == competence.Id); Assert.Equal("UserDeclaredUnverified", row.Provenance);
        // Stale version, unknown position and a link to a non-competence row are rejected.
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, CrewBase + "/competence-matrix", competence)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, CrewBase + "/competence-matrix", competence with { Id = Guid.NewGuid(), PositionId = Guid.NewGuid() })).StatusCode);
        // Training completion recorded against a competence requirement stays a GAP.
        var training = new CrewCompetenceRecordCommand(Guid.NewGuid(), null, competence.Id, CrewCompetenceKinds.TrainingCompletion, new(2026, 1, 1), null, "FICTIONAL course");
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{CrewBase}/{SeedVessel.Id}/competence", training)).StatusCode);
        var view = (await client.GetFromJsonAsync<CrewCompetenceView>($"/api/v1/{CrewBase}/{SeedVessel.Id}/competence?from={Today}&to={Today}", Ct))!;
        Assert.Contains(view.Evaluation.Gaps, g => g.RequirementId == competence.Id && g.Reason == "TRAINING_COMPLETION_IS_NOT_COMPETENCE");
        Assert.NotEqual(CrewEligibilityStates.Eligible, view.Evaluation.State);
        Assert.Contains(view.Evaluation.Reasons, r => r.Code == "WORK_REST_SOURCE_UNAVAILABLE");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/{CrewBase}/{SeedVessel.Id}/competence?from=2026-13-01", Ct)).StatusCode);
    }

    [Fact]
    [Trait("Requirement", "DEV-049")]
    public async Task Competence_sign_off_needs_an_appointment_and_the_recorder_never_signs_off()
    {
        await using var host = Host(crewRead: true, crewWrite: true); using var client = Client(host);
        var grant = CrewGrant("CompetenceAssessment", "Assessor"); CrewCompliancePermissions(host.Services); await Token(client);
        var requirement = new CrewCompetenceRequirementCommand(Guid.NewGuid(), null, SeedVessel.Id, CrewRequirementTypes.Familiarization, "FICTIONAL-FAM", true, null, null, null, null, null, "ACTIVE");
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewBase + "/competence-matrix", requirement)).StatusCode);
        var record = new CrewCompetenceRecordCommand(Guid.NewGuid(), null, requirement.Id, CrewCompetenceKinds.Familiarization, new(2026, 1, 1), null, null);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{CrewBase}/{SeedVessel.Id}/competence", record)).StatusCode);
        var signOff = new CrewCompetenceSignOffCommand(1, 0, grant.Id, "CONFIRMED", "FICTIONAL assessor rationale");
        var path = $"{CrewBase}/{SeedVessel.Id}/competence/{record.Id}/sign-off";
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, path, signOff)).StatusCode); // no appointment
        CrewCompliancePermissions(host.Services, grant); await Token(client);
        using var sod = await Send(client, path, signOff); Assert.Equal(HttpStatusCode.Forbidden, sod.StatusCode);
        Assert.Contains("separation_of_duties", await sod.Content.ReadAsStringAsync(Ct));
        await using var db = Db(); Assert.Equal(0, await db.CrewCompetenceSignOffs.CountAsync(s => s.RecordId == record.Id, Ct));
        Assert.Contains(await db.Set<SecurityEvent>().Where(e => e.Action == "CrewCompetenceCommand").Select(e => e.Result).ToArrayAsync(Ct), r => r == "separation_of_duties");
    }

    [Fact]
    [Trait("Requirement", "DEV-048")]
    public async Task Credential_assertion_requires_document_appointment_and_independent_human()
    {
        await using var host = Host(crewRead: true, crewWrite: true, crewIdentity: true, certificateRead: true, certificateWrite: true); using var client = Client(host);
        var grant = CrewGrant("CredentialVerification", "Crewing"); CrewCompliancePermissions(host.Services, grant); await Token(client);
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{CrewBase}/{SeedVessel.Id}/certificates", new CredentialSaveCommand(id, null,
            new(new("FICTIONAL-COC", null, null, new(2025, 1, 1), new(2030, 1, 1)))))).StatusCode);
        var view = (await client.GetFromJsonAsync<CrewCredentialVerificationView>($"/api/v1/{CrewBase}/{SeedVessel.Id}/certificates/{id}/verification", Ct))!;
        Assert.Equal("UNVERIFIED", view.Status); Assert.False(view.CanRecord); Assert.True(view.ActorIsDeclarer);
        Assert.Contains("CREDENTIAL_DOCUMENT_REQUIRED", view.Blockers); Assert.Contains("SEPARATION_OF_DUTIES_DECLARER_CANNOT_VERIFY", view.Blockers);
        using var denied = await Send(client, $"{CrewBase}/{SeedVessel.Id}/certificates/{id}/verification", new CrewCredentialAssertionCommand(1, 0, grant.Id, "Verified", "FICTIONAL check"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Contains("credential_document_required", await denied.Content.ReadAsStringAsync(Ct));
        var register = (await client.GetFromJsonAsync<CrewCertificateRegister>($"/api/v1/{CrewBase}/certificates", Ct))!;
        var listed = Assert.Single(register.Items, r => r.CredentialId == id); Assert.Equal("UNVERIFIED", listed.Status); Assert.Null(listed.AssertionState);
        await using var db = Db(); Assert.Equal(0, await db.CrewCredentialAssertions.CountAsync(a => a.CredentialId == id, Ct));
        Assert.Equal(actorId, (await db.CredentialRevisions.SingleAsync(r => r.CredentialId == id, Ct)).ActorId);
    }

    [Fact]
    [Trait("Requirement", "DEV-050")]
    public async Task Safe_manning_evaluation_is_advisory_review_required_without_sources_and_decision_pins_the_hash()
    {
        await using var host = Host(crewRead: true, crewWrite: true); using var client = Client(host);
        var grant = CrewGrant("SafeManningDecision", "Master"); CrewCompliancePermissions(host.Services, grant); await Token(client);
        var view = (await client.GetFromJsonAsync<SafeManningView>($"/api/v1/vessels/{SeedVessel.Id}/safe-manning/evaluate?from={Today}&to={Today}", Ct))!;
        Assert.Equal(CrewEligibilityStates.ReviewRequired, view.Evaluation.State);
        Assert.All(view.Evaluation.Members, m => Assert.NotEqual(CrewEligibilityStates.Eligible, m.State));
        Assert.Equal(CrewEligibilityPolicy.Authority, view.Evaluation.Authority); Assert.True(view.CanDecide); Assert.Null(view.LatestDecision);
        var path = $"vessels/{SeedVessel.Id}/safe-manning/decisions";
        var command = new SafeManningDecisionCommand(DateOnly.Parse(Today), DateOnly.Parse(Today), new string('0', 64), 0, grant.Id, "MANNING_NOT_ACCEPTED", "FICTIONAL master rationale");
        using var stale = await Send(client, path, command); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var recorded = await Send(client, path, command with { EvaluationHash = view.Evaluation.Hash }); Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        await using var db = Db(); var decision = await db.CrewSafeManningDecisions.SingleAsync(d => d.VesselId == SeedVessel.Id, Ct);
        Assert.Equal(view.Evaluation.Hash, decision.EvaluationHash); Assert.Equal(CrewEligibilityStates.ReviewRequired, decision.EvaluationState);
        Assert.Equal(actorId, decision.ActorId); Assert.Equal("Master", decision.Duty);
        var audit = await db.AuditEvents.Where(e => e.ResourceId == decision.Id).ToArrayAsync(Ct);
        Assert.Contains(audit, e => e.ChangeSummary != null && e.ChangeSummary.Contains("SafeManningDecisionRecord") && e.ChangeSummary.Contains("HUMAN_REVIEW_WORKFLOW"));
        Assert.DoesNotContain("FICTIONAL master rationale", JsonSerializer.Serialize(audit));
        // No appointment: the system never records the decision.
        CrewCompliancePermissions(host.Services); await Token(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, path, command with { EvaluationHash = view.Evaluation.Hash, ExpectedSequence = 1 })).StatusCode);
        decision.Rationale = "tamper"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-047")]
    public async Task Roster_search_and_filters_are_server_side_scoped_and_never_match_withheld_identity()
    {
        await using var host = Host(crewRead: true); using var client = Client(host);
        var all = (await client.GetFromJsonAsync<CrewPage>($"/api/v1/{CrewBase}", Ct))!; Assert.Null(all.Filter);
        var filtered = (await client.GetFromJsonAsync<CrewPage>($"/api/v1/{CrewBase}?status=REVIEW_REQUIRED", Ct))!;
        Assert.NotNull(filtered.MatchCount); Assert.All(filtered.Items, r => Assert.Equal("REVIEW_REQUIRED", r.ProfileStatus));
        Assert.All(filtered.Items, r => { Assert.Null(r.FullName); Assert.Equal("Withheld", r.PrivacyScope); });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/{CrewBase}?status=GO", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/{CrewBase}?unknown=1", Ct)).StatusCode);
        await using var db = Db(); Assert.Contains(await db.Set<SecurityEvent>().Where(e => e.Action == "CrewRead").Select(e => e.Result).ToArrayAsync(Ct), r => r == "Success");
    }

    [Fact]
    [Trait("Requirement", "DEV-051")]
    public async Task Protected_crew_values_never_appear_in_search_metrics_audit_ready_or_eligibility_and_access_is_audited()
    {
        const string canary = "FICTIONALCANARYZQX";
        await using var host = Host(crewRead: true, crewWrite: true, crewIdentity: true, certificateRead: true, certificateWrite: true, search: true, auditRead: true);
        using var client = Client(host); CrewCompliancePermissions(host.Services); await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{CrewBase}/{SeedVessel.Id}/certificates", new CredentialSaveCommand(Guid.NewGuid(), null,
            new(new("FICTIONAL-COC", canary, canary, null, null, canary), Notes: canary)))).StatusCode);
        foreach (var path in new[] { $"/api/v1/workspace/search?q={canary}", "/api/v1/metrics/definitions", "/api/v1/audit-ready",
                     $"/api/v1/{CrewBase}/certificates", $"/api/v1/vessels/{SeedVessel.Id}/safe-manning/evaluate", $"/api/v1/{CrewBase}?q={canary}" }) {
            using var response = await client.GetAsync(path, Ct);
            Assert.DoesNotContain(canary, await response.Content.ReadAsStringAsync(Ct));
        }
        await using var db = Db();
        Assert.DoesNotContain(canary, JsonSerializer.Serialize(await db.AuditEvents.Select(e => e.ChangeSummary).ToArrayAsync(Ct)));
        var events = await db.Set<SecurityEvent>().Where(e => e.Result == "Success").Select(e => e.Action).ToArrayAsync(Ct);
        Assert.Contains("CredentialRead", events); Assert.Contains("SafeManningRead", events); Assert.Contains("CrewRead", events);
    }

    [Fact]
    [Trait("Requirement", "DEV-049")]
    public async Task Crew_competence_fixture_is_add_missing_fictional_and_seeds_no_human_act()
    {
        for (var run = 0; run < 2; run++) {
            await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, new("system:crew-competence-fixture", "System", "crew-competence-fixture-" + run), administrativeImport: true);
            await using var tx = await db.Database.BeginTransactionAsync(Ct); await CrewCompetenceFixtures.AddMissingAsync(db, [SeedVessel], Ct); await tx.CommitAsync(Ct);
        }
        await using var check = Db();
        var rows = await check.CrewCompetenceRequirements.Where(r => r.VesselId == SeedVessel.Id && r.Provenance == "SyntheticUnverified").ToArrayAsync(Ct);
        Assert.Equal(CrewCompetenceFixtures.Rows.Length, rows.Length); Assert.All(rows, r => Assert.StartsWith("FICTIONAL-", r.Reference));
        Assert.Empty(await check.CrewCredentialAssertions.ToArrayAsync(Ct)); Assert.Empty(await check.CrewCompetenceSignOffs.ToArrayAsync(Ct));
        Assert.Empty(await check.CrewSafeManningDecisions.ToArrayAsync(Ct)); Assert.Empty(await check.CrewCompetenceRecords.ToArrayAsync(Ct));
    }
}
