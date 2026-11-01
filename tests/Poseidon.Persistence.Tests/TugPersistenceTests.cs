using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;
namespace Poseidon.Persistence.Tests;

[Trait("Category","Integration")]
public sealed class TugPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData data = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,ActorContext.SeedImport(),true);
    public async ValueTask InitializeAsync() { await postgres.StartAsync(Ct); data=SeedValidationTests.Demo(); await using var db=Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db,data,Ct); }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    [Fact]
    public async Task Independent_import_adds_only_tug_drafts_preserves_edits_and_seeds_no_assignments_owners_or_receipts() {
        var tug=data.Vessels.Single(v=>v.Kind==VesselKind.Tug);
        await using(var db=Import()) { var j=await db.TugJobs.SingleAsync(Ct); j.Notes="FICTIONAL operator edit"; j.Revision++; db.TugJobRevisions.Add(j.Snapshot()); await db.SaveChangesAsync(Ct); }
        int count;await using(var db=Import()) {count=await db.AuditEvents.CountAsync(Ct); await SeedImporter.ImportAsync(db,SeedValidationTests.Demo(),Ct);}
        await using var verify=Import(); var row=Assert.Single(await verify.TugJobs.ToArrayAsync(Ct)); Assert.Equal(tug.Id,row.VesselId); Assert.Equal("FICTIONAL operator edit",row.Notes);
        Assert.Equal("Draft",row.Status);Assert.Equal("SyntheticUnverified",row.Provenance);Assert.Null(row.RequestedStart);Assert.Null(row.RequestedEnd);Assert.Null(row.RequiredBollardPull);
        Assert.Empty(await verify.TugAssignments.ToArrayAsync(Ct));Assert.Empty(await verify.TugOwners.ToArrayAsync(Ct));Assert.Empty(await verify.CommandReceipts.ToArrayAsync(Ct));Assert.Equal(count,await verify.AuditEvents.CountAsync(Ct));
    }
    [Fact]
    public async Task Tenant_filter_kind_guard_composite_reference_and_immutable_revision_enforce_real_postgres_constraints() {
        var tug=data.Vessels.Single(v=>v.Kind==VesselKind.Tug);var other=data.Vessels.First(v=>v.TenantId!=tug.TenantId);
        var options=SeedImporter.Options(postgres.GetConnectionString());
        await using(var tenant=new PoseidonDbContext(options,other.TenantId,new("synthetic","User","tug-test"))) Assert.Empty(await tenant.TugJobs.ToArrayAsync(Ct));
        await using(var db=Import()) {
            var ship=data.Vessels.First(v=>v.Kind==VesselKind.Ship);db.TugJobs.Add(new(){TenantId=ship.TenantId,VesselId=ship.Id,Id=Guid.NewGuid(),JobNumber="SYNTHETIC-WRONG-KIND"});
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync(Ct));db.ChangeTracker.Clear();
            db.TugJobs.Add(new(){TenantId=tug.TenantId,VesselId=other.Id,Id=Guid.NewGuid(),JobNumber="SYNTHETIC-CROSS-TENANT"});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync(Ct));db.ChangeTracker.Clear();
            var r=await db.TugJobRevisions.SingleAsync(Ct);r.Provenance="forbidden";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
        }
        await using var connection=new Npgsql.NpgsqlConnection(postgres.GetConnectionString());await connection.OpenAsync(Ct);
        foreach(var sql in new[]{"UPDATE tug.\"JobRevisions\" SET \"Provenance\"='forbidden'", "TRUNCATE tug.\"Assignments\""}) {await using var cmd=new Npgsql.NpgsqlCommand(sql,connection);await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>cmd.ExecuteNonQueryAsync(Ct));}
    }
    [Fact]
    public void Model_snapshot_parity_is_a_required_later_gate() {using var db=Import();Assert.False(db.Database.HasPendingModelChanges());}
}
