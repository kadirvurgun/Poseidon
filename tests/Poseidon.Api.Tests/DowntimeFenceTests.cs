using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    [Theory]
    [InlineData("head")][InlineData("role")]
    public async Task Downtime_read_response_rechecks_actual_current_head_and_role_after_success_audit(string change) {
        await using var host=Host();using var reader=Client(host);using var writer=Client(host);DowntimePermissions(host.Services);
        var command=DowntimeDraft() with{Fields=new(){["cause"]=new("Known","FICTIONAL private downtime snapshot")}};
        using var created=await DowntimeSend(writer,command);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var row=(await created.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!;
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_downtime_read() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_downtime_read BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DowntimeRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.pause_downtime_read();
            """);
        var pending=reader.GetAsync(DowntimePath(row.Id),Ct);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            if(change=="head")Assert.Equal(HttpStatusCode.OK,(await DowntimeSend(writer,command with{ExpectedVersion=row.Version},update:true)).StatusCode);
            else{var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];settings.Memberships[0]=m with{Permissions=m.Permissions.Where(p=>p!="operations.downtime.read").ToArray()};}
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.DoesNotContain("FICTIONAL",await response.Content.ReadAsStringAsync(Ct));
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_downtime_read ON audit."SecurityEvents";DROP FUNCTION audit.pause_downtime_read();""");
        }
    }
    [Fact]
    public async Task Downtime_postcommit_revocation_keeps_committed_facts_and_withholds_payload_until_exact_same_command_can_be_reauthorized() {
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        var command=DowntimeDraft() with{Fields=new(){["cause"]=new("Known","FICTIONAL private unknown outcome")}};var commandKey=Guid.NewGuid().ToString("D");
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_downtime_outcome() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_downtime_outcome BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DowntimeCommand' AND NEW."Result"='Success') EXECUTE FUNCTION audit.pause_downtime_outcome();
            """);
        var pending=DowntimeSend(client,command,commandKey);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!="operations.downtime.write").ToArray()};
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);var body=await response.Content.ReadAsStringAsync(Ct);
            Assert.Contains("command_outcome_withheld",body);Assert.DoesNotContain("fields",body);Assert.DoesNotContain("FICTIONAL",body);
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_downtime_outcome ON audit."SecurityEvents";DROP FUNCTION audit.pause_downtime_outcome();""");
        }
        await using var db=Db();var receipt=await db.DowntimeReceipts.SingleAsync(r=>r.DowntimeId==command.Id,Ct);var stored=JsonSerializer.Deserialize<DowntimeRecordDto>(receipt.OutcomeJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Single(await db.DowntimeRevisions.Where(r=>r.DowntimeId==command.Id).ToArrayAsync(Ct));Assert.Equal(HttpStatusCode.Forbidden,(await DowntimeSend(client,command,commandKey)).StatusCode);
        settings.Memberships[0]=member;using var recovered=await DowntimeSend(client,command,commandKey);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(DowntimeFacts(stored),DowntimeFacts((await recovered.Content.ReadFromJsonAsync<DowntimeRecordDto>(Ct))!));
        Assert.Single(await db.DowntimeReceipts.Where(r=>r.DowntimeId==command.Id).ToArrayAsync(Ct));
    }
    [Theory]
    [InlineData("Defect")][InlineData("WorkOrder")][InlineData("Engine")]
    public async Task Downtime_new_origin_is_rolled_back_if_actual_source_head_advances_during_durable_attempt_audit(string kind) {
        await using var host=Host();using var writer=Client(host);using var sourceWriter=Client(host);DowntimePermissions(host.Services);DefectPermissions(host.Services);WorkOrderPermissions(host.Services);DailyPermissions(host.Services);
        Guid id;uint version;
        if(kind=="Defect"){var draft=DefectDraft();using var made=await DefectSend(sourceWriter,draft);Assert.Equal(HttpStatusCode.OK,made.StatusCode);var row=(await made.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;id=row.Id;version=row.Version;}
        else if(kind=="WorkOrder"){var draft=WorkOrderDraft();using var made=await WorkOrderSend(sourceWriter,draft);Assert.Equal(HttpStatusCode.OK,made.StatusCode);var row=(await made.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;id=row.Id;version=row.Version;}
        else{var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");using var made=await DailySend(sourceWriter,"engine-daily-logs",draft,Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.OK,made.StatusCode);var row=(await made.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;id=row.Id;version=row.Version;}
        var origin=(await sourceWriter.GetFromJsonAsync<DowntimeOriginDto>(DowntimeOriginPath(kind,id),Ct))!;var command=DowntimeDraft() with{Origin=origin.Pin};
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_downtime_origin_attempt() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_downtime_origin_attempt BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DowntimeCommand' AND NEW."Result"='Attempted') EXECUTE FUNCTION audit.pause_downtime_origin_attempt();
            """);
        var pending=DowntimeSend(writer,command);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            if(kind=="Defect")Assert.Equal(HttpStatusCode.OK,(await DefectSend(sourceWriter,DefectDraft(id) with{ExpectedVersion=version},update:true)).StatusCode);
            else if(kind=="WorkOrder")Assert.Equal(HttpStatusCode.OK,(await WorkOrderSend(sourceWriter,WorkOrderDraft(id) with{ExpectedVersion=version},update:true)).StatusCode);
            else Assert.Equal(HttpStatusCode.OK,(await DailySend(sourceWriter,"engine-daily-logs",new(id,version,new(),"AUTHORING_CORRECTION"),Guid.NewGuid().ToString("D"),true)).StatusCode);
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_downtime_origin_attempt ON audit."SecurityEvents";DROP FUNCTION audit.pause_downtime_origin_attempt();""");
        }
        await using var db=Db();Assert.Empty(await db.Downtimes.Where(r=>r.Id==command.Id).ToArrayAsync(Ct));Assert.Empty(await db.DowntimeReceipts.Where(r=>r.DowntimeId==command.Id).ToArrayAsync(Ct));Assert.Empty(await db.DowntimeRevisions.Where(r=>r.DowntimeId==command.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Downtime_retained_Engine_pin_rechecks_fresh_head_without_rebasing_immutable_history() {
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);DailyPermissions(host.Services);
        var command=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");
        using var created=await DailySend(client,"engine-daily-logs",command,Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var first=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var origin=(await client.GetFromJsonAsync<DowntimeOriginDto>(DowntimeOriginPath("Engine",first.Id),Ct))!;
        var actor=new TrustedActor(actorId,SeedVessel.TenantId,["directory.read","daily.engine.read"],true,false,[]);
        await using var db=Db();
        var captured=await DowntimeOriginReader.CaptureFence(db,actor,SeedVessel.Id,[origin.Pin],false,Ct);Assert.NotNull(captured);
        Assert.True(await actor.Scope(db.Vessels).Where(v=>v.Id==SeedVessel.Id).Where(captured!).AnyAsync(Ct));
        using var updated=await DailySend(client,"engine-daily-logs",command with{ExpectedVersion=first.Version,ReasonCode="AUTHORING_CORRECTION"},Guid.NewGuid().ToString("D"),true);
        Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        Assert.False(await actor.Scope(db.Vessels).Where(v=>v.Id==SeedVessel.Id).Where(captured!).AnyAsync(Ct));
        var refreshed=await DowntimeOriginReader.CaptureFence(db,actor,SeedVessel.Id,[origin.Pin],false,Ct);Assert.NotNull(refreshed);
        Assert.True(await actor.Scope(db.Vessels).Where(v=>v.Id==SeedVessel.Id).Where(refreshed!).AnyAsync(Ct));
        var currentOnly=await DowntimeOriginReader.CaptureFence(db,actor,SeedVessel.Id,[origin.Pin],true,Ct);Assert.NotNull(currentOnly);
        Assert.False(await actor.Scope(db.Vessels).Where(v=>v.Id==SeedVessel.Id).Where(currentOnly!).AnyAsync(Ct));
        Assert.Equal(1,origin.Pin.Revision);Assert.Equal(first.Version,origin.Pin.Version);
    }
    [Fact]
    public async Task Downtime_relational_scope_and_immutable_database_retention_guards_reject_cross_tenant_history_removal_and_head_reparenting() {
        await using var host=Host();using var client=Client(host);DowntimePermissions(host.Services);var command=DowntimeDraft();Assert.Equal(HttpStatusCode.OK,(await DowntimeSend(client,command)).StatusCode);
        foreach(var sql in new[]{"""UPDATE operations."DowntimeRevisions" SET "FieldsJson"='{}';""","""DELETE FROM operations."DowntimeRevisions";""","""TRUNCATE operations."DowntimeReceipts";""","""UPDATE operations."Downtimes" SET "VesselId"='00000000-0000-0000-0000-000000000001',"Revision"="Revision"+1;"""}) {
            await using var connection=new NpgsqlConnection(postgres.GetConnectionString());await connection.OpenAsync(Ct);await using var mutation=new NpgsqlCommand(sql,connection);await Assert.ThrowsAsync<PostgresException>(()=>mutation.ExecuteNonQueryAsync(Ct));
        }
        await using var db=Db();db.Downtimes.Add(new(){TenantId=SeedVessel.TenantId,VesselId=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId).Id,Id=Guid.NewGuid(),SourceVersion=DowntimePolicy.SourceVersion,Hash=new string('0',64),SavedAt=DateTimeOffset.UtcNow});
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync(Ct));Assert.Equal("23503",PostgresFailure.Provider(error)?.SqlState);
    }

}
