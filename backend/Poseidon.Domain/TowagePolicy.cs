using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-087 / T-TUG-008 (18.01 Backlog row 88): "Plan has tug roles, configuration, limits, communication,
// emergency release, contingency, snap-back controls and approval state." 10.01 paragraph 98-99 (configuration,
// BP, route/area, limits, comms, emergency/contingency), 08.02 paragraph 117-118.
// Section naming follows published harbour towage codes of practice (e.g. PLA Ship Towage Code of Practice,
// Clydeport/Peel Ports Towage Guidelines: tug positions and towing method, master/pilot/tug master information
// exchange, VHF working and standby channels, environmental limits, emergency release, contingency, snap-back
// zones). Every limit value is a plan INPUT the planner states with its source; nothing here is a default,
// a recommended value or an expert-approved threshold.
public static class TowagePlanPolicy
{
    public const string SourceVersion = "18.01:row88;10.01:p98-99;08.02:p117-118;harbour-towage-codes";
    public const string Draft = "DRAFT", InReview = "IN_REVIEW", Returned = "RETURNED", Approved = "APPROVED";
    public static readonly string[] States = [Draft, InReview, Returned, Approved];
    public static readonly string[] Positions = ["CENTRE_LEAD_FORWARD", "CENTRE_LEAD_AFT", "PORT_BOW", "STARBOARD_BOW", "PORT_QUARTER",
        "STARBOARD_QUARTER", "ALONGSIDE_PORT", "ALONGSIDE_STARBOARD", "ESCORT", "STANDBY"];
    public static readonly string[] Methods = ["TOWING_ON_LINE", "PUSH_PULL", "PUSHING_ONLY", "MADE_FAST_ALONGSIDE", "ESCORT"];
    public static readonly string[] LineSources = ["TUG_LINE", "SHIP_LINE"];
    // Each parameter has exactly one server-defined unit; the value itself always comes from the plan author.
    public static readonly IReadOnlyDictionary<string, string> LimitUnits = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["WIND_SPEED"] = "kn", ["SIGNIFICANT_WAVE_HEIGHT"] = "m", ["CURRENT"] = "kn", ["VISIBILITY"] = "nm",
        ["SPEED_THROUGH_WATER"] = "kn", ["MIN_BOLLARD_PULL_MARGIN"] = "t" };
    public static readonly string[] LimitParameters = [.. LimitUnits.Keys];
    public static readonly string[] Scenarios = ["TUG_PROPULSION_LOSS", "SHIP_PROPULSION_OR_STEERING_LOSS", "TOWLINE_PARTING", "COMMUNICATION_LOSS",
        "GIRTING_OR_CAPSIZE_RISK", "LIMITS_EXCEEDED", "PERSON_OVERBOARD", "FIRE_OR_FLOODING", "OTHER"];
    public static readonly string[] Decisions = ["APPROVE", "RETURN"];
    public const int MaxTugs = 12, MaxLimits = 12, MaxContingencies = 30, MaxControls = 30;

    public static bool NeedsLine(string? method) => method is "TOWING_ON_LINE" or "PUSH_PULL" or "MADE_FAST_ALONGSIDE" or "ESCORT";

    public static bool Short(string? v) => v is { Length: >= 1 and <= 200 } && v.Trim().Length > 0 && !v.Any(char.IsControl);
    public static bool Long(string? v) => v is { Length: >= 1 and <= 4000 } && v.Trim().Length > 0 && !v.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
    public static bool Reason(string? v) => RiskPolicy.Reason(v);
    private static bool OptShort(string? v) => v == null || Short(v);
    private static bool OptLong(string? v) => v == null || Long(v);
    public static bool TryValue(string? text, out decimal value) {
        value = 0;
        return text is { Length: >= 1 and <= 12 } && System.Text.RegularExpressions.Regex.IsMatch(text, @"\A[0-9]{1,6}(?:\.[0-9]{1,2})?\z") &&
            decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    // Structural bounds for a storable draft (POL-DEFAULT-01: incomplete drafts are allowed; submit checks completeness).
    public static bool ValidDraft(TowagePlanContent? c) {
        if (c?.Tugs == null || c.Limits == null || c.Communication == null || c.EmergencyRelease == null || c.Contingencies == null || c.SnapBack?.Controls == null) return false;
        if (c.Tugs.Length > MaxTugs || c.Limits.Length > MaxLimits || c.Contingencies.Length > MaxContingencies || c.SnapBack.Controls.Length > MaxControls) return false;
        if (!OptLong(c.InformationExchange)) return false;
        if (c.Tugs.Any(t => t == null || t.TugId == Guid.Empty || t.Position != null && !Positions.Contains(t.Position) || t.Method != null && !Methods.Contains(t.Method) ||
            t.LineSource != null && !LineSources.Contains(t.LineSource) || !OptShort(t.ConnectionPoint) || t.TowlineItemId == Guid.Empty || t.EmergencyReleaseItemId == Guid.Empty)) return false;
        if (c.Tugs.Select(t => t.TugId).Distinct().Count() != c.Tugs.Length) return false;
        if (c.Limits.Any(l => l == null || !LimitParameters.Contains(l.Parameter) || l.Value != null && !TryValue(l.Value, out _) || !OptShort(l.SourceRef))) return false;
        if (c.Limits.Select(l => l.Parameter).Distinct(StringComparer.Ordinal).Count() != c.Limits.Length) return false;
        var m = c.Communication;
        if (!OptShort(m.PrimaryChannel) || !OptShort(m.StandbyChannel) || !OptShort(m.WorkingLanguage) || !OptLong(m.LostCommunicationProcedure)) return false;
        if (!OptLong(c.EmergencyRelease.Procedure) || !OptShort(c.EmergencyRelease.OrderedBy)) return false;
        var ids = new HashSet<Guid>();
        if (c.Contingencies.Any(x => x == null || x.Id == Guid.Empty || !ids.Add(x.Id) || !Scenarios.Contains(x.Scenario) || !Long(x.Action))) return false;
        return c.SnapBack.Controls.All(x => x != null && x.Id != Guid.Empty && ids.Add(x.Id) && Long(x.Description));
    }

    // Submit completeness (codes only). assignedTugs: tugs with a current DEV-083 proposal for this job.
    public static string[] SubmitBlockers(TowagePlanContent c, IReadOnlyCollection<Guid> assignedTugs) {
        var b = new List<string>();
        if (c.Tugs.Length == 0) b.Add("tug_roles_missing");
        if (c.Tugs.Count(t => t.Coordinating) != 1) b.Add("coordinating_tug_required");
        foreach (var t in c.Tugs) {
            if (!assignedTugs.Contains(t.TugId)) b.Add("tug_assignment_missing");
            if (t.Position == null) b.Add("tug_position_missing");
            if (t.Method == null) b.Add("towing_method_missing");
            if (NeedsLine(t.Method)) {
                if (t.LineSource == null) b.Add("towline_source_missing");
                if (t.ConnectionPoint == null) b.Add("connection_point_missing");
                if (t.EmergencyReleaseItemId == null) b.Add("emergency_release_equipment_unlinked");
            }
        }
        if (c.InformationExchange == null) b.Add("information_exchange_missing");
        if (c.Limits.Length == 0) b.Add("limits_missing");
        if (c.Limits.Any(l => l.Value == null)) b.Add("limit_value_missing");
        if (c.Limits.Any(l => l.SourceRef == null)) b.Add("limit_source_missing");
        if (c.Communication.PrimaryChannel == null) b.Add("primary_channel_missing");
        if (c.Communication.StandbyChannel == null) b.Add("standby_channel_missing");
        if (c.Communication.WorkingLanguage == null) b.Add("working_language_missing");
        if (c.Communication.LostCommunicationProcedure == null) b.Add("lost_communication_procedure_missing");
        if (c.EmergencyRelease.Procedure == null) b.Add("emergency_release_procedure_missing");
        if (c.EmergencyRelease.OrderedBy == null) b.Add("emergency_release_authority_missing");
        if (c.Contingencies.Length == 0) b.Add("contingency_missing");
        if (c.SnapBack.ZonesMarked == null) b.Add("snap_back_zone_unknown");
        if (c.SnapBack.Controls.Length == 0) b.Add("snap_back_controls_missing");
        return [.. b.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    public static bool CommunicationComplete(TowageCommunication m) => m.PrimaryChannel != null && m.StandbyChannel != null && m.WorkingLanguage != null && m.LostCommunicationProcedure != null;

    public static RiskTransition Submit(string state, string[] blockers) =>
        state is not (Draft or Returned) ? new(null, "invalid_state", []) : blockers.Length > 0 ? new(null, "towage_record_incomplete", blockers) : new(InReview, null, []);

    // Master/Tug Master approval is a human record; the approver is never an author of the cycle or its submitter.
    public static RiskTransition Review(string state, string decision, Guid approver, IReadOnlyCollection<Guid> authors, Guid? submitter, string? reason) {
        if (state != InReview) return new(null, "invalid_state", []);
        if (!Decisions.Contains(decision) || !Reason(reason)) return new(null, "invalid_reason", []);
        if (approver == Guid.Empty || authors.Contains(approver) || submitter == approver) return new(null, "separation_of_duties", []);
        return new(decision == "APPROVE" ? Approved : Returned, null, []);
    }
    public static RiskTransition Amend(string state, string? reason) =>
        state is not (Approved or InReview) ? new(null, "invalid_state", []) : !Reason(reason) ? new(null, "invalid_reason", []) : new(Draft, null, []);

    // DEV-084 communication input. Only a current approved plan that covers the tug and still matches the
    // request revision is PASS; everything else stays UNKNOWN (never assumed).
    public static TugReadinessCheck Communication(TowagePlanFacts? plan, Guid tug, long jobRevision) {
        if (plan == null) return new("communication", TugReadinessEngine.Unknown, "CommunicationSourceUnavailable");
        if (plan.State != Approved || plan.ApprovedRevision != plan.Revision) return new("communication", TugReadinessEngine.Unknown, "TowagePlanNotApproved", true, plan.Id, plan.Revision);
        if (plan.JobRevision != jobRevision) return new("communication", TugReadinessEngine.Unknown, "TowagePlanRequestChanged", true, plan.Id, plan.Revision);
        if (plan.Content.Tugs.All(t => t.TugId != tug)) return new("communication", TugReadinessEngine.Unknown, "TugNotInApprovedTowagePlan", true, plan.Id, plan.Revision);
        if (!CommunicationComplete(plan.Content.Communication)) return new("communication", TugReadinessEngine.Unknown, "TowagePlanCommunicationIncomplete", true, plan.Id, plan.Revision);
        return new("communication", TugReadinessEngine.Pass, "ApprovedTowagePlanCommunication", true, plan.Id, plan.Revision);
    }

    public static string Hash(string kind, Guid vessel, Guid id, long revision, string state, long jobRevision, string contentJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', "poseidon." + kind + ".v1", vessel.ToString("D"), id.ToString("D"),
            revision.ToString(CultureInfo.InvariantCulture), state, jobRevision.ToString(CultureInfo.InvariantCulture), contentJson))));
}

public sealed record TowagePlanFacts(Guid Id, long Revision, string State, long? ApprovedRevision, long JobRevision, TowagePlanContent Content);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageTugRole([property: JsonRequired] Guid TugId, string? Position, string? Method, [property: JsonRequired] bool Coordinating,
    string? LineSource, string? ConnectionPoint, Guid? TowlineItemId, Guid? EmergencyReleaseItemId)
{ public override string ToString() => nameof(TowageTugRole); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageLimit([property: JsonRequired] string Parameter, string? Value, string? SourceRef)
{ public override string ToString() => nameof(TowageLimit); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageCommunication(string? PrimaryChannel, string? StandbyChannel, string? WorkingLanguage, string? LostCommunicationProcedure)
{ public override string ToString() => nameof(TowageCommunication); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageEmergencyRelease(string? Procedure, string? OrderedBy)
{ public override string ToString() => nameof(TowageEmergencyRelease); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageContingency([property: JsonRequired] Guid Id, [property: JsonRequired] string Scenario, [property: JsonRequired] string Action)
{ public override string ToString() => nameof(TowageContingency); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageItem([property: JsonRequired] Guid Id, [property: JsonRequired] string Description)
{ public override string ToString() => nameof(TowageItem); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageSnapBack(bool? ZonesMarked, [property: JsonRequired] TowageItem[] Controls)
{ public override string ToString() => nameof(TowageSnapBack); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowagePlanContent([property: JsonRequired] TowageTugRole[] Tugs, string? InformationExchange, [property: JsonRequired] TowageLimit[] Limits,
    [property: JsonRequired] TowageCommunication Communication, [property: JsonRequired] TowageEmergencyRelease EmergencyRelease,
    [property: JsonRequired] TowageContingency[] Contingencies, [property: JsonRequired] TowageSnapBack SnapBack)
{ public override string ToString() => nameof(TowagePlanContent); }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageParticipant([property: JsonRequired] Guid ActorId, [property: JsonRequired] string Role, Guid? TugId)
{ public override string ToString() => nameof(TowageParticipant); }
// DEV-088 briefing content: conditions observed at the briefing (not a weather source), hazards with
// preventive (threat-side) and mitigating (consequence-side) barriers, stop-work authority and triggers,
// emergency arrangements, the linked DEV-105 risk assessment and the participants who must acknowledge.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageBriefingContent(string? Weather, string? Current, string? Visibility, [property: JsonRequired] TowageItem[] Hazards,
    [property: JsonRequired] TowageItem[] PreventiveBarriers, [property: JsonRequired] TowageItem[] MitigatingBarriers, string? StopWorkAuthority,
    [property: JsonRequired] TowageItem[] StopWorkTriggers, string? EmergencyArrangements, Guid? RiskAssessmentId, [property: JsonRequired] TowageParticipant[] Participants)
{ public override string ToString() => nameof(TowageBriefingContent); }

public sealed record TowageBriefingFacts(Guid Id, long Revision, string State, long? PlanRevision, long? RiskRevision, TowageBriefingContent Content,
    (Guid Actor, string Choice)[] CurrentAcknowledgements);
public sealed record TowageRiskFacts(Guid Id, string State, long Revision, long? ApprovedRevision);

// DEV-088 / T-TUG-009 (18.01 Backlog row 89): "Risk state + preventive/mitigating barriers + stop-work + emergency +
// participants acknowledgement; incomplete briefing blocks READY." 10.01 paragraph 101-102, 08.02 paragraph 120-121.
public static class TowageBriefingPolicy
{
    public const string SourceVersion = "18.01:row89;10.01:p101-102;08.02:p120-121";
    public const string Draft = "DRAFT", Issued = "ISSUED";
    public static readonly string[] States = [Draft, Issued];
    public static readonly string[] Roles = ["TUG_MASTER", "MASTER", "PILOT", "MATE", "DECK_CREW", "ENGINEER", "OTHER"];
    public static readonly string[] AckChoices = ["ACK", "DISPUTE"];
    public const int MaxItems = 30, MaxParticipants = 50;

    public static bool ValidDraft(TowageBriefingContent? c) {
        if (c?.Hazards == null || c.PreventiveBarriers == null || c.MitigatingBarriers == null || c.StopWorkTriggers == null || c.Participants == null) return false;
        bool Opt(string? v) => v == null || TowagePlanPolicy.Long(v);
        if (!Opt(c.Weather) || !Opt(c.Current) || !Opt(c.Visibility) || !Opt(c.StopWorkAuthority) || !Opt(c.EmergencyArrangements) || c.RiskAssessmentId == Guid.Empty) return false;
        var lists = new[] { c.Hazards, c.PreventiveBarriers, c.MitigatingBarriers, c.StopWorkTriggers };
        if (lists.Any(l => l.Length > MaxItems) || c.Participants.Length > MaxParticipants) return false;
        var ids = new HashSet<Guid>();
        if (lists.SelectMany(l => l).Any(x => x == null || x.Id == Guid.Empty || !ids.Add(x.Id) || !TowagePlanPolicy.Long(x.Description))) return false;
        return c.Participants.All(p => p != null && p.ActorId != Guid.Empty && Roles.Contains(p.Role) && p.TugId != Guid.Empty) &&
            c.Participants.Select(p => p.ActorId).Distinct().Count() == c.Participants.Length;
    }

    // Issue completeness. The plan must be the current approved plan and the risk assessment approved.
    public static string[] IssueBlockers(TowageBriefingContent c, TowagePlanFacts? plan, TowageRiskFacts? risk) {
        var b = new List<string>();
        if (c.Weather == null) b.Add("weather_missing");
        if (c.Current == null) b.Add("current_missing");
        if (c.Visibility == null) b.Add("visibility_missing");
        if (c.Hazards.Length == 0) b.Add("hazard_missing");
        if (c.PreventiveBarriers.Length == 0) b.Add("preventive_barrier_missing");
        if (c.MitigatingBarriers.Length == 0) b.Add("mitigating_barrier_missing");
        if (c.StopWorkAuthority == null) b.Add("stop_work_authority_missing");
        if (c.StopWorkTriggers.Length == 0) b.Add("stop_work_trigger_missing");
        if (c.EmergencyArrangements == null) b.Add("emergency_arrangements_missing");
        if (c.Participants.Length == 0) b.Add("participants_missing");
        if (plan == null || plan.State != TowagePlanPolicy.Approved || plan.ApprovedRevision != plan.Revision) b.Add("towage_plan_not_approved");
        if (c.RiskAssessmentId == null) b.Add("risk_assessment_unlinked");
        else if (risk == null) b.Add("risk_assessment_unavailable");
        else if (risk.State != RiskPolicy.Approved || risk.ApprovedRevision != risk.Revision) b.Add("risk_assessment_not_approved");
        return [.. b.Order(StringComparer.Ordinal)];
    }

    public static string? AcknowledgementFailure(string state, long revision, long expectedRevision, TowageBriefingContent c, Guid actor, string choice, string? reason) {
        if (state != Issued || revision != expectedRevision) return "invalid_state";
        if (!AckChoices.Contains(choice) || choice == "DISPUTE" && !RiskPolicy.Reason(reason) || reason != null && !RiskPolicy.Reason(reason)) return "invalid_reason";
        return c.Participants.Any(p => p.ActorId == actor) ? null : "acknowledgment_role_required";
    }

    public static string AcknowledgementState(string state, TowageBriefingContent c, IEnumerable<(Guid Actor, string Choice)> current) {
        if (state != Issued) return "NOT_APPLICABLE";
        var rows = current.ToArray();
        if (rows.Any(r => r.Choice == "DISPUTE")) return "DISPUTED";
        return c.Participants.Length > 0 && c.Participants.All(p => rows.Any(r => r.Actor == p.ActorId && r.Choice == "ACK")) ? "COMPLETE" : "PENDING";
    }

    // Risk state of the linked assessment (50.02 high-risk risk_state codes READY|ATTENTION|BLOCKED|REVIEW_REQUIRED).
    public static (string State, string Reason) RiskState(TowageRiskFacts? risk, bool readable, string? riskAckState) {
        if (!readable) return ("REVIEW_REQUIRED", "RiskAccessRequired");
        if (risk == null) return ("BLOCKED", "RiskAssessmentMissing");
        if (risk.State == RiskPolicy.InReview) return ("REVIEW_REQUIRED", "RiskAssessmentInReview");
        if (risk.State != RiskPolicy.Approved || risk.ApprovedRevision != risk.Revision) return ("BLOCKED", "RiskAssessmentNotApproved");
        return riskAckState switch {
            "DISPUTED" => ("BLOCKED", "RiskAcknowledgementDisputed"),
            "COMPLETE" => ("READY", "RiskAssessmentApprovedAndAcknowledged"),
            _ => ("ATTENTION", "RiskAcknowledgementPending")
        };
    }

    // DEV-088 readiness input (category "briefing"). Incomplete briefing is a known FAIL so READY is blocked.
    public static TugReadinessCheck[] Checks(TowageBriefingFacts? b, TowagePlanFacts? plan, TowageRiskFacts? risk, bool riskReadable) {
        if (b == null) return [new("briefing", TugReadinessEngine.Fail, "TowageBriefingMissing")];
        if (b.State != Issued) return [new("briefing", TugReadinessEngine.Fail, "TowageBriefingNotIssued", true, b.Id, b.Revision)];
        var result = new List<TugReadinessCheck>();
        if (plan == null || plan.State != TowagePlanPolicy.Approved || plan.ApprovedRevision != b.PlanRevision)
            result.Add(new("briefing", TugReadinessEngine.Fail, "TowageBriefingPlanChanged", true, b.Id, b.Revision));
        if (!riskReadable) result.Add(new("briefing", TugReadinessEngine.Unknown, "RiskAccessRequired", true, b.Id, b.Revision));
        else if (risk == null || risk.State != RiskPolicy.Approved || risk.ApprovedRevision != b.RiskRevision)
            result.Add(new("briefing", TugReadinessEngine.Fail, "TowageRiskAssessmentChanged", true, risk?.Id, risk?.Revision));
        var ack = AcknowledgementState(b.State, b.Content, b.CurrentAcknowledgements);
        if (ack == "DISPUTED") result.Add(new("briefing", TugReadinessEngine.Fail, "BriefingAcknowledgementDisputed", true, b.Id, b.Revision));
        else if (ack != "COMPLETE") result.Add(new("briefing", TugReadinessEngine.Fail, "BriefingAcknowledgementPending", true, b.Id, b.Revision));
        if (result.Count == 0) result.Add(new("briefing", TugReadinessEngine.Pass, "BriefingIssuedAndAcknowledgedByAllParticipants", true, b.Id, b.Revision));
        return [.. result];
    }

    public static string ContentJson<T>(T content) => JsonSerializer.Serialize(content, TowageJson.Options);
}

public static class TowageJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
}
