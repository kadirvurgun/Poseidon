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

// DEV-055/T-S8-004: actual owning host/PostgreSQL sources, execution deferred.
public sealed partial class DirectoryWorkspaceTests
{
    private static void DefectPermissions(IServiceProvider services,bool write=true) {
        var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{Permissions=[..m.Permissions,"equipment.read","defect.read",write?"defect.write":"unused"]};
    }
    private string DefectPath(Guid? id=null,string suffix="",Guid? vessel=null)=>"/api/v1/defects"+(id==null?"":"/"+id)+suffix+"?vessel="+(vessel??SeedVessel.Id);
    private async Task<HttpResponseMessage> DefectSend(HttpClient client,DefectDraftCommand command,string? key=null,bool update=false,Guid? vessel=null) {
        await Token(client);using var request=new HttpRequestMessage(update?HttpMethod.Put:HttpMethod.Post,DefectPath(update?command.Id:null,vessel:vessel)){Content=JsonContent.Create(command)};
        request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString("D"));return await client.SendAsync(request,Ct);
    }
    private static DefectDraftCommand DefectDraft(Guid? id=null)=>new(id??Guid.NewGuid(),0,[],DefectPolicy.SourceVersion);
    private async Task<DefectAssetContextDto> DefectEquipment(HttpClient client) {
        await Token(client);var command=NewEquipment();using var created=await Send(client,EquipmentUrl,command);Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        return(await client.GetFromJsonAsync<DefectAssetContextDto>($"/api/v1/vessels/{SeedVessel.Id}/defect-asset-context/{command.Id}",Ct))!;
    }
    private static string DefectFacts(DefectRecordDto row)=>JsonSerializer.Serialize(row with{Temporal=new("OBSERVATION_SEPARATE",DateTimeOffset.UnixEpoch)});
    [Fact]
    public async Task Defect_draft_records_declared_stage_without_closure_and_preserves_exact_receipt_facts_actual_versions_and_audited_history() {
        await using var host=Host();using var client=Client(host);DefectPermissions(host.Services);
        var command=DefectDraft() with{Fields=new(){["criticality"]=new("Known","Critical"),["lifecycle_stage"]=new("Known","Effectiveness"),["notes"]=new("Known","FICTIONAL defect declaration\nNo verification")}};
        var key=Guid.NewGuid().ToString("D");using var first=await DefectSend(client,command,key);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var saved=(await first.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;
        Assert.Equal(11,saved.Fields.Count);Assert.Equal("Missing",saved.Fields["safeguard"].State);Assert.Equal("Effectiveness",saved.Fields["lifecycle_stage"].Value);
        Assert.Equal("DECLARATION",saved.Stage);Assert.Equal("REVIEW_REQUIRED",saved.Qualification);Assert.False(saved.Temporal.ClosureAvailable);Assert.False(saved.CurrentRevision);Assert.False(saved.CanEdit);
        using var replay=await DefectSend(client,command,key);Assert.Equal(DefectFacts(saved),DefectFacts((await replay.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!));
        Assert.Equal(HttpStatusCode.Conflict,(await DefectSend(client,command with{Fields=new(){["notes"]=new("Known","different body")}},key)).StatusCode);
        var edit=command with{ExpectedVersion=saved.Version,Fields=new(){["notes"]=new("Known","FICTIONAL corrected declaration")}};
        using var changed=await DefectSend(client,edit,update:true);Assert.Equal(HttpStatusCode.OK,changed.StatusCode);var updated=(await changed.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;
        Assert.Equal(2,updated.Revision);Assert.NotEqual(saved.Version,updated.Version);Assert.Equal(HttpStatusCode.Conflict,(await DefectSend(client,edit,update:true)).StatusCode);
        var history=(await client.GetFromJsonAsync<DefectHistoryDto>(DefectPath(saved.Id,"/history"),Ct))!;Assert.Equal(2,history.Items.Length);Assert.False(history.HasMore);Assert.False(history.Items.Single(r=>r.Revision==1).CurrentRevision);Assert.All(history.Items,r=>Assert.False(r.CanEdit));
        await using var db=Db();var receipts=await db.DefectReceipts.Where(r=>r.DefectId==saved.Id).OrderBy(r=>r.Revision).ToArrayAsync(Ct);Assert.Equal(2,receipts.Length);Assert.Equal(saved.Version,receipts[0].RecordVersion);
        var audit=await db.AuditEvents.Where(e=>e.ResourceId==saved.Id).ToArrayAsync(Ct);Assert.DoesNotContain("FICTIONAL",JsonSerializer.Serialize(audit));
        Assert.Contains(audit,e=>e.ResourceKind=="DefectReceipt" && e.AfterVersion==saved.Version && e.AfterRevision==saved.Revision);
        Assert.Contains(audit,e=>e.ChangeSummary != null && e.ChangeSummary.Contains("DefectCreate") && e.ChangeSummary.Contains("PREPARATION_COMMAND") && e.VesselId==SeedVessel.Id && e.ActorId==actorId.ToString());
        Assert.Contains(audit,e=>e.ChangeSummary != null && e.ChangeSummary.Contains("DefectUpdate") && e.BeforeVersion==saved.Version && e.AfterRevision==2);
        Assert.All(audit,e=>{Assert.Equal("User",e.ActorType);Assert.Equal("WebCommand",e.Source);Assert.NotNull(e.RoleContext);Assert.Contains("Vessel",e.ChangeSummary);Assert.Equal(SeedVessel.TenantId,e.TenantId);});
        var immutable=await db.DefectRevisions.SingleAsync(r=>r.DefectId==saved.Id && r.Revision==1,Ct);immutable.FieldsJson="{}";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    private sealed class DefectClock(DateTimeOffset value):TimeProvider {
        public DateTimeOffset Value {get;set;}=value;
        public override DateTimeOffset GetUtcNow()=>Value;
    }
    [Fact]
    public async Task Defect_concurrent_same_key_and_competing_versions_have_one_effect_and_recover_the_exact_committed_facts() {
        await using var host=Host();using var first=Client(host);using var second=Client(host);DefectPermissions(host.Services);var command=DefectDraft();var key=Guid.NewGuid().ToString("D");
        var duplicates=await Task.WhenAll(DefectSend(first,command,key),DefectSend(second,command,key));
        try {
            Assert.Contains(duplicates,r=>r.StatusCode==HttpStatusCode.OK);Assert.All(duplicates,r=>Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
            var saved=(await duplicates.First(r=>r.StatusCode==HttpStatusCode.OK).Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;
            using var recovered=await DefectSend(second,command,key);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(DefectFacts(saved),DefectFacts((await recovered.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!));
            var edit=command with{ExpectedVersion=saved.Version};var candidates=await Task.WhenAll(DefectSend(first,edit with{Fields=new(){["notes"]=new("Known","FICTIONAL contender one")}},update:true),DefectSend(second,edit with{Fields=new(){["notes"]=new("Known","FICTIONAL contender two")}},update:true));
            try{Assert.Single(candidates,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(candidates,r=>r.StatusCode==HttpStatusCode.Conflict);}
            finally{foreach(var response in candidates)response.Dispose();}
            await using var db=Db();Assert.Equal(2,(await db.Defects.SingleAsync(r=>r.Id==command.Id,Ct)).Revision);Assert.Equal(2,await db.DefectRevisions.CountAsync(r=>r.DefectId==command.Id,Ct));Assert.Equal(2,await db.DefectReceipts.CountAsync(r=>r.DefectId==command.Id,Ct));
        }finally{foreach(var response in duplicates)response.Dispose();}
    }
    [Fact]
    public async Task Defect_temporal_projection_refreshes_after_expiry_while_exact_durable_outcome_and_safety_qualification_stay_unchanged() {
        var clock=new DefectClock(new(2026,1,1,0,0,0,TimeSpan.Zero));await using var host=Host().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>s.AddSingleton<TimeProvider>(clock)));
        using var client=Client(host);DefectPermissions(host.Services);var command=DefectDraft() with{Fields=new(){["temporary_measure"]=new("Known","FICTIONAL declared measure"),["temporary_measure_expires_at"]=new("Known","2026-01-02T00:00:00Z")}};var key=Guid.NewGuid().ToString("D");
        using var first=await DefectSend(client,command,key);var before=(await first.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;Assert.Equal("NOT_DUE_DECLARED",before.Temporal.TemporaryMeasureState);
        await using var db=Db();var receiptBefore=(await db.DefectReceipts.AsNoTracking().SingleAsync(r=>r.DefectId==command.Id,Ct)).OutcomeJson;
        clock.Value=clock.Value.AddDays(2);using var replay=await DefectSend(client,command,key);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);var after=(await replay.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;
        Assert.Equal("EXPIRED_DECLARED",after.Temporal.TemporaryMeasureState);Assert.Equal(clock.Value,after.Temporal.AssessedAt);Assert.False(after.Temporal.ClosureAvailable);Assert.Equal(DefectFacts(before),DefectFacts(after));
        Assert.Equal(receiptBefore,(await db.DefectReceipts.AsNoTracking().SingleAsync(r=>r.DefectId==command.Id,Ct)).OutcomeJson);
        var current=(await client.GetFromJsonAsync<DefectRecordDto>(DefectPath(command.Id),Ct))!;Assert.Equal("EXPIRED_DECLARED",current.Temporal.TemporaryMeasureState);Assert.True(current.CurrentRevision);Assert.Equal("REVIEW_REQUIRED",current.Qualification);
    }
    [Fact]
    public async Task Defect_asset_uses_actual_current_origin_once_and_retains_established_history_without_inventing_historical_xmin_or_rebasing() {
        await using var host=Host(technicalRead:true,technicalWrite:true);using var client=Client(host);DefectPermissions(host.Services);var asset=await DefectEquipment(client);
        var command=DefectDraft() with{Asset=asset.Pin};using var first=await DefectSend(client,command);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var saved=(await first.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;Assert.Equal(asset.Pin,saved.Asset);
        using var advance=await Send(client,EquipmentUrl,NewEquipment(asset.Pin.Id) with{BaseVersion=asset.Pin.Version,Name="FICTIONAL later equipment declaration"});Assert.Equal(HttpStatusCode.OK,advance.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/defect-asset-context/{asset.Pin.Id}?revision={asset.Pin.Revision}",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await DefectSend(client,command with{Id=Guid.NewGuid()})).StatusCode);
        using var retained=await DefectSend(client,command with{ExpectedVersion=saved.Version,Fields=new(){["action"]=new("Known","FICTIONAL retained historical source")}},update:true);Assert.Equal(HttpStatusCode.OK,retained.StatusCode);var row=(await retained.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;Assert.Equal(asset.Pin,row.Asset);
        using var forged=await DefectSend(client,command with{Id=Guid.NewGuid(),Asset=asset.Pin with{Hash=new string('0',64)}});Assert.Equal(HttpStatusCode.Forbidden,forged.StatusCode);
        var current=(await client.GetFromJsonAsync<DefectRecordDto>(DefectPath(command.Id),Ct))!;Assert.Equal(asset.Pin,current.Asset);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!="equipment.read").ToArray()};
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(DefectPath(command.Id,"/history"),Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await DefectSend(client,command with{ExpectedVersion=row.Version,Asset=null},update:true)).StatusCode);
    }
    [Fact]
    public async Task Defect_source_scope_mode_and_attribution_are_required_and_client_lifecycle_or_source_override_never_grants_authority() {
        await using var host=Host();using var client=Client(host);Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(DefectPath(),Ct)).StatusCode);DefectPermissions(host.Services);
        await Token(client);using(var missingAsset=new HttpRequestMessage(HttpMethod.Post,DefectPath()){Content=JsonContent.Create(new{id=Guid.NewGuid(),expectedVersion=0,fields=new Dictionary<string,DefectFact>(),sourceVersion=DefectPolicy.SourceVersion})}) {
            missingAsset.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(missingAsset,Ct)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict,(await DefectSend(client,DefectDraft() with{SourceVersion="unapproved source"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await DefectSend(client,DefectDraft() with{Fields=new(){["closure"]=new("Known","Closed")}})).StatusCode);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);Assert.Equal(HttpStatusCode.NotFound,(await DefectSend(client,DefectDraft(),vessel:foreign.Id)).StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];settings.Memberships[0]=member with{RoleReferences=[]};
        Assert.Equal(HttpStatusCode.Forbidden,(await DefectSend(client,DefectDraft())).StatusCode);
        settings.Memberships[0]=member with{Permissions=[..member.Permissions.Where(p=>p!="mode.cross"),"mode.tug"]};Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(DefectPath(),Ct)).StatusCode);
        await using var db=Db();Assert.Empty(await db.Defects.ToArrayAsync(Ct));Assert.Empty(await db.DefectReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Defect_create_with_an_existing_other_vessel_id_is_not_classified_as_an_own_version_conflict() {
        await using var host=Host();using var client=Client(host);DefectPermissions(host.Services);var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        settings.Memberships[0]=member with{Permissions=[..member.Permissions,"mode.tug","mode.cross"]};var other=seed.Vessels.First(v=>v.TenantId==SeedVessel.TenantId && v.Id!=SeedVessel.Id);var command=DefectDraft();
        Assert.Equal(HttpStatusCode.OK,(await DefectSend(client,command,vessel:other.Id)).StatusCode);
        using var denied=await DefectSend(client,command,vessel:SeedVessel.Id);Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);Assert.DoesNotContain("version_conflict",await denied.Content.ReadAsStringAsync(Ct));
        await using var db=Db();Assert.Single(await db.Defects.Where(r=>r.Id==command.Id).ToArrayAsync(Ct));Assert.Equal(other.Id,(await db.Defects.SingleAsync(r=>r.Id==command.Id,Ct)).VesselId);
    }
    [Fact]
    public async Task Defect_synthetic_fixture_is_add_missing_and_incomplete_without_asset_safeguard_deadline_or_approval_and_preserves_operator_edits() {
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:defect-fixture","System","defect-fixture"),administrativeImport:true)) {
            await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await DefectFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}Assert.Single(await db.Defects.ToArrayAsync(Ct));Assert.Empty(await db.DefectReceipts.ToArrayAsync(Ct));
        }
        await using var host=Host();using var client=Client(host);DefectPermissions(host.Services);var id=DefectFixtures.Id(SeedVessel.Id);var before=(await client.GetFromJsonAsync<DefectRecordDto>(DefectPath(id),Ct))!;
        Assert.Equal("SyntheticUnverified",before.Provenance);Assert.Null(before.Asset);Assert.All(before.Fields,f=>Assert.Equal("Missing",f.Value.State));Assert.False(before.Temporal.ClosureAvailable);
        using var changed=await DefectSend(client,DefectDraft(id) with{ExpectedVersion=before.Version,Fields=new(){["notes"]=new("Known","FICTIONAL operator edit")}},update:true);Assert.Equal(HttpStatusCode.OK,changed.StatusCode);var saved=(await changed.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:defect-fixture","System","defect-fixture-retry"),administrativeImport:true)) {
            await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await DefectFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}var row=await db.Defects.SingleAsync(r=>r.Id==id,Ct);Assert.Equal(saved.Version,row.Version);Assert.Equal(2,row.Revision);
        }
    }
}
