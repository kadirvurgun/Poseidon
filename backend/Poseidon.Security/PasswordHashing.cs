using Microsoft.AspNetCore.Identity;

namespace Poseidon.Security;

// Reuses the versioned, salted Identity format. Never implement a password digest with SHA/MD5.
// Authentication endpoints must use UserManager/SignInManager (MFA, lockout, reset and session policy).
public sealed class PasswordHashing
{
    private readonly PasswordHasher<object> hasher = new();
    private readonly object subject = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length is < 12 or > 128)
            throw new ArgumentException("Password length must be between 12 and 128 characters.", nameof(password));
        return hasher.HashPassword(subject, password);
    }

    public PasswordVerificationResult Verify(string hash, string password) =>
        hasher.VerifyHashedPassword(subject, hash, password);
}
