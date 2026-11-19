using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class ControlEvidencePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData seed = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? seed.Tenants[0].Id, new("synthetic-author", "User", "synthetic-control-evidence"));
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync() { await postgres.StartAsync(Ct); seed = SeedValidationTests.Demo(); await using var db = Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db, seed, Ct); }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    private static ControlEvidenceRevision Revision(ControlEvidenceHead h) => new() { TenantId = h.TenantId, VesselId = h.VesselId, Kind = h.Kind, Id = h.Id, Revision = h.Revision,
        ContentJson = "{}", Hash = RegisterStorage.Hash("{}"), ReasonCode = "AUTHORING_CORRECTION", SavedAt = DateTimeOffset.UtcNow };
    [Fact]
    public async Task Reseed_preserves_operator_revision_without_observed_files_owners_or_verifiers() {
        Guid id;
        await using (var db = Db()) {
            var head = await db.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Control", Ct); id = head.Id; head.Revision++;
            db.ControlEvidenceRevisions.Add(Revision(head)); await db.SaveChangesAsync(Ct);
        }
        await using var import = Import(); var before = await import.AuditEvents.CountAsync(Ct);
        await SeedImporter.ImportAsync(import, SeedValidationTests.Demo(), Ct); await SeedImporter.ImportAsync(import, SeedValidationTests.Demo(), Ct);
        Assert.Equal(before, await import.AuditEvents.CountAsync(Ct)); Assert.Equal(2, (await import.ControlEvidenceHeads.SingleAsync(x => x.Id == id, Ct)).Revision);
        Assert.Empty(await import.StoredFiles.ToArrayAsync(Ct)); Assert.Empty(await import.ControlEvidenceReceipts.ToArrayAsync(Ct));
        Assert.All(await import.ControlEvidenceRevisions.ToArrayAsync(Ct), r => { Assert.Null(r.ActorId); Assert.Null(r.FileId); Assert.Equal("DRAFT", r.Lifecycle); });
    }
    [Fact]
    public async Task Tenant_filters_and_cross_vessel_composite_edges_reject_object_leaks() {
        await using var db = Db(); Assert.All(await db.ControlEvidenceHeads.ToArrayAsync(Ct), x => Assert.Equal(db.TenantId, x.TenantId));
        var evidence = await db.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Evidence", Ct);
        var other = seed.Vessels.First(x => x.Id != evidence.VesselId);
        db.ControlEvidenceEdges.Add(new() { TenantId = db.TenantId, VesselId = evidence.VesselId, Kind = "Evidence", Id = evidence.Id, Revision = 1,
            TargetKind = "Control", TargetId = ControlEvidenceFixtures.Id(other.Id, "Control"), TargetRevision = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        await using var check = Db(); Assert.Empty(await check.ControlEvidenceEdges.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Missing_and_wrong_vessel_file_versions_cannot_enter_graph_and_rollback_audits() {
        await using var db = Db(); var head = await db.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Evidence", Ct); var before = await db.AuditEvents.CountAsync(Ct);
        head.Revision++; var r = Revision(head); r.FileId = Guid.NewGuid(); r.FileRevision = 1; r.Checksum = new string('A', 64); db.ControlEvidenceRevisions.Add(r);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        await using var verify = Db(); Assert.Equal(1, (await verify.ControlEvidenceHeads.SingleAsync(x => x.Id == head.Id, Ct)).Revision); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
    }
    [Fact]
    public async Task Append_only_history_edges_requirements_receipts_guard_all_database_mutations() {
        await using var db = Db(); var revision = await db.ControlEvidenceRevisions.FirstAsync(Ct); revision.ContentJson = "{\"forged\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        // Closed test-owned identifier/verb lists; deliberate trigger mutation probes.
#pragma warning disable EF1002
        foreach (var table in new[] { "ControlEvidenceRevisions", "ControlEvidenceRequirements", "ControlEvidenceEdges", "ControlEvidenceReceipts" })
        foreach (var action in new[] { "DELETE FROM", "TRUNCATE" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync($"{action} core.\"{table}\"", Ct));
#pragma warning restore EF1002
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE core.\"ControlEvidenceRevisions\" SET \"Hash\"='forged'", Ct));
    }
    [Fact]
    public async Task Xmin_prevents_stale_noop_authoring_and_transaction_preserves_old_history() {
        await using var a = Db(); await using var b = Db(); var first = await a.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Control", Ct);
        var stale = await b.ControlEvidenceHeads.SingleAsync(x => x.Id == first.Id, Ct); first.Revision++; a.ControlEvidenceRevisions.Add(Revision(first)); await a.SaveChangesAsync(Ct);
        stale.Revision = 3; b.ControlEvidenceRevisions.Add(Revision(stale)); b.Entry(stale).Property(x => x.Revision).IsModified = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync(Ct));
        await using var verify = Db(); Assert.Equal(2, await verify.ControlEvidenceRevisions.CountAsync(x => x.Id == first.Id, Ct));
    }
    [Fact]
    public async Task Explicit_rollback_removes_head_history_links_and_audit_atomically() {
        await using var db = Db(); var head = await db.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Evidence", Ct); var before = await db.AuditEvents.CountAsync(Ct);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) { head.Revision++; db.ControlEvidenceRevisions.Add(Revision(head)); await db.SaveChangesAsync(Ct); await tx.RollbackAsync(Ct); }
        await using var verify = Db(); Assert.Equal(1, (await verify.ControlEvidenceHeads.SingleAsync(x => x.Id == head.Id, Ct)).Revision); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
    }

    // DEV-028: lifecycle state is server-guarded; every change carries its append-only event.
    private static ControlStateEvent StateEvent(ControlEvidenceHead h, string from, string to, string reason) => new() { TenantId = h.TenantId, VesselId = h.VesselId, ControlId = h.Id,
        Revision = 1, ControlRevision = h.Revision, FromState = from, ToState = to, ReasonCode = reason, Rationale = "FICTIONAL private rationale", ActorId = Guid.NewGuid(), SavedAt = DateTimeOffset.UtcNow };
    [Fact]
    [Trait("Requirement", "DEV-028")]
    public async Task Seeded_controls_start_draft_and_state_change_requires_its_exact_event() {
        await using var db = Db();
        // Schema-only lifecycle: a control with no lifecycle event stores NULL and derives DRAFT.
        Assert.All(await db.ControlEvidenceHeads.Where(x => x.Kind == "Control").ToArrayAsync(Ct), x => { Assert.Null(x.ControlState); Assert.Equal("DRAFT", ControlLifecyclePolicy.State(x.ControlState)); });
        Assert.All(await db.ControlEvidenceHeads.Where(x => x.Kind == "Evidence").ToArrayAsync(Ct), x => Assert.Null(x.ControlState));
        Assert.Empty(await db.ControlStateEvents.ToArrayAsync(Ct));
        var head = await db.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Control", Ct);
        head.ControlState = "IN_REVIEW";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.Add(StateEvent(head, "DRAFT", "APPROVED", "SUBMIT_FOR_REVIEW"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        head = await db.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Control", Ct);
        head.ControlState = "IN_REVIEW"; db.Add(StateEvent(head, "DRAFT", "IN_REVIEW", "SUBMIT_FOR_REVIEW"));
        await db.SaveChangesAsync(Ct);
        await using var verify = Db();
        Assert.Equal("IN_REVIEW", (await verify.ControlEvidenceHeads.SingleAsync(x => x.Id == head.Id && x.Kind == "Control", Ct)).ControlState);
        var audit = Assert.Single(await verify.AuditEvents.Where(a => a.EntityType == nameof(ControlStateEvent) && a.ResourceId == head.Id).ToArrayAsync(Ct));
        Assert.DoesNotContain("FICTIONAL private rationale", audit.ChangeSummary);
    }
    [Fact]
    [Trait("Requirement", "DEV-028")]
    public async Task Database_rejects_evidence_lifecycle_invalid_transition_and_event_mutation() {
        await using var db = Db();
        var evidence = await db.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Evidence", Ct);
        // Check constraint and closed event grammar hold even below the context guard.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql(db, $"UPDATE core.\"ControlEvidenceHeads\" SET \"ControlState\" = 'DRAFT' WHERE \"Id\" = '{evidence.Id}' AND \"Kind\" = 'Evidence'"));
        var control = await db.ControlEvidenceHeads.FirstAsync(x => x.Kind == "Control", Ct);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql(db, $"UPDATE core.\"ControlEvidenceHeads\" SET \"ControlState\" = 'ACTIVE' WHERE \"Id\" = '{control.Id}' AND \"Kind\" = 'Control'"));
        // 62.01 section 6 current pointer: PUBLISHED needs the pointer on its own revision; no pointer on evidence,
        // on a retired control, on a control with no lifecycle state, or beyond the latest revision.
        foreach (var statement in new[] {
            $"UPDATE core.\"ControlEvidenceHeads\" SET \"ControlState\" = 'PUBLISHED' WHERE \"Id\" = '{control.Id}' AND \"Kind\" = 'Control'",
            $"UPDATE core.\"ControlEvidenceHeads\" SET \"PublishedRevision\" = 1 WHERE \"Id\" = '{evidence.Id}' AND \"Kind\" = 'Evidence'",
            $"UPDATE core.\"ControlEvidenceHeads\" SET \"PublishedRevision\" = 1 WHERE \"Id\" = '{control.Id}' AND \"Kind\" = 'Control'",
            $"UPDATE core.\"ControlEvidenceHeads\" SET \"ControlState\" = 'RETIRED', \"PublishedRevision\" = 1 WHERE \"Id\" = '{control.Id}' AND \"Kind\" = 'Control'",
            $"UPDATE core.\"ControlEvidenceHeads\" SET \"ControlState\" = 'DRAFT', \"PublishedRevision\" = \"Revision\" + 1 WHERE \"Id\" = '{control.Id}' AND \"Kind\" = 'Control'" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql(db, statement));
        control.ControlState = "IN_REVIEW"; db.Add(StateEvent(control, "DRAFT", "IN_REVIEW", "SUBMIT_FOR_REVIEW")); await db.SaveChangesAsync(Ct);
        var stored = await db.ControlStateEvents.SingleAsync(Ct); stored.Rationale = "forged";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        foreach (var statement in new[] { "UPDATE core.\"ControlStateEvents\" SET \"ToState\" = 'RETIRED'", "DELETE FROM core.\"ControlStateEvents\"", "TRUNCATE core.\"ControlStateEvents\"" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql(db, statement));
        await using var import = Import(); await SeedImporter.ImportAsync(import, SeedValidationTests.Demo(), Ct);
        Assert.Equal("IN_REVIEW", (await import.ControlEvidenceHeads.SingleAsync(x => x.Id == control.Id && x.Kind == "Control", Ct)).ControlState);
        Assert.Single(await import.ControlStateEvents.ToArrayAsync(Ct));
    }
#pragma warning disable EF1002
    // Closed test-owned statements only; deliberate constraint/trigger probes.
    private Task<int> Sql(PoseidonDbContext db, string statement) => db.Database.ExecuteSqlRawAsync(statement, Ct);
#pragma warning restore EF1002
}
