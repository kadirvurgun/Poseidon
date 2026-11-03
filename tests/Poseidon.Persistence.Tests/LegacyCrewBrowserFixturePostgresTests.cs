using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class LegacyCrewBrowserFixturePostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18.6").Build();
    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Distinct_cohorts_and_default_coexist_with_fixed_scope_audit_and_reentry_preserving_edits()
    {
        var ct = TestContext.Current.CancellationToken;
        var seed = SeedValidationTests.Demo();
        var connection = container.GetConnectionString();
        await using (var setup = new PoseidonDbContext(SeedImporter.Options(connection, seed), Guid.Empty,
            ActorContext.SeedImport(), administrativeImport: true)) await setup.Database.MigrateAsync(ct);
        var vessel = seed.Vessels.First();
        var desktop = Guid.NewGuid(); var narrow = Guid.NewGuid();
        await LegacyCrewBrowserFixture.AddAsync(connection, "Test", seed, desktop);
        await LegacyCrewBrowserFixture.AddAsync(connection, "Test", seed, narrow);
        await LegacyCrewBrowserFixture.AddAsync(connection, "Test", seed);
        await using var own = new PoseidonDbContext(SeedImporter.Options(connection), vessel.TenantId,
            new ActorContext("test-legacy-editor", "User", Guid.NewGuid().ToString("D")));
        var ids = new[] { desktop, narrow, LegacyCrewBrowserFixture.BatchId };
        var rows = await own.ImportBatches.Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => {
            Assert.Equal(vessel.TenantId, row.TenantId); Assert.Equal(vessel.Id, row.VesselId);
            Assert.Equal(Guid.Empty, row.ActorId); Assert.Equal("synthetic-expired-session", row.SessionHash);
            Assert.Equal("synthetic-legacy-unsupported-v0", row.TemplateVersion);
            Assert.Null(row.LegacyProtectedAt); Assert.Null(row.LegacyProtectionPolicyVersion);
        });
        var authored = await own.AuditEvents.Where(x => x.EntityType == nameof(ImportBatch) &&
            x.ActorId == "system:synthetic-legacy-crew-fixture").ToArrayAsync(ct);
        Assert.Equal(3, authored.Length);
        Assert.All(authored, audit => {
            Assert.Equal(vessel.TenantId, audit.TenantId); Assert.Equal(vessel.Id, audit.VesselId);
            Assert.Equal("Added", audit.Action);
        });
        var first = rows.Single(x => x.Id == desktop);
        // Immutable historical source bytes must stay unchanged. Edit only the
        // existing mutable review metadata; reentry must not reset either slice.
        first.ReviewerState = "Assigned";
        first.Revision++;
        await own.SaveChangesAsync(ct);
        var preservedTable = first.TableJson; var preservedVersion = first.Version; var preservedRevision = first.Revision;
        await LegacyCrewBrowserFixture.AddAsync(connection, "Test", SeedValidationTests.Demo(), desktop);
        await own.Entry(first).ReloadAsync(ct);
        Assert.Equal(preservedTable, first.TableJson); Assert.Equal(preservedVersion, first.Version);
        Assert.Equal(preservedRevision, first.Revision);
        Assert.Equal("Assigned", first.ReviewerState);
        Assert.Equal(3, await own.AuditEvents.CountAsync(x => x.EntityType == nameof(ImportBatch) &&
            x.ActorId == "system:synthetic-legacy-crew-fixture", ct));
        await using var foreign = new PoseidonDbContext(SeedImporter.Options(connection), seed.Tenants[1].Id,
            new ActorContext("test-foreign-legacy-reader", "User", Guid.NewGuid().ToString("D")));
        Assert.Empty(await foreign.ImportBatches.Where(x => ids.Contains(x.Id)).ToArrayAsync(ct));
    }
}
