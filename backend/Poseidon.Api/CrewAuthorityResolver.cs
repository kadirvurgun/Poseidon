using System.Text.Json;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record CrewAuthorityDto(Guid Id, Guid RoleId, string Duty);

// S7 WP-A human gates: exactly one Human membership, a current server-provisioned
// appointment for this vessel and scope, a duty from the scope's closed list and an
// active role context. Never derived from request data, IdP role names or crew lists.
internal static class CrewAuthorityResolver
{
    internal const string CredentialVerification = "CredentialVerification";
    internal const string CompetenceAssessment = "CompetenceAssessment";
    internal const string SafeManningDecision = "SafeManningDecision";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static string[] Duties(string scope) => scope switch {
        CredentialVerification => CrewCompetenceStorage.CredentialDuties,
        CompetenceAssessment => CrewCompetenceStorage.AssessmentDuties,
        SafeManningDecision => CrewCompetenceStorage.DecisionDuties,
        _ => []
    };

    private static bool Source(string? value) => value is { Length: > 0 and <= 200 } && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);

    private static bool Grant(CrewAuthority? g, CrewAuthority[] all, Guid vessel, string scope, DateTimeOffset now) =>
        g != null && g.Id != Guid.Empty && all.Count(o => o != null && o.Id == g.Id) == 1 && g.VesselId == vessel && g.Scope == scope &&
        Duties(scope).Contains(g.Duty, StringComparer.Ordinal) && g.RoleId != Guid.Empty && Source(g.AppointmentSource) &&
        g.ValidUntil > g.ValidFrom && g.ValidFrom <= now && now < g.ValidUntil;

    internal static CrewAuthority[] Resolve(FederationSettings settings, TrustedActor? actor, Guid vessel, string scope, DateTimeOffset now)
    {
        if (actor == null || !actor.HasWriteAttribution || actor.RoleContext == null) return [];
        var members = settings.Memberships.Where(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).Take(2).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return [];
        var grants = members[0].CrewAuthorities ?? [];
        if (grants.Length > 256) return [];
        var roles = actor.RoleContext.Split(',');
        return grants.Where(g => Grant(g, grants, vessel, scope, now) && roles.Contains(g.RoleId.ToString("D"))).ToArray();
    }

    // A recorded act stays current only while its appointment is still provisioned and valid.
    internal static bool StillValid(FederationSettings settings, Guid tenant, Guid actorId, Guid authorityId, Guid roleId, string scope, Guid vessel, DateTimeOffset now)
    {
        var members = settings.Memberships.Where(m => m.ActorId == actorId && m.TenantId == tenant).Take(2).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return false;
        var grants = members[0].CrewAuthorities ?? [];
        if (grants.Length > 256) return false;
        var matches = grants.Where(g => g != null && g.Id == authorityId).ToArray();
        return matches.Length == 1 && matches[0].RoleId == roleId && Grant(matches[0], grants, vessel, scope, now);
    }

    internal static string Hash(CrewAuthority grant, TrustedActor actor) =>
        RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actor.Id, actor.TenantId, actorKind = "Human" }, Json));
    internal static CrewAuthorityDto[] Dto(CrewAuthority[] grants) => grants.Select(g => new CrewAuthorityDto(g.Id, g.RoleId, g.Duty)).ToArray();
}
