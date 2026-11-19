using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-100/T-OPS2-004 ROB continuity and DEV-101/T-OPS2-005 SOF timeline review
// aid, plus DEV-102/T-OPS2-006 declared-duration consistency.
public sealed class ShipOperationsLedgerTests
{
    private static ShipOpsFact K(string value) => new("Known", value);
    private static LedgerInput Fuel(Guid id, Dictionary<string, ShipOpsFact> declared) {
        Assert.True(ShipOperationsPolicy.TryNormalize("Fuel", declared, out var fields, out _));
        return new(id, fields);
    }
    private static Dictionary<string, ShipOpsFact> Movement(string date, string? before, string? received, string? consumed, string? after,
        string type = "Consumption", string fuel = "VLSFO") {
        var d = new Dictionary<string, ShipOpsFact> { ["record_date"] = K(date), ["record_type"] = K(type), ["fuel_type"] = K(fuel) };
        if (before != null) d["rob_before_mt"] = K(before);
        if (received != null) d["quantity_received_mt"] = K(received);
        if (consumed != null) d["quantity_consumed_mt"] = K(consumed);
        if (after != null) d["rob_after_mt"] = K(after);
        return d;
    }

    [Fact]
    public void Consistent_chain_reports_no_discrepancy_without_claiming_verification() {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var result = ShipOperationsLedger.AssessFuel([
            Fuel(b, Movement("2026-01-02T00:00:00Z", "115.50", "0", "4.0", "111.5")),
            Fuel(a, Movement("2026-01-01T02:00:00+02:00", "120", "0", "4.5", "115.5"))]);
        Assert.Equal([a, b], result.Select(r => r.RecordId));
        Assert.All(result, r => Assert.Equal("NO_DISCREPANCY_DETECTED", r.Balance.State));
        Assert.Equal("UNKNOWN", result[0].Continuity.State); Assert.Equal("NO_PREVIOUS_RECORD_IN_WINDOW", result[0].Continuity.Reason);
        Assert.Equal("UNKNOWN", result[0].ReviewState);
        Assert.Equal("NO_DISCREPANCY_DETECTED", result[1].Continuity.State); Assert.Equal(a, result[1].PreviousRecordId);
        Assert.Equal("NO_DISCREPANCY_DETECTED", result[1].ReviewState); Assert.Empty(result[1].Flags);
        Assert.Contains("DERIVED_REVIEW_AID_NOT_VERIFICATION", ShipOperationsLedger.FuelQualifications);
    }

    [Fact]
    public void Continuity_gap_and_balance_mismatch_are_flagged_review_required_and_never_corrected() {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var first = Fuel(a, Movement("2026-01-01T12:00:00Z", "120.0", "0", "4.5", "115.5"));
        var second = Fuel(b, Movement("2026-01-02T12:00:00Z", "115.0", "0", "4.0", "110.0"));
        var result = ShipOperationsLedger.AssessFuel([first, second]);
        var gap = result[1];
        Assert.Equal("REVIEW_REQUIRED", gap.Continuity.State); Assert.Equal("-0.5", gap.Continuity.DifferenceMt);
        Assert.Equal("REVIEW_REQUIRED", gap.Balance.State); Assert.Equal("1.0", gap.Balance.DifferenceMt);
        Assert.Contains("ROB_CONTINUITY_GAP", gap.Flags); Assert.Contains("ROB_BALANCE_MISMATCH", gap.Flags);
        Assert.Equal("REVIEW_REQUIRED", gap.ReviewState);
        Assert.Equal("115.0", second.Fields["rob_before_mt"].Value); Assert.Equal("110.0", second.Fields["rob_after_mt"].Value);
    }

    [Fact]
    public void Missing_terms_stay_unknown_and_are_never_assumed_zero() {
        var record = Fuel(Guid.NewGuid(), Movement("2026-01-01T12:00:00Z", "120", null, "4.5", "115.5"));
        var balance = ShipOperationsLedger.Balance(record.Fields);
        Assert.Equal("UNKNOWN", balance.State); Assert.Null(balance.DifferenceMt); Assert.Equal("BALANCE_TERM_NOT_DECLARED", balance.Reason);
        var unknown = Movement("2026-01-01T12:00:00Z", "120", "0", "4.5", "115.5"); unknown["quantity_consumed_mt"] = new("Unknown", null);
        Assert.Equal("UNKNOWN", ShipOperationsLedger.Balance(Fuel(Guid.NewGuid(), unknown).Fields).State);
    }

    [Fact]
    public void Chains_follow_declared_fuel_type_and_unplaced_records_are_not_guessed() {
        var vlsfo = Fuel(Guid.NewGuid(), Movement("2026-01-01T00:00:00Z", "10", "0", "1", "9"));
        var mgo = Fuel(Guid.NewGuid(), Movement("2026-01-02T00:00:00Z", "50", "0", "1", "49", fuel: "MGO"));
        var undated = Fuel(Guid.NewGuid(), new() { ["fuel_type"] = K("VLSFO"), ["rob_before_mt"] = K("9") });
        var result = ShipOperationsLedger.AssessFuel([vlsfo, mgo, undated]);
        Assert.Equal(2, result.Length); Assert.DoesNotContain(result, r => r.RecordId == undated.Id);
        Assert.Equal("NO_PREVIOUS_RECORD_IN_WINDOW", result.Single(r => r.RecordId == mgo.Id).Continuity.Reason);
        Assert.False(ShipOperationsLedger.Placeable(undated.Fields));
    }

    [Fact]
    public void Same_instant_order_is_ambiguous_rather_than_arbitrarily_chained() {
        var result = ShipOperationsLedger.AssessFuel([
            Fuel(Guid.NewGuid(), Movement("2026-01-01T00:00:00Z", "10", "0", "1", "9")),
            Fuel(Guid.NewGuid(), Movement("2026-01-01T02:00:00+02:00", "9", "0", "1", "8"))]);
        Assert.Equal("SAME_INSTANT_ORDER_AMBIGUOUS", result[1].Continuity.Reason); Assert.Contains("SAME_INSTANT_ORDER_AMBIGUOUS", result[1].Flags);
        Assert.Equal("REVIEW_REQUIRED", result[1].ReviewState);
    }

    [Fact]
    public void Bdn_delivery_requires_declared_number_flags_duplicates_and_declared_discrepancy() {
        var missing = Fuel(Guid.NewGuid(), Movement("2026-01-01T00:00:00Z", "10", null, "0", "60", "Delivery/BDN"));
        var one = Movement("2026-01-02T00:00:00Z", "60", "20", "0", "80", "Delivery/BDN"); one["bdn_no"] = K("FICTIONAL-BDN-1");
        var two = Movement("2026-01-03T00:00:00Z", "80", "20", "0", "100", "Delivery/BDN"); two["bdn_no"] = K("FICTIONAL-BDN-1"); two["delivery_discrepancy"] = K("DISPUTED");
        var result = ShipOperationsLedger.AssessFuel([missing, Fuel(Guid.NewGuid(), one), Fuel(Guid.NewGuid(), two)]);
        Assert.Contains("BDN_NUMBER_NOT_DECLARED", result[0].Flags); Assert.Contains("DELIVERED_QUANTITY_NOT_DECLARED", result[0].Flags);
        Assert.Contains("DUPLICATE_BDN_NUMBER", result[1].Flags); Assert.Contains("DUPLICATE_BDN_NUMBER", result[2].Flags);
        Assert.Contains("DECLARED_DELIVERY_DISCREPANCY", result[2].Flags);
        Assert.Equal("NO_DISCREPANCY_DETECTED", result[2].Balance.State); Assert.Equal("REVIEW_REQUIRED", result[2].ReviewState);
    }

    [Theory]
    [InlineData("-1")][InlineData("999999999999999999999999999999999999999999999999")]
    public void Negative_or_out_of_range_figures_are_review_or_unknown_never_rejected_or_truncated(string value) {
        var record = Fuel(Guid.NewGuid(), Movement("2026-01-01T00:00:00Z", value, "0", "0", value));
        var result = ShipOperationsLedger.AssessFuel([record]).Single();
        if (value == "-1") { Assert.Contains("NEGATIVE_DECLARED_QUANTITY", result.Flags); Assert.Equal("NO_DISCREPANCY_DETECTED", result.Balance.State); }
        else { Assert.Equal("UNKNOWN", result.Balance.State); Assert.Equal("NUMBER_OUTSIDE_EXACT_RANGE", result.Balance.Reason); }
        Assert.Equal(value, record.Fields["rob_before_mt"].Value);
    }

    private static LedgerInput Port(Guid id, string start, string? end, string activity = "Loading", string? delay = null) {
        var d = new Dictionary<string, ShipOpsFact> { ["event_start"] = K(start), ["activity_type"] = K(activity) };
        if (end != null) d["event_end"] = K(end);
        if (delay != null) d["delay_reason"] = K(delay);
        Assert.True(ShipOperationsPolicy.TryNormalize("Port", d, out var fields, out _)); return new(id, fields);
    }

    [Fact]
    public void Timeline_orders_by_utc_with_exact_tick_durations_and_open_ends_stay_unknown() {
        var later = Port(Guid.NewGuid(), "2026-01-01T10:00:00Z", "2026-01-01T11:30:00.0000001Z");
        var earlier = Port(Guid.NewGuid(), "2026-01-01T10:00:00+02:00", null, "Arrival");
        var result = ShipOperationsLedger.AssessPort([later, earlier]);
        Assert.Equal([earlier.Id, later.Id], result.Select(r => r.RecordId));
        Assert.Equal("UNKNOWN", result[0].DurationState); Assert.Null(result[0].DurationTicks); Assert.Contains("EVENT_END_NOT_DECLARED", result[0].Flags);
        Assert.Equal("KNOWN", result[1].DurationState); Assert.Equal("54000000001", result[1].DurationTicks);
        Assert.Equal("NO_DISCREPANCY_DETECTED", result[1].ReviewState);
        Assert.Contains("SOF_SIGNATURE_NOT_RECORDED", ShipOperationsLedger.PortQualifications);
    }

    [Fact]
    public void Timeline_flags_same_activity_overlap_and_declared_delay_without_rejecting() {
        var a = Port(Guid.NewGuid(), "2026-01-01T10:00:00Z", "2026-01-01T12:00:00Z");
        var b = Port(Guid.NewGuid(), "2026-01-01T11:00:00Z", "2026-01-01T13:00:00Z", delay: "FICTIONAL shore crane wait");
        var c = Port(Guid.NewGuid(), "2026-01-01T11:00:00Z", "2026-01-01T11:30:00Z", "Shifting");
        var undated = new LedgerInput(Guid.NewGuid(), new Dictionary<string, ShipOpsFact> { ["event_start"] = new("Unknown", null) });
        var result = ShipOperationsLedger.AssessPort([a, b, c, undated]);
        Assert.Equal(3, result.Length);
        Assert.Contains("SAME_ACTIVITY_OVERLAP", result.Single(r => r.RecordId == a.Id).Flags);
        Assert.Contains("DELAY_DECLARED", result.Single(r => r.RecordId == b.Id).Flags);
        Assert.Empty(result.Single(r => r.RecordId == c.Id).Flags);
    }

    [Theory]
    [InlineData("90", "NO_DISCREPANCY_DETECTED")][InlineData("90.0", "NO_DISCREPANCY_DETECTED")][InlineData("85", "REVIEW_REQUIRED")]
    public void Downtime_declared_minutes_are_compared_with_recorded_interval_for_human_review(string minutes, string expected) {
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> {
            ["start_at"] = new("Known", "2026-11-09T08:00:00Z"), ["end_at"] = new("Known", "2026-11-09T10:30:00+01:00"),
            ["duration_minutes"] = new("Known", minutes)
        }, out var fields, out _));
        Assert.Equal(expected, DowntimePolicy.DeclaredDurationConsistency(fields));
        Assert.Equal(expected == "REVIEW_REQUIRED", DowntimePolicy.Blockers(fields).Contains("DECLARED_DURATION_DIFFERS_FROM_RECORDED_INTERVAL"));
        Assert.Equal(minutes, fields["duration_minutes"].Value);
        Assert.DoesNotContain("DOWNTIME_PERIOD_OPEN_END_NOT_ASSUMED", DowntimePolicy.Blockers(fields));
    }

    [Fact]
    public void Downtime_open_period_is_never_closed_with_now() {
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> {
            ["start_at"] = new("Known", "2026-11-09T08:00:00Z"), ["duration_minutes"] = new("Known", "60")
        }, out var fields, out _));
        Assert.Equal("UNKNOWN", DowntimePolicy.DeclaredDurationConsistency(fields));
        Assert.Contains("DOWNTIME_PERIOD_OPEN_END_NOT_ASSUMED", DowntimePolicy.Blockers(fields));
        Assert.Equal("UNAVAILABLE", DowntimePolicy.ObserveInterval(fields).State);
    }
}
