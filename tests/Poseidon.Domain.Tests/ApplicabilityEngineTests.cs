using System.Globalization;
using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// T-S3-001/002; LA-01..08. AUTHORED_UNVERIFIED, NOT RUN.
public sealed class ApplicabilityEngineTests
{
    private static readonly DateTimeOffset Clock = DateTimeOffset.Parse("2026-11-02T00:00:00Z", CultureInfo.InvariantCulture);
    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static ApplicabilityRule Rule(string when, string state = "IN_FORCE", string date = "2026-11-02") => ApplicabilityEngine.Parse($$"""
        {"ruleId":"FICTIONAL-ONLY","version":1,"source":{"sourceId":"NOT-ADMITTED","locator":"synthetic/fixture","authorityLayer":"PRODUCT_CONTROL"},
         "lifecycle":{"state":"{{state}}","effectiveFrom":"{{date}}"},"scope":{},"when":{{when}},"then":{"result":"APPLICABLE","activate":[{"type":"CONTROL","id":"FICTIONAL"}]},"unknownPolicy":"REVIEW_REQUIRED"}
        """);
    private static ContextFact Fact(string field, string json, string state = "Known") => new(ApplicabilityEngine.Fields[field], state, J(json), "SyntheticSoftwareFixture", "1", Clock);
    public static IEnumerable<object[]> TruthTable() {
        foreach (var a in Enum.GetValues<RuleTruth>()) foreach (var b in Enum.GetValues<RuleTruth>()) {
            var all = a == RuleTruth.False || b == RuleTruth.False ? RuleTruth.False : a == RuleTruth.Unknown || b == RuleTruth.Unknown ? RuleTruth.Unknown : RuleTruth.True;
            var any = a == RuleTruth.True || b == RuleTruth.True ? RuleTruth.True : a == RuleTruth.Unknown || b == RuleTruth.Unknown ? RuleTruth.Unknown : RuleTruth.False;
            yield return [a, b, all, any];
        }
    }
    [Theory, MemberData(nameof(TruthTable))]
    public void All_any_truth_values_and_not_are_three_valued(RuleTruth a, RuleTruth b, RuleTruth all, RuleTruth any) {
        Assert.Equal(all, ApplicabilityEngine.Combine("all", [a, b])); Assert.Equal(any, ApplicabilityEngine.Combine("any", [a, b]));
        Assert.Equal(a == RuleTruth.Unknown ? a : a == RuleTruth.True ? RuleTruth.False : RuleTruth.True, ApplicabilityEngine.Combine("not", [a]));
    }
    [Theory]
    [InlineData("Missing")][InlineData("Unknown")][InlineData("Stale")][InlineData("Unverified")][InlineData("Conflicting")]
    public void Known_false_does_not_erase_critical_review_blocker(string state) {
        var r = Rule("""{"all":[{"field":"cargo.packagedDangerousGoods","operator":"IS_TRUE"},{"field":"class.rulePack.IACS_M79","operator":"IS_TRUE"}]}""");
        var facts = new Dictionary<string, ContextFact> { ["cargo.packagedDangerousGoods"] = Fact("cargo.packagedDangerousGoods", "false"), ["class.rulePack.IACS_M79"] = Fact("class.rulePack.IACS_M79", "null", state) };
        var result = ApplicabilityEngine.Preview(r, facts, Clock);
        Assert.Equal(RuleTruth.False, result.Reason.Truth); Assert.True(result.Reason.ReviewRequired); Assert.Equal("REVIEW_REQUIRED", result.Result);
        Assert.Equal(state, result.Reason.Children.Last().Children.Last().Reason);
        Assert.False(result.Authoritative); Assert.Empty(result.PredictedActivations);
    }
    [Theory]
    [InlineData("IS_FALSE", "false")][InlineData("IS_TRUE", "true")]
    public void Real_boolean_values_are_not_missing(string op, string value) {
        var r = Rule($$"""{"field":"cargo.packagedDangerousGoods","operator":"{{op}}"}""");
        var result = ApplicabilityEngine.Preview(r, new Dictionary<string, ContextFact> { ["cargo.packagedDangerousGoods"] = Fact("cargo.packagedDangerousGoods", value) }, Clock);
        Assert.Equal("APPLICABLE", result.Result); Assert.Equal("REVIEW_REQUIRED", result.CurrentResult);
    }
    [Theory]
    [InlineData("GT", "9007199254740992.0000000001", "9007199254740992", "APPLICABLE")]
    [InlineData("EQ", "0", "0", "APPLICABLE")]
    [InlineData("GTE", "0", "0", "APPLICABLE")]
    [InlineData("LT", "0", "0.0000000000000000000000000001", "APPLICABLE")]
    [InlineData("LTE", "79228162514264337593543950335", "79228162514264337593543950335", "APPLICABLE")]
    [InlineData("NE", "0.1", "0.10", "NOT_APPLICABLE")]
    public void Exact_decimal_strings_never_round_through_float(string op, string input, string threshold, string expected) {
        var r = Rule($$"""{"field":"vessel.gt","operator":"{{op}}","value":{{threshold}}}""");
        Assert.Equal(expected, ApplicabilityEngine.Preview(r, new Dictionary<string, ContextFact> { ["vessel.gt"] = Fact("vessel.gt", JsonSerializer.Serialize(input)) }, Clock).Result);
    }
    [Theory]
    [InlineData("DRAFT", "2026-11-02", "APPLICABLE")]
    [InlineData("ADOPTED", "2026-11-01", "NOT_CURRENT")]
    [InlineData("AWAITING", "2026-11-01", "NOT_CURRENT")]
    [InlineData("FUTURE_EFFECTIVE", "2026-11-01", "NOT_CURRENT")]
    [InlineData("IN_FORCE", "2026-11-03", "NOT_CURRENT")]
    [InlineData("IN_FORCE", "2026-11-02", "APPLICABLE")]
    public void Clock_does_not_adopt_a_source_and_effective_boundary_is_utc(string state, string start, string expected) {
        var rule = Rule("""{"field":"context.mode","operator":"EQ","value":"TUG"}""", state, start);
        var facts = new Dictionary<string, ContextFact> { ["context.mode"] = Fact("context.mode", "\"TUG\"") with { ObservedAt = null } };
        var a = ApplicabilityEngine.Preview(rule, facts, Clock);
        var b = ApplicabilityEngine.Preview(rule, facts, DateTimeOffset.Parse("2026-11-02T03:00:00+03:00", CultureInfo.InvariantCulture));
        Assert.Equal(expected, a.Result); Assert.Equal(a.Result, b.Result); Assert.False(a.Authoritative);
        Assert.Equal("REVIEW_REQUIRED", a.CurrentResult);
    }
    [Fact]
    public void Superseded_and_expired_rules_do_not_make_current_obligations() {
        var r = Rule("""{"field":"context.mode","operator":"EXISTS"}""");
        var context = new Dictionary<string, ContextFact> { ["context.mode"] = Fact("context.mode", "\"TUG\"") };
        Assert.Equal("NOT_CURRENT", ApplicabilityEngine.Preview(r with { Lifecycle = r.Lifecycle with { State = "SUPERSEDED", Supersedes = "synthetic-version-2" } }, context, Clock).Result);
        Assert.Equal("NOT_CURRENT", ApplicabilityEngine.Preview(r with { Lifecycle = r.Lifecycle with { EffectiveFrom = "2026-01-01", EffectiveTo = "2026-11-01" } }, context, Clock).Result);
        Assert.Equal("APPLICABLE", ApplicabilityEngine.Preview(r with { Lifecycle = r.Lifecycle with { EffectiveTo = "2026-11-02" } }, context, Clock).Result);
    }
    [Fact]
    public void Stale_future_observed_and_unverified_values_keep_provenance_for_replay() {
        var r = Rule("""{"field":"vessel.flag","operator":"EQ","value":"TR"}""");
        foreach (var fact in new[] { Fact("vessel.flag", "\"TR\"") with { FreshUntil = Clock.AddTicks(-1) }, Fact("vessel.flag", "\"TR\"") with { ObservedAt = Clock.AddTicks(1) }, Fact("vessel.flag", "\"TR\"", "Unverified") })
            Assert.Equal("REVIEW_REQUIRED", ApplicabilityEngine.Preview(r, new Dictionary<string, ContextFact> { ["vessel.flag"] = fact }, Clock).Result);
    }
    [Fact]
    public void Exists_false_requires_authoritative_meaningful_empty_and_unknown_collection_is_not_empty() {
        var e = J("""{"field":"synthetic.collection","operator":"EXISTS"}""");
        ContextFact f = new(RuleFieldType.Collection, "Known", J("[]"), "SyntheticSoftwareFixture", "1");
        var facts = new Dictionary<string, ContextFact> { ["synthetic.collection"] = f };
        Assert.Equal(RuleTruth.Unknown, ApplicabilityEngine.EvaluateExpression(e, facts, Clock).Truth);
        facts["synthetic.collection"] = f with { AuthoritativeEmpty = true }; Assert.Equal(RuleTruth.False, ApplicabilityEngine.EvaluateExpression(e, facts, Clock).Truth);
        facts["synthetic.collection"] = f with { Knowledge = "Unknown", AuthoritativeEmpty = true }; Assert.Equal(RuleTruth.Unknown, ApplicabilityEngine.EvaluateExpression(e, facts, Clock).Truth);
        e = J("""{"field":"synthetic.collection","operator":"CONTAINS","value":"A"}""");
        Assert.Equal(RuleTruth.Unknown, ApplicabilityEngine.EvaluateExpression(e, facts, Clock).Truth);
        facts["synthetic.collection"] = f with { Value = J("[\"A\",\"B\"]") }; Assert.Equal(RuleTruth.True, ApplicabilityEngine.EvaluateExpression(e, facts, Clock).Truth);
    }
    [Theory]
    [InlineData("{\"all\":[]}")][InlineData("{\"all\":[],\"any\":[]}")]
    [InlineData("{\"field\":\"vessel.gt\",\"operator\":\"GT\",\"value\":\"1\"}")]
    [InlineData("{\"field\":\"vessel.gt\",\"operator\":\"GT\",\"value\":1e3}")]
    [InlineData("{\"field\":\"vessel.gt\",\"operator\":\"IN\",\"value\":[1,\"2\"]}")]
    [InlineData("{\"field\":\"vessel.flag\",\"operator\":\"IS_TRUE\"}")]
    [InlineData("{\"field\":\"vessel.flag\",\"operator\":\"CONTAINS\",\"value\":\"TR\"}")]
    [InlineData("{\"field\":\"vessel.flag\",\"operator\":\"EXISTS\",\"value\":false}")]
    [InlineData("{\"field\":\"vessel.buildDate\",\"operator\":\"DATE_LT\",\"value\":\"2026-02-30\"}")]
    [InlineData("{\"field\":\"ActorContext\",\"operator\":\"EXISTS\"}")]
    [InlineData("{\"field\":\"vessel.gt\",\"operator\":\"eval\",\"value\":\"SQL\"}")]
    public void Invalid_mixed_or_unsupported_dsl_is_rejected(string expression) => Assert.Throws<JsonException>(() => Rule(expression));
    [Fact]
    public void Deep_lists_duplicate_keys_and_caller_weakened_unknown_policy_are_rejected() {
        var expression = "{\"field\":\"context.mode\",\"operator\":\"EXISTS\"}";
        for (var i = 0; i < 10; i++) expression = "{\"not\":" + expression + "}";
        Assert.Throws<JsonException>(() => Rule(expression));
        Assert.Throws<JsonException>(() => Rule("{\"any\":[" + string.Join(',', Enumerable.Repeat("{\"field\":\"context.mode\",\"operator\":\"EXISTS\"}", 33)) + "]}"));
        var r = Rule("""{"field":"context.mode","operator":"EXISTS"}""");
        foreach (var policy in new[] { "FAIL_CLOSED", "NOT_APPLICABLE", "anything" }) Assert.Throws<JsonException>(() => ApplicabilityEngine.Validate(r with { UnknownPolicy = policy }));
        Assert.Throws<JsonException>(() => Rule("""{"field":"context.mode","field":"vessel.flag","operator":"EXISTS"}"""));
    }
    [Fact]
    public void Schema_and_publication_claims_never_grant_authority() {
        var rule = Rule("""{"field":"context.mode","operator":"EXISTS"}""");
        var forged = rule with { PublicationStatus = "PUBLISHED", SourceVerificationStatus = "VERIFIED" };
        Assert.False(ApplicabilityEngine.Preview(forged, new Dictionary<string, ContextFact> { ["context.mode"] = Fact("context.mode", "\"SHIP\"") }, Clock).Authoritative);
        Assert.Throws<JsonException>(() => ApplicabilityEngine.Validate(forged with { ExampleOnly = true }));
    }
    [Fact]
    public void Any_true_and_not_false_retain_unverified_descendant_review() {
        var facts = new Dictionary<string, ContextFact> { ["context.mode"] = Fact("context.mode", "\"TUG\""), ["vessel.flag"] = Fact("vessel.flag", "\"TR\"", "Unverified") };
        var any = """{"any":[{"field":"context.mode","operator":"EQ","value":"TUG"},{"field":"vessel.flag","operator":"EQ","value":"TR"}]}""";
        Assert.Equal("REVIEW_REQUIRED", ApplicabilityEngine.Preview(Rule(any), facts, Clock).Result);
        Assert.Equal("REVIEW_REQUIRED", ApplicabilityEngine.Preview(Rule("{\"not\":" + any + "}"), facts, Clock).Result);
        var date = Rule("""{"field":"vessel.buildDate","operator":"DATE_LT","value":"2026-11-03"}""");
        facts["vessel.buildDate"] = Fact("vessel.buildDate", "\"2026-11-02\""); Assert.Equal("APPLICABLE", ApplicabilityEngine.Preview(date, facts, Clock).Result);
        facts["vessel.buildDate"] = Fact("vessel.buildDate", "\"2026-11-03\""); Assert.Equal("NOT_APPLICABLE", ApplicabilityEngine.Preview(date, facts, Clock).Result);
    }
}
