using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// REG-SOURCE-DRAFT-01 / T-S3-001. AUTHORED ONLY; no test run claimed.
public sealed class RegulatoryRegisterPolicyTests
{
    [Theory]
    [InlineData("Source")]
    [InlineData("Requirement")]
    public void Incomplete_drafts_keep_missing_and_unknown_distinct(string kind)
    {
        var content = new RegisterContent { Declaration = new("Unknown"), ObservedAt = new("Missing") };
        Assert.Empty(RegulatoryRegisterPolicy.Validate(content, kind));
        Assert.Equal("Unknown", content.Declaration.State); Assert.Null(content.Declaration.Value);
        Assert.Equal("Missing", content.ObservedAt.State);
    }
    [Theory]
    [InlineData("Verified", null)]
    [InlineData("No", null)]
    [InlineData("NotApplicable", null)]
    [InlineData("Missing", "hidden")]
    [InlineData("Unknown", "hidden")]
    [InlineData("Known", " ")]
    public void States_do_not_fabricate_legal_approval_or_unknown_facts(string state, string? value) =>
        Assert.Contains("title", RegulatoryRegisterPolicy.Validate(new() { Title = new(state, value) }, "Source").Keys);
    [Fact]
    public void Structural_bounds_and_text_only_locator_do_not_resolve_a_source()
    {
        Assert.Empty(RegulatoryRegisterPolicy.Validate(new() { Locator = new("Known", "file:///untrusted/declared-reference") }, "Source"));
        Assert.Contains("locator", RegulatoryRegisterPolicy.Validate(new() { Locator = new("Known", new string('x', 1001)) }, "Source").Keys);
        Assert.Contains("authority", RegulatoryRegisterPolicy.Validate(new() { Authority = new("Known", "bad\nvalue") }, "Source").Keys);
    }
    [Theory]
    [InlineData("2026-02-30", "2026-01-01")]
    [InlineData("01.11.2026", "2026-11-01T00:00:00")]
    public void Dates_are_declared_structurally_validated_inputs(string date, string observed)
    {
        var errors = RegulatoryRegisterPolicy.Validate(new() { EffectiveDate = new("Known", date), ObservedAt = new("Known", observed) }, "Requirement");
        Assert.Contains("effectiveDate", errors.Keys); Assert.Contains("observedAt", errors.Keys);
    }
    [Fact]
    public void Draft_effective_date_cannot_activate_a_requirement()
    {
        var content = new RegisterContent { EffectiveDate = new("Known", "2000-01-01"), ObservedAt = new("Known", "2001-01-01T01:00:00+01:00") };
        Assert.Empty(RegulatoryRegisterPolicy.Validate(content, "Requirement"));
        Assert.Contains("source", RegulatoryRegisterPolicy.Validate(content with { Title = new("Known", "forged source") }, "Requirement").Keys);
    }
}
