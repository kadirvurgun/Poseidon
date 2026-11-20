using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;
namespace Poseidon.Api.Tests;

// S9 WP-A DEV-058 / T-S9-001 and DEV-059 / T-S9-002 (18.01 Backlog rows 59-60; 60.01 Rev3 paragraphs 220, 936, 952):
// real PostgreSQL + actual host. Synthetic identities and a FICTIONAL appointment only. No source can make the
// synthetic ship READY (emergency source unavailable, certificate inventory unestablished), so no RELEASE is ever
// recorded; the recorded human act is a HOLD bound to the exact advisory assessment.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid DepartureMasterGrant = Guid.Parse("5a7b0000-0000-4000-8000-0000000000d1");
    private Vessel DepartureShip => seed.Vessels.First(v => v.TenantId == SeedVessel.TenantId && v.Kind == VesselKind.Ship);

    private WebApplicationFactory<Program> DepartureHost(bool appointed) {
        var config = new Dictionary<string, string?> {
            ["Authentication:Oidc:Memberships:0:Permissions:60"] = "departure.readiness.read",
            ["Authentication:Oidc:Memberships:0:Permissions:61"] = "departure.decide",
            ["Authentication:Oidc:Memberships:0:Permissions:62"] = "crew.safe_manning.read",
            ["Authentication:Oidc:Memberships:0:Permissions:63"] = "certificate.read",
            ["Authentication:Oidc:Memberships:0:ActorKind"] = "Human",
        };
        if (appointed) {
            const string prefix = "Authentication:Oidc:Memberships:0:DepartureAuthorities:0:";
            config[prefix + "Id"] = DepartureMasterGrant.ToString(); config[prefix + "VesselId"] = DepartureShip.Id.ToString(); config[prefix + "RoleId"] = SyntheticRole;
            config[prefix + "Duty"] = "Master"; config[prefix + "AppointmentSource"] = "Synthetic test appointment; not an operational appointment";
            config[prefix + "ValidFrom"] = "2026-01-01T00:00:00Z"; config[prefix + "ValidUntil"] = "2099-01-01T00:00:00Z";
        }
        return Host(crewRead: true, technicalRead: true).WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config)));
    }

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    [Trait("Requirement", "DEV-058")]
    [Trait("Requirement", "DEV-059")]
    public async Task Departure_readiness_is_advisory_and_only_the_appointed_human_master_records_a_hold_bound_to_the_exact_assessment() {
        using var host = DepartureHost(true); using var c = Client(host); await Token(c);
        var ship = DepartureShip.Id;
        var from = DateOnly.FromDateTime(DateTime.UtcNow); var to = from.AddDays(2);
        var url = $"/api/v1/departure-readiness?vessel={ship}&from={Day(from)}&to={Day(to)}";
        var view = (await c.GetFromJsonAsync<DepartureReadinessView>(url, Ct))!;
        // DEV-058: four categories, no averaging, an explicit Why list; missing sources are UNKNOWN, never READY.
        Assert.Equal(DepartureReadinessEngine.Categories, view.Categories.Select(x => x.Category));
        Assert.NotEqual(DepartureReadinessEngine.Ready, view.State);
        Assert.Equal(DepartureReadinessEngine.Current, view.Mode);
        Assert.Contains("emergency:EMERGENCY_SOURCE_UNAVAILABLE", view.ReviewReasons);
        // S9 integration: this synthetic Master lacks hse.safety_event.read, so the DEV-060 input is UNKNOWN.
        Assert.Contains("safety:SAFETY_EVENT_SOURCE_UNAVAILABLE", view.ReviewReasons);
        Assert.Contains("statutory:CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED", view.ReviewReasons);
        Assert.Contains(view.Why, w => w.Category == "emergency" && w.Result == DepartureReadinessEngine.Unknown);
        Assert.DoesNotContain(view.Why, w => w.Result == DepartureReadinessEngine.Pass);
        Assert.Equal(new[] { "HOLD", "DENY" }, view.AllowedDecisions);
        Assert.True(view.CanDecide); Assert.Equal(DepartureMasterGrant, Assert.Single(view.Authorities).Id);
        Assert.Equal(64, view.AssessmentHash.Length);

        var path = $"departure-signoff?vessel={ship}";
        DepartureSignoffCommand Command(string decision, string? hash = null, long? sequence = null, DateOnly? end = null) =>
            new(from, end ?? to, hash ?? view.AssessmentHash, sequence ?? view.Sequence, decision, "FICTIONAL emergency drill status unconfirmed", DepartureMasterGrant);
        using (var release = await Send(c, path, Command("RELEASE"))) {
            Assert.Equal((HttpStatusCode)422, release.StatusCode);
            Assert.Contains("departure_release_not_permitted", await release.Content.ReadAsStringAsync(Ct));
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, path, Command("HOLD", hash: new string('0', 64)))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, path, Command("HOLD", sequence: view.Sequence + 5))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, path, Command("HOLD") with { Reason = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(c, $"departure-signoff?vessel={Tug.Id}", Command("HOLD"))).StatusCode);

        var key = Guid.NewGuid().ToString();
        using var recorded = await Send(c, path, Command("HOLD"), key);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var hold = (await recorded.Content.ReadFromJsonAsync<DepartureDecisionDto>(Ct))!;
        Assert.Equal(("HOLD", view.State, view.AssessmentHash, view.Sequence + 1, "Master", "CURRENT", false),
            (hold.Decision, hold.AssessedState, hold.AssessmentHash, hold.Sequence, hold.Duty, hold.Validity, hold.Replayed));
        var replay = (await (await Send(c, path, Command("HOLD"), key)).Content.ReadFromJsonAsync<DepartureDecisionDto>(Ct))!;
        Assert.True(replay.Replayed); Assert.Equal(hold.Id, replay.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, path, Command("DENY"), key)).StatusCode); // same key, different body

        // Immutable lineage: one append-only row with its receipt; the read side lists it CURRENT for the same window
        // and STALE once a material input (here the window) changes.
        await using (var db = Db()) {
            var row = Assert.Single(await db.DepartureDecisions.Where(d => d.Id == hold.Id).ToArrayAsync(Ct));
            Assert.Equal((actorId, DepartureMasterGrant, "Master", view.Sequence + 1), (row.ActorId, row.AuthorityId, row.Duty, row.Sequence));
            Assert.Equal(view.Sequence == 0, row.PreviousDecisionId == null);
            Assert.Single(await db.DepartureReceipts.Where(r => r.RecordId == hold.Id).ToArrayAsync(Ct));
        }
        var after = (await c.GetFromJsonAsync<DepartureReadinessView>(url, Ct))!;
        Assert.Equal(("CURRENT", hold.Sequence), (after.Decisions.Single(d => d.Id == hold.Id).Validity, after.Sequence));
        var changed = (await c.GetFromJsonAsync<DepartureReadinessView>($"/api/v1/departure-readiness?vessel={ship}&from={Day(from)}&to={Day(to.AddDays(1))}", Ct))!;
        Assert.Equal("STALE", changed.Decisions.Single(d => d.Id == hold.Id).Validity);

        // AI or administrator principals cannot sign even with the appointment configured.
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0].ActorKind = "AI";
        var ai = await Send(c, path, Command("DENY", sequence: hold.Sequence));
        Assert.Equal(HttpStatusCode.Forbidden, ai.StatusCode);
        Assert.Contains("departure_authority_required", await ai.Content.ReadAsStringAsync(Ct));
        await using (var db = Db()) Assert.Equal(1, await db.DepartureDecisions.CountAsync(d => d.VesselId == ship && d.Sequence > view.Sequence, Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-059")]
    public async Task Without_a_master_appointment_the_readiness_is_read_only_and_a_reader_without_permission_is_denied() {
        using var host = DepartureHost(false); using var c = Client(host); await Token(c);
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var url = $"/api/v1/departure-readiness?vessel={DepartureShip.Id}&from={Day(from)}&to={Day(from)}";
        var view = (await c.GetFromJsonAsync<DepartureReadinessView>(url, Ct))!;
        Assert.False(view.CanDecide); Assert.Empty(view.Authorities);
        var denied = await Send(c, $"departure-signoff?vessel={DepartureShip.Id}",
            new DepartureSignoffCommand(from, from, view.AssessmentHash, view.Sequence, "HOLD", "FICTIONAL tenant-wide manager attempt", DepartureMasterGrant));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync(url + "&extra=1", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/v1/departure-readiness?vessel={DepartureShip.Id}&from={Day(from)}&to={Day(from.AddDays(-1))}", Ct)).StatusCode);
        using var plain = Host(); using var p = Client(plain); await Token(p);
        Assert.Equal(HttpStatusCode.Forbidden, (await p.GetAsync(url, Ct)).StatusCode);
        await using var db = Db();
        Assert.False(await db.DepartureDecisions.AnyAsync(d => d.ActorId == actorId && d.RecordedAt > DateTimeOffset.UtcNow.AddMinutes(-1) && d.Reason.Contains("tenant-wide"), Ct));
    }
}
