using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;
namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class TechnicalPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData data = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private Vessel Vessel => data.Vessels.First(v => v.TenantId == data.Tenants[0].Id);
    private PoseidonDbContext Db() => new(SeedImporter.Options(postgres.GetConnectionString()), Vessel.TenantId, new("synthetic-technical", "User", "technical-test"));
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync() {
        await postgres.StartAsync(Ct); data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db, data, Ct);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    private Asset Asset(Guid? id = null) => new() { TenantId = Vessel.TenantId, VesselId = Vessel.Id, Id = id ?? Guid.NewGuid(), Name = "Synthetic asset" };
    [Fact]
    public void Current_model_must_match_the_committed_migration_snapshot() {
        using var db = Db(); Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task Current_migration_seed_is_idempotent_and_preserves_operator_hierarchy_and_plan_inputs() {
        await using (var db = Db()) {
            var a = await db.Assets.SingleAsync(x => x.Id == Vessel.Id, Ct); a.Location = "Synthetic operator edit"; a.Revision++;
            db.AssetRevisions.Add(a.Snapshot());
            var p = await db.MaintenancePlans.SingleAsync(x => x.Id == Vessel.Id, Ct); p.Revision++;
            p.ContentJson = JsonSerializer.Serialize(new MaintenanceInputs(IntervalValue: "0.123456789012345678"));
            db.MaintenancePlanRevisions.Add(p.Snapshot(DateTimeOffset.UnixEpoch, a.Revision)); await db.SaveChangesAsync(Ct);
        }
        int audits;
        await using (var db = Import()) { audits = await db.AuditEvents.CountAsync(Ct); await SeedImporter.ImportAsync(db, SeedValidationTests.Demo(), Ct); }
        await using var verify = Import(); Assert.Equal(audits, await verify.AuditEvents.CountAsync(Ct));
        Assert.Equal("Synthetic operator edit", (await verify.Assets.SingleAsync(x => x.Id == Vessel.Id, Ct)).Location);
        Assert.Contains("0.123456789012345678", (await verify.MaintenancePlans.SingleAsync(x => x.Id == Vessel.Id, Ct)).ContentJson);
        Assert.All(await verify.Assets.ToArrayAsync(Ct), a => { Assert.Equal("Draft", a.Status); Assert.Equal("SyntheticUnverified", a.Provenance); });
        Assert.Empty(await verify.TechnicalOwners.ToArrayAsync(Ct)); Assert.Empty(await verify.CommandReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Composite_foreign_keys_reject_cross_vessel_and_tenant_edges_and_rollback_audit() {
        var other = data.Vessels.First(v => v.TenantId != Vessel.TenantId);
        await using var db = Db(); var before = await db.AuditEvents.CountAsync(Ct);
        var a = Asset(); a.ParentId = other.Id; db.Assets.Add(a);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        var p = new MaintenancePlan { TenantId = Vessel.TenantId, VesselId = Vessel.Id, Id = Guid.NewGuid(), AssetId = other.Id };
        db.MaintenancePlans.Add(p); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        var localOther = data.Vessels.First(v => v.TenantId == Vessel.TenantId && v.Id != Vessel.Id);
        a = Asset(); a.ParentId = localOther.Id; db.Assets.Add(a);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        await using var verify = Db(); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
    }
    [Fact]
    public async Task Serializable_two_connection_reparent_race_cannot_commit_a_cycle() {
        var a = Asset(); var b = Asset();
        await using (var db = Db()) { db.Assets.AddRange(a,b); db.AssetRevisions.AddRange(a.Snapshot(),b.Snapshot()); await db.SaveChangesAsync(Ct); }
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
        async Task<bool> Reparent(Guid id, Guid parent) {
            await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, Ct);
            try {
                var rows = await db.Assets.Where(x => x.VesselId == Vessel.Id).ToListAsync(Ct);
                var nodes = rows.Select(x => new EquipmentHierarchyNode(x.TenantId, x.VesselId, x.Id, x.ParentId)).ToArray();
                Assert.Equal(EquipmentHierarchyValidation.Valid, EquipmentHierarchyPolicy.ValidateReparent(Vessel.TenantId, Vessel.Id, nodes, id, parent));
                if (Interlocked.Increment(ref count) == 2) ready.SetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
                var row = rows.Single(x => x.Id == id); row.ParentId = parent; row.Revision++; db.AssetRevisions.Add(row.Snapshot());
                await db.SaveChangesAsync(Ct); await tx.CommitAsync(Ct); return true;
            } catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) { await tx.RollbackAsync(Ct); return false; }
        }
        var result = await Task.WhenAll(Reparent(a.Id,b.Id), Reparent(b.Id,a.Id)); Assert.Single(result.Where(x => x));
        await using var verify = Db();
        var final = await verify.Assets.Where(x => x.VesselId == Vessel.Id).ToArrayAsync(Ct);
        Assert.Equal(EquipmentHierarchyValidation.Valid, EquipmentHierarchyPolicy.Validate(Vessel.TenantId, Vessel.Id, final.Select(x => new EquipmentHierarchyNode(x.TenantId,x.VesselId,x.Id,x.ParentId)).ToArray()));
        Assert.Equal(4, await verify.AssetRevisions.CountAsync(x => x.VesselId == Vessel.Id, Ct));
    }
    [Fact]
    public async Task Direct_SaveChanges_cycle_and_orphan_are_rejected_by_database_not_just_api_policy() {
        await using var db = Db(); var a = Asset(); var b = Asset(); b.ParentId = a.Id;
        db.Assets.AddRange(a,b); db.AssetRevisions.AddRange(a.Snapshot(),b.Snapshot()); await db.SaveChangesAsync(Ct);
        var before = await db.AuditEvents.CountAsync(Ct); a.ParentId = b.Id; a.Revision++; db.AssetRevisions.Add(a.Snapshot());
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        Assert.Equal("23514", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
        await using var verify = Db(); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
        Assert.Null((await verify.Assets.SingleAsync(x => x.Id == a.Id, Ct)).ParentId);
    }
    [Fact]
    public async Task Database_trigger_arbitrates_two_direct_read_committed_reparents_without_API_precheck() {
        var a = Asset(); var b = Asset();
        await using (var db = Db()) { db.Assets.AddRange(a,b); db.AssetRevisions.AddRange(a.Snapshot(),b.Snapshot()); await db.SaveChangesAsync(Ct); }
        await using var first = Db(); await using var second = Db();
        var left = await first.Assets.SingleAsync(x => x.Id == a.Id, Ct); var right = await second.Assets.SingleAsync(x => x.Id == b.Id, Ct);
        left.ParentId = b.Id; right.ParentId = a.Id; left.Revision++; right.Revision++;
        first.AssetRevisions.Add(left.Snapshot()); second.AssetRevisions.Add(right.Snapshot());
        async Task<bool> Commit(PoseidonDbContext db) {
            try { await db.SaveChangesAsync(Ct); return true; }
            catch (DbUpdateException error) {
                var state = Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState;
                Assert.Contains(state, new[] { "23514", "40P01" }); return false;
            }
        }
        Assert.Single((await Task.WhenAll(Commit(first), Commit(second))).Where(x => x));
        await using var verify = Db(); var rows = await verify.Assets.Where(x => x.VesselId == Vessel.Id).ToArrayAsync(Ct);
        Assert.Equal(EquipmentHierarchyValidation.Valid, EquipmentHierarchyPolicy.Validate(Vessel.TenantId, Vessel.Id,
            rows.Select(x => new EquipmentHierarchyNode(x.TenantId,x.VesselId,x.Id,x.ParentId)).ToArray()));
    }
    [Fact]
    public async Task Stale_xmin_cannot_append_history_and_explicit_rollback_removes_all_writes() {
        await using var a = Db(); await using var b = Db();
        var first = await a.Assets.SingleAsync(x => x.Id == Vessel.Id, Ct); var stale = await b.Assets.SingleAsync(x => x.Id == Vessel.Id, Ct);
        first.Name = "Synthetic new name"; first.Revision++; a.AssetRevisions.Add(first.Snapshot()); await a.SaveChangesAsync(Ct);
        var audits = await a.AuditEvents.CountAsync(Ct); stale.Revision++; stale.Location = "stale"; b.AssetRevisions.Add(stale.Snapshot());
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => b.SaveChangesAsync(Ct));
        await using (var tx = await a.Database.BeginTransactionAsync(Ct)) {
            var added = Asset(); a.Assets.Add(added); a.AssetRevisions.Add(added.Snapshot()); await a.SaveChangesAsync(Ct); await tx.RollbackAsync(Ct);
        }
        await using var verify = Db(); Assert.Equal(audits, await verify.AuditEvents.CountAsync(Ct));
        Assert.Single(await verify.Assets.Where(x => x.VesselId == Vessel.Id).ToArrayAsync(Ct)); Assert.Equal(2, await verify.AssetRevisions.CountAsync(x => x.VesselId == Vessel.Id, Ct));
    }
    [Fact]
    public async Task Immutable_history_trigger_rejects_update_delete_truncate_and_lineage_references_remain_exact() {
        await using var db = Db();
        var r = await db.MaintenancePlanRevisions.SingleAsync(x => x.VesselId == Vessel.Id, Ct);
        Assert.Equal(1, r.AssetRevision); Assert.Equal("ReviewRequired", JsonSerializer.Deserialize<MaintenanceDue>(r.CalculationJson)!.State);
        foreach (var table in new[] { "AssetRevisions", "PlanRevisions" })
            foreach (var sql in new[] { $"DELETE FROM technical.\"{table}\"", $"TRUNCATE technical.\"{table}\" CASCADE", $"UPDATE technical.\"{table}\" SET \"Revision\"=999" })
                await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));
    }
}
