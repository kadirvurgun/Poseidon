using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugRequestInputs(string? CustomerRef = null, string? AssistedVessel = null,
    string? AssistedVesselImo = null, string? JobType = null, DateTimeOffset? RequestedStart = null,
    DateTimeOffset? RequestedEnd = null, string? OperationArea = null, string? PickupPoint = null,
    string? DestinationPoint = null, string? RequiredBollardPull = null, int? RequiredTugCount = null,
    string? SpecialRequirements = null, string? Priority = null, Guid? RequestOwner = null, string? Notes = null, string? RequestSource = null);

// 14.02 rows1916..1931. Bounds are transport/storage limits, not legal/BP thresholds.
public static class TugRequestPolicy
{
    public const string SourceVersion = "poseidon-v46.3-20261030/14.02/1914-1931/TUG-FOUNDATION-01";
    public static readonly string[] RequestSources = ["Customer", "Agent", "Internal", "Contract", "Emergency"];
    public static readonly string[] JobTypes = ["Harbour Towage", "Escort", "Berthing Assist", "Unberthing Assist", "Standby", "Emergency Towage", "Sea Towage"];
    public static readonly string[] Priorities = ["Normal", "High", "Critical", "Emergency"];
    public static bool TryBp(string? text, out decimal? value)
    {
        value = null;
        if (text == null) return true;
        if (!Regex.IsMatch(text, @"\A(0|[1-9][0-9]{0,7})(\.[0-9]{1,2})?\z", RegexOptions.CultureInvariant)) return false;
        value = decimal.Parse(text, CultureInfo.InvariantCulture); return true;
    }
    public static string[] Invalid(TugRequestInputs x)
    {
        var errors = new List<string>();
        foreach (var (key, value, max) in new (string, string?, int)[] {
            ("customer_ref", x.CustomerRef, 200), ("assisted_vessel", x.AssistedVessel, 200),
            ("operation_area", x.OperationArea, 200), ("pickup_point", x.PickupPoint, 200),
            ("destination_point", x.DestinationPoint, 200), ("special_requirements", x.SpecialRequirements, 2000), ("notes", x.Notes, 2000) })
            if (value != null && (value.Length > max || value.Any(char.IsControl) || string.IsNullOrWhiteSpace(value))) errors.Add(key);
        if (x.RequestSource != null && !RequestSources.Contains(x.RequestSource)) errors.Add("request_source");
        if (x.JobType != null && !JobTypes.Contains(x.JobType)) errors.Add("job_type");
        if (x.Priority != null && !Priorities.Contains(x.Priority)) errors.Add("priority");
        if (x.RequestOwner == Guid.Empty) errors.Add("request_owner");
        if (x.RequiredTugCount is < 1 or > 1000) errors.Add("required_tug_count");
        if (!TryBp(x.RequiredBollardPull, out _)) errors.Add("required_bollard_pull");
        if (x.AssistedVesselImo != null && !ValidImo(x.AssistedVesselImo)) errors.Add("assisted_vessel_imo");
        if (x.RequestedStart?.Offset != null && x.RequestedStart.Value.Offset != TimeSpan.Zero ||
            x.RequestedEnd?.Offset != null && x.RequestedEnd.Value.Offset != TimeSpan.Zero) errors.Add("time_offset");
        if (TugPlanningWindowPolicy.Assess(new(x.RequestedStart, x.RequestedEnd)) == TugPlanningWindowState.InvalidOrder) errors.Add("requested_end");
        return errors.ToArray();
    }
    public static bool ValidImo(string text) => text.Length == 7 && Regex.IsMatch(text, @"\A[0-9]{7}\z", RegexOptions.CultureInvariant) &&
        Enumerable.Range(0, 6).Sum(i => (text[i] - '0') * (7 - i)) % 10 == text[6] - '0';
    public static string[] Missing(TugRequestInputs x) => new (string Key, bool Missing)[] {
        ("request_source", x.RequestSource == null), ("assisted_vessel", x.AssistedVessel == null), ("job_type", x.JobType == null),
        ("requested_start", x.RequestedStart == null), ("operation_area", x.OperationArea == null),
        ("required_tug_count", x.RequiredTugCount == null), ("priority", x.Priority == null), ("request_owner", x.RequestOwner == null)
    }.Where(x => x.Missing).Select(x => x.Key).ToArray();
}
