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
    public async Task Defect_read_response_rechecks_actual_current_head_and_role_after_success_audit(string change) {
        await using var host=Host();using var reader=Client(host);using var writer=Client(host);DefectPermissions(host.Services);
        var command=DefectDraft() with{Fields=new(){["notes"]=new("Known","FICTIONAL private defect snapshot")}};
        using var created=await DefectSend(writer,command);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var row=(await created.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_defect_read() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_defect_read BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DefectRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.pause_defect_read();
            """);
        var pending=reader.GetAsync(DefectPath(row.Id),Ct);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            if(change=="head")Assert.Equal(HttpStatusCode.OK,(await DefectSend(writer,command with{ExpectedVersion=row.Version},update:true)).StatusCode);
            else{var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];settings.Memberships[0]=m with{Permissions=m.Permissions.Where(p=>p!="equipment.read").ToArray()};}
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.DoesNotContain("FICTIONAL",await response.Content.ReadAsStringAsync(Ct));
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_defect_read ON audit."SecurityEvents";DROP FUNCTION audit.pause_defect_read();""");
        }
    }
    [Fact]
    public async Task Defect_new_asset_binding_rolls_back_if_actual_source_head_advances_during_durable_attempt_audit() {
        await using var host=Host(technicalRead:true,technicalWrite:true);using var writer=Client(host);using var assetWriter=Client(host);DefectPermissions(host.Services);var asset=await DefectEquipment(assetWriter);
        var command=DefectDraft() with{Asset=asset.Pin};var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_defect_attempt() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_defect_attempt BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DefectCommand' AND NEW."Result"='Attempted') EXECUTE FUNCTION audit.pause_defect_attempt();
            """);
        var pending=DefectSend(writer,command);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);await Token(assetWriter);
            Assert.Equal(HttpStatusCode.OK,(await Send(assetWriter,EquipmentUrl,NewEquipment(asset.Pin.Id) with{BaseVersion=asset.Pin.Version,Name="FICTIONAL source changed during attempt"})).StatusCode);
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_defect_attempt ON audit."SecurityEvents";DROP FUNCTION audit.pause_defect_attempt();""");
        }
        await using var db=Db();Assert.Empty(await db.Defects.Where(r=>r.Id==command.Id).ToArrayAsync(Ct));Assert.Empty(await db.DefectReceipts.Where(r=>r.DefectId==command.Id).ToArrayAsync(Ct));
        await using var verify=new NpgsqlConnection(postgres.GetConnectionString());await verify.OpenAsync(Ct);
        await using var audit=new NpgsqlCommand("""SELECT count(*) FROM audit."SecurityEvents" WHERE "Action"='DefectCommand' AND "Result"='Attempted' AND "ResourceId"=@id;""",verify);
        audit.Parameters.AddWithValue("id",command.Id);Assert.Equal(1L,Convert.ToInt64(await audit.ExecuteScalarAsync(Ct)));
    }
    [Fact]
    public async Task Defect_postcommit_revocation_keeps_committed_facts_and_withholds_payload_until_exact_same_command_can_be_reauthorized() {
        await using var host=Host();using var client=Client(host);DefectPermissions(host.Services);var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        var command=DefectDraft() with{Fields=new(){["notes"]=new("Known","FICTIONAL private unknown outcome")}};var commandKey=Guid.NewGuid().ToString("D");
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_defect_outcome() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_defect_outcome BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DefectCommand' AND NEW."Result"='Success') EXECUTE FUNCTION audit.pause_defect_outcome();
            """);
        var pending=DefectSend(client,command,commandKey);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!="defect.write").ToArray()};
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);var body=await response.Content.ReadAsStringAsync(Ct);
            Assert.Contains("command_outcome_withheld",body);Assert.DoesNotContain("fields",body);Assert.DoesNotContain("FICTIONAL",body);
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_defect_outcome ON audit."SecurityEvents";DROP FUNCTION audit.pause_defect_outcome();""");
        }
        await using var db=Db();var receipt=await db.DefectReceipts.SingleAsync(r=>r.DefectId==command.Id,Ct);var stored=JsonSerializer.Deserialize<DefectRecordDto>(receipt.OutcomeJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Single(await db.DefectRevisions.Where(r=>r.DefectId==command.Id).ToArrayAsync(Ct));Assert.Equal(HttpStatusCode.Forbidden,(await DefectSend(client,command,commandKey)).StatusCode);
        settings.Memberships[0]=member;using var recovered=await DefectSend(client,command,commandKey);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(DefectFacts(stored),DefectFacts((await recovered.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!));
        Assert.Single(await db.DefectReceipts.Where(r=>r.DefectId==command.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Defect_database_binding_and_immutable_retention_guards_reject_partial_asset_scope_crossing_and_history_mutations() {
        await using var host=Host();using var client=Client(host);DefectPermissions(host.Services);Assert.Equal(HttpStatusCode.OK,(await DefectSend(client,DefectDraft())).StatusCode);
        foreach(var sql in new[]{"""UPDATE technical."DefectRevisions" SET "FieldsJson"='{}';""","""DELETE FROM technical."DefectRevisions";""","""TRUNCATE technical."DefectReceipts";"""}) {
            await using var connection=new NpgsqlConnection(postgres.GetConnectionString());await connection.OpenAsync(Ct);await using var mutation=new NpgsqlCommand(sql,connection);await Assert.ThrowsAsync<PostgresException>(()=>mutation.ExecuteNonQueryAsync(Ct));
        }
        foreach(var partial in new[]{true,false}) {
            await using var db=Db();db.Defects.Add(new(){TenantId=SeedVessel.TenantId,VesselId=partial?SeedVessel.Id:seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId).Id,Id=Guid.NewGuid(),AssetId=partial?Guid.NewGuid():null,SourceVersion=DefectPolicy.SourceVersion,Hash=new string('0',64),SavedAt=DateTimeOffset.UtcNow});
            var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync(Ct));Assert.Equal(partial?"23514":"23503",PostgresFailure.Provider(error)?.SqlState);
        }
    }
}
