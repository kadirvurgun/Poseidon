namespace Poseidon.Domain;

public sealed record HighRiskDimension(string Dimension, string State, string Reason, Guid? SourceId = null);
public sealed record HighRiskNextAction(string Kind, string? Dimension, string? Reason);

// DEV-107 / T-RISK2-003 (18.01 Backlog row 108): "Fleet filter; risk/permit/barrier/crew/equipment state;
// owner/due; Why drill-down; no score hides critical blocker." 50.01 Rev3 paragraph 230 (read-only projection;
// UNKNOWN grey with reason; historic ACTIVE is never a current green permit), 431 and 1072-1076
// (HighRiskWorkFields, NextAction priority stop/revoked -> expired -> required missing -> reassessment ->
// unavailable -> owner; NONE only when the full current source has no action; unresolved due stays null).
// 50.02 Kaynak Alanlar rows 40-51 (state code lists). There is no permit-to-work source in POSEIDON yet, so
// permit is always UNKNOWN; consequently NONE is never produced. No aggregate score exists: every dimension is
// returned separately and the counts are blocker counts, never a weighted or averaged value.
public static class HighRiskWorkPolicy
{
    public const string SourceVersion = "18.01:row108;50.01-Rev3:p230,431,1072-1076;50.02:KaynakAlanlar:40-51";
    public static readonly string[] WorkTypes = ["HOT_WORK", "ENCLOSED_SPACE", "WORKING_ALOFT", "ELECTRICAL_ISOLATION", "CRITICAL_MAINTENANCE",
        "TOWAGE", "BUNKERING", "OTHER", "UNCLASSIFIED"];
    public static readonly string[] RiskStates = ["READY", "ATTENTION", "BLOCKED", "REVIEW_REQUIRED"];
    public static readonly string[] BarrierStates = ["HEALTHY", "DEGRADED", "FAILED", "UNKNOWN"];
    public static readonly string[] ReadinessStates = [TugReadinessEngine.Ready, TugReadinessEngine.Conditional, TugReadinessEngine.NotReady, TugReadinessEngine.ReviewRequired];
    public static readonly string[] NextActions = ["RESOLVE_STOP", "RENEW_EXPIRED", "COMPLETE_REQUIRED", "REASSESS", "WAIT_SOURCE", "OPEN_OWNER", "NONE"];
    public const string PermitUnknown = "UNKNOWN";

    // S7 risk work types that are listed as high-risk work in 50.02 row 42. MAINTENANCE criticality is not
    // known here, so it is shown as UNCLASSIFIED (never silently dropped); a missing work type is UNCLASSIFIED.
    // CARGO and NAVIGATION are not in the source list and are not projected.
    public static string? FromRiskWorkType(string? workType) => workType switch {
        null => "UNCLASSIFIED",
        "HOT_WORK" or "ENCLOSED_SPACE" or "BUNKERING" or "TOWAGE" or "OTHER" => workType,
        "MAINTENANCE" => "UNCLASSIFIED",
        _ => null
    };

    public static string Level(string state) => state switch {
        "BLOCKED" or "FAILED" or TugReadinessEngine.NotReady or "FAIL" => "CRITICAL",
        "UNKNOWN" or TugReadinessEngine.ReviewRequired => "REVIEW",
        "ATTENTION" or "DEGRADED" or TugReadinessEngine.Conditional or "CONDITIONAL" => "WARNING",
        _ => "OK"
    };

    private static readonly string[] StopReasons = ["RiskAcknowledgementDisputed", "BriefingAcknowledgementDisputed"];
    private static readonly string[] ReassessReasons = ["RiskAssessmentReturned", "TowageBriefingPlanChanged", "TowageRiskAssessmentChanged", "TowagePlanRequestChanged"];

    // Fixed server priority (50.01 paragraph 1076).
    public static HighRiskNextAction NextAction(IReadOnlyCollection<HighRiskDimension> why) {
        HighRiskDimension? First(Func<HighRiskDimension, bool> f) => why.FirstOrDefault(f);
        if (First(d => StopReasons.Contains(d.Reason)) is { } stop) return new("RESOLVE_STOP", stop.Dimension, stop.Reason);
        if (First(d => Level(d.State) == "CRITICAL" && !ReassessReasons.Contains(d.Reason)) is { } missing) return new("COMPLETE_REQUIRED", missing.Dimension, missing.Reason);
        if (First(d => ReassessReasons.Contains(d.Reason)) is { } reassess) return new("REASSESS", reassess.Dimension, reassess.Reason);
        if (First(d => Level(d.State) == "REVIEW") is { } wait) return new("WAIT_SOURCE", wait.Dimension, wait.Reason);
        if (First(d => Level(d.State) == "WARNING") is { } owner) return new("OPEN_OWNER", owner.Dimension, owner.Reason);
        return new("NONE", null, null);
    }

    public static int Count(IEnumerable<HighRiskDimension> why, string level) => why.Count(d => Level(d.State) == level);

    // Worst-first ordering for the exception view: a row with any critical blocker always sorts before rows
    // without one, regardless of how many other dimensions are fine.
    public static int Rank(IReadOnlyCollection<HighRiskDimension> why) =>
        Count(why, "CRITICAL") > 0 ? 0 : Count(why, "REVIEW") > 0 ? 1 : Count(why, "WARNING") > 0 ? 2 : 3;

    // Worst state of an owner-readiness family (FAIL > UNKNOWN > CONDITIONAL > PASS) as a readiness state.
    public static string Readiness(IEnumerable<string> states) {
        var s = states.ToArray();
        if (s.Length == 0) return TugReadinessEngine.ReviewRequired;
        if (s.Contains(TugReadinessEngine.NotReady)) return TugReadinessEngine.NotReady;
        if (s.Contains(TugReadinessEngine.ReviewRequired)) return TugReadinessEngine.ReviewRequired;
        if (s.Contains(TugReadinessEngine.Conditional)) return TugReadinessEngine.Conditional;
        return TugReadinessEngine.Ready;
    }
}
