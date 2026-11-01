using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterCommand(uint? ExpectedVersion, RegisterContent? Content, Guid? SourceId = null, long? SourceRevision = null);
public sealed record RegisterRevisionDto(Guid Id, string Kind, long Revision, DateTimeOffset SavedAt, string Provenance,
    string Hash, RegisterContent Content, Guid? SourceId, long? SourceRevision, string Lifecycle = "DRAFT");
public sealed record RegisterDto(uint Version, bool CanEdit, RegisterRevisionDto Current);

public static class RegulatoryRegisterEndpoints
{
    public static void MapRegulatoryRegister(this IEndpointRouteBuilder app)
    {
        foreach (var (path, kind) in new[] { ("source-drafts", "Source"), ("requirement-drafts", "Requirement") })
        {
            var group = app.MapGroup("/api/v1/" + path).RequireAuthorization("RegulatoryDraftRead");
            group.MapGet("", (HttpContext h, PoseidonDbContext db, SecurityAudit a, string? q, int? offset, int? limit) => List(kind, h, db, a, q, offset, limit))
                .WithName("List" + kind + "Drafts").Produces<PageDto<RegisterDto>>().Produces(400).Produces(401).Produces(403).Produces(503);
            group.MapGet("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit a) => Read(kind, id, h, db, a))
                .WithName("Get" + kind + "Draft").Produces<RegisterDto>().Produces(401).Produces(403).Produces(404).Produces(503);
            group.MapGet("/{id:guid}/history", (Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit a, int? offset, int? limit) => History(kind, id, h, db, a, offset, limit))
                .WithName("Get" + kind + "DraftHistory").Produces<PageDto<RegisterRevisionDto>>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
            group.MapPost("", (Delegate)((HttpContext h) => Save(kind, null, h))).WithName("Create" + kind + "Draft").Accepts<RegisterCommand>("application/json")
                .Produces<RegisterDto>().ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(409).Produces(503);
            group.MapPut("/{id:guid}", (Guid id, HttpContext h) => Save(kind, id, h)).WithName("Save" + kind + "Draft").Accepts<RegisterCommand>("application/json")
                .Produces<RegisterDto>().ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).Produces(503);
        }
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static RegisterRevisionDto Revision(RegisterRevision r) => new(r.Id, r.Kind, r.Revision, r.SavedAt, r.Provenance, r.Hash, RegisterStorage.Deserialize(r.ContentJson), r.SourceId, r.SourceRevision);
    private static RegisterDto Dto(TrustedActor actor, RegisterDraft d, RegisterRevision r) => new(d.Version, actor.CanWriteRegulatory, Revision(r));
    private static bool Bounds(int? offset, int? limit) => offset is not (< 0 or > 100000) && limit is not (< 1 or > 100);
    private static async Task<IResult> List(string kind, HttpContext h, PoseidonDbContext db, SecurityAudit audit, string? q, int? offset, int? limit)
    {
        if (!Bounds(offset, limit) || q is { Length: > 100 } || q?.Any(char.IsControl) == true) {
            await audit.WriteAsync(h, "RegisterList", "Invalid"); return Results.BadRequest();
        }
        var actor = Actor(h); var skip = offset ?? 0; var take = limit ?? 25; var text = q?.Trim() ?? "";
        // Tenant-filtered metadata only; literal Contains avoids interpreting user wildcards.
        var rows = await (from d in db.RegisterDrafts.AsNoTracking() join r in db.RegisterRevisions.AsNoTracking()
            on new { d.TenantId, d.Kind, d.Id, d.Revision } equals new { r.TenantId, r.Kind, r.Id, r.Revision }
            where d.Kind == kind && (text == "" || d.ContentJson.Contains(text) || d.Id.ToString().Contains(text))
            orderby d.SavedAt descending, d.Id
            select new { Draft = d, Revision = r }).Skip(skip).Take(take + 1).ToArrayAsync(h.RequestAborted);
        await audit.WriteAsync(h, "RegisterList", "Success");
        return Results.Ok(new PageDto<RegisterDto>(rows.Take(take).Select(x => Dto(actor, x.Draft, x.Revision)).ToArray(), skip, take, rows.Length > take));
    }
    private static async Task<IResult> Read(string kind, Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit audit)
    {
        var d = await db.RegisterDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Kind == kind && x.Id == id, h.RequestAborted);
        await audit.WriteAsync(h, "RegisterRead", d == null ? "NotFound" : "Success", resourceId: id);
        if (d == null) return Results.NotFound();
        var r = await db.RegisterRevisions.AsNoTracking().SingleAsync(x => x.Kind == kind && x.Id == id && x.Revision == d.Revision, h.RequestAborted);
        return Results.Ok(Dto(Actor(h), d, r));
    }
    private static async Task<IResult> History(string kind, Guid id, HttpContext h, PoseidonDbContext db, SecurityAudit audit, int? offset, int? limit)
    {
        if (!Bounds(offset, limit)) { await audit.WriteAsync(h, "RegisterHistory", "Invalid", resourceId: id); return Results.BadRequest(); }
        var exists = await db.RegisterDrafts.AnyAsync(x => x.Id == id && x.Kind == kind, h.RequestAborted);
        await audit.WriteAsync(h, "RegisterHistory", exists ? "Success" : "NotFound", resourceId: id);
        if (!exists) return Results.NotFound();
        var skip = offset ?? 0; var take = limit ?? 25;
        var rows = await db.RegisterRevisions.AsNoTracking().Where(x => x.Id == id && x.Kind == kind).OrderByDescending(x => x.Revision).Skip(skip).Take(take + 1).ToArrayAsync(h.RequestAborted);
        return Results.Ok(new PageDto<RegisterRevisionDto>(rows.Take(take).Select(Revision).ToArray(), skip, take, rows.Length > take));
    }
    private static async Task<IResult> Save(string kind, Guid? id, HttpContext h)
    {
        var actor = Actor(h); var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        async Task<IResult> Fail(int status, string code, Dictionary<string, string[]>? errors = null)
        {
            await audit.WriteAsync(h, "RegisterSave", code, resourceId: id);
            return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["errors"] = errors ?? [], ["correlationId"] = h.TraceIdentifier });
        }
        // Rechecked before any receipt lookup. Neither body claims nor directory.write grant this authority.
        if (!actor.CanWriteRegulatory) return await Fail(403, "permission_denied");
        await audit.WriteAsync(h, "RegisterSave", "Attempted", resourceId: id);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(403, "csrf_invalid"); }
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > 16384) return await Fail(400, "invalid_request");
        RegisterCommand command;
        try {
            using var buffer = new MemoryStream(); var bytes = new byte[4096]; int count;
            while ((count = await h.Request.Body.ReadAsync(bytes, h.RequestAborted)) > 0) {
                if (buffer.Length + count > 16384) return await Fail(400, "invalid_request");
                buffer.Write(bytes, 0, count);
            }
            command = JsonSerializer.Deserialize<RegisterCommand>(buffer.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 6 }) ?? throw new JsonException();
        } catch (JsonException) { return await Fail(400, "invalid_request"); }
        if (command.ExpectedVersion == null || command.Content == null || (id == null && command.ExpectedVersion != 0)) return await Fail(400, "validation_failed");
        var errors = RegulatoryRegisterPolicy.Validate(command.Content, kind);
        if ((command.SourceId == null) != (command.SourceRevision == null) || command.SourceId == Guid.Empty || command.SourceRevision is <= 0 || kind == "Source" && command.SourceId != null)
            errors["sourceRevision"] = ["invalid_source_revision"];
        if (errors.Count > 0) return await Fail(400, "validation_failed", errors);
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(400, "invalid_idempotency_key");
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        var json = RegisterStorage.Serialize(command.Content);
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { kind, id, command.ExpectedVersion, json, command.SourceId, command.SourceRevision }));
        var action = kind == "Source" ? "SourceDraftSave" : "RequirementDraftSave";
        async Task<IResult?> Replay() {
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.Key == key, h.RequestAborted);
            if (receipt == null) return null;
            if (receipt.Action != action || receipt.Fingerprint != fingerprint) return await Fail(409, "idempotency_key_reused");
            h.Response.Headers["Idempotency-Replayed"] = "true";
            return await Read(kind, receipt.ResourceId, h, db, audit);
        }
        var replay = await Replay(); if (replay != null) return replay;
        if (command.SourceId != null && !await db.RegisterRevisions.AnyAsync(x => x.Kind == "Source" && x.Id == command.SourceId && x.Revision == command.SourceRevision, h.RequestAborted))
            return await Fail(400, "source_revision_unavailable");
        var draft = id == null ? null : await db.RegisterDrafts.SingleOrDefaultAsync(x => x.Id == id && x.Kind == kind, h.RequestAborted);
        if (id != null && draft == null) return await Fail(404, "resource_not_found");
        if ((draft?.Version ?? 0) != command.ExpectedVersion) return await Replay() ?? await Fail(409, "version_conflict");
        await using var transaction = await db.Database.BeginTransactionAsync(h.RequestAborted);
        try {
            if (draft == null) {
                draft = new() { TenantId = actor.TenantId, Id = Guid.NewGuid(), Kind = kind, ContentJson = json, Provenance = "DeclaredUnverified" };
                db.RegisterDrafts.Add(draft);
            } else {
                db.Entry(draft).Property(x => x.Version).OriginalValue = command.ExpectedVersion.Value;
                db.Entry(draft).Property(x => x.ContentJson).IsModified = true; // No-op saves also test xmin.
            }
            draft.ContentJson = json; draft.Provenance = "DeclaredUnverified"; draft.Revision++;
            var now = DateTimeOffset.UtcNow; draft.SavedAt = now.AddTicks(-(now.Ticks % 10));
            var revision = draft.Snapshot(command.SourceId, command.SourceRevision); db.RegisterRevisions.Add(revision);
            await db.SaveChangesAsync(h.RequestAborted);
            db.CommandReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = action, Fingerprint = fingerprint, ResourceId = draft.Id, CommittedAt = draft.SavedAt });
            await db.SaveChangesAsync(h.RequestAborted); await transaction.CommitAsync(h.RequestAborted);
            return Results.Ok(Dto(actor, draft, revision));
        } catch (DbUpdateConcurrencyException) {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Replay() ?? await Fail(409, "version_conflict");
        } catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Replay() ?? await Fail(409, "version_conflict");
        } catch (Exception e) when (e is DbUpdateException or NpgsqlException) {
            await transaction.RollbackAsync(CancellationToken.None); return await Fail(503, "service_unavailable");
        }
    }
}
