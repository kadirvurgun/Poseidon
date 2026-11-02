using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Operator-provisioned appointment, never a request/IdP/signatory-name authority.
// Every appointment is exact object + operation + role + time scope; no wildcard admin.
public sealed record HumanAuthority(Guid Id, Guid VesselId, Guid EvidenceId, Guid ControlId, Guid OperationId,
    Guid RoleId, string Capability, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, HumanRecordingPolicy? Recording = null);
// Trusted server configuration, empty by default. These are scoped assignments,
// not a production decision-type catalog. Unresolved types never gain a default.
public sealed record HumanRecordingPolicy(string DecisionType, string SubjectType, Guid SubjectId, string AuthorityRole,
    string Semantics, string SourceLocator, string? RequiredGate = null, string? GateSourceLocator = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecisionRecording(string DecisionType, string SubjectType, Guid SubjectId, string HumanDecision,
    string TriggerSource, string DecisionContext);
public sealed record RecordedDecision(DecisionRecording Content, HumanRecordingPolicy Policy, string Qualification, long? ProfileRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HumanReviewCommand([property: JsonRequired] uint ExpectedVersion, Guid AuthorityId, Guid OperationId,
    Guid EvidenceId, long EvidenceRevision, uint EvidenceVersion, Guid ControlId, long ControlRevision, uint ControlVersion,
    Guid? EvaluationId, Guid? VerificationId, long? VerificationRevision, string State, string ReasonCode, string Rationale, DecisionRecording? Recording = null, long? ExpectedProfileRevision = null);
public sealed record HumanReviewDto(Guid Id, Guid VesselId, long Revision, uint Version, string Kind, string State,
    string CurrentState, Guid EvidenceId, long EvidenceRevision, Guid ControlId, long ControlRevision, Guid? EvaluationId,
    Guid? VerificationId, long? VerificationRevision, Guid ActorId, Guid RoleId, Guid AuthorityId, Guid OperationId,
    string ReasonCode, string Rationale, string AuthorityHash, string ContextHash, DateTimeOffset SavedAt, DateTimeOffset? ValidUntil, DateTimeOffset AsOf,
    RecordedDecision? Recording = null);
public sealed record TraceSegment(string Kind, Guid? Id, long? Revision, string Availability, string State, string Why, string? Hash, DateTimeOffset? SavedAt);
// Reader appointments are independent of immutable signatory provenance. No mutation capability is implied.
public sealed record HumanTraceReader(Guid Id, Guid VesselId, Guid EvidenceId, Guid ControlId, Guid RoleId,
    string[] Kinds, string[] DecisionTypes, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, bool IncludeUntypedDecisions = false);
public sealed record AuditTraceEvent(Guid Id, long Revision, string Kind, string State, string CurrentState,
    DateTimeOffset SavedAt, DateTimeOffset? ValidUntil, Guid? EvaluationId, Guid? VerificationId, long? VerificationRevision,
    Guid AuthorityId, Guid RoleId, string AuthorityHash, string ContextHash);

public static class HumanReviewEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 10 };
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Read(TrustedActor a) => ControlEvidenceEndpoints.Read(a) && a.Permissions.Contains("human.review.read");
    private static HumanTraceReader? Reader(HttpContext h, TrustedActor a, Guid vessel, Guid evidence, Guid control)
    {
        if (!a.CanRead || !a.Permissions.Contains("human.trace.read") || !a.CanReadAudit || a.RoleContext == null) return null;
        var members = h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m => m.ActorId == a.Id && m.TenantId == a.TenantId).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return null;
        var now = DateTimeOffset.UtcNow;
        var grants = members[0].HumanTraceReaders.Where(g => g.Id != Guid.Empty && members[0].HumanTraceReaders.Count(x => x.Id == g.Id) == 1 &&
            g.VesselId == vessel && g.EvidenceId == evidence && g.ControlId == control && g.RoleId != Guid.Empty && a.RoleContext.Split(',').Contains(g.RoleId.ToString("D")) &&
            g.ValidFrom <= now && now < g.ValidUntil && g.Kinds.Length > 0 && g.Kinds.All(k => k is "Verification" or "Decision") && g.DecisionTypes.Length <= 32).ToArray();
        return grants.Length == 1 ? grants[0] : null;
    }
    private const string RecordingSource = "15.01/paragraph:99;18.01/Backlog/row:32";
    private static bool RecordingScope(HumanAuthority g, DecisionRecording r) =>
        g.Capability == "human.decide" && g.Recording is { } p && p.DecisionType == r.DecisionType &&
        p.SubjectType == r.SubjectType && p.SubjectId == r.SubjectId && p.AuthorityRole is "Master" or "Tug Master" or "DPA" &&
        p.SourceLocator == RecordingSource && p.Semantics == "HumanRecording" &&
        (p.SubjectType == "Control" && p.SubjectId == g.ControlId || p.SubjectType == "Evidence" && p.SubjectId == g.EvidenceId);
    private static string AuthorityHash(HumanAuthority grant, TrustedActor a) =>
        RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actorId = a.Id, tenantId = a.TenantId, actorKind = "Human" }));
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "HumanReview", code);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = h.TraceIdentifier });
    }
    private static HumanAuthority[] Authorities(HttpContext h, TrustedActor a, Guid vessel, Guid evidence, Guid control, DateTimeOffset now)
    {
        if (!Read(a) || !a.CanEditVessel(vessel)) return [];
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        var matches = settings.Memberships.Where(m => m.ActorId == a.Id && m.TenantId == a.TenantId).ToArray();
        if (matches.Length != 1 || matches[0].ActorKind != "Human" || a.RoleContext == null) return [];
        return (matches[0].HumanAuthorities ?? []).Where(g => g.Id != Guid.Empty && g.OperationId != Guid.Empty && g.RoleId != Guid.Empty &&
            matches[0].HumanAuthorities.Count(other => other.Id == g.Id) == 1 &&
            g.VesselId == vessel && g.EvidenceId == evidence && g.ControlId == control && g.ValidFrom <= now && now < g.ValidUntil &&
            g.ValidUntil > g.ValidFrom && a.RoleContext.Split(',').Contains(g.RoleId.ToString("D")) &&
            g.Capability is "human.review.submit" or "human.verify" or "human.decide" && a.Permissions.Contains(g.Capability))
            .GroupBy(g => g.Id).Where(g => g.Count() == 1).Select(g => g.Single()).ToArray();
    }
    private static IQueryable<ControlEvidenceRevision> Visible(PoseidonDbContext db, Guid vessel) => db.ControlEvidenceRevisions.AsNoTracking().Where(r => r.VesselId == vessel &&
        (r.FileId == null || db.StoredFiles.Any(f => f.Id == r.FileId && f.VesselId == vessel && f.Revision == r.FileRevision && f.Checksum == r.Checksum &&
            db.FileStages.Any(s => s.Id == f.Id && s.VesselId == vessel && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport"))));
    public static void MapHumanReviews(this IEndpointRouteBuilder app)
    {
        foreach (var (route, kind) in new[] { ("verifications", "Verification"), ("decisions", "Decision") }) {
            var g = app.MapGroup("/api/v1/" + route).RequireAuthorization("DirectoryRead");
            g.MapPost("", (HttpContext h) => Save(h, kind, null)).Accepts<HumanReviewCommand>("application/json").Produces<HumanReviewDto>();
            g.MapPost("/{id:guid}/events", (Guid id, HttpContext h) => Save(h, kind, id)).Accepts<HumanReviewCommand>("application/json").Produces<HumanReviewDto>();
        }
        foreach (var route in new[] { "regulatory-trace", "verification-gates", "audit-ready" })
            app.MapGet("/api/v1/" + route, Trace).RequireAuthorization("DirectoryRead");
    }
    private static bool GuidQuery(HttpContext h, string key, out Guid id) => Guid.TryParseExact(h.Request.Query[key], "D", out id) && id != Guid.Empty;
    private static bool RevisionQuery(HttpContext h, string key, out long revision) => long.TryParse(h.Request.Query[key], NumberStyles.None, CultureInfo.InvariantCulture, out revision) && revision is > 0 and <= 9007199254740991;
    private static bool Superseded(ControlEvidenceHead evidence, ControlEvidenceHead control, long er, long cr) => evidence.Revision != er || control.Revision != cr;
    private static async Task<HumanReviewDto> Dto(PoseidonDbContext db, HumanReviewEvent e, uint version, DateTimeOffset asOf, CancellationToken ct, bool currentView = true) {
        var superseded = await db.ControlEvidenceRevisions.AnyAsync(x => x.VesselId == e.VesselId && x.SavedAt <= asOf &&
            (x.Kind == "Evidence" && x.Id == e.EvidenceId && x.Revision > e.EvidenceRevision || x.Kind == "Control" && x.Id == e.ControlId && x.Revision > e.ControlRevision), ct)
            || await LineageChanged(db, e.VesselId, e.ControlId, e.ControlRevision, e.EvaluationId, asOf, ct, currentView);
        if (e.VerificationId != null) superseded |= await db.HumanReviewEvents.AnyAsync(x => x.VesselId == e.VesselId && x.Id == e.VerificationId && x.Revision > e.VerificationRevision && x.SavedAt <= asOf, ct);
        if (e.DecisionJson != null) {
            var capture = JsonSerializer.Deserialize<RecordedDecision>(e.DecisionJson, Json)!;
            superseded |= await db.RegulatoryDraftRevisions.AnyAsync(x => x.VesselId == e.VesselId && x.SavedAt <= asOf && (capture.ProfileRevision == null || x.Revision > capture.ProfileRevision), ct);
        }
        return new(e.Id, e.VesselId, e.Revision, version, e.Kind, e.State, HumanReviewPolicy.Current(e.State, e.ValidUntil, superseded, asOf), e.EvidenceId, e.EvidenceRevision,
            e.ControlId, e.ControlRevision, e.EvaluationId, e.VerificationId, e.VerificationRevision, e.ActorId, e.RoleId, e.AuthorityId, e.OperationId,
            e.ReasonCode, e.Rationale, e.AuthorityHash, e.ContextHash, e.SavedAt, e.ValidUntil, asOf,
            e.DecisionJson == null ? null : JsonSerializer.Deserialize<RecordedDecision>(e.DecisionJson, Json));
    }
    private static async Task<bool> LineageChanged(PoseidonDbContext db, Guid vessel, Guid control, long revision, Guid? evaluation, DateTimeOffset asOf, CancellationToken ct, bool currentView = true)
    {
        var pins = db.ControlEvidenceRequirements.Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == control && x.Revision == revision);
        if (await pins.AnyAsync(p => db.RegisterRevisions.Any(r => r.SavedAt <= asOf &&
            (r.Kind == "Source" && r.Id == p.SourceId && r.Revision > p.SourceRevision || r.Kind == "Requirement" && r.Id == p.RequirementId && r.Revision > p.RequirementRevision)) ||
            p.RuleId != null && db.RuleRevisions.Any(r => r.Id == p.RuleId && r.Revision > p.RuleRevision && r.SavedAt <= asOf), ct)) return true;
        if (evaluation == null) return false;
        var e = await db.ApplicabilityEvaluations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == evaluation && x.VesselId == vessel && x.SavedAt <= asOf, ct);
        if (currentView && e != null && await db.EvaluationQualifications.AnyAsync(x => x.EvaluationId == e.Id, ct) && (await CurrentRegulatoryContext.Stale(db, e.Id, vessel, asOf, ct)).Length > 0) return true;
        return e == null || e.ProfileRevision == null || await db.RegulatoryDraftRevisions.AnyAsync(x => x.VesselId == vessel && x.Revision > e.ProfileRevision && x.SavedAt <= asOf, ct);
    }
    private static async Task<IResult> Trace(HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a) && (!a.CanReadAudit || !a.Permissions.Contains("human.trace.read"))) return await Fail(h, 403, "permission_denied");
        if (h.Request.Query.Any(x => x.Value.Count != 1 || x.Key is not ("vesselId" or "evidenceId" or "evidenceRevision" or "controlId" or "controlRevision" or "asOf" or "offset")) ||
            !GuidQuery(h, "vesselId", out var vessel) || !GuidQuery(h, "evidenceId", out var evidence) || !GuidQuery(h, "controlId", out var control) ||
            !RevisionQuery(h, "evidenceRevision", out var er) || !RevisionQuery(h, "controlRevision", out var cr)) return await Fail(h, 400, "invalid_query");
        var asOf = DateTimeOffset.UtcNow; var historical = h.Request.Query.ContainsKey("asOf"); var offset = 0;
        if (historical && (!System.Text.RegularExpressions.Regex.IsMatch(h.Request.Query["asOf"].ToString(), @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z") ||
            !DateTimeOffset.TryParse(h.Request.Query["asOf"], CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf) || asOf > DateTimeOffset.UtcNow) ||
            h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset is < 0 or > 100000)) return await Fail(h, 400, "invalid_query");
        asOf = asOf.ToUniversalTime();
        var reader = Reader(h, a, vessel, evidence, control);
        var grants = Authorities(h, a, vessel, evidence, control, DateTimeOffset.UtcNow);
        if (reader == null && grants.Length == 0) return await Fail(h, 403, "trace_reader_required");
        if (!await a.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, ct)) return await Fail(h, 404, "resource_not_found");
        var ev = await Visible(db, vessel).SingleOrDefaultAsync(x => x.Kind == "Evidence" && x.Id == evidence && x.Revision == er && x.SavedAt <= asOf, ct);
        var co = await Visible(db, vessel).SingleOrDefaultAsync(x => x.Kind == "Control" && x.Id == control && x.Revision == cr && x.SavedAt <= asOf, ct);
        if (ev == null || co == null || !await db.ControlEvidenceEdges.AnyAsync(x => x.VesselId == vessel && x.Kind == "Evidence" && x.Id == evidence && x.Revision == er && x.TargetKind == "Control" && x.TargetId == control && x.TargetRevision == cr, ct)) return await Fail(h, 404, "resource_not_found");
        var segments = new List<TraceSegment>();
        var pins = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == control && x.Revision == cr).OrderBy(x => x.RequirementId).Take(33).ToArrayAsync(ct);
        foreach (var pin in pins.Take(32)) {
            var qualification = pin.RuleId == null ? null : await db.RegulatoryQualificationEvents.AsNoTracking().Where(q => q.RuleId == pin.RuleId && q.RuleRevision == pin.RuleRevision && q.SavedAt <= asOf)
                .OrderByDescending(q => q.Revision).FirstOrDefaultAsync(ct);
            var qualifiedState = qualification?.State is "ADOPTED" or "AWAITING" or "FUTURE_EFFECTIVE" or "IN_FORCE" or "SUPERSEDED" ? qualification.State : "DRAFT";
            var qualificationWhy = qualifiedState == "DRAFT" ? "SOURCE_ADMISSION_UNAVAILABLE" : "RECORDED_QUALIFICATION";
            foreach (var (kind, id, rev) in new[] { ("Source", pin.SourceId, pin.SourceRevision), ("Requirement", pin.RequirementId, pin.RequirementRevision) }) {
                var r = await db.RegisterRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Kind == kind && x.Id == id && x.Revision == rev && x.SavedAt <= asOf, ct);
                segments.Add(r == null ? new(kind, null, null, "Unavailable", "UNKNOWN", "REVISION_UNAVAILABLE", null, null) : new(kind, id, rev, "Available", qualifiedState, qualificationWhy, r.Hash, r.SavedAt));
            }
            if (pin.RuleId != null) {
                var rule = await db.RuleRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pin.RuleId && x.Revision == pin.RuleRevision && x.SavedAt <= asOf, ct);
                segments.Add(rule == null ? new("Rule", null, null, "Unavailable", "UNKNOWN", "REVISION_UNAVAILABLE", null, null) : new("Rule", rule.Id, rule.Revision, "Available", qualifiedState, qualificationWhy, rule.Hash, rule.SavedAt));
            }
        }
        var traceRuleIds = pins.Where(p => p.RuleId != null).Select(p => p.RuleId!.Value).ToArray();
        var latestEvaluation = await db.ApplicabilityEvaluations.AsNoTracking().Where(e => e.VesselId == vessel && e.SavedAt <= asOf && db.EvaluationQualifications.Any(q => q.EvaluationId == e.Id && traceRuleIds.Contains(q.RuleId)))
            .OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).FirstOrDefaultAsync(ct);
        var evaluationCauses = latestEvaluation == null ? new[] { "AUTHORITATIVE_EVALUATION_UNAVAILABLE" } : await CurrentRegulatoryContext.Stale(db, latestEvaluation.Id, vessel, DateTimeOffset.UtcNow, ct);
        segments.Add(latestEvaluation == null ? new("Applicability", null, null, "Unavailable", "REVIEW_REQUIRED", "AUTHORITATIVE_EVALUATION_UNAVAILABLE", null, null) :
            new("Applicability", latestEvaluation.Id, null, "Available", evaluationCauses.Length == 0 ? "RETAINED" : "REVIEW_REQUIRED", "HISTORICAL_INPUTS_ONLY", latestEvaluation.SnapshotHash, latestEvaluation.SavedAt));
        segments.Add(new("Control", co.Id, co.Revision, "Available", co.Lifecycle, "COMPLETION_AND_EFFECTIVENESS_UNKNOWN", co.Hash, co.SavedAt));
        segments.Add(new("Evidence", ev.Id, ev.Revision, "Available", ev.Lifecycle, "EXISTENCE_IS_NOT_VERIFICATION", ev.Hash, ev.SavedAt));
        // Reauthorize private decision history BEFORE paging/response. A receipt or
        // historical timestamp never retains a revoked role/type/subject binding.
        var reviewOperations = grants.Where(g => g.Capability is "human.verify" or "human.review.submit").Select(g => g.OperationId).ToArray();
        var decisionHashes = grants.Where(g => g.Capability is "human.decide" or "human.review.submit").Select(g => AuthorityHash(g, a)).ToArray();
        var legacyIds = grants.Where(g => g.Capability is "human.decide" or "human.review.submit").Select(g => g.Id).ToArray();
        var q = db.HumanReviewEvents.AsNoTracking().Where(x => x.VesselId == vessel && x.EvidenceId == evidence && x.EvidenceRevision == er && x.ControlId == control && x.ControlRevision == cr && x.SavedAt <= asOf &&
            (x.Kind == "Verification" && reviewOperations.Contains(x.OperationId) || x.Kind == "Decision" && (decisionHashes.Contains(x.AuthorityHash) || x.DecisionJson == null && x.ActorId == a.Id && legacyIds.Contains(x.AuthorityId))));
        if (!Read(a)) q = q.Where(_ => false);
        var events = await q.OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).ThenByDescending(x => x.Revision).Skip(offset).Take(26).ToArrayAsync(ct);
        var items = new List<HumanReviewDto>();
        foreach (var e in events.Take(25)) {
            var version = await db.HumanReviewHeads.Where(x => x.VesselId == vessel && x.Id == e.Id).Select(x => x.Version).SingleAsync(ct);
            items.Add(await Dto(db, e, version, asOf, ct, !historical));
        }
        var auditItems = new List<AuditTraceEvent>(); var auditHasMore = false;
        var authorizedMetadata = q;
        if (reader != null) {
            var kinds = reader.Kinds; var types = reader.DecisionTypes;
            var auditScope = db.HumanReviewEvents.AsNoTracking().Where(x => x.VesselId == vessel && x.EvidenceId == evidence && x.EvidenceRevision == er &&
                x.ControlId == control && x.ControlRevision == cr && x.SavedAt <= asOf && kinds.Contains(x.Kind) &&
                (x.Kind != "Decision" || types.Contains(x.DecisionType!) || x.DecisionType == null && reader.IncludeUntypedDecisions || x.DecisionType == null &&
                    db.HumanReviewEvents.Any(typed => typed.VesselId == vessel && typed.Id == x.Id && typed.SavedAt <= asOf && types.Contains(typed.DecisionType!))));
            authorizedMetadata = authorizedMetadata.Union(auditScope);
            var auditRows = await auditScope.OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).ThenByDescending(x => x.Revision).Skip(offset).Take(26).ToArrayAsync(ct);
            auditHasMore = auditRows.Length > 25;
            foreach (var e in auditRows.Take(25)) {
                var version = await db.HumanReviewHeads.Where(x => x.VesselId == vessel && x.Id == e.Id).Select(x => x.Version).SingleAsync(ct);
                var state = await Dto(db, e, version, asOf, ct, !historical);
                auditItems.Add(new(e.Id, e.Revision, e.Kind, e.State, state.CurrentState, e.SavedAt, e.ValidUntil, e.EvaluationId,
                    e.VerificationId, e.VerificationRevision, e.AuthorityId, e.RoleId, e.AuthorityHash, e.ContextHash));
            }
        }
        // Retained evaluations expose only their immutable lineage identifiers/hash.
        // Never send raw SnapshotJson/ResultJson, which may contain owning-domain data.
        foreach (var evaluationId in events.Take(25).Where(e => e.EvaluationId != null).Select(e => e.EvaluationId!.Value).Distinct()) {
            var evaluation = await db.ApplicabilityEvaluations.AsNoTracking().SingleOrDefaultAsync(e => e.Id == evaluationId && e.VesselId == vessel && e.SavedAt <= asOf, ct);
            if (evaluation != null) segments.Add(new("Evaluation", evaluation.Id, null, "Available", "RETAINED", "HISTORICAL_INPUTS_ONLY", evaluation.SnapshotHash, evaluation.SavedAt));
        }
        var eh = await db.ControlEvidenceHeads.SingleAsync(x => x.VesselId == vessel && x.Kind == "Evidence" && x.Id == evidence, ct);
        var ch = await db.ControlEvidenceHeads.SingleAsync(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == control, ct);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "HumanTraceRead", "Success", resourceId: evidence);
        var content = JsonSerializer.Deserialize<ControlEvidenceContent>(ev.ContentJson, Json)!;
        var historicalSuperseded = await db.ControlEvidenceRevisions.AnyAsync(x => x.VesselId == vessel && x.SavedAt <= asOf &&
            (x.Kind == "Evidence" && x.Id == evidence && x.Revision > er || x.Kind == "Control" && x.Id == control && x.Revision > cr), ct);
        var currentEvents = await authorizedMetadata.Where(e => db.HumanReviewHeads.Any(head => head.VesselId == vessel && head.Id == e.Id && head.Revision == e.Revision))
            .OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).Take(64).ToArrayAsync(ct);
        var chainBlockers = new List<string>(evaluationCauses);
        if (pins.Length == 0) chainBlockers.Add("REQUIREMENT_LINK_MISSING");
        if (co.Lifecycle != "COMPLETED") chainBlockers.Add("CONTROL_COMPLETION_PENDING");
        if (!currentEvents.Any(v => v.Kind == "Verification" && v.State == "VERIFIED" && v.ValidUntil > DateTimeOffset.UtcNow && v.EvaluationId == latestEvaluation?.Id)) chainBlockers.Add("VERIFICATION_PENDING_OR_STALE");
        if (!currentEvents.Any(d => d.Kind == "Decision" && d.State == "RECORDED" && d.EvaluationId == latestEvaluation?.Id)) chainBlockers.Add("DECISION_RECORDING_PENDING");
        var recordedChain = evaluationCauses.Length == 0 && pins.Length > 0 && co.Lifecycle == "COMPLETED" && !historicalSuperseded &&
            currentEvents.Any(v => v.Kind == "Verification" && v.State == "VERIFIED" && v.ValidUntil > DateTimeOffset.UtcNow && v.EvaluationId == latestEvaluation?.Id &&
                currentEvents.Any(d => d.Kind == "Decision" && d.State == "RECORDED" && d.VerificationId == v.Id && d.VerificationRevision == v.Revision && d.EvaluationId == v.EvaluationId));
        return Results.Ok(new { vesselId = vessel, evidenceId = evidence, evidenceRevision = er, evidenceVersion = eh.Version, controlId = control, controlRevision = cr, controlVersion = ch.Version,
            asOf, historical, evaluationId = latestEvaluation?.Id, disposition = recordedChain ? "RECORDED_CHAIN" : "REVIEW_REQUIRED", complete = recordedChain, segments, items, auditItems, auditHasMore, offset, hasMore = events.Length > 25,
            evidenceContext = new { content.Provenance, content.ObservedAt, content.ValidUntil, content.FreshUntil, timeState = ControlEvidencePolicy.TimeState(content, asOf, historicalSuperseded) },
            blockers = chainBlockers.Distinct().ToArray(),
            profileRevision = await db.RegulatoryDraftRevisions.Where(x => x.VesselId == vessel && x.SavedAt <= asOf).OrderByDescending(x => x.Revision).Select(x => (long?)x.Revision).FirstOrDefaultAsync(ct),
            authorities = (historical || Superseded(eh, ch, er, cr) ? [] : Authorities(h, a, vessel, evidence, control, DateTimeOffset.UtcNow))
                .Select(g => new { g.Id, g.OperationId, g.RoleId, g.Capability, g.ValidUntil, g.Recording }).ToArray() });
    }
    private static async Task<IResult> Save(HttpContext h, string kind, Guid? id)
    {
        var a = Actor(h); var db = h.RequestServices.GetRequiredService<PoseidonDbContext>(); var ct = h.RequestAborted;
        if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (h.Request.Query.Count != 1 || h.Request.Query["vesselId"].Count != 1 || !GuidQuery(h, "vesselId", out var vessel)) return await Fail(h, 400, "invalid_query");
        var scopedVessel = await a.Scope(db.Vessels).AsNoTracking().SingleOrDefaultAsync(x => x.Id == vessel, ct);
        if (scopedVessel == null) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(h, 400, "invalid_idempotency_key");
        HumanReviewCommand cmd; string body;
        try {
            if (!h.Request.HasJsonContentType()) throw new JsonException();
            using var b = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, b, 16384, ct);
            using var doc = JsonDocument.Parse(b.ToArray(), new() { MaxDepth = 10 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(doc.RootElement); body = doc.RootElement.GetRawText();
            cmd = JsonSerializer.Deserialize<HumanReviewCommand>(body, Json) ?? throw new JsonException();
            HumanReviewPolicy.Reason(cmd.Rationale);
            if (cmd.Recording is { } recording) {
                foreach (var value in new[] { recording.DecisionType, recording.HumanDecision, recording.TriggerSource, recording.DecisionContext }) HumanReviewPolicy.Reason(value);
                if (kind != "Decision" || recording.SubjectId == Guid.Empty || recording.SubjectType is not ("Control" or "Evidence") || cmd.State != "RECORDED") throw new JsonException();
            }
            if (cmd.EvidenceId == Guid.Empty || cmd.ControlId == Guid.Empty || cmd.OperationId == Guid.Empty || cmd.AuthorityId == Guid.Empty ||
                cmd.EvidenceRevision is <= 0 or > 9007199254740991 || cmd.ControlRevision is <= 0 or > 9007199254740991 || cmd.EvidenceVersion == 0 || cmd.ControlVersion == 0 ||
                (cmd.VerificationId == null) != (cmd.VerificationRevision == null) || cmd.VerificationRevision is <= 0 or > 9007199254740991 || cmd.VerificationId == Guid.Empty || cmd.EvaluationId == Guid.Empty) throw new JsonException();
        } catch (Exception e) when (e is JsonException or ArgumentException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        var capability = kind == "Decision" && cmd.State == "RECORDED" ? "human.decide" : id == null ? "human.review.submit" : kind == "Verification" ? "human.verify" : "human.decide";
        // Live appointments and relationships before replay; receipts confer no retained authority.
        HumanAuthority? Grant() {
            var live = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            return live == null || live.Id != a.Id || live.TenantId != a.TenantId || !live.CanAccessKind(scopedVessel.Kind) ? null :
                Authorities(h, live, vessel, cmd.EvidenceId, cmd.ControlId, DateTimeOffset.UtcNow).SingleOrDefault(g => g.Id == cmd.AuthorityId && g.OperationId == cmd.OperationId && g.Capability == capability &&
                    (cmd.Recording == null || RecordingScope(g, cmd.Recording) &&
                        (g.Recording!.AuthorityRole == "DPA" || g.Recording.AuthorityRole == "Tug Master" && scopedVessel.Kind == VesselKind.Tug || g.Recording.AuthorityRole == "Master" && scopedVessel.Kind == VesselKind.Ship)));
        }
        var grant = Grant(); if (grant == null) return await Fail(h, 403, "human_authority_required");
        var authorizedHash = AuthorityHash(grant, a);
        if (kind == "Decision" && cmd.State == "RECORDED") {
            if (cmd.Recording == null) return await Fail(h, 400, "decision_content_required");
            if (grant.Recording!.RequiredGate != null) {
                await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "HumanReview", "decision_type_gate_unavailable");
                return Results.Problem(statusCode: 409, title: "decision_type_gate_unavailable", extensions: new Dictionary<string, object?> {
                    ["code"] = "decision_type_gate_unavailable", ["requiredGate"] = grant.Recording.RequiredGate, ["sourceLocator"] = grant.Recording.GateSourceLocator });
            }
        }
        var ev = await Visible(db, vessel).SingleOrDefaultAsync(x => x.Kind == "Evidence" && x.Id == cmd.EvidenceId && x.Revision == cmd.EvidenceRevision, ct);
        var co = await Visible(db, vessel).SingleOrDefaultAsync(x => x.Kind == "Control" && x.Id == cmd.ControlId && x.Revision == cmd.ControlRevision, ct);
        if (ev == null || co == null || !await db.ControlEvidenceEdges.AnyAsync(x => x.VesselId == vessel && x.Kind == "Evidence" && x.Id == ev.Id && x.Revision == ev.Revision && x.TargetKind == "Control" && x.TargetId == co.Id && x.TargetRevision == co.Revision, ct)) return await Fail(h, 404, "resource_not_found");
        if (cmd.EvaluationId != null && !await db.ApplicabilityEvaluations.AnyAsync(x => x.Id == cmd.EvaluationId && x.VesselId == vessel, ct)) return await Fail(h, 404, "evaluation_unavailable");
        if (cmd.EvaluationId != null) {
            var pins = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == co.Id && x.Revision == co.Revision).ToArrayAsync(ct);
            if (pins.Length == 0 || pins.Any(x => x.RuleId == null)) return await Fail(h, 409, "evaluation_lineage_unavailable");
            foreach (var pin in pins)
                if (!await db.EvaluationRules.AnyAsync(x => x.EvaluationId == cmd.EvaluationId && x.RuleId == pin.RuleId && x.RuleRevision == pin.RuleRevision &&
                    db.RuleRevisions.Any(r => r.Id == x.RuleId && r.Revision == x.RuleRevision && r.RequirementId == pin.RequirementId && r.RequirementRevision == pin.RequirementRevision && r.SourceId == pin.SourceId && r.SourceRevision == pin.SourceRevision), ct)) return await Fail(h, 409, "evaluation_lineage_unavailable");
        }
        HumanReviewEvent? verification = null;
        if (kind == "Decision") {
            verification = await db.HumanReviewEvents.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == cmd.VerificationId && x.Revision == cmd.VerificationRevision &&
                x.Kind == "Verification" && x.EvidenceId == ev.Id && x.EvidenceRevision == ev.Revision && x.ControlId == co.Id && x.ControlRevision == co.Revision && x.OperationId == cmd.OperationId && x.EvaluationId == cmd.EvaluationId, ct);
            if (verification == null) return await Fail(h, 404, "verification_unavailable");
        } else if (cmd.VerificationId != null) return await Fail(h, 400, "invalid_lineage");
        if (cmd.State == "VERIFIED" && ev.FileId != null) {
            var file = await db.StoredFiles.AsNoTracking().SingleAsync(x => x.Id == ev.FileId && x.VesselId == vessel && x.Revision == ev.FileRevision && x.Checksum == ev.Checksum, ct);
            var settings = h.RequestServices.GetRequiredService<SecureFileSettings>();
            if (!settings.Ready(h.RequestServices.GetRequiredService<IWebHostEnvironment>())) return await Fail(h, 503, "file_settings_unavailable");
            try {
                await using var bytes = await h.RequestServices.GetRequiredService<FileObjects>().OpenAsync(file, ct);
                if (bytes.Length != file.Length || bytes.Length > settings.MaxBytes || Convert.ToHexString(await SHA256.HashDataAsync(bytes, ct)) != file.Checksum) return await Fail(h, 503, "file_integrity_unavailable");
            } catch (Exception e) when (e is IOException or InvalidDataException or HttpRequestException or CryptographicException) { return await Fail(h, 503, "file_integrity_unavailable"); }
        }
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { kind, id, vessel, body }));
        async Task<IResult?> Replay() {
            var liveGrant = Grant(); if (liveGrant == null) return await Fail(h, 403, "human_authority_required");
            var r = await db.HumanReviewReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
            if (r == null) return null;
            if (r.Fingerprint != fingerprint || r.VesselId != vessel) return await Fail(h, 409, "idempotency_key_reused");
            if (cmd.Recording != null) {
                var recorded = await db.HumanReviewEvents.AsNoTracking().SingleAsync(x => x.VesselId == vessel && x.Id == r.ResourceId && x.State == "RECORDED", ct);
                if (recorded.AuthorityHash != AuthorityHash(liveGrant, a)) return await Fail(h, 403, "human_authority_required");
                if (await RecordingChanged()) return await Fail(h, 409, "input_review_required");
                var capture = JsonSerializer.Deserialize<RecordedDecision>(recorded.DecisionJson!, Json)!;
                var currentProfile = await db.RegulatoryDrafts.Where(x => x.VesselId == vessel).Select(x => (long?)x.Revision).SingleOrDefaultAsync(ct);
                if (capture.ProfileRevision != currentProfile) return await Fail(h, 409, "input_review_required");
            }
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "HumanReviewReplay", "Success", resourceId: r.ResourceId);
            return Results.Content(r.OutcomeJson, "application/json");
        }
        async Task<bool> RecordingChanged() =>
            await db.ControlEvidenceHeads.AnyAsync(x => x.VesselId == vessel && (x.Kind == "Evidence" && x.Id == ev.Id && x.Revision != ev.Revision || x.Kind == "Control" && x.Id == co.Id && x.Revision != co.Revision), ct) ||
            await LineageChanged(db, vessel, co.Id, co.Revision, cmd.EvaluationId, DateTimeOffset.UtcNow, ct) ||
            await db.HumanReviewHeads.AnyAsync(x => x.VesselId == vessel && x.Id == cmd.VerificationId && x.Revision != cmd.VerificationRevision, ct);
        var replay = await Replay(); if (replay != null) return replay;
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "HumanReviewCommand", "Attempted", resourceId: id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        try {
            var head = id == null ? null : await db.HumanReviewHeads.SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
            if (id != null && head == null) return await Fail(h, 404, "resource_not_found");
            if (cmd.ExpectedVersion != (head?.Version ?? 0)) return await Fail(h, 409, "version_conflict");
            var prior = head == null ? null : await db.HumanReviewEvents.SingleAsync(x => x.VesselId == vessel && x.Id == head.Id && x.Revision == head.Revision, ct);
            if (prior != null && (prior.Kind != kind || prior.EvidenceId != ev.Id || prior.EvidenceRevision != ev.Revision || prior.ControlId != co.Id || prior.ControlRevision != co.Revision ||
                prior.OperationId != cmd.OperationId || prior.EvaluationId != cmd.EvaluationId || prior.VerificationId != cmd.VerificationId || prior.VerificationRevision != cmd.VerificationRevision)) return await Fail(h, 409, "immutable_context");
            var eh = await db.ControlEvidenceHeads.SingleAsync(x => x.VesselId == vessel && x.Kind == "Evidence" && x.Id == ev.Id, ct);
            var ch = await db.ControlEvidenceHeads.SingleAsync(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == co.Id, ct);
            if (eh.Version != cmd.EvidenceVersion || ch.Version != cmd.ControlVersion) return await Fail(h, 409, "input_version_conflict");
            var content = JsonSerializer.Deserialize<ControlEvidenceContent>(ev.ContentJson, Json)!; var now = DateTimeOffset.UtcNow;
            var changed = Superseded(eh, ch, ev.Revision, co.Revision) || await LineageChanged(db, vessel, co.Id, co.Revision, cmd.EvaluationId, now, ct);
            if (cmd.Recording != null) {
                var verificationHead = await db.HumanReviewHeads.SingleAsync(x => x.VesselId == vessel && x.Id == cmd.VerificationId, ct);
                changed |= verificationHead.Revision != cmd.VerificationRevision;
                // Preserve verifier SoD, but do not invent universal decision-maker SoD.
                db.Entry(verificationHead).Property(x => x.Revision).IsModified = true;
            }
            var requirementPins = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == co.Id && x.Revision == co.Revision)
                .Select(x => new RequirementPin(x.RequirementId, x.RequirementRevision, x.SourceId, x.SourceRevision, x.RuleId, x.RuleRevision)).ToArrayAsync(ct);
            var qualified = await QualifiedControlContext.Resolve(db, vessel, requirementPins, "Evidence", content, ev.FileId != null, now, ct);
            var fresh = qualified.Blockers.Length == 0;
            if (cmd.State == "VERIFIED") {
                if (!await db.EvaluationQualifications.AnyAsync(x => x.EvaluationId == cmd.EvaluationId, ct)) return await Fail(h, 409, "qualified_evaluation_required");
                if (qualified.Blockers.Length != 0) return Results.Problem(statusCode: 409, title: "qualified_evidence_policy_required", extensions: new Dictionary<string, object?> {
                    ["code"] = "qualified_evidence_policy_required", ["blockers"] = qualified.Blockers, ["requirements"] = requirementPins,
                    ["mechanisms"] = qualified.Policies.Where(p => p.RequiredVerificationMechanism != null).Select(p => new { p.RequiredVerificationMechanism, p.VerificationMechanismSourceLocator }).ToArray() });
                if (qualified.Policies.Any(p => p.RequireAcknowledgment == true && (p.AcknowledgmentRole == null || ev.RoleContext?.Split(',').Contains(p.AcknowledgmentRole.Value.ToString("D")) != true))) return await Fail(h, 409, "acknowledgment_role_required");
                await QualifiedControlContext.GuardInputs(db, vessel, requirementPins, ct);
            }
            var independent = prior != null && prior.SubmitterId != a.Id && ev.ActorId != a.Id && co.ActorId != a.Id && ev.ActorId != null && co.ActorId != null;
            if (cmd.State == "VERIFIED" && !independent) return await Fail(h, 409, "separation_of_duties");
            if (cmd.State == "VERIFIED" && cmd.EvaluationId == null) return await Fail(h, 409, "authoritative_evaluation_unavailable");
            var failure = HumanReviewPolicy.Transition(kind, prior?.State, cmd.State, cmd.ReasonCode, independent, !changed, fresh, prior?.ValidUntil <= now, changed);
            if (failure != null) return await Fail(h, 409, failure);
            // Serialize source/profile/rule changes with this review, without
            // overwriting content or manufacturing another source revision.
            var pins = await db.ControlEvidenceRequirements.Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == co.Id && x.Revision == co.Revision).ToArrayAsync(ct);
            foreach (var pin in pins) {
                foreach (var (pk, pid) in new[] { ("Source", pin.SourceId), ("Requirement", pin.RequirementId) }) {
                    var sourceHead = await db.RegisterDrafts.SingleAsync(x => x.Kind == pk && x.Id == pid, ct);
                    db.Entry(sourceHead).Property(x => x.Revision).IsModified = true;
                }
                if (pin.RuleId != null) {
                    var ruleHead = await db.RuleDrafts.SingleAsync(x => x.Id == pin.RuleId, ct);
                    db.Entry(ruleHead).Property(x => x.Revision).IsModified = true;
                }
            }
            RegulatoryDraft? profileHead = null;
            if (cmd.EvaluationId != null || cmd.Recording != null) {
                profileHead = await db.RegulatoryDrafts.SingleOrDefaultAsync(x => x.VesselId == vessel, ct);
                if (cmd.Recording != null && (profileHead == null || cmd.ExpectedProfileRevision != profileHead.Revision)) return await Fail(h, 409, "profile_context_conflict");
                if (profileHead != null) db.Entry(profileHead).Property(x => x.Revision).IsModified = true;
            }
            grant = Grant(); if (grant == null || AuthorityHash(grant, a) != authorizedHash) return await Fail(h, 403, "human_authority_required");
            if (head == null) { head = new() { TenantId = a.TenantId, VesselId = vessel, Id = Guid.NewGuid() }; db.HumanReviewHeads.Add(head); }
            else { db.Entry(head).Property(x => x.Version).OriginalValue = cmd.ExpectedVersion; db.Entry(head).Property(x => x.Revision).IsModified = true; }
            head.Revision++;
            // Touch both input heads with their xmin even for noops. A concurrent input
            // revision cannot race a verification and commit an apparently current result.
            foreach (var input in new[] { eh, ch }) db.Entry(input).Property(x => x.Revision).IsModified = true;
            var e = new HumanReviewEvent { TenantId = a.TenantId, VesselId = vessel, Id = head.Id, Revision = head.Revision, Kind = kind, State = cmd.State,
                EvidenceId = ev.Id, EvidenceRevision = ev.Revision, ControlId = co.Id, ControlRevision = co.Revision, EvaluationId = cmd.EvaluationId,
                VerificationId = cmd.VerificationId, VerificationRevision = cmd.VerificationRevision, SubmitterId = prior?.SubmitterId ?? a.Id, ActorId = a.Id,
                AuthorityId = grant.Id, RoleId = grant.RoleId, OperationId = grant.OperationId, ReasonCode = cmd.ReasonCode, Rationale = cmd.Rationale,
                AuthorityHash = AuthorityHash(grant, a),
                DecisionJson = cmd.Recording == null ? null : JsonSerializer.Serialize(new RecordedDecision(cmd.Recording, grant.Recording!, "UNASSESSED", profileHead?.Revision), Json),
                DecisionType = cmd.Recording?.DecisionType,
                ContextHash = cmd.Recording == null && prior != null ? prior.ContextHash : RegisterStorage.Hash(JsonSerializer.Serialize(new { ev.Hash, controlHash = co.Hash, cmd.EvaluationId, cmd.VerificationId, cmd.VerificationRevision, verificationHash = verification?.ContextHash, cmd.OperationId, cmd.Recording, policy = grant.Recording, profileRevision = profileHead?.Revision, priorContextHash = prior?.ContextHash })),
                SavedAt = now, ValidUntil = cmd.State == "VERIFIED" ? new[] { content.ValidUntil, content.FreshUntil, grant.ValidUntil }.Where(t => t != null).Select(t => t!.Value).Concat(qualified.Policies.Where(p => p.MaxAgeHours != null).Select(p => content.ObservedAt!.Value.AddHours(p.MaxAgeHours!.Value))).Min() : prior?.ValidUntil };
            db.HumanReviewEvents.Add(e); await db.SaveChangesAsync(ct);
            var outcome = JsonSerializer.Serialize(await Dto(db, e, head.Version, now, ct), Json);
            db.HumanReviewReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, ResourceId = head.Id, Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var commitGrant = Grant();
            if (commitGrant == null || AuthorityHash(commitGrant, a) != e.AuthorityHash) return await Fail(h, 403, "human_authority_required");
            await tx.CommitAsync(ct); return Results.Content(outcome, "application/json");
        } catch (DbUpdateConcurrencyException) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Replay() ?? await Fail(h, 409, "version_conflict");
        } catch (Exception e) when (e is DbUpdateException or NpgsqlException) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var state = e is PostgresException p ? p.SqlState : (e.InnerException as PostgresException)?.SqlState;
            var conflict = state is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
            return await Replay() ?? await Fail(h, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable");
        }
    }
}
