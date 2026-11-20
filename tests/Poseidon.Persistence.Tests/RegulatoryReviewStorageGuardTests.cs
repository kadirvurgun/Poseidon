using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-073/093: verdicts and receipts are append-only, item pins and campaign scope are immutable, a closed campaign never
// changes again, and the model carries the CHECKs that block synthetic or unqualified confirmations.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-073")]
public sealed class RegulatoryReviewStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000c073");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));
    private static RegulatoryReviewItem Item() => new() { TenantId = Tenant, Id = Guid.NewGuid(), CampaignId = Guid.NewGuid(), VesselId = Guid.NewGuid(), Ordinal = 1,
        Kind = "SOURCE_CANDIDATE", SubjectKey = "SRC-ISM", CandidateSourceCode = "SRC-ISM", CandidateAuthority = "STATUTORY", CandidateUnverified = true, CreatedBy = Guid.NewGuid() };
    private static RegulatoryReviewCampaign Campaign(string state = "OPEN") => new() { TenantId = Tenant, Id = Guid.NewGuid(), VesselId = Guid.NewGuid(), Scope = "SHIP",
        State = state, PinHash = new string('A', 64), PolicyVersion = "regulatory-sme-review-v1", CreatedBy = Guid.NewGuid() };

    [Fact]
    public void Verdicts_and_receipts_cannot_be_rewritten_or_deleted()
    {
        using var db = Context();
        var verdict = new RegulatoryReviewVerdict { TenantId = Tenant, Id = Guid.NewGuid(), CampaignId = Guid.NewGuid(), ItemId = Guid.NewGuid(), VesselId = Guid.NewGuid(), Sequence = 1,
            Verdict = "AMBIGUOUS", OwnerRole = "REGULATORY_LEAD", TrackedIssueId = "REG-1", Note = "FICTIONAL ambiguity note", PinHash = new string('B', 64),
            QualificationRef = "FICTIONAL-QUAL", QualificationState = "UNVERIFIED", AuthorityHash = new string('C', 64) };
        db.Attach(verdict); verdict.Verdict = "CONFIRMED";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new RegulatoryReviewReceipt { TenantId = Tenant, VesselId = Guid.NewGuid(), ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "RegulatoryReviewVerdict",
            Fingerprint = new string('D', 64) };
        db.Attach(receipt); db.Remove(receipt);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Item_pins_and_campaign_scope_are_immutable_and_closed_campaigns_are_final()
    {
        using var db = Context();
        var item = Item(); db.Attach(item); item.SubjectKey = "SRC-SOLAS";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var other = Item(); db.Attach(other); db.Remove(other);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var campaign = Campaign(); db.Attach(campaign); campaign.Scope = "TUG";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var closed = Campaign("CLOSED_WITH_OPEN_ITEMS"); db.Attach(closed); closed.State = "OPEN";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Model_checks_bind_confirmation_to_a_qualified_reviewer_and_block_synthetic_review()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Checks<T>() => string.Join(" ", model.FindEntityType(typeof(T))!.GetCheckConstraints().Select(c => c.Sql));
        var item = Checks<RegulatoryReviewItem>();
        Assert.Contains("\"Verdict\" NOT IN ('CONFIRMED', 'NOT_APPLICABLE_CONFIRMED') OR \"ReviewerId\" IS NOT NULL AND \"QualificationRef\" IS NOT NULL", item);
        Assert.Contains("\"ReviewerId\" <> \"CreatedBy\"", item);
        Assert.Contains("\"Provenance\" <> 'SyntheticUnverified' OR \"Verdict\" = 'REVIEW_REQUIRED' AND \"VerdictCount\" = 0", item);
        var verdict = Checks<RegulatoryReviewVerdict>();
        Assert.Contains("\"QualificationState\" = 'VERIFIED'", verdict);
        Assert.Contains("\"Verdict\" IN ('CONFIRMED', 'NOT_APPLICABLE_CONFIRMED') OR \"TrackedIssueId\" IS NOT NULL", verdict);
        var campaign = Checks<RegulatoryReviewCampaign>();
        Assert.Contains("\"Provenance\" <> 'SyntheticUnverified' OR \"State\" = 'OPEN'", campaign);
        Assert.Contains("\"State\" <> 'CLOSED' OR \"OpenItemsAtClose\" = 0", campaign);
        var active = model.FindEntityType(typeof(RegulatoryReviewCampaign))!.GetIndexes().Single(i => i.GetDatabaseName() == "UX_RegulatoryReviewCampaigns_Active");
        Assert.True(active.IsUnique);
        Assert.Equal("\"State\" = 'OPEN' OR \"State\" = 'IN_REVIEW'", active.GetFilter());
    }

    [Fact]
    public async Task The_fixture_requires_an_administrative_transaction()
    {
        await using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RegulatoryReviewFixtures.AddMissingAsync(db, [], TestContext.Current.CancellationToken));
    }
}
