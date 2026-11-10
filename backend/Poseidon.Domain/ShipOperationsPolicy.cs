using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShipOpsFact([property:JsonRequired] string State,[property:JsonRequired] string? Value) {
    public override string ToString()=>nameof(ShipOpsFact);
}
public sealed record ShipOpsFieldDefinition(string Key,string Type,bool Required,string[] Options,bool ReadOnly,
    bool ReferenceUnavailable,int SourceRow,string? Unit=null,string? RequiredWhen=null);

// DEV-100/101: one Ship Operations ledger, two kinds. Source L/M/N determine
// structural types; source Q/AR retain unresolved conditional predicates. This
// is an incomplete declaration policy, never an approved operational catalog.
public static class ShipOperationsPolicy
{
    public const string SourceVersion="14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2186-2225:v46.3";
    public const string SchemaVersion="poseidon.ship-operations.draft.v1";
    public const string ConditionalUnresolved="SOURCE_CONDITIONAL_UNRESOLVED";
    public static readonly string[] Kinds=["Fuel","Port"];
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=4,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    private static ShipOpsFieldDefinition F(string key,string type,int row,bool required=false,string options="",bool readOnly=false,string? unit=null)=>
        new(key,type,required,options.Length==0?[]:options.Split(';'),readOnly,type=="Reference"&&!readOnly,row,unit,required?null:ConditionalUnresolved);
    public static ShipOpsFieldDefinition[] Schema(string kind)=>kind switch {
        "Fuel"=>[
            F("record_date","Instant",2186,true),F("vessel_id","Reference",2187,true,readOnly:true),F("voyage_id","Reference",2188),
            F("record_type","Code",2189,true,"Consumption;Delivery/BDN;Transfer;ROB;Tank Sounding"),
            F("fuel_type","Code",2190,true,"VLSFO;HFO;MGO;LNG;Methanol;Other"),F("supplier","Reference",2191),F("bdn_no","Text",2192),
            F("delivery_port","Reference",2193),F("quantity_received_mt","Number",2194,unit:"mt"),F("quantity_consumed_mt","Number",2195,unit:"mt"),
            F("rob_before_mt","Number",2196,unit:"mt"),F("rob_after_mt","Number",2197,unit:"mt"),F("tank_id","Reference",2198),F("tank_capacity","Number",2199),
            F("sulphur_pct","Number",2200,unit:"%"),F("viscosity","Number",2201),F("density","Number",2202),F("sample_seal_no","Text",2203),
            F("delivery_discrepancy","Code",2204,options:"NONE;ATTENTION;DISPUTED"),
            F("compliance_state","Code",2205,true,"READY;CONDITIONAL;REVIEW_REQUIRED;NOT_READY",true),
            F("verification_state","Code",2206,true,"PENDING;VERIFIED;REJECTED",true),F("evidence_ref","Reference",2207)],
        "Port"=>[
            F("voyage_id","Reference",2208,true),F("port_call_id","Reference",2209,true),F("port","Reference",2210,true),F("terminal","Text",2211),
            F("activity_type","Code",2212,true,"Arrival;NOR;Pilot Onboard;Tug Made Fast;Berthing;Loading;Discharging;Shifting;Tug Let Go;Pilot Off;Departure;Other"),
            F("event_start","Instant",2213,true),F("event_end","Instant",2214),F("remarks","Narrative",2215),F("cargo_operation","Text",2216),
            F("cargo_quantity","Number",2217),F("draught_fwd","Number",2218),F("draught_aft","Number",2219),F("tug_usage","Text",2220),F("pilot_usage","Text",2221),
            F("delay_reason","Narrative",2222),F("letter_of_protest","Reference",2223),F("sof_state","Code",2224,true,"DRAFT;REVIEWED;FINAL",true),F("evidence_ref","Reference",2225)],
        _=>throw new ArgumentException("unknown_kind",nameof(kind))
    };

    public static bool TryNormalize(string kind,IReadOnlyDictionary<string,ShipOpsFact>? input,out SortedDictionary<string,ShipOpsFact> result,out string error) {
        result=new(StringComparer.Ordinal);error="invalid_payload";
        if(input==null || !Kinds.Contains(kind))return false;
        var schema=Schema(kind);
        if(input.Keys.Any(key=>!schema.Any(f=>f.Key==key&&!f.ReadOnly)))return false;
        foreach(var field in schema.Where(f=>!f.ReadOnly)) {
            var present=input.TryGetValue(field.Key,out var fact);
            if(present&&fact==null)return false;
            fact??=new("Missing",null);
            if(fact.State is "Missing" or "Unknown") {if(fact.Value!=null)return false;}
            else if(fact.State=="Known") {
                var text=fact.Value;
                if(string.IsNullOrWhiteSpace(text)||text.Length>(field.Type=="Narrative"?4000:512)||text.Any(c=>char.IsControl(c)&&!(field.Type=="Narrative"&&c is '\r' or '\n' or '\t')))return false;
                if(field.ReferenceUnavailable){error="lookup_source_unavailable";return false;}
                if(field.Type=="Code"&&!field.Options.Contains(text,StringComparer.Ordinal))return false;
                if(field.Type=="Number"&&!LexicalNumber(text))return false;
                if(field.Type=="Instant"&&!TryInstant(text,out _))return false;
            }else return false;
            result.Add(field.Key,fact);
        }
        if(kind=="Port" && result["event_start"] is {State:"Known",Value:{} start} && result["event_end"] is {State:"Known",Value:{} end} && TryInstant(start,out var from)&&TryInstant(end,out var to)&&to<from) {
            error="event_order_invalid";return false;
        }
        error="";return true;
    }
    // Technical text budget/grammar only. No conversion, rounding, unit guess,
    // positivity, sulphur ceiling, ROB arithmetic or legal threshold is implied.
    private static bool LexicalNumber(string text) {
        if(text.Length is <1 or >48)return false;
        var digits=text[0]=='-'?text[1..]:text;var parts=digits.Split('.');
        return parts.Length is 1 or 2 && parts.All(p=>p.Length>0&&p.All(c=>c is >= '0' and <= '9'));
    }
    // Explicit instant transport is an implementation boundary over source
    // Date/Time fields. Retain the original lexical offset in facts; the returned
    // typed projection is UTC for persistence/order, never a browser-local zone.
    public static bool TryInstant(string? value,out DateTimeOffset instant) {
        instant=default;
        if(value==null||value.Length>40||value.EndsWith("-00:00",StringComparison.Ordinal)||
            !Regex.IsMatch(value,@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})\z",RegexOptions.CultureInvariant|RegexOptions.NonBacktracking)||
            !DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))return false;
        instant=parsed.ToUniversalTime();return true;
    }
    public static SortedDictionary<string,ShipOpsFact> ReadFields(string kind,Guid vessel,string json) {
        if(vessel==Guid.Empty)throw new InvalidDataException("ship_operations_scope_unavailable");
        SortedDictionary<string,ShipOpsFact>? input;
        try{input=JsonSerializer.Deserialize<SortedDictionary<string,ShipOpsFact>>(json,Json);}catch(JsonException){throw new InvalidDataException("ship_operations_fields_unavailable");}
        if(!TryNormalize(kind,input,out var fields,out _))throw new InvalidDataException("ship_operations_fields_unavailable");
        if(kind=="Fuel") {
            fields["vessel_id"]=new("Known",vessel.ToString("D"));fields["compliance_state"]=new("Known","REVIEW_REQUIRED");fields["verification_state"]=new("Known","PENDING");
        }else fields["sof_state"]=new("Known","DRAFT");
        return fields;
    }
    public static string[] Blockers(string kind,IReadOnlyDictionary<string,ShipOpsFact> fields)=>Schema(kind).Where(f=>!f.ReadOnly)
        .SelectMany(f=>new[]{f.Required&&(!fields.TryGetValue(f.Key,out var value)||value?.State!="Known")?"REQUIRED_"+f.Key.ToUpperInvariant():null,
            f.RequiredWhen!=null?"CONDITIONAL_REQUIREMENT_UNRESOLVED_"+f.Key.ToUpperInvariant():null,f.ReferenceUnavailable?"REFERENCE_SOURCE_UNAVAILABLE_"+f.Key.ToUpperInvariant():null})
        .OfType<string>().Concat(["UNVERIFIED_DECLARATION","SOURCE_POLICY_REVIEW_REQUIRED","OFFLINE_POLICY_UNAVAILABLE"])
        .Concat(kind=="Fuel"?["COMPLIANCE_NOT_ASSESSED","VERIFICATION_NOT_RECORDED","APPLICABILITY_SOURCE_UNAVAILABLE"]:["SOF_FINALIZATION_UNAVAILABLE","HUMAN_REVIEW_NOT_RECORDED"]).ToArray();
}
