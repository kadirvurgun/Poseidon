using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private sealed class DailyRowsBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource read=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        public void Arm()=>Interlocked.Exchange(ref armed,1);
        public Task Wait(CancellationToken ct)=>read.Task.WaitAsync(TimeSpan.FromSeconds(30),ct);
        public void Release()=>released.TrySetResult();
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default) {
            if(command.CommandText.StartsWith("SELECT",StringComparison.Ordinal) && command.CommandText.Contains("operations.\"DailyOperations\"",StringComparison.Ordinal) && Interlocked.CompareExchange(ref armed,0,1)==1) {
                read.TrySetResult(); await released.Task.WaitAsync(TimeSpan.FromSeconds(30),cancellationToken);
            }
            return result;
        }
    }
    private sealed class ConcurrentDailyCreateBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource bothRead=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int readers;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default) {
            if(command.CommandText.StartsWith("SELECT",StringComparison.Ordinal) && command.CommandText.Contains("operations.\"DailyOperations\"",StringComparison.Ordinal)) {
                var count=Interlocked.Increment(ref readers);
                if(count==2)bothRead.TrySetResult();
                if(count<=2)await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(30),cancellationToken);
            }
            return result;
        }
    }
    [Fact]
    public async Task Concurrent_identical_daily_creates_produce_one_revision_and_exact_recoverable_receipt() {
        var barrier=new ConcurrentDailyCreateBarrier();await using var host=Host(interceptor:barrier);
        using var first=Client(host);using var second=Client(host);DailyPermissions(host.Services);
        var body=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");var key=Guid.NewGuid().ToString("D");
        // Both real SERIALIZABLE transactions have read the missing head before
        // either inserts. No sleep chooses the collision or retries a write.
        var results=await Task.WhenAll(DailySend(first,"noon-reports",body,key),DailySend(second,"noon-reports",body,key));
        Assert.Contains(results,r=>r.StatusCode==HttpStatusCode.OK);
        Assert.All(results,r=>Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var expected=await results.First(r=>r.StatusCode==HttpStatusCode.OK).Content.ReadAsStringAsync(Ct);
        foreach(var response in results)if(response.StatusCode==HttpStatusCode.OK)Assert.Equal(expected,await response.Content.ReadAsStringAsync(Ct));
        var recovered=await DailySend(first,"noon-reports",body,key);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);
        Assert.Equal(expected,await recovered.Content.ReadAsStringAsync(Ct));
        await using var db=Db();Assert.Equal(1,await db.DailyOperations.CountAsync(r=>r.Id==body.Id,Ct));
        Assert.Equal(1,await db.DailyOperationRevisions.CountAsync(r=>r.RecordId==body.Id,Ct));Assert.Equal(1,await db.DailyOperationReceipts.CountAsync(r=>r.RecordId==body.Id,Ct));
        foreach(var response in results)response.Dispose();
    }
    [Theory]
    [InlineData("read")] [InlineData("scope")] [InlineData("role")] [InlineData("tenant")] [InlineData("equivalent")]
    public async Task Daily_read_response_rechecks_trusted_scope_after_its_database_snapshot(string change) {
        var barrier=new DailyRowsBarrier(); await using var host=Host(interceptor:barrier); using var client=Client(host); DailyPermissions(host.Services);
        var settings=host.Services.GetRequiredService<FederationSettings>(); var prior=settings.Memberships[0];
        barrier.Arm(); var pending=client.GetAsync(DailyPath("noon-reports"),Ct); HttpResponseMessage? response=null;
        try {
            await barrier.Wait(Ct);
            settings.Memberships[0]=change switch {
                "read"=>prior with {Permissions=prior.Permissions.Where(p=>p!="daily.noon.read").ToArray()},
                "scope"=>prior with {TenantWideRead=false,TenantWideManage=false,VesselGrants=[]},
                "role"=>prior with {RoleReferences=[Guid.NewGuid().ToString("D")]},
                "tenant"=>prior with {TenantId=seed.Tenants[1].Id},
                _=>prior with {Permissions=prior.Permissions.Reverse().ToArray()}
            };
            barrier.Release(); response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);
            Assert.Equal(change=="equivalent"?HttpStatusCode.OK:HttpStatusCode.Forbidden,response.StatusCode);
            if(change!="equivalent")Assert.DoesNotContain("items",await response.Content.ReadAsStringAsync(Ct));
        } finally {barrier.Release(); response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();}
    }
    [Fact]
    public async Task Daily_response_rechecks_permission_after_success_audit_before_returning_private_fields() {
        await using var host=Host(); using var client=Client(host); DailyPermissions(host.Services);
        var key=(long)Random.Shared.Next(1,int.MaxValue);
        await using var blocker=new NpgsqlConnection(postgres.GetConnectionString()); await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.s5_pause_daily_read() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint); RETURN NEW; END $body$;
            CREATE TRIGGER s5_pause_daily_read BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DailyOperationsRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s5_pause_daily_read();
            """);
        var pending=client.GetAsync(DailyPath("noon-reports"),Ct); HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            var settings=host.Services.GetRequiredService<FederationSettings>(); var prior=settings.Memberships[0];
            settings.Memberships[0]=prior with {Permissions=prior.Permissions.Where(p=>p!="daily.noon.read").ToArray()};
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct); Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
            Assert.DoesNotContain("items",await response.Content.ReadAsStringAsync(Ct));
        } finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();
            await Sql("""DROP TRIGGER s5_pause_daily_read ON audit."SecurityEvents"; DROP FUNCTION audit.s5_pause_daily_read();""");
        }
    }
    [Fact]
    public async Task Daily_committed_command_withdraws_payload_but_preserves_exact_receipt_until_reauthorized_retry() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var settings=host.Services.GetRequiredService<FederationSettings>();var prior=settings.Memberships[0];
        var key=(long)Random.Shared.Next(1,int.MaxValue);
        await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.s5_pause_daily_commit_response() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint); RETURN NEW; END $body$;
            CREATE TRIGGER s5_pause_daily_commit_response BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DailyOperationsCommand' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s5_pause_daily_commit_response();
            """);
        var command=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");var idempotency=Guid.NewGuid().ToString("D");
        var pending=DailySend(client,"noon-reports",command,idempotency);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            // The pause is the post-commit HTTP audit. Observe real committed data
            // from another connection before withdrawing request authority.
            await using(var observer=Db()) {
                Assert.True(await observer.DailyOperations.AnyAsync(r=>r.Id==command.Id,Ct));
                Assert.True(await observer.DailyOperationReceipts.AnyAsync(r=>r.RecordId==command.Id && r.Key==idempotency,Ct));
            }
            settings.Memberships[0]=prior with {Permissions=prior.Permissions.Where(p=>p!="daily.noon.write").ToArray()};
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            var body=await response.Content.ReadAsStringAsync(Ct);Assert.Contains("command_outcome_withheld",body);Assert.DoesNotContain("fields",body);
            Assert.Equal(HttpStatusCode.Forbidden,(await DailySend(client,"noon-reports",command,idempotency)).StatusCode);
            settings.Memberships[0]=prior;
            var recovered=await DailySend(client,"noon-reports",command,idempotency);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);
            await using var db=Db();var receipt=await db.DailyOperationReceipts.SingleAsync(r=>r.RecordId==command.Id,Ct);
            Assert.Equal(receipt.OutcomeJson,await recovered.Content.ReadAsStringAsync(Ct));
            Assert.Equal(1,await db.DailyOperationRevisions.CountAsync(r=>r.RecordId==command.Id,Ct));
        } finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();
            await Sql("""DROP TRIGGER s5_pause_daily_commit_response ON audit."SecurityEvents"; DROP FUNCTION audit.s5_pause_daily_commit_response();""");
        }
    }
    [Fact]
    public async Task Synthetic_daily_fixtures_are_add_missing_only_and_cross_tenant_record_binding_is_rejected_by_PostgreSQL() {
        await using var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("synthetic-fixture","System","daily-fixture"),administrativeImport:true);
        await using(var tx=await db.Database.BeginTransactionAsync(Ct)) {
            await DailyOperationsFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);
        }
        var id=DailyOperationsFixtures.Id(SeedVessel.Id,"Noon"); var original=await db.DailyOperations.SingleAsync(r=>r.Id==id,Ct);
        var version=original.Version;var hash=original.Hash;
        await using(var tx=await db.Database.BeginTransactionAsync(Ct)) {
            await DailyOperationsFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);
        }
        await db.Entry(original).ReloadAsync(Ct);Assert.Equal(version,original.Version);Assert.Equal(hash,original.Hash);
        Assert.Equal(3,await db.DailyOperations.CountAsync(r=>r.VesselId==SeedVessel.Id,Ct));
        Assert.Equal(3,await db.DailyOperationRevisions.CountAsync(r=>r.VesselId==SeedVessel.Id,Ct));
        Assert.Empty(await db.DailyOperationReceipts.ToArrayAsync(Ct)); Assert.All(await db.DailyOperations.ToArrayAsync(Ct),r=>Assert.Equal("SyntheticUnverified",r.Provenance));
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        db.DailyOperationRevisions.Add(new(){TenantId=foreign.TenantId,VesselId=foreign.Id,RecordId=id,Kind="Noon",Revision=2,RecordVersion=version,
            FieldsJson="{}",Hash=hash,SourceVersion=original.SourceVersion,Provenance="SyntheticUnverified",SavedAt=DateTimeOffset.UtcNow});
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync(Ct));
        Assert.Equal("23503",PostgresFailure.Provider(error)?.SqlState);
    }
}
