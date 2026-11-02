using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RuleAuthorCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Contract, JsonElement? Rule, Guid? SourceId, long? SourceRevision,
    Guid? RequirementId, long? RequirementRevision, string ReasonCode);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RuleReviewCommand([property: JsonRequired] uint ExpectedVersion, string RequestedLifecycle, string ReasonCode);
public sealed record RuleVersionDto(Guid Id, long Revision, string Contract, string? RuleJson, JsonElement? Rule, string Hash, Guid? SourceId, long? SourceRevision,
    Guid? RequirementId, long? RequirementRevision, string Lifecycle, string RequestedLifecycle, string ReviewStatus, DateTimeOffset SavedAt);
public sealed record RuleDraftDto(uint Version, RuleVersionDto Current);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvaluateCommand(Guid VesselId, Guid[] RuleIds, string? AsOf = null, OwningInputReferences? OwningInputs = null);
public sealed record EvaluationPrediction(Guid RuleId, long Revision, string Hash, Guid? SourceId, long? SourceRevision, Guid? RequirementId, long? RequirementRevision, RulePrediction Prediction);
public sealed record EvaluationDto(Guid Id, Guid VesselId, DateTimeOffset SavedAt, DateTimeOffset AsOf, string AdapterVersion,
    long? ProfileRevision, string SnapshotHash, JsonElement Snapshot, string RuleSetHash, EvaluationPrediction[] Results,
    RuleConflict[] Conflicts,
    bool Preview = true, bool Authoritative = false, string CurrentResult = "REVIEW_REQUIRED", bool Simulation = true);

public static class LifecycleApplicabilityEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 24, PropertyNameCaseInsensitive = false };
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    internal static bool Read(TrustedActor a) => a.HasWriteAttribution && a.CanReadRegulatory && a.Permissions.Contains("applicability.read");
    internal static bool Write(TrustedActor a) => Read(a) && a.CanWriteRegulatory && a.Permissions.Contains("rules.write");
    internal static bool Review(TrustedActor a) => Read(a) && a.HasWriteAttribution && a.Permissions.Contains("rules.review");
    internal static bool Evaluate(TrustedActor a) => Read(a) && a.HasWriteAttribution && a.Permissions.Contains("applicability.evaluate");
    private static RuleVersionDto Dto(RuleRevision r)
    {
        using var document = r.RuleJson == null ? null : JsonDocument.Parse(r.RuleJson);
        return new(r.Id, r.Revision, r.Contract, r.RuleJson, document?.RootElement.Clone(), r.Hash, r.SourceId, r.SourceRevision, r.RequirementId, r.RequirementRevision,
            r.Lifecycle, r.RequestedLifecycle, r.ReviewStatus, r.SavedAt);
    }
    private static async Task<IResult> Fail(HttpContext h, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "Applicability", code);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = h.TraceIdentifier });
    }
    public static void MapLifecycleApplicability(this IEndpointRouteBuilder app)
    {
        var rules = app.MapGroup("/api/v1/rules").RequireAuthorization("DirectoryRead");
        rules.MapGet("/capabilities", async (HttpContext h, SecurityAudit audit) => {
            var a = Actor(h); await audit.WriteAsync(h, "RuleCapabilities", "Success");
            return Results.Ok(new { canRead = Read(a), canWrite = Write(a), canReview = Review(a), canEvaluate = Evaluate(a), canActivate = false, contract = ApplicabilityEngine.Contract });
        });
        rules.MapGet("", (HttpContext h, PoseidonDbContext db, SecurityAudit a) => List(h, db, a, null));
        rules.MapGet("/{id:guid}/versions", (Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit a) => List(h, db, a, id));
        rules.MapPost("", (HttpContext h) => Command(h, "author", null)).Accepts<RuleAuthorCommand>("application/json").Produces<RuleDraftDto>();
        rules.MapPut("/{id:guid}", (Guid id, HttpContext h) => Command(h, "author", id)).Accepts<RuleAuthorCommand>("application/json").Produces<RuleDraftDto>();
        rules.MapPost("/{id:guid}/review", (Guid id, HttpContext h) => Command(h, "review", id)).Accepts<RuleReviewCommand>("application/json").Produces<RuleDraftDto>();
        var eval = app.MapGroup("/api/v1/applicability").RequireAuthorization("DirectoryRead");
        eval.MapPost("/evaluate", (HttpContext h) => Command(h, "evaluate", null)).Accepts<EvaluateCommand>("application/json").Produces<EvaluationDto>();
        eval.MapPost("/simulate", (HttpContext h) => Command(h, "simulate", null)).Accepts<EvaluateCommand>("application/json").Produces<EvaluationDto>();
        eval.MapGet("/evaluations/{id:guid}/current", Current);
        eval.MapGet("/evaluations/{id:guid}", Detail).Produces<EvaluationDto>();
        eval.MapGet("/evaluations", History);
    }
    private static bool Paging(HttpContext h, string[] extra, out int offset)
    {
        offset = 0;
        return !h.Request.Query.Any(p => p.Value.Count != 1 || p.Key != "offset" && !extra.Contains(p.Key)) &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) && offset is >= 0 and <= 100000);
    }
    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db, SecurityAudit audit, Guid? id)
    {
        if (!Read(Actor(h))) return await Fail(h, 403, "permission_denied");
        if (!Paging(h, [], out var offset)) return await Fail(h, 400, "invalid_query");
        if (id != null && !await db.RuleDrafts.AnyAsync(x => x.Id == id, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var q = db.RuleRevisions.AsNoTracking().AsQueryable();
        if (id != null) q = q.Where(x => x.Id == id);
        else q = q.Where(x => db.RuleDrafts.Any(d => d.Id == x.Id && d.Revision == x.Revision));
        var rows = await q.OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).ThenByDescending(x => x.Revision).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        // Version lookup is only for current heads; old immutable rows retain revision.
        var ids = rows.Select(r => r.Id).ToArray();
        var heads = await db.RuleDrafts.AsNoTracking().Where(x => ids.Contains(x.Id)).ToArrayAsync(h.RequestAborted);
        await audit.WriteAsync(h, "RulesRead", "Success", resourceId: id);
        return Results.Ok(new PageDto<RuleDraftDto>(rows.Take(25).Select(r => new RuleDraftDto(heads.Single(d => d.Id == r.Id).Version, Dto(r))).ToArray(), offset, 25, rows.Length > 25));
    }
    private static async Task<IResult> Current(Guid id, HttpContext h, PoseidonDbContext db) {
        var a = Actor(h); if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_query");
        var vessels = a.Scope(db.Vessels);
        var row = await OperationEquipmentCommands.Evaluations(db, a).Where(x => a.Permissions.Contains("file.read") || !db.RegulatoryDraftRevisions.Any(p => p.VesselId == x.VesselId && p.Revision == x.ProfileRevision && p.ConstructionFileId != null))
            .SingleOrDefaultAsync(x => x.Id == id && vessels.Any(v => v.Id == x.VesselId), h.RequestAborted);
        if (row == null) return await Fail(h, 404, "resource_not_found");
        var causes = await CurrentRegulatoryContext.Stale(db, id, row.VesselId, h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), h.RequestAborted, h.RequestServices.GetRequiredService<FederationSettings>());
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ApplicabilityCurrentRead", "Success", resourceId: id);
        return Results.Ok(new { id, current = causes.Length == 0, causes });
    }
    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        var actor = Actor(h); if (!Read(actor)) return await Fail(h, 403, "permission_denied");
        var vessels = actor.Scope(db.Vessels);
        var row = await OperationEquipmentCommands.Evaluations(db, actor).Where(x => actor.Permissions.Contains("file.read") || !db.RegulatoryDraftRevisions.Any(p => p.VesselId == x.VesselId && p.Revision == x.ProfileRevision && p.ConstructionFileId != null))
            .SingleOrDefaultAsync(x => x.Id == id && vessels.Any(v => v.Id == x.VesselId), h.RequestAborted);
        if (row == null) return await Fail(h, 404, "resource_not_found");
        await audit.WriteAsync(h, "ApplicabilityRead", "Success", resourceId: id);
        return Results.Content(row.ResultJson, "application/json");
    }
    private static async Task<IResult> History(HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        var actor = Actor(h); if (!Read(actor)) return await Fail(h, 403, "permission_denied");
        if (!Paging(h, ["vesselId"], out var offset) || !Guid.TryParse(h.Request.Query["vesselId"], out var vessel)) return await Fail(h, 400, "invalid_query");
        if (!await actor.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var rows = await OperationEquipmentCommands.Evaluations(db, actor).Where(x => x.VesselId == vessel && (actor.Permissions.Contains("file.read") || !db.RegulatoryDraftRevisions.Any(p => p.VesselId == x.VesselId && p.Revision == x.ProfileRevision && p.ConstructionFileId != null)))
            .OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).Skip(offset).Take(26)
            .Select(x => new { x.Id, x.VesselId, x.SavedAt, x.AsOf, x.SnapshotHash, x.RuleSetHash }).ToArrayAsync(h.RequestAborted);
        await audit.WriteAsync(h, "ApplicabilityHistory", "Success", resourceId: vessel);
        return Results.Ok(new { items = rows.Take(25), offset, limit = 25, hasMore = rows.Length > 25 });
    }
    private static async Task<IResult> Command(HttpContext h, string action, Guid? id)
    {
        var actor = Actor(h); var ct = h.RequestAborted;
        if (!(action == "author" ? Write(actor) : action == "review" ? Review(actor) : Evaluate(actor))) return await Fail(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ApplicabilityCommand", "Attempted", resourceId: id);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(h, 400, "invalid_idempotency_key");
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > 49152) return await Fail(h, 400, "invalid_request");
        RuleAuthorCommand? author = null; RuleReviewCommand? review = null; EvaluateCommand? evaluate = null; string body;
        try {
            using var buffer = new MemoryStream(); var bytes = new byte[4096]; int count;
            while ((count = await h.Request.Body.ReadAsync(bytes, ct)) > 0) {
                if (buffer.Length + count > 49152) return await Fail(h, 400, "request_limit"); buffer.Write(bytes, 0, count);
            }
            using var doc = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 24 }); ApplicabilityEngine.RejectDuplicateKeys(doc.RootElement);
            body = doc.RootElement.GetRawText();
            if (action == "author") author = JsonSerializer.Deserialize<RuleAuthorCommand>(body, Json) ?? throw new JsonException();
            else if (action == "review") review = JsonSerializer.Deserialize<RuleReviewCommand>(body, Json) ?? throw new JsonException();
            else evaluate = JsonSerializer.Deserialize<EvaluateCommand>(body, Json) ?? throw new JsonException();
        } catch (JsonException) { return await Fail(h, 400, "invalid_request"); }
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { action, id, body }));
        // Live capabilities AND live vessel scope are checked before receipt lookup.
        if (evaluate != null && !await actor.Scope(db.Vessels).AnyAsync(x => x.Id == evaluate.VesselId, ct)) return await Fail(h, 404, "resource_not_found");
        if (evaluate != null && !await OperationEquipmentContext.Authorized(db, actor, evaluate.VesselId, evaluate.OwningInputs, ct)) return await Fail(h, 403, "permission_denied");
        if (evaluate != null && !actor.Permissions.Contains("file.read") && await db.RegulatoryDraftRevisions.AnyAsync(x => x.VesselId == evaluate.VesselId && x.ConstructionFileId != null, ct)) return await Fail(h, 403, "permission_denied");
        // 15.01 §104..108 and 09.03 publication boundary: draft/PENDING source
        // outputs are non-mutating simulation ONLY. No receipt, activation or saved
        // evaluation is manufactured while the admission/decision chain is absent.

        async Task<IResult?> Replay() {
            var live = Actor(h); if (live == null || live.Id != actor.Id || live.TenantId != actor.TenantId || !(action == "author" ? Write(live) : action == "review" ? Review(live) : Evaluate(live))) return await Fail(h, 403, "permission_denied");
            if (evaluate != null && !await live.Scope(db.Vessels).AnyAsync(v => v.Id == evaluate.VesselId, ct)) return await Fail(h, 404, "resource_not_found");
            if (evaluate != null && !await OperationEquipmentContext.Authorized(db, live, evaluate.VesselId, evaluate.OwningInputs, ct)) return await Fail(h, 403, "permission_denied");
            if (evaluate != null && !live.Permissions.Contains("file.read") && await db.RegulatoryDraftRevisions.AnyAsync(x => x.VesselId == evaluate.VesselId && x.ConstructionFileId != null, ct)) return await Fail(h, 403, "permission_denied");
            var r = await db.ApplicabilityReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.Key == key, ct);
            if (r == null) return null;
            if (r.Action != action || r.Fingerprint != fingerprint) return await Fail(h, 409, "idempotency_key_reused");
            if (r.VesselId != null && !await actor.Scope(db.Vessels).AnyAsync(x => x.Id == r.VesselId, ct)) return await Fail(h, 404, "resource_not_found");
            h.Response.Headers["Idempotency-Replayed"] = "true"; return Results.Content(r.OutcomeJson, "application/json");
        }
        var replay = await Replay(); if (replay != null) return replay;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var usedAttestations = new List<RegulatoryFactAttestation>();
        try {
            object outcome; Guid resource; var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(); now = now.AddTicks(-(now.Ticks % 10));
            if (evaluate != null) {
                if (evaluate.RuleIds == null || evaluate.RuleIds.Length is < 1 or > 64 || evaluate.RuleIds.Distinct().Count() != evaluate.RuleIds.Length || evaluate.RuleIds.Contains(Guid.Empty)) return await Fail(h, 400, "invalid_rule_set");
                var asOf = now;
                if (action == "evaluate" && evaluate.AsOf != null) return await Fail(h, 400, "current_evaluation_requires_server_time");
                if (evaluate.AsOf != null && (!DateTimeOffset.TryParseExact(evaluate.AsOf, ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"], CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf) || asOf > now)) return await Fail(h, 400, "invalid_as_of");
                asOf = asOf.ToUniversalTime(); asOf = asOf.AddTicks(-(asOf.Ticks % 10));
                var vessel = await actor.Scope(db.Vessels).AsNoTracking().SingleAsync(x => x.Id == evaluate.VesselId, ct);
                var revisions = await db.RuleRevisions.AsNoTracking().Where(r => evaluate.RuleIds.Contains(r.Id) && r.SavedAt <= asOf &&
                    !db.RuleRevisions.Any(later => later.Id == r.Id && later.SavedAt <= asOf && later.Revision > r.Revision)).OrderBy(r => r.Id).ToArrayAsync(ct);
                if (revisions.Length != evaluate.RuleIds.Length) return await Fail(h, 404, "rule_unavailable");
                if (revisions.Any(r => r.RuleJson == null)) return await Fail(h, 409, "incomplete_rule");
                var (profileRevision, facts) = await ApplicabilityContextResolver.Resolve(db, vessel, asOf, ct);
                var snapshot = JsonSerializer.SerializeToElement(new { adapterVersion = ApplicabilityEngine.AdapterVersion, vesselId = vessel.Id, vesselVersion = vessel.Version, profileRevision, asOf, facts }, Json);
                var snapshotJson = snapshot.GetRawText(); var results = revisions.Select(r => new EvaluationPrediction(r.Id, r.Revision, r.Hash, r.SourceId, r.SourceRevision, r.RequirementId, r.RequirementRevision,
                    ApplicabilityEngine.Preview(ApplicabilityEngine.Parse(r.RuleJson!), facts, asOf))).ToArray();
                var ruleSetHash = RegisterStorage.Hash(JsonSerializer.Serialize(revisions.Select(r => new { r.Id, r.Revision, r.Hash, r.SourceId, r.SourceRevision, r.RequirementId, r.RequirementRevision }), Json));
                resource = Guid.NewGuid();
                var conflicts = ApplicabilityEngine.Conflicts(revisions.Select((r, i) => new RuleCandidate($"{r.Id:D}/{r.Revision}", ApplicabilityEngine.Parse(r.RuleJson!), results[i].Prediction)));
                if (action == "evaluate") {
                    if (profileRevision == null) return await Fail(h, 409, "profile_unavailable");
                    var qualifications = new List<object>(); var bindings = new List<EvaluationQualification>();
                    for (var i = 0; i < revisions.Length; i++) {
                        var rule = revisions[i];
                        var ruleHead = await db.RuleDrafts.SingleAsync(x => x.Id == rule.Id, ct);
                        var q = await CurrentRegulatoryContext.Qualification(db, rule, ct);
                        if (q == null || ruleHead.Revision != rule.Revision) return await Fail(h, 409, "source_admission_pending");
                        var qualifiedHead = await db.RegulatoryQualificationHeads.SingleAsync(x => x.RuleId == rule.Id, ct);
                        db.Entry(qualifiedHead).Property(x => x.Revision).IsModified = true;
                        db.Entry(ruleHead).Property(x => x.Revision).IsModified = true;
                        foreach (var (kind, inputId) in new[] { ("Source", rule.SourceId), ("Requirement", rule.RequirementId) }) {
                            var input = await db.RegisterDrafts.SingleAsync(x => x.Kind == kind && x.Id == inputId, ct);
                            db.Entry(input).Property(x => x.Revision).IsModified = true;
                        }
                        var (qualifiedFacts, attestation) = await CurrentRegulatoryContext.Facts(db, q, vessel.Id, profileRevision, facts, asOf, ct, h.RequestServices.GetRequiredService<FederationSettings>());
                        if (evaluate.OwningInputs != null) {
                            foreach (var field in OperationEquipmentInputs.Fields.Concat(ComplianceInputs.Fields)) qualifiedFacts[field] = new(ApplicabilityEngine.Fields[field], "Unknown", JsonSerializer.SerializeToElement<object?>(null), "OwningReferenceUnqualified", ApplicabilityEngine.AdapterVersion);
                            await OperationEquipmentContext.Apply(db, h.RequestServices.GetRequiredService<FederationSettings>(), vessel.Id, q, evaluate.OwningInputs, qualifiedFacts, asOf, ct);
                        }
                        if (attestation != null && qualifiedFacts.Values.Any(f => f.Provenance == "RecordedHumanAttestation")) usedAttestations.Add(attestation);
                        var parsed = ApplicabilityEngine.Parse(rule.RuleJson!);
                        var prediction = ApplicabilityEngine.Preview(parsed with { Lifecycle = parsed.Lifecycle with { State = q.State, Supersedes = q.SupersedingRuleId == null ? parsed.Lifecycle.Supersedes : $"{q.SupersedingRuleId:D}/{q.SupersedingRuleRevision}" } }, qualifiedFacts, asOf);
                        var manningReview = prediction.Result == "APPLICABLE" && ComplianceInputs.ManningReview(parsed, qualifiedFacts);
                        prediction = prediction with { Authoritative = true, CurrentResult = manningReview ? "REVIEW_REQUIRED" : prediction.Result,
                            Blockers = prediction.Reason.ReviewRequired ? ["INPUT_REVIEW_REQUIRED"] : manningReview ? ["MSMD_READINESS_REVIEW_REQUIRED"] : [] };
                        results[i] = results[i] with { Prediction = prediction };
                        qualifications.Add(new { ruleId = rule.Id, revision = q.Revision, q.Hash, q.SourceHash, q.PolicyVersion,
                            attestationId = attestation?.Id, facts = qualifiedFacts });
                        bindings.Add(new() { TenantId = actor.TenantId, EvaluationId = resource, RuleId = rule.Id, QualificationRevision = q.Revision, AttestationId = attestation?.Id });
                    }
                    var profileHead = await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == vessel.Id, ct);
                    if (profileHead.Revision != profileRevision) return await Fail(h, 409, "profile_context_conflict");
                    db.Entry(profileHead).Property(x => x.Revision).IsModified = true;

                    var vesselHead = await db.Vessels.SingleAsync(x => x.Id == vessel.Id, ct);
                    db.Entry(vesselHead).Property(x => x.Name).IsModified = true;
                    await db.SaveChangesAsync(ct);
                    var owningProvenance = await OperationEquipmentContext.Provenance(db, vessel.Id, evaluate.OwningInputs, ct, resource);
                    snapshot = JsonSerializer.SerializeToElement(new { adapterVersion = ApplicabilityEngine.AdapterVersion, vesselId = vessel.Id,
                        vesselVersion = vesselHead.Version, vesselContextHash = CurrentRegulatoryContext.VesselHash(vesselHead), profileRevision, asOf, facts, qualifications, owningInputs = evaluate.OwningInputs, owningProvenance }, Json);
                    snapshotJson = snapshot.GetRawText();
                    conflicts = ApplicabilityEngine.Conflicts(revisions.Select((r, i) => new RuleCandidate($"{r.Id:D}/{r.Revision}", ApplicabilityEngine.Parse(r.RuleJson!), results[i].Prediction)));
                    outcome = new EvaluationDto(resource, vessel.Id, now, asOf, ApplicabilityEngine.AdapterVersion, profileRevision, RegisterStorage.Hash(snapshotJson), snapshot,
                        ruleSetHash, results, conflicts, Preview: false, Authoritative: true, CurrentResult: conflicts.Length > 0 || results.Any(x => x.Prediction.CurrentResult == "REVIEW_REQUIRED") ? "REVIEW_REQUIRED" : "CURRENT", Simulation: false);
                    db.ApplicabilityEvaluations.Add(new() { TenantId = actor.TenantId, Id = resource, VesselId = vessel.Id, ActorId = actor.Id,
                        IncludesOwningInputs = evaluate.OwningInputs != null, IncludesOperationInputs = evaluate.OwningInputs?.OperationId != null || evaluate.OwningInputs?.Ism is { } upstreamOperation && await db.ApplicabilityEvaluations.AnyAsync(e => e.Id == upstreamOperation.EvaluationId && e.IncludesOperationInputs, ct),
                        IncludesProtectedInputs = evaluate.OwningInputs?.RoleAssessmentIds?.Length > 0 || evaluate.OwningInputs?.SourceEvidenceId is { } sourceId && await db.OperationEquipmentBindings.AnyAsync(b => b.VesselId == vessel.Id && b.Id == sourceId && b.AssignmentId != null, ct) || evaluate.OwningInputs?.Ism is { } ismInput && await db.ApplicabilityEvaluations.AnyAsync(e => e.Id == ismInput.EvaluationId && e.IncludesProtectedInputs, ct),
                        ProfileRevision = profileRevision, SavedAt = now, AsOf = asOf, AdapterVersion = ApplicabilityEngine.AdapterVersion, SnapshotJson = snapshotJson,
                        SnapshotHash = RegisterStorage.Hash(snapshotJson), RuleSetHash = ruleSetHash, ResultJson = JsonSerializer.Serialize(outcome, Json) });
                    db.EvaluationRules.AddRange(revisions.Select(r => new EvaluationRule { TenantId = actor.TenantId, EvaluationId = resource, RuleId = r.Id, RuleRevision = r.Revision }));
                    db.EvaluationQualifications.AddRange(bindings);
                    await db.SaveChangesAsync(ct);
                } else outcome = new EvaluationDto(resource, vessel.Id, now, asOf, ApplicabilityEngine.AdapterVersion, profileRevision, RegisterStorage.Hash(snapshotJson), snapshot, ruleSetHash, results, conflicts);
            } else {
                var d = id == null ? null : await db.RuleDrafts.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (id != null && d == null) return await Fail(h, 404, "resource_not_found");
                var expected = author?.ExpectedVersion ?? review!.ExpectedVersion;
                if (expected != (d?.Version ?? 0)) return await Replay() ?? await Fail(h, 409, "version_conflict");
                RuleRevision r;
                if (author != null) {
                    if (author.Contract != ApplicabilityEngine.Contract || author.ReasonCode is not ("INITIAL_AUTHORING" or "AUTHORING_CORRECTION")) return await Fail(h, 400, "invalid_author_contract");
                    if (author.Rule is { } rule && rule.ValueKind != JsonValueKind.Null) {
                        var parsed = ApplicabilityEngine.Parse(rule.GetRawText());
                        if (parsed.PublicationStatus != "DRAFT" || parsed.SourceVerificationStatus != "PENDING") return await Fail(h, 400, "source_assertion_not_admitted");
                    }
                    else if (author.Rule is { ValueKind: JsonValueKind.Null }) author = author with { Rule = null };
                    if ((author.SourceId == null) != (author.SourceRevision == null) || (author.RequirementId == null) != (author.RequirementRevision == null) || author.SourceRevision is <= 0 || author.RequirementRevision is <= 0) return await Fail(h, 400, "invalid_lineage");
                    foreach (var (kind, linkedId, revision) in new[] { ("Source", author.SourceId, author.SourceRevision), ("Requirement", author.RequirementId, author.RequirementRevision) })
                        if (linkedId != null && !await db.RegisterRevisions.AnyAsync(x => x.Kind == kind && x.Id == linkedId && x.Revision == revision, ct)) return await Fail(h, 400, "lineage_unavailable");
                    if (author.RequirementId != null && author.SourceId != null) {
                        var requirement = await db.RegisterRevisions.AsNoTracking().SingleAsync(x => x.Kind == "Requirement" && x.Id == author.RequirementId && x.Revision == author.RequirementRevision, ct);
                        if (requirement.SourceId != author.SourceId || requirement.SourceRevision != author.SourceRevision) return await Fail(h, 400, "lineage_conflict");
                    }
                    r = new() { TenantId = actor.TenantId, Id = d?.Id ?? Guid.NewGuid(), Revision = (d?.Revision ?? 0) + 1, Contract = author.Contract, RuleJson = author.Rule?.GetRawText(), Hash = "",
                        SourceKind = author.SourceId == null ? null : "Source", SourceId = author.SourceId, SourceRevision = author.SourceRevision,
                        RequirementKind = author.RequirementId == null ? null : "Requirement", RequirementId = author.RequirementId, RequirementRevision = author.RequirementRevision,
                        ActorId = actor.Id, RoleContext = actor.RoleContext!, ReasonCode = author.ReasonCode, RequestedLifecycle = author.Rule == null ? "DRAFT" : ApplicabilityEngine.Parse(author.Rule.Value.GetRawText()).Lifecycle.State, SavedAt = now };
                    if (r.RuleJson != null && ApplicabilityEngine.Parse(r.RuleJson).Version != r.Revision) return await Fail(h, 400, "rule_version_mismatch");
                    if (d != null && r.RuleJson != null) {
                        var lastDocument = await db.RuleRevisions.AsNoTracking().Where(x => x.Id == d.Id && x.RuleJson != null).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
                        if (lastDocument != null && ApplicabilityEngine.Parse(lastDocument.RuleJson!).RuleId != ApplicabilityEngine.Parse(r.RuleJson).RuleId) return await Fail(h, 400, "rule_identity_immutable");
                    }
                } else {
                    if (d == null || review!.ReasonCode != "REQUEST_REVIEW" || !ApplicabilityEngine.States.Contains(review.RequestedLifecycle)) return await Fail(h, 400, "invalid_review_request");
                    var prior = await db.RuleRevisions.AsNoTracking().SingleAsync(x => x.Id == d.Id && x.Revision == d.Revision, ct);
                    r = new() { TenantId = actor.TenantId, Id = d.Id, Revision = d.Revision + 1, Contract = prior.Contract, RuleJson = prior.RuleJson, Hash = "",
                        SourceKind = prior.SourceKind, SourceId = prior.SourceId, SourceRevision = prior.SourceRevision, RequirementKind = prior.RequirementKind, RequirementId = prior.RequirementId, RequirementRevision = prior.RequirementRevision,
                        ActorId = actor.Id, RoleContext = actor.RoleContext!, ReasonCode = review.ReasonCode, RequestedLifecycle = review.RequestedLifecycle, ReviewStatus = "BLOCKED_SOURCE_ADMISSION", SavedAt = now };
                }
                // Hash covers immutable content AND exact linked source/requirement and requested state.
                r.Hash = ApplicabilityStorage.Hash(r);
                if (d == null) { d = new() { TenantId = actor.TenantId, Id = r.Id }; db.RuleDrafts.Add(d); }
                else { db.Entry(d).Property(x => x.Version).OriginalValue = expected; db.Entry(d).Property(x => x.Revision).IsModified = true; }
                d.Revision = r.Revision; db.RuleRevisions.Add(r); await db.SaveChangesAsync(ct);
                resource = d.Id; outcome = new RuleDraftDto(d.Version, Dto(r));
            }
            var outcomeJson = JsonSerializer.Serialize(outcome, Json);
            if (action == "simulate") {
                await tx.CommitAsync(ct);
                await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ApplicabilitySimulation", "Success", resourceId: evaluate!.VesselId);
                return Results.Content(outcomeJson, "application/json");
            }
            db.ApplicabilityReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = action, Fingerprint = fingerprint, ResourceId = resource,
                RuleResourceId = action == "evaluate" ? null : resource, EvaluationResourceId = action == "evaluate" ? resource : null,
                VesselId = evaluate?.VesselId, OutcomeJson = outcomeJson, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var live = Actor(h); if (live == null || live.Id != actor.Id || live.TenantId != actor.TenantId || !(action == "author" ? Write(live) : action == "review" ? Review(live) : Evaluate(live))) return await Fail(h, 403, "permission_denied");
            if (evaluate != null && !await live.Scope(db.Vessels).AnyAsync(v => v.Id == evaluate.VesselId, ct)) return await Fail(h, 404, "resource_not_found");
            if (evaluate != null && !await OperationEquipmentContext.Authorized(db, live, evaluate.VesselId, evaluate.OwningInputs, ct)) return await Fail(h, 403, "permission_denied");
            if (evaluate != null && !live.Permissions.Contains("file.read") && await db.RegulatoryDraftRevisions.AnyAsync(x => x.VesselId == evaluate.VesselId && x.ConstructionFileId != null, ct)) return await Fail(h, 403, "permission_denied");
            if (usedAttestations.Any(e => !CurrentRegulatoryContext.AttestationCurrent(h.RequestServices.GetRequiredService<FederationSettings>(), e, h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow())))
                return await Fail(h, 409, "attestation_authority_changed");
            if (evaluate?.OwningInputs != null && (await CurrentRegulatoryContext.Stale(db, resource, evaluate.VesselId,
                h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), ct, h.RequestServices.GetRequiredService<FederationSettings>())).Length != 0)
                return await Fail(h, 409, "owning_context_changed");
            await tx.CommitAsync(ct);
            return Results.Content(outcomeJson, "application/json");
        } catch (JsonException) { await tx.RollbackAsync(CancellationToken.None); return await Fail(h, 400, "invalid_rule");
        } catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Replay() ?? await Fail(h, 409, "version_conflict");
        } catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Replay() ?? await Fail(h, 409, "version_conflict");
        } catch (Exception e) when (e is DbUpdateException or NpgsqlException) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var state = e is PostgresException p ? p.SqlState : (e.InnerException as PostgresException)?.SqlState;
            return await Replay() ?? await Fail(h, state is "40001" or "40P01" ? 409 : 503, state is "40001" or "40P01" ? "version_conflict" : "service_unavailable");
        }
    }
}
