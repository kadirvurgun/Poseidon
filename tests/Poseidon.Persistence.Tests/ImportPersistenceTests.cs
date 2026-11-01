using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class ImportPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData data = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? data.Tenants[0].Id, new("synthetic-import-test", "User", "import-test"));
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync() { await postgres.StartAsync(Ct); data = SeedValidationTests.Demo(); await using var db = Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db, data, Ct); }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    [Fact]
    public async Task Normalized_private_fixture_is_incomplete_idempotent_and_preserves_operator_edits() {
        var vessel = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id);
        await using (var db = Db()) { var c = await db.Credentials.SingleAsync(x => x.Id == vessel.Id, Ct); c.Capture(new(new(CertificateNumber: "FICTIONAL OPERATOR EDIT"), IssuingCountryNumeric: "999999999999999999999999999999.000000000000000001")); await db.SaveChangesAsync(Ct); }
        int audits; await using (var db = Import()) { audits = await db.AuditEvents.CountAsync(Ct); await SeedImporter.ImportAsync(db, SeedValidationTests.Demo(), Ct); }
        await using var verify = Import(); Assert.Equal(audits, await verify.AuditEvents.CountAsync(Ct)); Assert.Equal(3, await verify.Credentials.CountAsync(Ct));
        Assert.All(await verify.Credentials.ToArrayAsync(Ct), c => { Assert.Equal("Unverified", c.VerificationState); Assert.Equal("Draft", c.Status); Assert.Equal("Unknown", c.AlertState); });
        var edited = await verify.Credentials.SingleAsync(x => x.Id == vessel.Id, Ct); Assert.Equal("FICTIONAL OPERATOR EDIT", edited.CertificateNumber);
        Assert.Equal("999999999999999999999999999999.000000000000000001", edited.IssuingCountryNumeric);
        Assert.Empty(await verify.CommandReceipts.ToArrayAsync(Ct)); Assert.Empty(await verify.ImportBatches.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Composite_assignment_person_vessel_key_rejects_cross_scope_credential_and_rolls_back_audit() {
        var first = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id); var other = data.Vessels.First(v => v.TenantId != first.TenantId);
        await using var db = Db(); var before = await db.AuditEvents.CountAsync(Ct);
        db.Credentials.Add(new() { TenantId = db.TenantId, Id = Guid.NewGuid(), VesselId = first.Id, AssignmentId = other.Id, PersonId = other.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        await using var verify = Db(); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
    }
    [Fact]
    public async Task Credential_ownership_and_revisions_are_immutable_through_audited_writes() {
        await using var db = Db(); var c = await db.Credentials.FirstAsync(Ct); c.PersonId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        var r = await db.CredentialRevisions.FirstAsync(Ct); r.Provenance = "forged";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Actual_outer_transaction_rollback_removes_domain_revision_batch_receipt_and_attributed_write_audit() {
        await using var db = Db(); var before = await db.AuditEvents.CountAsync(Ct); var id = Guid.NewGuid(); var vessel = data.Vessels.First(v => v.TenantId == db.TenantId);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            db.Credentials.Add(new() { TenantId = db.TenantId, Id = id, VesselId = vessel.Id, AssignmentId = vessel.Id, PersonId = vessel.Id });
            db.CredentialRevisions.Add(new() { TenantId = db.TenantId, CredentialId = id, VesselId = vessel.Id, Revision = 1, SavedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(Ct);
            db.CommandReceipts.Add(new() { TenantId = db.TenantId, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "CredentialSave", Fingerprint = new string('A', 64), ResourceId = id, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(Ct); await tx.RollbackAsync(Ct);
        }
        await using var verify = Db(); Assert.False(await verify.Credentials.AnyAsync(x => x.Id == id, Ct)); Assert.False(await verify.CredentialRevisions.AnyAsync(x => x.CredentialId == id, Ct));
        Assert.False(await verify.CommandReceipts.AnyAsync(x => x.ResourceId == id, Ct)); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
    }
    [Fact]
    public async Task Import_tables_and_private_credentials_obey_tenant_filters_and_authored_model_migration_parity() {
        await using var db = Db(); Assert.False(db.Database.HasPendingModelChanges());
        Assert.All(await db.Credentials.ToArrayAsync(Ct), x => Assert.Equal(db.TenantId, x.TenantId));
        Assert.All(await db.CredentialRevisions.ToArrayAsync(Ct), x => Assert.Equal(db.TenantId, x.TenantId));
    }
}
