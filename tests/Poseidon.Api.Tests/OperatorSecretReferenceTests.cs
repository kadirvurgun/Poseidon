using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class OperatorSecretReferenceTests
{
    [Fact]
    public void Only_bounded_operator_environment_references_are_resolved_and_missing_values_stay_missing()
    {
        var called = new List<string>(); var synthetic = string.Concat("synthetic", "-operator-value");
        Assert.Equal(synthetic, OperatorSecretReferences.Resolve("POSEIDON_TEST_REFERENCE", name => { called.Add(name); return synthetic; }));
        Assert.Single(called); Assert.Null(OperatorSecretReferences.Resolve("POSEIDON_TEST_REFERENCE", _ => null));
        foreach (var invalid in new[] { "", "../private", "https://remote", "ENV\n", "lowercase", new string('A', 81) })
        {
            var error = Assert.Throws<InvalidOperationException>(() => OperatorSecretReferences.Resolve(invalid, _ => throw new Exception("Must not resolve")));
            Assert.Equal("Invalid operator secret reference.", error.Message); Assert.DoesNotContain(synthetic, error.Message);
        }
    }
}
