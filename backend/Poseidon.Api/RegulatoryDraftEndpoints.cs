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
public sealed record SaveRegulatoryDraft(uint? ExpectedVersion, RegulatoryDraftContent? Content);
public sealed record RegulatoryDraftDto(Guid TenantId, VesselDto Vessel, uint Version, long Revision,
    DateTimeOffset? SavedAt, string Provenance, RegulatoryDraftContent Content);
public sealed record RegulatoryRevisionDto(long Revision, DateTimeOffset SavedAt, string Provenance, RegulatoryDraftContent Content);

public static class RegulatoryDraftEndpoints
{
    public static void MapRegulatoryDrafts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/vessels/{id:guid}/regulatory-profile").RequireAuthorization("DirectoryRead");
        group.MapGet("", Read).WithName("GetRegulatoryDraft").Produces<RegulatoryDraftDto>()
            .Produces(401).Produces(403).Produces(404).Produces(503);
        group.MapGet("/history", History).WithName("GetRegulatoryHistory").Produces<PageDto<RegulatoryRevisionDto>>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
        group.MapPut("", (Guid id, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) => Save(id, http))
            .WithName("SaveRegulatoryDraft").Accepts<SaveRegulatoryDraft>("application/json").Produces<RegulatoryDraftDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).Produces(503)
            .WithDescription("Draft only. Trusted vessel edit grant, CSRF and Idempotency-Key required. expectedVersion 0 creates an absent draft; otherwise use returned xmin. No submission, verification or operational release.");
    }

    private static TrustedActor Actor(HttpContext http) => http.RequestServices.GetRequiredService<ActorResolver>().Resolve(http.User)!;
    private static RegulatoryDraftDto Dto(TrustedActor actor, Vessel vessel, RegulatoryDraft? draft) => new(actor.TenantId,
        new(vessel.Id, vessel.CompanyId, vessel.Code, vessel.Name, vessel.Kind.ToString(), vessel.Version, actor.CanEditVessel(vessel.Id)),
        draft?.Version ?? 0, draft?.Revision ?? 0, draft?.SavedAt, draft?.Provenance ?? "Missing",
        draft == null ? new() : RegulatoryDraftStorage.Deserialize(draft.ContentJson));

    private static async Task<IResult> Read(Guid id, HttpContext http, PoseidonDbContext db, SecurityAudit audit)
    {
        var actor = Actor(http);
        var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, http.RequestAborted);
        await audit.WriteAsync(http, "ProfileRead", vessel == null ? "NotFound" : "Success", resourceId: id);
        if (vessel == null) return Results.NotFound();
        return Results.Ok(Dto(actor, vessel, await db.RegulatoryDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == id, http.RequestAborted)));
    }

    private static async Task<IResult> History(Guid id, HttpContext http, PoseidonDbContext db, SecurityAudit audit, int? offset, int? limit)
    {
        var actor = Actor(http);
        var allowed = await actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(x => x.Id == id, http.RequestAborted);
        await audit.WriteAsync(http, "ProfileHistory", allowed ? "Success" : "NotFound", resourceId: id);
        if (!allowed) return Results.NotFound();
        if (offset is < 0 or > 100000 || limit is < 1 or > 100) return Results.BadRequest();
        var (skip, take) = (offset ?? 0, limit ?? 25);
        var rows = await db.RegulatoryDraftRevisions.AsNoTracking().Where(x => x.VesselId == id)
            .OrderByDescending(x => x.Revision).Skip(skip).Take(take + 1).ToArrayAsync(http.RequestAborted);
        return Results.Ok(new PageDto<RegulatoryRevisionDto>(rows.Take(take).Select(x =>
            new RegulatoryRevisionDto(x.Revision, x.SavedAt, x.Provenance, RegulatoryDraftStorage.Deserialize(x.ContentJson))).ToArray(), skip, take, rows.Length > take));
    }

    private static async Task<IResult> Save(Guid id, HttpContext http)
    {
        var actor = Actor(http);
        var audit = http.RequestServices.GetRequiredService<SecurityAudit>();
        async Task<IResult> Fail(int status, string code, Dictionary<string, string[]>? errors = null)
        {
            await audit.WriteAsync(http, "ProfileSave", code, resourceId: id);
            return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?>
                { ["code"] = code, ["errors"] = errors ?? [], ["correlationId"] = http.TraceIdentifier });
        }
        await audit.WriteAsync(http, "ProfileSave", "Attempted", resourceId: id);
        var db = http.RequestServices.GetRequiredService<PoseidonDbContext>();
        // Scope and edit authorization are reevaluated before every receipt lookup, including replay.
        var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, http.RequestAborted);
        if (vessel == null) return await Fail(404, "resource_not_found");
        if (!actor.CanEditVessel(id)) return await Fail(403, "permission_denied");
        try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
        catch (AntiforgeryValidationException) { return await Fail(403, "csrf_invalid"); }
        if (!http.Request.HasJsonContentType() || http.Request.ContentLength > 32768) return await Fail(400, "invalid_request");
        SaveRegulatoryDraft command;
        try
        {
            using var buffer = new MemoryStream();
            var bytes = new byte[4096]; int count;
            while ((count = await http.Request.Body.ReadAsync(bytes, http.RequestAborted)) > 0)
            {
                if (buffer.Length + count > 32768) return await Fail(400, "invalid_request");
                buffer.Write(bytes, 0, count);
            }
            command = JsonSerializer.Deserialize<SaveRegulatoryDraft>(buffer.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 6 }) ?? throw new JsonException();
        }
        catch (JsonException) { return await Fail(400, "invalid_request"); }
        if (command.ExpectedVersion == null || command.Content == null) return await Fail(400, "validation_failed");
        var (content, errors) = RegulatoryDraftPolicy.Normalize(command.Content);
        if (errors.Count > 0) return await Fail(400, "validation_failed", errors);
        var keys = http.Request.Headers["Idempotency-Key"];
        var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return await Fail(400, "invalid_idempotency_key");
        var json = RegulatoryDraftStorage.Serialize(content);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"ProfileSave/{id}/{command.ExpectedVersion}/{json}")));
        async Task<IResult?> Replay()
        {
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.Key == key, http.RequestAborted);
            if (receipt == null) return null;
            if (receipt.Action != "ProfileSave" || receipt.ResourceId != id || receipt.Fingerprint != fingerprint)
                return await Fail(409, "idempotency_key_reused");
            http.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Ok(Dto(actor, vessel, await db.RegulatoryDrafts.AsNoTracking().SingleAsync(x => x.VesselId == id, http.RequestAborted)));
        }
        var replay = await Replay(); if (replay != null) return replay;
        var draft = await db.RegulatoryDrafts.SingleOrDefaultAsync(x => x.VesselId == id, http.RequestAborted);
        if ((draft?.Version ?? 0) != command.ExpectedVersion) return await Replay() ?? await Fail(409, "version_conflict");
        await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
        try
        {
            if (draft == null)
            {
                draft = new() { TenantId = actor.TenantId, VesselId = id, ContentJson = json, Provenance = "Missing" };
                db.RegulatoryDrafts.Add(draft);
            }
            else
            {
                db.Entry(draft).Property(x => x.Version).OriginalValue = command.ExpectedVersion.Value;
                // Always issue an audited UPDATE with xmin, even for unchanged values.
                db.Entry(draft).Property(x => x.ContentJson).IsModified = true;
            }
            draft.ContentJson = json; draft.Revision++; draft.SavedAt = DateTimeOffset.UtcNow;
            draft.Provenance = content.Declaration.State == "Known" || content.SourceReference.State == "Known" ? "DeclaredUnverified" :
                content.Declaration.State == "Unknown" || content.SourceReference.State == "Unknown" ? "Unknown" : "Missing";
            db.RegulatoryDraftRevisions.Add(draft.Snapshot());
            await db.SaveChangesAsync(http.RequestAborted);
            db.CommandReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = "ProfileSave",
                Fingerprint = fingerprint, ResourceId = id, CommittedAt = draft.SavedAt });
            await db.SaveChangesAsync(http.RequestAborted);
            await transaction.CommitAsync(http.RequestAborted);
            return Results.Ok(Dto(actor, vessel, draft));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await Replay() ?? await Fail(409, "version_conflict");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await Replay() ?? await Fail(409, "version_conflict");
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return await Fail(503, "service_unavailable");
        }
    }
}
