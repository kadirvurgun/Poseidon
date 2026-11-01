using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class LegacyCrewImportTests
{
    [Theory]
    [InlineData(" {\"headers\":[\"unknown historic field\"],\"rows\":[[\" 0001 \" ]]} ")]
    [InlineData("unsupported old schema\r\n\u00e7\u0131\u015f")]
    public void Storage_conversion_does_not_require_or_invent_historical_schema_interpretation(string historical) {
        Assert.True(LegacyCrewImport.Convertible("crew", historical, false));
        Assert.False(LegacyCrewImport.Convertible("certificate", historical, false));
        Assert.False(LegacyCrewImport.Convertible("crew", historical, true));
    }
    [Theory]
    [InlineData("protected-v1:key.nonce.tag.cipher")]
    [InlineData("protected-legacy-v1:key.nonce.tag.cipher")]
    public void Ciphertext_is_never_treated_as_historical_plaintext(string source) => Assert.False(LegacyCrewImport.Convertible("crew", source, false));
    [Fact]
    public void Operator_command_accepts_only_nonzero_expected_version_and_fixed_safe_reason() {
        Assert.True(LegacyCrewImport.ValidRequest(1, LegacyCrewImport.Reason));
        Assert.False(LegacyCrewImport.ValidRequest(0, LegacyCrewImport.Reason));
        Assert.False(LegacyCrewImport.ValidRequest(1, "private free text"));
        Assert.False(LegacyCrewImport.ValidRequest(1, null));
    }
}
