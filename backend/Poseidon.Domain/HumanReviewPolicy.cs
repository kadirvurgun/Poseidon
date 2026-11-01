namespace Poseidon.Domain;

// These are software workflow states, never an operational permit or source adoption.
public static class HumanReviewPolicy
{
    public static readonly string[] VerificationStates = ["PENDING", "VERIFIED", "REJECTED", "EXPIRED", "SUPERSEDED"];
    public static string? Transition(string kind, string? prior, string next, string reason, bool independent,
        bool evidenceCurrent, bool evidenceFresh, bool expired, bool superseded)
    {
        if (kind == "Decision") {
            if (next is not ("PENDING" or "REJECTED")) return "source_admission_and_signature_unavailable";
            if (prior == null && next != "PENDING") return "pending_required";
            if (prior is not (null or "PENDING")) return "terminal_review";
            return next == "PENDING" && reason == "REQUEST_REVIEW" || next == "REJECTED" && reason == "HUMAN_REJECTED" ? null : "invalid_reason";
        }
        if (kind != "Verification" || !VerificationStates.Contains(next)) return "invalid_state";
        if (prior == null) return next == "PENDING" && reason == "REQUEST_REVIEW" ? null : "pending_required";
        if (prior is "REJECTED" or "EXPIRED" or "SUPERSEDED") return "terminal_review";
        if (next == "PENDING") return prior == "PENDING" && reason == "REQUEST_REVIEW" ? null : "invalid_state";
        if (next == "EXPIRED") return prior == "VERIFIED" && expired && reason == "VALIDITY_ELAPSED" ? null : "expiry_not_due";
        if (next == "SUPERSEDED") return superseded && reason == "INPUT_SUPERSEDED" ? null : "supersession_not_proven";
        if (!independent) return "separation_of_duties";
        if (!evidenceCurrent) return "input_review_required";
        if (next == "VERIFIED") return prior == "PENDING" && evidenceFresh && reason == "HUMAN_VERIFIED" ? null : "freshness_review_required";
        return next == "REJECTED" && reason == "HUMAN_REJECTED" ? null : "invalid_state";
    }
    public static void Reason(string value)
    {
        // Private rationale is stored on the event only; audit summaries never copy it.
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > 2000 || value.Any(char.IsControl))
            throw new ArgumentException("Invalid rationale.");
    }
    public static string Current(string recorded, DateTimeOffset? until, bool superseded, DateTimeOffset asOf) =>
        superseded ? "SUPERSEDED_REVIEW_REQUIRED" : recorded == "VERIFIED" && (until == null || until <= asOf) ? "EXPIRED_REVIEW_REQUIRED" : recorded;
}
