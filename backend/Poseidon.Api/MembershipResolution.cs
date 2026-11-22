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

// Delegated permissions AND delegated scope are already intersected with the delegator's
// CURRENT authority (operator grant narrowed by the delegator's persisted membership).
// They are NOT merged into TrustedActor.Permissions. Contract for PLAN-1511-5402: a delegated
// permission may be applied only inside this delegation's own ActiveCompanyId/VesselGrants,
// never with the delegate's own vessel/company scope and never tenant-wide; the exact action
// resolver records actor + on-behalf-of + delegationId together (52.02 paragraph:102-103).
public sealed record EffectiveDelegation(Guid DelegationId, Guid DelegatorActorId, Guid DelegatorMembershipId, string[] Permissions,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, string ReasonCode, Guid? ActiveCompanyId, VesselGrant[] VesselGrants)
{
    // Delegations never convey tenant-wide read/manage scope.
    public bool TenantWide => false;
}

// Current authority of a delegation source, using the same narrowing rules as ActorResolver:
// non-Active => none; vessel grants = operator ∩ persisted snapshot (edit only when both allow);
// company survives only when operator and persisted snapshot agree; permissions follow the
// operator allow-list live.
internal static class DelegationAuthority
{
    internal static DelegationSource? Current(FederationSettings settings, Guid tenant, Guid accountId, string issuer, string? subject,
        string persistedStatus, Guid? persistedCompany, IReadOnlyCollection<AccountMembershipVesselGrant> persistedGrants, ProvisionedGrant? provisioned = null)
    {
        if (persistedStatus != "Active") return null;
        var config = settings.Memberships.Where(m => m.Issuer == issuer && m.Subject == subject).ToArray();
        // USER-ADMIN-01: an admin-provisioned source (no operator entry) uses its current database grant.
        if (config.Length == 0 && provisioned != null)
            return new DelegationSource(ActorResolver.TrustedPermissions(provisioned.Permissions), provisioned.ActiveCompanyId,
                provisioned.VesselGrants.Select(v => new DelegatedVessel(v.VesselId, v.CanEdit)).ToArray());
        if (config.Length != 1 || config[0].ActorId != accountId || config[0].TenantId != tenant) return null;
        var m = config[0];
        var vessels = (m.VesselGrants ?? []).Where(g => g.VesselId != Guid.Empty && persistedGrants.Any(p => p.VesselId == g.VesselId))
            .GroupBy(g => g.VesselId)
            .Select(g => new DelegatedVessel(g.Key, g.All(x => x.CanEdit) && persistedGrants.Any(p => p.VesselId == g.Key && p.CanEdit))).ToArray();
        var company = m.ActiveCompanyId is { } c && c != Guid.Empty && c == persistedCompany ? c : (Guid?)null;
        return new DelegationSource(ActorResolver.TrustedPermissions(m.Permissions), company, vessels);
    }
}

public sealed record MembershipSnapshot(Guid AccountId, Guid MembershipId, Guid TenantId, MembershipStatus Status, long AccessRevision,
    string? RoleContext, Guid? ActiveCompanyId, VesselGrant[] VesselGrants, bool IdentityConflict, ProvisionedGrant? Grant = null);

// USER-ADMIN-01: current (latest immutable revision) grant of an admin-provisioned membership. Permissions are the
// effective snapshot stored at grant time (already bounded by the granting administrator); they still pass the
// trusted allow-list at resolution. RoleContext is the canonical set of bundle role ids.
public sealed record ProvisionedGrant(int Revision, string[] Bundles, string[] IndividualPermissions, string[] Permissions, bool TenantWideRead,
    bool TenantWideManage, Guid? ActiveCompanyId, VesselGrant[] VesselGrants)
{
    public string? RoleContext => ActorResolver.TrustedRoleContext(Bundles.Select(b => MembershipRoleBundles.Find(b)?.RoleId.ToString("D")).OfType<string>().ToArray());
}

public static class ProvisionedGrants
{
    public const string Source = "AdminProvisioned";

    public static async Task<Dictionary<Guid, ProvisionedGrant>> LoadAsync(PoseidonDbContext db, Guid[] membershipIds, CancellationToken ct)
    {
        if (membershipIds.Length == 0) return [];
        var revisions = await db.MembershipGrantRevisions.AsNoTracking().Where(g => membershipIds.Contains(g.MembershipId)).ToArrayAsync(ct);
        var latest = revisions.GroupBy(g => g.MembershipId).Select(g => g.MaxBy(x => x.Revision)!).ToArray();
        var keys = latest.Select(g => g.MembershipId).ToArray();
        var vessels = (await db.MembershipGrantVessels.AsNoTracking().Where(v => keys.Contains(v.MembershipId)).ToArrayAsync(ct))
            .Where(v => latest.Any(l => l.MembershipId == v.MembershipId && l.Revision == v.Revision)).ToLookup(v => v.MembershipId);
        return latest.ToDictionary(g => g.MembershipId, g => new ProvisionedGrant(g.Revision, MembershipGrantPolicy.Split(g.Bundles),
            MembershipGrantPolicy.Split(g.IndividualPermissions), MembershipGrantPolicy.Split(g.Permissions), g.TenantWideRead, g.TenantWideManage,
            g.ActiveCompanyId, vessels[g.MembershipId].OrderBy(v => v.VesselId).Select(v => new VesselGrant(v.VesselId, v.CanEdit)).ToArray()));
    }
}

// USER-ADMIN-01 login lookup: the only cross-tenant read, keyed exclusively by the IdP-validated issuer+subject of
// the ID token being accepted (never by request input). It returns the binding of an admin-provisioned account only;
// its tenant is then stamped into the protected session cookie and every later request re-reads that tenant's state.
public sealed class ProvisionedIdentityLocator(IConfiguration configuration, IHostEnvironment environment)
{
    public async Task<ConfiguredIdentity?> FindAsync(string issuer, string subject, string correlationId, CancellationToken ct)
    {
        var connection = OperatorSecretReferences.Database(configuration, environment);
        if (string.IsNullOrWhiteSpace(connection)) throw new AuditUnavailableException();
        await using var db = new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connection).Options,
            Guid.Empty, new ActorContext("system:membership-resolver", "System", correlationId, Source: "MembershipResolver"));
        var rows = await db.UserAccounts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.Issuer == issuer && a.Subject == subject && a.Source == ProvisionedGrants.Source)
            .Select(a => new { a.TenantId, a.Id }).Take(2).ToArrayAsync(ct);
        return rows.Length == 1 ? new ConfiguredIdentity(issuer, subject, rows[0].Id, rows[0].TenantId, Provisioned: true) : null;
    }
}

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
        var provisioned = membership[0].Source == ProvisionedGrants.Source
            ? (await ProvisionedGrants.LoadAsync(db, [membership[0].Id], ct)).GetValueOrDefault(membership[0].Id) : null;
        return new(identity.ActorId, membership[0].Id, identity.TenantId, status, membership[0].AccessRevision,
            membership[0].RoleContext, membership[0].ActiveCompanyId, grants, false, provisioned);
    }

    private static MembershipSnapshot Conflict(ConfiguredIdentity identity) =>
        new(identity.ActorId, Guid.Empty, identity.TenantId, MembershipStatus.Revoked, long.MaxValue, null, null, [], true);
}

// Runs after authentication and before authorization on every request. A persisted
// non-active membership or a newer access revision turns the request anonymous, so
// all existing endpoint policies deny it (401) without any endpoint-specific code.
public sealed class MembershipSessionGate(RequestDelegate next)
{
    // EF reports a failed connection as InvalidOperationException("transient failure") wrapping
    // NpgsqlException, so the whole inner chain is classified.
    internal static bool StateUnavailable(Exception ex) => ex is AuditUnavailableException or TimeoutException || PostgresFailure.IsDatabase(ex) ||
        ex.InnerException is { } inner && StateUnavailable(inner);

    public async Task InvokeAsync(HttpContext context, ActorResolver actors, MembershipStateReader reader, SecurityAudit audit)
    {
        if (actors.SessionIdentityFor(context.User) is { } identity)
        {
            MembershipSnapshot? snapshot;
            try { snapshot = await reader.ReadAsync(identity, context.TraceIdentifier, context.RequestAborted); }
            catch (Exception ex) when (StateUnavailable(ex))
            {
                // Fail closed: current state unknown => no request proceeds with the old authority.
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
                if (context.Response.HasStarted) throw;
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { code = "service_unavailable", correlationId = context.TraceIdentifier }, context.RequestAborted);
                return;
            }
            // A provisioned session has no operator source to fall back to: missing state or grant denies.
            var admitted = identity.Provisioned ? snapshot is { IdentityConflict: false, Grant: not null } &&
                    MembershipLifecyclePolicy.AdmitsSession(snapshot.Status, snapshot.AccessRevision, MembershipSession.SessionAccessRevision(context.User))
                : snapshot is null || !snapshot.IdentityConflict &&
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
            .Where(d => d.DelegateMembershipId == snapshot.MembershipId && d.Status == "Active" && d.ValidFrom <= now && d.ValidUntil > now &&
                d.ReasonCode != DelegationPolicy.LegacyUnscoped)
            .Join(db.AccountMemberships.AsNoTracking().Where(m => m.Status == "Active"), d => d.DelegatorMembershipId, m => m.Id,
                (d, m) => new { d, m.AccountId, m.Status, m.ActiveCompanyId, m.Source })
            .Join(db.UserAccounts.AsNoTracking(), x => x.AccountId, a => a.Id, (x, a) => new { x.d, x.Status, x.ActiveCompanyId, x.Source, a.Id, a.Issuer, a.Subject })
            .OrderBy(x => x.d.ValidUntil).ThenBy(x => x.d.Id).Take(64).ToArrayAsync(cancellationToken);
        var delegationIds = rows.Select(x => x.d.Id).ToArray();
        var sourceIds = rows.Select(x => x.d.DelegatorMembershipId).Distinct().ToArray();
        var scopes = (await db.DelegationVesselScopes.AsNoTracking().Where(v => delegationIds.Contains(v.DelegationId)).ToArrayAsync(cancellationToken))
            .ToLookup(v => v.DelegationId);
        var sourceGrants = (await db.AccountMembershipVesselGrants.AsNoTracking().Where(g => sourceIds.Contains(g.MembershipId)).ToArrayAsync(cancellationToken))
            .ToLookup(g => g.MembershipId);
        var provisionedSources = await ProvisionedGrants.LoadAsync(db, rows.Where(x => x.Source == ProvisionedGrants.Source).Select(x => x.d.DelegatorMembershipId).Distinct().ToArray(), cancellationToken);
        var delegations = new List<EffectiveDelegation>();
        foreach (var row in rows)
        {
            // The delegator's CURRENT authority bounds both the permissions and the scope at request time.
            var source = DelegationAuthority.Current(settings, actor.TenantId, row.Id, row.Issuer, row.Subject, row.Status, row.ActiveCompanyId,
                sourceGrants[row.d.DelegatorMembershipId].ToArray(), provisionedSources.GetValueOrDefault(row.d.DelegatorMembershipId));
            if (source == null) continue;
            var effective = DelegationPolicy.Effective(row.d.Permissions.Split(','), row.d.CompanyId,
                scopes[row.d.Id].Select(v => new DelegatedVessel(v.VesselId, v.CanEdit)), source);
            if (effective.IsEmpty) continue; // Nothing left to apply; the delegate's own scope is never substituted.
            delegations.Add(new(row.d.Id, row.Id, row.d.DelegatorMembershipId, effective.Permissions, row.d.ValidFrom, row.d.ValidUntil,
                row.d.ReasonCode, effective.CompanyId, effective.Vessels.Select(v => new VesselGrant(v.VesselId, v.CanEdit)).ToArray()));
        }
        return new(actor.Id, actor.TenantId, snapshot.MembershipId, MembershipLifecyclePolicy.Wire(snapshot.Status), snapshot.AccessRevision,
            true, actor.ActiveCompanyId, delegations.ToArray());
    }
}
