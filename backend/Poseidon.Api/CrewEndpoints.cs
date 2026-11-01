using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewSaveCommand(Guid Id, Guid? PersonId, uint? BaseVersion, string? FullName,
    string? EmployeeNo, Guid? PositionId, DateOnly? From, DateOnly? To, DateOnly? ContractEndDate,
    string? WatchGroup, DateTimeOffset? SourceObservedAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewPositionCommand(Guid Id, string Code, string? Rank, string? Capacity, string? Department);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewPersonCommand(uint BaseVersion, string? FullName, string? EmployeeNo);
public sealed record CrewSavedDto(Guid Id, long Revision, uint Version, bool Replayed);
public sealed record CrewRow(Guid Id, Guid VesselId, Guid PersonId, uint Version, long Revision,
    Guid? PositionId, string? Rank, string? Capacity, string? Department, DateOnly? From, DateOnly? To,
    DateOnly? ContractEndDate, string? WatchGroup, string AssignmentStatus, string Provenance,
    string? FullName, string? EmployeeNo, string PrivacyScope, DateTimeOffset? SourceObservedAt,
    string EligibilityState = "Unknown", string WorkRestState = "Unknown", string CertificateState = "Unknown",
    string CompetenceState = "Unknown", string ContactStatus = "Unavailable");
public sealed record CrewPage(CrewRow[] Items, int Offset, bool HasMore, bool CanWrite, bool CanWriteIdentity);
public sealed record CrewProfile(CrewRow Assignment, string EmploymentState, string FitnessState,
    string IdentityStatus, string ContactStatus, string HealthStatus, uint? PersonVersion);

public static class CrewEndpoints
{
    public static void MapCrew(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew").RequireAuthorization("DirectoryRead");
        group.MapGet("", List).WithName("CrewList").Produces<CrewPage>();
        group.MapGet("/{id:guid}", Profile).WithName("CrewProfile").Produces<CrewProfile>();
        group.MapGet("/{id:guid}/history", History).WithName("CrewHistory");
        group.MapPost("", Save).WithName("CrewSave").Produces<CrewSavedDto>();
        group.MapPut("/{id:guid}/person", SavePerson).WithName("CrewPersonSave");
        group.MapGet("/positions", Positions).WithName("CrewPositions");
        group.MapPost("/positions", AddPosition).WithName("CrewPositionSave");
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Read(TrustedActor a) => a.CanRead && a.Permissions.Contains("crew.read");
    private static bool Write(TrustedActor a, Guid vessel) => Read(a) && a.HasWriteAttribution && a.Permissions.Contains("crew.write") &&
        (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit));
    private static bool Identity(TrustedActor a) => a.Permissions.Contains("crew.identity.read");
    private static bool IdentityWrite(TrustedActor a) => Identity(a) && a.Permissions.Contains("crew.identity.write");
    private static async Task<bool> Scope(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) =>
        Read(a) && await a.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, ct);
    private static async Task<IResult> Fail(HttpContext h, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, h.Request.Method is "POST" or "PUT" ? "CrewSave" : "CrewRead", code);
        return Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status);
    }
    private static async Task Observe(HttpContext h, string action, Guid vessel) =>
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, "Success", resourceId: vessel);

    // Scope before joining or projecting personal data. There is no PII search/export endpoint.
    private static IQueryable<CrewRow> Rows(PoseidonDbContext db, Guid vessel, bool identity, Guid? id = null, int offset = 0, int limit = 26) =>
        from c in db.CrewAssignments.AsNoTracking().Where(x => x.VesselId == vessel && (id == null || x.Id == id))
            .OrderBy(x => x.Id).Skip(offset).Take(limit)
        join p in db.Persons.AsNoTracking() on new { c.TenantId, Id = c.PersonId } equals new { p.TenantId, p.Id }
        join pos in db.CrewPositions.AsNoTracking().Where(x => x.VesselId == vessel)
            on new { c.TenantId, Id = c.PositionId } equals new { pos.TenantId, Id = (Guid?)pos.Id } into positions
        from pos in positions.DefaultIfEmpty()
        orderby c.Id
        select new CrewRow(c.Id, c.VesselId, c.PersonId, c.Version, c.Revision, c.PositionId,
            pos == null ? null : pos.Rank, pos == null ? null : pos.Capacity, pos == null ? null : pos.Department,
            c.From, c.To, c.ContractEndDate, c.WatchGroup, c.Status, c.Provenance,
            identity ? p.FullName : null, identity ? p.EmployeeNo : null, identity ? "IdentityPermitted" : "Withheld",
            c.SourceObservedAt, "Unknown", "Unknown", "Unknown", "Unknown", "Unavailable");

    private static async Task<IResult> List(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        if (h.Request.Query.Keys.Any(k => k != "offset") || h.Request.Query["offset"].Count > 1 ||
            h.Request.Query.ContainsKey("offset") && !int.TryParse(h.Request.Query["offset"], out _)) return await Fail(h, 400, "invalid_request");
        var offset = h.Request.Query.ContainsKey("offset") ? int.Parse(h.Request.Query["offset"]!) : 0;
        if (offset < 0 || offset > 100000) return await Fail(h, 400, "invalid_request");
        var rows = await Rows(db, vessel, Identity(a), offset: offset).ToArrayAsync(ct);
        await Observe(h, "CrewRead", vessel);
        return Results.Ok(new CrewPage(rows.Take(25).ToArray(), offset, rows.Length > 25, Write(a, vessel), IdentityWrite(a)));
    }
    private static async Task<IResult> Profile(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h);
        if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var row = await Rows(db, vessel, Identity(a), id, limit: 1).SingleOrDefaultAsync(h.RequestAborted);
        if (row == null) return await Fail(h, 404, "resource_not_found");
        var personVersion = IdentityWrite(a) ? await db.Persons.Where(x => x.Id == row.PersonId).Select(x => (uint?)x.Version).SingleAsync(h.RequestAborted) : null;
        await Observe(h, "CrewProfile", vessel);
        return Results.Ok(new CrewProfile(row, "Unknown", "Unknown", Identity(a) ? "Permitted" : "Withheld", "Withheld", "Withheld", personVersion));
    }
    private static async Task<IResult> SavePerson(Guid vessel, Guid id, CrewPersonCommand command, HttpContext h, PoseidonDbContext db, IAntiforgery csrf)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Write(a, vessel) || !IdentityWrite(a)) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        try { await csrf.ValidateRequestAsync(h); } catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        if (command.FullName?.Length > 200 || command.EmployeeNo?.Length > 64 || command.BaseVersion == 0) return await Fail(h, 400, "validation_failed");
        var key = h.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParseExact(key, "D", out _)) return await Fail(h, 400, "invalid_idempotency_key");
        var person = await (from c in db.CrewAssignments.Where(x => x.VesselId == vessel && x.Id == id)
            join p in db.Persons on new { c.TenantId, Id = c.PersonId } equals new { p.TenantId, p.Id } select p).SingleOrDefaultAsync(ct);
        if (person == null) return await Fail(h, 404, "resource_not_found");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, id, command })));
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewSave", "Attempted", resourceId: vessel);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
        if (receipt != null) {
            if (receipt.Action != "CrewPersonSave" || receipt.Fingerprint != fingerprint || receipt.ResourceId != person.Id) return await Fail(h, 409, "idempotency_key_reused");
            return Results.Ok(new { version = receipt.RegisterVersion, replayed = true });
        }
        if (person.Version != command.BaseVersion) return await Fail(h, 409, "version_conflict");
        person.FullName = command.FullName; person.EmployeeNo = command.EmployeeNo;
        // Even unchanged declarations observe the original xmin; no no-op race bypass.
        db.Entry(person).Property(x => x.FullName).IsModified = true;
        try {
            await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = "CrewPersonSave", Fingerprint = fingerprint,
                ResourceId = person.Id, RegisterVersion = person.Version, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { person.Version, replayed = false });
        } catch (DbUpdateException) { await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict"); }
    }
    private static async Task<IResult> History(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h);
        if (!Read(a) || !a.Permissions.Contains("crew.history.read")) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, h.RequestAborted) || !await db.CrewAssignments.AnyAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted))
            return await Fail(h, 404, "resource_not_found");
        var rows = await db.CrewAssignmentRevisions.AsNoTracking().Where(x => x.VesselId == vessel && x.AssignmentId == id)
            .OrderByDescending(x => x.Revision).Take(100).Select(x => new { x.Revision, x.From, x.To, x.PositionId, x.Provenance, x.SourceObservedAt, x.SavedAt }).ToArrayAsync(h.RequestAborted);
        await Observe(h, "CrewHistory", vessel); return Results.Ok(new { items = rows, historicalIdentity = "Unavailable" });
    }
    private static async Task<IResult> Positions(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h);
        if (!Read(a)) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var rows = await db.CrewPositions.AsNoTracking().Where(x => x.VesselId == vessel).OrderBy(x => x.Code).Take(200)
            .Select(x => new { x.Id, x.Code, x.Rank, x.Capacity, x.Department, x.Provenance }).ToArrayAsync(h.RequestAborted);
        await Observe(h, "CrewPositions", vessel); return Results.Ok(rows);
    }
    private static async Task<IResult> AddPosition(Guid vessel, CrewPositionCommand command, HttpContext h, PoseidonDbContext db, IAntiforgery csrf)
    {
        var a = Actor(h);
        if (!Write(a, vessel) || !a.Permissions.Contains("crew.position.write")) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        try { await csrf.ValidateRequestAsync(h); } catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        if (command.Id == Guid.Empty || string.IsNullOrWhiteSpace(command.Code) || command.Code.Length > 64 ||
            command.Rank?.Length > 100 || command.Capacity?.Length > 100 || command.Department?.Length > 100) return await Fail(h, 400, "validation_failed");
        // Client UUID makes retry safe; conflicting edits cannot overwrite the existing reference.
        var old = await db.CrewPositions.SingleOrDefaultAsync(x => x.Id == command.Id, h.RequestAborted);
        if (old != null) {
            if (old.VesselId != vessel || old.Code != command.Code || old.Rank != command.Rank || old.Capacity != command.Capacity || old.Department != command.Department)
                return await Fail(h, 409, "version_conflict");
            await Observe(h, "CrewPositions", vessel); return Results.Ok(new { old.Id });
        }
        db.CrewPositions.Add(new() { TenantId = a.TenantId, Id = command.Id, VesselId = vessel, Code = command.Code,
            Rank = command.Rank, Capacity = command.Capacity, Department = command.Department });
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewSave", "Attempted", resourceId: vessel);
        try { await db.SaveChangesAsync(h.RequestAborted); return Results.Ok(new { command.Id }); }
        catch (DbUpdateException) { return await Fail(h, 409, "version_conflict"); }
    }

    private static async Task<IResult> Save(Guid vessel, CrewSaveCommand command, HttpContext h, PoseidonDbContext db, IAntiforgery csrf)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Write(a, vessel)) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        try { await csrf.ValidateRequestAsync(h); } catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        if ((command.FullName != null || command.EmployeeNo != null) && !IdentityWrite(a)) return await Fail(h, 403, "permission_denied");
        if (command.Id == Guid.Empty || command.FullName?.Length > 200 || command.EmployeeNo?.Length > 64 || command.WatchGroup?.Length > 64 ||
            !CrewPeriod.Valid(command.From, command.To) || command.SourceObservedAt > DateTimeOffset.UtcNow)
            return await Fail(h, 400, "validation_failed");
        var key = h.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParseExact(key, "D", out _)) return await Fail(h, 400, "invalid_idempotency_key");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, command })));
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewSave", "Attempted", resourceId: vessel);
        // SERIALIZABLE plus DB exclusion constraint protects overlap and duplicate/restart races.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
        if (receipt != null) {
            if (receipt.Action != "CrewSave" || receipt.Fingerprint != fingerprint) return await Fail(h, 409, "idempotency_key_reused");
            // Replay returns opaque committed reference only, never a stale personal snapshot.
            if (!await db.CrewAssignments.AnyAsync(x => x.Id == receipt.ResourceId && x.VesselId == vessel, ct)) return await Fail(h, 404, "resource_not_found");
            return Results.Ok(new { id = receipt.ResourceId, revision = receipt.RegisterRevision, version = receipt.RegisterVersion, replayed = true });
        }
        var assignment = await db.CrewAssignments.SingleOrDefaultAsync(x => x.Id == command.Id && x.VesselId == vessel, ct);
        if (assignment == null && command.BaseVersion != null || assignment != null && command.BaseVersion != assignment.Version)
            return await Fail(h, 409, "version_conflict");
        if (assignment != null && (command.PersonId != assignment.PersonId || command.FullName != null || command.EmployeeNo != null))
            return await Fail(h, 400, "invalid_request");
        if (command.PositionId != null && !await db.CrewPositions.AnyAsync(x => x.Id == command.PositionId && x.VesselId == vessel, ct))
            return await Fail(h, 404, "resource_not_found");
        var personId = assignment?.PersonId ?? command.PersonId ?? Guid.NewGuid();
        if (assignment == null && command.PersonId != null && !await db.CrewAssignments.AnyAsync(x => x.PersonId == personId && x.VesselId == vessel, ct))
            return await Fail(h, 404, "resource_not_found");
        if (assignment == null && command.PersonId != null && (command.FullName != null || command.EmployeeNo != null)) return await Fail(h, 400, "invalid_request");
        // Do not expose other-vessel identifiers or periods in the conflict response.
        if (command.From != null && await db.CrewAssignments.AnyAsync(x => x.PersonId == personId && x.Id != command.Id && x.From != null &&
            (x.To == null || command.From < x.To) && (command.To == null || x.From < command.To), ct)) return await Fail(h, 409, "version_conflict");
        if (assignment == null) {
            if (command.PersonId == null) db.Persons.Add(new Person { TenantId = a.TenantId, Id = personId,
                FullName = command.FullName, EmployeeNo = command.EmployeeNo, SourceObservedAt = command.SourceObservedAt?.ToUniversalTime() });
            assignment = new() { TenantId = a.TenantId, Id = command.Id, VesselId = vessel, PersonId = personId };
            db.CrewAssignments.Add(assignment);
        } else assignment.Revision = checked(assignment.Revision + 1);
        assignment.PositionId = command.PositionId; assignment.From = command.From; assignment.To = command.To;
        assignment.ContractEndDate = command.ContractEndDate; assignment.WatchGroup = command.WatchGroup; assignment.SourceObservedAt = command.SourceObservedAt?.ToUniversalTime();
        db.CrewAssignmentRevisions.Add(assignment.Snapshot());
        try {
            await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = "CrewSave", Fingerprint = fingerprint,
                ResourceId = assignment.Id, RegisterRevision = assignment.Revision, RegisterVersion = assignment.Version, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(new { id = assignment.Id, assignment.Revision, assignment.Version, replayed = false });
        } catch (DbUpdateException) { await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict"); }
        catch (Npgsql.PostgresException e) when (e.SqlState == "40001") { await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict"); }
    }
}
