using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DefectAssetSummaryDto(string? Code, string? Name, string Level, string? Criticality,
    string? Maker, string? Model, string? Location);
public sealed record DefectAssetContextDto(Guid VesselId, DefectAssetPin Pin, bool Current, string Qualification,
    string Provenance, DateTimeOffset SavedAt, DefectAssetSummaryDto Asset, string Href);

// Exact engineering context over the actual equipment aggregate. No source
// notes, responsible-role IDs, user identities or operational fitness escape.
internal static class DefectAssetContextReader
{
    internal static bool CanRead(TrustedActor? actor) => actor?.CanRead == true &&
        actor.Permissions.Contains("equipment.read") && actor.Permissions.Contains("defect.read");
    internal static bool Valid(DefectAssetPin? pin) => pin is { Revision: > 0, Version: > 0, Hash.Length: 64 } &&
        pin.Id != Guid.Empty && pin.Hash.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    internal static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    internal static IQueryable<AssetRevision> Visible(PoseidonDbContext db, TrustedActor actor, Guid vessel)
    {
        var allowed = CanRead(actor);
        var vessels = actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel);
        return db.AssetRevisions.AsNoTracking().Where(row => allowed && row.VesselId == vessel && vessels.Any() &&
            db.Assets.Any(head => head.VesselId == vessel && head.Id == row.AssetId));
    }

    // AssetRevision.ContentJson predates SaveChanges and has no committed-xmin
    // column. Capture the exact JSON that hashes to an established pin, then
    // return a COMPOSABLE predicate: callers must combine this with their own
    // returned/head visibility in ONE final fresh SQL, not await two fences.
    internal static async Task<Expression<Func<Vessel, bool>>?> CaptureFence(PoseidonDbContext db, TrustedActor actor,
        Guid vessel, DefectAssetPin[] pins, bool currentOnly = false, CancellationToken ct = default)
    {
        if (!CanRead(actor) || pins.Length > 256 || pins.Any(pin => !Valid(pin))) return null;
        Expression<Func<Vessel, bool>> predicate = v => true;
        if (pins.Length == 0) return predicate;
        var unique = pins.Distinct().ToArray(); var ids = unique.Select(pin => pin.Id).Distinct().ToArray();
        var visible = Visible(db, actor, vessel);
        // Only requested pairs, not an unbounded asset-history scan.
        Expression<Func<AssetRevision, bool>> requested = row => false;
        foreach (var pin in unique) {
            Expression<Func<AssetRevision, bool>> pair = row => row.AssetId == pin.Id && row.Revision == pin.Revision;
            requested = Or(requested, pair);
        }
        var captured = await visible.Where(row => ids.Contains(row.AssetId)).Where(requested)
            .Select(row => new { row.AssetId, row.Revision, row.ContentJson }).ToArrayAsync(ct);
        foreach (var pin in unique) {
            var row = captured.SingleOrDefault(row => row.AssetId == pin.Id && row.Revision == pin.Revision);
            if (row == null || Hash(row.ContentJson) != pin.Hash) return null;
            var content = row.ContentJson;
            Expression<Func<Vessel, bool>> match = v => visible.Any(source => source.AssetId == pin.Id &&
                source.Revision == pin.Revision && source.ContentJson == content &&
                (!currentOnly || db.Assets.Any(head => head.VesselId == vessel && head.Id == pin.Id &&
                    head.Revision == pin.Revision && head.Version == pin.Version)));
            predicate = And(predicate, match);
        }
        return predicate;
    }

    private sealed class Replace(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
    private static Expression<Func<T, bool>> And<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) =>
        Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left.Body, new Replace(right.Parameters[0], left.Parameters[0]).Visit(right.Body)!), left.Parameters);
    private static Expression<Func<T, bool>> Or<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) =>
        Expression.Lambda<Func<T, bool>>(Expression.OrElse(left.Body, new Replace(right.Parameters[0], left.Parameters[0]).Visit(right.Body)!), left.Parameters);

    internal static void MapDefectAssetContexts(this IEndpointRouteBuilder app) => app
        .MapGet("/api/v1/vessels/{vessel:guid}/defect-asset-context/{id:guid}", Read)
        .RequireAuthorization("DirectoryRead").WithName("DefectAssetContext").Produces<DefectAssetContextDto>();

    private static async Task<IResult> Failure(HttpContext h, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "DefectAssetContextRead", code);
        return Results.Json(new { code }, statusCode: status);
    }

    private static async Task<bool> Current(HttpContext h, Guid vessel, DefectAssetPin pin, string rights)
    {
        await using var scope = h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ActorResolver>(); var actor = resolver.Resolve(h.User);
        if (!CanRead(actor) || DailyOperationsEndpoints.Rights(actor!) != rights) return false;
        var db = scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();
        if (db.TenantId != actor!.TenantId) return false;
        var fence = await CaptureFence(db, actor, vessel, [pin], true, h.RequestAborted);
        if (fence == null) return false;
        var found = await actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel).Where(fence).AnyAsync(h.RequestAborted);
        var live = resolver.Resolve(h.User);
        return found && CanRead(live) && DailyOperationsEndpoints.Rights(live!) == rights;
    }

    private static async Task<IResult> Read(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        long revision = 0;
        if (vessel == Guid.Empty || id == Guid.Empty || h.Request.Query.Any(q => q.Key != "revision" || q.Value.Count != 1) ||
            h.Request.Query.ContainsKey("revision") && (!long.TryParse(h.Request.Query["revision"], NumberStyles.None,
                CultureInfo.InvariantCulture, out revision) || revision <= 0)) return await Failure(h, 400, "invalid_query");
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (!CanRead(actor)) return await Failure(h, 403, "permission_denied");
        var rights = DailyOperationsEndpoints.Rights(actor!);
        var current = await Visible(db, actor!, vessel).Where(source => source.AssetId == id)
            .Join(db.Assets.AsNoTracking().Where(head => head.VesselId == vessel && head.Id == id),
                source => new { source.AssetId, source.Revision }, head => new { AssetId = head.Id, head.Revision },
                (source, head) => new { Source = source, head.Version }).SingleOrDefaultAsync(h.RequestAborted);
        if (current == null) return await Failure(h, 404, "resource_not_found");
        // We cannot manufacture the committed xmin of an older asset snapshot.
        // Existing owning defect history retains its original established pin.
        if (revision > 0 && revision != current.Source.Revision) return await Failure(h, 409, "asset_context_stale");
        var pin = new DefectAssetPin(id, current.Source.Revision, current.Version, Hash(current.Source.ContentJson));
        Asset? asset;
        try { asset = JsonSerializer.Deserialize<Asset>(current.Source.ContentJson); }
        catch (JsonException) { return await Failure(h, 503, "source_contract_changed"); }
        if (!Valid(pin) || asset == null || asset.Id != id || asset.TenantId != actor!.TenantId || asset.VesselId != vessel ||
            asset.Revision != pin.Revision || asset.Level is not ("System" or "Equipment" or "Component"))
            return await Failure(h, 503, "source_contract_changed");
        var result = new DefectAssetContextDto(vessel, pin, true, "REVIEW_REQUIRED", asset.Provenance, current.Source.SavedAt,
            new(asset.Code, asset.Name, asset.Level, asset.Criticality, asset.Maker, asset.Model, asset.Location),
            $"/app/equipment-hierarchy/{vessel:D}?id={id:D}");
        if (!await Current(h, vessel, pin, rights)) return await Failure(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "DefectAssetContextRead", "Success", resourceId: id);
        if (!await Current(h, vessel, pin, rights)) return await Failure(h, 403, "permission_denied");
        return Results.Ok(result);
    }
}
