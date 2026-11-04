namespace Poseidon.Api;

// Environment names are operator references, not credential values. Never echo either resolution errors or values.
public static class OperatorSecretReferences
{
    public static string? Resolve(string? reference, Func<string, string?> read)
    {
        if (reference is not { Length: > 0 and <= 80 } || !char.IsAsciiLetterUpper(reference[0]) ||
            reference.Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c) && c != '_'))
            throw new InvalidOperationException("Invalid operator secret reference.");
        string? value;
        try { value = read(reference); }
        catch (Exception)
        {
            // Provider exceptions can contain the reference or secret. Do not retain them as inner exceptions.
            throw new InvalidOperationException("Required operator secret is unavailable.");
        }
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? Oidc(FederationSettings settings, IHostEnvironment environment) =>
        Oidc(settings, environment, Environment.GetEnvironmentVariable);

    public static string? Oidc(FederationSettings settings, IHostEnvironment environment, Func<string, string?> read)
    {
        if (settings.ClientSecretReference != null)
        {
            if (!string.IsNullOrEmpty(settings.ClientSecret))
                throw new InvalidOperationException("Ambiguous operator secret configuration.");
            return Resolve(settings.ClientSecretReference, read)
                ?? throw new InvalidOperationException("Required operator secret is unavailable.");
        }
        // A public OIDC code+PKCE client has no secret. It must never discover an implicit fallback.
        if (string.IsNullOrEmpty(settings.ClientSecret)) return null;
        if (!AllowsLiteralFixture(environment))
            throw new InvalidOperationException("OIDC client secrets require an operator reference.");
        return !string.IsNullOrWhiteSpace(settings.ClientSecret) ? settings.ClientSecret
            : throw new InvalidOperationException("Required operator secret is unavailable.");
    }

    public static string? Database(IConfiguration configuration, IHostEnvironment environment) =>
        Database(configuration, environment, Environment.GetEnvironmentVariable);

    public static string? Database(IConfiguration configuration, IHostEnvironment environment, Func<string, string?> read)
    {
        var reference = configuration["SecretReferences:Database"];
        var value = Resolve(reference ?? "POSEIDON_DATABASE", read);
        // An explicit missing reference is unavailable in every environment, never a request to use a literal.
        // Preserve isolated existing test/development fixtures only when no explicit reference was configured.
        return value ?? (reference == null && AllowsLiteralFixture(environment)
            ? configuration.GetConnectionString("Poseidon") ?? configuration["POSEIDON_DATABASE"] : null);
    }

    private static bool AllowsLiteralFixture(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Test");
}
