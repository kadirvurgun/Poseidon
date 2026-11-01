using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TodayCoverageDto(string Source, string Status);
public sealed record TodayCardDto(string Key, string Priority, string Source, string Reason, string? Owner, string NextAction, string Href);
public sealed record TodayDto(VesselDto Vessel, DateTimeOffset ReadAt, TodayCoverageDto[] Coverage, PageDto<TodayCardDto> Cards);
public sealed record TodayWhyDto(Guid VesselId, string CardKey, string Rule, string Reason, DraftObservation Inputs,
    DateTimeOffset ReadAt, DateTimeOffset? SavedAt, string? SourceObservedAt, string Freshness,
    string Provenance, string[] Blockers, string? Owner, string NextAction, string Href);

public static class TodayEndpoints
{
    public static void MapToday(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/poseidon-today", (HttpContext http, PoseidonDbContext db, ActorResolver actors, SecurityAudit audit,
            [FromQuery] string? vesselId, [FromQuery] string? offset, [FromQuery] string? limit) => Read(http, db, actors, audit, false))
            .RequireAuthorization("DirectoryRead").WithName("GetToday").Produces<TodayDto>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
        app.MapGet("/api/v1/why-engine", (HttpContext http, PoseidonDbContext db, ActorResolver actors, SecurityAudit audit,
            [FromQuery] string? vesselId, [FromQuery] string? cardKey) => Read(http, db, actors, audit, true))
            .RequireAuthorization("DirectoryRead").WithName("GetTodayWhy").Produces<TodayWhyDto>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
    }

    private static async Task<IResult> Read(HttpContext http, PoseidonDbContext db, ActorResolver actors, SecurityAudit audit, bool why)
    {
        var action = why ? "TodayWhyRead" : "TodayRead";
        var query = http.Request.Query;
        var keys = why ? new[] { "vesselId", "cardKey" } : ["vesselId", "offset", "limit"];
        async Task<IResult> Invalid()
        {
            await audit.WriteAsync(http, action, "Invalid");
            return Results.BadRequest(new { code = "invalid_today_query" });
        }
        if (query.Any(x => !keys.Contains(x.Key) || x.Value.Count != 1) ||
            !Guid.TryParseExact(query["vesselId"], "D", out var id) || id == Guid.Empty) return await Invalid();
        var skip = 0; var take = 25;
        if (why && query["cardKey"] != "regulatory-draft") return await Invalid();
        if (!why && ((query.ContainsKey("offset") && (!int.TryParse(query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out skip) || skip > 100000)) ||
            (query.ContainsKey("limit") && (!int.TryParse(query["limit"], NumberStyles.None, CultureInfo.InvariantCulture, out take) || take is < 1 or > 25)))) return await Invalid();
        var actor = actors.Resolve(http.User)!;
        // The requested vessel is a selector, never authority. Recheck on every list AND expansion.
        var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(v => v.Id == id, http.RequestAborted);
        if (vessel == null)
        {
            await audit.WriteAsync(http, action, "NotFound");
            return Results.NotFound();
        }
        var draft = await db.RegulatoryDrafts.AsNoTracking().SingleOrDefaultAsync(d => d.VesselId == id, http.RequestAborted);
        var content = draft == null ? null : RegulatoryDraftStorage.Deserialize(draft.ContentJson);
        var observation = TodayAdvisoryPolicy.Observe(content);
        var readAt = DateTimeOffset.UtcNow;
        var href = $"/app/fleet/{id}/profile";
        var next = actor.CanEditVessel(id) ? "ReviewDraft" : "ViewDraft";
        await audit.WriteAsync(http, action, "Success", resourceId: id);
        if (why) return Results.Ok(new TodayWhyDto(id, "regulatory-draft", "TW-DRAFT-01", observation.Reason,
            observation, readAt, draft?.SavedAt, content?.SourceObservedAt.Value, "Unverified", draft?.Provenance ?? "Missing",
            [draft == null ? "DraftAbsent" : "VerificationUnavailable", "OperationalSourcesUnavailable"], null, next, href));
        TodayCardDto[] cards = [new("regulatory-draft", "Advisory", "RegulatoryDraft", observation.Reason, null, next, href)];
        return Results.Ok(new TodayDto(new(vessel.Id, vessel.CompanyId, vessel.Code, vessel.Name, vessel.Kind.ToString(), vessel.Version, actor.CanEditVessel(id)), readAt,
            [new("RegulatoryDraft", draft == null ? "Missing" : "Available"), new("OperationalTasks", "Unavailable"),
             new("SafetyDecisions", "Unavailable"), new("Permits", "Unavailable"), new("EvidenceVerification", "Unavailable")],
            new(cards.Skip(skip).Take(take).ToArray(), skip, take, false)));
    }
}
