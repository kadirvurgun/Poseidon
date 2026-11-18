using Poseidon.Domain;

namespace Poseidon.Api;

// PLAN-1511-5402: API adapter for the exact-action resolver. The subject is projected only from
// ActorResolver (server-provisioned membership); requests never contribute actor, tenant or grants.
public static class ExactActionAccess
{
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
}
