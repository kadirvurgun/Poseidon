using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class OperationEquipmentPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData data = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? data.Tenants[0].Id, new("synthetic-inspector", "User", "owning-input-test"));
    public async ValueTask InitializeAsync() {
        await postgres.StartAsync(Ct); data = SeedValidationTests.Demo(); await using var db = Import(); await db.Database.MigrateAsync(Ct);
        await SeedImporter.ImportAsync(db, data, Ct);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        await OperationEquipmentFixtures.AddMissingAsync(db, await db.Vessels.ToArrayAsync(Ct), Ct); await db.SaveChangesAsync(Ct); await tx.CommitAsync(Ct);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    [Fact]
    public async Task Optional_incomplete_fixture_is_idempotent_and_preserves_operator_head_and_history() {
        await using var db = Db(); var binding = await db.OperationEquipmentBindings.OrderBy(b => b.Id).FirstAsync(Ct);
        var head = await db.ControlEvidenceHeads.SingleAsync(h => h.Id == binding.Id && h.Kind == "Evidence", Ct);
        var first = await db.ControlEvidenceRevisions.SingleAsync(r => r.Id == binding.Id && r.Revision == 1, Ct);
        Assert.Equal("DRAFT", first.Lifecycle); Assert.Null(first.FileId);
        var original = first.ContentJson; var newId = head.Id; head.Revision++;
        db.ControlEvidenceRevisions.Add(new() { TenantId = head.TenantId, VesselId = head.VesselId, Kind = head.Kind, Id = head.Id, Revision = head.Revision,
            ContentJson = "{\"title\":\"FICTIONAL operator-preserved revision\"}", Hash = RegisterStorage.Hash("FICTIONAL operator edit"), ReasonCode = "AUTHORING_CORRECTION", SavedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct); var audits = await db.AuditEvents.LongCountAsync(Ct);
        await using (var import = Import()) {
            await SeedImporter.ImportAsync(import, data, Ct);
            var vessels = await import.Vessels.ToArrayAsync(Ct);
            await OperationEquipmentFixtures.AddMissingAsync(import, vessels, Ct); await import.SaveChangesAsync(Ct);
            await OperationEquipmentFixtures.AddMissingAsync(import, vessels, Ct); await import.SaveChangesAsync(Ct);
        }
        await using var check = Db(); Assert.Equal(2, (await check.ControlEvidenceHeads.SingleAsync(h => h.Id == newId, Ct)).Revision);
        Assert.Equal(original, (await check.ControlEvidenceRevisions.SingleAsync(r => r.Id == newId && r.Revision == 1, Ct)).ContentJson);
        Assert.Equal(audits, await check.AuditEvents.LongCountAsync(Ct)); Assert.Empty(await check.HumanReviewEvents.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Binding_is_append_only_and_cross_vessel_asset_relation_is_denied() {
        await using (var db = Db()) { var b = await db.OperationEquipmentBindings.FirstAsync(Ct); b.DocumentKind = "CargoPlan"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); }
        await using (var db = Db()) await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE core.\"OperationEquipmentBindings\" SET \"Revision\" = \"Revision\"", Ct));
        await using var check = Db(); var local = await check.OperationEquipmentBindings.Where(b => b.DocumentKind == "CargoPlan").FirstAsync(Ct);
        var other = await check.Assets.Where(a => a.VesselId != local.VesselId).FirstAsync(Ct);
        check.ControlEvidenceRevisions.Add(new() { TenantId = local.TenantId, VesselId = local.VesselId, Kind = local.Kind, Id = local.Id, Revision = 99,
            ContentJson = "{}", Hash = RegisterStorage.Hash("FICTIONAL cross-vessel negative"), ReasonCode = "SYNTHETIC_NEGATIVE", SavedAt = DateTimeOffset.UtcNow });
        check.OperationEquipmentBindings.Add(new() { TenantId = local.TenantId, VesselId = local.VesselId, Kind = local.Kind, Id = local.Id, Revision = 99, DocumentKind = "WinchFitment", AssetId = other.Id, AssetRevision = other.Revision });
        await Assert.ThrowsAsync<DbUpdateException>(() => check.SaveChangesAsync(Ct));
    }
}
