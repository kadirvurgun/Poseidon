using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

// One typed source check (60.02 paragraph 198 adapter result PASS|FAIL|UNKNOWN|CONDITIONAL).
public sealed record TugReadinessCheck(string Category, string Result, string Reason, bool Critical = true,
    Guid? SourceId = null, long? SourceRevision = null);
public sealed record TugReadinessCategoryState(string Category, string Result, string[] Reasons);
public sealed record TugReadinessOutcome(string State, TugReadinessCategoryState[] Categories, string[] CriticalBlockers,
    string[] ReviewReasons, string[] Conditions, string Hash, string Decision = TugReadinessEngine.DecisionAuthority);

// DEV-084 / T-TUG-005 (18.01 Backlog row 85: "READY/CONDITIONAL/NOT_READY/REVIEW_REQUIRED; critical blocker
// cannot be averaged; Master decision and reason retained"). 60.02 Rev2 paragraph 199: NOT_READY is a known
// FAIL, REVIEW_REQUIRED is UNKNOWN or an unavailable source, CONDITIONAL is all required checks valid with a
// source-permitted condition, READY is everything PASS; conditions never override a legal/device blocker.
// Paragraphs 3540-3736 (ReadinessView): category states, BP margin = verified available - required (null if
// either is null), default REVIEW_REQUIRED, stale READY never shown.
// The engine is advisory. It never produces GO/NO-GO; that is a separate Master/Tug Master human record.
public static class TugReadinessEngine
{
    public const string SourceLocator = "18.01/sheet:Backlog/row:85;60.02/paragraph:199,201,3540-3736";
    public const string Ready = "READY", Conditional = "CONDITIONAL", NotReady = "NOT_READY", ReviewRequired = "REVIEW_REQUIRED";
    public static readonly string[] States = [Ready, Conditional, NotReady, ReviewRequired];
    public const string Pass = "PASS", Fail = "FAIL", Unknown = "UNKNOWN", Condition = "CONDITIONAL";
    public static readonly string[] Results = [Pass, Fail, Unknown, Condition];
    public const string DecisionAuthority = "MasterOrTugMasterHumanDecisionRequired;EngineNeverIssuesGo";
    // Every required category must be observed; a missing category is UNKNOWN, never N/A.
    public static readonly string[] Categories = ["crew", "crew_competence", "equipment", "tow_equipment", "pms", "certificate",
        "communication", "weather", "bollard_pull_margin", "assignment"];
    public const int ReasonMax = 2000;

    public static TugReadinessOutcome Evaluate(IReadOnlyCollection<TugReadinessCheck> input)
    {
        var checks = input.Where(c => c != null).ToList();
        if (checks.Any(c => !Categories.Contains(c.Category, StringComparer.Ordinal) || !Results.Contains(c.Result, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(c.Reason)))
            throw new ArgumentException("invalid_readiness_check");
        foreach (var category in Categories.Where(c => checks.All(x => x.Category != c)))
            checks.Add(new(category, Unknown, "SourceMissing"));
        string Worst(IEnumerable<TugReadinessCheck> xs) {
            var r = xs.Select(x => x.Result).ToArray();
            return r.Contains(Fail) ? Fail : r.Contains(Unknown) ? Unknown : r.Contains(Condition) ? Condition : Pass;
        }
        var categories = Categories.Select(c => {
            var own = checks.Where(x => x.Category == c).ToArray();
            return new TugReadinessCategoryState(c, Worst(own), own.Select(x => x.Reason).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        }).ToArray();
        // Any FAIL is a blocker regardless of how many other checks pass: no scoring or averaging.
        var blockers = checks.Where(x => x.Result == Fail).Select(x => x.Category + ":" + x.Reason).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var review = checks.Where(x => x.Result == Unknown).Select(x => x.Category + ":" + x.Reason).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var conditions = checks.Where(x => x.Result == Condition).Select(x => x.Category + ":" + x.Reason).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var state = blockers.Length > 0 ? NotReady : review.Length > 0 ? ReviewRequired : conditions.Length > 0 ? Conditional : Ready;
        return new(state, categories, blockers, review, conditions, Hash(checks));
    }

    // Same precedence for a partial view (one category family): FAIL > UNKNOWN > CONDITIONAL > PASS.
    public static string StateOf(IEnumerable<TugReadinessCheck> checks) {
        var r = checks.Select(c => c.Result).ToArray();
        return r.Length == 0 || r.Contains(Unknown) && !r.Contains(Fail) ? ReviewRequired : r.Contains(Fail) ? NotReady : r.Contains(Condition) ? Conditional : Ready;
    }

    // Canonical hash of the exact inputs; a Master decision binds to this, so any change makes it stale.
    public static string Hash(IEnumerable<TugReadinessCheck> checks) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        "poseidon.tug-readiness.v1\n" + string.Join('\n', checks.Select(c => string.Join('|', c.Category, c.Result, c.Reason, c.Critical ? "1" : "0",
            c.SourceId?.ToString("D") ?? "-", c.SourceRevision?.ToString(CultureInfo.InvariantCulture) ?? "-")).Order(StringComparer.Ordinal)))));

    // ReadinessView.bollard_pull_margin: same-metric verified available - required. Either null -> null and UNKNOWN.
    // A non-negative margin is evidence for this check only; it never gives a safety decision by itself.
    public static (string? Margin, TugReadinessCheck Check) BollardPullMargin(decimal? requiredTonnes, decimal? verifiedAvailableTonnes)
    {
        if (requiredTonnes == null) return (null, new("bollard_pull_margin", Unknown, "RequiredBollardPullSourceMissing"));
        if (verifiedAvailableTonnes == null) return (null, new("bollard_pull_margin", Unknown, "VerifiedAvailableBollardPullMissing"));
        var margin = verifiedAvailableTonnes.Value - requiredTonnes.Value;
        var text = margin.ToString("0.##", CultureInfo.InvariantCulture);
        return (text, margin < 0 ? new("bollard_pull_margin", Fail, "AvailableBollardPullBelowRequired") : new("bollard_pull_margin", Pass, "VerifiedMarginNonNegative"));
    }

    public static TugReadinessCheck FromFact(string category, string state, string why, Guid? id, long? revision) => state switch {
        "Blocked" => new(category, Fail, why, true, id, revision),
        "Current" => new(category, Pass, why, true, id, revision),
        _ => new(category, Unknown, why, true, id, revision)
    };
}

// The Master/Tug Master GO/NO_GO record. It is a human decision bound to one exact assessment hash.
// 60.02 paragraph 201: release is allowed only on a current PASS or a source-permitted CONDITIONAL; a
// rejection is always recordable. A GO against NOT_READY/REVIEW_REQUIRED is refused, never overridden.
public static class TugReadinessDecisionPolicy
{
    public const string Go = "GO", NoGo = "NO_GO";
    public static readonly string[] Decisions = [Go, NoGo];
    public static bool ValidReason(string? reason) => TugAssignmentWorkflow.ValidReason(reason) && reason!.Length <= TugReadinessEngine.ReasonMax;
    public static string? Refusal(string decision, string assessedState, string? reason)
    {
        if (!Decisions.Contains(decision, StringComparer.Ordinal) || !TugReadinessEngine.States.Contains(assessedState, StringComparer.Ordinal)) return "validation_failed";
        if (!ValidReason(reason)) return "validation_failed";
        if (decision == Go && assessedState is not (TugReadinessEngine.Ready or TugReadinessEngine.Conditional)) return "tug_go_not_permitted";
        return null;
    }
}
