using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegacyCrewImportCommand(uint BaseVersion, string Reason);

// Explicit operator action; target discovery is supplied by controlled inventory, never a private count/list.
public static class LegacyCrewImportEndpoints
{
    public static void MapLegacyCrewImports(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/imports/{batch:guid}/legacy-protection").RequireAuthorization("DirectoryRead");
        group.MapGet("", Status).WithName("LegacyCrewImportProtection");
        group.MapPost("", Convert).WithName("LegacyCrewImportConvert");
    }
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ImportSave", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static async Task<IResult?> Gate(Guid vessel, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store"; h.Response.Headers.Pragma = "no-cache";
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        var identity = h.RequestServices.GetRequiredService<CrewIdentityPolicy>();
        var profile = h.RequestServices.GetRequiredService<CrewProfilePolicy>();
        // Unknown historical bindings may contain ANY private field. Require every current field rule.
        if (!a.CanRead || !a.HasWriteAttribution || !a.Permissions.Contains("import.legacy.convert") ||
            !new[] { "import.read", "import.write", "crew.read", "crew.write", "crew.identity.convert" }.All(a.Permissions.Contains) ||
            !identity.Allows(a, true) || !profile.Valid || profile.ImportPurpose.Length is < 1 or > 80 ||
            Enum.GetValues<CrewPersonalField>().Any(f => profile.Rule(f, profile.ImportPurpose, a.Permissions, true) == null))
            return await Fail(h, 403, "field_policy_denied");
        if (!(a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit)) ||
            !await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted))
            return await Fail(h, 404, "resource_not_found");
        if (!h.RequestServices.GetRequiredService<CrewProfileProtection>().Available)
            return await Fail(h, 503, "protected_storage_unavailable");
        return null;
    }
    private static Task<ImportBatch?> Target(Guid vessel, Guid batch, PoseidonDbContext db, CancellationToken ct) =>
        db.ImportBatches.SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == batch && x.Category == "crew", ct);
    private static object Dto(ImportBatch b, bool replayed = false) => new {
        b.Id, b.Version, storageState = b.LegacyProtectedAt == null ? "LegacyUnavailable" : "ProtectedHistoricalV1",
        interpretationState = "Unavailable", originalCaptureAuthority = "Unknown", b.LegacyProtectedAt, replayed
    };
    private static bool Current(ImportBatch b, CrewProfilePolicy p, CrewIdentityPolicy i) => b.LegacyProtectionPolicyVersion == p.Version &&
        b.LegacyProtectionIdentityVersion == i.Version && b.LegacyProtectionPurpose == p.ImportPurpose;
    private static async Task<IResult> Status(Guid vessel, Guid batch, HttpContext h, PoseidonDbContext db) {
        var denied = await Gate(vessel, h, db); if (denied != null) return denied;
        var b = await Target(vessel, batch, db, h.RequestAborted);
        if (b == null || !LegacyCrewImport.Convertible(b.Category, b.TableJson, b.LegacyProtectedAt != null) && b.LegacyProtectedAt == null)
            return await Fail(h, 404, "resource_not_found");
        if (b.LegacyProtectedAt != null) {
            if (!Current(b, h.RequestServices.GetRequiredService<CrewProfilePolicy>(), h.RequestServices.GetRequiredService<CrewIdentityPolicy>()))
                return await Fail(h, 503, "protected_storage_unavailable");
            try { _ = h.RequestServices.GetRequiredService<CrewProfileProtection>().OpenHistoricalImport(b); }
            catch (CryptographicException) { return await Fail(h, 503, "protected_storage_unavailable"); }
        }
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ImportRead", "Success", resourceId: vessel);
        return Results.Ok(Dto(b));
    }
    private static async Task<IResult> Convert(Guid vessel, Guid batch, LegacyCrewImportCommand command, HttpContext h, PoseidonDbContext db) {
        var denied = await Gate(vessel, h, db); if (denied != null) return denied;
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        var key = h.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParseExact(key, "D", out var attempt) || attempt == Guid.Empty || !LegacyCrewImport.ValidRequest(command.BaseVersion, command.Reason))
            return await Fail(h, 400, "invalid_request");
        var profile = h.RequestServices.GetRequiredService<CrewProfilePolicy>(); var identity = h.RequestServices.GetRequiredService<CrewIdentityPolicy>();
        var protection = h.RequestServices.GetRequiredService<CrewProfileProtection>();
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!; var ct = h.RequestAborted;
        // Only safe command/context metadata is hashed. No source/private contents or value hashes in receipts/logs.
        var fingerprint = System.Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            vessel, batch, command, profile.Version, identityVersion = identity.Version, profile.ImportPurpose
        })));
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var b = await Target(vessel, batch, db, ct); if (b == null) return await Fail(h, 404, "resource_not_found");
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != "CrewImportLegacyConvert" || receipt.ResourceId != batch || receipt.Fingerprint != fingerprint)
                    return await Fail(h, 409, "idempotency_key_reused");
                if (!Current(b, profile, identity)) return await Fail(h, 503, "protected_storage_unavailable");
                _ = protection.OpenHistoricalImport(b); // Missing/removed historic keys deny even an exact retry.
                return Results.Ok(Dto(b, true));
            }
            if (b.Version != command.BaseVersion || !LegacyCrewImport.Convertible(b.Category, b.TableJson, b.LegacyProtectedAt != null))
                return await Fail(h, 409, "version_conflict");
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ImportSave", "Attempted", resourceId: vessel);
            var source = new HistoricalCrewImportSource(b.TableJson, b.BindingsJson, b.SourceHash, b.SourceName, b.SourceLocator, b.CrewIdentityPolicyVersion);
            b.LegacyProtectionPolicyVersion = profile.Version; b.LegacyProtectionIdentityVersion = identity.Version; b.LegacyProtectionPurpose = profile.ImportPurpose;
            b.TableJson = protection.SealHistoricalImport(b, source);
            if (protection.OpenHistoricalImport(b) != source) throw new CryptographicException("protected_storage_unavailable");
            b.BindingsJson = "[]"; b.SourceHash = ""; b.SourceName = "name-withheld.xlsx"; b.SourceLocator = "protected-history";
            b.LegacyProtectedAt = DateTimeOffset.UtcNow;
            // State/revision/template/capture policy/preview/committed facts remain exactly as captured.
            await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = "CrewImportLegacyConvert",
                Fingerprint = fingerprint, ResourceId = batch, RegisterVersion = b.Version, RegisterRevision = b.Revision, CommittedAt = b.LegacyProtectedAt.Value });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(Dto(b));
        } catch (CryptographicException) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, 503, "protected_storage_unavailable");
        } catch (Exception e) when (e is DbUpdateException || PostgresFailure.IsSerialization(e)) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, 409, "version_conflict");
        }
    }
}
