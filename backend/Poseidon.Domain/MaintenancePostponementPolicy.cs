namespace Poseidon.Domain;

// DEV-052: closed equipment criticality vocabulary copied from 59.04 Rev2 paragraph:1255
// (frozenCriticality CRITICAL|IMPORTANT|STANDARD|null). Null/any other legacy text is UNKNOWN and
// is treated like CRITICAL by every safety consumer (UNKNOWN never becomes "not critical").
public static class EquipmentCriticality
{
    public const string Critical = "CRITICAL", Important = "IMPORTANT", Standard = "STANDARD", Unknown = "UNKNOWN";
    public static readonly string[] Declared = [Critical, Important, Standard];
    public static readonly string[] All = [Critical, Important, Standard, Unknown];
    public static bool ValidInput(string? value) => value == null || Declared.Contains(value, StringComparer.Ordinal);
    public static string Of(string? value) => value != null && Declared.Contains(value, StringComparer.Ordinal) ? value : Unknown;
    public static bool RequiresReview(string criticality) => criticality is Critical or Unknown;
}

// Tenant-configured postponement policy (59.04 p265-266 pms.deferral.policy_ref: default null,
// unconfigured => defer approve closed; authorised max extension must come from the rule).
// No product default exists: an unconfigured tenant can request but never approve.
public sealed record PostponementPolicyRule(Guid TenantId, string PolicyRef, string Version, int MaxExtensionDays);

// DEV-109 / T-PMS2-003 (18.01 row 110; 06.01 FR-V463-PMS-003 paragraph:284; 59.04 Rev2 §12
// paragraphs 146-147, AT-033/034 paragraphs 369-372, pms.defer.approve paragraph:49).
// Request -> human APPROVE/REJECT by a different person. An approved postponement never
// changes the plan's original due date and never produces a readiness PASS.
public static class MaintenancePostponementPolicy
{
    public const string Requested = "REQUESTED", Approved = "APPROVED", Rejected = "REJECTED", Expired = "EXPIRED";
    public const string Approve = "APPROVE", Reject = "REJECT";
    public const string ReviewRequired = "REVIEW_REQUIRED", ConditionalDeferred = "CONDITIONAL_DEFERRED", PendingDecision = "PENDING_DECISION", Unchanged = "UNCHANGED";
    public static readonly string[] States = [Requested, Approved, Rejected];
    public static readonly string[] Decisions = [Approve, Reject];
    public static readonly string[] Impacts = [ReviewRequired, ConditionalDeferred, PendingDecision, Unchanged];
    public const int MaxReason = 2000;

    public static bool ValidPolicy(PostponementPolicyRule? rule) => rule != null && rule.TenantId != Guid.Empty &&
        rule.PolicyRef is { Length: > 0 and <= 80 } && rule.Version is { Length: > 0 and <= 80 } && rule.MaxExtensionDays is >= 1 and <= 3650;

    public static bool ValidReason(string? reason) => reason != null && reason.Trim().Length is >= 10 and <= MaxReason &&
        !reason.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t');

    public static int ExtensionDays(DateOnly originalDue, DateOnly deadline) => deadline.DayNumber - originalDue.DayNumber;

    // Request admission. A missing policy does NOT block a request (POL-DEFAULT-01 drafts);
    // a configured policy that the request already exceeds is refused at request time too.
    public static string? ValidateRequest(string? reason, DateOnly? originalDue, DateOnly deadline, DateOnly? previousApprovedDeadline,
        PostponementPolicyRule? rule, bool postponementDeclaredNotAllowed)
    {
        if (!ValidReason(reason)) return "invalid_reason";
        if (postponementDeclaredNotAllowed) return "postponement_not_allowed";
        if (originalDue == null) return "original_due_unavailable";
        if (deadline <= originalDue.Value) return "postponement_deadline_not_after_due";
        if (previousApprovedDeadline != null && deadline <= previousApprovedDeadline.Value) return "postponement_deadline_not_after_previous";
        // Chained extensions are measured from the FIRST original due date (AT-034).
        if (ValidPolicy(rule) && ExtensionDays(originalDue.Value, deadline) > rule!.MaxExtensionDays) return "postponement_extension_exceeds_policy";
        return null;
    }

    // Approval admission. Every check is conjunctive; the first failing one is reported.
    public static string? ValidateApproval(Guid requester, Guid decider, string criticality, bool approvedRiskReview,
        DateOnly originalDue, DateOnly deadline, PostponementPolicyRule? rule, bool planContextCurrent)
    {
        if (requester == decider) return "separation_of_duties";
        if (!planContextCurrent) return "plan_context_stale";
        if (!ValidPolicy(rule)) return "postponement_policy_unconfigured";
        if (ExtensionDays(originalDue, deadline) > rule!.MaxExtensionDays) return "postponement_extension_exceeds_policy";
        if (EquipmentCriticality.RequiresReview(criticality) && !approvedRiskReview) return "risk_review_required";
        return null;
    }

    public static string EffectiveState(string state, DateOnly deadline, DateOnly today) =>
        state == Approved && deadline < today ? Expired : state;

    // Readiness recalculation (59.04 p75, p147): a postponement never yields readiness. Critical or
    // UNKNOWN-criticality equipment with an open/active postponement is REVIEW_REQUIRED; other
    // approved postponements are CONDITIONAL_DEFERRED (overdue-deferred, no requirement exemption).
    public static string ReadinessImpact(string criticality, string effectiveState) => effectiveState switch
    {
        Requested => EquipmentCriticality.RequiresReview(criticality) ? ReviewRequired : PendingDecision,
        Approved => EquipmentCriticality.RequiresReview(criticality) ? ReviewRequired : ConditionalDeferred,
        _ => Unchanged
    };
}
