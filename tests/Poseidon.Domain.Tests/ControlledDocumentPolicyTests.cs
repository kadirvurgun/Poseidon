using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// PLAN-1511-5802 P5802-AC-01..06: state separation, SoD, hold vs disposal (58.02 DE-003, DE-007..DE-009).
[Trait("Category", "Unit")]
[Trait("Requirement", "PLAN-1511-5802")]
public sealed class ControlledDocumentPolicyTests
{
    private static readonly Guid Uploader = Guid.NewGuid(), Registrar = Guid.NewGuid(), Other = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public void Upload_or_registration_never_yields_verified_state_and_scanner_release_is_not_verification()
    {
        Assert.Equal("UNVERIFIED", ControlledDocumentPolicy.Verification(null));
        Assert.Equal("VALID", ControlledDocumentPolicy.Verification("VALID"));
        Assert.Equal("INVALID", ControlledDocumentPolicy.Verification("INVALID"));
        Assert.Equal("INCONCLUSIVE", ControlledDocumentPolicy.Verification("INCONCLUSIVE"));
        Assert.DoesNotContain("VERIFIED", ControlledDocumentPolicy.IntakeStates);
        Assert.DoesNotContain("VERIFIED", ControlledDocumentPolicy.PublicationStates);
        // Quarantined content can be neither published nor verified.
        Assert.Equal("intake_not_released", ControlledDocumentPolicy.PublishBlocker("QUARANTINED", "UNPUBLISHED", "NONE", Other, Uploader, Registrar));
        Assert.Equal("intake_not_released", ControlledDocumentPolicy.VerifyBlocker("QUARANTINED", "NONE", Other, Uploader, Registrar, "MANUAL_SOURCE_COMPARISON", "VALID", "basis"));
    }

    [Fact]
    public void Release_requires_matching_clean_bytes_and_is_one_way()
    {
        Assert.Equal("clean_file_unavailable", ControlledDocumentPolicy.ReleaseBlocker("QUARANTINED", "NONE", false));
        Assert.Null(ControlledDocumentPolicy.ReleaseBlocker("QUARANTINED", "NONE", true));
        Assert.Equal("invalid_state", ControlledDocumentPolicy.ReleaseBlocker("RELEASED", "NONE", true));
        Assert.Equal("document_disposed", ControlledDocumentPolicy.ReleaseBlocker("QUARANTINED", "APPROVED", true));
    }

    [Theory]
    [InlineData("uploader")]
    [InlineData("registrar")]
    public void Self_verification_and_self_publication_are_separation_of_duties_violations(string who)
    {
        var self = who == "uploader" ? Uploader : Registrar;
        Assert.Equal("separation_of_duties", ControlledDocumentPolicy.VerifyBlocker("RELEASED", "NONE", self, Uploader, Registrar, "MANUAL_SOURCE_COMPARISON", "VALID", "Compared with issuing authority register"));
        Assert.Equal("separation_of_duties", ControlledDocumentPolicy.PublishBlocker("RELEASED", "UNPUBLISHED", "NONE", self, Uploader, Registrar));
        Assert.Null(ControlledDocumentPolicy.VerifyBlocker("RELEASED", "NONE", Other, Uploader, Registrar, "MANUAL_SOURCE_COMPARISON", "VALID", "Compared with issuing authority register"));
        Assert.Null(ControlledDocumentPolicy.PublishBlocker("RELEASED", "UNPUBLISHED", "NONE", Other, Uploader, Registrar));
        Assert.False(ControlledDocumentPolicy.Independent(Guid.Empty, Uploader, Registrar));
    }

    [Theory]
    [InlineData(null, "VALID", "basis")]
    [InlineData("SIGNATURE_VALIDATION", "VALID", "basis")]
    [InlineData("MANUAL_SOURCE_COMPARISON", "APPROVED", "basis")]
    [InlineData("MANUAL_SOURCE_COMPARISON", "VALID", "")]
    [InlineData("MANUAL_SOURCE_COMPARISON", "VALID", "   ")]
    [InlineData("MANUAL_SOURCE_COMPARISON", "VALID", "<script>")]
    public void Verification_requires_a_closed_method_result_and_a_recorded_basis(string? method, string? result, string? basis) =>
        Assert.Equal("invalid_request", ControlledDocumentPolicy.VerifyBlocker("RELEASED", "NONE", Other, Uploader, Registrar, method, result, basis));

    [Fact]
    public void Publication_is_independent_of_verification_and_never_republishes()
    {
        Assert.Null(ControlledDocumentPolicy.PublishBlocker("RELEASED", "UNPUBLISHED", "NONE", Other, Uploader, Registrar));
        Assert.Equal("invalid_state", ControlledDocumentPolicy.PublishBlocker("RELEASED", "EFFECTIVE", "NONE", Other, Uploader, Registrar));
        Assert.Equal("invalid_state", ControlledDocumentPolicy.PublishBlocker("RELEASED", "SUPERSEDED", "NONE", Other, Uploader, Registrar));
        Assert.Equal("document_disposal_pending", ControlledDocumentPolicy.PublishBlocker("RELEASED", "UNPUBLISHED", "REQUESTED", Other, Uploader, Registrar));
    }

    [Fact]
    public void Retention_is_never_invented_and_unconfigured_retention_closes_disposal()
    {
        Assert.Equal("UNCONFIGURED", ControlledDocumentPolicy.Retention(null, null, Today));
        Assert.Equal("UNCONFIGURED", ControlledDocumentPolicy.Retention("POLICY-1", null, Today));
        Assert.Equal("RETAINING", ControlledDocumentPolicy.Retention("POLICY-1", Today.AddDays(1), Today));
        Assert.Equal("ELIGIBLE", ControlledDocumentPolicy.Retention("POLICY-1", Today, Today));
        Assert.Equal("retention_unconfigured", ControlledDocumentPolicy.DisposalRequestBlocker(false, null, null, Today, "NONE", "end of retention"));
        Assert.Equal("retention_not_elapsed", ControlledDocumentPolicy.DisposalRequestBlocker(false, "POLICY-1", Today.AddDays(1), Today, "NONE", "end of retention"));
        Assert.Equal("invalid_request", ControlledDocumentPolicy.RetentionBlocker("NONE", "bad ref with spaces", Today));
        Assert.Equal("document_disposal_pending", ControlledDocumentPolicy.RetentionBlocker("REQUESTED", "POLICY-1", Today));
    }

    [Fact]
    public void Legal_hold_blocks_disposal_request_and_approval_even_after_retention_elapsed()
    {
        Assert.Equal("legal_hold_active", ControlledDocumentPolicy.DisposalRequestBlocker(true, "POLICY-1", Today.AddDays(-1), Today, "NONE", "end of retention"));
        Assert.Null(ControlledDocumentPolicy.DisposalRequestBlocker(false, "POLICY-1", Today.AddDays(-1), Today, "NONE", "end of retention"));
        // Hold placed between request and approval still blocks the approval.
        Assert.Equal("legal_hold_active", ControlledDocumentPolicy.DisposalApproveBlocker(true, "POLICY-1", Today.AddDays(-1), Today, "REQUESTED", Other, Registrar, "approve"));
    }

    [Fact]
    public void Disposal_approval_and_hold_release_need_a_different_actor()
    {
        Assert.Equal("separation_of_duties", ControlledDocumentPolicy.DisposalApproveBlocker(false, "POLICY-1", Today.AddDays(-1), Today, "REQUESTED", Other, Other, "approve"));
        Assert.Null(ControlledDocumentPolicy.DisposalApproveBlocker(false, "POLICY-1", Today.AddDays(-1), Today, "REQUESTED", Other, Registrar, "approve"));
        Assert.Equal("invalid_state", ControlledDocumentPolicy.DisposalApproveBlocker(false, "POLICY-1", Today.AddDays(-1), Today, "NONE", null, Registrar, "approve"));
        Assert.Equal("separation_of_duties", ControlledDocumentPolicy.ReleaseHoldBlocker(true, Other, Other, "case closed"));
        Assert.Null(ControlledDocumentPolicy.ReleaseHoldBlocker(true, Other, Registrar, "case closed"));
        Assert.Equal("legal_hold_not_active", ControlledDocumentPolicy.ReleaseHoldBlocker(false, Other, Registrar, "case closed"));
        Assert.Equal("invalid_state", ControlledDocumentPolicy.PlaceHoldBlocker(true, "AUTH-1", "litigation"));
        Assert.Equal("invalid_request", ControlledDocumentPolicy.PlaceHoldBlocker(false, "", "litigation"));
    }
}
