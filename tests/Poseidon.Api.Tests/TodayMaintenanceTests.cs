using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private string MaintenanceWhyPath(Guid plan) => $"/api/v1/why-engine?vesselId={SeedVessel.Id}&cardKey=maintenance-plan:{plan:D}";

    [Fact]
    public async Task Today_distinguishes_an_accessible_empty_maintenance_source_from_an_unavailable_module()
    {
        using var host = Host(technicalRead: true); using var client = Client(host); await Token(client);
        using var created = await Send(client, "vessels", new { companyId = SeedVessel.CompanyId, code = "TM-EMPTY", name = "SYNTHETIC empty maintenance source", kind = "Tug" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var vessel = (await created.Content.ReadFromJsonAsync<VesselDto>(Ct))!;
        var page = (await client.GetFromJsonAsync<TodayDto>($"/api/v1/poseidon-today?vesselId={vessel.Id}", Ct))!;
        Assert.Equal("Missing", page.Coverage.Single(c => c.Source == "TechnicalMaintenance").Status);
        Assert.DoesNotContain(page.Cards.Items, c => c.Source == "MaintenancePlan");
        Assert.Equal("DraftAbsent", Assert.Single(page.Cards.Items).Reason);
    }

    [Fact]
    public async Task Today_composes_current_maintenance_exceptions_with_stable_paging_and_recomputed_lineage()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var due = CurrentCalendar();
        var notDue = CurrentCalendar() with { Inputs = CurrentCalendar().Inputs with { IntervalValue = "100", LastDoneDate = DateOnly.FromDateTime(DateTime.UtcNow) }, ReferenceAt = DateTimeOffset.UtcNow };
        var review = CurrentCalendar() with { Inputs = new() };
        foreach (var command in new[] { due, notDue, review })
            Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        var page = (await client.GetFromJsonAsync<TodayDto>(TodayPath, Ct))!;
        Assert.Equal("Available", page.Coverage.Single(c => c.Source == "TechnicalMaintenance").Status);
        var first = page.Cards.Items[0]; Assert.Equal($"maintenance-plan:{due.Id:D}", first.Key);
        Assert.Equal("Attention", first.Priority); Assert.Equal("MaintenanceDue", first.Reason);
        Assert.Equal($"/app/maintenance-plan/{SeedVessel.Id:D}?id={due.Id:D}", first.Href);
        Assert.Equal(due.OwnerRoleId!.Value.ToString("D"), first.Owner);
        Assert.DoesNotContain(page.Cards.Items, x => x.Key == $"maintenance-plan:{notDue.Id:D}");
        var firstPage = (await client.GetFromJsonAsync<TodayDto>(TodayPath + "&offset=0&limit=1", Ct))!;
        Assert.True(firstPage.Cards.HasMore); Assert.Equal(first.Key, Assert.Single(firstPage.Cards.Items).Key);
        var secondPage = (await client.GetFromJsonAsync<TodayDto>(TodayPath + "&offset=1&limit=1", Ct))!;
        Assert.Equal(page.Cards.Items[1].Key, Assert.Single(secondPage.Cards.Items).Key);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(MaintenanceWhyPath(due.Id), Ct))!;
        Assert.Equal("TW-MAINTENANCE-01", why.Rule); Assert.Null(why.Inputs);
        Assert.NotNull(why.Maintenance); Assert.Equal(due.Id, why.Maintenance.PlanId);
        Assert.Equal(1, why.Maintenance.PlanRevision); Assert.Equal(1, why.Maintenance.CurrentAssetRevision);
        Assert.Equal(new TodayWhySourceDto("MaintenancePlan", due.Id, why.Maintenance.PlanRevision), why.Source);
        Assert.Equal("Due", why.Maintenance.Calculation.State); Assert.Equal("Unverified", why.Freshness);
        Assert.Contains("PlanningDraftNotVerified", why.Blockers);
        await using var db = Db();
        Assert.Single(await db.MaintenancePlanRevisions.Where(x => x.PlanId == due.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.ResourceId == due.Id).ToArrayAsync(Ct));
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), x => x.Action == "TodayWhyRead" && x.Result == "Success" && x.ResourceId == SeedVessel.Id);
    }

    [Theory]
    [InlineData("maintenance.read")]
    [InlineData("equipment.read")]
    public async Task Today_module_access_is_rechecked_before_maintenance_counts_identifiers_and_why(string removed)
    {
        using var host = Host(technicalRead: true); using var client = Client(host);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = settings.Memberships.Select(m => m with { Permissions = m.Permissions.Where(x => x != removed).ToArray() }).ToArray();
        var page = (await client.GetFromJsonAsync<TodayDto>(TodayPath, Ct))!;
        Assert.Equal("Unavailable", page.Coverage.Single(c => c.Source == "TechnicalMaintenance").Status);
        Assert.DoesNotContain(page.Cards.Items, x => x.Source == "MaintenancePlan");
        using var denied = await client.GetAsync(MaintenanceWhyPath(SeedVessel.Id), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain(SeedVessel.Id.ToString(), await denied.Content.ReadAsStringAsync(Ct));
        await using var db = Db();
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), x => x.Action == "TodayWhyRead" && x.Result == "Denied" && x.ResourceId == null);
    }

    [Fact]
    public async Task Today_why_recomputes_current_owner_and_never_reuses_the_list_owner_after_withdrawal()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var command = CurrentCalendar(); Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        var before = (await client.GetFromJsonAsync<TodayWhyDto>(MaintenanceWhyPath(command.Id), Ct))!;
        Assert.NotNull(before.Owner); Assert.Equal("MaintenanceDue", before.Reason);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = settings.Memberships.Select(m => m with { Permissions = m.Permissions.Where(x => x != "maintenance.owner.eligible").ToArray() }).ToArray();
        var after = (await client.GetFromJsonAsync<TodayWhyDto>(MaintenanceWhyPath(command.Id), Ct))!;
        Assert.Null(after.Owner); Assert.Equal("MaintenanceReviewRequired", after.Reason);
        Assert.NotNull(after.Maintenance); Assert.False(after.Maintenance.OwnerEligible);
        Assert.Equal("MaintenanceOwnerUnavailable", after.Maintenance.Calculation.Reason);
    }

    [Fact]
    public async Task Today_why_returns_not_found_after_current_plan_ceases_to_be_an_exception()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var command = CurrentCalendar(); Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MaintenanceWhyPath(command.Id), Ct)).StatusCode);
        var rows = (await client.GetFromJsonAsync<MaintenancePage>("/api/v1/" + PlansUrl, Ct))!;
        var row = rows.Items.Single(x => x.Id == command.Id);
        var changed = command with { BaseVersion = row.Version, ReferenceAt = DateTimeOffset.UtcNow,
            Inputs = command.Inputs with { IntervalValue = "100", LastDoneDate = DateOnly.FromDateTime(DateTime.UtcNow) } };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, changed)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(MaintenanceWhyPath(command.Id), Ct)).StatusCode);
        var page = (await client.GetFromJsonAsync<TodayDto>(TodayPath, Ct))!;
        Assert.DoesNotContain(page.Cards.Items, c => c.Key == $"maintenance-plan:{command.Id:D}");
    }

    [Fact]
    public async Task Today_maintenance_why_rejects_other_context_and_noncanonical_identifiers()
    {
        using var host = Host(technicalRead: true); using var client = Client(host);
        var other = seed.Vessels.First(x => x.TenantId != SeedVessel.TenantId);
        using var wrongPlan = await client.GetAsync(MaintenanceWhyPath(other.Id), Ct);
        Assert.Equal(HttpStatusCode.NotFound, wrongPlan.StatusCode);
        using var wrongVessel = await client.GetAsync($"/api/v1/why-engine?vesselId={other.Id}&cardKey=maintenance-plan:{other.Id:D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, wrongVessel.StatusCode);
        foreach (var key in new[] { "maintenance-plan:00000000-0000-0000-0000-000000000000", "maintenance-plan:bad", "maintenance-plan:../../private" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/why-engine?vesselId={SeedVessel.Id}&cardKey={Uri.EscapeDataString(key)}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Today_why_reassesses_revoked_observation_without_modifying_the_historical_calculation()
    {
        using var host = ObservationHost(); using var client = Client(host); await Token(client);
        var reading = Reading(); Assert.Equal(HttpStatusCode.OK, (await Send(client, ObservationPath, reading)).StatusCode);
        var command = new MaintenanceCommand(Guid.NewGuid(), null, SeedVessel.Id, SeedVessel.Id, null,
            new(TriggerType: "Meter", IntervalValue: "10", IntervalUnit: "hours", LastDoneMeter: "10", TimeZone: "UTC", ObservationId: reading.Id), DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, ObservationPath + $"/{reading.Id}/revoke", new { })).StatusCode);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(MaintenanceWhyPath(command.Id), Ct))!;
        Assert.NotNull(why.Maintenance); Assert.Equal("ObservationRevoked", why.Maintenance.ObservationState);
        Assert.Equal("ObservationRevoked", why.Maintenance.Calculation.Reason); Assert.Null(why.Maintenance.Calculation.NextDueMeter);
        var history = (await client.GetFromJsonAsync<MaintenanceHistoryRow[]>("/api/v1/" + PlansUrl + $"/{command.Id}/history", Ct))!;
        Assert.Equal("20", Assert.Single(history).Due.NextDueMeter);
    }
}
