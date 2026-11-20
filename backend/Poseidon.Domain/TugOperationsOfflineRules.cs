using System.Globalization;
using System.Text;

namespace Poseidon.Domain;

// DEV-092 / T-TUG-013 (18.01 Backlog row 93): "Job snapshot prefetched; briefing/execution/log durable queue; reconnect
// idempotent; material conflicts explicit." Protocol DEV-033/034/035 (offline receipt, reconciliation, conflict center).
// Pure rules only: which captured field is MATERIAL against the current server record, which human resolution choices
// remain possible and which operations never run offline. Nothing here merges, rebases or resolves on its own.
public static class TugOperationsOfflineRules
{
    public const string SourceVersion = "18.01:row93;63.03:mobile-task-center;06.01:p233-236;DEV-033/034/035";
    public const string Snapshot = "tug.job.snapshot.v1", BriefingAck = "tug.briefing.ack.v1", ExecutionEvent = "tug.execution.event.v1", DailyEntry = "tug.daily.entry.v1";
    public static readonly string[] CaptureOperations = [BriefingAck, ExecutionEvent, DailyEntry];
    public const string Committed = "Committed", MaterialConflict = "MaterialConflict", ServerRetained = "ServerRetained";
    public static readonly string[] Results = [Committed, MaterialConflict, ServerRetained];
    public const string KeepServer = "keepServer", UseCaptured = "useCaptured";
    public static readonly string[] ResolutionReasons = ["INPUT_CORRECTED", "CURRENT_SERVER_RETAINED"];
    // Human/online-only decisions (08.02 p133-134; 60.02 p217-254): Master release / readiness GO, execution start and
    // lifecycle transitions, verification and daily-log submit/review are never captured offline.
    public static readonly string[] OnlineOnly = ["MASTER_RELEASE", "READINESS_DECISION", "EXECUTION_START", "EXECUTION_TRANSITION",
        "EXECUTION_VERIFICATION", "BRIEFING_ISSUE", "DAILY_LOG_SUBMIT", "DAILY_LOG_REVIEW"];
    // Order-dependent ledger kinds: appended after a concurrent change they may describe a different physical state.
    public static readonly string[] OrderSensitiveKinds = ["TOWLINE_CONNECTED", "TOWLINE_RELEASED"];
    public static readonly string[] ConflictFields = ["briefing_revision", "briefing_content", "briefing_state", "execution_state", "execution_sequence",
        "daily_version", "daily_state", "daily_exists", "daily_slot_taken"];

    // Briefing acknowledgement binds the exact issued revision and content hash; any change is material.
    public static string[] BriefingConflict(long capturedRevision, string capturedHash, long currentRevision, string currentHash, string currentState)
    {
        var fields = new List<string>();
        if (currentRevision != capturedRevision) fields.Add("briefing_revision");
        if (!string.Equals(currentHash, capturedHash, StringComparison.Ordinal)) fields.Add("briefing_content");
        if (currentState != TowageBriefingPolicy.Issued) fields.Add("briefing_state");
        return [.. fields];
    }

    // Execution ledger is append-only: unrelated appends are not material, a lifecycle change is; towline order
    // events are material when anything was appended after the captured base.
    public static string[] ExecutionConflict(string baseState, long baseSequence, string currentState, long currentSequence, string kind)
    {
        var fields = new List<string>();
        if (currentState != baseState) fields.Add("execution_state");
        if (currentSequence != baseSequence && OrderSensitiveKinds.Contains(kind)) fields.Add("execution_sequence");
        return [.. fields];
    }

    // Daily entry: create (no base) conflicts with any existing record of that id or tug/date/watch slot; an edit
    // conflicts when the server version or editable state moved.
    public static string[] DailyConflict(uint? baseVersion, bool exists, bool slotTaken, uint? currentVersion, string? currentState)
    {
        if (baseVersion == null) return exists ? ["daily_exists"] : slotTaken ? ["daily_slot_taken"] : [];
        if (!exists) return ["daily_exists"];
        var fields = new List<string>();
        if (currentVersion != baseVersion) fields.Add("daily_version");
        if (currentState is not (TugDailyLogPolicy.Draft or TugDailyLogPolicy.Returned)) fields.Add("daily_state");
        return [.. fields];
    }

    // Choices a person may make. useCaptured re-submits the identical captured values on the CURRENT base and is
    // still subject to every ordinary server rule; it is not offered where the captured statement can no longer be true
    // (an acknowledgement of a superseded revision, an observation on a terminal execution, an edit of a reviewed log).
    public static string[] Choices(string operation, string[] fields, string? currentState, string kind = "")
    {
        if (operation == BriefingAck && fields.Any(f => f is "briefing_revision" or "briefing_content" or "briefing_state")) return [KeepServer];
        if (operation == ExecutionEvent && currentState is TowageExecutionPolicy.Completed or TowageExecutionPolicy.Aborted &&
            !TowageExecutionPolicy.TerminalKinds.Contains(kind)) return [KeepServer];
        if (operation == DailyEntry && (fields.Contains("daily_state") || fields.Contains("daily_slot_taken") || fields.Contains("daily_exists"))) return [KeepServer];
        return [KeepServer, UseCaptured];
    }

    // Capture must fall inside the prefetched snapshot lease and never in the future beyond technical clock skew.
    public static bool ValidCapture(DateTimeOffset capturedAt, int offsetMinutes, DateTimeOffset issuedAt, DateTimeOffset expiresAt, DateTimeOffset now) =>
        capturedAt.Offset == TimeSpan.Zero && offsetMinutes is >= -840 and <= 840 && capturedAt >= issuedAt.AddMinutes(-2) &&
        capturedAt < expiresAt && capturedAt <= now + TowageExecutionPolicy.ClockSkew;
}

// DEV-091 / T-TUG-012 (18.01 Backlog row 92; 06.01 p236 /api/v1/tug-operation-history; 13.02 screen 185 p1131;
// 15.01 p1609): "History filterable by tug/job/vessel/date/outcome; no silent deletion; trace to evidence/event."
// Outcome is derived from recorded human/system states only; nothing is inferred as completed or verified.
public static class TugOperationHistoryPolicy
{
    public const string SourceVersion = "18.01:row92;06.01:p236;13.02:p1131;15.01:p1609";
    public const string Cancelled = "CANCELLED", NotStarted = "NOT_STARTED", InProgress = "IN_PROGRESS", Completed = "COMPLETED", Aborted = "ABORTED", Mixed = "MIXED";
    public static readonly string[] Outcomes = [Cancelled, NotStarted, InProgress, Completed, Aborted, Mixed];
    public const int PageSize = 25, MaxRangeDays = 400;

    public static string Outcome(string jobStatus, IReadOnlyCollection<string> executionStates)
    {
        if (jobStatus == TugJobLifecycle.Cancelled) return Cancelled;
        if (executionStates.Count == 0) return NotStarted;
        if (executionStates.Any(s => s is TowageExecutionPolicy.Active or TowageExecutionPolicy.Suspended)) return InProgress;
        if (executionStates.All(s => s == TowageExecutionPolicy.Completed)) return Completed;
        if (executionStates.All(s => s == TowageExecutionPolicy.Aborted)) return Aborted;
        return Mixed;
    }

    // Evidence status shown for the job: verification is a separate human record; UNKNOWN when nothing was recorded.
    public static string Evidence(int evidenceEvents, IReadOnlyCollection<string> verificationStates)
    {
        if (verificationStates.Count > 0 && verificationStates.All(v => v == TowageExecutionPolicy.Verified)) return evidenceEvents > 0 ? "VERIFIED_WITH_EVIDENCE" : "VERIFIED_NO_EVIDENCE";
        if (verificationStates.Any(v => v == TowageExecutionPolicy.Rejected)) return "VERIFICATION_REJECTED";
        return evidenceEvents > 0 ? "UNVERIFIED_EVIDENCE" : "UNKNOWN";
    }

    public static long? DurationMinutes(DateTimeOffset? started, DateTimeOffset? ended) =>
        started is { } s && ended is { } e && e >= s ? (long)Math.Floor((e - s).TotalMinutes) : null;

    // Keyset cursor "ticks:guid" (base64url); a malformed cursor is refused, never treated as the first page.
    public static string Cursor(DateTimeOffset at, Guid id) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes(at.UtcTicks.ToString(CultureInfo.InvariantCulture) + ":" + id.ToString("N"))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static bool TryCursor(string? text, out DateTimeOffset at, out Guid id)
    {
        at = default; id = Guid.Empty;
        if (text is not { Length: > 0 and <= 96 } || text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return false;
        try {
            var padded = text.Replace('-', '+').Replace('_', '/'); padded += new string('=', (4 - padded.Length % 4) % 4);
            var parts = Encoding.ASCII.GetString(Convert.FromBase64String(padded)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks || !Guid.TryParseExact(parts[1], "N", out id) || id == Guid.Empty) return false;
            at = new DateTimeOffset(ticks, TimeSpan.Zero); return true;
        } catch (FormatException) { return false; }
    }
}
