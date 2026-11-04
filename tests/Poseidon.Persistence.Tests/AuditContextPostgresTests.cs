using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

[Trait("Category", "Integration")]
public sealed class AuditContextPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private readonly Guid actor = Guid.NewGuid();
    private readonly string role = Guid.NewGuid().ToString("D");
    private SeedData seed = null!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(Ct);
        seed = SeedValidationTests.Demo();
        await using var setup = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed),
            Guid.Empty, ActorContext.SeedImport(), administrativeImport: true);
        await setup.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    private PoseidonDbContext Web(string operation, string correlation, Guid? tenant = null) => new(
        SeedImporter.Options(postgres.GetConnectionString()), tenant ?? seed.Tenants[0].Id,
        new(actor.ToString(), "User", correlation, "directory.read,directory.write", "WebCommand", role,
            new(operation, "PREPARATION_COMMAND")));

    [Fact]
    public async Task Revocations_and_receipts_bind_actual_owned_parents_while_person_aggregates_keep_tenant_person_scope()
    {
        var vessel = seed.Vessels.First(x => x.TenantId == seed.Tenants[0].Id);
        var revokeCorrelation = Guid.NewGuid().ToString("N");
        await using (var db = Web("MaintenanceObservationRevoke", revokeCorrelation))
        {
            // Parent is deliberately not preloaded. Resolution must retain the tenant query filter.
            db.MaintenanceObservationRevocations.Add(new()
            {
                TenantId = db.TenantId, ObservationId = vessel.Id, RecordedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(Ct);
        }
        await using (var verify = Web("ReadAudit", Guid.NewGuid().ToString("N")))
        {
            var audit = await verify.AuditEvents.SingleAsync(x => x.CorrelationId == revokeCorrelation, Ct);
            Assert.Equal(nameof(MaintenanceObservationRevocation), audit.EntityType);
            Assert.Equal(vessel.Id, audit.VesselId); Assert.Equal(vessel.Id, audit.ResourceId);
            Assert.Equal(role, audit.RoleContext); Assert.Equal(actor.ToString(), audit.ActorId);
            Assert.Equal("RecordedControlledCode", audit.ReasonStatus);
            AssertContext(audit, "MaintenanceObservationRevoke", "Vessel");
            using var fields = JsonDocument.Parse(audit.ChangeSummary!);
            Assert.Equal("Revoked", Field(fields, "Lifecycle"));
        }

        var receiptCorrelation = Guid.NewGuid().ToString("N");
        await using (var db = Web("EquipmentSave", receiptCorrelation))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            var asset = await db.Assets.SingleAsync(x => x.Id == vessel.Id, Ct);
            var previous = asset.Version;
            asset.Name = "FICTIONAL withheld equipment change";
            asset.Revision++;
            db.AssetRevisions.Add(asset.Snapshot());
            await db.SaveChangesAsync(Ct);
            Assert.NotEqual(previous, asset.Version);
            db.CommandReceipts.Add(new()
            {
                TenantId = db.TenantId, ActorId = actor, Key = Guid.NewGuid().ToString(), Action = "EquipmentSave",
                ResourceId = asset.Id, Fingerprint = new string('A', 64), RegisterRevision = asset.Revision,
                RegisterVersion = asset.Version, CommittedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(Ct);
            await transaction.CommitAsync(Ct);
            var events = await db.AuditEvents.Where(x => x.CorrelationId == receiptCorrelation).ToArrayAsync(Ct);
            var changed = Assert.Single(events, x => x.EntityType == nameof(Asset));
            Assert.Equal(previous, changed.BeforeVersion);
            var receipt = Assert.Single(events, x => x.EntityType == nameof(CommandReceipt));
            Assert.Equal(vessel.Id, receipt.VesselId); Assert.Equal(asset.Id, receipt.ResourceId);
            Assert.Equal(asset.Version, receipt.AfterVersion); Assert.Equal(asset.Revision, receipt.AfterRevision);
            AssertContext(receipt, "EquipmentSave", "Vessel");
            Assert.All(events, e => Assert.DoesNotContain("FICTIONAL withheld equipment change", e.ChangeSummary));
        }

        var personCorrelation = Guid.NewGuid().ToString("N");
        await using (var db = Web("CrewProfileSave", personCorrelation))
        {
            var profile = await db.CrewPersonalProfiles.SingleAsync(x => x.PersonId == vessel.Id, Ct);
            profile.Revision++;
            db.CrewPersonalProfileRevisions.Add(new()
            {
                TenantId = db.TenantId, PersonId = profile.PersonId, Revision = profile.Revision,
                PolicyVersion = "synthetic-empty-training", Provenance = "SyntheticUnverified", SavedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(Ct);
            db.CommandReceipts.Add(new()
            {
                TenantId = db.TenantId, ActorId = actor, Key = Guid.NewGuid().ToString(), Action = "CrewProfileSave",
                ResourceId = profile.PersonId, Fingerprint = new string('A', 64), RegisterRevision = profile.Revision,
                RegisterVersion = profile.Version, CommittedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(Ct);
            var events = await db.AuditEvents.Where(x => x.CorrelationId == personCorrelation).ToArrayAsync(Ct);
            Assert.Equal(3, events.Length);
            Assert.All(events, e =>
            {
                Assert.Null(e.VesselId); Assert.Equal(profile.PersonId, e.ResourceId);
                AssertContext(e, "CrewProfileSave", "TenantPerson");
            });
        }
    }

    [Fact]
    public async Task A_late_receipt_database_failure_rolls_back_company_and_all_business_audit_in_the_same_transaction()
    {
        var correlation = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid();
        await using (var db = Web("CreateCompany", correlation))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            db.Companies.Add(new() { TenantId = db.TenantId, Id = id, Code = "AUDIT-ROLLBACK", Name = "FICTIONAL pending company" });
            await db.SaveChangesAsync(Ct);
            Assert.True(await db.AuditEvents.AnyAsync(x => x.CorrelationId == correlation, Ct));
            db.CommandReceipts.Add(new()
            {
                TenantId = db.TenantId, ActorId = actor, Key = Guid.NewGuid().ToString(), Action = "CompanyCreate",
                ResourceId = id, Fingerprint = new string('A', 65), CommittedAt = DateTimeOffset.UtcNow
            });
            // The actual PostgreSQL length constraint fails after the first audited business save.
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            await transaction.RollbackAsync(Ct);
        }
        await using var verify = Web("ReadAudit", Guid.NewGuid().ToString("N"));
        Assert.False(await verify.Companies.AnyAsync(x => x.Id == id, Ct));
        Assert.False(await verify.CommandReceipts.AnyAsync(x => x.ResourceId == id, Ct));
        Assert.False(await verify.AuditEvents.AnyAsync(x => x.CorrelationId == correlation, Ct));
    }

    [Fact]
    public async Task A_foreign_parent_cannot_be_used_to_invent_vessel_metadata_or_publish_a_receipt()
    {
        var foreign = seed.Vessels.First(x => x.TenantId != seed.Tenants[0].Id);
        var correlation = Guid.NewGuid().ToString("N");
        await using (var db = Web("EquipmentSave", correlation))
        {
            db.CommandReceipts.Add(new()
            {
                TenantId = db.TenantId, ActorId = actor, Key = Guid.NewGuid().ToString(), Action = "EquipmentSave",
                ResourceId = foreign.Id, Fingerprint = new string('A', 64), CommittedAt = DateTimeOffset.UtcNow
            });
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
            Assert.Equal("Audit parent ownership is unavailable.", error.Message);
            Assert.DoesNotContain(foreign.Id.ToString(), error.ToString());
            Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
        }
        await using var verify = Web("ReadAudit", Guid.NewGuid().ToString("N"));
        Assert.False(await verify.CommandReceipts.AnyAsync(x => x.ResourceId == foreign.Id, Ct));
        Assert.False(await verify.AuditEvents.AnyAsync(x => x.CorrelationId == correlation, Ct));
    }

    private void AssertContext(AuditEvent audit, string operation, string scope)
    {
        Assert.Equal(seed.Tenants[0].Id, audit.TenantId); Assert.Equal(actor.ToString(), audit.ActorId);
        Assert.Equal(role, audit.RoleContext); Assert.NotEqual(default, audit.OccurredAt);
        Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));
        Assert.Equal("RecordedControlledCode", audit.ReasonStatus);
        using var fields = JsonDocument.Parse(audit.ChangeSummary!);
        Assert.Equal(operation, Field(fields, "CommandName"));
        Assert.Equal("PREPARATION_COMMAND", Field(fields, "TechnicalReasonCode"));
        Assert.Equal(scope, Field(fields, "ScopeKind"));
        Assert.InRange(fields.RootElement.GetArrayLength(), 1, 20);
        Assert.InRange(audit.ChangeSummary!.Length, 1, 2048);
    }

    private static string? Field(JsonDocument summary, string name) => Assert.Single(summary.RootElement.EnumerateArray(),
        x => x.GetProperty("field").GetString() == name).GetProperty("after").GetString();
}
