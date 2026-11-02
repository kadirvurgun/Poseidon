using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    // Called by the ordinary file/credential/source/independent-verification
    // fixture in ComplianceInputTests. No verified records are inserted directly.
    private async Task CheckQualifiedTugCrewComposition(HttpClient client, IServiceProvider services, bool satisfied, string scenario, Guid msmd)
    {
        CapacityRights(services); await Token(client);
        var settings = services.GetRequiredService<FederationSettings>();
        var policy = services.GetRequiredService<TugPlanningPolicy>();
        policy.Version = "synthetic-plan-v1"; policy.SourceVersion = TugDraftPlanning.SourceVersion;
        policy.NotBefore = DateTimeOffset.UnixEpoch; policy.ExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        policy.Types = []; policy.Contexts = []; policy.BpSources = [];
        policy.Scopes = [new(SeedVessel.TenantId, settings.Memberships[0].ActorId, SeedVessel.Id, [SeedVessel.Id])];
        using var created = await Send(client, $"tug-jobs?vessel={SeedVessel.Id}", Draft());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode); var job = (await created.Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        var url = $"tug-jobs/{job.Id}/plan?vessel={SeedVessel.Id}";
        var start = PostgresInstant(DateTimeOffset.UtcNow.AddMinutes(1)); var end = start.AddMinutes(5);
        using var window = await Send(client, url, new TugPlanCommand(null, job.Version, policy.Version, new("Window", start, end)), update: true);
        Assert.Equal(HttpStatusCode.OK, window.StatusCode); var first = (await window.Content.ReadFromJsonAsync<TugPlanDto>(Ct))!;
        using var assigned = await Send(client, url, new TugPlanCommand(first.Version, job.Version, policy.Version, new("Assign", TugId: SeedVessel.Id)), update: true);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        long before; await using (var db = Db()) before = await db.CommandReceipts.LongCountAsync(Ct);
        var actual = (await client.GetFromJsonAsync<JsonElement>("/api/v1/" + url, Ct)).GetProperty("evidence").Deserialize<TugPlanningEvidence>(RegulatoryQualificationEndpoints.Json)!;
        var crew = Assert.Single(actual.Crew!); Assert.Equal(msmd, crew.MsmdId);
        Assert.Equal(PostgresInstant(start), crew.WindowStart); Assert.Equal(PostgresInstant(end), crew.WindowEnd);
        var role = Assert.Single(crew.Roles);
        var unknown = scenario is "tug-endorsement-unknown" or "tug-endorsement-expired";
        Assert.Equal(unknown ? "ReviewRequired" : !satisfied || scenario == "tug-count-shortage" ? "Blocked" : "CurrentMinimumRoleCoverage", crew.State);
        Assert.Equal(1, role.Declared); Assert.Equal(unknown ? 1 : 0, role.Unknown);
        // A qualified person's own criteria remain true when the role needs two
        // people. The missing second person is counted separately, not recursively.
        Assert.Equal(!unknown && satisfied ? 1 : 0, role.Qualified);
        Assert.Equal(scenario == "tug-count-shortage" ? 2 : 1, role.Required);
        Assert.NotEqual("Ready", actual.Readiness); Assert.Contains("MasterDecisionSeparate", crew.Coverage);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>($"/api/v1/why-engine?vesselId={SeedVessel.Id}&cardKey=tug-job:{job.Id}", Ct))!;
        Assert.Equal(crew.State, Assert.Single(why.Tug!.Evidence!.Crew!).State);
        if (scenario == "tug-private-policy") {
            var configuration = services.GetRequiredService<IConfiguration>(); var original = configuration["CrewFilePolicy:Version"];
            configuration["CrewFilePolicy:Version"] = "FICTIONAL withdrawn certificate file policy";
            var withdrawn = (await client.GetFromJsonAsync<JsonElement>("/api/v1/" + url, Ct)).GetProperty("evidence").Deserialize<TugPlanningEvidence>(RegulatoryQualificationEndpoints.Json)!;
            Assert.Equal("ReviewRequired", Assert.Single(withdrawn.Crew!).State);
            Assert.Equal(1, Assert.Single(Assert.Single(withdrawn.Crew!).Roles).Unknown);
            configuration["CrewFilePolicy:Version"] = original;
        }
        var originalMemberships = settings.Memberships;
        settings.Memberships = settings.Memberships.Select(m => m with { Permissions = m.Permissions.Where(p => p != "crew.assessment.read").ToArray() }).ToArray();
        var denied = (await client.GetFromJsonAsync<JsonElement>("/api/v1/" + url, Ct)).GetProperty("evidence").Deserialize<TugPlanningEvidence>(RegulatoryQualificationEndpoints.Json)!;
        var hidden = Assert.Single(denied.Crew!); Assert.Equal("Unavailable", hidden.State);
        Assert.Null(hidden.MsmdId); Assert.Null(hidden.MsmdRevision); Assert.Null(hidden.ValidUntil); Assert.Empty(hidden.Roles);
        Assert.DoesNotContain(msmd.ToString(), JsonSerializer.Serialize(hidden));
        // Restore source-test read capabilities for its remaining original assertions.
        settings.Memberships = originalMemberships;
        await using var verify = Db(); Assert.Equal(before, await verify.CommandReceipts.LongCountAsync(Ct));
    }

    [Fact]
    public async Task Tug_crew_composition_requires_private_source_authority_before_document_ids_counts_and_today_why()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var job = await PlanningJob(client); await ChangePlan(client, job, null, new("Assign", TugId: Tug.Id));
        var evidence = (await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id), Ct)).GetProperty("evidence").Deserialize<TugPlanningEvidence>(RegulatoryQualificationEndpoints.Json)!;
        var crew = Assert.Single(evidence.Crew!); Assert.Equal("Unavailable", crew.State); Assert.Empty(crew.Roles);
        Assert.Null(crew.MsmdId); Assert.Null(crew.MsmdRevision); Assert.Null(crew.ValidUntil);
        var fact = Assert.Single(evidence.ReadinessFacts!, f => f.Source == "CrewMinimumManning");
        Assert.Equal("Unavailable", fact.State); Assert.Null(fact.Id); Assert.Null(fact.Revision);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        Assert.Equal(crew.State, Assert.Single(why.Tug!.Evidence!.Crew!).State);
    }

    [Fact]
    public async Task Tug_crew_composition_does_not_supply_a_clock_based_window_when_the_draft_end_is_missing()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client); var job = await PlanningJob(client);
        await ChangePlan(client, job, null, new("Assign", TugId: Tug.Id));
        CapacityRights(host.Services);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..settings.Memberships[0].Permissions, ..ComplianceInputContext.CrewPermissions] };
        var evidence = (await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id), Ct)).GetProperty("evidence").Deserialize<TugPlanningEvidence>(RegulatoryQualificationEndpoints.Json)!;
        var crew = Assert.Single(evidence.Crew!); Assert.Equal("ReviewRequired", crew.State);
        Assert.Equal("DeclaredFutureOrCurrentJobWindowRequired", crew.Reason); Assert.Null(crew.WindowStart); Assert.Null(crew.WindowEnd);
        Assert.Empty(crew.Roles); Assert.Null(crew.MsmdId);
    }
}
