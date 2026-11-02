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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequirementPin(Guid RequirementId, long RequirementRevision, Guid SourceId, long SourceRevision, Guid? RuleId = null, long? RuleRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphPin(string Kind, Guid Id, long Revision, uint ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvidenceFilePin(Guid Id, long Revision, string Checksum);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlEvidenceCommand([property: JsonRequired] uint ExpectedVersion, ControlEvidenceContent Content,
    RequirementPin[] Requirements, GraphPin[] Links, EvidenceFilePin? File, string ReasonCode);
public sealed record ControlEvidenceDto(Guid Id, Guid VesselId, string Kind, long Revision, uint Version, ControlEvidenceContent Content,
    string Hash, string Lifecycle, string Completion, string Verification, string Effectiveness, string TimeState, bool EvidenceExists,
    EvidenceFilePin? File, RequirementPin[] Requirements, GraphPin[] Links, string[] Blockers, DateTimeOffset SavedAt, DateTimeOffset AsOf);

public static class ControlEvidenceEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 12 };
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    // This first consumer deliberately supports vessel-owned, non-protected files only.
    // Require authority for every resource family before any row/count/receipt is exposed.
    internal static bool Read(TrustedActor a) => a.HasWriteAttribution && a.CanReadRegulatory && new[] { "control.read", "evidence.read", "file.read", "applicability.read" }.All(a.Permissions.Contains);
    private static bool Write(TrustedActor a, string kind, Guid vessel) => Read(a) && a.CanEditVessel(vessel) && a.Permissions.Contains(kind == "Control" ? "control.write" : "evidence.write");
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlEvidence", code);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
    public static void MapControlEvidence(this IEndpointRouteBuilder app)
    {
        foreach (var (route, kind) in new[] { ("controls", "Control"), ("evidence", "Evidence"), ("operational-control-library", "Control"), ("evidence-graph", "Evidence") }) {
            var g = app.MapGroup("/api/v1/" + route).RequireAuthorization("DirectoryRead");
            g.MapGet("", (HttpContext h, PoseidonDbContext db) => List(h, db, kind, null));
            g.MapGet("/{id:guid}/versions", (Guid id, HttpContext h, PoseidonDbContext db) => List(h, db, kind, id));
            g.MapGet("/owners", Owners);
            g.MapGet("/orphans", Orphans);
            g.MapPost("", (Func<HttpContext, Task<IResult>>)((HttpContext h) => Save(h, kind, null))).Accepts<ControlEvidenceCommand>("application/json").Produces<ControlEvidenceDto>();
            g.MapPut("/{id:guid}", (Guid id, HttpContext h) => Save(h, kind, id)).Accepts<ControlEvidenceCommand>("application/json").Produces<ControlEvidenceDto>();
            g.MapPost("/{id:guid}/submit", (Guid id, HttpContext h) => Save(h, kind, id, "submit"));
            g.MapPost("/{id:guid}/complete", (Guid id, HttpContext h) => Save(h, kind, id, "complete"));
        }
    }
    private static bool Query(HttpContext h, bool history, out Guid vessel, out int offset, out DateTimeOffset asOf)
    {
        vessel = Guid.Empty; offset = 0; asOf = DateTimeOffset.UtcNow;
        if (h.Request.Query.Any(x => x.Value.Count != 1 || x.Key is not ("vesselId" or "offset" or "asOf")) ||
            !Guid.TryParseExact(h.Request.Query["vesselId"], "D", out vessel) || vessel == Guid.Empty) return false;
        if (h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset is < 0 or > 100000)) return false;
        if (h.Request.Query.ContainsKey("asOf")) {
            var raw = h.Request.Query["asOf"].ToString();
            if (!history || !OffsetTimestamp(raw) || !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf) || asOf > DateTimeOffset.UtcNow) return false;
            asOf = asOf.ToUniversalTime();
        }
        return true;
    }
    private static Task<bool> Scope(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) => a.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, ct);
    private static bool OffsetTimestamp(string raw) => System.Text.RegularExpressions.Regex.IsMatch(raw,
        @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})\z");
    private static Membership[] Eligible(HttpContext h, Vessel vessel)
    {
        var settings = h.RequestServices.GetRequiredService<FederationSettings>();
        return settings.Memberships.Where(m => m.Issuer == settings.Issuer && !string.IsNullOrWhiteSpace(m.Subject) &&
            settings.Memberships.Count(x => x.Issuer == m.Issuer && x.Subject == m.Subject) == 1 &&
            m.TenantId == Actor(h).TenantId && m.ActorId != Guid.Empty && ActorResolver.TrustedRoleContext(m.RoleReferences) != null &&
            m.Permissions.Contains("control.owner.eligible") && m.Permissions.Contains("directory.read") &&
            (m.TenantWideManage || m.TenantWideRead || (m.VesselGrants ?? []).Any(g => g.VesselId == vessel.Id)) &&
            (vessel.Kind == VesselKind.Tug ? m.Permissions.Contains("mode.tug") : !m.Permissions.Contains("mode.tug") || m.Permissions.Contains("mode.cross")))
            .GroupBy(m => m.ActorId).Where(g => g.Count() == 1).Select(g => g.Single()).ToArray();
    }
    private static async Task<IResult> Owners(HttpContext h, PoseidonDbContext db) {
        var a = Actor(h); if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (!Query(h, false, out var v, out _, out _)) return await Fail(h, 400, "invalid_query");
        if (!await Scope(db, a, v, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlOwners", "Success", resourceId: v);
        return Results.Ok(new { items = Eligible(h, await db.Vessels.AsNoTracking().SingleAsync(x => x.Id == v, h.RequestAborted)).SelectMany(m => m.RoleReferences!.Select(r => new { actorId = m.ActorId, roleId = Guid.Parse(r) })).ToArray() });
    }
    private static IQueryable<ControlEvidenceRevision> Visible(PoseidonDbContext db, Guid vessel, TrustedActor actor) => OperationEquipmentCommands.Visible(db, actor, db.ControlEvidenceRevisions.AsNoTracking()).Where(r => r.VesselId == vessel &&
        (!db.TugCapabilityBindings.Any(b => b.VesselId == vessel && b.Id == r.Id && b.Revision == r.Revision && b.Kind == r.Kind) || actor.CanReadTug && actor.Permissions.Contains("equipment.read")) &&
        (actor.CanReadTug && actor.Permissions.Contains("equipment.read") || !db.ControlEvidenceEdges.Any(edge => edge.VesselId == vessel && edge.Kind == r.Kind && edge.Id == r.Id && edge.Revision == r.Revision &&
            db.TugCapabilityBindings.Any(b => b.VesselId == vessel && b.Kind == edge.TargetKind && b.Id == edge.TargetId && b.Revision == edge.TargetRevision))) &&
        (r.FileId == null || db.StoredFiles.Any(f => f.Id == r.FileId && f.Revision == r.FileRevision && f.Checksum == r.Checksum && f.VesselId == vessel &&
            db.FileStages.Any(s => s.Id == f.Id && s.VesselId == vessel && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport"))));
    private static async Task<ControlEvidenceDto> Dto(PoseidonDbContext db, ControlEvidenceRevision r, uint version, DateTimeOffset asOf, CancellationToken ct, TrustedActor actor, FederationSettings settings)
    {
        var requirements = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == r.VesselId && x.Kind == r.Kind && x.Id == r.Id && x.Revision == r.Revision).OrderBy(x => x.RequirementId).ThenBy(x => x.RequirementRevision).ToArrayAsync(ct);
        var edges = await db.ControlEvidenceEdges.AsNoTracking().Where(x => x.VesselId == r.VesselId && x.Kind == r.Kind && x.Id == r.Id && x.Revision == r.Revision).OrderBy(x => x.TargetKind).ThenBy(x => x.TargetId).ThenBy(x => x.TargetRevision).ToArrayAsync(ct);
        var content = JsonSerializer.Deserialize<ControlEvidenceContent>(r.ContentJson, Json)!;
        var superseded = await db.ControlEvidenceRevisions.AnyAsync(x => x.VesselId == r.VesselId && x.Kind == r.Kind && x.Id == r.Id && x.Revision > r.Revision && x.SavedAt <= asOf, ct) ||
            await db.ControlEvidenceEdges.AnyAsync(x => x.VesselId == r.VesselId && x.TargetKind == r.Kind && x.TargetId == r.Id && x.TargetRevision == r.Revision && x.TargetKind == "Evidence" &&
                db.ControlEvidenceRevisions.Any(s => s.VesselId == x.VesselId && s.Kind == x.Kind && s.Id == x.Id && s.Revision == x.Revision && s.FileId != null && s.SavedAt <= asOf), ct);
        var qualified = await QualifiedControlContext.Resolve(db, r.VesselId, requirements.Select(x => new RequirementPin(x.RequirementId, x.RequirementRevision, x.SourceId, x.SourceRevision, x.RuleId, x.RuleRevision)).ToArray(), r.Kind, content, r.FileId != null, asOf, ct, settings);
        var missing = ControlEvidencePolicy.Blockers(r.Kind, content, requirements.Length, r.FileId != null, requirements.Count(p => p.RuleId != null))
            .Where(x => x is not ("SOURCE_ADMISSION_UNAVAILABLE" or "VERIFICATION_NOT_PERFORMED" or "CLEAN_FILE_MISSING")).Concat(qualified.Blockers).Distinct().ToArray();
        var visibleEvidence = Visible(db, r.VesselId, actor);
        var verification = await db.HumanReviewEvents.AsNoTracking().Where(e => e.VesselId == r.VesselId && e.Kind == "Verification" && e.SavedAt <= asOf &&
            (r.Kind == "Evidence" ? e.EvidenceId == r.Id && e.EvidenceRevision == r.Revision : e.ControlId == r.Id && e.ControlRevision == r.Revision) &&
            visibleEvidence.Any(visible => visible.Kind == "Evidence" && visible.Id == e.EvidenceId && visible.Revision == e.EvidenceRevision) &&
            !db.HumanReviewEvents.Any(later => later.VesselId == e.VesselId && later.Id == e.Id && later.Revision > e.Revision && later.SavedAt <= asOf))
            .OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).FirstOrDefaultAsync(ct);
        var verificationState = verification?.State == "VERIFIED" ? verification.ValidUntil > asOf && !superseded && qualified.Blockers.Length == 0 ? "VERIFIED" : "REVIEW_REQUIRED" : "UNVERIFIED";
        if (OperationEquipmentInputs.Present(content) && verificationState == "VERIFIED" &&
            (!OperationEquipmentContext.CurrentVerifier(settings, verification!, OperationEquipmentInputs.Kind(content), asOf) ||
                !await OperationEquipmentCommands.VerificationAllowed(db, settings, content, requirements.Select(x => new RequirementPin(x.RequirementId, x.RequirementRevision, x.SourceId, x.SourceRevision, x.RuleId, x.RuleRevision)).ToArray(), verification!.RoleId, asOf, ct))) verificationState = "REVIEW_REQUIRED";
        return new(r.Id, r.VesselId, r.Kind, r.Revision, version, content, r.Hash, r.Lifecycle, r.Lifecycle == "COMPLETED" ? "COMPLETED" : "UNKNOWN", verificationState, "UNKNOWN",
            ControlEvidencePolicy.TimeState(content, asOf, superseded), r.FileId != null,
            r.FileId == null ? null : new(r.FileId.Value, r.FileRevision!.Value, r.Checksum!),
            requirements.Select(x => new RequirementPin(x.RequirementId, x.RequirementRevision, x.SourceId, x.SourceRevision, x.RuleId, x.RuleRevision)).ToArray(),
            edges.Select(x => new GraphPin(x.TargetKind, x.TargetId, x.TargetRevision, x.TargetVersion)).ToArray(),
            missing, r.SavedAt, asOf);
    }
    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db, string kind, Guid? id)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (!Query(h, true, out var vessel, out var offset, out var asOf)) return await Fail(h, 400, "invalid_query");
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        var q = Visible(db, vessel, a).Where(r => r.Kind == kind && r.SavedAt <= asOf);
        if (id != null) q = q.Where(r => r.Id == id);
        else q = q.Where(r => !db.ControlEvidenceRevisions.Any(n => n.VesselId == vessel && n.Kind == kind && n.Id == r.Id && n.Revision > r.Revision && n.SavedAt <= asOf));
        if (id != null && !await q.AnyAsync(ct)) return await Fail(h, 404, "resource_not_found");
        var rows = await q.OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id).ThenByDescending(r => r.Revision).Skip(offset).Take(26).ToArrayAsync(ct);
        var items = new List<ControlEvidenceDto>();
        foreach (var r in rows.Take(25)) {
            var head = await db.ControlEvidenceHeads.AsNoTracking().SingleAsync(x => x.VesselId == vessel && x.Kind == kind && x.Id == r.Id, ct);
            items.Add(await Dto(db, r, head.Version, asOf, ct, a, h.RequestServices.GetRequiredService<FederationSettings>()));
        }
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlEvidenceRead", "Success", resourceId: vessel);
        return Results.Ok(new { items, offset, hasMore = rows.Length > 25, canWrite = Write(a, kind, vessel) });
    }
    private static async Task<IResult> Orphans(HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (!Query(h, false, out var vessel, out var offset, out _)) return await Fail(h, 400, "invalid_query");
        if (!await Scope(db, a, vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        // Only the honest draft orphan report is available. It cannot claim complete
        // critical obligations while authoritative applicability admission is absent.
        var q = Visible(db, vessel, a).Where(r => r.Kind == "Control" && r.CriticalDeclaration == true && db.ControlEvidenceHeads.Any(d => d.VesselId == vessel && d.Kind == "Control" && d.Id == r.Id && d.Revision == r.Revision) &&
            !db.ControlEvidenceRequirements.Any(l => l.VesselId == vessel && l.Kind == "Control" && l.Id == r.Id && l.Revision == r.Revision));
        var ids = await q.OrderBy(x => x.Id).Skip(offset).Take(26).Select(x => x.Id).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlOrphans", "Success", resourceId: vessel);
        var evaluation = await OperationEquipmentCommands.Evaluations(db, a).Where(e => e.VesselId == vessel && db.EvaluationQualifications.Any(q => q.EvaluationId == e.Id))
            .OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).FirstOrDefaultAsync(h.RequestAborted);
        var causes = evaluation == null ? new[] { "AUTHORITATIVE_APPLICABILITY_UNAVAILABLE" } : await CurrentRegulatoryContext.Stale(db, evaluation.Id, vessel, DateTimeOffset.UtcNow, h.RequestAborted, h.RequestServices.GetRequiredService<FederationSettings>());
        var result = evaluation == null ? null : JsonSerializer.Deserialize<EvaluationDto>(evaluation.ResultJson, Json);
        var eligible = await db.RegulatoryQualificationEvents.AsNoTracking().Where(q => q.State == "IN_FORCE" && db.RegulatoryQualificationHeads.Any(head => head.RuleId == q.RuleId && head.Revision == q.Revision) &&
            db.RuleDrafts.Any(rule => rule.Id == q.RuleId && rule.Revision == q.RuleRevision)).Select(q => q.RuleId).ToArrayAsync(h.RequestAborted);
        var available = causes.Length == 0 && eligible.Length > 0 && result != null && result.Authoritative && result.Conflicts.Length == 0 &&
            eligible.All(id => result.Results.Any(r => r.RuleId == id && r.Prediction.Authoritative && r.Prediction.Result is "APPLICABLE" or "NOT_APPLICABLE"));
        var missing = new List<object>();
        if (available) foreach (var obligation in result!.Results.Where(r => r.Prediction.Result == "APPLICABLE" && eligible.Contains(r.RuleId))) {
            if (!await db.ControlEvidenceRequirements.AnyAsync(p => p.VesselId == vessel && p.Kind == "Control" && p.RuleId == obligation.RuleId && p.RuleRevision == obligation.Revision &&
                db.ControlEvidenceHeads.Any(head => head.VesselId == vessel && head.Kind == p.Kind && head.Id == p.Id && head.Revision == p.Revision), h.RequestAborted))
                missing.Add(new { obligation.RuleId, ruleRevision = obligation.Revision, obligation.RequirementId, obligation.RequirementRevision });
        }
        return Results.Ok(new { available, reason = available ? "QUALIFIED_CATALOG_SCOPE_ONLY" : causes.FirstOrDefault() ?? "CURRENT_OBLIGATION_INPUT_INCOMPLETE",
            draftCriticalOrphans = ids.Take(25), uncontrolledObligations = missing.Skip(offset).Take(25), offset, hasMore = ids.Length > 25 || missing.Count > offset + 25 });
    }
    private static async Task<IResult> Save(HttpContext h, string kind, Guid? id, string action = "save")
    {
        var a = Actor(h); var db = h.RequestServices.GetRequiredService<PoseidonDbContext>(); var ct = h.RequestAborted;
        if (!Query(h, false, out var vessel, out _, out _)) return await Fail(h, 400, "invalid_query");
        if (!Write(a, kind, vessel)) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(h, 400, "invalid_idempotency_key");
        ControlEvidenceCommand command; string body;
        try {
            if (!h.Request.HasJsonContentType()) return await Fail(h, 400, "invalid_request");
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 32768, ct);
            using var doc = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 12 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(doc.RootElement); body = doc.RootElement.GetRawText();
            if (doc.RootElement.TryGetProperty("content", out var tc) && tc.ValueKind == JsonValueKind.Object && tc.TryGetProperty("tugCapability", out var ts) && ts.ValueKind == JsonValueKind.Object)
                foreach (var field in new[] { "testAt", "reviewAt" })
                    if (ts.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null &&
                        (value.ValueKind != JsonValueKind.String || !OffsetTimestamp(value.GetString()!))) throw new JsonException();
            if (doc.RootElement.TryGetProperty("content", out var rawContent) && rawContent.ValueKind == JsonValueKind.Object)
                foreach (var field in new[] { "observedAt", "validFrom", "validUntil", "freshUntil" })
                    if (rawContent.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null &&
                        (value.ValueKind != JsonValueKind.String || !OffsetTimestamp(value.GetString()!))) throw new JsonException();
            command = JsonSerializer.Deserialize<ControlEvidenceCommand>(body, Json) ?? throw new JsonException();
            if (command.Content == null || command.Requirements == null || command.Links == null || command.Requirements.Length > 32 || command.Links.Length > 32 || command.Requirements.Any(x => x == null) || command.Links.Any(x => x == null)) throw new JsonException();
            ControlEvidencePolicy.Validate(kind, command.Content, DateTimeOffset.UtcNow);
            if (command.ReasonCode != (action == "submit" ? "SUBMIT" : action == "complete" ? "COMPLETE" : id == null ? "INITIAL_AUTHORING" : "AUTHORING_CORRECTION") || kind == "Control" && (command.File != null || command.Links.Length != 0)) throw new JsonException();
        } catch (Exception e) when (e is JsonException or ArgumentException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        if (command.Content.CrewRoleAssessment != null && !ComplianceInputContext.CrewRead(a)) return await Fail(h, 403, "permission_denied");
        if (id != null && !(a.CanReadTug && a.Permissions.Contains("equipment.read")) && await db.TugCapabilityBindings.AnyAsync(b => b.VesselId == vessel && b.Id == id, ct)) return await Fail(h, 403, "permission_denied");
        if (OperationEquipmentInputs.Present(command.Content) && (!OperationEquipmentCommands.Read(a) ||
            (command.Content.Operation != null || command.Content.ServiceAuthorization != null) && !a.CanReadTug)) return await Fail(h, 403, "permission_denied");
        if (id != null && !await Visible(db, vessel, a).AnyAsync(r => r.Id == id && r.Kind == kind, ct)) return await Fail(h, 404, "resource_not_found");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { kind, id, vessel, action, body }));
        if (command.Content.TugCapability != null && !(a.CanReadTug && a.Permissions.Contains("equipment.read"))) return await Fail(h, 403, "permission_denied");
        if (command.Content.TugCapability is { } capability && !await TugCapabilityCommands.Check(db, a, vessel, id, capability, false, ct))
            return await Fail(h, 409, "tug_capability_context_unavailable");
        // All command relationships reauthorize before replay. A retained receipt never
        // restores a revoked role, private object, file owner or linked vessel grant.
        if (command.Content.OwnerId != null && !Eligible(h, await db.Vessels.AsNoTracking().SingleAsync(x => x.Id == vessel, ct)).Any(m => m.ActorId == command.Content.OwnerId && m.RoleReferences!.Contains(command.Content.OwnerRole!.Value.ToString("D"), StringComparer.OrdinalIgnoreCase))) return await Fail(h, 400, "owner_unavailable");
        foreach (var p in command.Requirements) {
            if (p.RequirementRevision <= 0 || p.SourceRevision <= 0 || (p.RuleId == null) != (p.RuleRevision == null) || p.RuleRevision is <= 0) return await Fail(h, 400, "invalid_lineage");
            var req = await db.RegisterRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Kind == "Requirement" && r.Id == p.RequirementId && r.Revision == p.RequirementRevision, ct);
            if (req == null || req.SourceId != p.SourceId || req.SourceRevision != p.SourceRevision) return await Fail(h, 400, "lineage_unavailable");
            if (p.RuleId != null && !await db.RuleRevisions.AnyAsync(r => r.Id == p.RuleId && r.Revision == p.RuleRevision && r.RequirementId == p.RequirementId && r.RequirementRevision == p.RequirementRevision && r.SourceId == p.SourceId && r.SourceRevision == p.SourceRevision, ct)) return await Fail(h, 400, "lineage_unavailable");
        }
        if (command.Requirements.Select(p => (p.RequirementId, p.RequirementRevision)).Distinct().Count() != command.Requirements.Length || command.Links.Select(p => (p.Kind, p.Id, p.Revision)).Distinct().Count() != command.Links.Length || command.Links.Count(p => p.Kind == "Evidence") > 1) return await Fail(h, 400, "duplicate_lineage");
        foreach (var p in command.Links)
            if (p.Kind is not ("Control" or "Evidence") || p.Id == id || p.Revision <= 0 || p.ExpectedVersion == 0 || !await Visible(db, vessel, a).AnyAsync(r => r.Kind == p.Kind && r.Id == p.Id && r.Revision == p.Revision, ct)) return await Fail(h, 400, "linked_resource_unavailable");
        StoredFile? file = null;
        if (command.File is { } f) {
            file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == f.Id && x.Revision == f.Revision && x.Checksum == f.Checksum && x.VesselId == vessel &&
                db.FileStages.Any(s => s.Id == x.Id && s.VesselId == vessel && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport"), ct);
            if (file == null) return await Fail(h, 400, "clean_file_unavailable");
            if (!h.RequestServices.GetRequiredService<SecureFileSettings>().Ready(h.RequestServices.GetRequiredService<IWebHostEnvironment>())) return await Fail(h, 503, "file_settings_unavailable");
            try {
                await using var bytes = await h.RequestServices.GetRequiredService<FileObjects>().OpenAsync(file, ct);
                if (bytes.Length != file.Length || bytes.Length > h.RequestServices.GetRequiredService<SecureFileSettings>().MaxBytes || Convert.ToHexString(await SHA256.HashDataAsync(bytes, ct)) != file.Checksum) return await Fail(h, 503, "file_integrity_unavailable");
            } catch (Exception e) when (e is IOException or InvalidDataException or HttpRequestException or CryptographicException) { return await Fail(h, 503, "file_integrity_unavailable"); }
        }
        if (ComplianceInputs.Present(command.Content) && !await OperationEquipmentCommands.Check(db, a, vessel, id, command.Content, ct)) return await Fail(h, 409, "owning_context_changed");
        async Task<IResult?> Replay() {
            var live = Actor(h); if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !Write(live, kind, vessel) || !await Scope(db, live, vessel, ct)) return await Fail(h, 403, "permission_denied");
            if (ComplianceInputs.Present(command.Content) && !await OperationEquipmentCommands.Check(db, live, vessel, id, command.Content, ct)) return await Fail(h, 409, "owning_context_changed");
            var receipt = await db.ControlEvidenceReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt == null) return null;
            if (receipt.Fingerprint != fingerprint || receipt.VesselId != vessel || receipt.Kind != kind) return await Fail(h, 409, "idempotency_key_reused");
            if (!await Visible(db, vessel, a).AnyAsync(r => r.Kind == kind && r.Id == receipt.ResourceId, ct)) return await Fail(h, 404, "resource_not_found");
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlEvidenceReplay", "Success", resourceId: receipt.ResourceId);
            return Results.Content(receipt.OutcomeJson, "application/json");
        }
        var replay = await Replay(); if (replay != null) return replay;
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlEvidenceCommand", "Attempted", resourceId: id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            if (!await OperationEquipmentCommands.Check(db, a, vessel, id, command.Content, ct)) return await Fail(h, 409, "owning_context_changed");
            if (command.Content.TugCapability is { } currentCapability && !await TugCapabilityCommands.Check(db, a, vessel, id, currentCapability, true, ct))
                return await Fail(h, 409, "tug_capability_context_unavailable");
            var head = id == null ? null : await db.ControlEvidenceHeads.SingleOrDefaultAsync(x => x.Kind == kind && x.VesselId == vessel && x.Id == id, ct);
            if (id != null && head == null) return await Fail(h, 404, "resource_not_found");
            if (command.ExpectedVersion != (head?.Version ?? 0)) return await Fail(h, 409, "version_conflict");
            foreach (var p in command.Links)
                if (!await db.ControlEvidenceHeads.AnyAsync(x => x.Kind == p.Kind && x.Id == p.Id && x.VesselId == vessel && x.Version == p.ExpectedVersion, ct)) return await Fail(h, 409, "linked_version_conflict");
            if (head != null) {
                var prior = await db.ControlEvidenceRevisions.AsNoTracking().SingleAsync(x => x.Kind == kind && x.Id == head.Id && x.VesselId == vessel && x.Revision == head.Revision, ct);
                if (action == "complete" && prior.ContentJson != JsonSerializer.Serialize(command.Content, Json)) return await Fail(h, 409, "submitted_content_changed");
                if (prior.FileId != null && (prior.FileId != command.File?.Id || prior.FileRevision != command.File?.Revision || prior.Checksum != command.File?.Checksum)) return await Fail(h, 409, "immutable_file_reference");
            }
            if (head == null) { head = new() { TenantId = a.TenantId, VesselId = vessel, Kind = kind, Id = Guid.NewGuid() }; db.ControlEvidenceHeads.Add(head); }
            else { db.Entry(head).Property(x => x.Version).OriginalValue = command.ExpectedVersion; db.Entry(head).Property(x => x.Revision).IsModified = true; }
            head.Revision++;
            var now = DateTimeOffset.UtcNow;
            if (action != "save") {
                if (!await OperationEquipmentCommands.SubmissionAllowed(db, h.RequestServices.GetRequiredService<FederationSettings>(), a, command.Content, command.Requirements, ct)) return await Fail(h, 403, "owning_document_author_required");
                var basic = ControlEvidencePolicy.Blockers(kind, command.Content, command.Requirements.Length, file != null, command.Requirements.Count(p => p.RuleId != null))
                    .Where(x => x is not ("SOURCE_ADMISSION_UNAVAILABLE" or "VERIFICATION_NOT_PERFORMED" or "CLEAN_FILE_MISSING")).ToArray();
                var qualified = await QualifiedControlContext.Resolve(db, vessel, command.Requirements, kind, command.Content, file != null, now, ct, h.RequestServices.GetRequiredService<FederationSettings>());
                if (basic.Length > 0 || qualified.Blockers.Length > 0) return Results.Problem(statusCode: 409, title: "submission_blocked",
                    extensions: new Dictionary<string, object?> { ["code"] = "submission_blocked", ["blockers"] = basic.Concat(qualified.Blockers).Distinct().ToArray(), ["mechanisms"] = kind == "Evidence" ? qualified.Policies.Where(p => p.RequiredVerificationMechanism != null).Select(p => new { p.RequiredVerificationMechanism, p.VerificationMechanismSourceLocator }).ToArray() : [] });
                if (command.Content.Acknowledgments?.Length > 0 && !h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Any(m => m.ActorId == a.Id && m.TenantId == a.TenantId && m.ActorKind == "Human")) return await Fail(h, 403, "human_acknowledgment_required");
                if (action == "complete" && !await db.ControlEvidenceRevisions.AnyAsync(x => x.Kind == kind && x.VesselId == vessel && x.Id == id && x.Revision == head.Revision - 1 && x.Lifecycle == "SUBMITTED", ct)) return await Fail(h, 409, "submission_required");
                if (kind == "Evidence" && qualified.Policies.Any(p => p.RequireAcknowledgment == true && (p.AcknowledgmentRole == null || a.RoleContext?.Split(',').Contains(p.AcknowledgmentRole.Value.ToString("D")) != true))) return await Fail(h, 403, "acknowledgment_role_required");
                await QualifiedControlContext.GuardInputs(db, vessel, command.Requirements, ct);
            }
            var revision = new ControlEvidenceRevision { TenantId = a.TenantId, VesselId = vessel, Kind = kind, Id = head.Id, Revision = head.Revision,
                ContentJson = JsonSerializer.Serialize(command.Content, Json), Hash = RegisterStorage.Hash(JsonSerializer.Serialize(new { command.Content, command.Requirements, command.Links, command.File, action }, Json)),
                CriticalDeclaration = command.Content.Critical, FileId = file?.Id, FileRevision = file?.Revision, Checksum = file?.Checksum, ActorId = a.Id, RoleContext = a.RoleContext, ReasonCode = command.ReasonCode, Lifecycle = action == "submit" ? "SUBMITTED" : action == "complete" ? "COMPLETED" : "DRAFT", SavedAt = now };
            db.ControlEvidenceRevisions.Add(revision);
            foreach (var role in command.Content.Msmd?.Roles ?? []) db.MsmdRoleBindings.Add(new() { TenantId = a.TenantId, VesselId = vessel, Id = head.Id, Revision = head.Revision, PositionId = role.PositionId, Count = role.Count, SourceLocator = role.SourceLocator });
            if (OperationEquipmentInputs.Present(command.Content)) db.OperationEquipmentBindings.Add(OperationEquipmentBinding.From(a.TenantId, vessel, head.Id, head.Revision, command.Content));
            if (command.Content.TugCapability is { } binding) db.TugCapabilityBindings.Add(new() {
                TenantId = a.TenantId, VesselId = vessel, Id = head.Id, Revision = head.Revision,
                AssetId = binding.AssetId, AssetRevision = binding.AssetRevision, ProfileVersion = binding.ProfileVersion,
                ConditionId = binding.ConditionEvidenceId, ConditionRevision = binding.ConditionEvidenceRevision, ConditionVersion = binding.ConditionEvidenceVersion });
            foreach (var p in command.Requirements) db.ControlEvidenceRequirements.Add(new() { TenantId = a.TenantId, VesselId = vessel, Kind = kind, Id = head.Id, Revision = head.Revision,
                RequirementId = p.RequirementId, RequirementRevision = p.RequirementRevision, SourceId = p.SourceId, SourceRevision = p.SourceRevision, RuleId = p.RuleId, RuleRevision = p.RuleRevision });
            foreach (var p in command.Links) db.ControlEvidenceEdges.Add(new() { TenantId = a.TenantId, VesselId = vessel, Kind = kind, Id = head.Id, Revision = head.Revision, TargetKind = p.Kind, TargetId = p.Id, TargetRevision = p.Revision, TargetVersion = p.ExpectedVersion });
            await db.SaveChangesAsync(ct);
            var outcome = JsonSerializer.Serialize(await Dto(db, revision, head.Version, now, ct, a, h.RequestServices.GetRequiredService<FederationSettings>()), Json);
            db.ControlEvidenceReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, Kind = kind, ResourceId = head.Id, ActorId = a.Id, Key = key, Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var live = Actor(h); if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !Write(live, kind, vessel) || !await Scope(db, live, vessel, ct)) return await Fail(h, 403, "permission_denied");
            if (!await OperationEquipmentCommands.Check(db, live, vessel, id, command.Content, ct)) return await Fail(h, 409, "owning_context_changed");
            if (action != "save" && !await OperationEquipmentCommands.SubmissionAllowed(db, h.RequestServices.GetRequiredService<FederationSettings>(), live, command.Content, command.Requirements, ct)) return await Fail(h, 403, "owning_document_author_required");
            await tx.CommitAsync(ct);
            return Results.Content(outcome, "application/json");
        } catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Replay() ?? await Fail(h, 409, "version_conflict");
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            DatabaseFailureObservation.Record(h, e);
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var code = PostgresFailure.Provider(e)?.SqlState;
            return await Replay() ?? await Fail(h, code is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure ? 409 : 503,
                code is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure ? "version_conflict" : "service_unavailable");
        }
    }
}
