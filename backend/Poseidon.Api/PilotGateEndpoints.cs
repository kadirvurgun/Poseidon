using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotSupportCheckCommand(string Item, string State, string? EvidenceReference, string Note, [property: JsonRequired] long ExpectedSequence);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotGateDecisionCommand(string Decision, string Rationale, string[] Conditions, string ExpectedReviewHash, [property: JsonRequired] long ExpectedSequence,
    Guid AuthorityId);

public sealed record PilotSupportCheckDto(Guid Id, string Item, long Sequence, Guid? PreviousCheckId, string State, string? EvidenceReference, string Note,
    Guid ActorId, DateTimeOffset RecordedAt);
public sealed record PilotGateDecisionDto(Guid Id, long Sequence, Guid? PreviousDecisionId, string Decision, string Recommendation, string ReviewHash, string[] Conditions,
    string Rationale, string Duty, Guid ActorId, DateTimeOffset RecordedAt, string Validity);
public sealed record PilotSupportItemView(string Item, PilotSupportCheckDto? Latest);
public sealed record PilotGateView(PilotGateReviewOutcome Review, PilotSupportItemView[] Support, PilotGateDecisionDto[] Decisions, long Sequence,
    PilotGateAuthorityDto[] Authorities, bool CanDecide, bool CanRecordSupport, string[] AllowedDecisions, DateTimeOffset AssessedAt,
    string SourceLocator = PilotGatePolicy.SourceLocator, string Authority = PilotGatePolicy.Authority);

// DEV-075 pilot go/no-go review (18.01 row 76; DoR DoD Gates row 4; 23.03 §5). GET aggregates the gate readers into
// an advisory recommendation bound to a hash; POST records the Product Owner's human decision against that exact hash.
// The engine never emits GO; GO/CONDITIONAL_GO on a failed/unknown review is 409, on a deferred gate 422; AI, automation and administrators never decide.
public static class PilotGateEndpoints
{
    private const string ReadAction = "PilotGateRead", CommandAction = "PilotGateCommand";
    public const string CurrentValidity = "CURRENT", StaleValidity = "STALE";

    public static void MapPilotGate(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/pilot-gate").RequireAuthorization("DirectoryRead");
        g.MapGet("", Read).WithName("PilotGateRead").Produces<PilotGateView>();
        g.MapPost("/support-checks", Support).WithName("PilotSupportCheckRecord").Accepts<PilotSupportCheckCommand>("application/json").Produces<PilotSupportCheckDto>();
        g.MapPost("/decisions", Decide).WithName("PilotGateDecide").Accepts<PilotGateDecisionCommand>("application/json").Produces<PilotGateDecisionDto>();
    }

    private static FederationSettings Settings(HttpContext h) => h.RequestServices.GetRequiredService<FederationSettings>();

    internal static PilotSupportCheckDto Dto(PilotSupportCheck c) => new(c.Id, c.Item, c.Sequence, c.PreviousCheckId, c.State, c.EvidenceReference, c.Note, c.ActorId, c.RecordedAt);
    internal static PilotGateDecisionDto Dto(PilotGateDecision d, string validity) => new(d.Id, d.Sequence, d.PreviousDecisionId, d.Decision, d.Recommendation, d.ReviewHash,
        JsonSerializer.Deserialize<string[]>(d.ConditionsJson) ?? [], d.Rationale, d.Duty, d.ActorId, d.RecordedAt, validity);

    private static string[] Allowed(PilotGateReviewOutcome review) =>
        PilotGatePolicy.Decisions.Where(d => PilotGatePolicy.Refusal(d, review, d == PilotGatePolicy.ConditionalGo ? ["condition"] : []) == null).ToArray();

    private static async Task<IResult> Read(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!PilotAccess.GateRead(a)) return await TugReadinessHttp.Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Count > 0) return await TugReadinessHttp.Fail(h, ReadAction, 400, "invalid_query");
        var now = TugReadinessHttp.Now(h);
        var review = await PilotGateReviewer.Review(h, db, now, ct);
        var support = await PilotGateReviewer.LatestSupport(db, ct);
        var decisions = await db.PilotGateDecisions.AsNoTracking().OrderByDescending(d => d.Sequence).Take(32).ToArrayAsync(ct);
        var grants = PilotAccess.Resolve(Settings(h), a, now);
        await TugReadinessHttp.Observe(h, ReadAction, "Success");
        return Results.Ok(new PilotGateView(review,
            PilotGatePolicy.SupportItems.Select(i => new PilotSupportItemView(i, support.FirstOrDefault(s => s.Item == i) is { } row ? Dto(row) : null)).ToArray(),
            decisions.Select(d => Dto(d, d.ReviewHash == review.Hash ? CurrentValidity : StaleValidity)).ToArray(), decisions.FirstOrDefault()?.Sequence ?? 0,
            PilotAccess.Dto(grants), grants.Length > 0, PilotAccess.SupportRecord(a) && PilotAccess.Human(Settings(h), a), Allowed(review), now));
    }

    // Human support/on-call readiness mark (append-only per item). READY requires an evidence reference (DB CHECK).
    private static async Task<IResult> Support(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!PilotAccess.SupportRecord(a)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied");
        if (!PilotAccess.Human(Settings(h), a)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "human_actor_required");
        if (h.Request.Query.Count > 0) return await TugReadinessHttp.Fail(h, CommandAction, 400, "invalid_request");
        var (c, key, failure) = await TugReadinessHttp.Read<PilotSupportCheckCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!PilotGatePolicy.SupportItems.Contains(c!.Item, StringComparer.Ordinal) || !PilotGatePolicy.SupportStates.Contains(c.State, StringComparer.Ordinal) ||
            !UatPolicy.ValidOptionalReference(c.EvidenceReference) || c.State == PilotGatePolicy.SupportReady && c.EvidenceReference == null ||
            !UatPolicy.ValidText(c.Note, 3, UatPolicy.TextMax) || c.ExpectedSequence < 0)
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed");
        var now = TugReadinessHttp.Now(h);
        return await PilotHttp.Commit(h, db, a, CommandAction, "PilotSupportCheckRecord", key, new { c }, async ct => {
            var previous = await db.PilotSupportChecks.AsNoTracking().Where(x => x.Item == c.Item).OrderByDescending(x => x.Sequence).Select(x => new { x.Id, x.Sequence }).FirstOrDefaultAsync(ct);
            if ((previous?.Sequence ?? 0) != c.ExpectedSequence) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "support_sequence_conflict"), Guid.Empty);
            var row = new PilotSupportCheck { TenantId = a.TenantId, Id = Guid.NewGuid(), Item = c.Item, Sequence = (previous?.Sequence ?? 0) + 1, PreviousCheckId = previous?.Id,
                State = c.State, EvidenceReference = c.EvidenceReference, Note = c.Note, ActorId = a.Id, RecordedAt = now };
            db.PilotSupportChecks.Add(row);
            return (null, row.Id);
        }, async (id, ct) => Dto(await db.PilotSupportChecks.AsNoTracking().SingleAsync(x => x.Id == id, ct)));
    }

    // The server re-evaluates the review; a changed hash is 409 review_stale. GO requires GO_ELIGIBLE, otherwise 409.
    private static async Task<IResult> Decide(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!PilotAccess.GateRead(a)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied");
        if (h.Request.Query.Count > 0) return await TugReadinessHttp.Fail(h, CommandAction, 400, "invalid_request");
        var (c, key, failure) = await TugReadinessHttp.Read<PilotGateDecisionCommand>(h, CommandAction);
        if (failure != null) return failure;
        var conditions = c!.Conditions ?? [];
        if (c.AuthorityId == Guid.Empty || c.ExpectedReviewHash is not { Length: 64 } hash || !hash.All(char.IsAsciiHexDigitUpper) || c.ExpectedSequence < 0 ||
            !PilotGatePolicy.Decisions.Contains(c.Decision, StringComparer.Ordinal) || !PilotGatePolicy.ValidRationale(c.Rationale) ||
            conditions.Length > PilotGatePolicy.MaxConditions || conditions.Any(x => !PilotGatePolicy.ValidCondition(x)) || conditions.Distinct(StringComparer.Ordinal).Count() != conditions.Length)
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed");
        var now = TugReadinessHttp.Now(h);
        var grant = PilotAccess.Resolve(Settings(h), a, now).SingleOrDefault(g => g.Id == c.AuthorityId);
        if (grant == null) return await TugReadinessHttp.Fail(h, CommandAction, 403, "pilot_gate_authority_required");
        // Gate reads run before the write transaction; the hash binds the decision to their exact result.
        var review = await PilotGateReviewer.Review(h, db, now, ct);
        return await PilotHttp.Commit(h, db, a, CommandAction, "PilotGateDecide", key, new { c }, async ct2 => {
            var previous = await db.PilotGateDecisions.AsNoTracking().OrderByDescending(d => d.Sequence).Select(d => new { d.Id, d.Sequence }).FirstOrDefaultAsync(ct2);
            if ((previous?.Sequence ?? 0) != c.ExpectedSequence) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "decision_sequence_conflict"), Guid.Empty);
            if (!string.Equals(review.Hash, c.ExpectedReviewHash, StringComparison.Ordinal)) return (await TugReadinessHttp.Fail(h, CommandAction, 409, "review_stale"), Guid.Empty);
            if (PilotGatePolicy.Refusal(c.Decision, review, conditions) is { } refusal)
                return (await TugReadinessHttp.Fail(h, CommandAction, PilotGatePolicy.RefusalStatus(refusal), refusal), Guid.Empty);
            var d = new PilotGateDecision { TenantId = a.TenantId, Id = Guid.NewGuid(), Sequence = (previous?.Sequence ?? 0) + 1, PreviousDecisionId = previous?.Id,
                Decision = c.Decision, Recommendation = review.Recommendation, ReviewHash = review.Hash, SnapshotJson = PilotGateReviewer.Snapshot(review),
                ConditionsJson = JsonSerializer.Serialize(conditions), Rationale = c.Rationale, ActorId = a.Id, AuthorityId = grant.Id, RoleId = grant.RoleId,
                Duty = grant.Duty, AuthorityHash = PilotAccess.Hash(grant, a), RecordedAt = now };
            db.PilotGateDecisions.Add(d);
            return (null, d.Id);
        }, async (id, ct2) => {
            var saved = await db.PilotGateDecisions.AsNoTracking().SingleAsync(x => x.Id == id, ct2);
            return Dto(saved, saved.ReviewHash == review.Hash ? CurrentValidity : StaleValidity);
        });
    }
}
