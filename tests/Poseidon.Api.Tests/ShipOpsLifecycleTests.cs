using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-101/T-OPS2-005 SOF finalization and DEV-102/T-OPS2-006 downtime lifecycle,
// actual HTTP/PG sources (Integration; needs Docker PostgreSQL).
public sealed partial class DirectoryWorkspaceTests
{
    private ShipOperationsAuthority OpsAppointment(IServiceProvider services,string scope,string duty,params string[] permissions) {
        var settings=services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        var grant=new ShipOperationsAuthority(Guid.NewGuid(),SeedVessel.Id,scope,Guid.Parse(member.RoleReferences![0]),duty,"FICTIONAL owned-test appointment",
            DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
        settings.Memberships[0]=member with{ActorKind="Human",ShipOperationsAuthorities=[grant],Permissions=[..member.Permissions,..permissions]};return grant;
    }
    private void OpsSwitchActor(IServiceProvider services,Guid actor){var settings=services.GetRequiredService<FederationSettings>();settings.Memberships[0]=settings.Memberships[0] with{ActorId=actor};}
    private async Task<HttpResponseMessage> OpsPost(HttpClient client,string path,object body,string? key=null) {
        await Token(client);using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
        request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString("D"));return await client.SendAsync(request,Ct);
    }
    private static readonly string[] Unresolved=["port","port_call_id","voyage_id"];

    [Fact]
    public async Task Sof_finalize_is_a_human_appointed_command_producing_an_immutable_final_revision_without_signature() {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);
        var incomplete=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){["remarks"]=new("Known","FICTIONAL")},"INITIAL_AUTHORING");
        using(var r=await ShipOpsSend(client,"Port",incomplete))Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){["activity_type"]=new("Known","Berthing"),["event_start"]=new("Known","2026-01-01T10:00:00Z")},"INITIAL_AUTHORING");
        using(var r=await ShipOpsSend(client,"Port",command))Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        var head=(await client.GetFromJsonAsync<ShipOpsRecordDto>(ShipOpsPath("Port",command.Id),Ct))!;
        // No permission / appointment: never finalized implicitly.
        Assert.Equal(HttpStatusCode.Forbidden,(await OpsPost(client,ShipOpsPath("Port",command.Id,"/finalize"),new PortSofFinalizeCommand(head.Version,Guid.NewGuid(),ShipOperationsLifecycle.SofReason,Unresolved))).StatusCode);
        var grant=OpsAppointment(host.Services,ShipOperationsLifecycle.SofScope,"Master","operations.port.finalize");
        var view=(await client.GetFromJsonAsync<PortSofFinalizationDto>(ShipOpsPath("Port",command.Id,"/finalization"),Ct))!;
        Assert.True(view.CanFinalize);Assert.Equal(Unresolved,view.UnresolvedReferences.Order(StringComparer.Ordinal));Assert.Contains("SOF_SIGNATURE_NOT_RECORDED",view.Qualifications);
        var incompleteHead=(await client.GetFromJsonAsync<ShipOpsRecordDto>(ShipOpsPath("Port",incomplete.Id),Ct))!;
        using(var r=await OpsPost(client,ShipOpsPath("Port",incomplete.Id,"/finalize"),new PortSofFinalizeCommand(incompleteHead.Version,grant.Id,ShipOperationsLifecycle.SofReason,Unresolved)))
            {Assert.Equal(HttpStatusCode.UnprocessableEntity,r.StatusCode);Assert.Contains("sof_record_incomplete",await r.Content.ReadAsStringAsync(Ct));}
        using(var r=await OpsPost(client,ShipOpsPath("Port",command.Id,"/finalize"),new PortSofFinalizeCommand(head.Version,grant.Id,ShipOperationsLifecycle.SofReason,["voyage_id"])))
            {Assert.Equal(HttpStatusCode.UnprocessableEntity,r.StatusCode);Assert.Contains("sof_references_not_acknowledged",await r.Content.ReadAsStringAsync(Ct));}
        var key=Guid.NewGuid().ToString("D");var finalize=new PortSofFinalizeCommand(head.Version,grant.Id,ShipOperationsLifecycle.SofReason,Unresolved);
        using var first=await OpsPost(client,ShipOpsPath("Port",command.Id,"/finalize"),finalize,key);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var bytes=await first.Content.ReadAsStringAsync(Ct);
        var final=System.Text.Json.JsonSerializer.Deserialize<ShipOpsRecordDto>(bytes,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal("FINAL",final.Fields["sof_state"].Value);Assert.Equal(head.Revision+1,final.Revision);Assert.Contains("SOF_SIGNATURE_NOT_RECORDED",final.Blockers);
        using(var replay=await OpsPost(client,ShipOpsPath("Port",command.Id,"/finalize"),finalize,key))Assert.Equal(bytes,await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Conflict,(await OpsPost(client,ShipOpsPath("Port",command.Id,"/finalize"),finalize with{ExpectedVersion=final.Version})).StatusCode);
        // T63: an ordinary correction is refused; an explicit amendment appends a DRAFT revision and keeps the FINAL revision immutable.
        using(var refused=await ShipOpsSend(client,"Port",command with{ExpectedVersion=final.Version,ReasonCode="AUTHORING_CORRECTION"},update:true))
            {Assert.Equal(HttpStatusCode.Conflict,refused.StatusCode);Assert.Contains("sof_finalized",await refused.Content.ReadAsStringAsync(Ct));}
        using var amended=await ShipOpsSend(client,"Port",command with{ExpectedVersion=final.Version,ReasonCode=ShipOperationsLifecycle.PostFinalAmendment,
            Fields=new(command.Fields){["remarks"]=new("Known","FICTIONAL late pilot event")}},update:true);Assert.Equal(HttpStatusCode.OK,amended.StatusCode);
        var amendment=(await amended.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;Assert.Equal("DRAFT",amendment.Fields["sof_state"].Value);Assert.Contains("SOF_AMENDED_AFTER_FINAL_REVIEW_REQUIRED",amendment.Blockers);
        var history=(await client.GetFromJsonAsync<ShipOpsHistoryDto>(ShipOpsPath("Port",command.Id,"/history"),Ct))!;
        var kept=history.Items.Single(i=>i.Revision==final.Revision);Assert.Equal(final.Hash,kept.Hash);Assert.Equal("FINAL",kept.Fields["sof_state"].Value);
        await using var db=Db();var events=await db.ShipOpsLifecycleEvents.Where(e=>e.RecordId==command.Id).ToArrayAsync(Ct);
        var recorded=Assert.Single(events);Assert.Equal("Master",recorded.Duty);Assert.Equal(grant.Id,recorded.AuthorityId);Assert.Equal("SOF_SIGNATURE_NOT_RECORDED",recorded.SignatureState);
        recorded.ReasonCode="TAMPERED";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }

    private static readonly Dictionary<string,DowntimeFact> CompleteDowntime=new() {
        ["downtime_type"]=new("Known","Technical Failure"),["start_at"]=new("Known","2026-11-09T08:00:00Z"),["end_at"]=new("Known","2026-11-09T10:00:00Z"),
        ["cause"]=new("Known","FICTIONAL cause"),["operational_impact"]=new("Known","FICTIONAL impact")};

    [Fact]
    public async Task Downtime_lifecycle_flags_loss_of_hire_candidate_and_enforces_recorder_never_verifies_or_closes() {
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);
        var draft=DowntimeDraft() with{Fields=new(CompleteDowntime)};using var created=await DowntimeSend(client,draft);Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var row=(await client.GetFromJsonAsync<DowntimeRecordDto>(DowntimePath(draft.Id),Ct))!;
        Assert.Equal("REVIEW_REQUIRED",row.Fields["loss_of_hire_flag"].Value);Assert.Equal("Unknown",row.Fields["closure_state"].State);Assert.Equal("NOT_SUBMITTED",row.Lifecycle!.State);
        Assert.Contains("LOSS_OF_HIRE_CANDIDATE_HUMAN_REVIEW_REQUIRED",row.Blockers);
        object Cmd(uint v,string action,Guid? authority=null)=>new DowntimeLifecycleCommand(v,action,OperationalDowntimeEndpoints.LifecycleReason,authority);
        // Verify before submission is not a shortcut.
        var recorder=host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId;
        var grant=OpsAppointment(host.Services,ShipOperationsLifecycle.DowntimeScope,"ChiefEngineer","operations.downtime.review");
        Assert.Equal(HttpStatusCode.Conflict,(await OpsPost(client,DowntimePath(draft.Id,"/lifecycle"),Cmd(row.Version,"VERIFY",grant.Id))).StatusCode);
        using var submitted=await OpsPost(client,DowntimePath(draft.Id,"/lifecycle"),Cmd(row.Version,"SUBMIT_FOR_REVIEW"));Assert.Equal(HttpStatusCode.OK,submitted.StatusCode);
        var inReview=(await submitted.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;Assert.Equal("IN_REVIEW",inReview.Lifecycle!.State);
        // Submitted content is read-only.
        using(var locked=await DowntimeSend(client,draft with{ExpectedVersion=inReview.Version},update:true))
            {Assert.Equal(HttpStatusCode.Conflict,locked.StatusCode);Assert.Contains("downtime_lifecycle_locked",await locked.Content.ReadAsStringAsync(Ct));}
        // The recorder holding an appointment still cannot verify (separation of duties).
        using(var self=await OpsPost(client,DowntimePath(draft.Id,"/lifecycle"),Cmd(inReview.Version,"VERIFY",grant.Id)))
            {Assert.Equal(HttpStatusCode.Forbidden,self.StatusCode);Assert.Contains("separation_of_duties",await self.Content.ReadAsStringAsync(Ct));}
        var reviewer=Guid.NewGuid();OpsSwitchActor(host.Services,reviewer);
        var view=(await client.GetFromJsonAsync<DowntimeLifecycleViewDto>(DowntimePath(draft.Id,"/lifecycle"),Ct))!;Assert.False(view.ActorIsRecorder);Assert.Contains("VERIFY",view.AllowedActions);
        using var verified=await OpsPost(client,DowntimePath(draft.Id,"/lifecycle"),Cmd(view.Version,"VERIFY",grant.Id));Assert.Equal(HttpStatusCode.OK,verified.StatusCode);
        var v=(await verified.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;Assert.Equal("VERIFIED",v.Fields["closure_state"].Value);Assert.Contains("HUMAN_VERIFICATION_IS_NOT_SAFETY_RELEASE",v.Blockers);
        using var closed=await OpsPost(client,DowntimePath(draft.Id,"/lifecycle"),Cmd(v.Version,"CLOSE",grant.Id));Assert.Equal(HttpStatusCode.OK,closed.StatusCode);
        var c=(await closed.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;Assert.Equal("CLOSED",c.Fields["closure_state"].Value);
        Assert.Equal("REVIEW_REQUIRED",c.Fields["loss_of_hire_flag"].Value);Assert.False(c.CanEdit);
        await using var db=Db();var events=await db.ShipOpsLifecycleEvents.Where(e=>e.RecordId==draft.Id).OrderBy(e=>e.ToRevision).ToArrayAsync(Ct);
        Assert.Equal(new[]{"SUBMIT_FOR_REVIEW","VERIFY","CLOSE"},events.Select(e=>e.Action));Assert.Equal(recorder,events[0].ActorId);Assert.All(events.Skip(1),e=>Assert.Equal(reviewer,e.ActorId));
        Assert.Equal(4,await db.DowntimeRevisions.CountAsync(r=>r.DowntimeId==draft.Id,Ct));
    }

    [Fact]
    public async Task Downtime_open_period_cannot_be_submitted_and_claim_evidence_requires_a_visible_current_pin() {
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);
        var fields=new Dictionary<string,DowntimeFact>(CompleteDowntime);fields.Remove("end_at");var draft=DowntimeDraft() with{Fields=fields};
        using(var r=await DowntimeSend(client,draft))Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        var row=(await client.GetFromJsonAsync<DowntimeRecordDto>(DowntimePath(draft.Id),Ct))!;Assert.Contains("DOWNTIME_PERIOD_OPEN_END_NOT_ASSUMED",row.Blockers);
        using(var r=await OpsPost(client,DowntimePath(draft.Id,"/lifecycle"),new DowntimeLifecycleCommand(row.Version,"SUBMIT_FOR_REVIEW",OperationalDowntimeEndpoints.LifecycleReason)))
            {Assert.Equal(HttpStatusCode.UnprocessableEntity,r.StatusCode);Assert.Contains("downtime_record_incomplete",await r.Content.ReadAsStringAsync(Ct));}
        using(var r=await OpsPost(client,DowntimePath(draft.Id,"/lifecycle"),new DowntimeLifecycleCommand(row.Version,"LINK_CLAIM_EVIDENCE",OperationalDowntimeEndpoints.LifecycleReason,null,new(Guid.NewGuid(),1))))
            Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await OpsPost(client,DowntimePath(draft.Id,"/lifecycle"),new DowntimeLifecycleCommand(row.Version,"APPROVE_CLAIM",OperationalDowntimeEndpoints.LifecycleReason))).StatusCode);
        await using var db=Db();Assert.Empty(await db.ShipOpsLifecycleEvents.Where(e=>e.RecordId==draft.Id).ToArrayAsync(Ct));
    }
}
