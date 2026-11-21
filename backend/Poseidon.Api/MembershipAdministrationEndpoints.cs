using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// USER-ADMIN-01 "Kullanıcılar ve yetkiler" (13.03 §9 paragraph:102-106). Runtime membership administration for the
// caller's tenant. The account itself stays in the IdP (issuer+subject only, no password, e-mail or name is stored).
public sealed record RoleBundleDto(string Code, Guid RoleId, string Locator, string[] Permissions, bool Grantable);
public sealed record PermissionOptionDto(string Code, string Module, bool DecisionAuthority, bool Delegable, bool Grantable);
public sealed record VesselOptionDto(Guid Id, string Code, string Name, string Kind);
public sealed record CompanyOptionDto(Guid Id, string Code, string Name);
public sealed record MembershipCatalogDto(RoleBundleDto[] Bundles, PermissionOptionDto[] Permissions, bool CanManage, bool CanSecuritySuspend,
    bool GrantorTenantWideRead, bool GrantorTenantWideManage, VesselOptionDto[] Vessels, CompanyOptionDto[] Companies, string[] DelegationReasons,
    string? TrustedIssuer, Guid ActorId);
// ManagedBy: Configuration (operator appsettings grant, break-glass/bootstrap source), Database (admin-provisioned grant) or None
// (persisted lifecycle row without any grant source: grants nothing).
public sealed record MembershipAccessSummaryDto(Guid Id, Guid AccountId, string Status, string StatusReason, string Source, string ManagedBy,
    string[] Bundles, int PermissionCount, bool TenantWideRead, bool TenantWideManage, int VesselGrantCount, bool Administrator, bool Self,
    DateTimeOffset StatusChangedAt, uint Version, long AccessRevision, int? GrantRevision);
public sealed record MembershipAccessPageDto(MembershipAccessSummaryDto[] Items, int Offset, int Limit, bool HasMore, int Total);
public sealed record GrantRevisionDto(int Revision, string[] Bundles, string[] IndividualPermissions, int PermissionCount, bool TenantWideRead,
    bool TenantWideManage, Guid? ActiveCompanyId, int VesselGrantCount, Guid GrantedBy, DateTimeOffset CreatedAt);
// EditBlockedReason: self_modification | configuration_managed | revoked | not_manager | null.
public sealed record MembershipAccessDto(MembershipAccessSummaryDto Membership, string[] IndividualPermissions, string[] Permissions,
    Guid? ActiveCompanyId, VesselGrant[] VesselGrants, bool Editable, string? EditBlockedReason, GrantRevisionDto[] History);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MembershipVesselGrantCommand(Guid VesselId, bool CanEdit = false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MembershipCreateCommand(string? Issuer, string? Subject, string[]? Bundles, string[]? Permissions, bool? TenantWideRead,
    bool? TenantWideManage, Guid? ActiveCompanyId, MembershipVesselGrantCommand[]? VesselGrants);
// ExpectedVersion is the current grant revision (optimistic concurrency).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MembershipGrantCommand(long? ExpectedVersion, string[]? Bundles, string[]? Permissions, bool? TenantWideRead,
    bool? TenantWideManage, Guid? ActiveCompanyId, MembershipVesselGrantCommand[]? VesselGrants);

public static partial class MembershipEndpoints
{
    private const int GrantBodyLimit = 16384;
    private static readonly HashSet<string> Known = new(ActorResolver.KnownPermissions, StringComparer.Ordinal);

    private static void MapMembershipAdministration(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/memberships/catalog", Catalog).RequireAuthorization("DirectoryRead").WithName("GetMembershipCatalog")
            .WithSummary("Role bundles, the trusted permission allow-list grouped by module, tenant vessels/companies and what the caller may grant. Requires membership.read or membership.manage with tenant-wide scope.")
            .Produces<MembershipCatalogDto>().Produces(401).ProducesProblem(403).Produces(503);
        app.MapGet("/api/v1/memberships/access", AccessList).RequireAuthorization("DirectoryRead").WithName("ListMembershipAccess")
            .WithSummary("Tenant memberships with their grant source, bundles and scope. Filters: status, bundle, q (membership/account id prefix or exact IdP subject; subjects are never returned).")
            .Produces<MembershipAccessPageDto>().ProducesProblem(400).Produces(401).ProducesProblem(403).Produces(503);
        app.MapGet("/api/v1/memberships/{id:guid}/access", AccessDetail).RequireAuthorization("DirectoryRead").WithName("GetMembershipAccess")
            .WithSummary("Effective grant, scope and immutable grant revision history of one membership.")
            .Produces<MembershipAccessDto>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);
        Command(app.MapPost("/api/v1/memberships", (HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
            CreateMembership(h)), "CreateMembership", "Provisions a database membership for an IdP identity (issuer+subject; the account and its credentials stay in the IdP). Role bundles and individual permissions are limited to the trusted allow-list and to the caller's own permissions (400 exceeds_grantor_authority). An identity already provisioned or present in the operator configuration returns 409 identity_unavailable. Takes effect at the member's next login.")
            .Accepts<MembershipCreateCommand>("application/json").Produces<MembershipAccessDto>();
        Command(app.MapPost("/api/v1/memberships/{id:guid}/grant", (Guid id, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
            UpdateGrant(h, id)), "UpdateMembershipGrant", "Appends a new immutable grant revision to an admin-provisioned membership. expectedVersion is the current grant revision (stale ⇒ 409 version_conflict). Own membership ⇒ 403 self_modification; configuration-managed ⇒ 409 configuration_managed; removing the last active administrator ⇒ 409 last_admin. Takes effect on the member's next request.")
            .Accepts<MembershipGrantCommand>("application/json").Produces<MembershipAccessDto>();
    }

    private sealed record AccessRow(AccountMembership Membership, string Issuer, string Subject, ProvisionedGrant? Grant, Membership? Config);

    private static async Task<AccessRow[]> AccessRows(PoseidonDbContext db, FederationSettings settings, Guid tenant, IQueryable<AccountMembership> query, CancellationToken ct)
    {
        var rows = await query.Join(db.UserAccounts.AsNoTracking(), m => m.AccountId, a => a.Id, (m, a) => new { m, a.Issuer, a.Subject })
            .Take(2000).ToArrayAsync(ct);
        var grants = await ProvisionedGrants.LoadAsync(db, rows.Where(r => r.m.Source == ProvisionedGrants.Source).Select(r => r.m.Id).ToArray(), ct);
        return rows.Select(r =>
        {
            var config = settings.Memberships.Where(c => c.Issuer == r.Issuer && c.Subject == r.Subject).ToArray();
            return new AccessRow(r.m, r.Issuer, r.Subject, grants.GetValueOrDefault(r.m.Id),
                config.Length == 1 && config[0].TenantId == tenant && config[0].ActorId == r.m.AccountId ? config[0] : null);
        }).ToArray();
    }

    private static MembershipAccessSummaryDto Summary(AccessRow r, HashSet<Guid> admins, Guid self)
    {
        var m = r.Membership;
        if (r.Config is { } c)
            return new(m.Id, m.AccountId, m.Status, m.StatusReason, m.Source, "Configuration", [], ActorResolver.TrustedPermissions(c.Permissions).Length,
                c.TenantWideRead, c.TenantWideManage, (c.VesselGrants ?? []).Count(g => g.VesselId != Guid.Empty), admins.Contains(m.AccountId),
                m.AccountId == self, m.StatusChangedAt, m.Version, m.AccessRevision, null);
        if (r.Grant is { } g)
            return new(m.Id, m.AccountId, m.Status, m.StatusReason, m.Source, "Database", g.Bundles, ActorResolver.TrustedPermissions(g.Permissions).Length,
                g.TenantWideRead, g.TenantWideManage, g.VesselGrants.Length, admins.Contains(m.AccountId), m.AccountId == self,
                m.StatusChangedAt, m.Version, m.AccessRevision, g.Revision);
        return new(m.Id, m.AccountId, m.Status, m.StatusReason, m.Source, "None", [], 0, false, false, 0, false, m.AccountId == self,
            m.StatusChangedAt, m.Version, m.AccessRevision, null);
    }

    private static async Task<MembershipAccessDto?> AccessView(PoseidonDbContext db, FederationSettings settings, TrustedActor actor, Guid id, CancellationToken ct)
    {
        var row = (await AccessRows(db, settings, actor.TenantId, db.AccountMemberships.AsNoTracking().Where(m => m.Id == id), ct)).SingleOrDefault();
        if (row == null) return null;
        var admins = await ActiveAdministrators(db, settings, actor.TenantId, ct);
        var summary = Summary(row, admins, actor.Id);
        var history = await db.MembershipGrantRevisions.AsNoTracking().Where(g => g.MembershipId == id).OrderByDescending(g => g.Revision).Take(100).ToArrayAsync(ct);
        var counts = await db.MembershipGrantVessels.AsNoTracking().Where(v => v.MembershipId == id).GroupBy(v => v.Revision)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var revisions = history.Select(g => new GrantRevisionDto(g.Revision, MembershipGrantPolicy.Split(g.Bundles), MembershipGrantPolicy.Split(g.IndividualPermissions),
            MembershipGrantPolicy.Split(g.Permissions).Length, g.TenantWideRead, g.TenantWideManage, g.ActiveCompanyId, counts.GetValueOrDefault(g.Revision),
            Guid.TryParse(g.GrantedBy, out var by) ? by : Guid.Empty, g.CreatedAt)).ToArray();
        string? blocked = !CanManageMemberships(actor) ? "not_manager" : MembershipGrantPolicy.IsSelf(actor.Id, row.Membership.AccountId) ? MembershipGrantPolicy.SelfModification
            : row.Config != null || row.Grant == null ? MembershipGrantPolicy.ConfigurationManaged : row.Membership.Status == "Revoked" ? "revoked" : null;
        string[] individual = [], permissions = [];
        Guid? company = null; VesselGrant[] vessels = [];
        if (row.Config is { } c)
        {
            permissions = [.. ActorResolver.TrustedPermissions(c.Permissions).Order(StringComparer.Ordinal)];
            company = c.ActiveCompanyId is { } configured && configured != Guid.Empty ? configured : null;
            vessels = [.. (c.VesselGrants ?? []).Where(v => v.VesselId != Guid.Empty).OrderBy(v => v.VesselId)];
        }
        else if (row.Grant is { } g)
        {
            individual = g.IndividualPermissions; permissions = [.. ActorResolver.TrustedPermissions(g.Permissions)];
            company = g.ActiveCompanyId; vessels = g.VesselGrants;
        }
        return new(summary, individual, permissions, company, vessels, blocked == null, blocked, revisions);
    }

    private static async Task<IResult> Catalog(HttpContext h, PoseidonDbContext db, FederationSettings settings, SecurityAudit audit)
    {
        var actor = Actor(h);
        if (!CanReadMemberships(actor)) { await audit.WriteAsync(h, "MembershipRead", "permission_denied"); return Problem(h, 403, "permission_denied"); }
        var own = actor.Permissions.ToHashSet(StringComparer.Ordinal);
        var bundles = MembershipRoleBundles.All.Select(b => new RoleBundleDto(b.Code, b.RoleId, b.Locator, b.Permissions, b.Permissions.All(own.Contains))).ToArray();
        var permissions = ActorResolver.KnownPermissions.Order(StringComparer.Ordinal).Select(p => new PermissionOptionDto(p, MembershipRoleBundles.Module(p),
            MembershipRoleBundles.IsDecisionAuthority(p), !DelegationPolicy.NonDelegable(p), own.Contains(p))).ToArray();
        var vessels = await db.Vessels.AsNoTracking().OrderBy(v => v.Code).Take(500)
            .Select(v => new VesselOptionDto(v.Id, v.Code, v.Name, v.Kind == VesselKind.Tug ? "Tug" : "Ship")).ToArrayAsync(h.RequestAborted);
        var companies = await db.Companies.AsNoTracking().OrderBy(c => c.Code).Take(500)
            .Select(c => new CompanyOptionDto(c.Id, c.Code, c.Name)).ToArrayAsync(h.RequestAborted);
        await audit.WriteAsync(h, "MembershipRead", "Success");
        return Results.Ok(new MembershipCatalogDto(bundles, permissions, CanManageMemberships(actor), CanSecuritySuspend(actor), actor.TenantWideRead,
            actor.TenantWideManage, vessels, companies, DelegationPolicy.ReasonCodes, string.IsNullOrWhiteSpace(settings.Issuer) ? null : settings.Issuer, actor.Id));
    }

    private static bool Matches(AccessRow r, string q) =>
        r.Membership.Id.ToString("D").StartsWith(q, StringComparison.OrdinalIgnoreCase) ||
        r.Membership.AccountId.ToString("D").StartsWith(q, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(r.Subject, q, StringComparison.Ordinal);

    private static async Task<IResult> AccessList(HttpContext h, PoseidonDbContext db, FederationSettings settings, SecurityAudit audit,
        string? offset, string? limit, string? status, string? bundle, string? q)
    {
        var actor = Actor(h);
        if (!CanReadMemberships(actor)) { await audit.WriteAsync(h, "MembershipRead", "permission_denied"); return Problem(h, 403, "permission_denied"); }
        var skip = 0; var take = 25;
        var invalid = offset != null && (!int.TryParse(offset, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out skip) || skip > 100000) ||
            limit != null && (!int.TryParse(limit, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out take) || take is < 1 or > 100) ||
            status != null && MembershipLifecyclePolicy.Parse(status) == null || bundle != null && MembershipRoleBundles.Find(bundle) == null ||
            q != null && (q.Trim().Length is 0 or > 255 || q.Any(char.IsControl));
        if (invalid) { await audit.WriteAsync(h, "MembershipRead", "invalid_query"); return Problem(h, 400, "invalid_query"); }
        var rows = await AccessRows(db, settings, actor.TenantId, db.AccountMemberships.AsNoTracking().Where(m => status == null || m.Status == status), h.RequestAborted);
        var admins = await ActiveAdministrators(db, settings, actor.TenantId, h.RequestAborted);
        var needle = q?.Trim();
        var filtered = rows.Where(r => (bundle == null || r.Grant?.Bundles.Contains(bundle) == true) && (needle == null || Matches(r, needle)))
            .OrderBy(r => r.Membership.AccountId).ThenBy(r => r.Membership.Id).ToArray();
        var page = filtered.Skip(skip).Take(take).Select(r => Summary(r, admins, actor.Id)).ToArray();
        await audit.WriteAsync(h, "MembershipRead", "Success");
        return Results.Ok(new MembershipAccessPageDto(page, skip, take, filtered.Length > skip + take, filtered.Length));
    }

    private static async Task<IResult> AccessDetail(Guid id, HttpContext h, PoseidonDbContext db, FederationSettings settings, SecurityAudit audit)
    {
        var actor = Actor(h);
        if (!CanReadMemberships(actor)) { await audit.WriteAsync(h, "MembershipRead", "permission_denied", resourceId: id); return Problem(h, 403, "permission_denied"); }
        var view = await AccessView(db, settings, actor, id, h.RequestAborted);
        if (view == null) { await audit.WriteAsync(h, "MembershipRead", "resource_not_found", resourceId: id); return Problem(h, 404, "resource_not_found"); }
        await audit.WriteAsync(h, "MembershipRead", "Success", resourceId: id);
        return Results.Ok(view);
    }

    // Validates the grant against the caller's authority and the tenant's real vessels/companies (tenant filter ⇒ a
    // foreign vessel or company id is unknown here).
    private static async Task<(GrantOutcome Outcome, Dictionary<string, string[]> Errors)> CheckGrant(PoseidonDbContext db, TrustedActor actor,
        string[]? bundles, string[]? permissions, bool tenantWideRead, bool tenantWideManage, Guid? company, MembershipVesselGrantCommand[]? vessels, CancellationToken ct)
    {
        var requested = (vessels ?? []).Select(v => v == null ? new VesselAccess(Guid.Empty, false) : new VesselAccess(v.VesselId, v.CanEdit)).ToArray();
        var outcome = MembershipGrantPolicy.Validate(new GrantRequest(bundles, permissions, tenantWideRead, tenantWideManage, requested),
            new Grantor(actor.Id, actor.Permissions, actor.TenantWideRead, actor.TenantWideManage), Known);
        var errors = new Dictionary<string, string[]>(outcome.Errors, StringComparer.Ordinal);
        if (outcome.Vessels.Length > 0)
        {
            var ids = outcome.Vessels.Select(v => v.VesselId).ToArray();
            var found = await db.Vessels.AsNoTracking().Where(v => ids.Contains(v.Id)).CountAsync(ct);
            if (found != ids.Length) errors["vesselGrants"] = ["unknown_vessel"];
        }
        if (company == Guid.Empty) errors["activeCompanyId"] = ["invalid"];
        else if (company is { } c && !await db.Companies.AsNoTracking().AnyAsync(x => x.Id == c, ct)) errors["activeCompanyId"] = ["unknown_company"];
        return (outcome, errors);
    }

    private static void AddRevision(PoseidonDbContext db, TrustedActor actor, Guid membership, int revision, GrantOutcome outcome, bool tenantWideRead,
        bool tenantWideManage, Guid? company, DateTimeOffset now)
    {
        db.MembershipGrantRevisions.Add(new MembershipGrantRevision { TenantId = actor.TenantId, MembershipId = membership, Revision = revision,
            Bundles = MembershipGrantPolicy.Canonical(outcome.Bundles), IndividualPermissions = MembershipGrantPolicy.Canonical(outcome.IndividualPermissions),
            Permissions = MembershipGrantPolicy.Canonical(outcome.EffectivePermissions), TenantWideRead = tenantWideRead, TenantWideManage = tenantWideManage,
            ActiveCompanyId = company, GrantedBy = actor.Id.ToString("D"), CreatedAt = now });
        foreach (var v in outcome.Vessels)
            db.MembershipGrantVessels.Add(new MembershipGrantVessel { TenantId = actor.TenantId, MembershipId = membership, Revision = revision, VesselId = v.VesselId, CanEdit = v.CanEdit });
    }

    private static string Scope(IEnumerable<MembershipVesselGrantCommand>? vessels) =>
        string.Join(',', (vessels ?? []).Select(v => v == null ? "null" : v.VesselId.ToString("D") + (v.CanEdit ? ":e" : ":r")).Order(StringComparer.Ordinal));

    internal static bool ValidIssuer(string? issuer, FederationSettings settings) =>
        issuer is { Length: > 0 and <= 1024 } && !issuer.Any(char.IsControl) && Uri.TryCreate(issuer, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        (string.IsNullOrWhiteSpace(settings.Issuer) || string.Equals(settings.Issuer, issuer, StringComparison.Ordinal));

    internal static bool ValidSubject(string? subject) => subject is { Length: > 0 and <= 255 } && subject.Trim() == subject && !subject.Any(char.IsControl);

    private static async Task<IResult> CreateMembership(HttpContext h)
    {
        var (prelude, failure) = await Begin(h, null, CanManageMemberships, GrantBodyLimit);
        if (prelude == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var command = Parse<MembershipCreateCommand>(prelude.Body);
        if (command == null) { await audit.WriteAsync(h, "MembershipCommand", "invalid_request"); return Problem(h, 400, "invalid_request"); }
        var actor = prelude.Actor;
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        const string action = "MembershipCreate";
        bool read = command.TenantWideRead ?? false, manage = command.TenantWideManage ?? false;
        var fingerprint = Fingerprint(new { action, command.Issuer, command.Subject, bundles = MembershipGrantPolicy.Canonical(command.Bundles ?? []),
            permissions = MembershipGrantPolicy.Canonical(command.Permissions ?? []), read, manage, command.ActiveCompanyId, scope = Scope(command.VesselGrants) });
        return await Run(h, null, async db =>
        {
            var receipt = await Receipt(db, actor, prelude.Key, action, fingerprint, h.RequestAborted);
            if (receipt.Found && !receipt.Matches) return (Problem(h, 409, "idempotency_key_reused"), "idempotency_key_reused");
            if (receipt.Found)
            {
                h.Response.Headers["Idempotency-Replayed"] = "true";
                var replayId = (await db.MembershipCommandReceipts.AsNoTracking().SingleAsync(r => r.ActorId == actor.Id && r.Key == prelude.Key, h.RequestAborted)).ResourceId;
                var replay = await AccessView(db, settings, actor, replayId, h.RequestAborted);
                return replay == null ? (Problem(h, 409, "conflict_changed"), "conflict_changed") : (Results.Ok(replay), "Success");
            }
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (!ValidIssuer(command.Issuer, settings)) errors["issuer"] = [command.Issuer is { Length: > 0 } ? "issuer_not_trusted" : "required"];
            if (!ValidSubject(command.Subject)) errors["subject"] = [command.Subject is { Length: > 0 } ? "invalid" : "required"];
            var (outcome, grantErrors) = await CheckGrant(db, actor, command.Bundles, command.Permissions, read, manage, command.ActiveCompanyId, command.VesselGrants, h.RequestAborted);
            foreach (var (k, v) in grantErrors) errors[k] = v;
            if (errors.Count > 0) return (Problem(h, 400, "validation_failed", errors), "validation_failed");
            // The operator configuration stays the bootstrap/break-glass source for its identities; a provisioned
            // duplicate (same tenant or bound elsewhere) is reported with one code that reveals no other tenant.
            if (settings.Memberships.Any(m => m.Issuer == command.Issuer && m.Subject == command.Subject) ||
                await db.UserAccounts.AsNoTracking().AnyAsync(a => a.Issuer == command.Issuer && a.Subject == command.Subject, h.RequestAborted))
                return (Problem(h, 409, "identity_unavailable"), "identity_unavailable");
            var account = Guid.NewGuid();
            var membership = MembershipIdFor(actor.TenantId, account);
            db.UserAccounts.Add(new UserAccount { TenantId = actor.TenantId, Id = account, Issuer = command.Issuer!, Subject = command.Subject!,
                Source = ProvisionedGrants.Source, CreatedAt = now });
            db.AccountMemberships.Add(new AccountMembership { TenantId = actor.TenantId, Id = membership, AccountId = account, Status = "Active",
                StatusReason = "AdminProvisioning", StatusChangedAt = now, AccessRevision = 0, Source = ProvisionedGrants.Source, CreatedAt = now });
            AddRevision(db, actor, membership, 1, outcome, read, manage, command.ActiveCompanyId, now);
            try { await db.SaveChangesAsync(h.RequestAborted); }
            catch (DbUpdateException ex) when (PostgresFailure.Provider(ex) is { SqlState: PostgresErrorCodes.UniqueViolation } unique &&
                unique.ConstraintName is "IX_Accounts_IssuerSubject")
            {
                // Identity bound in another tenant (invisible through the tenant filter): no cross-tenant query, no detail.
                db.ChangeTracker.Clear();
                return (Problem(h, 409, "identity_unavailable"), "identity_unavailable");
            }
            AddReceipt(db, actor, prelude.Key, action, fingerprint, membership, now);
            await db.SaveChangesAsync(h.RequestAborted);
            return (Results.Ok(await AccessView(db, settings, actor, membership, h.RequestAborted)), "Success");
        });
    }

    private static async Task<IResult> UpdateGrant(HttpContext h, Guid id)
    {
        var (prelude, failure) = await Begin(h, id, CanManageMemberships, GrantBodyLimit);
        if (prelude == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var command = Parse<MembershipGrantCommand>(prelude.Body);
        if (command == null) { await audit.WriteAsync(h, "MembershipCommand", "invalid_request", resourceId: id); return Problem(h, 400, "invalid_request"); }
        if (command.ExpectedVersion is null)
        {
            await audit.WriteAsync(h, "MembershipCommand", "validation_failed", resourceId: id);
            return Problem(h, 400, "validation_failed", new() { ["expectedVersion"] = ["required"] });
        }
        var actor = prelude.Actor;
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        const string action = "MembershipGrantUpdate";
        bool read = command.TenantWideRead ?? false, manage = command.TenantWideManage ?? false;
        var fingerprint = Fingerprint(new { action, id, command.ExpectedVersion, bundles = MembershipGrantPolicy.Canonical(command.Bundles ?? []),
            permissions = MembershipGrantPolicy.Canonical(command.Permissions ?? []), read, manage, command.ActiveCompanyId, scope = Scope(command.VesselGrants) });
        return await Run(h, id, async db =>
        {
            // Fresh object authorization precedes receipt reads (tenant filter ⇒ cross-tenant 404).
            var row = (await AccessRows(db, settings, actor.TenantId, db.AccountMemberships.AsNoTracking().Where(m => m.Id == id), h.RequestAborted)).SingleOrDefault();
            if (row == null) return (Problem(h, 404, "resource_not_found"), "resource_not_found");
            if (MembershipGrantPolicy.IsSelf(actor.Id, row.Membership.AccountId))
                return (Problem(h, 403, MembershipGrantPolicy.SelfModification), MembershipGrantPolicy.SelfModification);
            var receipt = await Receipt(db, actor, prelude.Key, action, fingerprint, h.RequestAborted);
            if (receipt.Found && !receipt.Matches) return (Problem(h, 409, "idempotency_key_reused"), "idempotency_key_reused");
            if (receipt.Found)
            {
                h.Response.Headers["Idempotency-Replayed"] = "true";
                return (Results.Ok(await AccessView(db, settings, actor, id, h.RequestAborted)), "Success");
            }
            if (row.Config != null || row.Grant == null || row.Membership.Source != ProvisionedGrants.Source)
                return (Problem(h, 409, MembershipGrantPolicy.ConfigurationManaged), MembershipGrantPolicy.ConfigurationManaged);
            if (row.Membership.Status == "Revoked") return (Problem(h, 409, "invalid_transition"), "invalid_transition");
            if (row.Grant.Revision != command.ExpectedVersion) return (Problem(h, 409, "version_conflict"), "version_conflict");
            var (outcome, errors) = await CheckGrant(db, actor, command.Bundles, command.Permissions, read, manage, command.ActiveCompanyId, command.VesselGrants, h.RequestAborted);
            if (errors.Count > 0) return (Problem(h, 400, "validation_failed", errors), "validation_failed");
            // Last-admin guard (P5202-D04 predicate) counted inside the same SERIALIZABLE transaction.
            var admins = await ActiveAdministrators(db, settings, actor.TenantId, h.RequestAborted);
            if (!outcome.Administrator && MembershipLifecyclePolicy.IsLastActiveAdministrator(row.Membership.AccountId, admins))
                return (Problem(h, 409, MembershipLifecyclePolicy.LastAdmin), MembershipLifecyclePolicy.LastAdmin);
            // A concurrent append of the same revision violates the primary key ⇒ 409 lineage_conflict (Run).
            AddRevision(db, actor, id, row.Grant.Revision + 1, outcome, read, manage, command.ActiveCompanyId, now);
            await db.SaveChangesAsync(h.RequestAborted);
            AddReceipt(db, actor, prelude.Key, action, fingerprint, id, now);
            await db.SaveChangesAsync(h.RequestAborted);
            return (Results.Ok(await AccessView(db, settings, actor, id, h.RequestAborted)), "Success");
        });
    }
}
