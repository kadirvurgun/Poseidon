using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StageFileCommand(string? Name, string? Mime, long Length, string? Checksum)
{
    public DateOnly? ExpiresOn { get; init; }
}
public sealed record FileDto(Guid Id, Guid VesselId, string Name, string State, long Length, string? Checksum, long? Revision, uint Version);
public sealed record FileListDto(FileDto[] Items, int Offset, bool HasMore, bool CanWrite, bool Available, long MaxBytes, string[] AllowedTypes);

public static class SecureFileEndpoints
{
    public static void MapSecureFiles(this IEndpointRouteBuilder app)
    {
        app.MapCrewFiles();
        var personal = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/personal-files/{personalKind}/versions/{personalRevision:long}").RequireAuthorization("DirectoryRead");
        personal.MapGet("", List).WithName("ListCrewFileVersion").Produces<FileListDto>();
        personal.MapPost("", Stage).WithName("StageCrewFileVersion").Produces<FileDto>();
        personal.MapGet("/{id:guid}", Detail).WithName("GetCrewFileVersion").Produces<FileDto>();
        personal.MapPut("/{id:guid}/bytes", Upload).WithName("UploadCrewFileVersion").Produces<FileDto>();
        personal.MapPost("/{id:guid}/access", Access).WithName("IssueCrewFileVersionAccess");
        personal.MapGet("/{id:guid}/bytes", Download).WithName("DownloadCrewFileVersion");
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/files").RequireAuthorization("DirectoryRead");
        var documents = app.MapGroup("/api/v1/vessels/{vessel:guid}/documents/{document:guid}/versions/{revision:long}/files").RequireAuthorization("DirectoryRead");
        documents.MapGet("", List).WithName("ListDocumentFiles").Produces<FileListDto>();
        documents.MapPost("", Stage).WithName("StageDocumentFile").Produces<FileDto>();
        documents.MapGet("/{id:guid}", Detail).WithName("GetDocumentFile").Produces<FileDto>();
        documents.MapPut("/{id:guid}/bytes", Upload).WithName("UploadDocumentFile").Produces<FileDto>();
        documents.MapPost("/{id:guid}/access", Access).WithName("IssueDocumentFileAccess");
        documents.MapGet("/{id:guid}/bytes", Download).WithName("DownloadDocumentFile");
        group.MapGet("", List).WithName("ListVesselFiles").Produces<FileListDto>();
        group.MapPost("", Stage).WithName("StageVesselFile").Accepts<StageFileCommand>("application/json").Produces<FileDto>();
        group.MapGet("/{id:guid}", Detail).WithName("GetVesselFile").Produces<FileDto>();
        group.MapPut("/{id:guid}/bytes", Upload).WithName("UploadVesselFile").Accepts<byte[]>("image/png", "image/jpeg", "application/pdf").Produces<FileDto>();
        group.MapPost("/{id:guid}/access", Access).WithName("IssueFileAccess");
        group.MapGet("/{id:guid}/bytes", Download).WithName("DownloadVesselFile");
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Read(TrustedActor a) => a.CanRead && a.Permissions.Contains("file.read");
    private static bool Personal(HttpContext h) => h.Request.RouteValues.ContainsKey("assignment");
    private static string PersonalKind(HttpContext h) => h.Request.RouteValues["personalKind"]?.ToString() ?? "";
    private static long PersonalRevision(HttpContext h) => long.TryParse(h.Request.RouteValues["personalRevision"]?.ToString(), out var r) ? r : 0;
    private static bool Write(TrustedActor a, Guid vessel, HttpContext h) => Read(a) && a.Permissions.Contains("file.write") &&
        (Personal(h) ? CrewProfileEndpoints.Permitted(a, vessel, true) && CrewFilePolicy.Current(h).Allows(a, true) : a.CanEditVessel(vessel));
    private static Guid? DocumentId(HttpContext h) => h.Request.RouteValues.TryGetValue("document", out var d) && Guid.TryParse(d?.ToString(), out var id) ? id : null;
    private static long? DocumentRevision(HttpContext h) => h.Request.RouteValues.TryGetValue("revision", out var r) && long.TryParse(r?.ToString(), out var revision) ? revision : null;
    private static IQueryable<FileStage> Owned(PoseidonDbContext db, HttpContext h) {
        var document = DocumentId(h); var revision = DocumentRevision(h);
        if (Personal(h)) {
            var assignment = Guid.Parse(h.Request.RouteValues["assignment"]!.ToString()!);
            var vessel = Guid.Parse(h.Request.RouteValues["vessel"]!.ToString()!);
            var owners = db.CrewAssignments.Where(x => x.Id == assignment && x.VesselId == vessel);
            var kind = PersonalKind(h); var personalRevision = PersonalRevision(h); var policy = CrewFilePolicy.Current(h);
            return db.FileStages.Where(x => owners.Any(a => a.PersonId == x.PersonId) && x.PersonalKind == kind &&
                x.PersonalRevision == personalRevision && x.PersonalPolicyVersion == policy.Version && x.PersonalPurpose == policy.Purpose);
        }
        return db.FileStages.Where(x => x.PersonId == null && x.DocumentId == document && x.DocumentRevision == revision);
    }
    private static IQueryable<StoredFile> OwnedFiles(PoseidonDbContext db, HttpContext h, Guid vessel) {
        var stages = Owned(db, h).Where(s => s.VesselId == vessel);
        return db.StoredFiles.Where(f => f.VesselId == vessel && stages.Any(s => s.Id == f.Id));
    }
    private static async Task<bool> Scope(PoseidonDbContext db, TrustedActor a, Guid vessel, HttpContext h, CancellationToken ct) {
        if (!Read(a) || !await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, ct)) return false;
        if (Personal(h)) {
            var policy = CrewFilePolicy.Current(h); var kind = PersonalKind(h);
            if (!policy.Allows(a, false) || !policy.Kinds.Contains(kind) || PersonalRevision(h) <= 0) return false;
            return await CrewProfileEndpoints.Owner(vessel, Guid.Parse(h.Request.RouteValues["assignment"]!.ToString()!), h, db, false) != null;
        }
        var document = DocumentId(h); var revision = DocumentRevision(h);
        return document == null || revision > 0 && await db.DocumentVersions.AnyAsync(x => x.DocumentId == document && x.Revision == revision && x.VesselId == vessel, ct);
    }
    private static FileDto Dto(FileStage s, StoredFile? f = null) => new(s.Id, s.VesselId, s.Name, s.State, f?.Length ?? s.ExpectedLength, f?.Checksum, f?.Revision, s.Version);
    private static async Task<IResult> Failure(HttpContext h, int status, string code, Guid? id = null)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "FileRequest", code, resourceId: id);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
    private static bool Ready(HttpContext h)
    {
        var settings = h.RequestServices.GetRequiredService<SecureFileSettings>();
        var environment = h.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!settings.Ready(environment)) return false;
        var root = Path.GetFullPath(settings.Root!);
        var labRoot = h.RequestServices.GetRequiredService<IConfiguration>()["DevelopmentLab:WebRoot"];
        return new[] { environment.ContentRootPath, environment.WebRootPath, labRoot }.Where(p => !string.IsNullOrEmpty(p)).All(p =>
            !root.Equals(Path.GetFullPath(p!), StringComparison.OrdinalIgnoreCase) &&
            !root.StartsWith(Path.GetFullPath(p!).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
    private static async Task<bool> Csrf(HttpContext h)
    {
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static async Task<IResult> List(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Failure(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, h, ct)) return await Failure(h, 404, "resource_not_found");
        if (h.Request.Query.Keys.Any(k => k != "offset") || h.Request.Query["offset"].Count > 1 ||
            h.Request.Query.ContainsKey("offset") && !int.TryParse(h.Request.Query["offset"], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _))
            return await Failure(h, 400, "invalid_request");
        var offset = h.Request.Query.ContainsKey("offset") ? int.Parse(h.Request.Query["offset"]!, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var rows = await Owned(db, h).AsNoTracking().Where(x => x.VesselId == vessel).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(26).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var files = await db.StoredFiles.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.Id)).ToArrayAsync(ct);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "FileList", "Success", resourceId: vessel);
        var settings = h.RequestServices.GetRequiredService<SecureFileSettings>();
        var types = Personal(h) && PersonalKind(h) == "Photo" ? settings.AllowedTypes.Where(t => t != "application/pdf").ToArray() : settings.AllowedTypes;
        return Results.Ok(new FileListDto(rows.Take(25).Select(s => Dto(s, files.SingleOrDefault(f => f.Id == s.Id))).ToArray(), offset, rows.Length > 25, Write(a, vessel, h), Ready(h), Ready(h) ? settings.MaxBytes : 0, types));
    }
    private static async Task<IResult> Detail(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h);
        if (!Read(a)) return await Failure(h, 403, "permission_denied", id);
        if (!await Scope(db, a, vessel, h, h.RequestAborted)) return await Failure(h, 404, "resource_not_found", id);
        var s = await Owned(db, h).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted);
        if (s == null) return await Failure(h, 404, "resource_not_found", id);
        var f = await OwnedFiles(db, h, vessel).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "FileDetail", "Success", resourceId: id);
        return Results.Ok(Dto(s, f));
    }
    private static async Task<IResult> Stage(Guid vessel, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Write(a, vessel, h)) return await Failure(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, h, ct)) return await Failure(h, 404, "resource_not_found");
        if (!await Csrf(h)) return await Failure(h, 403, "csrf_invalid");
        if (!Ready(h)) return await Failure(h, 503, "file_settings_unavailable");
        var key = h.Request.Headers["Idempotency-Key"].Count == 1 ? h.Request.Headers["Idempotency-Key"].ToString() : "";
        if (key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Failure(h, 400, "invalid_idempotency_key");
        StageFileCommand? command;
        try {
            if (!h.Request.HasJsonContentType()) return await Failure(h, 400, "invalid_request");
            using var buffer = new MemoryStream();
            await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 2048, ct);
            command = JsonSerializer.Deserialize<StageFileCommand>(buffer.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 4 });
        } catch (Exception e) when (e is JsonException or InvalidDataException) { return await Failure(h, 400, "invalid_request"); }
        var settings = h.RequestServices.GetRequiredService<SecureFileSettings>();
        if (command == null || command.Name is not { Length: > 0 and <= 120 } || command.Mime == null || !settings.AllowedTypes.Contains(command.Mime) || command.Length <= 0 || command.Length > settings.MaxBytes || !SecureFilePolicy.Hash(command.Checksum))
            return await Failure(h, 400, "invalid_file_metadata");
        if (!Personal(h) && command.ExpiresOn != null || Personal(h) && PersonalKind(h) == "Photo" &&
            (command.Mime == "application/pdf" || command.ExpiresOn != null)) return await Failure(h, 400, "invalid_file_metadata");
        // Private filenames are fixed transport labels, never original personal document names.
        if (Personal(h) && command.Name != (PersonalKind(h) == "Photo" ? "photo" : "document") +
            (command.Mime == "image/png" ? ".png" : command.Mime == "image/jpeg" ? ".jpg" : ".pdf"))
            return await Failure(h, 400, "invalid_file_metadata");
        var personId = Personal(h) ? (await CrewProfileEndpoints.Owner(vessel, Guid.Parse(h.Request.RouteValues["assignment"]!.ToString()!), h, db, true))?.PersonId : null;
        if (Personal(h) && personId == null) return await Failure(h, 404, "resource_not_found");
        var personalPolicy = Personal(h) ? CrewFilePolicy.Current(h) : null;
        // Preserve historical immutable PNG intent fingerprints and their original replay contract.
        var content = Personal(h) ? JsonSerializer.Serialize(new { vessel, PersonId = personId, Kind = PersonalKind(h), Revision = PersonalRevision(h),
            command.Name, command.Mime, command.Length, Checksum = command.Checksum!.ToUpperInvariant(), command.ExpiresOn, personalPolicy!.Version, personalPolicy.Purpose }) :
            DocumentId(h) == null && command.Mime == "image/png"
            ? JsonSerializer.Serialize(new { vessel, command.Name, command.Length, Checksum = command.Checksum!.ToUpperInvariant() })
            : JsonSerializer.Serialize(new { vessel, DocumentId = DocumentId(h), DocumentRevision = DocumentRevision(h), command.Name, command.Mime, command.Length, Checksum = command.Checksum!.ToUpperInvariant() });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        var existing = await db.FileStages.SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
        if (existing != null) return existing.PersonId == personId && (!Personal(h) || existing.PersonalKind == PersonalKind(h) && existing.PersonalRevision == PersonalRevision(h)) && existing.VesselId == vessel && existing.DocumentId == DocumentId(h) && existing.DocumentRevision == DocumentRevision(h) && existing.Fingerprint == fingerprint ? Results.Ok(Dto(existing, await db.StoredFiles.SingleOrDefaultAsync(x => x.Id == existing.Id, ct))) : await Failure(h, 409, "idempotency_key_reused");
        if (!DocumentBinaryPolicy.ValidName(command.Name, command.Mime)) return await Failure(h, 400, "invalid_file_metadata");
        // Versions are sequential, including pending/quarantined declarations. Never overwrite old intent.
        if (Personal(h)) {
            var personalKind = PersonalKind(h);
            var last = await db.FileStages.Where(x => x.PersonId == personId && x.PersonalKind == personalKind).MaxAsync(x => x.PersonalRevision, ct) ?? 0;
            if (PersonalRevision(h) != last + 1) return await Failure(h, 409, "file_attempt_conflict");
        }
        var stage = new FileStage { PersonId = personId, PersonalKind = Personal(h) ? PersonalKind(h) : null,
            PersonalRevision = Personal(h) ? PersonalRevision(h) : null, PersonalExpiresOn = command.ExpiresOn,
            PersonalPolicyVersion = personalPolicy?.Version, PersonalPurpose = personalPolicy?.Purpose, PersonalSourceVersion = personalPolicy?.SourceVersion, TenantId = a.TenantId, Id = Guid.NewGuid(), DocumentId = DocumentId(h), DocumentRevision = DocumentRevision(h), VesselId = vessel, ActorId = a.Id, Key = key, Fingerprint = fingerprint,
            StorageReference = settings.Adapter == "S3" ? settings.S3.ContextHash() : null,
            Name = command.Name!, Mime = command.Mime!, ExpectedLength = command.Length, ExpectedChecksum = command.Checksum!.ToUpperInvariant(), CreatedAt = DateTimeOffset.UtcNow };
        db.FileStages.Add(stage);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return await Failure(h, 409, "file_attempt_conflict"); }
        return Results.Ok(Dto(stage));
    }
    private static async Task<IResult> Upload(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Write(a, vessel, h)) return await Failure(h, 403, "permission_denied", id);
        if (!await Scope(db, a, vessel, h, ct)) return await Failure(h, 404, "resource_not_found", id);
        if (!await Csrf(h)) return await Failure(h, 403, "csrf_invalid", id);
        if (!Ready(h)) return await Failure(h, 503, "file_settings_unavailable", id);
        var stage = await Owned(db, h).SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel && x.ActorId == a.Id, ct);
        if (stage == null) return await Failure(h, 404, "resource_not_found", id);
        if (stage.State == "CleanTransport") return Results.Ok(Dto(stage, await db.StoredFiles.SingleAsync(x => x.Id == id, ct)));
        if (stage.State != "Pending") return await Failure(h, 409, "file_attempt_frozen", id);
        if (h.Request.ContentType != stage.Mime || h.Request.ContentLength.HasValue && h.Request.ContentLength != stage.ExpectedLength)
            return await Failure(h, 400, "file_type_or_size_invalid", id);
        stage.State = "Receiving";
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return await Failure(h, 409, "file_attempt_conflict", id); }
        var objects = h.RequestServices.GetRequiredService<FileObjects>();
        var settings = h.RequestServices.GetRequiredService<SecureFileSettings>();
        if (stage.StorageReference != (settings.Adapter == "S3" ? settings.S3.ContextHash() : null)) return await Failure(h, 503, "file_settings_unavailable", id);
        try {
            await using (var stream = objects.Create(a.TenantId, id)) {
                var observed = await SecureFilePolicy.CopyAsync(h.Request.Body, stream, Math.Min(settings.MaxBytes, stage.ExpectedLength), ct);
                await stream.FlushAsync(ct);
                if (observed.Length != stage.ExpectedLength || observed.Checksum != stage.ExpectedChecksum) throw new InvalidDataException("file_checksum_invalid");
                stream.Position = 0; await DocumentBinaryPolicy.ValidateAsync(stream, stage.Mime, settings.MaxPixels, ct);
                stream.Position = 0; await h.RequestServices.GetRequiredService<ClamdScanner>().ScanAsync(stream, ct);
                // Same exclusive immutable handle for validation and scan. No caller-provided clean assertion.
            }
            var provider = await objects.CommitAsync(a.TenantId, id, stage.ExpectedChecksum, stage.ExpectedLength, stage.Mime, ct);
            var current = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            if (current == null || current.Id != a.Id || current.TenantId != a.TenantId || !Write(current, vessel, h) || !await Scope(db, current, vessel, h, ct))
                return await Failure(h, 403, "permission_denied", id);
            // Bytes exist before committing accepted metadata; a failed commit leaves an orphan, never a downloadable row.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            stage.State = "CleanTransport";
            var file = new StoredFile { TenantId = a.TenantId, Id = id, VesselId = vessel, Name = stage.Name, Mime = stage.Mime, ProviderVersion = provider.Version, ProviderETag = provider.ETag,
                ProviderReference = settings.Adapter == "S3" ? settings.S3.ContextHash() : null,
                Checksum = stage.ExpectedChecksum, Length = stage.ExpectedLength, RetentionReference = settings.RetentionReference!, ScannerReference = settings.ScannerReference!, ScannedAt = DateTimeOffset.UtcNow };
            db.StoredFiles.Add(file); await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = stage.Key, Action = "FileFinalize", Fingerprint = stage.Fingerprint, ResourceId = id, RegisterRevision = 1, RegisterVersion = file.Version, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Results.Ok(Dto(stage, file));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Net.Sockets.SocketException or OperationCanceledException or DbUpdateException)
        {
            // A commit acknowledgement can be ambiguous. Never delete bytes here: commit may have succeeded.
            // Receiving/unknown attempts remain quarantined; explicit reconciliation owns orphan cleanup.
            // The connection/context may be unusable; do not invent a rejected/successful business event.
            return await Failure(h, 503, "file_quarantined_or_unknown", id);
        }
    }
    private static async Task<IResult> Access(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h);
        if (!Read(a)) return await Failure(h, 403, "permission_denied", id);
        if (!await Scope(db, a, vessel, h, h.RequestAborted)) return await Failure(h, 404, "resource_not_found", id);
        if (!await Csrf(h)) return await Failure(h, 403, "csrf_invalid", id);
        if (!Ready(h)) return await Failure(h, 503, "file_settings_unavailable", id);
        var f = await OwnedFiles(db, h, vessel).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted);
        if (f == null) return await Failure(h, 404, "resource_not_found", id);
        var expires = DateTimeOffset.UtcNow.AddSeconds(h.RequestServices.GetRequiredService<SecureFileSettings>().CapabilitySeconds);
        var token = h.RequestServices.GetRequiredService<FileCapabilities>().Issue(a.TenantId, a.Id, vessel, id, f.Revision, expires);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "FileAccessIssue", "Success", resourceId: id);
        // Capability is sent in a header, never a URL/query/referrer or log.
        return Results.Ok(new { token, expiresAt = expires, revision = f.Revision });
    }
    private static async Task<IResult> Download(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Read(a)) return await Failure(h, 403, "permission_denied", id);
        if (!await Scope(db, a, vessel, h, ct)) return await Failure(h, 404, "resource_not_found", id);
        if (!Ready(h)) return await Failure(h, 503, "file_settings_unavailable", id);
        var file = await OwnedFiles(db, h, vessel).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        if (file == null) return await Failure(h, 404, "resource_not_found", id);
        if (h.Request.Headers["X-File-Capability"].Count != 1 || !h.RequestServices.GetRequiredService<FileCapabilities>().Valid(h.Request.Headers["X-File-Capability"], a.TenantId, a.Id, vessel, id, file.Revision, DateTimeOffset.UtcNow))
            return await Failure(h, 403, "file_capability_invalid", id);
        FileStream? stream = null;
        try {
            stream = await h.RequestServices.GetRequiredService<FileObjects>().OpenAsync(file, ct);
            if (stream.Length != file.Length || stream.Length > h.RequestServices.GetRequiredService<SecureFileSettings>().MaxBytes || Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)) != file.Checksum)
                throw new InvalidDataException();
            stream.Position = 0;
            var current = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
            if (current == null || current.Id != a.Id || current.TenantId != a.TenantId || !await Scope(db, current, vessel, h, ct)) {
                await stream.DisposeAsync(); stream = null;
                return await Failure(h, 403, "permission_denied", id);
            }
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "FileDownload", "Authorized", resourceId: id);
            return Results.Stream(stream, file.Mime, file.Name, enableRangeProcessing: false);
        } catch (Exception e) when (e is IOException or InvalidDataException) {
            if (stream != null) await stream.DisposeAsync();
            return await Failure(h, 503, "file_bytes_unavailable", id);
        } catch { if (stream != null) await stream.DisposeAsync(); throw; }
    }
}
