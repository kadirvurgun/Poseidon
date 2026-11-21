using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S11 WP-D DEV-074/094/075: UAT and pilot gate human records are append-only, runs/findings move only forward, and the
// model carries the CHECKs for separation of duties, blocker handling, GO eligibility and synthetic runs.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-075")]
public sealed class PilotGateStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000d075");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));
    private static UatRun Run(string state = "OPEN") => new() { TenantId = Tenant, Id = Guid.NewGuid(), Scope = "SHIP", Role = "MASTER", Environment = "UAT",
        BuildCommit = "f1c2d51", ParticipantPseudonym = "P-01", CatalogVersion = Poseidon.Domain.UatScenarioCatalog.Version, State = state,
        FacilitatorActorId = Guid.NewGuid(), SubmittedAt = state == "OPEN" ? null : DateTimeOffset.UtcNow };
    private static PilotGateDecision Decision() => new() { TenantId = Tenant, Id = Guid.NewGuid(), Sequence = 1, Decision = "NO_GO", Recommendation = "NO_GO_RECOMMENDED",
        ReviewHash = new string('A', 64), SnapshotJson = "{}", ConditionsJson = "[]", Rationale = "FICTIONAL gates deferred", Duty = "ProductOwner", AuthorityHash = new string('B', 64) };

    [Fact]
    public void Decisions_thresholds_results_support_checks_and_receipts_cannot_be_rewritten_or_deleted()
    {
        using var db = Context();
        var decision = Decision();
        db.Attach(decision); decision.Decision = "GO";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var result = new UatStepResult { TenantId = Tenant, Id = Guid.NewGuid(), RunId = Guid.NewGuid(), ScenarioId = "S-UAT-01", Attempt = 1, Outcome = "FAIL" };
        db.Attach(result); result.Outcome = "PASS";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var threshold = new UatThreshold { TenantId = Tenant, Id = Guid.NewGuid(), Scope = "SHIP", Sequence = 1, MinScenarioPassPercent = 90, AgreementReference = "SC-1", Rationale = "FICTIONAL agreement" };
        db.Attach(threshold); db.Remove(threshold);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new PilotReceipt { TenantId = Tenant, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "PilotGateDecide", Fingerprint = new string('C', 64) };
        db.Attach(receipt); receipt.Action = "UatRunAttest";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    [Trait("Requirement", "DEV-074")]
    public void A_run_moves_only_forward_and_its_identity_fields_and_findings_content_are_immutable()
    {
        using var db = Context();
        var run = Run();
        db.Attach(run); run.Role = "DPA";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var skipped = Run();
        db.Attach(skipped); skipped.State = "ATTESTED"; skipped.AttestedBy = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var attested = Run("ATTESTED");
        db.Attach(attested); attested.State = "OPEN";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var added = Run("SUBMITTED");
        db.UatRuns.Add(added);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var finding = new UatFinding { TenantId = Tenant, Id = Guid.NewGuid(), RunId = Guid.NewGuid(), Scope = "SHIP", ScenarioId = "S-UAT-04", Severity = "BLOCKER", Title = "FICTIONAL blocker" };
        db.Attach(finding); finding.Severity = "MINOR";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_check_constraints_bind_separation_of_duties_blockers_go_and_synthetic_runs()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Checks<T>() => string.Join(" ", model.FindEntityType(typeof(T))!.GetCheckConstraints().Select(c => c.Sql));
        Assert.Contains("\"AttestedBy\" IS NULL OR \"AttestedBy\" <> \"FacilitatorActorId\"", Checks<UatRun>());
        Assert.Contains("\"Provenance\" <> 'SyntheticUnverified' OR \"State\" = 'OPEN'", Checks<UatRun>());
        Assert.Contains("\"Severity\" <> 'BLOCKER' OR \"State\" <> 'ACCEPTED_RISK'", Checks<UatFinding>());
        Assert.Contains("\"ResolvedBy\" IS NULL OR \"ResolvedBy\" <> \"ReportedBy\"", Checks<UatFinding>());
        Assert.Contains("\"Decision\" <> 'GO' OR \"Recommendation\" = 'GO_ELIGIBLE_PENDING_HUMAN_DECISION'", Checks<PilotGateDecision>());
        Assert.Contains("\"Duty\" = 'ProductOwner'", Checks<PilotGateDecision>());
        Assert.Contains("\"State\" <> 'READY' OR \"EvidenceReference\" IS NOT NULL", Checks<PilotSupportCheck>());
        Assert.True(model.FindEntityType(typeof(PilotGateDecision))!.GetIndexes().Single(i => i.Properties.Any(p => p.Name == "Sequence")).IsUnique);
        Assert.True(model.FindEntityType(typeof(UatRun))!.FindProperty("Version")!.IsConcurrencyToken);
    }

    [Fact]
    [Trait("Requirement", "DEV-074")]
    public async Task Fixture_requires_an_administrative_transaction()
    {
        await using var db = Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UatFixtures.AddMissingAsync(db, [Tenant], TestContext.Current.CancellationToken));
    }
}
