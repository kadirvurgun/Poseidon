using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S9 WP-D DEV-089/090/111: the execution ledger, daily log history/reviews, drydock timeline and receipts are
// append-only in the audited SaveChanges path; the model carries the human-gate CHECK constraints.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-089")]
[Trait("Requirement", "DEV-090")]
[Trait("Requirement", "DEV-111")]
public sealed class FieldOperationsStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000d089");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Ledger_history_reviews_and_receipts_cannot_be_rewritten()
    {
        using var db = Context();
        var e = new TowageExecutionEvent { TenantId = Tenant, VesselId = Guid.NewGuid(), ExecutionId = Guid.NewGuid(), Sequence = 2, Kind = "LOAD_OBSERVED", PayloadJson = "{\"value\":\"40\"}" };
        db.Attach(e); e.PayloadJson = "{\"value\":\"20\"}";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var review = new TugDailyLogReview { TenantId = Tenant, Id = Guid.NewGuid(), LogId = Guid.NewGuid(), Revision = 2, ResultRevision = 3, Decision = "VERIFY", Duty = "TugMaster", Reason = "FICTIONAL reviewed" };
        db.Attach(review); db.Remove(review);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var revision = new DrydockPackageRevision { TenantId = Tenant, VesselId = Guid.NewGuid(), PackageId = Guid.NewGuid(), Revision = 4, State = "VERIFIED", ReasonCode = "VERIFY" };
        db.Attach(revision); revision.Note = "tampered";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new FieldOperationReceipt { TenantId = Tenant, VesselId = Guid.NewGuid(), ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "TowageExecutionStart", Revision = 1 };
        db.Attach(receipt); receipt.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Heads_change_one_revision_at_a_time_and_keep_their_context()
    {
        using var db = Context();
        var x = new TowageExecution { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), JobId = Guid.NewGuid(), TugId = Guid.NewGuid(), Revision = 2, Sequence = 2 };
        db.Attach(x); x.TugId = Guid.NewGuid(); x.Revision = 3;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var skipped = new TowageExecution { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 2, Sequence = 2 };
        db.Attach(skipped); skipped.State = "COMPLETED"; skipped.Revision = 5;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var log = new TugDailyLog { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 1 };
        db.Attach(log); log.LogDate = new DateOnly(2026, 1, 2); log.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var package = new DrydockPackage { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 1 };
        db.Attach(package); db.Remove(package);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_carries_separation_and_synthetic_checks_and_unique_indexes()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Check(Type t, string name) => model.FindEntityType(t)!.GetCheckConstraints().Single(c => c.Name == name).Sql;
        Assert.Contains("\"VerifiedBy\"<>\"StartedBy\"", Check(typeof(TowageExecution), "CK_TowageExecution_Verification"));
        Assert.Contains("\"Provenance\"='UserDeclaredUnverified'", Check(typeof(TowageExecution), "CK_TowageExecution_State"));
        Assert.Contains("\"VerifiedRevision\"=\"Revision\"", Check(typeof(TugDailyLog), "CK_TugDailyLog_State"));
        Assert.Contains("\"VerifiedBy\"<>\"CompletedBy\"", Check(typeof(DrydockPackage), "CK_DrydockPackage_State"));
        Assert.Contains("\"ResultRevision\"=\"Revision\"+1", Check(typeof(TugDailyLogReview), "CK_TugDailyLogReview_Kind"));
        // QA S9 N5: the Master review row itself refuses a reviewer who submitted or authored the reviewed revision.
        Assert.Contains("\"ActorId\"<>\"SubmittedBy\"", Check(typeof(TugDailyLogReview), "CK_TugDailyLogReview_Kind"));
        Assert.Contains("NOT (\"AuthorsJson\"::jsonb @> jsonb_build_array(\"ActorId\"::text))", Check(typeof(TugDailyLogReview), "CK_TugDailyLogReview_Kind"));
        Assert.Contains(model.FindEntityType(typeof(TowageExecution))!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "VesselId", "JobId", "TugId"]));
        Assert.Contains(model.FindEntityType(typeof(TugDailyLog))!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "VesselId", "LogDate", "Watch"]));
    }

    [Fact]
    public async Task Fixture_refuses_a_normal_tenant_context()
    {
        using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => FieldOperationsFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
