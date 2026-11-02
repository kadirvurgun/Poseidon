namespace Poseidon.Api;

// Environment names are operator references, not credential values. Never echo either resolution errors or values.
public static class OperatorSecretReferences
{
    public static string? Resolve(string? reference, Func<string, string?> read)
    {
        if (reference is not { Length: > 0 and <= 80 } || !char.IsAsciiLetterUpper(reference[0]) ||
            reference.Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c) && c != '_'))
            throw new InvalidOperationException("Invalid operator secret reference.");
        var value = read(reference);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? Database(IConfiguration configuration, IHostEnvironment environment)
    {
        var value = Resolve(configuration["SecretReferences:Database"] ?? "POSEIDON_DATABASE", Environment.GetEnvironmentVariable);
        // Preserve isolated existing test/development configuration; production never falls back to a literal appsetting.
        return value ?? (environment.IsDevelopment() || environment.IsEnvironment("Test") ? configuration.GetConnectionString("Poseidon") ?? configuration["POSEIDON_DATABASE"] : null);
    }
}
