using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-100/T-OPS2-004 + DEV-101/T-OPS2-005. Authored only; no execution evidence.
public sealed class ShipOperationsPolicyTests
{
    private static ShipOpsFact Known(string text)=>new("Known",text);
    [Fact]
    public void Original_forty_fields_keep_exact_source_rows_and_only_actual_owning_derived_fields_are_readonly() {
        var fuel=ShipOperationsPolicy.Schema("Fuel");var port=ShipOperationsPolicy.Schema("Port");
        Assert.Equal(Enumerable.Range(2186,22),fuel.Select(f=>f.SourceRow));Assert.Equal(Enumerable.Range(2208,18),port.Select(f=>f.SourceRow));
        Assert.Equal(19,fuel.Count(f=>!f.ReadOnly));Assert.Equal(17,port.Count(f=>!f.ReadOnly));
        Assert.Equal(new[]{"vessel_id","compliance_state","verification_state"},fuel.Where(f=>f.ReadOnly).Select(f=>f.Key));Assert.Equal("sof_state",Assert.Single(port,f=>f.ReadOnly).Key);
        Assert.DoesNotContain(port,f=>f.Key=="vessel_id");Assert.Equal(40,fuel.Concat(port).Count());
        Assert.All(fuel.Concat(port),f=>Assert.Equal(f.Required?null:ShipOperationsPolicy.ConditionalUnresolved,f.RequiredWhen));
        Assert.All(fuel.Concat(port).Where(f=>f.Type=="Reference"&&!f.ReadOnly),f=>Assert.True(f.ReferenceUnavailable));
    }
    [Theory]
    [InlineData("Fuel",19)][InlineData("Port",17)]
    public void Empty_partial_drafts_preserve_missing_and_unknown_without_compliance_verification_or_finalization(string kind,int count) {
        Assert.True(ShipOperationsPolicy.TryNormalize(kind,new Dictionary<string,ShipOpsFact>(),out var missing,out var error));Assert.Equal("",error);Assert.Equal(count,missing.Count);
        Assert.All(missing.Values,v=>{Assert.Equal("Missing",v.State);Assert.Null(v.Value);});
        var key=kind=="Fuel"?"quantity_consumed_mt":"cargo_quantity";
        Assert.True(ShipOperationsPolicy.TryNormalize(kind,new Dictionary<string,ShipOpsFact>{{key,new("Unknown",null)}},out var fields,out _));Assert.Equal("Unknown",fields[key].State);Assert.Null(fields[key].Value);
        var blockers=ShipOperationsPolicy.Blockers(kind,fields);Assert.Contains("SOURCE_POLICY_REVIEW_REQUIRED",blockers);Assert.Contains("OFFLINE_POLICY_UNAVAILABLE",blockers);
        Assert.Contains(kind=="Fuel"?"COMPLIANCE_NOT_ASSESSED":"SOF_FINALIZATION_UNAVAILABLE",blockers);
        Assert.Contains(blockers,b=>b.StartsWith("CONDITIONAL_REQUIREMENT_UNRESOLVED_",StringComparison.Ordinal));
    }
    [Fact]
    public void Source_declared_options_remain_exact_declarations_and_source_conditional_is_not_an_invented_delivery_rule() {
        var schema=ShipOperationsPolicy.Schema("Fuel");Assert.Equal(new[]{"Consumption","Delivery/BDN","Transfer","ROB","Tank Sounding"},schema.Single(f=>f.Key=="record_type").Options);
        Assert.Equal(new[]{"VLSFO","HFO","MGO","LNG","Methanol","Other"},schema.Single(f=>f.Key=="fuel_type").Options);
        Assert.True(ShipOperationsPolicy.TryNormalize("Fuel",new Dictionary<string,ShipOpsFact>{{"record_type",Known("Delivery/BDN")},{"delivery_discrepancy",Known("NONE")}},out var fields,out _));
        Assert.Equal("Missing",fields["quantity_received_mt"].State);Assert.Equal("Missing",fields["bdn_no"].State);
        Assert.Contains("CONDITIONAL_REQUIREMENT_UNRESOLVED_BDN_NO",ShipOperationsPolicy.Blockers("Fuel",fields));
        Assert.False(ShipOperationsPolicy.TryNormalize("Fuel",new Dictionary<string,ShipOpsFact>{{"record_type",Known("Delivery")}},out _,out _));
        Assert.False(ShipOperationsPolicy.TryNormalize("Fuel",new Dictionary<string,ShipOpsFact>{{"delivery_discrepancy",Known("No")}},out _,out _));
        Assert.Equal(new[]{"Arrival","NOR","Pilot Onboard","Tug Made Fast","Berthing","Loading","Discharging","Shifting","Tug Let Go","Pilot Off","Departure","Other"},ShipOperationsPolicy.Schema("Port").Single(f=>f.Key=="activity_type").Options);
    }
    [Theory]
    [InlineData("Fuel","voyage_id")][InlineData("Fuel","supplier")][InlineData("Fuel","delivery_port")][InlineData("Fuel","tank_id")][InlineData("Fuel","evidence_ref")]
    [InlineData("Port","voyage_id")][InlineData("Port","port_call_id")][InlineData("Port","port")][InlineData("Port","letter_of_protest")][InlineData("Port","evidence_ref")]
    public void Unknown_reference_is_preserved_but_a_plausible_UUID_never_invents_a_scoped_owning_reference(string kind,string key) {
        Assert.True(ShipOperationsPolicy.TryNormalize(kind,new Dictionary<string,ShipOpsFact>{{key,new("Unknown",null)}},out var fields,out _));Assert.Equal("Unknown",fields[key].State);
        Assert.False(ShipOperationsPolicy.TryNormalize(kind,new Dictionary<string,ShipOpsFact>{{key,Known(Guid.NewGuid().ToString("D"))}},out _,out var error));Assert.Equal("lookup_source_unavailable",error);
    }
    [Fact]
    public void Structural_numbers_preserve_precision_sign_zero_and_unapproved_range_without_balancing_or_readiness() {
        var input=new Dictionary<string,ShipOpsFact>{{"quantity_received_mt",Known("000.000")},{"quantity_consumed_mt",Known("-0.000")},{"rob_before_mt",Known("1")},{"rob_after_mt",Known("999")},
            {"sulphur_pct",Known("2000.000")},{"density",Known("123456789012345678901234567890.000000000000")}};
        Assert.True(ShipOperationsPolicy.TryNormalize("Fuel",input,out var fields,out _));foreach(var pair in input)Assert.Equal(pair.Value,fields[pair.Key]);
        var read=ShipOperationsPolicy.ReadFields("Fuel",Guid.NewGuid(),JsonSerializer.Serialize(fields,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("REVIEW_REQUIRED",read["compliance_state"].Value);Assert.Equal("PENDING",read["verification_state"].Value);Assert.Equal("999",read["rob_after_mt"].Value);
        Assert.All(ShipOperationsPolicy.Schema("Fuel").Where(f=>f.Unit=="mt"),f=>Assert.EndsWith("_mt",f.Key));
        foreach(var key in new[]{"viscosity","density","tank_capacity"})Assert.Null(ShipOperationsPolicy.Schema("Fuel").Single(f=>f.Key==key).Unit);
        Assert.All(ShipOperationsPolicy.Schema("Port"),f=>Assert.Null(f.Unit));
    }
    [Theory]
    [InlineData("NaN")][InlineData("Infinity")][InlineData("1e3")][InlineData("1,5")][InlineData(" 1")][InlineData("1.")][InlineData(".1")][InlineData("--1")][InlineData("-")]
    public void Numeric_transport_rejects_nonlexical_or_ambiguous_values_without_coercion(string value)=>Assert.False(ShipOperationsPolicy.TryNormalize("Fuel",new Dictionary<string,ShipOpsFact>{{"density",Known(value)}},out _,out _));
    [Theory]
    [InlineData("2026-11-05")][InlineData("2026-11-05T12:00:00")][InlineData("2026-11-05T12:00Z")][InlineData("2026-02-30T12:00:00Z")]
    [InlineData("2026-11-05T12:00:60Z")][InlineData("2026-11-05T12:00:00+14:01")][InlineData("2026-11-05T12:00:00-00:00")][InlineData("2026-11-05T12:00:00.12345678Z")]
    public void Instant_transport_requires_valid_explicit_offset_full_seconds_and_known_zone(string value) {
        Assert.False(ShipOperationsPolicy.TryInstant(value,out _));Assert.False(ShipOperationsPolicy.TryNormalize("Fuel",new Dictionary<string,ShipOpsFact>{{"record_date",Known(value)}},out _,out _));
    }
    [Fact]
    public void UTC_projection_preserves_original_lexical_offsets_and_orders_events_by_instant_without_assuming_unknown_end() {
        var start="2026-11-05T10:00:00.1234567+02:00";var end="2026-11-05T09:30:00.1234567+01:00";
        Assert.True(ShipOperationsPolicy.TryInstant(start,out var utc));Assert.Equal(TimeSpan.Zero,utc.Offset);Assert.Equal(8,utc.Hour);
        Assert.True(ShipOperationsPolicy.TryNormalize("Port",new Dictionary<string,ShipOpsFact>{{"event_start",Known(start)},{"event_end",Known(end)}},out var fields,out _));Assert.Equal(start,fields["event_start"].Value);Assert.Equal(end,fields["event_end"].Value);
        Assert.False(ShipOperationsPolicy.TryNormalize("Port",new Dictionary<string,ShipOpsFact>{{"event_start",Known("2026-11-05T10:00:00+01:00")},{"event_end",Known("2026-11-05T10:30:00+02:00")}},out _,out var error));Assert.Equal("event_order_invalid",error);
        Assert.True(ShipOperationsPolicy.TryNormalize("Port",new Dictionary<string,ShipOpsFact>{{"event_start",Known(start)},{"event_end",new("Unknown",null)}},out var incomplete,out _));Assert.Equal("Unknown",incomplete["event_end"].State);
    }
    [Fact]
    public void Readonly_state_scope_and_unrecognized_field_injection_are_denied_even_for_unknown_facts() {
        foreach(var kind in ShipOperationsPolicy.Kinds)foreach(var field in ShipOperationsPolicy.Schema(kind).Where(f=>f.ReadOnly)) {
            Assert.False(ShipOperationsPolicy.TryNormalize(kind,new Dictionary<string,ShipOpsFact>{{field.Key,Known(field.Key=="vessel_id"?Guid.NewGuid().ToString():"VERIFIED")}},out _,out _));
            Assert.False(ShipOperationsPolicy.TryNormalize(kind,new Dictionary<string,ShipOpsFact>{{field.Key,new("Unknown",null)}},out _,out _));
        }
        Assert.False(ShipOperationsPolicy.TryNormalize("Port",new Dictionary<string,ShipOpsFact>{{"vessel_id",Known(Guid.NewGuid().ToString())}},out _,out _));
        Assert.False(ShipOperationsPolicy.TryNormalize("Fuel",new Dictionary<string,ShipOpsFact>{{"approved_by",Known("untrusted")}},out _,out _));
        Assert.False(ShipOperationsPolicy.TryNormalize("Fuel",new Dictionary<string,ShipOpsFact>{{"density",new("Unknown","0")}},out _,out _));
        Assert.False(ShipOperationsPolicy.TryNormalize("Fuel",new Dictionary<string,ShipOpsFact>{{"density",new("Known",null)}},out _,out _));
    }
    [Fact]
    public void Web_fact_JSON_roundtrips_lexically_with_server_readonly_values_and_never_manufactures_port_vessel_source_field() {
        var vessel=Guid.NewGuid();var fuel=ShipOperationsPolicy.ReadFields("Fuel",vessel,"{\"bdn_no\":{\"state\":\"Known\",\"value\":\"MASKED-BDN\"}}");
        Assert.Equal("MASKED-BDN",fuel["bdn_no"].Value);Assert.Equal(vessel.ToString("D"),fuel["vessel_id"].Value);Assert.Equal("REVIEW_REQUIRED",fuel["compliance_state"].Value);Assert.Equal("PENDING",fuel["verification_state"].Value);
        var port=ShipOperationsPolicy.ReadFields("Port",vessel,"{}");Assert.Equal(18,port.Count);Assert.Equal("DRAFT",port["sof_state"].Value);Assert.False(port.ContainsKey("vessel_id"));
        Assert.Throws<InvalidDataException>(()=>ShipOperationsPolicy.ReadFields("Port",vessel,"{\"sof_state\":{\"state\":\"Known\",\"value\":\"FINAL\"}}"));
        Assert.Throws<InvalidDataException>(()=>ShipOperationsPolicy.ReadFields("Fuel",vessel,"{\"density\":{\"state\":\"Known\",\"value\":5}}"));
        Assert.Throws<InvalidDataException>(()=>ShipOperationsPolicy.ReadFields("Fuel",vessel,"{\"density\":{\"state\":\"Missing\"}}"));
        Assert.DoesNotContain("MASKED",Known("MASKED-PRIVATE-TEXT").ToString());
    }
}
