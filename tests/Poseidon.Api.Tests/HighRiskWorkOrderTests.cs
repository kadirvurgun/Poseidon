using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// S8 QA N1 (DEV-107, 18.01 row 108 "no score hides critical blocker"): ordering runs over the complete
// dimension set of each row, so a critical blocker that only appears in the live crew/equipment readiness
// dimensions still sorts the row first. Unit level; the endpoint ranks after appending live dimensions.
[Trait("Requirement", "DEV-107")]
public sealed class HighRiskWorkOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    private static HighRiskWorkRow Row(Guid id, DateTimeOffset? start, params HighRiskDimension[] why) => new("TugJob", id, "FICTIONAL-" + id.ToString("N")[..6],
        Guid.NewGuid(), "SYN", "FICTIONAL tug", "Tug", "TOWAGE", start, "TugMaster", "READY", HighRiskWorkPolicy.PermitUnknown, "UNKNOWN", "READY", "READY",
        HighRiskWorkPolicy.NextAction(why), new("TugMaster", start, null), why, HighRiskWorkPolicy.Count(why, "CRITICAL"), HighRiskWorkPolicy.Count(why, "REVIEW"), "/app");

    private static HighRiskDimension D(string dimension, string state, string reason) => new(dimension, state, reason);

    [Fact]
    public void A_live_readiness_blocker_sorts_before_review_only_rows_with_earlier_starts()
    {
        var reviewOnly = Row(Guid.NewGuid(), Now.AddHours(1), D("risk", "READY", "RiskAssessmentApproved"), D("permit", "UNKNOWN", "PermitToWorkSourceUnavailable"));
        var liveBlocked = Row(Guid.NewGuid(), Now.AddDays(3), D("risk", "READY", "RiskAssessmentApproved"), D("permit", "UNKNOWN", "PermitToWorkSourceUnavailable"),
            new("crew", TugReadinessEngine.NotReady, "CrewCertificateExpired"));
        var quiet = Row(Guid.NewGuid(), Now, D("risk", "READY", "RiskAssessmentApproved"));
        var ordered = HighRiskWorkEndpoints.Order([(quiet, Now), (reviewOnly, Now), (liveBlocked, Now)]);
        Assert.Equal([liveBlocked.WorkId, reviewOnly.WorkId, quiet.WorkId], ordered.Select(r => r.WorkId));
        Assert.Equal(1, ordered[0].CriticalBlockers);
    }

    [Fact]
    public void Ties_break_by_planned_start_then_most_recent_update_then_id()
    {
        var late = Row(Guid.NewGuid(), Now.AddDays(1), D("permit", "UNKNOWN", "PermitToWorkSourceUnavailable"));
        var early = Row(Guid.NewGuid(), Now, D("permit", "UNKNOWN", "PermitToWorkSourceUnavailable"));
        var unscheduledOld = Row(Guid.NewGuid(), null, D("permit", "UNKNOWN", "PermitToWorkSourceUnavailable"));
        var unscheduledNew = Row(Guid.NewGuid(), null, D("permit", "UNKNOWN", "PermitToWorkSourceUnavailable"));
        var ordered = HighRiskWorkEndpoints.Order([(unscheduledOld, Now.AddDays(-2)), (late, Now), (unscheduledNew, Now), (early, Now)]);
        Assert.Equal([early.WorkId, late.WorkId, unscheduledNew.WorkId, unscheduledOld.WorkId], ordered.Select(r => r.WorkId));
    }
}
