using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record ServiceReportLinkPin(Guid Id, long Revision);
public sealed record ServiceReportEvidencePin(Guid Id, long Revision, string Checksum);
public sealed record ServiceReportLinkDto(string Kind, Guid Id, long Revision, string Currency);
public sealed record ServiceReportEvidenceDto(Guid Id, long Revision, string Currency, string State);
public sealed record ServiceReportVerificationDto(Guid Id, long Revision, long ResultRevision, string Decision, Guid ActorId, string Reason, DateTimeOffset DecidedAt);
public sealed record ServiceReportRevisionSummaryDto(long Revision, string State, string ReasonCode, string Hash, Guid ActorId, DateTimeOffset SavedAt);
public sealed record ServiceReportDto(Guid Id, Guid VesselId, string ReportNo, long Revision, uint Version, string State, ServiceReportContent Content,
    ServiceReportLinkDto? Equipment, ServiceReportLinkDto? WorkOrder, ServiceReportLinkDto[] Defects, ServiceReportEvidenceDto[] Evidence,
    string[] Missing, string Hash, string VerificationState, long? VerifiedRevision, ServiceReportVerificationDto[] Verifications,
    ServiceReportRevisionSummaryDto[] Revisions, Guid[] Authors, bool CanEdit, bool CanSubmit, bool CanVerify, bool CanAmend, string Provenance, DateTimeOffset SavedAt);
public sealed record ServiceReportRevisionDto(Guid ReportId, Guid VesselId, long Revision, string State, string ReasonCode, ServiceReportContent Content,
    ServiceReportLinkPin? Equipment, ServiceReportLinkPin? WorkOrder, ServiceReportLinkPin[] Defects, ServiceReportEvidencePin[] Evidence, string Hash,
    Guid ActorId, DateTimeOffset SavedAt, string Provenance);
public sealed record ServiceReportListItemDto(Guid Id, string ReportNo, string? ServiceProvider, string? ServiceDate, string State, long Revision,
    Guid? AssetId, Guid? WorkOrderId, long? VerifiedRevision, DateTimeOffset SavedAt, string Provenance);
public sealed record ServiceReportPageDto(ServiceReportListItemDto[] Items, int Offset, int Limit, bool HasMore, bool CanWrite);
public sealed record ServiceReportCapabilitiesDto(Guid VesselId, bool CanRead, bool CanWrite, bool CanVerify, bool CanLinkWorkOrder, bool CanLinkDefect,
    bool CanAttachEvidence, string[] MeasurementResults);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceReportLinks(Guid? AssetId, Guid? WorkOrderId, [property: JsonRequired] Guid[] DefectIds, [property: JsonRequired] Guid[] EvidenceFileIds)
{ public override string ToString() => nameof(ServiceReportLinks); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceReportCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string ReportNo,
    [property: JsonRequired] ServiceReportContent Content, [property: JsonRequired] ServiceReportLinks Links);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceReportUpdateCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] ServiceReportContent Content,
    [property: JsonRequired] ServiceReportLinks Links);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceReportSubmitCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceReportVerifyCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceReportAmendCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string Reason);

// DEV-103 / T-PMS2-001. DRAFT -> SUBMITTED -> VERIFIED | RETURNED; an amendment of a verified report
// opens a new DRAFT revision while the verified revision stays readable (immutable history). The
// verifier is never an author of the cycle. Links pin exact current revisions of equipment, work
// order and defects in the same vessel; evidence pins accepted secure-file transports, which are
// NOT verified evidence. A verified report does not close a defect or a work order.
public static class ServiceReportEndpoints
{
    private const string ReadAction = "ServiceReportRead", CommandAction = "ServiceReportCommand";
    internal static bool CanRead(TrustedActor? a) => a?.CanRead == true && a.Permissions.Contains("service.report.read") && a.Permissions.Contains("equipment.read");
    internal static bool CanWrite(TrustedActor? a, Guid vessel) => CanRead(a) && a!.HasWriteAttribution && a.CanEditVessel(vessel) && a.Permissions.Contains("service.report.write");
    internal static bool CanVerify(TrustedActor? a, Guid vessel) => CanRead(a) && a!.HasWriteAttribution && a.CanEditVessel(vessel) && a.Permissions.Contains("service.report.verify");

    public static void MapServiceReports(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/service-reports").RequireAuthorization("DirectoryRead");
        g.MapGet("", (HttpContext h, PoseidonDbContext db) => List(h, db)).WithName("ServiceReportList").Produces<ServiceReportPageDto>();
        g.MapGet("/capabilities", (HttpContext h, PoseidonDbContext db) => Capabilities(h, db)).WithName("ServiceReportCapabilities").Produces<ServiceReportCapabilitiesDto>();
        g.MapGet("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => Detail(id, h, db)).WithName("ServiceReportRead").Produces<ServiceReportDto>();
        g.MapGet("/{id:guid}/revisions/{revision:long}", (Guid id, long revision, HttpContext h, PoseidonDbContext db) => RevisionRead(id, revision, h, db))
            .WithName("ServiceReportRevisionRead").Produces<ServiceReportRevisionDto>();
        g.MapPost("", (HttpContext h, PoseidonDbContext db) => TechnicalRecordCommands.Run<ServiceReportCreateCommand>(h, db, "ServiceReportCreate", CommandAction, null, null,
                CanWrite, c => c.Id, Create, Fresh))
            .WithName("ServiceReportCreate").Accepts<ServiceReportCreateCommand>("application/json").Produces<ServiceReportDto>();
        g.MapPut("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => TechnicalRecordCommands.Run<ServiceReportUpdateCommand>(h, db, "ServiceReportUpdate", CommandAction, id, null,
                CanWrite, _ => id, Update, Fresh))
            .WithName("ServiceReportUpdate").Accepts<ServiceReportUpdateCommand>("application/json").Produces<ServiceReportDto>();
        g.MapPost("/{id:guid}/submit", (Guid id, HttpContext h, PoseidonDbContext db) => TechnicalRecordCommands.Run<ServiceReportSubmitCommand>(h, db, "ServiceReportSubmit", CommandAction, id, null,
                CanWrite, _ => id, Submit, Fresh))
            .WithName("ServiceReportSubmit").Accepts<ServiceReportSubmitCommand>("application/json").Produces<ServiceReportDto>().Produces<TechnicalRecordFailure>(422);
        g.MapPost("/{id:guid}/verify", (Guid id, HttpContext h, PoseidonDbContext db) => TechnicalRecordCommands.Run<ServiceReportVerifyCommand>(h, db, "ServiceReportVerify", CommandAction, id, null,
                CanVerify, _ => id, Verify, Fresh))
            .WithName("ServiceReportVerify").Accepts<ServiceReportVerifyCommand>("application/json").Produces<ServiceReportDto>();
        g.MapPost("/{id:guid}/amend", (Guid id, HttpContext h, PoseidonDbContext db) => TechnicalRecordCommands.Run<ServiceReportAmendCommand>(h, db, "ServiceReportAmend", CommandAction, id, null,
                CanWrite, _ => id, Amend, Fresh))
            .WithName("ServiceReportAmend").Accepts<ServiceReportAmendCommand>("application/json").Produces<ServiceReportDto>();
    }

    private static readonly JsonSerializerOptions Json = TechnicalRecordCommands.Json;
    private static ServiceReportContent Content(string json) => JsonSerializer.Deserialize<ServiceReportContent>(json, Json) ?? throw new InvalidDataException("service_report_content_unavailable");
    private static ServiceReportLinkPin[] Pins(string json) => JsonSerializer.Deserialize<ServiceReportLinkPin[]>(json, Json) ?? [];
    private static ServiceReportEvidencePin[] Evidence(string json) => JsonSerializer.Deserialize<ServiceReportEvidencePin[]>(json, Json) ?? [];
    private static string Hash(ServiceReport r) => RegisterStorage.Hash(JsonSerializer.Serialize(new { r.VesselId, r.Id, r.ReportNo, r.Revision, r.State,
        content = r.ContentJson, r.AssetId, r.AssetRevision, r.WorkOrderId, r.WorkOrderRevision, defects = r.DefectLinksJson, evidence = r.EvidenceJson }, Json));
    private static bool Reason(string? value) => value != null && value.Trim().Length >= ServiceReportPolicy.MinReason && value.Length <= 1000 &&
        !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    private static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    private static void Append(PoseidonDbContext db, ServiceReport r, string reason, DateTimeOffset now) =>
        db.ServiceReportRevisions.Add(new() { TenantId = r.TenantId, VesselId = r.VesselId, ReportId = r.Id, Revision = r.Revision, State = r.State, ReasonCode = reason,
            ContentJson = r.ContentJson, AssetId = r.AssetId, AssetRevision = r.AssetRevision, WorkOrderId = r.WorkOrderId, WorkOrderRevision = r.WorkOrderRevision,
            DefectLinksJson = r.DefectLinksJson, EvidenceJson = r.EvidenceJson, Hash = r.Hash, ActorId = r.ActorId, SavedAt = now, Provenance = r.Provenance });
    private static void Stamp(ServiceReport r, Guid actor, DateTimeOffset now) { r.ActorId = actor; r.SavedAt = now; r.Hash = Hash(r); }

    // Pins exact current revisions visible to the actor in the same vessel. An unchanged link keeps
    // its original pin (no silent re-pin); a changed or new link is pinned at its current head.
    private static async Task<bool> Link(PoseidonDbContext db, TrustedActor actor, Guid vessel, ServiceReportLinks? links, ServiceReport row, CancellationToken ct)
    {
        if (links == null || links.DefectIds == null || links.EvidenceFileIds == null || links.DefectIds.Length > ServiceReportPolicy.MaxLinks ||
            links.EvidenceFileIds.Length > ServiceReportPolicy.MaxLinks || links.DefectIds.Distinct().Count() != links.DefectIds.Length ||
            links.EvidenceFileIds.Distinct().Count() != links.EvidenceFileIds.Length || links.DefectIds.Contains(Guid.Empty) || links.EvidenceFileIds.Contains(Guid.Empty) ||
            links.AssetId == Guid.Empty || links.WorkOrderId == Guid.Empty) return false;
        if (links.AssetId != row.AssetId)
        {
            if (links.AssetId == null) { row.AssetId = null; row.AssetRevision = null; }
            else
            {
                var revision = await db.Assets.AsNoTracking().Where(a => a.VesselId == vessel && a.Id == links.AssetId).Select(a => (long?)a.Revision).SingleOrDefaultAsync(ct);
                if (revision is not > 0) return false;
                row.AssetId = links.AssetId; row.AssetRevision = revision;
            }
        }
        if (links.WorkOrderId != row.WorkOrderId)
        {
            if (links.WorkOrderId == null) { row.WorkOrderId = null; row.WorkOrderRevision = null; }
            else
            {
                if (!WorkOrderEndpoints.CanRead(actor)) return false;
                var revision = await db.WorkOrders.AsNoTracking().Where(w => w.VesselId == vessel && w.Id == links.WorkOrderId).Select(w => (long?)w.Revision).SingleOrDefaultAsync(ct);
                if (revision is not > 0) return false;
                row.WorkOrderId = links.WorkOrderId; row.WorkOrderRevision = revision;
            }
        }
        var existingDefects = Pins(row.DefectLinksJson); var defects = new List<ServiceReportLinkPin>();
        foreach (var id in links.DefectIds)
        {
            var kept = existingDefects.SingleOrDefault(p => p.Id == id);
            if (kept != null) { defects.Add(kept); continue; }
            if (!DefectEndpoints.CanRead(actor)) return false;
            var revision = await db.Defects.AsNoTracking().Where(d => d.VesselId == vessel && d.Id == id).Select(d => (long?)d.Revision).SingleOrDefaultAsync(ct);
            if (revision is not > 0) return false;
            defects.Add(new(id, revision.Value));
        }
        var existingEvidence = Evidence(row.EvidenceJson); var evidence = new List<ServiceReportEvidencePin>();
        foreach (var id in links.EvidenceFileIds)
        {
            var kept = existingEvidence.SingleOrDefault(p => p.Id == id);
            if (kept != null) { evidence.Add(kept); continue; }
            if (!actor.Permissions.Contains("file.read")) return false;
            var file = await db.StoredFiles.AsNoTracking().Where(f => f.Id == id && f.VesselId == vessel && db.FileStages.Any(s => s.Id == f.Id && s.VesselId == vessel &&
                s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport")).Select(f => new { f.Revision, f.Checksum }).SingleOrDefaultAsync(ct);
            if (file == null) return false;
            evidence.Add(new(id, file.Revision, file.Checksum));
        }
        row.DefectLinksJson = JsonSerializer.Serialize(defects, Json); row.EvidenceJson = JsonSerializer.Serialize(evidence, Json);
        return true;
    }

    private static async Task<TechnicalOutcome> Create(TechnicalContext<ServiceReportCreateCommand> c)
    {
        var cmd = c.Command; var db = c.Db;
        if (cmd.Id == Guid.Empty || string.IsNullOrWhiteSpace(cmd.ReportNo) || cmd.ReportNo.Length > 50 || cmd.ReportNo.Any(char.IsControl) || !ServiceReportPolicy.Valid(cmd.Content))
            return TechnicalOutcome.Fail(400, "invalid_request");
        if (await db.ServiceReports.AnyAsync(r => r.Id == cmd.Id, c.Ct)) return TechnicalOutcome.Fail(409, "version_conflict");
        if (await db.ServiceReports.AnyAsync(r => r.ReportNo == cmd.ReportNo, c.Ct)) return TechnicalOutcome.Fail(409, "duplicate_code");
        var row = new ServiceReport { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, ReportNo = cmd.ReportNo, Revision = 1, State = ServiceReportPolicy.Draft,
            ContentJson = JsonSerializer.Serialize(cmd.Content, Json), AuthorsJson = TechnicalRecordCommands.WithAuthor("[]", c.Actor.Id), CreatedBy = c.Actor.Id };
        if (!await Link(db, c.Actor, c.Vessel, cmd.Links, row, c.Ct)) return TechnicalOutcome.Fail(400, "linked_resource_unavailable");
        Stamp(row, c.Actor.Id, c.Now); db.ServiceReports.Add(row); await db.SaveChangesAsync(c.Ct);
        Append(db, row, "CREATE", c.Now); await db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    private static Task<ServiceReport?> Head<T>(TechnicalContext<T> c) => c.Db.ServiceReports.SingleOrDefaultAsync(r => r.VesselId == c.Vessel && r.Id == c.RouteId, c.Ct);

    private static async Task<TechnicalOutcome> Update(TechnicalContext<ServiceReportUpdateCommand> c)
    {
        var cmd = c.Command;
        if (!ServiceReportPolicy.Valid(cmd.Content)) return TechnicalOutcome.Fail(400, "invalid_request");
        var row = await Head(c); if (row == null) return TechnicalOutcome.Fail(404, "resource_not_found");
        if (row.Version != cmd.ExpectedVersion) return TechnicalOutcome.Fail(409, "version_conflict");
        if (!ServiceReportPolicy.CanEdit(row.State)) return TechnicalOutcome.Fail(409, "invalid_state");
        if (!await Link(c.Db, c.Actor, c.Vessel, cmd.Links, row, c.Ct)) return TechnicalOutcome.Fail(400, "linked_resource_unavailable");
        row.Revision++; row.State = ServiceReportPolicy.Draft; row.ContentJson = JsonSerializer.Serialize(cmd.Content, Json);
        row.AuthorsJson = TechnicalRecordCommands.WithAuthor(row.AuthorsJson, c.Actor.Id); Stamp(row, c.Actor.Id, c.Now);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "EDIT", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<TechnicalOutcome> Submit(TechnicalContext<ServiceReportSubmitCommand> c)
    {
        var row = await Head(c); if (row == null) return TechnicalOutcome.Fail(404, "resource_not_found");
        if (row.Version != c.Command.ExpectedVersion || row.Hash != c.Command.ContentHash) return TechnicalOutcome.Fail(409, "version_conflict");
        if (!ServiceReportPolicy.CanEdit(row.State)) return TechnicalOutcome.Fail(409, "invalid_state");
        var missing = ServiceReportPolicy.Missing(Content(row.ContentJson), row.AssetId != null, Today(c.Now));
        if (missing.Length > 0) return TechnicalOutcome.Fail(422, "service_report_incomplete", missing);
        row.Revision++; row.State = ServiceReportPolicy.Submitted; row.SubmittedBy = c.Actor.Id;
        row.AuthorsJson = TechnicalRecordCommands.WithAuthor(row.AuthorsJson, c.Actor.Id); Stamp(row, c.Actor.Id, c.Now);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "SUBMIT", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<TechnicalOutcome> Verify(TechnicalContext<ServiceReportVerifyCommand> c)
    {
        var cmd = c.Command;
        if (!ServiceReportPolicy.Decisions.Contains(cmd.Decision, StringComparer.Ordinal) || !Reason(cmd.Reason)) return TechnicalOutcome.Fail(400, "invalid_request");
        var row = await Head(c); if (row == null) return TechnicalOutcome.Fail(404, "resource_not_found");
        if (row.Version != cmd.ExpectedVersion || row.Hash != cmd.ContentHash) return TechnicalOutcome.Fail(409, "version_conflict");
        if (row.State != ServiceReportPolicy.Submitted) return TechnicalOutcome.Fail(409, "invalid_state");
        // Separation of duties: nobody who edited or submitted this cycle verifies it.
        if (TechnicalRecordCommands.AuthorIds(row.AuthorsJson).Contains(c.Actor.Id) || row.SubmittedBy == c.Actor.Id)
            return TechnicalOutcome.Fail(403, "separation_of_duties");
        var submitted = row.Revision; var hash = row.Hash;
        row.Revision++; row.State = cmd.Decision == "VERIFY" ? ServiceReportPolicy.Verified : ServiceReportPolicy.Returned;
        if (cmd.Decision == "VERIFY") row.VerifiedRevision = row.Revision;
        Stamp(row, c.Actor.Id, c.Now);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, cmd.Decision, c.Now); await c.Db.SaveChangesAsync(c.Ct);
        c.Db.ServiceReportVerifications.Add(new() { TenantId = row.TenantId, VesselId = row.VesselId, Id = Guid.NewGuid(), ReportId = row.Id, Revision = submitted,
            ResultRevision = row.Revision, Decision = cmd.Decision, ContentHash = hash, ActorId = c.Actor.Id, Reason = cmd.Reason, DecidedAt = c.Now });
        await c.Db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<TechnicalOutcome> Amend(TechnicalContext<ServiceReportAmendCommand> c)
    {
        if (!Reason(c.Command.Reason)) return TechnicalOutcome.Fail(400, "invalid_request");
        var row = await Head(c); if (row == null) return TechnicalOutcome.Fail(404, "resource_not_found");
        if (row.Version != c.Command.ExpectedVersion) return TechnicalOutcome.Fail(409, "version_conflict");
        if (row.State != ServiceReportPolicy.Verified) return TechnicalOutcome.Fail(409, "invalid_state");
        // The verified revision stays in history; the new cycle starts with a fresh author set.
        row.Revision++; row.State = ServiceReportPolicy.Draft; row.SubmittedBy = null;
        row.AuthorsJson = TechnicalRecordCommands.WithAuthor("[]", c.Actor.Id); Stamp(row, c.Actor.Id, c.Now);
        await c.Db.SaveChangesAsync(c.Ct); Append(c.Db, row, "AMEND", c.Now); await c.Db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<object?> Fresh(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid id, DateTimeOffset now, CancellationToken ct) =>
        await Describe(db, actor, vessel, id, now, ct);

    internal static async Task<ServiceReportDto?> Describe(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var r = await db.ServiceReports.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
        if (r == null) return null;
        var content = Content(r.ContentJson);
        async Task<string> Currency(string kind, Guid target, long revision)
        {
            long? head = kind switch
            {
                "Equipment" => await db.Assets.AsNoTracking().Where(a => a.VesselId == vessel && a.Id == target).Select(a => (long?)a.Revision).SingleOrDefaultAsync(ct),
                "WorkOrder" => WorkOrderEndpoints.CanRead(actor) ? await db.WorkOrders.AsNoTracking().Where(w => w.VesselId == vessel && w.Id == target).Select(w => (long?)w.Revision).SingleOrDefaultAsync(ct) : null,
                _ => DefectEndpoints.CanRead(actor) ? await db.Defects.AsNoTracking().Where(d => d.VesselId == vessel && d.Id == target).Select(d => (long?)d.Revision).SingleOrDefaultAsync(ct) : null
            };
            return head == null ? "UNAVAILABLE" : head == revision ? "CURRENT" : "STALE";
        }
        ServiceReportLinkDto? equipment = r.AssetId == null ? null : new("Equipment", r.AssetId.Value, r.AssetRevision!.Value, await Currency("Equipment", r.AssetId.Value, r.AssetRevision.Value));
        ServiceReportLinkDto? workOrder = r.WorkOrderId == null ? null : new("WorkOrder", r.WorkOrderId.Value, r.WorkOrderRevision!.Value, await Currency("WorkOrder", r.WorkOrderId.Value, r.WorkOrderRevision.Value));
        var defects = new List<ServiceReportLinkDto>();
        foreach (var p in Pins(r.DefectLinksJson)) defects.Add(new("Defect", p.Id, p.Revision, await Currency("Defect", p.Id, p.Revision)));
        var evidence = new List<ServiceReportEvidenceDto>();
        foreach (var p in Evidence(r.EvidenceJson))
        {
            var current = actor.Permissions.Contains("file.read") && await db.StoredFiles.AsNoTracking().AnyAsync(f => f.Id == p.Id && f.VesselId == vessel && f.Revision == p.Revision && f.Checksum == p.Checksum, ct);
            evidence.Add(new(p.Id, p.Revision, current ? "CURRENT" : "UNAVAILABLE", "TRANSPORT_ACCEPTED_UNVERIFIED"));
        }
        var verifications = await db.ServiceReportVerifications.AsNoTracking().Where(v => v.VesselId == vessel && v.ReportId == id).OrderBy(v => v.ResultRevision)
            .Select(v => new ServiceReportVerificationDto(v.Id, v.Revision, v.ResultRevision, v.Decision, v.ActorId, v.Reason, v.DecidedAt)).ToArrayAsync(ct);
        var revisions = await db.ServiceReportRevisions.AsNoTracking().Where(v => v.VesselId == vessel && v.ReportId == id).OrderByDescending(v => v.Revision).Take(50)
            .Select(v => new ServiceReportRevisionSummaryDto(v.Revision, v.State, v.ReasonCode, v.Hash, v.ActorId, v.SavedAt)).ToArrayAsync(ct);
        var missing = ServiceReportPolicy.Missing(content, r.AssetId != null, Today(now));
        var authors = TechnicalRecordCommands.AuthorIds(r.AuthorsJson);
        var verification = r.State == ServiceReportPolicy.Verified ? "VERIFIED" : r.VerifiedRevision != null ? "EARLIER_REVISION_VERIFIED" :
            r.State == ServiceReportPolicy.Submitted ? "PENDING_VERIFICATION" : r.State == ServiceReportPolicy.Returned ? "RETURNED" : "NOT_SUBMITTED";
        var editable = ServiceReportPolicy.CanEdit(r.State) && CanWrite(actor, vessel);
        var canVerify = r.State == ServiceReportPolicy.Submitted && CanVerify(actor, vessel) && !authors.Contains(actor.Id) && r.SubmittedBy != actor.Id;
        return new(r.Id, r.VesselId, r.ReportNo, r.Revision, r.Version, r.State, content, equipment, workOrder, [.. defects], [.. evidence], missing, r.Hash,
            verification, r.VerifiedRevision, verifications, revisions, authors, editable, editable && missing.Length == 0, canVerify,
            r.State == ServiceReportPolicy.Verified && CanWrite(actor, vessel), r.Provenance, r.SavedAt);
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead, "offset", "assetId", "workOrderId");
        if (failure != null) return failure;
        var offset = 0; Guid? asset = null, workOrder = null;
        if (h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 100000))
            return await TechnicalRecordCommands.Fail(h, ReadAction, 400, "invalid_query");
        if (h.Request.Query.ContainsKey("assetId")) { if (!Guid.TryParseExact(h.Request.Query["assetId"], "D", out var a)) return await TechnicalRecordCommands.Fail(h, ReadAction, 400, "invalid_query"); asset = a; }
        if (h.Request.Query.ContainsKey("workOrderId")) { if (!Guid.TryParseExact(h.Request.Query["workOrderId"], "D", out var w)) return await TechnicalRecordCommands.Fail(h, ReadAction, 400, "invalid_query"); workOrder = w; }
        var rows = await db.ServiceReports.AsNoTracking().Where(r => r.VesselId == vessel && (asset == null || r.AssetId == asset) && (workOrder == null || r.WorkOrderId == workOrder))
            .OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        var items = rows.Take(25).Select(r => { var content = Content(r.ContentJson); return new ServiceReportListItemDto(r.Id, r.ReportNo, content.ServiceProvider,
            content.ServiceDate, r.State, r.Revision, r.AssetId, r.WorkOrderId, r.VerifiedRevision, r.SavedAt, r.Provenance); }).ToArray();
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", vessel);
        return Results.Json(new ServiceReportPageDto(items, offset, 25, rows.Length > 25, CanWrite(actor, vessel)), Json);
    }

    private static async Task<IResult> Capabilities(HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead); if (failure != null) return failure;
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", vessel);
        return Results.Json(new ServiceReportCapabilitiesDto(vessel, true, CanWrite(actor, vessel), CanVerify(actor, vessel), WorkOrderEndpoints.CanRead(actor),
            DefectEndpoints.CanRead(actor), actor!.Permissions.Contains("file.read"), ServiceReportPolicy.MeasurementResults), Json);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead); if (failure != null) return failure;
        var dto = await Describe(db, actor!, vessel, id, TechnicalRecordCommands.Now(h), h.RequestAborted);
        if (dto == null) return await TechnicalRecordCommands.Fail(h, ReadAction, 404, "resource_not_found", id);
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", id);
        return Results.Json(dto, Json);
    }

    private static async Task<IResult> RevisionRead(Guid id, long revision, HttpContext h, PoseidonDbContext db)
    {
        var (_, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead); if (failure != null) return failure;
        var r = await db.ServiceReportRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.ReportId == id && x.Revision == revision, h.RequestAborted);
        if (r == null) return await TechnicalRecordCommands.Fail(h, ReadAction, 404, "resource_not_found", id);
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", id);
        return Results.Json(new ServiceReportRevisionDto(r.ReportId, r.VesselId, r.Revision, r.State, r.ReasonCode, Content(r.ContentJson),
            r.AssetId == null ? null : new(r.AssetId.Value, r.AssetRevision!.Value), r.WorkOrderId == null ? null : new(r.WorkOrderId.Value, r.WorkOrderRevision!.Value),
            Pins(r.DefectLinksJson), Evidence(r.EvidenceJson), r.Hash, r.ActorId, r.SavedAt, r.Provenance), Json);
    }
}
