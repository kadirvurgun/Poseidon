using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class HumanReviewPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private SeedData seed = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? seed.Tenants[0].Id, new("synthetic-review-test", "User", "synthetic-review"));
    private PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync() { await postgres.StartAsync(Ct); seed = SeedValidationTests.Demo(); await using var db = Import(); await db.Database.MigrateAsync(Ct); await SeedImporter.ImportAsync(db, seed, Ct); }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    private HumanReviewHead Head() => new() { TenantId = seed.Tenants[0].Id, VesselId = seed.Vessels.First(v => v.TenantId == seed.Tenants[0].Id).Id, Id = Guid.NewGuid(), Revision = 1 };
    private static HumanReviewEvent Event(HumanReviewHead h) => new() { TenantId = h.TenantId, VesselId = h.VesselId, Id = h.Id, Revision = h.Revision,
        Kind = "Verification", State = "PENDING", EvidenceId = ControlEvidenceFixtures.Id(h.VesselId, "Evidence"), EvidenceRevision = 1,
        ControlId = ControlEvidenceFixtures.Id(h.VesselId, "Control"), ControlRevision = 1, ActorId = Guid.NewGuid(), SubmitterId = Guid.NewGuid(),
        AuthorityId = Guid.NewGuid(), RoleId = Guid.NewGuid(), OperationId = Guid.NewGuid(), ReasonCode = "REQUEST_REVIEW", Rationale = "FICTIONAL test draft; no sign-off", AuthorityHash = RegisterStorage.Hash("synthetic-authority"), ContextHash = RegisterStorage.Hash("synthetic-review"), SavedAt = DateTimeOffset.UtcNow };
    [Fact]
    public async Task Seed_never_fabricates_human_events_and_reseed_preserves_actual_synthetic_session_event()
    {
        await using var db = Db(); Assert.Empty(await db.HumanReviewEvents.ToArrayAsync(Ct));
        var head = Head(); var e = Event(head); db.HumanReviewHeads.Add(head); db.HumanReviewEvents.Add(e); await db.SaveChangesAsync(Ct);
        await using var import = Import(); var audits = await import.AuditEvents.CountAsync(Ct);
        await SeedImporter.ImportAsync(import, seed, Ct); await SeedImporter.ImportAsync(import, seed, Ct);
        Assert.Equal(audits, await import.AuditEvents.CountAsync(Ct)); Assert.Equal(e.Rationale, (await import.HumanReviewEvents.SingleAsync(Ct)).Rationale);
        Assert.Empty(await import.HumanReviewReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Composite_tenant_vessel_evidence_control_and_evaluation_keys_fail_atomically()
    {
        await using var db = Db(); var audits = await db.AuditEvents.CountAsync(Ct);
        var h = Head(); var e = Event(h); e.ControlId = ControlEvidenceFixtures.Id(seed.Vessels.First(v => v.TenantId != h.TenantId).Id, "Control");
        db.HumanReviewHeads.Add(h); db.HumanReviewEvents.Add(e); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear(); h = Head(); e = Event(h); e.EvaluationId = Guid.NewGuid(); db.HumanReviewHeads.Add(h); db.HumanReviewEvents.Add(e);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        await using var check = Db(); Assert.Empty(await check.HumanReviewHeads.ToArrayAsync(Ct)); Assert.Equal(audits, await check.AuditEvents.CountAsync(Ct));
    }
    [Fact]
    public async Task Event_and_receipt_mutations_are_rejected_by_EF_and_database()
    {
        await using var db = Db(); var h = Head(); var e = Event(h); db.HumanReviewHeads.Add(h); db.HumanReviewEvents.Add(e); await db.SaveChangesAsync(Ct);
        e.Rationale = "forged"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        foreach (var table in new[] { "HumanReviewEvents", "HumanReviewReceipts" })
        foreach (var action in new[] { "DELETE FROM", "TRUNCATE" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync($"{action} core.\"{table}\"", Ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE core.\"HumanReviewEvents\" SET \"State\"='VERIFIED'", Ct));
    }
    [Fact]
    public async Task Xmin_prevents_stale_noop_and_rollback_preserves_head_event_receipt_and_audit()
    {
        var h = Head(); await using (var initial = Db()) { initial.HumanReviewHeads.Add(h); initial.HumanReviewEvents.Add(Event(h)); await initial.SaveChangesAsync(Ct); }
        await using var a = Db(); await using var b = Db(); var x = await a.HumanReviewHeads.SingleAsync(Ct); var y = await b.HumanReviewHeads.SingleAsync(Ct);
        x.Revision++; a.HumanReviewEvents.Add(Event(x)); await a.SaveChangesAsync(Ct); y.Revision++;
        b.Entry(y).Property(z => z.Revision).IsModified = true; b.HumanReviewEvents.Add(Event(y)); await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync(Ct));
        await using var check = Db(); var audits = await check.AuditEvents.CountAsync(Ct); var head = await check.HumanReviewHeads.SingleAsync(Ct);
        await using (var tx = await check.Database.BeginTransactionAsync(Ct)) {
            head.Revision++; check.HumanReviewEvents.Add(Event(head)); await check.SaveChangesAsync(Ct);
            check.HumanReviewReceipts.Add(new() { TenantId = head.TenantId, VesselId = head.VesselId, ResourceId = head.Id, ActorId = Guid.NewGuid(), Key = "synthetic-receipt-0001", Fingerprint = "synthetic", OutcomeJson = "{}", SavedAt = DateTimeOffset.UtcNow });
            await check.SaveChangesAsync(Ct); await tx.RollbackAsync(Ct);
        }
        await using var final = Db(); Assert.Equal(2, (await final.HumanReviewHeads.SingleAsync(Ct)).Revision); Assert.Equal(2, await final.HumanReviewEvents.CountAsync(Ct));
        Assert.Empty(await final.HumanReviewReceipts.ToArrayAsync(Ct)); Assert.Equal(audits, await final.AuditEvents.CountAsync(Ct));
    }
}
