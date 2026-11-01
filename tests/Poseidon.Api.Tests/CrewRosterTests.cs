using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private string CrewUrl => $"vessels/{SeedVessel.Id}/crew";
    private CrewSaveCommand NewCrew(Guid? id = null) => new(id ?? Guid.NewGuid(), null, null, null, null,
        null, new(2026, 11, 1), new(2026, 12, 1), null, null, null);

    [Fact]
    public async Task Crew_directory_write_and_spoofed_roles_do_not_grant_crew_access_or_counts()
    {
        using var host = Host(); using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Role", "Crewing");
        var denied = await client.GetAsync("/api/v1/" + CrewUrl, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var body = await denied.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(SeedVessel.Id.ToString(), body); Assert.DoesNotContain("items", body);
        await Token(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, CrewUrl, NewCrew())).StatusCode);
    }
    [Fact]
    public async Task Crew_masks_personal_fields_at_projection_and_rejects_personal_search()
    {
        using var host = Host(crewRead: true); using var client = Client(host);
        var page = (await client.GetFromJsonAsync<CrewPage>("/api/v1/" + CrewUrl, Ct))!;
        Assert.Single(page.Items); Assert.Null(page.Items[0].FullName); Assert.Null(page.Items[0].EmployeeNo);
        Assert.Equal("Withheld", page.Items[0].PrivacyScope); Assert.False(page.CanWriteIdentity);
        Assert.Equal("Unknown", page.Items[0].EligibilityState); Assert.Equal("Unknown", page.Items[0].CertificateState);
        var profile = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{SeedVessel.Id}", Ct))!;
        Assert.Equal("Withheld", profile.HealthStatus); Assert.Equal("Withheld", profile.ContactStatus);
        var bad = await client.GetAsync("/api/v1/" + CrewUrl + "?q=FICTIONAL", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.DoesNotContain("FICTIONAL", await bad.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Crew_wrong_vessel_cross_tenant_detail_and_history_disclose_no_identifiers()
    {
        using var host = Host(manage: false, read: false, crewRead: true); using var client = Client(host);
        foreach (var vessel in seed.Vessels.Where(x => x.Id != SeedVessel.Id)) {
            foreach (var suffix in new[] { "", $"/{vessel.Id}", $"/{vessel.Id}/history", "/positions" }) {
                var response = await client.GetAsync($"/api/v1/vessels/{vessel.Id}/crew{suffix}", Ct);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                var body = await response.Content.ReadAsStringAsync(Ct);
                Assert.DoesNotContain(vessel.Id.ToString(), body); Assert.DoesNotContain("items", body);
            }
        }
    }
    [Fact]
    public async Task Crew_identity_capability_is_explicit_and_writes_remain_unverified_without_plaintext_audit()
    {
        using var deniedHost = Host(crewRead: true, crewWrite: true); using var denied = Client(deniedHost); await Token(denied);
        var command = NewCrew() with { FullName = "FICTIONAL PRIVATE PERSON", EmployeeNo = "SYNTHETIC-PRIVATE" };
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(denied, CrewUrl, command)).StatusCode);
        using var host = Host(crewRead: true, crewWrite: true, crewIdentity: true); using var client = Client(host); await Token(client);
        var response = await Send(client, CrewUrl, command); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{command.Id}", Ct))!;
        Assert.Equal(command.FullName, detail.Assignment.FullName); Assert.Equal("Draft", detail.Assignment.AssignmentStatus);
        Assert.Equal("Unknown", detail.FitnessState);
        await using var db = Db();
        var logs = await db.AuditEvents.Select(x => x.ChangeSummary).ToArrayAsync(Ct);
        Assert.All(logs, value => { Assert.DoesNotContain(command.FullName!, value ?? ""); Assert.DoesNotContain(command.EmployeeNo!, value ?? ""); });
        Assert.Single(await db.CrewAssignmentRevisions.Where(x => x.AssignmentId == command.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Crew_exact_retry_survives_host_restart_and_changed_payload_conflicts_without_duplicate_effects()
    {
        var command = NewCrew(); var key = Guid.NewGuid().ToString();
        using (var host = Host(crewRead: true, crewWrite: true)) {
            using var client = Client(host); await Token(client); Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, command, key)).StatusCode);
        }
        using (var host = Host(crewRead: true, crewWrite: true)) {
            using var client = Client(host); await Token(client);
            var replay = await Send(client, CrewUrl, command, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            using var json = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct)); Assert.True(json.RootElement.GetProperty("replayed").GetBoolean());
            Assert.Equal(HttpStatusCode.Conflict, (await Send(client, CrewUrl, command with { To = new(2027, 1, 1) }, key)).StatusCode);
        }
        await using var db = Db(); Assert.Single(await db.CrewAssignments.Where(x => x.Id == command.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CrewAssignmentRevisions.Where(x => x.AssignmentId == command.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.Key == key).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Crew_missing_csrf_invalid_period_unscoped_person_and_wrong_position_have_no_business_effect()
    {
        using var host = Host(crewRead: true, crewWrite: true); using var client = Client(host);
        var command = NewCrew();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, CrewUrl, command)).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, CrewUrl, command with { To = command.From })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, CrewUrl, command with { PersonId = seed.Vessels.Last().Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, CrewUrl, command with { PositionId = seed.Vessels.Last().Id })).StatusCode);
        await using var db = Db(); Assert.False(await db.CrewAssignments.AnyAsync(x => x.Id == command.Id, Ct));
    }
    [Fact]
    public async Task Crew_stale_update_and_overlap_conflict_preserve_committed_revision()
    {
        using var host = Host(crewRead: true, crewWrite: true); using var client = Client(host); await Token(client);
        var original = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{SeedVessel.Id}", Ct))!.Assignment;
        var command = NewCrew(original.Id) with { PersonId = original.PersonId, BaseVersion = original.Version };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, command)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, CrewUrl, command)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, CrewUrl, NewCrew() with { PersonId = original.PersonId })).StatusCode);
        var current = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{original.Id}", Ct))!.Assignment;
        Assert.Equal(2, current.Revision); Assert.Equal(command.From, current.From);
    }
    [Fact]
    public async Task Crew_server_membership_revocation_blocks_old_session_before_projection()
    {
        using var host = Host(crewRead: true); using var client = Client(host);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/" + CrewUrl, Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<Poseidon.Api.FederationSettings>();
        settings.Memberships[0].Permissions[6] = "revoked";
        var result = await client.GetAsync("/api/v1/" + CrewUrl, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.DoesNotContain("items", await result.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Crew_person_edit_uses_independent_xmin_and_identity_authority_without_historical_pii_snapshots()
    {
        using var host = Host(crewRead: true, crewWrite: true, crewIdentity: true); using var client = Client(host); await Token(client);
        var profile = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{SeedVessel.Id}", Ct))!;
        var command = new CrewPersonCommand(profile.PersonVersion!.Value, "FICTIONAL amended name", null);
        var path = $"{CrewUrl}/{SeedVessel.Id}/person"; var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, path, command, key, true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, path, command, key, true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, path, command, update: true)).StatusCode);
        using var maskedHost = Host(crewRead: true, crewWrite: true); using var masked = Client(maskedHost); await Token(masked);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(masked, path, command, update: true)).StatusCode);
        var current = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{SeedVessel.Id}", Ct))!;
        Assert.Equal(command.FullName, current.Assignment.FullName);
        Assert.NotEqual(profile.PersonVersion, current.PersonVersion);
        await using var db = Db();
        Assert.All(await db.AuditEvents.Where(x => x.EntityType == nameof(Person)).ToArrayAsync(Ct), x => Assert.DoesNotContain(command.FullName!, x.ChangeSummary ?? ""));
    }
    [Fact]
    public async Task Crew_concurrent_duplicate_commands_have_one_committed_effect_and_can_be_replayed()
    {
        using var host = Host(crewRead: true, crewWrite: true); using var client = Client(host); await Token(client);
        var command = NewCrew(); var key = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(Send(client, CrewUrl, command, key), Send(client, CrewUrl, command, key));
        Assert.Contains(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(results, x => Assert.Contains(x.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, command, key)).StatusCode);
        await using var db = Db(); Assert.Single(await db.CrewAssignments.Where(x => x.Id == command.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CrewAssignmentRevisions.Where(x => x.AssignmentId == command.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.Key == key).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Crew_required_observation_outage_withholds_reads_and_prevents_mutation()
    {
        using var host = Host(crewRead: true, crewWrite: true); using var client = Client(host); await Token(client);
        var command = NewCrew();
        // This test's independently owned PostgreSQL database only; never a shared/production schema.
        await Sql("CREATE FUNCTION audit.crew_test_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic observation outage'; END; $$; CREATE TRIGGER crew_test_outage BEFORE INSERT ON audit.\"SecurityEvents\" FOR EACH STATEMENT EXECUTE FUNCTION audit.crew_test_outage();");
        var read = await client.GetAsync("/api/v1/" + CrewUrl, Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
        Assert.DoesNotContain("items", await read.Content.ReadAsStringAsync(Ct));
        var write = await Send(client, CrewUrl, command); Assert.Equal(HttpStatusCode.ServiceUnavailable, write.StatusCode);
        var body = await write.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("synthetic observation outage", body);
        await using var db = Db(); Assert.False(await db.CrewAssignments.AnyAsync(x => x.Id == command.Id, Ct));
    }
}
