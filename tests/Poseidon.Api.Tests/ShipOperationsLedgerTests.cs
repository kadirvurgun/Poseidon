using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-100/T-OPS2-004 ROB continuity ledger and DEV-101/T-OPS2-005 SOF timeline,
// actual HTTP/PG sources (Integration; needs Docker PostgreSQL).
public sealed partial class DirectoryWorkspaceTests
{
    private static Dictionary<string,ShipOpsFact> LedgerMovement(string date,string before,string consumed,string after)=>new() {
        ["record_date"]=new("Known",date),["record_type"]=new("Known","Consumption"),["fuel_type"]=new("Known","VLSFO"),
        ["rob_before_mt"]=new("Known",before),["quantity_received_mt"]=new("Known","0"),["quantity_consumed_mt"]=new("Known",consumed),["rob_after_mt"]=new("Known",after)};

    [Fact]
    public async Task ShipOps_fuel_ledger_flags_rob_gap_review_required_read_only_without_correcting_declarations() {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);
        var first=new ShipOpsDraftCommand(Guid.NewGuid(),0,LedgerMovement("2026-01-01T12:00:00Z","120.0","4.5","115.5"),"INITIAL_AUTHORING");
        var second=new ShipOpsDraftCommand(Guid.NewGuid(),0,LedgerMovement("2026-01-02T12:00:00Z","115.0","4.0","111.0"),"INITIAL_AUTHORING");
        foreach(var command in new[]{second,first}){using var saved=await ShipOpsSend(client,"Fuel",command);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);}
        var undated=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){["bdn_no"]=new("Known","FICTIONAL")},"INITIAL_AUTHORING");
        using(var saved=await ShipOpsSend(client,"Fuel",undated))Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        await using(var before=Db()){
            var revisions=await before.ShipOperationRevisions.CountAsync(Ct);
            using var response=await client.GetAsync(ShipOpsPath("Fuel",suffix:"/ledger"),Ct);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            Assert.Equal("no-store",response.Headers.CacheControl?.ToString());
            var ledger=(await response.Content.ReadFromJsonAsync<FuelLedgerDto>(Ct))!;
            Assert.Equal("mt",ledger.Unit);Assert.Equal(ShipOperationsLedger.FuelGrouping,ledger.Grouping);Assert.False(ledger.Truncated);
            Assert.Equal([first.Id,second.Id],ledger.Entries.Select(e=>e.Record.Id));Assert.Equal(undated.Id,Assert.Single(ledger.Unplaced).Id);
            var gap=ledger.Entries[1].Assessment;Assert.Equal("REVIEW_REQUIRED",gap.ReviewState);Assert.Equal("-0.5",gap.Continuity.DifferenceMt);
            Assert.Contains("ROB_CONTINUITY_GAP",gap.Flags);Assert.Equal("115.0",ledger.Entries[1].Record.Fields["rob_before_mt"].Value);
            Assert.All(ledger.Entries,e=>{Assert.Equal("DRAFT",e.Record.State);Assert.Equal("PENDING",e.Record.Fields["verification_state"].Value);});
            await using var after=Db();Assert.Equal(revisions,await after.ShipOperationRevisions.CountAsync(Ct));
        }
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(ShipOpsPath("Fuel",suffix:"/ledger")+"&offset=1",Ct)).StatusCode);
    }

    [Fact]
    public async Task ShipOps_fuel_ledger_over_window_reports_history_truncated_boundary_not_a_gap() {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);
        var start=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);var ids=new List<Guid>();
        for(var i=0;i<ShipOperationsEndpoints.LedgerWindow+1;i++) {
            var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,LedgerMovement(start.AddHours(i).ToString("yyyy-MM-ddTHH:mm:ssZ"),(1000-i).ToString(),"1",(999-i).ToString()),"INITIAL_AUTHORING");
            using var saved=await ShipOpsSend(client,"Fuel",command);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);ids.Add(command.Id);
        }
        var ledger=(await client.GetFromJsonAsync<FuelLedgerDto>(ShipOpsPath("Fuel",suffix:"/ledger"),Ct))!;
        Assert.True(ledger.Truncated);Assert.Contains("HISTORY_TRUNCATED",ledger.Qualifications);Assert.Contains("TANK_REFERENCE_UNAVAILABLE",ledger.Qualifications);
        Assert.Equal(ShipOperationsEndpoints.LedgerWindow,ledger.Entries.Length);Assert.DoesNotContain(ledger.Entries,e=>e.Record.Id==ids[0]);
        var boundary=ledger.Entries[0].Assessment;Assert.Equal(ids[1],boundary.RecordId);
        Assert.Equal("UNKNOWN",boundary.Continuity.State);Assert.Equal("HISTORY_TRUNCATED",boundary.Continuity.Reason);Assert.DoesNotContain("ROB_CONTINUITY_GAP",boundary.Flags);
        Assert.All(ledger.Entries.Skip(1),e=>Assert.Equal("NO_DISCREPANCY_DETECTED",e.Assessment.Continuity.State));
    }

    [Fact]
    public async Task ShipOps_port_timeline_orders_events_with_exact_durations_and_no_fabricated_signature() {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);
        var open=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){["event_start"]=new("Known","2026-01-01T12:00:00+02:00"),["activity_type"]=new("Known","Arrival")},"INITIAL_AUTHORING");
        var closed=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){["event_start"]=new("Known","2026-01-01T11:00:00Z"),["event_end"]=new("Known","2026-01-01T12:00:00Z"),
            ["activity_type"]=new("Known","Loading"),["delay_reason"]=new("Known","FICTIONAL delay")},"INITIAL_AUTHORING");
        foreach(var command in new[]{closed,open}){using var saved=await ShipOpsSend(client,"Port",command);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);}
        var timeline=(await client.GetFromJsonAsync<PortTimelineDto>(ShipOpsPath("Port",suffix:"/timeline"),Ct))!;
        Assert.Equal([open.Id,closed.Id],timeline.Entries.Select(e=>e.Record.Id));Assert.Equal("tick",timeline.DurationUnit);
        Assert.Equal("UNKNOWN",timeline.Entries[0].Assessment.DurationState);Assert.Contains("EVENT_END_NOT_DECLARED",timeline.Entries[0].Assessment.Flags);
        Assert.Equal("36000000000",timeline.Entries[1].Assessment.DurationTicks);Assert.Contains("DELAY_DECLARED",timeline.Entries[1].Assessment.Flags);
        Assert.Contains("SOF_SIGNATURE_NOT_RECORDED",timeline.Qualifications);Assert.All(timeline.Entries,e=>Assert.Equal("DRAFT",e.Record.Fields["sof_state"].Value));
    }

    [Fact]
    public async Task ShipOps_ledger_and_timeline_deny_missing_permission_foreign_vessel_and_tug() {
        await using var host=Host();using var client=Client(host);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(ShipOpsPath("Fuel",suffix:"/ledger"),Ct)).StatusCode);
        ShipOpsPermissions(host.Services,write:false);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(ShipOpsPath("Port",suffix:"/timeline"),Ct)).StatusCode);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(ShipOpsPath("Fuel",suffix:"/ledger",vessel:foreign.Id),Ct)).StatusCode);
        var tug=seed.Vessels.First(v=>v.Kind==VesselKind.Tug);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(ShipOpsPath("Port",suffix:"/timeline",vessel:tug.Id),Ct)).StatusCode);
    }

    [Fact]
    public async Task ShipOps_synthetic_ledger_fixture_demonstrates_review_required_gap_without_verification() {
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:shipops-fixture","System","shipops-ledger"),administrativeImport:true)) {
            await using var tx=await db.Database.BeginTransactionAsync(Ct);await ShipOperationsFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);
        }
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services,write:false);
        var ledger=(await client.GetFromJsonAsync<FuelLedgerDto>(ShipOpsPath("Fuel",suffix:"/ledger"),Ct))!;
        var b=ledger.Entries.Single(e=>e.Record.Id==ShipOperationsFixtures.Id(SeedVessel.Id,ShipOperationsFixtures.LedgerSlotB));
        Assert.Equal("SyntheticUnverified",b.Record.Provenance);Assert.Equal("REVIEW_REQUIRED",b.Assessment.ReviewState);Assert.Equal("-0.5",b.Assessment.Continuity.DifferenceMt);
    }
}
