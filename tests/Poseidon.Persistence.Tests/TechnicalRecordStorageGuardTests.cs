using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-055 / DEV-103: lifecycle records, report history, verifications and receipts are append-only;
// a closed lifecycle is immutable; the model carries the human-gate CHECK constraints.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-055")]
[Trait("Requirement", "DEV-103")]
public sealed class TechnicalRecordStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000c055");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Lifecycle_records_report_revisions_verifications_and_receipts_cannot_be_rewritten()
    {
        using var db = Context();
        var record = new DefectLifecycleRecord { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), DefectId = Guid.NewGuid(), Sequence = 5, Kind = "VERIFY",
            StageAfter = "VERIFY", DefectRevision = 1, Outcome = "VERIFIED", Text = "FICTIONAL verified at sea" };
        db.Attach(record); record.Outcome = "NOT_VERIFIED";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var revision = new ServiceReportRevision { TenantId = Tenant, VesselId = Guid.NewGuid(), ReportId = Guid.NewGuid(), Revision = 3, State = "VERIFIED", ReasonCode = "VERIFY" };
        db.Attach(revision); revision.ContentJson = "{\"tampered\":true}";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var verification = new ServiceReportVerification { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), ReportId = Guid.NewGuid(), Revision = 2, ResultRevision = 3,
            Decision = "VERIFY", Reason = "FICTIONAL verification record" };
        db.Attach(verification); db.Remove(verification);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new TechnicalRecordReceipt { TenantId = Tenant, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "DefectLifecycleClose", Revision = 7 };
        db.Attach(receipt); receipt.Revision = 8;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Closed_lifecycle_is_immutable_and_open_heads_move_one_revision_at_a_time()
    {
        using var db = Context();
        var closed = new DefectLifecycle { TenantId = Tenant, VesselId = Guid.NewGuid(), DefectId = Guid.NewGuid(), Revision = 7, Stage = "CLOSED", ClosedBy = Guid.NewGuid(), ClosedAt = DateTimeOffset.UtcNow };
        db.Attach(closed); closed.Revision = 8; closed.Stage = "ACTION";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var open = new DefectLifecycle { TenantId = Tenant, VesselId = Guid.NewGuid(), DefectId = Guid.NewGuid(), Revision = 2, Stage = "ASSESS" };
        db.Attach(open); open.Revision = 4; open.Stage = "SAFEGUARD";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var moved = new DefectLifecycle { TenantId = Tenant, VesselId = Guid.NewGuid(), DefectId = Guid.NewGuid(), Revision = 2, Stage = "ASSESS" };
        db.Attach(moved); moved.Revision = 3; moved.VesselId = Guid.NewGuid();
        Assert.ThrowsAny<Exception>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var report = new ServiceReport { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 2, ReportNo = "FICTIONAL-1" };
        db.Attach(report); report.Revision = 3; report.ReportNo = "FICTIONAL-2";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_check_constraints_block_synthetic_human_decisions()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Check(Type type) => string.Join(" ", model.FindEntityType(type)!.GetCheckConstraints().Select(c => c.Sql));
        Assert.Contains("\"Provenance\"<>'SyntheticUnverified' OR \"Kind\" IN ('START','ASSESS')", Check(typeof(DefectLifecycleRecord)));
        Assert.Contains("\"Provenance\"<>'SyntheticUnverified' OR \"Stage\" IN ('DETECT','ASSESS')", Check(typeof(DefectLifecycle)));
        Assert.Contains("\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT'", Check(typeof(ServiceReport)));
        Assert.Contains("\"Kind\"='CLOSE' AND \"StageAfter\"='CLOSED' AND \"Outcome\"='RETURNED_TO_SERVICE' AND length(\"Text\")>=10", Check(typeof(DefectLifecycleRecord)));
        Assert.Contains("length(\"Reason\")>=10", Check(typeof(ServiceReportVerification)));
    }

    [Fact]
    public async Task Fixture_refuses_a_normal_tenant_context()
    {
        using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => TechnicalRecordFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
