using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Reporter projection. A CONFIDENTIAL reporter is never named here (no actor id, no pseudonym derived from the
// actor); IsCurrentActor tells only the reporter themselves that the report is theirs.
public sealed record SafetyEventReporterDto(string Mode, Guid? ActorId, bool IsCurrentActor);
public sealed record SafetyEventTriageDto(Guid Id, long Revision, string Decision, string? Severity, string? InvestigationRequired, string Reason, Guid ActorId, DateTimeOffset DecidedAt);
public sealed record SafetyEventMessageDto(Guid Id, string Visibility, string AuthorKind, Guid? ActorId, string Body, DateTimeOffset CreatedAt);
public sealed record SafetyInvestigationDecisionDto(Guid Id, long Revision, string Decision, string Reason, Guid ActorId, DateTimeOffset DecidedAt);
public sealed record SafetyInvestigationDto(long Revision, uint Version, string State, SafetyInvestigationContent Content, string Hash, long? ApprovedRevision,
    string[] Missing, Guid[] Authors, SafetyInvestigationDecisionDto[] Decisions, bool CanEdit, bool CanSubmit, bool CanReview);
public sealed record SafetyActionStepDto(Guid Id, long Revision, string Kind, string? Outcome, string Text, CertificateFilePin[] Evidence, Guid ActorId, DateTimeOffset RecordedAt);
public sealed record SafetyActionDto(Guid Id, string Kind, string Title, string Description, Guid OwnerId, DateOnly DueDate, bool Overdue, Guid[] RootCauseIds,
    Guid[] BarrierIds, Guid? FollowUpOf, long Revision, uint Version, string State, string Effectiveness, SafetyActionStepDto[] Steps, string[] AllowedSteps);
public sealed record SafetyEventPermissionsDto(bool Edit, bool Submit, bool Withdraw, bool Triage, bool ReadIdentity, bool Message, bool Investigate,
    bool ReviewInvestigation, bool CreateAction, bool Close);
public sealed record SafetyEventDto(Guid Id, Guid VesselId, string EventNo, long Revision, uint Version, string State, string ReportingMode,
    SafetyEventDraftFields Fields, string Hash, string[] Missing, SafetyEventReporterDto Reporter, string? Severity, string? SeverityClaim,
    string? InvestigationRequired, string SeverityPolicy, string Origin, DateTimeOffset CreatedAt, DateTimeOffset SavedAt, DateTimeOffset? SubmittedAt,
    Guid? ClosedBy, DateTimeOffset? ClosedAt, string? ClosureReason, SafetyEventTriageDto[] Triages, SafetyInvestigationDto? Investigation,
    SafetyActionDto[] Actions, string Effectiveness, string[] ClosureBlockers, SafetyEventMessageDto[] Messages, SafetyEventPermissionsDto Can,
    string? FollowUpSecret, string Provenance, string SourceVersion);
public sealed record SafetyEventListItemDto(Guid Id, string EventNo, string State, string ReportingMode, string? Category, string? Summary,
    string? OccurrencePrecision, DateTimeOffset? OccurredAt, DateOnly? OccurrenceDate, string? Severity, bool Mine, string Origin, DateTimeOffset SavedAt);
public sealed record SafetyEventOfflineDto(bool Available, string State);
public sealed record SafetyEventCapabilitiesDto(Guid VesselId, bool CanRead, bool CanReport, bool CanTriage, bool CanInvestigate, bool CanReview,
    bool CanWriteActions, bool CanVerifyActions, bool CanReviewEffectiveness, bool CanReadIdentity, string AnonymousChannel, SafetyEventOfflineDto Offline,
    string[] Categories, string[] ReportingModes, string[] OperationContexts, string[] Severities, string SourceVersion);
public sealed record SafetyEventPageDto(SafetyEventListItemDto[] Items, int Offset, int Limit, bool HasMore, SafetyEventBlockerSnapshot Blockers,
    SafetyEventCapabilitiesDto Capabilities);

internal static class SafetyEventAccess
{
    private static bool Has(TrustedActor? a, string permission) => a?.CanRead == true && a.Permissions.Contains(permission);
    internal static bool Reader(TrustedActor? a) => Has(a, SafetyEventPolicy.ReadPermission);
    internal static bool Reporter(TrustedActor? a) => Has(a, SafetyEventPolicy.ReportPermission);
    internal static bool Any(TrustedActor? a) => Reader(a) || Reporter(a);
    internal static bool Triager(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(SafetyEventPolicy.TriagePermission);
    internal static bool Investigator(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(SafetyEventPolicy.InvestigatePermission);
    internal static bool Reviewer(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(SafetyEventPolicy.ReviewPermission);
    internal static bool IdentityReader(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(SafetyEventPolicy.IdentityPermission);
    internal static bool ActionWriter(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(SafetyEventPolicy.ActionWritePermission);
    internal static bool ActionActor(TrustedActor? a) => Reader(a) && (a!.Permissions.Contains(SafetyEventPolicy.ActionWritePermission) ||
        a.Permissions.Contains(SafetyEventPolicy.ActionVerifyPermission) || a.Permissions.Contains(SafetyEventPolicy.ActionEffectivenessPermission));
    internal static bool Team(TrustedActor? a) => Triager(a) || Investigator(a) || Reviewer(a);
}

internal static class SafetyEventReader
{
    internal static SafetyEventDraftFields Fields(string json) => JsonSerializer.Deserialize<SafetyEventDraftFields>(json, SafetyEventCommands.Json) ?? throw new InvalidDataException("safety_event_content_unavailable");
    internal static SafetyInvestigationContent Investigation(string json) => JsonSerializer.Deserialize<SafetyInvestigationContent>(json, SafetyEventCommands.Json) ?? throw new InvalidDataException("safety_investigation_unavailable");
    internal static CertificateFilePin[] Pins(string json) => JsonSerializer.Deserialize<CertificateFilePin[]>(json, SafetyEventCommands.Json) ?? [];
    internal static string Hash(SafetyEvent e) => RegisterStorage.Hash(JsonSerializer.Serialize(new { e.VesselId, e.Id, e.EventNo, e.Revision, e.State, e.ReportingMode,
        content = e.ContentJson, SafetyEventPolicy.SourceVersion }, SafetyEventCommands.Json));
    internal static string Hash(SafetyInvestigation v) => RegisterStorage.Hash(JsonSerializer.Serialize(new { v.VesselId, v.EventId, v.Revision, v.State,
        content = v.ContentJson, SafetyEventPolicy.SourceVersion }, SafetyEventCommands.Json));

    // Server-side only: whether the current actor is the reporter (NAMED head or CONFIDENTIAL vault binding).
    internal static async Task<bool> IsReporter(PoseidonDbContext db, SafetyEvent e, Guid actor, CancellationToken ct) =>
        e.ReportedBy == actor || e.ReportingMode == SafetyEventPolicy.Confidential &&
            await db.SafetyEventIdentities.AsNoTracking().AnyAsync(i => i.EventId == e.Id && i.VesselId == e.VesselId && i.ReporterId == actor, ct);

    // Drafts and withdrawn drafts are visible to their reporter only; submitted events to readers in vessel scope.
    internal static bool Visible(TrustedActor a, SafetyEvent e, bool mine) =>
        mine && SafetyEventAccess.Any(a) || SafetyEventAccess.Reader(a) && e.State is not (SafetyEventPolicy.Draft or SafetyEventPolicy.Withdrawn);

    internal static SafetyActionView View(SafetyAction a) => new(a.Id, a.State, a.Effectiveness, a.OwnerId, a.CompletedBy, a.VerifiedBy, a.EffectivenessBy,
        a.FollowUpOf, SafetyEventCommands.Ids(a.RootCauseIdsJson), SafetyEventCommands.Ids(a.BarrierIdsJson));

    internal static async Task<SafetyEventDto?> Detail(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, string? followUpSecret, HttpContext h, CancellationToken ct)
    {
        if (!await SafetyEventCommands.Vessels(db, a, vessel).AnyAsync(ct)) return null;
        var e = await db.SafetyEvents.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
        if (e == null) return null;
        var mine = await IsReporter(db, e, a.Id, ct);
        if (!Visible(a, e, mine)) return null;
        var reader = SafetyEventAccess.Reader(a); var team = SafetyEventAccess.Team(a);
        var fields = Fields(e.ContentJson);
        var triages = reader ? await db.SafetyEventTriages.AsNoTracking().Where(t => t.EventId == id && t.VesselId == vessel).OrderBy(t => t.Revision)
            .Select(t => new SafetyEventTriageDto(t.Id, t.ResultRevision, t.Decision, t.Severity, t.InvestigationRequired, t.Reason, t.ActorId, t.DecidedAt)).ToArrayAsync(ct) : [];
        var messages = await db.SafetyEventMessages.AsNoTracking().Where(m => m.EventId == id && m.VesselId == vessel && (team || m.Visibility == "REPORTER_SHARED"))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Take(200).Select(m => new SafetyEventMessageDto(m.Id, m.Visibility, m.AuthorKind, m.ActorId, m.Body, m.CreatedAt)).ToArrayAsync(ct);
        var head = reader ? await db.SafetyInvestigations.AsNoTracking().SingleOrDefaultAsync(v => v.EventId == id && v.VesselId == vessel, ct) : null;
        SafetyInvestigationDto? investigation = null; SafetyInvestigationContent? approved = null;
        if (head != null)
        {
            var content = Investigation(head.ContentJson); var authors = SafetyEventCommands.Ids(head.AuthorsJson);
            var decisions = await db.SafetyInvestigationDecisions.AsNoTracking().Where(d => d.EventId == id && d.VesselId == vessel).OrderBy(d => d.Revision)
                .Select(d => new SafetyInvestigationDecisionDto(d.Id, d.Revision, d.Decision, d.Reason, d.ActorId, d.DecidedAt)).ToArrayAsync(ct);
            if (head.State == "APPROVED") approved = content;
            var editable = head.State is "DRAFT" or "RETURNED" or "REJECTED";
            investigation = new(head.Revision, head.Version, head.State, content, head.Hash, head.ApprovedRevision, SafetyEventPolicy.InvestigationMissing(content), authors, decisions,
                editable && SafetyEventAccess.Investigator(a) && !mine,
                head.State is "DRAFT" or "RETURNED" && SafetyEventAccess.Investigator(a) && !mine && SafetyEventPolicy.InvestigationMissing(content).Length == 0,
                SafetyEventAccess.Reviewer(a) && CertificateRenewalEndpoints.Human(h, a) && SafetyEventPolicy.ReviewInvestigation(head.State, "APPROVE", a.Id, authors, head.SubmittedBy, mine).Ok);
        }
        var rows = reader ? await db.SafetyActions.AsNoTracking().Where(x => x.EventId == id && x.VesselId == vessel).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct) : [];
        var ids = rows.Select(r => r.Id).ToArray();
        var steps = ids.Length == 0 ? [] : await db.SafetyActionSteps.AsNoTracking().Where(s => ids.Contains(s.ActionId) && s.VesselId == vessel).OrderBy(s => s.ActionRevision).ToArrayAsync(ct);
        var today = DateOnly.FromDateTime(SafetyEventCommands.Now(h).UtcDateTime);
        var human = CertificateRenewalEndpoints.Human(h, a); var permissions = a.Permissions.ToHashSet(StringComparer.Ordinal);
        var actions = rows.Select(r => {
            var v = View(r);
            string[] allowed = e.State != SafetyEventPolicy.ActionTracking ? [] : SafetyEventPolicy.StepKinds.Where(k =>
                (k == "COMPLETE" || human) && SafetyEventPolicy.Step(v, k, k == "COMPLETE" ? null : k == "VERIFY" ? "VERIFIED" : "EFFECTIVE", a.Id, permissions).Ok).ToArray();
            return new SafetyActionDto(r.Id, r.Kind, r.Title, r.Description, r.OwnerId, r.DueDate, r.State == "OPEN" && r.DueDate < today,
                v.RootCauseIds, v.BarrierIds, r.FollowUpOf, r.Revision, r.Version, r.State, r.Effectiveness,
                steps.Where(s => s.ActionId == r.Id).Select(s => new SafetyActionStepDto(s.Id, s.ActionRevision, s.Kind, s.Outcome, s.Text, Pins(s.EvidenceJson), s.ActorId, s.RecordedAt)).ToArray(),
                allowed);
        }).ToArray();
        var views = rows.Select(View).ToArray();
        var blockers = reader && e.State is SafetyEventPolicy.Triaged or SafetyEventPolicy.ActionTracking
            ? SafetyEventPolicy.ClosureBlockers(e.State, e.InvestigationRequired, head?.State, approved, views) : [];
        var authorsOfInvestigation = head == null ? [] : SafetyEventCommands.Ids(head.AuthorsJson);
        var can = new SafetyEventPermissionsDto(
            SafetyEventPolicy.Edit(e.State, mine).Ok && SafetyEventAccess.Reporter(a),
            SafetyEventPolicy.Edit(e.State, mine).Ok && SafetyEventAccess.Reporter(a) && SafetyEventPolicy.Missing(fields).Length == 0,
            SafetyEventPolicy.Withdraw(e.State, mine).Ok && SafetyEventAccess.Reporter(a),
            SafetyEventAccess.Triager(a) && human && !mine && e.State is SafetyEventPolicy.Submitted or SafetyEventPolicy.Triaged,
            SafetyEventAccess.IdentityReader(a) && human && e.ReportingMode == SafetyEventPolicy.Confidential,
            SafetyEventAccess.Team(a) && SafetyEventPolicy.Open(e.State),
            SafetyEventAccess.Investigator(a) && !mine && (e.State == SafetyEventPolicy.Triaged && head == null || head?.State is "DRAFT" or "RETURNED" or "REJECTED"),
            investigation?.CanReview == true,
            SafetyEventAccess.ActionWriter(a) && e.State == SafetyEventPolicy.ActionTracking && approved != null,
            SafetyEventAccess.Reviewer(a) && human && !mine && !authorsOfInvestigation.Contains(a.Id) && e.State is SafetyEventPolicy.Triaged or SafetyEventPolicy.ActionTracking && blockers.Length == 0);
        var reporter = e.ReportingMode == SafetyEventPolicy.Named ? new SafetyEventReporterDto(SafetyEventPolicy.Named, e.ReportedBy, mine)
            : new SafetyEventReporterDto(SafetyEventPolicy.Confidential, null, mine);
        return new(e.Id, e.VesselId, e.EventNo, e.Revision, e.Version, e.State, e.ReportingMode, fields, e.Hash, SafetyEventPolicy.Missing(fields), reporter,
            reader ? e.Severity : null, fields.SeverityClaim, reader ? e.InvestigationRequired : null, "UNCONFIGURED", e.Origin, e.CreatedAt, e.SavedAt, e.SubmittedAt,
            e.ClosedBy, e.ClosedAt, reader ? e.ClosureReason : null, triages, investigation, actions, reader ? SafetyEventPolicy.Effectiveness(views) : "WITHHELD", blockers,
            messages, can, followUpSecret, e.Provenance, SafetyEventPolicy.SourceVersion);
    }
}

// DEV-060 read contract for other packages (readiness, high-risk work, voice drafts): open events per vessel that a
// consumer should treat as blockers or review items. Gated by hse.safety_event.read and vessel scope; protected
// reporter identity never appears. An empty list is NO_OPEN_HIGH_SEVERITY_EVENT, never READY.
public sealed record SafetyEventBlocker(Guid EventId, string EventNo, string? Category, string? Severity, string State, string Reason, bool Blocking);
public sealed record SafetyEventBlockerSnapshot(Guid VesselId, string State, SafetyEventBlocker[] Items, string[] Coverage, DateTimeOffset EvaluatedAt);
public interface ISafetyEventBlockerSource
{
    Task<SafetyEventBlockerSnapshot> ForVessel(PoseidonDbContext db, TrustedActor actor, Guid vessel, DateTimeOffset now, CancellationToken ct);
}
internal sealed class SafetyEventBlockerSource : ISafetyEventBlockerSource
{
    private const int MaxRows = 200;
    public async Task<SafetyEventBlockerSnapshot> ForVessel(PoseidonDbContext db, TrustedActor actor, Guid vessel, DateTimeOffset now, CancellationToken ct)
    {
        if (!SafetyEventAccess.Reader(actor) || !await SafetyEventCommands.Vessels(db, actor, vessel).AnyAsync(ct))
            return new(vessel, "UNAVAILABLE", [], ["SafetyEventReadRequired"], now);
        string[] open = [SafetyEventPolicy.Submitted, SafetyEventPolicy.Returned, SafetyEventPolicy.Triaged, SafetyEventPolicy.Investigating, SafetyEventPolicy.ActionTracking];
        var rows = await db.SafetyEvents.AsNoTracking().Where(e => e.VesselId == vessel && open.Contains(e.State)).OrderByDescending(e => e.SubmittedAt).ThenBy(e => e.Id)
            .Take(MaxRows + 1).Select(e => new { e.Id, e.EventNo, e.Category, e.Severity, e.State }).ToArrayAsync(ct);
        var coverage = new List<string> { "SeverityPolicyUnconfigured" };
        if (rows.Length > MaxRows) coverage.Add("SafetyEventSourceLimitExceeded");
        var items = rows.Take(MaxRows).Select(r => (r, reason: SafetyEventPolicy.BlockerReason(r.State, r.Severity))).Where(x => x.reason != null)
            .Select(x => new SafetyEventBlocker(x.r.Id, x.r.EventNo, x.r.Category, x.r.Severity, x.r.State, x.reason!, SafetyEventPolicy.Blocking(x.reason!)))
            .OrderByDescending(x => x.Blocking).ThenBy(x => x.EventNo, StringComparer.Ordinal).ToArray();
        var state = items.Any(i => i.Blocking) ? "BLOCKERS_PRESENT" : items.Length > 0 || rows.Length > MaxRows ? "REVIEW_REQUIRED" : "NO_OPEN_HIGH_SEVERITY_EVENT";
        return new(vessel, state, items, [.. coverage], now);
    }
}
