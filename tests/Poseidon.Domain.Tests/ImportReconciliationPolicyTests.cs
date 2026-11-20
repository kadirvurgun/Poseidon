using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S11 WP-A DEV-070 / T-S11-002 (18.01 Backlog row 71; 18.03 paragraphs 49-66, 82, 84-102). Synthetic values only;
// the critical field set is a reconciliation listing rule, never an approved regulatory catalog.
public sealed class ImportReconciliationPolicyTests
{
    private static readonly Guid Author = Guid.NewGuid(), Uploader = Guid.NewGuid(), Owner = Guid.NewGuid();
    private static ReconciliationBatchFacts Batch(int source, int accepted, int rejected, int duplicate, string state = "Committed") =>
        new(Guid.NewGuid(), "equipment", 3, state, "HASH", "FP", Guid.NewGuid(), source, accepted, rejected, duplicate, Uploader);
    private static readonly ReconciliationCounts Clean = new(2, 2, 0, 0, 0, 0);
    private static readonly Guid[] Sample = [Guid.NewGuid(), Guid.NewGuid()];

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void Every_source_row_is_explained_once_as_accepted_rejected_or_not_committed() {
        var counts = ImportReconciliationPolicy.Count([Batch(5, 5, 0, 0), Batch(4, 0, 3, 2, "Previewed"), Batch(2, 0, 0, 0, "Uploaded")], 7);
        Assert.Equal(new ReconciliationCounts(11, 5, 3, 2, 3, 7), counts);
        Assert.Equal(counts.Source, counts.Accepted + counts.Rejected + counts.NotCommitted);
        Assert.Throws<ArgumentException>(() => ImportReconciliationPolicy.Count([Batch(2, 2, 1, 0)], 0));
        Assert.Throws<ArgumentException>(() => ImportReconciliationPolicy.Count([Batch(3, 0, 1, 2, "Previewed")], 0));
    }

    [Theory]
    [Trait("Requirement", "DEV-070")]
    [InlineData(null, "MISSING")]
    [InlineData("", "MISSING")]
    [InlineData("   ", "MISSING")]
    [InlineData("UNKNOWN", "UNKNOWN")]
    [InlineData(" unknown ", "UNKNOWN")]
    public void Blank_is_missing_and_an_explicit_unknown_is_never_known(string? criticality, string reason) {
        var target = new ReconciliationTarget(Guid.NewGuid(), 2, "Asset", Guid.NewGuid());
        var items = ImportReconciliationPolicy.Missings(target, "equipment", new Dictionary<string, string?> {
            ["code"] = "FICTIONAL-1", ["name"] = "FICTIONAL pump", ["level"] = "Equipment", ["criticality"] = criticality });
        var item = Assert.Single(items);
        Assert.Equal(("criticality", reason, target.TargetId, 2), (item.Field, item.Reason, item.TargetId, item.RowNumber));
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void Each_category_has_an_explicit_critical_field_set_and_an_unknown_category_fails_closed() {
        Assert.Equal(["code", "name"], ImportReconciliationPolicy.CriticalFields("vessel").Select(f => f.Key));
        Assert.Equal(["position_ref", "from"], ImportReconciliationPolicy.CriticalFields("crew").Select(f => f.Key));
        Assert.Contains("expiry_date", ImportReconciliationPolicy.CriticalFields("certificate").Select(f => f.Key));
        Assert.Contains("document_ref", ImportReconciliationPolicy.CriticalFields("certificate").Select(f => f.Key));
        Assert.All(ImportReconciliationPolicy.Categories, c => Assert.All(ImportReconciliationPolicy.CriticalFields(c), f => Assert.True(f.Required)));
        Assert.Throws<ArgumentException>(() => ImportReconciliationPolicy.Assess("drydock", new Dictionary<string, string?>()));
        Assert.Equal(5, ImportReconciliationPolicy.Assess("certificate", new Dictionary<string, string?>()).Count);
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void Sample_is_deterministic_for_a_recorded_seed_bounded_and_distinct() {
        var batch = Guid.NewGuid();
        var targets = Enumerable.Range(2, 40).Select(i => new ReconciliationTarget(batch, i, "Asset", Guid.NewGuid())).ToArray();
        var seed = ImportReconciliationPolicy.Seed(Guid.NewGuid());
        var a = ImportReconciliationPolicy.Sample(seed, targets, 5);
        Assert.Equal(a, ImportReconciliationPolicy.Sample(seed, targets.Reverse(), 5));
        Assert.Equal(5, a.Select(t => t.TargetId).Distinct().Count());
        Assert.NotEqual(a, ImportReconciliationPolicy.Sample(ImportReconciliationPolicy.Seed(Guid.NewGuid()), targets, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImportReconciliationPolicy.Sample(seed, targets, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImportReconciliationPolicy.Sample(seed, targets, ImportReconciliationPolicy.MaxSample + 1));
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void Hash_pins_the_snapshot() {
        var vessel = Guid.NewGuid(); var report = Guid.NewGuid(); var batch = Batch(2, 2, 0, 0);
        var h = ImportReconciliationPolicy.Hash(vessel, report, [batch], Clean, [], [], "seed");
        Assert.Equal(64, h.Length);
        Assert.Equal(h, ImportReconciliationPolicy.Hash(vessel, report, [batch], Clean, [], [], "seed"));
        Assert.NotEqual(h, ImportReconciliationPolicy.Hash(vessel, report, [batch with { Revision = 4 }], Clean, [], [], "seed"));
        Assert.NotEqual(h, ImportReconciliationPolicy.Hash(vessel, report, [batch], Clean with { MissingCritical = 1 }, [], [], "seed"));
    }

    private static string? Sign(string decision, ReconciliationCounts? counts = null, ReconciliationException[]? exceptions = null, Guid[]? verified = null,
        Guid? signer = null, string state = ImportReconciliationPolicy.AwaitingSignOff, string reason = "FICTIONAL owner reviewed the sample") =>
        ImportReconciliationPolicy.SignOffFailure(state, decision, reason, exceptions, verified ?? Sample, Sample, counts ?? Clean, signer ?? Owner, Author, [Uploader]);

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void Author_and_uploader_never_sign_and_only_an_awaiting_report_is_signable() {
        Assert.Null(Sign("ACCEPTED"));
        Assert.Equal("separation_of_duties", Sign("ACCEPTED", signer: Author));
        Assert.Equal("separation_of_duties", Sign("ACCEPTED", signer: Uploader));
        Assert.Equal("separation_of_duties", Sign("REJECTED", signer: Guid.Empty));
        Assert.Equal("reconciliation_superseded", Sign("ACCEPTED", state: "SUPERSEDED"));
        Assert.Equal("invalid_state", Sign("ACCEPTED", state: "SIGNED_OFF"));
        Assert.Equal("invalid_decision", Sign("APPROVED"));
        Assert.Equal("invalid_reason", Sign("ACCEPTED", reason: "ok"));
        Assert.Equal("invalid_reason", Sign("ACCEPTED", reason: " FICTIONAL padded reason "));
        Assert.Equal("invalid_sample", Sign("ACCEPTED", verified: [Guid.NewGuid()]));
        Assert.Equal("invalid_sample", Sign("ACCEPTED", verified: [Sample[0], Sample[0]]));
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public void Accepted_needs_no_open_items_and_exceptions_must_disposition_every_open_item() {
        var open = new ReconciliationCounts(4, 2, 1, 1, 1, 2);
        Assert.Equal(["MISSING_CRITICAL", "REJECTED_ROWS", "DUPLICATE_ROWS", "NOT_COMMITTED", "SAMPLE_UNVERIFIED"], ImportReconciliationPolicy.OpenItems(open, 2, 1));
        Assert.Equal("reconciliation_open_items", Sign("ACCEPTED", open));
        Assert.Equal("reconciliation_open_items", Sign("ACCEPTED", verified: [Sample[0]]));
        Assert.Equal("reconciliation_exceptions_required", Sign("ACCEPTED_WITH_EXCEPTIONS"));
        ReconciliationException E(string kind) => new(kind, "FICTIONAL ref", "FICTIONAL owner disposition");
        Assert.Equal("reconciliation_exceptions_required", Sign("ACCEPTED_WITH_EXCEPTIONS", open, [E("MISSING_CRITICAL")]));
        Assert.Null(Sign("ACCEPTED_WITH_EXCEPTIONS", open, [E("MISSING_CRITICAL"), E("REJECTED_ROWS"), E("DUPLICATE_ROWS"), E("NOT_COMMITTED")]));
        Assert.Equal("invalid_exception", Sign("ACCEPTED_WITH_EXCEPTIONS", open, [E("WAIVED")]));
        Assert.Equal("invalid_exception", Sign("ACCEPTED_WITH_EXCEPTIONS", open, [new("OTHER", "", "FICTIONAL disposition")]));
        // A rejection is always recordable with a reason; it never needs the open items closed.
        Assert.Null(Sign("REJECTED", open));
        Assert.Equal("EFFECTIVE", ImportReconciliationPolicy.SignOffValidity("SIGNED_OFF"));
        Assert.Equal("INVALIDATED_SUPERSEDED", ImportReconciliationPolicy.SignOffValidity("SUPERSEDED"));
        Assert.Equal("NONE", ImportReconciliationPolicy.SignOffValidity("AWAITING_SIGN_OFF"));
    }
}
