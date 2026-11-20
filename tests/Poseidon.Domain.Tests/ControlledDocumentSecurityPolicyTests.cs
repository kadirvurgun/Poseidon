using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-114 / T-DOC2-001 (18.01 Backlog row 115; 58.02 Rev4 §33, paragraph 208, DE-005).
[Trait("Requirement", "DEV-114")]
public sealed class ControlledDocumentSecurityPolicyTests
{
    private const string Hash = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";

    [Theory]
    [InlineData("INTERNAL", false, "ALLOWED", "UNCONTROLLED_COPY_ONLY")]
    [InlineData("CONFIDENTIAL", false, "ALLOWED", "CONTROLLED_COPY_RENDERER_UNAVAILABLE")]
    [InlineData("RESTRICTED", false, "DENIED_RESTRICTED", "CONTROLLED_COPY_RENDERER_UNAVAILABLE")]
    [InlineData("RESTRICTED", true, "ALLOWED", "CONTROLLED_COPY_RENDERER_UNAVAILABLE")]
    [InlineData("UNKNOWN_CLASS", true, "DENIED_RESTRICTED", "CONTROLLED_COPY_RENDERER_UNAVAILABLE")]
    public void Download_and_print_follow_the_classification(string classification, bool permission, string download, string print)
    {
        var policy = ControlledDocumentSecurityPolicy.Content(classification, permission);
        Assert.Equal((download, print, true), (policy.Download, policy.Print, policy.AttributionRequired));
    }

    [Fact]
    public void Isps_documents_are_restricted_and_hidden_without_the_named_permission()
    {
        Assert.False(ControlledDocumentSecurityPolicy.Visible(true, false));
        Assert.True(ControlledDocumentSecurityPolicy.Visible(true, true));
        Assert.True(ControlledDocumentSecurityPolicy.Visible(false, false));
        Assert.False(ControlledDocumentSecurityPolicy.CreateAllowed(true, "CONFIDENTIAL", true));
        Assert.False(ControlledDocumentSecurityPolicy.CreateAllowed(true, "RESTRICTED", false));
        Assert.True(ControlledDocumentSecurityPolicy.CreateAllowed(true, "RESTRICTED", true));
    }

    [Fact]
    public void Content_needs_released_intake_and_an_undisposed_document()
    {
        Assert.Equal("intake_not_released", ControlledDocumentSecurityPolicy.ContentBlocker("INTERNAL", false, "QUARANTINED", "NONE"));
        Assert.Equal("document_disposed", ControlledDocumentSecurityPolicy.ContentBlocker("INTERNAL", false, "RELEASED", "APPROVED"));
        Assert.Equal("document_download_restricted", ControlledDocumentSecurityPolicy.ContentBlocker("RESTRICTED", false, "RELEASED", "NONE"));
        Assert.Null(ControlledDocumentSecurityPolicy.ContentBlocker("CONFIDENTIAL", false, "RELEASED", "NONE"));
    }

    [Fact]
    public void Acknowledgement_needs_the_effective_revision_its_exact_hash_and_accessible_content()
    {
        string? Block(string publication = "EFFECTIVE", string classification = "INTERNAL", bool permission = false, string? displayed = Hash, bool accepted = true, bool already = false) =>
            ControlledDocumentSecurityPolicy.AcknowledgeBlocker(publication, "RELEASED", "NONE", classification, permission, Hash, displayed, accepted, already);
        Assert.Null(Block());
        Assert.Null(Block(displayed: Hash.ToLowerInvariant()));
        Assert.Equal("invalid_request", Block(accepted: false));
        Assert.Equal("invalid_request", Block(displayed: "short"));
        Assert.Equal("acknowledgement_revision_stale", Block(publication: "SUPERSEDED"));
        Assert.Equal("acknowledgement_revision_stale", Block(publication: "UNPUBLISHED"));
        Assert.Equal("acknowledgement_revision_stale", Block(displayed: new string('0', 64)));
        Assert.Equal("content_access_required", Block(classification: "RESTRICTED"));
        Assert.Null(Block(classification: "RESTRICTED", permission: true));
        Assert.Equal("already_acknowledged", Block(already: true));
    }

    [Fact]
    public void Attribution_is_pseudonymous_and_marks_uncontrolled_copies()
    {
        var text = ControlledDocumentSecurityPolicy.Attribution("SSP-01", 3, Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), new DateTimeOffset(2026, 11, 20, 8, 5, 0, TimeSpan.Zero), "RESTRICTED");
        Assert.Equal("SSP-01 r3 · RESTRICTED · copy 11111111 · aaaaaaaa · 2026-11-20T08:05Z · UNCONTROLLED WHEN PRINTED", text);
    }
}
