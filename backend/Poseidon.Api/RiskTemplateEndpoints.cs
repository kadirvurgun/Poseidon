using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record RiskTemplateRevisionDto(Guid TemplateId, string TemplateCode, long Revision, uint Version, string Status, string EffectiveState,
    DateTimeOffset? EffectiveFrom, RiskTemplateContent Content, string Hash, string[] Blockers, Guid? PublishedBy, DateTimeOffset? PublishedAt,
    long? SupersededByRevision, bool CanEdit, bool CanSubmit, bool CanPublish, Guid ActorId, DateTimeOffset SavedAt, string Provenance);
public sealed record RiskTemplateDecisionDto(Guid Id, long Revision, string Decision, Guid ActorId, string Reason, DateTimeOffset? EffectiveFrom, DateTimeOffset DecidedAt);
public sealed record RiskTemplateSuggestionDto(Guid Id, long Revision, string Text, string Status, Guid ActorId, DateTimeOffset CreatedAt,
    Guid? ReviewedBy, string? ReviewReason, DateTimeOffset? ReviewedAt, uint Version, bool CanReview);
public sealed record RiskTemplateDetailDto(Guid Id, string TemplateCode, long LatestRevision, uint Version, RiskTemplateRevisionDto[] Revisions,
    RiskTemplateDecisionDto[] Decisions, RiskTemplateSuggestionDto[] Suggestions, int SuggestionCount, bool CanRevise, bool CanSuggest, string SourceVersion);
public sealed record RiskTemplateListItemDto(Guid Id, string TemplateCode, string? Title, string? WorkType, long LatestRevision, string LatestStatus,
    long? CurrentRevision, DateTimeOffset? CurrentEffectiveFrom, int SuggestionCount);
public sealed record RiskTemplatePageDto(RiskTemplateListItemDto[] Items, int Offset, int Limit, bool HasMore, bool CanWrite, bool CanPublish, string[] WorkTypes);
public sealed record RiskApplicableTemplateDto(Guid TemplateId, string TemplateCode, long Revision, string? Title, string? WorkType, DateTimeOffset EffectiveFrom,
    string Hash, int HazardCount, string Qualification);
public sealed record RiskApplicableTemplatesDto(Guid VesselId, RiskApplicableTemplateDto[] Items);
public sealed record RiskMethodDto(Guid Id, string Code, long Revision, uint Version, string Status, RiskMethodContent Content, string Hash, Guid ActorId,
    DateTimeOffset SavedAt, Guid? PublishedBy, DateTimeOffset? PublishedAt, bool CanPublish);
public sealed record RiskMethodListDto(RiskMethodDto[] Items, bool CanWrite, bool CanPublish);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string TemplateCode, [property: JsonRequired] RiskTemplateContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateUpdateCommand([property: JsonRequired] long Revision, [property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] RiskTemplateContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateSubmitCommand([property: JsonRequired] long Revision, [property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateReviewCommand([property: JsonRequired] long Revision, [property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason, DateTimeOffset? EffectiveFrom = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateReviseCommand([property: JsonRequired] long BaseRevision, [property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateSuggestCommand([property: JsonRequired] Guid Id, [property: JsonRequired] long Revision, [property: JsonRequired] string Text);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskTemplateSuggestionReviewCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Decision, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskMethodCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] RiskMethodContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RiskMethodPublishCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash, [property: JsonRequired] string Reason);

// DEV-106 / T-RISK2-002 (18.01 row 107; 50.01 Rev3 §3-4, paragraph 59). Fleet-standard
// templates are governed per revision: DRAFT -> IN_REVIEW -> PUBLISHED (HSEQ publication is
// a separate human decision by a non-author) -> SUPERSEDED; superseded revisions are kept.
// Applying a template copies it into a new assessment draft; it is never approved by that.
// Risk methods (matrix configuration) are tenant data with the same human publication rule;
// none is seeded, so ratings stay METHOD_UNCONFIGURED until a person publishes one.
public static class RiskTemplateEndpoints
{
    private const string Read = "RiskTemplateRead", Command = "RiskTemplateCommand";
    private static bool TenantWide(TrustedActor? a) => a?.CanRead == true && (a.TenantWideRead || a.TenantWideManage);
    internal static bool CanRead(TrustedActor? a) => a?.CanRead == true && a.Permissions.Contains("hse.risk_template.read");
    internal static bool CanWrite(TrustedActor? a) => CanRead(a) && TenantWide(a) && a!.HasWriteAttribution && a.Permissions.Contains("hse.risk_template.write");
    internal static bool CanPublish(TrustedActor? a) => CanRead(a) && TenantWide(a) && a!.HasWriteAttribution && a.Permissions.Contains("hse.risk_template.publish");
    internal static bool CanSuggest(TrustedActor? a) => CanRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("hse.risk_template.suggest");
    internal static bool CanReadMethods(TrustedActor? a) => CanRead(a) || RiskAssessmentEndpoints.CanRead(a);
    internal static bool CanWriteMethod(TrustedActor? a) => a?.CanRead == true && TenantWide(a) && a.HasWriteAttribution && a.Permissions.Contains("hse.risk_method.write");
    internal static bool CanPublishMethod(TrustedActor? a) => a?.CanRead == true && TenantWide(a) && a.HasWriteAttribution && a.Permissions.Contains("hse.risk_method.publish");

    public static void MapRiskTemplates(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/hse").RequireAuthorization("DirectoryRead");
        g.MapGet("/risk-templates", (HttpContext h, PoseidonDbContext db) => List(h, db)).WithName("RiskTemplateList").Produces<RiskTemplatePageDto>();
        g.MapGet("/risk-templates/applicable", (HttpContext h, PoseidonDbContext db) => Applicable(h, db)).WithName("RiskTemplateApplicable").Produces<RiskApplicableTemplatesDto>();
        g.MapGet("/risk-templates/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => Detail(id, h, db)).WithName("RiskTemplateRead").Produces<RiskTemplateDetailDto>();
        g.MapPost("/risk-templates", (HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskTemplateCreateCommand>(h, db, "RiskTemplateCreate", Command, false, null, null,
                (a, _) => CanWrite(a), c => c.Id, Create, Fresh(h))).WithName("RiskTemplateCreate").Accepts<RiskTemplateCreateCommand>("application/json").Produces<RiskTemplateDetailDto>();
        g.MapPut("/risk-templates/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskTemplateUpdateCommand>(h, db, "RiskTemplateUpdate", Command, false, id, null,
                (a, _) => CanWrite(a), _ => id, Update, Fresh(h))).WithName("RiskTemplateUpdate").Accepts<RiskTemplateUpdateCommand>("application/json").Produces<RiskTemplateDetailDto>();
        g.MapPost("/risk-templates/{id:guid}/submit", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskTemplateSubmitCommand>(h, db, "RiskTemplateSubmit", Command, false, id, null,
                (a, _) => CanWrite(a), _ => id, Submit, Fresh(h))).WithName("RiskTemplateSubmit").Accepts<RiskTemplateSubmitCommand>("application/json").Produces<RiskTemplateDetailDto>().Produces<RiskFailure>(422);
        g.MapPost("/risk-templates/{id:guid}/review", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskTemplateReviewCommand>(h, db, "RiskTemplateReview", Command, false, id, null,
                (a, _) => CanPublish(a), _ => id, Review, Fresh(h))).WithName("RiskTemplateReview").Accepts<RiskTemplateReviewCommand>("application/json").Produces<RiskTemplateDetailDto>();
        g.MapPost("/risk-templates/{id:guid}/revise", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskTemplateReviseCommand>(h, db, "RiskTemplateRevise", Command, false, id, null,
                (a, _) => CanWrite(a), _ => id, Revise, Fresh(h))).WithName("RiskTemplateRevise").Accepts<RiskTemplateReviseCommand>("application/json").Produces<RiskTemplateDetailDto>();
        g.MapPost("/risk-templates/{id:guid}/suggestions", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskTemplateSuggestCommand>(h, db, "RiskTemplateSuggest", Command, false, id, null,
                (a, _) => CanSuggest(a), _ => id, Suggest, FreshForSuggester(h))).WithName("RiskTemplateSuggest").Accepts<RiskTemplateSuggestCommand>("application/json").Produces<RiskTemplateDetailDto>();
        g.MapPost("/risk-templates/{id:guid}/suggestions/{suggestionId:guid}/review", (Guid id, Guid suggestionId, HttpContext h, PoseidonDbContext db) =>
                RiskCommands.Run<RiskTemplateSuggestionReviewCommand>(h, db, "RiskTemplateSuggestionReview", Command, false, id, suggestionId, (a, _) => CanWrite(a), _ => id, SuggestionReview, Fresh(h)))
            .WithName("RiskTemplateSuggestionReview").Accepts<RiskTemplateSuggestionReviewCommand>("application/json").Produces<RiskTemplateDetailDto>();
        g.MapGet("/risk-methods", (HttpContext h, PoseidonDbContext db) => Methods(h, db)).WithName("RiskMethodList").Produces<RiskMethodListDto>();
        g.MapPost("/risk-methods", (HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskMethodCreateCommand>(h, db, "RiskMethodCreate", Command, false, null, null,
                (a, _) => CanWriteMethod(a), c => c.Id, MethodCreate, FreshMethod)).WithName("RiskMethodCreate").Accepts<RiskMethodCreateCommand>("application/json").Produces<RiskMethodDto>();
        g.MapPost("/risk-methods/{id:guid}/publish", (Guid id, HttpContext h, PoseidonDbContext db) => RiskCommands.Run<RiskMethodPublishCommand>(h, db, "RiskMethodPublish", Command, false, id, null,
                (a, _) => CanPublishMethod(a), _ => id, MethodPublish, FreshMethod)).WithName("RiskMethodPublish").Accepts<RiskMethodPublishCommand>("application/json").Produces<RiskMethodDto>();
    }

    private static RiskTemplateContent Content(string json) => JsonSerializer.Deserialize<RiskTemplateContent>(json, RiskCommands.Json) ?? throw new InvalidDataException("risk_template_unavailable");
    private static string Hash(RiskTemplate t, RiskTemplateRevision r) => RegisterStorage.Hash(JsonSerializer.Serialize(new { t.Id, t.TemplateCode, r.Revision, r.Status,
        content = r.ContentJson, r.EffectiveFrom, RiskPolicy.SourceVersion }, RiskCommands.Json));
    private static string MethodHash(RiskMethod m) => RegisterStorage.Hash(JsonSerializer.Serialize(new { m.Id, m.Code, m.Revision, content = m.ContentJson }, RiskCommands.Json));

    private static async Task<RiskOutcome> Create(RiskContext<RiskTemplateCreateCommand> c)
    {
        var cmd = c.Command;
        if (cmd.Id == Guid.Empty || cmd.TemplateCode is not { Length: >= 1 and <= 50 } || !cmd.TemplateCode.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.') ||
            !RiskPolicy.ValidTemplateDraft(cmd.Content)) return RiskOutcome.Fail(400, "invalid_request");
        if (await c.Db.RiskTemplates.AnyAsync(t => t.Id == cmd.Id, c.Ct)) return RiskOutcome.Fail(409, "version_conflict");
        if (await c.Db.RiskTemplates.AnyAsync(t => t.TemplateCode == cmd.TemplateCode, c.Ct)) return RiskOutcome.Fail(409, "duplicate_code");
        var t = new RiskTemplate { TenantId = c.Actor.TenantId, Id = cmd.Id, TemplateCode = cmd.TemplateCode, LatestRevision = 1, CreatedBy = c.Actor.Id, CreatedAt = c.Now };
        c.Db.RiskTemplates.Add(t); await c.Db.SaveChangesAsync(c.Ct);
        var r = new RiskTemplateRevision { TenantId = t.TenantId, TemplateId = t.Id, Revision = 1, Status = RiskPolicy.Draft, ContentJson = JsonSerializer.Serialize(cmd.Content, RiskCommands.Json),
            AuthorsJson = RiskCommands.WithAuthor("[]", c.Actor.Id), ActorId = c.Actor.Id, SavedAt = c.Now };
        r.Hash = Hash(t, r); c.Db.RiskTemplateRevisions.Add(r); await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(t.Id, 1);
    }

    private static async Task<(RiskTemplate? Template, RiskTemplateRevision? Revision)> Tracked(PoseidonDbContext db, Guid id, long revision, CancellationToken ct) =>
        (await db.RiskTemplates.SingleOrDefaultAsync(t => t.Id == id, ct), await db.RiskTemplateRevisions.SingleOrDefaultAsync(r => r.TemplateId == id && r.Revision == revision, ct));

    private static async Task<RiskOutcome> Update(RiskContext<RiskTemplateUpdateCommand> c)
    {
        var (t, r) = await Tracked(c.Db, c.RouteId!.Value, c.Command.Revision, c.Ct);
        if (t == null || r == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (r.Version != c.Command.ExpectedVersion) return RiskOutcome.Fail(409, "version_conflict");
        if (r.Status is not (RiskPolicy.Draft or RiskPolicy.Returned)) return RiskOutcome.Fail(409, "invalid_state");
        if (!RiskPolicy.ValidTemplateDraft(c.Command.Content)) return RiskOutcome.Fail(400, "invalid_request");
        r.Status = RiskPolicy.Draft; r.ContentJson = JsonSerializer.Serialize(c.Command.Content, RiskCommands.Json); r.AuthorsJson = RiskCommands.WithAuthor(r.AuthorsJson, c.Actor.Id);
        r.SubmittedBy = null; r.ActorId = c.Actor.Id; r.SavedAt = c.Now; r.Hash = Hash(t, r); await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(t.Id, r.Revision);
    }

    private static async Task<RiskOutcome> Submit(RiskContext<RiskTemplateSubmitCommand> c)
    {
        var (t, r) = await Tracked(c.Db, c.RouteId!.Value, c.Command.Revision, c.Ct);
        if (t == null || r == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (r.Version != c.Command.ExpectedVersion || r.Hash != c.Command.ContentHash) return RiskOutcome.Fail(409, "version_conflict");
        var x = RiskPolicy.TemplateSubmit(r.Status, RiskPolicy.TemplateBlockers(Content(r.ContentJson), r.Revision));
        if (x.ToState == null) return x.Failure == "risk_record_incomplete" ? RiskOutcome.Fail(422, x.Failure, x.Blockers) : RiskOutcome.Fail(409, x.Failure!);
        r.Status = x.ToState; r.SubmittedBy = c.Actor.Id; r.ActorId = c.Actor.Id; r.SavedAt = c.Now; r.Hash = Hash(t, r); await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(t.Id, r.Revision);
    }

    private static async Task<RiskOutcome> Review(RiskContext<RiskTemplateReviewCommand> c)
    {
        var cmd = c.Command; var (t, r) = await Tracked(c.Db, c.RouteId!.Value, cmd.Revision, c.Ct);
        if (t == null || r == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (r.Version != cmd.ExpectedVersion || r.Hash != cmd.ContentHash) return RiskOutcome.Fail(409, "version_conflict");
        var x = RiskPolicy.TemplateReview(r.Status, cmd.Decision, c.Actor.Id, RiskCommands.AuthorIds(r.AuthorsJson), r.SubmittedBy, cmd.Reason);
        if (x.ToState == null) return RiskOutcome.Fail(x.Failure == "separation_of_duties" ? 403 : x.Failure == "invalid_reason" ? 400 : 409, x.Failure!);
        if (x.ToState == RiskPolicy.Published)
        {
            // Effective from must be explicit: not before this review moment minus a clock-skew minute, at most a year ahead.
            if (cmd.EffectiveFrom is not { } effective || effective < c.Now.AddMinutes(-1) || effective > c.Now.AddDays(366)) return RiskOutcome.Fail(400, "invalid_request");
            var blockers = RiskPolicy.TemplateBlockers(Content(r.ContentJson), r.Revision);
            if (blockers.Length > 0) return RiskOutcome.Fail(422, "risk_record_incomplete", blockers);
            r.EffectiveFrom = effective < c.Now ? c.Now : effective; r.PublishedBy = c.Actor.Id; r.PublishedAt = c.Now;
            if (r.EffectiveFrom <= c.Now)
                foreach (var old in await c.Db.RiskTemplateRevisions.Where(o => o.TemplateId == t.Id && o.Revision != r.Revision && o.Status == RiskPolicy.Published && o.EffectiveFrom <= c.Now).ToArrayAsync(c.Ct))
                { old.Status = RiskPolicy.Superseded; old.SupersededByRevision = r.Revision; }
        }
        else if (cmd.EffectiveFrom != null) return RiskOutcome.Fail(400, "invalid_request");
        r.Status = x.ToState; r.Hash = Hash(t, r); await c.Db.SaveChangesAsync(c.Ct);
        c.Db.RiskTemplateDecisions.Add(new() { TenantId = t.TenantId, Id = Guid.NewGuid(), TemplateId = t.Id, Revision = r.Revision, Decision = cmd.Decision, ContentHash = cmd.ContentHash,
            EffectiveFrom = r.EffectiveFrom, ActorId = c.Actor.Id, Reason = cmd.Reason, DecidedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(t.Id, r.Revision);
    }

    private static async Task<RiskOutcome> Revise(RiskContext<RiskTemplateReviseCommand> c)
    {
        var t = await c.Db.RiskTemplates.SingleOrDefaultAsync(x => x.Id == c.RouteId, c.Ct);
        if (t == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (t.Version != c.Command.ExpectedVersion || c.Command.BaseRevision != t.LatestRevision) return RiskOutcome.Fail(409, "version_conflict");
        if (!RiskPolicy.Reason(c.Command.Reason)) return RiskOutcome.Fail(400, "invalid_reason");
        var latest = await c.Db.RiskTemplateRevisions.AsNoTracking().SingleAsync(r => r.TemplateId == t.Id && r.Revision == t.LatestRevision, c.Ct);
        if (latest.Status is not (RiskPolicy.Published or RiskPolicy.Superseded)) return RiskOutcome.Fail(409, "invalid_state");
        var content = Content(latest.ContentJson) with { ChangeReason = c.Command.Reason };
        t.LatestRevision++; await c.Db.SaveChangesAsync(c.Ct);
        var r = new RiskTemplateRevision { TenantId = t.TenantId, TemplateId = t.Id, Revision = t.LatestRevision, Status = RiskPolicy.Draft,
            ContentJson = JsonSerializer.Serialize(content, RiskCommands.Json), AuthorsJson = RiskCommands.WithAuthor("[]", c.Actor.Id), ActorId = c.Actor.Id, SavedAt = c.Now };
        r.Hash = Hash(t, r); c.Db.RiskTemplateRevisions.Add(r); await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(t.Id, r.Revision);
    }

    private static async Task<RiskOutcome> Suggest(RiskContext<RiskTemplateSuggestCommand> c)
    {
        var cmd = c.Command;
        if (cmd.Id == Guid.Empty || !RiskPolicy.TextLong(cmd.Text)) return RiskOutcome.Fail(400, "invalid_request");
        var target = await c.Db.RiskTemplateRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.TemplateId == c.RouteId && r.Revision == cmd.Revision, c.Ct);
        if (target == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (target.Status is not (RiskPolicy.Published or RiskPolicy.Superseded)) return RiskOutcome.Fail(409, "invalid_state");
        if (await c.Db.RiskTemplateSuggestions.AnyAsync(s => s.Id == cmd.Id, c.Ct)) return RiskOutcome.Fail(409, "version_conflict");
        c.Db.RiskTemplateSuggestions.Add(new() { TenantId = c.Actor.TenantId, Id = cmd.Id, TemplateId = target.TemplateId, Revision = target.Revision, Text = cmd.Text,
            Status = "OPEN", ActorId = c.Actor.Id, CreatedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(target.TemplateId, target.Revision);
    }

    private static async Task<RiskOutcome> SuggestionReview(RiskContext<RiskTemplateSuggestionReviewCommand> c)
    {
        var s = await c.Db.RiskTemplateSuggestions.SingleOrDefaultAsync(x => x.Id == c.SubId && x.TemplateId == c.RouteId, c.Ct);
        if (s == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (s.Version != c.Command.ExpectedVersion) return RiskOutcome.Fail(409, "version_conflict");
        if (s.Status != "OPEN") return RiskOutcome.Fail(409, "invalid_state");
        if (c.Command.Decision is not ("ACCEPT" or "REJECT") || !RiskPolicy.Reason(c.Command.Reason)) return RiskOutcome.Fail(400, "invalid_reason");
        if (s.ActorId == c.Actor.Id) return RiskOutcome.Fail(403, "separation_of_duties");
        s.Status = c.Command.Decision == "ACCEPT" ? "ACCEPTED" : "REJECTED"; s.ReviewedBy = c.Actor.Id; s.ReviewReason = c.Command.Reason; s.ReviewedAt = c.Now;
        await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(s.TemplateId, s.Revision);
    }

    private static async Task<RiskOutcome> MethodCreate(RiskContext<RiskMethodCreateCommand> c)
    {
        var cmd = c.Command;
        if (cmd.Id == Guid.Empty) return RiskOutcome.Fail(400, "invalid_request");
        var failure = RiskPolicy.MethodFailure(cmd.Content);
        if (failure != null) return RiskOutcome.Fail(failure == "method_matrix_incomplete" ? 422 : 400, failure);
        if (await c.Db.RiskMethods.AnyAsync(m => m.Id == cmd.Id, c.Ct)) return RiskOutcome.Fail(409, "version_conflict");
        var revision = (await c.Db.RiskMethods.Where(m => m.Code == cmd.Content.Code).MaxAsync(m => (long?)m.Revision, c.Ct) ?? 0) + 1;
        var m = new RiskMethod { TenantId = c.Actor.TenantId, Id = cmd.Id, Code = cmd.Content.Code, Revision = revision, Status = RiskPolicy.Draft,
            ContentJson = JsonSerializer.Serialize(cmd.Content, RiskCommands.Json), ActorId = c.Actor.Id, SavedAt = c.Now };
        m.Hash = MethodHash(m); c.Db.RiskMethods.Add(m); await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(m.Id, m.Revision);
    }

    private static async Task<RiskOutcome> MethodPublish(RiskContext<RiskMethodPublishCommand> c)
    {
        var m = await c.Db.RiskMethods.SingleOrDefaultAsync(x => x.Id == c.RouteId, c.Ct);
        if (m == null) return RiskOutcome.Fail(404, "resource_not_found");
        if (m.Version != c.Command.ExpectedVersion || m.Hash != c.Command.ContentHash) return RiskOutcome.Fail(409, "version_conflict");
        if (m.Status != RiskPolicy.Draft) return RiskOutcome.Fail(409, "invalid_state");
        if (!RiskPolicy.Reason(c.Command.Reason)) return RiskOutcome.Fail(400, "invalid_reason");
        if (m.ActorId == c.Actor.Id) return RiskOutcome.Fail(403, "separation_of_duties");
        m.Status = RiskPolicy.Published; m.PublishedBy = c.Actor.Id; m.PublishedAt = c.Now; m.PublishReason = c.Command.Reason; await c.Db.SaveChangesAsync(c.Ct);
        return RiskOutcome.Ok(m.Id, m.Revision);
    }

    // ---- Reads -------------------------------------------------------------------------
    private static async Task<(TrustedActor? Actor, IResult? Failure)> Gate(HttpContext h, PoseidonDbContext db, Func<TrustedActor?, bool> allowed, string[]? extra = null)
    {
        h.Response.Headers.CacheControl = "no-store";
        if (!RiskCommands.Query(h, false, out _, extra)) return (null, await RiskCommands.Fail(h, Read, 400, "invalid_query"));
        var actor = RiskCommands.Actor(h);
        if (!allowed(actor) || db.TenantId != actor!.TenantId) return (null, await RiskCommands.Fail(h, Read, 403, "permission_denied"));
        return (actor, null);
    }

    private static int SuggestionCount(IEnumerable<string> statuses) => statuses.Count(s => s is "OPEN" or "ACCEPTED");

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db)
    {
        var (actor, failure) = await Gate(h, db, CanRead, ["offset"]); if (failure != null) return failure;
        var offset = 0;
        if (h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 100000))
            return await RiskCommands.Fail(h, Read, 400, "invalid_query");
        var templates = await db.RiskTemplates.AsNoTracking().OrderBy(t => t.TemplateCode).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        var ids = templates.Take(25).Select(t => t.Id).ToArray(); var now = RiskCommands.Now(h);
        var revisions = await db.RiskTemplateRevisions.AsNoTracking().Where(r => ids.Contains(r.TemplateId)).ToArrayAsync(h.RequestAborted);
        var suggestions = await db.RiskTemplateSuggestions.AsNoTracking().Where(s => ids.Contains(s.TemplateId)).Select(s => new { s.TemplateId, s.Status }).ToArrayAsync(h.RequestAborted);
        var items = templates.Take(25).Select(t =>
        {
            var own = revisions.Where(r => r.TemplateId == t.Id).ToArray(); var latest = own.Single(r => r.Revision == t.LatestRevision); var content = Content(latest.ContentJson);
            var published = own.Where(r => r.Status == RiskPolicy.Published && r.EffectiveFrom != null).Select(r => (r.Revision, r.EffectiveFrom!.Value)).ToArray();
            var current = own.FirstOrDefault(r => RiskPolicy.EffectiveState(r.Status, r.EffectiveFrom, r.Revision, published, now) == "CURRENT");
            return new RiskTemplateListItemDto(t.Id, t.TemplateCode, content.Title, content.WorkType, t.LatestRevision, latest.Status, current?.Revision, current?.EffectiveFrom,
                SuggestionCount(suggestions.Where(s => s.TemplateId == t.Id).Select(s => s.Status)));
        }).ToArray();
        await RiskCommands.Audit(h, Read, "Success");
        return Results.Json(new RiskTemplatePageDto(items, offset, 25, templates.Length > 25, CanWrite(actor), CanPublish(actor), RiskPolicy.WorkTypes), RiskCommands.Json);
    }

    // Suggestions for an assessment: only CURRENT published revisions that apply to the vessel kind.
    private static async Task<IResult> Applicable(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        if (!RiskCommands.Query(h, true, out var vessel, ["workType"])) return await RiskCommands.Fail(h, Read, 400, "invalid_query");
        var workType = h.Request.Query.ContainsKey("workType") ? h.Request.Query["workType"].ToString() : null;
        if (workType != null && !RiskPolicy.WorkTypes.Contains(workType)) return await RiskCommands.Fail(h, Read, 400, "invalid_query");
        var actor = RiskCommands.Actor(h);
        if (!RiskAssessmentEndpoints.CanRead(actor) || db.TenantId != actor!.TenantId) return await RiskCommands.Fail(h, Read, 403, "permission_denied");
        var target = await RiskCommands.Vessels(db, actor, vessel).Select(v => (VesselKind?)v.Kind).SingleOrDefaultAsync(h.RequestAborted);
        if (target == null) return await RiskCommands.Fail(h, Read, 404, "resource_not_found");
        var now = RiskCommands.Now(h);
        var rows = await db.RiskTemplateRevisions.AsNoTracking().Where(r => r.Status == RiskPolicy.Published && r.EffectiveFrom != null)
            .Join(db.RiskTemplates.AsNoTracking(), r => r.TemplateId, t => t.Id, (r, t) => new { r.TemplateId, t.TemplateCode, r.Revision, r.Status, r.EffectiveFrom, r.ContentJson, r.Hash })
            .ToArrayAsync(h.RequestAborted);
        var items = rows.GroupBy(r => r.TemplateId).Select(group =>
        {
            var published = group.Select(r => (r.Revision, r.EffectiveFrom!.Value)).ToArray();
            return group.FirstOrDefault(r => RiskPolicy.EffectiveState(r.Status, r.EffectiveFrom, r.Revision, published, now) == "CURRENT");
        }).Where(r => r != null).Select(r => (Row: r!, Content: Content(r!.ContentJson)))
            .Where(x => RiskPolicy.AppliesTo(x.Content, target == VesselKind.Tug) && (workType == null || x.Content.WorkType == workType))
            .OrderBy(x => x.Row.TemplateCode)
            .Select(x => new RiskApplicableTemplateDto(x.Row.TemplateId, x.Row.TemplateCode, x.Row.Revision, x.Content.Title, x.Content.WorkType, x.Row.EffectiveFrom!.Value,
                x.Row.Hash, x.Content.Hazards.Length, "TEMPLATE_COPY_REQUIRES_REVIEW")).Take(100).ToArray();
        await RiskCommands.Audit(h, Read, "Success", vessel);
        return Results.Json(new RiskApplicableTemplatesDto(vessel, items), RiskCommands.Json);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db)
    {
        var (actor, failure) = await Gate(h, db, CanRead); if (failure != null) return failure;
        var dto = await Describe(db, actor!, id, RiskCommands.Now(h), h.RequestAborted);
        if (dto == null) return await RiskCommands.Fail(h, Read, 404, "resource_not_found", id);
        await RiskCommands.Audit(h, Read, "Success", id);
        return Results.Json(dto, RiskCommands.Json);
    }

    private static Func<PoseidonDbContext, TrustedActor, Guid?, Guid, CancellationToken, Task<object?>> Fresh(HttpContext h) =>
        async (db, actor, _, id, ct) => CanRead(actor) ? await Describe(db, actor, id, h.UtcNow(), ct) : null;
    // A suggester may lack template read; the receipt then confirms only the template identity.
    private static Func<PoseidonDbContext, TrustedActor, Guid?, Guid, CancellationToken, Task<object?>> FreshForSuggester(HttpContext h) => Fresh(h);

    internal static async Task<RiskTemplateDetailDto?> Describe(PoseidonDbContext db, TrustedActor actor, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var t = await db.RiskTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (t == null) return null;
        var revisions = await db.RiskTemplateRevisions.AsNoTracking().Where(r => r.TemplateId == id).OrderByDescending(r => r.Revision).ToArrayAsync(ct);
        var published = revisions.Where(r => r.Status == RiskPolicy.Published && r.EffectiveFrom != null).Select(r => (r.Revision, r.EffectiveFrom!.Value)).ToArray();
        var decisions = await db.RiskTemplateDecisions.AsNoTracking().Where(d => d.TemplateId == id).OrderBy(d => d.DecidedAt)
            .Select(d => new RiskTemplateDecisionDto(d.Id, d.Revision, d.Decision, d.ActorId, d.Reason, d.EffectiveFrom, d.DecidedAt)).ToArrayAsync(ct);
        var suggestions = await db.RiskTemplateSuggestions.AsNoTracking().Where(s => s.TemplateId == id).OrderByDescending(s => s.CreatedAt).Take(100).ToArrayAsync(ct);
        var write = CanWrite(actor); var publish = CanPublish(actor);
        var items = revisions.Select(r =>
        {
            var content = Content(r.ContentJson); var blockers = RiskPolicy.TemplateBlockers(content, r.Revision); var authors = RiskCommands.AuthorIds(r.AuthorsJson);
            var editable = write && r.Status is RiskPolicy.Draft or RiskPolicy.Returned;
            return new RiskTemplateRevisionDto(t.Id, t.TemplateCode, r.Revision, r.Version, r.Status, RiskPolicy.EffectiveState(r.Status, r.EffectiveFrom, r.Revision, published, now),
                r.EffectiveFrom, content, r.Hash, blockers, r.PublishedBy, r.PublishedAt, r.SupersededByRevision, editable, editable && blockers.Length == 0,
                publish && r.Status == RiskPolicy.InReview && !authors.Contains(actor.Id) && r.SubmittedBy != actor.Id, r.ActorId, r.SavedAt, r.Provenance);
        }).ToArray();
        var latest = revisions.First(r => r.Revision == t.LatestRevision);
        return new(t.Id, t.TemplateCode, t.LatestRevision, t.Version, items, decisions,
            [.. suggestions.Select(s => new RiskTemplateSuggestionDto(s.Id, s.Revision, s.Text, s.Status, s.ActorId, s.CreatedAt, s.ReviewedBy, s.ReviewReason, s.ReviewedAt, s.Version,
                write && s.Status == "OPEN" && s.ActorId != actor.Id))],
            SuggestionCount(suggestions.Select(s => s.Status)), write && latest.Status is RiskPolicy.Published or RiskPolicy.Superseded,
            CanSuggest(actor) && revisions.Any(r => r.Status is RiskPolicy.Published or RiskPolicy.Superseded), RiskPolicy.SourceVersion);
    }

    private static async Task<IResult> Methods(HttpContext h, PoseidonDbContext db)
    {
        var (actor, failure) = await Gate(h, db, CanReadMethods); if (failure != null) return failure;
        var rows = await db.RiskMethods.AsNoTracking().OrderBy(m => m.Code).ThenByDescending(m => m.Revision).Take(200).ToArrayAsync(h.RequestAborted);
        await RiskCommands.Audit(h, Read, "Success");
        return Results.Json(new RiskMethodListDto([.. rows.Select(m => MethodDto(m, actor!))], CanWriteMethod(actor), CanPublishMethod(actor)), RiskCommands.Json);
    }
    private static RiskMethodDto MethodDto(RiskMethod m, TrustedActor actor) => new(m.Id, m.Code, m.Revision, m.Version, m.Status,
        JsonSerializer.Deserialize<RiskMethodContent>(m.ContentJson, RiskCommands.Json)!, m.Hash, m.ActorId, m.SavedAt, m.PublishedBy, m.PublishedAt,
        m.Status == RiskPolicy.Draft && CanPublishMethod(actor) && m.ActorId != actor.Id);
    private static async Task<object?> FreshMethod(PoseidonDbContext db, TrustedActor actor, Guid? _, Guid id, CancellationToken ct)
    {
        if (!CanReadMethods(actor)) return null;
        var m = await db.RiskMethods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return m == null ? null : MethodDto(m, actor);
    }
}
