using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// ---- DTOs (S7 WP-A: DEV-048 / DEV-049 / DEV-050) ----
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewCredentialAssertionCommand([property: JsonRequired] long CredentialRevision, [property: JsonRequired] long ExpectedSequence,
    [property: JsonRequired] Guid AuthorityId, [property: JsonRequired] string State, [property: JsonRequired] string Rationale);
public sealed record CrewCredentialAssertionDto(Guid Id, Guid CredentialId, long CredentialRevision, long Sequence, string State, Guid ActorId,
    Guid RoleId, string Duty, DateTimeOffset SavedAt, bool Current);
public sealed record CrewCredentialVerificationView(Guid CredentialId, Guid AssignmentId, long CredentialRevision, long Sequence, string Status,
    CrewCredentialAssertionDto? Latest, CrewCredentialAssertionDto[] History, CrewAuthorityDto[] Authorities, bool CanRecord, bool ActorIsDeclarer,
    bool DocumentAvailable, string[] States, string[] Blockers, string[] Qualifications);
public sealed record CrewCertificateRegisterRow(Guid CredentialId, Guid AssignmentId, Guid? PositionId, string? CertificateTypeReference,
    DateOnly? ExpiryDate, bool? EndorsementRequired, DateOnly? EndorsementExpiry, long Revision, string? AssertionState, bool AssertionCurrent, string Status);
public sealed record CrewCertificateRegister(Guid VesselId, DateOnly ReferenceDate, CrewCertificateRegisterRow[] Items, bool Truncated, string[] Qualifications);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewCompetenceRequirementCommand([property: JsonRequired] Guid Id, uint? ExpectedVersion, [property: JsonRequired] Guid PositionId,
    [property: JsonRequired] string RequirementType, [property: JsonRequired] string Reference, [property: JsonRequired] bool Mandatory,
    int? ValidityMonths, string? VesselTypeScope, string? CargoScope, Guid? CompetenceLinkId, Guid? FamiliarizationLinkId, [property: JsonRequired] string Status);
public sealed record CrewCompetenceRequirementDto(Guid Id, Guid PositionId, string? PositionCode, string RequirementType, string Reference, bool Mandatory,
    int? ValidityMonths, string? VesselTypeScope, string? CargoScope, Guid? CompetenceLinkId, Guid? FamiliarizationLinkId, string Status,
    string Provenance, long Revision, uint Version, DateTimeOffset SavedAt);
public sealed record CrewCompetenceMatrix(Guid VesselId, CrewCompetenceRequirementDto[] Items, bool CanWrite, string[] RequirementTypes, string[] Qualifications);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewCompetenceRecordCommand([property: JsonRequired] Guid Id, uint? ExpectedVersion, [property: JsonRequired] Guid RequirementId,
    [property: JsonRequired] string Kind, DateOnly? CompletedOn, DateOnly? ValidUntil, string? Reference);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewCompetenceSignOffCommand([property: JsonRequired] long RecordRevision, [property: JsonRequired] long ExpectedSequence,
    [property: JsonRequired] Guid AuthorityId, [property: JsonRequired] string Outcome, [property: JsonRequired] string Rationale);
public sealed record CrewCompetenceSignOffDto(Guid Id, long RecordRevision, long Sequence, string Outcome, Guid ActorId, Guid RoleId, string Duty, DateTimeOffset SavedAt, bool Current);
public sealed record CrewCompetenceRecordDto(Guid Id, Guid RequirementId, string Kind, DateOnly? CompletedOn, DateOnly? ValidUntil, string? Reference,
    string Provenance, long Revision, uint Version, DateTimeOffset SavedAt, bool ActorIsRecorder, CrewCompetenceSignOffDto? SignOff);
public sealed record CrewCompetenceView(Guid VesselId, Guid AssignmentId, Guid? PositionId, CrewEligibilityWindow Window, CrewCompetenceRecordDto[] Records,
    CrewEligibilityResult Evaluation, CrewAuthorityDto[] Authorities, bool CanWrite, bool CanSignOff, string[] Kinds, string[] Qualifications);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafeManningDecisionCommand([property: JsonRequired] DateOnly From, [property: JsonRequired] DateOnly To,
    [property: JsonRequired] string EvaluationHash, [property: JsonRequired] long ExpectedSequence, [property: JsonRequired] Guid AuthorityId,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Rationale,
    ManningReviewAcknowledgement[]? Acknowledgements = null, ManningDispensation[]? Dispensations = null);
public sealed record SafeManningDecisionDto(Guid Id, long Sequence, string Decision, string EvaluationState, string EvaluationHash, DateOnly WindowFrom,
    DateOnly WindowTo, Guid ActorId, Guid RoleId, string Duty, DateTimeOffset SavedAt, string[]? AcknowledgedReasonCodes = null, ManningDispensation[]? Dispensations = null);
public sealed record ManningDispensationMember(Guid AssignmentId, Guid? PositionId);
public sealed record ManningEvidenceOption(string Kind, Guid Id, long Revision, string Label);
public sealed record SafeManningView(SafeManningEvaluation Evaluation, CrewAuthorityDto[] Authorities, bool CanDecide, long Sequence,
    SafeManningDecisionDto? LatestDecision, string[] Decisions, string[]? ReviewReasonCodes = null, ManningDispensationMember[]? DispensationMembers = null,
    ManningEvidenceOption[]? EvidenceOptions = null, string[]? Posts = null, string[]? Rules = null);
public sealed record SafeManningDecisionPage(SafeManningDecisionDto[] Items, int Offset, bool HasMore);
public sealed record CrewCommandOutcome(Guid Id, long Revision, uint Version, bool Replayed);

public static class CrewComplianceEndpoints
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
    private static readonly string[] ManningQualifications = ["ADVISORY_EVALUATION_ONLY", "DECISION_IS_AN_ATTRIBUTED_HUMAN_RECORD", "NOT_A_GO_OR_RELEASE"];

    public static void MapCrewCompliance(this IEndpointRouteBuilder app)
    {
        var crew = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew").RequireAuthorization("DirectoryRead");
        crew.MapGet("/certificates", (Guid vessel, HttpContext h, PoseidonDbContext db) => Register(vessel, h, db))
            .WithName("CrewCertificateRegister").Produces<CrewCertificateRegister>();
        crew.MapGet("/{assignment:guid}/certificates/{id:guid}/verification", (Guid vessel, Guid assignment, Guid id, HttpContext h, PoseidonDbContext db) => Verification(vessel, assignment, id, h, db))
            .WithName("CrewCredentialVerification").Produces<CrewCredentialVerificationView>();
        crew.MapPost("/{assignment:guid}/certificates/{id:guid}/verification", (Guid vessel, Guid assignment, Guid id, HttpContext h, PoseidonDbContext db) => RecordAssertion(vessel, assignment, id, h, db))
            .WithName("CrewCredentialAssertionRecord").Accepts<CrewCredentialAssertionCommand>("application/json").Produces<CrewCommandOutcome>();
        crew.MapGet("/competence-matrix", (Guid vessel, HttpContext h, PoseidonDbContext db) => Matrix(vessel, h, db))
            .WithName("CrewCompetenceMatrix").Produces<CrewCompetenceMatrix>();
        crew.MapPost("/competence-matrix", (Guid vessel, HttpContext h, PoseidonDbContext db) => SaveRequirement(vessel, h, db))
            .WithName("CrewCompetenceRequirementSave").Accepts<CrewCompetenceRequirementCommand>("application/json").Produces<CrewCommandOutcome>();
        crew.MapGet("/{assignment:guid}/competence", (Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db) => Competence(vessel, assignment, h, db))
            .WithName("CrewCompetenceGaps").Produces<CrewCompetenceView>();
        crew.MapPost("/{assignment:guid}/competence", (Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db) => SaveRecord(vessel, assignment, h, db))
            .WithName("CrewCompetenceRecordSave").Accepts<CrewCompetenceRecordCommand>("application/json").Produces<CrewCommandOutcome>();
        crew.MapPost("/{assignment:guid}/competence/{record:guid}/sign-off", (Guid vessel, Guid assignment, Guid record, HttpContext h, PoseidonDbContext db) => SignOff(vessel, assignment, record, h, db))
            .WithName("CrewCompetenceSignOff").Accepts<CrewCompetenceSignOffCommand>("application/json").Produces<CrewCommandOutcome>();
        var manning = app.MapGroup("/api/v1/vessels/{id:guid}/safe-manning").RequireAuthorization("DirectoryRead");
        manning.MapGet("/evaluate", (Guid id, HttpContext h, PoseidonDbContext db) => Evaluate(id, h, db))
            .WithName("SafeManningEvaluate").Produces<SafeManningView>();
        manning.MapGet("/decisions", (Guid id, HttpContext h, PoseidonDbContext db) => Decisions(id, h, db))
            .WithName("SafeManningDecisions").Produces<SafeManningDecisionPage>();
        manning.MapPost("/decisions", (Guid id, HttpContext h, PoseidonDbContext db) => RecordDecision(id, h, db))
            .WithName("SafeManningDecisionRecord").Accepts<SafeManningDecisionCommand>("application/json").Produces<CrewCommandOutcome>();
    }

    // ---- shared guards ----
    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static DateTimeOffset Now(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    private static FederationSettings Settings(HttpContext h) => h.RequestServices.GetRequiredService<FederationSettings>();
    private static bool CrewRead(TrustedActor? a) => a != null && a.CanRead && a.Permissions.Contains("crew.read");
    internal static bool CompetenceRead(TrustedActor? a) => CrewRead(a) && a!.Permissions.Contains("crew.competence.read");
    private static bool CompetenceWrite(TrustedActor? a, Guid vessel) => CompetenceRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("crew.competence.write") && a.CanEditCrew(vessel);
    private static bool CompetenceAssess(TrustedActor? a) => CompetenceRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("crew.competence.assess");
    private static bool ManningRead(TrustedActor? a) => CrewRead(a) && a!.Permissions.Contains("crew.safe_manning.read");
    private static bool ManningDecide(TrustedActor? a) => ManningRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("crew.safe_manning.decide");
    private static bool CredentialVerify(TrustedActor? a, Guid vessel) => a != null && CredentialEndpoints.Permitted(a, vessel, false) && a.HasWriteAttribution && a.Permissions.Contains("crew.credential.verify");
    private static Task<bool> InScope(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) => a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == vessel, ct);

    private static async Task<IResult> Fail(HttpContext h, string action, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, code);
        return Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status);
    }
    private static Task Audit(HttpContext h, string action, string result, Guid? resource) =>
        h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, result, resourceId: resource);

    private static bool Text(string? value, int max, bool required) => value == null ? !required :
        value.Length <= max && (!required || !string.IsNullOrWhiteSpace(value)) && !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    private static bool Code(string? value, int max) => value == null || value.Length > 0 && value.Length <= max && value.Trim() == value && !value.Any(char.IsControl);

    // Window query: optional from/to (yyyy-MM-dd); defaults to the policy reference date.
    private static bool Window(HttpContext h, string[] extra, out CrewEligibilityWindow window, out int offset)
    {
        offset = 0;
        var today = h.RequestServices.GetRequiredService<CrewAssignmentPolicy>().ReferenceDate(Now(h)) ?? DateOnly.FromDateTime(Now(h).UtcDateTime);
        window = new(today, today);
        var q = h.Request.Query;
        if (q.Any(k => k.Value.Count != 1 || k.Key is not ("from" or "to") && !extra.Contains(k.Key))) return false;
        static bool Day(string? v, out DateOnly d) => DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        var from = today; var to = today;
        if (q.ContainsKey("from") && !Day(q["from"], out from)) return false;
        if (q.ContainsKey("to") && !Day(q["to"], out to)) return false;
        if (q.ContainsKey("from") && !q.ContainsKey("to")) to = from;
        if (q.ContainsKey("offset") && (!int.TryParse(q["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 100000)) return false;
        window = new(from, to);
        return true;
    }

    private static async Task<T?> Body<T>(HttpContext h) where T : class
    {
        if (!h.Request.HasJsonContentType()) return null;
        using var buffer = new MemoryStream();
        await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 16384, h.RequestAborted);
        using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 4 });
        ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
        return document.RootElement.Deserialize<T>(Json);
    }

    private sealed record Applied(IResult? Error, Guid Id = default, long Revision = 0, uint Version = 0);

    // One audited, idempotent, SERIALIZABLE command. The handler adds rows and returns the
    // committed resource; the receipt binds the key to the exact request fingerprint.
    private static async Task<IResult> Command<T>(HttpContext h, PoseidonDbContext db, string auditAction, string receiptAction, Guid vessel,
        Func<TrustedActor?, bool> permitted, object scope, Func<T, bool> valid, Func<TrustedActor, T, Task<Applied>> apply) where T : class
    {
        h.Response.Headers.CacheControl = "no-store";
        var ct = h.RequestAborted; var actor = Actor(h);
        if (!permitted(actor)) return await Fail(h, auditAction, 403, "permission_denied");
        if (!await InScope(db, actor!, vessel, ct)) return await Fail(h, auditAction, 404, "resource_not_found");
        if (h.Request.Query.Count != 0) return await Fail(h, auditAction, 400, "invalid_query");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, auditAction, 403, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || !Guid.TryParseExact(key, "D", out _)) return await Fail(h, auditAction, 400, "invalid_idempotency_key");
        T? command;
        try { command = await Body<T>(h); }
        catch (Exception e) when (e is JsonException or InvalidDataException) { command = null; }
        if (command == null) return await Fail(h, auditAction, 400, "invalid_request");
        if (!valid(command)) return await Fail(h, auditAction, 400, "validation_failed");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { receiptAction, vessel, scope, command }, Json));
        async Task<IResult?> Replay() {
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor!.Id && x.Key == key, ct);
            if (receipt == null) return null;
            if (receipt.Action != receiptAction || receipt.Fingerprint != fingerprint) return await Fail(h, auditAction, 409, "idempotency_key_reused");
            await Audit(h, auditAction, "Success", receipt.ResourceId);
            return Results.Ok(new CrewCommandOutcome(receipt.ResourceId, receipt.RegisterRevision ?? 0, receipt.RegisterVersion ?? 0, true));
        }
        await Audit(h, auditAction, "Attempted", vessel);
        IDbContextTransaction? tx = null;
        try {
            tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (db.TenantId != actor!.TenantId) return await Fail(h, auditAction, 403, "permission_denied");
            var replay = await Replay(); if (replay != null) return replay;
            var applied = await apply(actor, command);
            if (applied.Error != null) return applied.Error;
            db.CommandReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = receiptAction, Fingerprint = fingerprint,
                ResourceId = applied.Id, RegisterRevision = applied.Revision, RegisterVersion = applied.Version, CommittedAt = Now(h) });
            await db.SaveChangesAsync(ct);
            // Fresh rights fence: the actor must still hold the same permission after the write.
            if (!permitted(Actor(h))) return await Fail(h, auditAction, 403, "permission_denied");
            await tx.CommitAsync(ct);
            await Audit(h, auditAction, "Success", applied.Id);
            return Results.Ok(new CrewCommandOutcome(applied.Id, applied.Revision, applied.Version, false));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e) || e is DbUpdateException) {
            if (tx != null) await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var conflict = e is DbUpdateException || PostgresFailure.IsConflict(e);
            return await Replay() ?? await Fail(h, auditAction, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable");
        } finally { if (tx != null) await tx.DisposeAsync(); }
    }

    // ---- DEV-048: credential register + independent human verification assertion ----
    private static async Task<(CrewCredentialAssertion? Latest, bool Current)> LatestAssertion(HttpContext h, PoseidonDbContext db, Credential c, CancellationToken ct)
    {
        var latest = await db.CrewCredentialAssertions.AsNoTracking().Where(a => a.VesselId == c.VesselId && a.CredentialId == c.Id)
            .OrderByDescending(a => a.Sequence).FirstOrDefaultAsync(ct);
        return (latest, latest != null && latest.CredentialRevision == c.Revision && CrewAuthorityResolver.StillValid(Settings(h), c.TenantId, latest.ActorId,
            latest.AuthorityId, latest.RoleId, CrewAuthorityResolver.CredentialVerification, c.VesselId, Now(h)));
    }
    private static CrewCredentialAssertionDto AssertionDto(CrewCredentialAssertion a, bool current) =>
        new(a.Id, a.CredentialId, a.CredentialRevision, a.Sequence, a.State, a.ActorId, a.RoleId, a.Duty, a.SavedAt, current);
    private static async Task<bool> Declarer(PoseidonDbContext db, Credential c, Guid actor, CancellationToken ct) =>
        await db.CredentialRevisions.AsNoTracking().AnyAsync(r => r.VesselId == c.VesselId && r.CredentialId == c.Id && (r.ActorId == null || r.ActorId == actor), ct);

    private static async Task<IResult> Register(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var ct = h.RequestAborted; var actor = Actor(h);
        if (actor == null || !CredentialEndpoints.Permitted(actor, vessel, false)) return await Fail(h, "CredentialRead", 403, "permission_denied");
        if (!await InScope(db, actor, vessel, ct)) return await Fail(h, "CredentialRead", 404, "resource_not_found");
        if (h.Request.Query.Count != 0) return await Fail(h, "CredentialRead", 400, "invalid_query");
        var rows = await db.Credentials.AsNoTracking().Where(c => c.VesselId == vessel).OrderBy(c => c.AssignmentId).ThenBy(c => c.Id).Take(201).ToArrayAsync(ct);
        var assignmentIds = rows.Select(r => r.AssignmentId).Distinct().ToArray();
        var positions = await db.CrewAssignments.AsNoTracking().Where(a => a.VesselId == vessel && assignmentIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.PositionId, ct);
        var reference = DateOnly.FromDateTime(Now(h).UtcDateTime);
        var items = new List<CrewCertificateRegisterRow>();
        foreach (var c in rows.Take(200)) {
            // Same document authorization as the per-assignment list: hidden files hide the row.
            if (c.DocumentReference is { } file && !await CrewFileEndpoints.AuthorizedDocument(vessel, c.AssignmentId, file, h, db)) continue;
            var (latest, current) = await LatestAssertion(h, db, c, ct);
            var fact = new CrewCredentialFact(c.Id, c.CertificateTypeReference, c.ExpiryDate, c.EndorsementRequired, c.EndorsementExpiry, latest?.State, current);
            items.Add(new(c.Id, c.AssignmentId, positions.GetValueOrDefault(c.AssignmentId), c.CertificateTypeReference, c.ExpiryDate, c.EndorsementRequired,
                c.EndorsementExpiry, c.Revision, latest?.State, current, CrewEligibilityPolicy.CredentialStatus(fact, reference)));
        }
        await Audit(h, "CredentialRead", "Success", vessel);
        return Results.Ok(new CrewCertificateRegister(vessel, reference, items.ToArray(), rows.Length > 200,
            ["DECLARED_DATA_UNVERIFIED_UNTIL_HUMAN_ASSERTION", "EXPIRED_OR_UNVERIFIED_NEVER_POSITIVE", "NO_CERTIFICATE_NUMBER_OR_IDENTITY_PROJECTED"]));
    }

    private static async Task<IResult> Verification(Guid vessel, Guid assignment, Guid id, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var ct = h.RequestAborted; var actor = Actor(h);
        if (actor == null || !CredentialEndpoints.Permitted(actor, vessel, false)) return await Fail(h, "CredentialVerificationRead", 403, "permission_denied");
        if (!await InScope(db, actor, vessel, ct)) return await Fail(h, "CredentialVerificationRead", 404, "resource_not_found");
        if (h.Request.Query.Count != 0) return await Fail(h, "CredentialVerificationRead", 400, "invalid_query");
        var c = await db.Credentials.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.AssignmentId == assignment && x.Id == id, ct);
        if (c == null) return await Fail(h, "CredentialVerificationRead", 404, "resource_not_found");
        var document = c.DocumentReference is { } file && await CrewFileEndpoints.AuthorizedDocument(vessel, assignment, file, h, db);
        if (c.DocumentReference != null && !document) return await Fail(h, "CredentialVerificationRead", 404, "resource_not_found");
        var history = await db.CrewCredentialAssertions.AsNoTracking().Where(a => a.VesselId == vessel && a.CredentialId == id)
            .OrderByDescending(a => a.Sequence).Take(25).ToArrayAsync(ct);
        var (latest, current) = await LatestAssertion(h, db, c, ct);
        var declarer = await Declarer(db, c, actor.Id, ct);
        var authorities = CredentialVerify(actor, vessel) ? CrewAuthorityResolver.Resolve(Settings(h), actor, vessel, CrewAuthorityResolver.CredentialVerification, Now(h)) : [];
        var blockers = new List<string>();
        if (!document) blockers.Add("CREDENTIAL_DOCUMENT_REQUIRED");
        if (declarer) blockers.Add("SEPARATION_OF_DUTIES_DECLARER_CANNOT_VERIFY");
        if (authorities.Length == 0) blockers.Add("NO_CURRENT_VERIFICATION_APPOINTMENT");
        var fact = new CrewCredentialFact(c.Id, c.CertificateTypeReference, c.ExpiryDate, c.EndorsementRequired, c.EndorsementExpiry, latest?.State, current);
        await Audit(h, "CredentialVerificationRead", "Success", id);
        return Results.Ok(new CrewCredentialVerificationView(c.Id, assignment, c.Revision, latest?.Sequence ?? 0,
            CrewEligibilityPolicy.CredentialStatus(fact, DateOnly.FromDateTime(Now(h).UtcDateTime)), latest == null ? null : AssertionDto(latest, current),
            history.Select(a => AssertionDto(a, a.Id == latest?.Id && current)).ToArray(), CrewAuthorityResolver.Dto(authorities),
            blockers.Count == 0, declarer, document, CrewCompetenceStorage.AssertionStates, blockers.ToArray(),
            ["RECORDED_HUMAN_ASSERTION_ONLY", "NOT_LEGAL_CERTIFICATE_STATUS", "DECLARER_NEVER_VERIFIES"]));
    }

    private static Task<IResult> RecordAssertion(Guid vessel, Guid assignment, Guid id, HttpContext h, PoseidonDbContext db) =>
        Command<CrewCredentialAssertionCommand>(h, db, "CredentialVerificationCommand", "CrewCredentialAssertionRecord", vessel, a => CredentialVerify(a, vessel), new { assignment, id },
            c => c.CredentialRevision > 0 && c.ExpectedSequence >= 0 && c.AuthorityId != Guid.Empty && CrewCompetenceStorage.AssertionStates.Contains(c.State) && Text(c.Rationale, 2000, true),
            async (actor, command) => {
                var ct = h.RequestAborted; var now = Now(h);
                var c = await db.Credentials.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.AssignmentId == assignment && x.Id == id, ct);
                if (c == null) return new(await Fail(h, "CredentialVerificationCommand", 404, "resource_not_found"));
                if (c.Revision != command.CredentialRevision) return new(await Fail(h, "CredentialVerificationCommand", 409, "version_conflict"));
                if (c.DocumentReference is not { } file || !await CrewFileEndpoints.AuthorizedDocument(vessel, assignment, file, h, db))
                    return new(await Fail(h, "CredentialVerificationCommand", 403, "credential_document_required"));
                var grant = CrewAuthorityResolver.Resolve(Settings(h), actor, vessel, CrewAuthorityResolver.CredentialVerification, now).SingleOrDefault(g => g.Id == command.AuthorityId);
                if (grant == null) return new(await Fail(h, "CredentialVerificationCommand", 403, "human_authority_required"));
                // SoD: whoever declared any revision (or an unattributed historical revision) never verifies it.
                if (await Declarer(db, c, actor.Id, ct)) return new(await Fail(h, "CredentialVerificationCommand", 403, "separation_of_duties"));
                var sequence = await db.CrewCredentialAssertions.Where(a => a.VesselId == vessel && a.CredentialId == id).MaxAsync(a => (long?)a.Sequence, ct) ?? 0;
                if (sequence != command.ExpectedSequence) return new(await Fail(h, "CredentialVerificationCommand", 409, "version_conflict"));
                var row = new CrewCredentialAssertion { TenantId = actor.TenantId, Id = Guid.NewGuid(), VesselId = vessel, CredentialId = id, CredentialRevision = c.Revision,
                    Sequence = sequence + 1, State = command.State, ActorId = actor.Id, RoleId = grant.RoleId, AuthorityId = grant.Id, Duty = grant.Duty,
                    AuthorityHash = CrewAuthorityResolver.Hash(grant, actor), Rationale = command.Rationale, SavedAt = now };
                db.CrewCredentialAssertions.Add(row);
                await db.SaveChangesAsync(ct);
                return new(null, row.Id, row.Sequence, 0);
            });

    // ---- DEV-049: training-position/competence matrix and gap reasons ----
    private static CrewCompetenceRequirementDto RequirementDto(CrewCompetenceRequirement r, string? code) => new(r.Id, r.PositionId, code, r.RequirementType, r.Reference,
        r.Mandatory, r.ValidityMonths, r.VesselTypeScope, r.CargoScope, r.CompetenceLinkId, r.FamiliarizationLinkId, r.Status, r.Provenance, r.Revision, r.Version, r.SavedAt);

    private static async Task<IResult> Matrix(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var ct = h.RequestAborted; var actor = Actor(h);
        if (!CompetenceRead(actor)) return await Fail(h, "CrewCompetenceRead", 403, "permission_denied");
        if (!await InScope(db, actor!, vessel, ct)) return await Fail(h, "CrewCompetenceRead", 404, "resource_not_found");
        if (h.Request.Query.Count != 0) return await Fail(h, "CrewCompetenceRead", 400, "invalid_query");
        var rows = await db.CrewCompetenceRequirements.AsNoTracking().Where(r => r.VesselId == vessel)
            .OrderBy(r => r.PositionId).ThenBy(r => r.RequirementType).ThenBy(r => r.Reference).Take(500).ToArrayAsync(ct);
        var codes = await db.CrewPositions.AsNoTracking().Where(p => p.VesselId == vessel).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        await Audit(h, "CrewCompetenceRead", "Success", vessel);
        return Results.Ok(new CrewCompetenceMatrix(vessel, rows.Select(r => RequirementDto(r, codes.GetValueOrDefault(r.PositionId))).ToArray(),
            CompetenceWrite(actor, vessel), CrewRequirementTypes.All, ["OPERATOR_DECLARED_MATRIX_NOT_APPROVED_CATALOG", "TRAINING_COMPLETION_IS_NOT_COMPETENCE"]));
    }

    private static Task<IResult> SaveRequirement(Guid vessel, HttpContext h, PoseidonDbContext db) =>
        Command<CrewCompetenceRequirementCommand>(h, db, "CrewCompetenceCommand", "CrewCompetenceRequirementSave", vessel, a => CompetenceWrite(a, vessel), new { },
            c => c.Id != Guid.Empty && c.PositionId != Guid.Empty && CrewRequirementTypes.All.Contains(c.RequirementType) && Text(c.Reference, 200, true) && c.Reference.Trim() == c.Reference &&
                c.ValidityMonths is null or (>= 1 and <= 240) && Code(c.VesselTypeScope, 100) && Code(c.CargoScope, 100) &&
                CrewCompetenceStorage.RequirementStatuses.Contains(c.Status) && c.CompetenceLinkId != c.Id && c.FamiliarizationLinkId != c.Id &&
                c.CompetenceLinkId != Guid.Empty && c.FamiliarizationLinkId != Guid.Empty,
            async (actor, command) => {
                var ct = h.RequestAborted; const string audit = "CrewCompetenceCommand";
                if (!await db.CrewPositions.AnyAsync(p => p.VesselId == vessel && p.Id == command.PositionId, ct)) return new(await Fail(h, audit, 404, "resource_not_found"));
                foreach (var (link, type) in new[] { (command.CompetenceLinkId, CrewRequirementTypes.Competence), (command.FamiliarizationLinkId, CrewRequirementTypes.Familiarization) })
                    if (link is { } linked && !await db.CrewCompetenceRequirements.AnyAsync(r => r.VesselId == vessel && r.Id == linked && r.RequirementType == type, ct))
                        return new(await Fail(h, audit, 400, "linked_resource_unavailable"));
                var row = await db.CrewCompetenceRequirements.SingleOrDefaultAsync(r => r.Id == command.Id, ct);
                if (row == null && command.ExpectedVersion != null || row != null && (row.VesselId != vessel || row.Version != command.ExpectedVersion))
                    return new(await Fail(h, audit, 409, "version_conflict"));
                if (row != null && (row.PositionId != command.PositionId || row.RequirementType != command.RequirementType))
                    return new(await Fail(h, audit, 400, "invalid_request"));
                if (row == null) {
                    row = new() { TenantId = actor.TenantId, Id = command.Id, VesselId = vessel, PositionId = command.PositionId, RequirementType = command.RequirementType };
                    db.CrewCompetenceRequirements.Add(row);
                } else row.Revision = checked(row.Revision + 1);
                row.Reference = command.Reference; row.Mandatory = command.Mandatory; row.ValidityMonths = command.ValidityMonths;
                row.VesselTypeScope = command.VesselTypeScope; row.CargoScope = command.CargoScope; row.CompetenceLinkId = command.CompetenceLinkId;
                row.FamiliarizationLinkId = command.FamiliarizationLinkId; row.Status = command.Status; row.Provenance = "UserDeclaredUnverified";
                row.ActorId = actor.Id; row.SavedAt = Now(h);
                await db.SaveChangesAsync(ct);
                return new(null, row.Id, row.Revision, row.Version);
            });

    private static async Task<IResult> Competence(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var ct = h.RequestAborted; var actor = Actor(h);
        if (!CompetenceRead(actor)) return await Fail(h, "CrewCompetenceRead", 403, "permission_denied");
        if (!await InScope(db, actor!, vessel, ct)) return await Fail(h, "CrewCompetenceRead", 404, "resource_not_found");
        if (!Window(h, [], out var window, out _) || !window.Valid) return await Fail(h, "CrewCompetenceRead", 400, "evaluation_window_invalid");
        var owner = await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.VesselId == vessel && a.Id == assignment, ct);
        if (owner == null) return await Fail(h, "CrewCompetenceRead", 404, "resource_not_found");
        var evaluator = h.RequestServices.GetRequiredService<CrewEligibilityEvaluator>();
        var evaluation = await evaluator.CoreAsync(owner, window, Now(h), ct);
        var records = await db.CrewCompetenceRecords.AsNoTracking().Where(r => r.VesselId == vessel && r.AssignmentId == assignment).OrderBy(r => r.RequirementId).ThenBy(r => r.Id).Take(200).ToArrayAsync(ct);
        var ids = records.Select(r => r.Id).ToArray();
        var signOffs = (await db.CrewCompetenceSignOffs.AsNoTracking().Where(s => s.VesselId == vessel && ids.Contains(s.RecordId)).ToArrayAsync(ct))
            .GroupBy(s => s.RecordId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Sequence).First());
        var now = Now(h);
        var authorities = CompetenceAssess(actor) ? CrewAuthorityResolver.Resolve(Settings(h), actor, vessel, CrewAuthorityResolver.CompetenceAssessment, now) : [];
        var dtos = records.Select(r => {
            var s = signOffs.GetValueOrDefault(r.Id);
            var current = s != null && s.RecordRevision == r.Revision && CrewAuthorityResolver.StillValid(Settings(h), r.TenantId, s.ActorId, s.AuthorityId, s.RoleId, CrewAuthorityResolver.CompetenceAssessment, vessel, now);
            return new CrewCompetenceRecordDto(r.Id, r.RequirementId, r.Kind, r.CompletedOn, r.ValidUntil, r.Reference, r.Provenance, r.Revision, r.Version, r.SavedAt,
                r.ActorId == actor!.Id, s == null ? null : new(s.Id, s.RecordRevision, s.Sequence, s.Outcome, s.ActorId, s.RoleId, s.Duty, s.SavedAt, current));
        }).ToArray();
        await Audit(h, "CrewCompetenceRead", "Success", assignment);
        return Results.Ok(new CrewCompetenceView(vessel, assignment, owner.PositionId, window, dtos, evaluation, CrewAuthorityResolver.Dto(authorities),
            CompetenceWrite(actor, vessel), authorities.Length > 0, CrewCompetenceKinds.All,
            ["TRAINING_COMPLETION_IS_NOT_COMPETENCE", "SIGN_OFF_IS_A_HUMAN_RECORD", "RECORDER_NEVER_SIGNS_OFF", "ADVISORY_ONLY"]));
    }

    private static Task<IResult> SaveRecord(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db) =>
        Command<CrewCompetenceRecordCommand>(h, db, "CrewCompetenceCommand", "CrewCompetenceRecordSave", vessel, a => CompetenceWrite(a, vessel), new { assignment },
            c => c.Id != Guid.Empty && c.RequirementId != Guid.Empty && CrewCompetenceKinds.All.Contains(c.Kind) && Text(c.Reference, 200, false) &&
                !(c.CompletedOn != null && c.ValidUntil != null && c.ValidUntil < c.CompletedOn) &&
                !(c.CompletedOn > DateOnly.FromDateTime(Now(h).UtcDateTime)),
            async (actor, command) => {
                var ct = h.RequestAborted; const string audit = "CrewCompetenceCommand";
                var owner = await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.VesselId == vessel && a.Id == assignment, ct);
                if (owner == null) return new(await Fail(h, audit, 404, "resource_not_found"));
                var requirement = await db.CrewCompetenceRequirements.AsNoTracking().SingleOrDefaultAsync(r => r.VesselId == vessel && r.Id == command.RequirementId && r.Status == "ACTIVE", ct);
                if (requirement == null || requirement.PositionId != owner.PositionId) return new(await Fail(h, audit, 400, "competence_requirement_unavailable"));
                // Credential-backed requirements are satisfied only by credentials (DEV-048). Training
                // completion may support a competence requirement but never satisfies it.
                var allowed = CrewCompetenceKinds.For(requirement.RequirementType);
                if (allowed == "" || command.Kind != allowed && !(requirement.RequirementType == CrewRequirementTypes.Competence && command.Kind == CrewCompetenceKinds.TrainingCompletion))
                    return new(await Fail(h, audit, 400, "validation_failed"));
                var row = await db.CrewCompetenceRecords.SingleOrDefaultAsync(r => r.Id == command.Id, ct);
                if (row == null && command.ExpectedVersion != null || row != null && (row.VesselId != vessel || row.AssignmentId != assignment || row.Version != command.ExpectedVersion))
                    return new(await Fail(h, audit, 409, "version_conflict"));
                if (row != null && (row.RequirementId != command.RequirementId || row.Kind != command.Kind)) return new(await Fail(h, audit, 400, "invalid_request"));
                if (row == null) {
                    row = new() { TenantId = actor.TenantId, Id = command.Id, VesselId = vessel, AssignmentId = assignment, PersonId = owner.PersonId,
                        RequirementId = command.RequirementId, Kind = command.Kind };
                    db.CrewCompetenceRecords.Add(row);
                } else row.Revision = checked(row.Revision + 1);
                row.CompletedOn = command.CompletedOn; row.ValidUntil = command.ValidUntil; row.Reference = command.Reference;
                row.Provenance = "UserDeclaredUnverified"; row.ActorId = actor.Id; row.SavedAt = Now(h);
                await db.SaveChangesAsync(ct);
                return new(null, row.Id, row.Revision, row.Version);
            });

    private static Task<IResult> SignOff(Guid vessel, Guid assignment, Guid record, HttpContext h, PoseidonDbContext db) =>
        Command<CrewCompetenceSignOffCommand>(h, db, "CrewCompetenceCommand", "CrewCompetenceSignOff", vessel, CompetenceAssess, new { assignment, record },
            c => c.RecordRevision > 0 && c.ExpectedSequence >= 0 && c.AuthorityId != Guid.Empty && CrewCompetenceStorage.SignOffOutcomes.Contains(c.Outcome) && Text(c.Rationale, 2000, true),
            async (actor, command) => {
                var ct = h.RequestAborted; const string audit = "CrewCompetenceCommand"; var now = Now(h);
                var row = await db.CrewCompetenceRecords.AsNoTracking().SingleOrDefaultAsync(r => r.VesselId == vessel && r.AssignmentId == assignment && r.Id == record, ct);
                if (row == null) return new(await Fail(h, audit, 404, "resource_not_found"));
                if (row.Revision != command.RecordRevision) return new(await Fail(h, audit, 409, "version_conflict"));
                var grant = CrewAuthorityResolver.Resolve(Settings(h), actor, vessel, CrewAuthorityResolver.CompetenceAssessment, now).SingleOrDefault(g => g.Id == command.AuthorityId);
                if (grant == null) return new(await Fail(h, audit, 403, "human_authority_required"));
                if (row.ActorId == actor.Id) return new(await Fail(h, audit, 403, "separation_of_duties"));
                var sequence = await db.CrewCompetenceSignOffs.Where(s => s.VesselId == vessel && s.RecordId == record).MaxAsync(s => (long?)s.Sequence, ct) ?? 0;
                if (sequence != command.ExpectedSequence) return new(await Fail(h, audit, 409, "version_conflict"));
                var signOff = new CrewCompetenceSignOff { TenantId = actor.TenantId, Id = Guid.NewGuid(), VesselId = vessel, RecordId = record, RecordRevision = row.Revision,
                    Sequence = sequence + 1, Outcome = command.Outcome, ActorId = actor.Id, RoleId = grant.RoleId, AuthorityId = grant.Id, Duty = grant.Duty,
                    AuthorityHash = CrewAuthorityResolver.Hash(grant, actor), Rationale = command.Rationale, SavedAt = now };
                db.CrewCompetenceSignOffs.Add(signOff);
                await db.SaveChangesAsync(ct);
                return new(null, signOff.Id, signOff.Sequence, 0);
            });

    // ---- DEV-050: advisory Safe Manning evaluation and the human decision record ----
    private static SafeManningDecisionDto DecisionDto(CrewSafeManningDecision d) => new(d.Id, d.Sequence, d.Decision, d.EvaluationState, d.EvaluationHash,
        d.WindowFrom, d.WindowTo, d.ActorId, d.RoleId, d.Duty, d.SavedAt,
        (JsonSerializer.Deserialize<ManningReviewAcknowledgement[]>(d.AcknowledgementsJson, Json) ?? []).Select(a => a.ReasonCode).ToArray(),
        JsonSerializer.Deserialize<ManningDispensation[]>(d.DispensationsJson, Json) ?? []);
    private static readonly string[] ManningRules = ["SOLAS_V_14_MSMD_ISSUED_BY_FLAG_ADMINISTRATION", "A_1047_27_MANNED_ACCORDING_TO_MSMD",
        "STCW_ART_VIII_DISPENSATION_ONLY_BY_ADMINISTRATION", "NOT_ELIGIBLE_ACCEPTANCE_REQUIRES_DISPENSATION_RECORD", "REVIEW_REQUIRED_ACCEPTANCE_REQUIRES_PER_REASON_ACKNOWLEDGEMENT",
        "MASTER_CHIEF_ENGINEER_DISPENSATION_ONLY_FORCE_MAJEURE", "CREWING_DIFFICULTY_IS_NOT_A_GROUND", "DISPENSATION_LINK_IS_NOT_VERIFICATION"];
    private static async Task<ManningEvidenceOption[]> EvidenceOptions(PoseidonDbContext db, TrustedActor actor, Guid vessel, CancellationToken ct)
    {
        var options = new List<ManningEvidenceOption>();
        if (ControlEvidenceEndpoints.Read(actor))
            options.AddRange((await OperationEquipmentCommands.Visible(db, actor, db.ControlEvidenceRevisions.AsNoTracking()).Where(r => r.VesselId == vessel && r.Kind == "Evidence" &&
                    db.ControlEvidenceHeads.Any(x => x.VesselId == r.VesselId && x.Kind == r.Kind && x.Id == r.Id && x.Revision == r.Revision))
                .OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id).Take(25).Select(r => new { r.Id, r.Revision, r.Lifecycle }).ToArrayAsync(ct))
                .Select(r => new ManningEvidenceOption("EVIDENCE", r.Id, r.Revision, r.Lifecycle + " r" + r.Revision)));
        var isps = ControlledDocumentEndpoints.Isps(actor); // DEV-114: ISPS-sensitive documents stay hidden without document.isps.read.
        if (ControlledDocumentEndpoints.CanReadDocuments(actor))
            options.AddRange((await db.ControlledDocuments.AsNoTracking().Where(d => d.VesselId == vessel && d.CurrentRevision != null && d.DisposalState == "NONE" && (!d.IspsRestricted || isps))
                .OrderBy(d => d.Code).Take(25).Select(d => new { d.Id, d.Code, Revision = d.CurrentRevision!.Value }).ToArrayAsync(ct))
                .Select(d => new ManningEvidenceOption("CONTROLLED_DOCUMENT", d.Id, d.Revision, d.Code + " r" + d.Revision)));
        return options.ToArray();
    }
    // The pinned dispensation document must exist at the exact revision and be readable now.
    private static async Task<bool> EvidenceReadable(PoseidonDbContext db, TrustedActor actor, Guid vessel, ManningDispensation d, CancellationToken ct) => d.EvidenceKind switch {
        "EVIDENCE" => ControlEvidenceEndpoints.Read(actor) && await OperationEquipmentCommands.Visible(db, actor, db.ControlEvidenceRevisions.AsNoTracking())
            .AnyAsync(r => r.VesselId == vessel && r.Kind == "Evidence" && r.Id == d.EvidenceId && r.Revision == d.EvidenceRevision, ct),
        "CONTROLLED_DOCUMENT" => ControlledDocumentEndpoints.CanReadDocuments(actor) &&
            await db.ControlledDocuments.AsNoTracking().AnyAsync(x => x.VesselId == vessel && x.Id == d.EvidenceId && x.DisposalState == "NONE" && (!x.IspsRestricted || ControlledDocumentEndpoints.Isps(actor)), ct) &&
            await db.ControlledDocumentRevisions.AsNoTracking().AnyAsync(r => r.VesselId == vessel && r.DocumentId == d.EvidenceId && r.Revision == d.EvidenceRevision, ct),
        _ => false
    };

    private static async Task<IResult> Evaluate(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var ct = h.RequestAborted; var actor = Actor(h);
        if (!ManningRead(actor)) return await Fail(h, "SafeManningRead", 403, "permission_denied");
        if (!await InScope(db, actor!, vessel, ct)) return await Fail(h, "SafeManningRead", 404, "resource_not_found");
        if (!Window(h, [], out var window, out _) || !window.Valid) return await Fail(h, "SafeManningRead", 400, "evaluation_window_invalid");
        var evaluation = await h.RequestServices.GetRequiredService<ICrewEligibilityEvaluator>().EvaluateVesselAsync(actor!, vessel, window, ct);
        var latest = await db.CrewSafeManningDecisions.AsNoTracking().Where(d => d.VesselId == vessel).OrderByDescending(d => d.Sequence).FirstOrDefaultAsync(ct);
        var authorities = ManningDecide(actor) ? CrewAuthorityResolver.Resolve(Settings(h), actor, vessel, CrewAuthorityResolver.SafeManningDecision, Now(h)) : [];
        await Audit(h, "SafeManningRead", "Success", vessel);
        var evidence = authorities.Length > 0 && evaluation.State == CrewEligibilityStates.NotEligible ? await EvidenceOptions(db, actor!, vessel, ct) : [];
        return Results.Ok(new SafeManningView(evaluation, CrewAuthorityResolver.Dto(authorities), authorities.Length > 0, latest?.Sequence ?? 0,
            latest == null ? null : DecisionDto(latest), CrewCompetenceStorage.Decisions, SafeManningDecisionPolicy.ReviewReasonCodes(evaluation),
            SafeManningDecisionPolicy.NotEligibleMembers(evaluation).Select(m => new ManningDispensationMember(m.AssignmentId, m.PositionId)).ToArray(),
            evidence, SafeManningDecisionPolicy.Posts, ManningRules));
    }

    private static async Task<IResult> Decisions(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var ct = h.RequestAborted; var actor = Actor(h);
        if (!ManningRead(actor)) return await Fail(h, "SafeManningRead", 403, "permission_denied");
        if (!await InScope(db, actor!, vessel, ct)) return await Fail(h, "SafeManningRead", 404, "resource_not_found");
        if (h.Request.Query.Any(q => q.Key != "offset") || !Window(h, ["offset"], out _, out var offset)) return await Fail(h, "SafeManningRead", 400, "invalid_query");
        var rows = await db.CrewSafeManningDecisions.AsNoTracking().Where(d => d.VesselId == vessel).OrderByDescending(d => d.Sequence).Skip(offset).Take(26).ToArrayAsync(ct);
        await Audit(h, "SafeManningRead", "Success", vessel);
        return Results.Ok(new SafeManningDecisionPage(rows.Take(25).Select(DecisionDto).ToArray(), offset, rows.Length > 25));
    }

    private static Task<IResult> RecordDecision(Guid vessel, HttpContext h, PoseidonDbContext db) =>
        Command<SafeManningDecisionCommand>(h, db, "SafeManningCommand", "SafeManningDecisionRecord", vessel, ManningDecide, new { },
            c => new CrewEligibilityWindow(c.From, c.To).Valid && c.EvaluationHash is { Length: 64 } && c.EvaluationHash.All(char.IsAsciiHexDigit) &&
                c.ExpectedSequence >= 0 && c.AuthorityId != Guid.Empty && CrewCompetenceStorage.Decisions.Contains(c.Decision) && Text(c.Rationale, 2000, true) &&
                (c.Acknowledgements?.Length ?? 0) <= 64 && (c.Dispensations?.Length ?? 0) <= 64 &&
                (c.Acknowledgements ?? []).All(a => a != null && a.ReasonCode != null && Code(a.ReasonCode, 64)) && (c.Dispensations ?? []).All(d => d != null && d.Post != null && d.EvidenceKind != null),
            async (actor, command) => {
                var ct = h.RequestAborted; const string audit = "SafeManningCommand"; var now = Now(h);
                var grant = CrewAuthorityResolver.Resolve(Settings(h), actor, vessel, CrewAuthorityResolver.SafeManningDecision, now).SingleOrDefault(g => g.Id == command.AuthorityId);
                if (grant == null) return new(await Fail(h, audit, 403, "human_authority_required"));
                // The decision is taken against the exact advisory outcome the human saw.
                var window = new CrewEligibilityWindow(command.From, command.To);
                var evaluation = await h.RequestServices.GetRequiredService<ICrewEligibilityEvaluator>().EvaluateVesselAsync(actor, vessel, window, ct);
                if (!string.Equals(evaluation.Hash, command.EvaluationHash, StringComparison.OrdinalIgnoreCase)) return new(await Fail(h, audit, 409, "evaluation_stale"));
                var sequence = await db.CrewSafeManningDecisions.Where(d => d.VesselId == vessel).MaxAsync(d => (long?)d.Sequence, ct) ?? 0;
                if (sequence != command.ExpectedSequence) return new(await Fail(h, audit, 409, "version_conflict"));
                // SOLAS V/14 + A.1047(27) + STCW Art. VIII: NOT_ELIGIBLE needs a linked flag-Administration
                // dispensation per negative; REVIEW_REQUIRED needs a per-reason onboard acknowledgement.
                ManningReviewAcknowledgement[] acknowledgements = command.Acknowledgements ?? [];
                ManningDispensation[] dispensations = command.Dispensations ?? [];
                var positionIds = dispensations.Select(d => d.PositionId).Distinct().ToArray();
                var positionText = (await db.CrewPositions.AsNoTracking().Where(p => p.VesselId == vessel && positionIds.Contains(p.Id)).ToArrayAsync(ct))
                    .ToDictionary(p => p.Id, p => string.Join(' ', new[] { p.Code, p.Rank, p.Capacity }.Where(x => x != null)));
                var refusal = SafeManningDecisionPolicy.Refusal(evaluation, command.Decision, acknowledgements, dispensations, id => positionText.GetValueOrDefault(id));
                if (refusal == "validation_failed") return new(await Fail(h, audit, 400, "validation_failed"));
                if (refusal != null) return new(await Fail(h, audit, 422, refusal));
                foreach (var dispensation in dispensations)
                    if (!await EvidenceReadable(db, actor, vessel, dispensation, ct)) return new(await Fail(h, audit, 422, SafeManningDecisionPolicy.DispensationRequired));
                var row = new CrewSafeManningDecision { TenantId = actor.TenantId, Id = Guid.NewGuid(), VesselId = vessel, Sequence = sequence + 1, Decision = command.Decision,
                    EvaluationState = evaluation.State, EvaluationHash = evaluation.Hash, WindowFrom = command.From, WindowTo = command.To, ActorId = actor.Id,
                    RoleId = grant.RoleId, AuthorityId = grant.Id, Duty = grant.Duty, AuthorityHash = CrewAuthorityResolver.Hash(grant, actor),
                    Rationale = command.Rationale, SavedAt = now, AcknowledgementCount = acknowledgements.Length, DispensationCount = dispensations.Length,
                    AcknowledgementsJson = JsonSerializer.Serialize(acknowledgements.OrderBy(a => a.ReasonCode, StringComparer.Ordinal), Json),
                    DispensationsJson = JsonSerializer.Serialize(dispensations.OrderBy(d => d.AssignmentId), Json) };
                db.CrewSafeManningDecisions.Add(row);
                await db.SaveChangesAsync(ct);
                return new(null, row.Id, row.Sequence, 0);
            });
}
