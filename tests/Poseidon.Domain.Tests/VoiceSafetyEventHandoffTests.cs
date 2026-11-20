using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S9 integration DEV-062 x DEV-060: a confirmed voice candidate maps onto a safety event DRAFT with only exact values;
// nothing is guessed (category, immediate-action YES/NO, time zone, severity) and nothing becomes submittable by itself.
public sealed class VoiceSafetyEventHandoffTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
    private static VoiceDraftFields Fields(string? type = "NEAR_MISS", string? at = "2026-11-19T09:15", string? detail = null) =>
        new(type, at, "MAIN_DECK", detail, "FICTIONAL mooring line whipped across the walkway.", "FICTIONAL area cordoned off.");

    [Fact]
    [Trait("Requirement", "DEV-062")]
    [Trait("Requirement", "DEV-060")]
    public void Exact_values_are_carried_and_unknowns_stay_empty_for_the_reporter() {
        var f = VoiceSafetyEventHandoff.Map(Fields(detail: "port side"), Now);
        Assert.True(SafetyEventPolicy.ValidDraft(f));
        Assert.Equal(("NEAR_MISS", "DATE_ONLY", (DateOnly?)new DateOnly(2026, 11, 19), (DateTimeOffset?)null), (f.Category, f.OccurrencePrecision, f.OccurrenceDate, f.OccurredAt));
        Assert.Equal("MAIN_DECK: port side", f.LocationText);
        Assert.Equal(("FICTIONAL mooring line whipped across the walkway.", "FICTIONAL area cordoned off."), (f.Narrative, f.ImmediateActionText));
        // Not inferred: immediate-action taken, operation context, summary, severity, time zone, evidence.
        Assert.Equal(((string?)null, (string?)null, (string?)null, (string?)null, (string?)null), (f.ImmediateActionTaken, f.OperationContext, f.Summary, f.SeverityClaim, f.TimeZone));
        Assert.Null(f.Evidence);
        var missing = SafetyEventPolicy.Missing(f);
        Assert.Contains("summary", missing); Assert.Contains("operation_context", missing); Assert.Contains("immediate_action_taken", missing);
    }

    [Theory]
    [Trait("Requirement", "DEV-062")]
    [InlineData("NEAR_MISS", "NEAR_MISS")]
    [InlineData("INCIDENT", "INCIDENT")]
    [InlineData("HAZARDOUS_SITUATION", null)]
    [InlineData("NON_CONFORMITY", null)]
    public void Only_exact_category_counterparts_are_mapped(string type, string? category) =>
        Assert.Equal(category, VoiceSafetyEventHandoff.Map(Fields(type), Now).Category);

    [Fact]
    [Trait("Requirement", "DEV-062")]
    public void A_future_or_unparsable_occurrence_is_left_empty_instead_of_failing_the_confirmation() {
        var future = VoiceSafetyEventHandoff.Map(Fields(at: "2026-12-30T09:15"), Now);
        Assert.Equal(((string?)null, (DateOnly?)null), (future.OccurrencePrecision, future.OccurrenceDate));
        Assert.False(SafetyEventPolicy.InFuture(future, Now));
        var none = VoiceSafetyEventHandoff.Map(Fields(at: "yesterday"), Now);
        Assert.Null(none.OccurrencePrecision); Assert.True(SafetyEventPolicy.ValidDraft(none));
    }

    [Fact]
    [Trait("Requirement", "DEV-062")]
    public void Handoff_state_is_created_only_with_a_link_and_never_before_confirmation() {
        var id = Guid.NewGuid();
        Assert.Equal(VoiceSafetyEventHandoff.Created, VoiceSafetyEventHandoff.State(VoiceEventDraftPolicy.Confirmed, id));
        Assert.Equal(VoiceSafetyEventHandoff.Pending, VoiceSafetyEventHandoff.State(VoiceEventDraftPolicy.Confirmed, null));
        Assert.Equal(VoiceSafetyEventHandoff.NotReady, VoiceSafetyEventHandoff.State(VoiceEventDraftPolicy.Draft, id));
        Assert.Equal(VoiceSafetyEventHandoff.NotReady, VoiceSafetyEventHandoff.State(VoiceEventDraftPolicy.Rejected, null));
    }
}
