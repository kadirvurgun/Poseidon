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
    [Fact]
    public async Task Legacy_defaults_preserve_original_plaintext_and_unknown_capture_authority_and_reject_direct_source_rewrite() {
        var vessel = data.Vessels.First(v => v.TenantId == data.Tenants[0].Id); var id = Guid.NewGuid();
        await using (var db = Db()) {
            db.ImportBatches.Add(new() { TenantId = db.TenantId, Id = id, VesselId = vessel.Id, ActorId = Guid.NewGuid(),
                Category = "crew", TableJson = "FICTIONAL UNSUPPORTED LEGACY SOURCE", SourceName = "FICTIONAL historical.xlsx", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(Ct);
        }
        await using (var db = Db()) {
            var original = await db.ImportBatches.SingleAsync(x => x.Id == id, Ct);
            Assert.Null(original.LegacyProtectedAt); Assert.Null(original.CrewIdentityPolicyVersion); Assert.Null(original.LegacyProtectionPolicyVersion);
            Assert.Equal("FICTIONAL UNSUPPORTED LEGACY SOURCE", original.TableJson);
            // Deliberate negative DB-boundary probe; scoped target, no successful raw-SQL business mutation.
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE integration.\"ImportBatches\" SET \"TableJson\" = 'replacement' WHERE \"TenantId\" = {db.TenantId} AND \"Id\" = {id}", Ct));
        }
        await using var other = Db(data.Tenants.First(t => t.Id != vessel.TenantId).Id);
        Assert.False(await other.ImportBatches.AnyAsync(x => x.Id == id, Ct));
    }
    [Fact]
    public async Task Persisted_position_reference_is_immutable_in_EF_and_database_without_rewriting_saved_assignment_facts() {
        await using (var db = Db()) {
            var position = await db.CrewPositions.FirstAsync(Ct); position.Department = "FICTIONAL rewrite";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
        await using var boundary = Db(); var saved = await boundary.CrewPositions.AsNoTracking().FirstAsync(Ct);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => boundary.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE crew.\"Positions\" SET \"Department\" = 'rewrite' WHERE \"TenantId\" = {boundary.TenantId} AND \"Id\" = {saved.Id}", Ct));
        Assert.Equal(saved.Department, (await boundary.CrewPositions.AsNoTracking().SingleAsync(x => x.Id == saved.Id, Ct)).Department);
    }
    [Fact]
    public async Task Explicit_disposable_browser_fixture_is_attributed_idempotent_preserves_operator_state_and_never_invents_capture_policy() {
        await LegacyCrewBrowserFixture.AddAsync(postgres.GetConnectionString(), "Test", data);
        await using (var db = Db()) {
            var batch = await db.ImportBatches.SingleAsync(x => x.Id == LegacyCrewBrowserFixture.BatchId, Ct);
            Assert.Null(batch.CrewIdentityPolicyVersion); Assert.Null(batch.LegacyProtectedAt); Assert.Equal("crew", batch.Category);
            Assert.StartsWith("synthetic-legacy", batch.TemplateVersion); Assert.Equal(Guid.Empty, batch.ActorId);
            batch.State = "MappingDraft"; await db.SaveChangesAsync(Ct);
        }
        await LegacyCrewBrowserFixture.AddAsync(postgres.GetConnectionString(), "Test", data);
        await using var verify = Db(); var retained = await verify.ImportBatches.SingleAsync(x => x.Id == LegacyCrewBrowserFixture.BatchId, Ct);
        Assert.Equal("MappingDraft", retained.State);
        Assert.Equal(1, await verify.AuditEvents.CountAsync(x => x.ResourceId == retained.Id && x.Action == "Added" && x.ActorType == "System" && x.Source == "SyntheticFixture", Ct));
        Assert.Empty(await verify.CommandReceipts.ToArrayAsync(Ct));
    }
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Legacy_browser_fixture_is_never_normal_seed_or_production_data(string environment) {
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyCrewBrowserFixture.AddAsync(postgres.GetConnectionString(), environment, data));
        await using var db = Db(); Assert.False(await db.ImportBatches.AnyAsync(Ct));
    }
}
