using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S11 WP-B DEV-072 (18.01 Backlog row 73, T-S11-004; 19.01 paragraph:136-161).
[Trait("Requirement", "DEV-072")]
public sealed class PilotMetricsPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 37, 12, TimeSpan.Zero);
    private static PilotMetricEventInput E(string kind, string area = "SHIP", string? journey = null, string? ai = null, int? ms = null, DateTimeOffset? at = null) =>
        new(kind, area, journey, ai, ms, at ?? Now.AddMinutes(-1));
    private static PilotMetricBatchView B(string actor, params PilotMetricEventRecord[] events) => new(actor.PadLeft(32, '0'), new string('a', 32), events);
    private static PilotMetricEventRecord R(string kind, string area = "SHIP", string? journey = null, string? ai = null, int? ms = null) => new(kind, area, journey, ai, ms, PilotMetricsPolicy.Hour(Now));

    [Fact]
    public void Closed_schema_accepts_only_known_codes_in_the_right_shape()
    {
        Assert.Null(PilotMetricsPolicy.Validate(E("JOURNEY_STARTED", journey: "SHIP_DEPARTURE"), Now));
        Assert.Null(PilotMetricsPolicy.Validate(E("JOURNEY_COMPLETED", "TUG", "TUG_EXECUTION"), Now));
        Assert.Null(PilotMetricsPolicy.Validate(E("AI_SUGGESTION_CORRECTED", "TUG", ai: "TUG_RECOMMENDATION"), Now));
        Assert.Null(PilotMetricsPolicy.Validate(E("EVIDENCE_SEARCH", ms: 0), Now));
        Assert.Null(PilotMetricsPolicy.Validate(E("SYNC_CONFLICT", "TUG"), Now));
        Assert.Equal("invalid_event_code", PilotMetricsPolicy.Validate(E("PAGE_VIEW"), Now));
        Assert.Equal("invalid_event_code", PilotMetricsPolicy.Validate(E("ROLE_ACTIVE", "BOTH"), Now));
        Assert.Equal("invalid_event_code", PilotMetricsPolicy.Validate(E("JOURNEY_STARTED", "SHIP", "TUG_PLAN"), Now)); // tug journey on ship
        Assert.Equal("invalid_event_code", PilotMetricsPolicy.Validate(E("JOURNEY_STARTED", journey: "SHIP_DEPARTURE report by J. Doe"), Now));
        Assert.Equal("invalid_event_code", PilotMetricsPolicy.Validate(E("AI_SUGGESTION_ACCEPTED", ai: "TUG_RECOMMENDATION"), Now));
        Assert.Equal("invalid_event_shape", PilotMetricsPolicy.Validate(E("JOURNEY_STARTED"), Now));
        Assert.Equal("invalid_event_shape", PilotMetricsPolicy.Validate(E("ROLE_ACTIVE", journey: "SHIP_PMS"), Now));
        Assert.Equal("invalid_event_shape", PilotMetricsPolicy.Validate(E("ROLE_ACTIVE", ms: 5), Now));
        Assert.Equal("invalid_event_shape", PilotMetricsPolicy.Validate(E("AI_SUGGESTION_REJECTED"), Now));
        Assert.Equal("invalid_event_shape", PilotMetricsPolicy.Validate(E("EVIDENCE_SEARCH"), Now));
        Assert.Equal("invalid_event_value", PilotMetricsPolicy.Validate(E("EVIDENCE_SEARCH", ms: -1), Now));
        Assert.Equal("invalid_event_value", PilotMetricsPolicy.Validate(E("EVIDENCE_SEARCH", ms: 600_001), Now));
        Assert.Equal("invalid_event_time", PilotMetricsPolicy.Validate(E("ROLE_ACTIVE", at: Now.AddDays(-8)), Now));
        Assert.Equal("invalid_event_time", PilotMetricsPolicy.Validate(E("ROLE_ACTIVE", at: Now.AddMinutes(10)), Now));
        Assert.Equal("invalid_event_time", PilotMetricsPolicy.Validate(E("ROLE_ACTIVE", at: new DateTimeOffset(Now.DateTime, TimeSpan.FromHours(3))), Now));
    }

    [Fact]
    public void Stored_record_has_no_identity_and_hour_precision()
    {
        var record = PilotMetricsPolicy.Normalize(E("EVIDENCE_SEARCH", ms: 900, at: Now));
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero), record.OccurredHour);
        // Closed record shape: codes, one duration and the hour; no id, vessel, person, text or device member exists.
        Assert.Equal(["Kind", "Area", "Journey", "AiSource", "DurationMs", "OccurredHour"], typeof(PilotMetricEventRecord).GetProperties().Select(p => p.Name).ToArray());
        Assert.Equal(["Kind", "Area", "Journey", "AiSource", "DurationMs", "OccurredAt"], typeof(PilotMetricEventInput).GetProperties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Bucket_is_salted_stable_and_not_the_subject()
    {
        var salt = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var other = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var actor = "actor:" + Guid.NewGuid();
        var bucket = PilotMetricsPolicy.Bucket(salt, actor);
        Assert.Matches("^[0-9a-f]{32}$", bucket);
        Assert.Equal(bucket, PilotMetricsPolicy.Bucket(salt, actor));
        Assert.NotEqual(bucket, PilotMetricsPolicy.Bucket(other, actor)); // another tenant salt cannot be linked
        Assert.DoesNotContain(actor[6..14], bucket);
        Assert.Throws<ArgumentException>(() => PilotMetricsPolicy.Bucket(new byte[8], actor));
    }

    [Fact]
    public void No_observation_is_unknown_and_small_groups_are_suppressed_never_zero()
    {
        var empty = PilotMetricsPolicy.Summarize([]);
        Assert.Equal(PilotMetricsPolicy.MetricIds, empty.Select(i => i.MetricId).ToArray());
        Assert.All(empty, i => Assert.Equal((MetricStates.Unknown, (string?)null, "NO_OBSERVATIONS"), (i.State, i.Value, i.Reasons.Single())));
        var four = PilotMetricsPolicy.Summarize(Enumerable.Range(1, 4).Select(i => B(i.ToString(), R("SYNC_CONFLICT"))).ToArray());
        var conflicts = four.Single(i => i.MetricId == PilotMetricsPolicy.SyncConflicts);
        Assert.Equal((MetricStates.Suppressed, (string?)null, (int?)null, "SMALL_GROUP"), (conflicts.State, conflicts.Value, conflicts.Contributors, conflicts.Reasons.Single()));
        Assert.All(PilotMetricsPolicy.Unavailable("RETENTION_UNCONFIGURED"), i => Assert.Equal((MetricStates.Unavailable, (string?)null), (i.State, i.Value)));
    }

    [Fact]
    public void Rates_counts_and_percentile_are_computed_over_contributing_observations()
    {
        var batches = Enumerable.Range(1, 6).Select(i => B(i.ToString(),
            R(i <= 5 ? "JOURNEY_COMPLETED" : "JOURNEY_ABANDONED", journey: "SHIP_DEPARTURE"),
            R(i <= 2 ? "AI_SUGGESTION_CORRECTED" : "AI_SUGGESTION_ACCEPTED", ai: "OCR"),
            R("EVIDENCE_SEARCH", ms: i * 100),
            R("SYNC_CONFLICT"))).ToArray();
        var result = PilotMetricsPolicy.Summarize(batches).ToDictionary(i => i.MetricId);
        Assert.Equal(("833", (int?)6), (result[PilotMetricsPolicy.JourneyCompletion].Value, result[PilotMetricsPolicy.JourneyCompletion].Contributors));
        Assert.Equal("333", result[PilotMetricsPolicy.AiCorrectionRate].Value);
        Assert.Equal("6", result[PilotMetricsPolicy.SyncConflicts].Value);
        Assert.Equal("6", result[PilotMetricsPolicy.RoleAdoption].Value);
        Assert.Equal("600", result[PilotMetricsPolicy.EvidenceSearchP95].Value);
        Assert.Equal(19, PilotMetricsPolicy.P95(Enumerable.Range(1, 20).Select(i => (long)i)));
        Assert.Equal(7, PilotMetricsPolicy.P95([7]));
    }

    [Fact]
    public void A_single_suppressed_cell_cannot_be_differenced_out_of_a_published_total()
    {
        // SHIP has 6 contributors, TUG 2: TUG is SMALL_GROUP and SHIP must be suppressed complementarily.
        var batches = Enumerable.Range(1, 6).Select(i => B("s" + i, R("SYNC_CONFLICT")))
            .Concat(Enumerable.Range(1, 2).Select(i => B("t" + i, R("SYNC_CONFLICT", "TUG")))).ToArray();
        var conflicts = PilotMetricsPolicy.Summarize(batches).Single(i => i.MetricId == PilotMetricsPolicy.SyncConflicts);
        Assert.Equal((MetricStates.Value, "8"), (conflicts.State, conflicts.Value));
        var areas = conflicts.Breakdown.Where(b => b.Dimension == "AREA").ToDictionary(b => b.Key);
        Assert.Equal(("SMALL_GROUP", (string?)null), (areas["TUG"].Reasons.Single(), areas["TUG"].Value));
        Assert.Equal((MetricStates.Suppressed, "COMPLEMENTARY_SUPPRESSION", (string?)null), (areas["SHIP"].State, areas["SHIP"].Reasons.Single(), areas["SHIP"].Value));
    }

    [Fact]
    public void Pilot_definitions_are_registered_view_only_and_outside_the_generic_scalar_scopes()
    {
        foreach (var id in PilotMetricsPolicy.MetricIds)
        {
            var d = MetricRegistry.Find(id)!;
            Assert.Equal([MetricRegistry.ScopePilotWindow], d.Scopes);
            Assert.Equal(["directory.read", PilotMetricsPolicy.ReadPermission], d.RequiredPermissions);
            Assert.Equal((PilotMetricsPolicy.MinContributors, PilotMetricsPolicy.Unit(id)), (d.MinCohort, d.UnitCode));
            Assert.False(MetricRegistry.Permits(d, ["directory.read"]));
        }
    }
}
