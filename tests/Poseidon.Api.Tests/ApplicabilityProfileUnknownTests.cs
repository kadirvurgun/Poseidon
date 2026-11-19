using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// S3-WPA / DEV-027 / T-S3-002 with DEV-020 profile input: the real resolver over a stored
// vessel profile whose six predicate facts are Unknown/Missing/declared-only must produce
// REVIEW_REQUIRED with a per-field reason code, never NOT_APPLICABLE. FICTIONAL rule only.
// Real host + owned PostgreSQL; execution DEFERRED to CI.
public sealed partial class DirectoryWorkspaceTests
{
    private static JsonElement SixPredicateRule() => JsonSerializer.SerializeToElement(new {
        ruleId = "SYNTHETIC-SIX-PREDICATE-NOT-A-LEGAL-RULE", version = 1,
        source = new { sourceId = "UNVERIFIED-FIXTURE", locator = "synthetic/fixture", authorityLayer = "PRODUCT_CONTROL" },
        lifecycle = new { state = "DRAFT", effectiveFrom = "2000-01-01" }, scope = new { },
        // Each predicate is the NEGATIVE form: a wrong Unknown→false shortcut would make it NOT_APPLICABLE.
        when = new { all = new object[] {
            new { field = "vessel.flag", @operator = "IN", value = new[] { "FICTIONAL-FLAG" } },
            new { field = "vessel.type", @operator = "IN", value = new[] { "FICTIONAL-TYPE" } },
            new { field = "vessel.gt", @operator = "GTE", value = 900000000 },
            new { field = "vessel.buildDate", @operator = "DATE_GTE", value = "2999-01-01" },
            new { field = "cargo.type", @operator = "IN", value = new[] { "FICTIONAL-CARGO" } },
            new { field = "vessel.tradingArea", @operator = "EQ", value = "FICTIONAL-AREA" } } },
        then = new { result = "APPLICABLE" }, unknownPolicy = "REVIEW_REQUIRED"
    });
    private static IEnumerable<PredicateReason> Leaf(PredicateReason r) => r.Children.Length == 0 ? [r] : r.Children.SelectMany(Leaf);

    [Theory]
    [InlineData("Unknown", "Unknown")]
    [InlineData("Missing", "Missing")]
    [InlineData("Known", "Unverified")]
    public async Task Unknown_or_unqualified_DEV_020_profile_facts_are_review_required_with_reason_codes(string state, string reason)
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var tenant = seed.Tenants[0].Id;
        using var created = await Send(client, "rules", new RuleAuthorCommand(0, ApplicabilityEngine.Contract, SixPredicateRule(),
            RegulatoryRegisterFixtures.SourceId(tenant), 1, RegulatoryRegisterFixtures.RequirementId(tenant), 1, "INITIAL_AUTHORING"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var rule = (await created.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        await using (var db = Db()) {
            var profile = await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct);
            TextInput T(string value) => state == "Known" ? new("Known", value) : new(state);
            profile.Revision++;
            profile.ContentJson = RegulatoryDraftStorage.Serialize(new() {
                Flag = T("PA"), ShipType = T("BULK_CARRIER"), Gt = state == "Known" ? new("Known", "100") : new(state),
                BuildDate = T("2001-01-01"), CargoContext = T("CONTAINER"), TradingArea = T("HARBOUR") });
            profile.SavedAt = DateTimeOffset.UtcNow; db.RegulatoryDraftRevisions.Add(profile.Snapshot()); await db.SaveChangesAsync(Ct);
        }
        using var simulated = await Send(client, "applicability/simulate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal(HttpStatusCode.OK, simulated.StatusCode);
        var evaluation = (await simulated.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
        var prediction = Assert.Single(evaluation.Results).Prediction;
        Assert.Equal("REVIEW_REQUIRED", prediction.Result); Assert.Equal("REVIEW_REQUIRED", evaluation.CurrentResult);
        Assert.NotEqual(RuleTruth.False, prediction.Reason.Truth);
        var leaves = Leaf(prediction.Reason).Where(x => x.Field != null).ToDictionary(x => x.Field!);
        foreach (var field in new[] { "vessel.flag", "vessel.type", "vessel.gt", "vessel.buildDate", "cargo.type", "vessel.tradingArea" }) {
            Assert.Equal(RuleTruth.Unknown, leaves[field].Truth); Assert.True(leaves[field].ReviewRequired);
            Assert.Equal(reason, leaves[field].Reason);
        }
        Assert.Empty(prediction.PredictedActivations);
    }
}
