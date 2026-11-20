using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S9 WP-B DEV-060/061: safety event history, triage/review decisions, identity vault, vault accesses, messages,
// action steps and receipts are append-only in the audited SaveChanges path; the model carries the CHECKs that keep
// the reporter binding, Closed/Effective separation and synthetic DRAFT-only rows in the database.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-060")]
[Trait("Requirement", "DEV-061")]
public sealed class SafetyEventStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000d060");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void History_decisions_vault_accesses_messages_steps_and_receipts_cannot_be_rewritten()
    {
        using var db = Context();
        void Rejected(object entity, Action<object> change)
        {
            db.Attach(entity); change(entity);
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            db.ChangeTracker.Clear();
        }
        Rejected(new SafetyEventRevision { TenantId = Tenant, EventId = Guid.NewGuid(), Revision = 2, State = "SUBMITTED", ReasonCode = "SUBMIT" }, e => ((SafetyEventRevision)e).ContentJson = "{\"tampered\":true}");
        Rejected(new SafetyEventTriage { TenantId = Tenant, Id = Guid.NewGuid(), Decision = "TRIAGE", Severity = "LOW", Reason = "FICTIONAL triage" }, e => ((SafetyEventTriage)e).Severity = "HIGH");
        Rejected(new SafetyEventIdentityAccess { TenantId = Tenant, Id = Guid.NewGuid(), Purpose = "WELFARE_SUPPORT", Reason = "FICTIONAL purpose" }, e => db.Remove(e));
        Rejected(new SafetyEventMessage { TenantId = Tenant, Id = Guid.NewGuid(), Body = "x" }, e => ((SafetyEventMessage)e).Body = "y");
        Rejected(new SafetyInvestigationDecision { TenantId = Tenant, Id = Guid.NewGuid(), Decision = "APPROVE", Reason = "FICTIONAL review" }, e => ((SafetyInvestigationDecision)e).Decision = "REJECT");
        Rejected(new SafetyActionStep { TenantId = Tenant, Id = Guid.NewGuid(), Kind = "VERIFY", Outcome = "VERIFIED", Text = "FICTIONAL check" }, e => ((SafetyActionStep)e).Outcome = "NOT_VERIFIED");
        Rejected(new SafetyEventReceipt { TenantId = Tenant, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "SafetyEventClose", Revision = 4 }, e => ((SafetyEventReceipt)e).Revision = 5);
        Rejected(new SafetyEventOfflineOutcome { TenantId = Tenant, ActorId = Guid.NewGuid(), CommandId = Guid.NewGuid(), Revision = 1 }, e => db.Remove(e));
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Event_reporter_and_vault_binding_are_immutable_and_heads_advance_one_revision()
    {
        using var db = Context();
        var named = new SafetyEvent { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), ReportedBy = Guid.NewGuid(), Revision = 2 };
        db.Attach(named); named.ReportedBy = Guid.NewGuid(); named.Revision = 3;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var skipped = new SafetyEvent { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 2 };
        db.Attach(skipped); skipped.State = "CLOSED"; skipped.Revision = 5;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var vault = new SafetyEventIdentity { TenantId = Tenant, Id = Guid.NewGuid(), EventId = Guid.NewGuid(), ReporterId = Guid.NewGuid() };
        db.Attach(vault); vault.ReporterId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var issued = new SafetyEventIdentity { TenantId = Tenant, Id = Guid.NewGuid(), EventId = Guid.NewGuid(), ReporterId = Guid.NewGuid(), FollowUpHandle = "h", FollowUpHash = "x" };
        db.Attach(issued); issued.FollowUpHash = "y";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var action = new SafetyAction { TenantId = Tenant, Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Revision = 1 };
        db.Attach(action); action.OwnerId = Guid.NewGuid(); action.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var investigation = new SafetyInvestigation { TenantId = Tenant, EventId = Guid.NewGuid(), Revision = 1 };
        db.Attach(investigation); db.Remove(investigation);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_carries_reporter_closure_effectiveness_and_synthetic_checks()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Check(Type t, string name) => model.FindEntityType(t)!.GetCheckConstraints().Single(c => c.Name == name).Sql;
        var safetyEvent = Check(typeof(SafetyEvent), "CK_SafetyEvent_State");
        Assert.Contains("((\"ReportingMode\"='NAMED') = (\"ReportedBy\" IS NOT NULL))", safetyEvent);
        Assert.Contains("\"ClosedBy\" IS NOT NULL", safetyEvent);
        Assert.Contains("\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT'", safetyEvent);
        Assert.Contains("\"Effectiveness\"='PENDING' OR \"State\"='VERIFIED'", Check(typeof(SafetyAction), "CK_SafetyAction_State"));
        Assert.Contains("(\"AuthorKind\"='REPORTER') = (\"ActorId\" IS NULL)", Check(typeof(SafetyEventMessage), "CK_SafetyEventMessage_Kind"));
        Assert.Contains("\"ResultRevision\"=\"Revision\"+1", Check(typeof(SafetyEventTriage), "CK_SafetyEventTriage_Decision"));
        Assert.Contains(model.FindEntityType(typeof(SafetyEventIdentity))!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "EventId"]));
        Assert.Contains(model.FindEntityType(typeof(SafetyEvent))!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "EventNo"]));
    }

    [Fact]
    public async Task Fixture_refuses_a_normal_tenant_context()
    {
        using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SafetyEventFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
