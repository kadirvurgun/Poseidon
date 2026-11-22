using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// USER-ADMIN-02: invitation + acceptance by the identity holder (closes USER-ADMIN-01 N1, cross-tenant squatting).
// The token is returned once, to the creating administrator, and handed out of band; it is never stored or logged.
public sealed record MembershipInvitationIssueDto(MembershipAccessDto Membership, string? InvitationToken, DateTimeOffset? ExpiresAt);
// What the invitation grants in its tenant, shown to the signed-in identity holder before and after acceptance.
public sealed record InvitationGrantDto(string? TenantName, string[] Bundles, int PermissionCount, bool TenantWideRead, bool TenantWideManage,
    int VesselGrantCount, bool CompanyScoped, DateTimeOffset ExpiresAt);
public sealed record InvitationAcceptedDto(Guid MembershipId, InvitationGrantDto Grant, bool SignInRequired);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InvitationTokenCommand(string? Token);
// USER-ADMIN-03: reissue with an optional e-mail recipient for the new link. expectedVersion = membership version.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InvitationReissueCommand(uint? ExpectedVersion, string? DeliveryEmail = null);
// USER-ADMIN-03: 54.01 account.invite_hours for the caller's tenant. Version 0 = no tenant value yet (source default).
public sealed record InvitationSettingsDto(int LifetimeHours, int MinHours, int MaxHours, int DefaultHours, bool TenantValue, uint Version,
    bool DeliveryConfigured, bool CanManage);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InvitationSettingsCommand(uint? ExpectedVersion, int? LifetimeHours);

public enum InvitationAttempt { Allowed, Limited, LimitedRepeat }

// Technical abuse guard per validated identity (hash of issuer+subject), not an approved quota: preview and accept
// attempts share one fixed 15-minute window. Process-local; a multi-instance deployment needs a shared limiter.
// QA N4: bounded and time-bucketed. At capacity only entries of past windows are evicted; counters of the current window
// are never reset, and while the table is full of current-window identities a NEW identity is refused (fail closed) until
// the window rolls over. Limited = the first refused attempt of a window (audited once); LimitedRepeat = later ones.
public sealed class InvitationAttemptLimiter(TimeProvider clock, int capacity = InvitationAttemptLimiter.DefaultCapacity)
{
    public const int AttemptsPerWindow = 10;
    public const int DefaultCapacity = 10_000;
    private const long WindowSeconds = 15 * 60;
    private readonly Dictionary<string, (long Window, int Count)> windows = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    public int Tracked { get { lock (gate) return windows.Count; } }

    public InvitationAttempt TryAcquire(string issuer, string subject)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issuer + "\n" + subject)));
        var window = clock.GetUtcNow().ToUnixTimeSeconds() / WindowSeconds;
        lock (gate)
        {
            if (windows.TryGetValue(key, out var entry) && entry.Window == window)
            {
                if (entry.Count > AttemptsPerWindow) return InvitationAttempt.LimitedRepeat;
                windows[key] = (window, entry.Count + 1);
                return entry.Count + 1 > AttemptsPerWindow ? InvitationAttempt.Limited : InvitationAttempt.Allowed;
            }
            if (!windows.ContainsKey(key) && windows.Count >= capacity)
            {
                foreach (var stale in windows.Where(x => x.Value.Window != window).Select(x => x.Key).ToArray()) windows.Remove(stale);
                if (windows.Count >= capacity) return InvitationAttempt.LimitedRepeat;
            }
            windows[key] = (window, 1);
            return InvitationAttempt.Allowed;
        }
    }
}

// QA N3: pseudonymous reference to the authenticated identity of a not-yet-bound caller, for SecurityEvent attribution.
// HMAC-SHA256 over issuer+subject keyed by an HKDF of the operator-resolved OIDC client secret ("v1": stable while the
// secret is unchanged; rotating the secret starts new references). A public client without a secret uses a random
// per-process key ("p1": correlatable only within one process lifetime). The raw subject is never written.
public sealed class InvitationIdentityReference
{
    private readonly byte[] key;
    private readonly string version;

    public InvitationIdentityReference(string? serverSecret)
    {
        if (!string.IsNullOrEmpty(serverSecret))
        {
            key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(serverSecret), 32, info: Encoding.ASCII.GetBytes("poseidon.audit.identity-reference.v1"));
            version = "v1";
        }
        else { key = RandomNumberGenerator.GetBytes(32); version = "p1"; }
    }

    public string For(string issuer, string subject) =>
        $"idref:{version}:" + Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(issuer + "\n" + subject)).AsSpan(0, 16)).ToLowerInvariant();
}

public static partial class MembershipEndpoints
{
    private const string InvitationAction = "MembershipInvitation";
    private const string AcceptAction = "MembershipInvitationAccept";
    private const string ReissueAction = "MembershipInvitationReissue";
    private const string LifetimeAction = "InvitationLifetimeUpdate";

    private static void MapMembershipInvitations(IEndpointRouteBuilder app)
    {
        Command(app.MapPost("/api/v1/memberships/{id:guid}/invitation", (Guid id, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
            ReissueInvitation(h, id)), "ReissueMembershipInvitation", "Issues a new single-use invitation link for an Invited membership (the previous pending link stops working). The token is returned only in this response. expectedVersion is the membership version; a membership that is no longer Invited returns 409 invalid_transition.")
            .Accepts<InvitationReissueCommand>("application/json").Produces<MembershipInvitationIssueDto>();
        app.MapGet("/api/v1/memberships/invitation-settings", InvitationSettings).RequireAuthorization("DirectoryRead").WithName("GetInvitationSettings")
            .WithSummary("The tenant's invitation lifetime (54.01 account.invite_hours, 1-168 h, default 24) and whether invitation e-mail delivery is configured. Requires membership.read or membership.manage with tenant-wide scope.")
            .Produces<InvitationSettingsDto>().Produces(401).ProducesProblem(403).Produces(503);
        Command(app.MapPost("/api/v1/memberships/invitation-settings", (HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
            UpdateInvitationSettings(h)), "UpdateInvitationSettings", "Sets the tenant's invitation lifetime in hours (54.01 account.invite_hours: 1-168). Applies to invitations issued afterwards; existing invitations keep their expiry. expectedVersion is the settings version (0 while the source default applies); stale => 409 version_conflict; out of range => 400 validation_failed.")
            .Accepts<InvitationSettingsCommand>("application/json").Produces<InvitationSettingsDto>();
        app.MapPost("/api/v1/invitations/preview", (HttpContext h, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) => PreviewInvitation(h))
            .RequireAuthorization().WithName("PreviewMembershipInvitation").WithSummary("PreviewMembershipInvitation")
            .WithDescription("Shows the signed-in identity what a pending invitation grants in its tenant. No change is made. Every refusal (unknown, expired, used, revoked, another identity, identity already bound or configured) returns the same 409 invitation_unavailable; repeated attempts return 429 request_limit.")
            .Accepts<InvitationTokenCommand>("application/json").Produces<InvitationGrantDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(429).Produces(503);
        app.MapPost("/api/v1/invitations/accept", (HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) => AcceptInvitation(h))
            .RequireAuthorization().WithName("AcceptMembershipInvitation").WithSummary("AcceptMembershipInvitation")
            .WithDescription("Binds the signed-in IdP identity (validated issuer+subject) to the invited membership and activates its grant. One identity holds at most one accepted membership. The invitation session ends; the grant applies at the next sign-in. Every refusal returns the same 409 invitation_unavailable; repeated attempts return 429 request_limit.")
            .Accepts<InvitationTokenCommand>("application/json").Produces<InvitationAcceptedDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(429).Produces(503);
    }

    // USER-ADMIN-03: the tenant's 54.01 account.invite_hours value, or the 24 h source default.
    private static async Task<TimeSpan> InvitationLifetime(PoseidonDbContext db, CancellationToken ct) =>
        InvitationLifetimePolicy.For((await db.TenantInvitationSettings.AsNoTracking().SingleOrDefaultAsync(ct))?.LifetimeHours);

    // USER-ADMIN-03: outbox row. With a valid transport the recipient and token are protected (Data Protection, bound to
    // tenant + delivery id) until the dispatcher sends; without one the row is recorded NOT_CONFIGURED at once and keeps
    // nothing, so nothing personal waits for a transport that does not exist.
    private static InvitationDelivery NewDelivery(HttpContext h, MembershipInvitation invitation, string email, string token, string createdBy, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        var configured = h.RequestServices.GetRequiredService<IInvitationMailer>().Configured;
        return new InvitationDelivery { TenantId = invitation.TenantId, Id = id, InvitationId = invitation.Id, MembershipId = invitation.MembershipId,
            State = InvitationDeliveryPolicy.Wire(configured ? InvitationDeliveryState.Queued : InvitationDeliveryState.NotConfigured), Attempts = 0, CreatedAt = now,
            NextAttemptAt = configured ? now : null, CompletedAt = configured ? null : now,
            LastErrorCode = configured ? null : InvitationDeliveryPolicy.ErrorNotConfigured, CreatedBy = createdBy,
            ProtectedPayload = configured ? h.RequestServices.GetRequiredService<InvitationPayloadProtection>().Protect(invitation.TenantId, id, email, token) : null };
    }

    // A queued e-mail for a closed invitation is cancelled in the closing transaction and its payload erased.
    private static async Task CancelQueuedDeliveries(PoseidonDbContext db, Guid membership, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var queued in await db.InvitationDeliveries.Where(d => d.MembershipId == membership && d.State == "QUEUED").ToArrayAsync(ct))
        {
            queued.State = InvitationDeliveryPolicy.Wire(InvitationDeliveryState.Cancelled); queued.LastErrorCode = InvitationDeliveryPolicy.ErrorInvitationClosed;
            queued.ProtectedPayload = null; queued.LeaseUntil = null; queued.CompletedAt = now;
        }
    }

    private static async Task<IResult> InvitationSettings(HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        var actor = Actor(h);
        if (!CanReadMemberships(actor)) { await audit.WriteAsync(h, "MembershipRead", "permission_denied"); return Problem(h, 403, "permission_denied"); }
        var row = await db.TenantInvitationSettings.AsNoTracking().SingleOrDefaultAsync(h.RequestAborted);
        await audit.WriteAsync(h, "MembershipRead", "Success");
        return Results.Ok(SettingsDto(h, actor, row));
    }

    private static InvitationSettingsDto SettingsDto(HttpContext h, TrustedActor actor, TenantInvitationSettings? row) =>
        new(row?.LifetimeHours ?? InvitationLifetimePolicy.DefaultHours, InvitationLifetimePolicy.MinHours, InvitationLifetimePolicy.MaxHours,
            InvitationLifetimePolicy.DefaultHours, row != null, row?.Version ?? 0, h.RequestServices.GetRequiredService<IInvitationMailer>().Configured,
            CanManageMemberships(actor));

    private static async Task<IResult> UpdateInvitationSettings(HttpContext h)
    {
        var (prelude, failure) = await Begin(h, null, CanManageMemberships);
        if (prelude == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var command = Parse<InvitationSettingsCommand>(prelude.Body);
        if (command == null) { await audit.WriteAsync(h, "MembershipCommand", "invalid_request"); return Problem(h, 400, "invalid_request"); }
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (command.ExpectedVersion is null) errors["expectedVersion"] = ["required"];
        if (command.LifetimeHours is not { } hours) errors["lifetimeHours"] = ["required"];
        else if (!InvitationLifetimePolicy.ValidHours(hours)) errors["lifetimeHours"] = ["out_of_range"];
        if (errors.Count > 0) { await audit.WriteAsync(h, "MembershipCommand", "validation_failed"); return Problem(h, 400, "validation_failed", errors); }
        var actor = prelude.Actor;
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var fingerprint = Fingerprint(new { action = LifetimeAction, command.ExpectedVersion, command.LifetimeHours });
        return await Run(h, actor.TenantId, async db =>
        {
            var receipt = await Receipt(db, actor, prelude.Key, LifetimeAction, fingerprint, h.RequestAborted);
            if (receipt.Found && !receipt.Matches) return (Problem(h, 409, "idempotency_key_reused"), "idempotency_key_reused");
            var row = await db.TenantInvitationSettings.SingleOrDefaultAsync(h.RequestAborted);
            if (receipt.Found)
            {
                h.Response.Headers["Idempotency-Replayed"] = "true";
                return (Results.Ok(SettingsDto(h, actor, row)), "Success");
            }
            if ((row?.Version ?? 0) != command.ExpectedVersion) return (Problem(h, 409, "version_conflict"), "version_conflict");
            if (row == null)
            {
                row = new TenantInvitationSettings { TenantId = actor.TenantId, LifetimeHours = command.LifetimeHours!.Value, UpdatedAt = now, UpdatedBy = actor.Id.ToString("D") };
                db.TenantInvitationSettings.Add(row);
            }
            else { row.LifetimeHours = command.LifetimeHours!.Value; row.UpdatedAt = now; row.UpdatedBy = actor.Id.ToString("D"); }
            await db.SaveChangesAsync(h.RequestAborted);
            AddReceipt(db, actor, prelude.Key, LifetimeAction, fingerprint, actor.TenantId, now);
            await db.SaveChangesAsync(h.RequestAborted);
            return (Results.Ok(SettingsDto(h, actor, row)), "Success");
        });
    }

    private static MembershipInvitation NewInvitation(Guid tenant, Guid membership, string? expectedSubject, string createdBy, DateTimeOffset now, TimeSpan lifetime, out string token)
    {
        var secret = MembershipInvitationPolicy.NewSecret();
        token = MembershipInvitationPolicy.Format(tenant, secret);
        return new MembershipInvitation { TenantId = tenant, Id = Guid.NewGuid(), MembershipId = membership, TokenHash = MembershipInvitationPolicy.Hash(secret),
            ExpectedSubject = expectedSubject, Status = MembershipInvitationPolicy.Wire(InvitationStatus.Pending), CreatedAt = now,
            ExpiresAt = now + lifetime, CreatedBy = createdBy };
    }

    private static async Task<IResult> ReissueInvitation(HttpContext h, Guid id)
    {
        var (prelude, failure) = await Begin(h, id, CanManageMemberships);
        if (prelude == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var command = Parse<InvitationReissueCommand>(prelude.Body);
        if (command == null) { await audit.WriteAsync(h, "MembershipCommand", "invalid_request", resourceId: id); return Problem(h, 400, "invalid_request"); }
        if (command.ExpectedVersion is null || command.DeliveryEmail != null && !InvitationDeliveryPolicy.ValidEmail(command.DeliveryEmail))
        {
            await audit.WriteAsync(h, "MembershipCommand", "validation_failed", resourceId: id);
            return Problem(h, 400, "validation_failed", command.ExpectedVersion is null ? new() { ["expectedVersion"] = ["required"] } : new() { ["deliveryEmail"] = ["invalid"] });
        }
        var actor = prelude.Actor;
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var fingerprint = Fingerprint(new { action = ReissueAction, id, command.ExpectedVersion, email = command.DeliveryEmail != null });
        return await Run(h, id, async db =>
        {
            // Fresh object authorization precedes receipt reads (tenant filter ⇒ cross-tenant 404).
            var target = await db.AccountMemberships.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, h.RequestAborted);
            if (target == null) return (Problem(h, 404, "resource_not_found"), "resource_not_found");
            var receipt = await Receipt(db, actor, prelude.Key, ReissueAction, fingerprint, h.RequestAborted);
            if (receipt.Found && !receipt.Matches) return (Problem(h, 409, "idempotency_key_reused"), "idempotency_key_reused");
            if (receipt.Found)
            {
                // The token is never stored: a replay returns the membership without it (reissue again for a new link).
                h.Response.Headers["Idempotency-Replayed"] = "true";
                var replay = await AccessView(db, settings, actor, id, h.RequestAborted);
                return replay == null ? (Problem(h, 409, "conflict_changed"), "conflict_changed")
                    : (Results.Ok(new MembershipInvitationIssueDto(replay, null, null)), "Success");
            }
            if (target.Version != command.ExpectedVersion) return (Problem(h, 409, "version_conflict"), "version_conflict");
            if (target.Status != "Invited" || target.Source != ProvisionedGrants.Source) return (Problem(h, 409, "invalid_transition"), "invalid_transition");
            var previous = await db.MembershipInvitations.Where(i => i.MembershipId == id).OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id)
                .ToArrayAsync(h.RequestAborted);
            foreach (var open in previous.Where(i => i.Status == "Pending"))
            { open.Status = MembershipInvitationPolicy.Wire(InvitationStatus.Superseded); open.ClosedAt = now; }
            await CancelQueuedDeliveries(db, id, now, h.RequestAborted);
            // Superseded first: the partial unique index allows exactly one pending invitation per membership.
            await db.SaveChangesAsync(h.RequestAborted);
            var invitation = NewInvitation(actor.TenantId, id, previous.FirstOrDefault()?.ExpectedSubject, actor.Id.ToString("D"), now,
                await InvitationLifetime(db, h.RequestAborted), out var issued);
            db.MembershipInvitations.Add(invitation);
            if (command.DeliveryEmail != null) db.InvitationDeliveries.Add(NewDelivery(h, invitation, command.DeliveryEmail, issued, actor.Id.ToString("D"), now));
            await db.SaveChangesAsync(h.RequestAborted);
            AddReceipt(db, actor, prelude.Key, ReissueAction, fingerprint, id, now);
            await db.SaveChangesAsync(h.RequestAborted);
            return (Results.Ok(new MembershipInvitationIssueDto((await AccessView(db, settings, actor, id, h.RequestAborted))!, issued, invitation.ExpiresAt)), "Success");
        });
    }

    private sealed record InvitationTarget(MembershipInvitation Invitation, AccountMembership Membership, UserAccount Account);
    private sealed record InvitationRequest((string Issuer, string Subject) Identity, string Reference, Guid Tenant, string Hash, string? Key, byte[] Body);

    // Shared prelude of preview/accept: session check, rate limit (before any write), attempt audit, antiforgery, bounded
    // body, optional key, strict token. Every outcome after the transport checks is the same neutral 409, so no caller can
    // learn about other tenants. Pre-binding events carry the pseudonymous identity reference (QA N3) and, once the token's
    // tenant segment parses, that claimed tenant as UnverifiedTenantId (TenantId is set only once the invitation is found).
    private static async Task<(InvitationRequest? Value, IResult? Failure)> InvitationBegin(HttpContext h, bool keyRequired)
    {
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        // Only an invitation session (an IdP-validated identity with no membership, no operator entry, no binding) may
        // preview or accept: a member's session already holds its one membership (UA2-D04), so it is denied here.
        if (h.RequestServices.GetRequiredService<ActorResolver>().InvitationIdentityFor(h.User) is not { } identity)
        { await audit.WriteAsync(h, InvitationAction, "Denied"); return (null, Problem(h, 403, "permission_denied")); }
        var reference = h.RequestServices.GetRequiredService<InvitationIdentityReference>().For(identity.Issuer, identity.Subject);
        Guid? claimedTenant = null;
        async Task<(InvitationRequest?, IResult?)> Fail(int status, string code, string? auditCode = null)
        { await audit.WriteAsync(h, InvitationAction, auditCode ?? code, identityReference: reference, unverifiedTenant: claimedTenant); return (null, Problem(h, status, code)); }
        // QA N4: the limiter runs before any SecurityEvent write; only the first refused attempt of a window is audited.
        switch (h.RequestServices.GetRequiredService<InvitationAttemptLimiter>().TryAcquire(identity.Issuer, identity.Subject))
        {
            case InvitationAttempt.Limited: return await Fail(429, "request_limit");
            case InvitationAttempt.LimitedRepeat: return (null, Problem(h, 429, "request_limit"));
        }
        await audit.WriteAsync(h, InvitationAction, "Attempted", identityReference: reference);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(403, "csrf_invalid"); }
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > 1024) return await Fail(400, "invalid_request");
        using var buffer = new MemoryStream();
        var bytes = new byte[512]; int read;
        while ((read = await h.Request.Body.ReadAsync(bytes, h.RequestAborted)) > 0)
        {
            if (buffer.Length + read > 1024) return await Fail(400, "invalid_request");
            buffer.Write(bytes, 0, read);
        }
        string? key = null;
        if (keyRequired)
        {
            var keys = h.Request.Headers["Idempotency-Key"];
            key = keys.Count == 1 ? keys[0] : null;
            if (key is null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(400, "invalid_idempotency_key");
        }
        var command = Parse<InvitationTokenCommand>(buffer.ToArray());
        if (command == null) return await Fail(400, "invalid_request");
        if (!MembershipInvitationPolicy.TryParse(command.Token, out var tenant, out var secret))
            return await Fail(409, MembershipInvitationPolicy.NeutralRefusal, "invitation_token_invalid");
        claimedTenant = tenant;
        return (new InvitationRequest(identity, reference, tenant, MembershipInvitationPolicy.Hash(secret), key, buffer.ToArray()), null);
    }

    // A tenant-scoped context for the token's tenant: query filters stay on, so the token can only ever reach rows of
    // that one tenant (no IgnoreQueryFilters, no administrative context). The tenant id itself grants nothing.
    private static PoseidonDbContext InvitationContext(HttpContext h, Guid tenant, ActorContext actor)
    {
        var configuration = h.RequestServices.GetRequiredService<IConfiguration>();
        var connection = OperatorSecretReferences.Database(configuration, h.RequestServices.GetRequiredService<IHostEnvironment>());
        if (string.IsNullOrWhiteSpace(connection)) throw new AuditUnavailableException();
        return new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connection).Options, tenant, actor,
            qualifiedInputs: h.RequestServices.GetRequiredService<IQualifiedInputRuntime>(), timeProvider: h.RequestServices.GetRequiredService<TimeProvider>());
    }

    private static async Task<InvitationTarget?> FindInvitation(PoseidonDbContext db, string hash, bool tracked, CancellationToken ct)
    {
        var invitations = tracked ? db.MembershipInvitations : db.MembershipInvitations.AsNoTracking();
        var invitation = await invitations.SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invitation == null) return null;
        var memberships = tracked ? db.AccountMemberships : db.AccountMemberships.AsNoTracking();
        var membership = await memberships.SingleOrDefaultAsync(m => m.Id == invitation.MembershipId, ct);
        if (membership == null) return null;
        var accounts = tracked ? db.UserAccounts : db.UserAccounts.AsNoTracking();
        var account = await accounts.SingleOrDefaultAsync(a => a.Id == membership.AccountId, ct);
        return account == null ? null : new InvitationTarget(invitation, membership, account);
    }

    private static InvitationRefusal Evaluate(InvitationTarget target, (string Issuer, string Subject) identity, DateTimeOffset now)
    {
        if (MembershipInvitationPolicy.Parse(target.Invitation.Status) is not { } status ||
            MembershipLifecyclePolicy.Parse(target.Membership.Status) is not { } membership) return InvitationRefusal.Closed;
        // An account that is already bound (to anyone) can never be bound again.
        if (target.Account.Subject != null || target.Membership.Source != ProvisionedGrants.Source) return InvitationRefusal.Closed;
        return MembershipInvitationPolicy.Decide(new InvitationFacts(status, target.Invitation.ExpiresAt, target.Invitation.ExpectedSubject,
            target.Account.Issuer, membership), identity.Issuer, identity.Subject, now);
    }

    private static async Task<InvitationGrantDto> GrantView(PoseidonDbContext db, InvitationTarget target, CancellationToken ct)
    {
        var grant = (await ProvisionedGrants.LoadAsync(db, [target.Membership.Id], ct)).GetValueOrDefault(target.Membership.Id);
        var tenantName = await db.Tenants.AsNoTracking().Select(t => t.Name).SingleOrDefaultAsync(ct);
        return new InvitationGrantDto(tenantName, grant?.Bundles ?? [], ActorResolver.TrustedPermissions(grant?.Permissions).Length, grant?.TenantWideRead == true,
            grant?.TenantWideManage == true, grant?.VesselGrants.Length ?? 0, grant?.ActiveCompanyId != null, target.Invitation.ExpiresAt);
    }

    // UA2-D04: an identity that already holds an accepted (bound) membership in ANY tenant accepts nothing more. This is the
    // login lookup (ProvisionedIdentityLocator), keyed only by the IdP-validated issuer+subject of the caller's own session;
    // its result is never returned, only folded into the neutral refusal. The unique accepted-binding index stays the
    // authority under concurrency.
    private static async Task<bool> IdentityBound(HttpContext h, (string Issuer, string Subject) identity) =>
        await h.RequestServices.GetRequiredService<ProvisionedIdentityLocator>().FindAsync(identity.Issuer, identity.Subject, h.TraceIdentifier, h.RequestAborted) != null;

    private static ActorContext InvitationReader(HttpContext h) =>
        new("system:invitation-reader", "System", h.TraceIdentifier, Source: "MembershipInvitation");

    // QA N5: an audit-store outage anywhere in preview/accept is a 503, never a 500.
    private static async Task<IResult> AuditGuarded(HttpContext h, Func<Task<IResult>> handler)
    {
        try { return await handler(); }
        catch (AuditUnavailableException) { return Problem(h, 503, "service_unavailable"); }
    }

    private static Task<IResult> PreviewInvitation(HttpContext h) => AuditGuarded(h, () => Preview(h));
    private static Task<IResult> AcceptInvitation(HttpContext h) => AuditGuarded(h, () => Accept(h));

    private static async Task<IResult> Preview(HttpContext h)
    {
        var (request, failure) = await InvitationBegin(h, keyRequired: false);
        if (request == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        try
        {
            if (await IdentityBound(h, request.Identity))
            {
                await audit.WriteAsync(h, InvitationAction, "identity_unavailable", identityReference: request.Reference, unverifiedTenant: request.Tenant);
                return Problem(h, 409, MembershipInvitationPolicy.NeutralRefusal);
            }
            await using var db = InvitationContext(h, request.Tenant, InvitationReader(h));
            var target = await FindInvitation(db, request.Hash, tracked: false, h.RequestAborted);
            var refusal = target == null ? InvitationRefusal.Closed : Evaluate(target, request.Identity, now);
            if (target == null || refusal != InvitationRefusal.None)
            {
                await audit.WriteAsync(h, InvitationAction, target == null ? MembershipInvitationPolicy.NeutralRefusal : MembershipInvitationPolicy.AuditCode(refusal),
                    resourceId: target?.Membership.Id, attributedTenant: target == null ? null : request.Tenant, identityReference: request.Reference,
                    unverifiedTenant: target == null ? request.Tenant : null);
                return Problem(h, 409, MembershipInvitationPolicy.NeutralRefusal);
            }
            var view = await GrantView(db, target, h.RequestAborted);
            await audit.WriteAsync(h, InvitationAction, "Authorized", resourceId: target.Membership.Id, attributedTenant: request.Tenant, identityReference: request.Reference);
            return Results.Ok(view);
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
        {
            await audit.TryWriteAsync(h, InvitationAction, "Unavailable");
            return Problem(h, 503, "service_unavailable");
        }
    }

    private static async Task<IResult> Accept(HttpContext h)
    {
        var (request, failure) = await InvitationBegin(h, keyRequired: true);
        if (request == null) return failure!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var fingerprint = Fingerprint(new { action = AcceptAction, hash = request.Hash });
        async Task<IResult> Refuse(string code, Guid? membership = null)
        {
            // Pre-binding refusal: the pseudonymous identity reference; the claimed tenant stays unverified (ResourceId) until found.
            await audit.WriteAsync(h, InvitationAction, code, resourceId: membership, attributedTenant: membership == null ? null : request.Tenant,
                identityReference: request.Reference, unverifiedTenant: membership == null ? request.Tenant : null);
            return Problem(h, 409, MembershipInvitationPolicy.NeutralRefusal);
        }
        InvitationTarget? found;
        ProvisionedGrant? grant;
        try
        {
            await using var reader = InvitationContext(h, request.Tenant, InvitationReader(h));
            found = await FindInvitation(reader, request.Hash, tracked: false, h.RequestAborted);
            grant = found == null ? null : (await ProvisionedGrants.LoadAsync(reader, [found.Membership.Id], h.RequestAborted)).GetValueOrDefault(found.Membership.Id);
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
        {
            await audit.TryWriteAsync(h, InvitationAction, "Unavailable");
            return Problem(h, 503, "service_unavailable");
        }
        if (found == null || grant?.RoleContext == null) return await Refuse(found == null ? MembershipInvitationPolicy.NeutralRefusal : "invitation_closed", found?.Membership.Id);
        var accountId = found.Account.Id;
        var membershipId = found.Membership.Id;
        // The write is attributed to the account being bound (server-derived from the matched invitation, never a request
        // value) with the invited grant's role context, inside the token's tenant only.
        var actorContext = new ActorContext(accountId.ToString(), "User", h.TraceIdentifier, string.Join(',', ActorResolver.TrustedPermissions(grant.Permissions).Order(StringComparer.Ordinal)),
            "WebCommand", grant.RoleContext, S1SecurityContract.Command(h));
        await using var db = InvitationContext(h, request.Tenant, actorContext);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        InvitationGrantDto view;
        try
        {
            var receipt = await db.MembershipCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == accountId && r.Key == request.Key, h.RequestAborted);
            var target = await FindInvitation(db, request.Hash, tracked: true, h.RequestAborted);
            if (receipt != null)
            {
                // Lost-reply retry by the same bound identity: same key and token ⇒ the committed outcome, no second effect.
                await Rollback(transaction);
                if (receipt.Action != AcceptAction || receipt.Fingerprint != fingerprint || target == null || target.Account.Subject != request.Identity.Subject ||
                    target.Account.Issuer != request.Identity.Issuer)
                    return await Refuse(MembershipInvitationPolicy.NeutralRefusal, membershipId);
                h.Response.Headers["Idempotency-Replayed"] = "true";
                return Results.Ok(new InvitationAcceptedDto(membershipId, await GrantView(db, target, h.RequestAborted), true));
            }
            if (target == null || target.Membership.Id != membershipId) { await Rollback(transaction); return await Refuse("conflict_changed", membershipId); }
            if (await IdentityBound(h, request.Identity)) { await Rollback(transaction); return await Refuse("identity_unavailable", membershipId); }
            var refusal = Evaluate(target, request.Identity, now);
            if (refusal != InvitationRefusal.None) { await Rollback(transaction); return await Refuse(MembershipInvitationPolicy.AuditCode(refusal), membershipId); }
            target.Account.Subject = request.Identity.Subject;
            target.Membership.Status = MembershipLifecyclePolicy.Wire(MembershipStatus.Active);
            target.Membership.StatusReason = "InvitationAccepted";
            target.Membership.StatusChangedAt = now;
            target.Membership.AccessRevision += 1;
            target.Invitation.Status = MembershipInvitationPolicy.Wire(InvitationStatus.Accepted);
            target.Invitation.ClosedAt = now;
            // USER-ADMIN-03 (QA M2): an accepted invitation's queued e-mail is cancelled and its payload erased in this transaction.
            await CancelQueuedDeliveries(db, membershipId, now, h.RequestAborted);
            await db.SaveChangesAsync(h.RequestAborted);
            db.MembershipCommandReceipts.Add(new MembershipCommandReceipt { TenantId = request.Tenant, ActorId = accountId, Key = request.Key!, Action = AcceptAction,
                Fingerprint = fingerprint, ResourceId = membershipId, CommittedAt = now });
            await db.SaveChangesAsync(h.RequestAborted);
            view = await GrantView(db, target, h.RequestAborted);
            await transaction.CommitAsync(h.RequestAborted);
        }
        catch (DbUpdateException ex) when (PostgresFailure.Provider(ex) is { SqlState: PostgresErrorCodes.UniqueViolation } unique && unique.ConstraintName == "IX_Accounts_IssuerSubject")
        {
            // UA2-D04: the identity already holds an accepted membership (this or another tenant). No cross-tenant query.
            await Rollback(transaction);
            return await Refuse("identity_unavailable", membershipId);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || PostgresFailure.IsSerialization(ex) ||
            ex is DbUpdateException && PostgresFailure.Provider(ex)?.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // A concurrent accept/reissue/revoke committed first: exactly one binding wins.
            await Rollback(transaction);
            return await Refuse("conflict_changed", membershipId);
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
        {
            await Rollback(transaction);
            await audit.TryWriteAsync(h, InvitationAction, "Unavailable");
            return Problem(h, 503, "service_unavailable");
        }
        await audit.WriteAsync(h, InvitationAction, "Success", resourceId: membershipId, attributedTenant: request.Tenant, attributedActor: accountId.ToString());
        // The invitation session ends; the next sign-in stamps the accepted binding (UA-D07: a new membership applies at login).
        if (h.RequestServices.GetRequiredService<ActorResolver>().InvitationIdentityFor(h.User) != null && !h.Response.HasStarted)
            await h.SignOutAsync(FoundationIdentity.Cookie);
        return Results.Ok(new InvitationAcceptedDto(membershipId, view, true));
    }
}
