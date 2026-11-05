using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-100/T-OPS2-004 + DEV-101/T-OPS2-005, actual HTTP/PG sources. Not executed.
public sealed partial class DirectoryWorkspaceTests
{
    private static void ShipOpsPermissions(IServiceProvider services,bool write=true) {
        var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{Permissions=[..m.Permissions,"operations.fuel.read","operations.port.read",write?"operations.fuel.write":"unused",write?"operations.port.write":"unused"]};
    }
    private string ShipOpsPath(string kind,Guid? id=null,string suffix="",Guid? vessel=null)=>"/api/v1/"+(kind=="Fuel"?"fuel-bunker-logs":"port-activity-sof")+(id==null?"":"/"+id)+suffix+"?vesselId="+(vessel??SeedVessel.Id);
    private async Task<HttpResponseMessage> ShipOpsSend(HttpClient client,string kind,ShipOpsDraftCommand command,string? key=null,bool update=false,Guid? vessel=null) {
        await Token(client);using var request=new HttpRequestMessage(update?HttpMethod.Put:HttpMethod.Post,ShipOpsPath(kind,update?command.Id:null,vessel:vessel)){Content=JsonContent.Create(command)};
        request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString("D"));return await client.SendAsync(request,Ct);
    }
    [Theory]
    [InlineData("Fuel","bdn_no")][InlineData("Port","remarks")]
    public async Task ShipOps_partial_drafts_preserve_lexical_facts_exact_receipts_MVCC_history_and_unverified_state(string kind,string field) {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);
        var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){[field]=new("Known","FICTIONAL initial declaration")},"INITIAL_AUTHORING");var key=Guid.NewGuid().ToString("D");
        using var first=await ShipOpsSend(client,kind,command,key);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var bytes=await first.Content.ReadAsStringAsync(Ct);var saved=(await first.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;
        Assert.Equal("DRAFT",saved.State);Assert.False(saved.CurrentRevision);Assert.False(saved.CanEdit);Assert.Equal("UserDeclaredUnverified",saved.Provenance);Assert.Null(saved.Origin);
        Assert.Equal(kind=="Fuel"?22:18,saved.Fields.Count);Assert.Equal("Missing",saved.Fields["evidence_ref"].State);
        if(kind=="Fuel"){Assert.Equal("REVIEW_REQUIRED",saved.Fields["compliance_state"].Value);Assert.Equal("PENDING",saved.Fields["verification_state"].Value);}
        else Assert.Equal("DRAFT",saved.Fields["sof_state"].Value);
        using var replay=await ShipOpsSend(client,kind,command,key);Assert.Equal(bytes,await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Conflict,(await ShipOpsSend(client,kind=="Fuel"?"Port":"Fuel",command with{Fields=[]},key)).StatusCode);
        var update=command with{ExpectedVersion=saved.Version,ReasonCode="AUTHORING_CORRECTION",Fields=new(){[field]=new("Known","FICTIONAL corrected"),[kind=="Fuel"?"rob_after_mt":"cargo_quantity"]=new("Known","-000123.4500")}};
        Assert.Equal(HttpStatusCode.Conflict,(await ShipOpsSend(client,kind,update,key,true)).StatusCode);
        using var changed=await ShipOpsSend(client,kind,update,update:true);Assert.Equal(HttpStatusCode.OK,changed.StatusCode);var newer=(await changed.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;
        Assert.Equal(2,newer.Revision);Assert.NotEqual(saved.Version,newer.Version);Assert.Equal("-000123.4500",newer.Fields[kind=="Fuel"?"rob_after_mt":"cargo_quantity"].Value);
        Assert.Equal(HttpStatusCode.Conflict,(await ShipOpsSend(client,kind,update,update:true)).StatusCode);
        var history=(await client.GetFromJsonAsync<ShipOpsHistoryDto>(ShipOpsPath(kind,saved.Id,"/history"),Ct))!;Assert.Equal(2,history.Items.Length);Assert.False(history.HasMore);
        Assert.False(history.Items.Single(r=>r.Revision==1).CurrentRevision);Assert.True(history.Items.Single(r=>r.Revision==2).CurrentRevision);
        using var oldReplay=await ShipOpsSend(client,kind,command,key);Assert.Equal(bytes,await oldReplay.Content.ReadAsStringAsync(Ct));
        await using var db=Db();Assert.Equal(2,await db.ShipOperationReceipts.CountAsync(r=>r.RecordId==saved.Id,Ct));
        Assert.DoesNotContain("FICTIONAL",JsonSerializer.Serialize(await db.AuditEvents.Where(e=>e.ResourceId==saved.Id).ToArrayAsync(Ct)));
        var revision=await db.ShipOperationRevisions.SingleAsync(r=>r.RecordId==saved.Id && r.Revision==1,Ct);Assert.Equal(saved.Version,revision.RecordVersion);revision.FieldsJson="{}";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    [Theory]
    [InlineData("Fuel","bdn_no")][InlineData("Port","remarks")]
    public async Task ShipOps_concurrent_duplicate_and_competing_expected_version_commands_have_one_effect_and_recover_exact_receipt(string kind,string field) {
        await using var host=Host();using var first=Client(host);using var second=Client(host);ShipOpsPermissions(host.Services);
        var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){[field]=new("Known","FICTIONAL duplicate race")},"INITIAL_AUTHORING");var key=Guid.NewGuid().ToString("D");
        var duplicates=await Task.WhenAll(ShipOpsSend(first,kind,command,key),ShipOpsSend(second,kind,command,key));
        try {
            Assert.Contains(duplicates,r=>r.StatusCode==HttpStatusCode.OK);Assert.All(duplicates,r=>Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
            var success=duplicates.First(r=>r.StatusCode==HttpStatusCode.OK);var bytes=await success.Content.ReadAsStringAsync(Ct);var saved=(await success.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;
            using var recovered=await ShipOpsSend(second,kind,command,key);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(bytes,await recovered.Content.ReadAsStringAsync(Ct));
            var update=command with{ExpectedVersion=saved.Version,ReasonCode="AUTHORING_CORRECTION"};
            var edits=await Task.WhenAll(ShipOpsSend(first,kind,update with{Fields=new(){[field]=new("Known","FICTIONAL contender one")}},update:true),
                ShipOpsSend(second,kind,update with{Fields=new(){[field]=new("Known","FICTIONAL contender two")}},update:true));
            try{Assert.Single(edits.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(edits.Where(r=>r.StatusCode==HttpStatusCode.Conflict));}
            finally{foreach(var response in edits)response.Dispose();}
            await using var db=Db();Assert.Equal(2,(await db.ShipOperations.SingleAsync(r=>r.Id==saved.Id,Ct)).Revision);Assert.Equal(2,await db.ShipOperationRevisions.CountAsync(r=>r.RecordId==saved.Id,Ct));Assert.Equal(2,await db.ShipOperationReceipts.CountAsync(r=>r.RecordId==saved.Id,Ct));
        }finally{foreach(var response in duplicates)response.Dispose();}
    }
    [Fact]
    public async Task ShipOps_authorization_denies_foreign_vessel_Tug_mode_unattributed_write_and_unresolved_known_references() {
        await using var host=Host();using var client=Client(host);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(ShipOpsPath("Fuel"),Ct)).StatusCode);ShipOpsPermissions(host.Services);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(ShipOpsPath("Fuel",vessel:foreign.Id),Ct)).StatusCode);
        var tug=seed.Vessels.First(v=>v.Kind==VesselKind.Tug);Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(ShipOpsPath("Port",vessel:tug.Id),Ct)).StatusCode);
        var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){["evidence_ref"]=new("Known",Guid.NewGuid().ToString("D"))},"INITIAL_AUTHORING");
        using var unresolved=await ShipOpsSend(client,"Fuel",command);Assert.Equal(HttpStatusCode.BadRequest,unresolved.StatusCode);Assert.Contains("linked_resource_unavailable",await unresolved.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.BadRequest,(await ShipOpsSend(client,"Fuel",command with{Fields=new(){["compliance_state"]=new("Known","READY")}})).StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        settings.Memberships[0]=member with{RoleReferences=[]};Assert.Equal(HttpStatusCode.Forbidden,(await ShipOpsSend(client,"Fuel",command with{Fields=[]})).StatusCode);
        settings.Memberships[0]=member with{Permissions=[..member.Permissions.Where(p=>p!="mode.cross"),"mode.tug"]};
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(ShipOpsPath("Fuel"),Ct)).StatusCode);
        await using var db=Db();Assert.Empty(await db.ShipOperations.ToArrayAsync(Ct));Assert.Empty(await db.ShipOperationReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task ShipOps_Port_timeline_orders_explicit_UTC_instants_without_altering_declared_offsets_or_guessing_unknown_events() {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);
        foreach(var stamp in new[]{"2026-01-01T10:00:00+02:00","2026-01-01T08:30:00Z"}) {
            using var response=await ShipOpsSend(client,"Port",new(Guid.NewGuid(),0,new(){["event_start"]=new("Known",stamp),["activity_type"]=new("Known","Arrival")},"INITIAL_AUTHORING"));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        }
        var page=(await client.GetFromJsonAsync<ShipOpsPageDto>(ShipOpsPath("Port"),Ct))!;
        Assert.Equal("2026-01-01T08:30:00Z",page.Items[0].Fields["event_start"].Value);Assert.Equal("2026-01-01T10:00:00+02:00",page.Items[1].Fields["event_start"].Value);
        await using var db=Db();var row=await db.ShipOperations.SingleAsync(r=>r.Id==page.Items[1].Id,Ct);Assert.Equal(DateTimeOffset.Parse("2026-01-01T08:00:00Z"),row.OccurredAt);
        Assert.Null(row.EndedAt);Assert.False(page.HasMore);Assert.False(page.Offline.Supported);
    }
    [Fact]
    public async Task ShipOps_origin_pin_reuses_actual_authorized_daily_revision_without_copying_unknown_units_and_new_private_origin_restricts_old_receipts() {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);DailyPermissions(host.Services);
        var daily=new DailyDraftCommand(Guid.NewGuid(),0,new(){["fuel_summary"]=new("Known","FICTIONAL unstructured fuel, unit unknown")},"INITIAL_AUTHORING");
        using var source=await DailySend(client,"noon-reports",daily,Guid.NewGuid().ToString("D"));var noon=(await source.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var pin=new ShipOperationOriginPin("Noon",noon.Id,noon.Revision,noon.Version,noon.Hash,DailyOperationsPolicy.SourceVersion);
        var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,[],"INITIAL_AUTHORING",pin);var key=Guid.NewGuid().ToString("D");
        using var first=await ShipOpsSend(client,"Fuel",command,key);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var saved=(await first.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;Assert.Equal(pin,saved.Origin);Assert.Equal("Missing",saved.Fields["quantity_consumed_mt"].State);
        Assert.Equal(HttpStatusCode.Forbidden,(await ShipOpsSend(client,"Fuel",command with{Id=Guid.NewGuid(),Origin=pin with{Version=pin.Version+1}})).StatusCode);
        using var revisedNoon=await DailySend(client,"noon-reports",daily with{ExpectedVersion=noon.Version,ReasonCode="AUTHORING_CORRECTION"},Guid.NewGuid().ToString("D"),true);Assert.Equal(HttpStatusCode.OK,revisedNoon.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await ShipOpsSend(client,"Fuel",command with{Id=Guid.NewGuid()})).StatusCode);
        using var historicalEdit=await ShipOpsSend(client,"Fuel",command with{ExpectedVersion=saved.Version,ReasonCode="AUTHORING_CORRECTION"},update:true);Assert.Equal(HttpStatusCode.OK,historicalEdit.StatusCode);var edited=(await historicalEdit.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;
        using var engineSource=await DailySend(client,"engine-daily-logs",new(Guid.NewGuid(),0,[],"INITIAL_AUTHORING"),Guid.NewGuid().ToString("D"));var engine=(await engineSource.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var enginePin=new ShipOperationOriginPin("Engine",engine.Id,engine.Revision,engine.Version,engine.Hash,DailyOperationsPolicy.SourceVersion);
        Assert.Equal(HttpStatusCode.OK,(await ShipOpsSend(client,"Fuel",command with{ExpectedVersion=edited.Version,ReasonCode="AUTHORING_CORRECTION",Origin=enginePin},update:true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await ShipOpsSend(client,"Port",command with{Id=Guid.NewGuid(),Origin=enginePin})).StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!="daily.engine.read").ToArray()};
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await ShipOpsSend(client,"Fuel",command,key)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(ShipOpsPath("Fuel",saved.Id,"/history"),Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await ShipOpsSend(client,"Fuel",command with{ExpectedVersion=edited.Version,ReasonCode="AUTHORING_CORRECTION",Origin=null},update:true)).StatusCode);
    }
    [Fact]
    public async Task ShipOps_synthetic_fixture_is_add_missing_incomplete_and_preserves_operator_revisions_without_fake_receipts_or_evidence() {
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:shipops-fixture","System","shipops-fixture"),administrativeImport:true)) {
            await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await ShipOperationsFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}
            Assert.Equal(2,await db.ShipOperations.CountAsync(Ct));Assert.Empty(await db.ShipOperationReceipts.ToArrayAsync(Ct));
        }
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);var id=ShipOperationsFixtures.Id(SeedVessel.Id,"Fuel");
        var before=(await client.GetFromJsonAsync<ShipOpsRecordDto>(ShipOpsPath("Fuel",id),Ct))!;Assert.Equal("SyntheticUnverified",before.Provenance);Assert.All(before.Fields.Where(f=>!ShipOperationsPolicy.Schema("Fuel").Single(s=>s.Key==f.Key).ReadOnly),f=>Assert.Equal("Missing",f.Value.State));
        using var saved=await ShipOpsSend(client,"Fuel",new(id,before.Version,new(){["bdn_no"]=new("Known","FICTIONAL operator draft")},"AUTHORING_CORRECTION"),update:true);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);var changed=(await saved.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:shipops-fixture","System","shipops-fixture-retry"),administrativeImport:true)) {
            await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await ShipOperationsFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}
            var row=await db.ShipOperations.SingleAsync(r=>r.Id==id,Ct);Assert.Equal(changed.Version,row.Version);Assert.Equal(2,row.Revision);Assert.Contains("FICTIONAL operator draft",row.FieldsJson);
        }
    }
}
