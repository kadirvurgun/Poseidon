using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewIdentityConversionCommand(uint BaseVersion, string PolicyVersion, string Purpose);

public static class CrewIdentityEndpoints
{
    public static void MapCrewIdentity(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew/{id:guid}/person").RequireAuthorization("DirectoryRead");
        group.MapPost("/convert", ConvertLegacy).WithName("CrewIdentityConvert");
        group.MapGet("/history", History).WithName("CrewIdentityHistory");
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, h.Request.Method == "GET" ? "CrewHistory" : "CrewSave", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static async Task<bool> Scope(Guid vessel, HttpContext h, PoseidonDbContext db, bool write) {
        var a = Actor(h);
        return a.CanRead && a.Permissions.Contains("crew.read") && (!write || a.Permissions.Contains("crew.write") &&
            (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit))) &&
            await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted);
    }
    private static Task<Person?> Person(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db) =>
        (from c in db.CrewAssignments.Where(x => x.VesselId == vessel && x.Id == id)
         join p in db.Persons on new { c.TenantId, Id = c.PersonId } equals new { p.TenantId, p.Id }
         select p).SingleOrDefaultAsync(h.RequestAborted);

    internal static bool Valid(CrewPersonCommand c, CrewIdentityPolicy policy, TrustedActor actor) =>
        c.BaseVersion != 0 && policy.Allows(actor, true) && c.PolicyVersion == policy.Version && c.Purpose == policy.Purpose &&
        (c.FullNamePresent || c.EmployeeNoPresent) && CrewIdentityData.Valid(c.FullName, c.EmployeeNo) &&
        (c.FullNamePresent || c.FullName == null) && (c.EmployeeNoPresent || c.EmployeeNo == null);

    private static Task<IResult> ConvertLegacy(Guid vessel, Guid id, CrewIdentityConversionCommand c, HttpContext h, PoseidonDbContext db) =>
        Apply(vessel, id, new(c.BaseVersion, null, null, c.PolicyVersion, c.Purpose), h, db, true);

    internal static async Task<IResult> Apply(Guid vessel, Guid id, CrewPersonCommand command, HttpContext h, PoseidonDbContext db, bool conversion = false, bool imported = false) {
        var a = Actor(h); var ct = h.RequestAborted;
        var policy = h.RequestServices.GetRequiredService<CrewIdentityPolicy>();
        var protection = h.RequestServices.GetRequiredService<CrewProfileProtection>();
        // Current rights/purpose/version precede person lookup and receipt recovery.
        if (!Valid(command, policy, a) || conversion && !a.Permissions.Contains("crew.identity.convert")) return await Fail(h, 403, "field_policy_denied");
        if (!await Scope(vessel, h, db, true)) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        if (!protection.Available) return await Fail(h, 503, "protected_storage_unavailable");
        var key = h.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParseExact(key, "D", out _)) return await Fail(h, 400, "invalid_idempotency_key");
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var person = await Person(vessel, id, h, db);
        if (person == null) return await Fail(h, 404, "resource_not_found");
        var action = conversion ? "CrewIdentityConvert" : "CrewPersonSave";
        var fingerprint = System.Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, id, command, conversion, imported })));
        var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
        if (receipt != null) {
            if (receipt.Action != action || receipt.Fingerprint != fingerprint || receipt.ResourceId != person.Id) return await Fail(h, 409, "idempotency_key_reused");
            return Results.Ok(new { version = receipt.RegisterVersion, revision = receipt.RegisterRevision, replayed = true });
        }
        if (person.Version != command.BaseVersion) return await Fail(h, 409, "version_conflict");
        try {
            string? name; string? employee;
            if (conversion) {
                if (person.IdentityStorageState != "LegacyUnavailable") return await Fail(h, 409, "version_conflict");
                name = person.FullName; employee = person.EmployeeNo;
                if (!CrewIdentityData.Valid(name, employee)) return await Fail(h, 409, "protected_storage_unavailable");
            } else {
                // Never overwrite or coerce inaccessible historical identity on an ordinary edit.
                var current = CrewIdentityStorage.Open(person, protection);
                name = command.FullNamePresent ? command.FullName : current.Name;
                employee = command.EmployeeNoPresent ? command.EmployeeNo : current.Employee;
                if (!CrewIdentityData.EmployeeChangeAllowed(policy.EmployeeNumberMode, current.Employee, employee)) return await Fail(h, 403, "field_policy_denied");
            }
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewSave", "Attempted", resourceId: vessel);
            CrewIdentityStorage.Capture(person, name, employee, protection);
            db.CrewIdentityRevisions.Add(new() { TenantId = a.TenantId, PersonId = person.Id, Revision = person.IdentityRevision,
                PolicyVersion = policy.Version, Purpose = policy.Purpose, Operation = conversion ? "LegacyConversion" : imported ? "ImportedDeclaration" : "Declaration", SavedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = action, Fingerprint = fingerprint,
                ResourceId = person.Id, RegisterVersion = person.Version, RegisterRevision = person.IdentityRevision, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct);
            return Results.Ok(new { person.Version, revision = person.IdentityRevision, replayed = false });
        } catch (CryptographicException) { return await Fail(h, 503, "protected_storage_unavailable"); }
        catch (Exception e) when (e is DbUpdateException || PostgresFailure.IsSerialization(e)) {
            if (tx != null) await tx.RollbackAsync(CancellationToken.None);
            return await Fail(h, 409, "version_conflict");
        }
    }
    private static async Task<IResult> History(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db) {
        var a = Actor(h); var policy = h.RequestServices.GetRequiredService<CrewIdentityPolicy>();
        if (!policy.Allows(a, false) || !a.Permissions.Contains("crew.history.read")) return await Fail(h, 403, "field_policy_denied");
        if (!await Scope(vessel, h, db, false)) return await Fail(h, 404, "resource_not_found");
        var person = await Person(vessel, id, h, db); if (person == null) return await Fail(h, 404, "resource_not_found");
        var items = await db.CrewIdentityRevisions.AsNoTracking().Where(x => x.PersonId == person.Id).OrderByDescending(x => x.Revision)
            .Take(100).Select(x => new { x.Revision, x.PolicyVersion, x.Operation, x.SavedAt }).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewHistory", "Success", resourceId: vessel);
        return Results.Ok(new { items, historicalValues = "Unavailable", storageState = person.IdentityStorageState });
    }
}
