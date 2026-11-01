using System.Globalization;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class AuditValuePolicyTests
{
    private sealed class NeverSerialize { public override string ToString() => throw new InvalidOperationException("Must not serialize."); }
    [Theory]
    [InlineData("Name")]
    [InlineData("Code")]
    [InlineData("ContentJson")]
    public void Arbitrary_text_and_payload_values_are_withheld_without_evaluating_them(string field)
    {
        Assert.Null(AuditValuePolicy.Value(field, new NeverSerialize()));
        Assert.NotEqual("Allowlisted", AuditValuePolicy.Evidence(field));
    }
    [Theory]
    [InlineData("PasswordHash")]
    [InlineData("AccessToken")]
    [InlineData("Claims")]
    [InlineData("Health")]
    [InlineData("Fingerprint")]
    public void Sensitive_and_future_fields_cannot_silently_enter_the_summary(string field)
    {
        Assert.False(AuditValuePolicy.Includes(field));
        Assert.Throws<ArgumentException>(() => AuditValuePolicy.Value(field, new NeverSerialize()));
    }
    [Fact]
    public void Measurements_remain_exact_under_non_English_culture_and_wrong_runtime_types_fail_closed()
    {
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("123.4567890123456789", AuditValuePolicy.Value("GrossTonnage", 123.4567890123456789m));
            Assert.Throws<InvalidOperationException>(() => AuditValuePolicy.Value("GrossTonnage", "arbitrary text"));
            Assert.Throws<InvalidOperationException>(() => AuditValuePolicy.Value("Kind", "UnknownRole"));
        } finally { CultureInfo.CurrentCulture = original; }
    }
}
