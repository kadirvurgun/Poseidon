using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record NoonSummaryFieldDto(string Key, string Type, string? Unit, int SourceRow, DailyValue Value);
public sealed record NoonSummaryItemDto(Guid RecordId, string State, bool Consumable, ShipOperationOriginPin? ReviewedPin,
    long HeadRevision, bool ReviewedIsCurrent, Guid? ReviewEventId, DateTimeOffset? ReviewedAt, string Provenance,
    NoonSummaryFieldDto[] Fields, DailyReferenceDto[] References, string Href, string[] Qualifications);
public sealed record NoonSummaryPageDto(Guid VesselId, NoonSummaryItemDto[] Items, int Offset, int Limit, bool HasMore,
    string[] Consumers, string EmissionsBasis, string SourceVersion);

// DEV-097/098 read side: owning reference resolution, writer lookup options,
// and the single downstream projection of master-reviewed Noon data. Every
// response is fenced against the actor's current rights before and after the
// attributed read observation, like the owning daily read routes.
public static class DailyReferenceEndpoints
{
    private static readonly (string Kind, string Route, string App)[] Families = [
        ("Noon", "noon-reports", "noon-report"), ("Deck", "deck-daily-logs", "deck-daily-log"), ("Engine", "engine-daily-logs", "engine-daily-log")];
    // Inputs downstream voyage/emissions/dashboard consumers may reuse. Values
    // are the exact user-declared lexical text of the reviewed revision. No
    // CO2/CII/EEXI or any other regulatory figure is computed: no active source
    // defines a formula or approved emission factors for these inputs.
    internal static readonly string[] SummaryKeys = ["report_date", "report_time_utc", "voyage_id", "position_lat", "position_lon", "activity",
        "from_port", "to_port", "eta", "distance_run_nm", "distance_to_go_nm", "avg_speed_kn", "weather_summary", "cargo_status",
        "main_engine_hours", "fuel_summary", "rob_summary"];

    public static void MapDailyReferences(this IEndpointRouteBuilder app) {
        foreach (var family in Families) {
            var kind = family.Kind;
            var group = app.MapGroup("/api/v1/" + family.Route).RequireAuthorization("DirectoryRead");
            group.MapGet("/reference-options", (HttpContext h, PoseidonDbContext db) => Options(kind, h, db))
                .WithName(kind + "DailyReferenceOptions").Produces<DailyReferenceOptionsDto>();
            group.MapGet("/{id:guid}/references", (Guid id, HttpContext h, PoseidonDbContext db) => References(kind, id, h, db))
                .WithName(kind + "DailyReferences").Produces<DailyReferencesDto>();
        }
        app.MapGet("/api/v1/noon-reports/reviewed-summary", Summary).RequireAuthorization("DirectoryRead")
            .WithName("NoonReviewedSummary").Produces<NoonSummaryPageDto>();
    }

    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static DateTimeOffset Now(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "DailyOperationsRead", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static bool Query(HttpContext h, string[] allowed, out Guid vessel) {
        vessel = Guid.Empty;
        return h.Request.Query.All(q => q.Value.Count == 1 && allowed.Contains(q.Key)) &&
            Guid.TryParseExact(h.Request.Query["vesselId"], "D", out vessel) && vessel != Guid.Empty;
    }
    private static Task<VesselKind?> Vessel(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) =>
        a.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel && (v.Kind == VesselKind.Ship || a.Permissions.Contains("mode.cross")))
            .Select(v => (VesselKind?)v.Kind).SingleOrDefaultAsync(ct);
    private static async Task<IResult> Respond(HttpContext h, string kind, Guid vessel, string rights, Guid resource, object result) {
        if (!await DailyOperationsEndpoints.Current(h, kind, vessel, rights)) return await Fail(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "DailyOperationsRead", "Success", resourceId: resource);
        if (!await DailyOperationsEndpoints.Current(h, kind, vessel, rights)) return await Fail(h, 403, "permission_denied");
        return Results.Ok(result);
    }

    private static async Task<IResult> Options(string kind, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        if (!Query(h, ["vesselId", "field", "logDate"], out var vessel) || h.Request.Query["field"].ToString() is not { Length: > 0 and <= 64 } field ||
            !DailyOperationsPolicy.Schema(kind).Any(f => f.Key == field && f.Type == "Reference" && !f.ReadOnly)) return await Fail(h, 400, "invalid_query");
        // Optional exact yyyy-MM-dd log date the candidates must cover (Deck officer_of_watch).
        Dictionary<string, DailyValue>? declared = null;
        if (h.Request.Query.ContainsKey("logDate")) {
            var text = h.Request.Query["logDate"].ToString(); declared = new() { ["log_date"] = new("Known", text) };
            if (!DailyOfficerAssignment.TryLogDate(declared, out _)) return await Fail(h, 400, "invalid_query");
        }
        var a = Actor(h);
        // Lookup candidates are an authoring aid: only a current writer sees them.
        if (a == null || !DailyOperationsEndpoints.CanWrite(a, kind, vessel)) return await Fail(h, 403, "permission_denied");
        var rights = DailyOperationsEndpoints.Rights(a);
        if (await Vessel(db, a, vessel, h.RequestAborted) == null) return await Fail(h, 404, "resource_not_found");
        var reader = DailyOperationsEndpoints.References(h).For(kind, field);
        DailyReferenceOptionsDto result;
        if (reader == null || !reader.Available(a)) result = new(vessel, kind, field, false, [], false);
        else {
            var items = await reader.Options(new(db, a, vessel, kind, Now(h), declared), field, 51, h.RequestAborted);
            result = new(vessel, kind, field, true, items.Take(50).ToArray(), items.Length > 50);
        }
        return await Respond(h, kind, vessel, rights, vessel, result);
    }

    private static async Task<IResult> References(string kind, Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        if (!Query(h, ["vesselId"], out var vessel)) return await Fail(h, 400, "invalid_query");
        var a = Actor(h);
        if (a == null || !DailyOperationsEndpoints.CanRead(a, kind)) return await Fail(h, 403, "permission_denied");
        var rights = DailyOperationsEndpoints.Rights(a);
        if (await Vessel(db, a, vessel, h.RequestAborted) == null) return await Fail(h, 404, "resource_not_found");
        var row = await db.DailyOperations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.VesselId == vessel && r.Kind == kind, h.RequestAborted);
        if (row == null) return await Fail(h, 404, "resource_not_found");
        var fields = DailyOperationsPolicy.ReadFields(kind, vessel, row.FieldsJson);
        var registry = DailyOperationsEndpoints.References(h); var context = new DailyReferenceContext(db, a, vessel, kind, Now(h), fields);
        var items = new List<DailyReferenceDto>();
        foreach (var field in DailyOperationsPolicy.Schema(kind).Where(f => f.Type == "Reference" && !f.ReadOnly))
            items.Add(await registry.Describe(context, field.Key, fields.GetValueOrDefault(field.Key), h.RequestAborted));
        return await Respond(h, kind, vessel, rights, id, new DailyReferencesDto(id, vessel, kind, row.Revision, row.Version, items.ToArray()));
    }

    private static async Task<IResult> Summary(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var offset = 0;
        if (!Query(h, ["vesselId", "offset"], out var vessel) || h.Request.Query.ContainsKey("offset") &&
            (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 100000))
            return await Fail(h, 400, "invalid_query");
        var a = Actor(h);
        if (a == null || !DailyOperationsEndpoints.CanRead(a, "Noon")) return await Fail(h, 403, "permission_denied");
        var rights = DailyOperationsEndpoints.Rights(a);
        if (await Vessel(db, a, vessel, h.RequestAborted) != VesselKind.Ship) return await Fail(h, 404, "resource_not_found");
        var ct = h.RequestAborted;
        var heads = await db.DailyOperations.AsNoTracking().Where(r => r.VesselId == vessel && r.Kind == "Noon")
            .OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id).Skip(offset).Take(26).ToArrayAsync(ct);
        var page = heads.Take(25).ToArray(); var ids = page.Select(r => r.Id).ToArray();
        var events = await db.DailyReviewEvents.AsNoTracking().Where(e => e.VesselId == vessel && e.Kind == "Noon" && ids.Contains(e.RecordId))
            .Select(e => new { e.Id, e.RecordId, e.RecordRevision, e.Sequence, e.Decision, e.Duty, e.RecordHash, e.SourceVersion, e.SavedAt }).ToArrayAsync(ct);
        // A revision counts as master-reviewed only when its latest immutable
        // review event is a Master REVIEWED decision bound to that exact hash.
        var reviewed = events.GroupBy(e => new { e.RecordId, e.RecordRevision }).Select(g => g.OrderByDescending(e => e.Sequence).First())
            .Where(e => e.Decision == "REVIEWED" && e.Duty == "Master" && e.SourceVersion == DailyOperationsPolicy.SourceVersion)
            .GroupBy(e => e.RecordId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.RecordRevision).First());
        var revisionNumbers = reviewed.Values.Select(e => e.RecordRevision).Distinct().ToArray();
        var reviewedIds = reviewed.Keys.ToArray();
        var revisions = await db.DailyOperationRevisions.AsNoTracking().Where(r => r.VesselId == vessel && r.Kind == "Noon" &&
            reviewedIds.Contains(r.RecordId) && revisionNumbers.Contains(r.Revision)).ToArrayAsync(ct);
        var schema = DailyOperationsPolicy.Schema("Noon");
        var registry = DailyOperationsEndpoints.References(h); var context = new DailyReferenceContext(db, a, vessel, "Noon", Now(h));
        var items = new List<NoonSummaryItemDto>();
        foreach (var head in page) {
            var href = $"/app/noon-report/{vessel:D}?id={head.Id:D}";
            var review = reviewed.GetValueOrDefault(head.Id);
            var revision = review == null ? null : revisions.SingleOrDefault(r => r.RecordId == head.Id && r.Revision == review.RecordRevision);
            if (review == null || revision == null || revision.Hash != review.RecordHash || revision.SourceVersion != DailyOperationsPolicy.SourceVersion) {
                items.Add(new(head.Id, "UNREVIEWED", false, null, head.Revision, false, null, null, head.Provenance, [], [], href,
                    review == null ? ["MASTER_REVIEW_NOT_RECORDED", "NOT_FOR_DOWNSTREAM_REUSE"] : ["REVIEWED_REVISION_INTEGRITY_UNAVAILABLE", "NOT_FOR_DOWNSTREAM_REUSE"]));
                continue;
            }
            var fields = DailyOperationsPolicy.ReadFields("Noon", vessel, revision.FieldsJson);
            var synthetic = revision.Provenance != "UserDeclaredUnverified";
            var current = head.Revision == revision.Revision && head.Hash == revision.Hash;
            var references = new List<DailyReferenceDto>();
            foreach (var key in new[] { "voyage_id", "from_port", "to_port", "evidence_ref" })
                references.Add(await registry.Describe(context, key, fields.GetValueOrDefault(key), ct));
            var qualifications = new List<string> { "REVIEW_RECORDING_IS_NOT_VERIFICATION", "USER_DECLARED_UNVERIFIED", "NO_COMPUTED_EMISSIONS" };
            if (!current) qualifications.Add("LATER_UNREVIEWED_REVISION_EXISTS");
            if (synthetic) qualifications.AddRange(["SYNTHETIC_NOT_OPERATIONAL", "NOT_FOR_DOWNSTREAM_REUSE"]);
            items.Add(new(head.Id, "MASTER_REVIEW_RECORDED", !synthetic, DailyOperationOriginReader.Pin(revision), head.Revision, current, review.Id, review.SavedAt,
                revision.Provenance, schema.Where(f => SummaryKeys.Contains(f.Key)).Select(f => new NoonSummaryFieldDto(f.Key, f.Type, f.Unit, f.SourceRow,
                    fields.GetValueOrDefault(f.Key) ?? new("Missing", null))).ToArray(), references.ToArray(), href, qualifications.ToArray()));
        }
        var result = new NoonSummaryPageDto(vessel, items.ToArray(), offset, 25, heads.Length > 25, ["voyage", "emissions", "dashboard"],
            "INPUTS_ONLY_NO_SOURCE_DEFINED_FORMULA", DailyOperationsPolicy.SourceVersion);
        return await Respond(h, "Noon", vessel, rights, vessel, result);
    }
}
