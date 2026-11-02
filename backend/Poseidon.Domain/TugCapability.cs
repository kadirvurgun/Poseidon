using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// Owned by the immutable Evidence revision and its independent verification.
// Null fields are incomplete declarations, never a negative observation.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugCapabilityStatement(string Kind, Guid? AssetId = null, long? AssetRevision = null,
    uint? ProfileVersion = null, string? CertifiedTonnes = null, string? MeasuredTonnes = null,
    DateTimeOffset? TestAt = null, DateTimeOffset? ReviewAt = null, string? ConditionFactor = null,
    string? SourceLocator = null, Guid? ConditionEvidenceId = null, long? ConditionEvidenceRevision = null, uint? ConditionEvidenceVersion = null, uint? AssetVersion = null);

// No production formula, margin, coefficient or escort norm is supplied by software.
// This payload travels through the existing source bytes / independent qualification workflow.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugCalculationPolicy(string? Formula = null, string? Aggregation = null, string? SourceLocator = null,
    string[]? JobTypes = null, string[]? Areas = null, int? Scale = null, string? Rounding = null, int? MaxTestAgeDays = null, string? MaxLossPercent = null);

public sealed record TugCapacityResult(string State, string? Tonnes, string Why);

public static class TugCapability
{
    public static bool Complete(TugCapabilityStatement s) => s.AssetId != null && s.AssetRevision != null && s.AssetVersion != null && s.ProfileVersion != null && s.SourceLocator != null &&
        (s.Kind == "Condition" ? s.ConditionFactor != null : s.CertifiedTonnes != null && s.MeasuredTonnes != null && s.TestAt != null && s.ReviewAt != null &&
        s.ConditionEvidenceId != null && s.ConditionEvidenceRevision != null && s.ConditionEvidenceVersion != null);
    public static bool Number(string? raw, out decimal value) {
        value = 0;
        return raw != null && System.Text.RegularExpressions.Regex.IsMatch(raw, @"\A(?:0|[1-9][0-9]{0,5})(?:\.[0-9]{1,6})?\z") &&
            decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
    public static void Validate(TugCapabilityStatement s) {
        if (s.Kind is not ("BollardPullTest" or "Condition") || s.AssetId == Guid.Empty || s.AssetRevision is <= 0 || s.ProfileVersion == 0 || s.AssetVersion == 0 ||
            (s.AssetId == null) != (s.AssetRevision == null) || s.ConditionEvidenceId == Guid.Empty || s.ConditionEvidenceRevision is <= 0 || s.ConditionEvidenceVersion == 0 ||
            (s.ConditionEvidenceId == null) != (s.ConditionEvidenceRevision == null) || (s.ConditionEvidenceId == null) != (s.ConditionEvidenceVersion == null) ||
            s.SourceLocator != null && (s.SourceLocator.Length is < 1 or > 2000 || s.SourceLocator != s.SourceLocator.Trim() || s.SourceLocator.Any(char.IsControl))) throw new ArgumentException("invalid_tug_capability");
        foreach (var n in new[] { s.CertifiedTonnes, s.MeasuredTonnes, s.ConditionFactor })
            if (n != null && !Number(n, out _)) throw new ArgumentException("invalid_tug_numeric");
        if (s.Kind == "Condition" && (s.CertifiedTonnes != null || s.MeasuredTonnes != null || s.TestAt != null || s.ReviewAt != null || s.ConditionEvidenceId != null) ||
            s.Kind == "BollardPullTest" && s.ConditionFactor != null || s.TestAt != null && s.ReviewAt <= s.TestAt ||
            s.ConditionFactor != null && Number(s.ConditionFactor, out var factor) && factor > 1) throw new ArgumentException("invalid_tug_capability");
    }
    public static void Validate(TugCalculationPolicy p) {
        if (p.Formula is not (null or "MeasuredTimesVerifiedCondition") || p.Aggregation is not (null or "SumForPlanning") || p.Rounding is not (null or "Floor") || p.Scale is < 0 or > 6 ||
            p.MaxTestAgeDays is < 1 or > 36525 || p.MaxLossPercent != null && (!Number(p.MaxLossPercent, out var loss) || loss > 100) ||
            p.SourceLocator != null && (string.IsNullOrWhiteSpace(p.SourceLocator) || p.SourceLocator != p.SourceLocator.Trim() || p.SourceLocator.Length > 2000 || p.SourceLocator.Any(char.IsControl)) ||
            p.JobTypes != null && (p.JobTypes.Length > 32 || p.JobTypes.Any(x => !TugRequestPolicy.JobTypes.Contains(x)) || p.JobTypes.Distinct().Count() != p.JobTypes.Length) ||
            p.Areas != null && (p.Areas.Length > 128 || p.Areas.Any(x => string.IsNullOrWhiteSpace(x) || x != x.Trim() || x.Length > 200 || x.Any(char.IsControl)) || p.Areas.Distinct().Count() != p.Areas.Length))
            throw new ArgumentException("unsupported_tug_calculation_policy");
    }
    public static bool Complete(TugCalculationPolicy p) => p.Formula != null && p.Aggregation != null && p.Rounding != null &&
        p.Scale != null && p.MaxTestAgeDays != null && p.MaxLossPercent != null && p.SourceLocator != null && p.JobTypes is { Length: > 0 } && p.Areas is { Length: > 0 };
    // Verification, current source, ownership and pin checks are mandatory at the consumer boundary.
    public static TugCapacityResult Calculate(TugCapabilityStatement test, TugCapabilityStatement condition, TugCalculationPolicy p,
        string? jobType, string? area, DateTimeOffset from, DateTimeOffset until, DateTimeOffset now) {
        Validate(test); Validate(condition); Validate(p);
        if (!Complete(p)) return new("Unknown", null, "QualifiedFormulaIncomplete");
        if (!p.JobTypes!.Contains(jobType ?? "") || !p.Areas!.Contains(area ?? "")) return new("UnsupportedContext", null, "QualifiedFormulaContextMissing");
        if (until < from || from < now || test.Kind != "BollardPullTest" || condition.Kind != "Condition" ||
            test.AssetId == null || test.AssetRevision == null || test.AssetVersion == null || test.ProfileVersion == null || test.SourceLocator == null || condition.SourceLocator == null ||
            test.AssetId != condition.AssetId || test.AssetRevision != condition.AssetRevision || test.AssetVersion != condition.AssetVersion || test.ProfileVersion != condition.ProfileVersion ||
            !Number(test.CertifiedTonnes, out var certified) || certified <= 0 || !Number(test.MeasuredTonnes, out var measured) ||
            !Number(condition.ConditionFactor, out var factor) || test.TestAt == null || test.ReviewAt == null)
            return new("Unknown", null, "OwningMeasurementOrConditionIncomplete");
        if (test.TestAt > now) return new("Future", null, "TestInFuture");
        if (test.ReviewAt <= until || (until - test.TestAt.Value).TotalDays >= p.MaxTestAgeDays!.Value) return new("Expired", null, "TestDoesNotCoverPlanWindow");
        Number(p.MaxLossPercent, out var loss);
        if (measured > certified || measured < certified * (1 - loss / 100)) return new("ReviewRequired", null, "CertifiedMeasuredRestriction");
        var divisor = (decimal)Math.Pow(10, p.Scale!.Value);
        var result = decimal.Floor(measured * factor * divisor) / divisor;
        return new("CurrentPlanningCapacity", result.ToString("0.######", CultureInfo.InvariantCulture), "QualifiedMeasuredConditionFormula;MasterDecisionSeparate");
    }
}
