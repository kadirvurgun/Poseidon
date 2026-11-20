namespace Poseidon.Domain;

// DEV-114 / T-DOC2-001 (18.01 Backlog row 115; 58.02 Rev4 §33 CMP-001..T20, paragraph 208 controlled
// print, DE-005 acknowledgement). Side-by-side compare is a read-only user view: no new publish or
// approval command, no AI/OCR diff and a hash match is never a content review. Content (bytes) access
// follows the classification; ISPS-sensitive documents (e.g. the Ship Security Plan, ISPS Code A/9.7
// "protected from unauthorized access or disclosure") require a named permission even to be listed.
// No absolute DRM is claimed: an authorized download can still be copied outside the system.
public static class ControlledDocumentSecurityPolicy
{
    public const string IspsReadPermission = "document.isps.read";
    public const string RestrictedContentPermission = "document.restricted.download";
    public const string AcknowledgePermission = "document.acknowledge";
    public static readonly string[] Purposes = ["VIEW", "DOWNLOAD"];
    public static readonly string[] Bases = ["INTERNAL_DEFAULT", "CONFIDENTIAL_ATTRIBUTED", "RESTRICTED_EXPLICIT_PERMISSION"];
    public const string AcknowledgementStatement = "READ_AND_ACCESSED_V1";

    public sealed record ContentPolicy(string Classification, string Download, string Print, string Basis, bool AttributionRequired);

    // Download: ALLOWED | DENIED_RESTRICTED (needs explicit permission). Print: browser print of an
    // authorized copy is an UNCONTROLLED copy; a server-stamped controlled copy renderer is not configured.
    public static ContentPolicy Content(string classification, bool restrictedPermission) => classification switch
    {
        "INTERNAL" => new("INTERNAL", "ALLOWED", "UNCONTROLLED_COPY_ONLY", "INTERNAL_DEFAULT", true),
        "CONFIDENTIAL" => new("CONFIDENTIAL", "ALLOWED", "CONTROLLED_COPY_RENDERER_UNAVAILABLE", "CONFIDENTIAL_ATTRIBUTED", true),
        "RESTRICTED" => new("RESTRICTED", restrictedPermission ? "ALLOWED" : "DENIED_RESTRICTED", "CONTROLLED_COPY_RENDERER_UNAVAILABLE",
            "RESTRICTED_EXPLICIT_PERMISSION", true),
        _ => new(classification, "DENIED_RESTRICTED", "CONTROLLED_COPY_RENDERER_UNAVAILABLE", "RESTRICTED_EXPLICIT_PERMISSION", true)
    };

    public static bool Visible(bool ispsRestricted, bool ispsPermission) => !ispsRestricted || ispsPermission;

    // An ISPS-sensitive document is always RESTRICTED (it cannot be created at a lower class).
    public static bool CreateAllowed(bool ispsRestricted, string? classification, bool ispsPermission) =>
        !ispsRestricted || classification == "RESTRICTED" && ispsPermission;

    // Content access: released (CLEAN) intake, not disposed, and the classification allows the actor.
    public static string? ContentBlocker(string classification, bool restrictedPermission, string intake, string disposal) =>
        disposal == "APPROVED" ? "document_disposed" : intake != "RELEASED" ? "intake_not_released" :
        Content(classification, restrictedPermission).Download != "ALLOWED" ? "document_download_restricted" : null;

    // DE-005: acknowledgement only for the EFFECTIVE, accessible revision whose exact displayed content
    // hash the reader saw; opening/downloading never creates one; nobody acknowledges for someone else.
    public static string? AcknowledgeBlocker(string publication, string intake, string disposal, string classification, bool restrictedPermission,
        string revisionSha256, string? displayedSha256, bool statementAccepted, bool alreadyAcknowledged) =>
        !statementAccepted || displayedSha256 is not { Length: 64 } || !displayedSha256.All(char.IsAsciiHexDigit) ? "invalid_request" :
        publication != "EFFECTIVE" ? "acknowledgement_revision_stale" :
        !string.Equals(revisionSha256, displayedSha256, StringComparison.OrdinalIgnoreCase) ? "acknowledgement_revision_stale" :
        ContentBlocker(classification, restrictedPermission, intake, disposal) is { } blocked ? blocked == "document_download_restricted" ? "content_access_required" : blocked :
        alreadyAcknowledged ? "already_acknowledged" : null;

    // Attribution carried by the access record and shown as an on-screen watermark. Pseudonymous
    // (record/actor id prefixes), never a name or e-mail.
    public static string Attribution(string code, long revision, Guid recordId, Guid actorId, DateTimeOffset issuedAt, string classification) =>
        $"{code} r{revision} · {classification} · copy {recordId.ToString("N")[..8]} · {actorId.ToString("N")[..8]} · {issuedAt.UtcDateTime:yyyy-MM-dd'T'HH:mm'Z'} · UNCONTROLLED WHEN PRINTED";
}
