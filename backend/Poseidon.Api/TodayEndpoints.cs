using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TodayCoverageDto(string Source, string Status);
public sealed record TodayCardDto(string Key, string Priority, string Source, string Reason, string? Owner, string NextAction, string Href);
public sealed record TodayDto(VesselDto Vessel, DateTimeOffset ReadAt, TodayCoverageDto[] Coverage, PageDto<TodayCardDto> Cards);
public sealed record TodayMaintenanceEvidenceDto(Guid PlanId, long PlanRevision, Guid AssetId, long AssetRevision,
    long? CurrentAssetRevision, bool JobAvailable, bool OwnerEligible, MaintenanceDue Calculation, string ObservationState);
public sealed record TodayWhyDto(Guid VesselId, string CardKey, string Rule, string Reason, DraftObservation? Inputs,
    DateTimeOffset ReadAt, DateTimeOffset? SavedAt, string? SourceObservedAt, string Freshness,
    string Provenance, string[] Blockers, string? Owner, string NextAction, string Href,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TodayMaintenanceEvidenceDto? Maintenance = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TodayTugEvidenceDto? Tug = null);

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
        var cardKey = query["cardKey"].ToString();
        Guid? planId = null;
        Guid? jobId = null;
        if (why && cardKey != "regulatory-draft") {
            var prefix = cardKey.StartsWith("tug-job:", StringComparison.Ordinal) ? "tug-job:" : "maintenance-plan:";
            if (!cardKey.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(cardKey[prefix.Length..], "D", out var parsed) || parsed == Guid.Empty ||
                cardKey != prefix + parsed.ToString("D")) return await Invalid();
            if (prefix == "tug-job:") jobId = parsed; else planId = parsed;
        }
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
        var readAt = DateTimeOffset.UtcNow;
        if (why && jobId != null) {
            var source = await TodayTugReader.Read(id, http, db, actor, readAt, jobId);
            if (source == null) {
                await audit.WriteAsync(http, action, "Denied");
                return Results.StatusCode(403);
            }
            var current = source.SingleOrDefault();
            if (current == null) {
                await audit.WriteAsync(http, action, "NotFound");
                return Results.NotFound();
            }
            var card = TugCard(id, current);
            await audit.WriteAsync(http, action, "Success", resourceId: id);
            return Results.Ok(new TodayWhyDto(id, card.Key, "TW-TUG-01", card.Reason, null,
                readAt, current.SavedAt, null, "Unverified", current.Provenance,
                ["DraftPlanningNotOperationalApproval", "OwningReadinessCoverageIncomplete", "MasterDecisionRequired"],
                card.Owner, card.NextAction, card.Href, Tug: current.Detail));
        }
        if (why && planId != null) {
            var source = await MaintenanceCurrentReader.Read(id, http, db, actor, readAt, planId);
            if (source == null) {
                await audit.WriteAsync(http, action, "Denied");
                return Results.StatusCode(403);
            }
            var current = source.SingleOrDefault();
            var advice = current == null ? null : TodayMaintenancePolicy.Observe(current.Row.Due);
            if (current == null || advice == null) {
                await audit.WriteAsync(http, action, "NotFound");
                return Results.NotFound();
            }
            var row = current.Row;
            var card = MaintenanceCard(id, current, advice);
            await audit.WriteAsync(http, action, "Success", resourceId: id);
            return Results.Ok(new TodayWhyDto(id, card.Key, "TW-MAINTENANCE-01", card.Reason, null,
                readAt, row.SavedAt, row.Inputs.ObservedAt?.ToString("O", CultureInfo.InvariantCulture), "Unverified", row.Provenance,
                ["PlanningDraftNotVerified"], card.Owner, card.NextAction, card.Href,
                new(row.Id, row.Revision, row.AssetId, row.AssetRevision, current.CurrentAssetRevision,
                    current.JobAvailable, current.OwnerEligible, row.Due, row.ObservationState)));
        }
        var draft = await db.RegulatoryDrafts.AsNoTracking().SingleOrDefaultAsync(d => d.VesselId == id, http.RequestAborted);
        var content = draft == null ? null : RegulatoryDraftStorage.Deserialize(draft.ContentJson);
        var observation = TodayAdvisoryPolicy.Observe(content);
        var href = $"/app/fleet/{id}/profile";
        var next = actor.CanEditVessel(id) ? "ReviewDraft" : "ViewDraft";
        if (why) {
            await audit.WriteAsync(http, action, "Success", resourceId: id);
            return Results.Ok(new TodayWhyDto(id, "regulatory-draft", "TW-DRAFT-01", observation.Reason,
                observation, readAt, draft?.SavedAt, content?.SourceObservedAt.Value, "Unverified", draft?.Provenance ?? "Missing",
                [draft == null ? "DraftAbsent" : "VerificationUnavailable", "OperationalSourcesUnavailable"], null, next, href));
        }
        var maintenance = await MaintenanceCurrentReader.Read(id, http, db, actor, readAt);
        var tug = await TodayTugReader.Read(id, http, db, actor, readAt);
        var cards = new List<TodayCardDto> { new("regulatory-draft", "Advisory", "RegulatoryDraft", observation.Reason, null, next, href) };
        foreach (var current in maintenance ?? []) {
            var advice = TodayMaintenancePolicy.Observe(current.Row.Due);
            if (advice != null) cards.Add(MaintenanceCard(id, current, advice));
        }
        cards.AddRange((tug ?? []).Select(current => TugCard(id, current)));
        cards = cards.OrderBy(c => c.Priority == "Attention" ? 0 : c.Priority == "Review" ? 1 : 2)
            .ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
        await audit.WriteAsync(http, action, "Success", resourceId: id);
        return Results.Ok(new TodayDto(new(vessel.Id, vessel.CompanyId, vessel.Code, vessel.Name, vessel.Kind.ToString(), vessel.Version, actor.CanEditVessel(id)), readAt,
            [new("RegulatoryDraft", draft == null ? "Missing" : "Available"), new("OperationalTasks", "Unavailable"),
             new("SafetyDecisions", "Unavailable"), new("Permits", "Unavailable"), new("EvidenceVerification", "Unavailable"),
             new("TechnicalMaintenance", maintenance == null ? "Unavailable" : maintenance.Length == 0 ? "Missing" : "Available"),
             new("TugPlanning", tug == null ? "Unavailable" : tug.Length == 0 ? "Missing" : "Available")],
            new(cards.Skip(skip).Take(take).ToArray(), skip, take, cards.Count > skip + take)));
    }

    private static TodayCardDto MaintenanceCard(Guid vessel, MaintenanceCurrentProjection source, TodayMaintenanceAdvisory advice) =>
        new($"maintenance-plan:{source.Row.Id:D}", advice.Priority, "MaintenancePlan", advice.Reason,
            source.OwnerEligible ? source.Row.OwnerRoleId?.ToString("D") : null,
            "ReviewMaintenancePlan", $"/app/maintenance-plan/{vessel:D}?id={source.Row.Id:D}");

    private static TodayCardDto TugCard(Guid vessel, TodayTugProjection source) =>
        new($"tug-job:{source.Detail.JobId:D}", source.Advice.Priority, "TugJob", source.Advice.Reason, source.Detail.CurrentOwnerRoleId?.ToString("D"),
            "ReviewTugJob", $"/app/tug-job-detail/{source.Detail.JobId:D}?vessel={vessel:D}");
}
