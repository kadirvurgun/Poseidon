using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// FLT-QUALITY source cases; policy only, not source registration/API acceptance.
public sealed class FleetQualityPolicyTests
{
    private static readonly Guid Catalog = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static FleetQualityRequirementV1 Profile(string path = "/gt", bool nullable = false) => new(FleetQualitySourceKind.PROFILE, path, nullable);
    private static FleetQualityObservationV1 Known(FleetQualityRequirementV1 requirement) => new(requirement,
        FleetQualityObservationState.KNOWN_VALUE, requirement.SourceKind == FleetQualitySourceKind.ROOT ? null : Catalog, long.MaxValue);

    [Fact]
    public void Catalog_and_full_source_coverage_are_required_even_when_every_visible_value_is_known()
    {
        var required = FleetQualityPolicy.Requirements([Profile()]);
        var observed = required.Select(Known).ToArray();
        Assert.Equal(FleetQualityResult.COMPLETE, FleetQualityPolicy.Evaluate(required, observed, Catalog, true));
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required, observed, null, true));
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required, observed, Catalog, false));
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required, observed.SkipLast(1).ToArray(), Catalog, true));
        Assert.Equal(long.MaxValue, observed[0].SourceVersion); // No public JS-number round trip.
        Assert.Null(observed[0].SourceRevisionId);
    }

    [Fact]
    public void Dispute_precedes_unknown_which_precedes_missing_and_old_complete_is_not_reused()
    {
        var required = FleetQualityPolicy.Requirements([Profile(), Profile("/draft")]);
        var known = required.Select(Known).ToArray();
        var missing = known.Select(x => x.Requirement == Profile() ? x with { State = FleetQualityObservationState.MISSING, SourceRevisionId = null, SourceVersion = null } : x).ToArray();
        Assert.Equal(FleetQualityResult.PARTIAL, FleetQualityPolicy.Evaluate(required, missing, Catalog, true));
        var unavailable = missing.Select(x => x.Requirement == Profile("/draft") ? x with { State = FleetQualityObservationState.UNAVAILABLE } : x).ToArray();
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required, unavailable, Catalog, true));
        var disputed = unavailable.Select(x => x.Requirement == Profile() ? x with { State = FleetQualityObservationState.DISPUTED } : x).ToArray();
        Assert.Equal(FleetQualityResult.DISPUTED, FleetQualityPolicy.Evaluate(required, disputed, null, false));
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required, known, Catalog, false));
    }

    [Fact]
    public void Empty_set_and_omitted_roots_cannot_be_complete_and_optional_fields_are_not_required()
    {
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate([], [], Catalog, true));
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate([Profile()], [Known(Profile())], Catalog, true));
        var roots = FleetQualityPolicy.Requirements([]);
        Assert.Equal(3, roots.Length);
        Assert.DoesNotContain(roots, x => x.Path.Contains("Company"));
        Assert.Equal(FleetQualityResult.COMPLETE, FleetQualityPolicy.Evaluate(roots, roots.Select(Known).ToArray(), Catalog, true));
    }

    [Fact]
    public void Requirement_capacity_is_exact_and_never_truncates()
    {
        var full = FleetQualityPolicy.Requirements(Enumerable.Range(0, 97).Select(i => Profile($"/field{i}")));
        Assert.Equal(100, full.Length);
        Assert.Equal(FleetQualityResult.COMPLETE, FleetQualityPolicy.Evaluate(full, full.Select(Known).ToArray(), Catalog, true));
        Assert.Contains("QUALITY_REQUIREMENT_SET_TOO_LARGE", Assert.Throws<ArgumentException>(() =>
            FleetQualityPolicy.Requirements(Enumerable.Range(0, 98).Select(i => Profile($"/field{i}")))).Message);
    }

    [Fact]
    public void Same_pointer_in_different_sources_is_distinct_and_conflicting_native_bindings_stay_unknown()
    {
        var profile = Profile(); var identity = profile with { SourceKind = FleetQualitySourceKind.IDENTITY };
        var required = FleetQualityPolicy.Requirements([profile, identity, profile]);
        Assert.Equal(5, required.Length);
        var all = required.Select(Known).ToArray();
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required, all.Where(x => x.Requirement != identity).ToArray(), Catalog, true));
        Assert.Equal(FleetQualityResult.COMPLETE, FleetQualityPolicy.Evaluate(required, all, Catalog, true));
        var conflicting = FleetQualityPolicy.Requirements([profile, profile with { NullableAllowed = true }]);
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(conflicting, conflicting.Select(Known).ToArray(), Catalog, true));
    }

    [Fact]
    public void Known_values_need_real_source_lineage_and_nullable_does_not_make_missing_not_applicable()
    {
        var required = FleetQualityPolicy.Requirements([Profile(nullable: true)]);
        var observed = required.Select(Known).ToArray();
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required, observed.Select(x => x with { SourceVersion = null }).ToArray(), Catalog, true));
        var absent = observed.Select(x => x.Requirement.SourceKind == FleetQualitySourceKind.PROFILE ?
            x with { State = FleetQualityObservationState.MISSING, SourceRevisionId = null, SourceVersion = null } : x).ToArray();
        Assert.Equal(FleetQualityResult.PARTIAL, FleetQualityPolicy.Evaluate(required, absent, Catalog, true));
        var explicitNativeNa = observed.Select(x => x.Requirement.SourceKind == FleetQualitySourceKind.PROFILE ?
            x with { State = FleetQualityObservationState.KNOWN_NOT_APPLICABLE } : x).ToArray();
        Assert.Equal(FleetQualityResult.COMPLETE, FleetQualityPolicy.Evaluate(required, explicitNativeNa, Catalog, true));
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required,
            observed.Select(x => x.Requirement.SourceKind == FleetQualitySourceKind.ROOT ? x with { SourceRevisionId = Catalog } : x).ToArray(), Catalog, true));
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required,
            observed.Select(x => x.Requirement.SourceKind == FleetQualitySourceKind.ROOT ? x with { State = FleetQualityObservationState.MISSING, SourceVersion = null } : x).ToArray(), Catalog, true));
    }

    [Fact]
    public void Duplicate_observations_and_undefined_states_cannot_hide_a_source_problem()
    {
        var required = FleetQualityPolicy.Requirements([]); var observed = required.Select(Known).ToArray();
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required, [.. observed, observed[0]], Catalog, true));
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required,
            observed.Select(x => x with { State = (FleetQualityObservationState)123 }).ToArray(), Catalog, true));
        var extra = Known(Profile("/optional")) with { State = FleetQualityObservationState.DISPUTED };
        Assert.Equal(FleetQualityResult.COMPLETE, FleetQualityPolicy.Evaluate(required, [.. observed, extra], Catalog, true));
    }

    [Fact]
    public void Unqualified_not_applicable_invalid_source_versions_and_additional_root_fields_stay_unknown()
    {
        var required = FleetQualityPolicy.Requirements([Profile()]);
        var observed = required.Select(Known).ToArray();
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required,
            observed.Select(x => x.Requirement == Profile() ? x with { State = FleetQualityObservationState.KNOWN_NOT_APPLICABLE } : x).ToArray(), Catalog, true));
        foreach (var version in new long[] { 0, -1 })
            Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(required,
                observed.Select(x => x with { SourceVersion = version }).ToArray(), Catalog, true));
        var unsupported = FleetQualityPolicy.Requirements([new(FleetQualitySourceKind.ROOT, "/managingCompanyId", false)]);
        Assert.Equal(FleetQualityResult.UNKNOWN, FleetQualityPolicy.Evaluate(unsupported, unsupported.Select(Known).ToArray(), Catalog, true));
    }
}
