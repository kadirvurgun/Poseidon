using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class FleetCatalogPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    public SeedData Data { get; private set; } = null!;
    public PoseidonDbContext Db(Guid? tenant = null, string actor = "synthetic-catalog-author") =>
        new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? Data.Tenants[0].Id, new(actor, "User", "fleet-catalog-draft-test"));
    public PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(); Data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(); await SeedImporter.ImportAsync(db, Data);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}

[Trait("Category", "Integration")]
public sealed class FleetCatalogDraftPersistenceTests(FleetCatalogPostgresFixture fixture) : IClassFixture<FleetCatalogPostgresFixture>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private static FleetCatalogDraftContent Draft(string title = "FICTIONAL unverified catalog") => new(title, []);

    [Fact]
    public async Task Draft_and_immutable_outcome_share_an_attributed_audit_transaction_without_copying_values()
    {
        var catalog = Guid.NewGuid(); await using var db = fixture.Db();
        var saved = await FleetCatalogDraftWriter.AppendAsync(db, catalog, 0, Guid.NewGuid(), Draft("Withheld fixture title"), Ct);
        Assert.Equal(1, saved.Revision); Assert.Null(saved.PriorRevisionId);
        Assert.Equal("DeclaredUnverified", saved.Provenance); Assert.Equal("synthetic-catalog-author", saved.ActorId);
        var head = await db.FleetCatalogDrafts.AsNoTracking().SingleAsync(x => x.Id == catalog, Ct);
        Assert.True(head.Version > 0);
        var history = await db.FleetCatalogDraftRevisions.AsNoTracking().SingleAsync(x => x.Id == saved.Id, Ct);
        Assert.Equal(saved.RecordedAt, history.RecordedAt); Assert.Equal(saved.ContentJson, history.ContentJson);
        var audit = await db.AuditEvents.Where(x => x.ResourceId == catalog).ToArrayAsync(Ct);
        Assert.Equal(2, audit.Length); Assert.All(audit, x => { Assert.Equal(saved.ActorId, x.ActorId); Assert.Null(x.VesselId); });
        var summaries = string.Join('\n', audit.Select(x => x.ChangeSummary));
        Assert.DoesNotContain("Withheld fixture title", summaries); Assert.DoesNotContain(saved.ContentHash, summaries);
        Assert.Contains("DRAFT", summaries); Assert.DoesNotContain("APPROVED", summaries);
    }

    [Fact]
    public async Task Replay_returns_the_exact_original_revision_after_later_edits_and_rejects_new_actor_or_intent()
    {
        var catalog = Guid.NewGuid(); var command = Guid.NewGuid(); await using var db = fixture.Db();
        var first = await FleetCatalogDraftWriter.AppendAsync(db, catalog, 0, command, Draft("first"), Ct);
        var second = await FleetCatalogDraftWriter.AppendAsync(db, catalog, 1, Guid.NewGuid(), Draft("second"), Ct);
        Assert.Equal(first.Id, second.PriorRevisionId);
        var count = await db.AuditEvents.CountAsync(x => x.ResourceId == catalog, Ct);
        var replay = await FleetCatalogDraftWriter.AppendAsync(db, catalog, 0, command, Draft("first"), Ct);
        Assert.Equal(first.Id, replay.Id); Assert.Equal(first.ContentJson, replay.ContentJson); Assert.Equal(1, replay.Revision);
        Assert.Equal(count, await db.AuditEvents.CountAsync(x => x.ResourceId == catalog, Ct));
        var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() => FleetCatalogDraftWriter.AppendAsync(db, catalog, 0, command, Draft("different"), Ct));
        Assert.Equal("CATALOG_DRAFT_COMMAND_CONFLICT", conflict.Message);
        await using var otherActor = fixture.Db(actor: "synthetic-other-author");
        var denied = await Assert.ThrowsAsync<InvalidOperationException>(() => FleetCatalogDraftWriter.AppendAsync(otherActor, catalog, 0, command, Draft("first"), Ct));
        Assert.Equal("CATALOG_DRAFT_COMMAND_CONFLICT", denied.Message);
    }

    [Fact]
    public async Task Tenant_filters_and_command_uniqueness_never_probe_or_update_another_tenant()
    {
        var catalog = Guid.NewGuid(); var command = Guid.NewGuid();
        await using var first = fixture.Db();
        await FleetCatalogDraftWriter.AppendAsync(first, catalog, 0, command, Draft("tenant one"), Ct);
        await using var other = fixture.Db(fixture.Data.Tenants[1].Id);
        Assert.False(await other.FleetCatalogDrafts.AnyAsync(x => x.Id == catalog, Ct));
        Assert.False(await other.FleetCatalogDraftRevisions.AnyAsync(x => x.CommandId == command, Ct));
        var own = await FleetCatalogDraftWriter.AppendAsync(other, catalog, 0, command, Draft("tenant two"), Ct);
        Assert.Equal(other.TenantId, own.TenantId);
        Assert.Equal(1, (await first.FleetCatalogDrafts.AsNoTracking().SingleAsync(x => x.Id == catalog, Ct)).Revision);
    }

    [Fact]
    public async Task Stale_and_concurrent_writers_leave_one_head_and_no_orphan_outcome_or_audit()
    {
        var catalog = Guid.NewGuid();
        await using (var setup = fixture.Db()) await FleetCatalogDraftWriter.AppendAsync(setup, catalog, 0, Guid.NewGuid(), Draft(), Ct);
        async Task<Exception?> Attempt()
        {
            await using var db = fixture.Db();
            return await Record.ExceptionAsync(async () => { await FleetCatalogDraftWriter.AppendAsync(db, catalog, 1, Guid.NewGuid(), Draft("next"), Ct); });
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, x => x is null); Assert.Single(results, x => x is not null);
        await using var check = fixture.Db();
        Assert.Equal(2, (await check.FleetCatalogDrafts.SingleAsync(x => x.Id == catalog, Ct)).Revision);
        Assert.Equal(2, await check.FleetCatalogDraftRevisions.CountAsync(x => x.CatalogId == catalog, Ct));
        Assert.Equal(4, await check.AuditEvents.CountAsync(x => x.ResourceId == catalog, Ct));
        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() => FleetCatalogDraftWriter.AppendAsync(check, catalog, 0, Guid.NewGuid(), Draft("stale"), Ct));
        Assert.Equal("CATALOG_DRAFT_VERSION_CONFLICT", stale.Message);
        Assert.Equal(4, await check.AuditEvents.CountAsync(x => x.ResourceId == catalog, Ct));
    }

    [Fact]
    public async Task Revision_history_is_immutable_in_EF_and_PostgreSQL_and_roots_cannot_be_deleted()
    {
        var catalog = Guid.NewGuid(); await using var db = fixture.Db();
        var saved = await FleetCatalogDraftWriter.AppendAsync(db, catalog, 0, Guid.NewGuid(), Draft(), Ct);
        saved.ContentJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE fleet.\"CatalogDraftRevisions\" SET \"ContentJson\"='{{}}' WHERE \"TenantId\"={db.TenantId} AND \"Id\"={saved.Id}", Ct));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM fleet.\"CatalogDrafts\" WHERE \"TenantId\"={db.TenantId} AND \"Id\"={catalog}", Ct));
        Assert.Equal(1, await db.FleetCatalogDraftRevisions.CountAsync(x => x.CatalogId == catalog, Ct));
    }

    [Fact]
    public async Task Optional_empty_fixture_is_idempotent_and_preserves_later_operator_revisions()
    {
        await using (var import = fixture.Import())
        {
            await FleetCatalogDraftFixtures.AddMissingAsync(import, fixture.Data.Tenants, Ct);
            await FleetCatalogDraftFixtures.AddMissingAsync(import, fixture.Data.Tenants, Ct); // Pending additions count too.
            await import.SaveChangesAsync(Ct);
        }
        var catalog = FleetCatalogDraftFixtures.Id(fixture.Data.Tenants[0].Id);
        await using var db = fixture.Db();
        var seed = await db.FleetCatalogDraftRevisions.AsNoTracking().SingleAsync(x => x.CatalogId == catalog, Ct);
        Assert.Equal("SyntheticUnverified", seed.Provenance); Assert.Null(seed.CommandId); Assert.Contains("\"entries\":[]", seed.ContentJson);
        var edited = await FleetCatalogDraftWriter.AppendAsync(db, catalog, 1, Guid.NewGuid(), Draft("operator retained"), Ct);
        var count = await db.AuditEvents.CountAsync(x => x.ResourceId == catalog, Ct);
        await using (var import = fixture.Import())
        {
            await FleetCatalogDraftFixtures.AddMissingAsync(import, fixture.Data.Tenants, Ct); await import.SaveChangesAsync(Ct);
        }
        Assert.Equal(edited.ContentJson, (await db.FleetCatalogDraftRevisions.AsNoTracking().SingleAsync(x => x.Id == edited.Id, Ct)).ContentJson);
        Assert.Equal(count, await db.AuditEvents.CountAsync(x => x.ResourceId == catalog, Ct));
        Assert.Equal(2, (await db.FleetCatalogDrafts.AsNoTracking().SingleAsync(x => x.Id == catalog, Ct)).Revision);
    }

    [Fact]
    public async Task Unrelated_pending_work_cannot_be_committed_as_a_catalog_command()
    {
        var catalog = Guid.NewGuid(); await using var db = fixture.Db();
        var company = await db.Companies.FirstAsync(Ct); var original = company.Name; company.Name = "pending unrelated change";
        await Assert.ThrowsAsync<InvalidOperationException>(() => FleetCatalogDraftWriter.AppendAsync(db, catalog, 0, Guid.NewGuid(), Draft(), Ct));
        db.ChangeTracker.Clear();
        Assert.Equal(original, (await db.Companies.SingleAsync(x => x.Id == company.Id, Ct)).Name);
        Assert.False(await db.FleetCatalogDrafts.AnyAsync(x => x.Id == catalog, Ct));
    }

    [Fact]
    public async Task Ordinary_context_cannot_seed_fixtures_or_attribute_a_revision_to_another_actor()
    {
        await using var db = fixture.Db();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => FleetCatalogDraftFixtures.AddMissingAsync(db, fixture.Data.Tenants, Ct));
        var catalog = Guid.NewGuid();
        var first = await FleetCatalogDraftWriter.AppendAsync(db, catalog, 0, Guid.NewGuid(), Draft(), Ct);
        var head = await db.FleetCatalogDrafts.SingleAsync(x => x.Id == catalog, Ct); head.Revision++;
        db.FleetCatalogDraftRevisions.Add(new() { TenantId = db.TenantId, Id = Guid.NewGuid(), CatalogId = catalog,
            Revision = 2, ExpectedRevision = 1, PriorRevisionId = first.Id, CommandId = Guid.NewGuid(), ActorId = "forged-other-actor",
            ContentJson = first.ContentJson, ContentHash = first.ContentHash, IntentHash = first.IntentHash,
            RecordedAt = DateTimeOffset.UtcNow, Provenance = "DeclaredUnverified" });
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        Assert.Equal("Catalog draft revision requires its attributed head change.", failure.Message);
        db.ChangeTracker.Clear();
        Assert.Equal(1, (await db.FleetCatalogDrafts.SingleAsync(x => x.Id == catalog, Ct)).Revision);
        Assert.Equal(2, await db.AuditEvents.CountAsync(x => x.ResourceId == catalog, Ct));
    }

    [Fact]
    public async Task Prior_revision_composite_FK_rejects_a_cross_tenant_history_splice()
    {
        var catalog = Guid.NewGuid(); await using var first = fixture.Db();
        var foreign = await FleetCatalogDraftWriter.AppendAsync(first, catalog, 0, Guid.NewGuid(), Draft("first tenant"), Ct);
        await using var other = fixture.Db(fixture.Data.Tenants[1].Id);
        var own = await FleetCatalogDraftWriter.AppendAsync(other, catalog, 0, Guid.NewGuid(), Draft("second tenant"), Ct);
        await using var tx = await other.Database.BeginTransactionAsync(Ct);
        // Negative constraint test only; application business writes use audited SaveChanges.
        await other.Database.ExecuteSqlInterpolatedAsync($"UPDATE fleet.\"CatalogDrafts\" SET \"Revision\"=2 WHERE \"TenantId\"={other.TenantId} AND \"Id\"={catalog}", Ct);
        var nextId = Guid.NewGuid(); var nextCommand = Guid.NewGuid();
        var error = await Assert.ThrowsAnyAsync<Exception>(() => other.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO fleet."CatalogDraftRevisions"
              ("TenantId","Id","CatalogId","Revision","PriorRevisionId","CommandId","ExpectedRevision","ActorId","ContentJson","ContentHash","IntentHash","Contract","RecordedAt","Provenance")
            SELECT "TenantId",{nextId},"CatalogId",2,{foreign.Id},{nextCommand},1,"ActorId","ContentJson","ContentHash","IntentHash","Contract","RecordedAt","Provenance"
              FROM fleet."CatalogDraftRevisions" WHERE "TenantId"={other.TenantId} AND "Id"={own.Id}
            """, Ct));
        Assert.Equal("23503", PostgresFailure.Provider(error)?.SqlState);
        await tx.RollbackAsync(Ct); other.ChangeTracker.Clear();
        Assert.Equal(1, (await other.FleetCatalogDrafts.SingleAsync(x => x.Id == catalog, Ct)).Revision);
        Assert.Equal(2, await other.AuditEvents.CountAsync(x => x.ResourceId == catalog, Ct));
    }

    [Fact]
    public async Task Oversized_draft_is_rejected_before_any_head_outcome_or_audit_write()
    {
        await using var db = fixture.Db(); var catalog = Guid.NewGuid();
        var content = new FleetCatalogDraftContent("large synthetic draft", Enumerable.Range(0, 6000)
            .Select(_ => new FleetCatalogEntryDraft(Guid.NewGuid(), null, new string('L', 200), null, [], [])).ToArray());
        var error = await Assert.ThrowsAsync<ArgumentException>(() => FleetCatalogDraftWriter.AppendAsync(db, catalog, 0, Guid.NewGuid(), content, Ct));
        Assert.Equal("CATALOG_DRAFT_TOO_LARGE", error.Message);
        Assert.False(await db.FleetCatalogDrafts.AnyAsync(x => x.Id == catalog, Ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.ResourceId == catalog, Ct));
    }
}
