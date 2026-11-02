using Poseidon.Persistence;
using System.Text.Json;
using Poseidon.Domain;

namespace Poseidon.Api;

// Explicit deployment policy, not caller flags. No implicit field expansion from Q.
public sealed class TugOfflinePolicy {
    public const string Operation = "tug.draft.window.v1";
    public const string Source = "14.02:2316:1951-1952;11.01:2316:1951-1952:v46.3";
    public string Version { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string FieldClassification { get; set; } = "";
    public string DevicePolicyReference { get; set; } = "";
    public string KeyPolicyReference { get; set; } = "";
    public string StoragePolicyReference { get; set; } = "";
    public string RetentionPolicyReference { get; set; } = "";
    public string[] Fields { get; set; } = [];
    public int LeaseSeconds { get; set; }
    public bool Configured => SourceVersion == Source && FieldClassification == "NonPersonalControlledDraftDeclaration" &&
        new[] { Version, Purpose, DevicePolicyReference, KeyPolicyReference, StoragePolicyReference, RetentionPolicyReference }.All(s => s is { Length: > 0 and <= 80 } && !s.Any(char.IsControl)) &&
        Fields.SequenceEqual(new[] { "planned_start", "planned_end" }) && LeaseSeconds is >= 60 and <= 86400;
    public string Digest(TugPlanningPolicy planning) => RegisterStorage.Hash(JsonSerializer.Serialize(planning));
}
