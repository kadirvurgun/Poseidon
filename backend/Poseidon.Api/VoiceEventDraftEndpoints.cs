using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VoiceDraftCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string Transcript, [property: JsonRequired] string Origin,
    [property: JsonRequired] DateOnly ReferenceDate, string? SpeechAttestation)
{ public override string ToString() => nameof(VoiceDraftCreateCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VoiceDraftUpdateCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] VoiceDraftFields Fields,
    [property: JsonRequired] Guid[] EvidenceFileIds)
{ public override string ToString() => nameof(VoiceDraftUpdateCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VoiceDraftConfirmCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VoiceDraftRejectCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ReasonCode, string? Note)
{ public override string ToString() => nameof(VoiceDraftRejectCommand); }

public sealed record VoiceDraftEvidencePin(Guid Id, long Revision, string Checksum);
public sealed record VoiceDraftEvidenceDto(Guid Id, long Revision, string Currency, string State);
public sealed record VoiceDraftSpeechDto(string EngineVersion, string ModelVersion, decimal? MeanConfidence);
public sealed record VoiceDraftRejectionDto(string ReasonCode, string? Note, Guid RejectedBy, DateTimeOffset RejectedAt);
public sealed record VoiceDraftRevisionSummaryDto(long Revision, string State, string ReasonCode, string Hash, Guid ActorId, DateTimeOffset SavedAt);
public sealed record VoiceDraftDto(Guid Id, Guid VesselId, long Revision, uint Version, string State, string Origin, string Transcript, string TranscriptSha256,
    DateOnly ReferenceDate, VoiceDraftSpeechDto? Speech, string ExtractorVersion, string RuleSetVersion, VoiceDraftCandidate Candidate, VoiceDraftFields Fields,
    Dictionary<string, string> FieldOrigins, VoiceDraftEvidenceDto[] Evidence, string[] Missing, string Hash, VoiceDraftFieldCorrection[]? Corrections,
    Guid? ConfirmedBy, DateTimeOffset? ConfirmedAt, VoiceDraftRejectionDto? Rejection, VoiceDraftRevisionSummaryDto[] Revisions, Guid CreatedBy,
    bool CanEdit, bool CanConfirm, string Handoff, string AudioRetention, string Provenance, DateTimeOffset SavedAt, Guid? SafetyEventId = null, string? HandoffReason = null);
public sealed record VoiceDraftListItemDto(Guid Id, string State, string Origin, string? EventType, string? Location, string? OccurredAt, int MissingCount, long Revision, DateTimeOffset SavedAt);
public sealed record VoiceDraftPageDto(VoiceDraftListItemDto[] Items, int Offset, int Limit, bool HasMore, bool CanWrite);
public sealed record VoiceDraftCapabilitiesDto(Guid VesselId, bool CanRead, bool CanWrite, bool CanConfirm, bool SpeechAvailable, string? SpeechUnavailableReason,
    string? SpeechEngineVersion, int MaxAudioBytes, string AudioRetention, string ExtractorVersion, string RuleSetVersion, string[] EventTypes, string[] Locations,
    string[] RejectionReasons, string[] RequiredFields, bool CanAttachEvidence);
public sealed record VoiceDraftTranscriptionDto(string Transcript, string TranscriptSha256, string EngineVersion, string ModelVersion, decimal? MeanConfidence,
    SpeechSegment[] Segments, string Attestation, DateTimeOffset ExpiresAt, bool AudioRetained);
public sealed record VoiceDraftMetricsDto(Guid VesselId, string State, int Confirmed, int Rejected, int Open, decimal? FieldCompleteness, decimal? CorrectionRate,
    decimal? Usability, decimal? RejectionRate, Dictionary<string, int> RejectionsByReason, int SampleLimit, string ExtractorVersion);

// DEV-062 / T-S9-005 and DEV-063 / T-S9-006. Vessel-scoped voice-to-event draft candidates
// (NATIVE-COMMAND-1): server authorization, exact ?vesselId scope, CSRF, Idempotency-Key receipt,
// strict JSON, one SERIALIZABLE transaction through the audited SaveChanges path. Confirm and
// reject are human records. Nothing here submits, triages or closes a safety event. S9 integration: a confirmation by
// a human who also holds hse.safety_event.report creates a NAMED safety event DRAFT (reported by that human) through
// the DEV-060 command path in the same transaction (Handoff=EVENT_DRAFT_CREATED); otherwise the candidate stays
// PENDING_EVENT_DRAFT_INTEGRATION with the closed reason SAFETY_EVENT_REPORT_PERMISSION_REQUIRED.
public static class VoiceEventDraftEndpoints
{
    private const string ReadAction = "VoiceDraftRead", CommandAction = "VoiceDraftCommand", TranscriptionAction = "VoiceDraftTranscription";
    private static readonly JsonSerializerOptions Json = TechnicalRecordCommands.Json;
    private const int MaxBody = 65536, SampleLimit = 1000;
    internal static bool CanRead(TrustedActor? a) => a?.CanRead == true && a.Permissions.Contains(VoiceEventDraftPolicy.ReadPermission);
    internal static bool CanWrite(TrustedActor? a, Guid vessel) => CanRead(a) && a!.HasWriteAttribution && a.CanEditVessel(vessel) && a.Permissions.Contains(VoiceEventDraftPolicy.WritePermission);

    public static void MapVoiceEventDrafts(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/voice-event-drafts").RequireAuthorization("DirectoryRead");
        g.MapGet("", (HttpContext h, PoseidonDbContext db) => List(h, db)).WithName("VoiceDraftList").Produces<VoiceDraftPageDto>();
        g.MapGet("/capabilities", (HttpContext h, PoseidonDbContext db) => Capabilities(h, db)).WithName("VoiceDraftCapabilities").Produces<VoiceDraftCapabilitiesDto>();
        g.MapGet("/metrics", (HttpContext h, PoseidonDbContext db) => Metrics(h, db)).WithName("VoiceDraftMetrics").Produces<VoiceDraftMetricsDto>();
        g.MapGet("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => Detail(id, h, db)).WithName("VoiceDraftRead").Produces<VoiceDraftDto>();
        g.MapPost("/transcriptions", (HttpContext h, PoseidonDbContext db) => Transcribe(h, db)).WithName("VoiceDraftTranscribe")
            .Accepts<byte[]>("audio/webm", "audio/ogg", "audio/wav", "audio/mp4").Produces<VoiceDraftTranscriptionDto>();
        g.MapPost("", (HttpContext h, PoseidonDbContext db) => Run<VoiceDraftCreateCommand>(h, db, "VoiceDraftCreate", null, c => c.Id, Create))
            .WithName("VoiceDraftCreate").Accepts<VoiceDraftCreateCommand>("application/json").Produces<VoiceDraftDto>();
        g.MapPut("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => Run<VoiceDraftUpdateCommand>(h, db, "VoiceDraftUpdate", id, _ => id, Update))
            .WithName("VoiceDraftUpdate").Accepts<VoiceDraftUpdateCommand>("application/json").Produces<VoiceDraftDto>();
        g.MapPost("/{id:guid}/confirm", (Guid id, HttpContext h, PoseidonDbContext db) => Run<VoiceDraftConfirmCommand>(h, db, "VoiceDraftConfirm", id, _ => id, Confirm, humanOnly: true))
            .WithName("VoiceDraftConfirm").Accepts<VoiceDraftConfirmCommand>("application/json").Produces<VoiceDraftDto>().Produces<TechnicalRecordFailure>(422);
        g.MapPost("/{id:guid}/reject", (Guid id, HttpContext h, PoseidonDbContext db) => Run<VoiceDraftRejectCommand>(h, db, "VoiceDraftReject", id, _ => id, Reject, humanOnly: true))
            .WithName("VoiceDraftReject").Accepts<VoiceDraftRejectCommand>("application/json").Produces<VoiceDraftDto>();
    }

    private static VoiceDraftCandidate Candidate(string json) => JsonSerializer.Deserialize<VoiceDraftCandidate>(json, Json) ?? throw new InvalidDataException("voice_candidate_unavailable");
    private static VoiceDraftFields Fields(string json) => JsonSerializer.Deserialize<VoiceDraftFields>(json, Json) ?? throw new InvalidDataException("voice_fields_unavailable");
    private static VoiceDraftEvidencePin[] Evidence(string json) => JsonSerializer.Deserialize<VoiceDraftEvidencePin[]>(json, Json) ?? [];
    private static string Hash(VoiceEventDraft d) => RegisterStorage.Hash(JsonSerializer.Serialize(new { d.VesselId, d.Id, d.Revision, d.State, d.TranscriptSha256,
        fields = d.FieldsJson, evidence = d.EvidenceJson }, Json));
    private static void Stamp(VoiceEventDraft d, Guid actor, DateTimeOffset now) { d.ActorId = actor; d.SavedAt = now; d.Hash = Hash(d); }
    private static void Append(PoseidonDbContext db, VoiceEventDraft d, string reason, DateTimeOffset now) =>
        db.VoiceEventDraftRevisions.Add(new() { TenantId = d.TenantId, VesselId = d.VesselId, DraftId = d.Id, Revision = d.Revision, State = d.State, ReasonCode = reason,
            FieldsJson = d.FieldsJson, EvidenceJson = d.EvidenceJson, Hash = d.Hash, ActorId = d.ActorId, SavedAt = now });

    private static async Task<TechnicalOutcome> Create(TechnicalContext<VoiceDraftCreateCommand> c, HttpContext h)
    {
        var cmd = c.Command;
        if (cmd.Id == Guid.Empty || !VoiceEventDraftPolicy.ValidTranscript(cmd.Transcript) || !VoiceEventDraftPolicy.Origins.Contains(cmd.Origin, StringComparer.Ordinal) ||
            cmd.ReferenceDate < DateOnly.FromDateTime(c.Now.UtcDateTime).AddDays(-30) || cmd.ReferenceDate > DateOnly.FromDateTime(c.Now.UtcDateTime).AddDays(1) ||
            cmd.Origin == "TYPED" && cmd.SpeechAttestation != null) return TechnicalOutcome.Fail(400, "invalid_request");
        var sha = SpeechAttestations.Sha256(cmd.Transcript);
        (string Engine, string Model, decimal? Confidence)? speech = null;
        if (cmd.Origin == "LOCAL_SPEECH")
        {
            // Speech provenance only from a server-issued attestation over this exact transcript.
            speech = h.RequestServices.GetRequiredService<SpeechAttestations>().Read(cmd.SpeechAttestation, c.Actor.TenantId, c.Actor.Id, c.Vessel, sha, c.Now);
            if (speech == null) return TechnicalOutcome.Fail(400, "speech_attestation_invalid");
        }
        if (await c.Db.VoiceEventDrafts.AnyAsync(d => d.Id == cmd.Id, c.Ct)) return TechnicalOutcome.Fail(409, "version_conflict");
        var candidate = VoiceEventDraftPolicy.Extract(cmd.Transcript, sha, cmd.ReferenceDate);
        var row = new VoiceEventDraft { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, Revision = 1, State = VoiceEventDraftPolicy.Draft, TranscriptOrigin = cmd.Origin,
            Transcript = cmd.Transcript, TranscriptSha256 = sha, ReferenceDate = cmd.ReferenceDate, SpeechEngineVersion = speech?.Engine, SpeechModelVersion = speech?.Model,
            SpeechConfidence = speech?.Confidence, ExtractorVersion = VoiceEventDraftPolicy.ExtractorVersion, RuleSetVersion = VoiceEventDraftPolicy.RuleSetVersion,
            CandidateJson = JsonSerializer.Serialize(candidate, Json), FieldsJson = JsonSerializer.Serialize(VoiceEventDraftPolicy.FromCandidate(candidate), Json), CreatedBy = c.Actor.Id };
        Stamp(row, c.Actor.Id, c.Now); c.Db.VoiceEventDrafts.Add(row); await c.Db.SaveChangesAsync(c.Ct);
        Append(c.Db, row, "CREATE", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    private static Task<VoiceEventDraft?> Head<T>(TechnicalContext<T> c) => c.Db.VoiceEventDrafts.SingleOrDefaultAsync(d => d.VesselId == c.Vessel && d.Id == c.RouteId, c.Ct);

    private static async Task<TechnicalOutcome> Update(TechnicalContext<VoiceDraftUpdateCommand> c, HttpContext h)
    {
        var cmd = c.Command;
        if (!VoiceEventDraftPolicy.Valid(cmd.Fields) || cmd.EvidenceFileIds == null || cmd.EvidenceFileIds.Length > VoiceEventDraftPolicy.MaxEvidence ||
            cmd.EvidenceFileIds.Distinct().Count() != cmd.EvidenceFileIds.Length || cmd.EvidenceFileIds.Contains(Guid.Empty)) return TechnicalOutcome.Fail(400, "invalid_request");
        var row = await Head(c); if (row == null) return TechnicalOutcome.Fail(404, "resource_not_found");
        if (row.Version != cmd.ExpectedVersion) return TechnicalOutcome.Fail(409, "version_conflict");
        if (row.State != VoiceEventDraftPolicy.Draft) return TechnicalOutcome.Fail(409, "invalid_state");
        // Evidence pins accepted clean vessel transports (never verified evidence); unchanged pins are kept.
        var existing = Evidence(row.EvidenceJson); var pins = new List<VoiceDraftEvidencePin>();
        foreach (var id in cmd.EvidenceFileIds)
        {
            if (existing.SingleOrDefault(p => p.Id == id) is { } kept) { pins.Add(kept); continue; }
            if (!c.Actor.Permissions.Contains("file.read")) return TechnicalOutcome.Fail(400, "linked_resource_unavailable");
            var file = await c.Db.StoredFiles.AsNoTracking().Where(f => f.Id == id && f.VesselId == c.Vessel && c.Db.FileStages.Any(s => s.Id == f.Id && s.VesselId == c.Vessel &&
                s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport")).Select(f => new { f.Revision, f.Checksum }).SingleOrDefaultAsync(c.Ct);
            if (file == null) return TechnicalOutcome.Fail(400, "linked_resource_unavailable");
            pins.Add(new(id, file.Revision, file.Checksum));
        }
        row.Revision++; row.FieldsJson = JsonSerializer.Serialize(cmd.Fields, Json); row.EvidenceJson = JsonSerializer.Serialize(pins, Json); Stamp(row, c.Actor.Id, c.Now);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "EDIT", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<TechnicalOutcome> Confirm(TechnicalContext<VoiceDraftConfirmCommand> c, HttpContext h)
    {
        var row = await Head(c); if (row == null) return TechnicalOutcome.Fail(404, "resource_not_found");
        if (row.Version != c.Command.ExpectedVersion || row.Hash != c.Command.ContentHash) return TechnicalOutcome.Fail(409, "version_conflict");
        if (row.State != VoiceEventDraftPolicy.Draft) return TechnicalOutcome.Fail(409, "invalid_state");
        var fields = Fields(row.FieldsJson);
        var missing = VoiceEventDraftPolicy.Missing(fields);
        if (missing.Length > 0) return TechnicalOutcome.Fail(422, "voice_draft_incomplete", missing);
        // The safety event DRAFT is written first (its own audited SaveChanges in this SERIALIZABLE transaction) so the
        // voice draft changes exactly once, DRAFT -> CONFIRMED, together with its link or pending reason.
        Guid? eventId = null;
        if (SafetyEventAccess.Reporter(c.Actor))
        {
            // The confirming human is the NAMED reporter; the event stays an incomplete DRAFT until that human submits it.
            eventId = Guid.NewGuid();
            var created = await SafetyEventEndpoints.CreateDraft(c.Db, c.Actor, c.Vessel, eventId.Value, SafetyEventPolicy.Named,
                VoiceSafetyEventHandoff.Map(fields, c.Now), null, "ONLINE", c.Now, c.Ct);
            if (created.Status != 200) return TechnicalOutcome.Fail(created.Status, created.Code!, created.Missing);
        }
        row.Revision++; row.State = VoiceEventDraftPolicy.Confirmed; row.ConfirmedBy = c.Actor.Id; row.ConfirmedAt = c.Now;
        row.SafetyEventId = eventId; row.HandoffReason = eventId == null ? VoiceSafetyEventHandoff.ReportPermissionRequired : null;
        row.CorrectionsJson = JsonSerializer.Serialize(VoiceEventDraftPolicy.Corrections(Candidate(row.CandidateJson), fields), Json); Stamp(row, c.Actor.Id, c.Now);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "CONFIRM", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<TechnicalOutcome> Reject(TechnicalContext<VoiceDraftRejectCommand> c, HttpContext h)
    {
        var cmd = c.Command;
        if (!VoiceEventDraftPolicy.RejectionReasons.Contains(cmd.ReasonCode, StringComparer.Ordinal) || !VoiceEventDraftPolicy.ValidText(cmd.Note, VoiceEventDraftPolicy.MaxNote) ||
            cmd.ReasonCode == "OTHER" && cmd.Note == null) return TechnicalOutcome.Fail(400, "invalid_request");
        var row = await Head(c); if (row == null) return TechnicalOutcome.Fail(404, "resource_not_found");
        if (row.Version != cmd.ExpectedVersion) return TechnicalOutcome.Fail(409, "version_conflict");
        if (row.State != VoiceEventDraftPolicy.Draft) return TechnicalOutcome.Fail(409, "invalid_state");
        row.Revision++; row.State = VoiceEventDraftPolicy.Rejected; row.RejectedBy = c.Actor.Id; row.RejectedAt = c.Now; row.RejectionReason = cmd.ReasonCode; row.RejectionNote = cmd.Note;
        Stamp(row, c.Actor.Id, c.Now);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "REJECT", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    internal static async Task<VoiceDraftDto?> Describe(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid id, bool human, CancellationToken ct)
    {
        var d = await db.VoiceEventDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
        if (d == null) return null;
        var candidate = Candidate(d.CandidateJson); var fields = Fields(d.FieldsJson);
        var evidence = new List<VoiceDraftEvidenceDto>();
        foreach (var p in Evidence(d.EvidenceJson))
        {
            var current = actor.Permissions.Contains("file.read") && await db.StoredFiles.AsNoTracking().AnyAsync(f => f.Id == p.Id && f.VesselId == vessel && f.Revision == p.Revision && f.Checksum == p.Checksum, ct);
            evidence.Add(new(p.Id, p.Revision, current ? "CURRENT" : "UNAVAILABLE", "TRANSPORT_ACCEPTED_UNVERIFIED"));
        }
        var revisions = await db.VoiceEventDraftRevisions.AsNoTracking().Where(v => v.VesselId == vessel && v.DraftId == id).OrderByDescending(v => v.Revision).Take(50)
            .Select(v => new VoiceDraftRevisionSummaryDto(v.Revision, v.State, v.ReasonCode, v.Hash, v.ActorId, v.SavedAt)).ToArrayAsync(ct);
        var missing = VoiceEventDraftPolicy.Missing(fields);
        var editable = d.State == VoiceEventDraftPolicy.Draft && CanWrite(actor, vessel);
        return new(d.Id, d.VesselId, d.Revision, d.Version, d.State, d.TranscriptOrigin, d.Transcript, d.TranscriptSha256, d.ReferenceDate,
            d.SpeechEngineVersion == null ? null : new(d.SpeechEngineVersion, d.SpeechModelVersion!, d.SpeechConfidence), d.ExtractorVersion, d.RuleSetVersion, candidate, fields,
            VoiceEventDraftPolicy.FieldKeys.ToDictionary(k => k, k => VoiceEventDraftPolicy.Origin(candidate, fields, k)), [.. evidence], missing, d.Hash,
            d.CorrectionsJson == null ? null : JsonSerializer.Deserialize<VoiceDraftFieldCorrection[]>(d.CorrectionsJson, Json), d.ConfirmedBy, d.ConfirmedAt,
            d.RejectionReason == null ? null : new(d.RejectionReason, d.RejectionNote, d.RejectedBy!.Value, d.RejectedAt!.Value), revisions, d.CreatedBy,
            editable, editable && human && missing.Length == 0,
            VoiceSafetyEventHandoff.State(d.State, d.SafetyEventId), d.AudioRetention, d.Provenance, d.SavedAt, d.SafetyEventId, d.HandoffReason);
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead, "offset", "state"); if (failure != null) return failure;
        var offset = 0; string? state = null;
        if (h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 100000))
            return await TechnicalRecordCommands.Fail(h, ReadAction, 400, "invalid_query");
        if (h.Request.Query.ContainsKey("state")) { state = h.Request.Query["state"]; if (!VoiceEventDraftPolicy.States.Contains(state, StringComparer.Ordinal)) return await TechnicalRecordCommands.Fail(h, ReadAction, 400, "invalid_query"); }
        var rows = await db.VoiceEventDrafts.AsNoTracking().Where(d => d.VesselId == vessel && (state == null || d.State == state)).OrderByDescending(d => d.SavedAt).ThenBy(d => d.Id)
            .Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        var items = rows.Take(25).Select(d => { var f = Fields(d.FieldsJson);
            return new VoiceDraftListItemDto(d.Id, d.State, d.TranscriptOrigin, f.EventType, f.Location, f.OccurredAt, VoiceEventDraftPolicy.Missing(f).Length, d.Revision, d.SavedAt); }).ToArray();
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", vessel);
        return Results.Json(new VoiceDraftPageDto(items, offset, 25, rows.Length > 25, CanWrite(actor, vessel)), Json);
    }

    private static (bool Available, string? Reason, string? Engine) Speech(HttpContext h)
    {
        var settings = h.RequestServices.GetRequiredService<SpeechTranscriptionSettings>(); var provider = h.RequestServices.GetRequiredService<ISpeechTranscriptionProvider>();
        if (!settings.Configured) return (false, "LOCAL_ENGINE_NOT_CONFIGURED", null);
        if (!provider.Available || provider.EngineVersion != settings.EngineVersion) return (false, "LOCAL_ENGINE_NOT_INSTALLED", null);
        return (true, null, settings.EngineVersion);
    }

    private static async Task<IResult> Capabilities(HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead); if (failure != null) return failure;
        var speech = Speech(h);
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", vessel);
        return Results.Json(new VoiceDraftCapabilitiesDto(vessel, true, CanWrite(actor, vessel), CanWrite(actor, vessel) && CertificateRenewalEndpoints.Human(h, actor!),
            speech.Available, speech.Reason, speech.Engine, h.RequestServices.GetRequiredService<SpeechTranscriptionSettings>().MaxBytes, "NOT_RETAINED",
            VoiceEventDraftPolicy.ExtractorVersion, VoiceEventDraftPolicy.RuleSetVersion, VoiceEventDraftPolicy.EventTypes, VoiceEventDraftPolicy.Locations,
            VoiceEventDraftPolicy.RejectionReasons, VoiceEventDraftPolicy.RequiredKeys, actor!.Permissions.Contains("file.read")), Json);
    }

    // DEV-063: operational observation over the latest persisted confirmations and rejections.
    private static async Task<IResult> Metrics(HttpContext h, PoseidonDbContext db)
    {
        var (_, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead); if (failure != null) return failure;
        var rows = await db.VoiceEventDrafts.AsNoTracking().Where(d => d.VesselId == vessel).OrderByDescending(d => d.SavedAt).ThenBy(d => d.Id).Take(SampleLimit)
            .Select(d => new { d.State, d.CorrectionsJson, d.RejectionReason }).ToArrayAsync(h.RequestAborted);
        var observed = VoiceDraftGoldenEvaluation.Observe(
            rows.Where(r => r.State == VoiceEventDraftPolicy.Confirmed).Select(r => JsonSerializer.Deserialize<VoiceDraftFieldCorrection[]>(r.CorrectionsJson!, Json) ?? []).ToArray(),
            rows.Where(r => r.State == VoiceEventDraftPolicy.Rejected).Select(r => r.RejectionReason!).ToArray(), rows.Count(r => r.State == VoiceEventDraftPolicy.Draft));
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", vessel);
        return Results.Json(new VoiceDraftMetricsDto(vessel, "OPERATIONAL_OBSERVATION_NOT_ACCURACY_CLAIM", observed.Confirmed, observed.Rejected, observed.Open,
            observed.FieldCompleteness, observed.CorrectionRate, observed.Usability, observed.RejectionRate, observed.RejectionsByReason, SampleLimit, VoiceEventDraftPolicy.ExtractorVersion), Json);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead); if (failure != null) return failure;
        var dto = await Describe(db, actor!, vessel, id, CertificateRenewalEndpoints.Human(h, actor!), h.RequestAborted);
        if (dto == null) return await TechnicalRecordCommands.Fail(h, ReadAction, 404, "resource_not_found", id);
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", id);
        return Results.Json(dto, Json);
    }

    // Stateless local transcription: bytes are buffered in memory, transcribed in-process and discarded.
    private static async Task<IResult> Transcribe(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (!TechnicalRecordCommands.Query(h, out var vessel)) return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 400, "invalid_query");
        var actor = TechnicalRecordCommands.Actor(h);
        if (actor == null || db.TenantId != actor.TenantId || !CanWrite(actor, vessel)) return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 403, "permission_denied");
        if (!await TechnicalRecordCommands.Vessels(db, actor, vessel).AnyAsync(ct)) return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 403, "csrf_invalid"); }
        var speech = Speech(h);
        if (!speech.Available) return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 503, "speech_capability_unavailable");
        var settings = h.RequestServices.GetRequiredService<SpeechTranscriptionSettings>();
        var media = h.Request.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
        if (media is not ("audio/webm" or "audio/ogg" or "audio/wav" or "audio/mp4") || h.Request.ContentLength is not > 0 || h.Request.ContentLength > settings.MaxBytes)
            return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 400, "speech_input_invalid");
        byte[] audio;
        try { using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, settings.MaxBytes, ct); audio = buffer.ToArray(); }
        catch (InvalidDataException) { return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 400, "speech_input_invalid"); }
        SpeechTranscript result;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            try { result = await h.RequestServices.GetRequiredService<ISpeechTranscriptionProvider>().TranscribeAsync(audio, media, timeout.Token); }
            catch (Exception e) when (e is SpeechCapabilityUnavailableException || e is OperationCanceledException && !ct.IsCancellationRequested)
            { return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 503, "speech_capability_unavailable"); }
            finally { Array.Clear(audio); }
        }
        if (!VoiceEventDraftPolicy.ValidTranscript(result.Text) || result.EngineVersion != settings.EngineVersion || result.ModelVersion != settings.ModelVersion ||
            result.MeanConfidence is < 0 or > 1 || result.Segments.Length > 1000 || result.Segments.Any(s => s.Start < 0 || s.End > result.Text.Length || s.End < s.Start || s.Confidence is < 0 or > 1))
            return await TechnicalRecordCommands.Fail(h, TranscriptionAction, 503, "speech_capability_unavailable");
        var sha = SpeechAttestations.Sha256(result.Text); var now = TechnicalRecordCommands.Now(h); var expires = now.AddMinutes(30);
        var token = h.RequestServices.GetRequiredService<SpeechAttestations>().Issue(actor.TenantId, actor.Id, vessel, sha, result.EngineVersion, result.ModelVersion, result.MeanConfidence, expires);
        await TechnicalRecordCommands.Audit(h, TranscriptionAction, "Success", vessel);
        return Results.Json(new VoiceDraftTranscriptionDto(result.Text, sha, result.EngineVersion, result.ModelVersion, result.MeanConfidence, result.Segments, token, expires, false), Json);
    }

    // NATIVE-COMMAND-1 transport over the voice draft receipt table (same contract as TechnicalRecordCommands.Run).
    private static async Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string operation, Guid? routeId, Func<T, Guid?> resourceOf,
        Func<TechnicalContext<T>, HttpContext, Task<TechnicalOutcome>> apply, bool humanOnly = false) where T : class
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (!TechnicalRecordCommands.Query(h, out var vessel)) return await TechnicalRecordCommands.Fail(h, CommandAction, 400, "invalid_query");
        var actor = TechnicalRecordCommands.Actor(h);
        if (actor == null || !actor.HasWriteAttribution || db.TenantId != actor.TenantId || !CanWrite(actor, vessel)) return await TechnicalRecordCommands.Fail(h, CommandAction, 403, "permission_denied", routeId);
        if (humanOnly && !CertificateRenewalEndpoints.Human(h, actor)) return await TechnicalRecordCommands.Fail(h, CommandAction, 403, "human_actor_required", routeId);
        if (!await TechnicalRecordCommands.Vessels(db, actor, vessel).AnyAsync(ct)) return await TechnicalRecordCommands.Fail(h, CommandAction, 404, "resource_not_found", routeId);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await TechnicalRecordCommands.Fail(h, CommandAction, 403, "csrf_invalid", routeId); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await TechnicalRecordCommands.Fail(h, CommandAction, 400, "invalid_idempotency_key", routeId);
        var key = parsedKey.ToString("D");
        T command;
        try
        {
            if (!h.Request.HasJsonContentType() || h.Request.ContentLength > MaxBody) throw new JsonException();
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, MaxBody, ct);
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command = document.RootElement.Deserialize<T>(Json) ?? throw new JsonException();
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException or ArgumentException)
        { return await TechnicalRecordCommands.Fail(h, CommandAction, 400, "invalid_request", routeId); }
        var resource = resourceOf(command) ?? routeId ?? Guid.Empty;
        if (resource == Guid.Empty) return await TechnicalRecordCommands.Fail(h, CommandAction, 400, "invalid_request");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation, vessel, routeId, command }, Json));
        async Task<IResult> Fresh(Guid id)
        {
            var live = TechnicalRecordCommands.Actor(h);
            if (live == null || live.Id != actor.Id || !CanWrite(live, vessel)) return await TechnicalRecordCommands.Fail(h, CommandAction, 403, "permission_denied", id);
            db.ChangeTracker.Clear();
            var value = await Describe(db, live, vessel, id, CertificateRenewalEndpoints.Human(h, live), ct);
            if (value == null) return await TechnicalRecordCommands.Fail(h, CommandAction, 503, "command_outcome_withheld", id);
            await TechnicalRecordCommands.Audit(h, CommandAction, "Success", id);
            return Results.Json(value, Json);
        }
        await TechnicalRecordCommands.Audit(h, CommandAction, "Attempted", resource);
        Guid? committed = null; TechnicalOutcome? failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                var receipt = await db.VoiceEventDraftReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Action != operation || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel)
                        return await TechnicalRecordCommands.Fail(h, CommandAction, 409, "idempotency_key_reused", resource);
                    return await Fresh(receipt.ResourceId);
                }
                var outcome = await apply(new(actor, vessel, routeId, null, command, TechnicalRecordCommands.Now(h), db, ct), h);
                if (outcome.Status != 200) { failure = outcome; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
                else
                {
                    db.VoiceEventDraftReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = operation, Fingerprint = fingerprint,
                        VesselId = vessel, ResourceId = outcome.ResourceId, Revision = Math.Max(1, outcome.Revision), SavedAt = TechnicalRecordCommands.Now(h) });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = outcome.ResourceId;
                }
            }
            catch (DbUpdateConcurrencyException)
            { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); failure = TechnicalOutcome.Fail(409, "version_conflict"); }
            catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
            {
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                failure = PostgresFailure.IsConflict(ex) ? TechnicalOutcome.Fail(409, "version_conflict") : TechnicalOutcome.Fail(503, "service_unavailable");
            }
        }
        if (failure != null) return await TechnicalRecordCommands.Fail(h, CommandAction, failure.Status, failure.Code!, resource, failure.Missing);
        try { return await Fresh(committed!.Value); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex))
        { return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503); }
    }
}
