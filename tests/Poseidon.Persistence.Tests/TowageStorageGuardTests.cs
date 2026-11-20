using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S8 WP-D DEV-087/088: towage history, Master/Tug Master decisions, acknowledgements and receipts are
// append-only in the audited SaveChanges path; the model carries the human-gate CHECK constraints.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-087")]
[Trait("Requirement", "DEV-088")]
public sealed class TowageStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000d087");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Plan_revisions_decisions_acknowledgements_and_receipts_cannot_be_rewritten()
    {
        using var db = Context();
        var revision = new TowagePlanRevision { TenantId = Tenant, VesselId = Guid.NewGuid(), PlanId = Guid.NewGuid(), Revision = 4, State = "APPROVED", ReasonCode = "APPROVE" };
        db.Attach(revision); revision.ContentJson = "{\"tampered\":true}";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var decision = new TowagePlanDecision { TenantId = Tenant, Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), Revision = 3, ResultRevision = 4, Decision = "APPROVE", Duty = "TugMaster", Reason = "Reviewed plan sections" };
        db.Attach(decision); db.Remove(decision);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var ack = new TowageBriefingAcknowledgement { TenantId = Tenant, Id = Guid.NewGuid(), BriefingId = Guid.NewGuid(), Revision = 3, ActorId = Guid.NewGuid(), Choice = "DISPUTE", Reason = "Stop-work triggers unclear" };
        db.Attach(ack); ack.Choice = "ACK";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new TowageReceipt { TenantId = Tenant, VesselId = Guid.NewGuid(), ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "TowagePlanDecide", Revision = 4 };
        db.Attach(receipt); receipt.Revision = 5;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Plan_and_briefing_heads_change_only_by_one_revision_and_keep_their_job()
    {
        using var db = Context();
        var plan = new TowagePlan { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), JobId = Guid.NewGuid(), Revision = 2 };
        db.Attach(plan); plan.JobId = Guid.NewGuid(); plan.Revision = 3;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var skipped = new TowagePlan { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), JobId = Guid.NewGuid(), Revision = 2 };
        db.Attach(skipped); skipped.State = "APPROVED"; skipped.Revision = 5;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var briefing = new TowageBriefing { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), JobId = Guid.NewGuid(), Revision = 1 };
        db.Attach(briefing); db.Remove(briefing);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_carries_human_gate_checks_and_unique_per_job_and_per_person_indexes()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Check(Type t, string name) => model.FindEntityType(t)!.GetCheckConstraints().Single(c => c.Name == name).Sql;
        Assert.Contains("\"ApprovedRevision\"=\"Revision\"", Check(typeof(TowagePlan), "CK_TowagePlan_State"));
        Assert.Contains("SyntheticUnverified", Check(typeof(TowagePlan), "CK_TowagePlan_State"));
        Assert.Contains("\"ResultRevision\"=\"Revision\"+1", Check(typeof(TowagePlanDecision), "CK_TowagePlanDecision_Kind"));
        Assert.Contains("\"IssuedBy\" IS NOT NULL", Check(typeof(TowageBriefing), "CK_TowageBriefing_State"));
        Assert.Contains(model.FindEntityType(typeof(TowagePlan))!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "VesselId", "JobId"]));
        Assert.Contains(model.FindEntityType(typeof(TowageBriefingAcknowledgement))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "BriefingId", "Revision", "ActorId"]));
    }

    [Fact]
    public async Task Fixture_refuses_a_normal_tenant_context()
    {
        using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => TowageFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
