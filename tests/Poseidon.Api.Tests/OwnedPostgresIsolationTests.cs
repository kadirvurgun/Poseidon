using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

[Collection("directory-postgres")]
[Trait("Category", "Integration")]
public sealed class OwnedPostgresIsolationTests(OwnedPostgresFixture fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private static PoseidonDbContext Admin(OwnedTestDatabase database, SeedData? seed = null) => new(
        SeedImporter.Options(database.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);

    [Fact]
    public async Task Simultaneous_databases_isolate_migrations_rows_receipts_triggers_and_audit()
    {
        var leases = await Task.WhenAll(fixture.CreateAsync(Ct), fixture.CreateAsync(Ct));
        await using var first = leases[0]; await using var second = leases[1];
        Assert.NotEqual(first.Name, second.Name);
        var seed = SeedData.Load(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json"));
        await using (var db = Admin(first, seed)) await db.Database.MigrateAsync(Ct);
        // The second database must remain genuinely fresh, not a schema on the same connection.
        await using (var connection = new NpgsqlConnection(second.GetConnectionString())) {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("SELECT to_regclass('core.\"Tenants\"') IS NULL AND to_regclass('public.\"__EFMigrationsHistory\"') IS NULL", connection);
            Assert.True((bool)(await command.ExecuteScalarAsync(Ct))!);
        }
        await using (var db = Admin(second, SeedData.Load(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json")))) await db.Database.MigrateAsync(Ct);
        await using var a = Admin(first); await using var b = Admin(second);
        Assert.Equal(await a.Database.GetAppliedMigrationsAsync(Ct), await b.Database.GetAppliedMigrationsAsync(Ct));
        var marker = Guid.NewGuid(); var tenant = seed.Tenants[0].Id;
        a.Companies.Add(new() { TenantId = tenant, Id = marker, Code = "ISOLATED", Name = "Synthetic isolated company" });
        a.CommandReceipts.Add(new() { TenantId = tenant, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "CompanyCreate", ResourceId = marker, Fingerprint = new string('A', 64), CommittedAt = DateTimeOffset.UtcNow });
        await a.SaveChangesAsync(Ct);
        Assert.False(await b.Companies.AnyAsync(x => x.Id == marker, Ct));
        Assert.False(await b.CommandReceipts.AnyAsync(Ct));
        Assert.False(await b.AuditEvents.AnyAsync(x => x.ResourceId == marker, Ct));
        // Database-local trigger on a mutable business table may not affect the simultaneous second DB.
        await using (var connection = new NpgsqlConnection(first.GetConnectionString())) {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("CREATE FUNCTION core.test_owned_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned failure'; END $$; CREATE TRIGGER test_owned_reject BEFORE INSERT ON core.\"Companies\" FOR EACH ROW EXECUTE FUNCTION core.test_owned_reject();", connection);
            await command.ExecuteNonQueryAsync(Ct);
        }
        var secondMarker = Guid.NewGuid();
        b.Companies.Add(new() { TenantId = tenant, Id = secondMarker, Code = "SECOND", Name = "Synthetic second company" });
        await b.SaveChangesAsync(Ct);
        a.Companies.Add(new() { TenantId = tenant, Id = secondMarker, Code = "SECOND", Name = "Synthetic rejected company" });
        await Assert.ThrowsAsync<DbUpdateException>(() => a.SaveChangesAsync(Ct));
        a.ChangeTracker.Clear();
        Assert.False(await a.AuditEvents.AnyAsync(x => x.ResourceId == secondMarker, Ct));
        Assert.True(await b.AuditEvents.AnyAsync(x => x.ResourceId == secondMarker, Ct));
        // Database-level append-only protection remains present independently in each migrated DB.
        foreach (var database in leases) {
            await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("UPDATE audit.\"AuditEvents\" SET \"Action\"='Deleted'", connection);
            await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
            command.CommandText = "UPDATE audit.\"AuditEvents\" SET \"CapabilityContext\"='tamper'";
            await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
        }
    }

    [Fact]
    public async Task Cleanup_refuses_non_owned_valid_names_and_injected_identifiers_and_preserves_other_lease()
    {
        await using var owned = await fixture.CreateAsync(Ct);
        foreach (var name in new[] { "postgres", "test_x; DROP DATABASE postgres", "test_" + Guid.NewGuid().ToString("N"), owned.Name }) {
            // Even a valid known database name cannot be deleted through a different lease object.
            var forged = new OwnedTestDatabase(fixture, name, "");
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await forged.DisposeAsync());
        }
        await using var connection = new NpgsqlConnection(owned.GetConnectionString());
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT current_database()", connection);
        Assert.Equal(owned.Name, await command.ExecuteScalarAsync(Ct));
    }
}
