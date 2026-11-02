using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewPhotoCommand(uint? BaseVersion, Guid? FileId, string PolicyVersion, string Purpose)
{
    public override string ToString() => nameof(CrewPhotoCommand);
}
public sealed record CrewPhotoDto(Guid PersonId, uint? Version, long Revision, string PolicyVersion, string Purpose,
    bool CanWrite, string State, CrewFileVersionDto? Selected);

// FR46/TC46-crew-profile-photo_ref. Configured unverified selection, never identity verification.
public static class CrewPhotoEndpoints
{
    public static void MapCrewPhotos(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/photo").RequireAuthorization("DirectoryRead");
        group.MapGet("", Read).WithName("CrewPhotoRead").Produces<CrewPhotoDto>();
        group.MapPut("", Save).WithName("CrewPhotoSave").Produces<CrewSavedDto>();
        group.MapGet("/history", History).WithName("CrewPhotoHistory");
    }
    private static async Task<IResult> Fail(HttpContext h, int status, string result) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfile", result);
        return Results.Json(new { code = result }, statusCode: status);
    }
    private static async Task<CrewAssignment?> Owner(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db, bool write) {
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        var policy = CrewFilePolicy.Current(h);
        if (!policy.Allows(actor, write) || !policy.Kinds.Contains("Photo") || !actor.Permissions.Contains("file.read")) return null;
        return await CrewProfileEndpoints.Owner(vessel, assignment, h, db, write);
    }
    private static Task<FileStage?> Photo(Guid vessel, Guid person, Guid id, CrewFilePolicy policy, PoseidonDbContext db, CancellationToken ct) =>
        db.FileStages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel && x.PersonId == person &&
            x.PersonalKind == "Photo" && x.PersonalPolicyVersion == policy.Version && x.PersonalPurpose == policy.Purpose &&
            x.PersonalSourceVersion == policy.SourceVersion && x.State == "CleanTransport" &&
            db.StoredFiles.Any(f => f.Id == x.Id && f.VesselId == vessel), ct);
    private static CrewFileVersionDto Dto(FileStage f) => new(f.Id, f.PersonId!.Value, f.PersonalKind!, f.PersonalRevision!.Value,
        f.PersonalExpiresOn, f.PersonalPolicyVersion!, f.PersonalPurpose!, f.PersonalSourceVersion!, f.CreatedAt, f.State);
    private static async Task<IResult> Read(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var owner = await Owner(vessel, assignment, h, db, false); if (owner == null) return await Fail(h, 404, "resource_not_found");
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_request");
        var policy = CrewFilePolicy.Current(h); var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        var row = await db.CrewPersonalProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == owner.PersonId, h.RequestAborted);
        FileStage? selected = null;
        if (row?.PhotoFileId is { } id && row.PhotoVesselId == vessel && row.PhotoPolicyVersion == policy.Version && row.PhotoPurpose == policy.Purpose)
            selected = await Photo(vessel, owner.PersonId, id, policy, db, h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfileRead", "Success", resourceId: owner.PersonId);
        return Results.Ok(new CrewPhotoDto(owner.PersonId, row?.Version, row?.Revision ?? 0, policy.Version!, policy.Purpose!,
            policy.Allows(actor, true) && CrewProfileEndpoints.Permitted(actor, vessel, true),
            row?.PhotoFileId == null ? "Missing" : selected == null ? "Unavailable" : "SelectedUnverified", selected == null ? null : Dto(selected)));
    }
    private static async Task<IResult> History(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var owner = await Owner(vessel, assignment, h, db, false); if (owner == null) return await Fail(h, 404, "resource_not_found");
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        if (!actor.Permissions.Contains("crew.history.read")) return await Fail(h, 403, "permission_denied");
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_request");
        var policy = CrewFilePolicy.Current(h);
        // Current policy and exact file authorization precede projection and bounding, including old selections.
        var rows = await db.CrewPersonalProfileRevisions.AsNoTracking().Where(x => x.PersonId == owner.PersonId && x.IsPhotoSelection &&
            x.PolicyVersion == policy.Version && x.PhotoPurpose == policy.Purpose &&
            (x.PhotoFileId == null || x.PhotoVesselId == vessel && db.FileStages.Any(f => f.Id == x.PhotoFileId && f.VesselId == vessel &&
                f.PersonId == owner.PersonId && f.PersonalKind == "Photo" && f.PersonalPolicyVersion == policy.Version &&
                f.PersonalPurpose == policy.Purpose && f.PersonalSourceVersion == policy.SourceVersion && f.State == "CleanTransport" &&
                db.StoredFiles.Any(s => s.Id == f.Id && s.VesselId == vessel))))
            .OrderByDescending(x => x.Revision).Take(100).Select(x => new { x.Revision, x.PhotoFileId, x.SavedAt, x.Provenance }).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfileHistory", "Success", resourceId: owner.PersonId);
        return Results.Ok(new { items = rows });
    }
    private static async Task<IResult> Save(Guid vessel, Guid assignment, CrewPhotoCommand command, HttpContext h, PoseidonDbContext db) {
        var owner = await Owner(vessel, assignment, h, db, true); if (owner == null) return await Fail(h, 404, "resource_not_found");
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!; var policy = CrewFilePolicy.Current(h);
        if (command.PolicyVersion != policy.Version || command.Purpose != policy.Purpose) return await Fail(h, 403, "field_policy_denied");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        var key = h.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParseExact(key, "D", out _)) return await Fail(h, 400, "invalid_idempotency_key");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, assignment, command })));
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, h.RequestAborted);
        try {
            // Reauthorize the target before recovery: an old receipt never restores a withdrawn photo.
            if (command.FileId is { } id && await Photo(vessel, owner.PersonId, id, policy, db, h.RequestAborted) == null)
                return await Fail(h, 400, "invalid_request");
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.Key == key, h.RequestAborted);
            if (receipt != null) {
                if (receipt.Action != "CrewPhotoSave" || receipt.ResourceId != owner.PersonId || receipt.Fingerprint != fingerprint)
                    return await Fail(h, 409, "idempotency_key_reused");
                if (receipt.RegisterRevision == null || receipt.RegisterVersion == null) return await Fail(h, 503, "source_revision_unavailable");
                await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfileSave", "Success", resourceId: owner.PersonId);
                return Results.Ok(new CrewSavedDto(owner.PersonId, receipt.RegisterRevision.Value, receipt.RegisterVersion.Value, true));
            }
            var row = await db.CrewPersonalProfiles.SingleOrDefaultAsync(x => x.PersonId == owner.PersonId, h.RequestAborted);
            if (row == null && command.BaseVersion != null || row != null && row.Version != command.BaseVersion)
                return await Fail(h, 409, "version_conflict");
            if (row == null) { row = new() { TenantId = db.TenantId, PersonId = owner.PersonId }; db.CrewPersonalProfiles.Add(row); }
            else row.Revision = checked(row.Revision + 1);
            row.PhotoFileId = command.FileId; row.PhotoVesselId = command.FileId == null ? null : vessel;
            row.PhotoPolicyVersion = policy.Version; row.PhotoPurpose = policy.Purpose;
            db.CrewPersonalProfileRevisions.Add(new() { TenantId = db.TenantId, PersonId = owner.PersonId, Revision = row.Revision,
                PolicyVersion = policy.Version!, PhotoPurpose = policy.Purpose, IsPhotoSelection = true,
                PhotoFileId = command.FileId, PhotoVesselId = row.PhotoVesselId, Provenance = "UserDeclaredUnverified", SavedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(h.RequestAborted);
            db.CommandReceipts.Add(new() { TenantId = db.TenantId, ActorId = actor.Id, Key = key, Action = "CrewPhotoSave", Fingerprint = fingerprint,
                ResourceId = owner.PersonId, RegisterRevision = row.Revision, RegisterVersion = row.Version, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(h.RequestAborted); await tx.CommitAsync(h.RequestAborted);
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewProfileSave", "Success", resourceId: owner.PersonId);
            return Results.Ok(new CrewSavedDto(owner.PersonId, row.Revision, row.Version, false));
        } catch (Exception e) when (e is DbUpdateException || PostgresFailure.IsSerialization(e)) {
            await tx.RollbackAsync(CancellationToken.None); return await Fail(h, 409, "version_conflict");
        }
    }
}
