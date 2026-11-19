using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-105 / DEV-106: history immutability is enforced in the audited SaveChanges path before
// any database access; the model carries the human-gate CHECK constraints.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-105")]
[Trait("Requirement", "DEV-106")]
public sealed class RiskStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000c105");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Approved_revision_decision_acknowledgement_and_receipt_rows_cannot_be_rewritten()
    {
        using var db = Context();
        var revision = new RiskAssessmentRevision { TenantId = Tenant, VesselId = Guid.NewGuid(), AssessmentId = Guid.NewGuid(), Revision = 3, State = "APPROVED", ReasonCode = "APPROVE" };
        db.Attach(revision); revision.ContentJson = "{\"tampered\":true}";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var decision = new RiskAssessmentDecision { TenantId = Tenant, Id = Guid.NewGuid(), AssessmentId = Guid.NewGuid(), Revision = 2, ResultRevision = 3, Decision = "APPROVE", Reason = "Reviewed against SMS" };
        db.Attach(decision); db.Remove(decision);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var ack = new RiskAssessmentAcknowledgement { TenantId = Tenant, Id = Guid.NewGuid(), AssessmentId = Guid.NewGuid(), Revision = 3, ActorId = Guid.NewGuid(), Choice = "DISPUTE", Reason = "Not briefed on step 2" };
        db.Attach(ack); ack.Choice = "ACK";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Published_template_content_is_immutable_and_only_moves_to_superseded()
    {
        using var db = Context();
        var row = new RiskTemplateRevision { TenantId = Tenant, TemplateId = Guid.NewGuid(), Revision = 1, Status = "PUBLISHED", ContentJson = "{}", PublishedBy = Guid.NewGuid(), EffectiveFrom = DateTimeOffset.UtcNow };
        db.Attach(row); row.ContentJson = "{\"edited\":true}";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var other = new RiskTemplateRevision { TenantId = Tenant, TemplateId = Guid.NewGuid(), Revision = 1, Status = "PUBLISHED", ContentJson = "{}", PublishedBy = Guid.NewGuid(), EffectiveFrom = DateTimeOffset.UtcNow };
        db.Attach(other); other.Status = "DRAFT";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var method = new RiskMethod { TenantId = Tenant, Id = Guid.NewGuid(), Code = "RM", Status = "PUBLISHED", ContentJson = "{}", PublishedBy = Guid.NewGuid() };
        db.Attach(method); method.ContentJson = "{\"cells\":[]}";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Assessment_head_must_append_exactly_one_revision_and_keep_ownership()
    {
        using var db = Context();
        var head = new RiskAssessment { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), AssessmentNo = "RA-1", Revision = 2 };
        db.Attach(head); head.Revision = 4;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var moved = new RiskAssessment { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), AssessmentNo = "RA-2", Revision = 2 };
        db.Attach(moved); moved.Revision = 3; moved.VesselId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_declares_human_gate_constraints_and_synthetic_rows_cannot_be_approved()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Checks<T>() => string.Join("\n", model.FindEntityType(typeof(T))!.GetCheckConstraints().Select(c => c.Sql));
        Assert.Contains("\"ApprovedRevision\"=\"Revision\"", Checks<RiskAssessment>());
        Assert.Contains("SyntheticUnverified", Checks<RiskAssessment>());
        Assert.Contains("\"PublishedBy\" IS NOT NULL", Checks<RiskTemplateRevision>());
        Assert.Contains("\"PublishedBy\"<>\"ActorId\"", Checks<RiskMethod>());
        Assert.Contains("\"ResultRevision\"=\"Revision\"+1", Checks<RiskAssessmentDecision>());
        Assert.Equal("hse", model.FindEntityType(typeof(RiskAssessment))!.GetSchema());
        Assert.Contains(model.FindEntityType(typeof(RiskAssessmentAcknowledgement))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "AssessmentId", "Revision", "ActorId"]));
    }

    [Fact]
    public async Task Fixture_refuses_a_normal_tenant_context()
    {
        using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RiskFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
