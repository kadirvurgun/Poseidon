using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Persistence.Tests;

// Pilot program kit: human records are append-only, a link is withdrawn at most once, an issue moves only forward and
// the model carries the CHECKs for evidence-backed measurements, ACCEPTED/EXPAND eligibility and Product Owner duty.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-079")]
public sealed class PilotProgramStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000a079");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    [Trait("Requirement", "DEV-076")]
    public void Programs_measurements_closes_decisions_receipts_and_feedback_cannot_be_rewritten()
    {
        using var db = Context();
        var decision = new PilotProgramDecision { TenantId = Tenant, Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Sequence = 1, Decision = "FIX", Recommendation = "FIX_RECOMMENDED",
            ProgramHash = new string('A', 64), SnapshotJson = "{}", Rationale = "FICTIONAL fix decision", Duty = "ProductOwner", AuthorityHash = new string('B', 64) };
        db.Attach(decision); decision.Decision = "EXPAND";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var close = new PilotWeekClose { TenantId = Tenant, Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Week = "W1", Sequence = 1, Outcome = "NOT_ACCEPTED",
            Recommendation = "NOT_READY", WeekHash = new string('A', 64), SnapshotJson = "{}", Rationale = "FICTIONAL not accepted", Duty = "ProductOwner", AuthorityHash = new string('B', 64) };
        db.Attach(close); db.Remove(close);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var measurement = new PilotMeasurement { TenantId = Tenant, Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), MetricId = "TUG_JOB_PREP_TIME", Mode = "TUG", Phase = "BASELINE",
            Sequence = 1, Value = 1800, Unit = "SECOND", SampleSize = 5, EvidenceReference = "FICTIONAL-STUDY-1", Method = "FICTIONAL time study" };
        db.Attach(measurement); measurement.Value = 900;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var feedback = new PilotFeedback { TenantId = Tenant, Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Week = "W1", Area = "SHIP", Category = "USABILITY", Sentiment = "NEUTRAL",
            Role = "MASTER", Summary = "FICTIONAL feedback", ParticipantPseudonym = "P-01", RetentionPolicyRef = "FICTIONAL-RET" };
        db.Attach(feedback); feedback.Summary = "rewritten";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    [Trait("Requirement", "DEV-077")]
    public void A_link_target_is_immutable_and_it_is_withdrawn_at_most_once()
    {
        using var db = Context();
        PilotChecklistLink Link() => new() { TenantId = Tenant, Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Week = "W2", ItemCode = "W2_WORK_ORDER", RecordType = "WORK_ORDER",
            RecordId = Guid.NewGuid(), ActorId = Guid.NewGuid() };
        var link = Link();
        db.Attach(link); link.RecordId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var withdrawn = Link(); withdrawn.WithdrawnAt = DateTimeOffset.UtcNow; withdrawn.WithdrawnBy = Guid.NewGuid(); withdrawn.WithdrawalReason = "FICTIONAL wrong record";
        db.Attach(withdrawn); withdrawn.WithdrawalReason = "FICTIONAL second withdrawal";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var added = Link(); added.WithdrawnAt = DateTimeOffset.UtcNow;
        db.PilotChecklistLinks.Add(added);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var deleted = Link();
        db.Attach(deleted); db.Remove(deleted);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    [Trait("Requirement", "DEV-076")]
    public void An_issue_moves_only_open_to_triaged_to_resolved_and_its_severity_is_immutable()
    {
        using var db = Context();
        PilotIssue Issue(string state = "OPEN") => new() { TenantId = Tenant, Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Week = "W1", Area = "SHIP", Category = "CREW",
            Severity = "P0", Title = "FICTIONAL crew list missing", State = state, ReportedAt = DateTimeOffset.UtcNow };
        var created = Issue("TRIAGED");
        db.PilotIssues.Add(created);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var severity = Issue();
        db.Attach(severity); severity.Severity = "P2";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var skip = Issue();
        db.Attach(skip); skip.State = "RESOLVED"; skip.ResolvedAt = DateTimeOffset.UtcNow; skip.ResolvedBy = Guid.NewGuid(); skip.Resolution = "FICTIONAL skip";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var back = Issue("RESOLVED");
        db.Attach(back); back.State = "OPEN";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    [Trait("Requirement", "DEV-096")]
    [Trait("Requirement", "DEV-121")]
    public void The_model_carries_the_pilot_program_checks_and_unique_lineage()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        string Checks(Type type) => string.Join("\n", model.FindEntityType(type)!.GetCheckConstraints().Select(c => c.Name + ":" + c.Sql));
        Assert.Contains("\"Outcome\" <> 'ACCEPTED' OR \"Recommendation\" = 'READY_FOR_HUMAN_CLOSE'", Checks(typeof(PilotWeekClose)));
        Assert.Contains("\"Decision\" <> 'EXPAND' OR \"Recommendation\" = 'EXPAND_ELIGIBLE_PENDING_HUMAN_DECISION'", Checks(typeof(PilotProgramDecision)));
        Assert.Contains("\"Duty\" = 'ProductOwner'", Checks(typeof(PilotProgramDecision)));
        Assert.Contains("\"Duty\" = 'ProductOwner'", Checks(typeof(PilotWeekClose)));
        Assert.Contains("char_length(\"EvidenceReference\") >= 1", Checks(typeof(PilotMeasurement)));
        Assert.Contains("CK_PilotIssue_Triage", Checks(typeof(PilotIssue)));
        Assert.Contains("\"Provenance\" IN ('UserDeclaredUnverified', 'SyntheticUnverified')", Checks(typeof(PilotProgram)));
        var link = model.FindEntityType(typeof(PilotChecklistLink))!.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal("\"WithdrawnAt\" IS NULL", link.GetFilter());
        Assert.Contains(model.FindEntityType(typeof(PilotMeasurement))!.GetIndexes().Single(i => i.IsUnique).Properties, p => p.Name == "Sequence");
        foreach (var t in new[] { typeof(PilotProgram), typeof(PilotChecklistLink), typeof(PilotFeedback), typeof(PilotIssue), typeof(PilotMeasurement), typeof(PilotWeekClose),
                     typeof(PilotProgramDecision), typeof(PilotProgramReceipt) })
            Assert.Equal("pilot", model.FindEntityType(t)!.GetSchema());
    }

    // Final-gate QA N4b: the pilot kit migrations' design-time target models equal the snapshot (QaNotes columns
    // included), the model has no pending change, and the duty-separation trigger mirrors the domain rule.
    [Fact]
    [Trait("Requirement", "DEV-076")]
    [Trait("Requirement", "DEV-121")]
    public void Pilot_kit_migration_target_models_match_the_snapshot_and_the_duty_trigger_is_installed()
    {
        using var db = Context();
        Assert.False(db.Database.HasPendingModelChanges());
        var assembly = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>();
        var init = db.GetService<IModelRuntimeInitializer>();
        var differ = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsModelDiffer>();
        var snapshot = init.Initialize(assembly.ModelSnapshot!.Model, designTime: true, validationLogger: null).GetRelationalModel();
        foreach (var id in new[] { "20261124080000_PilotProgramKit", "20261124090000_PilotProgramDutySeparation", "20261124100000_PilotDecisionFeedbackSeparation" }) {
            var migration = assembly.CreateMigration(assembly.Migrations[id], "Npgsql.EntityFrameworkCore.PostgreSQL");
            var target = init.Initialize(migration.TargetModel!, designTime: true, validationLogger: null).GetRelationalModel();
            Assert.False(differ.HasDifferences(target, snapshot), id);
            if (id.EndsWith("DutySeparation", StringComparison.Ordinal)) {
                var sql = string.Join('\n', migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(o => o.Sql));
                Assert.Contains("BEFORE INSERT ON pilot.\"PilotWeekCloses\"", sql);
                Assert.Contains("BEFORE INSERT ON pilot.\"PilotProgramDecisions\"", sql);
                Assert.Contains("NEW.\"Week\" = 'W4'", sql);
            }
            if (id.EndsWith("FeedbackSeparation", StringComparison.Ordinal)) {
                var sql = string.Join('\n', migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(o => o.Sql));
                var down = string.Join('\n', migration.DownOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(o => o.Sql));
                Assert.Contains("CREATE OR REPLACE FUNCTION pilot.guard_pilot_duty()", sql);
                Assert.Contains("f.\"Week\" IN ('W1', 'W2', 'W3', 'W4')", sql);
                Assert.Contains("NEW.\"Week\" = 'W4'", sql);
                Assert.Contains("CREATE OR REPLACE FUNCTION pilot.guard_pilot_duty()", down);
                Assert.DoesNotContain("IN ('W1', 'W2', 'W3', 'W4')", down);
                Assert.DoesNotContain("DROP", sql + down);
            }
        }
        // The trigger hard-codes W4 as the only week whose checklist uses the value readout.
        Assert.Equal(["W4"], PilotProgramCatalog.WeekDefinitions.Where(PilotProgramPolicy.UsesReadout).Select(w => w.Week));
        var tug = DesignModel(db).FindEntityType(typeof(TugDailyLogReview))!;
        Assert.NotNull(tug.FindProperty("SubmittedBy")); Assert.NotNull(tug.FindProperty("AuthorsJson"));
    }
    private static IModel DesignModel(PoseidonDbContext db) => db.GetService<IDesignTimeModel>().Model;
}
