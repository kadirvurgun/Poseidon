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

// DEV-102 / T-OPS2-006. Real host + real PostgreSQL test SOURCES; NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    private static void DowntimePermissions(IServiceProvider services,bool write=true) {
        var settings=services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        settings.Memberships[0]=member with{Permissions=[..member.Permissions,"operations.downtime.read",write?"operations.downtime.write":"unused"]};
    }
    private string DowntimePath(Guid? id=null,string suffix="",Guid? vessel=null)=>"/api/v1/operational-downtime"+(id==null?"":"/"+id)+suffix+"?vessel="+(vessel??SeedVessel.Id);
    private string DowntimeOriginPath(string kind,Guid id,Guid? vessel=null)=>$"/api/v1/vessels/{vessel??SeedVessel.Id}/downtime-origins/{kind}/{id}";
    private async Task<HttpResponseMessage> DowntimeSend(HttpClient client,DowntimeDraftCommand command,string? key=null,bool update=false,Guid? vessel=null) {
        await Token(client);using var request=new HttpRequestMessage(update?HttpMethod.Put:HttpMethod.Post,DowntimePath(update?command.Id:null,vessel:vessel)){Content=JsonContent.Create(command)};
        request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString("D"));return await client.SendAsync(request,Ct);
    }
    private static DowntimeDraftCommand DowntimeDraft(Guid? id=null)=>new(id??Guid.NewGuid(),0,[],DowntimePolicy.SourceVersion);
    private static string DowntimeFacts(DowntimeRecordDto record)=>JsonSerializer.Serialize(record);
    [Fact]
    public async Task Downtime_preserves_incomplete_source_facts_actual_versions_exact_receipts_history_and_attributed_audit_without_LoH_or_closure() {
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);
        var command=DowntimeDraft() with{Fields=new(){["cause"]=new("Known","FICTIONAL incident\nNo approved conclusion"),["start_at"]=new("Known","2026-11-09T12:00:00+03:00"),["end_at"]=new("Known","2026-11-09T09:00:00.0000001Z"),["duration_minutes"]=new("Unknown",null)}};
        var key=Guid.NewGuid().ToString("D");using var first=await DowntimeSend(client,command,key);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var saved=(await first.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;
        Assert.Equal(14,saved.Fields.Count);Assert.Equal(new DowntimeFact("Unknown",null),saved.Fields["closure_state"]);Assert.Equal(new DowntimeFact("Unknown",null),saved.Fields["loss_of_hire_flag"]);Assert.Equal(SeedVessel.Id.ToString("D"),saved.Fields["vessel_id"].Value);
        Assert.Equal("DRAFT",saved.State);Assert.Equal("REVIEW_REQUIRED",saved.Qualification);Assert.False(saved.CurrentRevision);Assert.False(saved.CanEdit);Assert.Equal("1",saved.EngineeringInterval.ElapsedTicks);Assert.Equal("UNASSESSED",saved.EngineeringInterval.Qualification);Assert.Equal("Unknown",saved.Fields["duration_minutes"].State);
        using var replay=await DowntimeSend(client,command,key);Assert.Equal(DowntimeFacts(saved),DowntimeFacts((await replay.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!));
        Assert.Equal(HttpStatusCode.Conflict,(await DowntimeSend(client,command with{Fields=[]},key)).StatusCode);
        using var update=await DowntimeSend(client,command with{ExpectedVersion=saved.Version,Fields=new(){["cause"]=new("Known","FICTIONAL correction")}},update:true);Assert.Equal(HttpStatusCode.OK,update.StatusCode);var changed=(await update.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;
        Assert.Equal(2,changed.Revision);Assert.NotEqual(saved.Version,changed.Version);Assert.Equal(HttpStatusCode.Conflict,(await DowntimeSend(client,command with{ExpectedVersion=saved.Version},update:true)).StatusCode);
        var history=(await client.GetFromJsonAsync<DowntimeHistoryDto>(DowntimePath(saved.Id,"/history"),Ct))!;Assert.Equal(2,history.Items.Length);Assert.All(history.Items,r=>Assert.False(r.CanEdit));Assert.Equal(saved.Hash,history.Items.Single(r=>r.Revision==1).Hash);
        await using var db=Db();Assert.Equal(2,await db.DowntimeReceipts.CountAsync(r=>r.DowntimeId==saved.Id,Ct));var audit=await db.AuditEvents.Where(e=>e.ResourceId==saved.Id).ToArrayAsync(Ct);
        Assert.DoesNotContain("FICTIONAL",JsonSerializer.Serialize(audit));Assert.Contains(audit,e=>e.ResourceKind=="DowntimeReceipt" && e.AfterVersion==saved.Version && e.AfterRevision==1);
        Assert.Contains(audit,e=>e.ChangeSummary.Contains("DowntimeCreate") && e.ActorId==actorId.ToString() && e.VesselId==SeedVessel.Id);Assert.Contains(audit,e=>e.ChangeSummary.Contains("DowntimeUpdate") && e.BeforeVersion==saved.Version);
        var ledger=await db.DowntimeRevisions.FirstAsync(r=>r.DowntimeId==saved.Id,Ct);ledger.FieldsJson="{}";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Downtime_requires_real_permissions_Ship_scope_role_source_and_closed_commands_and_never_accepts_generic_lookup_or_client_state() {
        await using var host=Host();using var client=Client(host);Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(DowntimePath(),Ct)).StatusCode);DowntimePermissions(host.Services);
        var caps=(await client.GetFromJsonAsync<DowntimeCapabilitiesDto>(DowntimePath(suffix:"/capabilities"),Ct))!;Assert.True(caps.CanWrite);Assert.Empty(caps.OriginKinds);Assert.False(caps.CanVerify);Assert.False(caps.CanClose);Assert.False(caps.CanFinalize);Assert.False(caps.Offline.Supported);Assert.Equal(14,caps.Schema.Length);
        foreach(var field in new[]{"closure_state","loss_of_hire_flag","vessel_id","linked_defect","linked_work_order","linked_event","claim_evidence","voyage_id"})
            Assert.Equal(HttpStatusCode.BadRequest,(await DowntimeSend(client,DowntimeDraft() with{Fields=new(){[field]=new("Known","Closed")}})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await DowntimeSend(client,DowntimeDraft() with{SourceVersion="unapproved"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(DowntimePath()+"&tenantId="+SeedVessel.TenantId,Ct)).StatusCode);
        await Token(client);using(var unknown=new HttpRequestMessage(HttpMethod.Post,DowntimePath()){Content=JsonContent.Create(new{id=Guid.NewGuid(),expectedVersion=0,fields=new Dictionary<string,DowntimeFact>(),sourceVersion=DowntimePolicy.SourceVersion,origin=(object?)null,canClose=true})}){unknown.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(unknown,Ct)).StatusCode);}
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);Assert.Equal(HttpStatusCode.NotFound,(await DowntimeSend(client,DowntimeDraft(),vessel:foreign.Id)).StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];settings.Memberships[0]=member with{Permissions=[..member.Permissions,"mode.tug","mode.cross"]};var tug=seed.Vessels.Single(v=>v.Kind==VesselKind.Tug);Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(DowntimePath(vessel:tug.Id),Ct)).StatusCode);
        settings.Memberships[0]=member with{RoleReferences=[]};Assert.Equal(HttpStatusCode.Forbidden,(await DowntimeSend(client,DowntimeDraft())).StatusCode);await using var db=Db();Assert.Empty(await db.Downtimes.ToArrayAsync(Ct));Assert.Empty(await db.DowntimeReceipts.ToArrayAsync(Ct));
    }
    [Theory]
    [InlineData("Defect")][InlineData("WorkOrder")][InlineData("Engine")]
    public async Task Downtime_origins_are_actual_typed_current_sources_once_with_exact_retained_history_and_no_automatic_field_copy(string kind) {
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);DefectPermissions(host.Services);WorkOrderPermissions(host.Services);DailyPermissions(host.Services);
        Guid id;uint version;
        if(kind=="Defect"){var command=DefectDraft();using var response=await DefectSend(client,command);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var row=(await response.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;id=row.Id;version=row.Version;}
        else if(kind=="WorkOrder"){var command=WorkOrderDraft();using var response=await WorkOrderSend(client,command);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var row=(await response.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;id=row.Id;version=row.Version;}
        else{var command=new DailyDraftCommand(Guid.NewGuid(),0,new(){["defects"]=new("Known","FICTIONAL source observation")},"INITIAL_AUTHORING");using var response=await DailySend(client,"engine-daily-logs",command,Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.OK,response.StatusCode);var row=(await response.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;id=row.Id;version=row.Version;}
        var source=(await client.GetFromJsonAsync<DowntimeOriginDto>(DowntimeOriginPath(kind,id),Ct))!;Assert.Equal(version,source.Pin.Version);Assert.True(source.Current);Assert.Equal("REVIEW_REQUIRED",source.Qualification);Assert.DoesNotContain(source.Fields,f=>f.Key is "assigned_to" or "executed_by" or "verified_by" or "watch_engineer" or "evidence_ref");
        var sourceRoute=kind switch{"Defect"=>"defects","WorkOrder"=>"work-order-center",_=>"engine-daily-log"};
        Assert.Equal($"/app/{sourceRoute}/{SeedVessel.Id:D}?id={id:D}",source.Href);
        var draft=DowntimeDraft() with{Origin=source.Pin};using var first=await DowntimeSend(client,draft);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var saved=(await first.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;Assert.Equal(source.Pin,saved.Origin);Assert.Equal("Missing",saved.Fields["linked_defect"].State);Assert.Equal("Missing",saved.Fields["cause"].State);
        if(kind=="Defect")Assert.Equal(HttpStatusCode.OK,(await DefectSend(client,DefectDraft(id) with{ExpectedVersion=version},update:true)).StatusCode);
        else if(kind=="WorkOrder")Assert.Equal(HttpStatusCode.OK,(await WorkOrderSend(client,WorkOrderDraft(id) with{ExpectedVersion=version},update:true)).StatusCode);
        else Assert.Equal(HttpStatusCode.OK,(await DailySend(client,"engine-daily-logs",new(id,version,new(),"AUTHORING_CORRECTION"),Guid.NewGuid().ToString("D"),true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync(DowntimeOriginPath(kind,id)+"?revision=1",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await DowntimeSend(client,draft with{Id=Guid.NewGuid()})).StatusCode);
        using var retained=await DowntimeSend(client,draft with{ExpectedVersion=saved.Version},update:true);Assert.Equal(HttpStatusCode.OK,retained.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await DowntimeSend(client,DowntimeDraft() with{Origin=source.Pin with{Hash=new string('0',64)}})).StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];var permission=kind switch{"Defect"=>"defect.read","WorkOrder"=>"workorder.read",_=>"daily.engine.read"};settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!=permission).ToArray()};
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(DowntimePath(saved.Id,"/history"),Ct)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await DowntimeSend(client,draft with{ExpectedVersion=saved.Version,Origin=null},update:true)).StatusCode);
    }
    [Fact]
    public async Task Downtime_optional_add_missing_fixture_has_no_evidence_claim_financial_or_closure_and_preserves_actual_operator_head_and_receipt() {
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:downtime-fixture","System","downtime-fixture"),administrativeImport:true)) {
            await using var tx=await db.Database.BeginTransactionAsync(Ct);await DowntimeFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);Assert.Single(await db.Downtimes.ToArrayAsync(Ct));Assert.Empty(await db.DowntimeReceipts.ToArrayAsync(Ct));
        }
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);var id=DowntimeFixtures.Id(SeedVessel.Id);var row=(await client.GetFromJsonAsync<DowntimeRecordDto>(DowntimePath(id),Ct))!;
        Assert.Equal("SyntheticUnverified",row.Provenance);Assert.Null(row.Origin);Assert.Equal("Unknown",row.Fields["loss_of_hire_flag"].State);Assert.Equal("Unknown",row.Fields["closure_state"].State);
        using var update=await DowntimeSend(client,DowntimeDraft(id) with{ExpectedVersion=row.Version,Fields=new(){["cause"]=new("Known","FICTIONAL operator declaration")}},update:true);Assert.Equal(HttpStatusCode.OK,update.StatusCode);var saved=(await update.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:downtime-fixture","System","downtime-fixture-retry"),administrativeImport:true)) {
            await using var tx=await db.Database.BeginTransactionAsync(Ct);await DowntimeFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);Assert.Equal(saved.Version,(await db.Downtimes.SingleAsync(r=>r.Id==id,Ct)).Version);Assert.Equal(2,await db.DowntimeRevisions.CountAsync(r=>r.DowntimeId==id,Ct));Assert.Single(await db.DowntimeReceipts.Where(r=>r.DowntimeId==id).ToArrayAsync(Ct));
        }
    }
    [Fact]
    public async Task Downtime_concurrent_same_key_and_competing_versions_have_one_effect_and_recover_the_exact_committed_facts() {
        await using var host=Host();using var first=Client(host);using var second=Client(host);DowntimePermissions(host.Services);var command=DowntimeDraft();var key=Guid.NewGuid().ToString("D");
        var duplicates=await Task.WhenAll(DowntimeSend(first,command,key),DowntimeSend(second,command,key));
        try {
            Assert.Contains(duplicates,r=>r.StatusCode==HttpStatusCode.OK);Assert.All(duplicates,r=>Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
            var saved=(await duplicates.First(r=>r.StatusCode==HttpStatusCode.OK).Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;
            using var recovered=await DowntimeSend(second,command,key);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(DowntimeFacts(saved),DowntimeFacts((await recovered.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!));
            var edit=command with{ExpectedVersion=saved.Version};var candidates=await Task.WhenAll(DowntimeSend(first,edit with{Fields=new(){["cause"]=new("Known","FICTIONAL contender one")}},update:true),DowntimeSend(second,edit with{Fields=new(){["cause"]=new("Known","FICTIONAL contender two")}},update:true));
            try{Assert.Single(candidates.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(candidates.Where(r=>r.StatusCode==HttpStatusCode.Conflict));}
            finally{foreach(var response in candidates)response.Dispose();}
            await using var db=Db();Assert.Equal(2,(await db.Downtimes.SingleAsync(r=>r.Id==command.Id,Ct)).Revision);Assert.Equal(2,await db.DowntimeRevisions.CountAsync(r=>r.DowntimeId==command.Id,Ct));Assert.Equal(2,await db.DowntimeReceipts.CountAsync(r=>r.DowntimeId==command.Id,Ct));
        }finally{foreach(var response in duplicates)response.Dispose();}
    }
    [Fact]
    public async Task Downtime_transitive_work_order_plan_and_defect_asset_contexts_remain_pinned_and_source_summaries_exclude_private_actors() {
        await using var host=Host(technicalRead:true,technicalWrite:true);using var client=Client(host);DowntimePermissions(host.Services);DefectPermissions(host.Services);WorkOrderPermissions(host.Services);
        var plan=await WorkOrderPlan(client);var asset=await DefectEquipment(client);
        var wo=WorkOrderDraft() with{Plan=plan.Pin};using var woResponse=await WorkOrderSend(client,wo);Assert.Equal(HttpStatusCode.OK,woResponse.StatusCode);
        var defect=DefectDraft() with{Asset=asset.Pin};using var defectResponse=await DefectSend(client,defect);Assert.Equal(HttpStatusCode.OK,defectResponse.StatusCode);
        foreach(var source in new[]{(Kind:"Defect",Id:defect.Id),(Kind:"WorkOrder",Id:wo.Id)}) {
            var context=(await client.GetFromJsonAsync<DowntimeOriginDto>(DowntimeOriginPath(source.Kind,source.Id),Ct))!;
            var serialized=JsonSerializer.Serialize(context);Assert.DoesNotContain("ownerRoleId",serialized);Assert.DoesNotContain("actorId",serialized);Assert.DoesNotContain("assigned_to",serialized);
            var command=DowntimeDraft() with{Origin=context.Pin};using var created=await DowntimeSend(client,command);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var row=(await created.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;
            using var read=await client.GetAsync(DowntimePath(row.Id),Ct);Assert.Equal(HttpStatusCode.OK,read.StatusCode);
            var settings=host.Services.GetRequiredService<FederationSettings>();var original=settings.Memberships[0];settings.Memberships[0]=original with{Permissions=original.Permissions.Where(p=>p!="equipment.read").ToArray()};
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(DowntimePath(row.Id,"/history"),Ct)).StatusCode);settings.Memberships[0]=original;
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(DowntimePath(row.Id,"/history"),Ct)).StatusCode);
        }
    }
    [Fact]
    public async Task Downtime_page_order_uses_explicit_UTC_start_projection_and_never_lexical_local_offset_order_or_declared_duration() {
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);
        var earlier=DowntimeDraft() with{Fields=new(){["start_at"]=new("Known","2026-11-09T12:00:00+03:00"),["duration_minutes"]=new("Known","999999")}};
        var later=DowntimeDraft() with{Fields=new(){["start_at"]=new("Known","2026-11-09T10:00:00Z"),["duration_minutes"]=new("Missing",null)}};
        Assert.Equal(HttpStatusCode.OK,(await DowntimeSend(client,later)).StatusCode);Assert.Equal(HttpStatusCode.OK,(await DowntimeSend(client,earlier)).StatusCode);
        var page=(await client.GetFromJsonAsync<DowntimePageDto>(DowntimePath(),Ct))!;Assert.Equal(new[]{later.Id,earlier.Id},page.Items.Select(r=>r.Id));Assert.All(page.Items,r=>Assert.Equal("UNAVAILABLE",r.EngineeringInterval.State));
        await using var db=Db();Assert.Equal(new DateTimeOffset(2026,11,9,9,0,0,TimeSpan.Zero),(await db.Downtimes.SingleAsync(r=>r.Id==earlier.Id,Ct)).StartedAt);
    }

}
