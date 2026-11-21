using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotProgramCreateCommand(string Name, [property: JsonRequired] Guid ShipVesselId, Guid? TugVesselId, [property: JsonRequired] DateOnly StartDate, string TimeZone);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotLinkCommand(string ItemCode, string RecordType, [property: JsonRequired] Guid RecordId, string? Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotLinkWithdrawCommand(string Reason, [property: JsonRequired] long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotFeedbackCommand(string Week, string Area, string Category, string Sentiment, string Role, string Summary, string ParticipantPseudonym);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotIssueCommand(string Week, string Area, string Category, string Severity, string Title);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotIssueTriageCommand(string Note, [property: JsonRequired] long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotIssueResolveCommand(string Resolution, string? ResolutionReference, [property: JsonRequired] long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotMeasurementCommand(string MetricId, string Phase, [property: JsonRequired] decimal Value, [property: JsonRequired] int SampleSize,
    [property: JsonRequired] DateOnly PeriodFrom, [property: JsonRequired] DateOnly PeriodTo, string EvidenceReference, string Method, [property: JsonRequired] long ExpectedSequence);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotWeekCloseCommand(string Week, string Outcome, string Rationale, string ExpectedWeekHash, [property: JsonRequired] long ExpectedSequence,
    [property: JsonRequired] Guid AuthorityId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotProgramDecisionCommand(string Decision, string Rationale, string ExpectedProgramHash, [property: JsonRequired] long ExpectedSequence,
    [property: JsonRequired] Guid AuthorityId);

public sealed record PilotProgramDto(Guid Id, string Name, Guid ShipVesselId, Guid? TugVesselId, DateOnly StartDate, string TimeZone, string CatalogVersion,
    Guid CreatedBy, DateTimeOffset CreatedAt, string Provenance);
public sealed record PilotVesselOption(Guid Id, string Code, string Name, string Kind);
public sealed record PilotProgramListView(PilotProgramDto[] Programs, PilotVesselOption[] Vessels, bool CanCreate, string CatalogVersion,
    string Authority = PilotProgramPolicy.Authority);
public sealed record PilotLinkDto(Guid Id, string Week, string ItemCode, string RecordType, Guid RecordId, bool AuditTracedAtLink, string? Note, Guid ActorId,
    DateTimeOffset LinkedAt, Guid? WithdrawnBy, DateTimeOffset? WithdrawnAt, string? WithdrawalReason, long Version, string Validity, string? Refusal);
public sealed record PilotFeedbackDto(Guid Id, string Week, string Area, string Category, string Sentiment, string Role, string Summary, string ParticipantPseudonym,
    Guid ActorId, DateTimeOffset RecordedAt, DateTimeOffset ExpiresAt);
public sealed record PilotIssueDto(Guid Id, string Week, string Area, string Category, string Severity, string Title, string State, Guid ReportedBy,
    DateTimeOffset ReportedAt, Guid? TriagedBy, DateTimeOffset? TriagedAt, string? TriageNote, Guid? ResolvedBy, DateTimeOffset? ResolvedAt, string? Resolution,
    string? ResolutionReference, string Timing, DateOnly ReportedLocalDate, long Version);
public sealed record PilotMeasurementDto(Guid Id, string MetricId, string Mode, string Phase, long Sequence, Guid? PreviousMeasurementId, decimal Value, string Unit,
    int SampleSize, DateOnly PeriodFrom, DateOnly PeriodTo, string EvidenceReference, string Method, Guid ActorId, DateTimeOffset RecordedAt);
public sealed record PilotWeekCloseDto(Guid Id, string Week, long Sequence, Guid? PreviousCloseId, string Outcome, string Recommendation, string WeekHash,
    string Rationale, string Duty, Guid ActorId, DateTimeOffset RecordedAt, string Validity);
public sealed record PilotProgramDecisionDto(Guid Id, long Sequence, Guid? PreviousDecisionId, string Decision, string Recommendation, string ProgramHash,
    string Rationale, string Duty, Guid ActorId, DateTimeOffset RecordedAt, string Validity);
public sealed record PilotWeekView(PilotWeekDefinition Definition, DateOnly StartDate, DateOnly EndDateExclusive, PilotWeekOutcome Outcome, PilotLinkDto[] Links,
    PilotWeekCloseDto[] Closes, long CloseSequence, int FeedbackCount, string[] AllowedOutcomes);
public sealed record PilotReadoutView(PilotValueRow[] Metrics, string TelemetryState, string TelemetryBasis, string TelemetryRetention, int TelemetryWindowDays,
    PilotGateInput UatShip, PilotGateInput UatTug, PilotDefectSeverity[] Defects, int OpenP0);
public sealed record PilotProgramView(PilotProgramDto Program, PilotWeekView[] Weeks, PilotIssueDto[] Issues, PilotFeedbackDto[] Feedback,
    PilotMeasurementDto[] Measurements, PilotReadoutView Readout, PilotProgramOutcome Review, PilotProgramDecisionDto[] Decisions, long DecisionSequence,
    PilotGateAuthorityDto[] Authorities, bool CanRecord, bool CanTriage, bool CanDecide, string[] AllowedDecisions, string FeedbackRetention,
    PilotValueMetric[] MetricCatalog, string[] RecordTypes, DateTimeOffset AssessedAt, string Authority = PilotProgramPolicy.Authority,
    string SourceLocator = PilotProgramEndpoints.SourceLocator);

// Pilot program kit: DEV-076/077/078/079 (pilot weeks), DEV-095 (dual-mode proof), DEV-096 (tug value readout),
// DEV-121 (v46.3 ship+tug proof metrics). Software cannot complete a pilot: every write is a human record, every
// checklist item needs a server-verified link to a real record, and week close / CONTINUE-EXPAND-FIX-STOP are the
// appointed Product Owner's records bound to a content hash. AI, automation and administrators never decide.
public static class PilotProgramEndpoints
{
    public const string SourceLocator = "18.01/sheet:Backlog/row:77-80,96,97,122;18.01/sheet:Sprint Plan/row:14-17;19.01/paragraph:43-67,137-161,255-278;63.05/paragraph:142-156";
    private const string ReadAction = "PilotProgramRead", CommandAction = "PilotProgramCommand";
    public const string CurrentValidity = "CURRENT", StaleValidity = "STALE", ValidLink = "VALID", InvalidLink = "INVALID";
    private const int MaxPrograms = 100, MaxFeedbackView = 200;

    public static void MapPilotPrograms(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/pilot-programs").RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("PilotProgramList").Produces<PilotProgramListView>();
        g.MapPost("", Create).WithName("PilotProgramCreate").Accepts<PilotProgramCreateCommand>("application/json").Produces<PilotProgramDto>();
        g.MapGet("/{id:guid}", Read).WithName("PilotProgramRead").Produces<PilotProgramView>();
        g.MapPost("/{id:guid}/links", Link).WithName("PilotChecklistLink").Accepts<PilotLinkCommand>("application/json").Produces<PilotLinkDto>();
        g.MapPost("/{id:guid}/links/{link:guid}/withdraw", Withdraw).WithName("PilotChecklistWithdraw").Accepts<PilotLinkWithdrawCommand>("application/json").Produces<PilotLinkDto>();
        g.MapPost("/{id:guid}/feedback", Feedback).WithName("PilotFeedbackRecord").Accepts<PilotFeedbackCommand>("application/json").Produces<PilotFeedbackDto>();
        g.MapPost("/{id:guid}/issues", Issue).WithName("PilotIssueReport").Accepts<PilotIssueCommand>("application/json").Produces<PilotIssueDto>();
        g.MapPost("/{id:guid}/issues/{issue:guid}/triage", Triage).WithName("PilotIssueTriage").Accepts<PilotIssueTriageCommand>("application/json").Produces<PilotIssueDto>();
        g.MapPost("/{id:guid}/issues/{issue:guid}/resolve", Resolve).WithName("PilotIssueResolve").Accepts<PilotIssueResolveCommand>("application/json").Produces<PilotIssueDto>();
        g.MapPost("/{id:guid}/measurements", Measure).WithName("PilotMeasurementRecord").Accepts<PilotMeasurementCommand>("application/json").Produces<PilotMeasurementDto>();
        g.MapPost("/{id:guid}/week-closes", CloseWeek).WithName("PilotWeekClose").Accepts<PilotWeekCloseCommand>("application/json").Produces<PilotWeekCloseDto>();
        g.MapPost("/{id:guid}/decisions", Decide).WithName("PilotProgramDecide").Accepts<PilotProgramDecisionCommand>("application/json").Produces<PilotProgramDecisionDto>();
    }

    private static FederationSettings Federation(HttpContext h) => h.RequestServices.GetRequiredService<FederationSettings>();
    private static PilotProgramSettings Settings(HttpContext h) => h.RequestServices.GetRequiredService<PilotProgramSettings>();
    private static Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null) => TugReadinessHttp.Fail(h, action, status, code, id);
    private static bool HashText(string? v) => v is { Length: 64 } && v.All(char.IsAsciiHexDigitUpper);

    internal static PilotProgramDto Dto(PilotProgram p) => new(p.Id, p.Name, p.ShipVesselId, p.TugVesselId, p.StartDate, p.TimeZone, p.CatalogVersion, p.CreatedBy, p.CreatedAt, p.Provenance);
    internal static PilotLinkDto Dto(PilotChecklistLink l, string? refusal, bool evaluated) => new(l.Id, l.Week, l.ItemCode, l.RecordType, l.RecordId, l.AuditTracedAtLink, l.Note,
        l.ActorId, l.LinkedAt, l.WithdrawnBy, l.WithdrawnAt, l.WithdrawalReason, l.Version, !evaluated || l.WithdrawnAt != null ? "WITHDRAWN_OR_UNEVALUATED" : refusal == null ? ValidLink : InvalidLink, refusal);
    internal static PilotFeedbackDto Dto(PilotFeedback f) => new(f.Id, f.Week, f.Area, f.Category, f.Sentiment, f.Role, f.Summary, f.ParticipantPseudonym, f.ActorId, f.RecordedAt, f.ExpiresAt);
    internal static PilotIssueDto Dto(PilotIssue i, PilotIssueTiming t) => new(i.Id, i.Week, i.Area, i.Category, i.Severity, i.Title, i.State, i.ReportedBy, i.ReportedAt,
        i.TriagedBy, i.TriagedAt, i.TriageNote, i.ResolvedBy, i.ResolvedAt, i.Resolution, i.ResolutionReference, t.State, t.ReportedLocalDate, i.Version);
    internal static PilotMeasurementDto Dto(PilotMeasurement m) => new(m.Id, m.MetricId, m.Mode, m.Phase, m.Sequence, m.PreviousMeasurementId, m.Value, m.Unit, m.SampleSize,
        m.PeriodFrom, m.PeriodTo, m.EvidenceReference, m.Method, m.ActorId, m.RecordedAt);
    internal static PilotWeekCloseDto Dto(PilotWeekClose c, string validity) => new(c.Id, c.Week, c.Sequence, c.PreviousCloseId, c.Outcome, c.Recommendation, c.WeekHash,
        c.Rationale, c.Duty, c.ActorId, c.RecordedAt, validity);
    internal static PilotProgramDecisionDto Dto(PilotProgramDecision d, string validity) => new(d.Id, d.Sequence, d.PreviousDecisionId, d.Decision, d.Recommendation,
        d.ProgramHash, d.Rationale, d.Duty, d.ActorId, d.RecordedAt, validity);

    // Guards shared by every command: permission, exact human membership (never AI/automation), no query string.
    private static async Task<(TrustedActor? Actor, IResult? Failure)> Writer(HttpContext h, Func<TrustedActor?, bool> permission) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!permission(a)) return (null, await Fail(h, CommandAction, 403, "permission_denied"));
        if (!PilotAccess.Human(Federation(h), a)) return (null, await Fail(h, CommandAction, 403, "human_actor_required"));
        if (h.Request.Query.Count > 0) return (null, await Fail(h, CommandAction, 400, "invalid_request"));
        return (a, null);
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!PilotProgramAccess.Read(a)) return await Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Count > 0) return await Fail(h, ReadAction, 400, "invalid_query");
        var programs = await db.PilotPrograms.AsNoTracking().OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Take(MaxPrograms).ToArrayAsync(ct);
        var vessels = await a.Scope(db.Vessels).AsNoTracking().OrderBy(v => v.Code).Take(500)
            .Select(v => new PilotVesselOption(v.Id, v.Code, v.Name, v.Kind == VesselKind.Tug ? PilotProgramCatalog.Tug : PilotProgramCatalog.Ship)).ToArrayAsync(ct);
        await TugReadinessHttp.Observe(h, ReadAction, "Success");
        return Results.Ok(new PilotProgramListView(programs.Select(Dto).ToArray(), vessels, PilotProgramAccess.Manage(a) && PilotAccess.Human(Federation(h), a), PilotProgramCatalog.Version));
    }

    private static async Task<IResult> Create(HttpContext h, PoseidonDbContext db) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Manage);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotProgramCreateCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!PilotProgramPolicy.ValidText(c!.Name, PilotProgramPolicy.NameMin, PilotProgramPolicy.NameMax) || c.ShipVesselId == Guid.Empty || c.TugVesselId == Guid.Empty ||
            c.TugVesselId == c.ShipVesselId || c.TimeZone is not { Length: > 0 and <= 64 })
            return await Fail(h, CommandAction, 400, "validation_failed");
        if (PilotProgramReader.Zone(c.TimeZone) == null) return await Fail(h, CommandAction, 400, "invalid_time_zone");
        var ct = h.RequestAborted; var now = TugReadinessHttp.Now(h);
        // Ship and tug vessels must be visible to the actor and of the right kind (dual mode: 1 ship + 1 tug).
        var ship = await a!.Scope(db.Vessels).AsNoTracking().AnyAsync(v => v.Id == c.ShipVesselId && v.Kind == VesselKind.Ship, ct);
        var tug = c.TugVesselId == null || await a.Scope(db.Vessels).AsNoTracking().AnyAsync(v => v.Id == c.TugVesselId && v.Kind == VesselKind.Tug, ct);
        if (!ship || !tug) return await Fail(h, CommandAction, 422, "invalid_vessel");
        return await Commit(h, db, a, "PilotProgramCreate", key, new { c }, ct2 => {
            var p = new PilotProgram { TenantId = a.TenantId, Id = Guid.NewGuid(), Name = c.Name, ShipVesselId = c.ShipVesselId, TugVesselId = c.TugVesselId,
                StartDate = c.StartDate, TimeZone = c.TimeZone, CatalogVersion = PilotProgramCatalog.Version, CreatedBy = a.Id, CreatedAt = now };
            db.PilotPrograms.Add(p);
            return Task.FromResult<(IResult?, Guid)>((null, p.Id));
        }, async (id, ct2) => Dto(await db.PilotPrograms.AsNoTracking().SingleAsync(x => x.Id == id, ct2)));
    }

    private static async Task<IResult> Read(HttpContext h, PoseidonDbContext db, Guid id) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!PilotProgramAccess.Read(a)) return await Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Count > 0) return await Fail(h, ReadAction, 400, "invalid_query");
        var now = TugReadinessHttp.Now(h);
        PilotProgramState? s;
        try { s = await PilotProgramReader.Load(h, db, a, id, now, ct); }
        catch (InvalidDataException) { return await Fail(h, ReadAction, 503, "service_unavailable", id); }
        if (s == null) return await Fail(h, ReadAction, 404, "resource_not_found", id);
        var feedback = await db.PilotFeedback.AsNoTracking().Where(f => f.ProgramId == id && f.ExpiresAt > now).OrderByDescending(f => f.RecordedAt).ThenBy(f => f.Id)
            .Take(MaxFeedbackView).ToArrayAsync(ct);
        var feedbackCounts = await db.PilotFeedback.AsNoTracking().Where(f => f.ProgramId == id && f.ExpiresAt > now).GroupBy(f => f.Week)
            .Select(g => new { g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var decisions = await db.PilotProgramDecisions.AsNoTracking().Where(d => d.ProgramId == id).OrderByDescending(d => d.Sequence).Take(32).ToArrayAsync(ct);
        var grants = PilotProgramAccess.Resolve(Federation(h), a, now);
        var synthetic = s.Program.Provenance == "SyntheticUnverified";
        var weeks = PilotProgramCatalog.WeekDefinitions.Select(w => {
            var outcome = s.Weeks.Single(o => o.Week == w.Week);
            var closes = s.Closes.Where(c => c.Week == w.Week).OrderByDescending(c => c.Sequence).ToArray();
            var allowed = synthetic ? [] : PilotProgramPolicy.CloseOutcomes.Where(o => PilotProgramPolicy.CloseRefusal(o, outcome) == null).ToArray();
            return new PilotWeekView(w, PilotProgramPolicy.WeekStart(s.Program.StartDate, w.Number), PilotProgramPolicy.WeekStart(s.Program.StartDate, w.Number + 1), outcome,
                s.Links.Where(l => l.Week == w.Week).Select(l => Dto(l, s.LinkRefusals[l.Id], true)).ToArray(),
                closes.Select(c => Dto(c, c.WeekHash == outcome.Hash ? CurrentValidity : StaleValidity)).ToArray(), closes.FirstOrDefault()?.Sequence ?? 0,
                feedbackCounts.SingleOrDefault(x => x.Key == w.Week)?.Count ?? 0, allowed);
        }).ToArray();
        await TugReadinessHttp.Observe(h, ReadAction, "Success", id);
        return Results.Ok(new PilotProgramView(Dto(s.Program), weeks, s.Issues.Select(i => Dto(i, s.Timings[i.Id])).ToArray(), feedback.Select(Dto).ToArray(),
            s.LatestMeasurements.OrderBy(m => m.MetricId, StringComparer.Ordinal).ThenBy(m => m.Phase, StringComparer.Ordinal).Select(Dto).ToArray(),
            new PilotReadoutView(s.Readout, s.TelemetryState, "DECLARED_UNVERIFIED", s.TelemetryRetention, s.TelemetryWindowDays, s.UatShip, s.UatTug, s.Defects, s.OpenP0),
            s.Review, decisions.Select(d => Dto(d, d.ProgramHash == s.Review.Hash ? CurrentValidity : StaleValidity)).ToArray(), decisions.FirstOrDefault()?.Sequence ?? 0,
            PilotAccess.Dto(grants), PilotProgramAccess.Record(a) && PilotAccess.Human(Federation(h), a),
            PilotProgramAccess.Triage(a) && PilotAccess.Human(Federation(h), a), grants.Length > 0 && !synthetic,
            synthetic ? [] : PilotProgramPolicy.Decisions.Where(d => PilotProgramPolicy.DecisionRefusal(d, s.Review) == null).ToArray(),
            Settings(h).Configured ? "CONFIGURED" : "UNCONFIGURED", PilotProgramCatalog.ValueMetrics, PilotProgramCatalog.RecordTypes, now));
    }

    private static async Task<PilotProgram?> Program(PoseidonDbContext db, Guid id, CancellationToken ct) =>
        await db.PilotPrograms.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);

    // Human link of a checklist item to an existing record of this tenant. The server verifies type, existence, tenant,
    // vessel binding, provenance and (where the item requires it) an attributed audit trace before storing the link.
    private static async Task<IResult> Link(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Record);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotLinkCommand>(h, CommandAction);
        if (failure != null) return failure;
        var item = PilotProgramCatalog.Item(c!.ItemCode);
        if (item == null || !PilotProgramCatalog.RecordTypes.Contains(c.RecordType, StringComparer.Ordinal) || c.RecordId == Guid.Empty ||
            !PilotProgramPolicy.ValidOptionalText(c.Note, PilotProgramPolicy.NoteMax))
            return await Fail(h, CommandAction, 400, "validation_failed");
        if (!item.RecordTypes.Contains(c.RecordType, StringComparer.Ordinal)) return await Fail(h, CommandAction, 400, "record_type_not_allowed");
        // Ship/tug mode isolation: a record of a mode the actor cannot see is never linkable by that actor.
        if (item.Area == PilotProgramCatalog.Ship && !a!.CanSeeShip || item.Area == PilotProgramCatalog.Tug && !a!.CanSeeTug)
            return await Fail(h, CommandAction, 403, "mode_not_visible");
        var ct = h.RequestAborted; var now = TugReadinessHttp.Now(h);
        var program = await Program(db, id, ct);
        if (program == null) return await Fail(h, CommandAction, 404, "resource_not_found", id);
        var probe = await PilotRecordProbes.Probe(db, c.RecordType, c.RecordId, item.RequiresAuditTrace, ct);
        if (PilotProgramPolicy.LinkRefusal(item, c.RecordType, probe, new(program.ShipVesselId, program.TugVesselId), id) is { } refusal)
            return await Fail(h, CommandAction, 422, refusal, id);
        return await Commit(h, db, a!, "PilotChecklistLink", key, new { id, c }, async ct2 => {
            if (await db.PilotChecklistLinks.AnyAsync(l => l.ProgramId == id && l.ItemCode == c.ItemCode && l.RecordType == c.RecordType && l.RecordId == c.RecordId && l.WithdrawnAt == null, ct2))
                return (await Fail(h, CommandAction, 409, "link_exists", id), Guid.Empty);
            var row = new PilotChecklistLink { TenantId = a!.TenantId, Id = Guid.NewGuid(), ProgramId = id, Week = item.Week, ItemCode = item.Code, RecordType = c.RecordType,
                RecordId = c.RecordId, RecordVesselId = probe!.VesselId, AuditTracedAtLink = probe.AuditTraced, Note = c.Note, ActorId = a.Id, LinkedAt = now };
            db.PilotChecklistLinks.Add(row);
            return (null, row.Id);
        }, async (lid, ct2) => Dto(await db.PilotChecklistLinks.AsNoTracking().SingleAsync(x => x.Id == lid, ct2), null, false));
    }

    private static async Task<IResult> Withdraw(HttpContext h, PoseidonDbContext db, Guid id, Guid link) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Record);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotLinkWithdrawCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!PilotProgramPolicy.ValidText(c!.Reason, PilotProgramPolicy.ReasonMin, PilotProgramPolicy.NoteMax) || c.ExpectedVersion is < 0 or > uint.MaxValue)
            return await Fail(h, CommandAction, 400, "validation_failed");
        var now = TugReadinessHttp.Now(h);
        return await Commit(h, db, a!, "PilotChecklistWithdraw", key, new { id, link, c }, async ct => {
            var row = await db.PilotChecklistLinks.SingleOrDefaultAsync(l => l.Id == link && l.ProgramId == id, ct);
            if (row == null) return (await Fail(h, CommandAction, 404, "resource_not_found", link), Guid.Empty);
            if (row.Version != (uint)c.ExpectedVersion) return (await Fail(h, CommandAction, 409, "version_conflict", link), Guid.Empty);
            if (row.WithdrawnAt != null) return (await Fail(h, CommandAction, 409, "link_withdrawn", link), Guid.Empty);
            row.WithdrawnBy = a!.Id; row.WithdrawnAt = now; row.WithdrawalReason = c.Reason;
            return (null, row.Id);
        }, async (lid, ct) => Dto(await db.PilotChecklistLinks.AsNoTracking().SingleAsync(x => x.Id == lid, ct), null, false));
    }

    // Structured daily feedback; free text is minimised (PII heuristic) and expires with the operator retention policy.
    private static async Task<IResult> Feedback(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Record);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotFeedbackCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!PilotProgramCatalog.Weeks.Contains(c!.Week, StringComparer.Ordinal) || !PilotProgramCatalog.Areas.Contains(c.Area, StringComparer.Ordinal) ||
            !PilotFeedbackPolicy.Categories.Contains(c.Category, StringComparer.Ordinal) || !PilotFeedbackPolicy.Sentiments.Contains(c.Sentiment, StringComparer.Ordinal) ||
            !PilotFeedbackPolicy.Roles.Contains(c.Role, StringComparer.Ordinal) || !PilotProgramPolicy.ValidText(c.Summary, PilotFeedbackPolicy.SummaryMin, PilotFeedbackPolicy.SummaryMax) ||
            !UatPolicy.ValidPseudonym(c.ParticipantPseudonym))
            return await Fail(h, CommandAction, 400, "validation_failed");
        if (PilotFeedbackPolicy.SuspectedPersonalData(c.Summary)) return await Fail(h, CommandAction, 400, "pii_suspected");
        var settings = Settings(h);
        if (!settings.Configured) return await Fail(h, CommandAction, 503, "retention_unconfigured");
        var ct0 = h.RequestAborted; var now = TugReadinessHttp.Now(h);
        if (await Program(db, id, ct0) == null) return await Fail(h, CommandAction, 404, "resource_not_found", id);
        return await Commit(h, db, a!, "PilotFeedbackRecord", key, new { id, c }, async ct => {
            // Bounded retention purge of this tenant's expired feedback, through the audited path.
            db.PilotFeedback.RemoveRange(await db.PilotFeedback.Where(f => f.ExpiresAt <= now).OrderBy(f => f.ExpiresAt).Take(100).ToArrayAsync(ct));
            var row = new PilotFeedback { TenantId = a!.TenantId, Id = Guid.NewGuid(), ProgramId = id, Week = c.Week, Area = c.Area, Category = c.Category, Sentiment = c.Sentiment,
                Role = c.Role, Summary = c.Summary, ParticipantPseudonym = c.ParticipantPseudonym, ActorId = a.Id, RecordedAt = now,
                ExpiresAt = now.AddDays(settings.FeedbackRetentionDays!.Value), RetentionPolicyRef = settings.RetentionPolicyRef! };
            db.PilotFeedback.Add(row);
            return (null, row.Id);
        }, async (fid, ct) => Dto(await db.PilotFeedback.AsNoTracking().SingleAsync(x => x.Id == fid, ct)));
    }

    private static async Task<PilotIssueDto> IssueDto(PoseidonDbContext db, Guid issueId, DateTimeOffset now, CancellationToken ct) {
        var i = await db.PilotIssues.AsNoTracking().SingleAsync(x => x.Id == issueId, ct);
        var zone = PilotProgramReader.Zone((await db.PilotPrograms.AsNoTracking().SingleAsync(p => p.Id == i.ProgramId, ct)).TimeZone)!;
        return Dto(i, PilotIssuePolicy.Timing(i.Severity, i.Category, i.ReportedAt, i.TriagedAt, now, zone));
    }

    private static async Task<IResult> Issue(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Record);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotIssueCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!PilotProgramCatalog.Weeks.Contains(c!.Week, StringComparer.Ordinal) || !PilotProgramCatalog.Areas.Contains(c.Area, StringComparer.Ordinal) ||
            !PilotIssuePolicy.Categories.Contains(c.Category, StringComparer.Ordinal) || !PilotIssuePolicy.Severities.Contains(c.Severity, StringComparer.Ordinal) ||
            !PilotProgramPolicy.ValidText(c.Title, PilotIssuePolicy.TitleMin, PilotIssuePolicy.TitleMax))
            return await Fail(h, CommandAction, 400, "validation_failed");
        if (PilotFeedbackPolicy.SuspectedPersonalData(c.Title)) return await Fail(h, CommandAction, 400, "pii_suspected");
        var now = TugReadinessHttp.Now(h);
        if (await Program(db, id, h.RequestAborted) == null) return await Fail(h, CommandAction, 404, "resource_not_found", id);
        return await Commit(h, db, a!, "PilotIssueReport", key, new { id, c }, ct => {
            var row = new PilotIssue { TenantId = a!.TenantId, Id = Guid.NewGuid(), ProgramId = id, Week = c.Week, Area = c.Area, Category = c.Category, Severity = c.Severity,
                Title = c.Title, ReportedBy = a.Id, ReportedAt = now };
            db.PilotIssues.Add(row);
            return Task.FromResult<(IResult?, Guid)>((null, row.Id));
        }, async (iid, ct) => await IssueDto(db, iid, now, ct));
    }

    // Triage timestamp is the server clock; "same day" is judged on read against the program's calendar day.
    private static async Task<IResult> Triage(HttpContext h, PoseidonDbContext db, Guid id, Guid issue) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Triage);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotIssueTriageCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!PilotProgramPolicy.ValidText(c!.Note, PilotIssuePolicy.NoteMin, PilotIssuePolicy.NoteMax) || c.ExpectedVersion is < 0 or > uint.MaxValue)
            return await Fail(h, CommandAction, 400, "validation_failed");
        if (PilotFeedbackPolicy.SuspectedPersonalData(c.Note)) return await Fail(h, CommandAction, 400, "pii_suspected");
        var now = TugReadinessHttp.Now(h);
        return await Commit(h, db, a!, "PilotIssueTriage", key, new { id, issue, c }, async ct => {
            var row = await db.PilotIssues.SingleOrDefaultAsync(i => i.Id == issue && i.ProgramId == id, ct);
            if (row == null) return (await Fail(h, CommandAction, 404, "resource_not_found", issue), Guid.Empty);
            if (row.Version != (uint)c.ExpectedVersion) return (await Fail(h, CommandAction, 409, "version_conflict", issue), Guid.Empty);
            if (row.State != PilotIssuePolicy.Open) return (await Fail(h, CommandAction, 409, "issue_not_open", issue), Guid.Empty);
            row.State = PilotIssuePolicy.Triaged; row.TriagedBy = a!.Id; row.TriagedAt = now; row.TriageNote = c.Note;
            return (null, row.Id);
        }, async (iid, ct) => await IssueDto(db, iid, now, ct));
    }

    private static async Task<IResult> Resolve(HttpContext h, PoseidonDbContext db, Guid id, Guid issue) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Triage);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotIssueResolveCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!PilotProgramPolicy.ValidText(c!.Resolution, PilotIssuePolicy.NoteMin, PilotIssuePolicy.NoteMax) ||
            !PilotProgramPolicy.ValidOptionalText(c.ResolutionReference, PilotMeasurementPolicy.ReferenceMax) || c.ExpectedVersion is < 0 or > uint.MaxValue)
            return await Fail(h, CommandAction, 400, "validation_failed");
        if (PilotFeedbackPolicy.SuspectedPersonalData(c.Resolution)) return await Fail(h, CommandAction, 400, "pii_suspected");
        var now = TugReadinessHttp.Now(h);
        return await Commit(h, db, a!, "PilotIssueResolve", key, new { id, issue, c }, async ct => {
            var row = await db.PilotIssues.SingleOrDefaultAsync(i => i.Id == issue && i.ProgramId == id, ct);
            if (row == null) return (await Fail(h, CommandAction, 404, "resource_not_found", issue), Guid.Empty);
            if (row.Version != (uint)c.ExpectedVersion) return (await Fail(h, CommandAction, 409, "version_conflict", issue), Guid.Empty);
            if (row.State != PilotIssuePolicy.Triaged) return (await Fail(h, CommandAction, 409, "issue_not_triaged", issue), Guid.Empty);
            row.State = PilotIssuePolicy.Resolved; row.ResolvedBy = a!.Id; row.ResolvedAt = now; row.Resolution = c.Resolution; row.ResolutionReference = c.ResolutionReference;
            return (null, row.Id);
        }, async (iid, ct) => await IssueDto(db, iid, now, ct));
    }

    // Human baseline / pilot measurement. Mode and unit come from the closed metric catalog; an evidence reference is
    // mandatory; values are never derived by the server.
    private static async Task<IResult> Measure(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Record);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotMeasurementCommand>(h, CommandAction);
        if (failure != null) return failure;
        var metric = PilotProgramCatalog.Metric(c!.MetricId);
        if (metric == null || !PilotMeasurementPolicy.Phases.Contains(c.Phase, StringComparer.Ordinal) || !PilotMeasurementPolicy.ValidValue(metric.Unit, c.Value) ||
            c.SampleSize is < 1 or > PilotMeasurementPolicy.MaxSample || c.PeriodTo < c.PeriodFrom ||
            !PilotProgramPolicy.ValidText(c.EvidenceReference, 1, PilotMeasurementPolicy.ReferenceMax) ||
            !PilotProgramPolicy.ValidText(c.Method, PilotMeasurementPolicy.MethodMin, PilotMeasurementPolicy.MethodMax) || c.ExpectedSequence < 0)
            return await Fail(h, CommandAction, 400, "validation_failed");
        if (metric.Mode == PilotProgramCatalog.Ship && !a!.CanSeeShip || metric.Mode == PilotProgramCatalog.Tug && !a!.CanSeeTug)
            return await Fail(h, CommandAction, 403, "mode_not_visible");
        var now = TugReadinessHttp.Now(h);
        if (await Program(db, id, h.RequestAborted) == null) return await Fail(h, CommandAction, 404, "resource_not_found", id);
        return await Commit(h, db, a!, "PilotMeasurementRecord", key, new { id, c }, async ct => {
            var previous = await db.PilotMeasurements.AsNoTracking().Where(m => m.ProgramId == id && m.MetricId == metric.Id && m.Mode == metric.Mode && m.Phase == c.Phase)
                .OrderByDescending(m => m.Sequence).Select(m => new { m.Id, m.Sequence }).FirstOrDefaultAsync(ct);
            if ((previous?.Sequence ?? 0) != c.ExpectedSequence) return (await Fail(h, CommandAction, 409, "measurement_sequence_conflict", id), Guid.Empty);
            var row = new PilotMeasurement { TenantId = a!.TenantId, Id = Guid.NewGuid(), ProgramId = id, MetricId = metric.Id, Mode = metric.Mode, Phase = c.Phase,
                Sequence = (previous?.Sequence ?? 0) + 1, PreviousMeasurementId = previous?.Id, Value = c.Value, Unit = metric.Unit, SampleSize = c.SampleSize,
                PeriodFrom = c.PeriodFrom, PeriodTo = c.PeriodTo, EvidenceReference = c.EvidenceReference, Method = c.Method, ActorId = a.Id, RecordedAt = now };
            db.PilotMeasurements.Add(row);
            return (null, row.Id);
        }, async (mid, ct) => Dto(await db.PilotMeasurements.AsNoTracking().SingleAsync(x => x.Id == mid, ct)));
    }

    // Product Owner week close. The server re-evaluates the week; a changed hash is 409 week_stale; ACCEPTED requires a
    // ready week. The decider must not have authored any active link of that week (separation of duties).
    private static async Task<IResult> CloseWeek(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Decide);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotWeekCloseCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (PilotProgramCatalog.Week(c!.Week) == null || !PilotProgramPolicy.CloseOutcomes.Contains(c.Outcome, StringComparer.Ordinal) ||
            !PilotProgramPolicy.ValidText(c.Rationale, PilotProgramPolicy.RationaleMin, PilotProgramPolicy.RationaleMax) || !HashText(c.ExpectedWeekHash) ||
            c.ExpectedSequence < 0 || c.AuthorityId == Guid.Empty)
            return await Fail(h, CommandAction, 400, "validation_failed");
        var ct0 = h.RequestAborted; var now = TugReadinessHttp.Now(h);
        var grant = PilotProgramAccess.Resolve(Federation(h), a, now).SingleOrDefault(g => g.Id == c.AuthorityId);
        if (grant == null) return await Fail(h, CommandAction, 403, "pilot_gate_authority_required");
        PilotProgramState? s;
        try { s = await PilotProgramReader.Load(h, db, a!, id, now, ct0); }
        catch (InvalidDataException) { return await Fail(h, CommandAction, 503, "service_unavailable", id); }
        if (s == null) return await Fail(h, CommandAction, 404, "resource_not_found", id);
        if (s.Program.Provenance == "SyntheticUnverified") return await Fail(h, CommandAction, 409, "synthetic_program_not_decidable", id);
        var week = s.Weeks.Single(w => w.Week == c.Week);
        if (s.Links.Any(l => l.Week == c.Week && l.ActorId == a!.Id)) return await Fail(h, CommandAction, 403, "separation_of_duties_required", id);
        return await Commit(h, db, a!, "PilotWeekClose", key, new { id, c }, async ct => {
            var previous = await db.PilotWeekCloses.AsNoTracking().Where(x => x.ProgramId == id && x.Week == c.Week).OrderByDescending(x => x.Sequence)
                .Select(x => new { x.Id, x.Sequence }).FirstOrDefaultAsync(ct);
            if ((previous?.Sequence ?? 0) != c.ExpectedSequence) return (await Fail(h, CommandAction, 409, "close_sequence_conflict", id), Guid.Empty);
            if (!string.Equals(week.Hash, c.ExpectedWeekHash, StringComparison.Ordinal)) return (await Fail(h, CommandAction, 409, "week_stale", id), Guid.Empty);
            if (PilotProgramPolicy.CloseRefusal(c.Outcome, week) is { } refusal) return (await Fail(h, CommandAction, 409, refusal, id), Guid.Empty);
            var row = new PilotWeekClose { TenantId = a!.TenantId, Id = Guid.NewGuid(), ProgramId = id, Week = c.Week, Sequence = (previous?.Sequence ?? 0) + 1,
                PreviousCloseId = previous?.Id, Outcome = c.Outcome, Recommendation = week.Recommendation, WeekHash = week.Hash, SnapshotJson = PilotProgramReader.WeekSnapshot(week),
                Rationale = c.Rationale, ActorId = a.Id, AuthorityId = grant.Id, RoleId = grant.RoleId, Duty = grant.Duty, AuthorityHash = PilotAccess.Hash(grant, a), RecordedAt = now };
            db.PilotWeekCloses.Add(row);
            return (null, row.Id);
        }, async (cid, ct) => {
            var saved = await db.PilotWeekCloses.AsNoTracking().SingleAsync(x => x.Id == cid, ct);
            return Dto(saved, saved.WeekHash == week.Hash ? CurrentValidity : StaleValidity);
        });
    }

    // Product Owner CONTINUE/EXPAND/FIX/STOP bound to the program hash. EXPAND only when expand-eligible. The decider
    // must not have authored any measurement the readout uses (separation of duties).
    private static async Task<IResult> Decide(HttpContext h, PoseidonDbContext db, Guid id) {
        var (a, denied) = await Writer(h, PilotProgramAccess.Decide);
        if (denied != null) return denied;
        var (c, key, failure) = await TugReadinessHttp.Read<PilotProgramDecisionCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (!PilotProgramPolicy.Decisions.Contains(c!.Decision, StringComparer.Ordinal) ||
            !PilotProgramPolicy.ValidText(c.Rationale, PilotProgramPolicy.RationaleMin, PilotProgramPolicy.RationaleMax) || !HashText(c.ExpectedProgramHash) ||
            c.ExpectedSequence < 0 || c.AuthorityId == Guid.Empty)
            return await Fail(h, CommandAction, 400, "validation_failed");
        var ct0 = h.RequestAborted; var now = TugReadinessHttp.Now(h);
        var grant = PilotProgramAccess.Resolve(Federation(h), a, now).SingleOrDefault(g => g.Id == c.AuthorityId);
        if (grant == null) return await Fail(h, CommandAction, 403, "pilot_gate_authority_required");
        PilotProgramState? s;
        try { s = await PilotProgramReader.Load(h, db, a!, id, now, ct0); }
        catch (InvalidDataException) { return await Fail(h, CommandAction, 503, "service_unavailable", id); }
        if (s == null) return await Fail(h, CommandAction, 404, "resource_not_found", id);
        if (s.Program.Provenance == "SyntheticUnverified") return await Fail(h, CommandAction, 409, "synthetic_program_not_decidable", id);
        if (s.LatestMeasurements.Any(m => m.ActorId == a!.Id)) return await Fail(h, CommandAction, 403, "separation_of_duties_required", id);
        return await Commit(h, db, a!, "PilotProgramDecide", key, new { id, c }, async ct => {
            var previous = await db.PilotProgramDecisions.AsNoTracking().Where(x => x.ProgramId == id).OrderByDescending(x => x.Sequence)
                .Select(x => new { x.Id, x.Sequence }).FirstOrDefaultAsync(ct);
            if ((previous?.Sequence ?? 0) != c.ExpectedSequence) return (await Fail(h, CommandAction, 409, "decision_sequence_conflict", id), Guid.Empty);
            if (!string.Equals(s.Review.Hash, c.ExpectedProgramHash, StringComparison.Ordinal)) return (await Fail(h, CommandAction, 409, "program_stale", id), Guid.Empty);
            if (PilotProgramPolicy.DecisionRefusal(c.Decision, s.Review) is { } refusal) return (await Fail(h, CommandAction, 409, refusal, id), Guid.Empty);
            var row = new PilotProgramDecision { TenantId = a!.TenantId, Id = Guid.NewGuid(), ProgramId = id, Sequence = (previous?.Sequence ?? 0) + 1,
                PreviousDecisionId = previous?.Id, Decision = c.Decision, Recommendation = s.Review.Recommendation, ProgramHash = s.Review.Hash,
                SnapshotJson = PilotProgramReader.ProgramSnapshot(s), Rationale = c.Rationale, ActorId = a.Id, AuthorityId = grant.Id, RoleId = grant.RoleId,
                Duty = grant.Duty, AuthorityHash = PilotAccess.Hash(grant, a), RecordedAt = now };
            db.PilotProgramDecisions.Add(row);
            return (null, row.Id);
        }, async (did, ct) => {
            var saved = await db.PilotProgramDecisions.AsNoTracking().SingleAsync(x => x.Id == did, ct);
            return Dto(saved, saved.ProgramHash == s.Review.Hash ? CurrentValidity : StaleValidity);
        });
    }

    // SERIALIZABLE command path with a per-actor idempotency receipt; replays return the committed resource with
    // Idempotency-Replayed: true; a reused key with another body is 409. Failures classify only via PostgresFailure.
    private static async Task<IResult> Commit(HttpContext h, PoseidonDbContext db, TrustedActor a, string receiptAction, string key, object body,
        Func<CancellationToken, Task<(IResult? Failure, Guid RecordId)>> work, Func<Guid, CancellationToken, Task<object>> read) {
        var ct = h.RequestAborted; var now = TugReadinessHttp.Now(h);
        var fingerprint = TugReadinessHttp.Fingerprint(new { receiptAction, body });
        await TugReadinessHttp.Observe(h, CommandAction, "Attempted");
        Guid recordId;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)) {
            try {
                var receipt = await db.PilotProgramReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
                if (receipt != null) {
                    if (receipt.Action != receiptAction || receipt.Fingerprint != fingerprint) return await Fail(h, CommandAction, 409, "idempotency_key_reused");
                    h.Response.Headers["Idempotency-Replayed"] = "true";
                    var replay = await read(receipt.RecordId, ct);
                    await TugReadinessHttp.Observe(h, CommandAction, "Success", receipt.RecordId);
                    return Results.Ok(replay);
                }
                var (failure, id) = await work(ct);
                if (failure != null) return failure;
                db.PilotProgramReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = receiptAction, Fingerprint = fingerprint, RecordId = id, CommittedAt = now });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                recordId = id;
            } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
                return await TugReadinessHttp.Database(h, db, tx, CommandAction, e, null);
            }
        }
        db.ChangeTracker.Clear();
        await TugReadinessHttp.Observe(h, CommandAction, "Success", recordId);
        return Results.Ok(await read(recordId, ct));
    }
}
