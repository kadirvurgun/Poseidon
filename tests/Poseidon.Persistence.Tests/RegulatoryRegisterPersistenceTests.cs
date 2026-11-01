using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class RegulatoryRegisterPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private SeedData data = null!;
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? data.Tenants[0].Id, new("synthetic-register-actor", "User", "register-test"));
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(Ct); data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db, data, Ct);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    [Fact]
    public async Task Register_reseed_preserves_operator_revision_and_only_actual_imports_are_audited()
    {
        var id = RegulatoryRegisterFixtures.SourceId(data.Tenants[0].Id);
        await using (var db = Db()) {
            var d = await db.RegisterDrafts.SingleAsync(x => x.Id == id, Ct);
            d.ContentJson = RegisterStorage.Serialize(new() { Title = new("Known", "Operator declaration") }); d.Revision++;
            db.RegisterRevisions.Add(d.Snapshot(null, null)); await db.SaveChangesAsync(Ct);
        }
        int auditCount;
        await using (var db = Import()) { auditCount = await db.AuditEvents.CountAsync(Ct); await SeedImporter.ImportAsync(db, SeedValidationTests.Demo(), Ct); }
        await using var verify = Import(); Assert.Equal(auditCount, await verify.AuditEvents.CountAsync(Ct));
        Assert.Equal(4, await verify.RegisterDrafts.CountAsync(Ct)); Assert.Equal(5, await verify.RegisterRevisions.CountAsync(Ct));
        Assert.Equal("Operator declaration", RegisterStorage.Deserialize((await verify.RegisterDrafts.SingleAsync(x => x.Id == id, Ct)).ContentJson).Title.Value);
        Assert.All(await verify.RegisterDrafts.ToArrayAsync(Ct), x => Assert.Equal("SyntheticUnverified", x.Provenance));
    }
    [Fact]
    public async Task Register_exact_source_fk_rejects_cross_tenant_and_wrong_kind_with_atomic_audit_rollback()
    {
        await using var db = Db(); var count = await db.AuditEvents.CountAsync(Ct);
        var d = new RegisterDraft { Id = Guid.NewGuid(), TenantId = db.TenantId, Kind = "Requirement", ContentJson = RegisterStorage.Serialize(new()), Provenance = "DeclaredUnverified", Revision = 1, SavedAt = DateTimeOffset.UtcNow };
        db.RegisterDrafts.Add(d); db.RegisterRevisions.Add(d.Snapshot(RegulatoryRegisterFixtures.SourceId(data.Tenants[1].Id), 1));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)); Assert.IsType<Npgsql.PostgresException>(ex.InnerException);
        await using var verify = Db(); Assert.Equal(count, await verify.AuditEvents.CountAsync(Ct)); Assert.False(await verify.RegisterDrafts.AnyAsync(x => x.Id == d.Id, Ct));
        db.ChangeTracker.Clear(); db.RegisterDrafts.Add(d); var bad = d.Snapshot(RegulatoryRegisterFixtures.RequirementId(db.TenantId), 1); bad.SourceKind = "Requirement"; db.RegisterRevisions.Add(bad);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Register_revision_is_append_only_in_context_and_postgresql()
    {
        await using var db = Db(); var revision = await db.RegisterRevisions.FirstAsync(Ct); revision.ContentJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        foreach (var sql in new[] { "UPDATE core.\"RegisterRevisions\" SET \"Hash\"='forged'", "DELETE FROM core.\"RegisterRevisions\"", "TRUNCATE core.\"RegisterRevisions\"" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));
    }
    [Fact]
    public async Task Register_manual_transaction_rollback_removes_audit_history_and_receipt()
    {
        await using var db = Db(); var before = await db.AuditEvents.CountAsync(Ct); var id = Guid.NewGuid();
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            var d = new RegisterDraft { Id = id, TenantId = db.TenantId, Kind = "Source", Revision = 1, ContentJson = RegisterStorage.Serialize(new()), Provenance = "DeclaredUnverified", SavedAt = DateTimeOffset.UtcNow };
            db.RegisterDrafts.Add(d); db.RegisterRevisions.Add(d.Snapshot(null, null)); await db.SaveChangesAsync(Ct);
            db.CommandReceipts.Add(new() { TenantId = db.TenantId, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "SourceDraftSave", Fingerprint = new string('A', 64), ResourceId = id, CommittedAt = d.SavedAt });
            await db.SaveChangesAsync(Ct); await tx.RollbackAsync(Ct);
        }
        await using var verify = Db(); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
        Assert.False(await verify.RegisterRevisions.AnyAsync(x => x.Id == id, Ct)); Assert.False(await verify.CommandReceipts.AnyAsync(x => x.ResourceId == id, Ct));
    }
}
