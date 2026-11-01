using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentDraftCommand(string? Title);
public sealed record DocumentDraftDto(Guid Id, Guid VesselId, long Revision, uint Version, string? Title, string Status, string Provenance);
public sealed record DocumentListDto(DocumentDraftDto[] Items, bool CanWrite, int Offset, bool HasMore);

public static class DocumentEndpoints
{
    public static void MapDocuments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/documents").RequireAuthorization("DirectoryRead");
        group.MapGet("", List).Produces<DocumentListDto>();
        group.MapPost("", Create).Produces<DocumentDraftDto>();
        group.MapPost("/{document:guid}/versions", Revise).Produces<DocumentDraftDto>();
        group.MapGet("/{document:guid}/versions/{revision:long}", Detail).Produces<DocumentDraftDto>();
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Read(TrustedActor a) => a.CanRead && a.Permissions.Contains("file.read");
    private static bool Write(TrustedActor a, Guid vessel) => Read(a) && a.Permissions.Contains("file.write") && a.CanEditVessel(vessel);
    private static async Task<bool> Scope(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) =>
        Read(a) && await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, ct);
    private static DocumentDraftDto Dto(Document d, DocumentVersion v) => new(d.Id, d.VesselId, v.Revision, d.Version, v.Title, v.Status, v.Provenance);
    private static async Task<IResult> Failure(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "FileRequest", code);
        return Results.Problem(statusCode: status, title: code);
    }
    private static async Task<IResult> List(Guid vessel, HttpContext h, PoseidonDbContext db) {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Failure(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, ct)) return await Failure(h, 404, "resource_not_found");
        if (h.Request.Query.Keys.Any(k => k != "offset") || h.Request.Query["offset"].Count > 1 ||
            h.Request.Query.ContainsKey("offset") && !int.TryParse(h.Request.Query["offset"], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _)) return await Failure(h, 400, "invalid_request");
        var offset = h.Request.Query.ContainsKey("offset") ? int.Parse(h.Request.Query["offset"]!, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var rows = await (from d in db.Documents.AsNoTracking() join v in db.DocumentVersions.AsNoTracking()
            on new { d.TenantId, DocumentId = d.Id, d.Revision } equals new { v.TenantId, v.DocumentId, v.Revision }
            where d.VesselId == vessel orderby d.Id select new { d, v }).Skip(offset).Take(26).ToArrayAsync(ct);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "FileList", "Success", resourceId: vessel);
        return Results.Ok(new DocumentListDto(rows.Take(25).Select(x => Dto(x.d, x.v)).ToArray(), Write(a, vessel), offset, rows.Length > 25));
    }
    private static async Task<IResult> Detail(Guid vessel, Guid document, long revision, HttpContext h, PoseidonDbContext db) {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Failure(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, ct)) return await Failure(h, 404, "resource_not_found");
        var d = await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == document && x.VesselId == vessel, ct);
        var v = await db.DocumentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId == document && x.Revision == revision && x.VesselId == vessel, ct);
        if (d == null || v == null) return await Failure(h, 404, "resource_not_found");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "FileDetail", "Success", resourceId: document);
        return Results.Ok(Dto(d, v));
    }
    private static Task<IResult> Create(Guid vessel, HttpContext h, PoseidonDbContext db) => Save(vessel, null, h, db);
    private static Task<IResult> Revise(Guid vessel, Guid document, HttpContext h, PoseidonDbContext db) => Save(vessel, document, h, db);
    private static async Task<IResult> Save(Guid vessel, Guid? document, HttpContext h, PoseidonDbContext db) {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Write(a, vessel)) return await Failure(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, ct)) return await Failure(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Failure(h, 403, "csrf_invalid"); }
        var key = h.Request.Headers["Idempotency-Key"].Count == 1 ? h.Request.Headers["Idempotency-Key"].ToString() : "";
        if (!Guid.TryParseExact(key, "D", out _)) return await Failure(h, 400, "invalid_idempotency_key");
        DocumentDraftCommand? command;
        try {
            if (!h.Request.HasJsonContentType()) return await Failure(h, 400, "invalid_request");
            using var bytes = new MemoryStream(); await Poseidon.Domain.SecureFilePolicy.CopyAsync(h.Request.Body, bytes, 2048, ct);
            command = JsonSerializer.Deserialize<DocumentDraftCommand>(bytes.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 3 });
        } catch (Exception e) when (e is JsonException or InvalidDataException) { return await Failure(h, 400, "invalid_request"); }
        if (command == null || command.Title is { Length: > 200 } || command.Title?.Any(char.IsControl) == true) return await Failure(h, 400, "invalid_request");
        var etag = h.Request.Headers.IfMatch.ToString();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { vessel, document, command.Title, etag }))));
        var receipt = await db.CommandReceipts.SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
        if (receipt != null) {
            if (receipt.Action != "DocumentDraftSave" || receipt.Fingerprint != fingerprint) return await Failure(h, 409, "idempotency_key_reused");
            var original = await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.ResourceId && x.VesselId == vessel, ct);
            var snapshot = await db.DocumentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId == receipt.ResourceId && x.VesselId == vessel && x.Revision == receipt.RegisterRevision, ct);
            if (original == null || snapshot == null) return await Failure(h, 404, "resource_not_found");
            return Results.Ok(Dto(original, snapshot) with { Version = receipt.RegisterVersion ?? 0 });
        }
        Document d;
        if (document == null) { d = new() { TenantId = a.TenantId, Id = Guid.NewGuid(), VesselId = vessel }; db.Documents.Add(d); }
        else {
            var existing = await db.Documents.SingleOrDefaultAsync(x => x.Id == document && x.VesselId == vessel, ct);
            if (existing == null) return await Failure(h, 404, "resource_not_found");
            if (etag != $"\"{existing.Version}\"") return await Failure(h, 409, "file_attempt_conflict");
            d = existing; d.Revision = checked(d.Revision + 1);
        }
        var version = new DocumentVersion { TenantId = a.TenantId, DocumentId = d.Id, VesselId = vessel, Revision = d.Revision, Title = command.Title, CreatedAt = DateTimeOffset.UtcNow };
        db.DocumentVersions.Add(version);
        try {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = "DocumentDraftSave", Fingerprint = fingerprint, ResourceId = d.Id, RegisterRevision = d.Revision, RegisterVersion = d.Version, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(Dto(d, version));
        } catch (DbUpdateException) { return await Failure(h, 409, "file_attempt_conflict"); }
    }
}
