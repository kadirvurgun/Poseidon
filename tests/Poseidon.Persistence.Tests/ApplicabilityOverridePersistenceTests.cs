using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-040 persistence: optional synthetic fixture is terminal/withdrawn and idempotent; no approval, grant or
// ACTIVE override is seedable; history is append-only; tenant isolation holds. Authored; execution DEFERRED.
[Trait("Category", "Integration")]
[Trait("Requirement", "DEV-040")]
public sealed class ApplicabilityOverridePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData data = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? data.Tenants[0].Id, new("synthetic-inspector", "User", "override-test"));
    public async ValueTask InitializeAsync() {
        await postgres.StartAsync(Ct); data = SeedValidationTests.Demo(); await using var db = Import(); await db.Database.MigrateAsync(Ct);
        await SeedImporter.ImportAsync(db, data, Ct);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        await ApplicabilityOverrideFixtures.AddMissingAsync(db, await db.Vessels.ToArrayAsync(Ct), Ct); await tx.CommitAsync(Ct);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task Optional_fixture_is_withdrawn_unapproved_idempotent_and_tenant_scoped() {
        await using var db = Db();
        var rows = await db.ApplicabilityOverrides.ToArrayAsync(Ct);
        Assert.NotEmpty(rows);
        Assert.All(rows, r => {
            Assert.Equal(("REVOKED", 2L, (Guid?)null, (Guid?)null), (r.State, r.Sequence, r.ApproverId, r.AuthorityGrantId));
            Assert.Equal(ApplicabilityOverrideFixtures.Rationale, r.Rationale); Assert.Equal("REQUIRE_ADDITIONAL_CONTROL", r.Action);
            Assert.Equal(data.Tenants[0].Id, r.TenantId);
        });
        Assert.Equal(rows.Length * 2, await db.ApplicabilityOverrideEvents.CountAsync(Ct));
        Assert.Empty(await db.ComplianceCommandReceipts.ToArrayAsync(Ct));
        var audits = await db.AuditEvents.LongCountAsync(Ct);
        await using (var import = Import()) {
            var vessels = await import.Vessels.ToArrayAsync(Ct);
            await ApplicabilityOverrideFixtures.AddMissingAsync(import, vessels, Ct);
            await ApplicabilityOverrideFixtures.AddMissingAsync(import, vessels, Ct);
        }
        await using var check = Db();
        Assert.Equal(rows.Length, await check.ApplicabilityOverrides.CountAsync(Ct));
        Assert.Equal(audits, await check.AuditEvents.LongCountAsync(Ct));
        await using var other = Db(data.Tenants[1].Id);
        Assert.DoesNotContain(await other.ApplicabilityOverrides.Select(o => o.Id).ToArrayAsync(Ct), id => rows.Any(r => r.Id == id));
    }

    [Fact]
    public async Task Active_without_independent_approver_and_grant_and_history_rewrite_are_rejected() {
        await using var db = Db();
        var row = await db.ApplicabilityOverrides.FirstAsync(Ct);
        await using var connection = new Npgsql.NpgsqlConnection(postgres.GetConnectionString()); await connection.OpenAsync(Ct);
        async Task Sql(string sql) { await using var command = new Npgsql.NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(Ct); }
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"UPDATE core.\"ApplicabilityOverrides\" SET \"State\" = 'ACTIVE' WHERE \"Id\" = '{row.Id}'"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"UPDATE core.\"ApplicabilityOverrides\" SET \"State\" = 'ACTIVE', \"ApproverId\" = \"AuthorId\", \"AuthorityGrantId\" = gen_random_uuid() WHERE \"Id\" = '{row.Id}'"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"UPDATE core.\"ApplicabilityOverrideEvents\" SET \"EventCode\" = 'APPROVE' WHERE \"OverrideId\" = '{row.Id}'"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"DELETE FROM core.\"ApplicabilityOverrideEvents\" WHERE \"OverrideId\" = '{row.Id}'"));
        // The audited path refuses a silent state change without its exact event.
        row.State = "IN_REVIEW";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }
}
