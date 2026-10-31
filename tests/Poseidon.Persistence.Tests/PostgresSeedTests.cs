using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class PostgresSeedTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18.6").Build();
    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() => await container.DisposeAsync();
    private PoseidonDbContext Admin(SeedData? seed = null) => new(
        SeedImporter.Options(container.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), administrativeImport: true);
    private PoseidonDbContext Tenant(Guid id) => new(
        SeedImporter.Options(container.GetConnectionString()), id, new ActorContext("test-user", "User", "test-request"));

    [Fact]
    public async Task Migrations_seed_is_idempotent_preserves_edits_and_audits_once()
    {
        var data = SeedValidationTests.Demo();
        await using (var db = Admin(data)) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using (var db = Admin())
        {
            Assert.Equal(2, await db.Tenants.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(2, await db.Companies.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(3, await db.Vessels.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(3, await db.VesselProfiles.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await db.TugProfiles.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(17, await db.AuditEvents.CountAsync(TestContext.Current.CancellationToken));
            Assert.All(await db.AuditEvents.ToListAsync(TestContext.Current.CancellationToken), e => Assert.Equal("system:seed-import", e.ActorId));
        }
        await using (var db = Tenant(data.Tenants[0].Id))
        {
            (await db.Vessels.FirstAsync(TestContext.Current.CancellationToken)).Name = "Operator edited name";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        // A new manifest instance also proves the second import does not rely on tracked objects.
        await using (var db = Admin(SeedValidationTests.Demo())) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using (var db = Admin())
        {
            Assert.Equal(3, await db.Vessels.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(18, await db.AuditEvents.CountAsync(TestContext.Current.CancellationToken));
            Assert.Contains(await db.Vessels.ToListAsync(TestContext.Current.CancellationToken), v => v.Name == "Operator edited name");
            var audit = Assert.Single(await db.AuditEvents.Where(e => e.ActorId == "test-user").ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Modified", audit.Action);
        }
    }

    [Fact]
    public async Task Tenant_reads_and_writes_are_scoped()
    {
        var data = SeedValidationTests.Demo();
        await using (var db = Admin(data)) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var tenant = Tenant(data.Tenants[1].Id);
        Assert.Single(await tenant.Tenants.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await tenant.Vessels.ToListAsync(TestContext.Current.CancellationToken));
        Assert.All(await tenant.AuditEvents.ToListAsync(TestContext.Current.CancellationToken), e => Assert.Equal(data.Tenants[1].Id, e.TenantId));
        tenant.Companies.Add(new Company { Id = Guid.NewGuid(), TenantId = data.Tenants[0].Id, Code = "DENIED", Name = "Denied" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Audit_cannot_be_modified_through_context_or_raw_sql()
    {
        var data = SeedValidationTests.Demo();
        await using (var db = Admin(data)) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var tenant = Tenant(data.Tenants[0].Id);
        var audit = await tenant.AuditEvents.FirstAsync(TestContext.Current.CancellationToken);
        audit.Action = "Tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => tenant.SaveChangesAsync(TestContext.Current.CancellationToken));
        tenant.ChangeTracker.Clear();
        foreach (var sql in new[] { "DELETE FROM audit.\"AuditEvents\"", "UPDATE audit.\"AuditEvents\" SET \"Action\" = 'Tampered'", "TRUNCATE audit.\"AuditEvents\"" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => tenant.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Invalid_manifest_does_not_insert_partial_seed_rows()
    {
        await using (var db = Admin()) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var invalid = SeedValidationTests.Demo();
        invalid.Vessels[0].CompanyId = Guid.NewGuid();
        await using var seed = Admin();
        await Assert.ThrowsAsync<InvalidDataException>(() => SeedImporter.ImportAsync(seed, invalid, TestContext.Current.CancellationToken));
        Assert.Empty(await seed.Tenants.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await seed.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Database_rejects_cross_tenant_foreign_key_and_rolls_back_audit()
    {
        var data = SeedValidationTests.Demo();
        await using (var db = Admin(data)) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using (var tenant = Tenant(data.Tenants[0].Id))
        {
            tenant.Vessels.Add(new Vessel { Id = Guid.NewGuid(), TenantId = data.Tenants[0].Id, CompanyId = data.Companies[1].Id, Code = "BAD-FK", Name = "Bad reference", Kind = VesselKind.Ship });
            await Assert.ThrowsAsync<DbUpdateException>(() => tenant.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
        await using var verify = Admin();
        Assert.Equal(3, await verify.Vessels.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(17, await verify.AuditEvents.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Synchronous_migration_path_also_imports_seed()
    {
        using (var db = Admin(SeedValidationTests.Demo())) db.Database.Migrate();
        await using var verify = Admin();
        Assert.Equal(3, await verify.Vessels.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_initializers_do_not_duplicate_seed_or_audit()
    {
        await using (var db = Admin()) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var db = Admin(SeedValidationTests.Demo());
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }));
        await using var verify = Admin();
        Assert.Equal(3, await verify.Vessels.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(17, await verify.AuditEvents.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Import_respects_outer_transaction_rollback()
    {
        await using (var db = Admin()) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using (var db = Admin())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await SeedImporter.ImportAsync(db, SeedValidationTests.Demo(), TestContext.Current.CancellationToken);
            Assert.Equal(3, await db.Vessels.CountAsync(TestContext.Current.CancellationToken));
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
        await using var verify = Admin();
        Assert.Empty(await verify.Tenants.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verify.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Failed_write_can_be_corrected_without_duplicate_or_forged_audit()
    {
        var data = SeedValidationTests.Demo();
        await using (var db = Admin(data)) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using (var tenant = Tenant(data.Tenants[0].Id))
        {
            var vessel = new Vessel { Id = Guid.NewGuid(), TenantId = data.Tenants[0].Id, CompanyId = Guid.NewGuid(), Code = "RETRY", Name = "Retry vessel", Kind = VesselKind.Ship };
            tenant.Vessels.Add(vessel);
            await Assert.ThrowsAsync<DbUpdateException>(() => tenant.SaveChangesAsync(TestContext.Current.CancellationToken));
            vessel.CompanyId = data.Companies[0].Id;
            await tenant.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var verify = Admin();
        Assert.Equal(4, await verify.Vessels.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(18, await verify.AuditEvents.CountAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verify.AuditEvents.Where(e => e.ActorId == "test-user").ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Stale_xmin_write_is_rejected_without_audit_of_failed_change()
    {
        var data = SeedValidationTests.Demo();
        await using (var db = Admin(data)) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var first = Tenant(data.Tenants[0].Id);
        await using var stale = Tenant(data.Tenants[0].Id);
        var id = data.Vessels[0].Id;
        var firstVessel = await first.Vessels.SingleAsync(v => v.Id == id, TestContext.Current.CancellationToken);
        var staleVessel = await stale.Vessels.SingleAsync(v => v.Id == id, TestContext.Current.CancellationToken);
        firstVessel.Name = "Accepted change";
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        staleVessel.Name = "Stale overwrite";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(TestContext.Current.CancellationToken));
        await using var verify = Admin();
        Assert.Equal("Accepted change", (await verify.Vessels.SingleAsync(v => v.Id == id, TestContext.Current.CancellationToken)).Name);
        Assert.Equal(18, await verify.AuditEvents.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Command_receipts_are_audited_tenant_scoped_immutable_and_never_seeded()
    {
        var data = SeedValidationTests.Demo();
        await using (var setup = Admin(data)) await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var db = Tenant(data.Tenants[0].Id);
        Assert.Empty(await db.CommandReceipts.ToArrayAsync(TestContext.Current.CancellationToken));
        var receipt = new CommandReceipt { TenantId = data.Tenants[0].Id, ActorId = Guid.NewGuid(), Key = new string('K', 80),
            Action = "CompanyCreate", Fingerprint = new string('A', 64), ResourceId = data.Companies[0].Id, CommittedAt = DateTimeOffset.UtcNow };
        db.CommandReceipts.Add(receipt); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Contains(await db.AuditEvents.ToArrayAsync(TestContext.Current.CancellationToken), e => e.EntityType == nameof(CommandReceipt) && e.EntityId == receipt.ResourceId.ToString());
        await using (var other = Tenant(data.Tenants[1].Id)) Assert.Empty(await other.CommandReceipts.ToArrayAsync(TestContext.Current.CancellationToken));
        receipt.Action = "Tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        db.ChangeTracker.Clear();
        foreach (var sql in new[] { "DELETE FROM core.\"CommandReceipts\"", "UPDATE core.\"CommandReceipts\" SET \"Action\" = 'Tampered'", "TRUNCATE core.\"CommandReceipts\"" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken));
        await using (var reseed = Admin(SeedValidationTests.Demo())) await reseed.Database.MigrateAsync(TestContext.Current.CancellationToken);
        Assert.Single(await db.CommandReceipts.ToArrayAsync(TestContext.Current.CancellationToken));
    }
}
