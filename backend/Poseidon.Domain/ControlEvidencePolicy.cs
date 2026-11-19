using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// These are declarations. Neither a text policy nor a clean transport is admission,
// business completion, human verification or operational effectiveness.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlEvidenceContent(string? Title = null, Guid? OwnerId = null, Guid? OwnerRole = null,
    string? Frequency = null, string? EvidencePolicy = null, string? VerificationPolicy = null, bool? Critical = null,
    string? Provenance = null, DateTimeOffset? ObservedAt = null, DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidUntil = null, DateTimeOffset? FreshUntil = null, SourceAcknowledgment[]? Acknowledgments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TugCapabilityStatement? TugCapability = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OperationReference? Operation = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ServiceAuthorizationReference? ServiceAuthorization = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CargoPlanReference? CargoPlan = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WinchFitmentReference? WinchFitment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ClassPackReference? ClassPack = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MsmdReference? Msmd = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CrewRoleAssessmentReference? CrewRoleAssessment = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceAcknowledgment(Guid RuleId, string QualificationHash, string SourceHash);

public static class ControlEvidencePolicy
{
    public static void Validate(string kind, ControlEvidenceContent c, DateTimeOffset now)
    {
        OperationEquipmentInputs.Validate(kind, c);
        if (c.TugCapability != null) {
            if (kind != "Evidence") throw new ArgumentException("invalid_tug_evidence_kind");
            TugCapability.Validate(c.TugCapability);
        }
        if (c.Acknowledgments is { } acknowledgments && (acknowledgments.Length > 32 || acknowledgments.Any(x => x == null || x.RuleId == Guid.Empty ||
            !System.Text.RegularExpressions.Regex.IsMatch(x.QualificationHash ?? "", "\\A[A-F0-9]{64}\\z") ||
            !System.Text.RegularExpressions.Regex.IsMatch(x.SourceHash ?? "", "\\A[A-F0-9]{64}\\z")) || acknowledgments.Select(x => x.RuleId).Distinct().Count() != acknowledgments.Length)) throw new ArgumentException("invalid_acknowledgment");
        if (kind is not ("Control" or "Evidence")) throw new ArgumentException("invalid_kind");
        foreach (var text in new[] { c.Title, c.Frequency, c.EvidencePolicy, c.VerificationPolicy, c.Provenance })
            if (text != null && (text.Length is < 1 or > 2000 || text != text.Trim() || text.Any(char.IsControl))) throw new ArgumentException("invalid_content");
        if ((c.OwnerId == null) != (c.OwnerRole == null) || c.OwnerId == Guid.Empty || c.OwnerRole == Guid.Empty) throw new ArgumentException("invalid_owner");
        if (c.ObservedAt > now || c.ValidFrom != null && c.ValidUntil < c.ValidFrom || c.ObservedAt != null && c.FreshUntil < c.ObservedAt)
            throw new ArgumentException("invalid_chronology");
        if (kind == "Control" && (c.ObservedAt != null || c.ValidFrom != null || c.ValidUntil != null || c.FreshUntil != null)) throw new ArgumentException("invalid_control_dates");
    }
    public static string[] Blockers(string kind, ControlEvidenceContent c, int requirements, bool file, int applicabilityLinks = 0)
    {
        var reasons = new List<string>();
        if (OperationEquipmentInputs.Present(c) && !OperationEquipmentInputs.Complete(c)) reasons.Add("OWNING_INPUT_INCOMPLETE");
        if (c.TugCapability is { } capability && !TugCapability.Complete(capability)) reasons.Add("TUG_CAPABILITY_INCOMPLETE");
        if (c.Title == null) reasons.Add("TITLE_MISSING");
        if (c.OwnerId == null) reasons.Add("OWNER_MISSING");
        if (requirements == 0) reasons.Add("REQUIREMENT_LINK_MISSING");
        if (requirements > applicabilityLinks) reasons.Add("APPLICABILITY_LINK_MISSING");
        if (kind == "Control") {
            if (c.Frequency == null) reasons.Add("FREQUENCY_UNSPECIFIED");
            if (c.EvidencePolicy == null) reasons.Add("EVIDENCE_POLICY_MISSING");
            if (c.VerificationPolicy == null) reasons.Add("VERIFICATION_POLICY_MISSING");
            if (c.Critical == null) reasons.Add("CRITICALITY_UNKNOWN");
        } else {
            if (!file) reasons.Add("CLEAN_FILE_MISSING");
            if (c.Provenance == null) reasons.Add("PROVENANCE_MISSING");
            if (c.ObservedAt == null) reasons.Add("OBSERVATION_TIME_MISSING");
        }
        reasons.Add("SOURCE_ADMISSION_UNAVAILABLE");
        reasons.Add("VERIFICATION_NOT_PERFORMED");
        return reasons.ToArray();
    }
    // DEV-028 orphan finding for one current, non-retired control. Unknown criticality is
    // reported (never treated as non-critical); a declared non-critical control is not an orphan.
    public static string[] OrphanReasons(bool? critical, bool requirementLinked, bool ownerDeclared)
    {
        if (critical == false || requirementLinked && ownerDeclared) return [];
        var reasons = new List<string>();
        if (critical == null) reasons.Add("CRITICALITY_UNKNOWN");
        if (!requirementLinked) reasons.Add("REQUIREMENT_LINK_MISSING");
        if (!ownerDeclared) reasons.Add("OWNER_MISSING");
        return reasons.ToArray();
    }
    // Dates are retained declarations, never an invented qualified freshness policy.
    public static string TimeState(ControlEvidenceContent c, DateTimeOffset asOf, bool superseded) => superseded ? "SUPERSEDED" :
        c.ValidFrom > asOf ? "DECLARED_NOT_YET_VALID" : c.ValidUntil < asOf ? "DECLARED_EXPIRED" :
        c.FreshUntil < asOf ? "DECLARED_STALE" : "UNKNOWN";
}

// DEV-028 / 18.01 Backlog row 29 "state", using the implementation lifecycle of
// 62.01 §6 (paragraph:173-198, commands paragraph:445-465): DRAFT → IN_REVIEW → APPROVED →
// PUBLISHED → RETIRED, IN_REVIEW → DRAFT on return. APPROVED is a technical implementation
// approval, never an official plan/legal approval, verification, completion or effectiveness.
// Dual pointer (paragraph:188-190, 198, 447): a published revision is never edited; a revision
// of a reviewed/approved/published control is a successor candidate in DRAFT ("prior approval
// is not current") while the published revision stays the effective one until the successor
// itself is PUBLISHED, which supersedes it. RETIRED is terminal and accepts no revision.
public static class ControlLifecyclePolicy
{
    public static readonly string[] States = ["DRAFT", "IN_REVIEW", "APPROVED", "PUBLISHED", "RETIRED"];
    public static readonly string[] CommandReasons = ["SUBMIT_FOR_REVIEW", "RETURN_FOR_CORRECTION", "APPROVE", "PUBLISH", "RETIRE"];
    public const string ContentRevised = "CONTENT_REVISED";
    // A control head that has never had a lifecycle event stores NULL: its only honest state is
    // DRAFT. This is derived on read, so no stored row is rewritten to manufacture the state.
    public static string State(string? stored) => stored ?? "DRAFT";
    public static string? Next(string reason) => reason switch {
        "SUBMIT_FOR_REVIEW" => "IN_REVIEW", "RETURN_FOR_CORRECTION" => "DRAFT", "APPROVE" => "APPROVED",
        "PUBLISH" => "PUBLISHED", "RETIRE" => "RETIRED", _ => null };
    // prior: state of the candidate (latest) revision. published: an effective published revision
    // exists (it may be the candidate itself or an earlier revision with a pending successor).
    // recordReady: the exact candidate revision passed the submission blockers (SUBMITTED/COMPLETED).
    // reviewer: holds control.approve. independent: differs from the review submitter and revision author.
    // qualified: all required qualified source/policy gates resolve for the exact revision.
    public static string? Transition(string prior, string reason, bool recordReady, bool reviewer, bool independent, bool qualified, bool published = false)
    {
        if (!States.Contains(prior)) return "invalid_state";
        var next = Next(reason);
        if (next == null) return "invalid_reason";
        if (prior == "RETIRED") return "control_retired";
        // RETIRE acts on the effective published revision, also while a successor is pending.
        var allowed = (prior, next) is ("DRAFT", "IN_REVIEW") or ("IN_REVIEW", "DRAFT") or ("IN_REVIEW", "APPROVED") or ("APPROVED", "PUBLISHED") or ("PUBLISHED", "RETIRED") ||
            next == "RETIRED" && published;
        if (!allowed) return "invalid_transition";
        if (next == "IN_REVIEW") return recordReady ? null : "control_record_incomplete";
        if (!reviewer) return "control_reviewer_required";
        if (next is "DRAFT" or "APPROVED" && !independent) return "separation_of_duties";
        if (next == "APPROVED" && !recordReady) return "control_record_incomplete";
        if (next == "APPROVED" && !qualified) return "qualified_source_required";
        return null;
    }
    // A new content/submit/complete revision of a reviewed, approved or published control opens
    // (or resets) the successor candidate in DRAFT; a RETIRED control accepts no revision (409).
    public static bool RevisionResets(string prior) => prior is "IN_REVIEW" or "APPROVED" or "PUBLISHED";
    public static bool AcceptsRevision(string prior) => prior != "RETIRED";
    // Effective (current) published revision after a transition. PUBLISH makes the candidate
    // effective and supersedes the previous one; RETIRE clears the current pointer; any other
    // transition (including a successor's IN_REVIEW/APPROVED/return) leaves it unchanged.
    public static long? EffectiveAfter(string reason, long? published, long candidate) => reason switch {
        "PUBLISH" => candidate, "RETIRE" => null, _ => published };
    // The published revision a PUBLISH replaces, recorded on the audited event; null for a first publish.
    public static long? Supersedes(string reason, long? published, long candidate) =>
        reason == "PUBLISH" && published is { } prior && prior != candidate ? prior : null;
    // Revision that reads, orphan findings, supporting evidence and verification gates treat as
    // current: the published revision while a successor is pending, otherwise the latest revision.
    public static long EffectiveRevision(long? published, long latest) => published ?? latest;
}
