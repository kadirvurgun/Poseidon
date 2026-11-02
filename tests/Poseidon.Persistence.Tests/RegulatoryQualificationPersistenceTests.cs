using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class RegulatoryQualificationPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData data = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? data.Tenants[0].Id, new("synthetic-inspector", "User", "qualification-test"));
    public async ValueTask InitializeAsync() { await postgres.StartAsync(Ct); data = SeedValidationTests.Demo(); await using var db = Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db, data, Ct); }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    [Fact]
    public async Task Seed_is_incomplete_tenant_local_and_idempotent_without_appointments_attestations_or_evaluations()
    {
        await using var db = Db(); var e = Assert.Single(await db.RegulatoryQualificationEvents.ToArrayAsync(Ct));
        Assert.Equal("DRAFT", e.State); Assert.Equal(Guid.Empty, e.AuthorityId); Assert.Empty(e.SourceHash); Assert.Equal("null", e.ContractJson);
        Assert.Empty(await db.RegulatoryFactAttestations.ToArrayAsync(Ct)); Assert.Empty(await db.ApplicabilityEvaluations.ToArrayAsync(Ct));
        var auditCount = await db.AuditEvents.LongCountAsync(Ct);
        await using (var import = Import()) { await SeedImporter.ImportAsync(import, data, Ct); await SeedImporter.ImportAsync(import, data, Ct); }
        Assert.Equal(auditCount, await db.AuditEvents.LongCountAsync(Ct));
        Assert.Single(await db.RegulatoryQualificationEvents.ToArrayAsync(Ct));
        await using var other = Db(data.Tenants[1].Id); Assert.DoesNotContain(await other.RegulatoryQualificationEvents.ToArrayAsync(Ct), x => x.TenantId == e.TenantId);
    }
    [Fact]
    public async Task Immutable_source_history_is_rejected_by_EF_and_database_and_cross_tenant_FK_is_enforced()
    {
        await using (var db = Db()) {
            var e = await db.RegulatoryQualificationEvents.SingleAsync(Ct); e.ContentJson = "{}";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
        await using (var db = Db()) {
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE core.\"RegulatoryQualificationEvents\" SET \"State\" = 'IN_FORCE'", Ct));
            db.RegulatoryQualificationHeads.Add(new() { TenantId = data.Tenants[0].Id, RuleId = ApplicabilityFixtures.Id(data.Tenants[1].Id), Revision = 1 });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }
        await using var check = Db(); Assert.Equal("DRAFT", (await check.RegulatoryQualificationEvents.SingleAsync(Ct)).State);
    }
}
