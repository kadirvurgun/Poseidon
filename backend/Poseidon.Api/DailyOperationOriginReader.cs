using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DailyOriginFieldDto(string Key, string Type, DailyValue Value, int SourceRow, string? Unit);
public sealed record DailyOperationOriginDto(Guid VesselId, ShipOperationOriginPin Pin, bool Current, string State,
    string Qualification, string Provenance, DateTimeOffset SavedAt, DailyOriginFieldDto[] Fields, string Href);

// DEV-100 depends on the owning Noon/Engine sources, not a second fuel estimate.
// None of these values is copied, summed, converted to mt or made authoritative.
internal static class DailyOperationOriginReader
{
    internal static bool Valid(ShipOperationOriginPin? pin) => pin is { Kind: "Noon" or "Engine", Revision: > 0, Version: > 0 } &&
        pin.Id != Guid.Empty && pin.SourceVersion == DailyOperationsPolicy.SourceVersion &&
        pin.Hash is { Length: 64 } && pin.Hash.All(char.IsAsciiHexDigit);

    internal static IQueryable<DailyOperationRevision> Visible(PoseidonDbContext db, TrustedActor actor, Guid vessel)
    {
        var noon = DailyOperationsEndpoints.CanRead(actor, "Noon");
        var engine = DailyOperationsEndpoints.CanRead(actor, "Engine");
        var vessels = actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel && v.Kind == VesselKind.Ship);
        return db.DailyOperationRevisions.AsNoTracking().Where(row => row.VesselId == vessel &&
            row.SourceVersion == DailyOperationsPolicy.SourceVersion && vessels.Any() &&
            (row.Kind == "Noon" && noon || row.Kind == "Engine" && engine) &&
            db.DailyOperations.Any(head => head.Id == row.RecordId && head.VesselId == vessel && head.Kind == row.Kind));
    }

    internal static IQueryable<DailyOperationRevision> ReturnedVisible(PoseidonDbContext db, TrustedActor actor,
        Guid vessel, ShipOperationOriginPin[] pins, bool currentOnly = false)
    {
        Expression<Func<DailyOperationRevision, bool>> pairs = row => false;
        foreach (var pin in pins) {
            if (!Valid(pin)) continue;
            Expression<Func<DailyOperationRevision, bool>> match = row => row.Kind == pin.Kind && row.RecordId == pin.Id &&
                row.Revision == pin.Revision && row.RecordVersion == pin.Version && row.Hash == pin.Hash && row.SourceVersion == pin.SourceVersion &&
                (!currentOnly || db.DailyOperations.Any(head => head.VesselId == vessel && head.Id == row.RecordId && head.Kind == row.Kind &&
                    head.Revision == row.Revision && head.Version == pin.Version && head.Hash == row.Hash && head.SourceVersion == row.SourceVersion));
            var body = new ParameterReplacement(match.Parameters[0], pairs.Parameters[0]).Visit(match.Body)!;
            pairs = Expression.Lambda<Func<DailyOperationRevision, bool>>(Expression.OrElse(pairs.Body, body), pairs.Parameters);
        }
        return Visible(db, actor, vessel).Where(pairs);
    }

    private sealed class ParameterReplacement(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    internal static ShipOperationOriginPin Pin(DailyOperationRevision row) =>
        new(row.Kind, row.RecordId, row.Revision, checked((uint)row.RecordVersion), row.Hash, row.SourceVersion);

    private static DailyOriginFieldDto[] Fields(DailyOperationRevision row, Guid vessel)
    {
        string[] keys = row.Kind == "Noon"
            ? ["report_date", "report_time_utc", "activity", "fuel_summary", "rob_summary"]
            : ["log_date", "watch_period", "fuel_consumption", "bunkering_activity"];
        var fields = DailyOperationsPolicy.ReadFields(row.Kind, vessel, row.FieldsJson);
        return DailyOperationsPolicy.Schema(row.Kind).Where(field => keys.Contains(field.Key)).Select(field =>
            new DailyOriginFieldDto(field.Key, field.Type, fields.GetValueOrDefault(field.Key) ?? new("Missing", null), field.SourceRow, field.Unit)).ToArray();
    }

    private static async Task<bool> Current(HttpContext h, Guid vessel, string rights, ShipOperationOriginPin pin, bool currentOnly)
    {
        await using var scope = h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ActorResolver>(); var actor = resolver.Resolve(h.User);
        if (actor == null || !DailyOperationsEndpoints.CanRead(actor, pin.Kind) || DailyOperationsEndpoints.Rights(actor) != rights) return false;
        var db = scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();
        if (db.TenantId != actor.TenantId) return false;
        // Resource visibility and exact source head/version share one statement.
        var found = await ReturnedVisible(db, actor, vessel, [pin], currentOnly).AnyAsync(h.RequestAborted);
        var live = resolver.Resolve(h.User);
        return found && live != null && DailyOperationsEndpoints.CanRead(live, pin.Kind) && DailyOperationsEndpoints.Rights(live) == rights;
    }

    internal static void MapDailyOperationOrigins(this IEndpointRouteBuilder app) => app
        .MapGet("/api/v1/vessels/{vessel:guid}/ship-operation-origins/{kind}/{id:guid}", Read)
        .RequireAuthorization("DirectoryRead").WithName("DailyOperationOrigin").Produces<DailyOperationOriginDto>();

    private static async Task<IResult> Failure(HttpContext h, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "DailyOperationsRead", code);
        return Results.Json(new { code }, statusCode: status);
    }

    private static async Task<IResult> Read(Guid vessel, string kind, Guid id, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        long revision = 0;
        if (vessel == Guid.Empty || id == Guid.Empty || kind is not ("Noon" or "Engine") ||
            h.Request.Query.Any(q => q.Key != "revision" || q.Value.Count != 1) ||
            h.Request.Query.ContainsKey("revision") && (!long.TryParse(h.Request.Query["revision"], NumberStyles.None, CultureInfo.InvariantCulture, out revision) || revision <= 0))
            return await Failure(h, 400, "invalid_query");
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (actor == null || !DailyOperationsEndpoints.CanRead(actor, kind)) return await Failure(h, 403, "permission_denied");
        var rights = DailyOperationsEndpoints.Rights(actor);
        var query = Visible(db, actor, vessel).Where(row => row.Kind == kind && row.RecordId == id);
        query = revision > 0 ? query.Where(row => row.Revision == revision) : query.Where(row =>
            db.DailyOperations.Any(head => head.Id == id && head.VesselId == vessel && head.Kind == kind && head.Revision == row.Revision));
        var selected = await query.Select(row => new { Row = row, Current = db.DailyOperations.Any(head => head.Id == id &&
            head.VesselId == vessel && head.Kind == kind && head.Revision == row.Revision && head.Hash == row.Hash) })
            .SingleOrDefaultAsync(h.RequestAborted);
        if (selected == null) return await Failure(h, 404, "resource_not_found");
        var pin = Pin(selected.Row); if (!Valid(pin)) return await Failure(h, 503, "source_contract_changed");
        var href = $"/app/{(kind == "Noon" ? "noon-report" : "engine-daily-log")}/{vessel:D}?id={id:D}";
        var result = new DailyOperationOriginDto(vessel, pin, selected.Current, "DRAFT", "REVIEW_REQUIRED", selected.Row.Provenance,
            selected.Row.SavedAt, Fields(selected.Row, vessel), href);
        if (!await Current(h, vessel, rights, pin, selected.Current)) return await Failure(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "DailyOperationsRead", "Success", resourceId: id);
        if (!await Current(h, vessel, rights, pin, selected.Current)) return await Failure(h, 403, "permission_denied");
        return Results.Ok(result);
    }
}
