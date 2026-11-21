using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Server-provisioned appointment of one human as regulatory SME reviewer (18.01 RACI "Regulatory applicability": Regulatory
// SME A/R). The qualification reference and its verification state are recorded by the operator who provisions the
// appointment; they are never request-bound, inferred from an IdP role name, tenant-wide manage or a platform administrator.
// An UNVERIFIED qualification may track open items but can never confirm one (DEV-073).
public sealed record RegulatoryReviewerAuthority(Guid Id, Guid RoleId, string[] Scopes, string[] SourceAuthorities, string QualificationRef,
    string QualificationState, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
public sealed record RegulatoryReviewerAuthorityDto(Guid Id, string[] Scopes, string[] SourceAuthorities, bool QualificationVerified);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegulatoryReviewCreateCommand(Guid VesselId, string Scope);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegulatoryReviewVerdictCommand([property: JsonRequired] uint ExpectedVersion, Guid AuthorityId, string Verdict, string? SourceAuthority,
    string OwnerRole, string? SourceReference, string? SourceEdition, string? TrackedIssueId, string Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegulatoryReviewCloseCommand([property: JsonRequired] uint ExpectedVersion, string Outcome, string Reason);

public sealed record RegulatoryReviewItemDto(Guid Id, int Ordinal, string Kind, string SubjectKey, Guid? RuleId, long? RuleRevision, Guid? RequirementId,
    long? RequirementRevision, Guid? SourceId, long? SourceRevision, string? CandidateSourceCode, string? CandidateAuthority, bool CandidateUnverified,
    string? PredictedResult, string Verdict, string EffectiveVerdict, bool Stale, bool Open, string? SourceAuthority, string AuthorityCategory, string OwnerRole,
    string? SourceReference, string? SourceEdition, string? TrackedIssueId, Guid? ReviewerId, string? QualificationRef, int VerdictCount, DateTimeOffset? VerdictAt,
    uint Version, string[] Blockers, bool Replayed = false);
public sealed record RegulatoryReviewOwnerRow(string Authority, string Category, int Items, int Open, int Confirmed, string[] OwnerRoles);
public sealed record RegulatoryReviewSummaryDto(Guid Id, Guid VesselId, string Scope, string State, long? ProfileRevision, Guid? EvaluationId, string PinHash,
    int ItemCount, int OpenItems, int UntrackedOpenItems, int ConfirmedItems, int StaleItems, bool ProfileStale, Guid CreatedBy, DateTimeOffset CreatedAt,
    Guid? ClosedBy, DateTimeOffset? ClosedAt, int? OpenItemsAtClose, string Provenance, uint Version, string[] Blockers);
public sealed record RegulatoryReviewDetailDto(RegulatoryReviewSummaryDto Campaign, RegulatoryReviewItemDto[] Items, RegulatoryReviewOwnerRow[] OwnerMatrix,
    bool CanManage, bool CanReview, RegulatoryReviewerAuthorityDto[] ReviewerAuthorities, string[] Notices, bool Replayed = false,
    string PolicyVersion = RegulatoryReviewPolicy.Version, string CandidateSourcePlan = RegulatoryReviewPolicy.CandidateSourcePlan);
public sealed record RegulatoryReviewListDto(RegulatoryReviewSummaryDto[] Items, bool CanManage);

internal static class RegulatoryReviewAccess
{
    internal static bool CanRead(TrustedActor? a) => a != null && a.CanRead && a.Permissions.Contains("regulatory.review.read");
    internal static bool CanManage(TrustedActor? a) => CanRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("regulatory.review.manage");
    internal static bool CanReview(TrustedActor? a) => CanRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("regulatory.review.verdict");

    // Exactly one membership with ActorKind exact ordinal "Human"; AI, automation, service and unspecified principals fail closed.
    internal static bool Human(FederationSettings settings, TrustedActor a) {
        var members = settings.Memberships.Where(m => m.ActorId == a.Id && m.TenantId == a.TenantId).ToArray();
        return members.Length == 1 && string.Equals(members[0].ActorKind, "Human", StringComparison.Ordinal);
    }

    internal static RegulatoryReviewerAuthority[] Reviewers(FederationSettings settings, TrustedActor? a, string scope, DateTimeOffset now) {
        if (a == null || !CanReview(a) || a.RoleContext == null || !Human(settings, a)) return [];
        var grants = settings.Memberships.Single(m => m.ActorId == a.Id && m.TenantId == a.TenantId).RegulatoryReviewerAuthorities ?? [];
        if (grants.Length > 64) return [];
        var roles = a.RoleContext.Split(',');
        return grants.Where(g => g != null && g.Id != Guid.Empty && grants.Count(o => o != null && o.Id == g.Id) == 1 && g.RoleId != Guid.Empty &&
            roles.Contains(g.RoleId.ToString("D")) && g.Scopes is { Length: > 0 } scopes && scopes.Contains(scope, StringComparer.Ordinal) &&
            scopes.All(s => RegulatoryReviewPolicy.Scopes.Contains(s)) && g.SourceAuthorities is { Length: > 0 } labels &&
            labels.All(l => RegulatoryReviewPolicy.Authorities.Contains(l)) && g.QualificationRef is { Length: > 0 and <= 200 } q && q.Trim() == q && !q.Any(char.IsControl) &&
            g.QualificationState is { Length: > 0 and <= 20 } && g.ValidUntil > g.ValidFrom && g.ValidFrom <= now && now < g.ValidUntil).OrderBy(g => g.Id).ToArray();
    }
    internal static bool Verified(RegulatoryReviewerAuthority g) => string.Equals(g.QualificationState, RegulatoryReviewPolicy.QualificationVerified, StringComparison.Ordinal);
    internal static RegulatoryReviewerAuthorityDto Dto(RegulatoryReviewerAuthority g) => new(g.Id, g.Scopes, g.SourceAuthorities, Verified(g));
    internal static string Hash(RegulatoryReviewerAuthority g, TrustedActor a) =>
        RegisterStorage.Hash(JsonSerializer.Serialize(new { g, actorId = a.Id, tenantId = a.TenantId, actorKind = "Human" }));
}

// DEV-073 / DEV-093: /api/v1/regulatory-reviews. Records human review only; never activates a rule, a permit or a limit, never
// presents an unsourced tug limit as a legal default and never claims class/type approval (19.01 p124-129).
public static class RegulatoryReviewEndpoints
{
    private const string ReadAction = "RegulatoryReviewRead", CommandAction = "RegulatoryReviewCommand";
    private const string CreateName = "RegulatoryReviewCreate", VerdictName = "RegulatoryReviewVerdict", CloseName = "RegulatoryReviewClose";

    public static void MapRegulatoryReviews(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/regulatory-reviews").RequireAuthorization("DirectoryRead");
        g.MapGet("", ListCampaigns).WithName("RegulatoryReviewList").Produces<RegulatoryReviewListDto>();
        g.MapGet("/{id:guid}", Detail).WithName("RegulatoryReviewRead").Produces<RegulatoryReviewDetailDto>();
        g.MapPost("", Create).WithName(CreateName).Accepts<RegulatoryReviewCreateCommand>("application/json").Produces<RegulatoryReviewDetailDto>();
        g.MapPost("/{id:guid}/items/{item:guid}/verdicts", Verdict).WithName(VerdictName).Accepts<RegulatoryReviewVerdictCommand>("application/json").Produces<RegulatoryReviewItemDto>();
        g.MapPost("/{id:guid}/close", Close).WithName(CloseName).Accepts<RegulatoryReviewCloseCommand>("application/json").Produces<RegulatoryReviewDetailDto>();
    }

    private sealed record StaleFacts(bool ProfileStale, HashSet<Guid> Items);

    private static async Task<StaleFacts> Stale(PoseidonDbContext db, RegulatoryReviewCampaign c, RegulatoryReviewItem[] items, CancellationToken ct) {
        var profile = await db.RegulatoryDrafts.AsNoTracking().Where(x => x.VesselId == c.VesselId).Select(x => (long?)x.Revision).FirstOrDefaultAsync(ct);
        var profileStale = profile != c.ProfileRevision;
        var ruleIds = items.Where(i => i.RuleId != null).Select(i => i.RuleId!.Value).Distinct().ToArray();
        var registerIds = items.SelectMany(i => new[] { i.SourceId, i.RequirementId }).Where(x => x != null).Select(x => x!.Value).Distinct().ToArray();
        var rules = await db.RuleDrafts.AsNoTracking().Where(r => ruleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Revision, ct);
        var register = await db.RegisterDrafts.AsNoTracking().Where(r => registerIds.Contains(r.Id) && (r.Kind == "Source" || r.Kind == "Requirement"))
            .Select(r => new { r.Kind, r.Id, r.Revision }).ToArrayAsync(ct);
        long? Head(string kind, Guid id) => register.Where(r => r.Kind == kind && r.Id == id).Select(r => (long?)r.Revision).FirstOrDefault();
        var stale = items.Where(i =>
            i.RuleId is { } rule && (!rules.TryGetValue(rule, out var revision) || revision != i.RuleRevision) ||
            i.SourceId is { } source && Head("Source", source) != i.SourceRevision ||
            i.RequirementId is { } requirement && Head("Requirement", requirement) != i.RequirementRevision ||
            // An applicability prediction depends on the pinned vessel profile revision.
            i.Kind == RegulatoryReviewPolicy.RuleItem && profileStale).Select(i => i.Id).ToHashSet();
        return new(profileStale, stale);
    }

    private static RegulatoryReviewItemDto ItemDto(RegulatoryReviewItem i, bool stale, bool replayed = false) {
        var authority = i.SourceAuthority ?? i.CandidateAuthority;
        var pinned = i.SourceId != null && i.SourceRevision != null;
        return new(i.Id, i.Ordinal, i.Kind, i.SubjectKey, i.RuleId, i.RuleRevision, i.RequirementId, i.RequirementRevision, i.SourceId, i.SourceRevision,
            i.CandidateSourceCode, i.CandidateAuthority, i.CandidateUnverified, i.PredictedResult, i.Verdict, RegulatoryReviewPolicy.Effective(i.Verdict, stale), stale,
            RegulatoryReviewPolicy.IsOpen(i.Verdict, stale), i.SourceAuthority, RegulatoryReviewPolicy.Category(authority), i.OwnerRole, i.SourceReference, i.SourceEdition,
            i.TrackedIssueId, i.ReviewerId, i.QualificationRef, i.VerdictCount, i.VerdictAt, i.Version,
            RegulatoryReviewPolicy.Blockers(i.Kind, i.Verdict, stale, pinned, i.SourceReference, i.TrackedIssueId, i.CandidateUnverified), replayed);
    }

    private static RegulatoryReviewSummaryDto Summary(RegulatoryReviewCampaign c, RegulatoryReviewItem[] items, StaleFacts s) {
        var open = items.Where(i => RegulatoryReviewPolicy.IsOpen(i.Verdict, s.Items.Contains(i.Id))).ToArray();
        var blockers = new List<string>();
        if (c.EvaluationId == null) blockers.Add("APPLICABILITY_EVALUATION_UNAVAILABLE");
        if (c.ProfileRevision == null) blockers.Add("VESSEL_PROFILE_UNPINNED");
        if (s.ProfileStale) blockers.Add("VESSEL_PROFILE_CHANGED");
        if (open.Length > 0) blockers.Add("OPEN_ITEMS_REVIEW_REQUIRED");
        if (open.Any(i => i.TrackedIssueId == null)) blockers.Add("OPEN_ITEMS_UNTRACKED");
        return new(c.Id, c.VesselId, c.Scope, c.State, c.ProfileRevision, c.EvaluationId, c.PinHash, items.Length, open.Length, open.Count(i => i.TrackedIssueId == null),
            items.Count(i => !RegulatoryReviewPolicy.IsOpen(i.Verdict, s.Items.Contains(i.Id))), s.Items.Count, s.ProfileStale, c.CreatedBy, c.CreatedAt,
            c.ClosedBy, c.ClosedAt, c.OpenItemsAtClose, c.Provenance, c.Version, [.. blockers]);
    }

    private static async Task<RegulatoryReviewDetailDto> Build(HttpContext h, PoseidonDbContext db, TrustedActor a, RegulatoryReviewCampaign c, bool replayed, CancellationToken ct) {
        var items = await db.RegulatoryReviewItems.AsNoTracking().Where(i => i.CampaignId == c.Id).OrderBy(i => i.Ordinal).Take(256).ToArrayAsync(ct);
        var s = await Stale(db, c, items, ct);
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var reviewers = RegulatoryReviewAccess.Reviewers(settings, a, c.Scope, TugReadinessHttp.Now(h));
        var matrix = items.GroupBy(i => i.SourceAuthority ?? i.CandidateAuthority ?? RegulatoryReviewPolicy.Unassigned).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new RegulatoryReviewOwnerRow(x.Key, RegulatoryReviewPolicy.Category(x.Key), x.Count(), x.Count(i => RegulatoryReviewPolicy.IsOpen(i.Verdict, s.Items.Contains(i.Id))),
                x.Count(i => !RegulatoryReviewPolicy.IsOpen(i.Verdict, s.Items.Contains(i.Id))), x.Select(i => i.OwnerRole).Distinct().Order(StringComparer.Ordinal).ToArray())).ToArray();
        string[] notices = c.Scope == RegulatoryReviewPolicy.Tug
            ? ["NO_UNSOURCED_LIMIT_IS_A_LEGAL_DEFAULT", "GUIDANCE_IS_NOT_STATUTORY_LAW", "NO_CLASS_OR_TYPE_APPROVAL_CLAIM", "REVIEW_NEVER_ACTIVATES_RULES"]
            : ["GUIDANCE_IS_NOT_STATUTORY_LAW", "NO_CLASS_OR_TYPE_APPROVAL_CLAIM", "REVIEW_NEVER_ACTIVATES_RULES"];
        var terminal = RegulatoryReviewPolicy.Terminal(c.State);
        return new(Summary(c, items, s), items.Select(i => ItemDto(i, s.Items.Contains(i.Id))).ToArray(), matrix,
            !terminal && RegulatoryReviewAccess.CanManage(a) && RegulatoryReviewAccess.Human(settings, a) && a.CanEditVessel(c.VesselId),
            !terminal && reviewers.Length > 0 && c.CreatedBy != a.Id, reviewers.Select(RegulatoryReviewAccess.Dto).ToArray(), notices, replayed);
    }

    private static Task<Vessel?> ScopedVessel(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) =>
        a.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(v => v.Id == vessel, ct);

    private static async Task<RegulatoryReviewCampaign?> Campaign(PoseidonDbContext db, TrustedActor a, Guid id, bool track, CancellationToken ct) {
        var query = track ? db.RegulatoryReviewCampaigns : db.RegulatoryReviewCampaigns.AsNoTracking();
        var c = await query.SingleOrDefaultAsync(x => x.Id == id, ct);
        return c != null && await ScopedVessel(db, a, c.VesselId, ct) != null ? c : null;
    }

    private static async Task<IResult> ListCampaigns(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!RegulatoryReviewAccess.CanRead(a)) return await TugReadinessHttp.Fail(h, ReadAction, 403, "permission_denied");
        string? scope = h.Request.Query.TryGetValue("scope", out var scopeValue) ? scopeValue[0] : null;
        if (h.Request.Query.Any(q => q.Key is not ("vessel" or "scope") || q.Value.Count != 1) ||
            !Guid.TryParseExact(h.Request.Query["vessel"], "D", out var vessel) || vessel == Guid.Empty ||
            scope != null && !RegulatoryReviewPolicy.Scopes.Contains(scope, StringComparer.Ordinal))
            return await TugReadinessHttp.Fail(h, ReadAction, 400, "invalid_query");
        if (await ScopedVessel(db, a, vessel, ct) == null) return await TugReadinessHttp.Fail(h, ReadAction, 404, "resource_not_found");
        var campaigns = await db.RegulatoryReviewCampaigns.AsNoTracking().Where(c => c.VesselId == vessel && (scope == null || c.Scope == scope))
            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).Take(20).ToArrayAsync(ct);
        var ids = campaigns.Select(c => c.Id).ToArray();
        var items = await db.RegulatoryReviewItems.AsNoTracking().Where(i => ids.Contains(i.CampaignId)).ToArrayAsync(ct);
        var result = new List<RegulatoryReviewSummaryDto>();
        foreach (var c in campaigns) {
            var own = items.Where(i => i.CampaignId == c.Id).ToArray();
            result.Add(Summary(c, own, await Stale(db, c, own, ct)));
        }
        await TugReadinessHttp.Observe(h, ReadAction, "Success", vessel);
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        return Results.Ok(new RegulatoryReviewListDto([.. result], RegulatoryReviewAccess.CanManage(a) && RegulatoryReviewAccess.Human(settings, a!) && a!.CanEditVessel(vessel)));
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!RegulatoryReviewAccess.CanRead(a)) return await TugReadinessHttp.Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Count != 0) return await TugReadinessHttp.Fail(h, ReadAction, 400, "invalid_query");
        var c = await Campaign(db, a, id, false, ct);
        if (c == null) return await TugReadinessHttp.Fail(h, ReadAction, 404, "resource_not_found");
        var dto = await Build(h, db, a, c, false, ct);
        await TugReadinessHttp.Observe(h, ReadAction, "Success", id);
        return Results.Ok(dto);
    }

    private sealed record Candidate(string Kind, string Subject, Guid? RuleId = null, long? RuleRevision = null, Guid? RequirementId = null, long? RequirementRevision = null,
        Guid? SourceId = null, long? SourceRevision = null, string? Code = null, string? Authority = null, bool Unverified = false, string? Predicted = null);

    // Items: the pilot profile's latest visible applicability predictions (each REVIEW_REQUIRED), the DEV-008 candidate sources
    // (UNVERIFIED kept), for TUG every towage limit parameter (no limit is a legal default) and the unowned ownership slots.
    private static async Task<(Candidate[] Items, Guid? Evaluation)> Candidates(PoseidonDbContext db, TrustedActor a, Guid vessel, string scope, CancellationToken ct) {
        var list = new List<Candidate>(); Guid? evaluation = null;
        var row = await OperationEquipmentCommands.Evaluations(db, a).Where(e => e.VesselId == vessel)
            .OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).FirstOrDefaultAsync(ct);
        if (row != null) {
            try {
                var result = JsonSerializer.Deserialize<EvaluationDto>(row.ResultJson, RegulatoryQualificationEndpoints.Json);
                if (result?.VesselId == vessel && result.Results is { Length: > 0 } predictions) {
                    evaluation = row.Id;
                    foreach (var p in predictions.Where(p => p.RuleId != Guid.Empty && p.Revision > 0).DistinctBy(p => p.RuleId).Take(RegulatoryReviewPolicy.MaxRuleItems))
                        list.Add(new(RegulatoryReviewPolicy.RuleItem, p.RuleId.ToString("D"), p.RuleId, p.Revision, p.RequirementId, p.RequirementRevision,
                            p.SourceId, p.SourceRevision, Predicted: p.Prediction.Authoritative && p.Prediction.Result is { Length: > 0 and <= 40 } r ? r : RegulatoryReviewPolicy.ReviewRequired));
                }
            } catch (Exception e) when (e is JsonException or NotSupportedException) { evaluation = null; list.Clear(); }
        }
        foreach (var c in RegulatoryReviewPolicy.Candidates.Where(c => c.ReviewScopes.Contains(scope)))
            list.Add(new(RegulatoryReviewPolicy.SourceCandidate, c.Code, Code: c.Code, Authority: c.Authority, Unverified: true));
        if (scope == RegulatoryReviewPolicy.Tug)
            foreach (var p in RegulatoryReviewPolicy.TugLimitParameters) list.Add(new(RegulatoryReviewPolicy.TugLimit, p));
        foreach (var slot in RegulatoryReviewPolicy.OwnershipSlots(scope)) list.Add(new(RegulatoryReviewPolicy.OwnershipSlot, slot, Authority: slot));
        return ([.. list], evaluation);
    }

    private static async Task<IResult> Create(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        if (!RegulatoryReviewAccess.CanManage(a) || !RegulatoryReviewAccess.Human(settings, a)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied");
        if (h.Request.Query.Count != 0) return await TugReadinessHttp.Fail(h, CommandAction, 400, "invalid_request");
        var (command, key, failure) = await TugReadinessHttp.Read<RegulatoryReviewCreateCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (command!.VesselId == Guid.Empty || !RegulatoryReviewPolicy.Scopes.Contains(command.Scope, StringComparer.Ordinal))
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed");
        var vessel = await ScopedVessel(db, a, command.VesselId, ct);
        if (vessel == null) return await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found");
        if (!a.CanEditVessel(vessel.Id)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied", vessel.Id);
        if (vessel.Kind == VesselKind.Tug != (command.Scope == RegulatoryReviewPolicy.Tug)) return await TugReadinessHttp.Fail(h, CommandAction, 409, "review_scope_mismatch", vessel.Id);
        var fingerprint = TugReadinessHttp.Fingerprint(new { action = CreateName, command });
        await TugReadinessHttp.Observe(h, CommandAction, "Attempted", vessel.Id);
        var (candidates, evaluation) = await Candidates(db, a, vessel.Id, command.Scope, ct);
        var profile = await db.RegulatoryDrafts.AsNoTracking().Where(x => x.VesselId == vessel.Id).Select(x => (long?)x.Revision).FirstOrDefaultAsync(ct);
        var now = TugReadinessHttp.Now(h);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var receipt = await db.RegulatoryReviewReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != CreateName || receipt.VesselId != vessel.Id || receipt.Fingerprint != fingerprint)
                    return await TugReadinessHttp.Fail(h, CommandAction, 409, "idempotency_key_reused", vessel.Id);
                var saved = await db.RegulatoryReviewCampaigns.AsNoTracking().SingleAsync(c => c.Id == receipt.CampaignId, ct);
                await TugReadinessHttp.Observe(h, CommandAction, "Success", vessel.Id);
                return Results.Ok(await Build(h, db, a, saved, true, ct));
            }
            if (await db.RegulatoryReviewCampaigns.AnyAsync(c => c.VesselId == vessel.Id && c.Scope == command.Scope &&
                    (c.State == RegulatoryReviewPolicy.Open || c.State == RegulatoryReviewPolicy.InReview), ct))
                return await TugReadinessHttp.Fail(h, CommandAction, 409, "review_campaign_open", vessel.Id);
            var pinHash = RegulatoryReviewPolicy.Hash(JsonSerializer.Serialize(new { vessel = vessel.Id, command.Scope, profile, evaluation,
                items = candidates.Select(c => new { c.Kind, c.Subject, c.RuleRevision, c.SourceId, c.SourceRevision, c.RequirementId, c.RequirementRevision }), RegulatoryReviewPolicy.Version }));
            var campaign = new RegulatoryReviewCampaign { TenantId = a.TenantId, Id = Guid.NewGuid(), VesselId = vessel.Id, Scope = command.Scope, ProfileRevision = profile,
                EvaluationId = evaluation, PinHash = pinHash, ItemCount = candidates.Length, CreatedBy = a.Id, CreatedAt = now, PolicyVersion = RegulatoryReviewPolicy.Version };
            db.RegulatoryReviewCampaigns.Add(campaign);
            var ordinal = 0;
            foreach (var c in candidates)
                db.RegulatoryReviewItems.Add(new() { TenantId = a.TenantId, Id = Guid.NewGuid(), CampaignId = campaign.Id, VesselId = vessel.Id, Ordinal = ++ordinal, Kind = c.Kind,
                    SubjectKey = c.Subject, RuleId = c.RuleId, RuleRevision = c.RuleRevision, RequirementId = c.RequirementId, RequirementRevision = c.RequirementRevision,
                    SourceId = c.SourceId, SourceRevision = c.SourceRevision, CandidateSourceCode = c.Code, CandidateAuthority = c.Authority, CandidateUnverified = c.Unverified,
                    PredictedResult = c.Predicted, CreatedBy = a.Id });
            db.RegulatoryReviewReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel.Id, ActorId = a.Id, Key = key, Action = CreateName, Fingerprint = fingerprint,
                CampaignId = campaign.Id, RecordId = campaign.Id, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, CommandAction, "Success", vessel.Id);
            return Results.Ok(await Build(h, db, a, campaign, false, ct));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, CommandAction, e, vessel.Id);
        }
    }

    private static int Status(string code) => code switch {
        "validation_failed" or "unsupported_approval_claim" => 400,
        "separation_of_duties" or "reviewer_authority_required" => 403,
        _ => 409
    };

    private static async Task<IResult> Verdict(Guid id, Guid item, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!RegulatoryReviewAccess.CanReview(a)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied");
        if (h.Request.Query.Count != 0) return await TugReadinessHttp.Fail(h, CommandAction, 400, "invalid_request");
        var (command, key, failure) = await TugReadinessHttp.Read<RegulatoryReviewVerdictCommand>(h, CommandAction);
        if (failure != null) return failure;
        var input = new RegulatoryReviewPolicy.VerdictInput(command!.Verdict ?? "", command.SourceAuthority, command.OwnerRole ?? "", command.SourceReference,
            command.SourceEdition, command.TrackedIssueId, command.Note ?? "");
        if (command.AuthorityId == Guid.Empty || !RegulatoryReviewPolicy.ValidVerdict(input)) return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed");
        var existing = await Campaign(db, a, id, false, ct);
        if (existing == null) return await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found");
        var now = TugReadinessHttp.Now(h);
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var grant = RegulatoryReviewAccess.Reviewers(settings, a, existing.Scope, now).SingleOrDefault(g => g.Id == command.AuthorityId);
        if (grant == null) return await TugReadinessHttp.Fail(h, CommandAction, 403, "reviewer_authority_required", existing.VesselId);
        var fingerprint = TugReadinessHttp.Fingerprint(new { action = VerdictName, id, item, command });
        await TugReadinessHttp.Observe(h, CommandAction, "Attempted", existing.VesselId);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var receipt = await db.RegulatoryReviewReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != VerdictName || receipt.CampaignId != id || receipt.Fingerprint != fingerprint)
                    return await TugReadinessHttp.Fail(h, CommandAction, 409, "idempotency_key_reused", existing.VesselId);
                var current = await db.RegulatoryReviewItems.AsNoTracking().SingleAsync(i => i.Id == item && i.CampaignId == id, ct);
                var currentCampaign = await db.RegulatoryReviewCampaigns.AsNoTracking().SingleAsync(c => c.Id == id, ct);
                var replayStale = await Stale(db, currentCampaign, [current], ct);
                await TugReadinessHttp.Observe(h, CommandAction, "Success", existing.VesselId);
                return Results.Ok(ItemDto(current, replayStale.Items.Contains(current.Id), true));
            }
            var campaign = await db.RegulatoryReviewCampaigns.SingleAsync(c => c.Id == id, ct);
            var row = await db.RegulatoryReviewItems.SingleOrDefaultAsync(i => i.Id == item && i.CampaignId == id, ct);
            if (row == null) return await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found", existing.VesselId);
            if (row.Version != command.ExpectedVersion) return await TugReadinessHttp.Fail(h, CommandAction, 409, "version_conflict", existing.VesselId);
            var stale = (await Stale(db, campaign, [row], ct)).Items.Contains(row.Id);
            var refusal = RegulatoryReviewPolicy.Refusal(campaign.State, new(row.Kind, row.SourceId != null && row.SourceRevision != null, stale), input,
                new(RegulatoryReviewAccess.Verified(grant), grant.SourceAuthorities, campaign.CreatedBy == a.Id));
            if (refusal != null) return await TugReadinessHttp.Fail(h, CommandAction, Status(refusal), refusal, existing.VesselId);
            var qualification = RegulatoryReviewAccess.Verified(grant) ? RegulatoryReviewPolicy.QualificationVerified : "UNVERIFIED";
            var pinHash = RegulatoryReviewPolicy.Hash(JsonSerializer.Serialize(new { campaign.PinHash, row.Id, row.Kind, row.SubjectKey, row.RuleRevision, row.SourceRevision, row.RequirementRevision }));
            row.Verdict = input.Verdict; row.SourceAuthority = input.SourceAuthority; row.OwnerRole = input.OwnerRole; row.SourceReference = input.SourceReference;
            row.SourceEdition = input.SourceEdition; row.TrackedIssueId = input.TrackedIssueId; row.ReviewerId = a.Id; row.ReviewerRoleId = grant.RoleId;
            row.ReviewerAuthorityId = grant.Id; row.QualificationRef = grant.QualificationRef; row.VerdictCount++; row.VerdictAt = now;
            var verdict = new RegulatoryReviewVerdict { TenantId = a.TenantId, Id = Guid.NewGuid(), CampaignId = id, ItemId = row.Id, VesselId = row.VesselId, Sequence = row.VerdictCount,
                Verdict = input.Verdict, SourceAuthority = input.SourceAuthority, OwnerRole = input.OwnerRole, SourceReference = input.SourceReference, SourceEdition = input.SourceEdition,
                TrackedIssueId = input.TrackedIssueId, Note = input.Note, PinHash = pinHash, ReviewerId = a.Id, RoleId = grant.RoleId, AuthorityId = grant.Id,
                QualificationRef = grant.QualificationRef, QualificationState = qualification, AuthorityHash = RegulatoryReviewAccess.Hash(grant, a), RecordedAt = now };
            db.RegulatoryReviewVerdicts.Add(verdict);
            if (campaign.State == RegulatoryReviewPolicy.Open) campaign.State = RegulatoryReviewPolicy.InReview;
            db.RegulatoryReviewReceipts.Add(new() { TenantId = a.TenantId, VesselId = row.VesselId, ActorId = a.Id, Key = key, Action = VerdictName, Fingerprint = fingerprint,
                CampaignId = id, RecordId = verdict.Id, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, CommandAction, "Success", existing.VesselId);
            return Results.Ok(ItemDto(row, stale));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, CommandAction, e, existing.VesselId);
        }
    }

    private static async Task<IResult> Close(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        if (!RegulatoryReviewAccess.CanManage(a) || !RegulatoryReviewAccess.Human(settings, a)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied");
        if (h.Request.Query.Count != 0) return await TugReadinessHttp.Fail(h, CommandAction, 400, "invalid_request");
        var (command, key, failure) = await TugReadinessHttp.Read<RegulatoryReviewCloseCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!RegulatoryReviewPolicy.CloseOutcomes.Contains(command!.Outcome ?? "", StringComparer.Ordinal) ||
            !RegulatoryReviewPolicy.Text(command.Reason, RegulatoryReviewPolicy.NoteMin, RegulatoryReviewPolicy.CloseReasonMax))
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed");
        var existing = await Campaign(db, a, id, false, ct);
        if (existing == null) return await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found");
        if (!a.CanEditVessel(existing.VesselId)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied", existing.VesselId);
        var fingerprint = TugReadinessHttp.Fingerprint(new { action = CloseName, id, command });
        await TugReadinessHttp.Observe(h, CommandAction, "Attempted", existing.VesselId);
        var now = TugReadinessHttp.Now(h);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var receipt = await db.RegulatoryReviewReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != CloseName || receipt.CampaignId != id || receipt.Fingerprint != fingerprint)
                    return await TugReadinessHttp.Fail(h, CommandAction, 409, "idempotency_key_reused", existing.VesselId);
                var saved = await db.RegulatoryReviewCampaigns.AsNoTracking().SingleAsync(c => c.Id == id, ct);
                await TugReadinessHttp.Observe(h, CommandAction, "Success", existing.VesselId);
                return Results.Ok(await Build(h, db, a, saved, true, ct));
            }
            var campaign = await db.RegulatoryReviewCampaigns.SingleAsync(c => c.Id == id, ct);
            if (campaign.Version != command.ExpectedVersion) return await TugReadinessHttp.Fail(h, CommandAction, 409, "version_conflict", existing.VesselId);
            var items = await db.RegulatoryReviewItems.AsNoTracking().Where(i => i.CampaignId == id).ToArrayAsync(ct);
            var stale = await Stale(db, campaign, items, ct);
            var open = items.Where(i => RegulatoryReviewPolicy.IsOpen(i.Verdict, stale.Items.Contains(i.Id))).ToArray();
            var refusal = RegulatoryReviewPolicy.CloseRefusal(campaign.State, command.Outcome!, open.Length, open.Count(i => i.TrackedIssueId == null), command.Reason);
            if (refusal != null) return await TugReadinessHttp.Fail(h, CommandAction, refusal is "validation_failed" or "unsupported_approval_claim" ? 400 : 409, refusal, existing.VesselId);
            campaign.State = command.Outcome!; campaign.ClosedBy = a.Id; campaign.ClosedAt = now; campaign.CloseReason = command.Reason; campaign.OpenItemsAtClose = open.Length;
            db.RegulatoryReviewReceipts.Add(new() { TenantId = a.TenantId, VesselId = campaign.VesselId, ActorId = a.Id, Key = key, Action = CloseName, Fingerprint = fingerprint,
                CampaignId = id, RecordId = id, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, CommandAction, "Success", existing.VesselId);
            return Results.Ok(await Build(h, db, a, campaign, false, ct));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, CommandAction, e, existing.VesselId);
        }
    }
}
