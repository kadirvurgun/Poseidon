using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    [Theory]
    [InlineData("head")][InlineData("role")]
    public async Task ShipOps_current_response_is_withheld_if_head_or_scope_changes_during_success_audit(string change) {
        await using var host=Host();using var reader=Client(host);using var writer=Client(host);ShipOpsPermissions(host.Services);
        var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){["remarks"]=new("Known","FICTIONAL private port snapshot")},"INITIAL_AUTHORING");
        using var saved=await ShipOpsSend(writer,"Port",command);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);var row=(await saved.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.s6_pause_shipops_read() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER s6_pause_shipops_read BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='ShipOperationsRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s6_pause_shipops_read();
            """);
        var pending=reader.GetAsync(ShipOpsPath("Port",row.Id),Ct);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            if(change=="head")Assert.Equal(HttpStatusCode.OK,(await ShipOpsSend(writer,"Port",command with{ExpectedVersion=row.Version,ReasonCode="AUTHORING_CORRECTION"},update:true)).StatusCode);
            else{var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];settings.Memberships[0]=m with{Permissions=m.Permissions.Where(p=>p!="operations.port.read").ToArray()};}
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.DoesNotContain("FICTIONAL",await response.Content.ReadAsStringAsync(Ct));
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();
            await Sql("""DROP TRIGGER s6_pause_shipops_read ON audit."SecurityEvents";DROP FUNCTION audit.s6_pause_shipops_read();""");
        }
    }
    [Fact]
    public async Task ShipOps_postcommit_revocation_returns_unknown_outcome_and_retains_exact_receipt_without_rollback_or_duplicate() {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);
        var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,new(){["bdn_no"]=new("Known","FICTIONAL private BDN declaration")},"INITIAL_AUTHORING");var commandKey=Guid.NewGuid().ToString("D");
        var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.s6_pause_shipops_commit() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER s6_pause_shipops_commit BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='ShipOperationsCommand' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s6_pause_shipops_commit();
            """);
        var pending=ShipOpsSend(client,"Fuel",command,commandKey);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!="operations.fuel.write").ToArray()};
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            var body=await response.Content.ReadAsStringAsync(Ct);Assert.Contains("command_outcome_withheld",body);Assert.DoesNotContain("FICTIONAL",body);Assert.DoesNotContain("fields",body);
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();
            await Sql("""DROP TRIGGER s6_pause_shipops_commit ON audit."SecurityEvents";DROP FUNCTION audit.s6_pause_shipops_commit();""");
        }
        await using var db=Db();var receipt=await db.ShipOperationReceipts.SingleAsync(r=>r.RecordId==command.Id,Ct);
        Assert.Single(await db.ShipOperationRevisions.Where(r=>r.RecordId==command.Id).ToArrayAsync(Ct));Assert.Equal(1,(await db.ShipOperations.SingleAsync(r=>r.Id==command.Id,Ct)).Revision);
        Assert.Equal(HttpStatusCode.Forbidden,(await ShipOpsSend(client,"Fuel",command,commandKey)).StatusCode);
        settings.Memberships[0]=member;using var recovered=await ShipOpsSend(client,"Fuel",command,commandKey);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(receipt.OutcomeJson,await recovered.Content.ReadAsStringAsync(Ct));
        Assert.Single(await db.ShipOperationReceipts.Where(r=>r.RecordId==command.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task ShipOps_immutable_revision_and_receipt_database_guards_reject_update_delete_and_truncate_without_archiving_policy_bypass() {
        await using var host=Host();using var client=Client(host);ShipOpsPermissions(host.Services);
        var command=new ShipOpsDraftCommand(Guid.NewGuid(),0,[],"INITIAL_AUTHORING");Assert.Equal(HttpStatusCode.OK,(await ShipOpsSend(client,"Fuel",command)).StatusCode);
        foreach(var sql in new[]{"""UPDATE operations."ShipOperationRevisions" SET "FieldsJson"='{}';""","""DELETE FROM operations."ShipOperationRevisions";""","""TRUNCATE operations."ShipOperationReceipts";"""}) {
            await using var connection=new NpgsqlConnection(postgres.GetConnectionString());await connection.OpenAsync(Ct);await using var mutation=new NpgsqlCommand(sql,connection);
            await Assert.ThrowsAsync<PostgresException>(()=>mutation.ExecuteNonQueryAsync(Ct));
        }
        await using var db=Db();Assert.Single(await db.ShipOperations.ToArrayAsync(Ct));Assert.Single(await db.ShipOperationRevisions.ToArrayAsync(Ct));Assert.Single(await db.ShipOperationReceipts.ToArrayAsync(Ct));
    }
}
