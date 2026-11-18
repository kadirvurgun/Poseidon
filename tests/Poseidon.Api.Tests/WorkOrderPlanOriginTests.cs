using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-054 actual owning host/PostgreSQL source cases, authored and NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task WorkOrder_plan_context_uses_actual_plan_xmin_and_minimized_asset_without_fabricating_evidence_authority_or_old_versions() {
        await using var host=Host(technicalRead:true,technicalWrite:true);using var client=Client(host);WorkOrderPermissions(host.Services);
        var context=await WorkOrderPlan(client);await using var db=Db();var head=await db.MaintenancePlans.SingleAsync(p=>p.Id==context.Pin.Id,Ct);
        Assert.Equal(head.Version,context.Pin.Version);Assert.Equal(head.Revision,context.Pin.Revision);Assert.Equal("poseidon.maintenance-plan-context.v1",context.Pin.SourceVersion);
        Assert.Equal("REVIEW_REQUIRED",context.Qualification);Assert.True(context.Current);Assert.Equal("Draft",context.Plan.Status);
        var raw=JsonSerializer.Serialize(context,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach(var withheld in new[]{"tenantId","ownerRoleId","responsibleRoleId","serialNumber","contentJson","calculationJson","observedAt","evidence"})Assert.DoesNotContain(withheld,raw);
        var command=WorkOrderDraft() with{Plan=context.Pin};
        foreach(var forged in new[]{context.Pin with{Version=context.Pin.Version+1},context.Pin with{SourceVersion="unapproved"},context.Pin with{Hash=new string('0',64)}})
            Assert.Equal(HttpStatusCode.Forbidden,(await WorkOrderSend(client,command with{Id=Guid.NewGuid(),Plan=forged})).StatusCode);
        foreach(var suffix in new[]{"?revision=0","?revision=-1","?revision=1&revision=1","?actorId="+SeedVessel.Id,"?revision=1&tenantId="+SeedVessel.TenantId})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(WorkOrderPlanPath(context.Pin.Id)+suffix,Ct)).StatusCode);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        await using(var foreignDb=Db(foreign.TenantId))Assert.True(await foreignDb.MaintenancePlans.AnyAsync(p=>p.VesselId==foreign.Id,Ct));
        var foreignId=await DbPlanId(foreign.TenantId,foreign.Id);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/v1/vessels/{foreign.Id}/work-order-plan-origins/{foreignId}",Ct)).StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();var membership=settings.Memberships[0];
        foreach(var permission in new[]{"maintenance.read","equipment.read","workorder.read"}) {
            settings.Memberships[0]=membership with{Permissions=membership.Permissions.Where(p=>p!=permission).ToArray()};
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(WorkOrderPlanPath(context.Pin.Id),Ct)).StatusCode);
        }
    }
    private async Task<Guid> DbPlanId(Guid tenant,Guid vessel) {
        await using var db=Db(tenant);return await db.MaintenancePlans.Where(p=>p.VesselId==vessel).Select(p=>p.Id).FirstAsync(Ct);
    }
    [Theory]
    [InlineData("permission")][InlineData("source-head")]
    public async Task WorkOrder_plan_context_rechecks_permission_and_actual_current_source_after_success_audit(string change) {
        await using var host=Host(technicalRead:true,technicalWrite:true);using var client=Client(host);WorkOrderPermissions(host.Services);var context=await WorkOrderPlan(client);
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_work_order_plan() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_work_order_plan BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='WorkOrderPlanOriginRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.pause_work_order_plan();
            """);
        var pending=client.GetAsync(WorkOrderPlanPath(context.Pin.Id),Ct);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            if(change=="permission") {
                var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];settings.Memberships[0]=m with{Permissions=m.Permissions.Where(p=>p!="maintenance.read").ToArray()};
            }else Assert.Equal(HttpStatusCode.OK,(await Send(client,PlansUrl,new MaintenanceCommand(context.Pin.Id,context.Pin.Version,context.Plan.AssetId,null,null,new(Criticality:"Unknown"),DateTimeOffset.UnixEpoch))).StatusCode);
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.DoesNotContain("pin",await response.Content.ReadAsStringAsync(Ct));
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_work_order_plan ON audit."SecurityEvents";DROP FUNCTION audit.pause_work_order_plan();""");
        }
    }
    [Fact]
    public async Task WorkOrder_evidence_and_verifier_references_remain_unavailable_without_adoption_or_qualified_human_contract() {
        await using var host=Host();using var client=Client(host);WorkOrderPermissions(host.Services);
        foreach(var key in new[]{"photo_evidence","document_evidence","verified_by","equipment_id","job_id","assigned_to","work_order_type"})
            Assert.Equal(HttpStatusCode.BadRequest,(await WorkOrderSend(client,WorkOrderDraft() with{Fields=new(){[key]=new("Known",Guid.NewGuid().ToString("D"))}})).StatusCode);
        var declaration=WorkOrderDraft() with{Fields=new(){["execution_stage"]=new("Known","RELEASED"),["verification_result"]=new("Known","Verified")}};
        using var saved=await WorkOrderSend(client,declaration);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);var record=(await saved.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        Assert.Equal("Verified",record.Fields["verification_result"].Value);Assert.Equal("DRAFT",record.State);Assert.Equal("REVIEW_REQUIRED",record.Qualification);
        var caps=(await client.GetFromJsonAsync<WorkOrderCapabilitiesDto>(WorkOrderPath(suffix:"/capabilities"),Ct))!;
        Assert.False(caps.CanRelease);Assert.False(caps.CanSubmitEvidence);Assert.False(caps.CanVerify);Assert.False(caps.CanClose);Assert.False(caps.Offline.Supported);
    }
}
