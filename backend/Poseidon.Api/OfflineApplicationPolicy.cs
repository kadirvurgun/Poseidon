using Poseidon.Domain;

namespace Poseidon.Api;

// Explicit operator configuration; empty/unreviewed settings never enable storage or replay.
// The catalog is deliberately tenant/vessel/actor scoped and cannot be supplied by HTTP callers.
public sealed class OfflineApplicationPolicy
{
    public string Fingerprint() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this)));
    public string Version { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string FieldClassification { get; set; } = "";
    public string DevicePolicyReference { get; set; } = "";
    public string KeyPolicyReference { get; set; } = "";
    public string StoragePolicyReference { get; set; } = "";
    public string RetentionPolicyReference { get; set; } = "";
    public string Operation { get; set; } = "";
    public string[] Fields { get; set; } = [];
    public string[] WatchGroups { get; set; } = [];
    public OfflineScopeRule[] Scopes { get; set; } = [];
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int LeaseSeconds { get; set; }
    public int MaxRecords { get; set; }
    public int MaxEncryptedBytes { get; set; }
    public int MaxDeviceRecords { get; set; }
    public int MaxDeviceEncryptedBytes { get; set; }
    public int MaxEnrollments { get; set; }
    public bool Allows(Guid tenant, Guid actor, Guid vessel, DateTimeOffset now) => Configured &&
        NotBefore <= now && now < ExpiresAt && Scopes.Any(s => s.TenantId == tenant && s.ActorId == actor && s.VesselId == vessel);
    public bool Configured => Version is { Length: > 0 and <= 80 } && SourceVersion == OfflineWatchCapture.Source &&
        new[] { Purpose, DevicePolicyReference, KeyPolicyReference, StoragePolicyReference, RetentionPolicyReference }.All(x => x is { Length: > 0 and <= 256 }) &&
        FieldClassification == "NonPersonalControlledDraftDeclaration" && Operation == OfflineWatchCapture.Operation &&
        Fields is { Length: 1 } && Fields[0] == "watch_group" &&
        WatchGroups is { Length: > 0 and <= 64 } && WatchGroups.All(x => x is { Length: > 0 and <= 64 } && !x.Any(char.IsControl)) &&
        WatchGroups.Distinct(StringComparer.Ordinal).Count() == WatchGroups.Length &&
        Scopes is { Length: > 0 and <= 256 } && Scopes.All(s => s != null && s.TenantId != Guid.Empty && s.ActorId != Guid.Empty && s.VesselId != Guid.Empty) &&
        Scopes.Select(s => (s.TenantId, s.ActorId, s.VesselId)).Distinct().Count() == Scopes.Length &&
        ExpiresAt > NotBefore && LeaseSeconds is >= 60 and <= 86400 && MaxEnrollments is >= 1 and <= 32 &&
        MaxRecords is >= 2 and <= 128 && MaxEncryptedBytes is >= 4096 and <= 1048576 &&
        MaxDeviceRecords >= MaxRecords && MaxDeviceRecords <= 512 &&
        MaxDeviceEncryptedBytes >= MaxEncryptedBytes && MaxDeviceEncryptedBytes <= 4194304;
}
public sealed record OfflineScopeRule(Guid TenantId, Guid ActorId, Guid VesselId);
