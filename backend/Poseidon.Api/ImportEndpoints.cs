using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record ImportBatchDto(Guid Id, string Category, long Revision, uint Version, string State,
    ImportTemplateDto Template, string[] Headers, ImportColumnBinding[] Bindings, int RowCount, string SourceHash, DateTimeOffset CreatedAt, string ReviewerState, DateTimeOffset? CommittedAt, Guid? CommittedPreviewId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportMappingCommand(uint Version, ImportColumnBinding[] Bindings);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportCommitCommand(Guid PreviewId, long Revision, string TemplateFingerprint);
public sealed record ImportPreviewPageDto(Guid PreviewId, string Revision, string TemplateVersion, string TemplateFingerprint,
    int TotalRows, int ValidRows, int RejectedRows, int Offset, int Limit, bool HasMore, ImportValidationRowDto[] Rows);
public sealed record ImportCommittedDto(Guid BatchId, string State, int ImportedRows, Guid PreviewId, bool Replayed);

public static class ImportEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void MapImports(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/v1/vessels/{vessel:guid}/imports").RequireAuthorization("DirectoryRead");
        group.MapGet("/coverage", Coverage).WithName("ImportSourceCoverage").Produces<ImportSourceCoverageDto>();
        group.MapGet("/templates", Templates).WithName("ImportTemplates").Produces<ImportTemplateDto[]>();
        group.MapGet("/templates/{category}/download", DownloadTemplate).WithName("ImportTemplateDownload");
        group.MapPost("/uploads/{category}", Upload).WithName("ImportUpload").Produces<ImportBatchDto>();
        group.MapGet("", History).WithName("ImportHistory");
        group.MapGet("/{batch:guid}", Detail).WithName("ImportDetail").Produces<ImportBatchDto>();
        group.MapPut("/{batch:guid}/mapping", Draft).WithName("ImportMappingDraft").Accepts<ImportMappingCommand>("application/json").Produces<ImportBatchDto>();
        group.MapPost("/{batch:guid}/preview", Preview).WithName("ImportPreview").Produces<ImportPreviewPageDto>();
        group.MapGet("/{batch:guid}/previews/{preview:guid}", Page).WithName("ImportPreviewPage").Produces<ImportPreviewPageDto>();
        group.MapGet("/{batch:guid}/previews/{preview:guid}/rejects", Rejects).WithName("ImportRejectDownload");
        group.MapPost("/{batch:guid}/commit", Commit).WithName("ImportCommit").Accepts<ImportCommitCommand>("application/json").Produces<ImportCommittedDto>();
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static string Session(HttpContext h) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(h.Request.Headers.Cookie.ToString())));
    private static bool Category(TrustedActor a, Guid vessel, string category, bool write) {
        var module = category switch { "crew" => "crew", "equipment" => "equipment", "certificate" => "crew.certificate", "vessel" => "directory", _ => "invalid" };
        return a.Permissions.Contains(module + ".read") && (!write || a.HasWriteAttribution && a.Permissions.Contains(module + ".write") &&
            (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit))) &&
            (category is not ("crew" or "certificate") || a.Permissions.Contains("crew.identity.read") && (!write || a.Permissions.Contains("crew.identity.write"))) &&
            (category != "certificate" || CredentialEndpoints.Permitted(a, vessel, write));
    }
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "ImportSave", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static async Task<IResult?> Gate(HttpContext h, PoseidonDbContext db, Guid vessel, bool write = false) {
        h.Response.Headers.CacheControl = "no-store"; h.Response.Headers.Pragma = "no-cache";
        var a = Actor(h);
        if (!a.CanRead || !a.Permissions.Contains("import.read") || write && (!a.HasWriteAttribution || !a.Permissions.Contains("import.write")))
            return await Fail(h, 403, "permission_denied");
        if (!await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        if (write) {
            try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
            catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        }
        return null;
    }
    private static async Task<ImportBatch?> Load(Guid vessel, Guid batch, HttpContext h, PoseidonDbContext db, bool write = false) {
        var a = Actor(h); var session = Session(h);
        var b = await db.ImportBatches.SingleOrDefaultAsync(x => x.Id == batch && x.VesselId == vessel && x.ActorId == a.Id && x.SessionHash == session, h.RequestAborted);
        if (b == null || b.LegacyProtectedAt != null || b.Category == "crew" && !b.TableJson.StartsWith("protected-v1:", StringComparison.Ordinal) || !Category(a, vessel, b.Category, write)) return null;
        if (b.Category == "certificate" && !await db.CrewAssignments.AnyAsync(x => x.VesselId == vessel && x.Id == b.AssignmentId, h.RequestAborted)) return null;
        if (b.Category == "crew" && (!h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Allows(a, write) || b.CrewIdentityPolicyVersion != h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Version)) return null;
        if (b.Category == "crew" && !AllowedProfileBindings(b, h, write)) return null;
        // Protecting historic storage never makes an obsolete template editable through today's schema.
        var template = ImportTemplates.Describe(b.Category);
        if (b.TemplateVersion != template.Version || b.TemplateFingerprint != template.Fingerprint) return null;
        if (b.Category == "certificate" && b.BindingsJson.Contains("\"document_ref\"", StringComparison.Ordinal) && !CanReadPersonDocuments(h)) return null;
        return b;
    }
    private static bool CanReadPersonDocuments(HttpContext h) => Actor(h).Permissions.Contains("file.read") &&
        CrewFilePolicy.Current(h).Allows(Actor(h), false) && CrewFilePolicy.Current(h).Kinds.Contains("CertificateDocument");
    private static bool AllowedProfileBindings(ImportBatch b, HttpContext h, bool write) {
        var bindings = JsonSerializer.Deserialize<ImportColumnBinding[]>(b.BindingsJson, Json)!;
        var policy = h.RequestServices.GetRequiredService<CrewProfilePolicy>();
        return Enum.GetValues<CrewPersonalField>().Where(f => bindings.Any(c => c.TargetField == CrewPersonalData.Key(f)))
            .All(f => policy.Rule(f, policy.ImportPurpose, Actor(h).Permissions, write) != null);
    }
    private static ImportTable Table(ImportBatch b, HttpContext h) => JsonSerializer.Deserialize<ImportTable>(
        b.Category == "crew" ? h.RequestServices.GetRequiredService<CrewProfileProtection>().DecryptImport(b.TenantId, b.Id, b.TableJson) : b.TableJson, Json)!;
    private static ImportBatchDto Dto(ImportBatch b, HttpContext h) => new(b.Id, b.Category, b.Revision, b.Version, b.State, ImportTemplates.Describe(b.Category),
        Table(b, h).Headers,
        JsonSerializer.Deserialize<ImportColumnBinding[]>(b.BindingsJson, Json)!, b.RowCount, b.SourceHash, b.CreatedAt, b.ReviewerState, b.CommittedAt, b.CommittedPreviewId);
    private static async Task Observe(HttpContext h, Guid vessel, string action = "ImportRead") =>
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, "Success", resourceId: vessel);
    private static async Task<IResult> Coverage(Guid vessel, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel); if (failure != null) return failure;
        var categories = ImportTemplates.Categories.Where(c => Category(Actor(h), vessel, c, false) && (c != "crew" || h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Allows(Actor(h), false))).ToArray();
        await Observe(h, vessel); return Results.Ok(ImportCoverage.Describe(categories));
    }
    private static async Task<IResult> Templates(Guid vessel, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel); if (failure != null) return failure;
        var rows = ImportTemplates.Categories.Where(c => Category(Actor(h), vessel, c, false) && (c != "crew" || h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Allows(Actor(h), false))).Select(ImportTemplates.Describe).ToArray();
        await Observe(h, vessel); return Results.Ok(rows);
    }
    private static async Task<IResult> DownloadTemplate(Guid vessel, string category, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel); if (failure != null) return failure;
        if (ImportTemplates.Definition(category) == null || !Category(Actor(h), vessel, category, false)) return await Fail(h, 404, "resource_not_found");
        if (category == "crew" && !h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Allows(Actor(h), false)) return await Fail(h, 404, "resource_not_found");
        var t = ImportTemplates.Describe(category); await Observe(h, vessel);
        h.Response.Headers["X-Import-Schema"] = t.Fingerprint;
        return Results.File(ImportWorkbook.Template(t.Fields.Where(f => f.UserWritable).Select(f => f.Key).ToArray()),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"poseidon-{category}-declaration-v1.xlsx");
    }
    private static async Task<byte[]> Body(HttpContext h, int maximum) {
        using var stream = new MemoryStream(); var buffer = new byte[8192]; int n;
        while ((n = await h.Request.Body.ReadAsync(buffer, h.RequestAborted)) != 0) {
            if (stream.Length + n > maximum) throw new ImportInputException();
            stream.Write(buffer, 0, n);
        }
        return stream.ToArray();
    }
    private static async Task<IResult> Upload(Guid vessel, string category, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, true); if (failure != null) return failure;
        Guid? assignment = null;
        var selection = h.Request.Query["assignment"];
        if (selection.Count > 1) return await Fail(h, 400, "invalid_request");
        if (selection.Count == 1) {
            if (!Guid.TryParseExact(selection[0], "D", out var selected) || selected == Guid.Empty) return await Fail(h, 400, "invalid_request");
            assignment = selected;
        }
        if (ImportTemplates.Definition(category) == null || !Category(Actor(h), vessel, category, true)) return await Fail(h, 403, "permission_denied");
        if (category == "crew" && !h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Allows(Actor(h), true)) return await Fail(h, 403, "field_policy_denied");
        if (h.Request.Query.Keys.Any(k => k != "assignment") || category != "certificate" && assignment != null) return await Fail(h, 400, "invalid_request");
        if (category == "certificate" && (assignment == null || !await db.CrewAssignments.AnyAsync(x => x.Id == assignment && x.VesselId == vessel, h.RequestAborted)))
            return await Fail(h, 404, "resource_not_found");
        if (h.Request.ContentType != "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") return await Fail(h, 400, "invalid_request");
        byte[] bytes; ImportTable table;
        try { bytes = await Body(h, ImportWorkbook.MaxBytes); table = ImportWorkbook.Parse(bytes); }
        catch (ImportInputException) { return await Fail(h, 400, "validation_failed"); }
        var t = ImportTemplates.Describe(category);
        var b = new ImportBatch { TenantId = Actor(h).TenantId, ActorId = Actor(h).Id, SessionHash = Session(h), VesselId = vessel, Id = Guid.NewGuid(),
            AssignmentId = assignment, Category = category, TemplateVersion = t.Version, TemplateFingerprint = t.Fingerprint,
            SourceName = "name-withheld.xlsx", SourceHash = Convert.ToHexString(SHA256.HashData(bytes)), TableJson = JsonSerializer.Serialize(table, Json),
            RowCount = table.Rows.Length, CreatedAt = DateTimeOffset.UtcNow };
        if (category == "crew") {
            var protection = h.RequestServices.GetRequiredService<CrewProfileProtection>();
            if (!protection.Available) return await Fail(h, 503, "protected_storage_unavailable");
            b.CrewIdentityPolicyVersion = h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Version;
            b.TableJson = protection.EncryptImport(b.TenantId, b.Id, b.TableJson);
        }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, h.RequestAborted);
        try {
            // Exact source bytes + immutable authorized context identify an upload retry.
            // No fuzzy file/key match and no operational data merge is inferred.
            var old = await db.ImportBatches.SingleOrDefaultAsync(x => x.VesselId == vessel && x.ActorId == b.ActorId && x.SessionHash == b.SessionHash &&
                x.Category == category && x.AssignmentId == assignment && x.SourceHash == b.SourceHash && x.TemplateFingerprint == b.TemplateFingerprint, h.RequestAborted);
            if (old != null) {
                if (old.Category == "crew" && (old.LegacyProtectedAt != null || !old.TableJson.StartsWith("protected-v1:", StringComparison.Ordinal) || old.CrewIdentityPolicyVersion != b.CrewIdentityPolicyVersion)) return await Fail(h, 403, "field_policy_denied");
                if (old.Category == "crew" && !AllowedProfileBindings(old, h, false)) return await Fail(h, 403, "field_policy_denied");
                await Observe(h, vessel); return Results.Ok(Dto(old, h)); }
            db.ImportBatches.Add(b); await db.SaveChangesAsync(h.RequestAborted); await tx.CommitAsync(h.RequestAborted);
            await Observe(h, vessel, "ImportSave"); return Results.Ok(Dto(b, h));
        } catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" }) {
            await tx.RollbackAsync(CancellationToken.None); return await Fail(h, 409, "version_conflict");
        }
    }
    private static async Task<IResult> Detail(Guid vessel, Guid batch, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel); if (failure != null) return failure;
        var b = await Load(vessel, batch, h, db); if (b == null) return await Fail(h, 404, "resource_not_found");
        await Observe(h, vessel); return Results.Ok(Dto(b, h));
    }
    private static bool Offset(HttpContext h, out int value) {
        value = 0; var raw = h.Request.Query["offset"];
        return raw.Count == 0 || raw.Count == 1 && int.TryParse(raw[0], NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
    private static async Task<IResult> History(Guid vessel, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel); if (failure != null) return failure;
        if (!Offset(h, out var offset) || offset > 100000 || h.Request.Query.Keys.Any(k => k != "offset")) return await Fail(h, 400, "invalid_request");
        var a = Actor(h); var categories = ImportTemplates.Categories.Where(c => Category(a, vessel, c, false) && (c != "crew" || h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Allows(a, false))).ToArray(); var session = Session(h);
        var query = db.ImportBatches.AsNoTracking().Where(x => x.VesselId == vessel && x.ActorId == a.Id && x.SessionHash == session && categories.Contains(x.Category));
        var identityVersion = h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Version;
        query = query.Where(x => x.Category != "crew" || x.LegacyProtectedAt == null && x.TableJson.StartsWith("protected-v1:") && x.CrewIdentityPolicyVersion == identityVersion);
        if (!CanReadPersonDocuments(h)) query = query.Where(x => x.Category != "certificate" || !x.BindingsJson.Contains("\"document_ref\""));
        var profilePolicy = h.RequestServices.GetRequiredService<CrewProfilePolicy>();
        foreach (var field in Enum.GetValues<CrewPersonalField>().Where(f => profilePolicy.Rule(f, profilePolicy.ImportPurpose, a.Permissions, false) == null)) {
            var target = "\"" + CrewPersonalData.Key(field) + "\"";
            query = query.Where(x => x.Category != "crew" || !x.BindingsJson.Contains(target));
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(26)
            .Select(x => new { x.Id, x.Category, x.State, x.Revision, x.RowCount, x.SourceHash, x.TemplateVersion, x.CreatedAt, x.CommittedAt }).ToArrayAsync(h.RequestAborted);
        await Observe(h, vessel); return Results.Ok(new { items = rows.Take(25), offset = offset, hasMore = rows.Length > 25 });
    }
    private static async Task<IResult> Draft(Guid vessel, Guid batch, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, true); if (failure != null) return failure;
        var b = await Load(vessel, batch, h, db, true); if (b == null) return await Fail(h, 404, "resource_not_found");
        ImportMappingCommand? command;
        try { command = JsonSerializer.Deserialize<ImportMappingCommand>(await Body(h, 16384), Json); }
        catch (Exception e) when (e is JsonException or ImportInputException) { return await Fail(h, 400, "invalid_request"); }
        if (command?.Bindings == null || command.Bindings.Length > ImportWorkbook.MaxColumns || command.Bindings.Any(x => x == null || x.TargetField?.Length > 100))
            return await Fail(h, 400, "invalid_request");
        if (b.Version != command.Version || b.State == "Committed") return await Fail(h, 409, "version_conflict");
        if (b.Category == "crew") {
            var policy = h.RequestServices.GetRequiredService<CrewProfilePolicy>();
            if (Enum.GetValues<CrewPersonalField>().Where(f => command.Bindings.Any(c => c.TargetField == CrewPersonalData.Key(f)))
                .Any(f => policy.Rule(f, policy.ImportPurpose, Actor(h).Permissions, true) == null)) return await Fail(h, 403, "field_policy_denied");
        }
        // Incomplete drafts are captured; only preview requires complete validated bindings.
        b.BindingsJson = JsonSerializer.Serialize(command.Bindings, Json); b.Revision++; b.State = "MappingDraft";
        try { await db.SaveChangesAsync(h.RequestAborted); await Observe(h, vessel, "ImportSave"); return Results.Ok(Dto(b, h)); }
        catch (DbUpdateException) { return await Fail(h, 409, "version_conflict"); }
    }
    private static ImportColumnMappingResult Mapping(ImportBatch b, ImportTable table) => ImportColumnMappingPolicy.Validate(
        ImportTemplates.Definition(b.Category)!, table.Headers.Length, JsonSerializer.Deserialize<ImportColumnBinding[]>(b.BindingsJson, Json)!);

    private static async Task<ImportValidationRowDto[]> Validate(ImportBatch b, ImportTable table, ValidatedImportMapping mapping, HttpContext h, PoseidonDbContext db) {
        var results = new List<ImportValidationRowDto>(); var keys = new HashSet<Guid>();
        // Current authorized context only; these reads never mutate domain records.
        var assignments = b.Category == "crew" ? await db.CrewAssignments.AsNoTracking().Where(x => x.VesselId == b.VesselId).ToArrayAsync(h.RequestAborted) : Array.Empty<CrewAssignment>();
        var assets = b.Category == "equipment" ? await db.Assets.AsNoTracking().Where(x => x.VesselId == b.VesselId).ToArrayAsync(h.RequestAborted) : Array.Empty<Asset>();
        var credentials = b.Category == "certificate" ? await db.Credentials.AsNoTracking().Where(x => x.VesselId == b.VesselId && x.AssignmentId == b.AssignmentId).ToArrayAsync(h.RequestAborted) : Array.Empty<Credential>();
        var nodes = assets.Select(x => new EquipmentHierarchyNode(x.TenantId, x.VesselId, x.Id, x.ParentId)).ToList();
        foreach (var (cells, i) in table.Rows.Select((r, i) => (r, i))) {
            var issues = new List<ImportRowIssueDto>();
            void Issue(string code, string? key = null) => issues.Add(new(code, key == null ? null : Array.FindIndex(ImportTemplates.Describe(b.Category).Fields, f => f.Key == key)));
            var r = new ImportTemplates.Row(mapping, cells);
            try {
                var id = r.Id("record_key", true); var version = r.BaseVersion();
                if (!keys.Add(id)) Issue("duplicate", "record_key");
                if (b.Category == "vessel") {
                    var c = r.Vessel(); var v = await Actor(h).Scope(db.Vessels.AsNoTracking()).SingleAsync(x => x.Id == b.VesselId, h.RequestAborted);
                    if (id != b.VesselId || v.Version != version) Issue("conflict", "record_key");
                    if (DirectoryInputPolicy.Validate(c.Code, c.Name, c.ExpectedVersion, true).Count > 0) Issue("domain_validation");
                    if (await db.Vessels.AnyAsync(x => x.Id != id && x.Code == (c.Code ?? "").Trim(), h.RequestAborted)) Issue("conflict", "code");
                } else if (b.Category == "crew") {
                    var old = assignments.SingleOrDefault(x => x.Id == id); var c = r.Crew(old);
                    if (old == null && version != null || old != null && old.Version != version) Issue("conflict", "record_key");
                    var personal = r.PersonalProfile();
                    if (personal != null) {
                        var policy = h.RequestServices.GetRequiredService<CrewProfilePolicy>();
                        personal = personal with { Purpose = policy.ImportPurpose };
                        if (!CrewProfileEndpoints.Valid(personal, Actor(h), policy) || !h.RequestServices.GetRequiredService<CrewProfileProtection>().Available) Issue("unsupported_value");
                        var stored = old == null ? null : await db.CrewPersonalProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == old.PersonId, h.RequestAborted);
                        if (stored == null && personal.BaseVersion != null || stored != null && stored.Version != personal.BaseVersion) Issue("conflict", "profile_base_version");
                    }
                    if (!h.RequestServices.GetRequiredService<CrewAssignmentPolicy>().CanDeclare(c.DeclaredStatus, c.AssignmentPolicyVersion, Actor(h))) Issue("unsupported_value");
                    if (!CrewEndpoints.ValidInput(c)) Issue("domain_validation");
                    var identityPolicy = h.RequestServices.GetRequiredService<CrewIdentityPolicy>();
                    if (old != null && r.HasIdentity) {
                        if (r.Reference("person_id") != old.PersonId) Issue("unresolved_reference", "person_id");
                        var person = await db.Persons.AsNoTracking().SingleAsync(x => x.Id == old.PersonId, h.RequestAborted);
                        var identity = r.Identity();
                        if (!CrewIdentityEndpoints.Valid(identity, identityPolicy, Actor(h))) Issue("unsupported_value");
                        if (person.Version != identity.BaseVersion) Issue("conflict", "person_base_version");
                        try {
                            var current = CrewIdentityStorage.Open(person, h.RequestServices.GetRequiredService<CrewProfileProtection>());
                            if (identity.EmployeeNoPresent && !CrewIdentityData.EmployeeChangeAllowed(identityPolicy.EmployeeNumberMode, current.Employee, identity.EmployeeNo)) Issue("unsupported_value", "employee_no");
                        } catch (CryptographicException) { Issue("unsupported_value"); }
                    } else if (old == null && c.PersonId == null && (!identityPolicy.Allows(Actor(h), true) || c.IdentityPolicyVersion != identityPolicy.Version || c.IdentityPurpose != identityPolicy.Purpose)) Issue("unsupported_value");
                    if (old != null && c.PersonId != old.PersonId || old == null && c.PersonId != null && (r.HasIdentity || !assignments.Any(x => x.PersonId == c.PersonId))) Issue("unresolved_reference", "person_id");
                    if (!(await CrewPositionPolicy.Current(h, db, b.VesselId)).AllowsReference(Actor(h).TenantId, b.VesselId, c.PositionId, c.PositionPolicyVersion, await db.CrewPositions.AsNoTracking().Where(x => x.VesselId == b.VesselId).ToArrayAsync(h.RequestAborted))) Issue("unresolved_reference", "position_ref");
                    if (c.PositionId != null && !await db.CrewPositions.AnyAsync(x => x.Id == c.PositionId && x.VesselId == b.VesselId, h.RequestAborted)) Issue("unresolved_reference", "position_ref");
                    if (c.From != null && old != null && await db.CrewAssignments.AnyAsync(x => x.PersonId == old.PersonId && x.Id != id && x.From != null &&
                        (x.To == null || c.From < x.To) && (c.To == null || x.From < c.To), h.RequestAborted)) Issue("conflict");
                } else if (b.Category == "certificate") {
                    var old = credentials.SingleOrDefault(x => x.Id == id); var c = r.Certificate(old);
                    if (old == null && version != null || old != null && (old.Version != version || old.Status != "Draft" || old.VerificationState != "Unverified")) Issue("conflict", "record_key");
                    if (!CertificateMetadataPolicy.Valid(c.Metadata)) Issue("domain_validation");
                    if (c.Metadata.DocumentReference != null && !await CrewFileEndpoints.AuthorizedDocument(b.VesselId, b.AssignmentId!.Value, c.Metadata.DocumentReference.Value, h, db)) Issue("unresolved_reference", "document_ref");
                } else {
                    var old = assets.SingleOrDefault(x => x.Id == id); var c = r.Equipment(old);
                    if (old == null && version != null || old != null && old.Version != version) Issue("conflict", "record_key");
                    if (!TechnicalEndpoints.ValidEquipmentInput(c)) Issue("domain_validation");
                    if (c.ResponsibleRoleId is { } owner && !TechnicalEndpoints.EligibleOwners(h, b.VesselId).Contains(owner)) Issue("unresolved_reference");
                    if (c.Code != null && assets.Any(x => x.Id != id && x.Code == c.Code)) Issue("conflict", "code");
                    if (old == null) nodes.Add(new(db.TenantId, b.VesselId, id, null));
                    if (EquipmentHierarchyPolicy.ValidateReparent(db.TenantId, b.VesselId, nodes, id, c.ParentId) != EquipmentHierarchyValidation.Valid) Issue("unresolved_reference", "parent_ref");
                    if (issues.Count == 0) { nodes.RemoveAll(x => x.Id == id); nodes.Add(new(db.TenantId, b.VesselId, id, c.ParentId)); }
                }
            } catch (FormatException) { Issue("invalid_format"); }
            results.Add(new(i + 2, issues.Count == 0 ? "valid" : "rejected", issues.ToArray()));
        }
        // Duplicate keys reject every occurrence; there is no order-based winner.
        var duplicateKeys = table.Rows.Select((cells, i) => (row: i, key: new ImportTemplates.Row(mapping, cells).Get("record_key")))
            .GroupBy(x => x.key, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).SelectMany(g => g.Select(x => x.row)).ToHashSet();
        if (b.Category == "equipment") {
            // Same ordinal Code uniqueness as ordinary EF storage; never trim or fuzzy-merge.
            var duplicateCodes = table.Rows.Select((cells, i) => (row: i, code: new ImportTemplates.Row(mapping, cells).Get("code")))
                .Where(x => x.code != null).GroupBy(x => x.code, StringComparer.Ordinal).Where(g => g.Count() > 1).SelectMany(g => g.Select(x => x.row));
            foreach (var row in duplicateCodes) duplicateKeys.Add(row);
        }
        return results.Select((r, i) => duplicateKeys.Contains(i) ? r with { Outcome = "rejected", Issues = r.Issues.Append(new("duplicate", 0)).Distinct().ToArray() } : r).ToArray();
    }
    private static async Task<IResult> Preview(Guid vessel, Guid batch, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, true); if (failure != null) return failure;
        var b = await Load(vessel, batch, h, db, true); if (b == null) return await Fail(h, 404, "resource_not_found");
        if (b.State == "Committed") return await Fail(h, 409, "version_conflict");
        var table = Table(b, h); var mapping = Mapping(b, table);
        if (!mapping.IsValid || mapping.Mapping!.TemplateFingerprint != b.TemplateFingerprint) return await Fail(h, 400, "validation_failed");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, h.RequestAborted);
        var rows = await Validate(b, table, mapping.Mapping, h, db);
        var p = new ImportPreview { Id = Guid.NewGuid(), TenantId = db.TenantId, VesselId = vessel, BatchId = batch, BatchRevision = b.Revision,
            TemplateFingerprint = b.TemplateFingerprint, BindingsJson = b.BindingsJson, TotalRows = rows.Length, ValidRows = rows.Count(x => x.Outcome == "valid"),
            RejectedRows = rows.Count(x => x.Outcome == "rejected"), CreatedAt = DateTimeOffset.UtcNow };
        db.ImportPreviews.Add(p);
        foreach (var row in rows) db.ImportRowResults.Add(new() { TenantId = db.TenantId, VesselId = vessel, PreviewId = p.Id, RowNumber = row.RowNumber,
            Outcome = row.Outcome, IssuesJson = JsonSerializer.Serialize(row.Issues, Json) });
        b.State = "Previewed"; db.Entry(b).Property(x => x.State).IsModified = true;
        try { await db.SaveChangesAsync(h.RequestAborted); await tx.CommitAsync(h.RequestAborted); }
        catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" }) {
            await tx.RollbackAsync(CancellationToken.None); return await Fail(h, 409, "version_conflict");
        }
        await Observe(h, vessel); return Results.Ok(PageDto(p, b.TemplateVersion, rows.Take(25).ToArray(), 0));
    }
    private static ImportPreviewPageDto PageDto(ImportPreview p, string version, ImportValidationRowDto[] rows, int offset) =>
        new(p.Id, p.BatchRevision.ToString(CultureInfo.InvariantCulture), version, p.TemplateFingerprint, p.TotalRows, p.ValidRows, p.RejectedRows, offset, 25, offset + rows.Length < p.TotalRows, rows);
    private static async Task<ImportPreview?> ExactPreview(ImportBatch b, Guid preview, HttpContext h, PoseidonDbContext db) {
        if (h.Request.Query.Keys.Any(k => k is not ("offset" or "revision" or "fingerprint"))) return null;
        if (h.Request.Query["revision"].ToString() != b.Revision.ToString(CultureInfo.InvariantCulture) || h.Request.Query["fingerprint"].ToString() != b.TemplateFingerprint) return null;
        return await db.ImportPreviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == preview && x.BatchId == b.Id && x.VesselId == b.VesselId &&
            x.BatchRevision == b.Revision && x.TemplateFingerprint == b.TemplateFingerprint, h.RequestAborted);
    }
    private static async Task<IResult> Page(Guid vessel, Guid batch, Guid preview, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel); if (failure != null) return failure;
        var b = await Load(vessel, batch, h, db); if (b == null) return await Fail(h, 404, "resource_not_found");
        var p = await ExactPreview(b, preview, h, db); if (p == null) return await Fail(h, 409, "version_conflict");
        if (!Offset(h, out var offset) || offset > p.TotalRows || p.TotalRows > 0 && offset == p.TotalRows) return await Fail(h, 400, "invalid_request");
        var records = await db.ImportRowResults.AsNoTracking().Where(x => x.PreviewId == preview).OrderBy(x => x.RowNumber).Skip(offset).Take(25).ToArrayAsync(h.RequestAborted);
        await Observe(h, vessel);
        return Results.Ok(PageDto(p, b.TemplateVersion, records.Select(x => new ImportValidationRowDto(x.RowNumber, x.Outcome, JsonSerializer.Deserialize<ImportRowIssueDto[]>(x.IssuesJson, Json)!.Select(i => i with { Code = SafeCode(i.Code) }).ToArray())).ToArray(), offset));
    }
    private static async Task<IResult> Rejects(Guid vessel, Guid batch, Guid preview, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel); if (failure != null) return failure;
        var b = await Load(vessel, batch, h, db); if (b == null) return await Fail(h, 404, "resource_not_found");
        var p = await ExactPreview(b, preview, h, db); if (p == null) return await Fail(h, 409, "version_conflict");
        var rows = await db.ImportRowResults.AsNoTracking().Where(x => x.PreviewId == preview && x.Outcome == "rejected").OrderBy(x => x.RowNumber).ToArrayAsync(h.RequestAborted);
        var report = new StringBuilder("row_number,field_ordinal,code\r\n");
        foreach (var row in rows) foreach (var issue in JsonSerializer.Deserialize<ImportRowIssueDto[]>(row.IssuesJson, Json)!)
            report.Append(row.RowNumber.ToString(CultureInfo.InvariantCulture)).Append(',').Append(issue.FieldOrdinal?.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(SafeCode(issue.Code)).Append("\r\n");
        await Observe(h, vessel, "ImportReport");
        return Results.File(Encoding.UTF8.GetBytes(report.ToString()), "text/csv; charset=utf-8", "poseidon-rejects.csv");
    }
    internal static string SafeCode(string code) => code is "required" or "invalid_format" or "duplicate" or "conflict" or "unresolved_reference" or "unsupported_value" or "domain_validation" ? code : "unknown";

    private static async Task<IResult> Commit(Guid vessel, Guid batch, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, true); if (failure != null) return failure;
        var b = await Load(vessel, batch, h, db, true); if (b == null) return await Fail(h, 404, "resource_not_found");
        ImportCommitCommand? command;
        try { command = JsonSerializer.Deserialize<ImportCommitCommand>(await Body(h, 4096), Json); }
        catch (Exception e) when (e is JsonException or ImportInputException) { return await Fail(h, 400, "invalid_request"); }
        if (command == null || command.Revision != b.Revision || command.TemplateFingerprint != b.TemplateFingerprint) return await Fail(h, 409, "version_conflict");
        var p = await db.ImportPreviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.PreviewId && x.BatchId == batch && x.VesselId == vessel &&
            x.BatchRevision == b.Revision && x.TemplateFingerprint == b.TemplateFingerprint, h.RequestAborted);
        if (p == null || p.RejectedRows != 0 || p.TotalRows == 0) return await Fail(h, 409, "version_conflict");
        // 18.03 paragraphs34–36/44: trial authoring is available without inventing
        // DEV-070 owner sign-off or a production rollback/hold approval.
        var environment = h.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test")) return await Fail(h, 503, "service_unavailable");
        var originalKey = h.Request.Headers["Idempotency-Key"];
        if (!Guid.TryParseExact(originalKey.ToString(), "D", out _)) return await Fail(h, 400, "invalid_idempotency_key");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { batch, command }, Json)));
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, h.RequestAborted);
        try {
            var a = Actor(h);
            // Field/purpose/policy/catalog authority is current even for historic batch receipt recovery.
            // Do not re-run stale domain base-version validation to recover a receipt.
            var table = Table(b, h); var mapping = Mapping(b, table);
            if (!mapping.IsValid || mapping.Mapping!.TemplateFingerprint != b.TemplateFingerprint) return await Fail(h, 409, "version_conflict");
            if (b.Category == "crew") {
                var profilePolicy = h.RequestServices.GetRequiredService<CrewProfilePolicy>();
                foreach (var cells in table.Rows) {
                    var row = new ImportTemplates.Row(mapping.Mapping, cells);
                    var identityPolicy = h.RequestServices.GetRequiredService<CrewIdentityPolicy>();
                    if (!identityPolicy.Allows(a, true) || row.HasIdentity && (row.Get("identity_policy_version") != identityPolicy.Version || row.Get("identity_purpose") != identityPolicy.Purpose)) return await Fail(h, 403, "field_policy_denied");
                    var assignment = await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == row.Id("record_key", true), h.RequestAborted);
                    var crewCommand = row.Crew(assignment);
                    if (!(await CrewPositionPolicy.Current(h, db, vessel)).AllowsReference(a.TenantId, vessel, crewCommand.PositionId, crewCommand.PositionPolicyVersion,
                        await db.CrewPositions.AsNoTracking().Where(x => x.VesselId == vessel).ToArrayAsync(h.RequestAborted))) return await Fail(h, 403, "field_policy_denied");
                    var personal = row.PersonalProfile();
                    if (personal != null && !CrewProfileEndpoints.Valid(personal with { Purpose = profilePolicy.ImportPurpose }, a, profilePolicy)) return await Fail(h, 403, "field_policy_denied");
                    if (!h.RequestServices.GetRequiredService<CrewAssignmentPolicy>().CanDeclare(row.Get("assignment_status"), row.Get("assignment_policy_version"), a)) return await Fail(h, 403, "assignment_policy_denied");
                }
            }
            if (b.Category == "certificate") {
                foreach (var cells in table.Rows) {
                    var row = new ImportTemplates.Row(mapping.Mapping, cells); var id = row.Id("record_key", true);
                    var old = await db.Credentials.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel && x.AssignmentId == b.AssignmentId, h.RequestAborted);
                    var reference = row.Certificate(old).Metadata.DocumentReference;
                    if (reference is { } file && !await CrewFileEndpoints.AuthorizedDocument(vessel, b.AssignmentId!.Value, file, h, db)) return await Fail(h, 403, "permission_denied");
                }
            }
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == originalKey.ToString(), h.RequestAborted);
            if (receipt != null) {
                if (receipt.Action != "ImportCommit" || receipt.Fingerprint != fingerprint || receipt.ResourceId != batch) return await Fail(h, 409, "idempotency_key_reused");
                return Results.Ok(new ImportCommittedDto(batch, "Committed", b.RowCount, p.Id, true));
            }
            if (b.State == "Committed") return await Fail(h, 409, "version_conflict");
            var current = await Validate(b, table, mapping.Mapping, h, db);
            if (current.Any(r => r.Outcome != "valid")) return await Fail(h, 409, "version_conflict");
            foreach (var (cells, i) in table.Rows.Select((r, i) => (r, i))) {
                // Row keys are batch/preview/row-bound and deterministic across restart/retry.
                h.Request.Headers["Idempotency-Key"] = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{batch:D}/{p.Id:D}/{i}"))[..16]).ToString("D");
                var r = new ImportTemplates.Row(mapping.Mapping, cells); IResult result;
                if (b.Category == "vessel") result = await DirectoryCommands.ApplyImport(h, vessel, r.Vessel());
                else if (b.Category == "equipment") {
                    var id = r.Id("record_key", true);
                    var old = await db.Assets.SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted);
                    result = await TechnicalEndpoints.ApplyImport(vessel, r.Equipment(old), h, db, true);
                }
                else if (b.Category == "certificate") {
                    var id = r.Id("record_key", true);
                    var old = await db.Credentials.SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel && x.AssignmentId == b.AssignmentId, h.RequestAborted);
                    result = await CredentialEndpoints.Apply(vessel, b.AssignmentId!.Value, r.Certificate(old), h, db, true);
                }
                else {
                    var id = r.Id("record_key", true);
                    var old = await db.CrewAssignments.SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted);
                    result = await CrewEndpoints.ApplyImport(vessel, r.Crew(old), h, db, h.RequestServices.GetRequiredService<IAntiforgery>(), true);
                    if (old != null && r.HasIdentity && result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 }) {
                        h.Request.Headers["Idempotency-Key"] = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{batch:D}/{p.Id:D}/{i}/identity"))[..16]).ToString("D");
                        result = await CrewIdentityEndpoints.Apply(vessel, id, r.Identity(), h, db, imported: true);
                    }
                    var personal = r.PersonalProfile();
                    if (personal != null && result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 }) {
                        var policy = h.RequestServices.GetRequiredService<CrewProfilePolicy>();
                        h.Request.Headers["Idempotency-Key"] = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{batch:D}/{p.Id:D}/{i}/profile"))[..16]).ToString("D");
                        result = await CrewProfileEndpoints.Apply(vessel, id, personal with { Purpose = policy.ImportPurpose }, h, db, true);
                    }
                }
                if (result is not IStatusCodeHttpResult { StatusCode: >= 200 and < 300 }) {
                    await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, 409, "version_conflict");
                }
            }
            b.State = "Committed"; b.CommittedAt = DateTimeOffset.UtcNow; b.CommittedPreviewId = p.Id;
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = originalKey.ToString(), Action = "ImportCommit", Fingerprint = fingerprint,
                ResourceId = batch, RegisterRevision = b.Revision, CommittedAt = b.CommittedAt.Value });
            await db.SaveChangesAsync(h.RequestAborted); await tx.CommitAsync(h.RequestAborted);
            return Results.Ok(new ImportCommittedDto(batch, "Committed", b.RowCount, p.Id, false));
        } catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, 409, "version_conflict");
        } finally { h.Request.Headers["Idempotency-Key"] = originalKey; }
    }
}
