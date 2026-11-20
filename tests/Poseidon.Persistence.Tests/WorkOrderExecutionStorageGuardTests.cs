using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S8 WP-B DEV-054 / DEV-057 / DEV-110: immutable history, receipts and offline lineage are enforced in the
// audited SaveChanges path before any database access; the model carries the human-gate CHECKs.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-054")]
[Trait("Requirement", "DEV-057")]
[Trait("Requirement", "DEV-110")]
public sealed class WorkOrderExecutionStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000c054");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Execution_revision_receipt_and_offline_outcome_rows_cannot_be_rewritten_or_deleted()
    {
        using var db = Context();
        var revision = new WorkOrderExecutionRevision { TenantId = Tenant, VesselId = Guid.NewGuid(), WorkOrderId = Guid.NewGuid(), Revision = 4, State = "VERIFIED" };
        db.Attach(revision); revision.ContentJson = "{\"tampered\":true}";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new WorkOrderExecutionReceipt { TenantId = Tenant, VesselId = Guid.NewGuid(), ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString("D"), WorkOrderId = Guid.NewGuid() };
        db.Attach(receipt); db.Remove(receipt);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var outcome = new WorkOrderOfflineOutcome { TenantId = Tenant, ActorId = Guid.NewGuid(), CommandId = Guid.NewGuid(), Result = "MaterialConflict" };
        db.Attach(outcome); outcome.Result = "Committed";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var requisition = new TechnicalRequisitionRevision { TenantId = Tenant, VesselId = Guid.NewGuid(), RequisitionId = Guid.NewGuid(), Revision = 2, State = "APPROVED_FOR_ERP_EXPORT" };
        db.Attach(requisition); requisition.State = "PROPOSED";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Heads_advance_exactly_one_revision_and_keep_ownership()
    {
        using var db = Context();
        var head = new WorkOrderExecution { TenantId = Tenant, VesselId = Guid.NewGuid(), WorkOrderId = Guid.NewGuid(), Revision = 2, State = "IN_PROGRESS" };
        db.Attach(head); head.Revision = 4; head.State = "VERIFIED";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var spare = new CriticalSpare { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 1 };
        db.Attach(spare); spare.Revision = 2; spare.VesselId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var enrollment = new WorkOrderOfflineEnrollment { TenantId = Tenant, Id = Guid.NewGuid(), ActorId = Guid.NewGuid(), VesselId = Guid.NewGuid(), WorkOrderId = Guid.NewGuid(), PublicKey = "k1" };
        db.Attach(enrollment); enrollment.PublicKey = "k2";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_blocks_synthetic_verification_closure_and_approval_and_allows_one_open_proposal_per_spare()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Checks<T>() => string.Join("\n", model.FindEntityType(typeof(T))!.GetCheckConstraints().Select(c => c.Sql));
        Assert.Contains("\"Provenance\" = 'SyntheticUnverified' AND \"State\" IN ('RELEASED','IN_PROGRESS')", Checks<WorkOrderExecutionRevision>());
        Assert.Contains("\"Provenance\" = 'SyntheticUnverified' AND \"State\" = 'PROPOSED'", Checks<TechnicalRequisitionRevision>());
        Assert.Contains(model.FindEntityType(typeof(TechnicalRequisition))!.GetIndexes(),
            i => i.IsUnique && i.GetFilter() == "\"State\" = 'PROPOSED'" && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "VesselId", "SpareId"]));
        Assert.Equal("offline", model.FindEntityType(typeof(WorkOrderOfflineOutcome))!.GetSchema());
    }

    [Fact]
    public async Task Critical_spare_fixture_refuses_a_normal_tenant_context()
    {
        using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CriticalSpareFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
