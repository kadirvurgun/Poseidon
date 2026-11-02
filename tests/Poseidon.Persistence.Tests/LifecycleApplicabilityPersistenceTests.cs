using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

// LA-04/07/09; actual PostgreSQL guards/tenant FKs/reseed. AUTHORED, NOT RUN.
[Trait("Category", "Integration")]
public sealed class LifecycleApplicabilityPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData seed = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? seed.Tenants[0].Id, new("synthetic-author", "User", "synthetic-lifecycle"));
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync() {
        await postgres.StartAsync(Ct); seed = SeedValidationTests.Demo(); await using var db = Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db, seed, Ct);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    private static RuleRevision Revision(Guid tenant, Guid id, long revision = 1) => new() {
        TenantId = tenant, Id = id, Revision = revision, Contract = ApplicabilityEngine.Contract, RuleJson = null, Hash = RegisterStorage.Hash("fictional"),
        ActorId = Guid.Empty, RoleContext = "SystemSyntheticFixture", ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", RequestedLifecycle = "DRAFT", SavedAt = DateTimeOffset.UtcNow
    };
    [Fact]
    public async Task Add_missing_seed_preserves_operator_revisions_and_never_seeds_evaluations_or_authority() {
        await using (var db = Db()) {
            var head = await db.RuleDrafts.SingleAsync(Ct); head.Revision++;
            db.RuleRevisions.Add(Revision(db.TenantId, head.Id, head.Revision)); await db.SaveChangesAsync(Ct);
        }
        await using var import = Import(); var audits = await import.AuditEvents.CountAsync(Ct);
        await SeedImporter.ImportAsync(import, SeedValidationTests.Demo(), Ct); await SeedImporter.ImportAsync(import, SeedValidationTests.Demo(), Ct);
        Assert.Equal(audits, await import.AuditEvents.CountAsync(Ct)); Assert.Equal(3, await import.RuleRevisions.CountAsync(Ct));
        Assert.All(await import.RuleRevisions.ToArrayAsync(Ct), r => { Assert.Equal("DRAFT", r.Lifecycle); Assert.Equal("NOT_REQUESTED", r.ReviewStatus); Assert.Null(r.RuleJson); });
        Assert.Empty(await import.ApplicabilityEvaluations.ToArrayAsync(Ct)); Assert.Empty(await import.ApplicabilityReceipts.ToArrayAsync(Ct));
        await using var other = Db(seed.Tenants[1].Id); Assert.Single(await other.RuleRevisions.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Source_and_requirement_lineage_cannot_cross_tenant_or_use_wrong_kind() {
        foreach (var wrongKind in new[] { false, true }) {
            await using var db = Db(); var id = Guid.NewGuid(); var before = await db.AuditEvents.CountAsync(Ct);
            db.RuleDrafts.Add(new() { TenantId = db.TenantId, Id = id, Revision = 1 });
            var r = Revision(db.TenantId, id); r.SourceKind = wrongKind ? "Requirement" : "Source";
            r.SourceId = wrongKind ? RegulatoryRegisterFixtures.RequirementId(db.TenantId) : RegulatoryRegisterFixtures.SourceId(seed.Tenants[1].Id); r.SourceRevision = 1;
            db.RuleRevisions.Add(r); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            await using var verify = Db(); Assert.False(await verify.RuleDrafts.AnyAsync(x => x.Id == id, Ct)); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
        }
    }
    [Fact]
    public async Task Rule_revision_cannot_mutate_or_claim_adoption_in_context_or_database() {
        await using var db = Db(); var r = await db.RuleRevisions.SingleAsync(Ct); r.Hash = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        foreach (var statement in new[] { "UPDATE core.\"RuleRevisions\" SET \"Hash\"='forged'", "DELETE FROM core.\"RuleRevisions\"", "TRUNCATE core.\"RuleRevisions\"" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(statement, Ct));
        var id = Guid.NewGuid(); var adopted = Revision(db.TenantId, id); adopted.Lifecycle = "IN_FORCE";
        db.RuleDrafts.Add(new() { TenantId = db.TenantId, Id = id, Revision = 1 }); db.RuleRevisions.Add(adopted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Stale_noop_head_update_fails_xmin_and_rolls_back_new_history() {
        await using var first = Db(); await using var second = Db(); var a = await first.RuleDrafts.SingleAsync(Ct); var b = await second.RuleDrafts.SingleAsync(Ct);
        a.Revision++; first.RuleRevisions.Add(Revision(first.TenantId, a.Id, a.Revision)); await first.SaveChangesAsync(Ct);
        b.Revision = 3; second.RuleRevisions.Add(Revision(second.TenantId, b.Id, b.Revision)); second.Entry(b).Property(x => x.Revision).IsModified = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
        await using var verify = Db(); Assert.Equal(2, await verify.RuleRevisions.CountAsync(Ct));
    }
    [Fact]
    public async Task Evaluation_receipt_snapshot_is_append_only_and_business_rollback_is_atomic() {
        await using var db = Db(); var before = await db.AuditEvents.CountAsync(Ct); var id = Guid.NewGuid(); var vessel = seed.Vessels.First(v => v.TenantId == db.TenantId);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            db.ApplicabilityEvaluations.Add(new() { TenantId = db.TenantId, Id = id, VesselId = vessel.Id, ProfileRevision = 1, ActorId = Guid.Empty,
                SavedAt = DateTimeOffset.UtcNow, AsOf = DateTimeOffset.UtcNow, AdapterVersion = ApplicabilityEngine.AdapterVersion, SnapshotJson = "{}", SnapshotHash = RegisterStorage.Hash("{}"), ResultJson = "{}", RuleSetHash = RegisterStorage.Hash("[]") });
            db.EvaluationRules.Add(new() { TenantId = db.TenantId, EvaluationId = id, RuleId = ApplicabilityFixtures.Id(db.TenantId), RuleRevision = 1 });
            await db.SaveChangesAsync(Ct); await tx.RollbackAsync(Ct);
        }
        await using var verify = Db(); Assert.False(await verify.ApplicabilityEvaluations.AnyAsync(Ct)); Assert.Empty(await verify.EvaluationRules.ToArrayAsync(Ct)); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
        // Closed test-owned identifier/verb lists; deliberate trigger mutation probes.
#pragma warning disable EF1002
        foreach (var table in new[] { "ApplicabilityEvaluations", "EvaluationRules", "ApplicabilityReceipts" })
        foreach (var mutation in new[] { "DELETE FROM", "TRUNCATE" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => verify.Database.ExecuteSqlRawAsync($"{mutation} core.\"{table}\"", Ct));
#pragma warning restore EF1002
    }
}
