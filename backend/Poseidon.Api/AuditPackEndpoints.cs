using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AuditPackCreateCommand([property: JsonRequired] Guid Id, Guid? PreviousPackId = null)
{ public override string ToString() => nameof(AuditPackCreateCommand); }
public sealed record AuditPackItemDto(int Ordinal, string Kind, Guid ResourceId, long Revision, string State, string? Hash, Guid EvidenceId, Guid ControlId,
    DateTimeOffset? SavedAt, string Integrity, string? FileHref, string? HistoryHref);
public sealed record AuditPackDto(Guid Id, Guid VesselId, string Label, string Notice, string SourceVersion, DateTimeOffset AsOf, DateTimeOffset CreatedAt,
    bool CreatedByMe, string ManifestHash, int ItemCount, int Included, int Excluded, int UnavailableChains, bool Truncated, Guid? PreviousPackId,
    int Superseded, AuditPackItemDto[] Items);
public sealed record AuditPackSummaryDto(Guid Id, DateTimeOffset AsOf, DateTimeOffset CreatedAt, bool CreatedByMe, int ItemCount, string ManifestHash, Guid? PreviousPackId);
public sealed record AuditPackListDto(Guid VesselId, string Label, string Notice, bool CanCompile, AuditPackSummaryDto[] Items, bool HasMore);
public sealed record AuditPackExportDto(string Schema, string Label, string Notice, DateTimeOffset ExportedAt, AuditPackDto Pack);

// S10 WP-A DEV-064 / T-S10-001 (18.01 Backlog row:65). Builds on the DEV-032 audit-ready chain reader
// (AuditReadyEndpoints.Readers: exact human trace reader appointments). A pack is an immutable SYSTEM COMPILATION,
// NOT AN OFFICIAL RECORD. Every read re-authorizes each pinned item for the CURRENT reader; an item outside the
// reader's appointments is only counted as Excluded. Binaries are never embedded: an evidence file is a live link
// to the SecureFile endpoint, which re-checks clean status and authorization on its own request.
public static class AuditPackEndpoints
{
    public const string Notice = "Sistem derlemesi; resmî kayıt değildir. / System compilation; not an official record.";
    public const int MaxList = 50;

    public static void MapAuditPacks(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/audit-ready/packs").RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("ListAuditPacks").Produces<AuditPackListDto>().Produces(400).Produces(401).Produces(403).Produces(404);
        g.MapPost("", (HttpContext h, PoseidonDbContext db) => AssuranceCommands.Run<AuditPackCreateCommand>(h, db, "AuditPackCommand", "AuditPackCreate", null, true,
                AuditReadyEndpoints.CanRead, c => c.Id, Compile, Read, humanOnly: true))
            .WithName("AuditPackCreate").Accepts<AuditPackCreateCommand>("application/json").Produces<AuditPackDto>().Produces<AssuranceFailure>(422);
        g.MapGet("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => View(h, db, id, false)).WithName("GetAuditPack")
            .Produces<AuditPackDto>().Produces(400).Produces(401).Produces(403).Produces(404);
        g.MapGet("/{id:guid}/export", (Guid id, HttpContext h, PoseidonDbContext db) => View(h, db, id, true)).WithName("ExportAuditPack")
            .Produces<AuditPackExportDto>().Produces(400).Produces(401).Produces(403).Produces(404);
    }

    private static HumanTraceReader[] Readers(HttpContext h, TrustedActor a, Guid vessel) =>
        AuditReadyEndpoints.Readers(h.RequestServices.GetRequiredService<FederationSettings>().Memberships, a, vessel, DateTimeOffset.UtcNow);

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = SafetyEventCommands.Actor(h);
        if (a == null || !AuditReadyEndpoints.CanRead(a)) return await AssuranceCommands.Fail(h, "AuditPackRead", 403, "permission_denied");
        if (!SafetyEventCommands.Query(h, out var vessel)) return await AssuranceCommands.Fail(h, "AuditPackRead", 400, "invalid_query");
        if (!await SafetyEventCommands.Vessels(db, a, vessel).AnyAsync(ct)) return await AssuranceCommands.Fail(h, "AuditPackRead", 404, "resource_not_found");
        var readers = Readers(h, a, vessel);
        // Without a current appointment for this vessel there is nothing to list (no pack metadata either).
        var rows = readers.Length == 0 ? [] : await db.AuditPackManifests.AsNoTracking().Where(x => x.VesselId == vessel)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(MaxList + 1).ToArrayAsync(ct);
        await SafetyEventCommands.Audit(h, "AuditPackRead", "Success", vessel);
        return Results.Json(new AuditPackListDto(vessel, AuditPackPolicy.Label, Notice, readers.Length > 0 && CertificateRenewalEndpoints.Human(h, a),
            rows.Take(MaxList).Select(x => new AuditPackSummaryDto(x.Id, x.AsOf, x.CreatedAt, x.CreatedBy == a.Id, x.ItemCount, x.ManifestHash, x.PreviousPackId)).ToArray(),
            rows.Length > MaxList), AssuranceCommands.Json);
    }

    private static string HistoryHash(ControlStateEvent e) => AuditPackPolicy.Hash(string.Join("|", "CONTROL_STATE_EVENT_V1", e.ControlId.ToString("D"),
        e.Revision.ToString(CultureInfo.InvariantCulture), e.ControlRevision.ToString(CultureInfo.InvariantCulture), e.FromState, e.ToState, e.ReasonCode,
        e.SavedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)));

    // Compiles the pinned item set the creating reader can see right now. Nothing is copied except ids, revisions,
    // states, hashes and timestamps; source text, rationales and binaries stay in their owning records.
    private static async Task<AssuranceOutcome> Compile(AssuranceContext<AuditPackCreateCommand> c)
    {
        var vessel = c.Vessel!.Value; var a = c.Actor; var db = c.Db; var ct = c.Ct; var now = c.Now;
        if (c.Command.Id == Guid.Empty) return AssuranceOutcome.Fail(400, "invalid_request");
        if (await db.AuditPackManifests.AnyAsync(x => x.Id == c.Command.Id, ct)) return AssuranceOutcome.Fail(409, "version_conflict");
        if (c.Command.PreviousPackId is { } previous && !await db.AuditPackManifests.AnyAsync(x => x.Id == previous && x.VesselId == vessel, ct))
            return AssuranceOutcome.Fail(422, "linked_resource_unavailable");
        var readers = Readers(c.Http, a, vessel);
        if (readers.Length == 0) return AssuranceOutcome.Fail(422, "pack_scope_empty");
        var entries = new List<AuditPackEntry>(); var unavailable = 0;
        var pairs = readers.Select(g => (g.EvidenceId, g.ControlId)).Distinct().ToArray();
        foreach (var (evidence, control) in pairs.Take(AuditReadyEndpoints.MaxChains))
        {
            var head = await db.ControlEvidenceHeads.AsNoTracking().Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == control)
                .Select(x => new { x.ControlState, x.PublishedRevision }).SingleOrDefaultAsync(ct);
            var edge = head == null || head.ControlState == "RETIRED" ? null : await db.ControlEvidenceEdges.AsNoTracking()
                .Where(x => x.VesselId == vessel && x.Kind == "Evidence" && x.Id == evidence && x.TargetKind == "Control" && x.TargetId == control &&
                    (head.PublishedRevision == null || x.TargetRevision == head.PublishedRevision))
                .OrderByDescending(x => x.Revision).ThenByDescending(x => x.TargetRevision).FirstOrDefaultAsync(ct);
            if (edge == null) { unavailable++; continue; }
            var ev = await HumanReviewEndpoints.Visible(db, vessel, a, true).SingleOrDefaultAsync(x => x.Kind == "Evidence" && x.Id == evidence && x.Revision == edge.Revision && x.SavedAt <= now, ct);
            var co = await HumanReviewEndpoints.Visible(db, vessel, a, true).SingleOrDefaultAsync(x => x.Kind == "Control" && x.Id == control && x.Revision == edge.TargetRevision && x.SavedAt <= now, ct);
            if (ev == null || co == null) { unavailable++; continue; }
            entries.Add(new("Control", co.Id, co.Revision, "Current", co.Hash, evidence, control, co.SavedAt));
            entries.Add(new("Evidence", ev.Id, ev.Revision, "Current", ev.Hash, evidence, control, ev.SavedAt, ev.FileId, ev.FileId == null ? null : ev.FileRevision));
            // Superseded evidence revisions stay in the trace (never deleted, never presented as current).
            var older = await HumanReviewEndpoints.Visible(db, vessel, a, true).Where(x => x.Kind == "Evidence" && x.Id == evidence && x.Revision < ev.Revision && x.SavedAt <= now)
                .OrderByDescending(x => x.Revision).Take(AuditPackPolicy.MaxSupersededPerEvidence).ToArrayAsync(ct);
            entries.AddRange(older.Select(o => new AuditPackEntry("Evidence", o.Id, o.Revision, "Superseded", o.Hash, evidence, control, o.SavedAt, o.FileId, o.FileId == null ? null : o.FileRevision)));
            var pins = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == control && x.Revision == co.Revision)
                .OrderBy(x => x.RequirementId).ThenBy(x => x.SourceId).Take(32).ToArrayAsync(ct);
            foreach (var (kind, id, rev) in pins.SelectMany(p => new[] { ("Source", p.SourceId, p.SourceRevision), ("Requirement", p.RequirementId, p.RequirementRevision) }).Distinct())
            {
                var r = await db.RegisterRevisions.AsNoTracking().Where(x => x.Kind == kind && x.Id == id && x.Revision == rev && x.SavedAt <= now)
                    .Select(x => new { x.Hash, x.SavedAt }).SingleOrDefaultAsync(ct);
                entries.Add(r == null ? new(kind, id, rev, "Unavailable", null, evidence, control, null) : new(kind, id, rev, "Current", r.Hash, evidence, control, r.SavedAt));
            }
            // Human verification / decision rows within the reader's appointed kinds and decision types (as in the trace).
            var grants = readers.Where(g => g.EvidenceId == evidence && g.ControlId == control).ToArray();
            var kinds = grants.SelectMany(g => g.Kinds).Distinct().ToArray(); var types = grants.SelectMany(g => g.DecisionTypes).Distinct().ToArray();
            var untyped = grants.Any(g => g.IncludeUntypedDecisions);
            var events = await db.HumanReviewEvents.AsNoTracking().Where(x => x.VesselId == vessel && x.EvidenceId == evidence && x.EvidenceRevision == ev.Revision &&
                    x.ControlId == control && x.ControlRevision == co.Revision && x.SavedAt <= now && kinds.Contains(x.Kind) &&
                    (x.Kind != "Decision" || types.Contains(x.DecisionType!) || x.DecisionType == null && untyped))
                .OrderBy(x => x.SavedAt).ThenBy(x => x.Id).ThenBy(x => x.Revision).Take(AuditPackPolicy.MaxEventsPerChain).ToArrayAsync(ct);
            entries.AddRange(events.Select(e => new AuditPackEntry(e.Kind, e.Id, e.Revision, "Recorded", e.ContextHash, evidence, control, e.SavedAt)));
            var history = await db.ControlStateEvents.AsNoTracking().Where(x => x.VesselId == vessel && x.ControlId == control && x.SavedAt <= now)
                .OrderByDescending(x => x.Revision).Take(AuditPackPolicy.MaxHistoryPerControl).ToArrayAsync(ct);
            entries.AddRange(history.Select(e => new AuditPackEntry("History", e.ControlId, e.Revision, "Current", HistoryHash(e), evidence, control, e.SavedAt)));
        }
        var ordered = AuditPackPolicy.Ordered(entries.DistinctBy(e => (e.Kind, e.ResourceId, e.Revision, e.State, e.EvidenceId, e.ControlId))).ToArray();
        var truncated = ordered.Length > AuditPackPolicy.MaxItems || pairs.Length > AuditReadyEndpoints.MaxChains;
        ordered = ordered.Take(AuditPackPolicy.MaxItems).ToArray();
        if (ordered.Any(e => !AuditPackPolicy.Valid(e))) return AssuranceOutcome.Fail(503, "service_unavailable");
        var m = new AuditPackManifest { TenantId = a.TenantId, VesselId = vessel, Id = c.Command.Id, AsOf = now, ItemCount = ordered.Length, UnavailableChains = unavailable,
            Truncated = truncated, PreviousPackId = c.Command.PreviousPackId, CreatedBy = a.Id, CreatedAt = now, ManifestHash = AuditPackPolicy.ManifestHash(vessel, now, ordered) };
        db.AuditPackManifests.Add(m); await db.SaveChangesAsync(ct);
        db.AuditPackItems.AddRange(ordered.Select((e, i) => new AuditPackItem { TenantId = a.TenantId, VesselId = vessel, PackId = m.Id, Ordinal = i, Kind = e.Kind,
            ResourceId = e.ResourceId, Revision = e.Revision, State = e.State, Hash = e.Hash, EvidenceId = e.EvidenceId, ControlId = e.ControlId, SavedAt = e.SavedAt,
            FileId = e.FileId, FileRevision = e.FileRevision }));
        await db.SaveChangesAsync(ct);
        return AssuranceOutcome.Ok(m.Id, 1);
    }

    private static async Task<object?> Read(PoseidonDbContext db, TrustedActor a, Guid? vessel, Guid id, HttpContext h, CancellationToken ct) =>
        await Build(db, a, vessel!.Value, id, Readers(h, a, vessel.Value), ct);

    // Re-authorizes every pinned item for the current reader and re-checks it at its source.
    internal static async Task<AuditPackDto?> Build(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, HumanTraceReader[] readers, CancellationToken ct)
    {
        var m = await db.AuditPackManifests.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == id, ct);
        if (m == null) return null;
        var items = await db.AuditPackItems.AsNoTracking().Where(x => x.VesselId == vessel && x.PackId == id).OrderBy(x => x.Ordinal).ToArrayAsync(ct);
        var pairs = readers.Select(g => (g.EvidenceId, g.ControlId)).ToHashSet();
        bool Appointed(AuditPackItem i) => pairs.Contains((i.EvidenceId, i.ControlId)) &&
            (i.Kind is not ("Verification" or "Decision") || readers.Any(g => g.EvidenceId == i.EvidenceId && g.ControlId == i.ControlId && g.Kinds.Contains(i.Kind)));
        var allowed = items.Where(Appointed).ToArray();
        var ceIds = allowed.Where(i => i.Kind is "Evidence" or "Control").Select(i => i.ResourceId).Distinct().ToArray();
        var ce = ceIds.Length == 0 ? [] : await HumanReviewEndpoints.Visible(db, vessel, a, true).Where(x => ceIds.Contains(x.Id))
            .Select(x => new { x.Kind, x.Id, x.Revision, x.Hash }).ToArrayAsync(ct);
        var regIds = allowed.Where(i => i.Kind is "Source" or "Requirement").Select(i => i.ResourceId).Distinct().ToArray();
        var reg = regIds.Length == 0 ? [] : await db.RegisterRevisions.AsNoTracking().Where(x => regIds.Contains(x.Id)).Select(x => new { x.Kind, x.Id, x.Revision, x.Hash }).ToArrayAsync(ct);
        var evIds = allowed.Where(i => i.Kind is "Verification" or "Decision").Select(i => i.ResourceId).Distinct().ToArray();
        var events = evIds.Length == 0 ? [] : await db.HumanReviewEvents.AsNoTracking().Where(x => x.VesselId == vessel && evIds.Contains(x.Id))
            .Select(x => new { x.Kind, x.Id, x.Revision, x.ContextHash, x.DecisionType }).ToArrayAsync(ct);
        var ctlIds = allowed.Where(i => i.Kind == "History").Select(i => i.ResourceId).Distinct().ToArray();
        var history = ctlIds.Length == 0 ? [] : await db.ControlStateEvents.AsNoTracking().Where(x => x.VesselId == vessel && ctlIds.Contains(x.ControlId)).ToArrayAsync(ct);
        var dto = new List<AuditPackItemDto>(); var excluded = items.Length - allowed.Length;
        foreach (var i in allowed)
        {
            string? current; var readable = true;
            switch (i.Kind)
            {
                case "Evidence" or "Control": current = ce.FirstOrDefault(x => x.Kind == i.Kind && x.Id == i.ResourceId && x.Revision == i.Revision)?.Hash; break;
                case "Source" or "Requirement": current = reg.FirstOrDefault(x => x.Kind == i.Kind && x.Id == i.ResourceId && x.Revision == i.Revision)?.Hash; break;
                case "Verification" or "Decision":
                    var e = events.FirstOrDefault(x => x.Kind == i.Kind && x.Id == i.ResourceId && x.Revision == i.Revision);
                    // A decision type outside the reader's current appointment is excluded, never shown.
                    if (e != null && i.Kind == "Decision" && !readers.Any(g => g.EvidenceId == i.EvidenceId && g.ControlId == i.ControlId &&
                            (e.DecisionType == null ? g.IncludeUntypedDecisions : g.DecisionTypes.Contains(e.DecisionType))))
                    { excluded++; continue; }
                    current = e?.ContextHash; break;
                default:
                    var s = history.FirstOrDefault(x => x.ControlId == i.ResourceId && x.Revision == i.Revision);
                    current = s == null ? null : HistoryHash(s); break;
            }
            if (i.State == "Unavailable") { readable = false; current = null; }
            dto.Add(new(i.Ordinal, i.Kind, i.ResourceId, i.Revision, i.State, i.Hash, i.EvidenceId, i.ControlId, i.SavedAt,
                AuditPackPolicy.IntegrityOf(i.Hash, current, readable),
                i.FileId is { } file && current != null ? $"/api/v1/vessels/{vessel:D}/files/{file:D}" : null,
                i.Kind is "Evidence" or "Control" or "History" ? $"/api/v1/audit?resourceId={i.ResourceId:D}" : null));
        }
        return new(m.Id, m.VesselId, m.Label, Notice, m.SourceVersion, m.AsOf, m.CreatedAt, m.CreatedBy == a.Id, m.ManifestHash, m.ItemCount, dto.Count, excluded,
            m.UnavailableChains, m.Truncated, m.PreviousPackId, dto.Count(x => x.State == "Superseded"), dto.ToArray());
    }

    private static async Task<IResult> View(HttpContext h, PoseidonDbContext db, Guid id, bool export)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = SafetyEventCommands.Actor(h);
        if (a == null || !AuditReadyEndpoints.CanRead(a)) return await AssuranceCommands.Fail(h, "AuditPackRead", 403, "permission_denied", id);
        if (!SafetyEventCommands.Query(h, out var vessel)) return await AssuranceCommands.Fail(h, "AuditPackRead", 400, "invalid_query", id);
        if (!await SafetyEventCommands.Vessels(db, a, vessel).AnyAsync(ct)) return await AssuranceCommands.Fail(h, "AuditPackRead", 404, "resource_not_found", id);
        var readers = Readers(h, a, vessel);
        if (readers.Length == 0) return await AssuranceCommands.Fail(h, "AuditPackRead", 403, "trace_reader_required", id);
        var pack = await Build(db, a, vessel, id, readers, ct);
        if (pack == null) return await AssuranceCommands.Fail(h, "AuditPackRead", 404, "resource_not_found", id);
        // Withdrawn appointments during the awaits deny the response bytes.
        var live = SafetyEventCommands.Actor(h);
        if (live == null || live.Id != a.Id || !AuditReadyEndpoints.CanRead(live) || Readers(h, live, vessel).Length != readers.Length)
            return await AssuranceCommands.Fail(h, "AuditPackRead", 403, "trace_reader_required", id);
        await SafetyEventCommands.Audit(h, "AuditPackRead", "Success", id);
        if (!export) return Results.Json(pack, AssuranceCommands.Json);
        h.Response.Headers.ContentDisposition = $"attachment; filename=\"audit-pack-{id:D}.json\"";
        return Results.Json(new AuditPackExportDto("poseidon.audit-pack-export.v1", AuditPackPolicy.Label, Notice, DateTimeOffset.UtcNow, pack), AssuranceCommands.Json);
    }
}
