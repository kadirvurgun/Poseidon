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

// S10 WP-C DEV-066 (18.01 sheet:Backlog/row:67) pilot offline journey flow 1: certificate renewal draft offline.
// Enrollment prefetch, durable signed draft replay exactly once, duplicate reconnect, material conflict ->
// conflict center -> explicit human resolution, and closed policy denial. FICTIONAL classification
// references are NOT approved policy. Authored; PostgreSQL execution DEFERRED to CI (no local Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private CertificateRenewalOfflineSettings SyntheticRenewalOffline(DailyOfflineClock clock)=>new(){Policy=new CertificateRenewalOfflinePolicy {
        Version="fictional-renewal-offline-v1",SourceVersion=VesselCertificatePolicy.SourceVersion,StoragePolicyReference="FICTIONAL encrypted IndexedDB test only",
        KeyPolicyReference="FICTIONAL nonextractable key test only",RetentionPolicyReference="FICTIONAL isolated fixture retention; no approval",RetentionSeconds=7200,LeaseSeconds=600,
        NotBefore=clock.Current.AddDays(-1),ExpiresAt=clock.Current.AddDays(1),MaxEnrollments=8,MaxRecords=32,MaxEncryptedBytes=262144,MaxDeviceRecords=64,MaxDeviceEncryptedBytes=4194304,
        Fields=CertificateRenewalOfflinePolicy.Parts.Select(k=>new CertificateRenewalOfflineField(k,"FICTIONAL classification; not source approval","Fictional regression only","RestrictedOperationalDraft")).ToArray(),
        Scopes=[new(SeedVessel.TenantId,actorId,SeedVessel.Id)]}};
    private WebApplicationFactory<Program> RenewalOfflineHost(DailyOfflineClock clock)=>Host().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>{
        s.RemoveAll<CertificateRenewalOfflineSettings>();s.AddSingleton(SyntheticRenewalOffline(clock));s.RemoveAll<TimeProvider>();s.AddSingleton<TimeProvider>(clock);}));
    private string RenewalOfflinePath(Guid renewal,Guid? enrollment=null,string action="")=>
        $"certificate-renewal-offline/{renewal:D}"+(enrollment==null?"":$"/{enrollment:D}")+(action.Length==0?"":"/"+action)+$"?vesselId={SeedVessel.Id:D}";
    private async Task<CertificateRenewalOfflineContextDto> EnrollRenewalOffline(HttpClient c,Guid renewal) {
        await Token(c);using var response=await Send(c,RenewalOfflinePath(renewal),new{});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var e=(await response.Content.ReadFromJsonAsync<CertificateRenewalOfflineContextDto>(Ct))!;
        var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);offlineTestKeys.Add(e.Id,key);
        var proof=Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes($"certificate-renewal/{renewal:D}/{e.Id:D}/{e.KeyId:D}/{e.Challenge}"),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        using var enrolled=await Send(c,RenewalOfflinePath(renewal,e.Id,"key"),new{publicKey=Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),proof});
        Assert.Equal(HttpStatusCode.OK,enrolled.StatusCode);return e;
    }
    private static CertificateRenewalOfflineCapture RenewalCaptured(CertificateRenewalOfflineContextDto e,CertificateRenewalContent? content)=>new(Guid.NewGuid(),e.RenewalId,e.KeyId,e.DeviceId,
        e.PolicyVersion,e.PolicyDigest,e.SourceVersion,e.Operation,e.BaseVersion,e.ServerTime,180,content);
    private async Task<HttpResponseMessage> DeliverRenewal(HttpClient c,CertificateRenewalOfflineContextDto e,CertificateRenewalOfflineCapture capture,bool wrongProof=false) {
        await Token(c);var body=JsonSerializer.Serialize(capture,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        using var message=new HttpRequestMessage(HttpMethod.Post,"/api/v1/"+RenewalOfflinePath(e.RenewalId,e.Id,"replay")){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        message.Headers.Add("Idempotency-Key",capture.CommandId.ToString("D"));message.Headers.Add("X-Offline-Hash",digest);
        message.Headers.Add("X-Offline-Signature",Convert.ToBase64String(offlineTestKeys[e.Id].SignData(Encoding.UTF8.GetBytes(wrongProof?new string('0',64):digest),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        return await c.SendAsync(message,Ct);
    }
    // One FICTIONAL DRAFT renewal case read back through the ordinary current-revision path.
    private async Task<CertificateRenewalDto> DraftRenewal(HttpClient client,IServiceProvider services) {
        RenewalPermissions(services);await Token(client);var original=await CertificateForRenewal(client);
        var command=new CertificateRenewalCommand(Guid.NewGuid(),0,original.Id,original.Revision,original.Version,new(PlannedLocation:"FICTIONAL base plan"),"INITIAL_AUTHORING");
        using(var created=await Send(client,RenewalPath(),command))Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        return (await client.GetFromJsonAsync<CertificateRenewalDto>("/api/v1/"+RenewalPath(command.Id),Ct))!;
    }

    [Fact]
    [Trait("Requirement","DEV-066")]
    public async Task Renewal_offline_draft_prefetches_context_commits_signed_draft_once_and_replays_the_same_receipt_on_reconnect() {
        var clock=new DailyOfflineClock();await using var host=RenewalOfflineHost(clock);using var c=Client(host);
        var renewal=await DraftRenewal(c,host.Services);
        var e=await EnrollRenewalOffline(c,renewal.Id);
        Assert.Equal(renewal.Version,e.BaseVersion);Assert.Equal(renewal.Revision,e.BaseRevision);Assert.Equal("DRAFT",e.State);Assert.Equal(renewal.CertificateId,e.CertificateId);
        Assert.Equal("FICTIONAL base plan",e.BaseContent.PlannedLocation);Assert.Equal(CertificateRenewalOfflinePolicy.Parts.Order(),e.AllowedParts.Order());
        var command=RenewalCaptured(e,e.BaseContent with{PlannedLocation="FICTIONAL offline plan",AuthorityReference="FICTIONAL-REF"});
        await AssertCode(await DeliverRenewal(c,e,command,true),HttpStatusCode.Forbidden,"offline_key_proof_denied");
        using var first=await DeliverRenewal(c,e,command);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var bytes=await first.Content.ReadAsStringAsync(Ct);
        var outcome=JsonSerializer.Deserialize<CertificateRenewalOfflineResult>(bytes,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("Committed",outcome.Result);Assert.Equal(renewal.Revision+1,outcome.Revision);Assert.Null(outcome.ParentCommandId);
        // Lost response / duplicate send: the same signed body returns the same immutable outcome without a second revision.
        using(var retry=await DeliverRenewal(c,e,command))Assert.Equal(bytes,await retry.Content.ReadAsStringAsync(Ct));
        await AssertCode(await DeliverRenewal(c,e,command with{Content=command.Content! with{PlannedLocation="different"}}),HttpStatusCode.Conflict,"offline_command_reused");
        var current=(await c.GetFromJsonAsync<CertificateRenewalDto>("/api/v1/"+RenewalPath(renewal.Id),Ct))!;
        Assert.Equal("FICTIONAL offline plan",current.Content.PlannedLocation);Assert.Equal("DRAFT",current.State);Assert.Equal("REVIEW_REQUIRED",current.Qualification);Assert.False(current.CanComplete);
        await using var db=Db();
        Assert.Single(await db.CertificateRenewalOfflineOutcomes.Where(o=>o.RenewalId==renewal.Id).ToArrayAsync(Ct));
        Assert.Equal(2,await db.CertificateRenewalRevisions.CountAsync(r=>r.RenewalId==renewal.Id,Ct));
        var audit=JsonSerializer.Serialize(await db.AuditEvents.Where(a=>a.ResourceId==renewal.Id).ToArrayAsync(Ct));
        Assert.DoesNotContain("FICTIONAL offline plan",audit);Assert.DoesNotContain(e.PolicyDigest,audit);Assert.DoesNotContain("PublicKey",audit);
        // Linked replacement references are online only.
        await AssertCode(await DeliverRenewal(c,e,RenewalCaptured(e,e.BaseContent with{NewCertificateId=Guid.NewGuid(),NewCertificateRevision=1,NewCertificateVersion=1})),
            HttpStatusCode.BadRequest,"invalid_offline_command");

        // Lifecycle commands: renew returns a fresh context; revoke ends capture; lost/cleanup on a second enrollment.
        using(var renewed=await Send(c,RenewalOfflinePath(renewal.Id,e.Id,"renew"),new{}))Assert.Equal(HttpStatusCode.OK,renewed.StatusCode);
        using(var revoked=await Send(c,RenewalOfflinePath(renewal.Id,e.Id,"revoke"),new{}))Assert.Equal(HttpStatusCode.OK,revoked.StatusCode);
        await AssertCode(await DeliverRenewal(c,e,RenewalCaptured(e,e.BaseContent)),HttpStatusCode.Forbidden,"offline_policy_denied");
        var second=await EnrollRenewalOffline(c,renewal.Id);
        using(var lost=await Send(c,RenewalOfflinePath(renewal.Id,second.Id,"lost"),new{}))Assert.Equal(HttpStatusCode.OK,lost.StatusCode);
        using(var cleanup=await Send(c,RenewalOfflinePath(renewal.Id,second.Id,"cleanup"),new{}))Assert.Equal(HttpStatusCode.OK,cleanup.StatusCode);
        Assert.Equal(new[]{"CleanupAllowed","Enrolled","KeyEnrolled","KeyLost"},(await db.CertificateRenewalOfflineLifecycles.Where(l=>l.EnrollmentId==second.Id).Select(l=>l.Action).ToArrayAsync(Ct)).Order());
    }

    [Fact]
    [Trait("Requirement","DEV-066")]
    public async Task Concurrent_online_renewal_edit_makes_the_offline_draft_a_material_conflict_resolved_only_by_explicit_human_choice() {
        var clock=new DailyOfflineClock();await using var host=RenewalOfflineHost(clock);using var c=Client(host);
        var renewal=await DraftRenewal(c,host.Services);
        var e=await EnrollRenewalOffline(c,renewal.Id);
        var edit=new CertificateRenewalCommand(renewal.Id,renewal.Version,renewal.CertificateId,renewal.CertificateRevision,renewal.CertificateVersion,
            renewal.Content with{PlannedLocation="FICTIONAL online plan"},"AUTHORING_CORRECTION");
        using(var updated=await Send(c,RenewalPath(renewal.Id),edit,update:true))Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        var local=RenewalCaptured(e,e.BaseContent with{PlannedLocation="FICTIONAL offline plan"});
        using var conflictResponse=await DeliverRenewal(c,e,local);Assert.Equal(HttpStatusCode.OK,conflictResponse.StatusCode);
        var conflict=(await conflictResponse.Content.ReadFromJsonAsync<CertificateRenewalOfflineResult>(Ct))!;
        Assert.Equal("MaterialConflict",conflict.Result);Assert.NotEqual(e.BaseVersion,conflict.ServerVersion);
        Assert.Equal("FICTIONAL online plan",(await c.GetFromJsonAsync<CertificateRenewalDto>("/api/v1/"+RenewalPath(renewal.Id),Ct))!.Content.PlannedLocation);
        var center=(await c.GetFromJsonAsync<SyncConflictPageDto>("/api/v1/sync/conflicts?vesselId="+SeedVessel.Id,Ct))!;
        Assert.Contains(center.Items,i=>i.Source=="certificateRenewal" && i.ResourceId==renewal.Id && i.CommandId==local.CommandId && i.State=="Open");
        Assert.Contains(center.Sources,s=>s.Source=="certificateRenewal" && s.State=="Included");
        var detail=(await c.GetFromJsonAsync<CertificateRenewalOfflineConflictDto>("/api/v1/"+RenewalOfflinePath(renewal.Id,e.Id,"conflicts/"+local.CommandId),Ct))!;
        Assert.Equal("FICTIONAL offline plan",detail.Captured.PlannedLocation);Assert.Equal("FICTIONAL online plan",detail.Server.PlannedLocation);
        Assert.Equal("FICTIONAL base plan",detail.Base.PlannedLocation);Assert.Equal(new[]{"keepServer","useCaptured"},detail.Choices);
        var child=RenewalCaptured(e,null) with{BaseVersion=detail.CurrentVersion,ParentCommandId=local.CommandId,ExpectedServerVersion=detail.CurrentVersion,Choice="useCaptured",ReasonCode="INPUT_CORRECTED"};
        host.Services.GetRequiredService<FederationSettings>().Memberships[0]=host.Services.GetRequiredService<FederationSettings>().Memberships[0] with{ActorKind="AI"};
        await AssertCode(await DeliverRenewal(c,e,child),HttpStatusCode.Forbidden,"human_authority_required");
        host.Services.GetRequiredService<FederationSettings>().Memberships[0]=host.Services.GetRequiredService<FederationSettings>().Memberships[0] with{ActorKind="Human"};
        using var resolved=await DeliverRenewal(c,e,child);Assert.Equal(HttpStatusCode.OK,resolved.StatusCode);
        var result=(await resolved.Content.ReadFromJsonAsync<CertificateRenewalOfflineResult>(Ct))!;Assert.Equal("Committed",result.Result);Assert.Equal(local.CommandId,result.ParentCommandId);
        Assert.Equal("FICTIONAL offline plan",(await c.GetFromJsonAsync<CertificateRenewalDto>("/api/v1/"+RenewalPath(renewal.Id),Ct))!.Content.PlannedLocation);
        await AssertCode(await DeliverRenewal(c,e,child with{CommandId=Guid.NewGuid()}),HttpStatusCode.Conflict,"offline_conflict_changed");
        center=(await c.GetFromJsonAsync<SyncConflictPageDto>("/api/v1/sync/conflicts?vesselId="+SeedVessel.Id,Ct))!;
        Assert.Contains(center.Items,i=>i.CommandId==local.CommandId && i.State=="Resolved");
    }

    [Fact]
    [Trait("Requirement","DEV-066")]
    public async Task Missing_or_incomplete_offline_classification_never_permits_renewal_offline_enrollment() {
        var clock=new DailyOfflineClock();await using var host=RenewalOfflineHost(clock);using var c=Client(host);
        var renewal=await DraftRenewal(c,host.Services);
        var policy=host.Services.GetRequiredService<CertificateRenewalOfflineSettings>().Policy!;var fields=policy.Fields;
        policy.Fields=[..fields.Take(4)];
        await Token(c);await AssertCode(await Send(c,RenewalOfflinePath(renewal.Id),new{}),HttpStatusCode.Forbidden,"offline_policy_denied");
        policy.Fields=[..fields.Take(4),fields[4] with{PrivacyClass="NonPersonalOperationalDraft"}];
        await AssertCode(await Send(c,RenewalOfflinePath(renewal.Id),new{}),HttpStatusCode.Forbidden,"offline_policy_denied");
        policy.Fields=fields;policy.RetentionPolicyReference="";
        await AssertCode(await Send(c,RenewalOfflinePath(renewal.Id),new{}),HttpStatusCode.Forbidden,"offline_policy_denied");
        await using var db=Db();Assert.False(await db.CertificateRenewalOfflineEnrollments.AnyAsync(x=>x.RenewalId==renewal.Id,Ct));
    }
}
