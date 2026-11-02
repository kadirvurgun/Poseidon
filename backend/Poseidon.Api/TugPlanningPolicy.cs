using Poseidon.Domain;

namespace Poseidon.Api;

// Operator-controlled, versioned configuration. There is deliberately no built-in Tug catalog.
public sealed class TugPlanningPolicy
{
    public TugOfflinePolicy Offline { get; set; } = new();
    public string Version { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public TugPlanningScope[] Scopes { get; set; } = [];
    public TugTypeOption[] Types { get; set; } = [];
    public TugTypeBinding[] Contexts { get; set; } = [];
    public TugBpSource[] BpSources { get; set; } = [];
    public bool Current(DateTimeOffset now) => Version is { Length: > 0 and <= 80 } && SourceVersion == TugDraftPlanning.SourceVersion &&
        NotBefore <= now && now < ExpiresAt && Types is { Length: <= 128 } && Scopes != null && Contexts != null && BpSources != null &&
        Types.All(t => t != null && t.Code is { Length: > 0 and <= 64 } && t.Tr is { Length: > 0 and <= 100 } && t.En is { Length: > 0 and <= 100 } && !t.Code.Any(char.IsControl)) &&
        Types.Select(t => t.Code).Distinct(StringComparer.Ordinal).Count() == Types.Length &&
        Scopes.Length <= 256 && Scopes.All(s => s != null && s.TenantId != Guid.Empty && s.ActorId != Guid.Empty && s.VesselId != Guid.Empty && s.EligibleTugs != null && s.EligibleTugs.Length <= 1000 && s.EligibleTugs.All(t => t != Guid.Empty) && s.EligibleTugs.Distinct().Count() == s.EligibleTugs.Length) &&
        Scopes.Select(s => (s.TenantId, s.ActorId, s.VesselId)).Distinct().Count() == Scopes.Length &&
        Contexts.Length <= 1000 && Contexts.All(c => c != null && c.SourceReference is { Length: > 0 and <= 80 } && c.SourceRevision > 0 && Types.Any(t => t.Code == c.Code)) &&
        Contexts.Select(c => (c.TenantId, c.VesselId)).Distinct().Count() == Contexts.Length &&
        BpSources.Length <= 1000 && BpSources.All(s => s != null && s.Source is { Length: > 0 and <= 80 } && s.PolicyVersion is { Length: > 0 and <= 80 } && s.MaxAgeSeconds is >= 1 and <= 31536000) &&
        BpSources.Select(s => (s.TenantId, s.VesselId)).Distinct().Count() == BpSources.Length;
    public TugPlanningScope? Scope(TrustedActor a, Guid vessel) => Current(DateTimeOffset.UtcNow) ?
        Scopes.SingleOrDefault(s => s.TenantId == a.TenantId && s.ActorId == a.Id && s.VesselId == vessel) : null;
}
public sealed record TugPlanningScope(Guid TenantId, Guid ActorId, Guid VesselId, Guid[] EligibleTugs);
public sealed record TugTypeOption(string Code, string Tr, string En);
public sealed record TugTypeBinding(Guid TenantId, Guid VesselId, string Code, string SourceReference, long SourceRevision, DateTimeOffset ObservedAt);
public sealed record TugBpSource(Guid TenantId, Guid VesselId, Guid AssetId, string Source, string PolicyVersion, int MaxAgeSeconds);
