using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class DirectoryInputPolicyTests
{
    [Theory]
    [InlineData(null, "Name", "code")]
    [InlineData(" \t", "Name", "code")]
    [InlineData("C", "", "name")]
    [InlineData("C\n", "Name", "code")]
    [InlineData("C", "Name\u0085", "name")]
    public void Empty_and_control_values_are_rejected(string? code, string? name, string field) =>
        Assert.Contains(field, DirectoryInputPolicy.Validate(code, name, null, false).Keys);

    [Fact]
    public void Bounds_are_structural_and_unknown_version_never_overwrites()
    {
        Assert.Empty(DirectoryInputPolicy.Validate(new string('C', 64), new string('N', 200), 1, true));
        var invalid = DirectoryInputPolicy.Validate(new string('C', 65), new string('N', 201), null, true);
        Assert.Equal(3, invalid.Count);
        Assert.Contains("expectedVersion", DirectoryInputPolicy.Validate("C", "N", 0, true).Keys);
        Assert.Empty(DirectoryInputPolicy.Validate(" C ", " Name ", null, false));
    }
}
