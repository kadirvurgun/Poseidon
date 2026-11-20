using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-108 / DEV-109: readings and receipts are append-only, a postponement records exactly one
// decision, and the model carries the human-gate CHECK constraints (no seeded approval possible).
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-108")]
[Trait("Requirement", "DEV-109")]
public sealed class PmsStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000c108");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Readings_point_history_and_receipts_cannot_be_rewritten()
    {
        using var db = Context();
        var reading = new MeasurementReading { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), PointId = Guid.NewGuid(), Value = "10.0", Unit = "HOUR" };
        db.Attach(reading); reading.Value = "9.0";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var revision = new MeasurementPointRevision { TenantId = Tenant, VesselId = Guid.NewGuid(), PointId = Guid.NewGuid(), Revision = 1, ContentJson = "{}" };
        db.Attach(revision); db.Remove(revision);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new PmsCommandReceipt { TenantId = Tenant, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "MaintenancePostpone", Revision = 1 };
        db.Attach(receipt); receipt.ResourceId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Point_asset_is_immutable_and_each_edit_appends_exactly_one_revision()
    {
        using var db = Context();
        var point = new MeasurementPoint { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), AssetId = Guid.NewGuid(), Code = "P", Kind = "RPM", Unit = "RPM", Revision = 1 };
        db.Attach(point); point.AssetId = Guid.NewGuid(); point.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var other = new MeasurementPoint { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), AssetId = Guid.NewGuid(), Code = "P", Kind = "RPM", Unit = "RPM", Revision = 1 };
        db.Attach(other); other.Precision = 2; other.Revision = 3;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void A_decided_postponement_is_immutable_and_request_fields_never_change()
    {
        using var db = Context();
        var decided = new MaintenancePostponement { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), PlanRevision = 1,
            State = "APPROVED", RequestedBy = Guid.NewGuid(), DecidedBy = Guid.NewGuid(), Revision = 2 };
        db.Attach(decided); decided.State = "REJECTED"; decided.Revision = 3;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var requested = new MaintenancePostponement { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), PlanRevision = 1,
            State = "REQUESTED", RequestedBy = Guid.NewGuid(), Revision = 1, RequestedDeadline = new(2026, 12, 10), OriginalDue = new(2026, 12, 1) };
        db.Attach(requested); requested.RequestedDeadline = new(2027, 1, 1); requested.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_carries_the_decision_and_approved_policy_checks()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        var table = model.FindEntityType(typeof(MaintenancePostponement))!;
        var checks = table.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);
        Assert.Contains("\"DecidedBy\" <> \"RequestedBy\"", checks["CK_Postponement_Decision"]);
        Assert.Contains("\"ExtensionDays\" <= \"MaxExtensionDays\"", checks["CK_Postponement_ApprovedPolicy"]);
        Assert.Contains("\"RequestedDeadline\" > \"OriginalDue\"", checks["CK_Postponement_Deadline"]);
        var readings = model.FindEntityType(typeof(MeasurementReading))!.GetCheckConstraints().Select(c => c.Name).ToArray();
        Assert.Contains("CK_MeasurementReading_Propagated", readings);
    }
}
