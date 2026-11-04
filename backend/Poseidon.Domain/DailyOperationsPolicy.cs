using System.Globalization;
using System.Text.Json;

namespace Poseidon.Domain;

public sealed record DailyValue(string State, string? Value);
public sealed record DailyFieldDefinition(string Key, string Type, bool Required, string[] Options,
    bool ReadOnly, bool ReferenceUnavailable, int SourceRow, string? Unit = null);

// Structural draft policy, not an approved operational range or safety assessment.
// Source: 14.02/Data Dictionary 2316/rows2128-2185; exact source pin travels in capabilities.
public static class DailyOperationsPolicy
{
    public const string SourceVersion = "14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3";
    public static readonly string[] Kinds = ["Noon", "Deck", "Engine"];
    private const string Watch = "0000-0400;0400-0800;0800-1200;1200-1600;1600-2000;2000-2400;Daily Summary";
    private static DailyFieldDefinition F(string key, string type, int row, bool required = false,
        string options = "", bool readOnly = false, string? unit = null) =>
        new(key, type, required, options.Length == 0 ? [] : options.Split(';'), readOnly,
            type == "Reference" && !readOnly, row, unit);
    public static DailyFieldDefinition[] Schema(string kind) => kind switch {
        "Noon" => [
            F("report_date","Date",2128,true), F("report_time_utc","Text",2129,true), F("vessel_id","Reference",2130,true,readOnly:true),
            F("voyage_id","Reference",2131,true), F("position_lat","Number",2132,true), F("position_lon","Number",2133,true),
            F("activity","Code",2134,true,"At Sea;Anchored;Berthing;Berthed;Loading;Discharging;Drifting;Other"),
            F("from_port","Reference",2135), F("to_port","Reference",2136), F("eta","Text",2137),
            F("distance_run_nm","Number",2138,unit:"nm"), F("distance_to_go_nm","Number",2139,unit:"nm"), F("avg_speed_kn","Number",2140,unit:"kn"),
            F("weather_summary","Narrative",2141), F("cargo_status","Text",2142), F("main_engine_hours","Number",2143),
            F("fuel_summary","Text",2144), F("rob_summary","Text",2145), F("master_review","Code",2146,true,"DRAFT;REVIEWED;SUBMITTED",true), F("evidence_ref","Reference",2147)],
        "Deck" => [
            F("log_date","Text",2148,true), F("watch_period","Code",2149,true,Watch), F("vessel_id","Reference",2150,true,readOnly:true),
            F("position","Text",2151), F("heading","Number",2152), F("speed","Number",2153), F("weather","Text",2154), F("visibility","Text",2155),
            F("nav_status","Code",2156,options:"Underway;At Anchor;Moored;Pilotage;Restricted;Other"), F("port_activity","Text",2157),
            F("pilot_tug_activity","Text",2158), F("ballast_activity","Text",2159), F("deck_work","Narrative",2160),
            F("safety_observation","Narrative",2161), F("handover_note","Narrative",2162,true), F("officer_of_watch","Reference",2163,true),
            F("master_review","Code",2164,options:"PENDING;REVIEWED;RETURNED",readOnly:true), F("evidence_ref","Reference",2165)],
        "Engine" => [
            F("log_date","Text",2166,true), F("watch_period","Code",2167,true,Watch), F("vessel_id","Reference",2168,true,readOnly:true),
            F("main_engine_status","Code",2169,true,"RUNNING;STANDBY;STOPPED;RESTRICTED"), F("main_engine_hours","Number",2170),
            F("aux_engine_hours","Number",2171), F("fuel_consumption","Number",2172), F("lube_oil_consumption","Number",2173),
            F("fresh_water_production","Number",2174), F("critical_measurements","Narrative",2175), F("alarms","Narrative",2176),
            F("defects","Narrative",2177), F("work_orders","Reference",2178), F("bunkering_activity","Text",2179),
            F("bilge_sludge_status","Text",2180), F("environmental_control","Code",2181,options:"OK;ATTENTION;REVIEW_REQUIRED"),
            F("watch_engineer","Reference",2182,true), F("chief_engineer_review","Code",2183,options:"PENDING;REVIEWED;RETURNED",readOnly:true),
            F("handover_note","Narrative",2184,true), F("evidence_ref","Reference",2185)],
        _ => throw new ArgumentException("unknown_kind", nameof(kind))
    };

    public static bool TryNormalize(string kind, IReadOnlyDictionary<string, DailyValue>? input,
        out SortedDictionary<string, DailyValue> result, out string error)
    {
        result = new(StringComparer.Ordinal); error = "invalid_payload";
        if (input == null || !Kinds.Contains(kind)) return false;
        var schema = Schema(kind);
        if (input.Keys.Any(key => !schema.Any(f => f.Key == key && !f.ReadOnly))) return false;
        foreach (var field in schema.Where(f => !f.ReadOnly)) {
            var value = input.GetValueOrDefault(field.Key) ?? new DailyValue("Missing", null);
            if (value.State is "Missing" or "Unknown") {
                if (value.Value != null) return false;
            } else if (value.State == "Known") {
                var text = value.Value;
                if (string.IsNullOrWhiteSpace(text) || text.Length > (field.Type == "Narrative" ? 4000 : 512)
                    || text.Any(c => char.IsControl(c) && !(field.Type == "Narrative" && c is '\r' or '\n' or '\t'))) return false;
                if (field.ReferenceUnavailable) { error = "lookup_source_unavailable"; return false; }
                if (field.Type == "Code" && !field.Options.Contains(text, StringComparer.Ordinal)) return false;
                if (field.Type == "Date" && !DateOnly.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)) return false;
                if (field.Type == "Number" && !ExactDecimal(text)) return false;
            } else return false;
            result.Add(field.Key, value);
        }
        error = ""; return true;
    }
    // Lexical decimal text is retained exactly. No binary conversion, rounding,
    // guessed units, operational limits, or implied zero for a missing reading.
    private static bool ExactDecimal(string text) {
        if (text.Length > 48) return false;
        var digits = text[0] == '-' ? text[1..] : text;
        var parts = digits.Split('.');
        return parts.Length is 1 or 2 && parts.All(p => p.Length > 0 && p.All(c => c is >= '0' and <= '9'));
    }
    public static SortedDictionary<string, DailyValue> ReadFields(string kind, Guid vessel, string json) {
        var fields = JsonSerializer.Deserialize<SortedDictionary<string, DailyValue>>(json) ?? new(StringComparer.Ordinal);
        fields["vessel_id"] = new("Known", vessel.ToString("D"));
        fields[kind == "Engine" ? "chief_engineer_review" : "master_review"] = new("Known",kind == "Noon" ? "DRAFT" : "PENDING");
        return fields;
    }
    public static string[] Blockers(string kind, IReadOnlyDictionary<string, DailyValue> fields) =>
        Schema(kind).Where(f => f.Required && !f.ReadOnly && (!fields.TryGetValue(f.Key,out var value) || value.State != "Known"))
            .Select(f => "REQUIRED_" + f.Key.ToUpperInvariant()).Concat(["UNVERIFIED_DECLARATION", "HUMAN_REVIEW_AUTHORITY_UNAVAILABLE", "OFFLINE_POLICY_UNAVAILABLE"]).ToArray();
}
