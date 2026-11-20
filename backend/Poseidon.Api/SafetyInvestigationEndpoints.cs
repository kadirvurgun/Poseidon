using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyInvestigationSaveCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] SafetyInvestigationContent Content)
{ public override string ToString() => nameof(SafetyInvestigationSaveCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyInvestigationSubmitCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyInvestigationReviewCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason)
{ public override string ToString() => nameof(SafetyInvestigationReviewCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyActionCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string Kind, [property: JsonRequired] string Title,
    [property: JsonRequired] string Description, [property: JsonRequired] Guid OwnerId, [property: JsonRequired] DateOnly DueDate,
    [property: JsonRequired] Guid[] RootCauseIds, [property: JsonRequired] Guid[] BarrierIds, Guid? FollowUpOf = null)
{ public override string ToString() => nameof(SafetyActionCreateCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyActionStepCommand([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] string Kind, string? Outcome,
    [property: JsonRequired] string Text, CertificateFilePin[]? Evidence = null)
{ public override string ToString() => nameof(SafetyActionStepCommand); }

// DEV-061 / T-S9-004: investigation (contributing factors, human root causes, failed/missing barriers), independent
// review, corrective/preventive actions with owner/due/evidence, verification, effectiveness review and an
// independent closure. Closed != Effective: both are separate human records; nothing is decided automatically.
public static class SafetyInvestigationEndpoints
{
    public static void MapSafetyInvestigations(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/safety-events/{id:guid}").RequireAuthorization("DirectoryRead");
        g.MapPut("/investigation", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyInvestigationSaveCommand>(h, db, "SafetyInvestigationSave", id, null,
                (a, _) => SafetyEventAccess.Investigator(a), _ => id, Save, SafetyEventEndpoints.Fresh))
            .WithName("SafetyInvestigationSave").Accepts<SafetyInvestigationSaveCommand>("application/json").Produces<SafetyEventDto>();
        g.MapPost("/investigation/submit", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyInvestigationSubmitCommand>(h, db, "SafetyInvestigationSubmit", id, null,
                (a, _) => SafetyEventAccess.Investigator(a), _ => id, Submit, SafetyEventEndpoints.Fresh))
            .WithName("SafetyInvestigationSubmit").Accepts<SafetyInvestigationSubmitCommand>("application/json").Produces<SafetyEventDto>().Produces<SafetyFailure>(422);
        g.MapPost("/investigation/review", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyInvestigationReviewCommand>(h, db, "SafetyInvestigationReview", id, null,
                (a, _) => SafetyEventAccess.Reviewer(a), _ => id, Review, SafetyEventEndpoints.Fresh, humanOnly: true))
            .WithName("SafetyInvestigationReview").Accepts<SafetyInvestigationReviewCommand>("application/json").Produces<SafetyEventDto>();
        g.MapPost("/actions", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyActionCreateCommand>(h, db, "SafetyActionCreate", id, null,
                (a, _) => SafetyEventAccess.ActionWriter(a), _ => id, CreateAction, SafetyEventEndpoints.Fresh))
            .WithName("SafetyActionCreate").Accepts<SafetyActionCreateCommand>("application/json").Produces<SafetyEventDto>().Produces<SafetyFailure>(422);
        g.MapPost("/actions/{action:guid}/steps", (Guid id, Guid action, HttpContext h, PoseidonDbContext db) => Step(id, action, h, db))
            .WithName("SafetyActionStep").Accepts<SafetyActionStepCommand>("application/json").Produces<SafetyEventDto>();
        g.MapPost("/close", (Guid id, HttpContext h, PoseidonDbContext db) => SafetyEventCommands.Run<SafetyEventReasonCommand>(h, db, "SafetyEventClose", id, null,
                (a, _) => SafetyEventAccess.Reviewer(a), _ => id, Close, SafetyEventEndpoints.Fresh, humanOnly: true))
            .WithName("SafetyEventClose").Accepts<SafetyEventReasonCommand>("application/json").Produces<SafetyEventDto>().Produces<SafetyFailure>(422);
    }

    // VERIFY and EFFECTIVENESS are human-only records; COMPLETE (owner reporting work done) is not.
    private static async Task<IResult> Step(Guid id, Guid action, HttpContext h, PoseidonDbContext db)
    {
        var human = true;
        if (h.Request.ContentLength is > 0 and <= 262144) try
        {
            h.Request.EnableBuffering();
            using var doc = await JsonDocument.ParseAsync(h.Request.Body, new() { MaxDepth = 16 }, h.RequestAborted);
            human = !(doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String && k.GetString() == "COMPLETE");
        }
        catch (JsonException) { }
        finally { h.Request.Body.Position = 0; }
        return await SafetyEventCommands.Run<SafetyActionStepCommand>(h, db, "SafetyActionStep", id, action, (a, _) => SafetyEventAccess.ActionActor(a), _ => id, ApplyStep,
            SafetyEventEndpoints.Fresh, humanOnly: human);
    }

    private static string Hash(PoseidonDbContext db, SafetyInvestigation v) => SafetyEventReader.Hash(v);
    private static void Append(PoseidonDbContext db, SafetyInvestigation v, string reason, Guid actor, DateTimeOffset now) =>
        db.SafetyInvestigationRevisions.Add(new() { TenantId = v.TenantId, VesselId = v.VesselId, EventId = v.EventId, Revision = v.Revision, State = v.State, ReasonCode = reason,
            ContentJson = v.ContentJson, Hash = v.Hash, ActorId = actor, SavedAt = now });
    private static async Task Advance(PoseidonDbContext db, SafetyEvent e, string state, string reason, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        e.Revision++; e.State = state; e.SavedAt = now; e.Hash = SafetyEventReader.Hash(e);
        await db.SaveChangesAsync(ct); SafetyEventEndpoints.Append(db, e, reason, actor, now); await db.SaveChangesAsync(ct);
    }

    private static async Task<(SafetyEvent? Event, bool Reporter, SafetyOutcome? Fail)> Event(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, CancellationToken ct)
    {
        var e = await db.SafetyEvents.SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
        if (e == null) return (null, false, SafetyOutcome.Fail(404, "resource_not_found"));
        var reporter = await SafetyEventReader.IsReporter(db, e, a.Id, ct);
        return SafetyEventReader.Visible(a, e, reporter) ? (e, reporter, null) : (null, false, SafetyOutcome.Fail(404, "resource_not_found"));
    }

    // Opens (TRIAGED, no investigation), edits (DRAFT/RETURNED) or revises a REJECTED investigation into a new DRAFT.
    // ExpectedVersion 0 = no investigation exists yet. The reporter never investigates their own report.
    private static async Task<SafetyOutcome> Save(SafetyContext<SafetyInvestigationSaveCommand> c)
    {
        var (e, reporter, fail) = await Event(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Ct); if (fail != null) return fail;
        if (reporter) return SafetyOutcome.Fail(403, "separation_of_duties");
        var content = c.Command.Content;
        if (!SafetyEventPolicy.ValidInvestigation(content)) return SafetyOutcome.Fail(400, "invalid_request");
        if (!await PreSurveyChecklistEndpoints.FilesVisible(c.Db, c.Actor, c.Vessel, content.Evidence, c.Ct)) return SafetyOutcome.Fail(422, "linked_resource_unavailable");
        var json = JsonSerializer.Serialize(content, SafetyEventCommands.Json);
        var v = await c.Db.SafetyInvestigations.SingleOrDefaultAsync(x => x.EventId == e!.Id && x.VesselId == c.Vessel, c.Ct);
        if (v == null)
        {
            if (c.Command.ExpectedVersion != 0) return SafetyOutcome.Fail(409, "version_conflict");
            if (e!.State != SafetyEventPolicy.Triaged) return SafetyOutcome.Fail(409, "invalid_state");
            v = new SafetyInvestigation { TenantId = e.TenantId, VesselId = e.VesselId, EventId = e.Id, ContentJson = json, AuthorsJson = SafetyEventCommands.WithId("[]", c.Actor.Id),
                ActorId = c.Actor.Id, SavedAt = c.Now };
            v.Hash = Hash(c.Db, v); c.Db.SafetyInvestigations.Add(v); await c.Db.SaveChangesAsync(c.Ct);
            Append(c.Db, v, "CREATE", c.Actor.Id, c.Now);
            await Advance(c.Db, e, SafetyEventPolicy.Investigating, "INVESTIGATION_OPENED", c.Actor.Id, c.Now, c.Ct);
            return SafetyOutcome.Ok(e.Id, e.Revision);
        }
        if (v.Version != c.Command.ExpectedVersion) return SafetyOutcome.Fail(409, "version_conflict");
        if (v.State is not ("DRAFT" or "RETURNED" or "REJECTED")) return SafetyOutcome.Fail(409, "invalid_state");
        var revise = v.State == "REJECTED";
        if (revise && e!.State != SafetyEventPolicy.Triaged) return SafetyOutcome.Fail(409, "invalid_state");
        v.Revision++; v.State = revise ? "DRAFT" : v.State; v.ContentJson = json; v.AuthorsJson = SafetyEventCommands.WithId(v.AuthorsJson, c.Actor.Id);
        v.SubmittedBy = null; v.ActorId = c.Actor.Id; v.SavedAt = c.Now; v.Hash = Hash(c.Db, v);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, v, revise ? "REVISE" : "EDIT", c.Actor.Id, c.Now); await c.Db.SaveChangesAsync(c.Ct);
        if (revise) await Advance(c.Db, e!, SafetyEventPolicy.Investigating, "INVESTIGATION_OPENED", c.Actor.Id, c.Now, c.Ct);
        return SafetyOutcome.Ok(e!.Id, e.Revision);
    }

    private static async Task<SafetyOutcome> Submit(SafetyContext<SafetyInvestigationSubmitCommand> c)
    {
        var (e, reporter, fail) = await Event(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Ct); if (fail != null) return fail;
        if (reporter) return SafetyOutcome.Fail(403, "separation_of_duties");
        var v = await c.Db.SafetyInvestigations.SingleOrDefaultAsync(x => x.EventId == e!.Id && x.VesselId == c.Vessel, c.Ct);
        if (v == null) return SafetyOutcome.Fail(404, "resource_not_found");
        if (v.Version != c.Command.ExpectedVersion) return SafetyOutcome.Fail(409, "version_conflict");
        if (!string.Equals(v.Hash, c.Command.ContentHash, StringComparison.Ordinal)) return SafetyOutcome.Fail(409, "submitted_content_changed");
        if (v.State is not ("DRAFT" or "RETURNED") || e!.State != SafetyEventPolicy.Investigating) return SafetyOutcome.Fail(409, "invalid_state");
        var missing = SafetyEventPolicy.InvestigationMissing(SafetyEventReader.Investigation(v.ContentJson));
        if (missing.Length > 0) return SafetyOutcome.Fail(422, "investigation_incomplete", missing);
        v.Revision++; v.State = "SUBMITTED"; v.SubmittedBy = c.Actor.Id; v.ActorId = c.Actor.Id; v.SavedAt = c.Now; v.Hash = Hash(c.Db, v);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, v, "SUBMIT", c.Actor.Id, c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }

    private static async Task<SafetyOutcome> Review(SafetyContext<SafetyInvestigationReviewCommand> c)
    {
        var cmd = c.Command;
        if (!SafetyEventPolicy.Text(cmd.Reason, SafetyEventPolicy.MinStatement, 2000)) return SafetyOutcome.Fail(400, "invalid_reason");
        var (e, reporter, fail) = await Event(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Ct); if (fail != null) return fail;
        var v = await c.Db.SafetyInvestigations.SingleOrDefaultAsync(x => x.EventId == e!.Id && x.VesselId == c.Vessel, c.Ct);
        if (v == null) return SafetyOutcome.Fail(404, "resource_not_found");
        if (v.Version != cmd.ExpectedVersion) return SafetyOutcome.Fail(409, "version_conflict");
        if (!string.Equals(v.Hash, cmd.ContentHash, StringComparison.Ordinal)) return SafetyOutcome.Fail(409, "submitted_content_changed");
        if (SafetyEventPolicy.ReviewInvestigation(v.State, cmd.Decision, c.Actor.Id, SafetyEventCommands.Ids(v.AuthorsJson), v.SubmittedBy, reporter) is { Ok: false } d) return SafetyOutcome.From(d);
        c.Db.SafetyInvestigationDecisions.Add(new() { TenantId = v.TenantId, VesselId = v.VesselId, Id = Guid.NewGuid(), EventId = v.EventId, Revision = v.Revision,
            Decision = cmd.Decision, ContentHash = v.Hash, Reason = cmd.Reason, ActorId = c.Actor.Id, DecidedAt = c.Now });
        v.Revision++; v.State = cmd.Decision switch { "APPROVE" => "APPROVED", "RETURN" => "RETURNED", _ => "REJECTED" };
        v.ApprovedRevision = cmd.Decision == "APPROVE" ? v.Revision : v.ApprovedRevision; v.ActorId = c.Actor.Id; v.SavedAt = c.Now; v.Hash = Hash(c.Db, v);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, v, cmd.Decision, c.Actor.Id, c.Now); await c.Db.SaveChangesAsync(c.Ct);
        if (cmd.Decision == "APPROVE") await Advance(c.Db, e!, SafetyEventPolicy.ActionTracking, "INVESTIGATION_APPROVED", c.Actor.Id, c.Now, c.Ct);
        else if (cmd.Decision == "REJECT") await Advance(c.Db, e!, SafetyEventPolicy.Triaged, "INVESTIGATION_REJECTED", c.Actor.Id, c.Now, c.Ct);
        return SafetyOutcome.Ok(e!.Id, e.Revision);
    }

    private static async Task<SafetyOutcome> CreateAction(SafetyContext<SafetyActionCreateCommand> c)
    {
        var cmd = c.Command;
        if (cmd.Id == Guid.Empty || !SafetyEventPolicy.ActionKinds.Contains(cmd.Kind) || !SafetyEventPolicy.Text(cmd.Title, 3, 200) ||
            !SafetyEventPolicy.Text(cmd.Description, SafetyEventPolicy.MinStatement, SafetyEventPolicy.MaxText) || cmd.OwnerId == Guid.Empty ||
            cmd.RootCauseIds.Length + cmd.BarrierIds.Length is 0 or > SafetyEventPolicy.MaxItems ||
            cmd.RootCauseIds.Distinct().Count() != cmd.RootCauseIds.Length || cmd.BarrierIds.Distinct().Count() != cmd.BarrierIds.Length)
            return SafetyOutcome.Fail(400, "invalid_request");
        var (e, _, fail) = await Event(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Ct); if (fail != null) return fail;
        if (e!.State != SafetyEventPolicy.ActionTracking) return SafetyOutcome.Fail(409, "invalid_state");
        var v = await c.Db.SafetyInvestigations.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == e.Id && x.VesselId == c.Vessel, c.Ct);
        if (v?.State != "APPROVED") return SafetyOutcome.Fail(409, "invalid_state");
        var approved = SafetyEventReader.Investigation(v.ContentJson);
        if (cmd.RootCauseIds.Any(r => approved.RootCauses.All(x => x.Id != r)) || cmd.BarrierIds.Any(b => approved.Barriers.All(x => x.Id != b)))
            return SafetyOutcome.Fail(422, "linked_resource_unavailable");
        // The owner must be a server-provisioned member of this tenant; a name typed by the user is not an owner.
        var members = c.Http.RequestServices.GetRequiredService<FederationSettings>().Memberships;
        if (!members.Any(m => m.ActorId == cmd.OwnerId && m.TenantId == c.Actor.TenantId)) return SafetyOutcome.Fail(422, "owner_unavailable");
        if (cmd.DueDate < DateOnly.FromDateTime(c.Now.UtcDateTime)) return SafetyOutcome.Fail(422, "due_date_past");
        if (cmd.FollowUpOf is { } previous && !await c.Db.SafetyActions.AnyAsync(a => a.Id == previous && a.EventId == e.Id && a.Effectiveness == "NOT_EFFECTIVE", c.Ct))
            return SafetyOutcome.Fail(422, "linked_resource_unavailable");
        if (await c.Db.SafetyActions.AnyAsync(a => a.Id == cmd.Id, c.Ct)) return SafetyOutcome.Fail(409, "version_conflict");
        c.Db.SafetyActions.Add(new() { TenantId = e.TenantId, VesselId = e.VesselId, Id = cmd.Id, EventId = e.Id, Kind = cmd.Kind, Title = cmd.Title, Description = cmd.Description,
            OwnerId = cmd.OwnerId, DueDate = cmd.DueDate, RootCauseIdsJson = JsonSerializer.Serialize(cmd.RootCauseIds.Order().ToArray(), SafetyEventCommands.Json),
            BarrierIdsJson = JsonSerializer.Serialize(cmd.BarrierIds.Order().ToArray(), SafetyEventCommands.Json), FollowUpOf = cmd.FollowUpOf,
            InvestigationRevision = v.Revision, CreatedBy = c.Actor.Id, CreatedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }

    private static async Task<SafetyOutcome> ApplyStep(SafetyContext<SafetyActionStepCommand> c)
    {
        var cmd = c.Command;
        if (!SafetyEventPolicy.Text(cmd.Text, SafetyEventPolicy.MinStatement, SafetyEventPolicy.MaxText) || !SafetyEventPolicy.ValidPins(cmd.Evidence))
            return SafetyOutcome.Fail(400, "invalid_request");
        var (e, _, fail) = await Event(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Ct); if (fail != null) return fail;
        if (e!.State != SafetyEventPolicy.ActionTracking) return SafetyOutcome.Fail(409, "invalid_state");
        var a = await c.Db.SafetyActions.SingleOrDefaultAsync(x => x.Id == c.SubId && x.EventId == e.Id && x.VesselId == c.Vessel, c.Ct);
        if (a == null) return SafetyOutcome.Fail(404, "resource_not_found");
        if (a.Revision != cmd.ExpectedRevision) return SafetyOutcome.Fail(409, "version_conflict");
        if (SafetyEventPolicy.Step(SafetyEventReader.View(a), cmd.Kind, cmd.Outcome, c.Actor.Id, c.Actor.Permissions.ToHashSet(StringComparer.Ordinal)) is { Ok: false } d)
            return SafetyOutcome.From(d);
        if (!await PreSurveyChecklistEndpoints.FilesVisible(c.Db, c.Actor, c.Vessel, cmd.Evidence ?? [], c.Ct)) return SafetyOutcome.Fail(422, "linked_resource_unavailable");
        a.Revision++;
        switch (cmd.Kind)
        {
            case "COMPLETE": a.State = "COMPLETED"; a.CompletedBy = c.Actor.Id; break;
            case "VERIFY":
                if (cmd.Outcome == "VERIFIED") { a.State = "VERIFIED"; a.VerifiedBy = c.Actor.Id; }
                else { a.State = "OPEN"; a.CompletedBy = null; } // rejected work returns to the owner
                break;
            default: a.Effectiveness = cmd.Outcome!; a.EffectivenessBy = c.Actor.Id; break;
        }
        c.Db.SafetyActionSteps.Add(new() { TenantId = a.TenantId, VesselId = a.VesselId, Id = Guid.NewGuid(), EventId = a.EventId, ActionId = a.Id, ActionRevision = a.Revision,
            Kind = cmd.Kind, Outcome = cmd.Outcome, Text = cmd.Text, EvidenceJson = JsonSerializer.Serialize(cmd.Evidence ?? [], SafetyEventCommands.Json), ActorId = c.Actor.Id,
            RecordedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }

    // Independent closure: the reviewer is neither the reporter nor an investigation author and cannot bypass a blocker.
    private static async Task<SafetyOutcome> Close(SafetyContext<SafetyEventReasonCommand> c)
    {
        if (!SafetyEventPolicy.Text(c.Command.Reason, SafetyEventPolicy.MinStatement, 2000)) return SafetyOutcome.Fail(400, "invalid_reason");
        var (e, reporter, fail) = await Event(c.Db, c.Actor, c.Vessel, c.RouteId!.Value, c.Ct); if (fail != null) return fail;
        if (e!.Version != c.Command.ExpectedVersion) return SafetyOutcome.Fail(409, "version_conflict");
        if (e.State is not (SafetyEventPolicy.Triaged or SafetyEventPolicy.ActionTracking)) return SafetyOutcome.Fail(409, "invalid_state");
        var v = await c.Db.SafetyInvestigations.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == e.Id && x.VesselId == c.Vessel, c.Ct);
        if (reporter || v != null && SafetyEventCommands.Ids(v.AuthorsJson).Contains(c.Actor.Id)) return SafetyOutcome.Fail(403, "separation_of_duties");
        var actions = await c.Db.SafetyActions.AsNoTracking().Where(x => x.EventId == e.Id && x.VesselId == c.Vessel).ToArrayAsync(c.Ct);
        var blockers = SafetyEventPolicy.ClosureBlockers(e.State, e.InvestigationRequired, v?.State, v?.State == "APPROVED" ? SafetyEventReader.Investigation(v.ContentJson) : null,
            actions.Select(SafetyEventReader.View).ToArray());
        if (blockers.Length > 0) return SafetyOutcome.Fail(422, "safety_event_closure_blocked", blockers);
        e.ClosedBy = c.Actor.Id; e.ClosedAt = c.Now; e.ClosureReason = c.Command.Reason;
        await Advance(c.Db, e, SafetyEventPolicy.Closed, "CLOSE", c.Actor.Id, c.Now, c.Ct);
        return SafetyOutcome.Ok(e.Id, e.Revision);
    }
}
