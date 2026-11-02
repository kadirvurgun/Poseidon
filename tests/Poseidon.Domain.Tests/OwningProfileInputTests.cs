using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class OwningProfileInputTests
{
    [Theory]
    [InlineData("3600.000000", "3600")]
    [InlineData("9007199254740993", "9007199254740993")]
    [InlineData("0", "0")]
    public void Power_and_contract_date_are_independent_exact_declared_values(string power, string expected)
    {
        var (c, errors) = RegulatoryDraftPolicy.Normalize(new() { MainPropulsionKw = new("Known", power),
            BuildDate = new("Known", "2022-04-01"), ContractForConstructionDate = new("Known", "2022-03-14") });
        Assert.Empty(errors); Assert.Equal(expected, c.MainPropulsionKw.Value);
        Assert.Equal("2022-03-14", c.ContractForConstructionDate.Value); Assert.Equal("2022-04-01", c.BuildDate.Value);
        Assert.Null(c.ConstructionRecord); // Partial drafts remain possible; this is no verified record.
        Assert.Equal("Missing", new RegulatoryDraftContent { BuildDate = c.BuildDate }.ContractForConstructionDate.State);
    }
    [Theory]
    [InlineData("1e3", "2022-02-30")]
    [InlineData("3600 kW", "2022-03-14T00:00:00Z")]
    [InlineData("-1", "14/03/2022")]
    public void Numeric_units_and_other_date_semantics_are_not_coerced(string power, string date)
    {
        var (_, errors) = RegulatoryDraftPolicy.Normalize(new() { MainPropulsionKw = new("Known", power), ContractForConstructionDate = new("Known", date) });
        Assert.Contains("mainPropulsionKw", errors.Keys); Assert.Contains("contractForConstructionDate", errors.Keys);
    }
    [Theory]
    [InlineData("vessel.mainPropulsionKw")]
    [InlineData("vessel.contractForConstructionDate")]
    public void Qualified_policy_cannot_admit_a_declaration_as_verified_owning_input(string field)
    {
        Assert.Throws<ArgumentException>(() => RegulatoryQualificationPolicy.Validate(new(Inputs: [new(field, "Declared")])));
        RegulatoryQualificationPolicy.Validate(new(Inputs: [new(field, "HumanAttested")]));
        RegulatoryQualificationPolicy.Validate(new(Inputs: [new(field, "ProviderVerified")])); // Required provider still must exist at runtime.
        var expression = JsonSerializer.SerializeToElement(new { field, @operator = "EXISTS" });
        var fact = new ContextFact(ApplicabilityEngine.Fields[field], "Unverified", JsonSerializer.SerializeToElement(field.EndsWith("Kw") ? "3600" : "2022-03-14"), "DeclaredUnverified", "1");
        var result = ApplicabilityEngine.EvaluateExpression(expression, new Dictionary<string, ContextFact> { [field] = fact }, DateTimeOffset.UtcNow);
        Assert.Equal(RuleTruth.Unknown, result.Truth); Assert.True(result.ReviewRequired);
    }
    [Fact]
    public void Record_reference_is_structural_and_never_fills_unknown_fields()
    {
        var (_, errors) = RegulatoryDraftPolicy.Normalize(new() { ConstructionRecord = new(Guid.Empty, 0, "false") });
        Assert.Contains("constructionRecord", errors.Keys);
        var (c, valid) = RegulatoryDraftPolicy.Normalize(new() { MainPropulsionKw = new("Unknown"), ContractForConstructionDate = new("Unknown"),
            ConstructionRecord = new(Guid.NewGuid(), 1, new string('A', 64)) });
        Assert.Empty(valid); Assert.Equal("Unknown", c.MainPropulsionKw.State); Assert.Null(c.ContractForConstructionDate.Value);
    }
}
