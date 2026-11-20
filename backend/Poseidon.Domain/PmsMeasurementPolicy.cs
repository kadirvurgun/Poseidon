using System.Collections.Frozen;
using System.Globalization;

namespace Poseidon.Domain;

// DEV-108 / T-PMS2-002 (18.01 row 109; 06.01 FR-V463-PMS-002 paragraph:280; 59.04 Rev2 §9
// paragraphs 104-108, 1275-1279; running-hours-meter fields paragraphs 878-925).
// A measurement point is a declared definition (kind, unit, range, source); readings are an
// append-only declared history. Nothing here validates an instrument, approves a threshold or
// declares equipment fit for use. Thresholds are tenant-configured expert values: when none
// is configured the point is UNCONFIGURED and never silently NORMAL.
public sealed record MeasurementPointDefinition(string Code, string Kind, string Unit, string Source, int Precision,
    string? RangeMin, string? RangeMax, Guid? ParentPointId);

// One tenant/vessel/point-code policy entry. Configured by the operator (never seeded/invented).
// FreshnessMinutes is the maximum age of the latest reading for a plan trigger (59.04 p262 key
// pms.measurement.freshness_minutes, range 1-10080). Band boundaries are nullable; null is an
// unbounded side, never zero (59.04 p105).
public sealed record MeasurementPolicyRule(Guid TenantId, Guid VesselId, string PointCode, string Version, string SourceReference,
    int FreshnessMinutes, string? NormalMin = null, string? NormalMax = null, string? WarningMin = null, string? WarningMax = null,
    string? AlarmMin = null, string? AlarmMax = null);

public sealed record MeasurementCounterNode(Guid Id, Guid? ParentId, string Kind, string? LastValue, DateTimeOffset? LastMeasuredAt);
public sealed record MeasurementPropagation(Guid PointId, string Value, string Delta);
public sealed record MeasurementPropagationSkip(Guid PointId, string Reason);
public sealed record MeasurementLatest(Guid ReadingId, Guid PointId, string Value, DateTimeOffset MeasuredAt);
public sealed record MeasurementPointRef(Guid TenantId, Guid VesselId, Guid AssetId, Guid Id, string Code, string Kind, string Unit);

public static class PmsMeasurementPolicy
{
    public const string RunningHours = "RUNNING_HOURS", Rpm = "RPM", Pressure = "PRESSURE", Temperature = "TEMPERATURE", Vibration = "VIBRATION";
    public const string Manual = "MANUAL", Device = "DEVICE", Import = "IMPORT", Propagated = "PROPAGATED";
    public const string Normal = "NORMAL", Caution = "CAUTION", Warning = "WARNING", Alarm = "ALARM", Unknown = "UNKNOWN", Unconfigured = "UNCONFIGURED";
    public const string Ready = "ConfiguredDeclaredObservation";
    public const string SourceVersion = "59.04-Rev2:fa795bfb06694d9265dccd28c797be0f6f8cb7ecbbe036d07e070f00c9727553:p104-108,p878-987";

    // Closed SI/registered unit codes per kind. A unit catalog is a measurement convention, not an
    // expert maintenance parameter; conversions between units are not performed (59.04 p106).
    public static FrozenDictionary<string, string[]> Units { get; } = new Dictionary<string, string[]>
    {
        [RunningHours] = ["HOUR"], [Rpm] = ["RPM"], [Pressure] = ["BAR", "KPA", "MPA", "PSI"],
        [Temperature] = ["CEL", "KEL"], [Vibration] = ["MM_S"],
    }.ToFrozenDictionary(StringComparer.Ordinal);
    public static readonly string[] Kinds = [RunningHours, Rpm, Pressure, Temperature, Vibration];
    public static readonly string[] PointSources = [Manual, Device];
    // A caller may declare MANUAL/DEVICE/IMPORT; PROPAGATED is produced by the server only.
    public static readonly string[] CallerReadingSources = [Manual, Device, Import];
    public static readonly string[] ReadingSources = [Manual, Device, Import, Propagated];
    public static readonly string[] Bands = [Normal, Caution, Warning, Alarm, Unknown, Unconfigured, "COUNTER"];
    public static readonly string[] Triggers = [Warning, Alarm];

    public static bool Counter(string? kind) => kind == RunningHours;

    // Exact signed base-10 lexical value: optional '-', no exponent/plus/comma/leading zeros,
    // scale <= maxScale (59.04 p2795 RawLexical). Never parsed through double.
    public static bool Decimal(string? input, int maxScale, out decimal value)
    {
        value = 0;
        if (input is not { Length: > 0 and <= 60 } || maxScale is < 0 or > 18) return false;
        var body = input[0] == '-' ? input[1..] : input;
        if (body.Length == 0 || body.Count(c => c == '.') > 1 || body.Any(c => c != '.' && c is < '0' or > '9') ||
            body[0] == '.' || body[^1] == '.' || body.Length > 1 && body[0] == '0' && body[1] != '.') return false;
        var dot = body.IndexOf('.');
        if (dot >= 0 && body.Length - dot - 1 > maxScale) return false;
        if (input[0] == '-' && body.Trim('0', '.').Length == 0) return false; // negative zero
        if (body.Replace(".", "").Length > 28) return false;
        return decimal.TryParse(input, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    public static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool Code(string? value) => value is { Length: > 0 and <= 64 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    // Returns null when valid, else a closed failure code.
    public static string? ValidateDefinition(MeasurementPointDefinition? d, Guid pointId)
    {
        if (d == null || !Code(d.Code)) return "measurement_definition_invalid";
        if (!Units.TryGetValue(d.Kind ?? "", out var units) || !units.Contains(d.Unit, StringComparer.Ordinal)) return "measurement_definition_invalid";
        if (!PointSources.Contains(d.Source, StringComparer.Ordinal) || d.Precision is < 0 or > 6) return "measurement_definition_invalid";
        decimal? min = null, max = null;
        if (d.RangeMin != null) { if (!Decimal(d.RangeMin, d.Precision, out var v)) return "measurement_definition_invalid"; min = v; }
        if (d.RangeMax != null) { if (!Decimal(d.RangeMax, d.Precision, out var v)) return "measurement_definition_invalid"; max = v; }
        if (min != null && max != null && min >= max) return "measurement_definition_invalid";
        if (Counter(d.Kind) && min is < 0) return "measurement_definition_invalid";
        // Only a running-hours counter can be a sub counter of another running-hours counter.
        if (d.ParentPointId != null && (d.ParentPointId == Guid.Empty || d.ParentPointId == pointId || !Counter(d.Kind))) return "measurement_definition_invalid";
        return null;
    }

    // Reading admission against the definition and the latest prior reading of the same point.
    public static string? ValidateReading(MeasurementPointDefinition point, string? value, DateTimeOffset measuredAt, DateTimeOffset serverNow,
        MeasurementLatest? previous, out decimal parsed)
    {
        parsed = 0;
        if (!Decimal(value, point.Precision, out parsed)) return "reading_precision_unsupported";
        if (measuredAt > serverNow) return "invalid_request";
        if (point.RangeMin != null && Decimal(point.RangeMin, 18, out var min) && parsed < min ||
            point.RangeMax != null && Decimal(point.RangeMax, 18, out var max) && parsed > max) return "reading_out_of_range";
        if (Counter(point.Kind))
        {
            if (parsed < 0) return "reading_out_of_range";
            if (previous != null)
            {
                // A counter history is chronological; a decrease is a reset/replacement that
                // needs its own reviewed correction (59.04 p50), never a silent new baseline.
                if (measuredAt <= previous.MeasuredAt) return "reading_out_of_order";
                if (Decimal(previous.Value, 18, out var prior) && parsed < prior) return "counter_decreasing";
            }
        }
        return null;
    }

    // Parent -> sub counter propagation: a running-hours delta on the parent is added to each
    // descendant counter that already has its own baseline. A descendant without a baseline is
    // skipped (UNKNOWN baseline is never invented as zero). The caller supplies the complete
    // vessel counter forest after authorization, inside one serializable transaction.
    public static (MeasurementPropagation[] Derived, MeasurementPropagationSkip[] Skipped) Propagate(Guid source, decimal delta,
        DateTimeOffset measuredAt, IReadOnlyCollection<MeasurementCounterNode> nodes)
    {
        var derived = new List<MeasurementPropagation>(); var skipped = new List<MeasurementPropagationSkip>();
        if (delta <= 0) return ([], []);
        var children = nodes.Where(n => n.ParentId != null && Counter(n.Kind)).GroupBy(n => n.ParentId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(n => n.Id).ToArray());
        var queue = new Queue<Guid>(); queue.Enqueue(source); var seen = new HashSet<Guid> { source };
        while (queue.Count > 0)
        {
            foreach (var child in children.GetValueOrDefault(queue.Dequeue()) ?? [])
            {
                if (!seen.Add(child.Id)) continue;
                queue.Enqueue(child.Id);
                if (child.LastValue == null || !Decimal(child.LastValue, 18, out var last)) { skipped.Add(new(child.Id, "SubCounterBaselineMissing")); continue; }
                if (child.LastMeasuredAt >= measuredAt) { skipped.Add(new(child.Id, "SubCounterReadingNewer")); continue; }
                try { derived.Add(new(child.Id, Format(checked(last + delta)), Format(delta))); }
                catch (OverflowException) { skipped.Add(new(child.Id, "SubCounterRangeExceeded")); }
            }
        }
        return ([.. derived], [.. skipped]);
    }

    // Exact validation of a tenant-configured policy entry (59.04 p106 boundary order).
    public static bool ValidPolicy(MeasurementPolicyRule? rule, string kind, int precision)
    {
        if (rule == null || rule.FreshnessMinutes is < 1 or > 10080 || rule.Version is not { Length: > 0 and <= 80 } ||
            rule.SourceReference is not { Length: > 0 and <= 200 } || !Code(rule.PointCode)) return false;
        var bounds = new[] { rule.AlarmMin, rule.WarningMin, rule.NormalMin, rule.NormalMax, rule.WarningMax, rule.AlarmMax };
        if (Counter(kind)) return bounds.All(b => b == null);
        var values = new List<decimal>();
        foreach (var b in bounds)
        {
            if (b == null) continue;
            if (!Decimal(b, 18, out var v)) return false;
            values.Add(v);
        }
        // A band threshold needs at least one warning or alarm side; NORMAL-only is no trigger.
        if (rule.WarningMin == null && rule.WarningMax == null && rule.AlarmMin == null && rule.AlarmMax == null) return false;
        for (var i = 1; i < values.Count; i++) if (values[i - 1] > values[i]) return false;
        return precision >= 0;
    }

    // 59.04 p106: inclusive boundaries, heavier result wins on overlap.
    public static string Band(decimal value, MeasurementPolicyRule rule)
    {
        static decimal? D(string? s) => s != null && Decimal(s, 18, out var v) ? v : null;
        if (D(rule.AlarmMin) is { } amin && value <= amin || D(rule.AlarmMax) is { } amax && value >= amax) return Alarm;
        if (D(rule.WarningMin) is { } wmin && value <= wmin || D(rule.WarningMax) is { } wmax && value >= wmax) return Warning;
        var nmin = D(rule.NormalMin); var nmax = D(rule.NormalMax);
        return (nmin == null || value >= nmin) && (nmax == null || value <= nmax) ? Normal : Caution;
    }

    // Current band of a point for display. Missing policy is UNCONFIGURED, missing reading UNKNOWN.
    public static string CurrentBand(MeasurementPointRef point, MeasurementLatest? latest, MeasurementPolicyRule? rule, int precision)
    {
        // A running-hours counter has no band; it drives meter-interval plans only.
        if (Counter(point.Kind)) return latest == null ? Unknown : "COUNTER";
        if (!ValidPolicy(rule, point.Kind, precision)) return Unconfigured;
        if (latest == null || !Decimal(latest.Value, 18, out var value)) return Unknown;
        return Band(value, rule!);
    }

    private static int Rank(string band) => band switch { Alarm => 3, Warning => 2, Caution => 1, Normal => 0, _ => -1 };

    // DEV-053/DEV-108 plan trigger. The result is a MaintenanceObservationAssessment so the
    // existing deterministic calculation handles boundaries; any unresolved input is a
    // REVIEW_REQUIRED state with an explicit reason, never NotDue.
    public static MaintenanceObservationAssessment AssessTrigger(MaintenanceInputs input, MeasurementPointRef? point, int precision,
        MeasurementLatest? latest, MeasurementPolicyRule? rule, Guid tenant, Guid vessel, Guid asset, DateTimeOffset serverNow)
    {
        MaintenanceObservationAssessment Fail(string state) => new(state, rule?.Version ?? "Unavailable", input);
        if (point == null) return Fail("MeasurementPointUnavailable");
        if (point.TenantId != tenant || point.VesselId != vessel || point.AssetId != asset) return Fail("MeasurementPointScopeMismatch");
        if (input.TriggerType == "Meter" && !Counter(point.Kind) || input.TriggerType == "Condition" && Counter(point.Kind) ||
            input.TriggerType is not ("Meter" or "Condition")) return Fail("MeasurementKindMismatch");
        if (input.TriggerType == "Meter" && input.IntervalUnit != point.Unit) return Fail("MeasurementUnitMismatch");
        if (input.TriggerType == "Condition" && !Triggers.Contains(input.TriggerOn, StringComparer.Ordinal)) return Fail("ThresholdTriggerUnspecified");
        if (rule == null || !ValidPolicy(rule, point.Kind, precision) || rule.PointCode != point.Code || rule.TenantId != tenant || rule.VesselId != vessel)
            return Fail("MeasurementPolicyUnconfigured");
        if (latest == null || latest.PointId != point.Id || !Decimal(latest.Value, 18, out var value)) return Fail("MeasurementMissing");
        if (latest.MeasuredAt > serverNow) return Fail("MeasurementFuture");
        if (serverNow - latest.MeasuredAt > TimeSpan.FromMinutes(rule.FreshnessMinutes)) return Fail("MeasurementStale");
        var lineage = $"{latest.ReadingId:D}";
        if (input.TriggerType == "Meter")
            return new(Ready, rule.Version, input with { CurrentMeter = Format(value), Condition = null, ObservedAt = latest.MeasuredAt,
                ObservationRevision = lineage, MeterSource = "measurement-point:" + point.Code, Provenance = "ConfiguredSourceDeclaredUnverified" });
        var band = Band(value, rule);
        return new(Ready, rule.Version + "/" + band, input with { CurrentMeter = null, Condition = Rank(band) >= Rank(input.TriggerOn!) ? "True" : "False",
            ObservedAt = latest.MeasuredAt, ObservationRevision = lineage, MeterSource = "measurement-point:" + point.Code, Provenance = "ConfiguredSourceDeclaredUnverified" });
    }
}
