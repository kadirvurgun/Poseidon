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
    public Membership[] Memberships { get; set; } = [];
    public bool Configured => Uri.TryCreate(Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        !string.IsNullOrWhiteSpace(Issuer) && !string.IsNullOrWhiteSpace(ClientId);
}

// Provisioned by the server operator, never populated from request binding or IdP role claims.
public sealed record Membership(string Issuer, string Subject, Guid ActorId, Guid TenantId, string[] Permissions, bool TenantWideRead = false, bool TenantWideManage = false, VesselGrant[]? VesselGrants = null)
{
    // ConfigurationBinder must use property binding. Constructor binding can silently
    // omit an array member whose optional collection is an explicit empty JSON array.
    public Membership() : this("", "", Guid.Empty, Guid.Empty, []) { }
}
public sealed record VesselGrant(Guid VesselId, bool CanEdit = false);
public sealed record TrustedActor(Guid Id, Guid TenantId, string[] Permissions, bool TenantWideRead, bool TenantWideManage, VesselGrant[] VesselGrants)
{
    // Explicit tenant-local capability. Vessel-only grants and directory writes confer no catalog authority.
    public bool CanReadRegulatory => CanRead && (TenantWideRead || TenantWideManage) && Permissions.Contains("regulatory.draft.read");
    public bool CanWriteRegulatory => CanReadRegulatory && Permissions.Contains("regulatory.draft.write");
    public bool CanRead => Permissions.Contains("directory.read") && (TenantWideRead || TenantWideManage || VesselGrants.Length > 0);
    public bool CanManage => CanRead && TenantWideManage && Permissions.Contains("directory.write");
    public bool CanSearch => CanRead && Permissions.Contains("workspace.search");
    public bool CanEditVessel(Guid id) => CanRead && Permissions.Contains("directory.write") &&
        (TenantWideManage || VesselGrants.Any(g => g.VesselId == id && g.CanEdit));
    public IQueryable<Poseidon.Persistence.Vessel> Scope(IQueryable<Poseidon.Persistence.Vessel> query)
    {
        if (!CanRead) return query.Where(_ => false);
        if (TenantWideRead || TenantWideManage) return query;
        var ids = VesselGrants.Select(g => g.VesselId).ToArray();
        return query.Where(v => ids.Contains(v.Id));
    }
}

public sealed class ActorResolver(FederationSettings settings)
{
    public TrustedActor? Resolve(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var subjects = principal.FindAll("sub").ToArray();
        var issuers = principal.FindAll("iss").ToArray();
        if (subjects.Length != 1 || issuers.Length != 1) return null;
        var matches = settings.Memberships.Where(x => x.Issuer == issuers[0].Value && x.Subject == subjects[0].Value).ToArray();
        if (matches.Length != 1 || matches[0].ActorId == Guid.Empty || matches[0].TenantId == Guid.Empty) return null;
        var m = matches[0];
        return new(m.ActorId, m.TenantId, (m.Permissions ?? []).Where(x => x is "directory.read" or "directory.write" or "workspace.search" or "regulatory.draft.read" or "regulatory.draft.write").Distinct().ToArray(),
            m.TenantWideRead, m.TenantWideManage, (m.VesselGrants ?? []).Where(g => g.VesselId != Guid.Empty).ToArray());
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
        services.AddSingleton(settings);
        services.AddSingleton<ActorResolver>();
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
            var localCa = configuration["DevelopmentLab:OidcCaPath"];
            if (!string.IsNullOrEmpty(localCa))
                o.BackchannelHttpHandler = DevelopmentOidcTransport.Create(
                    environment, settings.Authority!, localCa);
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
                OnTokenValidated = c =>
                {
                    var actor = c.HttpContext.RequestServices.GetRequiredService<ActorResolver>().Resolve(c.Principal!);
                    if (actor == null || !actor.Permissions.Contains("directory.read"))
                    {
                        c.Fail("Membership unavailable.");
                        return Task.CompletedTask;
                    }
                    c.Principal = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("sub", c.Principal!.FindFirst("sub")!.Value),
                        new Claim("iss", c.Principal!.FindFirst("iss")!.Value),
                        new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString())
                    ], Oidc));
                    // Cookie must expire no later than the validated ID token, with a 30 minute ceiling.
                    var expiry = new DateTimeOffset(c.SecurityToken.ValidTo, TimeSpan.Zero);
                    c.Properties!.ExpiresUtc = expiry < DateTimeOffset.UtcNow.AddMinutes(30) ? expiry : DateTimeOffset.UtcNow.AddMinutes(30);
                    c.Properties.IsPersistent = false;
                    return Task.CompletedTask;
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
        services.AddAuthorizationBuilder().AddPolicy("RegulatoryDraftRead", policy => policy.RequireAuthenticatedUser()
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
