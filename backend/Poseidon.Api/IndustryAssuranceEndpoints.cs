using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssuranceMappingCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string Key, [property: JsonRequired] AssuranceColumnBinding[] Bindings)
{ public override string ToString() => nameof(AssuranceMappingCreateCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssuranceTableInput([property: JsonRequired] string[] Headers, [property: JsonRequired] string[][] Rows)
{ public override string ToString() => nameof(AssuranceTableInput); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssuranceImportStageCommand([property: JsonRequired] Guid Id, [property: JsonRequired] Guid InspectionId, [property: JsonRequired] Guid MappingId,
    string? ReferenceNo = null, DateOnly? InspectionDate = null, AssuranceTableInput? Table = null, string? Workbook = null)
{ public override string ToString() => nameof(AssuranceImportStageCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssuranceImportDecisionCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string DiffHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string[] AcceptChanged, [property: JsonRequired] string[] RejectChanged, [property: JsonRequired] string Reason)
{ public override string ToString() => nameof(AssuranceImportDecisionCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssuranceEvidenceCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string QuestionRef, [property: JsonRequired] CertificateFilePin File,
    [property: JsonRequired] string MediaKind, [property: JsonRequired] DateTimeOffset CapturedAt, [property: JsonRequired] string Origin)
{ public override string ToString() => nameof(AssuranceEvidenceCommand); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssuranceFindingLinkCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] Guid ActionId)
{ public override string ToString() => nameof(AssuranceFindingLinkCommand); }

public sealed record AssuranceMappingDto(Guid Id, string Key, int Version, string SourceVersion, AssuranceColumnBinding[] Bindings, string Hash, DateTimeOffset CreatedAt);
public sealed record AssuranceItemDto(string QuestionRef, long Revision, AssuranceItemContent Content, string Hash, int Evidence, DateTimeOffset SavedAt);
public sealed record AssuranceFindingDto(Guid Id, string QuestionRef, long FirstItemRevision, long LatestItemRevision, bool ObservationPresent, string State,
    string Verification, string ActionLinkState, Guid? ActionId, long Revision, uint Version);
public sealed record AssuranceEvidenceDto(Guid Id, string QuestionRef, Guid FileId, long FileRevision, string MediaKind, DateTimeOffset CapturedAt, string Origin,
    string Verification, string FileHref);
public sealed record AssuranceImportDto(Guid Id, string State, int MappingVersion, string SourceVersion, AssuranceDiffEntry[] Diff, string DiffHash, bool ImportedByMe,
    DateTimeOffset ImportedAt, DateTimeOffset? DecidedAt, uint Version, int New, int Changed, int Unchanged, int Absent);
public sealed record AssuranceInspectionDto(Guid Id, Guid VesselId, string Programme, string ReferenceNo, DateOnly InspectionDate, string State, string Classification,
    string Applicability, long Revision, string Provenance, string SourceVersion, AssuranceItemDto[] Items, AssuranceFindingDto[] Findings, AssuranceEvidenceDto[] Evidence,
    AssuranceImportDto[] Imports, string Notice);
public sealed record AssuranceInspectionSummaryDto(Guid Id, string ReferenceNo, DateOnly InspectionDate, string State, string Applicability, int Items, int OpenFindings, bool PendingImport);
public sealed record IndustryAssurancePageDto(Guid VesselId, string Classification, string Applicability, string SourceVersion, AssuranceInspectionSummaryDto[] Inspections,
    AssuranceMappingDto[] Mappings, IndustryAssuranceCapabilitiesDto Capabilities, string Notice);
public sealed record IndustryAssuranceCapabilitiesDto(bool CanImport, bool CanAccept, bool CanCaptureEvidence, bool CanLinkAction, bool CanWriteMapping, string[] MediaKinds,
    string[] AvailableMediaKinds, string[] Fields, string[] RequiredFields);

internal static class AssuranceAccess
{
    private static bool Has(TrustedActor? a, string permission) => a?.CanRead == true && a.Permissions.Contains(permission);
    internal static bool Reader(TrustedActor? a) => Has(a, AssurancePolicy.ReadPermission);
    internal static bool Importer(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(AssurancePolicy.ImportPermission);
    internal static bool Acceptor(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(AssurancePolicy.AcceptPermission);
    internal static bool EvidenceWriter(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(AssurancePolicy.EvidencePermission) && a.Permissions.Contains("file.read");
    internal static bool Linker(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(AssurancePolicy.LinkPermission) && SafetyEventAccess.Reader(a);
    internal static bool MappingWriter(TrustedActor? a) => Reader(a) && a!.Permissions.Contains(AssurancePolicy.MappingPermission);
}

// S10 WP-A DEV-113 / T-ASS2-001 (route industry-assurance + audit-ready). SIRE 2.0 inspection intake on a SYNTHETIC
// column schema (the licensed OCIMF format is not present; AssurancePolicy.SourceVersion pins what is used). Import is
// staged with a field-level diff; only a different human accepts it, deciding every changed item (no blind overwrite).
// Findings stay OPEN/UNVERIFIED: the import never closes or verifies them. Offline photo evidence is a clean SecureFile
// pin replayed with its Idempotency-Key as the offline command id. Tanker-only: a non-tanker vessel is refused.
public static class IndustryAssuranceEndpoints
{
    public const string Notice = "Sentetik SIRE 2.0 şeması; OCIMF lisanslı biçimi değildir. İçe aktarma bulgu kapatmaz/doğrulamaz. / Synthetic SIRE 2.0 schema; not the licensed OCIMF format. Import never closes or verifies findings.";
    private static readonly string[] PhotoMimes = ["image/png", "image/jpeg"];
    private static readonly JsonSerializerOptions Json = AssuranceCommands.Json;

    public static void MapIndustryAssurance(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/industry-assurance").RequireAuthorization("DirectoryRead");
        g.MapGet("", Page).WithName("GetIndustryAssurance").Produces<IndustryAssurancePageDto>().Produces(400).Produces(401).Produces(403).Produces(404);
        g.MapGet("/inspections/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => Detail(h, db, id)).WithName("GetAssuranceInspection")
            .Produces<AssuranceInspectionDto>().Produces(400).Produces(401).Produces(403).Produces(404);
        g.MapPost("/mappings", (HttpContext h, PoseidonDbContext db) => AssuranceCommands.Run<AssuranceMappingCreateCommand>(h, db, "AssuranceCommand", "AssuranceMappingCreate", null, false,
                AssuranceAccess.MappingWriter, c => c.Id, CreateMapping, ReadMapping))
            .WithName("AssuranceMappingCreate").Accepts<AssuranceMappingCreateCommand>("application/json").Produces<AssuranceMappingDto>().Produces<AssuranceFailure>(422);
        g.MapPost("/imports", (HttpContext h, PoseidonDbContext db) => AssuranceCommands.Run<AssuranceImportStageCommand>(h, db, "AssuranceCommand", "AssuranceImportStage", null, true,
                AssuranceAccess.Importer, c => c.InspectionId, Stage, ReadInspection, maxBody: AssuranceCommands.ImportMaxBody))
            .WithName("AssuranceImportStage").Accepts<AssuranceImportStageCommand>("application/json").Produces<AssuranceInspectionDto>().Produces<AssuranceFailure>(422);
        g.MapPost("/imports/{id:guid}/decision", (Guid id, HttpContext h, PoseidonDbContext db) => AssuranceCommands.Run<AssuranceImportDecisionCommand>(h, db, "AssuranceCommand",
                "AssuranceImportDecide", id, true, AssuranceAccess.Acceptor, _ => id, Decide, ReadInspectionOfImport, humanOnly: true))
            .WithName("AssuranceImportDecide").Accepts<AssuranceImportDecisionCommand>("application/json").Produces<AssuranceInspectionDto>().Produces<AssuranceFailure>(422);
        g.MapPost("/inspections/{id:guid}/evidence", (Guid id, HttpContext h, PoseidonDbContext db) => AssuranceCommands.Run<AssuranceEvidenceCommand>(h, db, "AssuranceCommand",
                "AssuranceEvidenceCapture", id, true, AssuranceAccess.EvidenceWriter, _ => id, Capture, ReadInspection))
            .WithName("AssuranceEvidenceCapture").Accepts<AssuranceEvidenceCommand>("application/json").Produces<AssuranceInspectionDto>().Produces<AssuranceFailure>(422);
        g.MapPost("/findings/{id:guid}/action-link", (Guid id, HttpContext h, PoseidonDbContext db) => AssuranceCommands.Run<AssuranceFindingLinkCommand>(h, db, "AssuranceCommand",
                "AssuranceFindingLink", id, true, AssuranceAccess.Linker, _ => id, Link, ReadInspectionOfFinding, humanOnly: true))
            .WithName("AssuranceFindingLink").Accepts<AssuranceFindingLinkCommand>("application/json").Produces<AssuranceInspectionDto>().Produces<AssuranceFailure>(422);
    }

    internal static async Task<string> Classification(PoseidonDbContext db, Guid vessel, CancellationToken ct)
    {
        var kind = await db.Vessels.AsNoTracking().Where(v => v.Id == vessel).Select(v => (VesselKind?)v.Kind).SingleOrDefaultAsync(ct);
        if (kind == null) return AssurancePolicy.Unknown;
        if (kind == VesselKind.Tug) return AssurancePolicy.Classify(true, "Unknown", null);
        var profile = await db.RegulatoryDraftRevisions.AsNoTracking().Where(x => x.VesselId == vessel).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        if (profile == null) return AssurancePolicy.Unknown;
        var content = RegulatoryDraftStorage.Deserialize(profile.ContentJson);
        return AssurancePolicy.Classify(false, content.ShipType.State, content.ShipType.Value);
    }

    private static AssuranceColumnBinding[] Bindings(string json) => JsonSerializer.Deserialize<AssuranceColumnBinding[]>(json, Json) ?? [];
    private static AssuranceMappingDto MappingDto(AssuranceImportMapping m) => new(m.Id, m.Key, m.Version, m.SourceVersion, Bindings(m.BindingsJson), m.Hash, m.CreatedAt);
    private static AssuranceItemContent Item(string json) => JsonSerializer.Deserialize<AssuranceItemContent>(json, Json) ?? throw new InvalidDataException("assurance_item_unavailable");

    private static async Task<IResult> Page(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = SafetyEventCommands.Actor(h);
        if (!AssuranceAccess.Reader(a)) return await AssuranceCommands.Fail(h, "AssuranceRead", 403, "permission_denied");
        if (!SafetyEventCommands.Query(h, out var vessel)) return await AssuranceCommands.Fail(h, "AssuranceRead", 400, "invalid_query");
        if (!await SafetyEventCommands.Vessels(db, a!, vessel).AnyAsync(ct)) return await AssuranceCommands.Fail(h, "AssuranceRead", 404, "resource_not_found");
        var classification = await Classification(db, vessel, ct);
        var inspections = await db.AssuranceInspections.AsNoTracking().Where(x => x.VesselId == vessel).OrderByDescending(x => x.InspectionDate).ThenBy(x => x.Id).Take(100).ToArrayAsync(ct);
        var ids = inspections.Select(x => x.Id).ToArray();
        var itemCounts = await db.AssuranceInspectionItems.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.InspectionId))
            .GroupBy(x => x.InspectionId).Select(g => new { g.Key, Count = g.Select(i => i.QuestionRef).Distinct().Count() }).ToArrayAsync(ct);
        var open = await db.AssuranceFindings.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.InspectionId) && x.State == "OPEN")
            .GroupBy(x => x.InspectionId).Select(g => new { g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var pending = await db.AssuranceImports.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.InspectionId) && x.State == "PENDING_REVIEW").Select(x => x.InspectionId).ToArrayAsync(ct);
        var mappings = await db.AssuranceImportMappings.AsNoTracking().OrderBy(x => x.Key).ThenByDescending(x => x.Version).Take(50).ToArrayAsync(ct);
        var human = CertificateRenewalEndpoints.Human(h, a!);
        await SafetyEventCommands.Audit(h, "AssuranceRead", "Success", vessel);
        return Results.Json(new IndustryAssurancePageDto(vessel, classification, AssurancePolicy.Applicability(classification), AssurancePolicy.SourceVersion,
            inspections.Select(x => new AssuranceInspectionSummaryDto(x.Id, x.ReferenceNo, x.InspectionDate, x.State, x.Applicability,
                itemCounts.FirstOrDefault(c => c.Key == x.Id)?.Count ?? 0, open.FirstOrDefault(c => c.Key == x.Id)?.Count ?? 0, pending.Contains(x.Id))).ToArray(),
            mappings.Select(MappingDto).ToArray(),
            new(AssuranceAccess.Importer(a), AssuranceAccess.Acceptor(a) && human, AssuranceAccess.EvidenceWriter(a), AssuranceAccess.Linker(a) && human,
                AssuranceAccess.MappingWriter(a), AssurancePolicy.MediaKinds, ["PHOTO"], AssurancePolicy.Fields, AssurancePolicy.RequiredFields), Notice), Json);
    }

    private static async Task<IResult> Detail(HttpContext h, PoseidonDbContext db, Guid id)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = SafetyEventCommands.Actor(h);
        if (!AssuranceAccess.Reader(a)) return await AssuranceCommands.Fail(h, "AssuranceRead", 403, "permission_denied", id);
        if (!SafetyEventCommands.Query(h, out var vessel)) return await AssuranceCommands.Fail(h, "AssuranceRead", 400, "invalid_query", id);
        if (!await SafetyEventCommands.Vessels(db, a!, vessel).AnyAsync(ct)) return await AssuranceCommands.Fail(h, "AssuranceRead", 404, "resource_not_found", id);
        var dto = await Inspection(db, a!, vessel, id, ct);
        if (dto == null) return await AssuranceCommands.Fail(h, "AssuranceRead", 404, "resource_not_found", id);
        await SafetyEventCommands.Audit(h, "AssuranceRead", "Success", id);
        return Results.Json(dto, Json);
    }

    internal static async Task<AssuranceInspectionDto?> Inspection(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, CancellationToken ct)
    {
        var x = await db.AssuranceInspections.AsNoTracking().SingleOrDefaultAsync(i => i.VesselId == vessel && i.Id == id, ct);
        if (x == null) return null;
        var revisions = await db.AssuranceInspectionItems.AsNoTracking().Where(i => i.VesselId == vessel && i.InspectionId == id).ToArrayAsync(ct);
        var evidence = await db.AssuranceEvidence.AsNoTracking().Where(e => e.VesselId == vessel && e.InspectionId == id).OrderBy(e => e.CapturedAt).ThenBy(e => e.Id).Take(500).ToArrayAsync(ct);
        var items = revisions.GroupBy(r => r.QuestionRef).Select(g => g.OrderByDescending(r => r.Revision).First()).OrderBy(r => r.QuestionRef, StringComparer.Ordinal)
            .Select(r => new AssuranceItemDto(r.QuestionRef, r.Revision, Item(r.ContentJson), r.Hash, evidence.Count(e => e.QuestionRef == r.QuestionRef), r.SavedAt)).ToArray();
        var findings = await db.AssuranceFindings.AsNoTracking().Where(f => f.VesselId == vessel && f.InspectionId == id).OrderBy(f => f.QuestionRef).ToArrayAsync(ct);
        var imports = await db.AssuranceImports.AsNoTracking().Where(i => i.VesselId == vessel && i.InspectionId == id).OrderByDescending(i => i.ImportedAt).Take(20).ToArrayAsync(ct);
        return new(x.Id, x.VesselId, x.Programme, x.ReferenceNo, x.InspectionDate, x.State, x.Classification, x.Applicability, x.Revision, x.Provenance, AssurancePolicy.SourceVersion,
            items,
            findings.Select(f => new AssuranceFindingDto(f.Id, f.QuestionRef, f.FirstItemRevision, f.LatestItemRevision, f.ObservationPresent, f.State, f.Verification,
                f.ActionLinkState, SafetyEventAccess.Reader(a) ? f.ActionId : null, f.Revision, f.Version)).ToArray(),
            evidence.Select(e => new AssuranceEvidenceDto(e.Id, e.QuestionRef, e.FileId, e.FileRevision, e.MediaKind, e.CapturedAt, e.Origin, e.Verification,
                $"/api/v1/vessels/{vessel:D}/files/{e.FileId:D}")).ToArray(),
            imports.Select(i => {
                var diff = JsonSerializer.Deserialize<AssuranceDiffEntry[]>(i.DiffJson, Json) ?? [];
                return new AssuranceImportDto(i.Id, i.State, i.MappingVersion, i.SourceVersion, diff, i.DiffHash, i.ImportedBy == a.Id, i.ImportedAt, i.DecidedAt, i.Version,
                    diff.Count(d => d.Change == AssurancePolicy.New), diff.Count(d => d.Change == AssurancePolicy.Changed),
                    diff.Count(d => d.Change == AssurancePolicy.Unchanged), diff.Count(d => d.Change == AssurancePolicy.Absent));
            }).ToArray(), Notice);
    }

    private static async Task<object?> ReadInspection(PoseidonDbContext db, TrustedActor a, Guid? vessel, Guid id, HttpContext h, CancellationToken ct) =>
        await Inspection(db, a, vessel!.Value, id, ct);
    private static async Task<object?> ReadInspectionOfImport(PoseidonDbContext db, TrustedActor a, Guid? vessel, Guid id, HttpContext h, CancellationToken ct)
    {
        var inspection = await db.AssuranceImports.AsNoTracking().Where(i => i.VesselId == vessel && i.Id == id).Select(i => (Guid?)i.InspectionId).SingleOrDefaultAsync(ct);
        return inspection == null ? null : await Inspection(db, a, vessel!.Value, inspection.Value, ct);
    }
    private static async Task<object?> ReadInspectionOfFinding(PoseidonDbContext db, TrustedActor a, Guid? vessel, Guid id, HttpContext h, CancellationToken ct)
    {
        var inspection = await db.AssuranceFindings.AsNoTracking().Where(f => f.VesselId == vessel && f.Id == id).Select(f => (Guid?)f.InspectionId).SingleOrDefaultAsync(ct);
        return inspection == null ? null : await Inspection(db, a, vessel!.Value, inspection.Value, ct);
    }
    private static async Task<object?> ReadMapping(PoseidonDbContext db, TrustedActor a, Guid? vessel, Guid id, HttpContext h, CancellationToken ct)
    {
        var m = await db.AssuranceImportMappings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return m == null ? null : MappingDto(m);
    }

    // A mapping change is a new version of the same key; earlier versions stay pinned by the imports that used them.
    private static async Task<AssuranceOutcome> CreateMapping(AssuranceContext<AssuranceMappingCreateCommand> c)
    {
        var cmd = c.Command;
        if (cmd.Id == Guid.Empty || string.IsNullOrWhiteSpace(cmd.Key) || cmd.Key.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(cmd.Key, @"\A[a-z0-9][a-z0-9._-]{0,63}\z"))
            return AssuranceOutcome.Fail(400, "invalid_request");
        var issues = AssurancePolicy.MappingIssues(cmd.Bindings);
        if (issues.Length > 0) return AssuranceOutcome.Fail(422, "mapping_invalid", issues);
        if (await c.Db.AssuranceImportMappings.AnyAsync(x => x.Id == cmd.Id, c.Ct)) return AssuranceOutcome.Fail(409, "version_conflict");
        var version = (await c.Db.AssuranceImportMappings.Where(x => x.Key == cmd.Key).MaxAsync(x => (int?)x.Version, c.Ct) ?? 0) + 1;
        var bindings = cmd.Bindings.OrderBy(b => b.Field, StringComparer.Ordinal).ToArray();
        c.Db.AssuranceImportMappings.Add(new() { TenantId = c.Actor.TenantId, Id = cmd.Id, Key = cmd.Key, Version = version, BindingsJson = JsonSerializer.Serialize(bindings, Json),
            Hash = AssurancePolicy.MappingHash(version, bindings), CreatedBy = c.Actor.Id, CreatedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return AssuranceOutcome.Ok(cmd.Id, version);
    }

    private static Dictionary<string, (AssuranceItemContent Content, long Revision)> Current(IEnumerable<AssuranceInspectionItem> revisions) =>
        revisions.GroupBy(r => r.QuestionRef, StringComparer.Ordinal).Select(g => g.OrderByDescending(r => r.Revision).First())
            .ToDictionary(r => r.QuestionRef, r => (Item(r.ContentJson), r.Revision), StringComparer.Ordinal);

    private static async Task<AssuranceOutcome> Stage(AssuranceContext<AssuranceImportStageCommand> c)
    {
        var cmd = c.Command; var vessel = c.Vessel!.Value; var db = c.Db; var ct = c.Ct;
        if (cmd.Id == Guid.Empty || cmd.InspectionId == Guid.Empty || cmd.MappingId == Guid.Empty || (cmd.Table == null) == (cmd.Workbook == null))
            return AssuranceOutcome.Fail(400, "invalid_request");
        // Tanker-only applicability (09.01 paragraph:228-229): a non-tanker is refused; an undeclared type stays REVIEW_REQUIRED.
        var classification = await Classification(db, vessel, ct);
        if (classification == AssurancePolicy.NotTanker) return AssuranceOutcome.Fail(409, "not_applicable_vessel_type");
        var mapping = await db.AssuranceImportMappings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cmd.MappingId, ct);
        if (mapping == null) return AssuranceOutcome.Fail(422, "mapping_unavailable");
        ImportTable table;
        try
        {
            table = cmd.Table != null ? new ImportTable(cmd.Table.Headers, cmd.Table.Rows) : ImportWorkbook.Parse(Convert.FromBase64String(cmd.Workbook!));
            if (table.Headers.Length is 0 or > ImportWorkbook.MaxColumns || table.Rows.Length > ImportWorkbook.MaxRows ||
                table.Rows.Any(r => r == null || r.Length > ImportWorkbook.MaxColumns || r.Any(x => x is { Length: > ImportWorkbook.MaxCellCharacters })))
                return AssuranceOutcome.Fail(400, "invalid_request");
        }
        catch (Exception ex) when (ex is FormatException or ImportInputException) { return AssuranceOutcome.Fail(400, "invalid_request"); }
        var parsed = AssurancePolicy.Parse(Bindings(mapping.BindingsJson), table);
        if (parsed.Issues.Length > 0) return AssuranceOutcome.Fail(422, "import_rows_invalid", parsed.Issues.Take(100).Select(i => "row:" + i.Row + ":" + i.Code).ToArray());
        var inspection = await db.AssuranceInspections.SingleOrDefaultAsync(x => x.Id == cmd.InspectionId, ct);
        if (inspection == null)
        {
            if (string.IsNullOrWhiteSpace(cmd.ReferenceNo) || cmd.ReferenceNo.Length > 64 || cmd.ReferenceNo != cmd.ReferenceNo.Trim() || cmd.ReferenceNo.Any(char.IsControl) ||
                cmd.InspectionDate == null || cmd.InspectionDate > DateOnly.FromDateTime(c.Now.UtcDateTime))
                return AssuranceOutcome.Fail(422, "import_rows_invalid", ["inspection:REFERENCE_OR_DATE_INVALID"]);
            if (await db.AssuranceInspections.AnyAsync(x => x.VesselId == vessel && x.ReferenceNo == cmd.ReferenceNo, ct)) return AssuranceOutcome.Fail(409, "version_conflict");
            inspection = new AssuranceInspection { TenantId = c.Actor.TenantId, VesselId = vessel, Id = cmd.InspectionId, ReferenceNo = cmd.ReferenceNo, InspectionDate = cmd.InspectionDate.Value,
                Classification = classification, Applicability = AssurancePolicy.Applicability(classification), CreatedBy = c.Actor.Id, CreatedAt = c.Now, SavedAt = c.Now };
            db.AssuranceInspections.Add(inspection); await db.SaveChangesAsync(ct);
        }
        else if (inspection.VesselId != vessel) return AssuranceOutcome.Fail(404, "resource_not_found");
        else if (cmd.ReferenceNo != null && cmd.ReferenceNo != inspection.ReferenceNo || cmd.InspectionDate != null && cmd.InspectionDate != inspection.InspectionDate)
            return AssuranceOutcome.Fail(409, "immutable_context");
        if (await db.AssuranceImports.AnyAsync(x => x.InspectionId == inspection.Id && x.State == "PENDING_REVIEW", ct)) return AssuranceOutcome.Fail(409, "import_pending_exists");
        if (await db.AssuranceImports.AnyAsync(x => x.Id == cmd.Id, ct)) return AssuranceOutcome.Fail(409, "version_conflict");
        var current = Current(await db.AssuranceInspectionItems.AsNoTracking().Where(x => x.InspectionId == inspection.Id).ToArrayAsync(ct));
        var diff = AssurancePolicy.Diff(current, parsed.Items);
        db.AssuranceImports.Add(new() { TenantId = c.Actor.TenantId, VesselId = vessel, Id = cmd.Id, InspectionId = inspection.Id, MappingId = mapping.Id, MappingVersion = mapping.Version,
            MappingHash = mapping.Hash, TableHash = AssurancePolicy.Hash(JsonSerializer.Serialize(table, Json)), ItemsJson = JsonSerializer.Serialize(parsed.Items, Json),
            DiffJson = JsonSerializer.Serialize(diff, Json), DiffHash = AssurancePolicy.DiffHash(diff, parsed.Items), ImportedBy = c.Actor.Id, ImportedAt = c.Now });
        await db.SaveChangesAsync(ct);
        return AssuranceOutcome.Ok(inspection.Id, inspection.Revision);
    }

    // Human acceptance by someone other than the importer. Every CHANGED item is decided explicitly; a rejected change keeps
    // the accepted value. Findings are created/updated as OPEN + UNVERIFIED and never closed by an import.
    private static async Task<AssuranceOutcome> Decide(AssuranceContext<AssuranceImportDecisionCommand> c)
    {
        var cmd = c.Command; var vessel = c.Vessel!.Value; var db = c.Db; var ct = c.Ct;
        if (cmd.Decision is not ("ACCEPT" or "REJECT") || !SafetyEventPolicy.Text(cmd.Reason, 10, 2000) || cmd.AcceptChanged.Length + cmd.RejectChanged.Length > AssurancePolicy.MaxRows)
            return AssuranceOutcome.Fail(400, "invalid_request");
        var import = await db.AssuranceImports.SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == c.RouteId, ct);
        if (import == null) return AssuranceOutcome.Fail(404, "resource_not_found");
        if (import.Version != cmd.ExpectedVersion) return AssuranceOutcome.Fail(409, "version_conflict");
        if (import.State != "PENDING_REVIEW") return AssuranceOutcome.Fail(409, "invalid_state");
        if (!string.Equals(import.DiffHash, cmd.DiffHash, StringComparison.Ordinal)) return AssuranceOutcome.Fail(409, "diff_changed");
        if (import.ImportedBy == c.Actor.Id) return AssuranceOutcome.Fail(403, "separation_of_duties");
        var inspection = await db.AssuranceInspections.SingleAsync(x => x.Id == import.InspectionId, ct);
        var diff = JsonSerializer.Deserialize<AssuranceDiffEntry[]>(import.DiffJson, Json) ?? [];
        var incoming = JsonSerializer.Deserialize<AssuranceItemContent[]>(import.ItemsJson, Json) ?? [];
        // The accepted values must still be the ones the diff was computed against (a concurrent acceptance changes them).
        var current = Current(await db.AssuranceInspectionItems.AsNoTracking().Where(x => x.InspectionId == inspection.Id).ToArrayAsync(ct));
        if (AssurancePolicy.DiffHash(AssurancePolicy.Diff(current, incoming), incoming) != import.DiffHash) return AssuranceOutcome.Fail(409, "diff_changed");
        if (cmd.Decision == "ACCEPT")
        {
            var issues = AssurancePolicy.AcceptanceIssues(diff, cmd.AcceptChanged, cmd.RejectChanged);
            if (issues.Length > 0) return AssuranceOutcome.Fail(422, "import_decision_incomplete", issues);
            if (await Classification(db, vessel, ct) == AssurancePolicy.NotTanker) return AssuranceOutcome.Fail(409, "not_applicable_vessel_type");
        }
        else if (cmd.AcceptChanged.Length + cmd.RejectChanged.Length > 0) return AssuranceOutcome.Fail(400, "invalid_request");
        import.State = cmd.Decision == "ACCEPT" ? "ACCEPTED" : "REJECTED"; import.DecidedBy = c.Actor.Id; import.DecidedAt = c.Now; import.Reason = cmd.Reason;
        import.DecisionJson = JsonSerializer.Serialize(new { accept = cmd.AcceptChanged.Order(StringComparer.Ordinal), reject = cmd.RejectChanged.Order(StringComparer.Ordinal) }, Json);
        await db.SaveChangesAsync(ct);
        if (cmd.Decision == "REJECT") return AssuranceOutcome.Ok(import.Id, inspection.Revision);
        var applied = 0;
        var findings = await db.AssuranceFindings.Where(f => f.InspectionId == inspection.Id).ToArrayAsync(ct);
        foreach (var entry in diff.Where(d => d.Change == AssurancePolicy.New || d.Change == AssurancePolicy.Changed && cmd.AcceptChanged.Contains(d.QuestionRef)))
        {
            var item = incoming.Single(i => i.QuestionRef == entry.QuestionRef);
            var revision = (entry.CurrentRevision ?? 0) + 1;
            db.AssuranceInspectionItems.Add(new() { TenantId = inspection.TenantId, VesselId = vessel, InspectionId = inspection.Id, QuestionRef = item.QuestionRef, Revision = revision,
                ChangeKind = entry.Change, ContentJson = JsonSerializer.Serialize(item, Json), Hash = AssurancePolicy.ItemHash(item), ImportId = import.Id, AcceptedBy = c.Actor.Id, SavedAt = c.Now });
            var finding = findings.SingleOrDefault(f => f.QuestionRef == item.QuestionRef);
            if (finding != null) { finding.LatestItemRevision = revision; finding.ObservationPresent = AssurancePolicy.IsFinding(item); finding.Revision++; }
            else if (AssurancePolicy.IsFinding(item))
                db.AssuranceFindings.Add(new() { TenantId = inspection.TenantId, VesselId = vessel, Id = Guid.NewGuid(), InspectionId = inspection.Id, QuestionRef = item.QuestionRef,
                    FirstItemRevision = revision, LatestItemRevision = revision, CreatedAt = c.Now });
            applied++;
        }
        if (applied > 0) { await db.SaveChangesAsync(ct); inspection.Revision++; inspection.SavedAt = c.Now; await db.SaveChangesAsync(ct); }
        return AssuranceOutcome.Ok(import.Id, inspection.Revision);
    }

    // Field photo evidence for one inspection item. The Idempotency-Key is the offline command id, so a replayed capture
    // after reconnect returns the committed evidence instead of a duplicate. Video needs an operator-enabled SecureFile
    // video type (not configured): VIDEO is refused as media_type_unavailable, never stored as an unscanned file.
    private static async Task<AssuranceOutcome> Capture(AssuranceContext<AssuranceEvidenceCommand> c)
    {
        var cmd = c.Command; var vessel = c.Vessel!.Value; var db = c.Db; var ct = c.Ct;
        if (cmd.Id == Guid.Empty || !AssurancePolicy.MediaKinds.Contains(cmd.MediaKind) || !AssurancePolicy.Origins.Contains(cmd.Origin) || cmd.File == null ||
            cmd.File.Id == Guid.Empty || cmd.File.Revision <= 0 || !System.Text.RegularExpressions.Regex.IsMatch(cmd.File.Checksum ?? "", @"\A[A-Fa-f0-9]{64}\z") ||
            cmd.CapturedAt > c.Now.AddMinutes(5) || cmd.CapturedAt < c.Now.AddDays(-90))
            return AssuranceOutcome.Fail(400, "invalid_request");
        var inspection = await db.AssuranceInspections.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == c.RouteId, ct);
        if (inspection == null) return AssuranceOutcome.Fail(404, "resource_not_found");
        if (!await db.AssuranceInspectionItems.AnyAsync(x => x.InspectionId == inspection.Id && x.QuestionRef == cmd.QuestionRef, ct)) return AssuranceOutcome.Fail(422, "linked_resource_unavailable");
        if (cmd.MediaKind == "VIDEO") return AssuranceOutcome.Fail(422, "media_type_unavailable");
        if (!await PreSurveyChecklistEndpoints.FilesVisible(db, c.Actor, vessel, [cmd.File], ct)) return AssuranceOutcome.Fail(422, "linked_resource_unavailable");
        var mime = await db.StoredFiles.AsNoTracking().Where(f => f.Id == cmd.File.Id && f.VesselId == vessel).Select(f => f.Mime).SingleAsync(ct);
        if (!PhotoMimes.Contains(mime)) return AssuranceOutcome.Fail(422, "media_type_unavailable");
        if (await db.AssuranceEvidence.AnyAsync(x => x.Id == cmd.Id, ct)) return AssuranceOutcome.Fail(409, "version_conflict");
        db.AssuranceEvidence.Add(new() { TenantId = inspection.TenantId, VesselId = vessel, Id = cmd.Id, InspectionId = inspection.Id, QuestionRef = cmd.QuestionRef, FileId = cmd.File.Id,
            FileRevision = cmd.File.Revision, Checksum = cmd.File.Checksum!.ToUpperInvariant(), MediaKind = cmd.MediaKind, CapturedAt = cmd.CapturedAt > c.Now ? c.Now : cmd.CapturedAt,
            Origin = cmd.Origin, CreatedBy = c.Actor.Id, CreatedAt = c.Now });
        await db.SaveChangesAsync(ct);
        return AssuranceOutcome.Ok(inspection.Id, inspection.Revision);
    }

    // Finding -> corrective/preventive action trace (S9 WP-B DEV-061 SafetyAction). Linking is a human record; it does not
    // close or verify the finding. The action must exist on the same vessel and be readable by the linker.
    private static async Task<AssuranceOutcome> Link(AssuranceContext<AssuranceFindingLinkCommand> c)
    {
        var cmd = c.Command; var vessel = c.Vessel!.Value; var db = c.Db; var ct = c.Ct;
        if (cmd.ActionId == Guid.Empty) return AssuranceOutcome.Fail(400, "invalid_request");
        var finding = await db.AssuranceFindings.SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == c.RouteId, ct);
        if (finding == null) return AssuranceOutcome.Fail(404, "resource_not_found");
        if (finding.Version != cmd.ExpectedVersion) return AssuranceOutcome.Fail(409, "version_conflict");
        if (finding.ActionId != null) return AssuranceOutcome.Fail(409, "invalid_state");
        if (!await db.SafetyActions.AnyAsync(x => x.VesselId == vessel && x.Id == cmd.ActionId, ct)) return AssuranceOutcome.Fail(422, "linked_resource_unavailable");
        finding.ActionId = cmd.ActionId; finding.ActionLinkState = "LINKED"; finding.LinkedBy = c.Actor.Id; finding.LinkedAt = c.Now; finding.Revision++;
        await db.SaveChangesAsync(ct);
        return AssuranceOutcome.Ok(finding.Id, finding.Revision);
    }
}
