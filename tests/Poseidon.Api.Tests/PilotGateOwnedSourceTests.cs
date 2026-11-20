using Poseidon.Api;
using Poseidon.Domain;
using Xunit;
using Campaign = Poseidon.Api.RegulatoryReviewPilotGateSource.CampaignState;
using Report = Poseidon.Api.DataReconciliationPilotGateSource.ReportState;

namespace Poseidon.Api.Tests;

// S11 integration of DEV-075 gate readers: REGULATORY <- WP-C SME review campaigns (DEV-073/093) and DATA <- WP-A
// data owner reconciliation sign-off (DEV-070). Fail closed: missing or in-progress human evidence is UNKNOWN, never PASS.
[Trait("Category", "Unit")]
public sealed class PilotGateOwnedSourceTests
{
    private static readonly Guid VesselA = Guid.NewGuid(), VesselB = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    private static Campaign C(Guid vessel, string scope, string state, int minutes = 0) => new(vessel, scope, state, T0.AddMinutes(minutes), null);

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Regulatory_gate_needs_a_closed_ship_and_tug_review_and_is_unknown_without_one()
    {
        var none = RegulatoryReviewPilotGateSource.Evaluate([]);
        Assert.Equal((PilotGatePolicy.Regulatory, PilotGatePolicy.Unknown), (none.Gate, none.State));
        Assert.Equal(new[] { "REGULATORY_REVIEW_MISSING:SHIP", "REGULATORY_REVIEW_MISSING:TUG" }, none.Reasons);

        var shipOnly = RegulatoryReviewPilotGateSource.Evaluate([C(VesselA, "SHIP", RegulatoryReviewPolicy.Closed)]);
        Assert.Equal(PilotGatePolicy.Unknown, shipOnly.State);
        Assert.Contains("REGULATORY_REVIEW_MISSING:TUG", shipOnly.Reasons);

        var both = RegulatoryReviewPilotGateSource.Evaluate([C(VesselA, "SHIP", RegulatoryReviewPolicy.Closed), C(VesselB, "TUG", RegulatoryReviewPolicy.Closed)]);
        Assert.Equal(PilotGatePolicy.Pass, both.State);
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Regulatory_open_items_fail_and_in_progress_reviews_stay_unknown_using_each_vessels_latest_campaign()
    {
        var open = RegulatoryReviewPilotGateSource.Evaluate([C(VesselA, "SHIP", RegulatoryReviewPolicy.ClosedWithOpenItems), C(VesselB, "TUG", RegulatoryReviewPolicy.Closed)]);
        Assert.Equal(PilotGatePolicy.Fail, open.State);
        Assert.Contains("REGULATORY_OPEN_ITEMS:SHIP", open.Reasons);

        var inReview = RegulatoryReviewPilotGateSource.Evaluate([C(VesselA, "SHIP", RegulatoryReviewPolicy.InReview), C(VesselB, "TUG", RegulatoryReviewPolicy.Closed)]);
        Assert.Equal(PilotGatePolicy.Unknown, inReview.State);
        Assert.Contains("REGULATORY_REVIEW_IN_PROGRESS:SHIP", inReview.Reasons);

        // A newer open campaign of the same vessel/scope replaces an older closed one: the older closure no longer counts.
        var reopened = RegulatoryReviewPilotGateSource.Evaluate([C(VesselA, "SHIP", RegulatoryReviewPolicy.Closed, 0), C(VesselA, "SHIP", RegulatoryReviewPolicy.Open, 5),
            C(VesselB, "TUG", RegulatoryReviewPolicy.Closed)]);
        Assert.Equal(PilotGatePolicy.Unknown, reopened.State);

        // Any vessel with open items fails the scope even when another vessel closed cleanly.
        var mixed = RegulatoryReviewPilotGateSource.Evaluate([C(VesselA, "SHIP", RegulatoryReviewPolicy.Closed), C(VesselB, "SHIP", RegulatoryReviewPolicy.ClosedWithOpenItems),
            C(VesselB, "TUG", RegulatoryReviewPolicy.Closed)]);
        Assert.Equal(PilotGatePolicy.Fail, mixed.State);
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Data_gate_is_unknown_without_a_signed_report_and_fails_on_a_rejected_reconciliation()
    {
        Assert.Equal((PilotGatePolicy.Data, PilotGatePolicy.Unknown, "DATA_RECONCILIATION_MISSING"),
            Single(DataReconciliationPilotGateSource.Evaluate([])));
        var pending = DataReconciliationPilotGateSource.Evaluate([new Report(ImportReconciliationPolicy.AwaitingSignOff, null)]);
        Assert.Equal(PilotGatePolicy.Unknown, pending.State);
        Assert.Contains("DATA_SIGN_OFF_PENDING", pending.Reasons);
        // A signed-off state without a resolvable sign-off row is never treated as accepted.
        Assert.Equal(PilotGatePolicy.Unknown, DataReconciliationPilotGateSource.Evaluate([new Report(ImportReconciliationPolicy.SignedOff, null)]).State);

        var rejected = DataReconciliationPilotGateSource.Evaluate([new Report(ImportReconciliationPolicy.SignedOff, ImportReconciliationPolicy.Accepted),
            new Report(ImportReconciliationPolicy.SignedOff, ImportReconciliationPolicy.Rejected)]);
        Assert.Equal(PilotGatePolicy.Fail, rejected.State);
        Assert.Contains("DATA_RECONCILIATION_REJECTED", rejected.Reasons);
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Data_gate_passes_only_when_every_current_report_is_signed_off_and_keeps_exceptions_visible()
    {
        var accepted = DataReconciliationPilotGateSource.Evaluate([new Report(ImportReconciliationPolicy.SignedOff, ImportReconciliationPolicy.Accepted)]);
        Assert.Equal(PilotGatePolicy.Pass, accepted.State);
        var exceptions = DataReconciliationPilotGateSource.Evaluate([new Report(ImportReconciliationPolicy.SignedOff, ImportReconciliationPolicy.Accepted),
            new Report(ImportReconciliationPolicy.SignedOff, ImportReconciliationPolicy.AcceptedWithExceptions)]);
        Assert.Equal(PilotGatePolicy.Pass, exceptions.State);
        Assert.Contains("DATA_ACCEPTED_WITH_EXCEPTIONS", exceptions.Reasons);
        var mixed = DataReconciliationPilotGateSource.Evaluate([new Report(ImportReconciliationPolicy.SignedOff, ImportReconciliationPolicy.Accepted),
            new Report(ImportReconciliationPolicy.AwaitingSignOff, null)]);
        Assert.Equal(PilotGatePolicy.Unknown, mixed.State);
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Every_pilot_gate_has_exactly_one_registered_reader_in_the_bundle()
    {
        IPilotGateSource[] sources = [.. DeferredPilotGateSource.Defaults(), new UatPilotGateSource(UatScenarioCatalog.Ship), new UatPilotGateSource(UatScenarioCatalog.Tug),
            new SupportPilotGateSource(), new RegulatoryReviewPilotGateSource(), new DataReconciliationPilotGateSource()];
        Assert.Equal(PilotGatePolicy.Gates.Order(StringComparer.Ordinal), sources.Select(s => s.Gate).Order(StringComparer.Ordinal));
    }

    private static (string, string, string) Single(PilotGateInput input) => (input.Gate, input.State, Assert.Single(input.Reasons));
}
