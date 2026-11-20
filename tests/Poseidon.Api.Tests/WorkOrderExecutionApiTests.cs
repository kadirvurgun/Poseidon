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

// S8 WP-B DEV-054 / T-S8-003 and DEV-110 / T-SUP2-001. Real host, antiforgery and owned PostgreSQL;
// synthetic memberships only (no operational appointment). Authored; execution DEFERRED (no Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly string[] ExecutionPermissions=["equipment.read","maintenance.read","workorder.read","workorder.write","spare.read"];
    private void ExecutionActor(IServiceProvider services,Guid actor,string kind="Human",params string[] extra) {
        var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{ActorId=actor,ActorKind=kind,Permissions=[..m.Permissions.Where(p=>!p.StartsWith("workorder.",StringComparison.Ordinal) &&
            !p.StartsWith("spare.",StringComparison.Ordinal) && !p.StartsWith("requisition.",StringComparison.Ordinal)),..ExecutionPermissions,..extra]};
    }
    private string ExecutionPath(Guid id,string suffix="")=>$"work-orders/{id:D}/execution{suffix}?vessel={SeedVessel.Id:D}";
    private async Task<WorkOrderExecutionViewDto> ExecutionView(HttpClient client,Guid id)=>(await client.GetFromJsonAsync<WorkOrderExecutionViewDto>("/api/v1/"+ExecutionPath(id),Ct))!;
    private async Task<HttpResponseMessage> ExecutionTransition(HttpClient client,WorkOrderExecutionDto current,string action,string? hash=null,string? reason=null,string[]? required=null,string? key=null) {
        await Token(client);
        return await Send(client,ExecutionPath(current.WorkOrderId,"/transitions"),new WorkOrderExecutionTransitionCommand(current.WorkOrderId,action,current.Version,current.Revision,
            RequiredEvidence:required,ExpectedEvidenceHash:hash,ReasonCode:reason),key);
    }
    private async Task<WorkOrderExecutionDto> ExecutionOk(HttpResponseMessage response) {
        using(response){Assert.Equal(HttpStatusCode.OK,response.StatusCode);return (await response.Content.ReadFromJsonAsync<WorkOrderExecutionDto>(Ct))!;}
    }
    private async Task<(WorkOrderRecordDto Order,WorkOrderExecutionDto Released)> ReleasedWorkOrder(HttpClient client,IServiceProvider services,Guid releaser,string[]? required) {
        ExecutionActor(services,releaser,"Human","workorder.release");await Token(client);
        using var created=await WorkOrderSend(client,WorkOrderDraft());Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var order=(await created.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        var release=new WorkOrderExecutionTransitionCommand(order.Id,"RELEASE",0,0,order.Revision,order.Version,required);
        var released=await ExecutionOk(await Send(client,ExecutionPath(order.Id,"/transitions"),release));
        return (order,released);
    }
    private async Task<CriticalSpareDto> SpareCreate(HttpClient client,string? min,string? max,string? count) {
        await Token(client);
        var command=new CriticalSpareSaveCommand(Guid.NewGuid(),0,0,new("FICTIONAL-SP-"+Guid.NewGuid().ToString("N")[..6],"FICTIONAL seal kit","pcs","CRITICAL_DECLARED",null,min,max),
            count==null?null:new(count,DateTimeOffset.UtcNow.AddHours(-1)));
        using var response=await Send(client,"critical-spares?vesselId="+SeedVessel.Id,command);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CriticalSpareDto>(Ct))!;
    }

    [Fact]
    [Trait("Requirement","DEV-054")]
    public async Task Work_order_execution_records_evidence_and_requires_an_independent_human_for_verification_and_closure() {
        await using var host=Host();using var client=Client(host);
        var releaser=Guid.NewGuid();var engineer=Guid.NewGuid();var verifier=Guid.NewGuid();var closer=Guid.NewGuid();
        var (order,released)=await ReleasedWorkOrder(client,host.Services,releaser,["READING","SPARES_RECORD","WORK_DONE"]);
        Assert.Equal("RELEASED",released.State);Assert.Equal(order.Revision,released.WorkOrderRevision);Assert.Equal("HUMAN_RECORDED_NOT_RETURN_TO_SERVICE",released.Qualification);
        // A second release of the same work order is a version conflict, never a second execution.
        await AssertCode(await Send(client,ExecutionPath(order.Id,"/transitions"),new WorkOrderExecutionTransitionCommand(order.Id,"RELEASE",0,0,order.Revision,order.Version)),HttpStatusCode.Conflict,"version_conflict");
        ExecutionActor(host.Services,Guid.NewGuid(),"AI","workorder.release");await Token(client);
        await AssertCode(await Send(client,ExecutionPath(order.Id,"/transitions"),new WorkOrderExecutionTransitionCommand(order.Id,"RELEASE",0,0,order.Revision,order.Version)),HttpStatusCode.Forbidden,"human_actor_required");

        ExecutionActor(host.Services,engineer,"Human","workorder.execute","spare.write");
        var spare=await SpareCreate(client,"1","5","3");
        var started=await ExecutionOk(await ExecutionTransition(client,released,"START"));Assert.Equal("IN_PROGRESS",started.State);Assert.True(started.ExecutedByCurrentActor);
        var evidence=new WorkOrderEvidence([new("BEARING_TEMP","61.5","degC")],[new(spare.Id,spare.Revision,"2")],[],"FICTIONAL bearing replaced","no finding");
        var key=Guid.NewGuid().ToString("D");
        var saved=await ExecutionOk(await Send(client,ExecutionPath(order.Id,"/evidence"),new WorkOrderExecutionEvidenceCommand(order.Id,started.Version,started.Revision,evidence),key,update:true));
        using(var replay=await Send(client,ExecutionPath(order.Id,"/evidence"),new WorkOrderExecutionEvidenceCommand(order.Id,started.Version,started.Revision,evidence),key,update:true))
            Assert.Equal(saved.Hash,(await replay.Content.ReadFromJsonAsync<WorkOrderExecutionDto>(Ct))!.Hash);
        await AssertCode(await Send(client,ExecutionPath(order.Id,"/evidence"),new WorkOrderExecutionEvidenceCommand(order.Id,started.Version,started.Revision,evidence with{Findings="other"}),key,update:true),
            HttpStatusCode.Conflict,"idempotency_key_reused");
        await AssertCode(await Send(client,ExecutionPath(order.Id,"/evidence"),new WorkOrderExecutionEvidenceCommand(order.Id,started.Version,started.Revision,evidence),update:true),HttpStatusCode.Conflict,"version_conflict");
        await AssertCode(await Send(client,ExecutionPath(order.Id,"/evidence"),new WorkOrderExecutionEvidenceCommand(order.Id,saved.Version,saved.Revision,evidence with{Spares=[new(Guid.NewGuid(),1,"1")]}),update:true),
            HttpStatusCode.Forbidden,"linked_resource_unavailable");
        Assert.Equal("PRESENT_PENDING_HUMAN_VERIFICATION",saved.Gate.State);
        await AssertCode(await ExecutionTransition(client,saved,"SUBMIT",new string('0',64)),HttpStatusCode.Conflict,"evidence_changed");
        var submitted=await ExecutionOk(await ExecutionTransition(client,saved,"SUBMIT",saved.EvidenceHash));Assert.Equal("EVIDENCE_SUBMITTED",submitted.State);
        // Submitted catalogued consumption lowers the projected stock (3 counted - 2 used <= min 1).
        var spareNow=(await client.GetFromJsonAsync<CriticalSpareDto>($"/api/v1/critical-spares/{spare.Id:D}?vesselId={SeedVessel.Id:D}",Ct))!;
        Assert.Equal("LOW_STOCK",spareNow.Stock.State);Assert.Equal("1",spareNow.Stock.Projected);Assert.Equal("4",spareNow.Stock.ProposedQuantity);

        // The executor cannot verify their own work even with the verify permission.
        ExecutionActor(host.Services,engineer,"Human","workorder.execute","workorder.verify");await Token(client);
        await AssertCode(await ExecutionTransition(client,submitted,"VERIFY",submitted.EvidenceHash),HttpStatusCode.Conflict,"separation_of_duties");
        ExecutionActor(host.Services,verifier,"AI","workorder.verify");await Token(client);
        await AssertCode(await ExecutionTransition(client,submitted,"VERIFY",submitted.EvidenceHash),HttpStatusCode.Forbidden,"human_actor_required");
        ExecutionActor(host.Services,verifier,"Human");await Token(client);
        await AssertCode(await ExecutionTransition(client,submitted,"VERIFY",submitted.EvidenceHash),HttpStatusCode.Forbidden,"permission_denied");
        ExecutionActor(host.Services,verifier,"Human","workorder.verify");await Token(client);
        var verified=await ExecutionOk(await ExecutionTransition(client,submitted,"VERIFY",submitted.EvidenceHash));
        Assert.Equal("VERIFIED",verified.State);Assert.Equal("HUMAN_VERIFIED",verified.Gate.State);Assert.NotNull(verified.VerifiedAt);
        ExecutionActor(host.Services,engineer,"Human","workorder.close");await Token(client);
        await AssertCode(await ExecutionTransition(client,verified,"CLOSE",verified.EvidenceHash),HttpStatusCode.Conflict,"separation_of_duties");
        ExecutionActor(host.Services,closer,"Human","workorder.close");await Token(client);
        var closed=await ExecutionOk(await ExecutionTransition(client,verified,"CLOSE",verified.EvidenceHash));
        Assert.Equal("CLOSED",closed.State);Assert.Contains("RETURN_TO_SERVICE_NOT_RECORDED",closed.Blockers);

        var history=(await client.GetFromJsonAsync<WorkOrderExecutionHistoryDto>("/api/v1/"+ExecutionPath(order.Id,"/history"),Ct))!;
        Assert.Equal(new[]{"CLOSE","VERIFY","SUBMIT","EVIDENCE","START","RELEASE"},history.Items.Select(r=>r.Transition));Assert.Single(history.Items,r=>r.CurrentRevision);
        await using var db=Db();
        var audit=await db.AuditEvents.Where(e=>e.ResourceId==order.Id).ToArrayAsync(Ct);var serialized=JsonSerializer.Serialize(audit);
        Assert.DoesNotContain("BEARING_TEMP",serialized);Assert.DoesNotContain("FICTIONAL bearing",serialized);
        Assert.Contains(audit,e=>e.ResourceKind=="WorkOrderExecutionReceipt" && e.AfterVersion==closed.Version && e.AfterRevision==closed.Revision &&
            e.ChangeSummary!=null && e.ChangeSummary.Contains("WorkOrderExecutionTransition") && e.ChangeSummary.Contains("HUMAN_REVIEW_WORKFLOW"));
        Assert.Contains(audit,e=>e.ChangeSummary!=null && e.ChangeSummary.Contains("WorkOrderExecutionEvidence") && e.ChangeSummary.Contains("PREPARATION_COMMAND"));
        Assert.Contains(audit,e=>e.ResourceKind=="WorkOrderExecution" && e.ChangeSummary!.Contains("CLOSED") && e.ActorId==closer.ToString());
        Assert.All(audit,e=>Assert.Equal(SeedVessel.TenantId,e.TenantId));
        var row=await db.WorkOrderExecutionRevisions.SingleAsync(r=>r.WorkOrderId==order.Id && r.Revision==1,Ct);row.ContentJson="{}";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement","DEV-054")]
    public async Task Unconfigured_required_evidence_policy_is_review_required_and_blocks_verification_until_an_independent_declaration() {
        await using var host=Host();using var client=Client(host);
        var releaser=Guid.NewGuid();var engineer=Guid.NewGuid();var verifier=Guid.NewGuid();
        var (order,released)=await ReleasedWorkOrder(client,host.Services,releaser,null);
        Assert.Null(released.RequiredEvidence);Assert.Equal("REVIEW_REQUIRED",released.Gate.State);Assert.Contains("REQUIRED_EVIDENCE_POLICY_UNCONFIGURED",released.Blockers);
        ExecutionActor(host.Services,engineer,"Human","workorder.execute");await Token(client);
        var started=await ExecutionOk(await ExecutionTransition(client,released,"START"));
        var saved=await ExecutionOk(await Send(client,ExecutionPath(order.Id,"/evidence"),new WorkOrderExecutionEvidenceCommand(order.Id,started.Version,started.Revision,
            new([new("RUN_HOURS","1200")],[],[],"FICTIONAL inspection")),update:true));
        var submitted=await ExecutionOk(await ExecutionTransition(client,saved,"SUBMIT",saved.EvidenceHash));
        ExecutionActor(host.Services,verifier,"Human","workorder.verify");await Token(client);
        await AssertCode(await ExecutionTransition(client,submitted,"VERIFY",submitted.EvidenceHash),HttpStatusCode.UnprocessableEntity,"required_evidence_unconfigured");
        var returned=await ExecutionOk(await ExecutionTransition(client,submitted,"RETURN",submitted.EvidenceHash,"EVIDENCE_INSUFFICIENT"));
        Assert.Equal("IN_PROGRESS",returned.State);Assert.Equal("EVIDENCE_INSUFFICIENT",returned.ReturnReason);
        ExecutionActor(host.Services,releaser,"Human","workorder.release");await Token(client);
        var configured=await ExecutionOk(await ExecutionTransition(client,returned,"REQUIREMENTS",required:["READING","PHOTO"]));
        Assert.Equal("MISSING",configured.Gate.State);Assert.Equal(new[]{"PHOTO"},configured.Gate.Missing);
        ExecutionActor(host.Services,engineer,"Human","workorder.execute");await Token(client);
        // A declared PHOTO requirement without a photo blocks submission.
        await AssertCode(await ExecutionTransition(client,configured,"SUBMIT",configured.EvidenceHash),HttpStatusCode.UnprocessableEntity,"required_evidence_missing");
        var view=await ExecutionView(client,order.Id);Assert.True(view.Capabilities.CanExecute);Assert.False(view.Capabilities.CanVerify);Assert.Equal("UNAVAILABLE_POLICY",view.Capabilities.Offline.State);
        using(var foreign=await client.GetAsync($"/api/v1/work-orders/{order.Id:D}/execution?vessel={Guid.NewGuid():D}",Ct))Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
    }

    [Fact]
    [Trait("Requirement","DEV-110")]
    public async Task Low_stock_proposes_a_technical_requisition_approved_by_a_different_human_as_an_erp_export_reference_only() {
        await using var host=Host(technicalRead:true);using var client=Client(host);
        var proposer=Guid.NewGuid();var approver=Guid.NewGuid();
        ExecutionActor(host.Services,proposer,"Human","spare.write","requisition.propose");await Token(client);
        var unknown=await SpareCreate(client,null,null,null);Assert.Equal("STOCK_UNKNOWN",unknown.Stock.State);Assert.Equal("UNKNOWN",unknown.EquipmentCriticality);
        await AssertCode(await Send(client,"technical-requisitions?vesselId="+SeedVessel.Id,new TechnicalRequisitionProposeCommand(Guid.NewGuid(),unknown.Id,unknown.Revision)),
            HttpStatusCode.UnprocessableEntity,"stock_not_low");
        var key=Guid.NewGuid().ToString("D");
        var update=new CriticalSpareSaveCommand(unknown.Id,unknown.Version,unknown.Revision,unknown.Definition with{MinQuantity="2"},new("1",DateTimeOffset.UtcNow.AddMinutes(-5)));
        using var updated=await Send(client,$"critical-spares/{unknown.Id:D}?vesselId={SeedVessel.Id:D}",update,key,update:true);Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        var low=(await updated.Content.ReadFromJsonAsync<CriticalSpareDto>(Ct))!;Assert.Equal("LOW_STOCK",low.Stock.State);Assert.Null(low.Stock.ProposedQuantity);
        await AssertCode(await Send(client,$"critical-spares/{unknown.Id:D}?vesselId={SeedVessel.Id:D}",update,update:true),HttpStatusCode.Conflict,"version_conflict");
        var page=(await client.GetFromJsonAsync<CriticalSparePageDto>("/api/v1/critical-spares?vesselId="+SeedVessel.Id,Ct))!;
        Assert.True(page.LowStock>=1);Assert.Equal("EXPORT_REFERENCE_ONLY_NO_PURCHASE_ORDER",page.ErpBoundary);

        var propose=new TechnicalRequisitionProposeCommand(Guid.NewGuid(),low.Id,low.Revision);
        using var proposed=await Send(client,"technical-requisitions?vesselId="+SeedVessel.Id,propose);Assert.Equal(HttpStatusCode.OK,proposed.StatusCode);
        var requisition=(await proposed.Content.ReadFromJsonAsync<TechnicalRequisitionDto>(Ct))!;
        Assert.Equal("PROPOSED",requisition.State);Assert.True(requisition.ProposedByCurrentActor);Assert.Null(requisition.ErpReference);Assert.Null(requisition.ProposedQuantity);
        await AssertCode(await Send(client,"technical-requisitions?vesselId="+SeedVessel.Id,propose with{Id=Guid.NewGuid()}),HttpStatusCode.Conflict,"requisition_already_open");
        var decide=new TechnicalRequisitionDecisionCommand(requisition.Id,requisition.Version,requisition.Revision,"APPROVE","3");
        ExecutionActor(host.Services,proposer,"Human","requisition.approve");await Token(client);
        await AssertCode(await Send(client,$"technical-requisitions/{requisition.Id:D}/decisions?vesselId={SeedVessel.Id:D}",decide),HttpStatusCode.Conflict,"separation_of_duties");
        ExecutionActor(host.Services,approver,"AI","requisition.approve");await Token(client);
        await AssertCode(await Send(client,$"technical-requisitions/{requisition.Id:D}/decisions?vesselId={SeedVessel.Id:D}",decide),HttpStatusCode.Forbidden,"human_actor_required");
        ExecutionActor(host.Services,approver,"Human","requisition.approve");await Token(client);
        await AssertCode(await Send(client,$"technical-requisitions/{requisition.Id:D}/decisions?vesselId={SeedVessel.Id:D}",decide with{ApprovedQuantity=null}),HttpStatusCode.UnprocessableEntity,"quantity_required");
        using var approved=await Send(client,$"technical-requisitions/{requisition.Id:D}/decisions?vesselId={SeedVessel.Id:D}",decide);Assert.Equal(HttpStatusCode.OK,approved.StatusCode);
        var decision=(await approved.Content.ReadFromJsonAsync<TechnicalRequisitionDto>(Ct))!;
        Assert.Equal("APPROVED_FOR_ERP_EXPORT",decision.State);Assert.Equal("3",decision.ApprovedQuantity);Assert.NotNull(decision.ErpReference);
        Assert.Equal("TechnicalRequisition",decision.ErpReference!.Kind);Assert.Equal("EXPORT_REFERENCE_ONLY_NO_PURCHASE_ORDER",decision.ErpReference.Boundary);
        var list=(await client.GetFromJsonAsync<TechnicalRequisitionPageDto>("/api/v1/technical-requisitions?vesselId="+SeedVessel.Id,Ct))!;
        Assert.Contains(list.Items,r=>r.Id==requisition.Id && r.State=="APPROVED_FOR_ERP_EXPORT" && !r.CanDecide);
        var navigation=(await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace",Ct))!;
        Assert.Contains(navigation.Destinations,d=>d.Key=="critical-spares" && d.Href=="/app/critical-spares");

        await using var db=Db();
        var audit=await db.AuditEvents.Where(e=>e.ResourceId==requisition.Id || e.ResourceId==low.Id).ToArrayAsync(Ct);var serialized=JsonSerializer.Serialize(audit);
        Assert.DoesNotContain("FICTIONAL seal kit",serialized);Assert.DoesNotContain(low.Definition.PartNumber,serialized);
        Assert.Contains(audit,e=>e.ResourceKind=="TechnicalSupplyReceipt" && e.ChangeSummary!.Contains("TechnicalRequisitionDecide") && e.AfterVersion==decision.Version);
        Assert.Contains(audit,e=>e.ResourceKind=="TechnicalSupplyReceipt" && e.ChangeSummary!.Contains("CriticalSpareUpdate"));
        Assert.Contains(audit,e=>e.ResourceKind=="TechnicalRequisition" && e.ChangeSummary!.Contains("APPROVED_FOR_ERP_EXPORT") && e.ActorId==approver.ToString());
        var stored=await db.TechnicalRequisitionRevisions.SingleAsync(r=>r.RequisitionId==requisition.Id && r.Revision==1,Ct);stored.ContentJson="{}";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
        // Without spare.read the screen and its data are withheld.
        ExecutionActor(host.Services,approver,"Human");var settings=host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0]=settings.Memberships[0] with{Permissions=settings.Memberships[0].Permissions.Where(p=>p!="spare.read").ToArray()};
        using(var denied=await client.GetAsync("/api/v1/critical-spares?vesselId="+SeedVessel.Id,Ct))Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }

    [Fact]
    [Trait("Requirement","DEV-110")]
    public async Task Critical_spare_fixture_is_optional_idempotent_fictional_and_never_low_stock_healthy_or_requisitioned() {
        for(var i=0;i<2;i++) {
            await using var admin=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:critical-spare-fixture","System","critical-spare-fixture"),administrativeImport:true);
            await using var tx=await admin.Database.BeginTransactionAsync(Ct);await CriticalSpareFixtures.AddMissingAsync(admin,[SeedVessel],Ct);await tx.CommitAsync(Ct);
        }
        await using var db=Db();var id=CriticalSpareFixtures.Id(SeedVessel.Id);
        var row=Assert.Single(await db.CriticalSpareRevisions.Where(r=>r.SpareId==id).ToArrayAsync(Ct));Assert.Equal("SyntheticUnverified",row.Provenance);
        var content=JsonSerializer.Deserialize<CriticalSpareContent>(row.ContentJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("STOCK_UNKNOWN",CriticalSparePolicy.Project(content,[]).State);Assert.Equal("UNKNOWN",content.Definition.Criticality);
        Assert.False(await db.TechnicalRequisitions.AnyAsync(r=>r.SpareId==id,Ct));Assert.False(await db.TechnicalSupplyReceipts.AnyAsync(r=>r.ResourceId==id,Ct));
    }
}
