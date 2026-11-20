using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S8 WP-B DEV-057 / T-S8-006 (18.01 row58): WO context prefetch, durable signed evidence capture, idempotent
// reconnect and material conflict -> conflict center. FICTIONAL classification references are NOT approved
// policy. Authored; execution DEFERRED (no Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private WorkOrderOfflineSettings SyntheticWorkOrderOffline(DailyOfflineClock clock,Guid actor)=>new(){Policy=new WorkOrderOfflinePolicy {
        Version="fictional-wo-offline-v1",SourceVersion=WorkOrderExecutionPolicy.SourceVersion,StoragePolicyReference="FICTIONAL encrypted IndexedDB test only",
        KeyPolicyReference="FICTIONAL nonextractable key test only",RetentionPolicyReference="FICTIONAL isolated fixture retention; no approval",RetentionSeconds=7200,LeaseSeconds=600,
        NotBefore=clock.Current.AddDays(-1),ExpiresAt=clock.Current.AddDays(1),MaxEnrollments=8,MaxRecords=32,MaxEncryptedBytes=1048576,MaxDeviceRecords=64,MaxDeviceEncryptedBytes=4194304,
        Fields=WorkOrderOfflinePolicy.EvidenceParts.Select(k=>new WorkOrderOfflineField(k,"FICTIONAL classification; not source approval","Fictional regression only","RestrictedOperationalDraft")).ToArray(),
        Scopes=[new(SeedVessel.TenantId,actor,SeedVessel.Id)]}};
    private WebApplicationFactory<Program> WorkOrderOfflineHost(DailyOfflineClock clock,Guid actor)=>Host().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>{
        s.RemoveAll<WorkOrderOfflineSettings>();s.AddSingleton(SyntheticWorkOrderOffline(clock,actor));s.RemoveAll<TimeProvider>();s.AddSingleton<TimeProvider>(clock);}));
    private string WorkOrderOfflinePath(Guid workOrder,Guid? enrollment=null,string action="")=>
        $"work-order-offline/{workOrder:D}"+(enrollment==null?"":$"/{enrollment:D}")+(action.Length==0?"":"/"+action)+$"?vesselId={SeedVessel.Id:D}";
    private async Task<WorkOrderOfflineContextDto> EnrollWorkOrderOffline(HttpClient c,Guid workOrder) {
        await Token(c);using var response=await Send(c,WorkOrderOfflinePath(workOrder),new{});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var e=(await response.Content.ReadFromJsonAsync<WorkOrderOfflineContextDto>(Ct))!;
        var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);offlineTestKeys.Add(e.Id,key);
        var proof=Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes($"work-order/{workOrder:D}/{e.Id:D}/{e.KeyId:D}/{e.Challenge}"),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        using var enrolled=await Send(c,WorkOrderOfflinePath(workOrder,e.Id,"key"),new{publicKey=Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),proof});
        Assert.Equal(HttpStatusCode.OK,enrolled.StatusCode);return e;
    }
    private static WorkOrderOfflineCapture WorkOrderCaptured(WorkOrderOfflineContextDto e,WorkOrderEvidence? evidence)=>new(Guid.NewGuid(),e.WorkOrderId,e.KeyId,e.DeviceId,
        e.PolicyVersion,e.PolicyDigest,e.SourceVersion,e.Operation,e.BaseVersion,e.ServerTime,180,evidence);
    private async Task<HttpResponseMessage> DeliverWorkOrder(HttpClient c,WorkOrderOfflineContextDto e,WorkOrderOfflineCapture capture,bool wrongProof=false) {
        await Token(c);var body=JsonSerializer.Serialize(capture,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        using var message=new HttpRequestMessage(HttpMethod.Post,"/api/v1/"+WorkOrderOfflinePath(e.WorkOrderId,e.Id,"replay")){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        message.Headers.Add("Idempotency-Key",capture.CommandId.ToString("D"));message.Headers.Add("X-Offline-Hash",digest);
        message.Headers.Add("X-Offline-Signature",Convert.ToBase64String(offlineTestKeys[e.Id].SignData(Encoding.UTF8.GetBytes(wrongProof?new string('0',64):digest),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        return await c.SendAsync(message,Ct);
    }
    private async Task<WorkOrderExecutionDto> InProgressWorkOrder(HttpClient client,IServiceProvider services,Guid engineer) {
        var (_,released)=await ReleasedWorkOrder(client,services,Guid.NewGuid(),["READING"]);
        ExecutionActor(services,engineer,"Human","workorder.execute");await Token(client);
        return await ExecutionOk(await ExecutionTransition(client,released,"START"));
    }

    [Fact]
    [Trait("Requirement","DEV-057")]
    public async Task Work_order_offline_prefetches_context_commits_signed_evidence_once_and_recovers_the_receipt_on_reconnect() {
        var clock=new DailyOfflineClock();var engineer=Guid.NewGuid();await using var host=WorkOrderOfflineHost(clock,engineer);using var c=Client(host);
        var started=await InProgressWorkOrder(c,host.Services,engineer);
        var e=await EnrollWorkOrderOffline(c,started.WorkOrderId);
        Assert.Equal(started.Version,e.BaseVersion);Assert.Equal("IN_PROGRESS",e.State);Assert.Equal(new[]{"READING"},e.RequiredEvidence);
        Assert.Equal(25,e.WorkOrderFields.Count);Assert.Equal(WorkOrderOfflinePolicy.EvidenceParts.Order(),e.AllowedParts.Order());
        var view=await ExecutionView(c,started.WorkOrderId);Assert.Equal("CONFIGURED_UNVERIFIED",view.Capabilities.Offline.State);
        var command=WorkOrderCaptured(e,new([new("VIBRATION","4.2","mm/s")],[],[],"FICTIONAL offline capture"));
        await AssertCode(await DeliverWorkOrder(c,e,command,true),HttpStatusCode.Forbidden,"offline_key_proof_denied");
        using var first=await DeliverWorkOrder(c,e,command);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var bytes=await first.Content.ReadAsStringAsync(Ct);
        var outcome=JsonSerializer.Deserialize<WorkOrderOfflineResult>(bytes,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("Committed",outcome.Result);Assert.Equal(started.Revision+1,outcome.Revision);
        // Lost response: the same signed body returns the same immutable outcome without a second write.
        using(var retry=await DeliverWorkOrder(c,e,command))Assert.Equal(bytes,await retry.Content.ReadAsStringAsync(Ct));
        await AssertCode(await DeliverWorkOrder(c,e,command with{Evidence=command.Evidence! with{WorkDone="different"}}),HttpStatusCode.Conflict,"offline_command_reused");
        await Token(c);
        var sync=new{vesselId=SeedVessel.Id,operations=new[]{new{operationId=command.CommandId,domain="WorkOrderEvidence",resourceId=started.WorkOrderId,
            payloadDigest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command,new JsonSerializerOptions(JsonSerializerDefaults.Web)))))}}};
        using(var reconciled=await Send(c,"sync/operations",sync)) {
            Assert.Equal(HttpStatusCode.OK,reconciled.StatusCode);var result=(await reconciled.Content.ReadFromJsonAsync<SyncOperationsResultDto>(Ct))!;
            Assert.Equal("Recorded",Assert.Single(result.Items).State);Assert.Equal("Committed",result.Items[0].Result);
        }
        var current=await ExecutionView(c,started.WorkOrderId);Assert.Equal("VIBRATION",Assert.Single(current.Current!.Evidence.Readings).Code);
        await using var db=Db();
        Assert.Single(await db.WorkOrderOfflineOutcomes.Where(o=>o.WorkOrderId==started.WorkOrderId).ToArrayAsync(Ct));
        Assert.Equal(1,await db.WorkOrderExecutionRevisions.CountAsync(r=>r.WorkOrderId==started.WorkOrderId && r.Transition=="EVIDENCE",Ct));
        var audit=JsonSerializer.Serialize(await db.AuditEvents.Where(a=>a.ResourceId==started.WorkOrderId).ToArrayAsync(Ct));
        Assert.DoesNotContain("VIBRATION",audit);Assert.DoesNotContain(e.PolicyDigest,audit);Assert.DoesNotContain("PublicKey",audit);

        // Lifecycle commands: renew returns a fresh context; revoke ends capture; lost/cleanup on a second enrollment.
        using(var renewed=await Send(c,WorkOrderOfflinePath(started.WorkOrderId,e.Id,"renew"),new{}))Assert.Equal(HttpStatusCode.OK,renewed.StatusCode);
        using(var revoked=await Send(c,WorkOrderOfflinePath(started.WorkOrderId,e.Id,"revoke"),new{}))Assert.Equal(HttpStatusCode.OK,revoked.StatusCode);
        await AssertCode(await DeliverWorkOrder(c,e,WorkOrderCaptured(e,new([new("X","1")],[],[]))),HttpStatusCode.Forbidden,"offline_policy_denied");
        var second=await EnrollWorkOrderOffline(c,started.WorkOrderId);
        using(var lost=await Send(c,WorkOrderOfflinePath(started.WorkOrderId,second.Id,"lost"),new{}))Assert.Equal(HttpStatusCode.OK,lost.StatusCode);
        using(var cleanup=await Send(c,WorkOrderOfflinePath(started.WorkOrderId,second.Id,"cleanup"),new{}))Assert.Equal(HttpStatusCode.OK,cleanup.StatusCode);
        Assert.Equal(new[]{"CleanupAllowed","Enrolled","KeyEnrolled","KeyLost"},(await db.WorkOrderOfflineLifecycles.Where(l=>l.EnrollmentId==second.Id).Select(l=>l.Action).ToArrayAsync(Ct)).Order());
    }

    [Fact]
    [Trait("Requirement","DEV-057")]
    public async Task Concurrent_online_change_makes_offline_evidence_a_material_conflict_in_the_conflict_center_resolved_only_by_explicit_human_choice() {
        var clock=new DailyOfflineClock();var engineer=Guid.NewGuid();await using var host=WorkOrderOfflineHost(clock,engineer);using var c=Client(host);
        var started=await InProgressWorkOrder(c,host.Services,engineer);
        var e=await EnrollWorkOrderOffline(c,started.WorkOrderId);
        var online=await ExecutionOk(await Send(c,ExecutionPath(started.WorkOrderId,"/evidence"),new WorkOrderExecutionEvidenceCommand(started.WorkOrderId,started.Version,started.Revision,
            new([new("PRESSURE","7.1","bar")],[],[])),update:true));
        var local=WorkOrderCaptured(e,new([new("PRESSURE","6.9","bar")],[],[],"FICTIONAL offline"));
        using var conflictResponse=await DeliverWorkOrder(c,e,local);var conflict=(await conflictResponse.Content.ReadFromJsonAsync<WorkOrderOfflineResult>(Ct))!;
        Assert.Equal("MaterialConflict",conflict.Result);Assert.Equal(online.Version,conflict.ServerVersion);
        var center=(await c.GetFromJsonAsync<SyncConflictPageDto>("/api/v1/sync/conflicts?vesselId="+SeedVessel.Id,Ct))!;
        Assert.Contains(center.Items,i=>i.Source=="workOrderEvidence" && i.ResourceId==started.WorkOrderId && i.CommandId==local.CommandId && i.State=="Open");
        Assert.Contains(center.Sources,s=>s.Source=="workOrderEvidence" && s.State=="Included");
        var detail=(await c.GetFromJsonAsync<WorkOrderOfflineConflictDto>("/api/v1/"+WorkOrderOfflinePath(started.WorkOrderId,e.Id,"conflicts/"+local.CommandId),Ct))!;
        Assert.Equal("6.9",detail.Captured.Readings[0].Value);Assert.Equal("7.1",detail.Server.Readings[0].Value);Assert.Empty(detail.Base.Readings);
        Assert.Equal(new[]{"keepServer","useCaptured"},detail.Choices);
        var child=WorkOrderCaptured(e,null) with{BaseVersion=detail.CurrentVersion,ParentCommandId=local.CommandId,ExpectedServerVersion=detail.CurrentVersion,Choice="useCaptured",ReasonCode="INPUT_CORRECTED"};
        host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorKind="AI";
        await AssertCode(await DeliverWorkOrder(c,e,child),HttpStatusCode.Forbidden,"human_authority_required");
        host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorKind="Human";
        using var resolved=await DeliverWorkOrder(c,e,child);Assert.Equal(HttpStatusCode.OK,resolved.StatusCode);
        var result=(await resolved.Content.ReadFromJsonAsync<WorkOrderOfflineResult>(Ct))!;Assert.Equal("Committed",result.Result);Assert.Equal(local.CommandId,result.ParentCommandId);
        Assert.Equal("6.9",(await ExecutionView(c,started.WorkOrderId)).Current!.Evidence.Readings[0].Value);
        await AssertCode(await DeliverWorkOrder(c,e,child with{CommandId=Guid.NewGuid()}),HttpStatusCode.Conflict,"offline_conflict_changed");
        center=(await c.GetFromJsonAsync<SyncConflictPageDto>("/api/v1/sync/conflicts?vesselId="+SeedVessel.Id,Ct))!;
        Assert.Contains(center.Items,i=>i.CommandId==local.CommandId && i.State=="Resolved");
    }

    [Fact]
    [Trait("Requirement","DEV-057")]
    public async Task Missing_or_incomplete_offline_classification_never_permits_work_order_enrollment() {
        var clock=new DailyOfflineClock();var engineer=Guid.NewGuid();await using var host=WorkOrderOfflineHost(clock,engineer);using var c=Client(host);
        var started=await InProgressWorkOrder(c,host.Services,engineer);
        var policy=host.Services.GetRequiredService<WorkOrderOfflineSettings>().Policy!;var fields=policy.Fields;
        policy.Fields=[..fields.Take(4)];
        await Token(c);await AssertCode(await Send(c,WorkOrderOfflinePath(started.WorkOrderId),new{}),HttpStatusCode.Forbidden,"offline_policy_denied");
        policy.Fields=[..fields.Take(4),fields[4] with{PrivacyClass="NonPersonalOperationalDraft"}];
        await AssertCode(await Send(c,WorkOrderOfflinePath(started.WorkOrderId),new{}),HttpStatusCode.Forbidden,"offline_policy_denied");
        policy.Fields=fields;policy.RetentionPolicyReference="";
        await AssertCode(await Send(c,WorkOrderOfflinePath(started.WorkOrderId),new{}),HttpStatusCode.Forbidden,"offline_policy_denied");
        await using var db=Db();Assert.False(await db.WorkOrderOfflineEnrollments.AnyAsync(x=>x.WorkOrderId==started.WorkOrderId,Ct));
    }
}
