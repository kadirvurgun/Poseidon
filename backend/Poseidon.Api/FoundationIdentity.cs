using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Poseidon.Api;

public sealed class FederationSettings
{
    public string? Authority { get; set; }
    public string? Issuer { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ClientSecretReference { get; set; }
    public Membership[] Memberships { get; set; } = [];
    public bool Configured => Uri.TryCreate(Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        !string.IsNullOrWhiteSpace(Issuer) && !string.IsNullOrWhiteSpace(ClientId);
}

// Provisioned by the server operator, never populated from request binding or IdP role claims.
public sealed record Membership(string Issuer, string Subject, Guid ActorId, Guid TenantId, string[] Permissions, bool TenantWideRead = false, bool TenantWideManage = false, VesselGrant[]? VesselGrants = null)
{
    public string[]? RoleReferences { get; set; }
    // Explicit current session scope. It is never inferred from a vessel owner,
    // operator, manager, role name, IdP claim or request selector.
    public Guid? ActiveCompanyId { get; set; }
    // Explicit operator appointments. No inference from administrator/IdP/client role names.
    public string ActorKind { get; set; } = "Unspecified";
    // S7 WP-B: vessel-scoped Tug Master/Master/Chief Engineer appointments (TugOperationsAuthority).
    public TugOperationsAuthority[] TugOperationsAuthorities { get; set; } = [];
    public HumanAuthority[] HumanAuthorities { get; set; } = [];
    public DailyReviewAuthority[] DailyReviewAuthorities { get; set; } = [];
    public CertificateAssertionAuthority[] CertificateAssertionAuthorities { get; set; } = [];
    public RegulatoryAuthority[] RegulatoryAuthorities { get; set; } = [];
    public HumanTraceReader[] HumanTraceReaders { get; set; } = [];
    public CrewCatalogAuthority[] CrewCatalogAuthorities { get; set; } = [];
    // S7 WP-A: credential verification, competence sign-off and Safe Manning decision appointments.
    public CrewAuthority[] CrewAuthorities { get; set; } = [];
    // S9 WP-A (DEV-059): vessel-scoped Master appointments for the departure decision (DepartureAuthority).
    public DepartureAuthority[] DepartureAuthorities { get; set; } = [];
    // DEV-101 SOF finalization / DEV-102 downtime review appointments (vessel-scoped duty).
    public ShipOperationsAuthority[] ShipOperationsAuthorities { get; set; } = [];
    // S11 WP-A (DEV-070): vessel-scoped customer data owner appointments for the reconciliation sign-off.
    public DataOwnerAuthority[] DataOwnerAuthorities { get; set; } = [];
    // S11 WP-C (DEV-073/093): regulatory SME reviewer appointments with an operator-recorded qualification reference.
    public RegulatoryReviewerAuthority[] RegulatoryReviewerAuthorities { get; set; } = [];
    // S11 WP-D (DEV-075): tenant pilot Product Owner appointments for the go/no-go decision (PilotGateAuthority).
    public PilotGateAuthority[] PilotGateAuthorities { get; set; } = [];
    // ConfigurationBinder must use property binding. Constructor binding can silently
    // omit an array member whose optional collection is an explicit empty JSON array.
    public Membership() : this("", "", Guid.Empty, Guid.Empty, []) { }
}
public sealed record VesselGrant(Guid VesselId, bool CanEdit = false);
// Server-provisioned crew appointment for one vessel and one scope (CredentialVerification,
// CompetenceAssessment, SafeManningDecision). Never request-bound or inferred from role names.
public sealed record CrewAuthority(Guid Id, Guid VesselId, string Scope, Guid RoleId, string Duty,
    string AppointmentSource, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
// Server-provisioned appointment for one vessel and one lifecycle scope. Never
// request-bound or inferred from an IdP role name, crew list or administrator.
public sealed record ShipOperationsAuthority(Guid Id, Guid VesselId, string Scope, Guid RoleId, string Duty,
    string AppointmentSource, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
public sealed record TrustedActor(Guid Id, Guid TenantId, string[] Permissions, bool TenantWideRead, bool TenantWideManage, VesselGrant[] VesselGrants)
{
    public string? RoleContext { get; init; }
    public Guid? ActiveCompanyId { get; init; }
    public bool CanReadTug => CanRead && Permissions.Contains("mode.tug") && Permissions.Contains("tug.read");
    public bool CanSeeShip => !Permissions.Contains("mode.tug") || Permissions.Contains("mode.cross");
    public bool CanSeeTug => Permissions.Contains("mode.tug");
    public bool CanAccessKind(Poseidon.Persistence.VesselKind kind) => kind == Poseidon.Persistence.VesselKind.Tug ? CanSeeTug : CanSeeShip;
    public bool CanReadAudit => Permissions.Contains("audit.read") && CanRead;
    public string AuditCapabilities => string.Join(',', Permissions.Order(StringComparer.Ordinal));
    // Explicit tenant-local capability. Vessel-only grants and directory writes confer no catalog authority.
    public bool CanReadRegulatory => CanRead && (TenantWideRead || TenantWideManage) && Permissions.Contains("regulatory.draft.read");
    public bool HasWriteAttribution => RoleContext != null;
    public bool CanWriteRegulatory => HasWriteAttribution && CanReadRegulatory && Permissions.Contains("regulatory.draft.write");
    public bool CanRead => Permissions.Contains("directory.read") && (TenantWideRead || TenantWideManage || VesselGrants.Length > 0);
    public bool CanManage => HasWriteAttribution && CanRead && TenantWideManage && Permissions.Contains("directory.write");
    public bool CanSearch => CanRead && Permissions.Contains("workspace.search");
    public bool CanEditVessel(Guid id) => HasWriteAttribution && CanRead && Permissions.Contains("directory.write") &&
        (TenantWideManage || VesselGrants.Any(g => g.VesselId == id && g.CanEdit));
    public IQueryable<Poseidon.Persistence.Vessel> Scope(IQueryable<Poseidon.Persistence.Vessel> query)
    {
        if (!CanRead) return query.Where(_ => false);
        query = query.Where(v => v.Kind == Poseidon.Persistence.VesselKind.Tug ? CanSeeTug : CanSeeShip);
        if (TenantWideRead || TenantWideManage) return query;
        var ids = VesselGrants.Select(g => g.VesselId).ToArray();
        return query.Where(v => ids.Contains(v.Id));
    }
}

// Operator-provisioned identity binding used by membership revalidation (PLAN-1511-5202).
// Provisioned: USER-ADMIN-01 admin-provisioned database membership (no operator entry); its tenant comes from the
// protected session stamped at login, never from a request value.
public sealed record ConfiguredIdentity(string Issuer, string Subject, Guid ActorId, Guid TenantId, bool Provisioned = false);

public sealed class ActorResolver(FederationSettings settings, IHttpContextAccessor? http = null)
{
    private Membership? Configured(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var subjects = principal.FindAll("sub").ToArray();
        var issuers = principal.FindAll("iss").ToArray();
        if (subjects.Length != 1 || issuers.Length != 1) return null;
        var matches = settings.Memberships.Where(x => x.Issuer == issuers[0].Value && x.Subject == subjects[0].Value).ToArray();
        return matches.Length != 1 || matches[0].ActorId == Guid.Empty || matches[0].TenantId == Guid.Empty ? null : matches[0];
    }

    public ConfiguredIdentity? ConfiguredIdentityFor(ClaimsPrincipal principal) =>
        Configured(principal) is { } m ? new(m.Issuer, m.Subject, m.ActorId, m.TenantId) : null;

    public const string ProvisionedTenantClaim = "poseidon:tid";
    public const string SessionSourceClaim = "poseidon:src";
    public const string ProvisionedSource = "provisioned";
    // USER-ADMIN-02: a short session for an IdP-validated identity WITHOUT any membership, issued only when the login
    // was started for the invitation acceptance page. It resolves to no actor (every policy denies it); it can only
    // preview/accept an invitation with its own validated issuer+subject.
    public const string InvitationSource = "invitation";

    // Exactly one validated issuer and subject of any authenticated session (configured, provisioned or invitation).
    public static (string Issuer, string Subject)? AuthenticatedIdentity(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var subjects = principal.FindAll("sub").ToArray();
        var issuers = principal.FindAll("iss").ToArray();
        return subjects.Length == 1 && issuers.Length == 1 && subjects[0].Value.Length is > 0 and <= 255 && issuers[0].Value.Length is > 0 and <= 1024
            ? (issuers[0].Value, subjects[0].Value) : null;
    }

    // An invitation session: the server-stamped source claim, no operator entry and no provisioned binding.
    public (string Issuer, string Subject)? InvitationIdentityFor(ClaimsPrincipal principal)
    {
        if (AuthenticatedIdentity(principal) is not { } identity) return null;
        var sources = principal.FindAll(SessionSourceClaim).ToArray();
        if (sources.Length != 1 || sources[0].Value != InvitationSource || principal.FindAll(ProvisionedTenantClaim).Any()) return null;
        return settings.Memberships.Any(x => x.Issuer == identity.Issuer && x.Subject == identity.Subject) ? null : identity;
    }

    // USER-ADMIN-01: an identity without any operator entry whose server-issued session carries the provisioned
    // binding (tenant + actor). An identity present in the operator configuration is always governed by it.
    public ConfiguredIdentity? ProvisionedIdentityFor(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var subjects = principal.FindAll("sub").ToArray();
        var issuers = principal.FindAll("iss").ToArray();
        var sources = principal.FindAll(SessionSourceClaim).ToArray();
        var tenants = principal.FindAll(ProvisionedTenantClaim).ToArray();
        var actors = principal.FindAll(ClaimTypes.NameIdentifier).ToArray();
        if (subjects.Length != 1 || issuers.Length != 1 || sources.Length != 1 || sources[0].Value != ProvisionedSource || tenants.Length != 1 || actors.Length != 1 ||
            !Guid.TryParseExact(tenants[0].Value, "D", out var tenant) || tenant == Guid.Empty || !Guid.TryParseExact(actors[0].Value, "D", out var actor) || actor == Guid.Empty)
            return null;
        if (settings.Memberships.Any(x => x.Issuer == issuers[0].Value && x.Subject == subjects[0].Value)) return null;
        return new(issuers[0].Value, subjects[0].Value, actor, tenant, Provisioned: true);
    }

    // The identity the session gate revalidates on every request: operator entry first, then provisioned binding.
    public ConfiguredIdentity? SessionIdentityFor(ClaimsPrincipal principal) => ConfiguredIdentityFor(principal) ?? ProvisionedIdentityFor(principal);

    // Actor of an admin-provisioned membership from the gate's current-request snapshot only (no snapshot ⇒ none).
    private TrustedActor? Provisioned(ClaimsPrincipal principal)
    {
        if (ProvisionedIdentityFor(principal) is not { } id) return null;
        if (http?.HttpContext?.Items[MembershipSession.OverlayItem] is not MembershipOverlay { Denied: false, Snapshot: { Grant: { } grant } snapshot } overlay ||
            overlay.Issuer != id.Issuer || overlay.Subject != id.Subject || snapshot.IdentityConflict || snapshot.Status != Poseidon.Domain.MembershipStatus.Active ||
            snapshot.AccountId != id.ActorId || snapshot.TenantId != id.TenantId) return null;
        return new TrustedActor(id.ActorId, id.TenantId, TrustedPermissions(grant.Permissions), grant.TenantWideRead, grant.TenantWideManage,
            grant.VesselGrants.Where(g => g.VesselId != Guid.Empty).ToArray()) { RoleContext = grant.RoleContext, ActiveCompanyId = grant.ActiveCompanyId };
    }

    // Persisted lifecycle state loaded by MembershipSessionGate for this request. It can only
    // narrow the operator grant: inactive => no actor; scope/role/company must agree.
    private MembershipOverlay? Overlay(Membership m) =>
        http?.HttpContext?.Items[MembershipSession.OverlayItem] is MembershipOverlay o && o.Issuer == m.Issuer && o.Subject == m.Subject ? o : null;
    // Operator-assigned local role references are observations only: never permission grants or IdP labels.
    public static string? TrustedRoleContext(string[]? refs) => refs is { Length: > 0 and <= 16 } &&
        refs.All(x => x != null && x.Length == 36 && Guid.TryParseExact(x, "D", out var id) && id != Guid.Empty) &&
        refs.Distinct(StringComparer.OrdinalIgnoreCase).Count() == refs.Length
        ? string.Join(',', refs.Select(x => Guid.Parse(x).ToString("D")).Order(StringComparer.Ordinal)) : null;
    // Closed allow-list of operator permissions the API understands; unknown values are dropped.
    public static readonly string[] KnownPermissions =
    [
        "import.reconciliation.sign", "regulatory.review.read", "regulatory.review.manage", "regulatory.review.verdict", "pilot.metrics.read", "pilot.metrics.contribute", "uat.read", "uat.record",
        "uat.threshold.record", "uat.attest", "pilot.gate.read", "pilot.gate.support.record", "pilot.gate.decide", "pilot.program.read", "pilot.program.manage", "pilot.program.record",
        "pilot.program.triage", "pilot.program.decide", "tug.execution.verify", "assurance.inspection.read", "assurance.inspection.import", "assurance.import.accept", "assurance.evidence.write", "assurance.finding.link",
        "assurance.mapping.write", "drydock.read", "drydock.write", "drydock.verify", "voice.draft.read", "voice.draft.write", "document.isps.read", "document.restricted.download",
        "document.acknowledge", "departure.readiness.read", "departure.decide", "crew.credential.verify", "crew.competence.read", "crew.competence.write", "crew.competence.assess", "crew.eligibility.read",
        "crew.safe_manning.read", "crew.safe_manning.decide", "document.publish", "evidence.verify", "record.hold", "record.retention.manage", "record.purge.request", "record.purge.approve",
        "hr.legal_entity.read", "hr.legal_entity.write", "hr.legal_entity.identifier.read", "hr.person.read", "hr.person.write", "hr.person.verify", "hr.employment.read", "hr.employment.write",
        "hr.employment.approve", "hr.assignment.read", "hr.assignment.write", "hr.assignment.approve", "hr.actual.record", "config.read", "config.resolve", "config.draft",
        "config.approve", "config.reject", "hse.risk.read", "hse.risk.write", "hse.risk.approve", "hse.risk.acknowledge", "hse.high_risk_work.read", "hse.safety_event.read",
        "hse.safety_event.report", "hse.safety_event.triage", "hse.safety_event.identity.read", "hse.safety_event.investigate", "hse.safety_event.review", "hse.safety_action.write", "hse.safety_action.verify", "hse.safety_action.effectiveness",
        "hse.risk_template.read", "hse.risk_template.write", "hse.risk_template.publish", "hse.risk_template.suggest", "hse.risk_method.write", "hse.risk_method.publish", "operations.downtime.read", "operations.downtime.write",
        "workorder.read", "workorder.write", "workorder.release", "workorder.execute", "workorder.verify", "workorder.close", "spare.read", "spare.write",
        "requisition.propose", "requisition.approve", "maintenance.measurement.write", "maintenance.postponement.request", "maintenance.postponement.decide", "defect.read", "defect.write", "defect.verify",
        "defect.effectiveness", "defect.close", "service.report.read", "service.report.write", "service.report.verify", "operations.fuel.read", "operations.fuel.write", "operations.port.read",
        "operations.port.write", "operations.port.finalize", "operations.downtime.review", "certificate.verify.record", "certificate.read", "certificate.extract", "certificate.write", "certificate.renewal.write",
        "certificate.renewal.owner", "certificate.renewal.verify", "certificate.renewal.complete", "certificate.survey.plan", "certificate.survey.review", "daily.noon.review", "daily.deck.review", "daily.engine.review",
        "daily.noon.read", "daily.noon.write", "daily.deck.read", "daily.deck.write", "daily.engine.read", "daily.engine.write", "system.health.read", "notification.evaluate",
        "notification.read", "notification.manage", "notification.acknowledge", "notification.preference.write", "crew.catalog.govern", "crew.assessment.read", "regulatory.qualify", "regulatory.facts.attest",
        "human.trace.read", "human.review.read", "human.review.submit", "human.verify", "human.decide", "control.read", "control.write", "control.approve",
        "control.owner.eligible", "evidence.read", "evidence.write", "applicability.read", "applicability.evaluate", "applicability.override.author", "applicability.override.approve", "applicability.override.revoke",
        "rules.write", "rules.review", "import.legacy.convert", "crew.identity.convert", "crew.documents.read", "crew.documents.write", "crew.position.write", "maintenance.observation.write",
        "crew.assignment.declare", "import.read", "import.write", "crew.certificate.read", "crew.certificate.write", "crew.profile.identity.read", "crew.profile.identity.write", "crew.profile.contact.read",
        "crew.profile.contact.write", "crew.profile.medical.read", "crew.profile.medical.write", "crew.profile.employment.read", "crew.profile.employment.write", "mode.tug", "mode.cross", "tug.read",
        "tug.write", "tug.plan.write", "tug.owner.eligible", "crew.read", "crew.write", "crew.identity.read", "crew.identity.write", "crew.history.read",
        "directory.read", "directory.write", "workspace.search", "regulatory.draft.read", "regulatory.draft.write", "audit.read", "file.read", "file.write",
        "equipment.read", "equipment.write", "equipment.history.read", "maintenance.read", "maintenance.write", "maintenance.history.read", "maintenance.owner.eligible", "membership.read",
        "membership.manage", "membership.security.suspend",
    ];
    private static readonly HashSet<string> Known = new(KnownPermissions, StringComparer.Ordinal);
    public static string[] TrustedPermissions(string[]? permissions) => (permissions ?? []).Where(x => x != null && Known.Contains(x)).Distinct().ToArray();
    public TrustedActor? Resolve(ClaimsPrincipal principal)
    {
        if (Configured(principal) is not { } m) return Provisioned(principal);
        var overlay = Overlay(m);
        if (overlay is { Denied: true } || overlay?.Snapshot is { } inactive && (inactive.IdentityConflict || inactive.Status != Poseidon.Domain.MembershipStatus.Active)) return null;
        var persisted = overlay?.Snapshot;
        var roleContext = TrustedRoleContext(m.RoleReferences);
        var company = m.ActiveCompanyId is { } configuredCompany && configuredCompany != Guid.Empty ? configuredCompany : (Guid?)null;
        var grants = (m.VesselGrants ?? []).Where(g => g.VesselId != Guid.Empty).ToArray();
        if (persisted != null)
        {
            grants = grants.Where(g => persisted.VesselGrants.Any(p => p.VesselId == g.VesselId))
                .Select(g => g with { CanEdit = g.CanEdit && persisted.VesselGrants.Any(p => p.VesselId == g.VesselId && p.CanEdit) }).ToArray();
            if (roleContext != persisted.RoleContext) roleContext = null;
            if (company != persisted.ActiveCompanyId) company = null;
        }
        return new TrustedActor(m.ActorId, m.TenantId, TrustedPermissions(m.Permissions),
            m.TenantWideRead, m.TenantWideManage, grants) {
                RoleContext = roleContext,
                ActiveCompanyId = company
            };
    }
}

public static class FoundationIdentity
{
    private static bool TrustedPermissionsContain(string[] permissions, string permission) => ActorResolver.TrustedPermissions(permissions).Contains(permission);

    public const string Cookie = "poseidon-session";
    public const string Oidc = "oidc";
    public static string LocalReturn(string? value) => value != null && value.StartsWith("/app", StringComparison.Ordinal) &&
        (value.Length == 4 || value[4] is '/' or '?' or '#') && value.Length <= 512 &&
        !value.Contains('%') && !value.Contains('\\') && !value.Split('?', '#')[0].Split('/').Any(x => x is "." or "..") &&
        !value.Any(char.IsControl) ? value : "/app";

    // USER-ADMIN-02: the login was started from the invitation acceptance page (local return path only).
    public const string InvitationPage = "/app/accept-invitation";
    public static bool IsInvitationReturn(string? redirect) => redirect != null && LocalReturn(redirect) == redirect &&
        redirect.StartsWith(InvitationPage, StringComparison.Ordinal) && (redirect.Length == InvitationPage.Length || redirect[InvitationPage.Length] is '?' or '#');

    public static readonly TimeSpan InvitationSessionLifetime = TimeSpan.FromMinutes(10);

    // USER-ADMIN-02 (QA N6): the decision OnTokenValidated applies to an IdP-validated identity that resolved to no
    // membership. Only a login that returns to the invitation page, for an identity with exactly one issuer and subject and
    // no operator entry, gets an invitation session: claims sub/iss/source only, lifetime min(ID token, 10 minutes).
    public static (ClaimsPrincipal Principal, DateTimeOffset ExpiresUtc)? InvitationSession(ClaimsPrincipal validated, string? redirectUri,
        FederationSettings settings, DateTimeOffset tokenValidTo, DateTimeOffset now)
    {
        if (!IsInvitationReturn(redirectUri) || ActorResolver.AuthenticatedIdentity(validated) is not { } invitee ||
            settings.Memberships.Any(x => x.Issuer == invitee.Issuer && x.Subject == invitee.Subject) || tokenValidTo <= now) return null;
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", invitee.Subject), new Claim("iss", invitee.Issuer),
            new Claim(ActorResolver.SessionSourceClaim, ActorResolver.InvitationSource)], Oidc));
        var limit = now + InvitationSessionLifetime;
        return (principal, tokenValidTo < limit ? tokenValidTo : limit);
    }

    public static void AddFoundationIdentity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection("Authentication:Oidc").Get<FederationSettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new();
        settings.ClientSecret = OperatorSecretReferences.Oidc(settings, environment);
        var localCa = configuration["DevelopmentLab:OidcCaPath"];
        var localPort = DevelopmentOidcTransport.ConfiguredPort(configuration);
        var selectedLocalPort = configuration.GetSection("DevelopmentLab").GetChildren()
            .Any(section => string.Equals(section.Key, "OidcPort", StringComparison.OrdinalIgnoreCase));
        if (selectedLocalPort && (!environment.IsDevelopment() || string.IsNullOrEmpty(localCa)))
            throw new InvalidOperationException("Local OIDC port selection requires explicit Development lab trust.");
        if (!string.IsNullOrEmpty(localCa) && (!settings.Configured || !string.Equals(settings.Issuer, settings.Authority, StringComparison.Ordinal)))
            throw new InvalidOperationException("Local OIDC trust requires a matching configured authority and issuer.");
        services.AddSingleton(settings);
        services.AddSingleton<ActorResolver>();
        services.AddSingleton<MembershipStateReader>();
        services.AddSingleton<ProvisionedIdentityLocator>();
        services.AddSingleton<InvitationAttemptLimiter>();
        services.AddSingleton(new InvitationIdentityReference(settings.ClientSecret));
        services.AddScoped<ICurrentMembershipResolver, CurrentMembershipResolver>();
        services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-CSRF-TOKEN";
            o.Cookie.Name = "__Host-poseidon-csrf";
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
        });
        var auth = services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = Cookie;
            o.DefaultSignInScheme = Cookie;
            o.DefaultChallengeScheme = Cookie; // APIs return 401; only /login initiates federation.
        }).AddCookie(Cookie, o =>
        {
            o.Cookie.Name = "__Host-poseidon-session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            o.SlidingExpiration = false;
            o.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; },
                OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; },
                OnValidatePrincipal = c =>
                {
                    // A provisioned session is revalidated by MembershipSessionGate against the database on every request.
                    var actors = c.HttpContext.RequestServices.GetRequiredService<ActorResolver>();
                    if (!settings.Configured || actors.Resolve(c.Principal!) == null && actors.ProvisionedIdentityFor(c.Principal!) == null &&
                        actors.InvitationIdentityFor(c.Principal!) == null)
                        c.RejectPrincipal();
                    return Task.CompletedTask;
                }
            };
        });
        if (settings.Configured) auth.AddOpenIdConnect(Oidc, o =>
        {
            if (!string.IsNullOrEmpty(localCa))
                o.BackchannelHttpHandler = DevelopmentOidcTransport.Create(
                    environment, settings.Authority!, localCa, localPort);
            o.Authority = settings.Authority;
            o.ClientId = settings.ClientId;
            o.ClientSecret = settings.ClientSecret;
            o.ResponseType = OpenIdConnectResponseType.Code;
            o.UsePkce = true;
            o.RequireHttpsMetadata = true;
            o.MapInboundClaims = false;
            o.SaveTokens = false;
            o.GetClaimsFromUserInfoEndpoint = false;
            // Default claim actions run after token validation. Keep the validated
            // issuer so the protected session still resolves its trusted membership.
            o.ClaimActions.Remove("iss");
            o.UseTokenLifetime = false;
            o.Scope.Clear();
            o.Scope.Add("openid");
            o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
            o.CorrelationCookie.SameSite = SameSiteMode.None;
            o.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
            o.NonceCookie.SameSite = SameSiteMode.None;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = settings.Issuer,
                ValidateAudience = true, ValidAudience = settings.ClientId,
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            o.Events = new OpenIdConnectEvents
            {
                OnTokenValidated = async c =>
                {
                    var actors = c.HttpContext.RequestServices.GetRequiredService<ActorResolver>();
                    var actor = actors.Resolve(c.Principal!);
                    var identity = actors.ConfiguredIdentityFor(c.Principal!);
                    if (identity == null && actor == null)
                    {
                        // USER-ADMIN-01: no operator entry ⇒ an admin-provisioned database membership may govern this
                        // IdP-validated identity (lookup keyed only by the validated issuer+subject).
                        var subjects = c.Principal!.FindAll("sub").ToArray();
                        var issuers = c.Principal!.FindAll("iss").ToArray();
                        if (subjects.Length == 1 && issuers.Length == 1 && !settings.Memberships.Any(x => x.Issuer == issuers[0].Value && x.Subject == subjects[0].Value))
                            identity = await c.HttpContext.RequestServices.GetRequiredService<ProvisionedIdentityLocator>()
                                .FindAsync(issuers[0].Value, subjects[0].Value, c.HttpContext.TraceIdentifier, c.HttpContext.RequestAborted);
                    }
                    if (identity == null && actor == null && InvitationSession(c.Principal!, c.Properties?.RedirectUri, settings,
                            new DateTimeOffset(c.SecurityToken.ValidTo, TimeSpan.Zero), c.HttpContext.UtcNow()) is { } invitation)
                    {
                        c.Principal = invitation.Principal;
                        c.Properties!.ExpiresUtc = invitation.ExpiresUtc;
                        c.Properties.IsPersistent = false;
                        return;
                    }
                    if (identity == null || !identity.Provisioned && (actor == null || !actor.Permissions.Contains("directory.read")))
                    {
                        c.Fail("Membership unavailable.");
                        return;
                    }
                    // PLAN-1511-5202: a persisted suspended/revoked membership or identity conflict
                    // denies new sessions; the current access revision is stamped into the session.
                    var persisted = await c.HttpContext.RequestServices.GetRequiredService<MembershipStateReader>()
                        .ReadAsync(identity, c.HttpContext.TraceIdentifier, c.HttpContext.RequestAborted);
                    if (persisted != null && (persisted.IdentityConflict || persisted.Status != Poseidon.Domain.MembershipStatus.Active) ||
                        identity.Provisioned && (persisted?.Grant is not { } grant || !TrustedPermissionsContain(grant.Permissions, "directory.read")))
                    {
                        c.Fail("Membership unavailable.");
                        return;
                    }
                    List<Claim> claims = [
                        new Claim("sub", c.Principal!.FindFirst("sub")!.Value),
                        new Claim("iss", c.Principal!.FindFirst("iss")!.Value),
                        new Claim(ClaimTypes.NameIdentifier, identity.ActorId.ToString("D")),
                        new Claim(MembershipSession.AccessRevisionClaim, (persisted?.AccessRevision ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    ];
                    if (identity.Provisioned)
                    {
                        claims.Add(new Claim(ActorResolver.ProvisionedTenantClaim, identity.TenantId.ToString("D")));
                        claims.Add(new Claim(ActorResolver.SessionSourceClaim, ActorResolver.ProvisionedSource));
                    }
                    c.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Oidc));
                    // Cookie must expire no later than the validated ID token, with a 30 minute ceiling.
                    var expiry = new DateTimeOffset(c.SecurityToken.ValidTo, TimeSpan.Zero);
                    var ceiling = c.HttpContext.UtcNow().AddMinutes(30);
                    c.Properties!.ExpiresUtc = expiry < ceiling ? expiry : ceiling;
                    c.Properties.IsPersistent = false;
                },
                OnTicketReceived = async c => await c.HttpContext.RequestServices.GetRequiredService<SecurityAudit>()
                    .WriteAsync(c.HttpContext, "Login", "Success", c.Principal),
                OnRemoteFailure = async c =>
                {
                    await c.HttpContext.RequestServices.GetRequiredService<SecurityAudit>().TryWriteAsync(c.HttpContext, "Login", "Failed");
                    c.HandleResponse();
                    c.Response.Redirect("/app?auth=failed");
                },
                OnAccessDenied = async c =>
                {
                    await c.HttpContext.RequestServices.GetRequiredService<SecurityAudit>().TryWriteAsync(c.HttpContext, "Login", "Denied");
                    c.HandleResponse();
                    c.Response.Redirect("/app?auth=denied");
                },
                OnRedirectToIdentityProviderForSignOut = c =>
                {
                    c.ProtocolMessage.ClientId = settings.ClientId;
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorizationBuilder().AddPolicy("AuditRead", policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(c => c.Resource is HttpContext http && http.RequestServices.GetRequiredService<ActorResolver>()
                .Resolve(c.User)?.CanReadAudit == true)).AddPolicy("RegulatoryDraftRead", policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(c => c.Resource is HttpContext http && http.RequestServices.GetRequiredService<ActorResolver>()
                .Resolve(c.User)?.CanReadRegulatory == true))
            .AddPolicy("DirectoryRead", policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(c => c.Resource is HttpContext http && http.RequestServices.GetRequiredService<ActorResolver>()
                .Resolve(c.User)?.CanRead == true))
            .AddPolicy("DirectoryWrite", policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(c => c.Resource is HttpContext http && http.RequestServices.GetRequiredService<ActorResolver>()
                    .Resolve(c.User)?.Permissions.Contains("directory.write") == true));
    }
}
