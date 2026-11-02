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
        await reader.DisposeAsync();
        await AssertRelations(connection, tables, ct);
    }

    private static async Task AssertRelations(NpgsqlConnection connection, ITable[] tables, CancellationToken ct)
    {
        // Compare ordered columns and referential actions, not just convention-derived names.
        await using var command = new NpgsqlCommand("""
            SELECT ns.nspname, t.relname,
              array_to_string(ARRAY(SELECT a.attname FROM unnest(c.conkey) WITH ORDINALITY k(n,i)
                JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.n ORDER BY k.i), ','),
              pns.nspname, p.relname,
              array_to_string(ARRAY(SELECT a.attname FROM unnest(c.confkey) WITH ORDINALITY k(n,i)
                JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.n ORDER BY k.i), ','), c.confdeltype::text
            FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
              JOIN pg_class p ON p.oid=c.confrelid JOIN pg_namespace pns ON pns.oid=p.relnamespace
            WHERE c.contype='f' AND ns.nspname NOT IN ('pg_catalog','information_schema')
            """, connection);
        var foreignKeys = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) foreignKeys.Add(string.Join('|', Enumerable.Range(0, 7).Select(reader.GetString)));
        command.CommandText = """
            SELECT ns.nspname, t.relname,
              array_to_string(ARRAY(SELECT a.attname FROM unnest(i.indkey::smallint[]) WITH ORDINALITY k(n,j)
                JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.n WHERE k.j<=i.indnkeyatts ORDER BY k.j), ','),
              i.indisunique, pg_get_expr(i.indpred,i.indrelid)
            FROM pg_index i JOIN pg_class t ON t.oid=i.indrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
            WHERE ns.nspname NOT IN ('pg_catalog','information_schema') AND i.indisvalid AND i.indisready
            """;
        var indexes = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) indexes.Add(string.Join('|', reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.IsDBNull(4) ? "" : reader.GetString(4).Trim('(', ')')));
        var missing = new List<string>();
        foreach (var table in tables) {
            foreach (var fk in table.ForeignKeyConstraints) {
                var action = fk.OnDeleteAction switch {
                    Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict => "r",
                    Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade => "c",
                    Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull => "n",
                    Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetDefault => "d", _ => "a"
                };
                var key = string.Join('|', table.Schema, table.Name, string.Join(',', fk.Columns.Select(c => c.Name)),
                    fk.PrincipalTable.Schema, fk.PrincipalTable.Name, string.Join(',', fk.PrincipalColumns.Select(c => c.Name)), action);
                if (!foreignKeys.Contains(key)) missing.Add("FK|" + key);
            }
            foreach (var index in table.Indexes) {
                var key = string.Join('|', table.Schema, table.Name, string.Join(',', index.Columns.Select(c => c.Name)), index.IsUnique, index.Filter ?? "");
                if (!indexes.Contains(key)) missing.Add("INDEX|" + key);
            }
        }
        Assert.True(missing.Count == 0, "Missing migrated relations:\n" + string.Join('\n', missing));
    }
}
