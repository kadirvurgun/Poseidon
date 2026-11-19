using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-026 / T-S3-001, 18.01 Backlog row 27 (requirement-master / /api/v1/requirements).
// Read projection only: requirement register revisions plus the lifecycle derived from the
// current per-rule qualification heads. No lifecycle state is stored here; writes stay on
// /requirement-drafts (immutable revisions, xmin + receipt) and source governance.
public sealed record RequirementQualificationDto(Guid RuleId, long RuleRevision, long QualificationRevision, long RequirementRevision,
    string State, string Lifecycle, bool Current, DateTimeOffset SavedAt);
public sealed record RequirementDto(Guid Id, long Revision, uint Version, string Lifecycle, string LifecycleBasis, DateTimeOffset SavedAt,
    string Provenance, string Hash, RegisterContent Content, Guid? SourceId, long? SourceRevision, RequirementQualificationDto[] Qualifications);

public static class RequirementEndpoints
{
    private const int MaxQualifications = 100;
    public static void MapRequirements(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/requirements").RequireAuthorization("RegulatoryDraftRead");
        group.MapGet("", (HttpContext h, PoseidonDbContext db, SecurityAudit a, string? q, string? offset, string? limit) => List(h, db, a))
            .WithName("ListRequirements").Produces<PageDto<RequirementDto>>().ProducesProblem(400).Produces(401).Produces(403).Produces(503);
        group.MapGet("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit a) => Read(id, h, db, a))
            .WithName("GetRequirement").Produces<RequirementDto>().ProducesProblem(400).Produces(401).Produces(403).ProducesProblem(404).Produces(503);
    }
    private static IResult Problem(HttpContext h, int status, string code) =>
        Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = h.TraceIdentifier });
    // Parsed inside the audited boundary, matching the register list contract.
    private static bool Query(HttpContext h, bool list, out string text, out int skip, out int take)
    {
        text = ""; skip = 0; take = 25;
        var query = h.Request.Query;
        if (!list) return query.Count == 0;
        if (query.Any(x => x.Value.Count != 1 || x.Key is not ("offset" or "limit" or "q"))) return false;
        if (query.ContainsKey("offset") && (!int.TryParse(query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out skip) || skip > 100000)) return false;
        if (query.ContainsKey("limit") && (!int.TryParse(query["limit"], NumberStyles.None, CultureInfo.InvariantCulture, out take) || take is < 1 or > 100)) return false;
        var q = query["q"].ToString();
        if (q.Length > 100 || q.Any(char.IsControl)) return false;
        text = q.Trim(); return true;
    }
    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        if (!Query(h, true, out var text, out var skip, out var take)) { await audit.WriteAsync(h, "RequirementList", "Invalid"); return Problem(h, 400, "invalid_query"); }
        // Tenant query filters scope every set; literal Contains never interprets user wildcards.
        var rows = await (from d in db.RegisterDrafts.AsNoTracking() join r in db.RegisterRevisions.AsNoTracking()
            on new { d.TenantId, d.Kind, d.Id, d.Revision } equals new { r.TenantId, r.Kind, r.Id, r.Revision }
            where d.Kind == "Requirement" && (text == "" || d.ContentJson.Contains(text) || d.Id.ToString().Contains(text))
            orderby d.SavedAt descending, d.Id
            select new { Draft = d, Revision = r }).Skip(skip).Take(take + 1).ToArrayAsync(h.RequestAborted);
        var page = rows.Take(take).ToArray();
        var qualifications = await Qualifications(db, page.Select(x => x.Draft.Id).ToArray(), h.RequestAborted);
        var projection = new PageDto<RequirementDto>(page.Select(x => Dto(x.Draft, x.Revision, qualifications)).ToArray(), skip, take, rows.Length > take);
        await audit.WriteAsync(h, "RequirementList", "Success");
        return Results.Ok(projection);
    }
    private static async Task<IResult> Read(Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        if (!Query(h, false, out _, out _, out _)) { await audit.WriteAsync(h, "RequirementRead", "Invalid", resourceId: id); return Problem(h, 400, "invalid_query"); }
        var d = await db.RegisterDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Kind == "Requirement" && x.Id == id, h.RequestAborted);
        if (d == null) { await audit.WriteAsync(h, "RequirementRead", "NotFound", resourceId: id); return Problem(h, 404, "resource_not_found"); }
        var r = await db.RegisterRevisions.AsNoTracking().SingleAsync(x => x.Kind == "Requirement" && x.Id == id && x.Revision == d.Revision, h.RequestAborted);
        var projection = Dto(d, r, await Qualifications(db, [id], h.RequestAborted));
        await audit.WriteAsync(h, "RequirementRead", "Success", resourceId: id);
        return Results.Ok(projection);
    }
    private sealed record Row(Guid RequirementId, long RequirementRevision, Guid RuleId, long RuleRevision, long CurrentRuleRevision,
        long QualificationRevision, string State, DateTimeOffset SavedAt);
    // Only each rule's qualification HEAD counts; older events are immutable history.
    private static async Task<ILookup<Guid, Row>> Qualifications(PoseidonDbContext db, Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return Array.Empty<Row>().ToLookup(x => x.RequirementId);
        var rows = await (from head in db.RegulatoryQualificationHeads.AsNoTracking()
            join e in db.RegulatoryQualificationEvents.AsNoTracking() on new { head.TenantId, head.RuleId, head.Revision } equals new { e.TenantId, e.RuleId, e.Revision }
            join rule in db.RuleDrafts.AsNoTracking() on new { head.TenantId, Id = head.RuleId } equals new { rule.TenantId, rule.Id }
            join rr in db.RuleRevisions.AsNoTracking() on new { e.TenantId, Id = e.RuleId, Revision = e.RuleRevision } equals new { rr.TenantId, rr.Id, rr.Revision }
            where rr.RequirementKind == "Requirement" && rr.RequirementId != null && rr.RequirementRevision != null && ids.Contains(rr.RequirementId.Value)
            orderby e.SavedAt descending, e.RuleId
            select new Row(rr.RequirementId!.Value, rr.RequirementRevision!.Value, e.RuleId, e.RuleRevision, rule.Revision, e.Revision, e.State, e.SavedAt))
            .Take(ids.Length * MaxQualifications).ToArrayAsync(ct);
        return rows.ToLookup(x => x.RequirementId);
    }
    private static RequirementDto Dto(RegisterDraft d, RegisterRevision r, ILookup<Guid, Row> qualifications)
    {
        // A qualification counts toward the current lifecycle only when it is the head for the
        // rule's current revision AND that revision pins this requirement's current revision.
        // A new requirement revision therefore starts as DRAFT while the prior in-force
        // revision stays visible here and in /requirement-drafts/{id}/history.
        var items = qualifications[d.Id].Take(MaxQualifications).Select(x => new RequirementQualificationDto(x.RuleId, x.RuleRevision, x.QualificationRevision,
            x.RequirementRevision, x.State, RequirementLifecyclePolicy.Map(x.State), x.RuleRevision == x.CurrentRuleRevision && x.RequirementRevision == d.Revision, x.SavedAt)).ToArray();
        var (lifecycle, basis) = RequirementLifecyclePolicy.Derive(items.Where(x => x.Current).Select(x => x.State));
        return new(d.Id, d.Revision, d.Version, lifecycle, basis, r.SavedAt, r.Provenance, r.Hash, RegisterStorage.Deserialize(r.ContentJson), r.SourceId, r.SourceRevision, items);
    }
}
