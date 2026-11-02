using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record WorkspaceRoleDto(Guid Reference, string Tr, string En);
public sealed record WorkspaceRoleContextDto(string State, string? CatalogVersion, WorkspaceRoleDto[] Roles);
public sealed record WorkspaceContextDto(Guid TenantId, Guid ActorId, WorkspaceRoleContextDto RoleContext, VesselDto? Vessel);

// Labels resolve ONLY existing server-provisioned role references. They confer no
// permissions, appointments, delegation or safety authority.
public static class WorkspaceRoleContext
{
    public static WorkspaceRoleContextDto Describe(Guid tenant, string? roleContext, IConfiguration config)
    {
        WorkspaceRoleContextDto Unavailable() => new("Unavailable", null, []);
        if (roleContext == null) return new("Missing", null, []);
        var normalized = ActorResolver.TrustedRoleContext(roleContext.Split(','));
        if (normalized == null || normalized != roleContext) return Unavailable();
        var catalog = config.GetSection("WorkspaceContext:RoleCatalog");
        var version = catalog["Version"];
        static bool Text(string? value, int max) => value is { Length: > 0 } && value.Length <= max &&
            value.Trim() == value && !value.Any(char.IsControl);
        if (!Text(version, 64)) return Unavailable();
        // Technical configuration bound, not an operational catalog size rule.
        var rows = catalog.GetSection("Roles").GetChildren().Take(4097).ToArray();
        if (rows.Length > 4096) return Unavailable();
        var local = new Dictionary<Guid, WorkspaceRoleDto>();
        foreach (var row in rows) {
            if (!Guid.TryParse(row["TenantId"], out var tenantId) || tenantId != tenant) continue;
            if (!Guid.TryParse(row["Reference"], out var reference) || reference == Guid.Empty ||
                !Text(row["Tr"], 200) || !Text(row["En"], 200) ||
                !local.TryAdd(reference, new(reference, row["Tr"]!, row["En"]!))) return Unavailable();
        }
        var references = normalized.Split(',').Select(Guid.Parse).ToArray();
        if (references.Any(id => !local.ContainsKey(id))) return Unavailable();
        return new("Configured", version, references.Select(id => local[id]).ToArray());
    }
}

public static class WorkspaceContextEndpoints
{
    public static void MapWorkspaceContext(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/workspace/context", (HttpContext h, ActorResolver actors, PoseidonDbContext db,
            SecurityAudit audit, IConfiguration config) => Read(null, h, actors, db, audit, config))
            .RequireAuthorization("DirectoryRead").WithName("GetWorkspaceContext").Produces<WorkspaceContextDto>()
            .Produces(400).Produces(401).Produces(403).Produces(503);
        app.MapGet("/api/v1/workspace/context/{vessel:guid}", (Guid vessel, HttpContext h, ActorResolver actors,
            PoseidonDbContext db, SecurityAudit audit, IConfiguration config) => Read(vessel, h, actors, db, audit, config))
            .RequireAuthorization("DirectoryRead").WithName("GetVesselWorkspaceContext").Produces<WorkspaceContextDto>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
    }

    private static async Task<IResult> Read(Guid? vessel, HttpContext h, ActorResolver actors,
        PoseidonDbContext db, SecurityAudit audit, IConfiguration config)
    {
        h.Response.Headers.CacheControl = "no-store"; h.Response.Headers.Pragma = "no-cache";
        if (h.Request.Query.Count != 0) {
            await audit.WriteAsync(h, "WorkspaceRead", "Invalid");
            return Results.BadRequest(new { code = "invalid_request" });
        }
        var actor = actors.Resolve(h.User)!;
        VesselDto? context = null;
        if (vessel != null) {
            var row = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == vessel, h.RequestAborted);
            if (row == null) {
                await audit.WriteAsync(h, "WorkspaceRead", "NotFound");
                return Results.NotFound(new { code = "resource_not_found" });
            }
            context = new(row.Id, row.CompanyId, row.Code, row.Name, row.Kind.ToString(), row.Version, actor.CanEditVessel(row.Id));
        }
        var role = WorkspaceRoleContext.Describe(actor.TenantId, actor.RoleContext, config);
        await audit.WriteAsync(h, "WorkspaceRead", "Success", resourceId: vessel);
        return Results.Ok(new WorkspaceContextDto(actor.TenantId, actor.Id, role, context));
    }
}
