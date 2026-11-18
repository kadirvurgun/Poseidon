using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlledDocumentCreateCommand(string? Code, string? Title, string? Classification);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlledDocumentRevisionCommand(Guid FileId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlledDocumentVerificationCommand(string? Method, string? Result, string? Basis);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlledDocumentHoldCommand(string? AuthorityRef, string? Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlledDocumentReasonCommand(string? Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlledDocumentRetentionCommand(string? PolicyRef, DateOnly? RetainUntil);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlledDocumentLinkCommand(string? TargetKind, Guid TargetId);

public sealed record ControlledDocumentVerificationDto(Guid Id, Guid VerifierId, string Method, string Result, string? Basis, DateTimeOffset CheckedAt);
public sealed record ControlledDocumentRevisionDto(long Revision, Guid FileId, string ContentSha256, long ContentLength, string Mime,
    string IntakeState, string PublicationState, string VerificationState, Guid UploadedBy, Guid RegisteredBy, DateTimeOffset RegisteredAt,
    DateTimeOffset? ReleasedAt, Guid? PublishedBy, DateTimeOffset? PublishedAt, ControlledDocumentVerificationDto[] Verifications);
public sealed record ControlledDocumentCustodyDto(long Sequence, long? Revision, string EventType, string Source, Guid? SubjectActorId,
    DateTimeOffset OccurredAt, DateTimeOffset RecordedAt);
public sealed record ControlledDocumentLinkDto(string TargetKind, Guid TargetId, DateTimeOffset LinkedAt);
public sealed record ControlledDocumentSummaryDto(Guid Id, Guid VesselId, string Code, string Title, string Classification, long? CurrentRevision,
    long LatestRevision, uint Version, bool HoldActive, string RetentionState, string DisposalState, string Provenance);
public sealed record ControlledDocumentDto(Guid Id, Guid VesselId, string Code, string Title, string Classification, long? CurrentRevision,
    long LatestRevision, uint Version, bool HoldActive, DateTimeOffset? HoldPlacedAt, string RetentionState, string? RetentionPolicyRef, DateOnly? RetainUntil,
    string DisposalState, string PhysicalDeletion, string Provenance, ControlledDocumentRevisionDto[] Revisions, ControlledDocumentCustodyDto[] Custody,
    ControlledDocumentLinkDto[] Links, DateTimeOffset AsOf);
public sealed record ControlledDocumentListDto(ControlledDocumentSummaryDto[] Items, int Offset, bool HasMore, bool CanWrite);

// PLAN-1511-5802 / 58.02 Rev4. Shared controlled document/evidence custody API over the
// existing secure-file transport. Every mutation: server-resolved actor and vessel scope,
// antiforgery, UUID Idempotency-Key with exact replay, If-Match CAS (428/412), SERIALIZABLE
// unit of work through the audited SaveChanges path, and no-store responses.
public static class ControlledDocumentEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 4 };
    private const string Prefix = "/api/v1/vessels/{vessel:guid}/controlled-documents";

    public static void MapControlledDocuments(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup(Prefix).RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("ListControlledDocuments").Produces<ControlledDocumentListDto>();
        g.MapGet("/{document:guid}", Detail).WithName("GetControlledDocument").Produces<ControlledDocumentDto>();
        g.MapPost("", (Guid vessel, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) =>
                Execute<ControlledDocumentCreateCommand>(h, vessel, null, "Create", Write, (db, a, c, _, ct) => Create(db, a, vessel, c, ct)))
            .WithName("CreateControlledDocument").Accepts<ControlledDocumentCreateCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/revisions", (Guid vessel, Guid document, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentRevisionCommand>(h, vessel, document, "Register", Write,
                    (db, a, c, d, ct) => ControlledDocumentChain.RegisterAsync(db, a.Id, d!, c.FileId, ct)))
            .WithName("RegisterControlledDocumentRevision").Accepts<ControlledDocumentRevisionCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/revisions/{revision:long}/release", (Guid vessel, Guid document, long revision, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentReasonCommand>(h, vessel, document, "Release", Write,
                    (db, a, c, d, ct) => ControlledDocumentChain.ReleaseAsync(db, a.Id, d!, revision, ct)))
            .WithName("ReleaseControlledDocumentRevision").Accepts<ControlledDocumentReasonCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/revisions/{revision:long}/publish", (Guid vessel, Guid document, long revision, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentReasonCommand>(h, vessel, document, "Publish", (a, _) => Authority(a, "document.publish"),
                    (db, a, c, d, ct) => ControlledDocumentChain.PublishAsync(db, a.Id, d!, revision, ct)))
            .WithName("PublishControlledDocumentRevision").Accepts<ControlledDocumentReasonCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/revisions/{revision:long}/verifications", (Guid vessel, Guid document, long revision, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentVerificationCommand>(h, vessel, document, "Verify", (a, _) => Authority(a, "evidence.verify"),
                    (db, a, c, d, ct) => ControlledDocumentChain.VerifyAsync(db, a.Id, d!, revision, c.Method, c.Result, c.Basis, ct)))
            .WithName("VerifyControlledDocumentRevision").Accepts<ControlledDocumentVerificationCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/hold", (Guid vessel, Guid document, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentHoldCommand>(h, vessel, document, "HoldPlace", (a, _) => Authority(a, "record.hold"),
                    (db, a, c, d, ct) => Sync(() => ControlledDocumentChain.PlaceHold(db, a.Id, d!, c.AuthorityRef, c.Reason))))
            .WithName("PlaceControlledDocumentHold").Accepts<ControlledDocumentHoldCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/hold/release", (Guid vessel, Guid document, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentReasonCommand>(h, vessel, document, "HoldRelease", (a, _) => Authority(a, "record.hold"),
                    (db, a, c, d, ct) => Sync(() => ControlledDocumentChain.ReleaseHold(db, a.Id, d!, c.Reason))))
            .WithName("ReleaseControlledDocumentHold").Accepts<ControlledDocumentReasonCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/retention", (Guid vessel, Guid document, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentRetentionCommand>(h, vessel, document, "Retention", (a, _) => Authority(a, "record.retention.manage"),
                    (db, a, c, d, ct) => Sync(() => ControlledDocumentChain.SetRetention(db, a.Id, d!, c.PolicyRef, c.RetainUntil))))
            .WithName("SetControlledDocumentRetention").Accepts<ControlledDocumentRetentionCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/disposal-requests", (Guid vessel, Guid document, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentReasonCommand>(h, vessel, document, "DisposalRequest", (a, _) => Authority(a, "record.purge.request"),
                    (db, a, c, d, ct) => Sync(() => ControlledDocumentChain.RequestDisposal(db, a.Id, d!, c.Reason))))
            .WithName("RequestControlledDocumentDisposal").Accepts<ControlledDocumentReasonCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/disposal-requests/approve", (Guid vessel, Guid document, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentReasonCommand>(h, vessel, document, "DisposalApprove", (a, _) => Authority(a, "record.purge.approve"),
                    (db, a, c, d, ct) => Sync(() => ControlledDocumentChain.ApproveDisposal(db, a.Id, d!, c.Reason))))
            .WithName("ApproveControlledDocumentDisposal").Accepts<ControlledDocumentReasonCommand>("application/json").Produces<ControlledDocumentDto>();
        g.MapPost("/{document:guid}/links", (Guid vessel, Guid document, HttpContext h, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken, [FromHeader(Name = "If-Match")] string? ifMatch) =>
                Command<ControlledDocumentLinkCommand>(h, vessel, document, "Link", Write,
                    (db, a, c, d, ct) => LinkTargetReadable(a, c.TargetKind) ? ControlledDocumentChain.LinkAsync(db, a.Id, d!, c.TargetKind, c.TargetId, ct)
                        : throw new ControlledDocumentException("permission_denied")))
            .WithName("LinkControlledDocument").Accepts<ControlledDocumentLinkCommand>("application/json").Produces<ControlledDocumentDto>();
    }

    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static bool Read(TrustedActor? a) => a != null && a.CanRead && a.Permissions.Contains("file.read");
    private static bool Write(TrustedActor a, Guid vessel) => Read(a) && a.Permissions.Contains("file.write") && a.CanEditVessel(vessel);
    // Authority roles (publisher, verifier, records officer, disposal approver) need a real
    // server role and the exact action key; they need not hold directory/vessel edit rights.
    private static bool Authority(TrustedActor a, string permission) => Read(a) && a.HasWriteAttribution && a.Permissions.Contains(permission);
    private static bool LinkTargetReadable(TrustedActor a, string? kind) => kind switch
    {
        "VesselCertificate" => a.Permissions.Contains("certificate.read"),
        "ControlEvidence" => a.Permissions.Contains("evidence.read"),
        "Credential" => a.Permissions.Contains("crew.certificate.read"),
        _ => true // unknown kinds are rejected by the chain as invalid_request
    };
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }
    private static Task<bool> Scope(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) => a.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, ct);

    private static async Task<IResult> Fail(HttpContext h, int status, string code, Guid? id = null)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlledDocument", code, resourceId: id);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = h.TraceIdentifier });
    }

    private static int Status(string code) => code switch
    {
        "resource_not_found" => 404, "permission_denied" or "separation_of_duties" => 403, "version_conflict" => 412,
        "invalid_request" => 400, _ => 409
    };

    private static async Task<IResult> List(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (h.Request.Query.Any(x => x.Key != "offset" || x.Value.Count != 1) || h.Request.Query.ContainsKey("offset") &&
            (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed > 100000)) return await Fail(h, 400, "invalid_query");
        if (!await Scope(db, a!, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        var offset = h.Request.Query.ContainsKey("offset") ? int.Parse(h.Request.Query["offset"]!, CultureInfo.InvariantCulture) : 0;
        var rows = await db.ControlledDocuments.AsNoTracking().Where(x => x.VesselId == vessel).OrderBy(x => x.Code).ThenBy(x => x.Id).Skip(offset).Take(26).ToArrayAsync(ct);
        var today = ControlledDocumentChain.Today(DateTimeOffset.UtcNow);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlledDocumentRead", "Success", resourceId: vessel);
        return Results.Ok(new ControlledDocumentListDto(rows.Take(25).Select(d => new ControlledDocumentSummaryDto(d.Id, d.VesselId, d.Code, d.Title, d.Classification,
            d.CurrentRevision, d.LatestRevision, d.Version, d.HoldActive, ControlledDocumentPolicy.Retention(d.RetentionPolicyRef, d.RetainUntil, today), d.DisposalState, d.Provenance)).ToArray(),
            offset, rows.Length > 25, Write(a!, vessel)));
    }

    private static async Task<IResult> Detail(Guid vessel, Guid document, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Fail(h, 403, "permission_denied", document);
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_query", document);
        if (!await Scope(db, a!, vessel, ct)) return await Fail(h, 404, "resource_not_found", document);
        var d = await db.ControlledDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == document && x.VesselId == vessel, ct);
        if (d == null) return await Fail(h, 404, "resource_not_found", document);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlledDocumentRead", "Success", resourceId: document);
        h.Response.Headers.ETag = $"\"{d.Version}\"";
        return Results.Ok(await Dto(db, a!, d, ct));
    }

    internal static async Task<ControlledDocumentDto> Dto(PoseidonDbContext db, TrustedActor a, ControlledDocument d, CancellationToken ct)
    {
        var revisions = await db.ControlledDocumentRevisions.AsNoTracking().Where(x => x.DocumentId == d.Id && x.VesselId == d.VesselId).OrderBy(x => x.Revision).ToArrayAsync(ct);
        var verifications = await db.ControlledDocumentVerifications.AsNoTracking().Where(x => x.DocumentId == d.Id && x.VesselId == d.VesselId)
            .OrderBy(x => x.CheckedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var custody = await db.ControlledDocumentCustodyEvents.AsNoTracking().Where(x => x.DocumentId == d.Id && x.VesselId == d.VesselId).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        var links = new List<ControlledDocumentLinkDto>();
        // A link is listed only when the reader may read that domain; hidden link counts are not disclosed.
        if (a.Permissions.Contains("certificate.read"))
            links.AddRange((await db.ControlledDocumentCertificateLinks.AsNoTracking().Where(x => x.DocumentId == d.Id && x.VesselId == d.VesselId).ToArrayAsync(ct))
                .Select(x => new ControlledDocumentLinkDto("VesselCertificate", x.CertificateId, x.LinkedAt)));
        if (a.Permissions.Contains("evidence.read"))
            links.AddRange((await db.ControlledDocumentEvidenceLinks.AsNoTracking().Where(x => x.DocumentId == d.Id && x.VesselId == d.VesselId).ToArrayAsync(ct))
                .Select(x => new ControlledDocumentLinkDto("ControlEvidence", x.EvidenceId, x.LinkedAt)));
        if (a.Permissions.Contains("crew.certificate.read"))
            links.AddRange((await db.ControlledDocumentCredentialLinks.AsNoTracking().Where(x => x.DocumentId == d.Id && x.VesselId == d.VesselId).ToArrayAsync(ct))
                .Select(x => new ControlledDocumentLinkDto("Credential", x.CredentialId, x.LinkedAt)));
        var showBasis = a.Permissions.Contains("evidence.verify");
        var now = DateTimeOffset.UtcNow;
        return new(d.Id, d.VesselId, d.Code, d.Title, d.Classification, d.CurrentRevision, d.LatestRevision, d.Version, d.HoldActive, d.HoldActive ? d.HoldPlacedAt : null,
            ControlledDocumentPolicy.Retention(d.RetentionPolicyRef, d.RetainUntil, ControlledDocumentChain.Today(now)), d.RetentionPolicyRef, d.RetainUntil,
            d.DisposalState, "NOT_STARTED", d.Provenance,
            revisions.Select(r => {
                var own = verifications.Where(v => v.Revision == r.Revision).ToArray();
                return new ControlledDocumentRevisionDto(r.Revision, r.FileId, r.ContentSha256, r.ContentLength, r.Mime, r.IntakeState, r.PublicationState,
                    ControlledDocumentPolicy.Verification(own.LastOrDefault()?.Result), r.UploadedBy, r.RegisteredBy, r.RegisteredAt, r.ReleasedAt, r.PublishedBy, r.PublishedAt,
                    own.Select(v => new ControlledDocumentVerificationDto(v.Id, v.VerifierId, v.Method, v.Result, showBasis ? v.Basis : null, v.CheckedAt)).ToArray());
            }).ToArray(),
            custody.Select(c => new ControlledDocumentCustodyDto(c.Sequence, c.Revision, c.EventType, c.Source, c.SubjectActorId, c.OccurredAt, c.RecordedAt)).ToArray(),
            links.OrderBy(x => x.TargetKind, StringComparer.Ordinal).ThenBy(x => x.TargetId).ToArray(), now);
    }

    // The command vessel comes from the authorized route, never from the body.
    private static async Task<ControlledDocument> Create(PoseidonDbContext db, TrustedActor a, Guid vessel, ControlledDocumentCreateCommand c, CancellationToken ct)
    {
        var code = c.Code?.Trim().ToUpperInvariant();
        if (code != null && await db.ControlledDocuments.AnyAsync(x => x.Code == code, ct)) throw new ControlledDocumentException("duplicate_code");
        return ControlledDocumentChain.Create(db, a.Id, vessel, c.Code ?? "", c.Title ?? "", c.Classification ?? "");
    }

    private static Task<IResult> Command<T>(HttpContext h, Guid vessel, Guid? document, string action, Func<TrustedActor, Guid, bool> allowed,
        Func<PoseidonDbContext, TrustedActor, T, ControlledDocument?, CancellationToken, Task> apply) where T : class =>
        Execute<T>(h, vessel, document, action, allowed, async (db, a, c, d, ct) => { await apply(db, a, c, d, ct); return d!; });

    private static async Task<IResult> Execute<T>(HttpContext h, Guid vessel, Guid? document, string action, Func<TrustedActor, Guid, bool> allowed,
        Func<PoseidonDbContext, TrustedActor, T, ControlledDocument?, CancellationToken, Task<ControlledDocument>> apply) where T : class
    {
        h.Response.Headers.CacheControl = "no-store";
        var a = Actor(h); var ct = h.RequestAborted;
        if (a == null || !allowed(a, vessel)) return await Fail(h, 403, "permission_denied", document);
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found", document);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid", document); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var keyId) || keyId == Guid.Empty) return await Fail(h, 400, "invalid_idempotency_key", document);
        var key = keyId.ToString("D");
        uint expected = 0;
        if (document != null)
        {
            var match = h.Request.Headers.IfMatch;
            if (match.Count != 1 || string.IsNullOrEmpty(match[0])) return await Fail(h, 428, "precondition_required", document);
            var raw = match[0]!;
            if (raw.Length < 3 || raw[0] != '"' || raw[^1] != '"' || !uint.TryParse(raw.AsSpan(1, raw.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out expected))
                return await Fail(h, 400, "invalid_request", document);
        }
        T command; string body;
        try
        {
            if (!h.Request.HasJsonContentType()) return await Fail(h, 400, "invalid_request", document);
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 4096, ct);
            using var parsed = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 4 });
            if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(parsed.RootElement); body = parsed.RootElement.GetRawText();
            command = JsonSerializer.Deserialize<T>(body, Json) ?? throw new JsonException();
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException) { return await Fail(h, 400, "invalid_request", document); }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { action, vessel, document, expected, body, route = h.Request.Path.Value }))));

        async Task<IResult?> Replay()
        {
            var live = Actor(h);
            if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !allowed(live, vessel) || !await Scope(db, live, vessel, ct)) return await Fail(h, 403, "permission_denied", document);
            var receipt = await db.ControlledDocumentReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt == null) return null;
            if (receipt.Fingerprint != fingerprint || receipt.Action != action || receipt.VesselId != vessel) return await Fail(h, 409, "idempotency_key_reused", document);
            if (!await db.ControlledDocuments.AnyAsync(x => x.Id == receipt.DocumentId && x.VesselId == vessel, ct)) return await Fail(h, 404, "resource_not_found", document);
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlledDocumentReplay", "Success", resourceId: receipt.DocumentId);
            return Results.Content(receipt.OutcomeJson, "application/json");
        }
        var replay = await Replay(); if (replay != null) return replay;
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ControlledDocumentCommand", "Attempted", resourceId: document);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var current = document == null ? null : await ControlledDocumentChain.LoadAsync(db, vessel, document.Value, expected, ct);
            var d = await apply(db, a, command, current, ct);
            await db.SaveChangesAsync(ct);
            var outcome = JsonSerializer.Serialize(await Dto(db, a, d, ct), Json);
            db.ControlledDocumentReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, VesselId = vessel, DocumentId = d.Id, Action = action,
                Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct);
            var live = Actor(h);
            if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !allowed(live, vessel) || !await Scope(db, live, vessel, ct))
            { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, 403, "permission_denied", document); }
            await tx.CommitAsync(ct);
            h.Response.Headers.ETag = $"\"{d.Version}\"";
            return Results.Content(outcome, "application/json");
        }
        catch (ControlledDocumentException e)
        {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await Fail(h, Status(e.Code), e.Code, document);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await Replay() ?? await Fail(h, 412, "version_conflict", document);
        }
        catch (Exception e) when (PostgresFailure.IsDatabase(e))
        {
            DatabaseFailureObservation.Record(h, e);
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var code = PostgresFailure.Provider(e)?.SqlState;
            var conflict = code is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.CheckViolation;
            return await Replay() ?? await Fail(h, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable", document);
        }
    }
}
