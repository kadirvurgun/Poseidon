using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-020 / T-S2-001: DEV020-AC-03..06. Declared provenance and computed freshness rules.
public sealed class VesselProfileFieldPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static ProfileFieldSourceFacts Source(string kind = "DOCUMENT", string referenceState = "Known", string observedState = "Known", DateTimeOffset? observed = null) =>
        new(kind, referenceState, referenceState == "Known" ? "Synthetic certificate ref" : null, observedState,
            observedState == "Known" ? observed ?? Now.AddDays(-10) : null);
    private static FreshnessPolicyResolution Policy(TimeSpan age) => new("RESOLVED", "synthetic-test-policy", new Dictionary<string, TimeSpan> { ["gt"] = age });
    private static DeclareProfileFieldSource Command(string? field = "gt", string? kind = "DOCUMENT", TextInput? reference = null, TextInput? observed = null) =>
        new(field, 2, 0, kind, reference ?? new("Known", "  Synthetic ref  "), observed ?? new("Known", "2026-11-01T10:00:00+03:00"));

    [Fact]
    public void Tracked_fields_cover_the_controlled_profile_identity_class_type_tonnage_build_trading_and_cargo_values()
    {
        foreach (var path in new[] { "imo", "flag", "classSociety", "classNumber", "shipType", "gt", "buildDate", "tradingArea", "cargoContext" })
            Assert.Contains(path, VesselProfileFieldPolicy.Paths);
        Assert.Equal(17, VesselProfileFieldPolicy.Fields.Count);
        Assert.DoesNotContain("sourceObservedAt", VesselProfileFieldPolicy.Paths);
        Assert.Equal("TONNAGE_INDEX", VesselProfileFieldPolicy.Fields.Single(x => x.Path == "gt").Unit);
        var content = new RegulatoryDraftContent { Gt = new("Known", "12000.5"), Flag = new("Unknown") };
        Assert.Equal(("Known", "12000.5"), VesselProfileFieldPolicy.Value(content, "gt"));
        Assert.Equal(("Unknown", (string?)null), VesselProfileFieldPolicy.Value(content, "flag"));
        Assert.Throws<ArgumentOutOfRangeException>(() => VesselProfileFieldPolicy.Value(content, "declaration"));
    }

    [Fact]
    public void Value_hash_distinguishes_state_and_exact_value()
    {
        Assert.Equal(VesselProfileFieldPolicy.ValueHash("Known", "1"), VesselProfileFieldPolicy.ValueHash("Known", "1"));
        Assert.NotEqual(VesselProfileFieldPolicy.ValueHash("Known", "1"), VesselProfileFieldPolicy.ValueHash("Known", "1.0"));
        Assert.NotEqual(VesselProfileFieldPolicy.ValueHash("Unknown", null), VesselProfileFieldPolicy.ValueHash("Missing", null));
        Assert.Matches("^[0-9a-f]{64}$", VesselProfileFieldPolicy.ValueHash("Known", "x"));
    }

    [Fact]
    public void Normalize_trims_reference_and_converts_offset_observation_to_utc()
    {
        var (command, errors) = VesselProfileFieldPolicy.Normalize(Command(), Now);
        Assert.Empty(errors);
        Assert.Equal(new TextInput("Known", "Synthetic ref"), command.Reference);
        Assert.Equal("2026-11-01T07:00:00.0000000+00:00", command.ObservedAt!.Value);
        var (blank, blankErrors) = VesselProfileFieldPolicy.Normalize(Command(reference: new("Known", "   ")), Now);
        Assert.Empty(blankErrors); Assert.Equal("Missing", blank.Reference!.State);
    }

    [Theory]
    [InlineData("declaration", "DOCUMENT", "fieldPath", "unknown_field")]
    [InlineData("gt", "VERIFIED", "sourceKind", "invalid_source_kind")]
    [InlineData(null, "DOCUMENT", "fieldPath", "unknown_field")]
    public void Normalize_rejects_unknown_fields_and_kinds(string? field, string kind, string key, string code)
    {
        var (_, errors) = VesselProfileFieldPolicy.Normalize(Command(field, kind), Now);
        Assert.Equal([code], errors[key]);
    }

    [Fact]
    public void Normalize_rejects_future_naive_and_inconsistent_observations_and_fences()
    {
        Assert.Equal(["observed_at_in_future"], VesselProfileFieldPolicy.Normalize(Command(observed: new("Known", "2026-11-19T00:00:00Z")), Now).Errors["observedAt"]);
        Assert.Equal(["offset_timestamp_required"], VesselProfileFieldPolicy.Normalize(Command(observed: new("Known", "2026-11-01T00:00:00")), Now).Errors["observedAt"]);
        Assert.Equal(["invalid_state_value"], VesselProfileFieldPolicy.Normalize(Command(observed: new("Unknown", "2026-11-01T00:00:00Z")), Now).Errors["observedAt"]);
        Assert.Equal(["invalid_state_value"], VesselProfileFieldPolicy.Normalize(Command(reference: new("Bogus")), Now).Errors["reference"]);
        Assert.Equal(["invalid_text"], VesselProfileFieldPolicy.Normalize(Command(reference: new("Known", new string('x', 501))), Now).Errors["reference"]);
        var fences = VesselProfileFieldPolicy.Normalize(Command() with { ExpectedValueRevision = 0, ExpectedSequence = -1 }, Now).Errors;
        Assert.Contains("expectedValueRevision", fences.Keys); Assert.Contains("expectedSequence", fences.Keys);
    }

    [Theory]
    [InlineData("Missing", "VALUE_NOT_RECORDED")]
    [InlineData("Unknown", "VALUE_UNKNOWN")]
    public void Missing_or_unknown_value_is_never_fresh(string state, string reason)
    {
        var result = VesselProfileFieldPolicy.Evaluate("gt", state, 1, Source(), false, Policy(TimeSpan.FromDays(365)), Now);
        Assert.Equal(("UNKNOWN", reason), (result.State, result.ReasonCode));
    }

    [Fact]
    public void Source_gaps_produce_explicit_unknown_reasons_in_order()
    {
        var policy = Policy(TimeSpan.FromDays(365));
        Assert.Equal("SOURCE_NOT_DECLARED", VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, null, false, policy, Now).ReasonCode);
        Assert.Equal("SOURCE_DECLARED_FOR_PRIOR_VALUE", VesselProfileFieldPolicy.Evaluate("gt", "Known", 2, null, true, policy, Now).ReasonCode);
        Assert.Equal("FIELD_VERSION_UNAVAILABLE", VesselProfileFieldPolicy.Evaluate("gt", "Known", null, Source(), false, policy, Now).ReasonCode);
        Assert.Equal("SOURCE_UNKNOWN", VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source("UNKNOWN"), false, policy, Now).ReasonCode);
        Assert.Equal("SOURCE_REFERENCE_NOT_RECORDED", VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source(referenceState: "Unknown"), false, policy, Now).ReasonCode);
        Assert.Equal("OBSERVED_AT_UNKNOWN", VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source(observedState: "Unknown"), false, policy, Now).ReasonCode);
        Assert.Equal("OBSERVED_AT_NOT_RECORDED", VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source(observedState: "Missing"), false, policy, Now).ReasonCode);
        var future = VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source(observed: Now.AddMinutes(1)), false, policy, Now);
        Assert.Equal(("UNKNOWN", "OBSERVED_AT_AFTER_CHECK"), (future.State, future.ReasonCode));
    }

    [Fact]
    public void Without_an_approved_policy_a_complete_declared_source_stays_unknown_with_age_shown()
    {
        var result = VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source(), false, FreshnessPolicyResolution.Unconfigured, Now);
        Assert.Equal(("UNKNOWN", "FRESHNESS_POLICY_UNCONFIGURED"), (result.State, result.ReasonCode));
        Assert.Equal(Now.AddDays(-10), result.ObservedAt); Assert.Equal(Now, result.CheckedAt); Assert.Equal(864000, result.AgeSeconds);
        var unrelated = VesselProfileFieldPolicy.Evaluate("flag", "Known", 1, Source(), false, Policy(TimeSpan.FromDays(30)), Now);
        Assert.Equal("FRESHNESS_POLICY_UNCONFIGURED", unrelated.ReasonCode);
        var unavailable = VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source(), false, FreshnessPolicyResolution.Unavailable, Now);
        Assert.Equal(("UNAVAILABLE", "FRESHNESS_POLICY_UNAVAILABLE"), (unavailable.State, unavailable.ReasonCode));
    }

    [Fact]
    public void Resolved_policy_marks_fresh_at_the_boundary_and_stale_after_it()
    {
        var boundary = VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source(observed: Now.AddDays(-30)), false, Policy(TimeSpan.FromDays(30)), Now);
        Assert.Equal(("FRESH", "WITHIN_POLICY_MAX_AGE"), (boundary.State, boundary.ReasonCode));
        var stale = VesselProfileFieldPolicy.Evaluate("gt", "Known", 1, Source(observed: Now.AddDays(-30).AddTicks(-10)), false, Policy(TimeSpan.FromDays(30)), Now);
        Assert.Equal(("STALE", "POLICY_MAX_AGE_EXCEEDED"), (stale.State, stale.ReasonCode));
    }

    [Fact]
    public void Summary_never_reports_fresh_while_any_field_is_unknown_stale_or_unavailable()
    {
        FieldFreshness F(string state, int days = 1) => new(state, "R", Now.AddDays(-days), Now, null);
        Assert.Equal("FRESH", VesselProfileFieldPolicy.Summarize([F("FRESH"), F("FRESH")], Now).State);
        Assert.Equal("UNKNOWN", VesselProfileFieldPolicy.Summarize([F("FRESH"), F("UNKNOWN")], Now).State);
        Assert.Equal("STALE", VesselProfileFieldPolicy.Summarize([F("UNKNOWN"), F("STALE")], Now).State);
        var all = VesselProfileFieldPolicy.Summarize([F("STALE", 3), F("UNAVAILABLE", 9), F("FRESH")], Now);
        Assert.Equal(("UNAVAILABLE", "FIELDS_UNAVAILABLE", 1, 1, 0, 1), (all.State, all.ReasonCode, all.Fresh, all.Stale, all.Unknown, all.Unavailable));
        Assert.Equal(Now.AddDays(-9), all.OldestObservedAt);
        Assert.Equal(("UNKNOWN", "NO_PROFILE_FIELDS"), (VesselProfileFieldPolicy.Summarize([], Now).State, VesselProfileFieldPolicy.Summarize([], Now).ReasonCode));
    }
}
