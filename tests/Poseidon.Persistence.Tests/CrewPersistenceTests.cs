using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class CrewPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData data = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? data.Tenants[0].Id, new("synthetic-crew-test", "User", "crew-test"));
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(Ct); data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db, data, Ct);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    private CrewAssignment Assignment(Guid vessel, Guid person) => new() {
        TenantId = data.Tenants[0].Id, Id = Guid.NewGuid(), VesselId = vessel, PersonId = person,
        From = new(2026, 11, 1), To = new(2026, 12, 1)
    };
    [Fact]
    public async Task Protected_identity_database_blocks_downgrade_plaintext_and_history_mutation_and_keeps_tenant_foreign_key() {
        var vessel = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id);
        await using var db = Db(); var person = await db.Persons.SingleAsync(x => x.Id == vessel.Id, Ct);
        // Empty protected declaration: no fake ciphertext, verified identity or credential is seeded.
        person.IdentityStorageState = "ProtectedV1"; person.IdentityRevision = 1; person.FullName = null; person.EmployeeNo = null;
        db.CrewIdentityRevisions.Add(new() { TenantId = db.TenantId, PersonId = person.Id, Revision = 1,
            PolicyVersion = "synthetic-empty", Purpose = "synthetic-test", Operation = "Declaration", SavedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
        // Isolated test database only. Deliberately bypass EF to exercise independent DB guards.
        foreach (var sql in new[] {
            "UPDATE crew.\"Persons\" SET \"IdentityStorageState\" = 'LegacyUnavailable' WHERE \"IdentityStorageState\" = 'ProtectedV1'",
            "UPDATE crew.\"Persons\" SET \"FullName\" = 'FICTIONAL forbidden plaintext' WHERE \"IdentityStorageState\" = 'ProtectedV1'",
            "UPDATE crew.\"IdentityRevisions\" SET \"Operation\" = 'tampered'", "DELETE FROM crew.\"IdentityRevisions\"", "TRUNCATE crew.\"IdentityRevisions\""
        }) await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));
        var foreign = data.Vessels.First(v => v.TenantId != db.TenantId);
        db.CrewIdentityRevisions.Add(new() { TenantId = db.TenantId, PersonId = foreign.Id, Revision = 1,
            PolicyVersion = "synthetic-empty", Purpose = "synthetic-test", Operation = "Declaration", SavedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        await using var verify = Db(foreign.TenantId); Assert.Empty(await verify.CrewIdentityRevisions.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Crew_reseed_preserves_operator_edits_and_never_creates_eligibility_or_receipts()
    {
        var vessel = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id);
        await using (var db = Db()) {
            var p = await db.Persons.SingleAsync(x => x.Id == vessel.Id, Ct); p.FullName = "FICTIONAL operator edit";
            var c = await db.CrewAssignments.SingleAsync(x => x.Id == vessel.Id, Ct); c.WatchGroup = "Synthetic operator group";
            c.Revision++; db.CrewAssignmentRevisions.Add(c.Snapshot()); await db.SaveChangesAsync(Ct);
        }
        int count;
        await using (var db = Import()) { count = await db.AuditEvents.CountAsync(Ct); await SeedImporter.ImportAsync(db, SeedValidationTests.Demo(), Ct); }
        await using var verify = Import();
        Assert.Equal(count, await verify.AuditEvents.CountAsync(Ct)); Assert.Equal(3, await verify.Persons.CountAsync(Ct));
        Assert.Equal("FICTIONAL operator edit", (await verify.Persons.SingleAsync(x => x.Id == vessel.Id, Ct)).FullName);
        Assert.Equal("Synthetic operator group", (await verify.CrewAssignments.SingleAsync(x => x.Id == vessel.Id, Ct)).WatchGroup);
        Assert.All(await verify.CrewAssignments.ToArrayAsync(Ct), x => { Assert.Equal("Draft", x.Status); Assert.Equal("SyntheticUnverified", x.Provenance); Assert.Null(x.From);
            Assert.Null(x.AssignmentPolicyVersion); Assert.Null(x.AssignmentPolicyFingerprint); Assert.Null(x.PositionPolicyVersion); Assert.Null(x.PositionPolicyFingerprint); });
        Assert.Empty(await verify.CommandReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Crew_tenant_and_position_vessel_foreign_keys_roll_back_business_and_audit()
    {
        var local = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id);
        var other = data.Vessels.First(v => v.TenantId != local.TenantId);
        await using var db = Db(); var count = await db.AuditEvents.CountAsync(Ct);
        db.CrewAssignments.Add(Assignment(local.Id, other.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        var wrongVesselPosition = Assignment(local.Id, local.Id); wrongVesselPosition.PositionId = other.Id;
        db.CrewAssignments.Add(wrongVesselPosition); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        await using var verify = Db(); Assert.Equal(count, await verify.AuditEvents.CountAsync(Ct));
        Assert.False(await verify.CrewAssignments.AnyAsync(x => x.Id == wrongVesselPosition.Id, Ct));
        Assert.All(await verify.Persons.ToArrayAsync(Ct), x => Assert.Equal(local.TenantId, x.TenantId));
    }
    [Fact]
    public async Task Database_exclusion_arbitrates_concurrent_overlapping_person_periods()
    {
        var vessel = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id);
        async Task<bool> Reserve() {
            await using var db = Db(); var c = Assignment(vessel.Id, vessel.Id);
            db.CrewAssignments.Add(c); db.CrewAssignmentRevisions.Add(c.Snapshot());
            try { await db.SaveChangesAsync(Ct); return true; } catch (DbUpdateException e) {
                Assert.Equal("23P01", Assert.IsType<Npgsql.PostgresException>(e.InnerException).SqlState); return false;
            }
        }
        var results = await Task.WhenAll(Reserve(), Reserve()); Assert.Single(results.Where(x => x));
        await using var verify = Db(); Assert.Single(await verify.CrewAssignments.Where(x => x.From != null).ToArrayAsync(Ct));
        Assert.Equal(2, await verify.CrewAssignmentRevisions.CountAsync(x => x.VesselId == vessel.Id, Ct));
    }
    [Fact]
    public async Task Stale_xmin_cannot_append_revision_or_audit_and_revision_triggers_block_raw_mutation()
    {
        var vessel = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id);
        await using var first = Db(); await using var stale = Db();
        var a = await first.CrewAssignments.SingleAsync(x => x.Id == vessel.Id, Ct);
        var b = await stale.CrewAssignments.SingleAsync(x => x.Id == vessel.Id, Ct);
        a.Revision++; a.From = new(2026, 11, 1); first.CrewAssignmentRevisions.Add(a.Snapshot()); await first.SaveChangesAsync(Ct);
        var auditCount = await first.AuditEvents.CountAsync(Ct);
        b.Revision += 2; b.From = new(2026, 11, 2); stale.CrewAssignmentRevisions.Add(b.Snapshot());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(Ct));
        await using var verify = Db(); Assert.Equal(auditCount, await verify.AuditEvents.CountAsync(Ct));
        Assert.Equal(2, await verify.CrewAssignmentRevisions.CountAsync(x => x.VesselId == vessel.Id, Ct));
        foreach (var sql in new[] { "DELETE FROM crew.\"AssignmentRevisions\"", "TRUNCATE crew.\"AssignmentRevisions\"", "UPDATE crew.\"AssignmentRevisions\" SET \"Provenance\" = 'tampered'" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => verify.Database.ExecuteSqlRawAsync(sql, Ct));
    }
    [Fact]
    public async Task Explicit_transaction_rollback_removes_person_assignment_history_and_audit_together()
    {
        var vessel = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id); var person = Guid.NewGuid();
        await using var db = Db(); var count = await db.AuditEvents.CountAsync(Ct);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            db.Persons.Add(new() { TenantId = db.TenantId, Id = person });
            var c = Assignment(vessel.Id, person); db.CrewAssignments.Add(c); db.CrewAssignmentRevisions.Add(c.Snapshot());
            await db.SaveChangesAsync(Ct); await tx.RollbackAsync(Ct);
        }
        await using var verify = Db(); Assert.False(await verify.Persons.AnyAsync(x => x.Id == person, Ct));
        Assert.False(await verify.CrewAssignments.AnyAsync(x => x.PersonId == person, Ct));
        Assert.Equal(count, await verify.AuditEvents.CountAsync(Ct));
    }
}
