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
    // DEV-101 SOF finalization / DEV-102 downtime review appointments (vessel-scoped duty).
    public ShipOperationsAuthority[] ShipOperationsAuthorities { get; set; } = [];
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
public sealed record ConfiguredIdentity(string Issuer, string Subject, Guid ActorId, Guid TenantId);

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
    public static string[] TrustedPermissions(string[]? permissions) => (permissions ?? []).Where(x => x is "crew.credential.verify" or "crew.competence.read" or "crew.competence.write" or "crew.competence.assess" or "crew.eligibility.read" or "crew.safe_manning.read" or "crew.safe_manning.decide" or "document.publish" or "evidence.verify" or "record.hold" or "record.retention.manage" or "record.purge.request" or "record.purge.approve" or "hr.legal_entity.read" or "hr.legal_entity.write" or "hr.legal_entity.identifier.read" or "hr.person.read" or "hr.person.write" or "hr.person.verify" or "hr.employment.read" or "hr.employment.write" or "hr.employment.approve" or "hr.assignment.read" or "hr.assignment.write" or "hr.assignment.approve" or "hr.actual.record" or "config.read" or "config.resolve" or "config.draft" or "config.approve" or "config.reject" or "hse.risk.read" or "hse.risk.write" or "hse.risk.approve" or "hse.risk.acknowledge" or "hse.high_risk_work.read" or "hse.risk_template.read" or "hse.risk_template.write" or "hse.risk_template.publish" or "hse.risk_template.suggest" or "hse.risk_method.write" or "hse.risk_method.publish" or "operations.downtime.read" or "operations.downtime.write" or "workorder.read" or "workorder.write" or "workorder.release" or "workorder.execute" or "workorder.verify" or "workorder.close" or "spare.read" or "spare.write" or "requisition.propose" or "requisition.approve" or "maintenance.measurement.write" or "maintenance.postponement.request" or "maintenance.postponement.decide" or "defect.read" or "defect.write" or "defect.verify" or "defect.effectiveness" or "defect.close" or "service.report.read" or "service.report.write" or "service.report.verify" or "operations.fuel.read" or "operations.fuel.write" or "operations.port.read" or "operations.port.write" or "certificate.verify.record" or "certificate.read" or "certificate.extract" or "certificate.write" or "certificate.renewal.write" or "certificate.renewal.owner" or "certificate.renewal.verify" or "certificate.renewal.complete" or "certificate.survey.plan" or "certificate.survey.review" or "daily.noon.review" or "daily.deck.review" or "daily.engine.review" or "daily.noon.read" or "daily.noon.write" or "daily.deck.read" or "daily.deck.write" or "daily.engine.read" or "daily.engine.write" or "system.health.read" or "notification.evaluate" or "notification.read" or "notification.manage" or "notification.acknowledge" or "notification.preference.write" or "crew.catalog.govern" or "crew.assessment.read" or "regulatory.qualify" or "regulatory.facts.attest" or "human.trace.read" or "human.review.read" or "human.review.submit" or "human.verify" or "human.decide" or "control.read" or "control.write" or "control.approve" or "control.owner.eligible" or "evidence.read" or "evidence.write" or "applicability.read" or "applicability.evaluate" or "applicability.override.author" or "applicability.override.approve" or "applicability.override.revoke" or "rules.write" or "rules.review" or "import.legacy.convert" or "crew.identity.convert" or "crew.documents.read" or "crew.documents.write" or "crew.position.write" or "maintenance.observation.write" or "crew.assignment.declare" or "import.read" or "import.write" or "crew.certificate.read" or "crew.certificate.write" or "crew.profile.identity.read" or "crew.profile.identity.write" or "crew.profile.contact.read" or "crew.profile.contact.write" or "crew.profile.medical.read" or "crew.profile.medical.write" or "crew.profile.employment.read" or "crew.profile.employment.write" or "mode.tug" or "mode.cross" or "tug.read" or "tug.write" or "tug.plan.write" or "tug.owner.eligible" or "crew.read" or "crew.write" or "crew.identity.read" or "crew.identity.write" or "crew.history.read" or "directory.read" or "directory.write" or "workspace.search" or "regulatory.draft.read" or "regulatory.draft.write" or "audit.read" or "file.read" or "file.write" or "equipment.read" or "equipment.write" or "equipment.history.read" or "maintenance.read" or "maintenance.write" or "maintenance.history.read" or "maintenance.owner.eligible" or "membership.read" or "membership.manage" or "membership.security.suspend").Distinct().ToArray();
    public TrustedActor? Resolve(ClaimsPrincipal principal)
    {
        if (Configured(principal) is not { } m) return null;
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
    public const string Cookie = "poseidon-session";
    public const string Oidc = "oidc";
    public static string LocalReturn(string? value) => value != null && value.StartsWith("/app", StringComparison.Ordinal) &&
        (value.Length == 4 || value[4] is '/' or '?' or '#') && value.Length <= 512 &&
        !value.Contains('%') && !value.Contains('\\') && !value.Split('?', '#')[0].Split('/').Any(x => x is "." or "..") &&
        !value.Any(char.IsControl) ? value : "/app";

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
                    if (!settings.Configured || c.HttpContext.RequestServices.GetRequiredService<ActorResolver>().Resolve(c.Principal!) == null)
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
                    if (actor == null || !actor.Permissions.Contains("directory.read") || actors.ConfiguredIdentityFor(c.Principal!) is not { } identity)
                    {
                        c.Fail("Membership unavailable.");
                        return;
                    }
                    // PLAN-1511-5202: a persisted suspended/revoked membership or identity conflict
                    // denies new sessions; the current access revision is stamped into the session.
                    var persisted = await c.HttpContext.RequestServices.GetRequiredService<MembershipStateReader>()
                        .ReadAsync(identity, c.HttpContext.TraceIdentifier, c.HttpContext.RequestAborted);
                    if (persisted != null && (persisted.IdentityConflict || persisted.Status != Poseidon.Domain.MembershipStatus.Active))
                    {
                        c.Fail("Membership unavailable.");
                        return;
                    }
                    c.Principal = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("sub", c.Principal!.FindFirst("sub")!.Value),
                        new Claim("iss", c.Principal!.FindFirst("iss")!.Value),
                        new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                        new Claim(MembershipSession.AccessRevisionClaim, (persisted?.AccessRevision ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    ], Oidc));
                    // Cookie must expire no later than the validated ID token, with a 30 minute ceiling.
                    var expiry = new DateTimeOffset(c.SecurityToken.ValidTo, TimeSpan.Zero);
                    c.Properties!.ExpiresUtc = expiry < DateTimeOffset.UtcNow.AddMinutes(30) ? expiry : DateTimeOffset.UtcNow.AddMinutes(30);
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
