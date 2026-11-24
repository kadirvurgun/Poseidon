using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record SessionDto(bool Authenticated, bool SetupRequired, string? ActorId, Guid? TenantId, bool CanRead, string CsrfToken, string? TenantName = null, bool CanReadCompanies = false, bool CanCreate = false, bool CanSearch = false,
    // USER-ADMIN-02: an IdP-validated identity without membership signed in only to accept an invitation (grants nothing).
    bool InvitationSession = false);
public sealed record CompanyDto(Guid Id, string Code, string Name, uint Version, bool CanEdit);
public sealed record VesselDto(Guid Id, Guid CompanyId, string Code, string Name, string Kind, uint Version, bool CanEdit);
public sealed record PageDto<T>(T[] Items, int Offset, int Limit, bool HasMore);

public static class FoundationEndpoints
{
    // INVITE-RATELIMIT-FOLLOWUPS: fixed page (no request data echoed) for a rate-limited sign-in navigation.
    internal const string LoginLimitedPage = "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\">" +
        "<meta http-equiv=\"refresh\" content=\"0;url=/app?auth=request_limit\"><title>POSEIDON</title></head>" +
        "<body><a href=\"/app?auth=request_limit\">POSEIDON</a></body></html>";

    public static void MapFoundation(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/session", async (HttpContext http, FederationSettings settings, ActorResolver actors, IAntiforgery csrf, SecurityAudit audit) =>
        {
            var actor = actors.Resolve(http.User);
            string? tenantName = null;
            if (actor?.CanRead == true && (actor.TenantWideRead || actor.TenantWideManage))
                tenantName = await http.RequestServices.GetRequiredService<PoseidonDbContext>().Tenants.AsNoTracking()
                    .Select(x => x.Name).SingleOrDefaultAsync(http.RequestAborted);
            if (http.User.Identity?.IsAuthenticated == true) await audit.WriteAsync(http, "Session", actor == null ? "Denied" : "Success");
            var invitation = actor == null && actors.InvitationIdentityFor(http.User) != null;
            var token = actor == null && !invitation ? "" : csrf.GetAndStoreTokens(http).RequestToken!;
            return TypedResults.Ok(new SessionDto(http.User.Identity?.IsAuthenticated == true, !settings.Configured,
                actor?.Id.ToString(), actor?.TenantId, actor?.CanRead == true, token, tenantName, actor?.CanRead == true && (actor.TenantWideRead || actor.TenantWideManage), actor?.CanManage == true, actor?.CanSearch == true, invitation));
        }).WithName("GetSession");

        app.MapGet("/api/v1/auth/login", async (HttpContext http, FederationSettings settings, SecurityAudit audit, LoginRateLimiter limiter, string? returnUrl) =>
        {
            // INVITE-RATELIMIT-FOLLOWUPS: bounded per client network before any audit write or challenge; only the first
            // refused attempt of a window is audited (no amplification), every refusal carries Retry-After.
            var limit = await limiter.AcquireAsync(http.Connection.RemoteIpAddress, http.RequestAborted);
            if (!limit.Allowed)
            {
                RateLimitingRuntime.RetryAfter(http, limit);
                if (limit.Outcome == RateLimitOutcome.Limited) await audit.TryWriteAsync(http, "Login", "request_limit");
                // A browser navigation (sign-in link) gets the same 429 with a static page that returns to the app, which
                // shows the localized notice; API clients get the problem document.
                if (http.Request.Headers.Accept.Any(v => v != null && v.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
                    return Results.Content(LoginLimitedPage, "text/html; charset=utf-8", statusCode: 429);
                return Results.Problem(statusCode: 429, title: "request_limit", extensions: new Dictionary<string, object?>
                    { ["code"] = "request_limit", ["errors"] = new Dictionary<string, string[]>(), ["correlationId"] = http.TraceIdentifier });
            }
            if (!settings.Configured)
            {
                await audit.TryWriteAsync(http, "Login", "SetupRequired");
                return Results.Json(new { code = "identity_setup_required" }, statusCode: 503);
            }
            return Results.Challenge(new AuthenticationProperties { RedirectUri = FoundationIdentity.LocalReturn(returnUrl) }, [FoundationIdentity.Oidc]);
        }).WithName("Login").WithDescription("Starts the OIDC sign-in (302 to the identity provider). Repeated starts from one client network return 429 request_limit with Retry-After (a browser navigation receives a page that returns to /app?auth=request_limit).")
            .Produces(302).ProducesProblem(429).Produces(503);

        app.MapPost("/api/v1/auth/logout", async (HttpContext http, IAntiforgery csrf, SecurityAudit audit, FederationSettings settings) =>
        {
            try { await csrf.ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException)
            {
                await audit.WriteAsync(http, "Logout", "Denied");
                return Results.StatusCode(403);
            }
            await audit.WriteAsync(http, "Logout", "Success");
            await http.SignOutAsync(FoundationIdentity.Cookie);
            if (settings.Configured)
                return Results.SignOut(new AuthenticationProperties { RedirectUri = "/app" }, [FoundationIdentity.Oidc]);
            return Results.LocalRedirect("/app");
        }).RequireAuthorization().WithName("Logout").Produces(302).Produces(401).Produces(403).Produces(503);

        app.MapDirectoryCommands();
        app.MapWorkspace();

        var companies = app.MapGroup("/api/v1/companies").RequireAuthorization("DirectoryRead");
        companies.MapGet("", async (HttpContext http, PoseidonDbContext db, SecurityAudit audit, ActorResolver actors, int? offset, int? limit, string? q) =>
        {
            var (skip, take) = Bounds(offset, limit);
            if (q is { Length: > 100 } || q?.Any(char.IsControl) == true) { await audit.WriteAsync(http, "CompaniesRead", "Invalid"); return Results.BadRequest(new { code = "invalid_search" }); }
            var search = q?.Trim() ?? "";
            if (skip < 0) { await audit.WriteAsync(http, "CompaniesRead", "Invalid"); return Results.BadRequest(new { code = "invalid_pagination" }); }
            var actor = actors.Resolve(http.User)!;
            var rows = await db.Companies.AsNoTracking().Where(_ => actor.TenantWideRead || actor.TenantWideManage)
                .Where(x => search == "" || x.Code.Contains(search) || x.Name.Contains(search)).OrderBy(x => x.Code).ThenBy(x => x.Id).Skip(skip).Take(take + 1)
                .Select(x => new CompanyDto(x.Id, x.Code, x.Name, x.Version, actor.CanManage)).ToArrayAsync(http.RequestAborted);
            await audit.WriteAsync(http, "CompaniesRead", "Success");
            return Results.Ok(new PageDto<CompanyDto>(rows.Take(take).ToArray(), skip, take, rows.Length > take));
        }).WithName("ListCompanies").Produces<PageDto<CompanyDto>>().Produces(400).Produces(401).Produces(403).Produces(503);
        companies.MapGet("/{id:guid}", async (Guid id, HttpContext http, PoseidonDbContext db, SecurityAudit audit, ActorResolver actors) =>
        {
            var actor = actors.Resolve(http.User)!;
            var row = await db.Companies.AsNoTracking().Where(_ => actor.TenantWideRead || actor.TenantWideManage).Where(x => x.Id == id).Select(x => new CompanyDto(x.Id, x.Code, x.Name, x.Version, actor.CanManage)).SingleOrDefaultAsync(http.RequestAborted);
            await audit.WriteAsync(http, "CompaniesRead", row == null ? "NotFound" : "Success", resourceId: id);
            return row == null ? Results.NotFound() : Results.Ok(row);
        }).WithName("GetCompany").Produces<CompanyDto>().Produces(401).Produces(403).Produces(404).Produces(503);

        var vessels = app.MapGroup("/api/v1/vessels").RequireAuthorization("DirectoryRead");
        vessels.MapGet("", async (HttpContext http, PoseidonDbContext db, SecurityAudit audit, ActorResolver actors, int? offset, int? limit, string? kind, string? q) =>
        {
            var (skip, take) = Bounds(offset, limit);
            if (q is { Length: > 100 } || q?.Any(char.IsControl) == true) { await audit.WriteAsync(http, "VesselsRead", "Invalid"); return Results.BadRequest(new { code = "invalid_search" }); }
            var search = q?.Trim() ?? "";
            if (skip < 0 || (kind != null && kind is not ("Ship" or "Tug"))) { await audit.WriteAsync(http, "VesselsRead", "Invalid"); return Results.BadRequest(new { code = "invalid_filter" }); }
            var actor = actors.Resolve(http.User)!;
            var query = actor.Scope(db.Vessels.AsNoTracking());
            query = query.Where(x => search == "" || x.Code.Contains(search) || x.Name.Contains(search));
            if (kind != null) query = query.Where(x => x.Kind == (kind == "Ship" ? VesselKind.Ship : VesselKind.Tug));
            var rows = await query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip(skip).Take(take + 1)
                .Select(x => new VesselDto(x.Id, x.CompanyId, x.Code, x.Name, x.Kind.ToString(), x.Version, false)).ToArrayAsync(http.RequestAborted);
            await audit.WriteAsync(http, "VesselsRead", "Success");
            return Results.Ok(new PageDto<VesselDto>(rows.Take(take).Select(x => x with { CanEdit = actor.CanEditVessel(x.Id) }).ToArray(), skip, take, rows.Length > take));
        }).WithName("ListVessels").Produces<PageDto<VesselDto>>().Produces(400).Produces(401).Produces(403).Produces(503);
        vessels.MapGet("/{id:guid}", async (Guid id, HttpContext http, PoseidonDbContext db, SecurityAudit audit, ActorResolver actors) =>
        {
            var row = await actors.Resolve(http.User)!.Scope(db.Vessels.AsNoTracking()).Where(x => x.Id == id)
                .Select(x => new VesselDto(x.Id, x.CompanyId, x.Code, x.Name, x.Kind.ToString(), x.Version, false)).SingleOrDefaultAsync(http.RequestAborted);
            await audit.WriteAsync(http, "VesselsRead", row == null ? "NotFound" : "Success", resourceId: id);
            return row == null ? Results.NotFound() : Results.Ok(row with { CanEdit = actors.Resolve(http.User)!.CanEditVessel(row.Id) });
        }).WithName("GetVessel").Produces<VesselDto>().Produces(401).Produces(403).Produces(404).Produces(503);
    }

    private static (int Skip, int Take) Bounds(int? offset, int? limit) =>
        offset is < 0 or > 100000 || limit is < 1 or > 100 ? (-1, 0) : (offset ?? 0, limit ?? 25);
}
