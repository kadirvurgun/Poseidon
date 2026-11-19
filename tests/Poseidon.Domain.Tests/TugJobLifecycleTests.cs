using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

// DEV-081 / T-TUG-002, 60.02 §5 paragraphs 81..92, POL-DEFAULT-01.
public sealed class TugJobLifecycleTests
{
    private static readonly TugRequestInputs Complete = new(RequestSource: "Customer", AssistedVessel: "FICTIONAL vessel", JobType: "Escort",
        RequestedStart: new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero), OperationArea: "FICTIONAL-AREA", RequiredTugCount: 2,
        Priority: "Normal", RequestOwner: Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535"));

    [Fact]
    public void Incomplete_draft_cannot_be_submitted_and_reports_every_missing_required_key() {
        var result = TugJobLifecycle.Evaluate(TugJobLifecycle.Draft, TugJobCommand.Submit, new TugRequestInputs());
        Assert.Null(result.ToState); Assert.Equal("tug_request_incomplete", result.Failure);
        Assert.Equal(TugRequestPolicy.Missing(new TugRequestInputs()), result.Missing);
        Assert.Equal(8, result.Missing.Length);
    }

    [Theory]
    [InlineData("requested_start")] [InlineData("required_tug_count")] [InlineData("priority")] [InlineData("request_owner")]
    public void Unknown_required_value_is_never_defaulted_on_submit(string key) {
        var inputs = key switch {
            "requested_start" => Complete with { RequestedStart = null }, "required_tug_count" => Complete with { RequiredTugCount = null },
            "priority" => Complete with { Priority = null }, _ => Complete with { RequestOwner = null } };
        var result = TugJobLifecycle.Evaluate(TugJobLifecycle.Draft, TugJobCommand.Submit, inputs);
        Assert.Equal([key], result.Missing); Assert.Null(result.ToState);
    }

    [Fact]
    public void Complete_draft_submits_and_conditional_end_is_not_required() {
        Assert.Null(Complete.RequestedEnd);
        var result = TugJobLifecycle.Evaluate(TugJobLifecycle.Draft, TugJobCommand.Submit, Complete);
        Assert.Equal(TugJobLifecycle.Submitted, result.ToState); Assert.Null(result.Failure); Assert.Empty(result.Missing);
    }

    [Fact]
    public void Invalid_inputs_fail_validation_before_completeness() {
        var result = TugJobLifecycle.Evaluate(TugJobLifecycle.Draft, TugJobCommand.Submit, Complete with { Priority = "Ready" });
        Assert.Equal("validation_failed", result.Failure);
    }

    [Theory]
    [InlineData("Submitted", "Submit")] [InlineData("Cancelled", "Submit")] [InlineData("Cancelled", "Cancel")]
    [InlineData("Ready", "Submit")] [InlineData("Planned", "Cancel")]
    public void Terminal_unknown_or_repeated_transitions_are_refused(string state, string command) {
        var result = TugJobLifecycle.Evaluate(state, Enum.Parse<TugJobCommand>(command), Complete, "FICTIONAL reason");
        Assert.Equal("invalid_state", result.Failure); Assert.Null(result.ToState);
    }

    [Theory]
    [InlineData("Draft")] [InlineData("Submitted")]
    public void Draft_or_submitted_request_cancels_with_bounded_reason(string state) {
        Assert.Equal(TugJobLifecycle.Cancelled, TugJobLifecycle.Evaluate(state, TugJobCommand.Cancel, new TugRequestInputs(), "FICTIONAL customer withdrew\nline two").ToState);
        foreach (var reason in new string?[] { null, "", "   ", " padded", "bell\u0007", new string('x', TugJobLifecycle.ReasonMax + 1) })
            Assert.Equal("validation_failed", TugJobLifecycle.Evaluate(state, TugJobCommand.Cancel, new TugRequestInputs(), reason).Failure);
    }

    [Fact]
    public void Request_instants_are_canonicalized_to_postgres_microsecond_precision_without_rounding() {
        var t = new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero).AddTicks(1234569);
        Assert.Equal(t.AddTicks(-9), TugRequestPolicy.StoragePrecision(t));
        Assert.Equal(t.AddTicks(-9), TugRequestPolicy.StoragePrecision(t.AddTicks(-9)));
        Assert.Null(TugRequestPolicy.StoragePrecision(null));
    }

    [Fact]
    public void Only_draft_accepts_field_edits() {
        Assert.True(TugJobLifecycle.Editable(TugJobLifecycle.Draft));
        Assert.False(TugJobLifecycle.Editable(TugJobLifecycle.Submitted)); Assert.False(TugJobLifecycle.Editable(TugJobLifecycle.Cancelled));
    }
}
