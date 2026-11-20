using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record RiskTemplatePin(Guid TemplateId, long Revision, string Hash);
public sealed record RiskMethodSummaryDto(Guid Id, string Code, long Revision, string Status, string Title);
public sealed record RiskHazardResultDto(Guid HazardId, RiskResult Initial, RiskResult Residual);
public sealed record RiskDecisionDto(Guid Id, long Revision, long ResultRevision, string Decision, Guid ActorId, string Reason, DateTimeOffset DecidedAt);
public sealed record RiskAcknowledgementDto(Guid ActorId, long Revision, string Choice, string? Reason, DateTimeOffset RecordedAt);
public sealed record RiskRevisionSummaryDto(long Revision, string State, string ReasonCode, string Hash, Guid ActorId, DateTimeOffset SavedAt);
public sealed record RiskAssessmentDto(Guid Id, Guid VesselId, string AssessmentNo, long Revision, uint Version, string State, RiskAssessmentContent Content,
    RiskLinkDto[] Links, RiskTemplatePin? Template, RiskMethodSummaryDto? Method, RiskHazardResultDto[] Results, string[] Blockers, string Hash,
    long? ApprovedRevision, string AcknowledgementState, RiskAcknowledgementDto[] Acknowledgements, RiskDecisionDto[] Decisions,
    RiskRevisionSummaryDto[] Revisions, bool CanEdit, bool CanSubmit, bool CanReview, bool CanAmend, bool CanAcknowledge,
    string Provenance, DateTimeOffset SavedAt, string SourceVersion);
public sealed record RiskAssessmentRevisionDto(Guid AssessmentId, Guid VesselId, long Revision, string State, string ReasonCode, RiskAssessmentContent Content,
    RiskLinkPin[] Links, RiskTemplatePin? Template, Guid? MethodId, string Hash, Guid ActorId, DateTimeOffset SavedAt, string Provenance);
public sealed record RiskAssessmentListItemDto(Guid Id, string AssessmentNo, string? Title, string State, long Revision, bool? StopWorkRequired,
    long? ApprovedRevision, DateTimeOffset SavedAt, string Provenance);
public sealed record RiskAssessmentPageDto(RiskAssessmentListItemDto[] Items, int Offset, int Limit, bool HasMore, bool CanWrite);
public sealed record RiskCapabilitiesDto(Guid VesselId, bool CanRead, bool CanWrite, bool CanApprove, bool CanAcknowledge, bool MethodConfigured,
    RiskMethodSummaryDto[] PublishedMethods, string[] LinkKinds, string[] WorkTypes, string SourceVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskAssessmentCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string AssessmentNo,
    RiskAssessmentContent? Content, [property: JsonRequired] RiskLinkRequest[] Links, Guid? TemplateId = null, long? TemplateRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskAssessmentUpdateCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] RiskAssessmentContent Content,
    [property: JsonRequired] RiskLinkRequest[] Links);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskAssessmentSubmitCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskAssessmentReviewCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskAssessmentAmendCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskAssessmentAcknowledgeCommand([property: JsonRequired] long Revision, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Choice, string? Reason = null);

// DEV-105 / T-RISK2-001 (18.01 row 106; 50.01 Rev3 §5-6). The assessment workflow
// (draft -> review requested -> approved/returned) is a human record: the approver is never
// an author or the submitter of the cycle and an approval is not a permit, an expert risk
// acceptance or an HSE gate PASS. Approved revisions are immutable; an amendment opens a
// new draft revision that must be reviewed again.
public static class RiskAssessmentEndpoints
{
    private const string Read = "RiskAssessmentRead", Command = "RiskAssessmentCommand";
    internal static bool CanRead(TrustedActor? a) => a?.CanRead == true && a.Permissions.Contains("hse.risk.read");
    internal static bool CanWrite(TrustedActor? a, Guid vessel) => CanRead(a) && a!.HasWriteAttribution && a.CanEditVessel(vessel) && a.Permissions.Contains("hse.risk.write");
    internal static bool CanApprove(TrustedActor? a) => CanRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("hse.risk.approve");
    internal static bool CanAcknowledge(TrustedActor? a) => CanRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("hse.risk.acknowledge");

    public static void MapRiskAssessments(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/hse/risk-assessments").RequireAuthorization("DirectoryRead");
        g.MapGet("", (HttpContext h, PoseidonDbContext db) => List(h, db)).WithName("RiskAssessmentList").Produces<RiskAssessmentPageDto>();
        g.MapGet("/capabilities", (HttpContext h, PoseidonDbContext db) => Capabilities(h, db)).WithName("RiskAssessmentCapabilities").Produces<RiskCapabilitiesDto>();
        g.MapGet("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => Detail(id, h, db)).WithName("RiskAssessmentRead").Produces<RiskAssessmentDto>();
        g.MapGet("/{id:guid}/revisions/{revision:long}", (Guid id, long revision, HttpContext h, PoseidonDbContext db) => RevisionRead(id, revision, h, db))
            .WithName("RiskAssessmentRevisionRead").Produces<RiskAssessmentRevisionDto>();
        g.MapPost("", (HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskAssessmentCreateCommand>(h, db, "RiskAssessmentCreate", Command, true, null, null,
                (a, v) => CanWrite(a, v!.Value), c => c.Id, Create, Fresh))
            .WithName("RiskAssessmentCreate").Accepts<RiskAssessmentCreateCommand>("application/json").Produces<RiskAssessmentDto>().Produces<RiskFailure>(422);
        g.MapPut("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskAssessmentUpdateCommand>(h, db, "RiskAssessmentUpdate", Command, true, id, null,
                (a, v) => CanWrite(a, v!.Value), _ => id, Update, Fresh))
            .WithName("RiskAssessmentUpdate").Accepts<RiskAssessmentUpdateCommand>("application/json").Produces<RiskAssessmentDto>();
        g.MapPost("/{id:guid}/submit", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskAssessmentSubmitCommand>(h, db, "RiskAssessmentSubmit", Command, true, id, null,
                (a, v) => CanWrite(a, v!.Value), _ => id, Submit, Fresh))
            .WithName("RiskAssessmentSubmit").Accepts<RiskAssessmentSubmitCommand>("application/json").Produces<RiskAssessmentDto>().Produces<RiskFailure>(422);
        g.MapPost("/{id:guid}/review", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskAssessmentReviewCommand>(h, db, "RiskAssessmentReview", Command, true, id, null,
                (a, _) => CanApprove(a), _ => id, Review, Fresh))
            .WithName("RiskAssessmentReview").Accepts<RiskAssessmentReviewCommand>("application/json").Produces<RiskAssessmentDto>();
        g.MapPost("/{id:guid}/amend", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskAssessmentAmendCommand>(h, db, "RiskAssessmentAmend", Command, true, id, null,
                (a, v) => CanWrite(a, v!.Value), _ => id, Amend, Fresh))
            .WithName("RiskAssessmentAmend").Accepts<RiskAssessmentAmendCommand>("application/json").Produces<RiskAssessmentDto>();
        g.MapPost("/{id:guid}/acknowledgements", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskAssessmentAcknowledgeCommand>(h, db, "RiskAssessmentAcknowledge", Command, true, id, null,
                (a, _) => CanAcknowledge(a), _ => id, Acknowledge, Fresh))
            .WithName("RiskAssessmentAcknowledge").Accepts<RiskAssessmentAcknowledgeCommand>("application/json").Produces<RiskAssessmentDto>();
    }

    private static RiskAssessmentContent Content(string json) => JsonSerializer.Deserialize<RiskAssessmentContent>(json, RiskCommands.Json) ?? throw new InvalidDataException("risk_content_unavailable");
    private static RiskLinkPin[] Links(string json) => JsonSerializer.Deserialize<RiskLinkPin[]>(json, RiskCommands.Json) ?? [];
    private static RiskTemplatePin? Template(string? json) => json == null ? null : JsonSerializer.Deserialize<RiskTemplatePin>(json, RiskCommands.Json);
    private static string Hash(Guid vessel, RiskAssessment r) => RegisterStorage.Hash(JsonSerializer.Serialize(new { vessel, r.Id, r.AssessmentNo, r.Revision, r.State,
        content = r.ContentJson, links = r.LinksJson, template = r.TemplateJson, r.MethodId, RiskPolicy.SourceVersion }, RiskCommands.Json));

    internal static async Task<RiskMethodContent?> PublishedMethod(PoseidonDbContext db, Guid? id, CancellationToken ct)
    {
        if (id == null) return null;
        var json = await db.RiskMethods.AsNoTracking().Where(m => m.Id == id && m.Status == RiskPolicy.Published).Select(m => m.ContentJson).SingleOrDefaultAsync(ct);
        return json == null ? null : JsonSerializer.Deserialize<RiskMethodContent>(json, RiskCommands.Json);
    }
    private static void Append(PoseidonDbContext db, RiskAssessment row, string reason, DateTimeOffset now) =>
        db.RiskAssessmentRevisions.Add(new() { TenantId = row.TenantId, VesselId = row.VesselId, AssessmentId = row.Id, Revision = row.Revision, State = row.State,
            ReasonCode = reason, ContentJson = row.ContentJson, LinksJson = row.LinksJson, TemplateJson = row.TemplateJson, MethodId = row.MethodId,
            Hash = row.Hash, ActorId = row.ActorId, SavedAt = now, Provenance = row.Provenance });
    private static void Stamp(RiskAssessment row, Guid vessel, Guid actor, DateTimeOffset now) { row.ActorId = actor; row.SavedAt = now; row.Hash = Hash(vessel, row); }

    private static async Task<bool> MethodVisible(PoseidonDbContext db, Guid? method, CancellationToken ct) => method == null || await db.RiskMethods.AnyAsync(m => m.Id == method, ct);

    private static async Task<RiskOutcome> Create(RiskContext<RiskAssessmentCreateCommand> c)
    {
        var cmd = c.Command; var vessel = c.Vessel!.Value; var db = c.Db;
        if (cmd.Id == Guid.Empty || !RiskPolicy.TextShort(cmd.AssessmentNo) || cmd.AssessmentNo.Length > 50 ||
            cmd.TemplateId == null != (cmd.TemplateRevision == null) || cmd.TemplateId != null && cmd.Content != null || cmd.TemplateId == null && cmd.Content == null)
            return RiskOutcome.Fail(400, "invalid_request");
        if (await db.RiskAssessments.AnyAsync(r => r.Id == cmd.Id, c.Ct)) return RiskOutcome.Fail(409, "version_conflict");
        if (await db.RiskAssessments.AnyAsync(r => r.AssessmentNo == cmd.AssessmentNo, c.Ct)) return RiskOutcome.Fail(409, "duplicate_code");
        RiskAssessmentContent content; RiskTemplatePin? pin = null;
        if (cmd.TemplateId is { } templateId)
        {
            var revisions = await db.RiskTemplateRevisions.AsNoTracking().Where(r => r.TemplateId == templateId && r.Status == RiskPolicy.Published && r.EffectiveFrom != null)
                .Select(r => new { r.Revision, r.EffectiveFrom, r.ContentJson, r.Hash, r.Status }).ToArrayAsync(c.Ct);
            var chosen = revisions.SingleOrDefault(r => r.Revision == cmd.TemplateRevision);
            if (chosen == null || RiskPolicy.EffectiveState(chosen.Status, chosen.EffectiveFrom, chosen.Revision,
                    revisions.Select(r => (r.Revision, r.EffectiveFrom!.Value)), c.Now) != "CURRENT") return RiskOutcome.Fail(409, "template_not_effective");
            var template = JsonSerializer.Deserialize<RiskTemplateContent>(chosen.ContentJson, RiskCommands.Json)!;
            var tug = await db.Vessels.AsNoTracking().AnyAsync(v => v.Id == vessel && v.Kind == VesselKind.Tug, c.Ct);
            if (!RiskPolicy.AppliesTo(template, tug)) return RiskOutcome.Fail(409, "template_not_effective");
            content = RiskPolicy.FromTemplate(template, Guid.NewGuid); pin = new(templateId, chosen.Revision, chosen.Hash);
        }
        else content = cmd.Content!;
        if (!RiskPolicy.ValidDraft(content)) return RiskOutcome.Fail(400, "invalid_request");
        if (!await MethodVisible(db, content.MethodId, c.Ct)) return RiskOutcome.Fail(400, "linked_resource_unavailable");
        var links = await RiskLinkReader.Pin(db, c.Actor, vessel, cmd.Links, [], c.Ct);
        if (links == null) return RiskOutcome.Fail(400, "linked_resource_unavailable");
        var row = new RiskAssessment { TenantId = c.Actor.TenantId, VesselId = vessel, Id = cmd.Id, AssessmentNo = cmd.AssessmentNo, Revision = 1, State = RiskPolicy.Draft,
            ContentJson = JsonSerializer.Serialize(content, RiskCommands.Json), LinksJson = JsonSerializer.Serialize(links, RiskCommands.Json),
            TemplateJson = pin == null ? null : JsonSerializer.Serialize(pin, RiskCommands.Json), MethodId = content.MethodId,
            AuthorsJson = RiskCommands.WithAuthor("[]", c.Actor.Id), CreatedBy = c.Actor.Id };
        Stamp(row, vessel, c.Actor.Id, c.Now); db.RiskAssessments.Add(row); await db.SaveChangesAsync(c.Ct);
        Append(db, row, pin == null ? "CREATE" : "TEMPLATE_COPY", c.Now); await db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(row.Id, row.Revision);
    }

    private static Task<RiskAssessment?> Tracked(PoseidonDbContext db, Guid vessel, Guid id, CancellationToken ct) =>
        db.RiskAssessments.SingleOrDefaultAsync(r => r.Id == id && r.VesselId == vessel, ct);

    private static async Task<RiskOutcome> Update(RiskContext<RiskAssessmentUpdateCommand> c)
    {
        var vessel = c.Vessel!.Value; var row = await Tracked(c.Db, vessel, c.RouteId!.Value, c.Ct);
        if (row == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (row.Version != c.Command.ExpectedVersion) return RiskOutcome.Fail(409, "version_conflict");
        if (row.State is not (RiskPolicy.Draft or RiskPolicy.Returned)) return RiskOutcome.Fail(409, "invalid_state");
        if (!RiskPolicy.ValidDraft(c.Command.Content)) return RiskOutcome.Fail(400, "invalid_request");
        if (!await MethodVisible(c.Db, c.Command.Content.MethodId, c.Ct)) return RiskOutcome.Fail(400, "linked_resource_unavailable");
        var links = await RiskLinkReader.Pin(c.Db, c.Actor, vessel, c.Command.Links, Links(row.LinksJson), c.Ct);
        if (links == null) return RiskOutcome.Fail(400, "linked_resource_unavailable");
        row.Revision++; row.State = RiskPolicy.Draft; row.ContentJson = JsonSerializer.Serialize(c.Command.Content, RiskCommands.Json);
        row.LinksJson = JsonSerializer.Serialize(links, RiskCommands.Json); row.MethodId = c.Command.Content.MethodId;
        row.AuthorsJson = RiskCommands.WithAuthor(row.AuthorsJson, c.Actor.Id); row.SubmittedBy = null;
        Stamp(row, vessel, c.Actor.Id, c.Now); await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "EDIT", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<RiskOutcome> Submit(RiskContext<RiskAssessmentSubmitCommand> c)
    {
        var vessel = c.Vessel!.Value; var row = await Tracked(c.Db, vessel, c.RouteId!.Value, c.Ct);
        if (row == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (row.Version != c.Command.ExpectedVersion || row.Hash != c.Command.ContentHash) return RiskOutcome.Fail(409, "version_conflict");
        var content = Content(row.ContentJson); var method = await PublishedMethod(c.Db, row.MethodId, c.Ct);
        var t = RiskPolicy.Submit(row.State, RiskPolicy.SubmitBlockers(content, method, Links(row.LinksJson).Length));
        if (t.ToState == null) return t.Failure == "risk_record_incomplete" ? RiskOutcome.Fail(422, t.Failure, t.Blockers) : RiskOutcome.Fail(409, t.Failure!);
        row.Revision++; row.State = t.ToState; row.SubmittedBy = c.Actor.Id;
        Stamp(row, vessel, c.Actor.Id, c.Now); await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "SUBMIT", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<RiskOutcome> Review(RiskContext<RiskAssessmentReviewCommand> c)
    {
        var vessel = c.Vessel!.Value; var row = await Tracked(c.Db, vessel, c.RouteId!.Value, c.Ct);
        if (row == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (row.Version != c.Command.ExpectedVersion || row.Hash != c.Command.ContentHash) return RiskOutcome.Fail(409, "version_conflict");
        var t = RiskPolicy.Review(row.State, c.Command.Decision, c.Actor.Id, RiskCommands.AuthorIds(row.AuthorsJson), row.SubmittedBy, c.Command.Reason);
        if (t.ToState == null) return RiskOutcome.Fail(t.Failure == "separation_of_duties" ? 403 : t.Failure == "invalid_reason" ? 400 : 409, t.Failure!);
        // Approval re-checks completeness against the current published method (it may have been retired meanwhile).
        if (t.ToState == RiskPolicy.Approved)
        {
            var blockers = RiskPolicy.SubmitBlockers(Content(row.ContentJson), await PublishedMethod(c.Db, row.MethodId, c.Ct), Links(row.LinksJson).Length);
            if (blockers.Length > 0) return RiskOutcome.Fail(422, "risk_record_incomplete", blockers);
        }
        var reviewed = row.Revision; row.Revision++; row.State = t.ToState; if (t.ToState == RiskPolicy.Approved) row.ApprovedRevision = row.Revision;
        Stamp(row, vessel, c.Actor.Id, c.Now); await c.Db.SaveChangesAsync(c.Ct);
        Append(c.Db, row, c.Command.Decision == "APPROVE" ? "APPROVE" : "RETURN", c.Now);
        c.Db.RiskAssessmentDecisions.Add(new() { TenantId = row.TenantId, VesselId = vessel, Id = Guid.NewGuid(), AssessmentId = row.Id, Revision = reviewed, ResultRevision = row.Revision,
            Decision = c.Command.Decision, ContentHash = c.Command.ContentHash, ActorId = c.Actor.Id, Reason = c.Command.Reason, DecidedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<RiskOutcome> Amend(RiskContext<RiskAssessmentAmendCommand> c)
    {
        var vessel = c.Vessel!.Value; var row = await Tracked(c.Db, vessel, c.RouteId!.Value, c.Ct);
        if (row == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (row.Version != c.Command.ExpectedVersion) return RiskOutcome.Fail(409, "version_conflict");
        var t = RiskPolicy.Amend(row.State, c.Command.Reason);
        if (t.ToState == null) return RiskOutcome.Fail(t.Failure == "invalid_reason" ? 400 : 409, t.Failure!);
        row.Revision++; row.State = t.ToState; row.SubmittedBy = null; row.AuthorsJson = RiskCommands.WithAuthor("[]", c.Actor.Id);
        Stamp(row, vessel, c.Actor.Id, c.Now); await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "AMEND", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<RiskOutcome> Acknowledge(RiskContext<RiskAssessmentAcknowledgeCommand> c)
    {
        var vessel = c.Vessel!.Value; var row = await c.Db.RiskAssessments.AsNoTracking().SingleOrDefaultAsync(r => r.Id == c.RouteId && r.VesselId == vessel, c.Ct);
        if (row == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (row.Revision != c.Command.Revision || row.Hash != c.Command.ContentHash) return RiskOutcome.Fail(409, "version_conflict");
        var failure = RiskPolicy.AcknowledgementFailure(row.State, row.ApprovedRevision, c.Command.Revision, Content(row.ContentJson), c.Actor.Id, c.Command.Choice, c.Command.Reason);
        if (failure != null) return RiskOutcome.Fail(failure == "acknowledgment_role_required" ? 403 : failure == "invalid_reason" ? 400 : 409, failure);
        if (await c.Db.RiskAssessmentAcknowledgements.AnyAsync(a => a.AssessmentId == row.Id && a.Revision == c.Command.Revision && a.ActorId == c.Actor.Id, c.Ct))
            return RiskOutcome.Fail(409, "risk_acknowledgement_exists");
        c.Db.RiskAssessmentAcknowledgements.Add(new() { TenantId = row.TenantId, VesselId = vessel, Id = Guid.NewGuid(), AssessmentId = row.Id, Revision = c.Command.Revision,
            ActorId = c.Actor.Id, Choice = c.Command.Choice, ContentHash = c.Command.ContentHash, Reason = c.Command.Reason, RecordedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(row.Id, c.Command.Revision);
    }

    // ---- Reads -------------------------------------------------------------------------
    private static async Task<(TrustedActor? Actor, Guid Vessel, IResult? Failure)> ReadGate(HttpContext h, PoseidonDbContext db, string[]? extra = null)
    {
        h.Response.Headers.CacheControl = "no-store";
        if (!RiskCommands.Query(h, true, out var vessel, extra)) return (null, Guid.Empty, await RiskCommands.Fail(h, Read, 400, "invalid_query"));
        var actor = RiskCommands.Actor(h);
        if (!CanRead(actor) || db.TenantId != actor!.TenantId) return (null, vessel, await RiskCommands.Fail(h, Read, 403, "permission_denied"));
        if (!await RiskCommands.Vessels(db, actor, vessel).AnyAsync(h.RequestAborted)) return (null, vessel, await RiskCommands.Fail(h, Read, 404, "resource_not_found"));
        return (actor, vessel, null);
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await ReadGate(h, db, ["offset"]); if (failure != null) return failure;
        var offset = 0;
        if (h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 100000))
            return await RiskCommands.Fail(h, Read, 400, "invalid_query");
        var rows = await db.RiskAssessments.AsNoTracking().Where(r => r.VesselId == vessel).OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        var items = rows.Take(25).Select(r => { var content = Content(r.ContentJson); return new RiskAssessmentListItemDto(r.Id, r.AssessmentNo, content.Title, r.State, r.Revision,
            content.StopWorkRequired, r.ApprovedRevision, r.SavedAt, r.Provenance); }).ToArray();
        await RiskCommands.Audit(h, Read, "Success", vessel);
        return Results.Json(new RiskAssessmentPageDto(items, offset, 25, rows.Length > 25, CanWrite(actor, vessel)), RiskCommands.Json);
    }

    private static async Task<IResult> Capabilities(HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await ReadGate(h, db); if (failure != null) return failure;
        var methods = await db.RiskMethods.AsNoTracking().Where(m => m.Status == RiskPolicy.Published).OrderBy(m => m.Code).ThenByDescending(m => m.Revision)
            .Select(m => new { m.Id, m.Code, m.Revision, m.Status, m.ContentJson }).ToArrayAsync(h.RequestAborted);
        var summaries = methods.Select(m => new RiskMethodSummaryDto(m.Id, m.Code, m.Revision, m.Status, JsonSerializer.Deserialize<RiskMethodContent>(m.ContentJson, RiskCommands.Json)!.Title)).ToArray();
        await RiskCommands.Audit(h, Read, "Success", vessel);
        return Results.Json(new RiskCapabilitiesDto(vessel, true, CanWrite(actor, vessel), CanApprove(actor), CanAcknowledge(actor), summaries.Length > 0, summaries,
            RiskPolicy.LinkKinds, RiskPolicy.WorkTypes, RiskPolicy.SourceVersion), RiskCommands.Json);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await ReadGate(h, db); if (failure != null) return failure;
        var dto = await Describe(db, actor!, vessel, id, h.RequestAborted);
        if (dto == null) return await RiskCommands.Fail(h, Read, 404, "resource_not_found", id);
        await RiskCommands.Audit(h, Read, "Success", id);
        return Results.Json(dto, RiskCommands.Json);
    }

    private static async Task<IResult> RevisionRead(Guid id, long revision, HttpContext h, PoseidonDbContext db)
    {
        var (_, vessel, failure) = await ReadGate(h, db); if (failure != null) return failure;
        var r = await db.RiskAssessmentRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.AssessmentId == id && x.Revision == revision, h.RequestAborted);
        if (r == null) return await RiskCommands.Fail(h, Read, 404, "resource_not_found", id);
        await RiskCommands.Audit(h, Read, "Success", id);
        return Results.Json(new RiskAssessmentRevisionDto(r.AssessmentId, r.VesselId, r.Revision, r.State, r.ReasonCode, Content(r.ContentJson), Links(r.LinksJson),
            Template(r.TemplateJson), r.MethodId, r.Hash, r.ActorId, r.SavedAt, r.Provenance), RiskCommands.Json);
    }

    private static async Task<object?> Fresh(PoseidonDbContext db, TrustedActor actor, Guid? vessel, Guid id, CancellationToken ct) =>
        await Describe(db, actor, vessel!.Value, id, ct);

    internal static async Task<RiskAssessmentDto?> Describe(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid id, CancellationToken ct)
    {
        var r = await db.RiskAssessments.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
        if (r == null) return null;
        var content = Content(r.ContentJson);
        var methodRow = r.MethodId == null ? null : await db.RiskMethods.AsNoTracking().Where(m => m.Id == r.MethodId)
            .Select(m => new { m.Id, m.Code, m.Revision, m.Status, m.ContentJson }).SingleOrDefaultAsync(ct);
        var methodContent = methodRow == null ? null : JsonSerializer.Deserialize<RiskMethodContent>(methodRow.ContentJson, RiskCommands.Json);
        var published = methodRow?.Status == RiskPolicy.Published ? methodContent : null;
        var results = content.Hazards.Select(x => new RiskHazardResultDto(x.Id, RiskPolicy.Rate(published, x.Initial), RiskPolicy.Rate(published, x.Residual))).ToArray();
        var pins = Links(r.LinksJson);
        var decisions = await db.RiskAssessmentDecisions.AsNoTracking().Where(d => d.AssessmentId == id).OrderBy(d => d.Revision)
            .Select(d => new RiskDecisionDto(d.Id, d.Revision, d.ResultRevision, d.Decision, d.ActorId, d.Reason, d.DecidedAt)).ToArrayAsync(ct);
        var acks = await db.RiskAssessmentAcknowledgements.AsNoTracking().Where(a => a.AssessmentId == id).OrderBy(a => a.Revision).ThenBy(a => a.RecordedAt)
            .Select(a => new RiskAcknowledgementDto(a.ActorId, a.Revision, a.Choice, a.Reason, a.RecordedAt)).ToArrayAsync(ct);
        var revisions = await db.RiskAssessmentRevisions.AsNoTracking().Where(x => x.AssessmentId == id).OrderByDescending(x => x.Revision).Take(50)
            .Select(x => new RiskRevisionSummaryDto(x.Revision, x.State, x.ReasonCode, x.Hash, x.ActorId, x.SavedAt)).ToArrayAsync(ct);
        var current = acks.Where(a => a.Revision == r.ApprovedRevision && r.State == RiskPolicy.Approved).Select(a => (a.ActorId, a.Choice));
        var blockers = RiskPolicy.SubmitBlockers(content, published, pins.Length);
        var authors = RiskCommands.AuthorIds(r.AuthorsJson);
        var editable = r.State is RiskPolicy.Draft or RiskPolicy.Returned && CanWrite(actor, vessel);
        return new(r.Id, r.VesselId, r.AssessmentNo, r.Revision, r.Version, r.State, content, await RiskLinkReader.Describe(db, actor, vessel, pins, ct), Template(r.TemplateJson),
            methodRow == null ? null : new(methodRow.Id, methodRow.Code, methodRow.Revision, methodRow.Status, methodContent!.Title), results, blockers, r.Hash, r.ApprovedRevision,
            RiskPolicy.AcknowledgementState(r.State, content, current), acks, decisions, revisions,
            editable, editable && blockers.Length == 0,
            r.State == RiskPolicy.InReview && CanApprove(actor) && !authors.Contains(actor.Id) && r.SubmittedBy != actor.Id,
            r.State == RiskPolicy.Approved && CanWrite(actor, vessel),
            r.State == RiskPolicy.Approved && CanAcknowledge(actor) && content.Participants.Any(p => p.ActorId == actor.Id) &&
                !acks.Any(a => a.ActorId == actor.Id && a.Revision == r.ApprovedRevision),
            r.Provenance, r.SavedAt, RiskPolicy.SourceVersion);
    }
}
