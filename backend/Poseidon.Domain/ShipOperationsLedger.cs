using System.Globalization;

namespace Poseidon.Domain;

// One declared mass check over exact decimal facts. Every quantity is the
// source's explicit "mt" unit; nothing is converted, rounded or tolerated.
public sealed record FuelLedgerCheck(string State, string? DifferenceMt, string? Reason);
public sealed record FuelLedgerAssessment(Guid RecordId, string ReviewState, FuelLedgerCheck Balance, FuelLedgerCheck Continuity,
    Guid? PreviousRecordId, string[] Flags, string[] Basis);
public sealed record PortTimelineAssessment(Guid RecordId, string ReviewState, string DurationState, string? DurationTicks, string[] Flags);
public sealed record LedgerInput(Guid Id, IReadOnlyDictionary<string, ShipOpsFact> Fields);

// DEV-100 / DEV-101 derived read model. A finding is a REVIEW_REQUIRED flag for
// a human; the ledger never edits, corrects, completes or approves a record.
// UNKNOWN stays UNKNOWN: a missing term is never assumed to be zero, a missing
// end never becomes "now", and "no discrepancy detected" is not verification.
public static class ShipOperationsLedger
{
    public const string Unit = "mt";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string Unknown = "UNKNOWN";
    public const string NoDiscrepancyDetected = "NO_DISCREPANCY_DETECTED";
    // tank_id is an unresolved reference (DD row 2198), so continuity can only
    // be followed per declared fuel type, never per physical tank.
    public const string FuelGrouping = "FUEL_TYPE_ONLY_TANK_REFERENCE_UNAVAILABLE";
    public const string TankReferenceUnavailable = "TANK_REFERENCE_UNAVAILABLE";
    // An older record exists outside the assessed window: the boundary link is
    // UNAVAILABLE, never a false "continuous" and never a false gap.
    public const string HistoryTruncated = "HISTORY_TRUNCATED";
    public static readonly string[] FuelQualifications = ["DERIVED_REVIEW_AID_NOT_VERIFICATION", "NO_AUTOMATIC_CORRECTION",
        "MISSING_TERM_NOT_ASSUMED_ZERO", "NO_TOLERANCE_APPROVED", "NO_COMPUTED_EMISSIONS", FuelGrouping, TankReferenceUnavailable];
    public static readonly string[] PortQualifications = ["DERIVED_REVIEW_AID_NOT_VERIFICATION", "SOF_SIGNATURE_NOT_RECORDED",
        "SOF_FINALIZATION_UNAVAILABLE", "OPEN_END_NOT_ASSUMED", "NOT_PORT_OPERATING_SYSTEM"];
    public static string[] Qualifications(string[] basis, bool historyTruncated) => historyTruncated ? [.. basis, HistoryTruncated] : basis;
    // Every continuity finding is a per-fuel-type aggregate: with no tank
    // reference a multi-tank gap is a review question about missing tank data.
    private static readonly string[] ContinuityBasis = ["FUEL_TYPE_AGGREGATE", TankReferenceUnavailable];

    private static string? Known(IReadOnlyDictionary<string, ShipOpsFact> fields, string key) =>
        fields.TryGetValue(key, out var fact) && fact.State == "Known" ? fact.Value : null;
    private static bool Instant(IReadOnlyDictionary<string, ShipOpsFact> fields, string key, out DateTimeOffset value) {
        value = default; return Known(fields, key) is { } text && ShipOperationsPolicy.TryInstant(text, out value);
    }
    // Lexical numbers are at most 48 characters; System.Decimal holds 28-29
    // significant digits. An out-of-range figure is UNKNOWN, never truncated.
    public static bool TryExact(string? text, out decimal value) {
        value = 0;
        return text != null && decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Overall(IEnumerable<string> states, IEnumerable<string> flags) {
        var all = states.ToArray();
        return flags.Any() || all.Contains(ReviewRequired) ? ReviewRequired : all.Contains(Unknown) ? Unknown : NoDiscrepancyDetected;
    }

    // Records without a known record_date or fuel_type cannot be placed in a
    // continuity chain; they are returned as unplaced rather than guessed.
    public static bool Placeable(IReadOnlyDictionary<string, ShipOpsFact> fields) =>
        Instant(fields, "record_date", out _) && Known(fields, "fuel_type") != null;

    public static FuelLedgerAssessment[] AssessFuel(IEnumerable<LedgerInput> records, bool historyTruncated = false) {
        var placed = records.Where(r => Placeable(r.Fields)).Select(r => {
            Instant(r.Fields, "record_date", out var at); return (Record: r, At: at);
        }).OrderBy(r => r.At).ThenBy(r => r.Record.Id).ToArray();
        var bdn = placed.Where(r => Known(r.Record.Fields, "record_type") == "Delivery/BDN" && Known(r.Record.Fields, "bdn_no") != null)
            .GroupBy(r => Known(r.Record.Fields, "bdn_no")!, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var last = new Dictionary<string, (LedgerInput Record, DateTimeOffset At)>(StringComparer.Ordinal);
        var result = new List<FuelLedgerAssessment>();
        foreach (var (record, at) in placed) {
            var f = record.Fields; var flags = new List<string>(); var type = Known(f, "fuel_type")!;
            var balance = Balance(f);
            if (balance.State == ReviewRequired) flags.Add("ROB_BALANCE_MISMATCH");
            FuelLedgerCheck continuity; Guid? previous = null;
            if (!last.TryGetValue(type, out var prior))
                continuity = historyTruncated ? new(Unknown, null, HistoryTruncated) : new(Unknown, null, "NO_PREVIOUS_RECORD_IN_WINDOW");
            else {
                previous = prior.Record.Id;
                if (prior.At == at) { continuity = new(Unknown, null, "SAME_INSTANT_ORDER_AMBIGUOUS"); flags.Add("SAME_INSTANT_ORDER_AMBIGUOUS"); }
                else if (!TryExact(Known(prior.Record.Fields, "rob_after_mt"), out var before) || !TryExact(Known(f, "rob_before_mt"), out var now))
                    continuity = new(Unknown, null, "ROB_TERM_NOT_DECLARED");
                else {
                    var difference = now - before;
                    continuity = difference == 0 ? new(NoDiscrepancyDetected, Format(difference), null) : new(ReviewRequired, Format(difference), "ROB_CONTINUITY_GAP");
                    if (difference != 0) flags.Add("ROB_CONTINUITY_GAP");
                }
            }
            last[type] = (record, at);
            var recordType = Known(f, "record_type");
            if (recordType == "Delivery/BDN") {
                if (Known(f, "bdn_no") == null) flags.Add("BDN_NUMBER_NOT_DECLARED");
                else if (bdn.Contains(Known(f, "bdn_no")!)) flags.Add("DUPLICATE_BDN_NUMBER");
                if (Known(f, "quantity_received_mt") == null) flags.Add("DELIVERED_QUANTITY_NOT_DECLARED");
            }
            if (Known(f, "delivery_discrepancy") is "ATTENTION" or "DISPUTED") flags.Add("DECLARED_DELIVERY_DISCREPANCY");
            foreach (var key in new[] { "quantity_received_mt", "quantity_consumed_mt", "rob_before_mt", "rob_after_mt" })
                if (TryExact(Known(f, key), out var quantity) && quantity < 0) { flags.Add("NEGATIVE_DECLARED_QUANTITY"); break; }
            string[] basis = previous == null && historyTruncated ? [.. ContinuityBasis, HistoryTruncated] : ContinuityBasis;
            result.Add(new(record.Id, Overall([balance.State, continuity.State], flags), balance, continuity, previous, flags.Distinct().ToArray(), basis));
        }
        return result.ToArray();
    }

    // rob_before + received - consumed compared with rob_after, all as
    // declared. A transfer quantity has no source field, so transfer records
    // stay UNKNOWN unless every term was declared explicitly.
    public static FuelLedgerCheck Balance(IReadOnlyDictionary<string, ShipOpsFact> f) {
        string[] keys = ["rob_before_mt", "quantity_received_mt", "quantity_consumed_mt", "rob_after_mt"];
        var values = new decimal[4];
        for (var i = 0; i < keys.Length; i++) {
            var text = Known(f, keys[i]);
            if (text == null) return new(Unknown, null, "BALANCE_TERM_NOT_DECLARED");
            if (!TryExact(text, out values[i])) return new(Unknown, null, "NUMBER_OUTSIDE_EXACT_RANGE");
        }
        try {
            var difference = values[0] + values[1] - values[2] - values[3];
            return difference == 0 ? new(NoDiscrepancyDetected, Format(difference), null) : new(ReviewRequired, Format(difference), "ROB_BALANCE_MISMATCH");
        } catch (OverflowException) { return new(Unknown, null, "NUMBER_OUTSIDE_EXACT_RANGE"); }
    }

    public static bool PlaceableEvent(IReadOnlyDictionary<string, ShipOpsFact> fields) => Instant(fields, "event_start", out _);

    // Structured SOF timeline: declared events in UTC order with the exact
    // recorded interval. No sequence rule, laytime or demurrage is inferred.
    public static PortTimelineAssessment[] AssessPort(IEnumerable<LedgerInput> records) {
        var placed = records.Where(r => PlaceableEvent(r.Fields)).Select(r => {
            Instant(r.Fields, "event_start", out var start);
            DateTimeOffset? end = Instant(r.Fields, "event_end", out var e) ? e : null;
            return (Record: r, Start: start, End: end, Activity: Known(r.Fields, "activity_type"));
        }).OrderBy(r => r.Start).ThenBy(r => r.Record.Id).ToArray();
        var result = new List<PortTimelineAssessment>();
        foreach (var item in placed) {
            var flags = new List<string>(); var f = item.Record.Fields;
            string state; string? ticks = null;
            if (item.End is { } end) { state = "KNOWN"; ticks = (end - item.Start).Ticks.ToString(CultureInfo.InvariantCulture); }
            else { state = Unknown; flags.Add("EVENT_END_NOT_DECLARED"); }
            if (item.Activity == null) flags.Add("ACTIVITY_TYPE_NOT_DECLARED");
            if (Known(f, "delay_reason") != null) flags.Add("DELAY_DECLARED");
            // Two declared intervals of the same activity overlapping is a
            // record-keeping question for the master, not a rejected record.
            if (item.Activity != null && item.End != null && placed.Any(o => o.Record.Id != item.Record.Id && o.Activity == item.Activity &&
                o.End is { } otherEnd && o.Start < item.End && item.Start < otherEnd)) flags.Add("SAME_ACTIVITY_OVERLAP");
            result.Add(new(item.Record.Id, flags.Count == 0 ? NoDiscrepancyDetected : ReviewRequired, state, ticks, flags.ToArray()));
        }
        return result.ToArray();
    }
}
