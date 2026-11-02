namespace Poseidon.Domain;

// Storage conversion is not template migration, consent, verification or capture authorization.
public static class LegacyCrewImport
{
    public const string Reason = "ProtectHistoricalStorage";
    public const string Prefix = "protected-legacy-v1:";
    public static bool ValidRequest(uint version, string? reason) => version != 0 && reason == Reason;
    public static bool Convertible(string category, string table, bool alreadyProtected) =>
        category == "crew" && !alreadyProtected && !table.StartsWith("protected-", StringComparison.Ordinal);
}

// Exact strings, including unsupported schemas/unknown properties/whitespace, are retained.
// Never normalize these through today's ImportTable or current template mapping.
public sealed record HistoricalCrewImportSource(string TableJson, string BindingsJson, string SourceHash,
    string SourceName, string SourceLocator, string? OriginalIdentityPolicyVersion);
