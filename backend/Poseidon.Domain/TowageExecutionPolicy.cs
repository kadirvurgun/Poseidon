using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// DEV-089 / T-TUG-010 (18.01 Backlog row 90): "Timeline, towline times, max load, comms, weather, events, Master
// decision, evidence, verification; shore/AI cannot start/abort operation." 08.02 p133-134 (onboard-authorized
// start/suspend/abort/complete state machine), 10.01 p103-105, 60.02 Rev2 p217-254 (execution ACTIVE|SUSPENDED|
// COMPLETED|ABORTED, server start_time, abort/complete text 20..4000, terminal accepts only evidence/correction,
// verification PENDING|VERIFIED|REJECTED) and p1118-1198 (ExecutionView projections: first accepted towline
// connection, last release, VALID max load samples, communication GOOD|ATTENTION|LOST|UNKNOWN).
// Every observation is a recorded value with its unit; nothing is defaulted, converted or judged against a limit.
public static class TowageExecutionPolicy
{
    public const string SourceVersion = "18.01:row90;08.02:p133-134;10.01:p103-105;60.02Rev2:p217-254,p1118-1198";
    public const string Active = "ACTIVE", Suspended = "SUSPENDED", Completed = "COMPLETED", Aborted = "ABORTED";
    public static readonly string[] States = [Active, Suspended, Completed, Aborted];
    public const string Pending = "PENDING", Verified = "VERIFIED", Rejected = "REJECTED";
    public static readonly string[] VerificationStates = [Pending, Verified, Rejected];
    public static readonly string[] Actions = ["SUSPEND", "RESUME", "ABORT", "COMPLETE"];
    public static readonly string[] VerificationDecisions = [Verified, Rejected];
    // Lifecycle events are written by the server for the start/transition/verification commands only.
    public static readonly string[] LifecycleKinds = ["STARTED", "SUSPENDED", "RESUMED", "ABORTED", "COMPLETED", "VERIFIED", "VERIFICATION_REJECTED"];
    public static readonly string[] RecordableKinds = ["TOWLINE_CONNECTED", "TOWLINE_RELEASED", "LOAD_OBSERVED", "COMMS_CHECK", "WEATHER_OBSERVED",
        "WINCH_STATE", "EMERGENCY_RELEASE_TEST", "POSITION", "EVIDENCE", "NOTE"];
    public static readonly string[] Kinds = [.. LifecycleKinds, .. RecordableKinds];
    // 60.02 p238: a terminal execution accepts only additional evidence or a correction note.
    public static readonly string[] TerminalKinds = ["EVIDENCE", "NOTE"];
    public static readonly string[] LoadUnits = ["t", "kN"];
    public static readonly string[] CommunicationStates = ["GOOD", "ATTENTION", "LOST"];
    public static readonly string[] WinchStates = ["PASS", "FAIL", "UNKNOWN", "CONDITIONAL"];
    public static readonly string[] ReleaseTestResults = ["PASS", "FAIL"];
    public static readonly string[] EvidencePurposes = ["PHOTO", "AIS_OR_TIME", "DOCUMENT"];
    public const int MaxEvents = 2000;
    // Technical device clock tolerance for an observed instant; not an operational parameter.
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    public static bool Short(string? v, int max = 200) => v is { Length: >= 1 } && v.Length <= max && v.Trim().Length > 0 && !v.Any(char.IsControl);
    public static bool Long(string? v, int min = 1, int max = 4000) => v != null && v.Trim().Length >= min && v.Length <= max &&
        !v.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
    public static bool Measure(string? text) => text is { Length: >= 1 and <= 12 } && Regex.IsMatch(text, @"\A[0-9]{1,7}(?:\.[0-9]{1,3})?\z") &&
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value >= 0;
    public static decimal Value(string text) => decimal.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    // Kind-specific shape. Fields a kind does not use must be absent (null); nothing is inferred.
    public static bool ValidEvent(TowageEventInput? e) {
        if (e == null || !RecordableKinds.Contains(e.Kind)) return false;
        bool Only(bool value, bool unit, bool result, bool text, bool reference, bool weather) =>
            (value || e.Value == null) && (unit || e.Unit == null) && (result || e.Result == null) && (text || e.Text == null) &&
            (reference || e.Reference == null) && (weather || e.Weather == null) && (e.Text == null || Long(e.Text)) && (e.Reference == null || Short(e.Reference, 400));
        return e.Kind switch {
            "TOWLINE_CONNECTED" or "TOWLINE_RELEASED" => Only(false, false, false, true, false, false),
            "LOAD_OBSERVED" => Only(true, true, false, true, false, false) && Measure(e.Value) && LoadUnits.Contains(e.Unit),
            "COMMS_CHECK" => Only(false, false, true, true, false, false) && CommunicationStates.Contains(e.Result),
            "WEATHER_OBSERVED" => Only(false, false, false, true, false, true) && ValidWeather(e.Weather),
            "WINCH_STATE" => Only(false, false, true, true, false, false) && WinchStates.Contains(e.Result),
            "EMERGENCY_RELEASE_TEST" => Only(false, false, true, true, false, false) && ReleaseTestResults.Contains(e.Result),
            "POSITION" => Only(false, false, false, true, false, false) && e.Text != null,
            "EVIDENCE" => Only(false, false, true, true, true, false) && EvidencePurposes.Contains(e.Result) && e.Reference != null,
            "NOTE" => Only(false, false, false, true, false, false) && e.Text != null,
            _ => false
        };
    }

    // An observation states what was observed; at least one value, no defaults. Wind speed carries its unit (kn).
    public static bool ValidWeather(TowageWeather? w) => w != null && (w.WindSpeedKn != null || w.WindDirection != null || w.SeaState != null || w.Visibility != null || w.Current != null) &&
        (w.WindSpeedKn == null || Measure(w.WindSpeedKn)) && new[] { w.WindDirection, w.SeaState, w.Visibility, w.Current }.All(x => x == null || Short(x));

    // Observed instant: never in the future (beyond device clock tolerance) and never before the execution start.
    public static bool Chronological(DateTimeOffset occurred, DateTimeOffset startedAt, DateTimeOffset now) =>
        occurred.Offset == TimeSpan.Zero && occurred <= now + ClockSkew && occurred >= startedAt;

    // State machine (60.02 p240-246). ABORT is always available to the Tug Master while the operation runs.
    public static (string? To, string? Failure) Transition(string state, string action, string? reason) {
        string? to = (state, action) switch {
            (Active, "SUSPEND") => Suspended,
            (Suspended, "RESUME") => Active,
            (Active or Suspended, "ABORT") => Aborted,
            (Active, "COMPLETE") => Completed,
            _ => null
        };
        if (!Actions.Contains(action)) return (null, "validation_failed");
        if (to == null) return (null, "invalid_state");
        var ok = action is "ABORT" or "COMPLETE" ? Long(reason, 20) : Long(reason, 10, 1000);
        return ok ? (to, null) : (null, "invalid_reason");
    }

    public static string LifecycleKind(string action) => action switch { "SUSPEND" => "SUSPENDED", "RESUME" => "RESUMED", "ABORT" => "ABORTED", _ => "COMPLETED" };

    // Start gate. Plan approval, briefing issue + acknowledgement and the GO are human records produced elsewhere;
    // this only checks they exist, are current and belong to this job/tug. It never creates or implies a GO.
    public static string[] StartBlockers(TowageStartFacts f) {
        var b = new List<string>();
        // QA S9 N10: only a SUBMITTED job starts; a cancelled, unsubmitted (DRAFT) or unrecognised status is refused.
        if (f.JobStatus == TugJobLifecycle.Cancelled) b.Add("job_cancelled");
        else if (f.JobStatus != TugJobLifecycle.Submitted) b.Add("job_not_submitted");
        if (f.ExecutionExists) b.Add("execution_already_started");
        if (!f.PlanPresent) b.Add("towage_plan_missing");
        else {
            if (!f.PlanApprovedCurrent) b.Add("towage_plan_not_approved");
            if (!f.PlanRequestCurrent) b.Add("towage_plan_request_changed");
            if (!f.TugInPlan) b.Add("tug_not_in_approved_plan");
        }
        if (f.BriefingChecks.Length == 0) b.Add("briefing:TowageBriefingMissing");
        foreach (var c in f.BriefingChecks.Where(c => c.Result != TugReadinessEngine.Pass)) b.Add("briefing:" + c.Reason);
        if (!f.AssignmentActive) b.Add("tug_assignment_missing");
        else if (f.Decision == null) b.Add("readiness_go_missing");
        else if (f.Decision != TugReadinessDecisionPolicy.Go) b.Add("readiness_no_go_recorded");
        else {
            if (!f.DecisionRevisionCurrent) b.Add("readiness_go_assignment_changed");
            if (!f.DecisionHashCurrent) b.Add("readiness_go_stale");
            else if (!f.AssessmentReady) b.Add("readiness_not_ready");
        }
        return [.. b.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    // Completion record (60.02 p230/p246): end position and a note; a towline still connected is not hidden.
    public static string[] CompleteBlockers(string? endPosition, TowageLedgerFacts ledger) {
        var b = new List<string>();
        if (!Short(endPosition, 400)) b.Add("end_position_missing");
        if (ledger.TowlineConnected) b.Add("towline_still_connected");
        return [.. b.Order(StringComparer.Ordinal)];
    }

    // Independent verification: never the person who started or ended the operation.
    public static string? VerificationFailure(string state, string verificationState, Guid verifier, Guid startedBy, Guid? endedBy, string decision, string? reason) {
        if (state is not (Completed or Aborted) || verificationState != Pending) return "invalid_state";
        if (!VerificationDecisions.Contains(decision) || !Long(reason, 10, 1000)) return "invalid_reason";
        if (verifier == Guid.Empty || verifier == startedBy || verifier == endedBy) return "separation_of_duties";
        return null;
    }

    // ExecutionView projections from the append-only ledger (60.02 p1151-1192).
    public static TowageLedgerFacts Ledger(IReadOnlyList<TowageLedgerEvent> events, DateTimeOffset now) {
        var ordered = events.OrderBy(e => e.Sequence).ToArray();
        DateTimeOffset? firstConnected = null, lastReleased = null; var connected = false;
        var loads = new Dictionary<string, (decimal Max, string Text, int Samples)>(StringComparer.Ordinal);
        string comms = "UNKNOWN", winch = "UNKNOWN", release = "UNKNOWN"; TowageLedgerEvent? weather = null;
        var intervals = new List<(DateTimeOffset From, DateTimeOffset? To)>(); DateTimeOffset? open = null;
        foreach (var e in ordered) {
            switch (e.Kind) {
                case "TOWLINE_CONNECTED": firstConnected ??= e.OccurredAt; connected = true; break;
                case "TOWLINE_RELEASED": if (connected) { lastReleased = e.OccurredAt; connected = false; } break;
                case "LOAD_OBSERVED" when e.Value != null && e.Unit != null && Measure(e.Value):
                    var v = Value(e.Value);
                    loads[e.Unit] = loads.TryGetValue(e.Unit, out var cur) ? (v > cur.Max ? (v, e.Value, cur.Samples + 1) : (cur.Max, cur.Text, cur.Samples + 1)) : (v, e.Value, 1);
                    break;
                case "COMMS_CHECK" when e.Result != null: comms = e.Result; break;
                case "WINCH_STATE" when e.Result != null: winch = e.Result; break;
                case "EMERGENCY_RELEASE_TEST" when e.Result != null: release = e.Result; break;
                case "WEATHER_OBSERVED": weather = e; break;
                case "STARTED" or "RESUMED": open ??= e.OccurredAt; break;
                case "SUSPENDED" or "ABORTED" or "COMPLETED":
                    if (open != null) { intervals.Add((open.Value, e.OccurredAt < open.Value ? open.Value : e.OccurredAt)); open = null; }
                    break;
            }
        }
        if (open != null) intervals.Add((open.Value, null));
        var maxLoads = loads.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new TowageMaxLoad(x.Value.Text, x.Key, x.Value.Samples)).ToArray();
        var active = intervals.Sum(i => Math.Max(0, ((i.To ?? now) - i.From).TotalSeconds));
        return new(firstConnected, connected ? null : lastReleased, connected, maxLoads, comms, winch, release, weather?.Sequence,
            intervals.Select(i => new TowageActiveInterval(i.From, i.To)).ToArray(), (long)active);
    }

    public static string EventHash(string previous, Guid execution, long sequence, string kind, DateTimeOffset occurred, Guid actor, string payloadJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', "poseidon.towage-execution-event.v1", previous, execution.ToString("D"),
            sequence.ToString(CultureInfo.InvariantCulture), kind, occurred.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), actor.ToString("D"), payloadJson))));

    public static string HeadHash(Guid vessel, Guid id, long revision, string state, string verification, long sequence, string lastEventHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', "poseidon.towage-execution.v1", vessel.ToString("D"), id.ToString("D"),
            revision.ToString(CultureInfo.InvariantCulture), state, verification, sequence.ToString(CultureInfo.InvariantCulture), lastEventHash))));

    public static string PayloadJson(TowageEventInput? e, string? reason = null, string? position = null) =>
        JsonSerializer.Serialize(new TowageEventPayload(e?.Value, e?.Unit, e?.Result, e?.Text, e?.Reference, e?.Weather, reason, position), FieldOpsJson.Options);
    public static TowageEventPayload Payload(string json) => JsonSerializer.Deserialize<TowageEventPayload>(json, FieldOpsJson.Options) ?? throw new InvalidDataException("towage_event_unavailable");
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageWeather(string? WindSpeedKn, string? WindDirection, string? SeaState, string? Visibility, string? Current)
{ public override string ToString() => nameof(TowageWeather); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowageEventInput([property: JsonRequired] string Kind, [property: JsonRequired] DateTimeOffset OccurredAt, string? Value, string? Unit,
    string? Result, string? Text, string? Reference, TowageWeather? Weather)
{ public override string ToString() => nameof(TowageEventInput); }
public sealed record TowageEventPayload(string? Value, string? Unit, string? Result, string? Text, string? Reference, TowageWeather? Weather, string? Reason, string? Position)
{ public override string ToString() => nameof(TowageEventPayload); }

public sealed record TowageStartFacts(string JobStatus, bool ExecutionExists, bool PlanPresent, bool PlanApprovedCurrent, bool PlanRequestCurrent, bool TugInPlan,
    TugReadinessCheck[] BriefingChecks, bool AssignmentActive, string? Decision, bool DecisionRevisionCurrent, bool DecisionHashCurrent,
    bool AssessmentReady);
public sealed record TowageLedgerEvent(long Sequence, string Kind, DateTimeOffset OccurredAt, string? Value, string? Unit, string? Result);
public sealed record TowageMaxLoad(string Value, string Unit, int Samples);
public sealed record TowageActiveInterval(DateTimeOffset From, DateTimeOffset? To);
public sealed record TowageLedgerFacts(DateTimeOffset? TowlineConnectedAt, DateTimeOffset? TowlineReleasedAt, bool TowlineConnected, TowageMaxLoad[] MaxTowLoad,
    string CommunicationState, string WinchState, string EmergencyReleaseTested, long? WeatherEventSequence, TowageActiveInterval[] ActiveIntervals, long ActiveSeconds);

public static class FieldOpsJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
}
