using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-054/T-S8-003: actual owning host/PostgreSQL sources, execution deferred.
public sealed partial class DirectoryWorkspaceTests
{
    private static void WorkOrderPermissions(IServiceProvider services,bool write=true) {
        var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{Permissions=[..m.Permissions,"equipment.read","maintenance.read","workorder.read",write?"workorder.write":"unused"]};
    }
    private string WorkOrderPath(Guid? id=null,string suffix="",Guid? vessel=null)=>"/api/v1/work-orders"+(id==null?"":"/"+id)+suffix+"?vessel="+(vessel??SeedVessel.Id);
    private async Task<HttpResponseMessage> WorkOrderSend(HttpClient client,WorkOrderDraftCommand command,string? key=null,bool update=false,Guid? vessel=null) {
        await Token(client);using var request=new HttpRequestMessage(update?HttpMethod.Put:HttpMethod.Post,WorkOrderPath(update?command.Id:null,vessel:vessel)){Content=JsonContent.Create(command)};
        request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString("D"));return await client.SendAsync(request,Ct);
    }
    private static WorkOrderDraftCommand WorkOrderDraft(Guid? id=null)=>new(id??Guid.NewGuid(),0,[],WorkOrderPolicy.SourceVersion);
    private string WorkOrderPlanPath(Guid id)=>$"/api/v1/vessels/{SeedVessel.Id}/work-order-plan-origins/{id}";
    private async Task<WorkOrderPlanContextDto> WorkOrderPlan(HttpClient client) {
        await Token(client);var command=new MaintenanceCommand(Guid.NewGuid(),null,SeedVessel.Id,null,null,new(),DateTimeOffset.UnixEpoch);
        using var created=await Send(client,PlansUrl,command);Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        return(await client.GetFromJsonAsync<WorkOrderPlanContextDto>(WorkOrderPlanPath(command.Id),Ct))!;
    }
    private static string WorkOrderFacts(WorkOrderRecordDto row)=>JsonSerializer.Serialize(row);
    [Fact]
    public async Task WorkOrder_draft_records_declared_stage_without_closure_and_preserves_exact_receipt_facts_actual_versions_and_audited_history() {
        await using var host=Host();using var client=Client(host);WorkOrderPermissions(host.Services);
        var command=WorkOrderDraft() with{Fields=new(){["execution_stage"]=new("Known","EVIDENCE_SUBMITTED"),["findings"]=new("Known","FICTIONAL work_order declaration\nNo verification")}};
        var key=Guid.NewGuid().ToString("D");using var first=await WorkOrderSend(client,command,key);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var saved=(await first.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        Assert.Equal(25,saved.Fields.Count);Assert.Equal("Missing",saved.Fields["equipment_id"].State);Assert.Equal("EVIDENCE_SUBMITTED",saved.Fields["execution_stage"].Value);
        Assert.Equal("DRAFT",saved.State);Assert.Equal("REVIEW_REQUIRED",saved.Qualification);Assert.False(saved.CurrentRevision);Assert.False(saved.CanEdit);
        using var replay=await WorkOrderSend(client,command,key);Assert.Equal(WorkOrderFacts(saved),WorkOrderFacts((await replay.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!));
        Assert.Equal(HttpStatusCode.Conflict,(await WorkOrderSend(client,command with{Fields=new(){["findings"]=new("Known","different body")}},key)).StatusCode);
        var edit=command with{ExpectedVersion=saved.Version,Fields=new(){["findings"]=new("Known","FICTIONAL corrected declaration")}};
        using var changed=await WorkOrderSend(client,edit,update:true);Assert.Equal(HttpStatusCode.OK,changed.StatusCode);var updated=(await changed.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        Assert.Equal(2,updated.Revision);Assert.NotEqual(saved.Version,updated.Version);Assert.Equal(HttpStatusCode.Conflict,(await WorkOrderSend(client,edit,update:true)).StatusCode);
        var history=(await client.GetFromJsonAsync<WorkOrderHistoryDto>(WorkOrderPath(saved.Id,"/history"),Ct))!;Assert.Equal(2,history.Items.Length);Assert.False(history.HasMore);Assert.False(history.Items.Single(r=>r.Revision==1).CurrentRevision);Assert.All(history.Items,r=>Assert.False(r.CanEdit));
        await using var db=Db();var receipts=await db.WorkOrderReceipts.Where(r=>r.WorkOrderId==saved.Id).OrderBy(r=>r.Revision).ToArrayAsync(Ct);Assert.Equal(2,receipts.Length);Assert.Equal(saved.Version,receipts[0].RecordVersion);
        var audit=await db.AuditEvents.Where(e=>e.ResourceId==saved.Id).ToArrayAsync(Ct);Assert.DoesNotContain("FICTIONAL",JsonSerializer.Serialize(audit));
        Assert.Contains(audit,e=>e.ResourceKind=="WorkOrderReceipt" && e.AfterVersion==saved.Version && e.AfterRevision==saved.Revision);
        Assert.Contains(audit,e=>e.ChangeSummary.Contains("WorkOrderCreate") && e.ChangeSummary.Contains("PREPARATION_COMMAND") && e.VesselId==SeedVessel.Id && e.ActorId==actorId.ToString());
        Assert.Contains(audit,e=>e.ChangeSummary.Contains("WorkOrderUpdate") && e.BeforeVersion==saved.Version && e.AfterRevision==2);
        Assert.All(audit,e=>{Assert.Equal("User",e.ActorType);Assert.Equal("WebCommand",e.Source);Assert.NotNull(e.RoleContext);Assert.Contains("Vessel",e.ChangeSummary);Assert.Equal(SeedVessel.TenantId,e.TenantId);});
        var immutable=await db.WorkOrderRevisions.SingleAsync(r=>r.WorkOrderId==saved.Id && r.Revision==1,Ct);immutable.FieldsJson="{}";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task WorkOrder_plan_uses_actual_current_origin_once_and_retains_established_history_without_inventing_historical_xmin_or_rebasing() {
        await using var host=Host(technicalRead:true,technicalWrite:true);using var client=Client(host);WorkOrderPermissions(host.Services);var asset=await WorkOrderPlan(client);
        var command=WorkOrderDraft() with{Plan=asset.Pin};using var first=await WorkOrderSend(client,command);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var saved=(await first.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;Assert.Equal(asset.Pin,saved.Plan);
        using var advance=await Send(client,PlansUrl,new MaintenanceCommand(asset.Pin.Id,asset.Pin.Version,asset.Plan.AssetId,null,null,new(Criticality:"Unknown"),DateTimeOffset.UnixEpoch));Assert.Equal(HttpStatusCode.OK,advance.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync(WorkOrderPlanPath(asset.Pin.Id)+$"?revision={asset.Pin.Revision}",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await WorkOrderSend(client,command with{Id=Guid.NewGuid()})).StatusCode);
        using var retained=await WorkOrderSend(client,command with{ExpectedVersion=saved.Version,Fields=new(){["work_done"]=new("Known","FICTIONAL retained historical source")}},update:true);Assert.Equal(HttpStatusCode.OK,retained.StatusCode);var row=(await retained.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;Assert.Equal(asset.Pin,row.Plan);
        using var forged=await WorkOrderSend(client,command with{Id=Guid.NewGuid(),Plan=asset.Pin with{Hash=new string('0',64)}});Assert.Equal(HttpStatusCode.Forbidden,forged.StatusCode);
        var current=(await client.GetFromJsonAsync<WorkOrderRecordDto>(WorkOrderPath(command.Id),Ct))!;Assert.Equal(asset.Pin,current.Plan);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!="equipment.read").ToArray()};
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(WorkOrderPath(command.Id,"/history"),Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await WorkOrderSend(client,command with{ExpectedVersion=row.Version,Plan=null},update:true)).StatusCode);
    }
    [Fact]
    public async Task WorkOrder_source_scope_mode_and_attribution_are_required_and_client_lifecycle_or_source_override_never_grants_authority() {
        await using var host=Host();using var client=Client(host);Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(WorkOrderPath(),Ct)).StatusCode);WorkOrderPermissions(host.Services);
        await Token(client);using(var missingAsset=new HttpRequestMessage(HttpMethod.Post,WorkOrderPath()){Content=JsonContent.Create(new{id=Guid.NewGuid(),expectedVersion=0,fields=new Dictionary<string,WorkOrderFact>(),sourceVersion=WorkOrderPolicy.SourceVersion})}) {
            missingAsset.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(missingAsset,Ct)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict,(await WorkOrderSend(client,WorkOrderDraft() with{SourceVersion="unapproved source"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await WorkOrderSend(client,WorkOrderDraft() with{Fields=new(){["state"]=new("Known","Closed")}})).StatusCode);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);Assert.Equal(HttpStatusCode.NotFound,(await WorkOrderSend(client,WorkOrderDraft(),vessel:foreign.Id)).StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];settings.Memberships[0]=member with{RoleReferences=[]};
        Assert.Equal(HttpStatusCode.Forbidden,(await WorkOrderSend(client,WorkOrderDraft())).StatusCode);
        settings.Memberships[0]=member with{Permissions=[..member.Permissions.Where(p=>p!="mode.cross"),"mode.tug"]};Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(WorkOrderPath(),Ct)).StatusCode);
        await using var db=Db();Assert.Empty(await db.WorkOrders.ToArrayAsync(Ct));Assert.Empty(await db.WorkOrderReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task WorkOrder_create_with_an_existing_other_vessel_id_is_not_classified_as_an_own_version_conflict() {
        await using var host=Host();using var client=Client(host);WorkOrderPermissions(host.Services);var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        settings.Memberships[0]=member with{Permissions=[..member.Permissions,"mode.tug","mode.cross"]};var other=seed.Vessels.First(v=>v.TenantId==SeedVessel.TenantId && v.Id!=SeedVessel.Id);var command=WorkOrderDraft();
        Assert.Equal(HttpStatusCode.OK,(await WorkOrderSend(client,command,vessel:other.Id)).StatusCode);
        using var denied=await WorkOrderSend(client,command,vessel:SeedVessel.Id);Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);Assert.DoesNotContain("version_conflict",await denied.Content.ReadAsStringAsync(Ct));
        await using var db=Db();Assert.Single(await db.WorkOrders.Where(r=>r.Id==command.Id).ToArrayAsync(Ct));Assert.Equal(other.Id,(await db.WorkOrders.SingleAsync(r=>r.Id==command.Id,Ct)).VesselId);
    }
    [Fact]
    public async Task WorkOrder_synthetic_fixture_is_add_missing_and_incomplete_without_plan_evidence_permit_or_approval_and_preserves_operator_edits() {
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:work_order-fixture","System","work_order-fixture"),administrativeImport:true)) {
            await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await WorkOrderFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}Assert.Single(await db.WorkOrders.ToArrayAsync(Ct));Assert.Empty(await db.WorkOrderReceipts.ToArrayAsync(Ct));
        }
        await using var host=Host();using var client=Client(host);WorkOrderPermissions(host.Services);var id=WorkOrderFixtures.Id(SeedVessel.Id);var before=(await client.GetFromJsonAsync<WorkOrderRecordDto>(WorkOrderPath(id),Ct))!;
        Assert.Equal("SyntheticUnverified",before.Provenance);Assert.Null(before.Plan);Assert.All(before.Fields,f=>Assert.Equal("Missing",f.Value.State));
        using var changed=await WorkOrderSend(client,WorkOrderDraft(id) with{ExpectedVersion=before.Version,Fields=new(){["findings"]=new("Known","FICTIONAL operator edit")}},update:true);Assert.Equal(HttpStatusCode.OK,changed.StatusCode);var saved=(await changed.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:work_order-fixture","System","work_order-fixture-retry"),administrativeImport:true)) {
            await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await WorkOrderFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}var row=await db.WorkOrders.SingleAsync(r=>r.Id==id,Ct);Assert.Equal(saved.Version,row.Version);Assert.Equal(2,row.Revision);
        }
    }
    [Fact]
    public async Task WorkOrder_concurrent_same_key_and_competing_versions_have_one_effect_and_recover_the_exact_committed_facts() {
        await using var host=Host();using var first=Client(host);using var second=Client(host);WorkOrderPermissions(host.Services);var command=WorkOrderDraft();var key=Guid.NewGuid().ToString("D");
        var duplicates=await Task.WhenAll(WorkOrderSend(first,command,key),WorkOrderSend(second,command,key));
        try {
            Assert.Contains(duplicates,r=>r.StatusCode==HttpStatusCode.OK);Assert.All(duplicates,r=>Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
            var saved=(await duplicates.First(r=>r.StatusCode==HttpStatusCode.OK).Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
            using var recovered=await WorkOrderSend(second,command,key);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(WorkOrderFacts(saved),WorkOrderFacts((await recovered.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!));
            var edit=command with{ExpectedVersion=saved.Version};var candidates=await Task.WhenAll(WorkOrderSend(first,edit with{Fields=new(){["findings"]=new("Known","FICTIONAL contender one")}},update:true),WorkOrderSend(second,edit with{Fields=new(){["findings"]=new("Known","FICTIONAL contender two")}},update:true));
            try{Assert.Single(candidates.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(candidates.Where(r=>r.StatusCode==HttpStatusCode.Conflict));}
            finally{foreach(var response in candidates)response.Dispose();}
            await using var db=Db();Assert.Equal(2,(await db.WorkOrders.SingleAsync(r=>r.Id==command.Id,Ct)).Revision);Assert.Equal(2,await db.WorkOrderRevisions.CountAsync(r=>r.WorkOrderId==command.Id,Ct));Assert.Equal(2,await db.WorkOrderReceipts.CountAsync(r=>r.WorkOrderId==command.Id,Ct));
        }finally{foreach(var response in duplicates)response.Dispose();}
    }
}
