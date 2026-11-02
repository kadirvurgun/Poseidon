using System.Text;
using System.Text.Json.Nodes;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class GoldenPilotDatasetTests
{
    private static string Fixture => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pilot-dataset.v1.json"));
    private static GoldenPilotDataset Load(string json) => GoldenPilotDataset.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Deterministic_profiles_drive_actual_domain_policies_without_claiming_readiness()
    {
        var data = Load(Fixture);
        foreach (var c in data.ConsumerCases) Assert.Equal(c.Expected, data.Evaluate(c));
        Assert.Contains(data.ConsumerCases, c => c.Area == "defect");
        Assert.Contains(data.ConsumerCases, c => c.Profile == "SYN-GENERAL-CARGO");
        Assert.All(data.SourceCases, c => Assert.Equal("UNAVAILABLE", c.GetProperty("adapter").GetString()));
    }

    [Theory]
    [InlineData("id", "duplicate")]
    [InlineData("engine", "InventedEligibilityEngine")]
    [InlineData("profile", "unknown-profile")]
    [InlineData("expectedReadiness", "READY")]
    public void Loader_rejects_ambiguous_or_invented_consumer(string property, string value)
    {
        var json = JsonNode.Parse(Fixture)!;
        var cases = json["consumerCases"]!.AsArray();
        cases[1]![property] = property == "id" ? cases[0]!["id"]!.GetValue<string>() : value;
        Assert.Throws<InvalidDataException>(() => Load(json.ToJsonString()));
    }

    [Fact]
    public void Actual_output_changes_with_input_rather_than_echoing_expected_fixture()
    {
        var data = Load(Fixture); var c = data.ConsumerCases.First(x => x.Id == "SYN-APP-KnownTrue");
        Assert.Equal("ReviewRequired", data.Evaluate(c with { Inputs = new() { ["knowledge"] = "Conflicting", ["currency"] = "Current" } }));
        var expired = data.ConsumerCases.First(x => x.Id == "SYN-CERT-EXPIRED");
        Assert.Equal("AfterReferenceDate/IssueBeforeExpiry", data.Evaluate(expired with { Inputs = new() { ["issue"] = "2025-01-01", ["expiry"] = "2026-10-05" } }));
    }

    [Fact]
    public void Loader_bounds_bytes_and_refuses_time_drift()
    {
        Assert.Throws<InvalidDataException>(() => Load(new string(' ', 512 * 1024 + 1)));
        Assert.Throws<InvalidDataException>(() => Load(Fixture.Replace("2026-10-04T00:00:00Z", "2026-10-05T00:00:00Z")));
    }

    [Theory]
    [InlineData(PredicateKnowledge.Missing)]
    [InlineData(PredicateKnowledge.Unknown)]
    [InlineData(PredicateKnowledge.Stale)]
    [InlineData(PredicateKnowledge.Unverified)]
    [InlineData(PredicateKnowledge.Conflicting)]
    public void Uncertain_critical_fact_cannot_become_not_applicable(PredicateKnowledge value) =>
        Assert.Equal(ApplicabilityObservation.ReviewRequired, ApplicabilityObservationPolicy.Assess(value, SourceCurrency.Current));

    [Fact]
    public void Future_and_superseded_are_not_current_obligations_and_invalid_enums_are_rejected()
    {
        Assert.Equal(ApplicabilityObservation.NotCurrent, ApplicabilityObservationPolicy.Assess(PredicateKnowledge.KnownTrue, SourceCurrency.Future));
        Assert.Equal(ApplicabilityObservation.NotCurrent, ApplicabilityObservationPolicy.Assess(PredicateKnowledge.KnownTrue, SourceCurrency.Superseded));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicabilityObservationPolicy.Assess((PredicateKnowledge)999, SourceCurrency.Current));
    }
}
