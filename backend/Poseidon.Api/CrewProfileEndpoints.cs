using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;
namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewProfileCommand(uint? BaseVersion, string PolicyVersion, string Purpose, CrewPersonalField[] Fields, CrewPersonalData Data)
{
    public override string ToString() => nameof(CrewProfileCommand);
}
public sealed record CrewProfileOption(CrewPersonalField Field, string[] Values);
public sealed record CrewPersonalProfileDto(uint? Version, long Revision, string PolicyVersion, string Purpose, CrewPersonalData Data,
    CrewPersonalField[] Readable, CrewPersonalField[] Writable, string StorageState, CrewProfileOption[] Options);
public static class CrewProfileEndpoints
{
    public static void MapCrewPersonalProfiles(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/personal-profile").RequireAuthorization("DirectoryRead");
        group.MapGet("", Read).WithName("CrewPersonalProfile").Produces<CrewPersonalProfileDto>();
        group.MapGet("/history", History).WithName("CrewPersonalProfileHistory");
        group.MapPut("", Save).WithName("CrewPersonalProfileSave").Produces<CrewSavedDto>();
    }
    internal static bool Permitted(TrustedActor a, Guid vessel, bool write) => a.CanRead && a.Permissions.Contains("crew.read") &&
        a.Permissions.Contains("crew.identity.read") && (!write || a.HasWriteAttribution && a.CanEditCrew(vessel) && a.Permissions.Contains("crew.identity.write"));
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfile", code);
        return Results.Json(new { code }, statusCode: status);
    }
    internal static async Task<CrewAssignment?> Owner(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db, bool write) {
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        if (!Permitted(a, vessel, write) || !await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted)) return null;
        return await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == assignment, h.RequestAborted);
    }
    private static async Task<IResult> Read(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db, CrewProfilePolicy policy, CrewProfileProtection protection) {
        h.Response.Headers.CacheControl = "no-store";
        var owner = await Owner(vessel, assignment, h, db, false); if (owner == null) return await Fail(h, 404, "resource_not_found");
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        if (h.Request.Query.Keys.Any(k => k != "purpose") || h.Request.Query["purpose"].Count != 1) return await Fail(h, 400, "invalid_request");
        var purpose = h.Request.Query["purpose"].ToString();
        var readable = Enum.GetValues<CrewPersonalField>().Where(f => policy.Rule(f, purpose, a.Permissions, false) != null).ToArray();
        var writable = readable.Where(f => Permitted(a, vessel, true) && policy.Rule(f, purpose, a.Permissions, true) != null).ToArray();
        if (readable.Length == 0) return await Fail(h, 403, "field_policy_denied");
        if (!protection.Available) return await Fail(h, 503, "protected_storage_unavailable");
        var stored = await db.CrewPersonalProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == owner.PersonId, h.RequestAborted);
        var data = new CrewPersonalData();
        try { if (stored != null) foreach (var field in readable) data = data.With(field, protection.Decrypt(db.TenantId, owner.PersonId, field, stored.Ciphertext(field))); }
        catch (CryptographicException) { return await Fail(h, 503, "protected_storage_unavailable"); }
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfileRead", "Success", resourceId: owner.PersonId);
        return Results.Ok(new CrewPersonalProfileDto(stored?.Version, stored?.Revision ?? 0, policy.Version, purpose, data, readable, writable, "Protected", readable.Where(CrewProfilePolicy.Controlled).Select(f => new CrewProfileOption(f, policy.Fields.Single(x => x.Field == f).ControlledValues!)).ToArray()));
    }
    private static async Task<IResult> History(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db, CrewProfilePolicy policy) {
        var owner = await Owner(vessel, assignment, h, db, false); if (owner == null) return await Fail(h, 404, "resource_not_found");
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        if (!a.Permissions.Contains("crew.history.read") || !policy.Fields.Any(f => f.Field is { } field && policy.Rule(field, policy.ImportPurpose, a.Permissions, false) != null)) return await Fail(h, 403, "permission_denied");
        var rows = await db.CrewPersonalProfileRevisions.AsNoTracking().Where(x => x.PersonId == owner.PersonId)
            .OrderByDescending(x => x.Revision).Take(100).Select(x => new { x.Revision, x.PolicyVersion, x.Provenance, x.SavedAt }).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfileHistory", "Success", resourceId: owner.PersonId);
        return Results.Ok(new { items = rows, privateSnapshots = "Withheld" });
    }
    private static Task<IResult> Save(Guid vessel, Guid assignment, CrewProfileCommand command, HttpContext h, PoseidonDbContext db) => Apply(vessel, assignment, command, h, db, false);
    internal static bool Valid(CrewProfileCommand? command, TrustedActor a, CrewProfilePolicy policy) => command?.Data != null && command.Fields is { Length: > 0 and <= 16 } &&
        command.Fields.Distinct().Count() == command.Fields.Length && command.PolicyVersion == policy.Version &&
        command.Fields.All(f => Enum.IsDefined(f) && policy.Rule(f, command.Purpose, a.Permissions, true) != null && policy.ValueAllowed(f, command.Data.Lexical(f))) &&
        Enum.GetValues<CrewPersonalField>().All(f => command.Fields.Contains(f) || command.Data.Lexical(f) == null);
    internal static async Task<IResult> Apply(Guid vessel, Guid assignment, CrewProfileCommand command, HttpContext h, PoseidonDbContext db, bool imported) {
        var owner = await Owner(vessel, assignment, h, db, true); if (owner == null) return await Fail(h, 404, "resource_not_found");
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        var policy = h.RequestServices.GetRequiredService<CrewProfilePolicy>(); var protection = h.RequestServices.GetRequiredService<CrewProfileProtection>();
        // Current field/purpose permissions and key availability precede historic receipt lookup.
        if (!Valid(command, a, policy)) return await Fail(h, 403, "field_policy_denied");
        if (!protection.Available) return await Fail(h, 503, "protected_storage_unavailable");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        var key = h.Request.Headers["Idempotency-Key"].ToString(); if (!Guid.TryParseExact(key, "D", out _)) return await Fail(h, 400, "invalid_idempotency_key");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, assignment, command })));
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, h.RequestAborted) : null;
        try {
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, h.RequestAborted);
            if (receipt != null) {
                if (receipt.Action != "CrewProfileSave" || receipt.ResourceId != owner.PersonId || receipt.Fingerprint != fingerprint) return await Fail(h, 409, "idempotency_key_reused");
                return Results.Ok(new CrewSavedDto(owner.PersonId, receipt.RegisterRevision ?? 0, receipt.RegisterVersion ?? 0, true));
            }
            var row = await db.CrewPersonalProfiles.SingleOrDefaultAsync(x => x.PersonId == owner.PersonId, h.RequestAborted);
            if (row == null && command.BaseVersion != null || row != null && row.Version != command.BaseVersion) return await Fail(h, 409, "version_conflict");
            if (row == null) { row = new() { TenantId = db.TenantId, PersonId = owner.PersonId }; db.CrewPersonalProfiles.Add(row); }
            else row.Revision = checked(row.Revision + 1);
            foreach (var field in command.Fields) row.Capture(field, protection.Encrypt(db.TenantId, owner.PersonId, field, command.Data.Lexical(field)));
            db.CrewPersonalProfileRevisions.Add(new() { TenantId = db.TenantId, PersonId = owner.PersonId, Revision = row.Revision,
                PolicyVersion = policy.Version, Provenance = imported ? "ImportedDeclaredUnverified" : "UserDeclaredUnverified", SavedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(h.RequestAborted);
            db.CommandReceipts.Add(new() { TenantId = db.TenantId, ActorId = a.Id, Key = key, Action = "CrewProfileSave", Fingerprint = fingerprint,
                ResourceId = owner.PersonId, RegisterRevision = row.Revision, RegisterVersion = row.Version, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(h.RequestAborted); if (tx != null) await tx.CommitAsync(h.RequestAborted);
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfileSave", "Success", resourceId: owner.PersonId);
            return Results.Ok(new CrewSavedDto(owner.PersonId, row.Revision, row.Version, false));
        } catch (CryptographicException) { if (tx != null) await tx.RollbackAsync(CancellationToken.None); return await Fail(h, 503, "protected_storage_unavailable"); }
        catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) {
            if (tx != null) await tx.RollbackAsync(CancellationToken.None); return await Fail(h, 409, "version_conflict");
        }
    }
}
