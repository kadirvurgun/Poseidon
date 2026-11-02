using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

// FOUNDATION-ACCEPTANCE-BUNDLE-01 / DEV-005, DEV-002: actual DDL, not snapshot equality alone.
[Trait("Category", "Integration")]
public sealed class FoundationModelParityTests
{
    [Fact]
    public async Task Retained_additive_migrations_match_actual_model_columns_and_nullability()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:18.6@sha256:979bccfde35445db618c65107d27d08c6c21339d8dc3ad4c830c6633947be0e4").Build();
        await postgres.StartAsync(ct);
        await using var db = new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options, Guid.Empty, ActorContext.SeedImport(), true);
        Assert.False(db.Database.HasPendingModelChanges());
        await db.Database.MigrateAsync(ct);
        var seed = SeedValidationTests.Demo();
        await Seeding.SeedImporter.ImportAsync(db, seed, ct);
        var audits = await db.AuditEvents.CountAsync(ct);
        Assert.True(audits > 0);
        await Seeding.SeedImporter.ImportAsync(db, seed, ct);
        Assert.Equal(audits, await db.AuditEvents.CountAsync(ct));
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT table_schema, table_name, column_name, is_nullable, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema NOT IN ('pg_catalog','information_schema')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var actual = new Dictionary<(string, string, string), (bool Nullable, int? Length)>();
        while (await reader.ReadAsync(ct))
            actual.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)), (reader.GetString(3) == "YES", reader.IsDBNull(4) ? null : reader.GetInt32(4)));
        var tables = db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables.ToArray();
        Assert.NotEmpty(tables);
        foreach (var table in tables)
        foreach (var column in table.Columns.Where(c => c.Name != "xmin"))
        {
            var key = (table.Schema!, table.Name, column.Name);
            Assert.True(actual.TryGetValue(key, out var nullable), $"Missing migrated column {key}");
            Assert.Equal(column.IsNullable, nullable.Nullable);
            if (column.StoreType.StartsWith("character varying(", StringComparison.Ordinal))
                Assert.Equal(int.Parse(column.StoreType[18..^1], System.Globalization.CultureInfo.InvariantCulture), nullable.Length);
        }
    }
}
