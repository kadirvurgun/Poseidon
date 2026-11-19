using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class ParameterPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    public SeedData Data { get; private set; } = null!;
    public string ConnectionString => postgres.GetConnectionString();
    public PoseidonDbContext Db(Guid? tenant = null, string actor = "synthetic-parameter-actor") =>
        new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? Data.Tenants[0].Id, new(actor, "User", "parameter-test"));
    public PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(); Data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(); await SeedImporter.ImportAsync(db, Data);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}

// PLAN-1511-5401: real PostgreSQL lineage, tenant isolation, immutability, maker-checker,
// idempotent receipts and CAS through the audited SaveChanges path.
[Trait("Category", "Integration")]
[Trait("Requirement", "PLAN-1511-5401")]
public sealed class ParameterPersistenceTests(ParameterPostgresFixture fixture) : IClassFixture<ParameterPostgresFixture>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateTimeOffset Now() => ParameterValuePolicy.Micro(DateTimeOffset.UtcNow);
    private static Func<Task<bool>> Allowed => () => Task.FromResult(true);
    private static readonly ParameterDefinition Cache = ParameterRegistry.Find("regulatory.cache.max_age_seconds")!;

    private static ParameterProposal Proposal(string value, string reason = "Synthetic cache proposal", DateTimeOffset? from = null)
    {
        var (proposal, errors) = ParameterValuePolicy.Propose(Cache, 1, "TENANT", null, "VALUE", JsonDocument.Parse(value).RootElement.Clone(), from, null, reason, Now().AddSeconds(-1));
        Assert.Empty(errors);
        return proposal!;
    }

    // Each test isolates itself on its own definition root by using the second synthetic tenant or a unique effective time.
    private async Task<(ParameterWriteResult Proposed, Guid Author)> ProposeNew(PoseidonDbContext db, string value = "45")
    {
        var author = Guid.NewGuid();
        var existing = await db.ParameterRoots.AsNoTracking().SingleOrDefaultAsync(x => x.Key == Cache.Key, Ct);
        var result = await ParameterWriter.ProposeAsync(db, author, "propose-" + Guid.NewGuid().ToString("N"), existing?.Version ?? 0, Cache, Proposal(value),
            db.TenantId, Now(), Allowed, Ct);
        Assert.Null(result.Code);
        return (result, author);
    }

    private async Task Settle(PoseidonDbContext db)
    {
        // Leave the shared root with no pending proposal so the next test can propose.
        var root = await db.ParameterRoots.AsNoTracking().SingleOrDefaultAsync(x => x.Key == Cache.Key, Ct);
        if (root?.PendingRevisionId is { } pending)
            Assert.Null((await ParameterWriter.DecideAsync(db, Guid.NewGuid(), "settle-" + Guid.NewGuid().ToString("N"), root.Id, root.Version, pending, false,
                "Synthetic settle rejection", Now(), Allowed, Ct)).Code);
    }

    [Fact] // P5401-AC-13, P5401-AC-14
    public async Task Proposal_and_independent_approval_append_attributed_lineage_and_become_selectable()
    {
        await using var db = fixture.Db(fixture.Data.Tenants[1].Id); await Settle(db);
        var (proposed, author) = await ProposeNew(db, "42");
        Assert.Equal("IN_REVIEW", proposed.State);
        var chain = new ParameterScopeLevel[] { new("TENANT", db.TenantId), new("GLOBAL", null) };
        var hash = ParameterValuePolicy.Hash("{}");
        Assert.DoesNotContain(await ParameterWriter.CandidatesAsync(db, Cache.Key, chain, hash, Ct), c => c.RevisionId == proposed.RevisionId);
        var approver = Guid.NewGuid();
        var approved = await ParameterWriter.DecideAsync(db, approver, "approve-" + Guid.NewGuid().ToString("N"), proposed.RootId, proposed.Version,
            proposed.RevisionId, true, "Synthetic independent approval", Now(), Allowed, Ct);
        Assert.Null(approved.Code); Assert.Equal("APPROVED", approved.State); Assert.Equal(proposed.Revision + 1, approved.Revision);
        var candidates = await ParameterWriter.CandidatesAsync(db, Cache.Key, chain, hash, Ct);
        var result = ParameterResolver.Resolve(Cache, chain, hash, candidates, approved.SelectedFrom!.Value, approved.SelectedFrom.Value);
        Assert.Equal("RESOLVED", result.Status); Assert.Equal("42", result.ValueJson); Assert.Equal(proposed.RevisionId, result.SourceRevisionId);
        var decision = await db.ParameterDecisions.AsNoTracking().SingleAsync(x => x.RevisionId == proposed.RevisionId, Ct);
        Assert.Equal(approver, decision.DeciderId); Assert.NotEqual(author, decision.DeciderId);
        var audits = await db.AuditEvents.AsNoTracking().Where(x => x.ResourceId == proposed.RootId).ToArrayAsync(Ct);
        Assert.Contains(audits, x => x.EntityType == nameof(ParameterRevision) && x.Action == "Added");
        Assert.Contains(audits, x => x.EntityType == nameof(ParameterDecision) && x.Action == "Added");
        Assert.Contains(audits, x => x.EntityType == nameof(ParameterCommandReceipt));
        Assert.All(audits, x => { Assert.Null(x.VesselId); Assert.Equal("synthetic-parameter-actor", x.ActorId); Assert.DoesNotContain("Synthetic independent approval", x.ChangeSummary); });
        Assert.Contains(audits, x => x.ChangeSummary!.Contains("APPROVED"));
    }

    [Fact] // P5401-AC-15
    public async Task Self_approval_is_rejected_by_domain_and_by_the_database_guard()
    {
        await using var db = fixture.Db(fixture.Data.Tenants[1].Id); await Settle(db);
        var (proposed, author) = await ProposeNew(db);
        var self = await ParameterWriter.DecideAsync(db, author, "self-" + Guid.NewGuid().ToString("N"), proposed.RootId, proposed.Version,
            proposed.RevisionId, true, "Synthetic self approval attempt", Now(), Allowed, Ct);
        Assert.Equal("separation_of_duties", self.Code);
        Assert.False(await db.ParameterDecisions.AnyAsync(x => x.RevisionId == proposed.RevisionId, Ct));
        // Bypassing the writer still cannot record maker == checker.
        var root = await db.ParameterRoots.SingleAsync(x => x.Id == proposed.RootId, Ct);
        root.Revision++; root.PendingRevisionId = null;
        db.ParameterDecisions.Add(new() { TenantId = db.TenantId, Id = Guid.NewGuid(), RootId = root.Id, RevisionId = proposed.RevisionId, Sequence = root.Revision,
            Outcome = "APPROVED", DeciderId = author, DecidedAt = Now(), SelectedFrom = Now(), Reason = "Forged self approval row" });
        var forged = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        AssertTrigger(forged, "23514", "independent decider");
        db.ChangeTracker.Clear();
        Assert.False(await db.ParameterDecisions.AnyAsync(x => x.RevisionId == proposed.RevisionId, Ct));
        await Settle(db);
    }

    // The guard must be the named trigger (SqlState + message), not any SQL failure (typo, FK, check).
    private static void AssertTrigger(Exception error, string sqlState, string message)
    {
        var postgres = error as Npgsql.PostgresException ?? error.InnerException as Npgsql.PostgresException;
        Assert.NotNull(postgres);
        Assert.Equal(sqlState, postgres.SqlState);
        Assert.Contains(message, postgres.MessageText, StringComparison.Ordinal);
    }

    [Fact] // P5401-AC-16
    public async Task Approved_revisions_and_decisions_are_immutable_in_ef_and_in_postgres()
    {
        await using var db = fixture.Db(fixture.Data.Tenants[1].Id); await Settle(db);
        var (proposed, _) = await ProposeNew(db, "33");
        var approved = await ParameterWriter.DecideAsync(db, Guid.NewGuid(), "imm-" + Guid.NewGuid().ToString("N"), proposed.RootId, proposed.Version,
            proposed.RevisionId, true, "Synthetic independent approval", Now(), Allowed, Ct);
        Assert.Null(approved.Code);
        var revision = await db.ParameterRevisions.SingleAsync(x => x.Id == proposed.RevisionId, Ct);
        revision.ValueJson = "1";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        var decision = await db.ParameterDecisions.SingleAsync(x => x.RevisionId == proposed.RevisionId, Ct);
        db.ParameterDecisions.Remove(decision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        var root = await db.ParameterRoots.SingleAsync(x => x.Id == proposed.RootId, Ct);
        root.ScopeKind = "COMPANY"; root.Revision++;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        AssertTrigger(await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE config.\"ParameterRevisions\" SET \"ValueJson\"='1' WHERE \"TenantId\"={db.TenantId} AND \"Id\"={proposed.RevisionId}", Ct)), "55000", "Parameter lineage is immutable");
        AssertTrigger(await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM config.\"ParameterDecisions\" WHERE \"TenantId\"={db.TenantId} AND \"RevisionId\"={proposed.RevisionId}", Ct)), "55000", "Parameter lineage is immutable");
        AssertTrigger(await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM config.\"ParameterRoots\" WHERE \"TenantId\"={db.TenantId} AND \"Id\"={proposed.RootId}", Ct)), "55000", "Parameter roots cannot be removed");
        Assert.Equal("33", (await db.ParameterRevisions.AsNoTracking().SingleAsync(x => x.Id == proposed.RevisionId, Ct)).ValueJson);
    }

    [Fact] // P5401-AC-17
    public async Task Another_tenant_cannot_see_select_or_decide_a_tenant_value()
    {
        await using var owner = fixture.Db(fixture.Data.Tenants[1].Id); await Settle(owner);
        var (proposed, _) = await ProposeNew(owner, "21");
        await using var other = fixture.Db(fixture.Data.Tenants[0].Id);
        Assert.False(await other.ParameterRoots.AnyAsync(x => x.Id == proposed.RootId, Ct));
        Assert.False(await other.ParameterRevisions.AnyAsync(x => x.Id == proposed.RevisionId, Ct));
        var decided = await ParameterWriter.DecideAsync(other, Guid.NewGuid(), "cross-" + Guid.NewGuid().ToString("N"), proposed.RootId, proposed.Version,
            proposed.RevisionId, true, "Synthetic cross tenant approval", Now(), Allowed, Ct);
        Assert.Equal("resource_not_found", decided.Code);
        Assert.Empty(await ParameterWriter.CandidatesAsync(other, Cache.Key, [new("TENANT", fixture.Data.Tenants[1].Id)], ParameterValuePolicy.Hash("{}"), Ct));
        other.ParameterRoots.Add(new() { TenantId = fixture.Data.Tenants[1].Id, Id = Guid.NewGuid(), Key = Cache.Key, ScopeKind = "TENANT",
            ScopeId = fixture.Data.Tenants[1].Id, SelectorJson = "{}", SelectorHash = ParameterValuePolicy.Hash("{}"), Revision = 1, CreatedAt = Now() });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => other.SaveChangesAsync(Ct));
        await Settle(owner);
    }

    [Fact] // P5401-AC-18
    public async Task Receipts_replay_exactly_reject_reuse_and_stale_versions_conflict()
    {
        await using var db = fixture.Db(fixture.Data.Tenants[1].Id); await Settle(db);
        var root = await db.ParameterRoots.AsNoTracking().SingleOrDefaultAsync(x => x.Key == Cache.Key, Ct);
        var actor = Guid.NewGuid(); var key = "replay-" + Guid.NewGuid().ToString("N"); var proposal = Proposal("50");
        var first = await ParameterWriter.ProposeAsync(db, actor, key, root?.Version ?? 0, Cache, proposal, db.TenantId, Now(), Allowed, Ct);
        Assert.Null(first.Code);
        var audits = await db.AuditEvents.CountAsync(Ct);
        var replay = await ParameterWriter.ProposeAsync(db, actor, key, root?.Version ?? 0, Cache, proposal, db.TenantId, Now(), Allowed, Ct);
        Assert.True(replay.Replayed); Assert.Equal(first.RevisionId, replay.RevisionId); Assert.Equal(first.Version, replay.Version);
        Assert.Equal(audits, await db.AuditEvents.CountAsync(Ct));
        Assert.Equal("idempotency_key_reused", (await ParameterWriter.ProposeAsync(db, actor, key, root?.Version ?? 0, Cache, Proposal("51"), db.TenantId, Now(), Allowed, Ct)).Code);
        Assert.Equal("idempotency_key_reused", (await ParameterWriter.ProposeAsync(db, Guid.NewGuid(), key, root?.Version ?? 0, Cache, proposal, db.TenantId, Now(), Allowed, Ct)).Code);
        Assert.Equal("pending_change_exists", (await ParameterWriter.ProposeAsync(db, actor, "pending-" + Guid.NewGuid().ToString("N"), first.Version, Cache, Proposal("52"), db.TenantId, Now(), Allowed, Ct)).Code);
        Assert.Equal("version_conflict", (await ParameterWriter.DecideAsync(db, Guid.NewGuid(), "stale-" + Guid.NewGuid().ToString("N"), first.RootId, first.Version + 1,
            first.RevisionId, false, "Synthetic stale rejection", Now(), Allowed, Ct)).Code);
        Assert.Equal(1, await db.ParameterRevisions.CountAsync(x => x.Id == first.RevisionId, Ct));
        await Settle(db);
    }

    [Fact] // P5401-AC-19
    public async Task Commit_time_revocation_rolls_back_rows_audit_and_receipt()
    {
        await using var db = fixture.Db(fixture.Data.Tenants[1].Id); await Settle(db);
        var root = await db.ParameterRoots.AsNoTracking().SingleOrDefaultAsync(x => x.Key == Cache.Key, Ct);
        var key = "revoked-" + Guid.NewGuid().ToString("N");
        var audits = await db.AuditEvents.CountAsync(Ct);
        var result = await ParameterWriter.ProposeAsync(db, Guid.NewGuid(), key, root?.Version ?? 0, Cache, Proposal("44"), db.TenantId, Now(), () => Task.FromResult(false), Ct);
        Assert.Equal("permission_denied", result.Code);
        Assert.False(await db.ParameterCommandReceipts.AnyAsync(x => x.Key == key, Ct));
        Assert.Equal(audits, await db.AuditEvents.CountAsync(Ct));
        Assert.Null((await db.ParameterRoots.AsNoTracking().SingleOrDefaultAsync(x => x.Key == Cache.Key, Ct))?.PendingRevisionId);
    }

    [Fact] // P5401-AC-20
    public async Task Optional_fixture_is_idempotent_unapprovable_and_preserves_operator_decisions()
    {
        await using (var import = fixture.Import())
        {
            await ParameterFixtures.AddMissingAsync(import, fixture.Data.Tenants, Ct);
            await ParameterFixtures.AddMissingAsync(import, fixture.Data.Tenants, Ct);
            await import.SaveChangesAsync(Ct);
        }
        var tenant = fixture.Data.Tenants[0].Id;
        await using var db = fixture.Db(tenant);
        var root = await db.ParameterRoots.AsNoTracking().SingleAsync(x => x.Id == ParameterFixtures.RootId(tenant), Ct);
        var revision = await db.ParameterRevisions.AsNoTracking().SingleAsync(x => x.RootId == root.Id, Ct);
        Assert.Equal("SyntheticUnverified", revision.Provenance); Assert.Equal("INHERIT", revision.ValueMode); Assert.Null(revision.ValueJson);
        Assert.Equal(Guid.Empty, revision.AuthorId); Assert.False(await db.ParameterDecisions.AnyAsync(x => x.RootId == root.Id, Ct));
        var approve = await ParameterWriter.DecideAsync(db, Guid.NewGuid(), "fixture-" + Guid.NewGuid().ToString("N"), root.Id, root.Version, revision.Id, true,
            "Synthetic approval of fixture attempt", Now(), Allowed, Ct);
        Assert.Equal("synthetic_not_approvable", approve.Code);
        var reject = await ParameterWriter.DecideAsync(db, Guid.NewGuid(), "fixture-" + Guid.NewGuid().ToString("N"), root.Id, root.Version, revision.Id, false,
            "Operator rejects synthetic fixture", Now(), Allowed, Ct);
        Assert.Null(reject.Code);
        var audits = await db.AuditEvents.CountAsync(x => x.ResourceId == root.Id, Ct);
        await using (var import = fixture.Import()) { await ParameterFixtures.AddMissingAsync(import, fixture.Data.Tenants, Ct); await import.SaveChangesAsync(Ct); }
        Assert.Equal(audits, await db.AuditEvents.CountAsync(x => x.ResourceId == root.Id, Ct));
        Assert.Equal("REJECTED", (await db.ParameterDecisions.AsNoTracking().SingleAsync(x => x.RootId == root.Id, Ct)).Outcome);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ParameterFixtures.AddMissingAsync(db, fixture.Data.Tenants, Ct));
    }
}
