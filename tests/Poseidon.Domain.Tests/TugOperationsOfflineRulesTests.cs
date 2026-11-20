using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S10 WP-B DEV-092 (T-TUG-013) material conflict rules and DEV-091 (T-TUG-012) history derivation.
public sealed class TugOperationsOfflineRulesTests
{
    [Theory]
    [Trait("Requirement", "DEV-092")]
    [InlineData(2, "H", 2, "H", "ISSUED", "")]
    [InlineData(2, "H", 3, "H2", "ISSUED", "briefing_revision,briefing_content")]
    [InlineData(2, "H", 2, "H", "DRAFT", "briefing_state")]
    [InlineData(2, "H", 2, "X", "ISSUED", "briefing_content")]
    public void Briefing_acknowledgement_binds_the_exact_issued_revision_and_hash(long revision, string hash, long current, string currentHash, string state, string expected) =>
        Assert.Equal(expected, string.Join(',', TugOperationsOfflineRules.BriefingConflict(revision, hash, current, currentHash, state)));

    [Theory]
    [Trait("Requirement", "DEV-092")]
    [InlineData("ACTIVE", 4, "ACTIVE", 6, "NOTE", "")]
    [InlineData("ACTIVE", 4, "ACTIVE", 6, "TOWLINE_RELEASED", "execution_sequence")]
    [InlineData("ACTIVE", 4, "ACTIVE", 4, "TOWLINE_CONNECTED", "")]
    [InlineData("ACTIVE", 4, "ABORTED", 5, "NOTE", "execution_state")]
    [InlineData("ACTIVE", 4, "SUSPENDED", 5, "TOWLINE_CONNECTED", "execution_state,execution_sequence")]
    public void Execution_observation_is_material_only_on_lifecycle_change_or_towline_order(string baseState, long baseSequence, string state, long sequence, string kind, string expected) =>
        Assert.Equal(expected, string.Join(',', TugOperationsOfflineRules.ExecutionConflict(baseState, baseSequence, state, sequence, kind)));

    [Fact]
    [Trait("Requirement", "DEV-092")]
    public void Daily_entry_conflicts_on_version_state_existing_record_or_taken_slot()
    {
        Assert.Empty(TugOperationsOfflineRules.DailyConflict(null, false, false, null, null));
        Assert.Equal(["daily_exists"], TugOperationsOfflineRules.DailyConflict(null, true, false, 3, "DRAFT"));
        Assert.Equal(["daily_slot_taken"], TugOperationsOfflineRules.DailyConflict(null, false, true, null, null));
        Assert.Empty(TugOperationsOfflineRules.DailyConflict(7, true, false, 7, "DRAFT"));
        Assert.Equal(["daily_version"], TugOperationsOfflineRules.DailyConflict(7, true, false, 8, "RETURNED"));
        Assert.Equal(["daily_version", "daily_state"], TugOperationsOfflineRules.DailyConflict(7, true, false, 9, "SUBMITTED"));
        Assert.Equal(["daily_exists"], TugOperationsOfflineRules.DailyConflict(7, false, false, null, null));
    }

    [Fact]
    [Trait("Requirement", "DEV-092")]
    public void Resolution_choices_never_offer_applying_a_statement_that_can_no_longer_be_true()
    {
        string[] both = [TugOperationsOfflineRules.KeepServer, TugOperationsOfflineRules.UseCaptured];
        Assert.Equal([TugOperationsOfflineRules.KeepServer], TugOperationsOfflineRules.Choices(TugOperationsOfflineRules.BriefingAck, ["briefing_revision"], "ISSUED"));
        Assert.Equal([TugOperationsOfflineRules.KeepServer], TugOperationsOfflineRules.Choices(TugOperationsOfflineRules.ExecutionEvent, ["execution_state"], "COMPLETED", "TOWLINE_RELEASED"));
        Assert.Equal(both, TugOperationsOfflineRules.Choices(TugOperationsOfflineRules.ExecutionEvent, ["execution_state"], "COMPLETED", "NOTE"));
        Assert.Equal(both, TugOperationsOfflineRules.Choices(TugOperationsOfflineRules.ExecutionEvent, ["execution_sequence"], "ACTIVE", "TOWLINE_RELEASED"));
        Assert.Equal(both, TugOperationsOfflineRules.Choices(TugOperationsOfflineRules.DailyEntry, ["daily_version"], "DRAFT"));
        Assert.Equal([TugOperationsOfflineRules.KeepServer], TugOperationsOfflineRules.Choices(TugOperationsOfflineRules.DailyEntry, ["daily_version", "daily_state"], "VERIFIED"));
        Assert.Equal([TugOperationsOfflineRules.KeepServer], TugOperationsOfflineRules.Choices(TugOperationsOfflineRules.DailyEntry, ["daily_slot_taken"], null));
    }

    [Fact]
    [Trait("Requirement", "DEV-092")]
    public void Master_release_readiness_and_execution_lifecycle_are_never_offline_operations()
    {
        foreach (var online in new[] { "MASTER_RELEASE", "READINESS_DECISION", "EXECUTION_START", "EXECUTION_TRANSITION", "EXECUTION_VERIFICATION", "DAILY_LOG_REVIEW" })
            Assert.Contains(online, TugOperationsOfflineRules.OnlineOnly);
        Assert.Equal(["tug.briefing.ack.v1", "tug.execution.event.v1", "tug.daily.entry.v1"], TugOperationsOfflineRules.CaptureOperations);
        Assert.DoesNotContain(TugOperationsOfflineRules.Snapshot, TugOperationsOfflineRules.CaptureOperations);
    }

    [Fact]
    [Trait("Requirement", "DEV-092")]
    public void Capture_time_must_fall_inside_the_snapshot_lease_and_not_in_the_future()
    {
        var issued = new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.Zero); var expires = issued.AddHours(4); var now = issued.AddHours(6);
        Assert.True(TugOperationsOfflineRules.ValidCapture(issued.AddHours(1), 180, issued, expires, now));
        Assert.False(TugOperationsOfflineRules.ValidCapture(expires, 0, issued, expires, now));
        Assert.False(TugOperationsOfflineRules.ValidCapture(issued.AddHours(-1), 0, issued, expires, now));
        Assert.False(TugOperationsOfflineRules.ValidCapture(issued.AddHours(1), 900, issued, expires, now));
        Assert.False(TugOperationsOfflineRules.ValidCapture(new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.FromHours(3)), 0, issued, expires, now));
        Assert.False(TugOperationsOfflineRules.ValidCapture(issued.AddHours(2), 0, issued, expires, issued.AddHours(1)));
    }

    [Theory]
    [Trait("Requirement", "DEV-091")]
    [InlineData("Cancelled", new[] { "COMPLETED" }, "CANCELLED")]
    [InlineData("Submitted", new string[0], "NOT_STARTED")]
    [InlineData("Submitted", new[] { "COMPLETED", "ACTIVE" }, "IN_PROGRESS")]
    [InlineData("Submitted", new[] { "COMPLETED", "COMPLETED" }, "COMPLETED")]
    [InlineData("Submitted", new[] { "ABORTED" }, "ABORTED")]
    [InlineData("Submitted", new[] { "ABORTED", "COMPLETED" }, "MIXED")]
    public void History_outcome_is_derived_from_recorded_states_only(string job, string[] executions, string expected) =>
        Assert.Equal(expected, TugOperationHistoryPolicy.Outcome(job, executions));

    [Fact]
    [Trait("Requirement", "DEV-091")]
    public void Evidence_state_is_unknown_without_records_and_never_verified_without_a_verification()
    {
        Assert.Equal("UNKNOWN", TugOperationHistoryPolicy.Evidence(0, []));
        Assert.Equal("UNVERIFIED_EVIDENCE", TugOperationHistoryPolicy.Evidence(2, ["PENDING"]));
        Assert.Equal("VERIFICATION_REJECTED", TugOperationHistoryPolicy.Evidence(2, ["VERIFIED", "REJECTED"]));
        Assert.Equal("VERIFIED_WITH_EVIDENCE", TugOperationHistoryPolicy.Evidence(1, ["VERIFIED"]));
        Assert.Equal("VERIFIED_NO_EVIDENCE", TugOperationHistoryPolicy.Evidence(0, ["VERIFIED"]));
        Assert.Null(TugOperationHistoryPolicy.DurationMinutes(DateTimeOffset.UnixEpoch, null));
        Assert.Equal(90, TugOperationHistoryPolicy.DurationMinutes(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(90.5)));
    }

    [Fact]
    [Trait("Requirement", "DEV-091")]
    public void Keyset_cursor_round_trips_and_refuses_malformed_input()
    {
        var at = new DateTimeOffset(2031, 5, 6, 7, 8, 9, TimeSpan.Zero).AddTicks(1234); var id = Guid.NewGuid();
        Assert.True(TugOperationHistoryPolicy.TryCursor(TugOperationHistoryPolicy.Cursor(at, id), out var back, out var backId));
        Assert.Equal((at, id), (back, backId));
        foreach (var bad in new[] { "", "!!", "AAAA", new string('A', 97), TugOperationHistoryPolicy.Cursor(at, Guid.Empty) })
            Assert.False(TugOperationHistoryPolicy.TryCursor(bad, out _, out _));
    }
}
