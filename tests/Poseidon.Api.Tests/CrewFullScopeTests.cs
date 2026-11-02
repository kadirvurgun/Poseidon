using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// Actual API host + independently owned PostgreSQL, synthetic grants only. AUTHORED / NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    private WebApplicationFactory<Program> RosterDeclarationHost(string version = "synthetic-presence-v1", string onboard = "SyntheticOnboard", bool declare = true) =>
        Host(crewRead: true, crewWrite: true).WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["CrewAssignmentPolicy:Version"] = version, ["CrewAssignmentPolicy:TimeZone"] = "UTC",
            ["CrewAssignmentPolicy:EffectiveStatuses:0"] = onboard, ["CrewAssignmentPolicy:PlannedStatuses:0"] = "SyntheticPlanned",
            ["CrewAssignmentPolicy:Provenances:0"] = "UserDeclaredUnverified", ["CrewAssignmentPolicy:RequireSourceObservation"] = "true",
            ["Authentication:Oidc:Memberships:0:Permissions:44"] = declare ? "crew.assignment.declare" : "unused"
        })));

    [Fact]
    public async Task Ordinary_roster_commands_preserve_policy_lineage_onboard_planned_period_and_current_catalog()
    {
        using var host = RosterDeclarationHost(); using var client = Client(host); await Token(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var onboard = NewCrew() with { From = today, To = today.AddDays(1), PositionId = SeedVessel.Id,
            PositionPolicyVersion = "synthetic-position-v1", DeclaredStatus = "SyntheticOnboard", AssignmentPolicyVersion = "synthetic-presence-v1", SourceObservedAt = DateTimeOffset.UtcNow.AddSeconds(-1) };
        var planned = onboard with { Id = Guid.NewGuid(), PositionId = null, DeclaredStatus = "SyntheticPlanned", From = today.AddDays(2), To = today.AddDays(3) };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, onboard)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, planned)).StatusCode);
        var profile = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{onboard.Id}", Ct))!;
        Assert.Equal("OnboardDeclaration", profile.Assignment.PresenceState); Assert.Equal("DeclaredUnverified", profile.Assignment.PositionState);
        var page = (await client.GetFromJsonAsync<CrewPage>($"/api/v1/{CrewUrl}", Ct))!;
        Assert.Equal("PlannedDeclaration", page.Items.Single(x => x.Id == planned.Id).PresenceState);
        Assert.All(page.Items, x => Assert.Equal("Unknown", x.EligibilityState));
        await using var db = Db();
        var saved = await db.CrewAssignments.AsNoTracking().SingleAsync(x => x.Id == onboard.Id, Ct);
        var revision = await db.CrewAssignmentRevisions.AsNoTracking().SingleAsync(x => x.AssignmentId == onboard.Id, Ct);
        Assert.Equal(saved.AssignmentPolicyFingerprint, revision.AssignmentPolicyFingerprint);
        Assert.Equal(saved.PositionPolicyFingerprint, revision.PositionPolicyFingerprint);
        Assert.Equal("synthetic-presence-v1", revision.AssignmentPolicyVersion);
        Assert.NotNull(saved.AssignmentPolicyFingerprint); Assert.NotNull(saved.PositionPolicyFingerprint);
        Assert.True(await db.AuditEvents.AnyAsync(x => x.EntityType == nameof(CrewAssignment) && x.ResourceId == onboard.Id, Ct));
    }

    [Fact]
    public async Task Same_version_changed_assignment_policy_is_stale_and_redeclaration_is_an_explicit_xmin_command()
    {
        using var host = RosterDeclarationHost(); using var client = Client(host); await Token(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = NewCrew() with { From = today, To = today.AddDays(2), DeclaredStatus = "SyntheticOnboard",
            AssignmentPolicyVersion = "synthetic-presence-v1", SourceObservedAt = DateTimeOffset.UtcNow.AddSeconds(-1) };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, command)).StatusCode);
        using var changedHost = RosterDeclarationHost(onboard: "ChangedSyntheticOnboard"); using var changed = Client(changedHost); await Token(changed);
        var stale = (await changed.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{command.Id}", Ct))!;
        Assert.Equal("Stale", stale.Assignment.PresenceState);
        var revised = command with { PersonId = stale.Assignment.PersonId, BaseVersion = stale.Assignment.Version, DeclaredStatus = "ChangedSyntheticOnboard" };
        Assert.Equal(HttpStatusCode.OK, (await Send(changed, CrewUrl, revised)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(changed, CrewUrl, revised)).StatusCode);
        var current = (await changed.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{command.Id}", Ct))!;
        Assert.Equal("OnboardDeclaration", current.Assignment.PresenceState); Assert.NotEqual(stale.Assignment.Version, current.Assignment.Version);
        var history = await changed.GetStringAsync($"/api/v1/{CrewUrl}/{command.Id}/history", Ct);
        Assert.Contains("synthetic-presence-v1", history); Assert.DoesNotContain("assignmentPolicyFingerprint", history);
    }

    [Fact]
    public async Task Current_catalog_change_masks_old_tuple_and_revoked_crew_rights_deny_history_before_any_revisions()
    {
        using var host = RosterDeclarationHost(); using var client = Client(host); await Token(client);
        var command = NewCrew() with { PositionId = SeedVessel.Id, PositionPolicyVersion = "synthetic-position-v1" };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, command)).StatusCode);
        using var changedHost = RosterDeclarationHost().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["CrewPositionPolicy:Version"] = "synthetic-position-v2"
        })));
        using var changed = Client(changedHost);
        var stale = (await changed.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{command.Id}", Ct))!;
        Assert.Equal("Stale", stale.Assignment.PositionState); Assert.Null(stale.Assignment.Rank); Assert.Null(stale.Assignment.Department);
        using var revokedHost = Host(crewRead: false); using var revoked = Client(revokedHost);
        var response = await revoked.GetAsync($"/api/v1/{CrewUrl}/{command.Id}/history", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.DoesNotContain("items", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Owning_input_consumer_distinguishes_real_declarations_from_withheld_inputs_and_never_grants_eligibility()
    {
        using var host = Host(crewRead: true, crewWrite: true, crewIdentity: true, certificateRead: true, certificateWrite: true);
        using var client = Client(host); await Token(client);
        // The seeded assignment deliberately has a synthetic credential. Create
        // an independent assignment/person to exercise the genuinely empty case.
        var assignment = NewCrew();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, assignment)).StatusCode);
        var path = $"{CrewUrl}/{assignment.Id}/owning-inputs";
        var before = await client.GetStringAsync("/api/v1/" + path, Ct);
        Assert.Contains("no_credential_declaration", before);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"{CrewUrl}/{assignment.Id}/certificates",
            new CredentialSaveCommand(Guid.NewGuid(), null, new(new(null, null, null, null, null, null))))).StatusCode);
        var observed = await client.GetStringAsync("/api/v1/" + path, Ct);
        Assert.Contains("DeclaredUnverified", observed); Assert.Contains("owning_evaluator_not_implemented", observed);
        Assert.DoesNotContain("ELIGIBLE", observed); Assert.DoesNotContain("Fit", observed);
        using var revokedHost = Host(crewRead: true, crewIdentity: true, certificateRead: false); using var revoked = Client(revokedHost);
        var masked = await revoked.GetStringAsync("/api/v1/" + path, Ct);
        Assert.DoesNotContain("DeclaredUnverified", masked); Assert.DoesNotContain("no_credential_declaration", masked);
        Assert.Contains("Withheld", masked);
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{foreign.Id}/crew/{assignment.Id}/owning-inputs", Ct)).StatusCode);
    }

    [Fact]
    public async Task Ordinary_noops_advance_xmin_and_history_is_paged_after_current_scope_authorization()
    {
        using var host = RosterDeclarationHost(); using var client = Client(host); await Token(client);
        var command = NewCrew();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, command)).StatusCode);
        var current = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{command.Id}", Ct))!;
        var version = current.Assignment.Version;
        for (var i = 0; i < 25; i++) {
            var response = await Send(client, CrewUrl, command with { PersonId = current.Assignment.PersonId, BaseVersion = version });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = (await response.Content.ReadFromJsonAsync<CrewSavedDto>(Ct))!;
            Assert.NotEqual(version, saved.Version); version = saved.Version;
        }
        var path = $"/api/v1/{CrewUrl}/{command.Id}/history";
        using var first = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(path, Ct));
        Assert.True(first.RootElement.GetProperty("hasMore").GetBoolean()); Assert.Equal(25, first.RootElement.GetProperty("items").GetArrayLength());
        using var second = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(path + "?offset=25", Ct));
        Assert.False(second.RootElement.GetProperty("hasMore").GetBoolean()); Assert.Single(second.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1, second.RootElement.GetProperty("items")[0].GetProperty("revision").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?offset=-1", Ct)).StatusCode);
        using var deniedHost = Host(crewRead: false); using var denied = Client(deniedHost);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(path + "?offset=25", Ct)).StatusCode);
        await using var db = Db(); Assert.Equal(26, await db.CrewAssignmentRevisions.CountAsync(x => x.AssignmentId == command.Id, Ct));
        Assert.Equal(26, await db.CommandReceipts.CountAsync(x => x.ResourceId == command.Id, Ct));
    }
}
