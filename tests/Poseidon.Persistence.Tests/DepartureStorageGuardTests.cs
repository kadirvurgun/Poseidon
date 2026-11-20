using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-059 / T-S9-002: the Master departure decision lineage and its receipts are append-only, and the model carries
// the CHECKs that a RELEASE exists only against a CURRENT READY assessment by the Master duty.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-059")]
public sealed class DepartureStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000d059");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));
    private static DepartureDecision Decision() => new() { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Sequence = 2, PreviousDecisionId = Guid.NewGuid(),
        Decision = "HOLD", AssessedState = "REVIEW_REQUIRED", AssessmentMode = "CURRENT", AssessmentHash = new string('A', 64), SnapshotJson = "{}",
        Reason = "FICTIONAL emergency source unavailable", Duty = "Master", AuthorityHash = new string('B', 64) };

    [Fact]
    public void Decisions_and_receipts_cannot_be_rewritten_or_deleted()
    {
        using var db = Context();
        var decision = Decision();
        db.Attach(decision); decision.Decision = "RELEASE";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var other = Decision();
        db.Attach(other); db.Remove(other);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new DepartureReceipt { TenantId = Tenant, VesselId = Guid.NewGuid(), ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "DepartureSignoffRecord",
            Fingerprint = new string('C', 64), Sequence = 1 };
        db.Attach(receipt); receipt.Sequence = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Model_check_constraints_bind_release_to_current_ready_and_the_master_duty()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        var checks = string.Join(" ", model.FindEntityType(typeof(DepartureDecision))!.GetCheckConstraints().Select(c => c.Sql));
        Assert.Contains("\"Decision\" <> 'RELEASE' OR \"AssessedState\" = 'READY' AND \"AssessmentMode\" = 'CURRENT'", checks);
        Assert.Contains("\"Duty\" = 'Master'", checks);
        Assert.Contains("(\"Sequence\" = 1) = (\"PreviousDecisionId\" IS NULL)", checks);
        Assert.Contains("char_length(\"Reason\") >= 10", checks);
        Assert.True(model.FindEntityType(typeof(DepartureDecision))!.GetIndexes().Single(i => i.Properties.Any(p => p.Name == "Sequence")).IsUnique);
    }
}
