using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ControlledDocumentContentAccessCommand(string? Purpose);
public sealed record ControlledDocumentContentAccessDto(Guid RecordId, Guid DocumentId, long Revision, Guid FileId, long FileRevision, string Mime, string ContentSha256,
    string Classification, string Purpose, string PolicyBasis, string Attribution, string Print, string Capability, DateTimeOffset ExpiresAt, DateTimeOffset IssuedAt);

// DEV-114 / T-DOC2-001 content access by classification. The only way to obtain bytes of a revision
// registered in a controlled document: the generic vessel-file access route refuses such files.
// Every authorization appends an attributed access record (purpose VIEW for the side-by-side compare
// renderer, DOWNLOAD for saving) and the bytes route re-checks the policy for the live actor. The
// attribution text is shown as an on-screen watermark; bytes are not stamped and no DRM is claimed.
public static class ControlledDocumentAccessEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 4 };
    private const string Action = "ContentAccess", AuditAction = "ControlledDocumentContentAccess";

    internal static void MapControlledDocumentAccess(RouteGroupBuilder g) =>
        g.MapPost("/{document:guid}/revisions/{revision:long}/content-access", (Guid vessel, Guid document, long revision, HttpContext h, PoseidonDbContext db) => Authorize(vessel, document, revision, h, db))
            .WithName("AuthorizeControlledDocumentContent").Accepts<ControlledDocumentContentAccessCommand>("application/json").Produces<ControlledDocumentContentAccessDto>();

    internal static async Task AcknowledgeAsync(HttpContext h, PoseidonDbContext db, TrustedActor a, ControlledDocument d, long revision, ControlledDocumentAcknowledgeCommand c, CancellationToken ct)
    {
        // DE-005: a person acknowledges only for themselves; automation/service principals cannot.
        if (!CertificateRenewalEndpoints.Human(h, a)) throw new ControlledDocumentException("human_actor_required");
        var r = await db.ControlledDocumentRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId == d.Id && x.VesselId == d.VesselId && x.Revision == revision, ct)
            ?? throw new ControlledDocumentException("resource_not_found");
        var already = await db.ControlledDocumentAcknowledgements.AnyAsync(x => x.DocumentId == d.Id && x.Revision == revision && x.ActorId == a.Id, ct);
        if (ControlledDocumentSecurityPolicy.AcknowledgeBlocker(r.PublicationState, r.IntakeState, d.DisposalState, d.Classification, ControlledDocumentEndpoints.RestrictedContent(a),
                r.ContentSha256, c.DisplayedContentSha256, c.StatementAccepted, already) is { } blocked)
            throw new ControlledDocumentException(blocked);
        db.ControlledDocumentAcknowledgements.Add(new() { TenantId = d.TenantId, Id = Guid.NewGuid(), VesselId = d.VesselId, DocumentId = d.Id, Revision = revision, ActorId = a.Id,
            ContentSha256 = r.ContentSha256, Statement = ControlledDocumentSecurityPolicy.AcknowledgementStatement, AcknowledgedAt = DateTimeOffset.UtcNow });
    }

    // Live policy for the bytes route; null = not a controlled-document file.
    internal static async Task<bool?> FileAllowedAsync(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid file, CancellationToken ct)
    {
        var hit = await db.ControlledDocumentRevisions.AsNoTracking().Where(r => r.FileId == file && r.VesselId == vessel)
            .Join(db.ControlledDocuments.AsNoTracking(), r => new { r.TenantId, r.DocumentId }, d => new { d.TenantId, DocumentId = d.Id },
                (r, d) => new { r.IntakeState, d.Classification, d.IspsRestricted, d.DisposalState }).SingleOrDefaultAsync(ct);
        if (hit == null) return null;
        return ControlledDocumentSecurityPolicy.Visible(hit.IspsRestricted, ControlledDocumentEndpoints.Isps(a)) &&
            ControlledDocumentSecurityPolicy.ContentBlocker(hit.Classification, ControlledDocumentEndpoints.RestrictedContent(a), hit.IntakeState, hit.DisposalState) == null;
    }

    private static async Task<IResult> Authorize(Guid vessel, Guid document, long revision, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = ControlledDocumentEndpoints.Actor(h);
        if (!ControlledDocumentEndpoints.Read(a) || !a!.HasWriteAttribution) return await ControlledDocumentEndpoints.Fail(h, 403, "permission_denied", document);
        if (!await ControlledDocumentEndpoints.Scope(db, a, vessel, ct)) return await ControlledDocumentEndpoints.Fail(h, 404, "resource_not_found", document);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await ControlledDocumentEndpoints.Fail(h, 403, "csrf_invalid", document); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var keyId) || keyId == Guid.Empty) return await ControlledDocumentEndpoints.Fail(h, 400, "invalid_idempotency_key", document);
        var key = keyId.ToString("D");
        if (!SecureFileEndpoints.Ready(h)) return await ControlledDocumentEndpoints.Fail(h, 503, "file_settings_unavailable", document);
        ControlledDocumentContentAccessCommand command; string body;
        try
        {
            if (!h.Request.HasJsonContentType()) throw new JsonException();
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 1024, ct);
            using var parsed = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 4 });
            if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(parsed.RootElement); body = parsed.RootElement.GetRawText();
            command = JsonSerializer.Deserialize<ControlledDocumentContentAccessCommand>(body, Json) ?? throw new JsonException();
            if (command.Purpose == null || !ControlledDocumentSecurityPolicy.Purposes.Contains(command.Purpose, StringComparer.Ordinal)) throw new JsonException();
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException) { return await ControlledDocumentEndpoints.Fail(h, 400, "invalid_request", document); }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Action, vessel, document, revision, body }))));
        var isps = ControlledDocumentEndpoints.Isps(a); var restricted = ControlledDocumentEndpoints.RestrictedContent(a);
        Guid recordId;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                var receipt = await db.ControlledDocumentReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
                if (receipt != null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Fingerprint != fingerprint || receipt.Action != Action || receipt.VesselId != vessel || receipt.DocumentId != document)
                        return await ControlledDocumentEndpoints.Fail(h, 409, "idempotency_key_reused", document);
                    recordId = JsonSerializer.Deserialize<Guid>(receipt.OutcomeJson);
                }
                else
                {
                    var d = await db.ControlledDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == document && x.VesselId == vessel && (!x.IspsRestricted || isps), ct);
                    var r = d == null ? null : await db.ControlledDocumentRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId == d.Id && x.VesselId == vessel && x.Revision == revision, ct);
                    if (d == null || r == null) { await tx.RollbackAsync(CancellationToken.None); return await ControlledDocumentEndpoints.Fail(h, 404, "resource_not_found", document); }
                    if (ControlledDocumentSecurityPolicy.ContentBlocker(d.Classification, restricted, r.IntakeState, d.DisposalState) is { } blocked)
                    { await tx.RollbackAsync(CancellationToken.None); return await ControlledDocumentEndpoints.Fail(h, blocked == "document_download_restricted" ? 403 : 409, blocked, document); }
                    var policy = ControlledDocumentSecurityPolicy.Content(d.Classification, restricted);
                    var now = DateTimeOffset.UtcNow; recordId = Guid.NewGuid();
                    db.ControlledDocumentAccessRecords.Add(new() { TenantId = a.TenantId, Id = recordId, VesselId = vessel, DocumentId = d.Id, Revision = r.Revision, FileId = r.FileId,
                        ActorId = a.Id, Classification = d.Classification, Purpose = command.Purpose!, PolicyBasis = policy.Basis,
                        Attribution = ControlledDocumentSecurityPolicy.Attribution(d.Code, r.Revision, recordId, a.Id, now, d.Classification), ContentSha256 = r.ContentSha256, IssuedAt = now });
                    await db.SaveChangesAsync(ct);
                    // The receipt keeps only the record id: capabilities are never persisted.
                    db.ControlledDocumentReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, VesselId = vessel, DocumentId = d.Id, Action = Action,
                        Fingerprint = fingerprint, OutcomeJson = JsonSerializer.Serialize(recordId), SavedAt = now });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                }
            }
            catch (Exception e) when (PostgresFailure.IsDatabase(e) || e is DbUpdateConcurrencyException)
            {
                DatabaseFailureObservation.Record(h, e);
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                var conflict = e is DbUpdateConcurrencyException || PostgresFailure.IsConflict(e);
                return await ControlledDocumentEndpoints.Fail(h, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable", document);
            }
        }
        // Fresh outcome under the live actor: the policy is re-evaluated and a new short-lived capability issued.
        db.ChangeTracker.Clear();
        var live = ControlledDocumentEndpoints.Actor(h);
        if (live == null || live.Id != a.Id || live.TenantId != a.TenantId || !ControlledDocumentEndpoints.Read(live) || !await ControlledDocumentEndpoints.Scope(db, live, vessel, ct))
            return await ControlledDocumentEndpoints.Fail(h, 403, "permission_denied", document);
        var record = await db.ControlledDocumentAccessRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recordId && x.ActorId == a.Id && x.VesselId == vessel, ct);
        if (record == null) return await ControlledDocumentEndpoints.Fail(h, 503, "command_outcome_withheld", document);
        if (await FileAllowedAsync(db, live, vessel, record.FileId, ct) != true) return await ControlledDocumentEndpoints.Fail(h, 403, "document_download_restricted", document);
        var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == record.FileId && f.VesselId == vessel, ct);
        if (file == null || !string.Equals(file.Checksum, record.ContentSha256, StringComparison.OrdinalIgnoreCase))
            return await ControlledDocumentEndpoints.Fail(h, 409, "clean_file_unavailable", document);
        var expires = DateTimeOffset.UtcNow.AddSeconds(h.RequestServices.GetRequiredService<SecureFileSettings>().CapabilitySeconds);
        var token = h.RequestServices.GetRequiredService<FileCapabilities>().Issue(a.TenantId, a.Id, vessel, file.Id, file.Revision, expires);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, AuditAction, "Authorized", resourceId: document);
        var print = ControlledDocumentSecurityPolicy.Content(record.Classification, ControlledDocumentEndpoints.RestrictedContent(live)).Print;
        return Results.Json(new ControlledDocumentContentAccessDto(record.Id, record.DocumentId, record.Revision, file.Id, file.Revision, file.Mime, record.ContentSha256,
            record.Classification, record.Purpose, record.PolicyBasis, record.Attribution, print, token, expires, record.IssuedAt), Json);
    }
}
