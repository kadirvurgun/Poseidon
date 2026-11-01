using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-053. Draft declarations are not completed/verified work or operational fitness.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaintenanceInputs(
    string? TriggerType = null, string? IntervalValue = null, string? IntervalUnit = null,
    string? MeterSource = null, DateOnly? LastDoneDate = null, string? LastDoneMeter = null,
    string? CurrentMeter = null, DateTimeOffset? ObservedAt = null, string? ObservationRevision = null,
    string? Condition = null, string? TimeZone = null, string? Provenance = null,
    string? WarningLead = null, string? GracePeriod = null, string? PostponementAllowed = null,
    string? ApprovalRule = null, string? Criticality = null, string? ApplicableRequirement = null,
    string? PlanType = null, Guid? ObservationId = null);
public sealed record MaintenanceDue(string State, string Reason, DateOnly? NextDueDate = null,
    string? NextDueMeter = null, string Algorithm = "planning-v1");

public static class MaintenanceCalculation
{
    // Exact base-10 input. Do not round excess precision, parse exponents or convert via double.
    public static bool Number(string? input, out decimal value)
    {
        value = 0;
        if (input == null || input.Length > 60 || input.Length == 0 || input.Count(c => c == '.') > 1 ||
            input.Any(c => c != '.' && (c < '0' || c > '9'))) return false;
        var digits = input.Replace(".", "").TrimStart('0');
        if (digits.Length > 28 || input.EndsWith('.') || (input.Contains('.') && input.Length - input.IndexOf('.') - 1 > 28)) return false;
        return decimal.TryParse(input, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
    public static MaintenanceDue Evaluate(MaintenanceInputs x, DateTimeOffset reference, MaintenanceObservationAssessment? observation = null)
    {
        if (string.IsNullOrWhiteSpace(x.Provenance)) return new("ReviewRequired", "MissingProvenance");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(x.TimeZone ?? ""); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { return new("ReviewRequired", "MissingTimeZone"); }
        if (x.TriggerType == "Calendar") {
            if (!Number(x.IntervalValue, out var interval) || interval <= 0 || interval != decimal.Truncate(interval) || interval > int.MaxValue || x.LastDoneDate == null)
                return new("ReviewRequired", "MissingCalendarInputs");
            DateOnly due;
            try {
                due = x.IntervalUnit switch {
                    "day" => x.LastDoneDate.Value.AddDays((int)interval),
                    "month" => x.LastDoneDate.Value.AddMonths((int)interval),
                    "year" => x.LastDoneDate.Value.AddYears((int)interval),
                    _ => throw new ArgumentException()
                };
            } catch (ArgumentException) { return new("ReviewRequired", "UnsupportedCalendarInterval"); }
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reference, zone).DateTime);
            if (x.LastDoneDate > today) return new("ReviewRequired", "FutureBaseline");
            return new(today >= due ? "Due" : "NotDue", "CalendarBoundary", due);
        }
        if (x.TriggerType == "Meter") {
            if (string.IsNullOrWhiteSpace(x.MeterSource) || string.IsNullOrWhiteSpace(x.IntervalUnit) || x.ObservedAt == null ||
                string.IsNullOrWhiteSpace(x.ObservationRevision) || !Number(x.CurrentMeter, out var current))
                return new("ReviewRequired", "MissingMeterAdapterOrReading");
            if (x.ObservedAt > reference) return new("ReviewRequired", "FutureObservation");
            // No source freshness duration exists: a historic observation does not prove current usage.
            if (x.ObservedAt != reference && observation?.Ready != true) return new("ReviewRequired", "ReadingFreshnessPolicyUnresolved");
            if (!Number(x.IntervalValue, out var interval) || interval <= 0 || !Number(x.LastDoneMeter, out var baseline) || current < baseline)
                return new("ReviewRequired", "MissingOrResetMeterBaseline");
            try {
                var next = checked(baseline + interval);
                if (next - baseline != interval) return new("ReviewRequired", "MeterPrecisionExceeded");
                return new(current >= next ? "Due" : "NotDue", "DeclaredMeterBoundary", NextDueMeter: next.ToString(CultureInfo.InvariantCulture));
            } catch (OverflowException) { return new("ReviewRequired", "MeterRangeExceeded"); }
        }
        if (x.TriggerType == "Condition") {
            if (x.Condition is not ("True" or "False") || x.ObservedAt == null || string.IsNullOrWhiteSpace(x.ObservationRevision))
                return new("ReviewRequired", "MissingConditionAdapterOrObservation");
            if (x.ObservedAt > reference) return new("ReviewRequired", "FutureObservation");
            if (x.ObservedAt != reference && observation?.Ready != true) return new("ReviewRequired", "ConditionFreshnessPolicyUnresolved");
            return new(x.Condition == "True" ? "Due" : "NotDue", "DeclaredConditionObservation");
        }
        return new("ReviewRequired", "MissingOrUnsupportedTrigger");
    }
}
