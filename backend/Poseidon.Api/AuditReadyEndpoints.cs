using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-032 / T-S3-007 (18.01 Backlog row:33): an auditor's entry point to the requirement ->
// control -> evidence -> verification -> decision drill-down. It lists only the chains this
// human trace reader is explicitly appointed to, with the exact pinned source/requirement
// revisions and their DECLARED (unverified) locator text. The per-chain states/timestamps stay
// in GET /api/v1/audit-ready, which re-authorizes every request. Nothing here is a readiness
// or approval statement; an unavailable revision is reported as Unavailable, never as ready.
public sealed record AuditReadySource(Guid SourceId, long SourceRevision, Guid RequirementId, long RequirementRevision, Guid? RuleId, long? RuleRevision,
    string Availability, string LocatorState, string? Locator, string TitleState, string? Title, string Provenance, string? SourceHash, DateTimeOffset? SourceSavedAt);
public sealed record AuditReadyChain(Guid EvidenceId, long EvidenceRevision, Guid ControlId, long ControlRevision, AuditReadySource[] Sources);
public sealed record AuditReadyChains(Guid VesselId, DateTimeOffset AsOf, AuditReadyChain[] Items, int Unavailable, bool HasMore);

public static class AuditReadyEndpoints
{
    public const int MaxChains = 32;
    // Menu/route projection only; GET /audit-ready still requires an exact reader appointment.
    public static bool CanRead(TrustedActor actor) => actor.CanRead && actor.CanReadAudit && actor.Permissions.Contains("human.trace.read") && actor.RoleContext != null;

    // Same appointment semantics as the trace reader in HumanReviewEndpoints, scoped to a vessel.
    public static HumanTraceReader[] Readers(IReadOnlyList<Membership> memberships, TrustedActor actor, Guid vessel, DateTimeOffset now)
    {
        if (!CanRead(actor)) return [];
        var members = memberships.Where(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).ToArray();
        if (members.Length != 1 || !string.Equals(members[0].ActorKind, "Human", StringComparison.Ordinal)) return [];
        var roles = actor.RoleContext!.Split(',');
        var all = members[0].HumanTraceReaders ?? [];
        return all.Where(g => g.Id != Guid.Empty && all.Count(x => x.Id == g.Id) == 1 && g.VesselId == vessel && g.EvidenceId != Guid.Empty && g.ControlId != Guid.Empty &&
                g.RoleId != Guid.Empty && roles.Contains(g.RoleId.ToString("D")) && g.ValidFrom <= now && now < g.ValidUntil &&
                g.Kinds.Length > 0 && g.Kinds.All(k => k is "Verification" or "Decision") && g.DecisionTypes.Length <= 32)
            .OrderBy(g => g.EvidenceId).ThenBy(g => g.ControlId).ThenBy(g => g.Id).ToArray();
    }

    private static string AccessHash(TrustedActor actor, HumanTraceReader[] readers) =>
        RegisterStorage.Hash(JsonSerializer.Serialize(new { actor.Id, actor.TenantId, actor.RoleContext, permissions = actor.Permissions.Order(StringComparer.Ordinal), readers }));

    public static void MapAuditReady(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/audit-ready/chains", Chains).RequireAuthorization("DirectoryRead").WithName("GetAuditReadyChains")
            .Produces<AuditReadyChains>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);

    private static async Task<IResult> Fail(HttpContext h, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "HumanTraceRead", code);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = h.TraceIdentifier });
    }

    private static async Task<IResult> Chains(HttpContext h, PoseidonDbContext db, ActorResolver actors, FederationSettings settings)
    {
        var a = actors.Resolve(h.User)!; var ct = h.RequestAborted;
        h.Response.Headers.CacheControl = "no-store";
        if (!CanRead(a)) return await Fail(h, 403, "permission_denied");
        // Closed query: exactly one vessel selector, nothing that could widen the reader scope.
        if (h.Request.Query.Count != 1 || h.Request.Query["vesselId"].Count != 1 ||
            !Guid.TryParseExact(h.Request.Query["vesselId"], "D", out var vessel) || vessel == Guid.Empty) return await Fail(h, 400, "invalid_query");
        if (!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == vessel, ct)) return await Fail(h, 404, "resource_not_found");
        var now = DateTimeOffset.UtcNow;
        var readers = Readers(settings.Memberships, a, vessel, now);
        var accessHash = AccessHash(a, readers);
        var pairs = readers.Select(g => (g.EvidenceId, g.ControlId)).Distinct().ToArray();
        var items = new List<AuditReadyChain>(); var unavailable = 0;
        foreach (var (evidence, control) in pairs.Take(MaxChains)) {
            var edge = await db.ControlEvidenceEdges.AsNoTracking().Where(x => x.VesselId == vessel && x.Kind == "Evidence" && x.Id == evidence && x.TargetKind == "Control" && x.TargetId == control)
                .OrderByDescending(x => x.Revision).ThenByDescending(x => x.TargetRevision).FirstOrDefaultAsync(ct);
            var visible = edge != null &&
                await HumanReviewEndpoints.Visible(db, vessel, a, true).AnyAsync(x => x.Kind == "Evidence" && x.Id == evidence && x.Revision == edge.Revision && x.SavedAt <= now, ct) &&
                await HumanReviewEndpoints.Visible(db, vessel, a, true).AnyAsync(x => x.Kind == "Control" && x.Id == control && x.Revision == edge.TargetRevision && x.SavedAt <= now, ct);
            if (!visible) { unavailable++; continue; }
            var pins = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == control && x.Revision == edge!.TargetRevision)
                .OrderBy(x => x.RequirementId).ThenBy(x => x.SourceId).Take(32).ToArrayAsync(ct);
            var sources = new List<AuditReadySource>();
            foreach (var pin in pins) {
                var source = await db.RegisterRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Kind == "Source" && x.Id == pin.SourceId && x.Revision == pin.SourceRevision && x.SavedAt <= now, ct);
                if (source == null) {
                    sources.Add(new(pin.SourceId, pin.SourceRevision, pin.RequirementId, pin.RequirementRevision, pin.RuleId, pin.RuleRevision, "Unavailable", "Unknown", null, "Unknown", null, "Unavailable", null, null));
                    continue;
                }
                var content = RegisterStorage.Deserialize(source.ContentJson);
                // Missing/Unknown locator text is passed through as its state, never filled in.
                string? Known(Poseidon.Domain.TextInput input) => input.State == "Known" ? input.Value : null;
                sources.Add(new(pin.SourceId, pin.SourceRevision, pin.RequirementId, pin.RequirementRevision, pin.RuleId, pin.RuleRevision, "Available",
                    content.Locator.State, Known(content.Locator), content.Title.State, Known(content.Title), source.Provenance, source.Hash, source.SavedAt));
            }
            items.Add(new(evidence, edge!.Revision, control, edge.TargetRevision, sources.ToArray()));
        }
        // Withdrawn appointments during the awaits deny the response bytes.
        var live = actors.Resolve(h.User);
        if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || AccessHash(live, Readers(settings.Memberships, live, vessel, DateTimeOffset.UtcNow)) != accessHash)
            return await Fail(h, 403, "trace_reader_required");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "HumanTraceRead", "Success", resourceId: vessel);
        return Results.Ok(new AuditReadyChains(vessel, now, items.ToArray(), unavailable, pairs.Length > MaxChains));
    }
}
