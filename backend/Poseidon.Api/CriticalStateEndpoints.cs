using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// VISIBLE_ONLY (61.03 p109-110): the list carries only states this reader may know exist;
// there is no hidden/redacted count and no list of withheld sources.
public sealed record CriticalStatesDto(Guid VesselId, DateTimeOffset ReadAt, string Scope, CriticalStateExplanation[] States);

// DEV-024 / T-S2-005. Critical-state explanations (source, reason codes, freshness, blockers)
// composed server-side from the owning reads. Disclosure never exceeds the owner's own detail
// endpoint (DEV024-DEC-01): FULL when the reader could open that detail, STATE_ONLY (coarse,
// non-positive state without source/reason/blocker) when the reader has the vessel scope but
// not the detail, and the component is omitted when there is no state-level capability.
public static class CriticalStateEndpoints
{
    private static readonly string[] Keys = [CriticalStatePolicy.Onboarding, CriticalStatePolicy.ProfileFreshness, CriticalStatePolicy.Applicability];

    public static void MapCriticalStates(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/vessels/{id:guid}/critical-states", (Guid id, HttpContext http, PoseidonDbContext db, SecurityAudit audit,
                TimeProvider clock, [FromQuery] string? key) => Read(id, http, db, audit, clock))
            .RequireAuthorization("DirectoryRead").WithName("GetCriticalStates").Produces<CriticalStatesDto>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(503)
            .WithDescription("Critical-state explanations for one vessel: owning source and revision, reason codes, server-computed freshness and blockers with resolution types. Never a ready, verified or compliant outcome. Readers without the owning detail authority receive STATE_ONLY entries without source details; components without a state capability are omitted.");
    }

    private static async Task<IResult> Read(Guid id, HttpContext http, PoseidonDbContext db, SecurityAudit audit, TimeProvider clock)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var query = http.Request.Query;
        string? only = null;
        if (query.Any(x => x.Key != "key" || x.Value.Count != 1) || query.ContainsKey("key") && !Keys.Contains(only = query["key"].ToString()))
        {
            await audit.WriteAsync(http, "CriticalStateRead", "Invalid", resourceId: id);
            return Results.BadRequest(new { code = "invalid_query" });
        }
        var actors = http.RequestServices.GetRequiredService<ActorResolver>();
        var actor = actors.Resolve(http.User)!;
        var access = ComplianceWorkspaceEndpoints.AccessHash(actor);
        var ct = http.RequestAborted;
        CriticalStateExplanation? onboarding = null, freshness = null, applicability = null;
        bool profileDetail, fieldDetail, policyUnavailable;
        long? headRevision;
        Guid? evaluation = null;
        DateTimeOffset now;
        try
        {
            // One snapshot for the vessel scope, profile head, field sources and evaluation.
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(v => v.Id == id, ct);
            if (vessel == null) { await audit.WriteAsync(http, "CriticalStateRead", "NotFound", resourceId: id); return Results.NotFound(); }
            // Owning detail gates, exactly as the owner endpoints apply them: the onboarding owner
            // (current draft binding) and the DEV-020 field owner (any bound revision).
            profileDetail = (await RegulatoryDraftEndpoints.ReadCurrent(actor, id, db, ct)).Readable;
            fieldDetail = profileDetail && await VesselProfileFieldEndpoints.Readable(actor, id, db, ct);
            // The full state is computed server-side, then redacted before serialization.
            var projection = await VesselProfileFieldEndpoints.Project(actor.TenantId, id, db, http, clock, ct);
            var head = projection.Head;
            headRevision = head?.Revision;
            var content = head == null ? null : RegulatoryDraftStorage.Deserialize(head.ContentJson);
            now = projection.CheckedAt;
            // The coarse UNAVAILABLE derives from the policy service, never from withheld fields.
            policyUnavailable = projection.Policy.Status == "UNAVAILABLE";
            if (only is null or CriticalStatePolicy.Onboarding)
                onboarding = CriticalStatePolicy.ForOnboarding(id, ComplianceOnboardingPolicy.Assess(vessel.Id, vessel.Code, vessel.Name, content),
                    head?.Revision, head?.SavedAt, head?.Provenance ?? "Missing", projection.Summary, now, $"/app/fleet/{id:D}/onboarding");
            if (only is null or CriticalStatePolicy.ProfileFreshness)
                freshness = CriticalStatePolicy.ForProfileFreshness(id, head?.Revision ?? 0, head?.SavedAt, head?.Provenance ?? "Missing",
                    [.. projection.Fields.Select(f => (f.FieldPath, f.Freshness))], projection.Summary, now, $"/app/fleet/{id:D}/profile");
            // 61.03 p214: no independent minimal-result capability exists for applicability, so a
            // reader without the review gate gets no component at all (never emulated by broad read).
            // DEV024-DEC-05: the review gate includes file.read, so its reader always holds the
            // profile and field detail; there is no STATE_ONLY applicability (see Compose).
            if (only is (null or CriticalStatePolicy.Applicability) && ControlEvidenceEndpoints.Read(actor) && fieldDetail)
            {
                var review = await ComplianceWorkspaceEndpoints.Review(id, actor, http, db, now);
                evaluation = review.EvaluationId;
                applicability = CriticalStatePolicy.ForApplicability(id, review.Available, review.Current, review.EvaluationId,
                    review.EvaluatedAt, review.Blockers, projection.Summary, now, review.Href);
            }
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
        {
            await audit.WriteAsync(http, "CriticalStateRead", "service_unavailable", resourceId: id);
            return Results.StatusCode(503);
        }

        // Each sub-object is disclosed only under its own owner's detail predicate (QA F1):
        // onboarding by the onboarding owner, its embedded DEV-020 freshness summary and the
        // profile-freshness component by the DEV-020 owner, applicability by the review owner.
        CriticalStatesDto Compose(bool profile, bool field)
        {
            var states = new List<CriticalStateExplanation>();
            if (onboarding != null)
                states.Add(!profile ? CriticalStatePolicy.Redact(onboarding) : field ? onboarding : CriticalStatePolicy.WithoutProfileFreshness(onboarding));
            if (freshness != null) states.Add(profile && field ? freshness : CriticalStatePolicy.Redact(freshness, policyUnavailable));
            if (applicability != null && profile && field) states.Add(applicability);
            return new(id, now, "VISIBLE_ONLY", [.. states]);
        }

        // Live access fence: a grant change during composition withholds the whole response.
        bool Current() => actors.Resolve(http.User) is { CanRead: true } live && ComplianceWorkspaceEndpoints.AccessHash(live) == access;
        // Live resource fence (QA F2), as ComplianceWorkspaceEndpoints.ResponseCurrent: a fresh DI
        // scope reads outside the composition snapshot, so a construction-file binding or an
        // evaluation withdrawn after the snapshot is seen. All predicates share one statement.
        async Task<(bool Visible, bool Profile, bool Field)> Fence()
        {
            if (!Current()) return (false, false, false);
            await using var scope = http.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var live = scope.ServiceProvider.GetRequiredService<ActorResolver>().Resolve(http.User);
            if (live is not { CanRead: true } || ComplianceWorkspaceEndpoints.AccessHash(live) != access) return (false, false, false);
            var fresh = scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();
            var files = live.Permissions.Contains("file.read");
            var evaluations = OperationEquipmentCommands.Evaluations(fresh, live);
            var gates = await live.Scope(fresh.Vessels.AsNoTracking()).Where(v => v.Id == id).Select(v => new
            {
                Profile = files || !fresh.RegulatoryDrafts.Any(p => p.VesselId == id && p.ConstructionFileId != null) &&
                    !fresh.RegulatoryDraftRevisions.Any(p => p.VesselId == id && p.Revision == headRevision && p.ConstructionFileId != null),
                Field = files || !fresh.RegulatoryDraftRevisions.Any(p => p.VesselId == id && p.ConstructionFileId != null),
                Evaluation = evaluation == null || evaluations.Any(e => e.Id == evaluation && e.VesselId == id)
            }).SingleOrDefaultAsync(ct);
            // A withdrawn evaluation is denied like the review owner's fence (403); a new file
            // binding narrows the disclosure to what the owners would now grant.
            return gates is { Evaluation: true } && Current() ? (true, gates.Profile, gates.Field) : (false, false, false);
        }

        try
        {
            var before = await Fence();
            if (!before.Visible) { await audit.WriteAsync(http, "CriticalStateRead", "permission_denied", resourceId: id); return Results.StatusCode(403); }
            await audit.WriteAsync(http, "CriticalStateRead", "Success", resourceId: id);
            var after = await Fence();
            if (!after.Visible) { await audit.WriteAsync(http, "CriticalStateRead", "permission_denied", resourceId: id); return Results.StatusCode(403); }
            return Results.Ok(Compose(profileDetail && before.Profile && after.Profile, fieldDetail && before.Field && after.Field));
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
        {
            await audit.WriteAsync(http, "CriticalStateRead", "service_unavailable", resourceId: id);
            return Results.StatusCode(503);
        }
    }
}
