using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;
namespace Poseidon.Api.Tests;

// DEV097..099 / T-OPS2-001..003 + DEV033..035. Owned real PG/HTTP sources.
// Fictional test classification references are NOT approved production policies.
// AUTHORED ONLY: no tests, migration, build or generation executed.
public sealed partial class DirectoryWorkspaceTests
{
    private sealed class DailyOfflineClock : TimeProvider {
        public DateTimeOffset Current {get;set;}=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Current;
    }
    private DailyOfflineSettings SyntheticDailyOffline(DailyOfflineClock clock)=>new(){Policies=DailyOperationsPolicy.Kinds.Select(kind=>new DailyOfflinePolicy {
        Kind=kind,Version="fictional-daily-v1",SourceVersion=DailyOperationsPolicy.SourceVersion,ResourcePolicy="SelectedExistingOrNewDraft",
        StoragePolicyReference="FICTIONAL encrypted IndexedDB test only",KeyPolicyReference="FICTIONAL nonextractable key test only",
        RetentionPolicyReference="FICTIONAL isolated fixture retention; no approval",RetentionSeconds=7200,LeaseSeconds=600,
        NotBefore=clock.Current.AddDays(-1),ExpiresAt=clock.Current.AddDays(1),MaxEnrollments=8,MaxRecords=32,MaxEncryptedBytes=1048576,
        MaxDeviceRecords=64,MaxDeviceEncryptedBytes=4194304,
        Fields=[new(kind=="Noon"?"position_lat":kind=="Deck"?"handover_note":"alarms","FICTIONAL classification; not source approval","Fictional authoring regression only","NonPersonalOperationalDraft")],
        Scopes=[new(SeedVessel.TenantId,actorId,SeedVessel.Id,kind)]
    }).ToArray()};
    private WebApplicationFactory<Program> DailyOfflineHost(DailyOfflineClock clock,DbCommandInterceptor? interceptor=null)=>Host(interceptor:interceptor)
        .WithWebHostBuilder(b=>b.ConfigureTestServices(s=>{
            s.RemoveAll<DailyOfflineSettings>();s.AddSingleton(SyntheticDailyOffline(clock));s.RemoveAll<TimeProvider>();s.AddSingleton<TimeProvider>(clock);
        }));
    private string DailyOfflinePath(string kind,Guid record,Guid? enrollment=null,string action="",Guid? vessel=null)=>
        $"daily-offline/{kind}/{record:D}"+(enrollment==null?"":$"/{enrollment:D}")+(action.Length==0?"":"/"+action)+$"?vesselId={vessel??SeedVessel.Id:D}";
    private async Task<DailyOfflineContextDto> EnrollDailyOffline(HttpClient c,string kind,Guid record) {
        using var response=await Send(c,DailyOfflinePath(kind,record),new{});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var e=(await response.Content.ReadFromJsonAsync<DailyOfflineContextDto>(Ct))!;
        var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);offlineTestKeys.Add(e.Id,key);
        var proof=Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes($"daily/{kind}/{record:D}/{e.Id:D}/{e.KeyId:D}/{e.Challenge}"),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        using var enrolled=await Send(c,DailyOfflinePath(kind,record,e.Id,"key"),new{publicKey=Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),proof});
        Assert.Equal(HttpStatusCode.OK,enrolled.StatusCode);return e;
    }
    private static DailyOfflineCapture DailyCaptured(DailyOfflineContextDto e,DailyValue? value=null)=>new(Guid.NewGuid(),e.RecordId,e.Kind,e.KeyId,e.DeviceId,
        e.PolicyVersion,e.PolicyDigest,e.SourceVersion,e.SchemaHash,e.Operation,e.BaseVersion,e.ServerTime,180,
        e.AllowedFields.ToDictionary(k=>k,_=>value??new DailyValue("Unknown",null)));
    private async Task<HttpResponseMessage> DeliverDaily(HttpClient c,DailyOfflineContextDto e,DailyOfflineCapture capture,bool wrongProof=false) {
        var body=JsonSerializer.Serialize(capture,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        using var message=new HttpRequestMessage(HttpMethod.Post,"/api/v1/"+DailyOfflinePath(e.Kind,e.RecordId,e.Id,"replay")){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        message.Headers.Add("Idempotency-Key",capture.CommandId.ToString("D"));message.Headers.Add("X-Offline-Hash",digest);
        message.Headers.Add("X-Offline-Signature",Convert.ToBase64String(offlineTestKeys[e.Id].SignData(Encoding.UTF8.GetBytes(wrongProof?new string('0',64):digest),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        return await c.SendAsync(message,Ct);
    }
    [Theory]
    [InlineData("Noon")] [InlineData("Deck")] [InlineData("Engine")]
    public async Task Daily_offline_creates_a_dedicated_draft_once_and_recovers_exact_signed_outcome_with_audited_actual_version(string kind) {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var id=Guid.NewGuid();var e=await EnrollDailyOffline(c,kind,id);Assert.Equal(0u,e.BaseVersion);Assert.Equal(0,e.BaseRevision);
        var command=DailyCaptured(e);using var first=await DeliverDaily(c,e,command);Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var bytes=await first.Content.ReadAsStringAsync(Ct);var outcome=(await first.Content.ReadFromJsonAsync<DailyOfflineResult>(Ct))!;
        Assert.Equal("Committed",outcome.Result);Assert.True(outcome.ServerVersion>0);Assert.Equal(1,outcome.Revision);
        using var retry=await DeliverDaily(c,e,command);Assert.Equal(HttpStatusCode.OK,retry.StatusCode);Assert.Equal(bytes,await retry.Content.ReadAsStringAsync(Ct));
        await using var db=Db();var row=await db.DailyOperations.SingleAsync(r=>r.Id==id,Ct);Assert.Equal(kind,row.Kind);Assert.Equal(outcome.ServerVersion,row.Version);
        var fields=JsonSerializer.Deserialize<Dictionary<string,DailyValue>>(row.FieldsJson)!;Assert.Equal("Unknown",fields[e.AllowedFields[0]].State);Assert.Null(fields[e.AllowedFields[0]].Value);
        Assert.Single(await db.DailyOperationRevisions.Where(r=>r.RecordId==id).ToArrayAsync(Ct));Assert.Single(await db.DailyOfflineOutcomes.Where(o=>o.RecordId==id).ToArrayAsync(Ct));
        var audit=await db.AuditEvents.Where(a=>a.ResourceId==id).ToArrayAsync(Ct);Assert.NotEmpty(audit);
        Assert.Contains(audit,a=>a.EntityType==nameof(DailyOfflineOutcome) && a.ActorId==actorId.ToString("D") && a.VesselId==e.VesselId && a.RoleContext==ReviewRole.ToString("D"));
        Assert.DoesNotContain("PublicKey",JsonSerializer.Serialize(audit));Assert.DoesNotContain(e.PolicyDigest,JsonSerializer.Serialize(audit));
    }
    [Fact]
    public async Task Daily_offline_subset_preserves_unclassified_ordinary_fields_and_never_exports_them_in_enrollment_or_conflict() {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var id=Guid.NewGuid();var ordinary=new DailyDraftCommand(id,0,new(){["position_lat"]=new("Known","0"),["weather_summary"]=new("Known","FICTIONAL outside offline classification")},"INITIAL_AUTHORING");
        Assert.Equal(HttpStatusCode.OK,(await DailySend(c,"noon-reports",ordinary,Guid.NewGuid().ToString("D"))).StatusCode);
        var e=await EnrollDailyOffline(c,"Noon",id);Assert.Equal(new[]{"position_lat"},e.AllowedFields);Assert.Equal(new[]{"position_lat"},e.BaseFields.Keys);
        Assert.DoesNotContain("FICTIONAL outside",JsonSerializer.Serialize(e));
        var captured=DailyCaptured(e,new("Known","-0.000"));Assert.Equal(HttpStatusCode.OK,(await DeliverDaily(c,e,captured)).StatusCode);
        await using var db=Db();var row=await db.DailyOperations.SingleAsync(r=>r.Id==id,Ct);var fields=JsonSerializer.Deserialize<Dictionary<string,DailyValue>>(row.FieldsJson)!;
        Assert.Equal("-0.000",fields["position_lat"].Value);Assert.Equal(ordinary.Fields["weather_summary"],fields["weather_summary"]);
        var retained=await db.DailyOfflineEnrollments.SingleAsync(r=>r.Id==e.Id,Ct);Assert.DoesNotContain("FICTIONAL outside",retained.ProtectedBase);
    }
    [Fact]
    public async Task Daily_offline_whole_head_change_in_an_unclassified_field_creates_immutable_conflict_then_explicit_parent_resolution() {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var id=Guid.NewGuid();var draft=new DailyDraftCommand(id,0,new(){["position_lat"]=new("Known","1"),["weather_summary"]=new("Known","FICTIONAL before")},"INITIAL_AUTHORING");
        var first=await DailySend(c,"noon-reports",draft,Guid.NewGuid().ToString("D"));var saved=(await first.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var e=await EnrollDailyOffline(c,"Noon",id);var local=DailyCaptured(e,new("Known","2"));
        var update=draft with{ExpectedVersion=saved.Version,ReasonCode="AUTHORING_CORRECTION",Fields=new(){["position_lat"]=new("Known","1"),["weather_summary"]=new("Known","FICTIONAL current outside classification")}};
        Assert.Equal(HttpStatusCode.OK,(await DailySend(c,"noon-reports",update,Guid.NewGuid().ToString("D"),true)).StatusCode);
        using var conflictResponse=await DeliverDaily(c,e,local);var conflict=(await conflictResponse.Content.ReadFromJsonAsync<DailyOfflineResult>(Ct))!;Assert.Equal("MaterialConflict",conflict.Result);
        var bytes=await conflictResponse.Content.ReadAsStringAsync(Ct);using var retry=await DeliverDaily(c,e,local);Assert.Equal(bytes,await retry.Content.ReadAsStringAsync(Ct));
        var detail=(await c.GetFromJsonAsync<DailyOfflineConflictDto>("/api/v1/"+DailyOfflinePath("Noon",id,e.Id,"conflicts/"+local.CommandId),Ct))!;
        var field=Assert.Single(detail.Fields);Assert.Equal("1",field.Base.Value);Assert.Equal("2",field.Captured.Value);Assert.Equal("1",field.Server.Value);
        Assert.DoesNotContain("weather_summary",JsonSerializer.Serialize(detail));Assert.Equal(new[]{"keepServer","useCaptured"},detail.Choices);
        var child=DailyCaptured(e) with{BaseVersion=detail.CurrentVersion,Fields=[],ParentCommandId=local.CommandId,ExpectedServerVersion=detail.CurrentVersion,Choice="useCaptured",ReasonCode="INPUT_CORRECTED"};
        using var resolved=await DeliverDaily(c,e,child);Assert.Equal(HttpStatusCode.OK,resolved.StatusCode);var result=(await resolved.Content.ReadFromJsonAsync<DailyOfflineResult>(Ct))!;
        Assert.Equal("Committed",result.Result);Assert.Equal(local.CommandId,result.ParentCommandId);
        await using var db=Db();var current=await db.DailyOperations.SingleAsync(r=>r.Id==id,Ct);var values=JsonSerializer.Deserialize<Dictionary<string,DailyValue>>(current.FieldsJson)!;
        Assert.Equal("2",values["position_lat"].Value);Assert.Equal(update.Fields["weather_summary"],values["weather_summary"]);
        Assert.Equal(2,await db.DailyOfflineOutcomes.CountAsync(o=>o.RecordId==id,Ct));Assert.Equal("MaterialConflict",(await db.DailyOfflineOutcomes.SingleAsync(o=>o.CommandId==local.CommandId,Ct)).Result);
        Assert.Equal(HttpStatusCode.Conflict,(await DeliverDaily(c,e,child with{CommandId=Guid.NewGuid()})).StatusCode);
    }
    [Fact]
    public async Task Daily_policy_missing_classification_reference_or_crew_only_grants_cannot_enroll_or_recover_daily_data() {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);await Token(c);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(c,DailyOfflinePath("Noon",Guid.NewGuid()),new{})).StatusCode);
        DailyPermissions(host.Services);var policy=host.Services.GetRequiredService<DailyOfflineSettings>().Policies.Single(p=>p.Kind=="Noon");
        policy.Fields=[policy.Fields[0] with{ClassificationReference=""}];
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(c,DailyOfflinePath("Noon",Guid.NewGuid()),new{})).StatusCode);
        policy.Fields=[new("master_review","FICTIONAL","FICTIONAL","NonPersonalOperationalDraft")];
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(c,DailyOfflinePath("Noon",Guid.NewGuid()),new{})).StatusCode);
        await using var db=Db();Assert.Empty(await db.DailyOfflineEnrollments.ToArrayAsync(Ct));Assert.Empty(await db.DailyOfflineOutcomes.ToArrayAsync(Ct));Assert.Empty(await db.DailyOfflineLifecycles.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Daily_current_role_policy_kind_device_and_proof_precede_immutable_outcome_recovery() {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var e=await EnrollDailyOffline(c,"Noon",Guid.NewGuid());var command=DailyCaptured(e);Assert.Equal(HttpStatusCode.OK,(await DeliverDaily(c,e,command)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await DeliverDaily(c,e,command,true)).StatusCode);
        foreach(var changed in new[]{command with{DeviceId=Guid.NewGuid()},command with{KeyId=Guid.NewGuid()},command with{Kind="Deck"},command with{SchemaHash=new string('0',64)}})
            Assert.Equal(HttpStatusCode.BadRequest,(await DeliverDaily(c,e,changed)).StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!="daily.noon.write").ToArray()};
        Assert.Equal(HttpStatusCode.Forbidden,(await DeliverDaily(c,e,command)).StatusCode);
        settings.Memberships[0]=member;
        var policies=host.Services.GetRequiredService<DailyOfflineSettings>();policies.Policies.Single(p=>p.Kind=="Noon").KeyPolicyReference="FICTIONAL changed key context";
        Assert.Equal(HttpStatusCode.Forbidden,(await DeliverDaily(c,e,command)).StatusCode);
        await using var db=Db();Assert.Single(await db.DailyOfflineOutcomes.ToArrayAsync(Ct));Assert.Single(await db.DailyOperationRevisions.Where(r=>r.RecordId==e.RecordId).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Daily_foreign_tenant_vessel_or_resource_kind_cannot_read_inventory_base_conflict_or_cleanup() {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var e=await EnrollDailyOffline(c,"Noon",Guid.NewGuid());Assert.Equal(HttpStatusCode.OK,(await DeliverDaily(c,e,DailyCaptured(e))).StatusCode);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        foreach(var path in new[]{DailyOfflinePath("Noon",e.RecordId,e.Id,"cleanup",foreign.Id),DailyOfflinePath("Deck",e.RecordId,e.Id,"renew"),DailyOfflinePath("Noon",Guid.NewGuid(),e.Id,"renew")}) {
            var result=await Send(c,path,new{});Assert.Equal(HttpStatusCode.NotFound,result.StatusCode);Assert.DoesNotContain(e.PolicyDigest,await result.Content.ReadAsStringAsync(Ct));
        }
        await using var db=Db();Assert.Single(await db.DailyOfflineEnrollments.ToArrayAsync(Ct));Assert.Null((await db.DailyOfflineEnrollments.SingleAsync(Ct)).RevokedAt);
    }
    [Fact]
    public async Task Daily_renew_preserves_original_base_and_key_identity_revoke_denies_replay_but_authorized_cleanup_remains() {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var e=await EnrollDailyOffline(c,"Noon",Guid.NewGuid());clock.Current=clock.Current.AddSeconds(601);
        var command=DailyCaptured(e);Assert.Equal(HttpStatusCode.Conflict,(await DeliverDaily(c,e,command)).StatusCode);
        using var renew=await Send(c,DailyOfflinePath("Noon",e.RecordId,e.Id,"renew"),new{});Assert.Equal(HttpStatusCode.OK,renew.StatusCode);
        var renewed=(await renew.Content.ReadFromJsonAsync<DailyOfflineContextDto>(Ct))!;Assert.Equal(e.KeyId,renewed.KeyId);Assert.Equal(e.BaseFields,renewed.BaseFields);Assert.Equal(e.BaseVersion,renewed.BaseVersion);
        Assert.Equal(HttpStatusCode.OK,(await DeliverDaily(c,renewed,command)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Send(c,DailyOfflinePath("Noon",e.RecordId,e.Id,"lost"),new{})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await DeliverDaily(c,renewed,command)).StatusCode);
        host.Services.GetRequiredService<DailyOfflineSettings>().Policies=[];
        using var cleanup=await Send(c,DailyOfflinePath("Noon",e.RecordId,e.Id,"cleanup"),new{});Assert.Equal(HttpStatusCode.OK,cleanup.StatusCode);
        Assert.DoesNotContain("baseFields",await cleanup.Content.ReadAsStringAsync(Ct));
        await using var db=Db();var outcome=await db.DailyOfflineOutcomes.SingleAsync(Ct);outcome.Result="CommittedForged";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    private sealed class DailyOfflineOutcomeFault : DbCommandInterceptor {
        public bool Armed {get;set;}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default) {
            if(Armed && command.CommandText.Contains("INSERT INTO offline.\"DailyOutcomes\"",StringComparison.Ordinal))throw new Npgsql.NpgsqlException("Fictional daily outcome fixture fault");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Daily_offline_outcome_failure_rolls_back_ordinary_apply_history_and_audit_then_exact_retry_commits_once() {
        var clock=new DailyOfflineClock();var fault=new DailyOfflineOutcomeFault();await using var host=DailyOfflineHost(clock,fault);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var e=await EnrollDailyOffline(c,"Noon",Guid.NewGuid());var command=DailyCaptured(e);long auditCount;await using(var before=Db())auditCount=await before.AuditEvents.LongCountAsync(Ct);
        fault.Armed=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await DeliverDaily(c,e,command)).StatusCode);
        await using(var check=Db()) {Assert.False(await check.DailyOperations.AnyAsync(r=>r.Id==e.RecordId,Ct));Assert.Empty(await check.DailyOperationRevisions.Where(r=>r.RecordId==e.RecordId).ToArrayAsync(Ct));Assert.Empty(await check.DailyOfflineOutcomes.ToArrayAsync(Ct));Assert.Equal(auditCount,await check.AuditEvents.LongCountAsync(Ct));}
        fault.Armed=false;Assert.Equal(HttpStatusCode.OK,(await DeliverDaily(c,e,command)).StatusCode);
        await using var verify=Db();Assert.Single(await verify.DailyOperationRevisions.Where(r=>r.RecordId==e.RecordId).ToArrayAsync(Ct));Assert.Single(await verify.DailyOfflineOutcomes.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Daily_duplicate_concurrent_signed_commands_have_one_effect_and_retry_recovers_original_response_bytes() {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var e=await EnrollDailyOffline(c,"Deck",Guid.NewGuid());var command=DailyCaptured(e,new("Known","FICTIONAL handover"));
        var deliveries=await Task.WhenAll(DeliverDaily(c,e,command),DeliverDaily(c,e,command));Assert.Contains(deliveries,r=>r.StatusCode==HttpStatusCode.OK);
        foreach(var delivery in deliveries){Assert.True(delivery.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);delivery.Dispose();}
        using var a=await DeliverDaily(c,e,command);using var b=await DeliverDaily(c,e,command);
        Assert.Equal(HttpStatusCode.OK,a.StatusCode);Assert.Equal(HttpStatusCode.OK,b.StatusCode);Assert.Equal(await a.Content.ReadAsStringAsync(Ct),await b.Content.ReadAsStringAsync(Ct));
        await using var db=Db();Assert.Single(await db.DailyOfflineOutcomes.Where(o=>o.RecordId==e.RecordId).ToArrayAsync(Ct));Assert.Single(await db.DailyOperationRevisions.Where(r=>r.RecordId==e.RecordId).ToArrayAsync(Ct));
    }
    private sealed class DailyOfflineApplyBarrier : DbCommandInterceptor {
        private readonly TaskCompletionSource reached=new(TaskCreationOptions.RunContinuationsAsynchronously),release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        public void Arm()=>Interlocked.Exchange(ref armed,1);
        public Task Wait(CancellationToken ct)=>reached.Task.WaitAsync(TimeSpan.FromSeconds(30),ct);
        public void Release()=>release.TrySetResult();
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default) {
            if(command.CommandText.Contains("INSERT INTO offline.\"DailyOutcomes\"",StringComparison.Ordinal) && Interlocked.CompareExchange(ref armed,0,1)==1) {reached.TrySetResult();await release.Task.WaitAsync(TimeSpan.FromSeconds(30),cancellationToken);}return result;
        }
    }
    [Theory]
    [InlineData("role")] [InlineData("policy")]
    public async Task Daily_pending_replay_rechecks_live_role_and_classification_after_ordinary_apply_and_rolls_back_every_effect(string change) {
        var clock=new DailyOfflineClock();var barrier=new DailyOfflineApplyBarrier();await using var host=DailyOfflineHost(clock,barrier);using var c=Client(host);DailyPermissions(host.Services);await Token(c);
        var e=await EnrollDailyOffline(c,"Engine",Guid.NewGuid());var command=DailyCaptured(e,new("Known","FICTIONAL private operational note"));
        barrier.Arm();var pending=DeliverDaily(c,e,command);HttpResponseMessage? response=null;
        try {
            await barrier.Wait(Ct);
            if(change=="role")host.Services.GetRequiredService<FederationSettings>().Memberships[0].RoleReferences=[Guid.NewGuid().ToString("D")];
            else host.Services.GetRequiredService<DailyOfflineSettings>().Policies.Single(p=>p.Kind=="Engine").Fields=[];
            barrier.Release();response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
            Assert.DoesNotContain("FICTIONAL",await response.Content.ReadAsStringAsync(Ct));
            await using var db=Db();Assert.False(await db.DailyOperations.AnyAsync(r=>r.Id==e.RecordId,Ct));Assert.Empty(await db.DailyOfflineOutcomes.ToArrayAsync(Ct));Assert.Empty(await db.DailyOperationRevisions.Where(r=>r.RecordId==e.RecordId).ToArrayAsync(Ct));
            Assert.Equal(HttpStatusCode.Forbidden,(await DeliverDaily(c,e,command)).StatusCode);
        }finally{barrier.Release();response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();}
    }
    [Fact]
    public async Task Daily_enrollment_scope_FK_and_retained_lineage_guards_reject_cross_tenant_links_and_immutable_mutation() {
        await using(var db=Db()) {
            Assert.Empty(await db.DailyOfflineEnrollments.ToArrayAsync(Ct));Assert.Empty(await db.DailyOfflineOutcomes.ToArrayAsync(Ct));Assert.Empty(await db.DailyOfflineLifecycles.ToArrayAsync(Ct));
            var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);var now=DateTimeOffset.UtcNow;
            db.DailyOfflineEnrollments.Add(new(){TenantId=SeedVessel.TenantId,VesselId=foreign.Id,ActorId=actorId,Id=Guid.NewGuid(),RecordId=Guid.NewGuid(),Kind="Noon",DeviceId=Guid.NewGuid(),KeyId=Guid.NewGuid(),CreatedAt=now,ExpiresAt=now.AddMinutes(1),RetainUntil=now.AddHours(1)});
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync(Ct));
        }
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var c=Client(host);DailyPermissions(host.Services);await Token(c);var e=await EnrollDailyOffline(c,"Noon",Guid.NewGuid());
        await using var verify=Db();var header=await verify.DailyOfflineEnrollments.SingleAsync(r=>r.Id==e.Id,Ct);header.Kind="Engine";await Assert.ThrowsAsync<InvalidOperationException>(()=>verify.SaveChangesAsync(Ct));verify.ChangeTracker.Clear();
        var lifecycle=await verify.DailyOfflineLifecycles.FirstAsync(Ct);lifecycle.Action="Forged";await Assert.ThrowsAsync<InvalidOperationException>(()=>verify.SaveChangesAsync(Ct));verify.ChangeTracker.Clear();
        // Expected-to-fail immutable database guard probe, never a successful raw business write.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>verify.Database.ExecuteSqlRawAsync("""DELETE FROM offline."DailyLifecycle"""",Ct));
    }

}
