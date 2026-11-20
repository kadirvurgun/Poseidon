using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-056 / T-S8-005 (18.01 Backlog row 57): critical overdue / open WO / condition alert / spare gap,
// "Why" drill-down, healthy items quiet, stale data visible; unknown sources are never healthy.
[Trait("Requirement", "DEV-056")]
public sealed class PmsCommandCenterPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Healthy_plan_rows_stay_quiet_and_due_rows_carry_a_why_chain()
    {
        Assert.Null(PmsCommandCenterPolicy.Classify(Guid.NewGuid(), 1, Guid.NewGuid(), new("NotDue", "CalendarBoundary", new(2026, 12, 1)), "Critical", "STANDARD", "UTC", Now, Now));
        var overdue = PmsCommandCenterPolicy.Classify(Guid.NewGuid(), 3, Guid.NewGuid(), new("Due", "CalendarBoundary", new(2026, 11, 1)), "Critical", "STANDARD", "UTC", Now, Now)!.Value;
        Assert.Equal("critical_overdue_jobs", overdue.Section);
        Assert.Equal("CRITICAL", overdue.Item.Severity);
        Assert.Equal(["MAINTENANCE_OVERDUE", "NEXT_DUE_DATE", "PLAN_CRITICALITY"], overdue.Item.Why.Select(w => w.Code));
        var due = PmsCommandCenterPolicy.Classify(Guid.NewGuid(), 1, Guid.NewGuid(), new("Due", "CalendarBoundary", new(2026, 11, 20)), "Low", "STANDARD", "UTC", Now, Now)!.Value;
        Assert.Equal(("overdue_jobs", "ATTENTION", "MAINTENANCE_DUE"), (due.Section, due.Item.Severity, due.Item.Why[0].Code));
    }

    [Fact]
    public void Declared_critical_plans_and_undeclared_equipment_criticality_are_critical()
    {
        // DEV-052: UNKNOWN equipment criticality is handled like CRITICAL; plan data uses "DeclaredCritical".
        var unknown = PmsCommandCenterPolicy.Classify(Guid.NewGuid(), 1, Guid.NewGuid(), new("Due", "CalendarBoundary", new(2026, 11, 1)), "Low", null, "UTC", Now, Now)!.Value;
        Assert.Equal(("critical_overdue_jobs", "CRITICAL"), (unknown.Section, unknown.Item.Severity));
        Assert.Contains(unknown.Item.Why, w => w.Code == "EQUIPMENT_CRITICALITY_UNKNOWN");
        var declared = PmsCommandCenterPolicy.Classify(Guid.NewGuid(), 1, Guid.NewGuid(), new("Due", "CalendarBoundary", new(2026, 11, 1)), "DeclaredCritical", "STANDARD", "UTC", Now, Now)!.Value;
        Assert.Equal(("critical_overdue_jobs", "CRITICAL"), (declared.Section, declared.Item.Severity));
        Assert.Contains(declared.Item.Why, w => w.Code == "PLAN_CRITICALITY" && w.Detail == "DeclaredCritical");
        var equipment = PmsCommandCenterPolicy.Classify(Guid.NewGuid(), 1, Guid.NewGuid(), new("Due", "CalendarBoundary", new(2026, 11, 1)), "Low", "CRITICAL", "UTC", Now, Now)!.Value;
        Assert.Equal(("critical_overdue_jobs", "CRITICAL"), (equipment.Section, equipment.Item.Severity));
        var review = PmsCommandCenterPolicy.Classify(Guid.NewGuid(), 1, Guid.NewGuid(), new("ReviewRequired", "IncompletePlanContext"), "Low", "garbage", null, null, Now)!.Value;
        Assert.Equal(("critical_overdue_jobs", "REVIEW"), (review.Section, review.Item.Severity));
        Assert.True(PmsCommandCenterPolicy.IsCritical(null, "UNKNOWN"));
        Assert.False(PmsCommandCenterPolicy.IsCritical("Low", "IMPORTANT"));
        Assert.False(PmsCommandCenterPolicy.IsCritical("critical", "STANDARD"));
    }

    [Fact]
    public void Review_required_rows_are_shown_and_freshness_gaps_are_marked_stale()
    {
        var stale = PmsCommandCenterPolicy.Classify(Guid.NewGuid(), 1, Guid.NewGuid(), new("ReviewRequired", "ReadingFreshnessPolicyUnresolved"), "Critical", "STANDARD", null, Now.AddDays(-9), Now)!.Value;
        Assert.Equal(("REVIEW", PmsCommandCenterPolicy.Stale), (stale.Item.Severity, stale.Item.Freshness));
        var section = PmsCommandCenterPolicy.Section("critical_overdue_jobs", [stale.Item], 4, Now);
        Assert.Equal((PmsCommandCenterPolicy.Exceptions, PmsCommandCenterPolicy.Stale, 1, 4), (section.State, section.Freshness, section.Count, section.QuietCount));
    }

    [Fact]
    public void Unknown_or_forbidden_sources_never_count_as_zero_or_healthy()
    {
        var unknown = PmsCommandCenterPolicy.Unavailable("open_work_orders", "WORK_ORDER_STATE_SOURCE_UNAVAILABLE");
        Assert.Equal((PmsCommandCenterPolicy.Unknown, (int?)null), (unknown.State, unknown.Count));
        var denied = PmsCommandCenterPolicy.Unavailable("condition_alerts", "DEFECT_ACCESS_REQUIRED", true);
        Assert.Equal(PmsCommandCenterPolicy.AccessRequired, denied.State);
        var quiet = PmsCommandCenterPolicy.Section("overdue_jobs", [], 7, Now);
        Assert.Equal("REVIEW_REQUIRED", PmsCommandCenterPolicy.Readiness([quiet, unknown]));
        Assert.Equal("NO_OPEN_EXCEPTIONS", PmsCommandCenterPolicy.Readiness([quiet]));
    }

    [Fact]
    public void Critical_items_block_and_attention_items_need_attention()
    {
        var critical = new PmsCommandItem("Defect", Guid.NewGuid(), 1, "CRITICAL", [new("DECLARED_CRITICAL")], Now, PmsCommandCenterPolicy.Current);
        var attention = new PmsCommandItem("MaintenancePlan", Guid.NewGuid(), 1, "ATTENTION", [new("MAINTENANCE_DUE")], Now, PmsCommandCenterPolicy.Current);
        Assert.Equal("BLOCKED", PmsCommandCenterPolicy.Readiness([PmsCommandCenterPolicy.Section("condition_alerts", [critical], 0, Now)]));
        Assert.Equal("ATTENTION", PmsCommandCenterPolicy.Readiness([PmsCommandCenterPolicy.Section("overdue_jobs", [attention], 0, Now)]));
    }

    [Fact]
    public void Defect_alerts_reflect_lifecycle_state()
    {
        var id = Guid.NewGuid();
        Assert.Equal("CRITICAL", PmsCommandCenterPolicy.DefectAlert(id, 1, null, "Critical", Now)!.Severity);
        Assert.Equal("REVIEW", PmsCommandCenterPolicy.DefectAlert(id, 1, null, null, Now)!.Severity);
        Assert.Null(PmsCommandCenterPolicy.DefectAlert(id, 1, null, "Low", Now));
        var closed = DefectLifecyclePolicy.Evaluate([new(Guid.NewGuid(), 1, "CLOSE", "CLOSED", 1, null, null, false, null, null, "RETURNED_TO_SERVICE", id, Now)], "Critical", 1, Now);
        Assert.Null(PmsCommandCenterPolicy.DefectAlert(id, 1, closed, "Critical", Now));
        var expired = DefectLifecyclePolicy.Evaluate([
            new(Guid.NewGuid(), 1, "START", "DETECT", 1, null, null, false, null, null, null, id, Now.AddDays(-3)),
            new(Guid.NewGuid(), 2, "ASSESS", "ASSESS", 1, "Medium", null, false, null, null, null, id, Now.AddDays(-3)),
            new(Guid.NewGuid(), 3, "SAFEGUARD", "SAFEGUARD", 1, null, "RESTRICTION", true, Now.AddDays(-1), null, null, id, Now.AddDays(-3))], "Medium", 1, Now);
        var alert = PmsCommandCenterPolicy.DefectAlert(id, 1, expired, "Medium", Now)!;
        Assert.Equal("CRITICAL", alert.Severity);
        Assert.Contains(alert.Why, w => w.Code == "TEMPORARY_MEASURE_EXPIRED");
    }
}
