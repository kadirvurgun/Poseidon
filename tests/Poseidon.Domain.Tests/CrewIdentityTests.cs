using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class CrewIdentityTests
{
    [Theory]
    [InlineData("ImmutableAfterCapture", "0001", "0001", true)]
    [InlineData("ImmutableAfterCapture", "0001", "1", false)]
    [InlineData("ImmutableAfterCapture", "0001", null, false)]
    [InlineData("ImmutableAfterCapture", null, "0001", true)]
    [InlineData("MutableDeclaration", "0001", null, true)]
    [InlineData("", "0001", "0001", false)]
    public void Natural_identity_is_exact_and_requires_an_explicit_operator_mode(string mode, string? before, string? after, bool allowed) =>
        Assert.Equal(allowed, CrewIdentityData.EmployeeChangeAllowed(mode, before, after));

    [Fact]
    public void Incomplete_identity_is_allowed_without_coercing_unknown_or_accepting_control_characters() {
        Assert.True(CrewIdentityData.Valid(null, null));
        Assert.True(CrewIdentityData.Valid("FICTIONAL declaration", "0001"));
        Assert.False(CrewIdentityData.Valid("FICTIONAL\n", null));
        Assert.False(CrewIdentityData.Valid(null, new string('1', 65)));
    }
}
