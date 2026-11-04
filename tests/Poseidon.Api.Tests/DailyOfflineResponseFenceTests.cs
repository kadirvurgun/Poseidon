using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S5-SOURCE-01 P1: deterministic real PostgreSQL/HTTP interleavings. Sources only.
// The paused audit occurs after private composition/commit; another actual HTTP
// request revokes the grant. No timing sleep or direct successful business SQL.
public sealed partial class DirectoryWorkspaceTests
{
    private Task InstallDailyOfflineAuditPause(long key,string action,Guid record)=>Sql($$"""
        CREATE FUNCTION audit.s5_pause_daily_offline_fence() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint); RETURN NEW; END $body$;
        CREATE TRIGGER s5_pause_daily_offline_fence BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
          WHEN (NEW."Action"='{{action}}' AND NEW."Result"='Success' AND NEW."ResourceId"='{{record:D}}'::uuid)
          EXECUTE FUNCTION audit.s5_pause_daily_offline_fence();
        """);
    private Task RemoveDailyOfflineAuditPause()=>Sql("""DROP TRIGGER s5_pause_daily_offline_fence ON audit."SecurityEvents";DROP FUNCTION audit.s5_pause_daily_offline_fence();""");
    private async Task DailyOfflineUnlock(NpgsqlConnection connection,long key) {
        await using var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",connection);release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);
    }
    [Theory]
    [InlineData("conflict","revoke")][InlineData("conflict","lost")]
    [InlineData("history","revoke")][InlineData("history","lost")]
    public async Task Daily_private_read_rechecks_actual_revocation_committed_while_success_audit_is_waiting(string view,string revoke) {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var client=Client(host);using var revoker=Client(host);
        DailyOfflinePermissions(host.Services);await Token(client);await Token(revoker);
        var id=Guid.NewGuid();var draft=new DailyDraftCommand(id,0,new(){["alarms"]=new("Known","FICTIONAL before private note")},"INITIAL_AUTHORING");
        using var created=await DailySend(client,"engine-daily-logs",draft,Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;var enrollment=await EnrollDailyOffline(client,"Engine",id);
        using var updated=await DailySend(client,"engine-daily-logs",draft with {ExpectedVersion=row.Version,ReasonCode="AUTHORING_CORRECTION",Fields=new(){["alarms"]=new("Known","FICTIONAL current private note")}},Guid.NewGuid().ToString("D"),true);
        Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        var capture=DailyCaptured(enrollment,new("Known","FICTIONAL local private note"));using var conflict=await DeliverDaily(client,enrollment,capture);
        Assert.Equal(HttpStatusCode.OK,conflict.StatusCode);Assert.Equal("MaterialConflict",(await conflict.Content.ReadFromJsonAsync<DailyOfflineResult>(Ct))!.Result);
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await InstallDailyOfflineAuditPause(key,"DailyOfflineRead",id);
        var path=DailyOfflinePath("Engine",id,enrollment.Id,view=="conflict"?"conflicts/"+capture.CommandId:"history");
        var pending=client.GetAsync("/api/v1/"+path,Ct);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            using var revoked=await Send(revoker,DailyOfflinePath("Engine",id,enrollment.Id,revoke),new{});Assert.Equal(HttpStatusCode.OK,revoked.StatusCode);
            await DailyOfflineUnlock(blocker,key);response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);
            Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);var body=await response.Content.ReadAsStringAsync(Ct);
            foreach(var secret in new[]{"FICTIONAL before","FICTIONAL local","FICTIONAL current","fields","items","choices","recordId"})Assert.DoesNotContain(secret,body);
            Assert.Equal(HttpStatusCode.Forbidden,(await DeliverDaily(client,enrollment,capture)).StatusCode);
            await using var db=Db();Assert.NotNull((await db.DailyOfflineEnrollments.SingleAsync(e=>e.Id==enrollment.Id,Ct)).RevokedAt);
            Assert.Equal(2,await db.DailyOperationRevisions.CountAsync(r=>r.RecordId==id,Ct));Assert.Single(await db.DailyOfflineOutcomes.Where(o=>o.RecordId==id).ToArrayAsync(Ct));
        }finally{
            await DailyOfflineUnlock(blocker,key);response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await RemoveDailyOfflineAuditPause();
        }
    }
    [Theory]
    [InlineData(false,"revoke")][InlineData(false,"lost")]
    [InlineData(true,"revoke")][InlineData(true,"lost")]
    public async Task Daily_committed_or_recovered_receipt_is_withheld_after_concurrent_revocation_without_erasing_or_duplicating_it(bool recovery,string revoke) {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var client=Client(host);using var revoker=Client(host);
        DailyOfflinePermissions(host.Services);await Token(client);await Token(revoker);
        var enrollment=await EnrollDailyOffline(client,"Deck",Guid.NewGuid());var capture=DailyCaptured(enrollment,new("Known","FICTIONAL retained handover"));
        string? original=null;
        if(recovery){using var first=await DeliverDaily(client,enrollment,capture);Assert.Equal(HttpStatusCode.OK,first.StatusCode);original=await first.Content.ReadAsStringAsync(Ct);}
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await InstallDailyOfflineAuditPause(key,"DailyOfflineRequest",enrollment.RecordId);
        var pending=DeliverDaily(client,enrollment,capture);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            await using(var observer=Db()) {
                Assert.True(await observer.DailyOperations.AnyAsync(r=>r.Id==enrollment.RecordId,Ct));
                Assert.True(await observer.DailyOfflineOutcomes.AnyAsync(o=>o.CommandId==capture.CommandId,Ct));
            }
            using var revoked=await Send(revoker,DailyOfflinePath("Deck",enrollment.RecordId,enrollment.Id,revoke),new{});Assert.Equal(HttpStatusCode.OK,revoked.StatusCode);
            await DailyOfflineUnlock(blocker,key);response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);var body=await response.Content.ReadAsStringAsync(Ct);
            Assert.Contains("command_outcome_withheld",body);Assert.DoesNotContain("recordId",body);Assert.DoesNotContain("FICTIONAL",body);
            Assert.Equal(HttpStatusCode.Forbidden,(await DeliverDaily(client,enrollment,capture)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await DeliverDaily(client,enrollment,DailyCaptured(enrollment))).StatusCode);
            await using var db=Db();var retained=await db.DailyOfflineOutcomes.SingleAsync(o=>o.CommandId==capture.CommandId,Ct);
            if(original!=null)Assert.Equal(original,retained.OutcomeJson);
            Assert.Equal("Committed",retained.Result);Assert.Equal(1,await db.DailyOperationRevisions.CountAsync(r=>r.RecordId==enrollment.RecordId,Ct));
            Assert.Single(await db.DailyOfflineOutcomes.Where(o=>o.RecordId==enrollment.RecordId).ToArrayAsync(Ct));
        }finally{
            await DailyOfflineUnlock(blocker,key);response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await RemoveDailyOfflineAuditPause();
        }
    }
    [Theory]
    [InlineData("revoke")][InlineData("lost")]
    public async Task Daily_revocation_committed_before_replay_commit_rolls_back_the_pending_report_and_receipt(string revoke) {
        var clock=new DailyOfflineClock();var barrier=new DailyOfflineApplyBarrier();await using var host=DailyOfflineHost(clock,barrier);
        using var client=Client(host);using var revoker=Client(host);DailyOfflinePermissions(host.Services);await Token(client);await Token(revoker);
        var enrollment=await EnrollDailyOffline(client,"Noon",Guid.NewGuid());var capture=DailyCaptured(enrollment,new("Known","1.25"));
        barrier.Arm();var pending=DeliverDaily(client,enrollment,capture);HttpResponseMessage? response=null;
        try {
            await barrier.Wait(Ct);using var revoked=await Send(revoker,DailyOfflinePath("Noon",enrollment.RecordId,enrollment.Id,revoke),new{});Assert.Equal(HttpStatusCode.OK,revoked.StatusCode);
            barrier.Release();response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
            await using var db=Db();Assert.False(await db.DailyOperations.AnyAsync(r=>r.Id==enrollment.RecordId,Ct));
            Assert.Empty(await db.DailyOperationRevisions.Where(r=>r.RecordId==enrollment.RecordId).ToArrayAsync(Ct));Assert.Empty(await db.DailyOfflineOutcomes.Where(o=>o.RecordId==enrollment.RecordId).ToArrayAsync(Ct));
            Assert.DoesNotContain(await db.AuditEvents.Where(a=>a.ResourceId==enrollment.RecordId).ToArrayAsync(Ct),a=>a.EntityType is nameof(DailyOperation) or nameof(DailyOperationRevision) or nameof(DailyOfflineOutcome));
        }finally{barrier.Release();response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();}
    }
    [Fact]
    public async Task Expired_capture_lease_still_allows_current_retained_receipt_but_not_a_new_write() {
        var clock=new DailyOfflineClock();await using var host=DailyOfflineHost(clock);using var client=Client(host);DailyOfflinePermissions(host.Services);await Token(client);
        var enrollment=await EnrollDailyOffline(client,"Noon",Guid.NewGuid());var capture=DailyCaptured(enrollment);
        using var committed=await DeliverDaily(client,enrollment,capture);Assert.Equal(HttpStatusCode.OK,committed.StatusCode);var bytes=await committed.Content.ReadAsStringAsync(Ct);
        clock.Current=clock.Current.AddSeconds(601);
        using var replay=await DeliverDaily(client,enrollment,capture);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);Assert.Equal(bytes,await replay.Content.ReadAsStringAsync(Ct));
        using var history=await client.GetAsync("/api/v1/"+DailyOfflinePath("Noon",enrollment.RecordId,enrollment.Id,"history"),Ct);Assert.Equal(HttpStatusCode.OK,history.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await DeliverDaily(client,enrollment,DailyCaptured(enrollment))).StatusCode);
        await using var db=Db();Assert.Single(await db.DailyOfflineOutcomes.Where(o=>o.RecordId==enrollment.RecordId).ToArrayAsync(Ct));
        Assert.Single(await db.DailyOperationRevisions.Where(r=>r.RecordId==enrollment.RecordId).ToArrayAsync(Ct));
    }
}
