using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// DEV-060 / T-S9-003 (18.01 Backlog row 61): "Type/date/vessel/context/description/evidence; immediate action;
// protected mode separate where needed; offline capture supported." Field contract: 61.01 Rev3 §4-6, §10-11
// (ReportRevision: occurrence precision EXACT|RANGE|DATE_ONLY|UNKNOWN with UNKNOWN requiring a reason, summary
// 10..500, narrative 10..20000, immediate action 0..4000, evidence revisions 0..50; reporter identity kept in a
// separate vault, never mass-assigned). POL-DEFAULT-01: drafts may be incomplete, submission lists what is missing.
// UNKNOWN stays UNKNOWN; it is never converted to No/NotApplicable. A reporter severity is a claim, never the
// triaged severity. RANGE precision is not offered in this package (open item, see phase report).
//
// This is the shared draft DTO shape: the parallel voice-draft package (DEV-062) produces candidate values for
// exactly these fields; a human edits/confirms them and the normal create/update/submit commands apply.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventDraftFields(string? Category = null, string? OccurrencePrecision = null, DateTimeOffset? OccurredAt = null,
    DateOnly? OccurrenceDate = null, string? OccurrenceUnknownReason = null, string? TimeZone = null, string? OperationContext = null,
    string? LocationText = null, string? Summary = null, string? Narrative = null, string? ImmediateActionTaken = null,
    string? ImmediateActionText = null, string? SeverityClaim = null, CertificateFilePin[]? Evidence = null)
{ public override string ToString() => nameof(SafetyEventDraftFields); }

// DEV-061 / T-S9-004 (18.01 Backlog row 62): "Contributing factors/root cause/barrier/action/owner/due/evidence/
// verification/effectiveness; Closed != Effective." 61.01 Rev3 §7 (Finding kind FACT|ALLEGATION|INFERENCE|
// UNRESOLVED, verification UNVERIFIED|SUPPORTED|CONTRADICTED|INCONCLUSIVE; a model confidence is not source truth).
// Root causes are only ever human statements: the server never derives one.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyFactor([property: JsonRequired] Guid Id, [property: JsonRequired] string Kind, [property: JsonRequired] string Category,
    [property: JsonRequired] string Statement)
{ public override string ToString() => nameof(SafetyFactor); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyRootCause([property: JsonRequired] Guid Id, [property: JsonRequired] string Statement, [property: JsonRequired] Guid[] FactorIds,
    [property: JsonRequired] string Verification)
{ public override string ToString() => nameof(SafetyRootCause); }

// A barrier analysed for this event: which preventive/mitigating barrier failed, was missing, degraded or worked.
// RiskBarrierRef optionally names a DEV-105 barrier reference; it is text, not a verified barrier-health source.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyBarrier([property: JsonRequired] Guid Id, [property: JsonRequired] string Name, [property: JsonRequired] string Function,
    [property: JsonRequired] string Performance, string? RiskBarrierRef = null)
{ public override string ToString() => nameof(SafetyBarrier); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyInvestigationContent(string? Scope, [property: JsonRequired] SafetyFactor[] Factors, [property: JsonRequired] SafetyRootCause[] RootCauses,
    [property: JsonRequired] SafetyBarrier[] Barriers, [property: JsonRequired] string[] Uncertainties, [property: JsonRequired] CertificateFilePin[] Evidence)
{ public override string ToString() => nameof(SafetyInvestigationContent); }

public sealed record SafetyActionView(Guid Id, string State, string Effectiveness, Guid OwnerId, Guid? CompletedBy, Guid? VerifiedBy, Guid? EffectivenessBy,
    Guid? FollowUpOf, Guid[] RootCauseIds, Guid[] BarrierIds);
public sealed record SafetyDecision(int Status, string? Code, string[]? Missing = null)
{
    public static readonly SafetyDecision Allowed = new(200, null);
    public static SafetyDecision Fail(int status, string code, string[]? missing = null) => new(status, code, missing);
    public bool Ok => Status == 200;
}

public static partial class SafetyEventPolicy
{
    public const string SourceVersion = "18.01:rows61-62;61.01-Rev3:s4-s11,s17";
    public const string Draft = "DRAFT", Submitted = "SUBMITTED", Returned = "RETURNED", Triaged = "TRIAGED", Investigating = "INVESTIGATING",
        ActionTracking = "ACTION_TRACKING", Closed = "CLOSED", Withdrawn = "WITHDRAWN";
    public static readonly string[] States = [Draft, Submitted, Returned, Triaged, Investigating, ActionTracking, Closed, Withdrawn];
    public static readonly string[] Categories = ["NEAR_MISS", "INCIDENT", "ACCIDENT", "AUDIT_FINDING", "DEFECT", "FATIGUE", "ENVIRONMENTAL", "CYBER", "OBSERVATION"];
    // NAMED: the reporter is visible to authorised readers. CONFIDENTIAL: the reporter identity lives only in the
    // identity vault (restricted permission + recorded purpose). ANONYMOUS needs the unauthenticated public intake
    // gateway (61.01 §11), which is not configured: an authenticated request can never be anonymous (422).
    public const string Named = "NAMED", Confidential = "CONFIDENTIAL", Anonymous = "ANONYMOUS";
    public static readonly string[] ReportingModes = [Named, Confidential];
    public static readonly string[] Precisions = ["EXACT", "DATE_ONLY", "UNKNOWN"];
    public static readonly string[] OperationContexts = ["AT_SEA", "IN_PORT", "AT_ANCHOR", "MOORING", "TOWAGE", "CARGO", "BUNKERING", "MAINTENANCE", "DRYDOCK", "OTHER", "UNKNOWN"];
    public static readonly string[] YesNoUnknown = ["YES", "NO", "UNKNOWN"];
    public static readonly string[] Severities = ["UNASSESSED", "LOW", "MEDIUM", "HIGH", "CRITICAL"];
    public static readonly string[] TriageDecisions = ["TRIAGE", "RETURN"];
    public static readonly string[] IdentityPurposes = ["INVESTIGATION_FOLLOW_UP", "WELFARE_SUPPORT", "LEGAL_OBLIGATION"];
    public static readonly string[] MessageVisibilities = ["TEAM_ONLY", "REPORTER_SHARED"];
    public static readonly string[] Origins = ["ONLINE", "OFFLINE"];

    public static readonly string[] InvestigationStates = ["DRAFT", "SUBMITTED", "RETURNED", "APPROVED", "REJECTED"];
    public static readonly string[] ReviewDecisions = ["APPROVE", "RETURN", "REJECT"];
    public static readonly string[] FactorKinds = ["FACT", "ALLEGATION", "INFERENCE", "UNRESOLVED"];
    public static readonly string[] FactorCategories = ["PEOPLE", "EQUIPMENT", "PROCEDURE", "ENVIRONMENT", "ORGANISATION", "EXTERNAL", "UNKNOWN"];
    public static readonly string[] Verifications = ["UNVERIFIED", "SUPPORTED", "CONTRADICTED", "INCONCLUSIVE"];
    public static readonly string[] BarrierFunctions = ["PREVENTIVE", "MITIGATING", "UNKNOWN"];
    public static readonly string[] BarrierPerformances = ["FAILED", "MISSING", "DEGRADED", "WORKED", "UNKNOWN"];

    public static readonly string[] ActionKinds = ["CORRECTIVE", "PREVENTIVE"];
    public static readonly string[] ActionStates = ["OPEN", "COMPLETED", "VERIFIED"];
    public static readonly string[] EffectivenessStates = ["PENDING", "EFFECTIVE", "NOT_EFFECTIVE", "INCONCLUSIVE"];
    public static readonly string[] StepKinds = ["COMPLETE", "VERIFY", "EFFECTIVENESS"];
    public static readonly string[] VerifyOutcomes = ["VERIFIED", "NOT_VERIFIED"];
    public static readonly string[] EffectivenessOutcomes = ["EFFECTIVE", "NOT_EFFECTIVE", "INCONCLUSIVE"];

    public const string ReadPermission = "hse.safety_event.read", ReportPermission = "hse.safety_event.report", TriagePermission = "hse.safety_event.triage",
        IdentityPermission = "hse.safety_event.identity.read", InvestigatePermission = "hse.safety_event.investigate", ReviewPermission = "hse.safety_event.review",
        ActionWritePermission = "hse.safety_action.write", ActionVerifyPermission = "hse.safety_action.verify", ActionEffectivenessPermission = "hse.safety_action.effectiveness";

    public const int MaxEvidence = 50, MaxItems = 50, MaxText = 4000, MaxNarrative = 20000, MinStatement = 10;

    private static bool In(string? value, string[] allowed) => value != null && allowed.Contains(value, StringComparer.Ordinal);
    private static bool Clean(string value) => !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    public static bool Text(string? value, int min, int max) => value != null && value.Trim().Length >= min && value.Length <= max && Clean(value);
    private static bool Optional(string? value, int max) => value == null || value.Length <= max && Clean(value);

    [GeneratedRegex(@"\A(UTC|[A-Za-z]{1,32}(/[A-Za-z0-9_+\-]{1,32}){1,2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Zone();

    public static bool ValidPins(CertificateFilePin[]? pins) => pins == null || pins.Length <= MaxEvidence &&
        pins.All(p => p != null && p.Id != Guid.Empty && p.Revision > 0 && p.Checksum is { Length: 64 } && p.Checksum.All(Uri.IsHexDigit)) &&
        pins.Select(p => p.Id).Distinct().Count() == pins.Length;

    // Draft shape: every provided value is closed, bounded and consistent with the chosen precision. Absent values
    // are allowed (incomplete draft). A contradictory draft (e.g. a date with UNKNOWN precision) is rejected.
    public static bool ValidDraft(SafetyEventDraftFields? f)
    {
        if (f == null) return false;
        if (f.Category != null && !In(f.Category, Categories) || f.OccurrencePrecision != null && !In(f.OccurrencePrecision, Precisions) ||
            f.OperationContext != null && !In(f.OperationContext, OperationContexts) || f.ImmediateActionTaken != null && !In(f.ImmediateActionTaken, YesNoUnknown) ||
            f.SeverityClaim != null && !In(f.SeverityClaim, Severities)) return false;
        if (!Optional(f.LocationText, 500) || !Optional(f.Summary, 500) || !Optional(f.Narrative, MaxNarrative) || !Optional(f.ImmediateActionText, MaxText) ||
            !Optional(f.OccurrenceUnknownReason, 1000) || f.TimeZone != null && !Zone().IsMatch(f.TimeZone)) return false;
        if (f.OccurredAt != null && f.OccurrencePrecision is not (null or "EXACT")) return false;
        if (f.OccurrenceDate != null && f.OccurrencePrecision is not (null or "DATE_ONLY")) return false;
        if (f.OccurrenceUnknownReason != null && f.OccurrencePrecision is not (null or "UNKNOWN")) return false;
        if (f.OccurredAt != null && f.OccurrenceDate != null) return false;
        // Text describing an immediate action while declaring that none was taken is contradictory.
        if (f.ImmediateActionTaken == "NO" && !string.IsNullOrWhiteSpace(f.ImmediateActionText)) return false;
        return ValidPins(f.Evidence);
    }

    // Submission requirements (POL-DEFAULT-01). Evidence is optional: a report without evidence is accepted.
    public static string[] Missing(SafetyEventDraftFields f)
    {
        var missing = new List<string>();
        if (f.Category == null) missing.Add("category");
        switch (f.OccurrencePrecision)
        {
            case null: missing.Add("occurrence_precision"); break;
            case "EXACT" when f.OccurredAt == null: missing.Add("occurred_at"); break;
            case "DATE_ONLY" when f.OccurrenceDate == null: missing.Add("occurrence_date"); break;
            case "UNKNOWN" when !Text(f.OccurrenceUnknownReason, MinStatement, 1000): missing.Add("occurrence_unknown_reason"); break;
        }
        if (f.OperationContext == null) missing.Add("operation_context");
        if (!Text(f.Summary, MinStatement, 500)) missing.Add("summary");
        if (!Text(f.Narrative, MinStatement, MaxNarrative)) missing.Add("narrative");
        if (f.ImmediateActionTaken == null) missing.Add("immediate_action_taken");
        else if (f.ImmediateActionTaken == "YES" && !Text(f.ImmediateActionText, 1, MaxText)) missing.Add("immediate_action_text");
        return [.. missing];
    }

    // occurredAt <= server now (61.01 §2); a date-only occurrence may not be after the server's UTC date plus the
    // widest zone offset (+14h), so a ship west/east of UTC is never rejected for its local date.
    public static bool InFuture(SafetyEventDraftFields f, DateTimeOffset now) =>
        f.OccurredAt is { } at && at.ToUniversalTime() > now.ToUniversalTime() ||
        f.OccurrenceDate is { } date && date > DateOnly.FromDateTime(now.ToUniversalTime().AddHours(14).DateTime);

    public static bool Open(string state) => state is Submitted or Returned or Triaged or Investigating or ActionTracking;

    // Reporter draft edits: author only, DRAFT or RETURNED (61.01 §5 PUT draft).
    public static SafetyDecision Edit(string state, bool reporter) =>
        state is not (Draft or Returned) ? SafetyDecision.Fail(409, "invalid_state") : !reporter ? SafetyDecision.Fail(403, "permission_denied") : SafetyDecision.Allowed;

    public static SafetyDecision Submit(string state, bool reporter, SafetyEventDraftFields f, DateTimeOffset now)
    {
        var edit = Edit(state, reporter); if (!edit.Ok) return edit;
        var missing = Missing(f);
        if (missing.Length > 0) return SafetyDecision.Fail(422, "safety_event_incomplete", missing);
        return InFuture(f, now) ? SafetyDecision.Fail(422, "occurrence_in_future") : SafetyDecision.Allowed;
    }

    // WITHDRAWN only for an unsubmitted draft; a submitted report can never be withdrawn or deleted by its author.
    public static SafetyDecision Withdraw(string state, bool reporter) =>
        state != Draft ? SafetyDecision.Fail(409, "invalid_state") : !reporter ? SafetyDecision.Fail(403, "permission_denied") : SafetyDecision.Allowed;

    // Triage (SUBMITTED, or a re-triage while TRIAGED) and RETURN (SUBMITTED only). The reporter never triages
    // their own report. Severity is a human classification; no severity policy source exists, so the read model
    // marks it SeverityPolicyUnconfigured and nothing derives a severity automatically.
    public static SafetyDecision Triage(string state, string decision, string? severity, string? investigationRequired, bool reporter)
    {
        if (!In(decision, TriageDecisions)) return SafetyDecision.Fail(400, "invalid_request");
        if (decision == "RETURN") return state != Submitted ? SafetyDecision.Fail(409, "invalid_state") : severity != null || investigationRequired != null
            ? SafetyDecision.Fail(400, "invalid_request") : reporter ? SafetyDecision.Fail(403, "separation_of_duties") : SafetyDecision.Allowed;
        if (!In(severity, Severities) || !In(investigationRequired, YesNoUnknown)) return SafetyDecision.Fail(400, "invalid_request");
        if (state is not (Submitted or Triaged)) return SafetyDecision.Fail(409, "invalid_state");
        return reporter ? SafetyDecision.Fail(403, "separation_of_duties") : SafetyDecision.Allowed;
    }

    public static bool ValidInvestigation(SafetyInvestigationContent? c)
    {
        if (c == null || !Optional(c.Scope, MaxText) || c.Factors.Length > MaxItems || c.RootCauses.Length > MaxItems || c.Barriers.Length > MaxItems ||
            c.Uncertainties.Length > MaxItems || !ValidPins(c.Evidence)) return false;
        if (c.Factors.Any(f => f == null || f.Id == Guid.Empty || !In(f.Kind, FactorKinds) || !In(f.Category, FactorCategories) || !Text(f.Statement, 1, MaxText))) return false;
        var factors = c.Factors.Select(f => f.Id).ToHashSet();
        if (factors.Count != c.Factors.Length) return false;
        if (c.RootCauses.Any(r => r == null || r.Id == Guid.Empty || !Text(r.Statement, 1, MaxText) || !In(r.Verification, Verifications) ||
            r.FactorIds.Length > MaxItems || r.FactorIds.Distinct().Count() != r.FactorIds.Length || r.FactorIds.Any(id => !factors.Contains(id)))) return false;
        if (c.Barriers.Any(b => b == null || b.Id == Guid.Empty || !Text(b.Name, 1, 200) || !In(b.Function, BarrierFunctions) ||
            !In(b.Performance, BarrierPerformances) || !Optional(b.RiskBarrierRef, 64))) return false;
        var ids = c.Factors.Select(f => f.Id).Concat(c.RootCauses.Select(r => r.Id)).Concat(c.Barriers.Select(b => b.Id)).ToArray();
        return ids.Distinct().Count() == ids.Length && c.Uncertainties.All(u => Text(u, 1, MaxText));
    }

    // Investigation submission: scope, at least one contributing factor, the barrier analysis, and either a human
    // root-cause statement or an explicit uncertainty saying why none could be determined.
    public static string[] InvestigationMissing(SafetyInvestigationContent c)
    {
        var missing = new List<string>();
        if (!Text(c.Scope, MinStatement, MaxText)) missing.Add("scope");
        if (c.Factors.Length == 0) missing.Add("contributing_factors");
        if (c.Barriers.Length == 0) missing.Add("barriers");
        if (c.RootCauses.Length == 0 && c.Uncertainties.Length == 0) missing.Add("root_cause_or_uncertainty");
        return [.. missing];
    }

    // Independent review (61.01 §7): reviewer is neither an investigation author, the submitter, nor the reporter.
    public static SafetyDecision ReviewInvestigation(string state, string decision, Guid actor, IEnumerable<Guid> authors, Guid? submitter, bool reporter)
    {
        if (!In(decision, ReviewDecisions)) return SafetyDecision.Fail(400, "invalid_request");
        if (state != "SUBMITTED") return SafetyDecision.Fail(409, "invalid_state");
        return authors.Contains(actor) || submitter == actor || reporter ? SafetyDecision.Fail(403, "separation_of_duties") : SafetyDecision.Allowed;
    }

    // Action steps (DefectLifecyclePolicy pattern): COMPLETE by the owner; VERIFY by someone who is neither owner
    // nor completer; EFFECTIVENESS by someone who is none of owner, completer or verifier and only after
    // verification. Verified != Effective; an INCONCLUSIVE review may be reviewed again later, an EFFECTIVE or
    // NOT_EFFECTIVE review is final (a failed action needs a new follow-up action).
    public static SafetyDecision Step(SafetyActionView a, string kind, string? outcome, Guid actor, ISet<string> permissions)
    {
        switch (kind)
        {
            case "COMPLETE":
                if (outcome != null) return SafetyDecision.Fail(400, "invalid_request");
                if (a.State != "OPEN") return SafetyDecision.Fail(409, "invalid_state");
                return actor != a.OwnerId ? SafetyDecision.Fail(403, "separation_of_duties") : SafetyDecision.Allowed;
            case "VERIFY":
                if (!In(outcome, VerifyOutcomes)) return SafetyDecision.Fail(400, "invalid_request");
                if (a.State != "COMPLETED") return SafetyDecision.Fail(409, "invalid_state");
                if (!permissions.Contains(ActionVerifyPermission)) return SafetyDecision.Fail(403, "permission_denied");
                return actor == a.OwnerId || actor == a.CompletedBy ? SafetyDecision.Fail(403, "separation_of_duties") : SafetyDecision.Allowed;
            case "EFFECTIVENESS":
                if (!In(outcome, EffectivenessOutcomes)) return SafetyDecision.Fail(400, "invalid_request");
                if (a.State != "VERIFIED" || a.Effectiveness is not ("PENDING" or "INCONCLUSIVE")) return SafetyDecision.Fail(409, "invalid_state");
                if (!permissions.Contains(ActionEffectivenessPermission)) return SafetyDecision.Fail(403, "permission_denied");
                return actor == a.OwnerId || actor == a.CompletedBy || actor == a.VerifiedBy ? SafetyDecision.Fail(403, "separation_of_duties") : SafetyDecision.Allowed;
            default:
                return SafetyDecision.Fail(400, "invalid_request");
        }
    }

    // Closure blockers (61.01 §9): the closure reviewer cannot bypass them. Effectiveness is recorded before closure
    // (DEV-055 pattern) but closure stays a separate human record: Closed != Effective.
    public static string[] ClosureBlockers(string state, string? investigationRequired, string? investigationState, SafetyInvestigationContent? approved,
        IReadOnlyCollection<SafetyActionView> actions)
    {
        var blockers = new List<string>();
        if (state == Triaged)
        {
            if (investigationRequired == "UNKNOWN") blockers.Add("INVESTIGATION_NEED_UNKNOWN");
            else if (investigationRequired != "NO") blockers.Add("INVESTIGATION_REQUIRED");
            if (actions.Count > 0) blockers.Add("ACTIONS_WITHOUT_APPROVED_INVESTIGATION");
            return [.. blockers];
        }
        if (state != ActionTracking) return ["INVALID_STATE"];
        if (investigationState != "APPROVED" || approved == null) return ["INVESTIGATION_NOT_APPROVED"];
        var linkedBarriers = actions.SelectMany(a => a.BarrierIds).ToHashSet();
        var linkedCauses = actions.SelectMany(a => a.RootCauseIds).ToHashSet();
        foreach (var b in approved.Barriers.Where(b => b.Performance is "FAILED" or "MISSING" or "DEGRADED" && !linkedBarriers.Contains(b.Id)))
            blockers.Add("BARRIER_WITHOUT_ACTION:" + b.Id.ToString("D"));
        foreach (var r in approved.RootCauses.Where(r => r.Verification != "CONTRADICTED" && !linkedCauses.Contains(r.Id)))
            blockers.Add("ROOT_CAUSE_WITHOUT_ACTION:" + r.Id.ToString("D"));
        foreach (var a in actions.OrderBy(a => a.Id))
        {
            var id = a.Id.ToString("D");
            if (a.State != "VERIFIED") blockers.Add("ACTION_NOT_VERIFIED:" + id);
            else if (a.Effectiveness == "PENDING") blockers.Add("EFFECTIVENESS_NOT_REVIEWED:" + id);
            else if (a.Effectiveness == "INCONCLUSIVE") blockers.Add("EFFECTIVENESS_INCONCLUSIVE:" + id);
            else if (a.Effectiveness == "NOT_EFFECTIVE" && !actions.Any(x => x.FollowUpOf == a.Id)) blockers.Add("NOT_EFFECTIVE_WITHOUT_FOLLOW_UP:" + id);
        }
        return [.. blockers];
    }

    // Event-level effectiveness is a separate projection of the human per-action reviews, never inferred from
    // closure. NO_ACTIONS is shown as such (not EFFECTIVE, not NOT_APPLICABLE).
    public static string Effectiveness(IReadOnlyCollection<SafetyActionView> actions)
    {
        var followed = actions.Where(a => a.FollowUpOf != null).Select(a => a.FollowUpOf!.Value).ToHashSet();
        var leaves = actions.Where(a => !(a.Effectiveness == "NOT_EFFECTIVE" && followed.Contains(a.Id))).ToArray();
        if (actions.Count == 0) return "NO_ACTIONS";
        if (leaves.Any(a => a.Effectiveness == "NOT_EFFECTIVE")) return "NOT_EFFECTIVE";
        if (leaves.Any(a => a.Effectiveness == "INCONCLUSIVE")) return "INCONCLUSIVE";
        if (leaves.Any(a => a.Effectiveness == "PENDING")) return "PENDING";
        return "EFFECTIVE";
    }

    // Read contract for other packages (ISafetyEventBlockerSource): an open HIGH/CRITICAL event is a blocker
    // candidate; an untriaged/returned report or an UNASSESSED triage is REVIEW_REQUIRED; nothing is ever READY.
    public static string? BlockerReason(string state, string? severity) => state switch
    {
        Submitted or Returned => "UNTRIAGED_EVENT",
        _ when !Open(state) => null,
        _ when severity is "HIGH" or "CRITICAL" => "HIGH_SEVERITY_OPEN",
        _ when severity is null or "UNASSESSED" => "SEVERITY_UNASSESSED",
        _ => null
    };
    public static bool Blocking(string reason) => reason == "HIGH_SEVERITY_OPEN";
}
