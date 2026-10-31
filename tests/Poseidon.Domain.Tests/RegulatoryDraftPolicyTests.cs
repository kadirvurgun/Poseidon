using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class RegulatoryDraftPolicyTests
{
    [Fact]
    public void Incomplete_draft_retains_unknown_zero_and_missing_without_approval()
    {
        var (draft, errors) = RegulatoryDraftPolicy.Normalize(new()
        {
            Imo = new("Known", "   "), Flag = new("Unknown"), Gt = new("Known", 0),
            SourceObservedAt = new("Unknown")
        });
        Assert.Empty(errors); Assert.Equal("Missing", draft.Imo.State); Assert.Null(draft.Imo.Value);
        Assert.Equal("Unknown", draft.Flag.State); Assert.Equal(0, draft.Gt.Value);
        Assert.Equal("Known", draft.Gt.State); Assert.Equal("Unknown", draft.SourceObservedAt.State);
    }

    [Theory]
    [InlineData("Verified", null)]
    [InlineData("Unknown", "TR")]
    [InlineData("Missing", "TR")]
    public void Undefined_or_inconsistent_states_are_rejected(string state, string? value)
    {
        var (_, errors) = RegulatoryDraftPolicy.Normalize(new() { Flag = new(state, value) });
        Assert.Contains("flag", errors.Keys);
    }

    [Fact]
    public void Structural_validation_is_explicit_without_invented_catalogs()
    {
        var (_, errors) = RegulatoryDraftPolicy.Normalize(new()
        {
            Imo = new("Known", "ABC1234"), Gt = new("Known", -1), Nt = new("Known"),
            BuildDate = new("Known", "2025-02-30"), SourceObservedAt = new("Known", "2020-01-01T00:00:00"),
            ClassSociety = new("Known", new string('a', 501)), Declaration = new("Known", "bad\u0001text")
        });
        Assert.Equal(7, errors.Count);
        var (valid, noErrors) = RegulatoryDraftPolicy.Normalize(new()
        {
            Imo = new("Known", " 0000000 "), Flag = new("Known", "Unverified declared flag"),
            BuildDate = new("Known", "2020-02-29"), SourceObservedAt = new("Known", "2001-01-01T03:00:00+03:00")
        });
        Assert.Empty(noErrors); Assert.Equal("0000000", valid.Imo.Value);
        Assert.Equal("2001-01-01T00:00:00.0000000+00:00", valid.SourceObservedAt.Value);
    }
}
