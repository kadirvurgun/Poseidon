using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// PLAN-1511-5202 public membership-resolution contract. PLAN-1511-5402 (exact action scope
// resolver) consumes ICurrentMembershipResolver; it must not read FederationSettings or the
// identity tables directly. Results are current-request facts, never cached grants.
public interface ICurrentMembershipResolver
{
    // Null when the caller has no current trusted membership (unauthenticated, unknown,
    // suspended, revoked, identity conflict or a stale session access revision).
    Task<CurrentMembership?> ResolveAsync(HttpContext http, CancellationToken cancellationToken);
}

public sealed record CurrentMembership(Guid ActorId, Guid TenantId, Guid? MembershipId, string Status, long AccessRevision,
    bool Persisted, Guid? ActiveCompanyId, EffectiveDelegation[] ReceivedDelegations);

// Delegated permissions are already intersected with the delegator's current operator grant.
// They are NOT merged into TrustedActor.Permissions; an exact action resolver must apply
// them per action and record actor + on-behalf-of + delegationId together (52.02 §6).
public sealed record EffectiveDelegation(Guid DelegationId, Guid DelegatorActorId, Guid DelegatorMembershipId, string[] Permissions,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);

public sealed record MembershipSnapshot(Guid AccountId, Guid MembershipId, Guid TenantId, MembershipStatus Status, long AccessRevision,
    string? RoleContext, Guid? ActiveCompanyId, VesselGrant[] VesselGrants, bool IdentityConflict);

internal sealed record MembershipOverlay(string Issuer, string Subject, MembershipSnapshot? Snapshot, bool Denied);

public static class MembershipSession
{
    // Access revision stamped into the session at login; compared on every request.
    public const string AccessRevisionClaim = "poseidon:arv";
    internal const string OverlayItem = "Poseidon.MembershipOverlay";

    public static long SessionAccessRevision(ClaimsPrincipal principal)
    {
        var claims = principal.FindAll(AccessRevisionClaim).ToArray();
        if (claims.Length == 0) return 0; // Sessions issued before persisted lifecycle state.
        return claims.Length == 1 && long.TryParse(claims[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : -1;
    }
}

// Reads persisted lifecycle state with a dedicated read-only context bound to the operator
// tenant. It never writes and never exposes an administrative/cross-tenant query.
public sealed class MembershipStateReader(IConfiguration configuration, IHostEnvironment environment)
{
    public async Task<MembershipSnapshot?> ReadAsync(ConfiguredIdentity identity, string correlationId, CancellationToken ct)
    {
        var connection = OperatorSecretReferences.Database(configuration, environment);
        if (string.IsNullOrWhiteSpace(connection)) throw new AuditUnavailableException();
        await using var db = new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connection).Options,
            identity.TenantId, new ActorContext("system:membership-resolver", "System", correlationId, Source: "MembershipResolver"));
        var accounts = await db.UserAccounts.AsNoTracking()
            .Where(a => a.Issuer == identity.Issuer && a.Subject == identity.Subject || a.Id == identity.ActorId)
            .Select(a => new { a.Id, a.Issuer, a.Subject }).Take(3).ToArrayAsync(ct);
        if (accounts.Length == 0) return null; // Not imported: operator bootstrap source governs.
        if (accounts.Length != 1 || accounts[0].Id != identity.ActorId || accounts[0].Issuer != identity.Issuer || accounts[0].Subject != identity.Subject)
            return Conflict(identity);
        var membership = await db.AccountMemberships.AsNoTracking().Where(m => m.AccountId == identity.ActorId).Take(2).ToArrayAsync(ct);
        if (membership.Length != 1 || MembershipLifecyclePolicy.Parse(membership[0].Status) is not { } status) return Conflict(identity);
        var grants = await db.AccountMembershipVesselGrants.AsNoTracking().Where(g => g.MembershipId == membership[0].Id)
            .Select(g => new VesselGrant(g.VesselId, g.CanEdit)).ToArrayAsync(ct);
        return new(identity.ActorId, membership[0].Id, identity.TenantId, status, membership[0].AccessRevision,
            membership[0].RoleContext, membership[0].ActiveCompanyId, grants, false);
    }

    private static MembershipSnapshot Conflict(ConfiguredIdentity identity) =>
        new(identity.ActorId, Guid.Empty, identity.TenantId, MembershipStatus.Revoked, long.MaxValue, null, null, [], true);
}

// Runs after authentication and before authorization on every request. A persisted
// non-active membership or a newer access revision turns the request anonymous, so
// all existing endpoint policies deny it (401) without any endpoint-specific code.
public sealed class MembershipSessionGate(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ActorResolver actors, MembershipStateReader reader, SecurityAudit audit)
    {
        if (actors.ConfiguredIdentityFor(context.User) is { } identity)
        {
            var snapshot = await reader.ReadAsync(identity, context.TraceIdentifier, context.RequestAborted);
            var admitted = snapshot is null || !snapshot.IdentityConflict &&
                MembershipLifecyclePolicy.AdmitsSession(snapshot.Status, snapshot.AccessRevision, MembershipSession.SessionAccessRevision(context.User));
            if (!admitted)
            {
                // Attributed denial before the principal is removed; then fail closed.
                await audit.WriteAsync(context, "SessionRevalidation", "membership_inactive");
                context.Items[MembershipSession.OverlayItem] = new MembershipOverlay(identity.Issuer, identity.Subject, snapshot, true);
                if (!context.Response.HasStarted) await context.SignOutAsync(FoundationIdentity.Cookie);
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else context.Items[MembershipSession.OverlayItem] = new MembershipOverlay(identity.Issuer, identity.Subject, snapshot, false);
        }
        await next(context);
    }
}

public sealed class CurrentMembershipResolver(ActorResolver actors, FederationSettings settings, TimeProvider time) : ICurrentMembershipResolver
{
    public async Task<CurrentMembership?> ResolveAsync(HttpContext http, CancellationToken cancellationToken)
    {
        var actor = actors.Resolve(http.User);
        if (actor == null) return null;
        var snapshot = (http.Items[MembershipSession.OverlayItem] as MembershipOverlay)?.Snapshot;
        if (snapshot == null)
            return new(actor.Id, actor.TenantId, null, "Active", 0, false, actor.ActiveCompanyId, []);
        var db = http.RequestServices.GetRequiredService<PoseidonDbContext>();
        var now = time.GetUtcNow();
        var rows = await db.MembershipDelegations.AsNoTracking()
            .Where(d => d.DelegateMembershipId == snapshot.MembershipId && d.Status == "Active" && d.ValidFrom <= now && d.ValidUntil > now)
            .Join(db.AccountMemberships.AsNoTracking().Where(m => m.Status == "Active"), d => d.DelegatorMembershipId, m => m.Id, (d, m) => new { d, m.AccountId })
            .Join(db.UserAccounts.AsNoTracking(), x => x.AccountId, a => a.Id, (x, a) => new { x.d, a.Id, a.Issuer, a.Subject })
            .OrderBy(x => x.d.ValidUntil).ThenBy(x => x.d.Id).Take(64).ToArrayAsync(cancellationToken);
        var delegations = new List<EffectiveDelegation>();
        foreach (var row in rows)
        {
            // The delegator's current operator grant bounds the delegation at request time.
            var source = settings.Memberships.Where(m => m.Issuer == row.Issuer && m.Subject == row.Subject).ToArray();
            if (source.Length != 1 || source[0].ActorId != row.Id || source[0].TenantId != actor.TenantId) continue;
            var current = (source[0].Permissions ?? []).ToHashSet(StringComparer.Ordinal);
            var permissions = row.d.Permissions.Split(',').Where(p => current.Contains(p) && !DelegationPolicy.NonDelegable(p)).ToArray();
            if (permissions.Length == 0) continue;
            delegations.Add(new(row.d.Id, row.Id, row.d.DelegatorMembershipId, permissions, row.d.ValidFrom, row.d.ValidUntil));
        }
        return new(actor.Id, actor.TenantId, snapshot.MembershipId, MembershipLifecyclePolicy.Wire(snapshot.Status), snapshot.AccessRevision,
            true, actor.ActiveCompanyId, delegations.ToArray());
    }
}
