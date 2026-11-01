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
    Guid RoleId, string Capability, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HumanReviewCommand([property: JsonRequired] uint ExpectedVersion, Guid AuthorityId, Guid OperationId,
    Guid EvidenceId, long EvidenceRevision, uint EvidenceVersion, Guid ControlId, long ControlRevision, uint ControlVersion,
    Guid? EvaluationId, Guid? VerificationId, long? VerificationRevision, string State, string ReasonCode, string Rationale);
public sealed record HumanReviewDto(Guid Id, Guid VesselId, long Revision, uint Version, string Kind, string State,
    string CurrentState, Guid EvidenceId, long EvidenceRevision, Guid ControlId, long ControlRevision, Guid? EvaluationId,
    Guid? VerificationId, long? VerificationRevision, Guid ActorId, Guid RoleId, Guid AuthorityId, Guid OperationId,
    string ReasonCode, string Rationale, string AuthorityHash, string ContextHash, DateTimeOffset SavedAt, DateTimeOffset? ValidUntil, DateTimeOffset AsOf);
public sealed record TraceSegment(string Kind, Guid? Id, long? Revision, string Availability, string State, string Why, string? Hash, DateTimeOffset? SavedAt);

public static class HumanReviewEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 10 };
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Read(TrustedActor a) => ControlEvidenceEndpoints.Read(a) && a.Permissions.Contains("human.review.read");
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
    private static async Task<HumanReviewDto> Dto(PoseidonDbContext db, HumanReviewEvent e, uint version, DateTimeOffset asOf, CancellationToken ct) {
        var superseded = await db.ControlEvidenceRevisions.AnyAsync(x => x.VesselId == e.VesselId && x.SavedAt <= asOf &&
            (x.Kind == "Evidence" && x.Id == e.EvidenceId && x.Revision > e.EvidenceRevision || x.Kind == "Control" && x.Id == e.ControlId && x.Revision > e.ControlRevision), ct)
            || await LineageChanged(db, e.VesselId, e.ControlId, e.ControlRevision, e.EvaluationId, asOf, ct);
        return new(e.Id, e.VesselId, e.Revision, version, e.Kind, e.State, HumanReviewPolicy.Current(e.State, e.ValidUntil, superseded, asOf), e.EvidenceId, e.EvidenceRevision,
            e.ControlId, e.ControlRevision, e.EvaluationId, e.VerificationId, e.VerificationRevision, e.ActorId, e.RoleId, e.AuthorityId, e.OperationId,
            e.ReasonCode, e.Rationale, e.AuthorityHash, e.ContextHash, e.SavedAt, e.ValidUntil, asOf);
    }
    private static async Task<bool> LineageChanged(PoseidonDbContext db, Guid vessel, Guid control, long revision, Guid? evaluation, DateTimeOffset asOf, CancellationToken ct)
    {
        var pins = db.ControlEvidenceRequirements.Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == control && x.Revision == revision);
        if (await pins.AnyAsync(p => db.RegisterRevisions.Any(r => r.SavedAt <= asOf &&
            (r.Kind == "Source" && r.Id == p.SourceId && r.Revision > p.SourceRevision || r.Kind == "Requirement" && r.Id == p.RequirementId && r.Revision > p.RequirementRevision)) ||
            p.RuleId != null && db.RuleRevisions.Any(r => r.Id == p.RuleId && r.Revision > p.RuleRevision && r.SavedAt <= asOf), ct)) return true;
        if (evaluation == null) return false;
        var e = await db.ApplicabilityEvaluations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == evaluation && x.VesselId == vessel && x.SavedAt <= asOf, ct);
        return e == null || e.ProfileRevision == null || await db.RegulatoryDraftRevisions.AnyAsync(x => x.VesselId == vessel && x.Revision > e.ProfileRevision && x.SavedAt <= asOf, ct);
    }
    private static async Task<IResult> Trace(HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (h.Request.Query.Any(x => x.Value.Count != 1 || x.Key is not ("vesselId" or "evidenceId" or "evidenceRevision" or "controlId" or "controlRevision" or "asOf" or "offset")) ||
            !GuidQuery(h, "vesselId", out var vessel) || !GuidQuery(h, "evidenceId", out var evidence) || !GuidQuery(h, "controlId", out var control) ||
            !RevisionQuery(h, "evidenceRevision", out var er) || !RevisionQuery(h, "controlRevision", out var cr)) return await Fail(h, 400, "invalid_query");
        var asOf = DateTimeOffset.UtcNow; var historical = h.Request.Query.ContainsKey("asOf"); var offset = 0;
        if (historical && (!System.Text.RegularExpressions.Regex.IsMatch(h.Request.Query["asOf"].ToString(), @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z") ||
            !DateTimeOffset.TryParse(h.Request.Query["asOf"], CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf) || asOf > DateTimeOffset.UtcNow) ||
            h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset is < 0 or > 100000)) return await Fail(h, 400, "invalid_query");
        asOf = asOf.ToUniversalTime();
        if (!await a.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, ct)) return await Fail(h, 404, "resource_not_found");
        var ev = await Visible(db, vessel).SingleOrDefaultAsync(x => x.Kind == "Evidence" && x.Id == evidence && x.Revision == er && x.SavedAt <= asOf, ct);
        var co = await Visible(db, vessel).SingleOrDefaultAsync(x => x.Kind == "Control" && x.Id == control && x.Revision == cr && x.SavedAt <= asOf, ct);
        if (ev == null || co == null || !await db.ControlEvidenceEdges.AnyAsync(x => x.VesselId == vessel && x.Kind == "Evidence" && x.Id == evidence && x.Revision == er && x.TargetKind == "Control" && x.TargetId == control && x.TargetRevision == cr, ct)) return await Fail(h, 404, "resource_not_found");
        var segments = new List<TraceSegment>();
        var pins = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == control && x.Revision == cr).OrderBy(x => x.RequirementId).Take(33).ToArrayAsync(ct);
        foreach (var pin in pins.Take(32)) {
            foreach (var (kind, id, rev) in new[] { ("Source", pin.SourceId, pin.SourceRevision), ("Requirement", pin.RequirementId, pin.RequirementRevision) }) {
                var r = await db.RegisterRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Kind == kind && x.Id == id && x.Revision == rev && x.SavedAt <= asOf, ct);
                segments.Add(r == null ? new(kind, null, null, "Unavailable", "UNKNOWN", "REVISION_UNAVAILABLE", null, null) : new(kind, id, rev, "Available", "DRAFT", "SOURCE_ADMISSION_UNAVAILABLE", r.Hash, r.SavedAt));
            }
            if (pin.RuleId != null) {
                var rule = await db.RuleRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pin.RuleId && x.Revision == pin.RuleRevision && x.SavedAt <= asOf, ct);
                segments.Add(rule == null ? new("Rule", null, null, "Unavailable", "UNKNOWN", "REVISION_UNAVAILABLE", null, null) : new("Rule", rule.Id, rule.Revision, "Available", rule.Lifecycle, "SOURCE_ADMISSION_UNAVAILABLE", rule.Hash, rule.SavedAt));
            }
        }
        segments.Add(new("Applicability", null, null, "Unavailable", "REVIEW_REQUIRED", "AUTHORITATIVE_EVALUATION_UNAVAILABLE", null, null));
        segments.Add(new("Control", co.Id, co.Revision, "Available", co.Lifecycle, "COMPLETION_AND_EFFECTIVENESS_UNKNOWN", co.Hash, co.SavedAt));
        segments.Add(new("Evidence", ev.Id, ev.Revision, "Available", ev.Lifecycle, "EXISTENCE_IS_NOT_VERIFICATION", ev.Hash, ev.SavedAt));
        var q = db.HumanReviewEvents.AsNoTracking().Where(x => x.VesselId == vessel && x.EvidenceId == evidence && x.EvidenceRevision == er && x.ControlId == control && x.ControlRevision == cr && x.SavedAt <= asOf);
        var events = await q.OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).ThenByDescending(x => x.Revision).Skip(offset).Take(26).ToArrayAsync(ct);
        var items = new List<HumanReviewDto>();
        foreach (var e in events.Take(25)) {
            var version = await db.HumanReviewHeads.Where(x => x.VesselId == vessel && x.Id == e.Id).Select(x => x.Version).SingleAsync(ct);
            items.Add(await Dto(db, e, version, asOf, ct));
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
        return Results.Ok(new { vesselId = vessel, evidenceId = evidence, evidenceRevision = er, evidenceVersion = eh.Version, controlId = control, controlRevision = cr, controlVersion = ch.Version,
            asOf, historical, disposition = "REVIEW_REQUIRED", complete = false, segments, items, offset, hasMore = events.Length > 25,
            evidenceContext = new { content.Provenance, content.ObservedAt, content.ValidUntil, content.FreshUntil, timeState = ControlEvidencePolicy.TimeState(content, asOf, historicalSuperseded) },
            blockers = new[] { "SOURCE_ADMISSION_UNAVAILABLE", "AUTHORITATIVE_EVALUATION_UNAVAILABLE", "EXTERNAL_SIGNATURE_UNAVAILABLE" }.Concat(pins.Length == 0 ? ["REQUIREMENT_LINK_MISSING"] : Array.Empty<string>()).ToArray(),
            authorities = (historical || Superseded(eh, ch, er, cr) ? [] : Authorities(h, a, vessel, evidence, control, DateTimeOffset.UtcNow))
                .Select(g => new { g.Id, g.OperationId, g.RoleId, g.Capability, g.ValidUntil }).ToArray() });
    }
    private static async Task<IResult> Save(HttpContext h, string kind, Guid? id)
    {
        var a = Actor(h); var db = h.RequestServices.GetRequiredService<PoseidonDbContext>(); var ct = h.RequestAborted;
        if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (h.Request.Query.Count != 1 || h.Request.Query["vesselId"].Count != 1 || !GuidQuery(h, "vesselId", out var vessel)) return await Fail(h, 400, "invalid_query");
        if (!await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, ct)) return await Fail(h, 404, "resource_not_found");
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
            if (cmd.EvidenceId == Guid.Empty || cmd.ControlId == Guid.Empty || cmd.OperationId == Guid.Empty || cmd.AuthorityId == Guid.Empty ||
                cmd.EvidenceRevision is <= 0 or > 9007199254740991 || cmd.ControlRevision is <= 0 or > 9007199254740991 || cmd.EvidenceVersion == 0 || cmd.ControlVersion == 0 ||
                (cmd.VerificationId == null) != (cmd.VerificationRevision == null) || cmd.VerificationRevision is <= 0 or > 9007199254740991 || cmd.VerificationId == Guid.Empty || cmd.EvaluationId == Guid.Empty) throw new JsonException();
        } catch (Exception e) when (e is JsonException or ArgumentException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        var capability = id == null ? "human.review.submit" : kind == "Verification" ? "human.verify" : "human.decide";
        // Live appointments and relationships before replay; receipts confer no retained authority.
        HumanAuthority? Grant() {
            var live = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            return live == null || live.Id != a.Id || live.TenantId != a.TenantId ? null :
                Authorities(h, live, vessel, cmd.EvidenceId, cmd.ControlId, DateTimeOffset.UtcNow).SingleOrDefault(g => g.Id == cmd.AuthorityId && g.OperationId == cmd.OperationId && g.Capability == capability);
        }
        var grant = Grant(); if (grant == null) return await Fail(h, 403, "human_authority_required");
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
            if (Grant() == null) return await Fail(h, 403, "human_authority_required");
            var r = await db.HumanReviewReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
            if (r == null) return null;
            if (r.Fingerprint != fingerprint || r.VesselId != vessel) return await Fail(h, 409, "idempotency_key_reused");
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "HumanReviewReplay", "Success", resourceId: r.ResourceId);
            return Results.Content(r.OutcomeJson, "application/json");
        }
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
            var fresh = ev.FileId != null && content.ObservedAt <= now && content.ValidFrom <= now && content.ValidUntil > now && content.FreshUntil > now;
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
            if (cmd.EvaluationId != null) {
                var profileHead = await db.RegulatoryDrafts.SingleOrDefaultAsync(x => x.VesselId == vessel, ct);
                if (profileHead != null) db.Entry(profileHead).Property(x => x.Revision).IsModified = true;
            }
            grant = Grant(); if (grant == null) return await Fail(h, 403, "human_authority_required");
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
                AuthorityHash = RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actorId = a.Id, tenantId = a.TenantId, actorKind = "Human" })),
                ContextHash = prior?.ContextHash ?? RegisterStorage.Hash(JsonSerializer.Serialize(new { ev.Hash, controlHash = co.Hash, cmd.EvaluationId, cmd.VerificationId, cmd.VerificationRevision, cmd.OperationId })),
                SavedAt = now, ValidUntil = cmd.State == "VERIFIED" ? new[] { content.ValidUntil!.Value, content.FreshUntil!.Value, grant.ValidUntil }.Min() : prior?.ValidUntil };
            db.HumanReviewEvents.Add(e); await db.SaveChangesAsync(ct);
            var outcome = JsonSerializer.Serialize(await Dto(db, e, head.Version, now, ct), Json);
            db.HumanReviewReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, ResourceId = head.Id, Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Content(outcome, "application/json");
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
