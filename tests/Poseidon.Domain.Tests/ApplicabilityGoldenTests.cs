using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// 09.02 Golden Tests rows2..7 and exact 09.04 fixtures. These are controlled
// software expectations, NEVER real vessel facts, approved sources or a catalog.
public sealed class ApplicabilityGoldenTests
{
    private static readonly DateTimeOffset AsOf = DateTimeOffset.Parse("2026-11-02T00:00:00Z", CultureInfo.InvariantCulture);
    [Fact]
    public void Controlled_example_bytes_are_preserved_only_as_test_fixtures() => Assert.Equal(
        "46EEDEE19A973C234E21724B08B1851D0A42AF5578402CBDE47D266680A2828D",
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/applicability.sample-rules.json")))));
    private static ApplicabilityRule Sample(string id) {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/applicability.sample-rules.json")));
        return ApplicabilityEngine.Parse(file.RootElement.EnumerateArray().Single(r => r.GetProperty("ruleId").GetString() == id).GetRawText());
    }
    private static Dictionary<string, ContextFact> Harbour() {
        var values = new Dictionary<string, object> { ["context.mode"] = "TUG", ["vessel.flag"] = "TR", ["vessel.contractForConstructionDate"] = "2022-03-14",
            ["equipment.towingWinchFitted"] = true, ["class.rulePack.IACS_M79"] = true, ["operation.profile"] = "CLOSE_QUARTERS_TOWAGE", ["regulatory.ismApplicable"] = true, ["regulatory.msmdVerified"] = true };
        return values.ToDictionary(p => p.Key, p => new ContextFact(ApplicabilityEngine.Fields[p.Key], "Known", JsonSerializer.SerializeToElement(p.Value), "SyntheticGoldenSoftwareFixture", "09.02:Golden Profiles/row:5"));
    }
    [Fact]
    public void GOLD_001_towage_prediction_retains_activation_list_but_has_no_current_authority() {
        var r = Sample("RULE-ISM-7-TUG"); var result = ApplicabilityEngine.Preview(r, Harbour(), AsOf);
        Assert.Equal("APPLICABLE", result.Result); Assert.Contains(result.PredictedActivations, x => x.GetProperty("id").GetString() == "towage-plan");
        Assert.False(result.Authoritative); Assert.Equal("REVIEW_REQUIRED", result.CurrentResult); Assert.True(r.ExampleOnly); Assert.Equal("PENDING", r.SourceVerificationStatus);
    }
    [Theory]
    [InlineData("Known", "CLOSE_QUARTERS_TOWAGE", "APPLICABLE")]
    [InlineData("Unknown", "CLOSE_QUARTERS_TOWAGE", "REVIEW_REQUIRED")]
    [InlineData("Known", "SOLELY_LONG_DISTANCE_OCEAN_TOWAGE", "NOT_APPLICABLE")]
    public void GOLD_002_003_006_m79_class_and_ocean_exclusion(string classKnowledge, string profile, string expected) {
        // Matrix RULE-IACS-M79 aliases the explicitly longer 09.04 fixture ID.
        var r = Sample("RULE-IACS-M79-APPLICABILITY"); var facts = Harbour();
        facts["class.rulePack.IACS_M79"] = facts["class.rulePack.IACS_M79"] with { Knowledge = classKnowledge };
        facts["operation.profile"] = facts["operation.profile"] with { Value = JsonSerializer.SerializeToElement(profile) };
        Assert.Equal(expected, ApplicabilityEngine.Preview(r, facts, AsOf).Result);
    }
    [Fact]
    public void GOLD_004_applicability_is_separate_from_unverified_manning_readiness() {
        var facts = Harbour(); facts["regulatory.msmdVerified"] = facts["regulatory.msmdVerified"] with { Knowledge = "Unverified" };
        // Matrix RULE-TR-TUG-MSMD maps to 09.04 RULE-TUG-MIN-MANNING-MSMD.
        var result = ApplicabilityEngine.Preview(Sample("RULE-TUG-MIN-MANNING-MSMD"), facts, AsOf);
        Assert.Equal("APPLICABLE", result.Result); Assert.Equal("REVIEW_REQUIRED", result.CurrentResult); Assert.False(result.Authoritative);
        Assert.Contains(result.PredictedActivations, x => x.GetProperty("id").GetString() == "tug-crew-readiness.minimum_manning_state");
        Assert.True(ComplianceInputs.ManningReview(Sample("RULE-TUG-MIN-MANNING-MSMD"), facts));
        // Owning MSMD/role consumers are separately authored; this is a domain case.
    }
    [Fact]
    public void GOLD_005_escort_overlay_stays_unavailable_without_validated_source() {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/applicability.sample-rules.json")));
        // The matrix requires a validated escort source, absent from 09.04. Preserve
        // that concrete conflict/gap rather than invent a rule or extra crew approval.
        Assert.DoesNotContain(file.RootElement.EnumerateArray(), r => r.GetProperty("ruleId").GetString() == "RULE-TR-TUG-ESCORT-EXTRA");
        var rule = Sample("RULE-IACS-M79-APPLICABILITY");
        var review = ApplicabilityEngine.Preview(rule, new Dictionary<string, ContextFact>(), AsOf);
        Assert.Equal("REVIEW_REQUIRED", review.Result); Assert.False(review.Authoritative);
    }
    [Fact]
    public void Contradictory_candidates_preserve_both_obligations_and_never_choose_priority_winner() {
        var a = Sample("RULE-ISM-7-TUG"); var then = JsonNode.Parse(a.Then.GetRawText())!; then["result"] = "NOT_APPLICABLE";
        var b = a with { RuleId = "FICTIONAL-CONTRADICTION", Priority = int.MaxValue, Then = JsonSerializer.SerializeToElement(then) };
        var first = ApplicabilityEngine.Preview(a, Harbour(), AsOf); var second = ApplicabilityEngine.Preview(b, Harbour(), AsOf);
        var conflicts = ApplicabilityEngine.Conflicts([new("synthetic-a/1", a, first), new("synthetic-b/1", b, second)]);
        Assert.NotEmpty(conflicts); Assert.All(conflicts, c => { Assert.Equal("REVIEW_REQUIRED", c.Status); Assert.Equal(new[] { "synthetic-a/1", "synthetic-b/1" }, c.RuleReferences); });
        Assert.NotEmpty(first.PredictedActivations); Assert.Equal("NOT_APPLICABLE", second.Result);
    }
}
