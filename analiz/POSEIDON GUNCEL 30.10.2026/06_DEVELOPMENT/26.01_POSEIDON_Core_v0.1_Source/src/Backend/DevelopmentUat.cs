using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Poseidon;

// Separate opt-in synthetic runtime. Never uses test assemblies, real accounts or persisted keys.
public sealed class DevelopmentUat
{
    public bool Enabled { get; set; }
    public string? Origin { get; set; }
    public string? StaticRoot { get; set; }
    public const string BrowserOriginHeader = "X-Poseidon-Uat-Origin";
    public const string Issuer = "https://poseidon-synthetic.invalid";
    public static DevelopmentUat Configure(WebApplicationBuilder builder)
    {
        var s = builder.Configuration.GetSection("DevelopmentUat").Get<DevelopmentUat>() ?? new();
        if (s.Enabled) {
            IdentityBoundary.RequireHttps(s.Origin);
            var uri = new Uri(s.Origin!);
            if (!builder.Environment.IsDevelopment() || uri.GetLeftPart(UriPartial.Authority) != s.Origin ||
                uri.Port != 443 || (uri.Host != "localhost" && !System.Text.RegularExpressions.Regex.IsMatch(uri.Host, @"^[a-z0-9]+(?:-[a-z0-9]+)*-8080\.app\.github\.dev$")) ||
                builder.Configuration.GetValue<bool>("SyntheticIdentity:Enabled") || builder.Configuration.GetValue<bool>("Bff:Enabled") ||
                builder.Configuration.GetValue<bool>("EvidenceIntake:Enabled") || string.IsNullOrWhiteSpace(s.StaticRoot) ||
                !File.Exists(Path.Combine(s.StaticRoot, "index.html")))
                throw new InvalidOperationException("Development UAT requires an exact reviewed HTTPS origin, built static UI, Development and no other identity/evidence mode.");
            // DataProtectionHostedService may warm the framework key ring independently
            // of IDataProtectionProvider. Keep that repository in RAM too: no implicit keys on disk.
            builder.Services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(o => o.XmlRepository = new DevelopmentKeyRepository());
            builder.Services.AddSingleton<IBffInfrastructure, DevelopmentSessions>();
            builder.Services.AddSingleton<IIdentityGrantResolver, DevelopmentGrants>();
        }
        builder.Services.AddSingleton(s);
        return s;
    }
    public void UseBoundary(WebApplication app)
    {
        if (!Enabled) return;
        var expected = new Uri(Origin!);
        UatSessionDiagnostics.Write(app.Logger, UatSessionEvent.PROCESS_STARTED);
        app.Use(async (c, next) => {
            // The one listener is loopback-only. Never interpret arbitrary forwarded headers.
            void Reject(string reason) {
                // Fixed enums only: never interpolate headers, paths, queries, cookies or tokens.
                var stage = c.Request.Method == "POST" && c.Request.Path == "/bff/login" ? "LOGIN" : "OTHER_REQUEST";
                app.Logger.LogWarning(new EventId(4811, "DevelopmentUatBoundaryRejected"), "POSEIDON_UAT_REJECT {Stage} {Reason}", stage, reason);
                c.Response.StatusCode = 400;
            }
            if (c.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)) {
                Reject("PEER_NOT_LOOPBACK"); return;
            }
            if (!string.Equals(c.Request.Host.Value, expected.Authority, StringComparison.OrdinalIgnoreCase) &&
                !(expected.Host.EndsWith("-8080.app.github.dev", StringComparison.Ordinal) && c.Request.Host.Value == "localhost:8080")) {
                Reject("HOST_NOT_ALLOWED"); return;
            }
            // A custom browser header is non-safelisted: cross-origin script needs a
            // successful CORS preflight. This application grants no CORS access.
            // It is NOT proxy authentication. CSRF validation remains mandatory downstream.
            var browserOrigin = c.Request.Headers[BrowserOriginHeader];
            var transformed = expected.Host.EndsWith("-8080.app.github.dev", StringComparison.Ordinal) &&
                c.Request.Host.Value == "localhost:8080" &&
                c.Request.Headers.Origin.Count == 1 && c.Request.Headers.Origin.ToString() == "https://localhost:8080" &&
                browserOrigin.Count == 1 && browserOrigin.ToString() == Origin;
            if (c.Request.Method is not ("GET" or "HEAD" or "OPTIONS") &&
                (c.Request.Headers.Origin.ToString() != Origin && !transformed ||
                 browserOrigin.Count > 0 && (browserOrigin.Count != 1 || browserOrigin.ToString() != Origin))) {
                var values = c.Request.Headers.Origin;
                var reason = values.Count > 1 ? "ORIGIN_MULTIPLE" : values.ToString() switch {
                    "" => "ORIGIN_MISSING",
                    "null" => "ORIGIN_NULL",
                    "http://localhost:8080" => "ORIGIN_LOCAL_HTTP_8080",
                    "https://localhost:8080" => "ORIGIN_LOCAL_HTTPS_8080",
                    _ => "ORIGIN_OTHER"
                };
                Reject(reason); return;
            }
            // Codespaces rewrites Host to this observed local transport authority.
            // Canonical authority comes ONLY from validated configuration, never forwarded headers.
            c.Request.Host = new HostString(expected.Authority);
            c.Request.Scheme = "https"; // Explicit development proxy contract, not TLS detection.
            c.Response.Headers.CacheControl = "no-store";
            c.Response.Headers["Referrer-Policy"] = "same-origin";
            c.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await next(c);
        });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(StaticRoot!)) });
    }
    public void MapUi(WebApplication app)
    {
        if (!Enabled) return;
        app.MapFallback((HttpContext c) => c.Request.Path.StartsWithSegments("/api") || c.Request.Path.StartsWithSegments("/bff")
            ? Results.NotFound() : Results.File(Path.Combine(Path.GetFullPath(StaticRoot!), "index.html"), "text/html")).AllowAnonymous();
    }
}

public sealed class DevelopmentGrants : IIdentityGrantResolver
{
    public Task<IdentityGrant?> ResolveAsync(ExternalIdentity identity, CancellationToken ct) => Task.FromResult<IdentityGrant?>(
        identity == new ExternalIdentity(DevelopmentUat.Issuer, "uat-manager")
        ? new(identity, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001"), [], true, true, true, false) : null);
}

public sealed class DevelopmentSessions(ILogger<DevelopmentSessions>? logger = null) : IBffInfrastructure, ITicketStore
{
    private readonly ConcurrentDictionary<string, AuthenticationTicket> tickets = new();
    private readonly object gate = new();
    public ITicketStore Tickets => this;
    public IDataProtectionProvider DataProtection { get; } = new EphemeralDataProtectionProvider();
    public bool IsEphemeral => true;
    public Task<string> StoreAsync(AuthenticationTicket ticket) { lock (gate) {
        foreach (var item in tickets.Where(x => x.Value.Properties.ExpiresUtc <= DateTimeOffset.UtcNow)) tickets.TryRemove(item.Key, out _);
        if (tickets.Count >= 100) throw new InvalidOperationException("Synthetic session capacity exceeded.");
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); tickets[key] = ticket;
        if (logger is not null) UatSessionDiagnostics.Write(logger, UatSessionEvent.TICKET_CREATED, ticket.Properties);
        return Task.FromResult(key);
    } }
    public Task RenewAsync(string key, AuthenticationTicket ticket) { if (tickets.ContainsKey(key)) tickets[key] = ticket; return Task.CompletedTask; }
    public Task RemoveAsync(string key) { if (tickets.TryRemove(key, out var removed) && logger is not null) UatSessionDiagnostics.Write(logger, UatSessionEvent.TICKET_REMOVED, removed.Properties); return Task.CompletedTask; }
    public Task<AuthenticationTicket?> RetrieveAsync(string key) {
        var found = tickets.TryGetValue(key, out var ticket);
        if (found && ticket!.Properties.ExpiresUtc > DateTimeOffset.UtcNow) return Task.FromResult<AuthenticationTicket?>(ticket);
        if (logger is not null) UatSessionDiagnostics.Write(logger, found ? UatSessionEvent.TICKET_EXPIRED : UatSessionEvent.TICKET_MISSING, ticket?.Properties);
        tickets.TryRemove(key, out _); return Task.FromResult<AuthenticationTicket?>(null);
    }
}
// Default API authentication in this isolated mode rejects bearer and cookie credentials.
public sealed class DevelopmentDeniedHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}

internal sealed class DevelopmentKeyRepository : Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository
{
    private readonly List<System.Xml.Linq.XElement> elements = new();
    public IReadOnlyCollection<System.Xml.Linq.XElement> GetAllElements() { lock(elements) return elements.Select(e => new System.Xml.Linq.XElement(e)).ToArray(); }
    public void StoreElement(System.Xml.Linq.XElement element, string friendlyName) { lock(elements) elements.Add(new(element)); }
}
