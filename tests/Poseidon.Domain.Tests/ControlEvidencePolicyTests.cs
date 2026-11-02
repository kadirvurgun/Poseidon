using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class ControlEvidencePolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-02T09:00:00Z");
    [Fact]
    public void Partial_drafts_keep_unknown_criticality_and_block_submission() {
        var draft = new ControlEvidenceContent(); ControlEvidencePolicy.Validate("Control", draft, Now);
        var blockers = ControlEvidencePolicy.Blockers("Control", draft, 0, false);
        Assert.Contains("CRITICALITY_UNKNOWN", blockers); Assert.Contains("OWNER_MISSING", blockers); Assert.Contains("FREQUENCY_UNSPECIFIED", blockers);
        Assert.Contains("SOURCE_ADMISSION_UNAVAILABLE", blockers); Assert.Null(draft.Critical);
    }
    [Fact]
    public void Populated_declarations_never_become_verified_or_effective() {
        var draft = new ControlEvidenceContent("Fictional", Guid.NewGuid(), Guid.NewGuid(), "Operator declaration", "Declared proof", "Human review", false);
        var blockers = ControlEvidencePolicy.Blockers("Control", draft, 2, true, 2);
        Assert.Equal(new[] { "SOURCE_ADMISSION_UNAVAILABLE", "VERIFICATION_NOT_PERFORMED" }, blockers);
        Assert.Equal("UNKNOWN", ControlEvidencePolicy.TimeState(draft, Now, false));
    }
    [Theory]
    [InlineData(-1, "DECLARED_EXPIRED")]
    [InlineData(0, "UNKNOWN")]
    [InlineData(1, "UNKNOWN")]
    public void Expiry_boundary_preserves_declared_policy_and_no_ready_default(int seconds, string expected) {
        Assert.Equal(expected, ControlEvidencePolicy.TimeState(new(ValidUntil: Now.AddSeconds(seconds)), Now, false));
    }
    [Fact]
    public void Replay_uses_supplied_instant_and_keeps_supersession_separate() {
        var content = new ControlEvidenceContent(ValidFrom: Now.AddDays(-3), ValidUntil: Now.AddDays(2), FreshUntil: Now.AddDays(-1));
        Assert.Equal("UNKNOWN", ControlEvidencePolicy.TimeState(content, Now.AddDays(-2), false));
        Assert.Equal("DECLARED_STALE", ControlEvidencePolicy.TimeState(content, Now, false));
        Assert.Equal("SUPERSEDED", ControlEvidencePolicy.TimeState(content, Now, true));
        Assert.Equal("DECLARED_NOT_YET_VALID", ControlEvidencePolicy.TimeState(content, Now.AddDays(-4), false));
    }
    [Fact]
    public void Future_observation_and_reversed_validity_are_rejected() {
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Evidence", new(ObservedAt: Now.AddTicks(1)), Now));
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Evidence", new(ValidFrom: Now, ValidUntil: Now.AddSeconds(-1)), Now));
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Evidence", new(ObservedAt: Now, FreshUntil: Now.AddSeconds(-1)), Now));
    }
    [Fact]
    public void Owner_requires_member_and_role_and_control_has_no_observation_clock() {
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Control", new(OwnerId: Guid.NewGuid()), Now));
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Control", new(OwnerId: Guid.Empty, OwnerRole: Guid.NewGuid()), Now));
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Control", new(ObservedAt: Now), Now));
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Control", new(Title: "unsafe\ntext"), Now));
    }
    [Fact]
    public void Empty_metadata_is_explicitly_missing_clean_file_and_observation() {
        var blockers = ControlEvidencePolicy.Blockers("Evidence", new(), 0, false);
        Assert.Contains("CLEAN_FILE_MISSING", blockers); Assert.Contains("OBSERVATION_TIME_MISSING", blockers); Assert.Contains("PROVENANCE_MISSING", blockers);
    }
}
