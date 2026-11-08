using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-102/T-OPS2-006. Test sources only; execution is deliberately deferred.
public sealed class DowntimePolicyTests
{
    private static DowntimeFact Known(string text) => new("Known", text);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Incomplete_draft_preserves_unknown_and_missing_without_defaulting_legal_hire_or_closure()
    {
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> {
            ["end_at"] = new("Unknown", null), ["claim_evidence"] = new("Unknown", null)
        }, out var fields, out var error));
        Assert.Equal("", error); Assert.Equal(11, fields.Count);
        Assert.Equal("Unknown", fields["end_at"].State); Assert.Equal("Unknown", fields["claim_evidence"].State);
        Assert.Equal("Missing", fields["start_at"].State); Assert.Null(fields["duration_minutes"].Value);
        var vessel = Guid.NewGuid(); var read = DowntimePolicy.ReadFields(vessel, JsonSerializer.Serialize(fields, Web));
        Assert.Equal(14, read.Count); Assert.Equal(new DowntimeFact("Known", vessel.ToString("D")), read["vessel_id"]);
        Assert.Equal(new DowntimeFact("Unknown", null), read["loss_of_hire_flag"]);
        Assert.Equal(new DowntimeFact("Unknown", null), read["closure_state"]);
        var blockers = DowntimePolicy.Blockers(fields);
        Assert.Contains("REQUIRED_START_AT", blockers); Assert.Contains("REQUIRED_CAUSE", blockers);
        Assert.Contains("REQUIRED_OPERATIONAL_IMPACT", blockers); Assert.Contains("LOSS_OF_HIRE_NOT_ASSESSED", blockers);
        Assert.Contains("CLOSURE_DEFAULT_UNAVAILABLE", blockers); Assert.Contains("VERIFIED_CLOSURE_UNAVAILABLE", blockers);
        Assert.Contains("OFFLINE_POLICY_UNAVAILABLE", blockers);
    }

    [Fact]
    public void Source_metadata_retains_conditional_gates_and_exact_candidate_options_without_approved_catalog_claim()
    {
        var schema = DowntimePolicy.Schema();
        Assert.Equal(Enumerable.Range(2226, 14), schema.Select(f => f.SourceRow));
        Assert.Equal(new[] { "vessel_id", "loss_of_hire_flag", "closure_state" }, schema.Where(f => f.ReadOnly).Select(f => f.Key));
        Assert.Equal(new[] { "Technical Failure", "Planned Maintenance", "Weather", "Port Delay", "Crew", "Regulatory", "Commercial", "Other" }, schema.Single(f => f.Key == "downtime_type").Options);
        Assert.Equal(new[] { "YES", "NO", "REVIEW_REQUIRED" }, schema.Single(f => f.Key == "loss_of_hire_flag").Options);
        Assert.Equal(new[] { "OPEN", "VERIFIED", "CLOSED" }, schema.Single(f => f.Key == "closure_state").Options);
        Assert.All(schema, f => Assert.Equal(f.Required ? null : DowntimePolicy.ConditionalUnresolved, f.RequiredWhen));
        Assert.Equal("min", schema.Single(f => f.Key == "duration_minutes").Unit);
        Assert.All(schema.Where(f => f.Key != "duration_minutes"), f => Assert.Null(f.Unit));
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { ["downtime_type"] = Known("Technical Failure") }, out var fields, out _));
        Assert.Equal("Missing", fields["linked_defect"].State);
        Assert.Contains("CONDITIONAL_REQUIREMENT_UNRESOLVED_LINKED_DEFECT", DowntimePolicy.Blockers(fields));
        Assert.False(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { ["downtime_type"] = Known("technical failure") }, out _, out _));
    }

    [Theory]
    [InlineData("vessel_id", "00000000-0000-0000-0000-000000000001")]
    [InlineData("loss_of_hire_flag", "NO")][InlineData("loss_of_hire_flag", "YES")]
    [InlineData("loss_of_hire_flag", "REVIEW_REQUIRED")][InlineData("closure_state", "OPEN")]
    [InlineData("closure_state", "VERIFIED")][InlineData("closure_state", "CLOSED")]
    [InlineData("qualified_by", "MASKED-HUMAN")][InlineData("origin", "MASKED-PIN")]
    public void Client_cannot_inject_scope_state_assessment_or_engineering_context_into_source_facts(string key, string value)
    {
        Assert.False(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { [key] = Known(value) }, out _, out _));
        Assert.False(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { [key] = new("Unknown", null) }, out _, out _));
    }

    [Theory]
    [InlineData("voyage_id")][InlineData("linked_defect")][InlineData("linked_work_order")]
    [InlineData("linked_event")][InlineData("claim_evidence")]
    public void Plausible_UUID_does_not_convert_recovered_read_API_into_approved_reference_or_adopted_evidence(string key)
    {
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { [key] = new("Unknown", null) }, out var fields, out _));
        Assert.Equal("Unknown", fields[key].State);
        Assert.False(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { [key] = Known(Guid.NewGuid().ToString("D")) }, out _, out var error));
        Assert.Equal("downtime_lookup_source_unavailable", error);
    }

    [Fact]
    public void Declared_minutes_keep_precision_and_are_not_replaced_by_exact_elapsed_observation()
    {
        const string declared = "000000000000000000000000000001.00000000000000000";
        var input = new Dictionary<string, DowntimeFact> {
            ["start_at"] = Known("2026-11-09T10:00:00.0000000+02:00"),
            ["end_at"] = Known("2026-11-09T09:30:00.0000001+01:00"),
            ["duration_minutes"] = Known(declared)
        };
        Assert.True(DowntimePolicy.TryNormalize(input, out var fields, out _));
        var observed = DowntimePolicy.ObserveInterval(fields);
        Assert.Equal(new DowntimeIntervalObservation("KNOWN", "18000000001", "tick", "UNASSESSED"), observed);
        Assert.Equal(declared, fields["duration_minutes"].Value);
        Assert.Equal(input["start_at"], fields["start_at"]); Assert.Equal(input["end_at"], fields["end_at"]);
        Assert.Contains("DURATION_FORMULA_NOT_APPROVED", DowntimePolicy.Blockers(fields));
        Assert.Contains("LOSS_OF_HIRE_NOT_ASSESSED", DowntimePolicy.Blockers(fields));
    }

    [Theory]
    [InlineData("-0.000")][InlineData("-1")][InlineData("000.00")][InlineData("999999999999999999999999999999999999999999999999")]
    public void Source_has_no_approved_signedness_or_business_precision_so_lexical_minutes_are_preserved(string minutes)
    {
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { ["duration_minutes"] = Known(minutes) }, out var fields, out _));
        Assert.Equal(minutes, fields["duration_minutes"].Value);
        Assert.Equal("UNAVAILABLE", DowntimePolicy.ObserveInterval(fields).State);
    }

    [Theory]
    [InlineData("NaN")][InlineData("Infinity")][InlineData("1e3")][InlineData("1,5")][InlineData(" 1")]
    [InlineData("1.")][InlineData(".1")][InlineData("+")][InlineData("-")][InlineData("１２")]
    [InlineData("9999999999999999999999999999999999999999999999999")]
    public void Malformed_or_overbudget_numeric_transport_is_rejected_without_coercion(string value) =>
        Assert.False(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { ["duration_minutes"] = Known(value) }, out _, out _));

    [Theory]
    [InlineData("2026-11-09")][InlineData("2026-11-09T08:00:00")][InlineData("2026-11-09T08:00Z")]
    [InlineData("2026-02-30T08:00:00Z")][InlineData("2026-11-09T08:00:60Z")]
    [InlineData("2026-11-09T08:00:00-00:00")][InlineData("2026-11-09T08:00:00+14:01")]
    [InlineData("2026-11-09T08:00:00.12345678Z")]
    public void Instants_need_explicit_known_offset_full_seconds_and_representable_precision(string value)
    {
        Assert.False(DowntimePolicy.TryInstant(value, out _));
        Assert.False(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> { ["start_at"] = Known(value) }, out _, out _));
    }

    [Fact]
    public void Chronology_uses_actual_instants_and_equal_end_has_zero_observation_without_implying_closed()
    {
        Assert.False(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> {
            ["start_at"] = Known("2026-11-09T10:00:00+01:00"), ["end_at"] = Known("2026-11-09T10:30:00+02:00")
        }, out _, out var error)); Assert.Equal("downtime_time_order_invalid", error);
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> {
            ["start_at"] = Known("2026-11-09T10:00:00+02:00"), ["end_at"] = Known("2026-11-09T08:00:00Z")
        }, out var fields, out _));
        Assert.True(DowntimePolicy.TryInstant(fields["start_at"].Value, out var utc)); Assert.Equal(TimeSpan.Zero, utc.Offset); Assert.Equal(8, utc.Hour);
        Assert.Equal(new DowntimeIntervalObservation("KNOWN", "0", "tick", "UNASSESSED"), DowntimePolicy.ObserveInterval(fields));
        Assert.Equal(new DowntimeFact("Unknown", null), DowntimePolicy.ReadFields(Guid.NewGuid(), JsonSerializer.Serialize(fields, Web))["closure_state"]);
    }

    [Theory]
    [InlineData("Missing")][InlineData("Unknown")]
    public void Unknown_endpoint_never_becomes_now_or_running_elapsed_duration(string state)
    {
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> {
            ["start_at"] = Known("2026-11-09T08:00:00Z"), ["end_at"] = new(state, null)
        }, out var fields, out _));
        Assert.Equal(new DowntimeIntervalObservation("UNAVAILABLE", null, "tick", "UNASSESSED"), DowntimePolicy.ObserveInterval(fields));
        Assert.Equal(state, fields["end_at"].State); Assert.Equal("Missing", fields["duration_minutes"].State);
    }

    [Fact]
    public void Large_interval_uses_lossless_string_ticks_rather_than_unsafe_JSON_number_or_rounded_minutes()
    {
        Assert.True(DowntimePolicy.TryNormalize(new Dictionary<string, DowntimeFact> {
            ["start_at"] = Known("0001-01-01T00:00:00Z"), ["end_at"] = Known("9999-12-31T23:59:59.9999999Z")
        }, out var fields, out _));
        var observed = DowntimePolicy.ObserveInterval(fields);
        Assert.Equal("3155378975999999999", observed.ElapsedTicks);
        Assert.Equal(JsonValueKind.String, JsonSerializer.SerializeToElement(observed, Web).GetProperty("elapsedTicks").ValueKind);
        Assert.Equal("Missing", fields["duration_minutes"].State);
    }

    [Theory]
    [InlineData("null")][InlineData("[]")]
    [InlineData("{\"cause\":null}")]
    [InlineData("{\"cause\":{\"state\":\"Known\"}}")]
    [InlineData("{\"cause\":{\"state\":\"Known\",\"value\":null}}")]
    [InlineData("{\"cause\":{\"state\":\"Unknown\",\"value\":\"None\"}}")]
    [InlineData("{\"cause\":{\"state\":\"Missing\",\"value\":null,\"approved\":true}}")]
    [InlineData("{\"cause\":{\"state\":\"Missing\",\"state\":\"Unknown\",\"value\":null}}")]
    [InlineData("{\"cause\":{\"state\":\"Missing\",\"value\":null},\"cause\":{\"state\":\"Unknown\",\"value\":null}}")]
    [InlineData("{\"loss_of_hire_flag\":{\"state\":\"Known\",\"value\":\"NO\"}}")]
    public void Stored_fact_decoding_rejects_ambiguous_or_injected_JSON(string json) =>
        Assert.Throws<InvalidDataException>(() => DowntimePolicy.ReadFields(Guid.NewGuid(), json));

    [Fact]
    public void Narrative_roundtrip_keeps_newlines_but_diagnostic_strings_do_not_disclose_operator_text_or_source_pin()
    {
        const string text = "MASKED-CAUSE\nMASKED-DETAIL\tMASKED";
        var fields = DowntimePolicy.ReadFields(Guid.NewGuid(), JsonSerializer.Serialize(new Dictionary<string, DowntimeFact> { ["cause"] = Known(text) }, Web));
        Assert.Equal(text, fields["cause"].Value); Assert.DoesNotContain("MASKED", fields["cause"].ToString());
        Assert.DoesNotContain("MASKED", new DowntimeOriginPin("Defect", Guid.NewGuid(), 1, 2, "MASKED-HASH", "MASKED-SOURCE").ToString());
        Assert.Throws<InvalidDataException>(() => DowntimePolicy.ReadFields(Guid.Empty, "{}"));
    }
}
