using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string ReportingMode,
    [property: JsonRequired] SafetyEventDraftFields Fields, string? ContactPreference = null)
{ public override string ToString() => nameof(SafetyEventCreateCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventUpdateCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] SafetyEventDraftFields Fields)
{ public override string ToString() => nameof(SafetyEventUpdateCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventSubmitCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventReasonCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Reason)
{ public override string ToString() => nameof(SafetyEventReasonCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventTriageCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Decision,
    string? Severity, string? InvestigationRequired, [property: JsonRequired] string Reason)
{ public override string ToString() => nameof(SafetyEventTriageCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventIdentityAccessCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string Purpose, [property: JsonRequired] string Reason)
{ public override string ToString() => nameof(SafetyEventIdentityAccessCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventMessageCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string Visibility, [property: JsonRequired] string Body)
{ public override string ToString() => nameof(SafetyEventMessageCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventFollowUpMessageCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string Body)
{ public override string ToString() => nameof(SafetyEventFollowUpMessageCommand); }

// Restricted vault read result: returned only to hse.safety_event.identity.read with a recorded purpose.
public sealed record SafetyEventIdentityDto(Guid AccessId, Guid EventId, Guid ReporterActorId, string ContactPreference, string Purpose, DateTimeOffset AccessedAt);
public sealed record SafetyEventFollowUpDto(string EventNo, string State, DateTimeOffset? SubmittedAt, SafetyEventMessageDto[] Messages);

// DEV-060 / T-S9-003 /api/v1/safety-events. Capture (normal or CONFIDENTIAL), draft edit, submit (POL-DEFAULT-01),
// withdraw, human triage/return, restricted identity vault access with purpose, team/reporter messages and the
// pseudonymous follow-up channel. No endpoint closes, classifies or decides anything automatically.
public static class SafetyEventEndpoints
{
    private const int MaxLimit = 50;

    public static void MapSafetyEvents(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/safety-events").RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("SafetyEventList").Produces<SafetyEventPageDto>();
        g.MapGet("/{id:guid}", Detail).WithName("SafetyEventRead").Produces<SafetyEventDto>();
        g.MapGet("/follow-up", FollowUp).WithName("SafetyEventFollowUpRead").Produces<SafetyEventFollowUpDto>();
        g.MapPost("/follow-up/messages", FollowUpMessage).WithName("SafetyEventFollowUpMessage").Accepts<SafetyEventFollowUpMessageCommand>("application/json")
            .Produces<SafetyEventFollowUpDto>();
        g.MapPost("", (HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyEventCreateCommand>(h, db, "SafetyEventCreate", null, null,
                (a, _) => SafetyEventAccess.Reporter(a), c => c.Id, Create, Fresh))
            .WithName("SafetyEventCreate").Accepts<SafetyEventCreateCommand>("application/json").Produces<SafetyEventDto>().Produces<SafetyFailure>(422);
        g.MapPut("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyEventUpdateCommand>(h, db, "SafetyEventUpdate", id, null,
                (a, _) => SafetyEventAccess.Reporter(a), _ => id, Update, Fresh))
            .WithName("SafetyEventUpdate").Accepts<SafetyEventUpdateCommand>("application/json").Produces<SafetyEventDto>();
        g.MapPost("/{id:guid}/submit", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyEventSubmitCommand>(h, db, "SafetyEventSubmit", id, null,
                (a, _) => SafetyEventAccess.Reporter(a), _ => id, Submit, Fresh))
            .WithName("SafetyEventSubmit").Accepts<SafetyEventSubmitCommand>("application/json").Produces<SafetyEventDto>().Produces<SafetyFailure>(422);
        g.MapPost("/{id:guid}/withdraw", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyEventReasonCommand>(h, db, "SafetyEventWithdraw", id, null,
                (a, _) => SafetyEventAccess.Reporter(a), _ => id, Withdraw, Fresh))
            .WithName("SafetyEventWithdraw").Accepts<SafetyEventReasonCommand>("application/json").Produces<SafetyEventDto>();
        g.MapPost("/{id:guid}/triage", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyEventTriageCommand>(h, db, "SafetyEventTriage", id, null,
                (a, _) => SafetyEventAccess.Triager(a), _ => id, Triage, Fresh, humanOnly: true))
            .WithName("SafetyEventTriage").Accepts<SafetyEventTriageCommand>("application/json").Produces<SafetyEventDto>();
        g.MapPost("/{id:guid}/identity-access", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyEventIdentityAccessCommand>(h, db,
                "SafetyEventIdentityAccess", id, null, (a, _) => SafetyEventAccess.IdentityReader(a), c => c.Id, IdentityAccess, IdentityRead, humanOnly: true))
            .WithName("SafetyEventIdentityAccess").Accepts<SafetyEventIdentityAccessCommand>("application/json").Produces<SafetyEventIdentityDto>();
        g.MapPost("/{id:guid}/messages", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyEventMessageCommand>(h, db, "SafetyEventMessage", id, null,
                (a, _) => SafetyEventAccess.Team(a), _ => id, Message, Fresh))
            .WithName("SafetyEventMessage").Accepts<SafetyEventMessageCommand>("application/json").Produces<SafetyEventDto>();
    }

    internal static async Task<object?> Fresh(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, SafetyOutcome? outcome, HttpContext h, CancellationToken ct) =>
        await SafetyEventReader.Detail(db, a, vessel, id, outcome?.Extra, h, ct);

    internal static string EventNo(Guid id, DateTimeOffset now) => "SE-" + now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + id.ToString("N")[..8].ToUpperInvariant();

    internal static void Append(PoseidonDbContext db, SafetyEvent e, string reason, Guid? actor, DateTimeOffset now) =>
        db.SafetyEventRevisions.Add(new() { TenantId = e.TenantId, VesselId = e.VesselId, EventId = e.Id, Revision = e.Revision, State = e.State, ReasonCode = reason,
            ContentJson = e.ContentJson, Hash = e.Hash, ActorId = actor, SavedAt = now, Provenance = e.Provenance });

    // Revision author on the history: null when the author is the protected reporter (no actor id leaks via history).
    private static Guid? HistoryActor(SafetyEvent e, Guid actor, bool reporter) => reporter && e.ReportingMode == SafetyEventPolicy.Confidential ? null : actor;

    internal static async Task<SafetyOutcome?> Load(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, uint expected, CancellationToken ct)
    {
        var e = await db.SafetyEvents.SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
        if (e == null) return SafetyOutcome.Fail(404, "resource_not_found");
        if (!SafetyEventReader.Visible(a, e, await SafetyEventReader.IsReporter(db, e, a.Id, ct))) return SafetyOutcome.Fail(404, "resource_not_found");
        if (e.Version != expected) return SafetyOutcome.Fail(409, "version_conflict");
        return null;
    }

    // Shared by the online create and the DEV-060 offline replay (Origin OFFLINE, always NAMED).
    internal static async Task<SafetyOutcome> CreateDraft(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, string mode, SafetyEventDraftFields fields,
        string? contact, string origin, DateTimeOffset now, CancellationToken ct)
    {
        if (id == Guid.Empty) return SafetyOutcome.Fail(400, "invalid_request");
        if (mode == SafetyEventPolicy.Anonymous) return SafetyOutcome.Fail(422, "anonymous_channel_unavailable");
        if (!SafetyEventPolicy.ReportingModes.Contains(mode) || !SafetyEventPolicy.ValidDraft(fields)) return SafetyOutcome.Fail(400, "invalid_request");
        if (mode == SafetyEventPolicy.Named ? contact != null : contact is not ("NONE" or "IN_APP_FOLLOW_UP")) return SafetyOutcome.Fail(400, "invalid_request");
        if (SafetyEventPolicy.InFuture(fields, now)) return SafetyOutcome.Fail(422, "occurrence_in_future");
        if (!await PreSurveyChecklistEndpoints.FilesVisible(db, a, vessel, fields.Evidence ?? [], ct)) return SafetyOutcome.Fail(422, "linked_resource_unavailable");
        if (await db.SafetyEvents.AnyAsync(x => x.Id == id, ct)) return SafetyOutcome.Fail(409, "version_conflict");
        var e = new SafetyEvent { TenantId = a.TenantId, VesselId = vessel, Id = id, EventNo = EventNo(id, now), ReportingMode = mode, Category = fields.Category,
            ContentJson = JsonSerializer.Serialize(fields, SafetyEventCommands.Json), ReportedBy = mode == SafetyEventPolicy.Named ? a.Id : null, Origin = origin,
            CreatedAt = now, SavedAt = now };
        e.Hash = SafetyEventReader.Hash(e);
        db.SafetyEvents.Add(e);
        if (mode == SafetyEventPolicy.Confidential)
            db.SafetyEventIdentities.Add(new() { TenantId = a.TenantId, VesselId = vessel, Id = Guid.NewGuid(), EventId = id, ReporterId = a.Id, ContactPreference = contact!, CreatedAt = now });
        await db.SaveChangesAsync(ct);
        Append(db, e, origin == "OFFLINE" ? "OFFLINE_CAPTURE" : "CREATE", mode == SafetyEventPolicy.Confidential ? null : a.Id, now); await db.SaveChangesAsync(ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }

    private static Task<SafetyOutcome> Create(SafetyContext<SafetyEventCreateCommand> c) =>
        CreateDraft(c.Db, c.Actor, c.Vessel, c.Command.Id, c.Command.ReportingMode, c.Command.Fields, c.Command.ContactPreference, "ONLINE", c.Now, c.Ct);

    private static async Task<SafetyOutcome> Update(SafetyContext<SafetyEventUpdateCommand> c)
    {
        if (await Load(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Command.ExpectedVersion, c.Ct) is { } fail) return fail;
        var e = await c.Db.SafetyEvents.SingleAsync(x => x.Id == c.RouteId, c.Ct); var reporter = await SafetyEventReader.IsReporter(c.Db, e, c.Actor.Id, c.Ct);
        if (SafetyEventPolicy.Edit(e.State, reporter) is { Ok: false } d) return SafetyOutcome.From(d);
        var f = c.Command.Fields;
        if (!SafetyEventPolicy.ValidDraft(f)) return SafetyOutcome.Fail(400, "invalid_request");
        if (SafetyEventPolicy.InFuture(f, c.Now)) return SafetyOutcome.Fail(422, "occurrence_in_future");
        if (!await PreSurveyChecklistEndpoints.FilesVisible(c.Db, c.Actor, c.Vessel, f.Evidence ?? [], c.Ct)) return SafetyOutcome.Fail(422, "linked_resource_unavailable");
        e.Revision++; e.Category = f.Category; e.ContentJson = JsonSerializer.Serialize(f, SafetyEventCommands.Json); e.SavedAt = c.Now; e.Hash = SafetyEventReader.Hash(e);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, e, "EDIT", HistoryActor(e, c.Actor.Id, true), c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }

    // CONFIDENTIAL with IN_APP_FOLLOW_UP: the submit response carries the follow-up token ONCE; only its hash is stored.
    private static async Task<SafetyOutcome> Submit(SafetyContext<SafetyEventSubmitCommand> c)
    {
        if (await Load(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Command.ExpectedVersion, c.Ct) is { } fail) return fail;
        var e = await c.Db.SafetyEvents.SingleAsync(x => x.Id == c.RouteId, c.Ct); var reporter = await SafetyEventReader.IsReporter(c.Db, e, c.Actor.Id, c.Ct);
        if (!string.Equals(e.Hash, c.Command.ContentHash, StringComparison.Ordinal)) return SafetyOutcome.Fail(409, "submitted_content_changed");
        var fields = SafetyEventReader.Fields(e.ContentJson);
        if (SafetyEventPolicy.Submit(e.State, reporter, fields, c.Now) is { Ok: false } d) return SafetyOutcome.From(d);
        if (!await PreSurveyChecklistEndpoints.FilesVisible(c.Db, c.Actor, c.Vessel, fields.Evidence ?? [], c.Ct)) return SafetyOutcome.Fail(422, "linked_resource_unavailable");
        string? token = null;
        if (e.ReportingMode == SafetyEventPolicy.Confidential)
        {
            var vault = await c.Db.SafetyEventIdentities.SingleAsync(i => i.EventId == e.Id && i.VesselId == e.VesselId, c.Ct);
            if (vault.ContactPreference == "IN_APP_FOLLOW_UP" && vault.FollowUpHandle == null)
            {
                var handle = Base64Url(RandomNumberGenerator.GetBytes(16));
                token = handle + "." + Base64Url(RandomNumberGenerator.GetBytes(32));
                vault.FollowUpHandle = handle; vault.FollowUpHash = RegisterStorage.Hash(token);
            }
        }
        e.Revision++; e.State = SafetyEventPolicy.Submitted; e.SubmittedAt = c.Now; e.SavedAt = c.Now; e.Hash = SafetyEventReader.Hash(e);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, e, "SUBMIT", HistoryActor(e, c.Actor.Id, true), c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision, token);
    }

    private static async Task<SafetyOutcome> Withdraw(SafetyContext<SafetyEventReasonCommand> c)
    {
        if (!SafetyEventPolicy.Text(c.Command.Reason, SafetyEventPolicy.MinStatement, 1000)) return SafetyOutcome.Fail(400, "invalid_reason");
        if (await Load(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Command.ExpectedVersion, c.Ct) is { } fail) return fail;
        var e = await c.Db.SafetyEvents.SingleAsync(x => x.Id == c.RouteId, c.Ct); var reporter = await SafetyEventReader.IsReporter(c.Db, e, c.Actor.Id, c.Ct);
        if (SafetyEventPolicy.Withdraw(e.State, reporter) is { Ok: false } d) return SafetyOutcome.From(d);
        e.Revision++; e.State = SafetyEventPolicy.Withdrawn; e.SavedAt = c.Now; e.Hash = SafetyEventReader.Hash(e);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, e, "WITHDRAW", HistoryActor(e, c.Actor.Id, true), c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }

    private static async Task<SafetyOutcome> Triage(SafetyContext<SafetyEventTriageCommand> c)
    {
        var cmd = c.Command;
        if (!SafetyEventPolicy.Text(cmd.Reason, SafetyEventPolicy.MinStatement, 2000)) return SafetyOutcome.Fail(400, "invalid_reason");
        if (await Load(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, cmd.ExpectedVersion, c.Ct) is { } fail) return fail;
        var e = await c.Db.SafetyEvents.SingleAsync(x => x.Id == c.RouteId, c.Ct); var reporter = await SafetyEventReader.IsReporter(c.Db, e, c.Actor.Id, c.Ct);
        if (SafetyEventPolicy.Triage(e.State, cmd.Decision, cmd.Severity, cmd.InvestigationRequired, reporter) is { Ok: false } d) return SafetyOutcome.From(d);
        // A re-triage cannot remove the investigation requirement once an investigation exists.
        if (e.State == SafetyEventPolicy.Triaged && await c.Db.SafetyInvestigations.AnyAsync(v => v.EventId == e.Id, c.Ct) && cmd.InvestigationRequired != "YES")
            return SafetyOutcome.Fail(409, "invalid_transition");
        c.Db.SafetyEventTriages.Add(new() { TenantId = e.TenantId, VesselId = e.VesselId, Id = Guid.NewGuid(), EventId = e.Id, Revision = e.Revision, ResultRevision = e.Revision + 1,
            Decision = cmd.Decision, Severity = cmd.Severity, InvestigationRequired = cmd.InvestigationRequired, Reason = cmd.Reason, ActorId = c.Actor.Id, DecidedAt = c.Now });
        e.Revision++; e.SavedAt = c.Now;
        if (cmd.Decision == "RETURN") e.State = SafetyEventPolicy.Returned;
        else { e.State = SafetyEventPolicy.Triaged; e.Severity = cmd.Severity; e.InvestigationRequired = cmd.InvestigationRequired; }
        e.Hash = SafetyEventReader.Hash(e);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, e, cmd.Decision == "RETURN" ? "RETURN" : "TRIAGE", c.Actor.Id, c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }

    private static async Task<SafetyOutcome> IdentityAccess(SafetyContext<SafetyEventIdentityAccessCommand> c)
    {
        var cmd = c.Command;
        if (cmd.Id == Guid.Empty || !SafetyEventPolicy.IdentityPurposes.Contains(cmd.Purpose) || !SafetyEventPolicy.Text(cmd.Reason, SafetyEventPolicy.MinStatement, 1000))
            return SafetyOutcome.Fail(400, "invalid_request");
        var e = await c.Db.SafetyEvents.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == c.Vessel && x.Id == c.RouteId, c.Ct);
        if (e == null || !SafetyEventReader.Visible(c.Actor, e, false)) return SafetyOutcome.Fail(404, "resource_not_found");
        if (e.ReportingMode != SafetyEventPolicy.Confidential) return SafetyOutcome.Fail(409, "identity_not_protected");
        if (await c.Db.SafetyEventIdentityAccesses.AnyAsync(x => x.Id == cmd.Id, c.Ct)) return SafetyOutcome.Fail(409, "version_conflict");
        c.Db.SafetyEventIdentityAccesses.Add(new() { TenantId = e.TenantId, VesselId = e.VesselId, Id = cmd.Id, EventId = e.Id, EventRevision = e.Revision, ActorId = c.Actor.Id,
            Purpose = cmd.Purpose, Reason = cmd.Reason, AccessedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(cmd.Id, e.Revision);
    }

    // The receipt resource is the access record; the vault is re-read under the CURRENT identity permission.
    private static async Task<object?> IdentityRead(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid access, SafetyOutcome? _, HttpContext h, CancellationToken ct)
    {
        if (!SafetyEventAccess.IdentityReader(a) || !CertificateRenewalEndpoints.Human(h, a) || !await SafetyEventCommands.Vessels(db, a, vessel).AnyAsync(ct)) return null;
        var row = await db.SafetyEventIdentityAccesses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == access && x.VesselId == vessel && x.ActorId == a.Id, ct);
        if (row == null) return null;
        var vault = await db.SafetyEventIdentities.AsNoTracking().SingleOrDefaultAsync(i => i.EventId == row.EventId && i.VesselId == vessel, ct);
        return vault == null ? null : new SafetyEventIdentityDto(row.Id, row.EventId, vault.ReporterId, vault.ContactPreference, row.Purpose, row.AccessedAt);
    }

    private static async Task<SafetyOutcome> Message(SafetyContext<SafetyEventMessageCommand> c)
    {
        var cmd = c.Command;
        if (cmd.Id == Guid.Empty || !SafetyEventPolicy.MessageVisibilities.Contains(cmd.Visibility) || !SafetyEventPolicy.Text(cmd.Body, 1, SafetyEventPolicy.MaxText))
            return SafetyOutcome.Fail(400, "invalid_request");
        var e = await c.Db.SafetyEvents.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == c.Vessel && x.Id == c.RouteId, c.Ct);
        if (e == null || !SafetyEventReader.Visible(c.Actor, e, false)) return SafetyOutcome.Fail(404, "resource_not_found");
        if (!SafetyEventPolicy.Open(e.State)) return SafetyOutcome.Fail(409, "invalid_state");
        if (await c.Db.SafetyEventMessages.AnyAsync(m => m.Id == cmd.Id, c.Ct)) return SafetyOutcome.Fail(409, "version_conflict");
        c.Db.SafetyEventMessages.Add(new() { TenantId = e.TenantId, VesselId = e.VesselId, Id = cmd.Id, EventId = e.Id, EventRevision = e.Revision, Visibility = cmd.Visibility,
            AuthorKind = "TEAM", ActorId = c.Actor.Id, Body = cmd.Body, Hash = RegisterStorage.Hash(cmd.Body), CreatedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }

    private static bool Paging(HttpContext h, out string? state, out bool mine, out int offset, out int limit)
    {
        state = null; mine = false; offset = 0; limit = 25;
        if (h.Request.Query.TryGetValue("state", out var s)) { if (!SafetyEventPolicy.States.Contains(s.ToString())) return false; state = s; }
        if (h.Request.Query.TryGetValue("mine", out var m)) { if (m != "true" && m != "false") return false; mine = m == "true"; }
        if (h.Request.Query.TryGetValue("offset", out var o) && (!int.TryParse(o, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 10000)) return false;
        if (h.Request.Query.TryGetValue("limit", out var l) && (!int.TryParse(l, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > MaxLimit)) return false;
        return true;
    }

    internal static SafetyEventCapabilitiesDto Capabilities(HttpContext h, TrustedActor a, Guid vessel) => new(vessel, SafetyEventAccess.Reader(a), SafetyEventAccess.Reporter(a),
        SafetyEventAccess.Triager(a), SafetyEventAccess.Investigator(a), SafetyEventAccess.Reviewer(a), SafetyEventAccess.ActionWriter(a),
        SafetyEventAccess.Reader(a) && a.Permissions.Contains(SafetyEventPolicy.ActionVerifyPermission),
        SafetyEventAccess.Reader(a) && a.Permissions.Contains(SafetyEventPolicy.ActionEffectivenessPermission), SafetyEventAccess.IdentityReader(a),
        "UNAVAILABLE_GATEWAY_UNCONFIGURED", SafetyEventOfflineEndpoints.Capability(h, a, vessel), SafetyEventPolicy.Categories, SafetyEventPolicy.ReportingModes,
        SafetyEventPolicy.OperationContexts, SafetyEventPolicy.Severities, SafetyEventPolicy.SourceVersion);

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = SafetyEventCommands.Actor(h);
        if (!SafetyEventAccess.Any(a)) return await SafetyEventCommands.Fail(h, SafetyEventCommands.ReadAction, 403, "permission_denied");
        if (!SafetyEventCommands.Query(h, out var vessel, "state", "mine", "offset", "limit") || !Paging(h, out var state, out var mineOnly, out var offset, out var limit))
            return await SafetyEventCommands.Fail(h, SafetyEventCommands.ReadAction, 400, "invalid_query");
        if (!await SafetyEventCommands.Vessels(db, a!, vessel).AnyAsync(ct)) return await SafetyEventCommands.Fail(h, SafetyEventCommands.ReadAction, 404, "resource_not_found");
        var actor = a!.Id; var reader = SafetyEventAccess.Reader(a);
        var vaulted = db.SafetyEventIdentities.Where(i => i.VesselId == vessel && i.ReporterId == actor).Select(i => i.EventId);
        var query = db.SafetyEvents.AsNoTracking().Where(e => e.VesselId == vessel);
        // Not a reader (or "mine"): only the actor's own reports. Readers never see other people's drafts.
        query = !reader || mineOnly ? query.Where(e => e.ReportedBy == actor || vaulted.Contains(e.Id))
            : query.Where(e => e.ReportedBy == actor || vaulted.Contains(e.Id) || e.State != SafetyEventPolicy.Draft && e.State != SafetyEventPolicy.Withdrawn);
        if (state != null) query = query.Where(e => e.State == state);
        var rows = await query.OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).Skip(offset).Take(limit + 1)
            .Select(e => new { e.Id, e.EventNo, e.State, e.ReportingMode, e.Category, e.ContentJson, e.Severity, e.Origin, e.SavedAt, Mine = e.ReportedBy == actor || vaulted.Contains(e.Id) })
            .ToArrayAsync(ct);
        var items = rows.Take(limit).Select(r => {
            var f = SafetyEventReader.Fields(r.ContentJson);
            return new SafetyEventListItemDto(r.Id, r.EventNo, r.State, r.ReportingMode, r.Category, f.Summary, f.OccurrencePrecision, f.OccurredAt, f.OccurrenceDate,
                reader ? r.Severity : null, r.Mine, r.Origin, r.SavedAt);
        }).ToArray();
        var blockers = await h.RequestServices.GetRequiredService<ISafetyEventBlockerSource>().ForVessel(db, a, vessel, SafetyEventCommands.Now(h), ct);
        await SafetyEventCommands.Audit(h, SafetyEventCommands.ReadAction, "Success", vessel);
        return Results.Json(new SafetyEventPageDto(items, offset, limit, rows.Length > limit, blockers, Capabilities(h, a, vessel)), SafetyEventCommands.Json);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var a = SafetyEventCommands.Actor(h);
        if (!SafetyEventAccess.Any(a)) return await SafetyEventCommands.Fail(h, SafetyEventCommands.ReadAction, 403, "permission_denied", id);
        if (!SafetyEventCommands.Query(h, out var vessel)) return await SafetyEventCommands.Fail(h, SafetyEventCommands.ReadAction, 400, "invalid_query", id);
        var dto = await SafetyEventReader.Detail(db, a!, vessel, id, null, h, h.RequestAborted);
        // Hidden, foreign, draft-of-another and missing records share one generic 404.
        if (dto == null) return await SafetyEventCommands.Fail(h, SafetyEventCommands.ReadAction, 404, "resource_not_found", id);
        await SafetyEventCommands.Audit(h, SafetyEventCommands.ReadAction, "Success", id);
        return Results.Json(dto, SafetyEventCommands.Json);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // Pseudonymous reporter channel: the token in the X-Safety-Follow-Up header (never in the URL) is the authority;
    // no vessel scope or identity lookup is involved. Every failure is the same generic 404.
    private static async Task<SafetyEventIdentity?> Token(HttpContext h, PoseidonDbContext db, CancellationToken ct)
    {
        var header = h.Request.Headers["X-Safety-Follow-Up"];
        if (header.Count != 1 || header[0] is not { Length: > 23 and < 100 } token || token[22] != '.') return null;
        var handle = token[..22];
        var vault = await db.SafetyEventIdentities.AsNoTracking().SingleOrDefaultAsync(i => i.FollowUpHandle == handle, ct);
        if (vault?.FollowUpHash == null) return null;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(vault.FollowUpHash), Encoding.ASCII.GetBytes(RegisterStorage.Hash(token))) ? vault : null;
    }

    private static async Task<SafetyEventFollowUpDto?> FollowUpView(PoseidonDbContext db, SafetyEventIdentity vault, CancellationToken ct)
    {
        var e = await db.SafetyEvents.AsNoTracking().SingleAsync(x => x.Id == vault.EventId && x.VesselId == vault.VesselId, ct);
        var messages = await db.SafetyEventMessages.AsNoTracking().Where(m => m.EventId == e.Id && m.VesselId == e.VesselId && m.Visibility == "REPORTER_SHARED")
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Take(200).Select(m => new SafetyEventMessageDto(m.Id, m.Visibility, m.AuthorKind, null, m.Body, m.CreatedAt)).ToArrayAsync(ct);
        return new(e.EventNo, e.State, e.SubmittedAt, messages);
    }

    private static async Task<IResult> FollowUp(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var a = SafetyEventCommands.Actor(h);
        if (!SafetyEventAccess.Reporter(a) || db.TenantId != a!.TenantId || h.Request.Query.Count != 0) return await SafetyEventCommands.Fail(h, SafetyEventCommands.ReadAction, 404, "resource_unavailable");
        var vault = await Token(h, db, h.RequestAborted);
        if (vault == null) return await SafetyEventCommands.Fail(h, SafetyEventCommands.ReadAction, 404, "resource_unavailable");
        var view = await FollowUpView(db, vault, h.RequestAborted);
        await SafetyEventCommands.Audit(h, SafetyEventCommands.ReadAction, "Success");
        return Results.Json(view, SafetyEventCommands.Json);
    }

    // Reporter message through the follow-up token. Deliberately NO actor receipt (which would bind the actor to the
    // event): idempotency is the message id (= Idempotency-Key) plus its content hash.
    private static async Task<IResult> FollowUpMessage(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted; const string action = SafetyEventCommands.CommandAction;
        var a = SafetyEventCommands.Actor(h);
        if (!SafetyEventAccess.Reporter(a) || !a!.HasWriteAttribution || db.TenantId != a.TenantId || h.Request.Query.Count != 0)
            return await SafetyEventCommands.Fail(h, action, 404, "resource_unavailable");
        if (!await SafetyEventCommands.Csrf(h)) return await SafetyEventCommands.Fail(h, action, 403, "csrf_invalid");
        SafetyEventFollowUpMessageCommand? cmd;
        try { cmd = await SafetyEventCommands.Body<SafetyEventFollowUpMessageCommand>(h, ct); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException) { cmd = null; }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (cmd == null || cmd.Id == Guid.Empty || !SafetyEventPolicy.Text(cmd.Body, 1, SafetyEventPolicy.MaxText)) return await SafetyEventCommands.Fail(h, action, 400, "invalid_request");
        if (keys.Count != 1 || keys[0] != cmd.Id.ToString("D")) return await SafetyEventCommands.Fail(h, action, 400, "invalid_idempotency_key");
        var vault = await Token(h, db, ct);
        if (vault == null) return await SafetyEventCommands.Fail(h, action, 404, "resource_unavailable");
        await SafetyEventCommands.Audit(h, action, "Attempted", cmd.Id);
        string? failure = null; var hash = RegisterStorage.Hash(cmd.Body);
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                var existing = await db.SafetyEventMessages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == cmd.Id, ct);
                if (existing != null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (existing.EventId != vault.EventId || existing.AuthorKind != "REPORTER" || existing.Hash != hash) return await SafetyEventCommands.Fail(h, action, 409, "command_reuse_conflict", cmd.Id);
                }
                else
                {
                    var e = await db.SafetyEvents.AsNoTracking().SingleAsync(x => x.Id == vault.EventId && x.VesselId == vault.VesselId, ct);
                    if (!SafetyEventPolicy.Open(e.State)) failure = "invalid_state";
                    else
                    {
                        db.SafetyEventMessages.Add(new() { TenantId = e.TenantId, VesselId = e.VesselId, Id = cmd.Id, EventId = e.Id, EventRevision = e.Revision, Visibility = "REPORTER_SHARED",
                            AuthorKind = "REPORTER", ActorId = null, Body = cmd.Body, Hash = hash, CreatedAt = SafetyEventCommands.Now(h) });
                        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                    }
                    if (failure != null) await tx.RollbackAsync(CancellationToken.None);
                }
            }
            catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
            {
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                failure = PostgresFailure.IsConflict(ex) ? "version_conflict" : "service_unavailable";
            }
        }
        if (failure != null) return await SafetyEventCommands.Fail(h, action, failure == "invalid_state" ? 409 : failure == "version_conflict" ? 409 : 503, failure, cmd.Id);
        try
        {
            db.ChangeTracker.Clear();
            var view = await FollowUpView(db, vault, ct);
            await SafetyEventCommands.Audit(h, action, "Success", cmd.Id);
            return Results.Json(view, SafetyEventCommands.Json);
        }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex))
        { return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503); }
    }
}
