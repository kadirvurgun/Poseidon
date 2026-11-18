using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// PLAN-1511-5402: API adapter for the exact-action resolver. The subject is projected only from
// ActorResolver (server-provisioned membership); requests never contribute actor, tenant or grants.
public static class ExactActionAccess
{
    private const string RevokedBeforeCommitItem = "Poseidon.ExactAction.RevokedBeforeCommit";

    public static AccessSubject Subject(this TrustedActor actor) => new(actor.Id, actor.TenantId, actor.ActiveCompanyId,
        actor.Permissions, actor.TenantWideRead, actor.TenantWideManage,
        actor.VesselGrants.Select(g => new AccessGrant(g.VesselId, g.CanEdit)).ToArray(), actor.RoleContext);

    public static ExactAccessDecision Evaluate(TrustedActor? actor, ExactAccessRequest request) =>
        ExactActionResolver.Evaluate(actor?.Subject(), request);

    // Commit-time revalidation: re-resolve the CURRENT membership from the live principal inside
    // the caller's write transaction, immediately before CommitAsync. Never reuses the earlier allow.
    public static ExactRevalidation RevalidateBeforeCommit(HttpContext http, ExactAccessDecision prior, ExactAccessRequest request, Guid expectedActor)
    {
        var live = http.RequestServices.GetRequiredService<ActorResolver>().Resolve(http.User);
        return ExactActionResolver.Revalidate(prior, live?.Subject(), request, expectedActor);
    }

    // Vessel-scoped write revalidation: the exact action must still hold AND the live actor must still
    // see the vessel through its current scope (tenant, grants and kind visibility mode.tug/mode.cross).
    public static async Task<bool> VesselWriteHoldsBeforeCommit(HttpContext http, PoseidonDbContext db, ExactAccessDecision prior, ExactAccessRequest request, TrustedActor actor)
    {
        var live = http.RequestServices.GetRequiredService<ActorResolver>().Resolve(http.User);
        if (live == null || live.Id != actor.Id || live.TenantId != actor.TenantId || request.VesselId is not { } vessel) return false;
        if (!ExactActionResolver.Revalidate(prior, live.Subject(), request, actor.Id).Allowed) return false;
        return await live.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == vessel, http.RequestAborted);
    }

    // Set when a nested write (import row) lost access at commit-time revalidation.
    public static void MarkRevokedBeforeCommit(HttpContext http) => http.Items[RevokedBeforeCommitItem] = true;
    public static bool RevokedBeforeCommit(HttpContext http) => http.Items.ContainsKey(RevokedBeforeCommitItem);
}
