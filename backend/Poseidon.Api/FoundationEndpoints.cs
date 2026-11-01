using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record SessionDto(bool Authenticated, bool SetupRequired, string? ActorId, Guid? TenantId, bool CanRead, string CsrfToken, string? TenantName = null, bool CanReadCompanies = false, bool CanCreate = false, bool CanSearch = false);
public sealed record CompanyDto(Guid Id, string Code, string Name, uint Version, bool CanEdit);
public sealed record VesselDto(Guid Id, Guid CompanyId, string Code, string Name, string Kind, uint Version, bool CanEdit);
public sealed record PageDto<T>(T[] Items, int Offset, int Limit, bool HasMore);

public static class FoundationEndpoints
{
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
            var token = actor == null ? "" : csrf.GetAndStoreTokens(http).RequestToken!;
            return TypedResults.Ok(new SessionDto(http.User.Identity?.IsAuthenticated == true, !settings.Configured,
                actor?.Id.ToString(), actor?.TenantId, actor?.CanRead == true, token, tenantName, actor?.CanRead == true && (actor.TenantWideRead || actor.TenantWideManage), actor?.CanManage == true, actor?.CanSearch == true));
        }).WithName("GetSession");

        app.MapGet("/api/v1/auth/login", async (HttpContext http, FederationSettings settings, SecurityAudit audit, string? returnUrl) =>
        {
            if (!settings.Configured)
            {
                await audit.TryWriteAsync(http, "Login", "SetupRequired");
                return Results.Json(new { code = "identity_setup_required" }, statusCode: 503);
            }
            return Results.Challenge(new AuthenticationProperties { RedirectUri = FoundationIdentity.LocalReturn(returnUrl) }, [FoundationIdentity.Oidc]);
        }).WithName("Login").Produces(302).Produces(503);

        app.MapPost("/api/v1/auth/logout", async (HttpContext http, IAntiforgery csrf, SecurityAudit audit, FederationSettings settings) =>
        {
            try { await csrf.ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException) { return Results.StatusCode(403); }
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
            if (q is { Length: > 100 } || q?.Any(char.IsControl) == true) return Results.BadRequest(new { code = "invalid_search" });
            var search = q?.Trim() ?? "";
            if (skip < 0) return Results.BadRequest(new { code = "invalid_pagination" });
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
            if (q is { Length: > 100 } || q?.Any(char.IsControl) == true) return Results.BadRequest(new { code = "invalid_search" });
            var search = q?.Trim() ?? "";
            if (skip < 0 || (kind != null && kind is not ("Ship" or "Tug"))) return Results.BadRequest(new { code = "invalid_filter" });
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
