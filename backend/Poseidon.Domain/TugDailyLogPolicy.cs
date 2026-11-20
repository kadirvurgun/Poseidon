using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// DEV-090 / T-TUG-011 (18.01 Backlog row 91): "Daily log works offline; job links and meter values reconcile;
// Master review + sync state visible." 10.01 p112-114 (jobs/hours/fuel/defects/events/daily summary/Master review),
// 08.02 p135-137, 60.02 Rev2 p258-267 (DailyLog state DRAFT|SUBMITTED|RETURNED|VERIFIED, IANA timezone, summary
// 20..8000, closing<opening without reset/rollover evidence is METER_SEQUENCE_CONFLICT, clock time is not engine
// runtime, fuel unit/source required) and p1325-1370 (DailyLogView: watch 00-08/08-16/16-24/Other, jobs_completed
// = distinct completed executions in the log interval, operating_hours = union of ACTIVE intervals, master_review
// Pending|Reviewed|Attention, sync_state SYNCED|LOCAL_PENDING|CONFLICT|FAILED). Safety events have no current
// source (61.01 owner not available here): they stay UNKNOWN, never zero.
public static class TugDailyLogPolicy
{
    public const string SourceVersion = "18.01:row91;10.01:p112-114;08.02:p135-137;60.02Rev2:p258-267,p1325-1370";
    public const string Draft = "DRAFT", Submitted = "SUBMITTED", Returned = "RETURNED", Verified = "VERIFIED";
    public static readonly string[] States = [Draft, Submitted, Returned, Verified];
    public static readonly string[] Watches = ["00-08", "08-16", "16-24", "OTHER"];
    public static readonly string[] FuelUnits = ["L", "m3", "t"];
    public static readonly string[] Decisions = ["VERIFY", "RETURN"];
    public const string Reconciled = "RECONCILED", ReviewRequired = "REVIEW_REQUIRED", Incomplete = "INCOMPLETE";
    public const int MaxMeters = 20, MaxLinks = 50, MaxDefects = 100;

    private static bool Number(string? v) => v is { Length: >= 1 and <= 14 } && Regex.IsMatch(v, @"\A[0-9]{1,9}(?:\.[0-9]{1,2})?\z");
    private static decimal Value(string v) => decimal.Parse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public static bool ValidDraft(TugDailyLogContent? c) {
        if (c?.Meters == null || c.Fuel == null || c.ExecutionIds == null || c.DefectIds == null) return false;
        if (c.Meters.Length > MaxMeters || c.ExecutionIds.Length > MaxLinks || c.DefectIds.Length > MaxDefects) return false;
        if (c.TimeZone != null && !TowageExecutionPolicy.Short(c.TimeZone, 64)) return false;
        if (c.DailySummary != null && !TowageExecutionPolicy.Long(c.DailySummary, 1, 8000)) return false;
        if (c.Meters.Any(m => m == null || m.Id == Guid.Empty || !TowageExecutionPolicy.Short(m.Meter, 60) || m.Opening != null && !Number(m.Opening) ||
            m.Closing != null && !Number(m.Closing) || m.ResetEvidenceRef != null && !TowageExecutionPolicy.Short(m.ResetEvidenceRef, 400))) return false;
        if (c.Meters.Select(m => m.Id).Distinct().Count() != c.Meters.Length || c.Meters.Select(m => m.Meter.Trim()).Distinct(StringComparer.Ordinal).Count() != c.Meters.Length) return false;
        if (c.Fuel.Quantity != null && !Number(c.Fuel.Quantity) || c.Fuel.Unit != null && !FuelUnits.Contains(c.Fuel.Unit) ||
            c.Fuel.SourceRef != null && !TowageExecutionPolicy.Short(c.Fuel.SourceRef, 400)) return false;
        return c.ExecutionIds.All(x => x != Guid.Empty) && c.ExecutionIds.Distinct().Count() == c.ExecutionIds.Length &&
            c.DefectIds.All(x => x != Guid.Empty) && c.DefectIds.Distinct().Count() == c.DefectIds.Length;
    }

    // 60.02 p267: a closing reading below the opening needs reset/rollover evidence; otherwise the save is refused (409).
    public static bool MeterSequenceConflict(TugDailyLogContent c) =>
        c.Meters.Any(m => m.Opening != null && m.Closing != null && Value(m.Closing) < Value(m.Opening) && m.ResetEvidenceRef == null);

    public static string[] SubmitBlockers(TugDailyLogContent c, bool timeZoneKnown) {
        var b = new List<string>();
        if (c.DailySummary == null) b.Add("daily_summary_missing");
        else if (c.DailySummary.Trim().Length < 20) b.Add("daily_summary_too_short");
        if (c.TimeZone == null) b.Add("timezone_missing");
        else if (!timeZoneKnown) b.Add("timezone_invalid");
        if (c.Meters.Any(m => m.Opening == null || m.Closing == null)) b.Add("meter_reading_missing");
        if (c.Fuel.Quantity != null && c.Fuel.Unit == null) b.Add("fuel_unit_missing");
        if (c.Fuel.Quantity != null && c.Fuel.SourceRef == null) b.Add("fuel_source_missing");
        if (MeterSequenceConflict(c)) b.Add("meter_sequence_conflict");
        return [.. b.Order(StringComparer.Ordinal)];
    }

    // Log interval in the log's own IANA zone: the date for OTHER, otherwise the 8-hour watch.
    public static (DateTimeOffset From, DateTimeOffset To)? Interval(DateOnly date, string watch, TimeZoneInfo? zone) {
        if (zone == null) return null;
        var (start, hours) = watch switch { "00-08" => (0, 8), "08-16" => (8, 8), "16-24" => (16, 8), _ => (0, 24) };
        var local = date.ToDateTime(new TimeOnly(0, 0)).AddHours(start);
        DateTimeOffset At(DateTime t) => new(t, zone.GetUtcOffset(t));
        return (At(local).ToUniversalTime(), At(local.AddHours(hours)).ToUniversalTime());
    }

    // Reconciliation: meter continuity against the previous log of the same tug and meter, and job links against
    // the tug's executions in the log interval. Nothing is filled in; a missing comparison is REVIEW_REQUIRED.
    public static TugDailyReconciliation Reconcile(TugDailyLogContent c, IReadOnlyDictionary<string, string> previousClosings,
        (DateTimeOffset From, DateTimeOffset To)? interval, IReadOnlyCollection<TugDailyExecutionFact> executions, DateTimeOffset now) {
        var reasons = new List<TugDailyReason>(); var incomplete = false;
        if (interval == null) { reasons.Add(new("log", "timezone_unknown", null)); incomplete = true; }
        decimal meterHours = 0; var meterKnown = c.Meters.Length > 0;
        if (c.Meters.Length == 0) reasons.Add(new("meter", "no_meter_readings", null));
        foreach (var m in c.Meters) {
            if (m.Opening == null || m.Closing == null) { reasons.Add(new("meter", "meter_reading_missing", m.Meter)); incomplete = true; meterKnown = false; continue; }
            var open = Value(m.Opening); var close = Value(m.Closing);
            if (close < open) { reasons.Add(new("meter", "meter_reset_declared", m.Meter)); meterKnown = false; }
            else meterHours += close - open;
            if (!previousClosings.TryGetValue(m.Meter.Trim(), out var previous)) reasons.Add(new("meter", "meter_previous_unknown", m.Meter));
            else if (Value(previous) < open) reasons.Add(new("meter", "meter_gap", m.Meter));
            else if (Value(previous) > open && m.ResetEvidenceRef == null) reasons.Add(new("meter", "meter_regression", m.Meter));
        }
        var linked = c.ExecutionIds.ToHashSet();
        var completed = 0; double active = 0; var open_ = false;
        if (interval is { } span) {
            bool Overlaps(TugDailyExecutionFact e) => e.StartedAt < span.To && (e.EndedAt ?? now) >= span.From;
            foreach (var e in executions) {
                var overlaps = Overlaps(e);
                if (overlaps && !linked.Contains(e.Id)) reasons.Add(new("job", "execution_unlinked", e.Id.ToString("D")));
                if (!linked.Contains(e.Id)) continue;
                if (!overlaps) { reasons.Add(new("job", "execution_outside_log_interval", e.Id.ToString("D"))); continue; }
                if (e.State == TowageExecutionPolicy.Completed && e.EndedAt >= span.From && e.EndedAt < span.To) completed++;
                foreach (var i in e.Intervals) {
                    if (i.To == null) open_ = true;
                    var from = i.From > span.From ? i.From : span.From; var end = i.To ?? now; var to = end < span.To ? end : span.To;
                    if (to > from) active += (to - from).TotalSeconds;
                }
            }
        }
        foreach (var id in linked.Where(id => executions.All(e => e.Id != id))) reasons.Add(new("job", "execution_unavailable", id.ToString("D")));
        var state = incomplete ? Incomplete : reasons.Count > 0 ? ReviewRequired : Reconciled;
        return new(state, [.. reasons.OrderBy(r => r.Scope, StringComparer.Ordinal).ThenBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.Subject, StringComparer.Ordinal)],
            completed, interval == null ? null : (active / 3600d).ToString("0.##", CultureInfo.InvariantCulture), open_,
            meterKnown ? meterHours.ToString("0.##", CultureInfo.InvariantCulture) : null);
    }

    // Master review is a human record; the reviewer never authored or submitted the log.
    public static string? ReviewFailure(string state, string decision, Guid reviewer, IReadOnlyCollection<Guid> authors, Guid? submitter, string? reason) {
        if (state != Submitted) return "invalid_state";
        if (!Decisions.Contains(decision) || !TowageExecutionPolicy.Long(reason, 10, 1000)) return "invalid_reason";
        if (reviewer == Guid.Empty || authors.Contains(reviewer) || submitter == reviewer) return "separation_of_duties";
        return null;
    }

    public static string MasterReview(string state, string reconciliation) => state switch {
        Verified => "REVIEWED",
        Returned => "ATTENTION",
        Submitted when reconciliation != Reconciled => "ATTENTION",
        _ => "PENDING"
    };

    public static string Hash(Guid vessel, Guid id, long revision, string state, DateOnly date, string watch, string contentJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', "poseidon.tug-daily-log.v1", vessel.ToString("D"), id.ToString("D"),
            revision.ToString(CultureInfo.InvariantCulture), state, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), watch, contentJson))));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugDailyMeter([property: JsonRequired] Guid Id, [property: JsonRequired] string Meter, string? Opening, string? Closing, string? ResetEvidenceRef)
{ public override string ToString() => nameof(TugDailyMeter); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugDailyFuel(string? Quantity, string? Unit, string? SourceRef)
{ public override string ToString() => nameof(TugDailyFuel); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugDailyLogContent(string? TimeZone, [property: JsonRequired] TugDailyMeter[] Meters, [property: JsonRequired] TugDailyFuel Fuel,
    [property: JsonRequired] Guid[] ExecutionIds, [property: JsonRequired] Guid[] DefectIds, string? DailySummary)
{ public override string ToString() => nameof(TugDailyLogContent); }

public sealed record TugDailyExecutionFact(Guid Id, string State, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, TowageActiveInterval[] Intervals);
public sealed record TugDailyReason(string Scope, string Code, string? Subject);
public sealed record TugDailyReconciliation(string State, TugDailyReason[] Reasons, int JobsCompleted, string? OperatingHours, bool OpenInterval, string? MeterHours);
