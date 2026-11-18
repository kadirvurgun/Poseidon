using System.Globalization;
using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class WorkOrderPolicyTests
{
    private static SortedDictionary<string, WorkOrderFact> Fields(params (string Key, string State, string? Value)[] values)
    {
        Assert.True(WorkOrderPolicy.TryNormalize(values.ToDictionary(x => x.Key, x => new WorkOrderFact(x.State, x.Value)), out var fields, out _));
        return fields;
    }

    [Fact]
    public void Incomplete_owning_declaration_preserves_unknown_and_does_not_authorize_execution()
    {
        var fields = Fields(("equipment_id", "Unknown", null), ("planned_start", "Unknown", null));
        Assert.Equal(25, fields.Count);
        Assert.Equal("Unknown", fields["equipment_id"].State);
        Assert.Equal("Unknown", fields["planned_start"].State);
        Assert.Equal("Missing", fields["work_order_no"].State);
        Assert.Contains("REQUIRED_WORK_ORDER_NO", WorkOrderPolicy.Blockers(fields));
        Assert.Contains("REQUIRED_EQUIPMENT_ID", WorkOrderPolicy.Blockers(fields));
        Assert.Contains("RELEASE_AUTHORITY_UNAVAILABLE", WorkOrderPolicy.Blockers(fields));
        Assert.Contains("REQUIRED_EVIDENCE_POLICY_UNAVAILABLE", WorkOrderPolicy.Blockers(fields));
    }

    [Theory]
    [InlineData("RELEASED")]
    [InlineData("IN_PROGRESS")]
    [InlineData("EVIDENCE_SUBMITTED")]
    public void Execution_literal_and_verification_text_are_unassessed_declarations_only(string stage)
    {
        var fields = Fields(("execution_stage", "Known", stage), ("work_done", "Known", "Declared work\nFurther observation"),
            ("verification_result", "Known", "Verified"));
        Assert.Equal(stage, fields["execution_stage"].Value);
        Assert.Contains("EXECUTION_STAGE_UNASSESSED", WorkOrderPolicy.Blockers(fields));
        Assert.Contains("HUMAN_VERIFICATION_NOT_RECORDED", WorkOrderPolicy.Blockers(fields));
        Assert.Contains("VERIFICATION_AND_CLOSURE_UNAVAILABLE", WorkOrderPolicy.Blockers(fields));
        Assert.Contains("PERMIT_AND_RISK_NOT_AUTHORIZED", WorkOrderPolicy.Blockers(fields));
        Assert.Contains("EVIDENCE_ADOPTION_UNAVAILABLE", WorkOrderPolicy.Blockers(fields));
        var json = JsonSerializer.Serialize(fields, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(fields.ToArray(), WorkOrderPolicy.ReadFields(json).ToArray());
    }

    [Theory]
    [InlineData("status", "RELEASED")]
    [InlineData("permit_status", "APPROVED")]
    [InlineData("risk_status", "READY")]
    [InlineData("sync_state", "SYNCED")]
    [InlineData("vessel_id", "00000000-0000-0000-0000-000000000001")]
    [InlineData("execution_stage", "VERIFIED")]
    [InlineData("execution_stage", "CLOSED")]
    [InlineData("equipment_id", "00000000-0000-0000-0000-000000000001")]
    [InlineData("job_id", "a tug job is not a maintenance plan")]
    [InlineData("assigned_to", "2026-11-08")]
    [InlineData("assigned_to", "Chief Engineer")]
    [InlineData("executed_by", "Chief Engineer")]
    [InlineData("verified_by", "Expert")]
    [InlineData("photo_evidence", "00000000-0000-0000-0000-000000000001")]
    [InlineData("document_evidence", "00000000-0000-0000-0000-000000000001")]
    [InlineData("priority", "High")]
    [InlineData("criticality", "Critical")]
    [InlineData("work_order_type", "Preventive")]
    public void Caller_state_guessed_catalogs_and_conflicting_person_date_are_rejected(string key, string value) =>
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { [key] = new("Known", value) }, out _, out _));

    [Theory]
    [InlineData("2026-11-08")]
    [InlineData("2028-02-29")]
    public void Calendar_date_remains_exact_without_browser_or_server_timezone_conversion(string date)
    {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var fields = Fields(("planned_start", "Known", date));
            Assert.Equal(date, fields["planned_start"].Value);
            Assert.True(WorkOrderPolicy.TryDate(date, out var parsed));
            Assert.Equal(date, parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        } finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("2026-02-29")]
    [InlineData("2026-11-08T00:00:00Z")]
    [InlineData("08.11.2026")]
    [InlineData("2026-1-08")]
    [InlineData("２０２６-１１-０８")]
    [InlineData("0000-01-01")]
    public void Invalid_or_ambiguous_calendar_dates_are_rejected(string date) =>
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { ["due_date"] = new("Known", date) }, out _, out _));

    [Theory]
    [InlineData("planned_start", "planned_finish")]
    [InlineData("actual_start", "actual_finish")]
    public void Known_reversed_ranges_fail_but_missing_and_unknown_finish_remain_draftable(string start, string finish)
    {
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> {
            [start] = new("Known", "2026-11-08"), [finish] = new("Known", "2026-11-07")
        }, out _, out var error));
        Assert.Equal("work_order_date_order_invalid", error);
        Assert.Equal("Unknown", Fields((start, "Known", "2026-11-08"), (finish, "Unknown", null))[finish].State);
        Assert.Equal("Missing", Fields((start, "Known", "2026-11-08"))[finish].State);
    }

    [Theory]
    [InlineData("-12.005")]
    [InlineData("0001.2300")]
    [InlineData("999999999999999999999999999999999999999999999999")]
    public void Numeric_reading_remains_opaque_without_rounding_unit_or_consumption_inference(string value)
    {
        var fields = Fields(("meter_before", "Known", value), ("meter_after", "Known", "-300"));
        Assert.Equal(value, fields["meter_before"].Value);
        Assert.Equal("-300", fields["meter_after"].Value);
        Assert.Contains("VERIFICATION_AND_CLOSURE_UNAVAILABLE", WorkOrderPolicy.Blockers(fields));
    }

    [Theory]
    [InlineData("1e3")]
    [InlineData("1,2")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("+1")]
    [InlineData(".2")]
    [InlineData("1.")]
    [InlineData("-")]
    [InlineData("0.1 hours")]
    public void Reading_transport_rejects_ambiguous_numbers_and_units(string value) =>
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { ["meter_before"] = new("Known", value) }, out _, out _));

    [Fact]
    public void Uncertain_facts_do_not_hide_values_and_payload_structure_is_strict()
    {
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { ["findings"] = new("Unknown", "hidden finding") }, out _, out _));
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { ["findings"] = new("Missing", "hidden finding") }, out _, out _));
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { ["findings"] = new("Known", " ") }, out _, out _));
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { ["work_order_no"] = new("Known", "A\nB") }, out _, out _));
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { ["meter_before"] = new("Known", new string('1', 49)) }, out _, out _));
        Assert.False(WorkOrderPolicy.TryNormalize(new Dictionary<string, WorkOrderFact> { ["description"] = new("Known", new string('x', 4001)) }, out _, out _));
        Assert.Throws<InvalidDataException>(() => WorkOrderPolicy.ReadFields("{\"findings\":{\"state\":\"Missing\",\"value\":null},\"findings\":{\"state\":\"Known\",\"value\":\"shadow\"}}"));
        Assert.Throws<InvalidDataException>(() => WorkOrderPolicy.ReadFields("{\"findings\":{\"state\":\"Unknown\",\"value\":null,\"actor\":\"forged\"}}"));
        Assert.Throws<InvalidDataException>(() => WorkOrderPolicy.ReadFields("{\"findings\":{\"State\":\"Unknown\",\"value\":null}}"));
        Assert.Throws<InvalidDataException>(() => WorkOrderPolicy.ReadFields("{\"findings\":{\"state\":\"Unknown\"}}"));
    }
}
