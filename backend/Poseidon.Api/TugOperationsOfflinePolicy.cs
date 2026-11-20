using System.Text.Json;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-092 offline tug operations: explicit server operator configuration only, never a caller flag. The sources name
// no approved storage/retention classification for an on-device tug job snapshot, so a missing or incomplete policy
// never permits a snapshot or a replay (UNAVAILABLE_POLICY). Gate TUG-OFFLINE-CLASSIFICATION stays open until an
// authorized privacy/retention decision exists. Master release, readiness GO, execution start/transitions, briefing
// issue, verification and daily-log submit/review are always online human records (TugOperationsOfflineRules.OnlineOnly).
public sealed class TugOperationsOfflinePolicy
{
    public const string RequiredClassification = "RestrictedOperationalSnapshot";
    public string Version { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string Classification { get; set; } = "";
    public string StoragePolicyReference { get; set; } = "";
    public string KeyPolicyReference { get; set; } = "";
    public string RetentionPolicyReference { get; set; } = "";
    public int LeaseSeconds { get; set; }
    public int MaxActiveSnapshots { get; set; }
    private static bool Text(string? value, int maximum = 256) => value is { Length: > 0 } && value.Length <= maximum && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    internal bool Configured => Text(Version, 80) && SourceVersion == TugOperationsOfflineRules.SourceVersion && Classification == RequiredClassification &&
        new[] { StoragePolicyReference, KeyPolicyReference, RetentionPolicyReference }.All(v => Text(v)) &&
        LeaseSeconds is >= 60 and <= 86400 && MaxActiveSnapshots is >= 1 and <= 16;
    internal string Fingerprint() => RegisterStorage.Hash(JsonSerializer.Serialize(new { Version, SourceVersion, Classification, StoragePolicyReference,
        KeyPolicyReference, RetentionPolicyReference, LeaseSeconds, MaxActiveSnapshots }));
}
public sealed class TugOperationsOfflineSettings
{
    public TugOperationsOfflinePolicy? Policy { get; set; }
    internal TugOperationsOfflinePolicy? Current => Policy?.Configured == true ? Policy : null;
}
