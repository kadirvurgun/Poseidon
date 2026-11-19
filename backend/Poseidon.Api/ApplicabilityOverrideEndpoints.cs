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
public sealed record ApplicabilityOverrideCommand([property: JsonRequired] Guid EvaluationId, string InputHash, string RuleSetHash,
    [property: JsonRequired] Guid RuleId, [property: JsonRequired] long RuleRevision, string Action, string ReasonCode, string Purpose,
    string Rationale, Guid[] EvidenceIds, [property: JsonRequired] DateTimeOffset ValidFrom, [property: JsonRequired] DateTimeOffset ExpiresAt,
    [property: JsonRequired] DateTimeOffset ReviewAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApplicabilityOverrideDecisionCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] long ExpectedSequence,
    string Decision, string Rationale);
public sealed record ApplicabilityOverrideEventDto(long Sequence, string? FromState, string ToState, string EventCode, Guid ActorId,
    string? Rationale, DateTimeOffset SavedAt);
// BaseResult is the unchanged recorded evaluation result; an override never rewrites it (60.03 paragraph:155).
public sealed record ApplicabilityOverrideDto(Guid Id, Guid VesselId, Guid EvaluationId, Guid RuleId, long RuleRevision,
    Guid? RequirementId, long? RequirementRevision, Guid? SourceId, long? SourceRevision, string BaseResult, string Purpose,
    string Action, string ReasonCode, string? Rationale, Guid[] EvidenceIds, DateTimeOffset ValidFrom, DateTimeOffset ExpiresAt,
    DateTimeOffset ReviewAt, Guid AuthorId, Guid? ApproverId, string State, string CurrentValidity, long Sequence, uint Version,
    DateTimeOffset CreatedAt, ApplicabilityOverrideEventDto[] Events);
// Authority is UNCONFIGURED while no published OVERRIDE_AUTHORITY policy and AuthorityGrant store exist:
// proposals may be recorded and withdrawn, but no one can activate an override (60.03 paragraph:155, 580).
public sealed record ApplicabilityOverrideListDto(Guid VesselId, string Authority, string[] AuthorityBlockers, bool CanPropose,
    bool CanDecide, bool CanRevoke, Guid? CurrentEvaluationId, ApplicabilityOverrideDto[] Items, int Offset, bool HasMore,
    string Source = "18.01/Backlog/row:41;60.03/paragraph:154-166,575-580");
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlCandidateCommand([property: JsonRequired] Guid EvaluationId, string InputHash, string RuleSetHash);
public sealed record ControlCandidateItem(Guid ControlId, long Revision, uint Version, Guid RuleId, long RuleRevision, Guid RequirementId,
    long RequirementRevision, Guid SourceId, long SourceRevision, string Title, string Lifecycle, string ControlState, string[] Blockers);
public sealed record ControlCandidateOutcome(Guid VesselId, Guid EvaluationId, ControlCandidateItem[] Created, int AlreadyLinked, string[] Blockers);

// DEV-040 manual override (60.03 §10) and DEV-041 control candidate generation. Both are critical, audited,
// CSRF + Idempotency-Key + SERIALIZABLE commands on the S1 audited SaveChanges path.
public static class ApplicabilityOverrideEndpoints
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 12 };
    private static readonly System.Text.RegularExpressions.Regex Hash = new("\\A[A-F0-9]{64}\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Read(TrustedActor a) => ControlEvidenceEndpoints.Read(a);
    private static bool Author(TrustedActor a, Guid vessel) => Read(a) && a.CanEditVessel(vessel) && a.Permissions.Contains(ApplicabilityOverridePolicy.AuthorPermission);
    private static bool Approver(TrustedActor a, Guid vessel) => Read(a) && a.CanEditVessel(vessel) && a.Permissions.Contains(ApplicabilityOverridePolicy.ApprovePermission);
    private static bool Revoker(TrustedActor a, Guid vessel) => Read(a) && a.CanEditVessel(vessel) && a.Permissions.Contains(ApplicabilityOverridePolicy.RevokePermission);
    private static bool ControlWriter(TrustedActor a, Guid vessel) => Read(a) && a.CanEditVessel(vessel) && a.Permissions.Contains("control.write");
    // AI, service and automation identities never author or decide an override (60.03 paragraph:160).
    private static bool Human(HttpContext h, TrustedActor a) => h.RequestServices.GetRequiredService<FederationSettings>().Memberships
        .Any(m => m.ActorId == a.Id && m.TenantId == a.TenantId && m.ActorKind == "Human");
    // 60.03 paragraph:576/580: a grant is resolved only from a published OVERRIDE_AUTHORITY policy plus a
    // current AuthorityGrant for this person/scope. No such controlled store is implemented or seeded, so
    // there is no grant: activation stays closed (REG-034 approve -> 422). Never derived from a role label,
    // tenant admin, configuration flag, AI output or the request body.
    internal static OverrideAuthorityGrant? AuthorityGrant(TrustedActor actor, Guid vessel, Guid rule, string action) => null;

    public static void MapApplicabilityOverrides(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/vessels/{id:guid}").RequireAuthorization("DirectoryRead");
        group.MapGet("/applicability-overrides", List).WithName("ApplicabilityOverrides").Produces<ApplicabilityOverrideListDto>();
        group.MapPost("/applicability-overrides", Propose).WithName("ApplicabilityOverridePropose")
            .Accepts<ApplicabilityOverrideCommand>("application/json").Produces<ApplicabilityOverrideDto>();
        group.MapPost("/applicability-overrides/{overrideId:guid}/decisions", Decide).WithName("ApplicabilityOverrideDecision")
            .Accepts<ApplicabilityOverrideDecisionCommand>("application/json").Produces<ApplicabilityOverrideDto>();
        group.MapPost("/compliance-control-plan/candidates", Candidates).WithName("ComplianceControlCandidates")
            .Accepts<ControlCandidateCommand>("application/json").Produces<ControlCandidateOutcome>();
    }

    private static async Task<IResult> Fail(HttpContext h, int status, string code, string action = "ApplicabilityOverrideCommand") {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, code);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = h.TraceIdentifier });
    }
    private static int Status(string code) => code switch {
        "permission_denied" or "human_actor_required" or "override_author_required" => 403,
        "override_authority_unavailable" or "override_reason_not_allowed" or "override_evidence_required" or "override_unknown_not_acceptable" or "override_validity_invalid" => 422,
        "invalid_request" => 400,
        _ => 409
    };
    private static async Task<(string? Body, string? Key, IResult? Failure)> Envelope(HttpContext h, int limit, string action) {
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return (null, null, await Fail(h, 403, "csrf_invalid", action)); }
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return (null, null, await Fail(h, 400, "invalid_idempotency_key", action));
        try {
            if (!h.Request.HasJsonContentType()) return (null, null, await Fail(h, 400, "invalid_request", action));
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, limit, h.RequestAborted);
            using var doc = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 4 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(doc.RootElement);
            return (doc.RootElement.GetRawText(), key, null);
        } catch (Exception e) when (e is JsonException or ArgumentException or InvalidDataException) { return (null, null, await Fail(h, 400, "invalid_request", action)); }
    }
    private static bool Bound(ApplicabilityOverride o, ComplianceApplicabilityDto review) =>
        review.Available && review.EvaluationId == o.EvaluationId && review.InputHash == o.InputHash && review.RuleSetHash == o.RuleSetHash &&
        review.Results.Any(r => r.RuleId == o.RuleId && r.Revision == o.RuleRevision && r.Prediction.Result == o.BaseResult);
    private static async Task<ApplicabilityOverrideDto> Dto(PoseidonDbContext db, ApplicabilityOverride o, ComplianceApplicabilityDto review,
        bool privileged, DateTimeOffset now, CancellationToken ct) {
        var events = await db.ApplicabilityOverrideEvents.AsNoTracking().Where(e => e.VesselId == o.VesselId && e.OverrideId == o.Id)
            .OrderByDescending(e => e.Sequence).Take(20).ToArrayAsync(ct);
        // Validity binds to the current qualified review: a re-evaluation, changed input or rule revision -> REQUIRES_REVIEW.
        var bound = Bound(o, review) && review.Current;
        return new(o.Id, o.VesselId, o.EvaluationId, o.RuleId, o.RuleRevision, o.RequirementId, o.RequirementRevision, o.SourceId, o.SourceRevision,
            o.BaseResult, o.Purpose, o.Action, o.ReasonCode, privileged ? o.Rationale : null, JsonSerializer.Deserialize<Guid[]>(o.EvidenceIdsJson, Json) ?? [],
            o.ValidFrom, o.ExpiresAt, o.ReviewAt, o.AuthorId, o.ApproverId, o.State,
            ApplicabilityOverridePolicy.CurrentValidity(o.State, o.ValidFrom, o.ExpiresAt, o.ReviewAt, bound, now), o.Sequence, o.Version, o.CreatedAt,
            events.Select(e => new ApplicabilityOverrideEventDto(e.Sequence, e.FromState, e.ToState, e.EventCode, e.ActorId, privileged ? e.Rationale : null, e.SavedAt)).ToArray());
    }

    private static async Task<IResult> List(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Fail(h, 403, "permission_denied", "ApplicabilityOverrideRead");
        var offset = 0;
        if (h.Request.Query.Any(q => q.Key != "offset" || q.Value.Count != 1) ||
            h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset is < 0 or > 100000))
            return await Fail(h, 400, "invalid_query", "ApplicabilityOverrideRead");
        if (!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == id, ct)) return await Fail(h, 404, "resource_not_found", "ApplicabilityOverrideRead");
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var review = await ComplianceWorkspaceEndpoints.Review(id, a, h, db, now);
        var rows = await db.ApplicabilityOverrides.AsNoTracking().Where(o => o.VesselId == id)
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).Skip(offset).Take(26).ToArrayAsync(ct);
        var items = new List<ApplicabilityOverrideDto>();
        foreach (var row in rows.Take(25))
            items.Add(await Dto(db, row, review, row.AuthorId == a.Id || Approver(a, id) || Revoker(a, id), now, ct));
        await tx.CommitAsync(ct);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ApplicabilityOverrideRead", "Success", resourceId: id);
        return Results.Ok(new ApplicabilityOverrideListDto(id, "UNCONFIGURED", ["OVERRIDE_AUTHORITY_POLICY_UNAVAILABLE"],
            Author(a, id) && Human(h, a), Approver(a, id) && Human(h, a), Revoker(a, id) && Human(h, a), review.Available ? review.EvaluationId : null,
            items.ToArray(), offset, rows.Length > 25));
    }

    private static async Task<IResult> Propose(Guid id, HttpContext h) {
        var a = Actor(h); var db = h.RequestServices.GetRequiredService<PoseidonDbContext>(); var ct = h.RequestAborted;
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_query");
        if (!Author(a, id)) return await Fail(h, 403, "permission_denied");
        if (!Human(h, a)) return await Fail(h, 403, "human_actor_required");
        if (!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == id, ct)) return await Fail(h, 404, "resource_not_found");
        var (body, key, failure) = await Envelope(h, 16384, "ApplicabilityOverrideCommand"); if (failure != null) return failure;
        ApplicabilityOverrideCommand command;
        try {
            command = JsonSerializer.Deserialize<ApplicabilityOverrideCommand>(body!, Json) ?? throw new JsonException();
            if (command.EvaluationId == Guid.Empty || command.RuleId == Guid.Empty || command.RuleRevision is <= 0 or > 9007199254740991 ||
                command.InputHash == null || !Hash.IsMatch(command.InputHash) || command.RuleSetHash == null || !Hash.IsMatch(command.RuleSetHash) ||
                command.Action == null || command.ReasonCode == null || command.Purpose == null || command.Rationale == null || command.EvidenceIds == null) throw new JsonException();
        } catch (JsonException) { return await Fail(h, 400, "invalid_request"); }
        foreach (var evidence in command.EvidenceIds.Distinct())
            if (!await ControlEvidenceEndpoints.CurrentVisible(db, id, a).AnyAsync(r => r.Kind == "Evidence" && r.Id == evidence, ct)) return await Fail(h, 400, "override_evidence_unavailable");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { kind = "ApplicabilityOverridePropose", id, body }));
        var replay = await Replay(h, db, a, key!, fingerprint, id, "ApplicabilityOverridePropose", live => Author(live, id) && Human(h, live), ct); if (replay != null) return replay;
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ApplicabilityOverrideCommand", "Attempted", resourceId: id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var inner = await Replay(h, db, a, key!, fingerprint, id, "ApplicabilityOverridePropose", live => Author(live, id) && Human(h, live), ct); if (inner != null) return inner;
            var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var review = await ComplianceWorkspaceEndpoints.Review(id, a, h, db, now);
            // Exact pinned evaluation/input/rule-set: a proposal against another evaluation is stale (409).
            if (!review.Available || review.EvaluationId != command.EvaluationId || review.InputHash != command.InputHash || review.RuleSetHash != command.RuleSetHash)
                return await Fail(h, 409, "evaluation_stale");
            var result = review.Results.SingleOrDefault(r => r.RuleId == command.RuleId && r.Revision == command.RuleRevision);
            if (result == null) return await Fail(h, 409, "override_rule_unavailable");
            // Only a current qualified applicability can be reviewed for an override; changed inputs need re-evaluation first.
            if (!review.Current) return await Fail(h, 409, "evaluation_stale");
            var proposal = new OverrideProposal(command.Action, command.ReasonCode, command.Purpose, command.Rationale, command.EvidenceIds,
                command.ValidFrom.ToUniversalTime(), command.ExpiresAt.ToUniversalTime(), command.ReviewAt.ToUniversalTime());
            var invalid = ApplicabilityOverridePolicy.ValidateProposal(proposal, result.Prediction.Result, result.Prediction.Authoritative, now);
            if (invalid != null) return await Fail(h, Status(invalid), invalid);
            var row = new ApplicabilityOverride {
                TenantId = a.TenantId, VesselId = id, Id = Guid.NewGuid(), EvaluationId = command.EvaluationId, RuleId = result.RuleId, RuleRevision = result.Revision,
                RequirementId = result.RequirementId, RequirementRevision = result.RequirementRevision, SourceId = result.SourceId, SourceRevision = result.SourceRevision,
                BaseResult = result.Prediction.Result, InputHash = command.InputHash, RuleSetHash = command.RuleSetHash, Purpose = command.Purpose,
                Action = command.Action, ReasonCode = command.ReasonCode, Rationale = command.Rationale, EvidenceIdsJson = JsonSerializer.Serialize(command.EvidenceIds, Json),
                ValidFrom = proposal.ValidFrom, ExpiresAt = proposal.ExpiresAt, ReviewAt = proposal.ReviewAt, AuthorId = a.Id, AuthorRoleContext = a.RoleContext,
                State = "IN_REVIEW", Sequence = 1, CreatedAt = now };
            db.ApplicabilityOverrides.Add(row);
            db.ApplicabilityOverrideEvents.Add(new() { TenantId = a.TenantId, VesselId = id, OverrideId = row.Id, Sequence = 1, FromState = null, ToState = "IN_REVIEW",
                EventCode = "PROPOSE", Rationale = command.Rationale, ActorId = a.Id, RoleContext = a.RoleContext, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var outcome = JsonSerializer.Serialize(await Dto(db, row, review, true, now, ct), Json);
            db.ComplianceCommandReceipts.Add(new() { TenantId = a.TenantId, VesselId = id, ActorId = a.Id, Key = key!, Operation = "ApplicabilityOverridePropose",
                ResourceId = row.Id, Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var live = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !Author(live, id) || !Human(h, live)) return await Fail(h, 403, "permission_denied");
            await tx.CommitAsync(ct);
            return Results.Content(outcome, "application/json");
        } catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, 409, "version_conflict");
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) { return await DatabaseFailure(h, db, tx, e, "ApplicabilityOverrideCommand"); }
    }

    private static async Task<IResult> Decide(Guid id, Guid overrideId, HttpContext h) {
        var a = Actor(h); var db = h.RequestServices.GetRequiredService<PoseidonDbContext>(); var ct = h.RequestAborted;
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_query");
        if (!Read(a) || !a.CanEditVessel(id)) return await Fail(h, 403, "permission_denied");
        if (!Human(h, a)) return await Fail(h, 403, "human_actor_required");
        if (!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == id, ct)) return await Fail(h, 404, "resource_not_found");
        var (body, key, failure) = await Envelope(h, 8192, "ApplicabilityOverrideCommand"); if (failure != null) return failure;
        ApplicabilityOverrideDecisionCommand command;
        try {
            command = JsonSerializer.Deserialize<ApplicabilityOverrideDecisionCommand>(body!, Json) ?? throw new JsonException();
            if (command.Decision == null || !ApplicabilityOverridePolicy.Decisions.Contains(command.Decision) || command.ExpectedSequence is <= 0 or > 9007199254740991) throw new JsonException();
            HumanReviewPolicy.Reason(command.Rationale);
        } catch (Exception e) when (e is JsonException or ArgumentException) { return await Fail(h, 400, "invalid_request"); }
        bool Allowed(TrustedActor actor) => Human(h, actor) && actor.CanEditVessel(id) && Read(actor) && command.Decision switch {
            "APPROVE" or "REJECT" => Approver(actor, id), "REVOKE" => Revoker(actor, id), _ => true };
        if (!Allowed(a)) return await Fail(h, 403, "permission_denied");
        if (!await db.ApplicabilityOverrides.AsNoTracking().AnyAsync(o => o.VesselId == id && o.Id == overrideId, ct)) return await Fail(h, 404, "resource_not_found");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { kind = "ApplicabilityOverrideDecision", id, overrideId, body }));
        var replay = await Replay(h, db, a, key!, fingerprint, id, "ApplicabilityOverrideDecision", Allowed, ct, overrideId); if (replay != null) return replay;
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ApplicabilityOverrideCommand", "Attempted", resourceId: overrideId);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var inner = await Replay(h, db, a, key!, fingerprint, id, "ApplicabilityOverrideDecision", Allowed, ct, overrideId); if (inner != null) return inner;
            var row = await db.ApplicabilityOverrides.SingleOrDefaultAsync(o => o.VesselId == id && o.Id == overrideId, ct);
            if (row == null) return await Fail(h, 404, "resource_not_found");
            if (row.Version != command.ExpectedVersion || row.Sequence != command.ExpectedSequence) return await Fail(h, 409, "version_conflict");
            var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var review = await ComplianceWorkspaceEndpoints.Review(id, a, h, db, now);
            var bound = Bound(row, review) && review.Current;
            var overlap = command.Decision == "APPROVE" && await db.ApplicabilityOverrides.AnyAsync(o => o.VesselId == id && o.Id != row.Id && o.RuleId == row.RuleId &&
                o.Purpose == row.Purpose && o.State == "ACTIVE" && o.ValidFrom < row.ExpiresAt && row.ValidFrom < o.ExpiresAt, ct);
            var grant = command.Decision == "APPROVE" ? AuthorityGrant(a, id, row.RuleId, row.Action) : null;
            var refused = ApplicabilityOverridePolicy.Decide(row.State, command.Decision, a.Id, row.AuthorId, true, Approver(a, id), Revoker(a, id), grant,
                row.Action, row.ValidFrom, row.ExpiresAt, row.ReviewAt, bound, overlap, now);
            if (refused != null) return await Fail(h, Status(refused), refused);
            var from = row.State;
            db.Entry(row).Property(x => x.Version).OriginalValue = command.ExpectedVersion;
            row.State = ApplicabilityOverridePolicy.Next(command.Decision); row.Sequence++;
            if (command.Decision == "APPROVE") { row.ApproverId = a.Id; row.AuthorityGrantId = grant!.GrantId; }
            db.ApplicabilityOverrideEvents.Add(new() { TenantId = a.TenantId, VesselId = id, OverrideId = row.Id, Sequence = row.Sequence, FromState = from, ToState = row.State,
                EventCode = command.Decision, Rationale = command.Rationale, ActorId = a.Id, RoleContext = a.RoleContext, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var outcome = JsonSerializer.Serialize(await Dto(db, row, review, true, now, ct), Json);
            db.ComplianceCommandReceipts.Add(new() { TenantId = a.TenantId, VesselId = id, ActorId = a.Id, Key = key!, Operation = "ApplicabilityOverrideDecision",
                ResourceId = row.Id, Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var live = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !Allowed(live)) return await Fail(h, 403, "permission_denied");
            await tx.CommitAsync(ct);
            return Results.Content(outcome, "application/json");
        } catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, 409, "version_conflict");
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) { return await DatabaseFailure(h, db, tx, e, "ApplicabilityOverrideCommand"); }
    }

    private static async Task<IResult> Candidates(Guid id, HttpContext h) {
        const string action = "ComplianceControlCandidateCommand";
        var a = Actor(h); var db = h.RequestServices.GetRequiredService<PoseidonDbContext>(); var ct = h.RequestAborted;
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_query", action);
        if (!ControlWriter(a, id)) return await Fail(h, 403, "permission_denied", action);
        if (!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == id, ct)) return await Fail(h, 404, "resource_not_found", action);
        var (body, key, failure) = await Envelope(h, 4096, action); if (failure != null) return failure;
        ControlCandidateCommand command;
        try {
            command = JsonSerializer.Deserialize<ControlCandidateCommand>(body!, Json) ?? throw new JsonException();
            if (command.EvaluationId == Guid.Empty || command.InputHash == null || !Hash.IsMatch(command.InputHash) || command.RuleSetHash == null || !Hash.IsMatch(command.RuleSetHash)) throw new JsonException();
        } catch (JsonException) { return await Fail(h, 400, "invalid_request", action); }
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { kind = "ComplianceControlCandidates", id, body }));
        var replay = await Replay(h, db, a, key!, fingerprint, id, "ComplianceControlCandidates", live => ControlWriter(live, id), ct, id, "ComplianceControlCandidateReplay"); if (replay != null) return replay;
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, "Attempted", resourceId: id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var inner = await Replay(h, db, a, key!, fingerprint, id, "ComplianceControlCandidates", live => ControlWriter(live, id), ct, id, "ComplianceControlCandidateReplay"); if (inner != null) return inner;
            var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var review = await ComplianceWorkspaceEndpoints.Review(id, a, h, db, now);
            // Only the current qualified applicability may seed candidates; a stale/REVIEW result never does.
            if (!review.Current) return await Fail(h, 409, "current_applicability_unavailable", action);
            if (review.EvaluationId != command.EvaluationId || review.InputHash != command.InputHash || review.RuleSetHash != command.RuleSetHash)
                return await Fail(h, 409, "evaluation_stale", action);
            var visible = ControlEvidenceEndpoints.CurrentVisible(db, id, a);
            var sources = new List<ControlCandidateSource>();
            foreach (var result in review.Results.Where(r => r.Prediction.Result == "APPLICABLE" && r.Prediction.Authoritative)) {
                if (result.RequirementId is not { } requirementId || result.RequirementRevision is not { } requirementRevision ||
                    result.SourceId is not { } sourceId || result.SourceRevision is not { } sourceRevision) continue;
                var requirement = await db.RegisterRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Kind == "Requirement" && r.Id == requirementId && r.Revision == requirementRevision, ct);
                if (requirement == null || requirement.SourceId != sourceId || requirement.SourceRevision != sourceRevision ||
                    !await db.RuleRevisions.AnyAsync(r => r.Id == result.RuleId && r.Revision == result.Revision && r.RequirementId == requirementId &&
                        r.RequirementRevision == requirementRevision && r.SourceId == sourceId && r.SourceRevision == sourceRevision, ct))
                    return await Fail(h, 409, "lineage_unavailable", action);
                var linked = await db.ControlEvidenceRequirements.AsNoTracking().AnyAsync(p => p.VesselId == id && p.Kind == "Control" &&
                    p.RuleId == result.RuleId && p.RuleRevision == result.Revision && p.RequirementId == requirementId && p.RequirementRevision == requirementRevision &&
                    p.SourceId == sourceId && p.SourceRevision == sourceRevision && visible.Any(r => r.Kind == p.Kind && r.Id == p.Id && r.Revision == p.Revision), ct);
                var content = RegisterStorage.Deserialize(requirement.ContentJson);
                sources.Add(new(result.RuleId, result.Revision, requirementId, requirementRevision, sourceId, sourceRevision,
                    content.Title.State == "Known" ? content.Title.Value : null, linked));
            }
            var candidates = ComplianceControlCandidatePolicy.Plan(sources);
            if (candidates.Length == 0) return await Fail(h, 409, "no_control_candidates", action);
            var created = new List<(ControlEvidenceHead Head, ControlCandidate Candidate)>();
            foreach (var candidate in candidates) {
                var s = candidate.Source;
                var head = new ControlEvidenceHead { TenantId = a.TenantId, VesselId = id, Kind = "Control", Id = Guid.NewGuid(), Revision = 1 };
                db.ControlEvidenceHeads.Add(head);
                var pins = new[] { new RequirementPin(s.RequirementId, s.RequirementRevision, s.SourceId, s.SourceRevision, s.RuleId, s.RuleRevision) };
                db.ControlEvidenceRevisions.Add(new() { TenantId = a.TenantId, VesselId = id, Kind = "Control", Id = head.Id, Revision = 1,
                    ContentJson = JsonSerializer.Serialize(candidate.Content, Json),
                    Hash = RegisterStorage.Hash(JsonSerializer.Serialize(new { candidate.Content, Requirements = pins, Links = Array.Empty<GraphPin>(), File = (EvidenceFilePin?)null, action = "candidate" }, Json)),
                    CriticalDeclaration = null, ActorId = a.Id, RoleContext = a.RoleContext, ReasonCode = ComplianceControlCandidatePolicy.ReasonCode, Lifecycle = "DRAFT", SavedAt = now });
                db.ControlEvidenceRequirements.Add(new() { TenantId = a.TenantId, VesselId = id, Kind = "Control", Id = head.Id, Revision = 1,
                    RequirementId = s.RequirementId, RequirementRevision = s.RequirementRevision, SourceId = s.SourceId, SourceRevision = s.SourceRevision, RuleId = s.RuleId, RuleRevision = s.RuleRevision });
                created.Add((head, candidate));
            }
            await db.SaveChangesAsync(ct);
            var items = created.Select(x => new ControlCandidateItem(x.Head.Id, x.Head.Revision, x.Head.Version, x.Candidate.Source.RuleId, x.Candidate.Source.RuleRevision,
                x.Candidate.Source.RequirementId, x.Candidate.Source.RequirementRevision, x.Candidate.Source.SourceId, x.Candidate.Source.SourceRevision,
                x.Candidate.Content.Title!, "DRAFT", "DRAFT", ControlEvidencePolicy.Blockers("Control", x.Candidate.Content, 1, false, 1))).ToArray();
            var outcome = JsonSerializer.Serialize(new ControlCandidateOutcome(id, command.EvaluationId, items, sources.Count(s => s.AlreadyLinked),
                ["CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE", "VERIFICATION_NOT_PERFORMED"]), Json);
            db.ComplianceCommandReceipts.Add(new() { TenantId = a.TenantId, VesselId = id, ActorId = a.Id, Key = key!, Operation = "ComplianceControlCandidates",
                ResourceId = id, Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var live = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !ControlWriter(live, id)) return await Fail(h, 403, "permission_denied", action);
            await tx.CommitAsync(ct);
            return Results.Content(outcome, "application/json");
        } catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, 409, "version_conflict", action);
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) { return await DatabaseFailure(h, db, tx, e, action); }
    }

    // A retained receipt replays only for the same actor/key/body after current authorization is re-checked.
    private static async Task<IResult?> Replay(HttpContext h, PoseidonDbContext db, TrustedActor a, string key, string fingerprint, Guid vessel,
        string operation, Func<TrustedActor, bool> allowed, CancellationToken ct, Guid? resource = null, string action = "ApplicabilityOverrideReplay") {
        var live = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !allowed(live) || !await live.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == vessel, ct))
            return await Fail(h, 403, "permission_denied", action == "ApplicabilityOverrideReplay" ? "ApplicabilityOverrideCommand" : "ComplianceControlCandidateCommand");
        var receipt = await db.ComplianceCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
        if (receipt == null) return null;
        if (receipt.Fingerprint != fingerprint || receipt.VesselId != vessel || receipt.Operation != operation || resource != null && receipt.ResourceId != resource)
            return await Fail(h, 409, "idempotency_key_reused", action == "ApplicabilityOverrideReplay" ? "ApplicabilityOverrideCommand" : "ComplianceControlCandidateCommand");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, "Success", resourceId: receipt.ResourceId);
        return Results.Content(receipt.OutcomeJson, "application/json");
    }
    private static async Task<IResult> DatabaseFailure(HttpContext h, PoseidonDbContext db, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, Exception e, string action) {
        DatabaseFailureObservation.Record(h, e);
        await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
        var code = PostgresFailure.Provider(e)?.SqlState;
        var conflict = code is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
        return await Fail(h, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable", action);
    }
}
