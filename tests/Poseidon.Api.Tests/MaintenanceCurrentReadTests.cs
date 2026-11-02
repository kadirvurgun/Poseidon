using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private MaintenanceCommand CurrentCalendar() => new(Guid.NewGuid(), null, SeedVessel.Id, SeedVessel.Id,
        Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535"),
        new(TriggerType: "Calendar", IntervalValue: "1", IntervalUnit: "day",
            LastDoneDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), TimeZone: "UTC",
            Provenance: "SyntheticUnverified", Criticality: "DeclaredCritical"), PostgresInstant(DateTimeOffset.UtcNow.AddDays(-2)));

    // PostgreSQL timestamptz stores microseconds; pin the fixture at that exact
    // precision so history equality still detects any persisted time change.
    private static DateTimeOffset PostgresInstant(DateTimeOffset value) => new(value.Ticks - value.Ticks % 10, value.Offset);

    [Fact]
    public async Task Maintenance_current_read_recalculates_due_without_changing_historical_revision_or_receipts()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var command = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        var before = DateTimeOffset.UtcNow;
        var page = (await client.GetFromJsonAsync<MaintenancePage>("/api/v1/" + PlansUrl, Ct))!;
        var current = page.Items.Single(x => x.Id == command.Id);
        Assert.Equal("Due", current.Due.State); Assert.True(current.ReferenceAt >= before);
        Assert.Equal("planning-current-v1", current.Due.Algorithm);
        var history = (await client.GetFromJsonAsync<MaintenanceHistoryRow[]>("/api/v1/" + PlansUrl + $"/{command.Id}/history", Ct))!;
        var original = Assert.Single(history);
        Assert.Equal("NotDue", original.Due.State); Assert.Equal(command.ReferenceAt, original.ReferenceAt);
        await using var db = Db();
        Assert.Single(await db.MaintenancePlanRevisions.Where(x => x.PlanId == command.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.ResourceId == command.Id).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Maintenance_read_rechecks_owner_eligibility_after_same_session_provisioning_change()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var command = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = settings.Memberships.Select(m => m with {
            Permissions = m.Permissions.Where(p => p != "maintenance.owner.eligible").ToArray()
        }).ToArray();
        var page = (await client.GetFromJsonAsync<MaintenancePage>("/api/v1/" + PlansUrl, Ct))!;
        var current = page.Items.Single(x => x.Id == command.Id);
        Assert.Equal("ReviewRequired", current.Due.State); Assert.Equal("MaintenanceOwnerUnavailable", current.Due.Reason);
        var history = (await client.GetFromJsonAsync<MaintenanceHistoryRow[]>("/api/v1/" + PlansUrl + $"/{command.Id}/history", Ct))!;
        Assert.Equal("NotDue", Assert.Single(history).Due.State);
    }

    [Fact]
    public async Task Maintenance_read_requires_review_when_its_equipment_revision_changes()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var command = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        var equipment = (await client.GetFromJsonAsync<EquipmentSnapshot>("/api/v1/" + EquipmentUrl, Ct))!;
        var asset = equipment.Items.Single(x => x.Id == SeedVessel.Id);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, EquipmentUrl,
            NewEquipment() with { Id = asset.Id, BaseVersion = asset.Version, ParentId = asset.ParentId })).StatusCode);
        var page = (await client.GetFromJsonAsync<MaintenancePage>("/api/v1/" + PlansUrl, Ct))!;
        var current = page.Items.Single(x => x.Id == command.Id);
        Assert.Equal("ReviewRequired", current.Due.State); Assert.Equal("EquipmentRevisionChanged", current.Due.Reason);
        Assert.Null(current.Due.NextDueDate); Assert.Equal(asset.Revision, current.AssetRevision);
        var history = (await client.GetFromJsonAsync<MaintenanceHistoryRow[]>("/api/v1/" + PlansUrl + $"/{command.Id}/history", Ct))!;
        Assert.Equal("NotDue", Assert.Single(history).Due.State);
    }
}
