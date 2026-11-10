using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// Bounded real-host profile. Counts PostgreSQL backend identities observed by
// actual audited HTTP reads; it never omits seeding, migrations or assertions.
public sealed partial class DirectoryWorkspaceTests
{
    private sealed class OwnedPoolCommandCounter:DbCommandInterceptor
    {
        private readonly ConcurrentDictionary<int,byte> backends=new();
        private int statements;
        internal int BackendCount=>backends.Count;
        internal int Statements=>Volatile.Read(ref statements);
        internal void Reset(){backends.Clear();Interlocked.Exchange(ref statements,0);}
        private void Observe(DbCommand command){if(command.Connection is NpgsqlConnection c){backends.TryAdd(c.ProcessID,0);Interlocked.Increment(ref statements);}}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
            {Observe(command);return ValueTask.FromResult(result);}
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default)
            {Observe(command);return ValueTask.FromResult(result);}
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<object> result,CancellationToken cancellationToken=default)
            {Observe(command);return ValueTask.FromResult(result);}
    }

    [Fact]
    public async Task Owned_database_pool_profile_of_ten_actual_audit_reads_preserves_business_history()
    {
        var counter=new OwnedPoolCommandCounter();await using var host=Host(auditRead:true,interceptor:counter);using var client=Client(host);
        await Token(client);var warm=(await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=1",Ct))!;Assert.NotEmpty(warm.Items);
        await using var before=Db();var business=await before.AuditEvents.CountAsync(Ct);counter.Reset();var watch=Stopwatch.StartNew();
        for(var i=0;i<10;i++) {
            var page=(await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=1",Ct))!;
            Assert.Equal(warm.Total,page.Total);Assert.Single(page.Items);
        }
        watch.Stop();Assert.True(counter.Statements>=10);Assert.True(counter.BackendCount>0);
        Assert.Equal(business,await before.AuditEvents.CountAsync(Ct));
        TestContext.Current.TestOutputHelper!.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
            profile="ten_actual_audit_reads",pooling=new NpgsqlConnectionStringBuilder(postgres.GetConnectionString()).Pooling,
            requests=10,statements=counter.Statements,physicalBackends=counter.BackendCount,elapsedMilliseconds=watch.ElapsedMilliseconds}));
    }
}
