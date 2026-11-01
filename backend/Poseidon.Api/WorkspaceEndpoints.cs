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
        var destinations = BaseDestinations(actor);
        return actor.CanReadRegulatory ? [.. destinations, new("sources", "/app/sources"), new("requirements", "/app/requirements")] : destinations;
    }
    private static NavigationDto[] BaseDestinations(TrustedActor actor) =>
        actor.TenantWideRead || actor.TenantWideManage
            ? [new("today", "/app/today"), new("companies", "/app/companies"), new("fleet", "/app/fleet")]
            : [new("today", "/app/today"), new("fleet", "/app/fleet")];

    public static void MapWorkspace(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/workspace", async (HttpContext http, ActorResolver actors, SecurityAudit audit) =>
        {
            var actor = actors.Resolve(http.User)!;
            await audit.WriteAsync(http, "WorkspaceRead", "Success");
            return TypedResults.Ok(new WorkspaceDto(Destinations(actor), actor.CanSearch, actor.CanWriteRegulatory));
        }).RequireAuthorization("DirectoryRead").WithName("GetWorkspace").Produces(401).Produces(403).Produces(503);

        app.MapGet("/api/v1/workspace/search", async (HttpContext http, ActorResolver actors, PoseidonDbContext db, SecurityAudit audit, string? q, int? offset, int? limit) =>
        {
            var actor = actors.Resolve(http.User)!;
            if (!actor.CanSearch)
            {
                await audit.WriteAsync(http, "WorkspaceSearch", "Denied");
                return Results.StatusCode(403);
            }
            if (q is { Length: > 100 } || q?.Any(char.IsControl) == true || offset is < 0 or > 100000 || limit is < 1 or > 25)
            {
                await audit.WriteAsync(http, "WorkspaceSearch", "Invalid");
                return Results.BadRequest(new { code = "invalid_search" });
            }
            var search = q?.Trim() ?? "";
            var skip = offset ?? 0;
            var take = limit ?? 25;
            // Stable ordinal destination order, followed by Code/Id vessel order. No total count.
            var destinations = Destinations(actor).Where(x => search == "" || x.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Select(x => new SearchItemDto(x.Key, x.Href, x.Key, "Destination")).ToArray();
            var vesselSkip = Math.Max(0, skip - destinations.Length);
            var rows = await actor.Scope(db.Vessels.AsNoTracking())
                .Where(x => search == "" || x.Code.Contains(search) || x.Name.Contains(search))
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
