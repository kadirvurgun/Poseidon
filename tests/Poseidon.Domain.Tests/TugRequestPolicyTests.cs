using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

public sealed class TugRequestPolicyTests
{
    [Fact]
    public void Draft_may_be_incomplete_and_end_is_conditional() {
        var empty = new TugRequestInputs(); Assert.Empty(TugRequestPolicy.Invalid(empty));
        Assert.Contains("requested_start", TugRequestPolicy.Missing(empty)); Assert.DoesNotContain("requested_end", TugRequestPolicy.Missing(empty));
        var start = new DateTimeOffset(2026,11,1,10,0,0,TimeSpan.Zero);
        Assert.Empty(TugRequestPolicy.Invalid(empty with { RequestedStart = start }));
        Assert.Contains("requested_end", TugRequestPolicy.Invalid(empty with { RequestedStart = start, RequestedEnd = start.AddTicks(-1) }));
    }
    [Theory]
    [InlineData("0", true)] [InlineData("99999999.99", true)] [InlineData("0.01", true)]
    [InlineData("1.001", false)] [InlineData("1e2", false)] [InlineData("1,5", false)]
    [InlineData("+1", false)] [InlineData("-1", false)] [InlineData("01", false)] [InlineData("100000000", false)]
    public void Bp_is_exact_invariant_bounded_without_rounding(string input, bool valid) {
        Assert.Equal(valid, TugRequestPolicy.TryBp(input, out var bp));
        if (valid) Assert.Equal(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture), bp);
    }
    [Fact]
    public void Unknown_is_not_zero_and_identifiers_enums_quantities_are_validated() {
        Assert.True(TugRequestPolicy.TryBp(null, out var bp)); Assert.Null(bp);
        Assert.True(TugRequestPolicy.ValidImo("9074729")); Assert.False(TugRequestPolicy.ValidImo("9074720"));
        var errors = TugRequestPolicy.Invalid(new(JobType: "Ready", RequiredTugCount: 0, Priority: "No", RequestOwner: Guid.Empty, AssistedVesselImo: "https://outside.invalid"));
        Assert.Equal(5, errors.Length);
    }
}
