namespace Poseidon.Domain;

// DEV-056 / T-S8-005 (18.01 Backlog row 57; 59.04 Rev2 pms-command-center paragraphs 2000-2018:
// readiness_state, overdue_jobs, critical_overdue_jobs, open_work_orders, critical_spare_gaps,
// condition_alerts, last_refresh_at). Exceptions are listed with a "Why" chain; healthy rows stay
// quiet (counted, not listed). A missing or unauthorized source is UNKNOWN / ACCESS_REQUIRED, never
// zero or healthy. The readiness value is an advisory exception summary, not a fitness decision.
public sealed record PmsWhy(string Code, string? Detail = null);
public sealed record PmsCommandItem(string Kind, Guid SourceId, long? SourceRevision, string Severity, PmsWhy[] Why,
    DateTimeOffset? ObservedAt, string Freshness, Guid? AssetId = null);
public sealed record PmsSection(string Key, string State, int? Count, int QuietCount, PmsCommandItem[] Items, DateTimeOffset? AsOf,
    string Freshness, string? Reason);

public static class PmsCommandCenterPolicy
{
    public const string Exceptions = "EXCEPTIONS", Quiet = "QUIET", Unknown = "UNKNOWN", AccessRequired = "ACCESS_REQUIRED";
    public const string Current = "CURRENT", Stale = "STALE", FreshnessUnknown = "UNKNOWN";
    public static readonly string[] SectionKeys = ["critical_overdue_jobs", "overdue_jobs", "open_work_orders", "condition_alerts", "critical_spare_gaps"];
    public static readonly string[] Severities = ["CRITICAL", "ATTENTION", "REVIEW"];
    // Due reasons that say the reading behind the calculation is not current (no freshness policy approved).
    private static readonly string[] StaleReasons = ["ReadingFreshnessPolicyUnresolved", "ConditionFreshnessPolicyUnresolved", "EquipmentRevisionChanged", "FutureObservation"];

    public static PmsSection Unavailable(string key, string reason, bool access = false) =>
        new(key, access ? AccessRequired : Unknown, null, 0, [], null, FreshnessUnknown, reason);

    public static PmsSection Section(string key, IReadOnlyCollection<PmsCommandItem> items, int quiet, DateTimeOffset? asOf)
    {
        var ordered = items.OrderBy(i => Array.IndexOf(Severities, i.Severity)).ThenBy(i => i.ObservedAt).ThenBy(i => i.SourceId).ToArray();
        var freshness = ordered.Any(i => i.Freshness == Stale) ? Stale : ordered.Any(i => i.Freshness == FreshnessUnknown) ? FreshnessUnknown : Current;
        return new(key, ordered.Length == 0 ? Quiet : Exceptions, ordered.Length, quiet, ordered, asOf, freshness, null);
    }

    // One maintenance plan row. Returns null for a healthy (NotDue) row, which stays quiet.
    // Plan-level critical declarations (free-text plan input) and the DEV-052 closed equipment criticality.
    // Undeclared equipment criticality (UNKNOWN) is treated like CRITICAL (EquipmentCriticality.RequiresReview):
    // an unknown never hides an overdue job among non-critical rows.
    private static readonly string[] CriticalPlanDeclarations = ["Critical", "DeclaredCritical", EquipmentCriticality.Critical];
    public static bool PlanDeclaredCritical(string? planCriticality) => planCriticality != null && CriticalPlanDeclarations.Contains(planCriticality, StringComparer.Ordinal);
    public static bool IsCritical(string? planCriticality, string? assetCriticality) =>
        PlanDeclaredCritical(planCriticality) || EquipmentCriticality.RequiresReview(EquipmentCriticality.Of(assetCriticality));

    public static (string Section, PmsCommandItem Item)? Classify(Guid planId, long revision, Guid assetId, MaintenanceDue due, string? criticality,
        string? assetCriticality, string? timeZone, DateTimeOffset? observedAt, DateTimeOffset now)
    {
        if (due.State == "NotDue") return null;
        var critical = IsCritical(criticality, assetCriticality);
        var equipment = EquipmentCriticality.Of(assetCriticality);
        PmsWhy[] criticalWhy = [.. PlanDeclaredCritical(criticality) ? new[] { new PmsWhy("PLAN_CRITICALITY", criticality) } : [],
            .. equipment == EquipmentCriticality.Critical ? new[] { new PmsWhy("EQUIPMENT_CRITICALITY", equipment) }
                : equipment == EquipmentCriticality.Unknown ? new[] { new PmsWhy("EQUIPMENT_CRITICALITY_UNKNOWN") } : []];
        if (due.State == "Due")
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            try { if (timeZone != null) today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime); }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { }
            var overdue = due.NextDueDate is { } d && d < today;
            PmsWhy[] why = [new(overdue ? "MAINTENANCE_OVERDUE" : "MAINTENANCE_DUE", due.Reason),
                .. due.NextDueDate is { } date ? new[] { new PmsWhy("NEXT_DUE_DATE", date.ToString("yyyy-MM-dd")) } : [],
                .. due.NextDueMeter is { } meter ? new[] { new PmsWhy("NEXT_DUE_METER", meter) } : [],
                .. criticalWhy];
            return (critical ? "critical_overdue_jobs" : "overdue_jobs",
                new("MaintenancePlan", planId, revision, critical ? "CRITICAL" : "ATTENTION", why, observedAt, Current, assetId));
        }
        // ReviewRequired: the calculation could not decide. Shown, never counted as healthy.
        var stale = StaleReasons.Contains(due.Reason, StringComparer.Ordinal);
        return (critical ? "critical_overdue_jobs" : "overdue_jobs",
            new("MaintenancePlan", planId, revision, "REVIEW", [new("MAINTENANCE_REVIEW_REQUIRED", due.Reason), .. criticalWhy], observedAt,
                stale ? Stale : observedAt == null ? FreshnessUnknown : Current, assetId));
    }

    // Advisory summary: an unknown source can never yield a clean state.
    public static string Readiness(IReadOnlyCollection<PmsSection> sections)
    {
        if (sections.Any(s => s.Items.Any(i => i.Severity == "CRITICAL"))) return "BLOCKED";
        if (sections.Any(s => s.State is Unknown or AccessRequired || s.Items.Any(i => i.Severity == "REVIEW"))) return "REVIEW_REQUIRED";
        if (sections.Any(s => s.State == Exceptions)) return "ATTENTION";
        return "NO_OPEN_EXCEPTIONS";
    }

    // Defect lifecycle alerts become condition alerts with their Why chain.
    public static PmsCommandItem? DefectAlert(Guid defectId, long revision, DefectLifecycleEvaluation? lifecycle, string? declaredCriticality, DateTimeOffset savedAt)
    {
        if (lifecycle == null)
        {
            // Not yet in the lifecycle: a declared Critical / unknown criticality still needs attention.
            if (declaredCriticality is "High" or "Medium" or "Low") return null;
            return new("Defect", defectId, revision, declaredCriticality == "Critical" ? "CRITICAL" : "REVIEW",
                [new("DEFECT_LIFECYCLE_NOT_STARTED"), new(declaredCriticality == "Critical" ? "DECLARED_CRITICAL" : "CRITICALITY_UNKNOWN")], savedAt, Current);
        }
        if (lifecycle.Stage == DefectLifecyclePolicy.Closed) return null;
        var why = lifecycle.Alerts.Select(a => new PmsWhy(a.Code, a.At?.ToString("O"))).ToList();
        why.Add(new("LIFECYCLE_STAGE", lifecycle.Stage));
        var blocking = lifecycle.Alerts.Any(a => a.Blocking);
        if (!blocking && !lifecycle.Critical) return null;
        var expired = lifecycle.Alerts.Any(a => a.Code == "TEMPORARY_MEASURE_EXPIRED");
        return new("Defect", defectId, revision, lifecycle.Critical || expired ? "CRITICAL" : "REVIEW", [.. why], savedAt, Current);
    }
}
