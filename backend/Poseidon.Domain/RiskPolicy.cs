using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-105 (T-RISK2-001, 18.01 Backlog row 106) and DEV-106 (T-RISK2-002, row 107).
// Field contract: 50.01 Rev3 §3-6 (paragraphs 29, 37, 59, 64-74, 85-99, 318-430).
// Development target only. HSE-G01..G08 stay OPEN; nothing here is a permit, a risk
// acceptance by an expert, an approved production catalog or a signed decision.
// 50.01 paragraph 37 / N06: there is no default 5x5, multiplication or universal
// low/medium/high threshold. A score exists only as a lookup in an exact PUBLISHED,
// human-approved tenant risk method; a missing method or L/S value is NOT_ASSESSED.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskMatrixCode([property: JsonRequired] string Code, [property: JsonRequired] string Label)
{ public override string ToString() => nameof(RiskMatrixCode); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskMatrixCell([property: JsonRequired] string Likelihood, [property: JsonRequired] string Severity,
    [property: JsonRequired] string Result, [property: JsonRequired] int Rank)
{ public override string ToString() => nameof(RiskMatrixCell); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskMethodContent([property: JsonRequired] string Code, [property: JsonRequired] string Title,
    [property: JsonRequired] RiskMatrixCode[] Likelihoods, [property: JsonRequired] RiskMatrixCode[] Severities,
    [property: JsonRequired] RiskMatrixCell[] Cells, [property: JsonRequired] string[] SourceRefs, [property: JsonRequired] string ChangeReason)
{ public override string ToString() => nameof(RiskMethodContent); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskRating(string? Likelihood, string? Severity, string? Reason)
{ public override string ToString() => nameof(RiskRating); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskControl([property: JsonRequired] Guid Id, [property: JsonRequired] string Description,
    [property: JsonRequired] string Kind, string? Owner, [property: JsonRequired] bool Required, [property: JsonRequired] string[] BarrierRefs)
{ public override string ToString() => nameof(RiskControl); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskHazard([property: JsonRequired] Guid Id, string? Step, [property: JsonRequired] string Description,
    [property: JsonRequired] string[] Consequences, [property: JsonRequired] RiskRating Initial, [property: JsonRequired] RiskControl[] Controls,
    [property: JsonRequired] string[] BarrierRefs, [property: JsonRequired] RiskRating Residual)
{ public override string ToString() => nameof(RiskHazard); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskStopCondition([property: JsonRequired] Guid Id, [property: JsonRequired] string Description)
{ public override string ToString() => nameof(RiskStopCondition); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskParticipant([property: JsonRequired] Guid ActorId, [property: JsonRequired] string Role)
{ public override string ToString() => nameof(RiskParticipant); }

// AssessmentDraft (50.01 §5). Drafts may be incomplete (POL-DEFAULT-01); submit applies
// the conditional requirements. StopWorkRequired=null is UNKNOWN, never "no stop-work".
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskAssessmentContent(string? Title, string? OperationContext, string? WorkType, string? WorkScope, Guid? MethodId,
    [property: JsonRequired] RiskHazard[] Hazards, bool? StopWorkRequired, [property: JsonRequired] RiskStopCondition[] StopWorkConditions,
    [property: JsonRequired] RiskParticipant[] Participants)
{ public override string ToString() => nameof(RiskAssessmentContent); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateControl([property: JsonRequired] Guid Id, [property: JsonRequired] string Description,
    [property: JsonRequired] string Kind, string? OwnerRole, [property: JsonRequired] bool Required, [property: JsonRequired] string[] BarrierRefs)
{ public override string ToString() => nameof(RiskTemplateControl); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateHazard([property: JsonRequired] Guid Id, string? Step, [property: JsonRequired] string Description,
    [property: JsonRequired] string[] Consequences, [property: JsonRequired] RiskTemplateControl[] DefaultControls, [property: JsonRequired] string[] BarrierRefs)
{ public override string ToString() => nameof(RiskTemplateHazard); }

// RiskTemplateDraft (50.01 paragraph 59). Arrays follow the JSA shape without any
// actual/verified, rating, signature or participant-confirmation field.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateContent(string? Title, string? WorkType, [property: JsonRequired] string[] VesselKinds,
    [property: JsonRequired] string[] RequiredBarriers, [property: JsonRequired] RiskTemplateHazard[] Hazards,
    [property: JsonRequired] string[] SourceRefs, string? ApprovalOwner, string? ChangeReason)
{ public override string ToString() => nameof(RiskTemplateContent); }

public sealed record RiskResult(string State, string? Result, int? Rank);
public sealed record RiskTransition(string? ToState, string? Failure, string[] Blockers);

public static class RiskPolicy
{
    public const string SourceVersion = "50.01-Rev3:6eee8e317f011b1f9b4a98301230fb422e37a05e83d407fe216594eed7c83507:p318-430";
    public const string Draft = "DRAFT", InReview = "IN_REVIEW", Returned = "RETURNED", Approved = "APPROVED";
    public const string Published = "PUBLISHED", Superseded = "SUPERSEDED";
    public static readonly string[] AssessmentStates = [Draft, InReview, Returned, Approved];
    public static readonly string[] TemplateStates = [Draft, InReview, Returned, Published, Superseded];
    public static readonly string[] MethodStates = [Draft, Published];
    public static readonly string[] WorkTypes = ["MAINTENANCE", "HOT_WORK", "ENCLOSED_SPACE", "BUNKERING", "CARGO", "NAVIGATION", "TOWAGE", "OTHER"];
    public static readonly string[] VesselKinds = ["SHIP", "TUG"];
    public static readonly string[] ControlKinds = ["EXISTING", "PROPOSED"];
    public static readonly string[] ParticipantRoles = ["PREPARER", "ASSESSOR", "CONTROL_OWNER", "PARTICIPANT"];
    public static readonly string[] AssessmentDecisions = ["APPROVE", "RETURN"];
    public static readonly string[] TemplateDecisions = ["PUBLISH", "RETURN"];
    public static readonly string[] AckChoices = ["ACK", "DISPUTE"];
    public static readonly string[] SuggestionStates = ["OPEN", "ACCEPTED", "REJECTED"];
    public static readonly string[] LinkKinds = ["TugJob", "WorkOrder", "Defect", "OperationalEvent"];
    public const int MaxHazards = 30, MaxControls = 30, MaxConsequences = 20, MaxRefs = 20, MaxStops = 30, MaxParticipants = 50, MaxLinks = 20;

    public static bool TextShort(string? value) => value is { Length: >= 1 and <= 200 } && value.Trim().Length > 0 && !value.Any(char.IsControl);
    public static bool TextLong(string? value) => value is { Length: >= 1 and <= 4000 } && value.Trim().Length > 0 &&
        !value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
    public static bool Reason(string? value) => value is { Length: >= 10 and <= 1000 } && value.Trim().Length >= 10 &&
        !value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
    private static bool Code(string? value) => value is { Length: >= 1 and <= 40 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');
    private static bool OptionalShort(string? value) => value == null || TextShort(value);
    private static bool OptionalLong(string? value) => value == null || TextLong(value);
    private static bool Refs(string[]? values, int max = MaxRefs) => values != null && values.Length <= max && values.All(TextShort) &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Length;

    // Structural bounds for a storable draft. Completeness is a separate submit check.
    public static bool ValidDraft(RiskAssessmentContent? c)
    {
        if (c?.Hazards == null || c.StopWorkConditions == null || c.Participants == null) return false;
        if (!OptionalShort(c.Title) || !OptionalLong(c.OperationContext) || !OptionalLong(c.WorkScope) ||
            c.WorkType != null && !WorkTypes.Contains(c.WorkType) || c.MethodId == Guid.Empty) return false;
        if (c.Hazards.Length > MaxHazards || c.StopWorkConditions.Length > MaxStops || c.Participants.Length > MaxParticipants) return false;
        var ids = new HashSet<Guid>();
        foreach (var h in c.Hazards)
        {
            if (h == null || h.Id == Guid.Empty || !ids.Add(h.Id) || !OptionalShort(h.Step) || !TextLong(h.Description) ||
                h.Consequences == null || h.Consequences.Length > MaxConsequences || !h.Consequences.All(TextLong) ||
                !Refs(h.BarrierRefs) || !Rating(h.Initial) || !Rating(h.Residual) || h.Controls == null || h.Controls.Length > MaxControls) return false;
            foreach (var control in h.Controls)
                if (control == null || control.Id == Guid.Empty || !ids.Add(control.Id) || !TextLong(control.Description) ||
                    !ControlKinds.Contains(control.Kind) || !OptionalShort(control.Owner) || !Refs(control.BarrierRefs)) return false;
        }
        foreach (var s in c.StopWorkConditions) if (s == null || s.Id == Guid.Empty || !ids.Add(s.Id) || !TextLong(s.Description)) return false;
        if (c.StopWorkRequired == false && c.StopWorkConditions.Length > 0) return false;
        return c.Participants.All(p => p != null && p.ActorId != Guid.Empty && ParticipantRoles.Contains(p.Role)) &&
            c.Participants.Select(p => (p.ActorId, p.Role)).Distinct().Count() == c.Participants.Length;
    }
    private static bool Rating(RiskRating? r) => r != null && (r.Likelihood == null || Code(r.Likelihood)) &&
        (r.Severity == null || Code(r.Severity)) && OptionalLong(r.Reason);

    // 50.01 paragraph 29: every L x S pair exactly once, closed result codes, rank >= 0.
    public static string? MethodFailure(RiskMethodContent? m)
    {
        if (m == null || !Code(m.Code) || !TextShort(m.Title) || !Reason(m.ChangeReason) || !Refs(m.SourceRefs, 50) || m.SourceRefs.Length == 0) return "invalid_request";
        bool Codes(RiskMatrixCode[]? values) => values is { Length: >= 2 and <= 20 } && values.All(v => v != null && Code(v.Code) && TextShort(v.Label)) &&
            values.Select(v => v.Code).Distinct(StringComparer.Ordinal).Count() == values.Length;
        if (!Codes(m.Likelihoods) || !Codes(m.Severities) || m.Cells == null) return "method_matrix_incomplete";
        if (m.Cells.Any(c => c == null || !Code(c.Result) || c.Rank < 0) || m.Cells.Length != m.Likelihoods.Length * m.Severities.Length ||
            m.Cells.Select(c => (c.Likelihood, c.Severity)).Distinct().Count() != m.Cells.Length ||
            m.Cells.Any(c => !m.Likelihoods.Any(l => l.Code == c.Likelihood) || !m.Severities.Any(s => s.Code == c.Severity)) ||
            m.Cells.Select(c => c.Result).Distinct(StringComparer.Ordinal).Count() > 100) return "method_matrix_incomplete";
        return null;
    }

    // Lookup only. No method, an unpublished method or a missing/unknown code is NOT_ASSESSED.
    public static RiskResult Rate(RiskMethodContent? publishedMethod, RiskRating? rating)
    {
        if (publishedMethod == null) return new("METHOD_UNCONFIGURED", null, null);
        if (rating?.Likelihood == null || rating.Severity == null) return new("NOT_ASSESSED", null, null);
        var cell = publishedMethod.Cells.SingleOrDefault(c => c.Likelihood == rating.Likelihood && c.Severity == rating.Severity);
        return cell == null ? new("NOT_ASSESSED", null, null) : new("ASSESSED", cell.Result, cell.Rank);
    }

    // 50.01 paragraph 74 (submit completeness). Codes only, never content.
    public static string[] SubmitBlockers(RiskAssessmentContent c, RiskMethodContent? publishedMethod, int linkCount)
    {
        var blockers = new List<string>();
        if (c.Title == null) blockers.Add("title_missing");
        if (c.OperationContext == null) blockers.Add("operation_context_missing");
        if (c.WorkType == null) blockers.Add("work_type_missing");
        if (c.MethodId == null || publishedMethod == null) blockers.Add("risk_method_unconfigured");
        if (c.Hazards.Length == 0) blockers.Add("hazard_missing");
        if (c.StopWorkRequired == null) blockers.Add("stop_work_unknown");
        if (c.StopWorkRequired == true && c.StopWorkConditions.Length == 0) blockers.Add("stop_work_conditions_missing");
        if (!c.Participants.Any(p => p.Role is "PREPARER" or "ASSESSOR")) blockers.Add("preparer_or_assessor_missing");
        if (linkCount == 0 && c.WorkScope == null) blockers.Add("job_reference_or_scope_missing");
        foreach (var h in c.Hazards)
        {
            if (h.Consequences.Length == 0) blockers.Add("consequence_missing");
            if (publishedMethod != null && Rate(publishedMethod, h.Initial).State != "ASSESSED") blockers.Add("initial_risk_not_assessed");
            if (publishedMethod != null && Rate(publishedMethod, h.Residual).State != "ASSESSED") blockers.Add("residual_risk_not_assessed");
            if (h.Controls.Length == 0) blockers.Add("control_missing");
            if (h.Controls.Any(x => x.Owner == null)) blockers.Add("control_owner_missing");
        }
        return [.. blockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    public static RiskTransition Submit(string state, string[] blockers) =>
        state is not (Draft or Returned) ? new(null, "invalid_state", []) :
        blockers.Length > 0 ? new(null, "risk_record_incomplete", blockers) : new(InReview, null, []);

    // An approver is never an author of the reviewed cycle nor its submitter (SoD).
    public static RiskTransition Review(string state, string decision, Guid approver, IReadOnlyCollection<Guid> authors, Guid? submitter, string? reason)
    {
        if (state != InReview) return new(null, "invalid_state", []);
        if (!AssessmentDecisions.Contains(decision) || !Reason(reason)) return new(null, "invalid_reason", []);
        if (approver == Guid.Empty || authors.Contains(approver) || submitter == approver) return new(null, "separation_of_duties", []);
        return new(decision == "APPROVE" ? Approved : Returned, null, []);
    }

    public static RiskTransition Amend(string state, string? reason) =>
        state != Approved ? new(null, "invalid_state", []) : !Reason(reason) ? new(null, "invalid_reason", []) : new(Draft, null, []);

    // Each person acknowledges for themselves, on the exact approved revision.
    public static string? AcknowledgementFailure(string state, long? approvedRevision, long revision, RiskAssessmentContent c, Guid actor, string choice, string? reason)
    {
        if (state != Approved || approvedRevision != revision) return "invalid_state";
        if (!AckChoices.Contains(choice) || choice == "DISPUTE" && !Reason(reason) || reason != null && !Reason(reason)) return "invalid_reason";
        return c.Participants.Any(p => p.ActorId == actor) ? null : "acknowledgment_role_required";
    }

    // PENDING until every required participant ACKed the current approved revision;
    // a dispute is never closed by someone else's ACK. NOT_APPLICABLE before approval.
    public static string AcknowledgementState(string state, RiskAssessmentContent c, IEnumerable<(Guid Actor, string Choice)> current)
    {
        if (state != Approved) return "NOT_APPLICABLE";
        var rows = current.ToArray();
        if (rows.Any(r => r.Choice == "DISPUTE")) return "DISPUTED";
        var required = c.Participants.Select(p => p.ActorId).Distinct().ToArray();
        return required.Length > 0 && required.All(a => rows.Any(r => r.Actor == a && r.Choice == "ACK")) ? "COMPLETE" : "PENDING";
    }

    public static bool ValidTemplateDraft(RiskTemplateContent? t)
    {
        if (t?.Hazards == null || t.VesselKinds == null || t.RequiredBarriers == null || t.SourceRefs == null) return false;
        if (!OptionalShort(t.Title) || t.WorkType != null && !WorkTypes.Contains(t.WorkType) || !OptionalShort(t.ApprovalOwner) ||
            t.ChangeReason != null && !Reason(t.ChangeReason) || !Refs(t.RequiredBarriers) || !Refs(t.SourceRefs, 50) ||
            t.VesselKinds.Length > 2 || t.VesselKinds.Any(k => !VesselKinds.Contains(k)) || t.VesselKinds.Distinct().Count() != t.VesselKinds.Length ||
            t.Hazards.Length > MaxHazards) return false;
        var ids = new HashSet<Guid>();
        foreach (var h in t.Hazards)
        {
            if (h == null || h.Id == Guid.Empty || !ids.Add(h.Id) || !OptionalShort(h.Step) || !TextLong(h.Description) || h.Consequences == null ||
                h.Consequences.Length > MaxConsequences || !h.Consequences.All(TextLong) || !Refs(h.BarrierRefs) || h.DefaultControls == null ||
                h.DefaultControls.Length > MaxControls) return false;
            foreach (var x in h.DefaultControls)
                if (x == null || x.Id == Guid.Empty || !ids.Add(x.Id) || !TextLong(x.Description) || !ControlKinds.Contains(x.Kind) ||
                    !OptionalShort(x.OwnerRole) || !Refs(x.BarrierRefs)) return false;
        }
        return true;
    }

    public static string[] TemplateBlockers(RiskTemplateContent t, long revision)
    {
        var blockers = new List<string>();
        if (t.Title == null) blockers.Add("title_missing");
        if (t.WorkType == null) blockers.Add("work_type_missing");
        if (t.ApprovalOwner == null) blockers.Add("approval_owner_missing");
        if (t.Hazards.Length == 0) blockers.Add("hazard_missing");
        if (t.Hazards.Any(h => h.Consequences.Length == 0)) blockers.Add("consequence_missing");
        if (t.Hazards.Any(h => h.DefaultControls.Length == 0)) blockers.Add("control_missing");
        if (revision > 1 && t.ChangeReason == null) blockers.Add("change_reason_missing");
        return [.. blockers.Order(StringComparer.Ordinal)];
    }

    public static RiskTransition TemplateSubmit(string state, string[] blockers) => Submit(state, blockers);

    // HSEQ publication is a separate human record; the publisher is never an author/submitter.
    public static RiskTransition TemplateReview(string state, string decision, Guid publisher, IReadOnlyCollection<Guid> authors, Guid? submitter, string? reason)
    {
        if (state != InReview) return new(null, "invalid_state", []);
        if (!TemplateDecisions.Contains(decision) || !Reason(reason)) return new(null, "invalid_reason", []);
        if (publisher == Guid.Empty || authors.Contains(publisher) || submitter == publisher) return new(null, "separation_of_duties", []);
        return new(decision == "PUBLISH" ? Published : Returned, null, []);
    }

    // Derived effective view: the latest PUBLISHED revision effective at "now" is CURRENT,
    // future ones SCHEDULED, older ones SUPERSEDED. A future revision never supersedes early.
    public static string EffectiveState(string status, DateTimeOffset? effectiveFrom, long revision, IEnumerable<(long Revision, DateTimeOffset EffectiveFrom)> published, DateTimeOffset now)
    {
        if (status == Superseded) return Superseded;
        if (status != Published || effectiveFrom == null) return "NOT_EFFECTIVE";
        if (effectiveFrom > now) return "SCHEDULED";
        var current = published.Where(p => p.EffectiveFrom <= now).OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.Revision).FirstOrDefault();
        return current.Revision == revision ? "CURRENT" : Superseded;
    }

    // Template application is a COPY into a new draft (50.01 paragraph 59): no rating,
    // signature, participant confirmation, approval or acknowledgement is carried.
    public static RiskAssessmentContent FromTemplate(RiskTemplateContent t, Func<Guid> ids) => new(t.Title, null, t.WorkType, null, null,
        [.. t.Hazards.Select(h => new RiskHazard(ids(), h.Step, h.Description, [.. h.Consequences], new(null, null, null),
            [.. h.DefaultControls.Select(x => new RiskControl(ids(), x.Description, x.Kind, null, x.Required, [.. x.BarrierRefs]))],
            [.. h.BarrierRefs.Concat(t.RequiredBarriers).Distinct(StringComparer.Ordinal).Take(MaxRefs)], new(null, null, null)))],
        null, [], []);

    public static bool AppliesTo(RiskTemplateContent t, bool tug) => t.VesselKinds.Length == 0 || t.VesselKinds.Contains(tug ? "TUG" : "SHIP");
}
