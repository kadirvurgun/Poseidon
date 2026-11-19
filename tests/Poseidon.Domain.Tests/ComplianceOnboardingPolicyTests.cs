using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class ComplianceOnboardingPolicyTests
{
    private static readonly Guid Vessel = Guid.Parse("10000000-0000-4000-8000-000000000001");
    [Fact]
    public void Missing_and_unknown_required_declarations_never_become_no_or_not_applicable() {
        var result = ComplianceOnboardingPolicy.Assess(Vessel, "FICTIONAL", "FICTIONAL vessel", new() { Flag = new("Unknown") });
        Assert.Equal("REVIEW_REQUIRED", result.State);
        Assert.Equal("Unknown", Assert.Single(result.Fields, f => f.Key == "profile.flag").State);
        Assert.Contains("REQUIRED_DATA_UNKNOWN", result.Blockers);
        Assert.Contains("REQUIRED_DATA_MISSING", result.Blockers);
        var unknown = Assert.Single(result.BlockerDetails, b => b.FieldPath == "profile.flag");
        Assert.Equal(Vessel, unknown.SubjectId); Assert.Equal("REQUIRED_DATA_UNKNOWN", unknown.ReasonCode);
        Assert.Equal("POLICY_OR_EVIDENCE", unknown.ResolutionType);
        Assert.All(result.Fields, f => Assert.True(f.Critical));
    }
    [Fact]
    public void Complete_declared_context_is_still_unverified_and_never_ready() {
        var profile = new RegulatoryDraftContent {
            Imo = new("Known", "1234567"), Flag = new("Known", "FICTIONAL"), ClassSociety = new("Known", "FICTIONAL"), ClassNumber = new("Known", "FICTIONAL-0001"),
            ShipType = new("Known", "FICTIONAL"), Gt = new("Known", "0"), BuildDate = new("Known", "2000-01-01"),
            TradingArea = new("Known", "FICTIONAL"), CargoContext = new("Known", "FICTIONAL") };
        var result = ComplianceOnboardingPolicy.Assess(Vessel, "FICTIONAL", "FICTIONAL vessel", profile);
        Assert.Equal("DECLARED_COMPLETE_UNVERIFIED", result.State); Assert.Equal(11, result.Fields.Length);
        Assert.All(result.Fields, f => Assert.True(f.Complete));
        Assert.Equal(["VERIFICATION_NOT_ESTABLISHED"], result.Blockers);
        var blocker = Assert.Single(result.BlockerDetails);
        Assert.Equal("profile", blocker.FieldPath); Assert.Equal("QUALIFIED_EVIDENCE", blocker.ResolutionType);
        Assert.Equal("0", profile.Gt.Value); // no legal minimum is invented by the UI completeness gate
    }
    [Fact]
    public void Missing_profile_and_blank_known_identity_are_explicit_incomplete_states() {
        var result = ComplianceOnboardingPolicy.Assess(Vessel, " ", "FICTIONAL", null);
        Assert.Contains("PROFILE_MISSING", result.Blockers); Assert.Equal("REVIEW_REQUIRED", result.State);
        Assert.Equal("Missing", Assert.Single(result.Fields, f => f.Key == "vessel.code").State);
        Assert.Equal(11, result.Fields.Select(f => f.Key).Distinct().Count());
        Assert.Contains(result.BlockerDetails, b => b.FieldPath == "profile" && b.ReasonCode == "PROFILE_MISSING" && b.ResolutionType == "PROFILE_DECLARATION");
    }
    // DEV-039 / 18.01 row 40: identity, flag, class (society + number), type, GT, cargo and trading are critical;
    // any one missing or UNKNOWN keeps the assessment REVIEW_REQUIRED and never becomes a verified/ready state.
    [Theory]
    [Trait("Requirement", "DEV-039")]
    [InlineData("profile.imo")] [InlineData("profile.flag")] [InlineData("profile.classSociety")] [InlineData("profile.classNumber")]
    [InlineData("profile.shipType")] [InlineData("profile.gt")] [InlineData("profile.tradingArea")] [InlineData("profile.cargoContext")]
    public void Each_missing_or_unknown_critical_context_field_requires_review(string key) {
        RegulatoryDraftContent Complete() => new() {
            Imo = new("Known", "1234567"), Flag = new("Known", "FICTIONAL"), ClassSociety = new("Known", "FICTIONAL"), ClassNumber = new("Known", "FICTIONAL-0001"),
            ShipType = new("Known", "FICTIONAL"), Gt = new("Known", "1"), BuildDate = new("Known", "2000-01-01"),
            TradingArea = new("Known", "FICTIONAL"), CargoContext = new("Known", "FICTIONAL") };
        foreach (var state in new[] { "Missing", "Unknown" }) {
            var p = Complete();
            p = key switch {
                "profile.imo" => p with { Imo = new(state) }, "profile.flag" => p with { Flag = new(state) },
                "profile.classSociety" => p with { ClassSociety = new(state) }, "profile.classNumber" => p with { ClassNumber = new(state) },
                "profile.shipType" => p with { ShipType = new(state) }, "profile.gt" => p with { Gt = new(state) },
                "profile.tradingArea" => p with { TradingArea = new(state) }, _ => p with { CargoContext = new(state) } };
            var result = ComplianceOnboardingPolicy.Assess(Vessel, "FICTIONAL", "FICTIONAL vessel", p);
            Assert.Equal("REVIEW_REQUIRED", result.State);
            var field = Assert.Single(result.Fields, f => f.Key == key);
            Assert.Equal(state, field.State); Assert.False(field.Complete); Assert.True(field.Critical);
            Assert.Contains(state == "Unknown" ? "REQUIRED_DATA_UNKNOWN" : "REQUIRED_DATA_MISSING", result.Blockers);
            Assert.Contains("VERIFICATION_NOT_ESTABLISHED", result.Blockers);
            Assert.DoesNotContain(result.Blockers, b => b.Contains("VERIFIED", StringComparison.Ordinal) && b != "VERIFICATION_NOT_ESTABLISHED");
        }
        Assert.Equal("DECLARED_COMPLETE_UNVERIFIED", ComplianceOnboardingPolicy.Assess(Vessel, "FICTIONAL", "FICTIONAL vessel", Complete()).State);
    }
    [Fact]
    public void Invalid_legacy_known_values_are_review_required_instead_of_counting_as_complete() {
        var result = ComplianceOnboardingPolicy.Assess(Vessel, "FICTIONAL", "FICTIONAL", new() {
            Imo = new("Known", "not-an-imo"), Gt = new("Known", "NaN"), BuildDate = new("Known", "not-a-date") });
        foreach (var key in new[] { "profile.imo", "profile.gt", "profile.buildDate" }) {
            var field = Assert.Single(result.Fields, f => f.Key == key); Assert.Equal("Unknown", field.State); Assert.False(field.Complete);
        }
        Assert.Equal("REVIEW_REQUIRED", result.State);
    }
}
