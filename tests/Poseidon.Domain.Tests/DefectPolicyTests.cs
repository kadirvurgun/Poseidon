using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class DefectPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-06T00:00:00Z");
    private static SortedDictionary<string, DefectFact> Fields(params (string Key, string State, string? Value)[] values)
    {
        Assert.True(DefectPolicy.TryNormalize(values.ToDictionary(x => x.Key, x => new DefectFact(x.State, x.Value)), out var result, out _));
        return result;
    }

    [Fact]
    public void Incomplete_capture_preserves_unknown_and_does_not_create_a_safeguard_or_closure()
    {
        var fields = Fields(("criticality", "Unknown", null));
        Assert.Equal(11, fields.Count);
        Assert.Equal("Unknown", fields["criticality"].State);
        Assert.Equal("Missing", fields["safeguard"].State);
        var assessment = DefectPolicy.Assess(fields, Now);
        Assert.Equal("MEASURE_MISSING", assessment.TemporaryMeasureState);
        Assert.False(assessment.ClosureAvailable);
        Assert.Contains("SAFEGUARD_NOT_VERIFIED", DefectPolicy.Blockers(fields));
    }

    [Theory]
    [InlineData("2026-11-06T02:59:59+03:00", "EXPIRED_DECLARED")]
    [InlineData("2026-11-06T03:00:00+03:00", "EXPIRED_DECLARED")]
    [InlineData("2026-11-06T03:00:01+03:00", "NOT_DUE_DECLARED")]
    public void Deadline_observation_uses_the_actual_instant_and_never_verifies_the_measure(string expiry, string expected)
    {
        var fields = Fields(("temporary_measure", "Known", "Declared temporary arrangement"), ("temporary_measure_expires_at", "Known", expiry));
        var assessment = DefectPolicy.Assess(fields, Now.ToOffset(TimeSpan.FromHours(3)));
        Assert.Equal(expected, assessment.TemporaryMeasureState);
        Assert.Equal(TimeSpan.Zero, assessment.AssessedAt.Offset);
        Assert.False(assessment.ClosureAvailable);
        Assert.Equal(expiry, fields["temporary_measure_expires_at"].Value);
    }

    [Fact]
    public void Missing_or_unknown_deadline_never_becomes_a_clear_or_effective_measure()
    {
        var missing = Fields(("temporary_measure", "Known", "Declared measure"));
        var unknown = Fields(("temporary_measure", "Known", "Declared measure"), ("temporary_measure_expires_at", "Unknown", null));
        Assert.Equal("DEADLINE_MISSING", DefectPolicy.Assess(missing, Now).TemporaryMeasureState);
        Assert.Equal("DEADLINE_UNKNOWN", DefectPolicy.Assess(unknown, Now).TemporaryMeasureState);
        Assert.Equal("MEASURE_UNKNOWN", DefectPolicy.Assess(Fields(("temporary_measure", "Unknown", null)), Now).TemporaryMeasureState);
    }

    [Theory]
    [InlineData("lifecycle_stage", "Closed")]
    [InlineData("lifecycle_stage", "Verified")]
    [InlineData("criticality", "Unknown")]
    [InlineData("record_datetime", "2026-11-06T10:00:00")]
    [InlineData("temporary_measure_expires_at", "2026-11-06T10:00:00-00:00")]
    [InlineData("verifier", "approved")]
    public void Unapproved_decisions_defaults_and_ambiguous_instants_are_rejected(string key, string value) =>
        Assert.False(DefectPolicy.TryNormalize(new Dictionary<string, DefectFact> { [key] = new("Known", value) }, out _, out _));

    [Fact]
    public void Literal_verify_declaration_does_not_remove_human_or_effectiveness_gates()
    {
        var fields = Fields(("lifecycle_stage", "Known", "Verify"), ("safeguard", "Known", "Human declaration only"));
        Assert.Contains("HUMAN_VERIFICATION_NOT_RECORDED", DefectPolicy.Blockers(fields));
        Assert.Contains("EFFECTIVENESS_NOT_ASSESSED", DefectPolicy.Blockers(fields));
        Assert.False(DefectPolicy.Assess(fields, Now).ClosureAvailable);
        var json = JsonSerializer.Serialize(fields, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(fields.ToArray(), DefectPolicy.ReadFields(json).ToArray());
    }

    [Fact]
    public void Uncertain_values_cannot_carry_hidden_text_and_stored_duplicate_keys_fail_closed()
    {
        Assert.False(DefectPolicy.TryNormalize(new Dictionary<string, DefectFact> { ["restriction"] = new("Unknown", "hidden restriction") }, out _, out _));
        Assert.Throws<InvalidDataException>(() => DefectPolicy.ReadFields("{\"notes\":{\"state\":\"Missing\",\"value\":null},\"notes\":{\"state\":\"Known\",\"value\":\"shadow\"}}"));
    }
}
