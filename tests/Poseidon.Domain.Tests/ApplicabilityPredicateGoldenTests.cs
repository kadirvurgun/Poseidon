using System.Globalization;
using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S3-WPA / DEV-027 / T-S3-002 (18.01 Backlog row 28): per-predicate golden cases for
// flag/type/GT/build date/cargo/trading area incl. boundaries and REVIEW_REQUIRED.
// FICTIONAL software expectations (contracts/golden/applicability.predicate-cases.v1.json);
// never real vessel facts, an approved catalog or legal applicability.
public sealed class ApplicabilityPredicateGoldenTests
{
    private static readonly string[] Predicates = ["flag", "type", "gt", "buildDate", "cargo", "tradingArea"];
    private static JsonDocument Golden() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/applicability.predicate-cases.v1.json")));
    public static TheoryData<string> CaseIds() {
        using var doc = Golden(); var data = new TheoryData<string>();
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray()) data.Add(c.GetProperty("id").GetString()!);
        return data;
    }
    private static ApplicabilityRule Rule(JsonElement c) => ApplicabilityEngine.Parse($$"""
        {"ruleId":"FICTIONAL-{{c.GetProperty("id").GetString()}}","version":1,"source":{"sourceId":"NOT-ADMITTED","locator":"synthetic/golden","authorityLayer":"PRODUCT_CONTROL"},
         "lifecycle":{"state":"IN_FORCE","effectiveFrom":"2026-11-02"},"scope":{{(c.TryGetProperty("scope", out var s) ? s.GetRawText() : "{}")}},
         "when":{{c.GetProperty("when").GetRawText()}},"then":{"result":"APPLICABLE","activate":[{"type":"CONTROL","id":"FICTIONAL"}]},"unknownPolicy":"REVIEW_REQUIRED"}
        """);
    private static Dictionary<string, ContextFact> Facts(JsonElement c) => c.GetProperty("facts").EnumerateObject().ToDictionary(p => p.Name, p => new ContextFact(
        ApplicabilityEngine.Fields[p.Name], p.Value.GetProperty("knowledge").GetString()!, p.Value.GetProperty("value").Clone(), "SyntheticGoldenSoftwareFixture", "golden-v1",
        p.Value.TryGetProperty("observedAt", out var o) ? DateTimeOffset.Parse(o.GetString()!, CultureInfo.InvariantCulture) : null,
        p.Value.TryGetProperty("freshUntil", out var f) ? DateTimeOffset.Parse(f.GetString()!, CultureInfo.InvariantCulture) : null));
    private static IEnumerable<PredicateReason> Leaves(PredicateReason r) => r.Children.Length == 0 ? [r] : r.Children.SelectMany(Leaves);

    [Theory, MemberData(nameof(CaseIds))]
    public void Golden_predicate_case_returns_expected_result_and_reason(string id)
    {
        using var doc = Golden();
        var c = doc.RootElement.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("id").GetString() == id);
        var asOf = DateTimeOffset.Parse(doc.RootElement.GetProperty("asOf").GetString()!, CultureInfo.InvariantCulture);
        var result = ApplicabilityEngine.Preview(Rule(c), Facts(c), asOf);
        var expected = c.GetProperty("expected").GetString();
        Assert.Equal(expected, result.Result);
        // Simulation never carries current authority, whatever the predicate outcome.
        Assert.False(result.Authoritative); Assert.Equal("REVIEW_REQUIRED", result.CurrentResult);
        if (expected == "REVIEW_REQUIRED") {
            Assert.True(result.Reason.ReviewRequired);
            Assert.Equal(c.GetProperty("reviewReason").GetString(), Leaves(result.Reason).First(x => x.ReviewRequired).Reason);
            Assert.Empty(result.PredictedActivations);
        } else {
            Assert.False(result.Reason.ReviewRequired);
            Assert.All(Leaves(result.Reason), x => Assert.Equal("compared", x.Reason));
        }
    }

    [Fact]
    public void Golden_set_covers_every_predicate_with_applicable_not_applicable_and_review_required()
    {
        using var doc = Golden();
        var cases = doc.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(cases.Length, cases.Select(x => x.GetProperty("id").GetString()).Distinct().Count());
        foreach (var p in Predicates)
        foreach (var r in new[] { "APPLICABLE", "NOT_APPLICABLE", "REVIEW_REQUIRED" })
            Assert.Contains(cases, x => x.GetProperty("predicate").GetString() == p && x.GetProperty("expected").GetString() == r);
        foreach (var p in Predicates)
        foreach (var reason in new[] { "Unknown", "Missing", "Stale", "Unverified", "NotYetObserved" })
            Assert.Contains(cases, x => x.GetProperty("predicate").GetString() == p && x.TryGetProperty("reviewReason", out var v) && v.GetString() == reason);
        Assert.All(cases, x => Assert.Contains(x.GetProperty("predicate").GetString(), Predicates));
    }

    // DEV-020 profile facts that the resolver cannot know (adapter/profile absent, state Unknown
    // or Missing, declared-only, stale or future-observed) must be REVIEW_REQUIRED with a reason,
    // for every predicate field and operator shape — never NOT_APPLICABLE.
    [Theory]
    [InlineData("vessel.flag", """{"field":"vessel.flag","operator":"NOT_IN","value":["TR"]}""")]
    [InlineData("vessel.type", """{"field":"vessel.type","operator":"NE","value":"TUG"}""")]
    [InlineData("vessel.gt", """{"field":"vessel.gt","operator":"LT","value":500}""")]
    [InlineData("vessel.buildDate", """{"field":"vessel.buildDate","operator":"DATE_LT","value":"2009-07-01"}""")]
    [InlineData("cargo.type", """{"field":"cargo.type","operator":"NOT_IN","value":["OIL"]}""")]
    [InlineData("vessel.tradingArea", """{"not":{"field":"vessel.tradingArea","operator":"EQ","value":"HARBOUR"}}""")]
    public void Unknown_DEV_020_profile_fact_is_review_required_with_reason_never_not_applicable(string field, string when)
    {
        var clock = DateTimeOffset.Parse("2026-11-02T00:00:00Z", CultureInfo.InvariantCulture);
        var rule = ApplicabilityEngine.Parse($$"""
            {"ruleId":"FICTIONAL-DEV020","version":1,"source":{"sourceId":"NOT-ADMITTED","locator":"synthetic/golden","authorityLayer":"PRODUCT_CONTROL"},
             "lifecycle":{"state":"IN_FORCE","effectiveFrom":"2026-11-02"},"scope":{},"when":{{when}},"then":{"result":"APPLICABLE"},"unknownPolicy":"REVIEW_REQUIRED"}
            """);
        var type = ApplicabilityEngine.Fields[field]; var none = JsonSerializer.SerializeToElement<object?>(null);
        var sample = JsonSerializer.SerializeToElement(type switch { RuleFieldType.Decimal => "1", RuleFieldType.Date => "2001-01-01", _ => "FICTIONAL" });
        var variants = new (string Reason, ContextFact? Fact)[] {
            // ApplicabilityContextResolver default when no profile/adapter exists.
            ("Unknown", new(type, "Unknown", none, "AdapterUnavailable", ApplicabilityEngine.AdapterVersion)),
            ("Missing", new(type, "Missing", none, "DeclaredUnverified", "1")),
            ("Missing", null),
            ("Unverified", new(type, "Unverified", sample, "DeclaredUnverified", "1")),
            ("Stale", new(type, "Known", sample, "DeclaredUnverified", "1", clock.AddDays(-30), clock.AddTicks(-1))),
            ("NotYetObserved", new(type, "Known", sample, "DeclaredUnverified", "1", clock.AddTicks(1))),
        };
        foreach (var (reason, fact) in variants) {
            var facts = fact == null ? new Dictionary<string, ContextFact>() : new Dictionary<string, ContextFact> { [field] = fact };
            var result = ApplicabilityEngine.Preview(rule, facts, clock);
            Assert.Equal("REVIEW_REQUIRED", result.Result); Assert.NotEqual("NOT_APPLICABLE", result.CurrentResult);
            Assert.Equal(RuleTruth.Unknown, result.Reason.Truth);
            var leaf = Leaves(result.Reason).Single(); Assert.True(leaf.ReviewRequired);
            Assert.Equal((field, reason), (leaf.Field, leaf.Reason));
        }
    }
}
