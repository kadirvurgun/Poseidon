using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S11 WP-A DEV-069/070: provenance, sign-offs and receipts are insert-only in the audited SaveChanges path; a report
// snapshot never changes except its lifecycle columns, one revision at a time, and a superseded report is final.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-069")]
[Trait("Requirement", "DEV-070")]
public sealed class ImportReconciliationStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000d070");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Provenance_sign_offs_and_receipts_are_insert_only()
    {
        using var db = Context();
        var p = new ImportProvenance { TenantId = Tenant, VesselId = Guid.NewGuid(), BatchId = Guid.NewGuid(), PreviewId = Guid.NewGuid(), RowNumber = 2, TargetKind = "Asset", TargetRevision = 1 };
        db.Attach(p); p.TargetRevision = 9;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var s = new ReconciliationSignOff { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), ReportId = Guid.NewGuid(), Decision = "ACCEPTED", Reason = "FICTIONAL reviewed" };
        db.Attach(s); db.Remove(s);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var r = new ImportReconciliationReceipt { TenantId = Tenant, VesselId = Guid.NewGuid(), ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "ReconciliationSignOff", Revision = 1 };
        db.Attach(r); r.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Report_snapshot_is_immutable_and_a_superseded_report_is_final()
    {
        using var db = Context();
        var counts = new ReconciliationReport { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 1, SourceCount = 3, AcceptedCount = 3 };
        db.Attach(counts); counts.AcceptedCount = 2; counts.RejectedCount = 1; counts.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var skipped = new ReconciliationReport { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 1 };
        db.Attach(skipped); skipped.State = "SUPERSEDED"; skipped.SupersededAt = DateTimeOffset.UtcNow; skipped.Revision = 3;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var final = new ReconciliationReport { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 2, State = "SUPERSEDED", SupersededAt = DateTimeOffset.UtcNow };
        db.Attach(final); final.State = "SIGNED_OFF"; final.Revision = 3;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var deleted = new ReconciliationReport { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 1 };
        db.Attach(deleted); db.Remove(deleted);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_carries_separation_count_and_synthetic_checks_and_the_current_report_index()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Check(Type t, string name) => model.FindEntityType(t)!.GetCheckConstraints().Single(c => c.Name == name).Sql;
        Assert.Contains("\"SignedBy\" <> \"ReportAuthor\"", Check(typeof(ReconciliationSignOff), "CK_ReconciliationSignOff_Decision"));
        Assert.Contains("\"SignedBy\" <> \"CreatedBy\"", Check(typeof(ReconciliationReport), "CK_ReconciliationReport_State"));
        Assert.Contains("\"SourceCount\" = \"AcceptedCount\" + \"RejectedCount\" + \"NotCommittedCount\"", Check(typeof(ReconciliationReport), "CK_ReconciliationReport_State"));
        Assert.Contains("\"Provenance\" <> 'SyntheticUnverified' OR \"SignOffId\" IS NULL", Check(typeof(ReconciliationReport), "CK_ReconciliationReport_State"));
        var current = Assert.Single(model.FindEntityType(typeof(ReconciliationReport))!.GetIndexes(), i => i.GetDatabaseName() == "IX_ReconciliationReports_Current");
        Assert.True(current.IsUnique); Assert.Equal("\"SupersededAt\" IS NULL", current.GetFilter());
        Assert.Contains(model.FindEntityType(typeof(ReconciliationSignOff))!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "ReportId"]));
    }

    [Fact]
    public async Task Fixture_refuses_a_normal_tenant_context()
    {
        using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ImportReconciliationFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
