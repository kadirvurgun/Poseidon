namespace Poseidon.Domain;

// DEV-040 (18.01 Backlog row 41: "manual override controlled reason + audit gerektirir") implemented with
// the owning 60.03 contract: §10 OverrideDecision (paragraph:154-166), the OVERRIDE_AUTHORITY policy
// (paragraph:575-576, 580) and REG-033..035 (paragraph:1075-1082).
//  - An override never changes the rule, the source or the recorded evaluation result; the effective view
//    shows baseResult plus override lineage (paragraph:155).
//  - Activation needs a current AuthorityGrant inside a published OVERRIDE_AUTHORITY policy; without it,
//    activation is closed (paragraph:155, 580) and approve is 422 (REG-034). This repository has no such
//    published policy or grant store yet, so the server resolver supplies none and no override can become
//    ACTIVE. The open expert/authority decision is recorded in docs/phases/S5-WPA-20261119.md.
//  - AI/admin sole approval, approving one's own override, out-of-tenant scope and open-ended validity are
//    forbidden (paragraph:160); a second ACTIVE decision for the same scope is 409 OVERRIDE_OVERLAP (162).
//  - ACCEPT_INTERPRETATION cannot turn a missing critical fact into FALSE or an UNKNOWN/REVIEW result into a
//    decision (paragraph:154; REG-034): it is only proposable for an authoritative APPLICABLE/NOT_APPLICABLE.
public sealed record OverrideProposal(string Action, string ReasonCode, string Purpose, string Rationale,
    Guid[] EvidenceIds, DateTimeOffset ValidFrom, DateTimeOffset ExpiresAt, DateTimeOffset ReviewAt);

// A current, scoped grant resolved by the server from a published OVERRIDE_AUTHORITY policy (60.03
// paragraph:576). It is never constructed from a request, a role label or an administrative flag.
public sealed record OverrideAuthorityGrant(Guid GrantId, Guid PersonId, string[] AllowedActions,
    long MaxDurationSeconds, long MaxReviewIntervalSeconds, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);

public static class ApplicabilityOverridePolicy
{
    public const string ApprovePermission = "applicability.override.approve";
    public const string AuthorPermission = "applicability.override.author";
    public const string RevokePermission = "applicability.override.revoke";
    public static readonly string[] Actions = ["ACCEPT_INTERPRETATION", "REQUIRE_ADDITIONAL_CONTROL"];
    public static readonly string[] Purposes = ["COMPLIANCE_REVIEW"];
    public static readonly string[] States = ["DRAFT", "IN_REVIEW", "ACTIVE", "REVOKED", "EXPIRED", "SUPERSEDED"];
    public static readonly string[] Decisions = ["APPROVE", "REJECT", "WITHDRAW", "REVOKE"];
    public static readonly string[] EventCodes = ["PROPOSE", .. Decisions];
    // Controlled reason catalog. ACCEPT_INTERPRETATION rests only on a referenced verified legal
    // interpretation/permission or a flag/class exemption (paragraph:154, 164); a restriction may add a
    // control as a precaution or because of an open source conflict. Free text is the private rationale.
    public static readonly IReadOnlyDictionary<string, string[]> Reasons = new Dictionary<string, string[]>(StringComparer.Ordinal) {
        ["ACCEPT_INTERPRETATION"] = ["VERIFIED_LEGAL_INTERPRETATION", "FLAG_STATE_EXEMPTION_REFERENCE", "CLASS_EXEMPTION_REFERENCE"],
        ["REQUIRE_ADDITIONAL_CONTROL"] = ["ADDITIONAL_PRECAUTION", "SOURCE_CONFLICT_PRECAUTION"],
    };
    public static IEnumerable<string> ReasonCodes => Reasons.Values.SelectMany(x => x);
    // Technical upper limits from the OVERRIDE_AUTHORITY schema (paragraph:576). They are capacity
    // bounds, never a legal default; a real grant may only shorten them. There is no default 30 days.
    public const long MaxDurationSeconds = 31536000;
    public const int MaxEvidence = 10;

    public static string? ValidateProposal(OverrideProposal p, string baseResult, bool baseAuthoritative, DateTimeOffset now)
    {
        if (!Actions.Contains(p.Action) || !Purposes.Contains(p.Purpose)) return "invalid_request";
        if (!Reasons[p.Action].Contains(p.ReasonCode)) return "override_reason_not_allowed";
        if (p.Rationale is not { Length: >= 20 and <= 4000 } || p.Rationale != p.Rationale.Trim() || p.Rationale.Any(c => char.IsControl(c) && c is not ('\n' or '\r')))
            return "invalid_request";
        if (p.EvidenceIds is not { Length: <= MaxEvidence } || p.EvidenceIds.Any(x => x == Guid.Empty) || p.EvidenceIds.Distinct().Count() != p.EvidenceIds.Length)
            return "invalid_request";
        // An interpretation must cite a reference record; a restriction may stand on its rationale.
        if (p.Action == "ACCEPT_INTERPRETATION" && p.EvidenceIds.Length == 0) return "override_evidence_required";
        if (p.Action == "ACCEPT_INTERPRETATION" && !(baseAuthoritative && baseResult is "APPLICABLE" or "NOT_APPLICABLE"))
            return "override_unknown_not_acceptable";
        // expiresAt and reviewAt are mandatory, reviewAt <= expiresAt, no open-ended validity (paragraph:155, 160).
        if (p.ExpiresAt <= p.ValidFrom || p.ReviewAt > p.ExpiresAt || p.ReviewAt <= now || p.ExpiresAt <= now ||
            p.ValidFrom < now.AddMinutes(-5) || (p.ExpiresAt - p.ValidFrom).TotalSeconds > MaxDurationSeconds)
            return "override_validity_invalid";
        return null;
    }

    // Returns null when the decision is allowed, otherwise a fixed failure code. "bindingCurrent" means the
    // override's pinned evaluation, input hash, rule-set hash and exact rule revision are still the current
    // qualified applicability; "overlap" means another ACTIVE decision intersects scope/rule/purpose/time.
    public static string? Decide(string state, string decision, Guid actor, Guid author, bool humanActor, bool holdsApprove,
        bool holdsRevoke, OverrideAuthorityGrant? grant, string action, DateTimeOffset validFrom, DateTimeOffset expiresAt,
        DateTimeOffset reviewAt, bool bindingCurrent, bool overlap, DateTimeOffset now)
    {
        if (!States.Contains(state)) return "invalid_state";
        if (!Decisions.Contains(decision)) return "invalid_request";
        if (!humanActor) return "human_actor_required";
        switch (decision) {
            case "WITHDRAW":
                if (state is not ("DRAFT" or "IN_REVIEW")) return "invalid_transition";
                return actor == author ? null : "override_author_required";
            case "REJECT":
                if (state != "IN_REVIEW") return "invalid_transition";
                if (!holdsApprove) return "permission_denied";
                return actor == author ? "separation_of_duties" : null;
            case "REVOKE":
                if (state != "ACTIVE") return "invalid_transition";
                return holdsRevoke ? null : "permission_denied";
            default: // APPROVE
                if (state != "IN_REVIEW") return "invalid_transition";
                if (!holdsApprove) return "permission_denied";
                if (actor == author) return "separation_of_duties";
                if (grant == null) return "override_authority_unavailable";
                if (grant.PersonId != actor || !grant.AllowedActions.Contains(action) || grant.ValidFrom > now || grant.ValidUntil <= now ||
                    grant.MaxReviewIntervalSeconds > grant.MaxDurationSeconds || grant.MaxDurationSeconds is < 1 or > MaxDurationSeconds ||
                    (expiresAt - validFrom).TotalSeconds > grant.MaxDurationSeconds || (reviewAt - validFrom).TotalSeconds > grant.MaxReviewIntervalSeconds ||
                    expiresAt > grant.ValidUntil)
                    return "override_authority_unavailable";
                if (!bindingCurrent) return "evaluation_stale";
                if (now >= (reviewAt < expiresAt ? reviewAt : expiresAt)) return "override_validity_invalid";
                return overlap ? "override_overlap" : null;
        }
    }

    public static string Next(string decision) => decision == "APPROVE" ? "ACTIVE" : "REVOKED";

    // Read-time validity (paragraph:156): an ACTIVE row is invalid for current use once now >=
    // min(reviewAt, expiresAt) even if no worker has run; a changed evaluation, input hash, rule-set hash
    // or rule revision makes it REQUIRES_REVIEW. No stored state is rewritten to compute this.
    public static string CurrentValidity(string state, DateTimeOffset validFrom, DateTimeOffset expiresAt, DateTimeOffset reviewAt,
        bool bindingCurrent, DateTimeOffset now)
    {
        if (state is "REVOKED" or "EXPIRED" or "SUPERSEDED") return state;
        if (!bindingCurrent) return "REQUIRES_REVIEW";
        if (now >= (reviewAt < expiresAt ? reviewAt : expiresAt)) return "EXPIRED";
        if (state != "ACTIVE") return "NOT_ACTIVE";
        return now < validFrom ? "NOT_YET_VALID" : "CURRENT";
    }

    // Two ACTIVE decisions overlap when they share vessel/rule/purpose and their [validFrom, expiresAt) intersect.
    public static bool Overlaps(DateTimeOffset aFrom, DateTimeOffset aTo, DateTimeOffset bFrom, DateTimeOffset bTo) => aFrom < bTo && bFrom < aTo;
}
