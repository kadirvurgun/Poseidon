using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-103 / T-PMS2-001 (18.01 Backlog row 104): Service & Engineer Report linked to work order and
// equipment. Field set: 59.04 Rev2 service-engineer-report (paragraphs 2440-2474: report_no, vessel_id,
// equipment_id, work_order_id, service_provider, engineer_name, service_date, reason_scope,
// work_performed, parts_used, measurements, defects_found, recommendations, next_action_due,
// verification_state, evidence_ref). POL-DEFAULT-01: drafts may be incomplete; submission is
// blocked while required data is missing. A VERIFY record is a human record by someone who did not
// author the revision; it is not class/maker approval and does not close any linked defect.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServicePart([property: JsonRequired] string Description, string? PartNumber, string? Quantity, string? Unit)
{ public override string ToString() => nameof(ServicePart); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceMeasurement([property: JsonRequired] string Name, string? Value, string? Unit, string? Result)
{ public override string ToString() => nameof(ServiceMeasurement); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceReportContent(string? ServiceProvider, string? EngineerName, string? ServiceDate, string? ReasonScope,
    string? WorkPerformed, [property: JsonRequired] ServicePart[] Parts, [property: JsonRequired] ServiceMeasurement[] Measurements,
    string? DefectsFound, string? Recommendations, string? NextActionDue)
{ public override string ToString() => nameof(ServiceReportContent); }

public static class ServiceReportPolicy
{
    public const string Draft = "DRAFT", Submitted = "SUBMITTED", Verified = "VERIFIED", Returned = "RETURNED";
    public static readonly string[] States = [Draft, Submitted, Verified, Returned];
    public static readonly string[] ReasonCodes = ["CREATE", "EDIT", "SUBMIT", "VERIFY", "RETURN", "AMEND"];
    public static readonly string[] Decisions = ["VERIFY", "RETURN"];
    public static readonly string[] MeasurementResults = ["WITHIN_LIMITS", "OUT_OF_LIMITS", "INFORMATION"];
    public const int MaxItems = 100, MaxLinks = 20, MaxShort = 200, MaxLong = 4000, MinReason = 10;
    public const string ReadPermission = "service.report.read", WritePermission = "service.report.write", VerifyPermission = "service.report.verify";

    private static bool Clean(string? value, int max, bool multiline) => value == null || value.Trim().Length > 0 && value.Length <= max &&
        !value.Any(c => char.IsControl(c) && !(multiline && c is '\r' or '\n' or '\t'));
    public static bool TryDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static bool Quantity(string? value) => value == null || value.Length <= 32 &&
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var q) && q > 0;

    public static bool Valid(ServiceReportContent? c)
    {
        if (c == null || c.Parts == null || c.Measurements == null || c.Parts.Length > MaxItems || c.Measurements.Length > MaxItems) return false;
        if (!Clean(c.ServiceProvider, MaxShort, false) || !Clean(c.EngineerName, MaxShort, false) || !Clean(c.ReasonScope, MaxLong, true) ||
            !Clean(c.WorkPerformed, MaxLong, true) || !Clean(c.DefectsFound, MaxLong, true) || !Clean(c.Recommendations, MaxLong, true)) return false;
        if (c.ServiceDate != null && !TryDate(c.ServiceDate, out _) || c.NextActionDue != null && !TryDate(c.NextActionDue, out _)) return false;
        if (c.Parts.Any(p => p == null || p.Description == null || !Clean(p.Description, MaxShort, false) || !Clean(p.PartNumber, 80, false) ||
            !Quantity(p.Quantity) || !Clean(p.Unit, 24, false))) return false;
        return c.Measurements.All(m => m != null && m.Name != null && Clean(m.Name, MaxShort, false) && Clean(m.Value, 80, false) && Clean(m.Unit, 24, false) &&
            (m.Result == null || MeasurementResults.Contains(m.Result, StringComparer.Ordinal)));
    }

    // Missing required data for submission. Equipment is required (row 104 links the report to the
    // equipment history); the work order link is optional because a report may precede its WO.
    public static string[] Missing(ServiceReportContent c, bool hasEquipment, DateOnly today)
    {
        var missing = new List<string>();
        if (c.ServiceProvider == null) missing.Add("service_provider");
        if (c.EngineerName == null) missing.Add("engineer_name");
        if (c.ServiceDate == null) missing.Add("service_date");
        else if (TryDate(c.ServiceDate, out var date) && date > today) missing.Add("service_date_in_future");
        if (c.ReasonScope == null) missing.Add("reason_scope");
        if (c.WorkPerformed == null) missing.Add("work_performed");
        if (!hasEquipment) missing.Add("equipment_id");
        if (c.Measurements.Any(m => m.Value == null)) missing.Add("measurement_value");
        return [.. missing];
    }

    public static bool CanEdit(string state) => state is Draft or Returned;
}
