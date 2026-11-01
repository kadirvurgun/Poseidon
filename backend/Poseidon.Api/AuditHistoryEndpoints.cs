using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record AuditRecordDto(Guid Id, DateTimeOffset OccurredAt, string? ActorReference, string ActorType,
    string Action, string EntityType, Guid? ResourceId, Guid? VesselId, string? CapabilityContext,
    string? ChangeSummary, uint? BeforeVersion, uint? AfterVersion, long? BeforeRevision, long? AfterRevision, string? ReasonStatus,
    string? CorrelationId, string MetadataStatus, string? RevisionHref);
public sealed record AuditEvidenceDto(string Kind, RegulatoryRevisionDto? Profile, RegisterRevisionDto? Register);
public sealed record AuditPageDto(AuditRecordDto[] Items, int Total, int Limit, string? NextCursor);

// AUDIT-TRACE-01 / T-S1-004. Security events and internal receipt rows are deliberately not a log viewer.
public static class AuditHistoryEndpoints
{
    private static readonly string[] Types = [nameof(Tenant), nameof(Company), nameof(Vessel), nameof(VesselProfile),
        nameof(TugProfile), nameof(RegulatoryDraft), nameof(RegulatoryDraftRevision), nameof(RegisterDraft), nameof(RegisterRevision)];
    public static void MapAuditHistory(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit").RequireAuthorization("AuditRead");
        group.MapGet("", List).WithName("GetAuditHistory").Produces<AuditPageDto>().Produces(400).Produces(401).Produces(403).Produces(503);
        group.MapGet("/{id:guid}", Detail).WithName("GetAuditRecord").Produces<AuditRecordDto>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
        group.MapGet("/{id:guid}/revision", Revision).WithName("GetAuditRevision").Produces<AuditEvidenceDto>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
    }

    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static IQueryable<AuditEvent> Scoped(PoseidonDbContext db, TrustedActor actor)
    {
        // Tenant filter is explicit as well as in EF. Apply every restriction before count, paging or detail.
        var rows = db.AuditEvents.AsNoTracking().Where(x => x.TenantId == actor.TenantId && Types.Contains(x.EntityType));
        if (!actor.CanReadAudit) return rows.Where(_ => false);
        if (!(actor.TenantWideRead || actor.TenantWideManage))
        {
            var ids = actor.VesselGrants.Select(x => x.VesselId).ToArray();
            rows = rows.Where(x => x.VesselId != null && ids.Contains(x.VesselId.Value));
        }
        // Catalog identifiers and source lineage require the same capability as the actual resource.
        if (!actor.CanReadRegulatory) rows = rows.Where(x => x.EntityType != nameof(RegisterDraft) && x.EntityType != nameof(RegisterRevision));
        return rows;
    }

    private static AuditRecordDto Dto(AuditEvent e, uint? afterVersion = null) => new(e.Id, e.OccurredAt,
        Guid.TryParse(e.ActorId, out var actor) ? actor.ToString() : e.ActorId == "system:seed-import" ? e.ActorId : null,
        e.ActorType is "User" or "System" ? e.ActorType : "Unavailable", e.Action, e.EntityType,
        e.ResourceId, e.VesselId, e.CapabilityContext, e.ChangeSummary, e.BeforeVersion, afterVersion, e.BeforeRevision, e.AfterRevision, e.ReasonStatus,
        (e.CorrelationId.Length == 32 && Guid.TryParseExact(e.CorrelationId, "N", out _) || e.CorrelationId.Length == 36 && Guid.TryParseExact(e.CorrelationId, "D", out _)) ? e.CorrelationId : null,
        e.Source == null ? "HistoricalMetadataUnavailable" : e.CapabilityContext == null ? "CapabilityContextUnavailable" : "Recorded",
        e.AfterRevision is > 0 && e.ResourceId != null && (e.EntityType is nameof(RegulatoryDraft) or nameof(RegulatoryDraftRevision) or nameof(RegisterDraft) or nameof(RegisterRevision))
            ? $"/api/v1/audit/{e.Id}/revision" : null);

    private static async Task<AuditRecordDto[]> Project(PoseidonDbContext db, AuditEvent[] authorized, CancellationToken ct)
    {
        // Only enrich already-authorized records; receipt keys, fingerprints and source IDs never leave storage.
        var resources = authorized.Where(x => x.ResourceId != null).Select(x => x.ResourceId!.Value).Distinct().ToArray();
        var correlations = authorized.Select(x => x.CorrelationId).Distinct().ToArray();
        var receipts = await db.AuditEvents.AsNoTracking().Where(x => x.TenantId == db.TenantId && x.EntityType == nameof(CommandReceipt) &&
            x.ResourceId != null && resources.Contains(x.ResourceId.Value) && correlations.Contains(x.CorrelationId) && x.AfterVersion != null).ToArrayAsync(ct);
        return authorized.Select(e => Dto(e, receipts.FirstOrDefault(r => r.TenantId == e.TenantId && r.ResourceId == e.ResourceId && r.CorrelationId == e.CorrelationId)?.AfterVersion)).ToArray();
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        var q = h.Request.Query;
        var keys = new[] { "vesselId", "resourceId", "entityType", "action", "limit", "cursor" };
        async Task<IResult> Invalid() { await audit.WriteAsync(h, "AuditQuery", "Invalid"); return Results.BadRequest(new { code = "invalid_audit_query" }); }
        if (q.Any(x => !keys.Contains(x.Key) || x.Value.Count != 1 || x.Value[0]?.Length > 256)) return await Invalid();
        var take = 25;
        if (q.ContainsKey("limit") && (!int.TryParse(q["limit"], NumberStyles.None, CultureInfo.InvariantCulture, out take) || take is < 1 or > 100)) return await Invalid();
        var rows = Scoped(db, Actor(h));
        foreach (var key in new[] { "vesselId", "resourceId" })
        {
            if (!q.ContainsKey(key)) continue;
            if (q[key].ToString().Length != 36 || !Guid.TryParseExact(q[key], "D", out var id) || id == Guid.Empty) return await Invalid();
            rows = key == "vesselId" ? rows.Where(x => x.VesselId == id) : rows.Where(x => x.ResourceId == id);
        }
        if (q.ContainsKey("entityType")) {
            var type = q["entityType"].ToString(); if (!Types.Contains(type)) return await Invalid();
            rows = rows.Where(x => x.EntityType == type);
        }
        if (q.ContainsKey("action")) {
            var action = q["action"].ToString(); if (action is not ("Added" or "Modified" or "Deleted")) return await Invalid();
            rows = rows.Where(x => x.Action == action);
        }
        var cutoff = DateTimeOffset.UtcNow; DateTimeOffset? lastTime = null; Guid lastId = default;
        if (q.ContainsKey("cursor")) {
            var parts = q["cursor"].ToString().Split('~');
            if (parts.Length != 3 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var upper) ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var last) ||
                upper < DateTimeOffset.UnixEpoch.Ticks || upper > DateTimeOffset.UtcNow.Ticks || last < DateTimeOffset.UnixEpoch.Ticks || last > upper ||
                parts[2].Length != 36 || !Guid.TryParseExact(parts[2], "D", out lastId) || lastId == Guid.Empty) return await Invalid();
            cutoff = new(upper, TimeSpan.Zero); lastTime = new(last, TimeSpan.Zero);
        }
        rows = rows.Where(x => x.OccurredAt <= cutoff);
        var total = await rows.CountAsync(h.RequestAborted);
        if (lastTime != null) {
            var last = lastTime.Value;
            rows = rows.Where(x => EF.Functions.LessThan(ValueTuple.Create(x.OccurredAt, x.Id), ValueTuple.Create(last, lastId)));
        }
        var result = await rows.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(take + 1).ToArrayAsync(h.RequestAborted);
        var page = result.Take(take).ToArray();
        var cursor = result.Length > take ? $"{cutoff.Ticks}~{page[^1].OccurredAt.Ticks}~{page[^1].Id:D}" : null;
        var projection = await Project(db, page, h.RequestAborted);
        await audit.WriteAsync(h, "AuditQuery", "Success");
        return Results.Ok(new AuditPageDto(projection, total, take, cursor));
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        if (h.Request.Query.Count != 0) {
            await audit.WriteAsync(h, "AuditDetail", "Invalid");
            return Results.BadRequest(new { code = "invalid_audit_query" });
        }
        var row = await Scoped(db, Actor(h)).SingleOrDefaultAsync(x => x.Id == id, h.RequestAborted);
        var projection = row == null ? null : (await Project(db, [row], h.RequestAborted))[0];
        await audit.WriteAsync(h, "AuditDetail", row == null ? "NotFound" : "Success");
        return projection == null ? Results.NotFound() : Results.Ok(projection);
    }

    private static async Task<IResult> Revision(Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        if (h.Request.Query.Count != 0) {
            await audit.WriteAsync(h, "AuditRevision", "Invalid");
            return Results.BadRequest(new { code = "invalid_audit_query" });
        }
        var row = await Scoped(db, Actor(h)).SingleOrDefaultAsync(x => x.Id == id, h.RequestAborted);
        // Only exact, committed immutable revisions. Historical gaps produce 404, never an invented snapshot.
        AuditEvidenceDto? evidence = null;
        if (row != null && row.AfterRevision is > 0 && row.ResourceId != null) {
            if (row.EntityType is nameof(RegulatoryDraft) or nameof(RegulatoryDraftRevision)) {
                var r = await db.RegulatoryDraftRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == row.ResourceId && x.Revision == row.AfterRevision, h.RequestAborted);
                if (r != null) evidence = new("RegulatoryProfile", new(r.Revision, r.SavedAt, r.Provenance, RegulatoryDraftStorage.Deserialize(r.ContentJson)), null);
            } else if (Actor(h).CanReadRegulatory && (row.EntityType is nameof(RegisterDraft) or nameof(RegisterRevision))) {
                var r = await db.RegisterRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.ResourceId && x.Kind == row.ResourceKind && x.Revision == row.AfterRevision, h.RequestAborted);
                if (r != null) evidence = new(r.Kind, null, new(r.Id, r.Kind, r.Revision, r.SavedAt, r.Provenance, r.Hash, RegisterStorage.Deserialize(r.ContentJson), r.SourceId, r.SourceRevision));
            }
        }
        await audit.WriteAsync(h, "AuditRevision", evidence == null ? "NotFound" : "Success");
        return evidence == null ? Results.NotFound(new { code = "historical_evidence_unavailable" }) : Results.Ok(evidence);
    }
}
