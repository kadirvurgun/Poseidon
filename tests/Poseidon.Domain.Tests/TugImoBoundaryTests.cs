using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class TugImoBoundaryTests
{
    [Theory]
    [InlineData("9074729\n")]
    [InlineData("9074729\r")]
    [InlineData("9074729\r\n")]
    [InlineData("9074729\u2028")]
    [InlineData("9074729\u2029")]
    [InlineData(" 9074729")]
    [InlineData("9074729 ")]
    [InlineData("٩٠٧٤٧٢٩")]
    [InlineData("9074720")]
    public void Existing_imo_contract_consumes_exactly_seven_ascii_digits_and_checksum(string value)
    {
        Assert.False(TugRequestPolicy.ValidImo(value));
        Assert.Contains("assisted_vessel_imo", TugRequestPolicy.Invalid(new(AssistedVesselImo: value)));
    }

    [Fact]
    public void Exact_imo_remains_valid_and_an_omitted_conditional_value_remains_a_draft()
    {
        Assert.True(TugRequestPolicy.ValidImo("9074729"));
        Assert.Empty(TugRequestPolicy.Invalid(new(AssistedVesselImo: "9074729")));
        Assert.Empty(TugRequestPolicy.Invalid(new()));
        Assert.DoesNotContain("assisted_vessel_imo", TugRequestPolicy.Missing(new()));
    }
}
