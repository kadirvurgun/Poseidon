using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S10 WP-B DEV-092: snapshot leases and offline outcomes are insert-only receipts in the audited SaveChanges path;
// the model carries the closed result/operation CHECK and the single-resolution unique index. DEV-119 demo fixture
// refuses a normal tenant context.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-092")]
public sealed class TugOperationsOfflineStorageTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000d092");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Snapshots_and_outcomes_cannot_be_rewritten_or_deleted()
    {
        using var db = Context();
        var s = new TugOperationOfflineSnapshot { TenantId = Tenant, Id = Guid.NewGuid(), ActorId = Guid.NewGuid(), VesselId = Guid.NewGuid(), JobId = Guid.NewGuid(),
            IssuedAt = DateTimeOffset.UnixEpoch, ExpiresAt = DateTimeOffset.UnixEpoch.AddHours(1) };
        db.Attach(s); s.ExpiresAt = s.ExpiresAt.AddDays(30);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var o = new TugOperationOfflineOutcome { TenantId = Tenant, ActorId = Guid.NewGuid(), CommandId = Guid.NewGuid(), SnapshotId = s.Id, Operation = "tug.daily.entry.v1",
            Result = "MaterialConflict", ConflictFields = "daily_version" };
        db.Attach(o); o.Result = "Committed";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        db.Attach(o); db.Remove(o);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Model_closes_results_and_allows_one_resolution_per_conflict()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        var outcome = model.FindEntityType(typeof(TugOperationOfflineOutcome))!;
        var check = outcome.GetCheckConstraints().Single(c => c.Name == "CK_TugOperationOfflineOutcome_State").Sql;
        Assert.Contains("'Committed','MaterialConflict','ServerRetained'", check);
        Assert.Contains("\"Result\"<>'ServerRetained' OR \"ParentCommandId\" IS NOT NULL", check);
        Assert.Contains("'tug.briefing.ack.v1','tug.execution.event.v1','tug.daily.entry.v1'", check);
        Assert.Contains(outcome.GetIndexes(), i => i.IsUnique && i.GetFilter() == "\"ParentCommandId\" IS NOT NULL" &&
            i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "ActorId", "ParentCommandId"]));
        Assert.Equal(["TenantId", "ActorId", "CommandId"], outcome.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal("tug", model.FindEntityType(typeof(TugOperationOfflineSnapshot))!.GetSchema());
    }

    [Fact]
    [Trait("Requirement", "DEV-119")]
    public async Task Demo_fixture_refuses_a_normal_tenant_context()
    {
        using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => TugDemoFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
