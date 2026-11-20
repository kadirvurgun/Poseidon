using System.Text.Json;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record ShipOpsAuthorityDto(Guid Id, Guid RoleId, string Duty);

// DEV-101/102 lifecycle authority: exactly one Human membership, a current
// server-provisioned appointment for this vessel and scope, a duty from the
// source role list and an active role context. Never derived from request data.
internal static class ShipOpsLifecycleAuthority
{
    internal static ShipOperationsAuthority[] Resolve(HttpContext h, TrustedActor? actor, Guid vessel, string scope) {
        if (actor == null || !actor.HasWriteAttribution || actor.RoleContext == null) return [];
        var duties = scope == ShipOperationsLifecycle.SofScope ? ShipOperationsLifecycle.SofDuties : scope == ShipOperationsLifecycle.DowntimeScope ? ShipOperationsLifecycle.DowntimeDuties : [];
        var members = h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return [];
        var grants = members[0].ShipOperationsAuthorities ?? [];
        if (grants.Length > 256) return [];
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var roles = actor.RoleContext.Split(',');
        return grants.Where(g => g != null && g.Id != Guid.Empty && grants.Count(o => o != null && o.Id == g.Id) == 1 && g.VesselId == vessel && g.Scope == scope &&
            duties.Contains(g.Duty, StringComparer.Ordinal) && g.RoleId != Guid.Empty && roles.Contains(g.RoleId.ToString("D")) &&
            ShipOperationsLifecycle.AppointmentSource(g.AppointmentSource) && g.ValidUntil > g.ValidFrom && g.ValidFrom <= now && now < g.ValidUntil).ToArray();
    }
    internal static string Hash(ShipOperationsAuthority grant, TrustedActor actor) =>
        RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actor.Id, actor.TenantId, actorKind = "Human" }, ShipOperationsEndpoints.Json));
    internal static ShipOpsAuthorityDto[] Dto(ShipOperationsAuthority[] grants) => grants.Select(g => new ShipOpsAuthorityDto(g.Id, g.RoleId, g.Duty)).ToArray();
}
