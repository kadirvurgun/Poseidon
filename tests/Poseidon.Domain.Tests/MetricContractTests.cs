using System.Text.Json;
using System.Text.Json.Nodes;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// PLAN-1511-5501-SLICE-01 P5501S-AC-01..05. 55.01 paragraph:646-647, 1397, 1704-1705.
[Trait("Requirement", "PLAN-1511-5501-SLICE")]
public sealed class MetricContractTests
{
    private static MetricDefinition Vessel => MetricRegistry.Find(MetricRegistry.OnboardingVesselsReviewRequired)!;
    private static MetricDefinition Crew => MetricRegistry.Find(MetricRegistry.CrewDeclaredPersons)!;
    private static MetricDefinition Fields => MetricRegistry.Find(MetricRegistry.OnboardingRequiredFieldsUnknown)!;

    [Fact]
    public void Registry_matches_the_published_consumer_contract_file()
    {
        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "metrics.v1.json")))!;
        var registry = JsonNode.Parse(JsonSerializer.Serialize(new { contractVersion = "metrics.v1", items = MetricRegistry.All },
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
        Assert.True(JsonNode.DeepEquals(file, registry), "contracts/metrics.v1.json must equal MetricRegistry.All; bump Version for any change.");
    }

    [Fact]
    public void Registry_ids_are_unique_versioned_and_view_only()
    {
        Assert.Equal(MetricRegistry.All.Length, MetricRegistry.All.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(MetricRegistry.All, d => {
            Assert.True(d.Version >= 1); Assert.True(d.MinCohort >= 1);
            Assert.Equal(["view"], d.AllowedActions); // No export action in this slice.
            Assert.Contains("directory.read", d.RequiredPermissions);
            Assert.Equal("DECLARED_UNVERIFIED", d.Basis);
            Assert.False(string.IsNullOrWhiteSpace(d.SourceProjectionId)); Assert.Contains("55.01", d.Source + d.FreshnessRule);
        });
        Assert.Null(MetricRegistry.Find("unknown.metric")); Assert.Null(MetricRegistry.Find(null));
        Assert.False(MetricRegistry.Permits(Crew, ["directory.read"]));
        Assert.True(MetricRegistry.Permits(Crew, ["directory.read", "crew.read"]));
        Assert.False(MetricRegistry.Permits(Vessel, ["directory.read"], "export"));
        // QA follow-up 4: the crew scalar is explicitly declared-any-status, never current crew.
        Assert.Equal(2, Crew.Version); Assert.Contains("any status", Crew.Label); Assert.Contains("not current crew", Crew.Label);
        Assert.Equal(2, Fields.Version);
    }

    [Fact]
    public void Missing_or_withheld_sources_are_unknown_never_zero()
    {
        var none = MetricScalarPolicy.Evaluate(Vessel, new(0, 0, 0, 0));
        Assert.Equal(MetricStates.Unknown, none.State); Assert.Null(none.Value); Assert.Contains("NO_VISIBLE_SOURCE", none.Reasons);
        var withheld = MetricScalarPolicy.Evaluate(Vessel, new(3, 0, 0, 0));
        Assert.Equal(MetricStates.Unknown, withheld.State); Assert.Null(withheld.Value); Assert.Equal(3, withheld.UnknownCount);
        Assert.Equal(MetricQualities.Unknown, withheld.Quality);
        var nullCount = MetricScalarPolicy.Evaluate(Vessel, new(2, 2, null, 0));
        Assert.Equal(MetricStates.Unknown, nullCount.State);
        var unavailable = MetricScalarPolicy.Evaluate(Vessel, new(1001, 0, null, 0, null, "QUERY_LIMIT_EXCEEDED"));
        Assert.Equal(MetricStates.Unavailable, unavailable.State); Assert.Null(unavailable.Value);
        Assert.Equal(["QUERY_LIMIT_EXCEEDED"], unavailable.Reasons);
    }

    [Fact]
    public void Real_zero_over_read_sources_is_a_complete_value_and_partial_coverage_is_marked()
    {
        var zero = MetricScalarPolicy.Evaluate(Vessel, new(2, 2, 0, 0));
        Assert.Equal(MetricStates.Value, zero.State); Assert.Equal(0, zero.Value); Assert.Equal(MetricQualities.Complete, zero.Quality);
        Assert.Empty(zero.Reasons);
        var partial = MetricScalarPolicy.Evaluate(Vessel, new(3, 2, 1, 0));
        Assert.Equal(MetricStates.Value, partial.State); Assert.Equal(1, partial.Value);
        Assert.Equal(MetricQualities.Partial, partial.Quality); Assert.Equal(1, partial.UnknownCount);
        Assert.Contains("PARTIAL_SOURCE_COVERAGE", partial.Reasons);
        var unknownValues = MetricScalarPolicy.Evaluate(Vessel, new(1, 1, 4, 2));
        Assert.Equal(MetricQualities.Partial, unknownValues.Quality); Assert.Contains("UNKNOWN_SOURCE_VALUES", unknownValues.Reasons);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(4L)]
    public void Small_group_is_suppressed_without_value(long count)
    {
        var result = MetricScalarPolicy.Evaluate(Crew, new(1, 1, count, 0, [count]));
        Assert.Equal(MetricStates.Suppressed, result.State); Assert.Null(result.Value);
        Assert.Contains("SMALL_GROUP", result.Reasons);
    }

    [Fact]
    public void Threshold_and_empty_group_are_not_suppressed_but_complementary_cells_are()
    {
        Assert.Equal(5, MetricScalarPolicy.Evaluate(Crew, new(1, 1, 5, 0, [5])).Value);
        Assert.Equal(0, MetricScalarPolicy.Evaluate(Crew, new(1, 1, 0, 0, [])).Value);
        // Fleet total 7 = 5 + 2: publishing it would let the 2-person vessel be differenced out.
        var fleet = MetricScalarPolicy.Evaluate(Crew, new(2, 2, 7, 0, [5, 2]));
        Assert.Equal(MetricStates.Suppressed, fleet.State); Assert.Null(fleet.Value);
        Assert.Contains("COMPLEMENTARY_SUPPRESSION", fleet.Reasons);
        Assert.Equal(10, MetricScalarPolicy.Evaluate(Crew, new(2, 2, 10, 0, [5, 5])).Value);
        // Non-personal metrics (MinCohort 1) are never suppressed.
        Assert.Equal(1, MetricScalarPolicy.Evaluate(Vessel, new(1, 1, 1, 0, [1])).Value);
    }

    [Fact]
    public void Not_collected_sources_are_unknown_or_partial_never_a_complete_zero()
    {
        // QA follow-up 1: no profile declaration on any vessel -> UNKNOWN, not 0 unknown fields.
        var none = MetricScalarPolicy.Evaluate(Fields, new(3, 0, 0, 0, NotCollectedSources: 3));
        Assert.Equal(MetricStates.Unknown, none.State); Assert.Null(none.Value); Assert.Equal(MetricQualities.Unknown, none.Quality);
        Assert.Equal(["SOURCE_NOT_COLLECTED"], none.Reasons); Assert.Equal(3, none.UnknownCount);
        var mixed = MetricScalarPolicy.Evaluate(Fields, new(3, 2, 0, 0, NotCollectedSources: 1));
        Assert.Equal(MetricStates.Value, mixed.State); Assert.Equal(0, mixed.Value); Assert.Equal(MetricQualities.Partial, mixed.Quality);
        Assert.Equal(["PARTIAL_SOURCE_COVERAGE", "SOURCE_NOT_COLLECTED"], mixed.Reasons); Assert.Equal(1, mixed.UnknownCount);
    }

    [Fact]
    public void Unreadable_sources_are_a_separate_cause_from_access_withholding()
    {
        var observation = new MetricObservation(4, 2, 1, 0, UnreadableSources: 1);
        Assert.Equal(1, observation.WithheldSources);
        var result = MetricScalarPolicy.Evaluate(Vessel, observation);
        Assert.Equal(MetricQualities.Partial, result.Quality); Assert.Equal(2, result.UnknownCount);
        Assert.Equal(["PARTIAL_SOURCE_COVERAGE", "SOURCE_WITHHELD", "SOURCE_UNREADABLE"], result.Reasons);
        var corruptOnly = MetricScalarPolicy.Evaluate(Vessel, new(1, 0, 0, 0, UnreadableSources: 1));
        Assert.Equal(MetricStates.Unknown, corruptOnly.State); Assert.Equal(["SOURCE_UNREADABLE"], corruptOnly.Reasons);
        Assert.Equal(["SOURCE_WITHHELD"], MetricScalarPolicy.Evaluate(Vessel, new(2, 0, 0, 0)).Reasons);
    }

    [Fact]
    public void Small_overlap_between_scope_components_is_complementarily_suppressed()
    {
        // QA follow-up 3: A=5, B=5, fleet distinct 9 -> one person on both vessels would be disclosed by 10 - 9.
        var overlap = MetricScalarPolicy.Evaluate(Crew, new(2, 2, 9, 0, [5, 5]));
        Assert.Equal(MetricStates.Suppressed, overlap.State); Assert.Null(overlap.Value);
        Assert.Contains("COMPLEMENTARY_SUPPRESSION", overlap.Reasons);
        // Overlap at or above the cohort threshold is publishable.
        Assert.Equal(10, MetricScalarPolicy.Evaluate(Crew, new(2, 2, 10, 0, [10, 5])).Value);
        Assert.Equal(5, MetricScalarPolicy.Evaluate(Crew, new(2, 2, 5, 0, [5, 5])).Value);
    }

    [Fact]
    public void Inconsistent_observations_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => MetricScalarPolicy.Evaluate(Vessel, new(1, 2, 0, 0)));
        Assert.Throws<ArgumentException>(() => MetricScalarPolicy.Evaluate(Vessel, new(1, 1, -1, 0)));
        Assert.Throws<ArgumentException>(() => MetricScalarPolicy.Evaluate(Vessel, new(1, 1, 0, -1)));
        Assert.Throws<ArgumentException>(() => MetricScalarPolicy.Evaluate(Crew, new(1, 1, 5, 0, [-1])));
        Assert.Throws<ArgumentException>(() => MetricScalarPolicy.Evaluate(Vessel with { MinCohort = 0 }, new(1, 1, 0, 0)));
        Assert.Throws<ArgumentException>(() => MetricScalarPolicy.Evaluate(Vessel, new(2, 1, 0, 0, UnreadableSources: 1, NotCollectedSources: 1)));
        Assert.Throws<ArgumentException>(() => MetricScalarPolicy.Evaluate(Vessel, new(2, 1, 0, 0, NotCollectedSources: -1)));
    }
}
