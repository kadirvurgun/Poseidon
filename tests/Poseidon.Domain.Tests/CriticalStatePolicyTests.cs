using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-024 / T-S2-005: DEV024-AC-01..05 pure policy coverage.
public sealed class CriticalStatePolicyTests
{
    private static readonly Guid Vessel = Guid.Parse("10000000-0000-4000-8000-000000000024");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-18T12:00:00Z");
    private static FreshnessSummary Summary(string state = "UNKNOWN", string reason = "FIELDS_UNKNOWN") => new(state, reason, null, Now, 0, 0, 1, 0);
    private static readonly RegulatoryDraftContent Complete = new()
    {
        Imo = new("Known", "1234567"), Flag = new("Known", "FICTIONAL"), ClassSociety = new("Known", "FICTIONAL"),
        ShipType = new("Known", "FICTIONAL"), Gt = new("Known", "10"), BuildDate = new("Known", "2000-01-01"),
        TradingArea = new("Known", "FICTIONAL"), CargoContext = new("Known", "FICTIONAL")
    };

    [Fact, Trait("Requirement", "DEV-024")]
    public void Onboarding_explanation_carries_source_reasons_freshness_and_resolution_typed_blockers()
    {
        var assessment = ComplianceOnboardingPolicy.Assess(Vessel, "SYN", "FICTIONAL", new() { Flag = new("Unknown") });
        var e = CriticalStatePolicy.ForOnboarding(Vessel, assessment, 3, Now, "DeclaredUnverified", Summary(), Now, "/app/x");
        Assert.Equal(("vessel.onboarding", "BLOCKED", "FULL", "NOT_VERIFIED"), (e.Key, e.State, e.Disclosure, e.Verification));
        Assert.Equal(new CriticalStateSource("RegulatoryDraft", Vessel, 3, Now, "DeclaredUnverified"), e.Source);
        Assert.Contains("REQUIRED_DATA_UNKNOWN", e.ReasonCodes!); Assert.Contains("VERIFICATION_NOT_ESTABLISHED", e.ReasonCodes!);
        Assert.Equal(("UNKNOWN", "FIELDS_UNKNOWN"), (e.Freshness!.State, e.Freshness.ReasonCode));
        Assert.Contains(new CriticalStateBlocker("profile.flag", "REQUIRED_DATA_UNKNOWN", "POLICY_OR_EVIDENCE"), e.Blockers!);
        Assert.Contains(new CriticalStateBlocker("profile", "VERIFICATION_NOT_ESTABLISHED", "QUALIFIED_EVIDENCE"), e.Blockers!);
    }

    [Fact, Trait("Requirement", "DEV-024")]
    public void Unknown_only_requires_review_and_complete_declarations_stay_unverified_never_ready()
    {
        var unknown = ComplianceOnboardingPolicy.Assess(Vessel, "SYN", "FICTIONAL", Complete with { Flag = new("Unknown") });
        Assert.Equal("REVIEW_REQUIRED", CriticalStatePolicy.ForOnboarding(Vessel, unknown, 1, Now, "x", Summary(), Now, "/").State);
        var complete = ComplianceOnboardingPolicy.Assess(Vessel, "SYN", "FICTIONAL", Complete);
        var e = CriticalStatePolicy.ForOnboarding(Vessel, complete, 1, Now, "x", Summary("FRESH", "ALL_FIELDS_WITHIN_POLICY"), Now, "/");
        Assert.Equal("DECLARED_UNVERIFIED", e.State);
        var missing = ComplianceOnboardingPolicy.Assess(Vessel, "SYN", "FICTIONAL", null);
        var absent = CriticalStatePolicy.ForOnboarding(Vessel, missing, null, null, "Missing", Summary(), Now, "/");
        Assert.Equal("BLOCKED", absent.State); Assert.Null(absent.Source!.Revision);
        Assert.Contains(new CriticalStateBlocker("profile", "PROFILE_MISSING", "PROFILE_DECLARATION"), absent.Blockers!);
        Assert.DoesNotContain(CriticalStatePolicy.States, s => s is "READY" or "VERIFIED" or "COMPLIANT" or "FRESH");
    }

    [Theory, Trait("Requirement", "DEV-024")]
    [InlineData("FRESH", "DECLARED_UNVERIFIED")]
    [InlineData("STALE", "STALE")]
    [InlineData("UNKNOWN", "UNKNOWN")]
    [InlineData("UNAVAILABLE", "UNAVAILABLE")]
    public void Profile_freshness_maps_server_summary_without_a_positive_state(string summary, string expected)
    {
        var fields = new (string, FieldFreshness)[]
        {
            ("gt", new("UNKNOWN", "SOURCE_NOT_DECLARED", null, Now, null)),
            ("imo", new("FRESH", "WITHIN_POLICY_MAX_AGE", Now, Now, 0)),
            ("flag", new("STALE", "POLICY_MAX_AGE_EXCEEDED", Now.AddDays(-9), Now, 9)),
        };
        var e = CriticalStatePolicy.ForProfileFreshness(Vessel, 4, Now, "DeclaredUnverified", fields, Summary(summary, "FIELDS_X"), Now, "/");
        Assert.Equal(expected, e.State);
        Assert.Equal(["FIELDS_X", "SOURCE_NOT_DECLARED", "POLICY_MAX_AGE_EXCEEDED"], e.ReasonCodes!);
        Assert.Equal([new("profile.gt", "SOURCE_NOT_DECLARED", "SOURCE_DECLARATION"), new("profile.flag", "POLICY_MAX_AGE_EXCEEDED", "SOURCE_RENEWAL")], e.Blockers!);
        Assert.Equal(summary, e.Freshness!.State);
    }

    [Fact, Trait("Requirement", "DEV-024")]
    public void Applicability_unavailable_stale_review_and_current_are_distinct_and_never_compliant()
    {
        var none = CriticalStatePolicy.ForApplicability(Vessel, false, false, null, null, ["QUALIFIED_EVALUATION_UNAVAILABLE"], Summary(), Now, "/");
        Assert.Equal("UNAVAILABLE", none.State); Assert.Null(none.Source);
        Assert.Equal(("UNKNOWN", "QUALIFIED_EVALUATION_UNAVAILABLE"), (none.Freshness!.State, none.Freshness.ReasonCode));
        Assert.Equal("QUALIFIED_EVALUATION", Assert.Single(none.Blockers!).ResolutionType);
        var id = Guid.NewGuid();
        var stale = CriticalStatePolicy.ForApplicability(Vessel, true, false, id, Now, ["PROFILE_CHANGED", "APPLICABILITY_REVIEW_REQUIRED"], Summary(), Now, "/");
        Assert.Equal("STALE", stale.State); Assert.Equal(("STALE", "PROFILE_CHANGED"), (stale.Freshness!.State, stale.Freshness.ReasonCode));
        Assert.Equal(new CriticalStateSource("ApplicabilityEvaluation", id, 1, Now, "QualifiedEvaluation"), stale.Source);
        Assert.Contains(new CriticalStateBlocker("applicability", "PROFILE_CHANGED", "RE_EVALUATION"), stale.Blockers!);
        Assert.Contains(new CriticalStateBlocker("applicability", "APPLICABILITY_REVIEW_REQUIRED", "HUMAN_REVIEW"), stale.Blockers!);
        Assert.Equal("REVIEW_REQUIRED", CriticalStatePolicy.ForApplicability(Vessel, true, false, id, Now, ["APPLICABILITY_REVIEW_REQUIRED"], Summary(), Now, "/").State);
        var current = CriticalStatePolicy.ForApplicability(Vessel, true, true, id, Now, [], Summary(), Now, "/");
        Assert.Equal(("QUALIFIED_CURRENT_UNVERIFIED", "NOT_VERIFIED"), (current.State, current.Verification));
        Assert.Throws<ArgumentException>(() => CriticalStatePolicy.ForApplicability(Vessel, false, true, null, null, [], Summary(), Now, "/"));
    }

    [Fact, Trait("Requirement", "DEV-024")]
    public void Redaction_removes_every_source_detail_and_collapses_reason_bearing_states()
    {
        var assessment = ComplianceOnboardingPolicy.Assess(Vessel, "SYN", "FICTIONAL", new() { Flag = new("Unknown") });
        var full = CriticalStatePolicy.ForOnboarding(Vessel, assessment, 7, Now, "DeclaredUnverified", Summary(), Now, "/app/secret");
        var redacted = CriticalStatePolicy.Redact(full);
        Assert.Equal(new CriticalStateExplanation("vessel.onboarding", Vessel, "REVIEW_REQUIRED", "STATE_ONLY", Now), redacted);
        var json = JsonSerializer.Serialize(redacted, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["key", "subjectId", "state", "disclosure", "asOf"], document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.DoesNotContain("REQUIRED_DATA", json); Assert.DoesNotContain("RegulatoryDraft", json); Assert.DoesNotContain("secret", json);
        foreach (var state in CriticalStatePolicy.States) Assert.Contains(CriticalStatePolicy.Redact(state), CriticalStatePolicy.RedactedStates);
        // DEV024-DEC-02 (QA F3): UNKNOWN reveals a reason class, so it collapses into the
        // non-positive REVIEW_REQUIRED; it never becomes a positive state.
        Assert.Equal("REVIEW_REQUIRED", CriticalStatePolicy.Redact("UNKNOWN")); Assert.Equal("REVIEW_REQUIRED", CriticalStatePolicy.Redact("STALE"));
        Assert.Equal("UNAVAILABLE", CriticalStatePolicy.Redact("UNAVAILABLE"));
        Assert.Equal(["REVIEW_REQUIRED", "UNAVAILABLE"], CriticalStatePolicy.RedactedStates);
        Assert.Equal("REVIEW_REQUIRED", CriticalStatePolicy.Redact("QUALIFIED_CURRENT_UNVERIFIED"));
        Assert.Throws<ArgumentOutOfRangeException>(() => CriticalStatePolicy.Redact("READY"));
    }

    [Fact, Trait("Requirement", "DEV-024")]
    public void Onboarding_without_dev020_detail_omits_only_the_embedded_freshness()
    {
        var assessment = ComplianceOnboardingPolicy.Assess(Vessel, "SYN", "FICTIONAL", new() { Flag = new("Unknown") });
        var full = CriticalStatePolicy.ForOnboarding(Vessel, assessment, 7, Now, "DeclaredUnverified", Summary("STALE", "FIELDS_STALE"), Now, "/app/x");
        var stripped = CriticalStatePolicy.WithoutProfileFreshness(full);
        Assert.Null(stripped.Freshness);
        Assert.Equal(full with { Freshness = null }, stripped);
        Assert.Equal("FULL", stripped.Disclosure);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(stripped, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.False(document.RootElement.TryGetProperty("freshness", out _));
        Assert.DoesNotContain("FIELDS_STALE", document.RootElement.GetRawText());
        Assert.Throws<ArgumentException>(() => CriticalStatePolicy.WithoutProfileFreshness(CriticalStatePolicy.Redact(full)));
        var profile = CriticalStatePolicy.ForProfileFreshness(Vessel, 7, Now, "x", [], Summary(), Now, "/");
        Assert.Throws<ArgumentException>(() => CriticalStatePolicy.WithoutProfileFreshness(profile));
    }

    [Fact, Trait("Requirement", "DEV-024")]
    public void Every_dev020_freshness_reason_has_a_resolution_type()
    {
        foreach (var reason in new[] { "VALUE_NOT_RECORDED", "VALUE_UNKNOWN", "FIELD_VERSION_UNAVAILABLE", "SOURCE_NOT_DECLARED", "SOURCE_DECLARED_FOR_PRIOR_VALUE",
            "SOURCE_UNKNOWN", "SOURCE_REFERENCE_NOT_RECORDED", "OBSERVED_AT_UNKNOWN", "OBSERVED_AT_NOT_RECORDED", "OBSERVED_AT_AFTER_CHECK",
            "FRESHNESS_POLICY_UNCONFIGURED", "FRESHNESS_POLICY_UNAVAILABLE", "POLICY_MAX_AGE_EXCEEDED" })
            Assert.Contains(CriticalStatePolicy.FreshnessResolution(reason), CriticalStatePolicy.ResolutionTypes);
        Assert.Throws<ArgumentOutOfRangeException>(() => CriticalStatePolicy.FreshnessResolution("NEW_REASON"));
    }
}
