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
    public async Task WorkOrder_read_response_rechecks_actual_current_head_and_role_after_success_audit(string change) {
        await using var host=Host();using var reader=Client(host);using var writer=Client(host);WorkOrderPermissions(host.Services);
        var command=WorkOrderDraft() with{Fields=new(){["findings"]=new("Known","FICTIONAL private work_order snapshot")}};
        using var created=await WorkOrderSend(writer,command);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var row=(await created.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_work_order_read() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_work_order_read BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='WorkOrderRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.pause_work_order_read();
            """);
        var pending=reader.GetAsync(WorkOrderPath(row.Id),Ct);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            if(change=="head")Assert.Equal(HttpStatusCode.OK,(await WorkOrderSend(writer,command with{ExpectedVersion=row.Version},update:true)).StatusCode);
            else{var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];settings.Memberships[0]=m with{Permissions=m.Permissions.Where(p=>p!="equipment.read").ToArray()};}
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.DoesNotContain("FICTIONAL",await response.Content.ReadAsStringAsync(Ct));
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_work_order_read ON audit."SecurityEvents";DROP FUNCTION audit.pause_work_order_read();""");
        }
    }
    [Fact]
    public async Task WorkOrder_new_plan_binding_rolls_back_if_actual_source_head_advances_during_durable_attempt_audit() {
        await using var host=Host(technicalRead:true,technicalWrite:true);using var writer=Client(host);using var assetWriter=Client(host);WorkOrderPermissions(host.Services);var asset=await WorkOrderPlan(assetWriter);
        var command=WorkOrderDraft() with{Plan=asset.Pin};var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_work_order_attempt() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_work_order_attempt BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='WorkOrderCommand' AND NEW."Result"='Attempted') EXECUTE FUNCTION audit.pause_work_order_attempt();
            """);
        var pending=WorkOrderSend(writer,command);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);await Token(assetWriter);
            Assert.Equal(HttpStatusCode.OK,(await Send(assetWriter,PlansUrl,new MaintenanceCommand(asset.Pin.Id,asset.Pin.Version,asset.Plan.AssetId,null,null,new(Criticality:"Unknown"),DateTimeOffset.UnixEpoch))).StatusCode);
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_work_order_attempt ON audit."SecurityEvents";DROP FUNCTION audit.pause_work_order_attempt();""");
        }
        await using var db=Db();Assert.Empty(await db.WorkOrders.Where(r=>r.Id==command.Id).ToArrayAsync(Ct));Assert.Empty(await db.WorkOrderReceipts.Where(r=>r.WorkOrderId==command.Id).ToArrayAsync(Ct));
        await using var verify=new NpgsqlConnection(postgres.GetConnectionString());await verify.OpenAsync(Ct);
        await using var audit=new NpgsqlCommand("""SELECT count(*) FROM audit."SecurityEvents" WHERE "Action"='WorkOrderCommand' AND "Result"='Attempted' AND "ResourceId"=@id;""",verify);
        audit.Parameters.AddWithValue("id",command.Id);Assert.Equal(1L,Convert.ToInt64(await audit.ExecuteScalarAsync(Ct)));
    }
    [Fact]
    public async Task WorkOrder_postcommit_revocation_keeps_committed_facts_and_withholds_payload_until_exact_same_command_can_be_reauthorized() {
        await using var host=Host();using var client=Client(host);WorkOrderPermissions(host.Services);var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        var command=WorkOrderDraft() with{Fields=new(){["findings"]=new("Known","FICTIONAL private unknown outcome")}};var commandKey=Guid.NewGuid().ToString("D");
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.pause_work_order_outcome() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER pause_work_order_outcome BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='WorkOrderCommand' AND NEW."Result"='Success') EXECUTE FUNCTION audit.pause_work_order_outcome();
            """);
        var pending=WorkOrderSend(client,command,commandKey);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);settings.Memberships[0]=member with{Permissions=member.Permissions.Where(p=>p!="workorder.write").ToArray()};
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);var body=await response.Content.ReadAsStringAsync(Ct);
            Assert.Contains("command_outcome_withheld",body);Assert.DoesNotContain("fields",body);Assert.DoesNotContain("FICTIONAL",body);
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER pause_work_order_outcome ON audit."SecurityEvents";DROP FUNCTION audit.pause_work_order_outcome();""");
        }
        await using var db=Db();var receipt=await db.WorkOrderReceipts.SingleAsync(r=>r.WorkOrderId==command.Id,Ct);var stored=JsonSerializer.Deserialize<WorkOrderRecordDto>(receipt.OutcomeJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Single(await db.WorkOrderRevisions.Where(r=>r.WorkOrderId==command.Id).ToArrayAsync(Ct));Assert.Equal(HttpStatusCode.Forbidden,(await WorkOrderSend(client,command,commandKey)).StatusCode);
        settings.Memberships[0]=member;using var recovered=await WorkOrderSend(client,command,commandKey);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(WorkOrderFacts(stored),WorkOrderFacts((await recovered.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!));
        Assert.Single(await db.WorkOrderReceipts.Where(r=>r.WorkOrderId==command.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task WorkOrder_database_binding_and_immutable_retention_guards_reject_partial_plan_scope_crossing_and_history_mutations() {
        await using var host=Host();using var client=Client(host);WorkOrderPermissions(host.Services);Assert.Equal(HttpStatusCode.OK,(await WorkOrderSend(client,WorkOrderDraft())).StatusCode);
        foreach(var sql in new[]{"""UPDATE technical."WorkOrderRevisions" SET "FieldsJson"='{}';""","""DELETE FROM technical."WorkOrderRevisions";""","""TRUNCATE technical."WorkOrderReceipts";"""}) {
            await using var connection=new NpgsqlConnection(postgres.GetConnectionString());await connection.OpenAsync(Ct);await using var mutation=new NpgsqlCommand(sql,connection);await Assert.ThrowsAsync<PostgresException>(()=>mutation.ExecuteNonQueryAsync(Ct));
        }
        foreach(var partial in new[]{true,false}) {
            await using var db=Db();db.WorkOrders.Add(new(){TenantId=SeedVessel.TenantId,VesselId=partial?SeedVessel.Id:seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId).Id,Id=Guid.NewGuid(),PlanId=partial?Guid.NewGuid():null,SourceVersion=WorkOrderPolicy.SourceVersion,Hash=new string('0',64),SavedAt=DateTimeOffset.UtcNow});
            var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync(Ct));Assert.Equal(partial?"23514":"23503",PostgresFailure.Provider(error)?.SqlState);
        }
    }
}
