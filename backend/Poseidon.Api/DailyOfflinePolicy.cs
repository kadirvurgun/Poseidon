using System.Text.Json;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DailyOfflineScope(Guid TenantId, Guid ActorId, Guid VesselId, string Kind);
public sealed record DailyOfflineField(string Key, string ClassificationReference, string Purpose, string PrivacyClass);

// Server operator configuration only. A reference is configuration, not a legal
// approval. Empty or incomplete classification never permits local persistence.
public sealed class DailyOfflinePolicy
{
    public string Kind { get; set; } = "";
    public string Version { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string ResourcePolicy { get; set; } = "";
    public string StoragePolicyReference { get; set; } = "";
    public string KeyPolicyReference { get; set; } = "";
    public string RetentionPolicyReference { get; set; } = "";
    public int RetentionSeconds { get; set; }
    public int LeaseSeconds { get; set; }
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxEnrollments { get; set; }
    public int MaxRecords { get; set; }
    public int MaxEncryptedBytes { get; set; }
    public int MaxDeviceRecords { get; set; }
    public int MaxDeviceEncryptedBytes { get; set; }
    public DailyOfflineField[] Fields { get; set; } = [];
    public DailyOfflineScope[] Scopes { get; set; } = [];
    private static bool Text(string? value, int maximum = 256) => value is { Length: > 0 } && value.Length <= maximum &&
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    internal bool Configured => DailyOperationsPolicy.Kinds.Contains(Kind) && Text(Version,80) &&
        SourceVersion == DailyOperationsPolicy.SourceVersion && ResourcePolicy == "SelectedExistingOrNewDraft" &&
        new[] { StoragePolicyReference, KeyPolicyReference, RetentionPolicyReference }.All(v => Text(v)) &&
        LeaseSeconds is >= 60 and <= 86400 && RetentionSeconds >= LeaseSeconds && RetentionSeconds <= 2592000 &&
        NotBefore.Offset == TimeSpan.Zero && ExpiresAt.Offset == TimeSpan.Zero && ExpiresAt > NotBefore &&
        MaxEnrollments is >= 1 and <= 32 && MaxRecords is >= 2 and <= 128 && MaxEncryptedBytes is >= 131072 and <= 1048576 &&
        MaxDeviceRecords >= MaxRecords && MaxDeviceRecords <= 512 && MaxDeviceEncryptedBytes >= MaxEncryptedBytes && MaxDeviceEncryptedBytes <= 4194304 &&
        Fields is { Length: > 0 and <= 18 } && Fields.All(f => f != null &&
            DailyOperationsPolicy.Schema(Kind).Any(s => s.Key == f.Key && !s.ReadOnly) && Text(f.ClassificationReference) && Text(f.Purpose) &&
            f.PrivacyClass is "NonPersonalOperationalDraft" or "RestrictedOperationalDraft") &&
        Fields.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count() == Fields.Length &&
        Scopes is { Length: > 0 and <= 256 } && Scopes.All(s => s != null && s.TenantId != Guid.Empty && s.ActorId != Guid.Empty &&
            s.VesselId != Guid.Empty && s.Kind == Kind) && Scopes.Distinct().Count() == Scopes.Length;
    internal string Fingerprint() => RegisterStorage.Hash(JsonSerializer.Serialize(this));
    internal string SchemaHash() => RegisterStorage.Hash(JsonSerializer.Serialize(DailyOperationsPolicy.Schema(Kind)));
    internal bool Allows(Guid tenant, Guid actor, Guid vessel, string kind, DateTimeOffset now) => Configured && kind == Kind &&
        now >= NotBefore && now < ExpiresAt && Scopes.Contains(new(tenant, actor, vessel, kind));
    internal DailyOfflinePolicy Snapshot() => new() { Kind=Kind, Version=Version, SourceVersion=SourceVersion, ResourcePolicy=ResourcePolicy,
        StoragePolicyReference=StoragePolicyReference, KeyPolicyReference=KeyPolicyReference, RetentionPolicyReference=RetentionPolicyReference,
        RetentionSeconds=RetentionSeconds, LeaseSeconds=LeaseSeconds, NotBefore=NotBefore, ExpiresAt=ExpiresAt, MaxEnrollments=MaxEnrollments,
        MaxRecords=MaxRecords, MaxEncryptedBytes=MaxEncryptedBytes, MaxDeviceRecords=MaxDeviceRecords, MaxDeviceEncryptedBytes=MaxDeviceEncryptedBytes,
        Fields=Fields.Select(f=>f with {}).OrderBy(f=>f.Key,StringComparer.Ordinal).ToArray(),
        Scopes=Scopes.OrderBy(s=>s.TenantId).ThenBy(s=>s.ActorId).ThenBy(s=>s.VesselId).ToArray() };
    internal static string Operation(string kind) => "daily-operations." + kind.ToLowerInvariant() + "-draft.v1";
}
public sealed class DailyOfflineSettings
{
    public DailyOfflinePolicy[] Policies { get; set; } = [];
    internal DailyOfflinePolicy? For(Guid tenant, Guid actor, Guid vessel, string kind, DateTimeOffset now)
    {
        if (Policies is not { Length: > 0 and <= 3 } || Policies.Any(p=>p==null) || Policies.Select(p=>p.Kind).Distinct(StringComparer.Ordinal).Count()!=Policies.Length) return null;
        var policy=Policies.SingleOrDefault(p=>p.Kind==kind);
        return policy?.Allows(tenant,actor,vessel,kind,now)==true ? policy.Snapshot() : null;
    }
}
