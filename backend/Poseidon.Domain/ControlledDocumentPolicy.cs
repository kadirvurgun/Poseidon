namespace Poseidon.Domain;

// PLAN-1511-5802 / 58.02 Rev4 (DE-003, DE-006..DE-009). Each lifecycle is a separate
// dimension: intake (quarantine/scan release), publication, independent verification,
// legal hold and disposal never imply one another. An upload is never verified evidence,
// a scanner CLEAN result is never a human verification and publication is neither.
public static class ControlledDocumentPolicy
{
    public static readonly string[] Classifications = ["INTERNAL", "CONFIDENTIAL", "RESTRICTED"];
    public static readonly string[] IntakeStates = ["QUARANTINED", "RELEASED"];
    public static readonly string[] PublicationStates = ["UNPUBLISHED", "EFFECTIVE", "SUPERSEDED"];
    public static readonly string[] DisposalStates = ["NONE", "REQUESTED", "APPROVED"];
    // SIGNATURE_VALIDATION needs an accepted trust profile/validation service (58.02 §9) and is not offered.
    public static readonly string[] VerificationMethods = ["EVIDENCE_IDENTITY_CHECK", "AUTHORITY_RESPONSE", "MANUAL_SOURCE_COMPARISON"];
    public static readonly string[] VerificationResults = ["VALID", "INVALID", "INCONCLUSIVE"];
    public static readonly string[] LinkKinds = ["VesselCertificate", "ControlEvidence", "Credential"];
    public static readonly string[] EventTypes = ["CREATED", "RECEIVED", "QUARANTINED", "SCANNED_CLEAN", "RELEASED", "PUBLISHED", "SUPERSEDED",
        "VERIFICATION_RECORDED", "HOLD_PLACED", "HOLD_RELEASED", "RETENTION_SET", "DISPOSAL_REQUESTED", "DISPOSAL_APPROVED", "LINKED",
        "DISPOSAL_REQUEST_INVALIDATED"];

    public static bool Text(string? value, int max, bool required = true) => value == null ? !required :
        value.Length <= max && (!required || value.Trim().Length > 0) && !value.Any(c => char.IsControl(c) && c is not ('\n' or '\r')) &&
        value.IndexOfAny(['<', '>']) < 0 && value.IsNormalized(System.Text.NormalizationForm.FormC);
    public static bool Code(string? value) => value is { Length: > 0 and <= 64 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    // DE-008: no policy => UNCONFIGURED and disposal closed; retention is not invented.
    public static string Retention(string? policyRef, DateOnly? retainUntil, DateOnly today) =>
        policyRef == null || retainUntil == null ? "UNCONFIGURED" : retainUntil > today ? "RETAINING" : "ELIGIBLE";

    // Verification state is derived from the latest human record only; no record => UNVERIFIED.
    public static string Verification(string? latestResult) => latestResult switch
    {
        null => "UNVERIFIED", "VALID" => "VALID", "INVALID" => "INVALID", _ => "INCONCLUSIVE"
    };

    // Independent actor: different from the uploader and from the registering author (DE actor matrix).
    public static bool Independent(Guid actor, Guid uploader, Guid registrar) => actor != Guid.Empty && actor != uploader && actor != registrar;

    public static string? RegisterBlocker(string disposal) => disposal == "NONE" ? null : "document_disposal_pending";

    public static string? ReleaseBlocker(string intake, string disposal, bool cleanFileMatches) =>
        disposal == "APPROVED" ? "document_disposed" : intake != "QUARANTINED" ? "invalid_state" : !cleanFileMatches ? "clean_file_unavailable" : null;

    // LEAD decision (QA F5): a revision older than the current EFFECTIVE one is never published
    // (no rollback publish); a rollback is a new revision of the older content.
    public static string? PublishBlocker(string intake, string publication, string disposal, Guid actor, Guid uploader, Guid registrar,
        long revision = 0, long? currentEffective = null) =>
        disposal != "NONE" ? "document_disposal_pending" : intake != "RELEASED" ? "intake_not_released" :
        publication != "UNPUBLISHED" ? "invalid_state" : currentEffective is long current && revision < current ? "revision_older_than_effective" :
        !Independent(actor, uploader, registrar) ? "separation_of_duties" : null;

    public static string? VerifyBlocker(string intake, string disposal, Guid verifier, Guid uploader, Guid registrar,
        string? method, string? result, string? basis) =>
        method == null || !VerificationMethods.Contains(method) || result == null || !VerificationResults.Contains(result) || !Text(basis, 1000) ? "invalid_request" :
        disposal == "APPROVED" ? "document_disposed" : intake != "RELEASED" ? "intake_not_released" :
        !Independent(verifier, uploader, registrar) ? "separation_of_duties" : null;

    public static string? PlaceHoldBlocker(bool holdActive, string? authorityRef, string? reason) =>
        !Text(authorityRef, 200) || !Text(reason, 1000) ? "invalid_request" : holdActive ? "invalid_state" : null;

    // Release by an actor different from the placer; expiry/time never releases a hold.
    public static string? ReleaseHoldBlocker(bool holdActive, Guid? placer, Guid actor, string? reason) =>
        !Text(reason, 1000) ? "invalid_request" : !holdActive ? "legal_hold_not_active" : placer == actor ? "separation_of_duties" : null;

    // LEAD decision (QA F5): a retain-until date earlier than today (UTC) is rejected at set
    // time; it would otherwise make a record immediately disposable by back-dating.
    public static string? RetentionBlocker(string disposal, string? policyRef, DateOnly? retainUntil, DateOnly today) =>
        !Code(policyRef) || retainUntil == null || retainUntil < today ? "invalid_request" : disposal != "NONE" ? "document_disposal_pending" : null;

    // 58.02 §271 (DE-009) "kapatılmış aktif kullanım": active use is open while any revision is
    // EFFECTIVE or any domain link (certificate/evidence/credential) exists. Links are
    // append-only in this slice (no unlink yet) and there is no withdraw/retire, so such a
    // document stays non-disposable (fail-closed) until those slices exist.
    public static bool ActiveUseOpen(bool effectiveRevision, bool liveLinks) => effectiveRevision || liveLinks;

    // DE-009: hold blocks disposal; open active use, unconfigured or unelapsed retention blocks disposal.
    public static string? DisposalRequestBlocker(bool holdActive, string? policyRef, DateOnly? retainUntil, DateOnly today, string disposal, string? reason,
        bool activeUseOpen = false) =>
        !Text(reason, 1000) ? "invalid_request" : holdActive ? "legal_hold_active" : disposal != "NONE" ? "invalid_state" :
        activeUseOpen ? "active_use_open" :
        Retention(policyRef, retainUntil, today) switch { "UNCONFIGURED" => "retention_unconfigured", "RETAINING" => "retention_not_elapsed", _ => null };

    // Approval re-checks hold/active use/retention at approval time and requires a different approver.
    public static string? DisposalApproveBlocker(bool holdActive, string? policyRef, DateOnly? retainUntil, DateOnly today, string disposal,
        Guid? requester, Guid approver, string? reason, bool activeUseOpen = false) =>
        !Text(reason, 1000) ? "invalid_request" : disposal != "REQUESTED" ? "invalid_state" : requester == approver ? "separation_of_duties" :
        holdActive ? "legal_hold_active" : activeUseOpen ? "active_use_open" :
        Retention(policyRef, retainUntil, today) switch { "UNCONFIGURED" => "retention_unconfigured", "RETAINING" => "retention_not_elapsed", _ => null };

    // 58.02 §100/§273: a new hold or a new link while a disposal request is pending invalidates
    // the request (back to NONE, recorded in custody); the requester must request again and
    // approval re-checks everything. An APPROVED decision is terminal and is not reset.
    public static bool InvalidatesDisposalRequest(string disposal) => disposal == "REQUESTED";
}
