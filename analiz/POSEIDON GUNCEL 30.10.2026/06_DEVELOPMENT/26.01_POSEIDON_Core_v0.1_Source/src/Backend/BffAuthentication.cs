using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Poseidon;

// Deployment must explicitly supply reviewed implementations. There is NO runtime fallback.
// Store requires unpredictable handles, expiry, revocation, protected storage and multi-node semantics.
public interface IBffInfrastructure
{
    ITicketStore Tickets { get; }
    IDataProtectionProvider DataProtection { get; }
    bool IsEphemeral { get; }
}
// Future refresh/proxy contract only; no tokens are retained or forwarded by this slice.
public interface IBffTokenStore
{
    Task StoreAsync(string sessionHandle, string accessToken, string? refreshToken, DateTimeOffset expires, CancellationToken ct);
    Task RemoveAsync(string sessionHandle, CancellationToken ct);
}
public sealed class BffSettings
{
    public bool Enabled { get; set; }
    public string? Authority { get; set; }
    public string? Issuer { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Origin { get; set; }
    public void Validate(bool synthetic)
    {
        if (!Enabled) return;
        IdentityBoundary.RequireHttps(Authority);
        IdentityBoundary.RequireHttps(Issuer);
        IdentityBoundary.RequireHttps(Origin);
        var origin = new Uri(Origin!);
        if (synthetic || origin.AbsolutePath != "/" || string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
            throw new InvalidOperationException("BFF requires a confidential OIDC client, exact HTTPS origin and non-synthetic mode.");
    }
}

public static class BffAuthentication
{
    public const string CookieScheme = "Poseidon.Bff.Session";
    public const string OidcScheme = "Poseidon.Bff.Oidc";
    public static IServiceCollection AddPoseidonBff(this IServiceCollection services, IConfiguration configuration, bool synthetic, DevelopmentUat? uat = null)
    {
        var settings = configuration.GetSection("Bff").Get<BffSettings>() ?? new();
        if (uat?.Enabled == true) settings = new BffSettings { Enabled = true, Origin = uat.Origin, Issuer = DevelopmentUat.Issuer };
        else settings.Validate(synthetic);
        if (settings.Enabled && string.Equals(settings.ClientId, configuration["Authentication:Audience"], StringComparison.Ordinal) && uat?.Enabled != true)
            throw new InvalidOperationException("BFF client ID must differ from API access-token audience.");
        services.AddSingleton(settings);
        if (!settings.Enabled) return services;
        services.AddAuthorization(o => o.AddPolicy("bff-draft-write", p => p.AddAuthenticationSchemes(CookieScheme).RequireAuthenticatedUser().RequireClaim("permission", "draft:write")));
        // Overriding the provider prevents implicit filesystem key generation. Resolution fails without infrastructure.
        services.AddSingleton<IDataProtectionProvider>(sp => sp.GetRequiredService<IBffInfrastructure>().DataProtection);
        services.AddAntiforgery(o => {
            o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "__Host-Poseidon.Csrf";
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
        });
        var authentication = services.AddAuthentication().AddCookie(CookieScheme, o => {
            o.Cookie.Name = "__Host-Poseidon.Session"; o.Cookie.Path = "/"; o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(20); o.SlidingExpiration = false;
            o.Events.OnRedirectToLogin = c => {
                UatSessionDiagnostics.Write(c.HttpContext, c.Request.Cookies.ContainsKey("__Host-Poseidon.Session")
                    ? UatSessionEvent.AUTH_401_COOKIE_PRESENT : UatSessionEvent.AUTH_401_COOKIE_ABSENT);
                c.Response.StatusCode = 401; return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
            o.Events.OnValidatePrincipal = async c => {
                try {
                    var identity = IdentityBoundary.Read(c.Principal!, settings.Issuer!);
                    var grant = await c.HttpContext.RequestServices.GetRequiredService<IIdentityGrantResolver>()
                        .ResolveAsync(identity, c.HttpContext.RequestAborted);
                    var mapped = IdentityBoundary.Map(identity, grant);
                    if (uat?.Enabled == true) ((System.Security.Claims.ClaimsIdentity)mapped.Identity!).AddClaim(new("development_uat", "true"));
                    c.ReplacePrincipal(mapped);
                } catch {
                    UatSessionDiagnostics.Write(c.HttpContext, UatSessionEvent.PRINCIPAL_REJECTED);
                    c.RejectPrincipal();
                    await c.HttpContext.SignOutAsync(CookieScheme);
                }
            };
        });
        if (uat?.Enabled != true) authentication.AddOpenIdConnect(OidcScheme, o => {
            o.SignInScheme = CookieScheme; o.Authority = settings.Authority; o.ClientId = settings.ClientId;
            o.ClientSecret = settings.ClientSecret; o.RequireHttpsMetadata = true; o.MapInboundClaims = false;
            o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always; o.CorrelationCookie.HttpOnly = true;
            o.NonceCookie.SecurePolicy = CookieSecurePolicy.Always; o.NonceCookie.HttpOnly = true;
            o.ResponseType = OpenIdConnectResponseType.Code; o.UsePkce = true; o.SaveTokens = false;
            o.Scope.Clear(); o.Scope.Add("openid"); o.CallbackPath = "/bff/callback";
            // The handler runs claim actions AFTER OnTokenValidated. Our principal is already
            // allowlisted by the server resolver: preserve its issuer and do not remap IdP claims.
            o.ClaimActions.Clear();
            // Remote logout is intentionally not exposed in this slice.
            o.RemoteSignOutPath = PathString.Empty; o.SignedOutCallbackPath = PathString.Empty;
            o.TokenValidationParameters = new TokenValidationParameters {
                ValidateIssuer = true, ValidIssuer = settings.Issuer, ValidateAudience = true,
                ValidAudience = settings.ClientId, ValidateLifetime = true, RequireSignedTokens = true, RequireExpirationTime = true, ValidateIssuerSigningKey = true
            };
            o.Events.OnTokenValidated = async c => {
                try {
                    var identity = IdentityBoundary.Read(c.Principal!, settings.Issuer!);
                    var grant = await c.HttpContext.RequestServices.GetRequiredService<IIdentityGrantResolver>()
                        .ResolveAsync(identity, c.HttpContext.RequestAborted);
                    c.Principal = IdentityBoundary.Map(identity, grant);
                } catch { c.Fail("Identity grant rejected."); }
            };
            o.Events.OnRemoteFailure = async c => {
                c.HandleResponse(); c.Response.StatusCode = 401; c.Response.ContentType = "text/html; charset=utf-8";
                // Fixed bilingual failure page: no reflected protocol details or automatic redirect loop.
                await c.Response.WriteAsync("""<!doctype html><html lang=tr><meta charset=utf-8><title>Poseidon — Giriş / Sign-in</title><h1>Giriş doğrulanamadı.</h1><p lang=en>Sign-in could not be verified.</p><a href="/">Giriş sayfasına dön / Return to sign-in</a></html>""");
            };
        });
        services.AddOptions<CookieAuthenticationOptions>(CookieScheme).Configure<IBffInfrastructure>((o, infrastructure) => {
            o.SessionStore = infrastructure.Tickets ?? throw new InvalidOperationException("Server ticket store required.");
        });
        return services;
    }

    public static void ValidatePoseidonBff(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<BffSettings>().Enabled) return;
        var infrastructure = app.Services.GetRequiredService<IBffInfrastructure>();
        if (infrastructure.IsEphemeral && !app.Environment.IsEnvironment("Testing") && !(app.Environment.IsDevelopment() && app.Services.GetRequiredService<DevelopmentUat>().Enabled && infrastructure is DevelopmentSessions))
            throw new InvalidOperationException("Ephemeral BFF infrastructure is test-only.");
        _ = infrastructure.DataProtection ?? throw new InvalidOperationException("Externally configured data protection required.");
        _ = app.Services.GetRequiredService<IIdentityGrantResolver>();
        _ = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieScheme).SessionStore
            ?? throw new InvalidOperationException("Server ticket store required.");
        // Runs before authentication handlers, including the OIDC callback.
        app.Use(async (context, next) => {
            if (context.Request.Path.StartsWithSegments("/bff")) {
                var settings = app.Services.GetRequiredService<BffSettings>();
                var expected = new Uri(settings.Origin!);
                if (!context.Request.IsHttps || !string.Equals(context.Request.Host.Value, expected.Authority, StringComparison.OrdinalIgnoreCase)) {
                    context.Response.StatusCode = 400; return;
                }
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = app.Services.GetRequiredService<DevelopmentUat>().Enabled ? "same-origin" : "no-referrer";
            }
            await next(context);
        });
    }

    public static void MapPoseidonBff(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<BffSettings>().Enabled) return;
        app.MapGet("/bff/antiforgery", async (HttpContext c, IAntiforgery csrf) => {
            var session = await c.AuthenticateAsync(CookieScheme);
            if (session.Succeeded) c.User = session.Principal!;
            return Results.Ok(new { requestToken = csrf.GetAndStoreTokens(c).RequestToken });
        }).AllowAnonymous();
        app.MapPost("/bff/login", async (HttpContext c, IAntiforgery csrf, string? returnUrl) => {
            var session = await c.AuthenticateAsync(CookieScheme);
            if (session.Succeeded) c.User = session.Principal!;
            if (!await ValidCsrf(c, csrf)) {
                UatSessionDiagnostics.Write(c, UatSessionEvent.LOGIN_CSRF_REJECTED);
                return Results.BadRequest();
            }
            var target = returnUrl ?? "/";
            if (!IdentityBoundary.IsLocalReturnUrl(target)) return Results.BadRequest();
            if (app.Services.GetRequiredService<DevelopmentUat>().Enabled) {
                // Reject replacement of an active session; logout is scope-bound and CSRF protected.
                if (session.Succeeded) {
                    UatSessionDiagnostics.Write(c, UatSessionEvent.LOGIN_EXISTING_SESSION, session.Properties);
                    return Results.Conflict();
                }
                var identity = new ExternalIdentity(DevelopmentUat.Issuer, "uat-manager");
                var grant = await c.RequestServices.GetRequiredService<IIdentityGrantResolver>().ResolveAsync(identity, c.RequestAborted);
                var principal = IdentityBoundary.Map(identity, grant);
                ((System.Security.Claims.ClaimsIdentity)principal.Identity!).AddClaim(new("development_uat", "true"));
                await c.SignInAsync(CookieScheme, principal, new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20), IsPersistent = false });
                UatSessionDiagnostics.Write(c, UatSessionEvent.LOGIN_CREATED);
                // UAT fetch never follows an authentication redirect. Real OIDC below
                // retains top-level navigation. No token or redirect URL enters JavaScript.
                if (c.Request.Headers[DevelopmentUat.BrowserOriginHeader].ToString() ==
                    app.Services.GetRequiredService<DevelopmentUat>().Origin) return Results.NoContent();
                return Results.LocalRedirect(target);
            }
            return Results.Challenge(new AuthenticationProperties { RedirectUri = target }, [OidcScheme]);
        }).AllowAnonymous();
        app.MapGet("/bff/session", (HttpContext c) => Results.Ok(Contracts.Scope(c.User)))
            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { AuthenticationSchemes = CookieScheme });
        app.MapPost("/bff/logout", async (HttpContext c, IAntiforgery csrf) => {
            if (!await ValidCsrf(c, csrf)) return Results.BadRequest();
            if (c.Request.Headers["X-Poseidon-Scope"].ToString() != IdentityBoundary.ScopeStamp(Contracts.Scope(c.User)))
                return Results.Json(new { code = "logout_scope_changed", message = "Displayed session no longer matches the current session. Logout was not performed." }, statusCode: 409);
            await c.SignOutAsync(CookieScheme);
            UatSessionDiagnostics.Write(c, UatSessionEvent.LOGOUT_COMPLETED);
            return Results.NoContent();
        }).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { AuthenticationSchemes = CookieScheme });
    }
    private static async Task<bool> ValidCsrf(HttpContext c, IAntiforgery csrf)
    {
        try { await csrf.ValidateRequestAsync(c); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}
