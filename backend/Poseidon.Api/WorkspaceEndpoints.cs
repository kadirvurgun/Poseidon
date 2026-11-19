using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record NavigationDto(string Key, string Href);
public sealed record WorkspaceDto(NavigationDto[] Destinations, bool CanSearch, bool CanWriteRegulatory = false);
public sealed record SearchItemDto(string Key, string Href, string Label, string Kind);

// NAV-01/02: projections contain implemented routes only, with the same scope as their endpoints.
public static class WorkspaceEndpoints
{
    public static NavigationDto[] Destinations(TrustedActor actor)
    {
        if (!actor.CanRead) return [];
        NavigationDto[] destinations = actor.CanReadAudit ? [.. BaseDestinations(actor), new NavigationDto("audit", "/app/audit")] : BaseDestinations(actor);
        // DEV-032: audit-ready trace only for an audit reader with the human trace capability.
        if (AuditReadyEndpoints.CanRead(actor)) destinations = [.. destinations, new("audit-ready", "/app/audit-ready")];
        if (SystemHealthEndpoints.Permitted(actor)) destinations = [.. destinations, new("system-health", "/app/system-health")];
        if (NotificationEndpoints.CanRead(actor)) destinations = [.. destinations, new("notification-escalation", "/app/notification-escalation")];
        if (DefectAssetContextReader.CanRead(actor)) destinations = [.. destinations, new("defects", "/app/defects")];
        if (WorkOrderEndpoints.CanRead(actor)) destinations = [.. destinations, new("work-order-center", "/app/work-order-center")];
        if (OperationalDowntimeEndpoints.CanRead(actor)) destinations = [.. destinations, new("operational-downtime", "/app/operational-downtime")];
        if (CertificateReader.Read(actor)) destinations = [.. destinations,
            new("certificate-command-center", "/app/certificate-command-center"), new("renewal-workflow", "/app/renewal-workflow")];
        if (actor.CanReadTug) destinations = [.. destinations, new("tug-command", "/app/tug-command"), new("tug-job-board", "/app/tug-job-board"), new("tug-job-intake", "/app/tug-job-intake")];
        if (actor.CanSeeShip) {
            if (ShipOperationsEndpoints.CanRead(actor,"Fuel")) destinations = [.. destinations,new("fuel-bunker-log","/app/fuel-bunker-log")];
            if (ShipOperationsEndpoints.CanRead(actor,"Port")) destinations = [.. destinations,new("port-activity-sof","/app/port-activity-sof")];
            if (DailyOperationsEndpoints.CanRead(actor,"Noon")) destinations = [.. destinations,new("noon-report","/app/noon-report")];
            if (DailyOperationsEndpoints.CanRead(actor,"Deck")) destinations = [.. destinations,new("deck-daily-log","/app/deck-daily-log")];
            if (DailyOperationsEndpoints.CanRead(actor,"Engine")) destinations = [.. destinations,new("engine-daily-log","/app/engine-daily-log")];
        }
        return actor.CanReadRegulatory ? [.. destinations, new("sources", "/app/sources"), new("requirements", "/app/requirements")] : destinations;
    }
    private static NavigationDto[] BaseDestinations(TrustedActor actor) =>
        actor.TenantWideRead || actor.TenantWideManage
            ? [new("today", "/app/today"), new("companies", "/app/companies"), new("fleet", "/app/fleet")]
            : [new("today", "/app/today"), new("fleet", "/app/fleet")];

    private static readonly string[] SearchQueryKeys = ["q", "offset", "limit"];
    internal static bool SearchQueryClosed(IQueryCollection query) =>
        query.All(p => SearchQueryKeys.Contains(p.Key, StringComparer.OrdinalIgnoreCase) && p.Value.Count == 1);

    public static void MapWorkspace(this IEndpointRouteBuilder app)
    {
        app.MapWorkspaceContext();
        app.MapWorkspaceLenses();
        app.MapGet("/api/v1/workspace", async (HttpContext http, ActorResolver actors, SecurityAudit audit) =>
        {
            var actor = actors.Resolve(http.User)!;
            http.Response.Headers.CacheControl = "no-store";
            // DEV-022: the menu projection has no selector; a client tenant/role/vessel hint is rejected.
            if (http.Request.Query.Count != 0)
            {
                await audit.WriteAsync(http, "WorkspaceRead", "Invalid");
                return Results.BadRequest(new { code = "invalid_request" });
            }
            await audit.WriteAsync(http, "WorkspaceRead", "Success");
            return Results.Ok(new WorkspaceDto(Destinations(actor), actor.CanSearch, actor.CanWriteRegulatory));
        }).RequireAuthorization("DirectoryRead").WithName("GetWorkspace").Produces<WorkspaceDto>().Produces(400).Produces(401).Produces(403).Produces(503);

        app.MapGet("/api/v1/workspace/search", async (HttpContext http, ActorResolver actors, PoseidonDbContext db, SecurityAudit audit, string? q, int? offset, int? limit) =>
        {
            var actor = actors.Resolve(http.User)!;
            if (!actor.CanSearch)
            {
                await audit.WriteAsync(http, "WorkspaceSearch", "Denied");
                return Results.StatusCode(403);
            }
            // DEV-022: closed query union (65.03 paragraph 170; 54.02 IAM_READ_V1 paragraph 13347 "unknown filter 400"). A tenant, vessel,
            // role, mode or permission selector is never an authority input and is rejected, not ignored.
            if (!SearchQueryClosed(http.Request.Query) || q is { Length: > 100 } || q?.Any(char.IsControl) == true || offset is < 0 or > 100000 || limit is < 1 or > 25)
            {
                await audit.WriteAsync(http, "WorkspaceSearch", "Invalid");
                return Results.BadRequest(new { code = "invalid_search" });
            }
            var search = q?.Trim() ?? "";
            var folded = search.ToLowerInvariant();
            var skip = offset ?? 0;
            var take = limit ?? 25;
            // Stable ordinal destination order, followed by Code/Id vessel order. No total count.
            // Destinations are matched in memory AFTER the scope projection (Destinations(actor)), by route
            // key or TR/EN display label with Turkish-aware folding; a hidden destination's label never
            // participates, so it is indistinguishable from no match.
            var needle = WorkspaceDestinationLabels.Fold(search);
            var destinations = Destinations(actor).Where(x => search == "" || WorkspaceDestinationLabels.Matches(x.Key, needle))
                .Select(x => new SearchItemDto(x.Key, x.Href, x.Key, "Destination")).ToArray();
            var vesselSkip = Math.Max(0, skip - destinations.Length);
            // Scope is applied before matching and pagination, so hidden vessels never influence
            // matches, ordering, hasMore or the page boundary. Matching is case-insensitive via SQL
            // lower(); known limitation: lower() follows the database collation/ctype, so Turkish
            // dotted/dotless I and diacritics are not folded for vessel code/name (destinations are).
            var rows = await actor.Scope(db.Vessels.AsNoTracking())
                .Where(x => folded == "" || x.Code.ToLower().Contains(folded) || x.Name.ToLower().Contains(folded))
                .OrderBy(x => x.Code).ThenBy(x => x.Id).Skip(vesselSkip).Take(take + 1)
                .Select(x => new { x.Id, x.Code, x.Name, x.Kind }).ToArrayAsync(http.RequestAborted);
            var candidates = new List<SearchItemDto>();
            // Keep the destination/vessel boundary explicit, including pages inside either partition.
            for (var index = Math.Min(skip, destinations.Length); index < destinations.Length; index++)
                candidates.Add(destinations[index]);
            foreach (var row in rows)
                candidates.Add(new(row.Id.ToString(), $"/app/fleet/{row.Id}", row.Code + " · " + row.Name, row.Kind.ToString()));
            await audit.WriteAsync(http, "WorkspaceSearch", "Success");
            return Results.Ok(new PageDto<SearchItemDto>(candidates.GetRange(0, Math.Min(take, candidates.Count)).ToArray(), skip, take, candidates.Count > take));
        }).RequireAuthorization("DirectoryRead").WithName("SearchWorkspace").Produces<PageDto<SearchItemDto>>().Produces(400).Produces(401).Produces(403).Produces(503);
    }
}
