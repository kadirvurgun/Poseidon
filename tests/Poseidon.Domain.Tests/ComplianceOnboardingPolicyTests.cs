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
            Imo = new("Known", "1234567"), Flag = new("Known", "FICTIONAL"), ClassSociety = new("Known", "FICTIONAL"),
            ShipType = new("Known", "FICTIONAL"), Gt = new("Known", "0"), BuildDate = new("Known", "2000-01-01"),
            TradingArea = new("Known", "FICTIONAL"), CargoContext = new("Known", "FICTIONAL") };
        var result = ComplianceOnboardingPolicy.Assess(Vessel, "FICTIONAL", "FICTIONAL vessel", profile);
        Assert.Equal("DECLARED_COMPLETE_UNVERIFIED", result.State); Assert.Equal(10, result.Fields.Length);
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
        Assert.Equal(10, result.Fields.Select(f => f.Key).Distinct().Count());
        Assert.Contains(result.BlockerDetails, b => b.FieldPath == "profile" && b.ReasonCode == "PROFILE_MISSING" && b.ResolutionType == "PROFILE_DECLARATION");
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
