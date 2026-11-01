using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;
public sealed class TugExactNumericTests
{
    [Theory][InlineData("1\n")][InlineData("1\r\n")][InlineData("1\t")][InlineData("1 ")][InlineData("١")]
    public void Bollard_pull_consumes_the_entire_ASCII_declaration_without_coercion(string value) => Assert.False(TugRequestPolicy.TryBp(value,out _));
}
