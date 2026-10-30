using Microsoft.AspNetCore.Identity;
using Poseidon.Security;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class PasswordHashingTests
{
    [Fact]
    public void Hashes_are_salted_and_wrong_passwords_are_rejected()
    {
        var passwords = new PasswordHashing();
        const string password = "Synthetic-Test-Password-2026!";
        var first = passwords.Hash(password);
        var second = passwords.Hash(password);
        Assert.NotEqual(first, second);
        Assert.NotEqual(password, first);
        Assert.Equal(PasswordVerificationResult.Success, passwords.Verify(first, password));
        Assert.Equal(PasswordVerificationResult.Failed, passwords.Verify(first, "incorrect"));
    }

    [Fact]
    public void Weak_length_is_rejected_before_storage()
    {
        Assert.Throws<ArgumentException>(() => new PasswordHashing().Hash("short"));
    }
}
