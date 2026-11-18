using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class ProfileFieldPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    public SeedData Data { get; private set; } = null!;
    public PoseidonDbContext Db(Guid? tenant = null, string actor = "10000000-0000-4000-8000-0000000000aa") =>
        new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? Data.Tenants[0].Id, new(actor, "User", "profile-field-source-test"));
    public PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(); Data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(); await SeedImporter.ImportAsync(db, Data);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}

// DEV-020 / T-S2-001: DEV020-AC-02/03/07/08/10 on real PostgreSQL.
[Trait("Category", "Integration")]
public sealed class VesselProfileFieldSourcePersistenceTests(ProfileFieldPostgresFixture fixture) : IClassFixture<ProfileFieldPostgresFixture>
{
    private static readonly Guid Actor = Guid.Parse("10000000-0000-4000-8000-0000000000aa");
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Vessel> NewProfile(Guid tenant, params RegulatoryDraftContent[] revisions)
    {
        await using var db = fixture.Db(tenant);
        var company = fixture.Data.Companies.First(c => c.TenantId == tenant);
        var vessel = new Vessel { Id = Guid.NewGuid(), TenantId = tenant, CompanyId = company.Id, Code = "PF-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "Synthetic field vessel", Kind = VesselKind.Ship };
        db.Vessels.Add(vessel);
        var draft = new RegulatoryDraft { TenantId = tenant, VesselId = vessel.Id, Revision = 1, SavedAt = DateTimeOffset.UtcNow, Provenance = "Missing",
            ContentJson = RegulatoryDraftStorage.Serialize(revisions.Length > 0 ? revisions[0] : new()) };
        db.RegulatoryDrafts.Add(draft); db.RegulatoryDraftRevisions.Add(draft.Snapshot());
        await db.SaveChangesAsync(Ct);
        foreach (var content in revisions.Skip(1))
        {
            draft.ContentJson = RegulatoryDraftStorage.Serialize(content); draft.Revision++; draft.SavedAt = DateTimeOffset.UtcNow;
            db.RegulatoryDraftRevisions.Add(draft.Snapshot()); await db.SaveChangesAsync(Ct);
        }
        return vessel;
    }

    private static VesselProfileFieldSource Source(Vessel vessel, string field = "gt", long sequence = 1, long valueRevision = 1, string state = "Known", string? value = "100") => new()
    {
        TenantId = vessel.TenantId, VesselId = vessel.Id, FieldPath = field, Sequence = sequence, ValueRevision = valueRevision,
        ValueState = state, ValueHash = VesselProfileFieldPolicy.ValueHash(state, value), SourceKind = "DOCUMENT",
        ReferenceState = "Known", Reference = "Withheld synthetic reference", ObservedAtState = "Known", ObservedAt = DateTimeOffset.UtcNow.AddDays(-1),
        DeclaredBy = Actor, StewardCompanyId = vessel.CompanyId, RecordedAt = VesselProfileFieldPolicy.Truncate(DateTimeOffset.UtcNow), Provenance = "DeclaredUnverified"
    };

    [Fact]
    public async Task Value_versions_are_derived_from_immutable_profile_revisions_without_a_second_copy()
    {
        var vessel = await NewProfile(fixture.Data.Tenants[0].Id,
            new() { Gt = new("Known", "100"), Flag = new("Unknown") },
            new() { Gt = new("Known", "100"), Flag = new("Known", "Synthetic flag") },
            new() { Gt = new("Known", "120"), Flag = new("Known", "Synthetic flag") },
            new() { Gt = new("Known", "120"), Flag = new("Known", "Synthetic flag") });
        await using var db = fixture.Db();
        var current = await VesselProfileFieldStorage.CurrentValueRevisions(db, vessel.TenantId, vessel.Id, 4, ["gt", "flag", "imo"], Ct);
        Assert.Equal(3, current["gt"]); Assert.Equal(2, current["flag"]); Assert.Equal(1, current["imo"]);
        var asOfTwo = await VesselProfileFieldStorage.CurrentValueRevisions(db, vessel.TenantId, vessel.Id, 2, ["gt"], Ct);
        Assert.Equal(1, asOfTwo["gt"]);
        var versions = await VesselProfileFieldStorage.ValueVersions(db, vessel.TenantId, vessel.Id, "gt", 0, 10, Ct);
        Assert.Equal([3L, 1L], versions.Select(x => x.Revision));
        Assert.Equal(["120", "100"], versions.Select(x => x.Value));
        var flag = await VesselProfileFieldStorage.ValueVersions(db, vessel.TenantId, vessel.Id, "flag", 0, 10, Ct);
        Assert.Equal([("Known", "Synthetic flag"), ("Unknown", (string?)null)], flag.Select(x => (x.State, x.Value)));
        // Explicit tenant predicate: another tenant's identifiers derive nothing.
        await using var other = fixture.Db(fixture.Data.Tenants[1].Id);
        Assert.Empty(await VesselProfileFieldStorage.CurrentValueRevisions(other, fixture.Data.Tenants[1].Id, vessel.Id, 4, ["gt"], Ct));
        Assert.Empty(await VesselProfileFieldStorage.ValueVersions(other, fixture.Data.Tenants[1].Id, vessel.Id, "gt", 0, 10, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => VesselProfileFieldStorage.ValueVersions(db, vessel.TenantId, vessel.Id, "x' OR 1=1 --", 0, 10, Ct));
    }

    [Fact]
    public async Task Declaration_is_attributed_audited_without_values_and_immutable_in_ef_and_sql()
    {
        var vessel = await NewProfile(fixture.Data.Tenants[0].Id, new RegulatoryDraftContent { Gt = new("Known", "100") });
        await using (var db = fixture.Db())
        {
            db.VesselProfileFieldSources.Add(Source(vessel)); await db.SaveChangesAsync(Ct);
        }
        await using var read = fixture.Db();
        var audit = await read.AuditEvents.AsNoTracking().SingleAsync(x => x.EntityType == nameof(VesselProfileFieldSource) && x.VesselId == vessel.Id, Ct);
        Assert.Equal(Actor.ToString(), audit.ActorId); Assert.Equal("User", audit.ActorType); Assert.Equal(1, audit.AfterRevision);
        Assert.Contains("DOCUMENT", audit.ChangeSummary); Assert.Contains("\"gt\"", audit.ChangeSummary);
        Assert.DoesNotContain("Withheld synthetic reference", audit.ChangeSummary);
        Assert.DoesNotContain(VesselProfileFieldPolicy.ValueHash("Known", "100"), audit.ChangeSummary);
        var row = await read.VesselProfileFieldSources.SingleAsync(x => x.VesselId == vessel.Id, Ct);
        row.SourceKind = "OTHER";
        await Assert.ThrowsAsync<InvalidOperationException>(() => read.SaveChangesAsync(Ct));
        read.ChangeTracker.Clear();
        foreach (var sql in new[] {
            $"UPDATE core.\"VesselProfileFieldSources\" SET \"SourceKind\"='OTHER' WHERE \"VesselId\"='{vessel.Id}'",
            $"DELETE FROM core.\"VesselProfileFieldSources\" WHERE \"VesselId\"='{vessel.Id}'",
            "TRUNCATE core.\"VesselProfileFieldSources\" CASCADE" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => read.Database.ExecuteSqlRawAsync(sql, Ct));
            Assert.Equal("55000", error.SqlState);
        }
        Assert.Equal("DOCUMENT", (await read.VesselProfileFieldSources.AsNoTracking().SingleAsync(x => x.VesselId == vessel.Id, Ct)).SourceKind);
    }

    [Fact]
    public async Task Lineage_gaps_backward_value_versions_and_unattested_missing_values_are_rejected_by_postgres()
    {
        var vessel = await NewProfile(fixture.Data.Tenants[0].Id,
            new() { Gt = new("Known", "100") }, new() { Gt = new("Known", "120") });
        async Task<PostgresException> Rejected(VesselProfileFieldSource row)
        {
            await using var db = fixture.Db(); db.VesselProfileFieldSources.Add(row);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            return Assert.IsType<PostgresException>(error.InnerException);
        }
        Assert.Equal("23514", (await Rejected(Source(vessel, sequence: 2))).SqlState);
        await using (var db = fixture.Db()) { db.VesselProfileFieldSources.Add(Source(vessel, valueRevision: 2, value: "120")); await db.SaveChangesAsync(Ct); }
        Assert.Equal("23514", (await Rejected(Source(vessel, sequence: 2, valueRevision: 1))).SqlState);
        Assert.Equal("23514", (await Rejected(Source(vessel, "imo", state: "Missing", value: null))).SqlState);
        // Unknown value revision of this vessel violates the exact profile-revision FK.
        Assert.Equal("23503", (await Rejected(Source(vessel, "flag", valueRevision: 9))).SqlState);
    }

    [Fact]
    public async Task Tenant_scope_filters_reads_and_rejects_cross_tenant_writes_and_bindings()
    {
        var mine = await NewProfile(fixture.Data.Tenants[0].Id, new RegulatoryDraftContent { Gt = new("Known", "100") });
        var theirs = await NewProfile(fixture.Data.Tenants[1].Id, new RegulatoryDraftContent { Gt = new("Known", "100") });
        await using (var db = fixture.Db()) { db.VesselProfileFieldSources.Add(Source(mine)); await db.SaveChangesAsync(Ct); }
        await using var other = fixture.Db(fixture.Data.Tenants[1].Id);
        Assert.False(await other.VesselProfileFieldSources.AnyAsync(x => x.VesselId == mine.Id, Ct));
        // EF tenant fence: a tenant-two context cannot write a tenant-one row.
        other.VesselProfileFieldSources.Add(Source(mine, "flag", value: null, state: "Unknown"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => other.SaveChangesAsync(Ct));
        other.ChangeTracker.Clear();
        // Composite FK: a steward company of another tenant cannot be bound.
        var forged = Source(theirs);
        forged.StewardCompanyId = mine.CompanyId;
        other.VesselProfileFieldSources.Add(forged);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync(Ct));
        Assert.Equal("23503", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Concurrent_declarations_of_the_same_sequence_commit_exactly_one()
    {
        var vessel = await NewProfile(fixture.Data.Tenants[0].Id, new RegulatoryDraftContent { Gt = new("Known", "100") });
        await using var first = fixture.Db(); await using var second = fixture.Db();
        first.VesselProfileFieldSources.Add(Source(vessel)); second.VesselProfileFieldSources.Add(Source(vessel));
        second.VesselProfileFieldSources.Local.Single().SourceKind = "OTHER";
        var results = await Task.WhenAll(Attempt(first), Attempt(second));
        Assert.Single(results, x => x == null);
        Assert.Single(results, x => x == "23505");
        await using var read = fixture.Db();
        Assert.Equal(1, await read.VesselProfileFieldSources.CountAsync(x => x.VesselId == vessel.Id, Ct));
        Assert.Equal(1, await read.AuditEvents.CountAsync(x => x.EntityType == nameof(VesselProfileFieldSource) && x.VesselId == vessel.Id, Ct));
        async Task<string?> Attempt(PoseidonDbContext db)
        {
            try { await db.SaveChangesAsync(Ct); return null; }
            catch (DbUpdateException ex) { return PostgresFailure.Provider(ex)?.SqlState; }
        }
    }

    [Fact]
    public async Task Optional_fixture_is_synthetic_incomplete_idempotent_and_preserves_operator_edits()
    {
        await using var import = fixture.Import();
        await using (var plain = fixture.Db()) await Assert.ThrowsAsync<UnauthorizedAccessException>(() => VesselProfileFieldSourceFixtures.AddMissingAsync(plain, fixture.Data.Vessels, Ct));
        // Operator-edited profile (revision 2) for the first seed vessel is never annotated.
        var edited = fixture.Data.Vessels[0];
        await using (var db = fixture.Db(edited.TenantId))
        {
            var draft = await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == edited.Id, Ct);
            if (draft.Revision == 1) { draft.Revision = 2; draft.Provenance = "DeclaredUnverified"; draft.SavedAt = DateTimeOffset.UtcNow; db.RegulatoryDraftRevisions.Add(draft.Snapshot()); await db.SaveChangesAsync(Ct); }
        }
        var added = await VesselProfileFieldSourceFixtures.AddMissingAsync(import, fixture.Data.Vessels, Ct);
        await import.SaveChangesAsync(Ct);
        Assert.Equal(fixture.Data.Vessels.Length - 1, added);
        var rows = await import.VesselProfileFieldSources.AsNoTracking().Where(x => x.Provenance == "SyntheticUnverified").ToArrayAsync(Ct);
        Assert.Equal(added, rows.Length);
        Assert.DoesNotContain(rows, x => x.VesselId == edited.Id);
        Assert.All(rows, x =>
        {
            Assert.Equal(("UNKNOWN", "Unknown", "Unknown", "Missing"), (x.SourceKind, x.ReferenceState, x.ObservedAtState, x.ValueState));
            Assert.Null(x.Reference); Assert.Null(x.ObservedAt); Assert.Equal(Guid.Empty, x.DeclaredBy);
            var vessel = fixture.Data.Vessels.Single(v => v.Id == x.VesselId); Assert.Equal(vessel.CompanyId, x.StewardCompanyId);
        });
        var audits = await import.AuditEvents.CountAsync(Ct);
        Assert.Equal(0, await VesselProfileFieldSourceFixtures.AddMissingAsync(import, fixture.Data.Vessels, Ct));
        await import.SaveChangesAsync(Ct);
        Assert.Equal(audits, await import.AuditEvents.CountAsync(Ct));
        // A seeded row never evaluates as a declared or fresh source.
        var freshness = VesselProfileFieldPolicy.Evaluate("imo", rows[0].ValueState, 1, null, true, FreshnessPolicyResolution.Unconfigured, DateTimeOffset.UtcNow);
        Assert.Equal("UNKNOWN", freshness.State);
    }
}
