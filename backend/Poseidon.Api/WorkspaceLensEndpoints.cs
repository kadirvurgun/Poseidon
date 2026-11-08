using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record WorkspaceLensItem(string Key, string Href, string Group);
public sealed record WorkspaceLens(Guid Reference, string Tr, string En, WorkspaceLensItem[] Items);
public sealed record WorkspaceLenses(Guid TenantId, Guid ActorId, Guid? VesselId, string Mode,
    string State, string? Version, WorkspaceLens[] Lenses, bool CanSearch);

// NAVIGATION-ROLE-LENS-01: a presentation intersection, never an authorization input.
public static class WorkspaceLensPolicy
{
    public static WorkspaceLensItem[] Available(TrustedActor actor, Guid? vessel, VesselKind? kind)
    {
        var items = WorkspaceEndpoints.Destinations(actor)
            .Where(x => x.Key is not ("fuel-bunker-log" or "port-activity-sof") || kind != VesselKind.Tug)
            .Where(x => x.Key is not ("noon-report" or "deck-daily-log" or "engine-daily-log") || kind != VesselKind.Tug || actor.Permissions.Contains("mode.cross"))
            .Where(x => !x.Key.StartsWith("tug-", StringComparison.Ordinal) || kind != VesselKind.Ship)
            .Where(x => x.Key != "tug-job-intake" || actor.HasWriteAttribution && actor.Permissions.Contains("tug.write") &&
                (actor.TenantWideManage || vessel != null && actor.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit)))
            .Select(x => new WorkspaceLensItem(x.Key,
                x.Key.StartsWith("tug-", StringComparison.Ordinal) && vessel != null ? x.Href + "?vessel=" + vessel :
                x.Key is "work-order-center" or "defects" or "today" or "noon-report" or "deck-daily-log" or "engine-daily-log" or "fuel-bunker-log" or "port-activity-sof" or "certificate-command-center" or "renewal-workflow" && vessel != null ? x.Href + "/" + vessel : x.Href,
                x.Key is "sources" or "requirements" or "audit" or "certificate-command-center" or "renewal-workflow" ? "review" : "daily")).ToList();
        if (!actor.CanRead || vessel == null) return items.ToArray();
        if (actor.Permissions.Contains("crew.read")) items.Add(new("crew", $"/app/crew-roster/{vessel}", "daily"));
        if (actor.Permissions.Contains("equipment.read")) items.Add(new("equipment", $"/app/equipment-hierarchy/{vessel}", "daily"));
        // The plans workspace loads equipment alongside plans, so both endpoints must be allowed.
        if (actor.Permissions.Contains("equipment.read") && actor.Permissions.Contains("maintenance.read"))
            items.Add(new("maintenance", $"/app/maintenance-plan/{vessel}", "daily"));
        if (actor.Permissions.Contains("import.read")) items.Add(new("import", $"/app/excel-migration/{vessel}", "review"));
        return items.ToArray();
    }

    public static WorkspaceLenses Describe(TrustedActor actor, Guid? vessel, VesselKind? kind, IConfiguration config)
    {
        var mode = kind?.ToString() ?? "Fleet";
        WorkspaceLenses Empty(string state) => new(actor.TenantId, actor.Id, vessel, mode, state, null, [], actor.CanSearch);
        if (!actor.CanRead) return Empty("Unavailable");
        var roles = WorkspaceRoleContext.Describe(actor.TenantId, actor.RoleContext, config);
        if (roles.State != "Configured") return Empty(roles.State);
        var catalog = config.GetSection("WorkspaceNavigation");
        var version = catalog["Version"];
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64 || version.Trim() != version || version.Any(char.IsControl))
            return Empty("Unavailable");
        var rows = catalog.GetSection("Mappings").GetChildren().Take(4097).ToArray();
        if (rows.Length > 4096) return Empty("Unavailable");
        var mappings = new Dictionary<Guid, (string[] Keys, string[] Modes)>();
        string[] known = ["work-order-center", "defects", "today", "fleet", "companies", "crew", "equipment", "maintenance", "import", "audit", "sources", "requirements", "tug-command", "tug-job-board", "tug-job-intake", "noon-report", "deck-daily-log", "engine-daily-log", "certificate-command-center", "renewal-workflow", "fuel-bunker-log", "port-activity-sof"];
        foreach (var row in rows) {
            if (!Guid.TryParse(row["TenantId"], out var tenant) || tenant != actor.TenantId) continue;
            var keys = row.GetSection("Keys").GetChildren().Select(x => x.Value ?? "").ToArray();
            var modes = row.GetSection("Modes").GetChildren().Select(x => x.Value ?? "").ToArray();
            if (!Guid.TryParse(row["Reference"], out var reference) || reference == Guid.Empty ||
                keys.Length is < 1 or > 16 || keys.Distinct().Count() != keys.Length || keys.Any(x => !known.Contains(x)) ||
                modes.Length is < 1 or > 3 || modes.Distinct().Count() != modes.Length || modes.Any(x => x is not ("Fleet" or "Ship" or "Tug")) ||
                !mappings.TryAdd(reference, (keys, modes))) return Empty("Unavailable");
        }
        if (roles.Roles.Any(r => !mappings.ContainsKey(r.Reference))) return Empty("Unavailable");
        var available = Available(actor, vessel, kind);
        var lenses = roles.Roles.Where(r => mappings[r.Reference].Modes.Contains(mode)).Select(r =>
            new WorkspaceLens(r.Reference, r.Tr, r.En, available.Where(i => mappings[r.Reference].Keys.Contains(i.Key)).ToArray())).ToArray();
        return new(actor.TenantId, actor.Id, vessel, mode, "Configured", version, lenses, actor.CanSearch);
    }
}

public static class WorkspaceLensEndpoints
{
    public static void MapWorkspaceLenses(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/workspace/lenses", (HttpContext h, ActorResolver a, PoseidonDbContext db, SecurityAudit audit, IConfiguration c) => Read(null, h, a, db, audit, c))
            .RequireAuthorization("DirectoryRead").WithName("WorkspaceLenses").Produces<WorkspaceLenses>()
            .Produces(400).Produces(401).Produces(403).Produces(503);
        app.MapGet("/api/v1/workspace/lenses/{vessel:guid}", (Guid vessel, HttpContext h, ActorResolver a, PoseidonDbContext db, SecurityAudit audit, IConfiguration c) => Read(vessel, h, a, db, audit, c))
            .RequireAuthorization("DirectoryRead").WithName("VesselWorkspaceLenses").Produces<WorkspaceLenses>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
    }
    private static async Task<IResult> Read(Guid? vessel, HttpContext h, ActorResolver actors, PoseidonDbContext db, SecurityAudit audit, IConfiguration config)
    {
        h.Response.Headers.CacheControl = "no-store"; h.Response.Headers.Pragma = "no-cache";
        if (h.Request.Query.Count != 0) {
            await audit.WriteAsync(h, "WorkspaceRead", "Invalid");
            return Results.BadRequest(new { code = "invalid_request" });
        }
        var actor = actors.Resolve(h.User)!;
        VesselKind? kind = null;
        if (vessel != null) {
            kind = await actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel)
                .Select(v => (VesselKind?)v.Kind).SingleOrDefaultAsync(h.RequestAborted);
            if (kind == null) {
                await audit.WriteAsync(h, "WorkspaceRead", "NotFound");
                return Results.NotFound(new { code = "resource_not_found" });
            }
        }
        var value = WorkspaceLensPolicy.Describe(actor, vessel, kind, config);
        await audit.WriteAsync(h, "WorkspaceRead", "Success", resourceId: vessel);
        return Results.Ok(value);
    }
}
