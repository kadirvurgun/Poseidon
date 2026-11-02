using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class ComplianceInputPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData data = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? data.Tenants[0].Id, new("synthetic-inspector", "User", "compliance-input-test"));
    public async ValueTask InitializeAsync() {
        await postgres.StartAsync(Ct); data = SeedValidationTests.Demo(); await using var db = Import(); await db.Database.MigrateAsync(Ct);
        await SeedImporter.ImportAsync(db, data, Ct);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        await ComplianceInputFixtures.AddMissingAsync(db, await db.Vessels.ToArrayAsync(Ct), Ct); await db.SaveChangesAsync(Ct); await tx.CommitAsync(Ct);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    [Fact]
    public async Task Optional_fixture_is_incomplete_idempotent_and_preserves_operator_revision_and_normal_receipts() {
        await using var db = Db(); var first = await db.ControlEvidenceRevisions.FirstAsync(Ct); var original = first.ContentJson;
        Assert.Equal("DRAFT", first.Lifecycle); Assert.Null(first.FileId);
        var head = await db.ControlEvidenceHeads.SingleAsync(h => h.Id == first.Id, Ct); head.Revision++;
        db.ControlEvidenceRevisions.Add(new() { TenantId = head.TenantId, VesselId = head.VesselId, Kind = head.Kind, Id = head.Id, Revision = head.Revision,
            ContentJson = "{\"title\":\"FICTIONAL operator correction\"}", Hash = RegisterStorage.Hash("FICTIONAL operator correction"), ReasonCode = "AUTHORING_CORRECTION", SavedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct); var audits = await db.AuditEvents.LongCountAsync(Ct);
        await using (var import = Import()) {
            var vessels = await import.Vessels.ToArrayAsync(Ct);
            await ComplianceInputFixtures.AddMissingAsync(import, vessels, Ct); await import.SaveChangesAsync(Ct);
            await ComplianceInputFixtures.AddMissingAsync(import, vessels, Ct); await import.SaveChangesAsync(Ct);
        }
        await using var check = Db(); Assert.Equal(2, (await check.ControlEvidenceHeads.SingleAsync(h => h.Id == head.Id, Ct)).Revision);
        Assert.Equal(original, (await check.ControlEvidenceRevisions.SingleAsync(r => r.Id == head.Id && r.Revision == 1, Ct)).ContentJson);
        Assert.Equal(audits, await check.AuditEvents.LongCountAsync(Ct)); Assert.Empty(await check.HumanReviewEvents.ToArrayAsync(Ct));
        Assert.Empty(await check.EvaluationUpstreamReferences.ToArrayAsync(Ct)); Assert.Empty(await check.MsmdRoleBindings.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Cross_vessel_msmd_role_is_rejected_and_rollback_retains_no_partial_relationship_or_audit() {
        await using var db = Db(); var evidence = await db.OperationEquipmentBindings.FirstAsync(b => b.DocumentKind == "Msmd", Ct);
        var foreign = await db.CrewPositions.FirstAsync(p => p.VesselId != evidence.VesselId, Ct);
        var audits = await db.AuditEvents.LongCountAsync(Ct);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            db.MsmdRoleBindings.Add(new() { TenantId = evidence.TenantId, VesselId = evidence.VesselId, Id = evidence.Id, Revision = evidence.Revision,
                PositionId = foreign.Id, Count = 1, SourceLocator = "FICTIONAL/cross-vessel-negative" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)); await tx.RollbackAsync(Ct);
        }
        await using var check = Db(); Assert.Empty(await check.MsmdRoleBindings.ToArrayAsync(Ct)); Assert.Equal(audits, await check.AuditEvents.LongCountAsync(Ct));
        await using var otherTenant = Db(data.Tenants[1].Id); Assert.False(await otherTenant.OperationEquipmentBindings.AnyAsync(b => b.Id == evidence.Id, Ct));
    }
}
