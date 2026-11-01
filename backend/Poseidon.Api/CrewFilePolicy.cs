using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Trusted operator configuration. A purpose binding is not lawful-basis or legal approval.
public sealed class CrewFilePolicy
{
    public string? Version { get; set; }
    public string? SourceVersion { get; set; }
    public string? Purpose { get; set; }
    public string[] Kinds { get; set; } = [];
    public bool Available => Token(Version) && Token(Purpose) && SourceVersion == "poseidon-v46.3-20261030" &&
        Kinds is { Length: > 0 and <= 4 } && Kinds.Distinct().Count() == Kinds.Length && Kinds.All(KnownKind);
    private static bool Token(string? value) => value is { Length: > 0 and <= 80 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_');
    public static bool KnownKind(string kind) => kind is "Photo" or "Passport" or "SeamanBook" or "CertificateDocument";
    public bool Allows(TrustedActor actor, bool write) => Available && actor.Permissions.Contains("crew.documents.read") &&
        (!write || actor.Permissions.Contains("crew.documents.write"));
    public static CrewFilePolicy Current(HttpContext h) => h.RequestServices.GetRequiredService<IConfiguration>()
        .GetSection("CrewFilePolicy").Get<CrewFilePolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new();
}

public sealed record CrewFileVersionDto(Guid Id, Guid PersonId, string Kind, long Revision, DateOnly? ExpiresOn,
    string PolicyVersion, string Purpose, string SourceVersion, DateTimeOffset CreatedAt, string State);

public static class CrewFileEndpoints
{
    internal static async Task<bool> AuthorizedDocument(Guid vessel, Guid assignment, Guid file, HttpContext h, PoseidonDbContext db)
    {
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        var policy = CrewFilePolicy.Current(h);
        if (!actor.Permissions.Contains("file.read") || !policy.Allows(actor, false) || !policy.Kinds.Contains("CertificateDocument")) return false;
        var owner = await CrewProfileEndpoints.Owner(vessel, assignment, h, db, false);
        if (owner == null) return false;
        return await db.FileStages.AnyAsync(x => x.Id == file && x.PersonId == owner.PersonId && x.VesselId == vessel &&
            x.PersonalKind == "CertificateDocument" && x.PersonalPolicyVersion == policy.Version && x.PersonalPurpose == policy.Purpose &&
            x.State == "CleanTransport" && db.StoredFiles.Any(f => f.Id == x.Id && f.VesselId == vessel), h.RequestAborted);
    }
    public static void MapCrewFiles(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/personal-files", async
            (Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db, ActorResolver actors, SecurityAudit audit) =>
        {
            h.Response.Headers.CacheControl = "no-store";
            var owner = await CrewProfileEndpoints.Owner(vessel, assignment, h, db, false);
            var actor = actors.Resolve(h.User)!; var policy = CrewFilePolicy.Current(h);
            if (owner == null || !actor.Permissions.Contains("file.read") || !policy.Allows(actor, false)) {
                await audit.WriteAsync(h, "FileRequest", "permission_denied");
                return Results.NotFound();
            }
            if (h.Request.Query.Count != 0) {
                await audit.WriteAsync(h, "FileRequest", "invalid_request"); return Results.BadRequest();
            }
            // Filter policy BEFORE projection/count/bounding. Old-policy files cannot leak metadata.
            var rows = await db.FileStages.AsNoTracking().Where(x => x.PersonId == owner.PersonId && x.VesselId == vessel &&
                x.PersonalPolicyVersion == policy.Version && x.PersonalPurpose == policy.Purpose && policy.Kinds.Contains(x.PersonalKind!))
                .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(101).ToArrayAsync(h.RequestAborted);
            await audit.WriteAsync(h, "FileList", "Success", resourceId: owner.PersonId);
            // A write-capable operator receives the next immutable sequence only for configured kinds.
            var next = new Dictionary<string, long>();
            if (actor.Permissions.Contains("file.write") && policy.Allows(actor, true) && CrewProfileEndpoints.Permitted(actor, vessel, true))
                foreach (var kind in policy.Kinds) next[kind] = 1 + (await db.FileStages.Where(x => x.PersonId == owner.PersonId && x.PersonalKind == kind)
                    .MaxAsync(x => x.PersonalRevision, h.RequestAborted) ?? 0);
            return Results.Ok(new { next, personId = owner.PersonId, policyVersion = policy.Version, purpose = policy.Purpose,
                kinds = policy.Kinds, canWrite = CrewProfileEndpoints.Permitted(actor, vessel, true) && policy.Allows(actor, true) && actor.Permissions.Contains("file.write"),
                items = rows.Take(100).Select(x => new CrewFileVersionDto(x.Id, x.PersonId!.Value, x.PersonalKind!, x.PersonalRevision!.Value,
                    x.PersonalExpiresOn, x.PersonalPolicyVersion!, x.PersonalPurpose!, x.PersonalSourceVersion!, x.CreatedAt, x.State)), hasMore = rows.Length > 100 });
        }).RequireAuthorization("DirectoryRead").WithName("CrewPersonalFileVersions");
    }
}
