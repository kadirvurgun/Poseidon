using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;
namespace Poseidon.Api.Tests;
public sealed partial class DirectoryWorkspaceTests
{
    private string ObservationPath => $"equipment-hierarchy/{SeedVessel.Id}/{SeedVessel.Id}/observations";
    private WebApplicationFactory<Program> ObservationHost(bool write = true) => Host(technicalRead: true, technicalWrite: true).WithWebHostBuilder(b =>
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
            ["Authentication:Oidc:Memberships:0:Permissions:27"] = write ? "maintenance.observation.write" : "unused",
            ["MaintenanceObservations:Sources:0:TenantId"] = seed.Tenants[0].Id.ToString(), ["MaintenanceObservations:Sources:0:VesselId"] = SeedVessel.Id.ToString(),
            ["MaintenanceObservations:Sources:0:AssetId"] = SeedVessel.Id.ToString(), ["MaintenanceObservations:Sources:0:Source"] = "synthetic-meter",
            ["MaintenanceObservations:Sources:0:Version"] = "synthetic-v1", ["MaintenanceObservations:Sources:0:Kind"] = "Meter", ["MaintenanceObservations:Sources:0:Unit"] = "hours",
            ["MaintenanceObservations:Sources:0:FreshnessSeconds"] = "3600", ["MaintenanceObservations:Sources:0:MaxScale"] = "4"
        })));
    private MaintenanceObservationCommand Reading(string? value = "19.5000") => new(Guid.NewGuid(),"synthetic-meter","synthetic-v1",1,"Meter","hours",value,DateTimeOffset.UtcNow.AddMinutes(-1));
    [Fact]
    public async Task Maintenance_observation_actual_capture_replay_and_plan_adapter_ignore_client_echo_values() {
        using var host = ObservationHost(); using var client = Client(host); await Token(client); var reading = Reading();
        Assert.Equal(HttpStatusCode.OK, (await Send(client,ObservationPath,reading)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client,ObservationPath,reading)).StatusCode);
        var plan = new MaintenanceCommand(Guid.NewGuid(),null,SeedVessel.Id,SeedVessel.Id,null,
            new(TriggerType: "Meter", IntervalValue: "10", IntervalUnit: "hours", LastDoneMeter: "10", CurrentMeter: "999", TimeZone: "UTC", ObservationId: reading.Id),DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, (await Send(client,PlansUrl,plan)).StatusCode);
        var saved = (await client.GetFromJsonAsync<MaintenancePage>("/api/v1/"+PlansUrl,Ct))!.Items.Single(x => x.Id == plan.Id);
        Assert.Equal("19.5000",saved.Inputs.CurrentMeter); Assert.Equal("20",saved.Due.NextDueMeter); Assert.Equal("ConfiguredDeclaredObservation",saved.ObservationState);
        await using var db=Db(); Assert.Single(await db.MaintenanceObservations.Where(x=>x.Id==reading.Id).ToArrayAsync(Ct));
        var row=await db.MaintenanceObservations.SingleAsync(x=>x.Id==reading.Id,Ct); row.Value="mutated";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Maintenance_observation_revocation_changes_current_assessment_without_rewriting_historic_plan() {
        using var host=ObservationHost(); using var client=Client(host); await Token(client); var reading=Reading(); await Send(client,ObservationPath,reading);
        var plan=new MaintenanceCommand(Guid.NewGuid(),null,SeedVessel.Id,SeedVessel.Id,null,new(TriggerType:"Meter",IntervalValue:"10",IntervalUnit:"hours",LastDoneMeter:"10",TimeZone:"UTC",ObservationId:reading.Id),DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK,(await Send(client,PlansUrl,plan)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Send(client,ObservationPath+$"/{reading.Id}/revoke",new {})).StatusCode);
        var saved=(await client.GetFromJsonAsync<MaintenancePage>("/api/v1/"+PlansUrl,Ct))!.Items.Single(x=>x.Id==plan.Id);
        Assert.Equal("ObservationRevoked",saved.ObservationState); Assert.Equal("20",saved.Due.NextDueMeter); Assert.Equal(1,saved.Revision);
    }
    [Fact]
    public async Task Maintenance_observation_same_session_write_revocation_allows_reads_then_blocks_capture_retry() {
        using var host=ObservationHost(); using var client=ImportSessionClient(host); var session=await ImportSession(client);var reading=Reading();
        Assert.Equal(HttpStatusCode.OK,(await Send(client,ObservationPath,reading)).StatusCode);
        using var deniedHost=ObservationHost(false);using var denied=ImportSessionClient(deniedHost,session.Cookie,session.Csrf);
        Assert.Equal(HttpStatusCode.OK,(await denied.GetAsync("/api/v1/"+ObservationPath,Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send(denied,ObservationPath,reading)).StatusCode);
    }
    [Fact]
    public async Task Maintenance_observation_exact_large_declaration_is_retained_but_unsupported_calculation_is_explicit() {
        using var host=ObservationHost();using var client=Client(host);await Token(client);var reading=Reading("123456789012345678901234567890.0001");
        Assert.Equal(HttpStatusCode.OK,(await Send(client,ObservationPath,reading)).StatusCode);
        var plan=new MaintenanceCommand(Guid.NewGuid(),null,SeedVessel.Id,SeedVessel.Id,null,new(TriggerType:"Meter",IntervalValue:"10",IntervalUnit:"hours",LastDoneMeter:"10",TimeZone:"UTC",ObservationId:reading.Id),DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK,(await Send(client,PlansUrl,plan)).StatusCode);
        var saved=(await client.GetFromJsonAsync<MaintenancePage>("/api/v1/"+PlansUrl,Ct))!.Items.Single(x=>x.Id==plan.Id);
        Assert.Equal("ObservationPrecisionUnsupported",saved.ObservationState);Assert.Null(saved.Due.NextDueMeter);
        await using var db=Db();Assert.Equal(reading.Value,(await db.MaintenanceObservations.SingleAsync(x=>x.Id==reading.Id,Ct)).Value);
    }
    [Fact]
    public async Task Maintenance_observation_unknown_source_unit_and_conflicting_UUID_do_not_write() {
        using var host=ObservationHost();using var client=Client(host);await Token(client);var reading=Reading();
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(client,ObservationPath,reading with {Source="invented"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(client,ObservationPath,reading with {Unit="cycles"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Send(client,ObservationPath,reading)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(client,ObservationPath,reading with {Value="20"})).StatusCode);
    }
    [Theory]
    [InlineData("2026-11-01T10:00:00")]
    [InlineData("2026-11-01")]
    [InlineData("2026-11-01T10:00:00Z\n")]
    public async Task Maintenance_observation_requires_an_explicit_full_ISO_offset_instant(string instant) {
        using var host=ObservationHost(); using var client=Client(host); await Token(client);
        var body=new {id=Guid.NewGuid(),source="synthetic-meter",policyVersion="synthetic-v1",sourceRevision=1,kind="Meter",unit="hours",value="1",observedAt=instant};
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(client,ObservationPath,body)).StatusCode);
    }
    [Fact]
    public async Task Maintenance_observation_synthetic_empty_source_is_add_missing_and_preserves_entered_observations() {
        using var host=ObservationHost();using var client=Client(host);await Token(client);var reading=Reading();await Send(client,ObservationPath,reading);
        await using(var admin=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,ActorContext.SeedImport(),true)) await SeedImporter.ImportAsync(admin,seed,Ct);
        await using var db=Db();var empty=await db.MaintenanceObservations.SingleAsync(x=>x.Id==SeedVessel.Id,Ct);
        Assert.Null(empty.Value);Assert.Null(empty.ObservedAt);Assert.Equal("SyntheticUnverified",empty.Provenance);
        Assert.Equal(reading.Value,(await db.MaintenanceObservations.SingleAsync(x=>x.Id==reading.Id,Ct)).Value);
    }
}
