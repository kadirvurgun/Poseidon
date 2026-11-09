using Npgsql;
using Xunit;

namespace Poseidon.Api.Tests;

[Collection("directory-postgres")]
[Trait("Category","Integration")]
public sealed class OwnedPostgresPoolingTests(OwnedPostgresFixture fixture)
{
    private CancellationToken Ct=>TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owned_pool_reuses_transports_resets_session_state_and_never_crosses_database_leases()
    {
        await using var first=await fixture.CreateAsync(Ct);await using var second=await fixture.CreateAsync(Ct);
        var settings=new NpgsqlConnectionStringBuilder(first.GetConnectionString());Assert.True(settings.Pooling);Assert.InRange(settings.MaxPoolSize,2,16);Assert.Equal(0,settings.MinPoolSize);
        int original;
        await using(var connection=new NpgsqlConnection(first.GetConnectionString())) {
            await connection.OpenAsync(Ct);original=connection.ProcessID;
            await using var state=new NpgsqlCommand("SET application_name='synthetic-session-marker'",connection);await state.ExecuteNonQueryAsync(Ct);
        }
        await using(var returned=new NpgsqlConnection(first.GetConnectionString())) {
            await returned.OpenAsync(Ct);Assert.Equal(original,returned.ProcessID);
            await using var database=new NpgsqlCommand("SELECT current_database()",returned);Assert.Equal(first.Name,await database.ExecuteScalarAsync(Ct));
            await using var reset=new NpgsqlCommand("SHOW application_name",returned);Assert.Equal(settings.ApplicationName??"",await reset.ExecuteScalarAsync(Ct));
        }
        await using var other=new NpgsqlConnection(second.GetConnectionString());await other.OpenAsync(Ct);Assert.NotEqual(original,other.ProcessID);
        await using var scope=new NpgsqlCommand("SELECT current_database()",other);Assert.Equal(second.Name,await scope.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task Releasing_one_owned_database_clears_its_idle_sessions_without_clearing_another_lease_pool()
    {
        await using var first=await fixture.CreateAsync(Ct);await using var second=await fixture.CreateAsync(Ct);
        await using(var idle=new NpgsqlConnection(first.GetConnectionString())){await idle.OpenAsync(Ct);}
        int otherBackend;
        await using(var idle=new NpgsqlConnection(second.GetConnectionString())){await idle.OpenAsync(Ct);otherBackend=idle.ProcessID;}
        await first.DisposeAsync();Assert.Throws<ObjectDisposedException>(()=>first.GetConnectionString());
        await using var other=new NpgsqlConnection(second.GetConnectionString());await other.OpenAsync(Ct);Assert.Equal(otherBackend,other.ProcessID);
        await using(var verify=new NpgsqlCommand("SELECT NOT EXISTS (SELECT 1 FROM pg_database WHERE datname=@name) AND NOT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=@name)",other)) {
            verify.Parameters.AddWithValue("name",first.Name);Assert.True((bool)(await verify.ExecuteScalarAsync(Ct))!);
        }
        await using var third=await fixture.CreateAsync(Ct);Assert.NotEqual(first.Name,third.Name);
        await using var next=new NpgsqlConnection(third.GetConnectionString());await next.OpenAsync(Ct);
        await using var scope=new NpgsqlCommand("SELECT current_database()",next);Assert.Equal(third.Name,await scope.ExecuteScalarAsync(Ct));Assert.NotEqual(otherBackend,next.ProcessID);
    }
}
