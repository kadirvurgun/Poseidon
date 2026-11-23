using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record MembershipDto(Guid Id, Guid AccountId, string Status, string StatusReason, long AccessRevision, Guid? ActiveCompanyId,
    bool RoleBound, int VesselGrantCount, string Source, bool Administrator, DateTimeOffset StatusChangedAt, uint Version);
public sealed record MembershipPageDto(MembershipDto[] Items, int Offset, int Limit, bool HasMore, bool RecoveryRequired, int ConfiguredNotImported);
// Skip reasons are closed codes; issuer/subject are never returned. ActorId is the operator-configured id.
public sealed record MembershipImportSkip(Guid ActorId, string Reason);
public sealed record MembershipImportDto(int Imported, int AlreadyPresent, int Skipped, MembershipImportSkip[] Skips);
public sealed record DelegationDto(Guid Id, Guid DelegatorMembershipId, Guid DelegateMembershipId, string[] Permissions,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, string Status, string State, string? RevocationReason, uint Version,
    string ReasonCode, Guid? CompanyId, VesselGrant[] VesselScope);
public sealed record CurrentMembershipDto(Guid ActorId, Guid TenantId, Guid? MembershipId, string Status, long AccessRevision, bool Persisted,
    Guid? ActiveCompanyId, EffectiveDelegation[] ReceivedDelegations);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MembershipImportCommand;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MembershipTransitionCommand(uint? ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DelegationCreateCommand(Guid? DelegateMembershipId, string[]? Permissions, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil,
    string? ReasonCode = null, Guid? CompanyId = null, DelegationVesselCommand[]? VesselScope = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DelegationVesselCommand(Guid VesselId, bool CanEdit = false);

// PLAN-1511-5202 / 52.02 ACC04-ACC05, §6, §9. Tenant-scoped membership lifecycle commands.
// Exact controlled transitions only (no state PATCH); CSRF, Idempotency-Key, xmin CAS and
// audited SaveChanges in one SERIALIZABLE transaction so the last-admin count cannot race.
public static partial class MembershipEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 4 };
    private const string Description = "Requires antiforgery, a 16–80 character Idempotency-Key and server-provisioned membership.manage with tenant-wide manage scope and a trusted role. Transitions require expectedVersion; stale changes return 409. Voluntary removal of the last active identity administrator returns 409 last_admin. Replay returns the fresh representation.";

    internal static bool CanReadMemberships(TrustedActor a) => a.CanRead && (a.TenantWideRead || a.TenantWideManage) &&
        (a.Permissions.Contains("membership.read") || a.Permissions.Contains("membership.manage"));
    internal static bool CanManageMemberships(TrustedActor a) => a.HasWriteAttribution && a.CanRead && a.TenantWideManage && a.Permissions.Contains("membership.manage");
    internal static bool CanSecuritySuspend(TrustedActor a) => a.HasWriteAttribution && a.CanRead && a.TenantWideManage && a.Permissions.Contains("membership.security.suspend");
    // Import skip reasons (closed). Cross-tenant bindings skip the entry; config keeps governing that identity.
    internal static readonly string[] ImportSkipReasons = ["invalid_configuration", "duplicate_configuration", "actor_conflict", "identity_bound_other_tenant"];

    public static void MapMemberships(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/memberships/current", Current).RequireAuthorization("DirectoryRead").WithName("GetCurrentMembership")
            .WithSummary("Caller's current membership state and currently effective received delegations. Delegations are context for exact action checks, not generic grants.")
            .Produces<CurrentMembershipDto>().Produces(401).Produces(403).Produces(503);
        app.MapGet("/api/v1/memberships", List).RequireAuthorization("DirectoryRead").WithName("ListMemberships")
            .WithSummary("Tenant memberships (persisted lifecycle state). Issuer/subject are never returned.")
            .Produces<MembershipPageDto>().ProducesProblem(400).Produces(401).ProducesProblem(403).Produces(503);
        app.MapGet("/api/v1/memberships/{id:guid}", Detail).RequireAuthorization("DirectoryRead").WithName("GetMembership")
            .Produces<MembershipDto>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);
        app.MapGet("/api/v1/memberships/{id:guid}/delegations", Delegations).RequireAuthorization("DirectoryRead").WithName("ListMembershipDelegations")
            .WithSummary("Delegations where the membership is source or delegate. State is computed from server time.")
            .Produces<DelegationDto[]>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);
        Command(app.MapPost("/api/v1/memberships/import", (HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
            Import(h)), "ImportConfiguredMemberships", "Deterministically imports operator-provisioned memberships of the caller's tenant that have no persisted record. Never changes existing records and never widens access. Entries that cannot be imported (including an identity already bound in another tenant) are skipped with a closed reason; configuration keeps governing them. Replay returns the original result.")
            .Accepts<MembershipImportCommand>("application/json").Produces<MembershipImportDto>();
        foreach (var (segment, name, transition) in new[] {
            ("suspend", "SuspendMembership", MembershipTransition.Suspend), ("resume", "ResumeMembership", MembershipTransition.Resume),
            ("revoke", "RevokeMembership", MembershipTransition.Revoke), ("security-suspend", "SecuritySuspendMembership", MembershipTransition.SecuritySuspend) })
            Command(app.MapPost($"/api/v1/memberships/{{id:guid}}/{segment}", (Guid id, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
                Transition(h, id, transition)), name, transition == MembershipTransition.SecuritySuspend
                    ? "Forced security suspension (membership.security.suspend). Not blocked by the last-admin guard; removing the last administrator records RECOVERY_REQUIRED and creates no hidden administrator."
                    : transition == MembershipTransition.Resume
                        ? Description + " Resuming a security suspension requires membership.security.suspend (membership.manage alone returns 403) and closes its RECOVERY_REQUIRED case."
                        : Description)
                .Accepts<MembershipTransitionCommand>("application/json").Produces<MembershipDto>();
        Command(app.MapPost("/api/v1/memberships/{id:guid}/delegations", (Guid id, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
            CreateDelegation(h, id)), "CreateDelegation", "Approves a time-boxed delegation from this membership with a mandatory closed reasonCode and an explicit company/vessel scope. The approver can be neither source nor delegate; permissions and scope cannot exceed the source's current grant (400 exceeds_source_scope); tenant-wide scope is never delegated; re-delegation is not possible.")
            .Accepts<DelegationCreateCommand>("application/json").Produces<DelegationDto>();
        Command(app.MapPost("/api/v1/delegations/{id:guid}/revoke", (Guid id, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
            RevokeDelegation(h, id)), "RevokeDelegation", Description)
            .Accepts<MembershipTransitionCommand>("application/json").Produces<DelegationDto>();
        MapMembershipAdministration(app); // USER-ADMIN-01
        MapMembershipInvitations(app); // USER-ADMIN-02
    }

    private static RouteHandlerBuilder Command(RouteHandlerBuilder route, string name, string description) =>
        route.RequireAuthorization("DirectoryRead").WithName(name).WithSummary(name).WithDescription(description)
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).Produces(503);

    private static IResult Problem(HttpContext http, int status, string code, Dictionary<string, string[]>? errors = null) =>
        Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?>
        { ["code"] = code, ["errors"] = errors ?? [], ["correlationId"] = http.TraceIdentifier });

    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;

    internal static Guid MembershipIdFor(Guid tenant, Guid actor) =>
        new(SHA256.HashData(tenant.ToByteArray().Concat(actor.ToByteArray()).Concat(Encoding.UTF8.GetBytes("POSEIDON-MEMBERSHIP-v1")).ToArray()).AsSpan(0, 16));

    // Current active tenant identity administrators: operator grant (membership.manage, tenant-wide
    // manage, trusted role, directory.read) AND a persisted Active state when one exists.
    // Operator-side administrator predicate; the MembershipDto.Administrator flag and the last-admin
    // count both use ActiveAdministrators, so they can never disagree.
    internal static bool IsConfiguredAdministrator(FederationSettings settings, Membership m, Guid tenant) =>
        m.TenantId == tenant && m.ActorId != Guid.Empty && !string.IsNullOrEmpty(m.Issuer) &&
        !string.IsNullOrEmpty(m.Subject) && m.TenantWideManage && (m.Permissions ?? []).Contains("membership.manage") &&
        (m.Permissions ?? []).Contains("directory.read") && ActorResolver.TrustedRoleContext(m.RoleReferences) != null &&
        settings.Memberships.Count(x => x.Issuer == m.Issuer && x.Subject == m.Subject) == 1;

    internal static async Task<HashSet<Guid>> ActiveAdministrators(PoseidonDbContext db, FederationSettings settings, Guid tenant, CancellationToken ct)
    {
        var configured = settings.Memberships.Where(m => IsConfiguredAdministrator(settings, m, tenant)).ToArray();
        var persisted = await db.UserAccounts.AsNoTracking().Join(db.AccountMemberships.AsNoTracking(), a => a.Id, m => m.AccountId,
            (a, m) => new { a.Id, a.Issuer, a.Subject, m.Status, m.RoleContext, m.Source, MembershipId = m.Id }).ToArrayAsync(ct);
        var result = new HashSet<Guid>();
        foreach (var m in configured)
        {
            var rows = persisted.Where(x => x.Issuer == m.Issuer && x.Subject == m.Subject || x.Id == m.ActorId).ToArray();
            if (rows.Length == 0) { result.Add(m.ActorId); continue; } // Not imported: bootstrap source governs.
            if (rows.Length == 1 && rows[0].Id == m.ActorId && rows[0].Issuer == m.Issuer && rows[0].Subject == m.Subject &&
                rows[0].Status == "Active" && rows[0].RoleContext == ActorResolver.TrustedRoleContext(m.RoleReferences))
                result.Add(m.ActorId);
        }
        // USER-ADMIN-01: admin-provisioned memberships whose identity is not in the operator configuration
        // (config governs its own identities) count with the same predicate on their current grant.
        var provisioned = persisted.Where(x => x.Status == "Active" && x.Source == ProvisionedGrants.Source &&
            !settings.Memberships.Any(c => c.Issuer == x.Issuer && c.Subject == x.Subject)).ToArray();
        var grants = await ProvisionedGrants.LoadAsync(db, provisioned.Select(x => x.MembershipId).ToArray(), ct);
        foreach (var x in provisioned)
            if (grants.TryGetValue(x.MembershipId, out var g) && MembershipGrantPolicy.IsAdministrator(g.Permissions, g.TenantWideManage, g.Bundles.Length))
                result.Add(x.Id);
        return result;
    }

    private static async Task<MembershipDto[]> Dtos(PoseidonDbContext db, FederationSettings settings, Guid tenant, IQueryable<AccountMembership> query, CancellationToken ct)
    {
        var rows = await query.Join(db.UserAccounts.AsNoTracking(), m => m.AccountId, a => a.Id, (m, a) => new { m, a.Issuer, a.Subject }).ToArrayAsync(ct);
        var admins = await ActiveAdministrators(db, settings, tenant, ct);
        var ids = rows.Select(x => x.m.Id).ToArray();
        var grants = await db.AccountMembershipVesselGrants.AsNoTracking().Where(g => ids.Contains(g.MembershipId))
            .GroupBy(g => g.MembershipId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var provisioned = await ProvisionedGrants.LoadAsync(db, rows.Where(x => x.m.Source == ProvisionedGrants.Source).Select(x => x.m.Id).ToArray(), ct);
        return rows.Select(x => provisioned.TryGetValue(x.m.Id, out var p)
            ? new MembershipDto(x.m.Id, x.m.AccountId, x.m.Status, x.m.StatusReason, x.m.AccessRevision, p.ActiveCompanyId,
                p.RoleContext != null, p.VesselGrants.Length, x.m.Source, admins.Contains(x.m.AccountId), x.m.StatusChangedAt, x.m.Version)
            : new MembershipDto(x.m.Id, x.m.AccountId, x.m.Status, x.m.StatusReason, x.m.AccessRevision, x.m.ActiveCompanyId,
            x.m.RoleContext != null, grants.GetValueOrDefault(x.m.Id), x.m.Source, admins.Contains(x.m.AccountId),
            x.m.StatusChangedAt, x.m.Version)).ToArray();
    }

    private static async Task<IResult> Current(HttpContext h, ICurrentMembershipResolver resolver, SecurityAudit audit)
    {
        var current = await resolver.ResolveAsync(h, h.RequestAborted);
        if (current == null) { await audit.WriteAsync(h, "MembershipRead", "Denied"); return Problem(h, 403, "permission_denied"); }
        await audit.WriteAsync(h, "MembershipRead", "Success");
        return Results.Ok(new CurrentMembershipDto(current.ActorId, current.TenantId, current.MembershipId, current.Status, current.AccessRevision,
            current.Persisted, current.ActiveCompanyId, current.ReceivedDelegations));
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db, FederationSettings settings, SecurityAudit audit, int? offset, int? limit, string? status)
    {
        var actor = Actor(h);
        if (!CanReadMemberships(actor)) { await audit.WriteAsync(h, "MembershipRead", "permission_denied"); return Problem(h, 403, "permission_denied"); }
        var skip = offset ?? 0; var take = limit ?? 25;
        if (skip < 0 || take is < 1 or > 100 || status != null && MembershipLifecyclePolicy.Parse(status) == null)
        { await audit.WriteAsync(h, "MembershipRead", "invalid_query"); return Problem(h, 400, "invalid_query"); }
        var query = db.AccountMemberships.AsNoTracking().Where(m => status == null || m.Status == status).OrderBy(m => m.AccountId).ThenBy(m => m.Id).Skip(skip).Take(take + 1);
        var items = await Dtos(db, settings, actor.TenantId, query, h.RequestAborted);
        var recovery = await db.TenantAccessRecoveries.AsNoTracking().AnyAsync(r => r.ResolvedAt == null, h.RequestAborted) &&
            (await ActiveAdministrators(db, settings, actor.TenantId, h.RequestAborted)).Count == 0;
        var accounts = await db.UserAccounts.AsNoTracking().Select(a => new { a.Issuer, a.Subject }).ToArrayAsync(h.RequestAborted);
        var notImported = settings.Memberships.Count(m => m.TenantId == actor.TenantId && !accounts.Any(a => a.Issuer == m.Issuer && a.Subject == m.Subject));
        await audit.WriteAsync(h, "MembershipRead", "Success");
        return Results.Ok(new MembershipPageDto(items.Take(take).ToArray(), skip, take, items.Length > take, recovery, notImported));
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db, FederationSettings settings, SecurityAudit audit)
    {
        var actor = Actor(h);
        if (!CanReadMemberships(actor)) { await audit.WriteAsync(h, "MembershipRead", "permission_denied", resourceId: id); return Problem(h, 403, "permission_denied"); }
        var item = (await Dtos(db, settings, actor.TenantId, db.AccountMemberships.AsNoTracking().Where(m => m.Id == id), h.RequestAborted)).SingleOrDefault();
        if (item == null) { await audit.WriteAsync(h, "MembershipRead", "resource_not_found", resourceId: id); return Problem(h, 404, "resource_not_found"); }
        await audit.WriteAsync(h, "MembershipRead", "Success", resourceId: id);
        h.Response.Headers.ETag = $"\"{item.Version}\"";
        return Results.Ok(item);
    }

    private static async Task<DelegationDto> DelegationDto(PoseidonDbContext db, MembershipDelegation d, DateTimeOffset now, CancellationToken ct)
    {
        var states = await db.AccountMemberships.AsNoTracking().Where(m => m.Id == d.DelegatorMembershipId || m.Id == d.DelegateMembershipId)
            .Select(m => new { m.Id, m.Status }).ToArrayAsync(ct);
        var scope = await db.DelegationVesselScopes.AsNoTracking().Where(v => v.DelegationId == d.Id).OrderBy(v => v.VesselId)
            .Select(v => new VesselGrant(v.VesselId, v.CanEdit)).ToArrayAsync(ct);
        bool Active(Guid id) => states.Any(x => x.Id == id && x.Status == "Active");
        // Stored snapshot. A LegacyUnscoped row (predates the snapshot) is never effective.
        var state = d.ReasonCode == DelegationPolicy.LegacyUnscoped && d.Status == "Active" ? "Ineffective"
            : DelegationPolicy.State(d.Status == "Revoked", d.ValidFrom, d.ValidUntil, now, Active(d.DelegatorMembershipId), Active(d.DelegateMembershipId));
        return new(d.Id, d.DelegatorMembershipId, d.DelegateMembershipId, d.Permissions.Split(','), d.ValidFrom, d.ValidUntil, d.Status,
            state, d.RevocationReason, d.Version, d.ReasonCode, d.CompanyId, scope);
    }

    private static async Task<IResult> Delegations(Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit audit, TimeProvider time)
    {
        var actor = Actor(h);
        if (!CanReadMemberships(actor)) { await audit.WriteAsync(h, "MembershipRead", "permission_denied", resourceId: id); return Problem(h, 403, "permission_denied"); }
        if (!await db.AccountMemberships.AsNoTracking().AnyAsync(m => m.Id == id, h.RequestAborted))
        { await audit.WriteAsync(h, "MembershipRead", "resource_not_found", resourceId: id); return Problem(h, 404, "resource_not_found"); }
        var rows = await db.MembershipDelegations.AsNoTracking().Where(d => d.DelegatorMembershipId == id || d.DelegateMembershipId == id)
            .OrderBy(d => d.ValidFrom).ThenBy(d => d.Id).Take(100).ToArrayAsync(h.RequestAborted);
        var now = time.GetUtcNow();
        var result = new List<DelegationDto>();
        foreach (var row in rows) result.Add(await DelegationDto(db, row, now, h.RequestAborted));
        await audit.WriteAsync(h, "MembershipRead", "Success", resourceId: id);
        return Results.Ok(result.ToArray());
    }

    // Shared command prelude: attempt audit, authority, antiforgery, bounded body and key.
    private sealed record Prelude(TrustedActor Actor, string Key, byte[] Body);
    // issuesInvitation (INVITE-RATELIMIT-FOLLOWUPS): invitation create/reissue draw on InvitationIssueRateLimiter before
    // any SecurityEvent write; only the first refused attempt of a window is audited, every refusal carries Retry-After.
    private static async Task<(Prelude? Value, IResult? Failure)> Begin(HttpContext h, Guid? resource, Func<TrustedActor, bool> authorized, int limit = 4096,
        bool issuesInvitation = false)
    {
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        async Task<(Prelude?, IResult?)> Fail(int status, string code) { await audit.WriteAsync(h, "MembershipCommand", code, resourceId: resource); return (null, Problem(h, status, code)); }
        var actor = Actor(h);
        // Invitation QA N1 (followups-20261123): a cross-site request (invalid antiforgery token) must not consume the
        // administrator's invitation budget, so only a request with a valid token draws on the limiter; an invalid one
        // falls through to the audited csrf_invalid refusal below.
        var csrfValid = true;
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { csrfValid = false; }
        if (issuesInvitation && csrfValid)
        {
            var decision = await h.RequestServices.GetRequiredService<InvitationIssueRateLimiter>().AcquireAsync(actor.TenantId, actor.Id, h.RequestAborted);
            if (!decision.Allowed)
            {
                RateLimitingRuntime.RetryAfter(h, decision);
                return decision.Outcome == RateLimitOutcome.Limited ? await Fail(429, "request_limit") : (null, Problem(h, 429, "request_limit"));
            }
        }
        await audit.WriteAsync(h, "MembershipCommand", "Attempted", resourceId: resource);
        if (!authorized(actor)) return await Fail(403, "permission_denied");
        if (!csrfValid) return await Fail(403, "csrf_invalid");
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > limit) return await Fail(400, "invalid_request");
        using var buffer = new MemoryStream();
        var bytes = new byte[1024]; int read;
        while ((read = await h.Request.Body.ReadAsync(bytes, h.RequestAborted)) > 0)
        {
            if (buffer.Length + read > limit) return await Fail(400, "invalid_request");
            buffer.Write(bytes, 0, read);
        }
        var keys = h.Request.Headers["Idempotency-Key"];
        var key = keys.Count == 1 ? keys[0] : null;
        if (key is null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(400, "invalid_idempotency_key");
        return (new Prelude(actor, key, buffer.ToArray()), null);
    }

    private static T? Parse<T>(byte[] body) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(body, Json); }
        catch (JsonException) { return null; }
    }

    private static string Fingerprint(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));

    // Receipt replay inside the command transaction: same key+intent returns the fresh view.
    private static async Task<(bool Found, bool Matches)> Receipt(PoseidonDbContext db, TrustedActor actor, string key, string action, string fingerprint, CancellationToken ct)
    {
        var receipt = await db.MembershipCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
        return receipt == null ? (false, false) : (true, receipt.Action == action && receipt.Fingerprint == fingerprint);
    }

    private static void AddReceipt(PoseidonDbContext db, TrustedActor actor, string key, string action, string fingerprint, Guid resource, DateTimeOffset now, string? result = null) =>
        db.MembershipCommandReceipts.Add(new MembershipCommandReceipt { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = action,
            Fingerprint = fingerprint, ResourceId = resource, CommittedAt = now, Result = result });

    // A COMMIT that fails (e.g. SSI 40001 raised at commit) already completed the transaction; a second
    // rollback would throw and turn a classified 409 into a 500.
    private static async Task Rollback(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try { await transaction.RollbackAsync(CancellationToken.None); }
        catch (InvalidOperationException) { /* Already completed by the failed COMMIT; nothing left to undo. */ }
    }

    private static async Task<IResult> Run(HttpContext h, Guid? resource, Func<PoseidonDbContext, Task<(IResult Result, string Outcome)>> body)
    {
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        try
        {
            // Commit-time fence for the acting administrator: the gate checked the session before the
            // transaction; a concurrent suspension of the actor must not let this command commit.
            var actor = Actor(h);
            var self = await db.AccountMemberships.AsNoTracking().Where(m => m.AccountId == actor.Id)
                .Select(m => new { m.Status, m.AccessRevision }).Take(2).ToArrayAsync(h.RequestAborted);
            var (result, outcome) = self.Length > 1 || self.Length == 1 &&
                !MembershipLifecyclePolicy.AdmitsSession(MembershipLifecyclePolicy.Parse(self[0].Status), self[0].AccessRevision, MembershipSession.SessionAccessRevision(h.User))
                ? (Problem(h, 409, "conflict_changed"), "conflict_changed")
                : await body(db);
            if (outcome == "Success")
            {
                // Business change, receipt and EF write audit commit together; no fallible write after commit.
                await transaction.CommitAsync(h.RequestAborted);
                return result;
            }
            await Rollback(transaction); db.ChangeTracker.Clear();
            await audit.WriteAsync(h, "MembershipCommand", outcome, resourceId: resource);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await Rollback(transaction); db.ChangeTracker.Clear();
            await audit.WriteAsync(h, "MembershipCommand", "version_conflict", resourceId: resource);
            return Problem(h, 409, "version_conflict");
        }
        catch (Exception ex) when (PostgresFailure.IsSerialization(ex))
        {
            await Rollback(transaction); db.ChangeTracker.Clear();
            await audit.WriteAsync(h, "MembershipCommand", "conflict_changed", resourceId: resource);
            return Problem(h, 409, "conflict_changed");
        }
        catch (DbUpdateException ex) when (PostgresFailure.Provider(ex)?.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await Rollback(transaction); db.ChangeTracker.Clear();
            await audit.WriteAsync(h, "MembershipCommand", "lineage_conflict", resourceId: resource);
            return Problem(h, 409, "lineage_conflict");
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
        {
            await Rollback(transaction);
            await audit.WriteAsync(h, "MembershipCommand", "service_unavailable", resourceId: resource);
            return Problem(h, 503, "service_unavailable");
        }
    }

    private static async Task<IResult> Import(HttpContext h)
    {
        var (prelude, failure) = await Begin(h, null, CanManageMemberships);
        if (prelude == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        if (Parse<MembershipImportCommand>(prelude.Body) == null) { await audit.WriteAsync(h, "MembershipCommand", "invalid_request"); return Problem(h, 400, "invalid_request"); }
        var actor = prelude.Actor;
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        const string action = "MembershipImport";
        var fingerprint = Fingerprint(new { action, tenant = actor.TenantId });
        return await Run(h, actor.TenantId, async db =>
        {
            var receipt = await Receipt(db, actor, prelude.Key, action, fingerprint, h.RequestAborted);
            if (receipt.Found && !receipt.Matches) return (Problem(h, 409, "idempotency_key_reused"), "idempotency_key_reused");
            if (receipt.Found)
            {
                // Replay returns the original committed result stored with the receipt.
                h.Response.Headers["Idempotency-Replayed"] = "true";
                var stored = (await db.MembershipCommandReceipts.AsNoTracking().SingleAsync(r => r.ActorId == actor.Id && r.Key == prelude.Key, h.RequestAborted)).Result;
                var original = stored == null ? null : JsonSerializer.Deserialize<MembershipImportDto>(stored, Json);
                return original == null ? (Problem(h, 409, "conflict_changed"), "conflict_changed") : (Results.Ok(original), "Success");
            }
            var accounts = await db.UserAccounts.AsNoTracking().Select(a => new { a.Id, a.Issuer, a.Subject }).ToArrayAsync(h.RequestAborted);
            var companies = await db.Companies.AsNoTracking().Select(c => c.Id).ToArrayAsync(h.RequestAborted);
            var vessels = await db.Vessels.AsNoTracking().Select(v => v.Id).ToArrayAsync(h.RequestAborted);
            int imported = 0, present = 0;
            var skips = new List<MembershipImportSkip>();
            foreach (var m in settings.Memberships.Where(m => m.TenantId == actor.TenantId).OrderBy(m => m.ActorId))
            {
                if (m.ActorId == Guid.Empty || m.Issuer is not { Length: > 0 and <= 1024 } || m.Subject is not { Length: > 0 and <= 255 })
                { skips.Add(new(m.ActorId, "invalid_configuration")); continue; }
                if (settings.Memberships.Count(x => x.Issuer == m.Issuer && x.Subject == m.Subject) != 1 || settings.Memberships.Count(x => x.ActorId == m.ActorId) != 1)
                { skips.Add(new(m.ActorId, "duplicate_configuration")); continue; }
                if (accounts.Any(a => a.Issuer == m.Issuer && a.Subject == m.Subject)) { present++; continue; }
                if (accounts.Any(a => a.Id == m.ActorId)) { skips.Add(new(m.ActorId, "actor_conflict")); continue; } // Identity conflict: never rebind.
                var membership = MembershipIdFor(actor.TenantId, m.ActorId);
                db.UserAccounts.Add(new UserAccount { TenantId = actor.TenantId, Id = m.ActorId, Issuer = m.Issuer, Subject = m.Subject,
                    Source = "ConfigurationImport", CreatedAt = now });
                db.AccountMemberships.Add(new AccountMembership { TenantId = actor.TenantId, Id = membership, AccountId = m.ActorId,
                    Status = "Active", StatusReason = "ConfigurationImport", StatusChangedAt = now, AccessRevision = 0,
                    RoleContext = ActorResolver.TrustedRoleContext(m.RoleReferences),
                    // Unknown company/vessel references narrow the snapshot; nothing is invented.
                    ActiveCompanyId = m.ActiveCompanyId is { } company && companies.Contains(company) ? company : null,
                    Source = "ConfigurationImport", CreatedAt = now });
                foreach (var grant in (m.VesselGrants ?? []).Where(g => vessels.Contains(g.VesselId)).GroupBy(g => g.VesselId))
                    db.AccountMembershipVesselGrants.Add(new AccountMembershipVesselGrant { TenantId = actor.TenantId, MembershipId = membership,
                        VesselId = grant.Key, CanEdit = grant.All(g => g.CanEdit) });
                try
                {
                    // One audited SaveChanges per entry inside the SERIALIZABLE transaction; EF wraps it in a
                    // savepoint, so a global issuer/subject or actor-id binding in another tenant (invisible
                    // through the tenant filter) skips only this entry. No cross-tenant query is issued.
                    await db.SaveChangesAsync(h.RequestAborted);
                    imported++;
                }
                catch (DbUpdateException ex) when (PostgresFailure.Provider(ex) is { SqlState: PostgresErrorCodes.UniqueViolation } unique &&
                    unique.ConstraintName is "IX_Accounts_IssuerSubject" or "AK_Accounts_Id")
                {
                    db.ChangeTracker.Clear();
                    skips.Add(new(m.ActorId, "identity_bound_other_tenant"));
                }
            }
            var result = new MembershipImportDto(imported, present, skips.Count, skips.Take(100).ToArray());
            AddReceipt(db, actor, prelude.Key, action, fingerprint, actor.TenantId, now, JsonSerializer.Serialize(result, Json));
            await db.SaveChangesAsync(h.RequestAborted);
            return (Results.Ok(result), "Success");
        });
    }

    private static async Task<IResult> Transition(HttpContext h, Guid id, MembershipTransition transition)
    {
        // Resume is admitted for either authority here; the exact authority depends on the current
        // suspension reason and is checked against the fresh row inside the transaction.
        var (prelude, failure) = await Begin(h, id, transition switch
        {
            MembershipTransition.SecuritySuspend => CanSecuritySuspend,
            MembershipTransition.Resume => a => CanManageMemberships(a) || CanSecuritySuspend(a),
            _ => CanManageMemberships
        });
        if (prelude == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var command = Parse<MembershipTransitionCommand>(prelude.Body);
        if (command == null) { await audit.WriteAsync(h, "MembershipCommand", "invalid_request", resourceId: id); return Problem(h, 400, "invalid_request"); }
        if (command.ExpectedVersion is null)
        {
            await audit.WriteAsync(h, "MembershipCommand", "validation_failed", resourceId: id);
            return Problem(h, 400, "validation_failed", new() { ["expectedVersion"] = ["required"] });
        }
        var actor = prelude.Actor;
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var action = transition switch
        {
            MembershipTransition.Suspend => "MembershipSuspend", MembershipTransition.Resume => "MembershipResume",
            MembershipTransition.Revoke => "MembershipRevoke", _ => "MembershipSecuritySuspend"
        };
        var fingerprint = Fingerprint(new { action, id, command.ExpectedVersion });
        return await Run(h, id, async db =>
        {
            // Fresh object authorization precedes receipt reads (tenant filter => cross-tenant 404).
            var target = await db.AccountMemberships.SingleOrDefaultAsync(m => m.Id == id, h.RequestAborted);
            if (target == null) return (Problem(h, 404, "resource_not_found"), "resource_not_found");
            var security = MembershipLifecyclePolicy.RequiresSecurityAuthority(transition, target.StatusReason) ||
                transition == MembershipTransition.Resume && target.StatusReason == "SecurityResume";
            if (!(security ? CanSecuritySuspend(actor) : CanManageMemberships(actor))) return (Problem(h, 403, "permission_denied"), "permission_denied");
            var receipt = await Receipt(db, actor, prelude.Key, action, fingerprint, h.RequestAborted);
            if (receipt.Found && !receipt.Matches) return (Problem(h, 409, "idempotency_key_reused"), "idempotency_key_reused");
            if (receipt.Found)
            {
                h.Response.Headers["Idempotency-Replayed"] = "true";
                var fresh = (await Dtos(db, settings, actor.TenantId, db.AccountMemberships.AsNoTracking().Where(m => m.Id == id), h.RequestAborted)).Single();
                return (Results.Ok(fresh), "Success");
            }
            if (target.Version != command.ExpectedVersion) return (Problem(h, 409, "version_conflict"), "version_conflict");
            var status = MembershipLifecyclePolicy.Parse(target.Status) ?? throw new InvalidOperationException("Membership state is outside its closed contract.");
            var admins = await ActiveAdministrators(db, settings, actor.TenantId, h.RequestAborted);
            var decision = MembershipLifecyclePolicy.Decide(status, transition, MembershipLifecyclePolicy.IsLastActiveAdministrator(target.AccountId, admins), target.StatusReason);
            if (!decision.Allowed) return (Problem(h, 409, decision.ErrorCode!), decision.ErrorCode!);
            target.Status = MembershipLifecyclePolicy.Wire(decision.Next);
            target.StatusReason = decision.ReasonCode;
            target.StatusChangedAt = now;
            target.AccessRevision += 1;
            db.Entry(target).Property(x => x.Version).OriginalValue = command.ExpectedVersion.Value;
            if (transition != MembershipTransition.Resume)
            {
                // 52.02 §6: delegations bound to a suspended/revoked membership end; resume never revives them.
                var linked = await db.MembershipDelegations.Where(d => d.Status == "Active" && (d.DelegatorMembershipId == id || d.DelegateMembershipId == id))
                    .ToArrayAsync(h.RequestAborted);
                foreach (var d in linked)
                {
                    d.Status = "Revoked"; d.RevokedAt = now;
                    d.RevocationReason = transition == MembershipTransition.Revoke ? "MembershipRevoked" : "MembershipSuspended";
                }
            }
            if (status == MembershipStatus.Invited)
                // USER-ADMIN-02: revoking an unaccepted membership closes its pending invitation in the same transaction.
                foreach (var open in await db.MembershipInvitations.Where(i => i.MembershipId == id && i.Status == "Pending").ToArrayAsync(h.RequestAborted))
                { open.Status = MembershipInvitationPolicy.Wire(InvitationStatus.Revoked); open.ClosedAt = now; }
            if (status == MembershipStatus.Invited) await CancelQueuedDeliveries(db, id, now, h.RequestAborted);
            if (decision.RecoveryRequired)
                db.TenantAccessRecoveries.Add(new TenantAccessRecovery { TenantId = actor.TenantId, Id = Guid.NewGuid(), MembershipId = id,
                    Cause = "SecuritySuspension", RaisedAt = now });
            if (decision.ReasonCode == "SecurityResume")
                // RECOVERY_REQUIRED for this membership closes with an audited closed resolution (one-time).
                foreach (var open in await db.TenantAccessRecoveries.Where(r => r.MembershipId == id && r.ResolvedAt == null).ToArrayAsync(h.RequestAborted))
                { open.ResolvedAt = now; open.Resolution = "SecuritySuspensionResumed"; }
            await db.SaveChangesAsync(h.RequestAborted);
            AddReceipt(db, actor, prelude.Key, action, fingerprint, id, now);
            await db.SaveChangesAsync(h.RequestAborted);
            var dto = (await Dtos(db, settings, actor.TenantId, db.AccountMemberships.AsNoTracking().Where(m => m.Id == id), h.RequestAborted)).Single();
            return (Results.Ok(dto), "Success");
        });
    }

    private static async Task<IResult> CreateDelegation(HttpContext h, Guid delegator)
    {
        var (prelude, failure) = await Begin(h, delegator, CanManageMemberships);
        if (prelude == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var command = Parse<DelegationCreateCommand>(prelude.Body);
        if (command == null) { await audit.WriteAsync(h, "MembershipCommand", "invalid_request", resourceId: delegator); return Problem(h, 400, "invalid_request"); }
        var actor = prelude.Actor;
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        const string action = "DelegationCreate";
        var permissions = command.Permissions ?? [];
        var vesselScope = (command.VesselScope ?? []).Select(v => v == null ? new DelegatedVessel(Guid.Empty, false) : new DelegatedVessel(v.VesselId, v.CanEdit)).ToArray();
        var fingerprint = Fingerprint(new { action, delegator, command.DelegateMembershipId, permissions = DelegationPolicy.Canonical(permissions), command.ValidFrom, command.ValidUntil,
            command.ReasonCode, command.CompanyId, scope = string.Join(',', vesselScope.Select(v => v.VesselId.ToString("D") + (v.CanEdit ? ":e" : ":r")).Order(StringComparer.Ordinal)) });
        return await Run(h, delegator, async db =>
        {
            var pair = await db.AccountMemberships.Where(m => m.Id == delegator || m.Id == command.DelegateMembershipId)
                .Join(db.UserAccounts, m => m.AccountId, a => a.Id, (m, a) => new { m, a.Issuer, a.Subject }).ToArrayAsync(h.RequestAborted);
            var source = pair.SingleOrDefault(x => x.m.Id == delegator);
            if (source == null) return (Problem(h, 404, "resource_not_found"), "resource_not_found");
            var receipt = await Receipt(db, actor, prelude.Key, action, fingerprint, h.RequestAborted);
            if (receipt.Found && !receipt.Matches) return (Problem(h, 409, "idempotency_key_reused"), "idempotency_key_reused");
            if (receipt.Found)
            {
                h.Response.Headers["Idempotency-Replayed"] = "true";
                var replayId = (await db.MembershipCommandReceipts.AsNoTracking().SingleAsync(r => r.ActorId == actor.Id && r.Key == prelude.Key, h.RequestAborted)).ResourceId;
                var existing = await db.MembershipDelegations.AsNoTracking().SingleAsync(d => d.Id == replayId, h.RequestAborted);
                return (Results.Ok(await DelegationDto(db, existing, now, h.RequestAborted)), "Success");
            }
            var target = command.DelegateMembershipId is { } delegateId ? pair.SingleOrDefault(x => x.m.Id == delegateId) : null;
            if (command.DelegateMembershipId is { } requested && requested != Guid.Empty && requested != delegator && target == null)
                return (Problem(h, 404, "resource_not_found"), "resource_not_found");
            // Maker-checker: the approving administrator is neither source nor delegate.
            if (source.m.AccountId == actor.Id || target?.m.AccountId == actor.Id) return (Problem(h, 403, "separation_of_duties"), "separation_of_duties");
            // Source's CURRENT authority: operator grant narrowed by its persisted membership and grant snapshot.
            var sourceGrants = await db.AccountMembershipVesselGrants.AsNoTracking().Where(g => g.MembershipId == delegator).ToArrayAsync(h.RequestAborted);
            var sourceProvisioned = source.m.Source == ProvisionedGrants.Source
                ? (await ProvisionedGrants.LoadAsync(db, [delegator], h.RequestAborted)).GetValueOrDefault(delegator) : null;
            var current = DelegationAuthority.Current(settings, actor.TenantId, source.m.AccountId, source.Issuer, source.Subject, source.m.Status,
                source.m.ActiveCompanyId, sourceGrants, sourceProvisioned) ?? new DelegationSource([], null, []);
            var errors = DelegationPolicy.Validate(new(delegator, command.DelegateMembershipId ?? Guid.Empty, command.Permissions, command.ValidFrom, command.ValidUntil,
                command.ReasonCode, command.CompanyId, vesselScope), current, now);
            if (target != null && target.m.Status != "Active") errors["delegateMembershipId"] = ["membership_inactive"];
            if (source.m.Status != "Active") errors["delegatorMembershipId"] = ["membership_inactive"];
            if (errors.Count > 0) return (Problem(h, 400, "validation_failed", errors), "validation_failed");
            var delegation = new MembershipDelegation { TenantId = actor.TenantId, Id = Guid.NewGuid(), DelegatorMembershipId = delegator,
                DelegateMembershipId = target!.m.Id, Permissions = DelegationPolicy.Canonical(permissions), ValidFrom = command.ValidFrom!.Value,
                ValidUntil = command.ValidUntil!.Value, Status = "Active", ApprovedBy = actor.Id.ToString(), CreatedAt = now,
                ReasonCode = command.ReasonCode!, CompanyId = command.CompanyId };
            db.MembershipDelegations.Add(delegation);
            foreach (var vessel in vesselScope)
                db.DelegationVesselScopes.Add(new DelegationVesselScope { TenantId = actor.TenantId, DelegationId = delegation.Id, VesselId = vessel.VesselId, CanEdit = vessel.CanEdit });
            await db.SaveChangesAsync(h.RequestAborted);
            AddReceipt(db, actor, prelude.Key, action, fingerprint, delegation.Id, now);
            await db.SaveChangesAsync(h.RequestAborted);
            return (Results.Ok(await DelegationDto(db, delegation, now, h.RequestAborted)), "Success");
        });
    }

    private static async Task<IResult> RevokeDelegation(HttpContext h, Guid id)
    {
        var (prelude, failure) = await Begin(h, id, CanManageMemberships);
        if (prelude == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var command = Parse<MembershipTransitionCommand>(prelude.Body);
        if (command == null) { await audit.WriteAsync(h, "MembershipCommand", "invalid_request", resourceId: id); return Problem(h, 400, "invalid_request"); }
        if (command.ExpectedVersion is null)
        {
            await audit.WriteAsync(h, "MembershipCommand", "validation_failed", resourceId: id);
            return Problem(h, 400, "validation_failed", new() { ["expectedVersion"] = ["required"] });
        }
        var actor = prelude.Actor;
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        const string action = "DelegationRevoke";
        var fingerprint = Fingerprint(new { action, id, command.ExpectedVersion });
        return await Run(h, id, async db =>
        {
            var delegation = await db.MembershipDelegations.SingleOrDefaultAsync(d => d.Id == id, h.RequestAborted);
            if (delegation == null) return (Problem(h, 404, "resource_not_found"), "resource_not_found");
            var receipt = await Receipt(db, actor, prelude.Key, action, fingerprint, h.RequestAborted);
            if (receipt.Found && !receipt.Matches) return (Problem(h, 409, "idempotency_key_reused"), "idempotency_key_reused");
            if (receipt.Found) { h.Response.Headers["Idempotency-Replayed"] = "true"; return (Results.Ok(await DelegationDto(db, delegation, now, h.RequestAborted)), "Success"); }
            if (delegation.Version != command.ExpectedVersion) return (Problem(h, 409, "version_conflict"), "version_conflict");
            if (delegation.Status != "Active") return (Problem(h, 409, "invalid_transition"), "invalid_transition");
            delegation.Status = "Revoked"; delegation.RevokedAt = now; delegation.RevocationReason = "AdministrativeRevocation";
            db.Entry(delegation).Property(x => x.Version).OriginalValue = command.ExpectedVersion.Value;
            await db.SaveChangesAsync(h.RequestAborted);
            AddReceipt(db, actor, prelude.Key, action, fingerprint, id, now);
            await db.SaveChangesAsync(h.RequestAborted);
            return (Results.Ok(await DelegationDto(db, delegation, now, h.RequestAborted)), "Success");
        });
    }
}
