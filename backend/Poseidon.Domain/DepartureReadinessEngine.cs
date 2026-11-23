using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

// One typed source observation for the departure gate (PASS|FAIL|UNKNOWN|CONDITIONAL). Reasons are closed
// UPPER_SNAKE codes; no names, medical data, certificate numbers or free text.
public sealed record DepartureCheck(string Category, string Result, string Reason, string? SourceKind = null, Guid? SourceId = null, long? SourceRevision = null);
public sealed record DepartureSafetyItem(Guid EventId, string Reason, bool Blocking);
public sealed record DepartureCategoryState(string Category, string Result, string[] Reasons);
public sealed record DepartureWhy(string Category, string Result, string Reason, string? SourceKind, Guid? SourceId, long? SourceRevision);
public sealed record DepartureOutcome(string State, string Mode, DepartureCategoryState[] Categories, string[] CriticalBlockers, string[] ReviewReasons,
    string[] Conditions, DepartureWhy[] Why, string Hash, string Decision = DepartureReadinessEngine.DecisionAuthority);

// DEV-058 / T-S9-001 (18.01 Backlog row 59: "Readiness state READY/CONDITIONAL/NOT_READY/REVIEW_REQUIRED; critical
// blocker average score ile maskelenmez; Why listesi vardir"; user story: crew/technical/statutory/emergency blockers).
// 60.01 Rev3 paragraph 928/936: the server computes the assessment from owning sources only; a missing source is
// UNKNOWN, never PASS; a future departure is FORECAST (planning only) and only a window that contains the server
// date is CURRENT. The engine is advisory: it never issues RELEASE; that is the Master's human record (DEV-059).
public static class DepartureReadinessEngine
{
    public const string SourceLocator = "18.01/sheet:Backlog/row:59-60;60.01/paragraph:220,928,936,952,1080;15.01/paragraph:1203-1213";
    public const string Ready = "READY", Conditional = "CONDITIONAL", NotReady = "NOT_READY", ReviewRequired = "REVIEW_REQUIRED";
    public static readonly string[] States = [Ready, Conditional, NotReady, ReviewRequired];
    public const string Pass = "PASS", Fail = "FAIL", Unknown = "UNKNOWN", Condition = "CONDITIONAL";
    public static readonly string[] Results = [Pass, Fail, Unknown, Condition];
    public const string Current = "CURRENT", Forecast = "FORECAST";
    public static readonly string[] Modes = [Current, Forecast];
    public const string DecisionAuthority = "MasterHumanDecisionRequired;EngineNeverReleases;NotADepartureClearance";
    // Every category must be observed; a missing category is UNKNOWN (SOURCE_MISSING), never N/A or PASS.
    // S9 integration: "safety" is the DEV-060 open safety event read contract (ISafetyEventBlockerSource).
    public static readonly string[] Categories = ["crew", "technical", "statutory", "emergency", "safety"];
    public const int ReasonCodeMax = 160, MaxChecks = 2048, MaxWindowDays = 366;

    public static bool ValidReasonCode(string? code) => code is { Length: > 0 and <= ReasonCodeMax } &&
        code.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or ':' or '-');

    // CURRENT only when the window contains the server date; a future window is FORECAST (60.01 p936).
    public static string Mode(DateOnly from, DateOnly to, DateOnly today) => from <= today && today <= to ? Current : Forecast;
    public static bool ValidWindow(DateOnly from, DateOnly to, DateOnly today) =>
        to >= from && to.DayNumber - from.DayNumber <= MaxWindowDays && to >= today;

    public static DepartureOutcome Evaluate(IReadOnlyCollection<DepartureCheck> input, DateOnly from, DateOnly to, string mode)
    {
        if (!Modes.Contains(mode, StringComparer.Ordinal) || to < from) throw new ArgumentException("invalid_departure_window");
        var checks = input.Where(c => c != null).ToList();
        if (checks.Count > MaxChecks || checks.Any(c => !Categories.Contains(c.Category, StringComparer.Ordinal) ||
                !Results.Contains(c.Result, StringComparer.Ordinal) || !ValidReasonCode(c.Reason) || c.SourceKind is { Length: > 40 }))
            throw new ArgumentException("invalid_departure_check");
        foreach (var category in Categories.Where(c => checks.All(x => x.Category != c)))
            checks.Add(new(category, Unknown, "SOURCE_MISSING"));
        var categories = Categories.Select(c => {
            var own = checks.Where(x => x.Category == c).ToArray();
            return new DepartureCategoryState(c, Worst(own.Select(x => x.Result)), own.Select(x => x.Reason).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        }).ToArray();
        string[] Codes(string result) => checks.Where(x => x.Result == result).Select(x => x.Category + ":" + x.Reason)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        // No scoring: any FAIL is a blocker however many other checks pass.
        var blockers = Codes(Fail); var review = Codes(Unknown); var conditions = Codes(Condition);
        var state = blockers.Length > 0 ? NotReady : review.Length > 0 ? ReviewRequired : conditions.Length > 0 ? Conditional : Ready;
        // The "Why" list: every non-PASS observation with its source, most severe first.
        var why = checks.Where(x => x.Result != Pass).OrderBy(x => Array.IndexOf(Severity, x.Result)).ThenBy(x => Array.IndexOf(Categories, x.Category))
            .ThenBy(x => x.Reason, StringComparer.Ordinal).ThenBy(x => x.SourceId)
            .Select(x => new DepartureWhy(x.Category, x.Result, x.Reason, x.SourceKind, x.SourceId, x.SourceRevision)).ToArray();
        return new(state, mode, categories, blockers, review, conditions, why, Hash(checks, from, to, mode));
    }

    // S9 integration (DEV-058 x DEV-060): maps the safety event blocker snapshot into this gate. BLOCKERS_PRESENT ->
    // FAIL per blocking event (an open HIGH/CRITICAL event is never averaged away); REVIEW_REQUIRED / UNAVAILABLE / any
    // unrecognised state -> UNKNOWN; only NO_OPEN_HIGH_SEVERITY_EVENT with complete coverage is PASS for this input.
    // Non-blocking items are always UNKNOWN (review), never PASS. Reasons are closed codes; no narrative or reporter
    // identity is copied. Coverage gaps (DEPARTURE-SEVERITY-UNKNOWN-20261123, S9 gate finding N3): while the source
    // reports SeverityPolicyUnconfigured no approved policy says which severities block departure, so "no open
    // high-severity event" cannot rule out a blocking event; it is UNKNOWN SAFETY_EVENT_SEVERITY_POLICY_UNCONFIGURED,
    // never PASS. Any other unrecognised coverage code fails closed as SAFETY_EVENT_COVERAGE_INCOMPLETE.
    public const string SeverityPolicyUnconfiguredCoverage = "SeverityPolicyUnconfigured";
    public const string SourceLimitExceededCoverage = "SafetyEventSourceLimitExceeded";
    public const string ReadRequiredCoverage = "SafetyEventReadRequired";
    public const string SeverityPolicyUnconfiguredReason = "SAFETY_EVENT_SEVERITY_POLICY_UNCONFIGURED";
    public const string CoverageIncompleteReason = "SAFETY_EVENT_COVERAGE_INCOMPLETE";
    public const string SafetySourceKind = "SafetyEvent";
    public const int SafetyItemMax = 256;
    public static DepartureCheck[] SafetyChecks(string? state, IEnumerable<DepartureSafetyItem>? items, IEnumerable<string>? coverage = null)
    {
        var list = (items ?? []).Where(i => i != null).Take(SafetyItemMax + 1).ToArray();
        // A blank or null coverage code is malformed input, not "no gap": it fails closed as an unrecognised gap.
        var gaps = (coverage ?? []).Select(c => string.IsNullOrWhiteSpace(c) ? "" : c).Distinct(StringComparer.Ordinal).ToArray();
        var result = new List<DepartureCheck>();
        DepartureCheck Item(DepartureSafetyItem i, string r) => new("safety", r, "SAFETY_EVENT:" + Code(i.Reason), SafetySourceKind, i.EventId);
        switch (state)
        {
            case "BLOCKERS_PRESENT":
                result.AddRange(list.Take(SafetyItemMax).Select(i => Item(i, i.Blocking ? Fail : Unknown)));
                if (!list.Any(i => i.Blocking)) result.Add(new("safety", Fail, "SAFETY_EVENT_BLOCKERS_PRESENT", SafetySourceKind));
                break;
            case "REVIEW_REQUIRED":
                result.AddRange(list.Take(SafetyItemMax).Select(i => Item(i, Unknown)));
                result.Add(new("safety", Unknown, "SAFETY_EVENT_REVIEW_REQUIRED", SafetySourceKind));
                break;
            case "NO_OPEN_HIGH_SEVERITY_EVENT" when list.Length == 0 && gaps.Length == 0:
                result.Add(new("safety", Pass, "NO_OPEN_HIGH_SEVERITY_EVENT", SafetySourceKind));
                break;
            case "NO_OPEN_HIGH_SEVERITY_EVENT" when list.Length == 0:
                break; // every coverage gap is reported below as UNKNOWN; no PASS observation exists.
            case "UNAVAILABLE":
                result.Add(new("safety", Unknown, "SAFETY_EVENT_SOURCE_UNAVAILABLE", SafetySourceKind));
                break;
            default:
                result.Add(new("safety", Unknown, "SAFETY_EVENT_STATE_UNRECOGNIZED", SafetySourceKind));
                break;
        }
        if (list.Length > SafetyItemMax || gaps.Contains(SourceLimitExceededCoverage, StringComparer.Ordinal))
            result.Add(new("safety", Unknown, "SAFETY_EVENT_SOURCE_LIMIT_EXCEEDED", SafetySourceKind));
        if (gaps.Contains(SeverityPolicyUnconfiguredCoverage, StringComparer.Ordinal))
            result.Add(new("safety", Unknown, SeverityPolicyUnconfiguredReason, SafetySourceKind));
        // An UNAVAILABLE source explains itself with ReadRequired; anything else unrecognised is an unknown gap.
        if (gaps.Any(g => g is not (SeverityPolicyUnconfiguredCoverage or SourceLimitExceededCoverage) && !(state == "UNAVAILABLE" && g == ReadRequiredCoverage)))
            result.Add(new("safety", Unknown, CoverageIncompleteReason, SafetySourceKind));
        return result.ToArray();
    }

    private static readonly string[] Severity = [Fail, Unknown, Condition, Pass];
    public static string Worst(IEnumerable<string> results) {
        var r = results.ToArray();
        return r.Length == 0 || r.Contains(Unknown) && !r.Contains(Fail) ? Unknown : r.Contains(Fail) ? Fail : r.Contains(Condition) ? Condition : Pass;
    }

    // Canonical hash of the exact inputs and window; the Master decision binds to it, so any changed
    // material input (source revision, result, window or mode) makes a previous decision STALE.
    public static string Hash(IEnumerable<DepartureCheck> checks, DateOnly from, DateOnly to, string mode) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        "poseidon.departure-readiness.v1\n" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + mode + "\n" +
        string.Join('\n', checks.Select(c => string.Join('|', c.Category, c.Result, c.Reason, c.SourceKind ?? "-", c.SourceId?.ToString("D") ?? "-",
            c.SourceRevision?.ToString(CultureInfo.InvariantCulture) ?? "-")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))));

    // Closed code from an owning module's code (PascalCase or snake) into this gate's UPPER_SNAKE reason space.
    public static string Code(string value) {
        var b = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length && b.Length < ReasonCodeMax; i++) {
            var c = value[i];
            if (char.IsAsciiLetterUpper(c) && i > 0 && (char.IsAsciiLetterLower(value[i - 1]) || char.IsAsciiDigit(value[i - 1]))) b.Append('_');
            b.Append(char.IsAsciiLetterOrDigit(c) ? char.ToUpperInvariant(c) : c is ':' or '-' ? c : '_');
        }
        return b.Length == 0 ? "UNSPECIFIED" : b.ToString();
    }
}

// DEV-059 / T-S9-002 (18.01 Backlog row 60: "Only authorized Master; decision/reason/time/context stored immutable
// lineage; AI/admin cannot sign; changed material input makes previous decision STALE"). 60.01 Rev3 paragraph 952:
// MasterOperationalDecision RELEASE|HOLD|DENY; "RELEASE ancak CURRENT READY ve gemide o karar icin gecerli onboard
// authority ile mumkundur"; the shore coordinator cannot substitute for the Master. HOLD and DENY are always
// recordable with a reason (ISM Code 5.2 overriding authority of the Master to refuse is never blocked). A RELEASE
// against CONDITIONAL is refused: no source defines a conditional-release policy (expert gate stays open).
public static class DepartureDecisionPolicy
{
    public const string Release = "RELEASE", Hold = "HOLD", Deny = "DENY";
    public static readonly string[] Decisions = [Release, Hold, Deny];
    public const string MasterDuty = "Master";
    public const int ReasonMin = 10, ReasonMax = 2000;
    public const string CurrentValidity = "CURRENT", StaleValidity = "STALE";

    public static bool ValidReason(string? reason) => reason != null && reason.Trim() == reason && reason.Length is >= ReasonMin and <= ReasonMax &&
        !reason.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));

    // Returns null when the human record may be stored, otherwise a closed refusal code.
    public static string? Refusal(string decision, string assessedState, string mode, string? reason)
    {
        if (!Decisions.Contains(decision, StringComparer.Ordinal) || !DepartureReadinessEngine.States.Contains(assessedState, StringComparer.Ordinal) ||
            !DepartureReadinessEngine.Modes.Contains(mode, StringComparer.Ordinal) || !ValidReason(reason)) return "validation_failed";
        if (decision == Release && mode != DepartureReadinessEngine.Current) return "departure_release_forecast_only";
        if (decision == Release && assessedState != DepartureReadinessEngine.Ready) return "departure_release_not_permitted";
        return null;
    }

    // A decision is CURRENT only while the live assessment for the same window still has its exact hash.
    public static string Validity(string decisionHash, DateOnly decisionFrom, DateOnly decisionTo, string currentHash, DateOnly from, DateOnly to) =>
        decisionFrom == from && decisionTo == to && string.Equals(decisionHash, currentHash, StringComparison.Ordinal) ? CurrentValidity : StaleValidity;
}
