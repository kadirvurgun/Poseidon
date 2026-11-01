using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CredentialSaveCommand(Guid Id, uint? BaseVersion, CertificateMetadata Metadata);
public sealed record CredentialDto(Guid Id, uint Version, long Revision, CertificateMetadata Metadata,
    string VerificationState, string Provenance, string LookupState, DeclaredCredentialDateFacts Dates, DateOnly ReferenceDate);

public static class CredentialEndpoints
{
    public static void MapCredentials(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/certificates").RequireAuthorization("DirectoryRead");
        group.MapGet("", List).WithName("CredentialList").Produces<CredentialDto[]>();
        group.MapPost("", Save).WithName("CredentialSave").Produces<CrewSavedDto>();
    }
    internal static bool Permitted(TrustedActor a, Guid vessel, bool write) => a.CanRead &&
        a.Permissions.Contains("crew.read") && a.Permissions.Contains("crew.certificate.read") &&
        a.Permissions.Contains("crew.identity.read") && (!write || a.HasWriteAttribution &&
            a.Permissions.Contains("crew.certificate.write") && a.CanEditCrew(vessel));
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CredentialSave", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static async Task<IResult> List(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        if (!Permitted(a, vessel, false)) return await Fail(h, 403, "permission_denied");
        if (!await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted) ||
            !await db.CrewAssignments.AnyAsync(x => x.VesselId == vessel && x.Id == assignment, h.RequestAborted))
            return await Fail(h, 404, "resource_not_found");
        var rows = await db.Credentials.AsNoTracking().Where(x => x.VesselId == vessel && x.AssignmentId == assignment)
            .OrderBy(x => x.Id).Take(100).ToArrayAsync(h.RequestAborted);
        var visible = new List<Credential>();
        foreach (var row in rows)
            if (row.DocumentReference == null || await CrewFileEndpoints.AuthorizedDocument(vessel, assignment, row.DocumentReference.Value, h, db)) visible.Add(row);
        // Explicit UTC calendar observation, disclosed with the response; no legal cutoff policy.
        var reference = DateOnly.FromDateTime(DateTime.UtcNow);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CredentialRead", "Success", resourceId: vessel);
        return Results.Ok(visible.Select(x => new CredentialDto(x.Id, x.Version, x.Revision, x.Metadata(), x.VerificationState,
            x.Provenance, x.LegacyDocumentReference != null && x.DocumentReference == null ? "LegacyDocumentReferenceUnavailable" : "ControlledLookupAndSignatureRequired", CredentialDeclarationDates.Evaluate(x.Metadata().Declaration, reference), reference)).ToArray());
    }
    private static Task<IResult> Save(Guid vessel, Guid assignment, CredentialSaveCommand command, HttpContext h, PoseidonDbContext db) =>
        Apply(vessel, assignment, command, h, db, false);

    internal static async Task<IResult> Apply(Guid vessel, Guid assignment, CredentialSaveCommand command, HttpContext h, PoseidonDbContext db, bool imported) {
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!; var ct = h.RequestAborted;
        if (!Permitted(a, vessel, true)) return await Fail(h, 403, "permission_denied");
        if (!await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, ct)) return await Fail(h, 404, "resource_not_found");
        var owner = await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignment && x.VesselId == vessel, ct);
        if (owner == null) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        if (command.Id == Guid.Empty || !CertificateMetadataPolicy.Valid(command.Metadata)) return await Fail(h, 400, "validation_failed");
        if (command.Metadata.DocumentReference is { } document && !await CrewFileEndpoints.AuthorizedDocument(vessel, assignment, document, h, db))
            return await Fail(h, 400, "validation_failed");
        var key = h.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParseExact(key, "D", out _)) return await Fail(h, 400, "invalid_idempotency_key");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, assignment, command })));
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CredentialSave", "Attempted", resourceId: vessel);
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        try {
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != "CredentialSave" || receipt.Fingerprint != fingerprint) return await Fail(h, 409, "idempotency_key_reused");
                if (!await db.Credentials.AnyAsync(x => x.Id == receipt.ResourceId && x.VesselId == vessel && x.AssignmentId == assignment, ct))
                    return await Fail(h, 404, "resource_not_found");
                return Results.Ok(new CrewSavedDto(receipt.ResourceId, receipt.RegisterRevision ?? 0, receipt.RegisterVersion ?? 0, true));
            }
            var row = await db.Credentials.SingleOrDefaultAsync(x => x.Id == command.Id && x.VesselId == vessel && x.AssignmentId == assignment, ct);
            if (row == null && command.BaseVersion != null || row != null && (row.Version != command.BaseVersion || row.Status != "Draft" || row.VerificationState != "Unverified"))
                return await Fail(h, 409, "version_conflict");
            if (row == null) {
                row = new() { TenantId = a.TenantId, Id = command.Id, VesselId = vessel, AssignmentId = assignment, PersonId = owner.PersonId };
                db.Credentials.Add(row);
            } else row.Revision = checked(row.Revision + 1);
            row.Capture(command.Metadata); row.Provenance = imported ? "ImportedDeclaredUnverified" : "UserDeclaredUnverified";
            db.CredentialRevisions.Add(new() { TenantId = a.TenantId, CredentialId = row.Id, VesselId = vessel,
                Revision = row.Revision, SavedAt = DateTimeOffset.UtcNow, Provenance = row.Provenance });
            await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = "CredentialSave", Fingerprint = fingerprint,
                ResourceId = row.Id, RegisterRevision = row.Revision, RegisterVersion = row.Version, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct);
            return Results.Ok(new CrewSavedDto(row.Id, row.Revision, row.Version, false));
        } catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) {
            if (tx != null) await tx.RollbackAsync(CancellationToken.None);
            return await Fail(h, 409, "version_conflict");
        }
    }
    internal static bool CanEditCrew(this TrustedActor a, Guid vessel) => a.Permissions.Contains("crew.write") &&
        (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit));
}
