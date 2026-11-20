using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record MeasurementReadingDto(Guid Id, Guid PointId, string Value, string Unit, DateTimeOffset MeasuredAt, DateTimeOffset RecordedAt,
    string SourceType, string? SourceReference, Guid? EvidenceFileId, Guid? PreviousReadingId, string? Delta, Guid? PropagatedFromReadingId,
    Guid ActorId, string Provenance);
public sealed record MeasurementPointDto(Guid Id, Guid AssetId, string Code, string Kind, string Unit, string Source, int Precision,
    string? RangeMin, string? RangeMax, Guid? ParentPointId, long Revision, uint Version, string Provenance, MeasurementReadingDto? Latest,
    string Band, string PolicyState, string? PolicyVersion, int? FreshnessMinutes, string Freshness);
public sealed record MeasurementPage(MeasurementPointDto[] Items, bool CanWrite, string[] Kinds, Dictionary<string, string[]> Units,
    string[] Sources, string SourceVersion);
public sealed record MeasurementReadingPage(MeasurementPointDto Point, MeasurementReadingDto[] Items, bool CanWrite);
public sealed record MeasurementPointOutcome(MeasurementPointDto Point);
public sealed record MeasurementReadingOutcome(MeasurementReadingDto Reading, MeasurementReadingDto[] Propagated, MeasurementPropagationSkip[] Skipped);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MeasurementPointSaveCommand([property: JsonRequired] Guid Id, uint? ExpectedVersion, [property: JsonRequired] Guid AssetId,
    [property: JsonRequired] string Code, [property: JsonRequired] string Kind, [property: JsonRequired] string Unit, [property: JsonRequired] string Source,
    [property: JsonRequired] int Precision, string? RangeMin = null, string? RangeMax = null, Guid? ParentPointId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MeasurementReadingCommand([property: JsonRequired] Guid Id, [property: JsonRequired] string Value,
    [property: JsonRequired] DateTimeOffset MeasuredAt, [property: JsonRequired] string SourceType, string? SourceReference = null, Guid? EvidenceFileId = null);

// DEV-108 / T-PMS2-002 (18.01 row 109; 06.01 FR-V463-PMS-002; 59.04 Rev2 §9, p195, p878-987).
// Declared measurement point definitions and append-only reading history with parent -> sub
// running-hours propagation. A reading is never validated, a threshold never approved here:
// thresholds are tenant-configured; with none configured the band is UNCONFIGURED.
public static class MeasurementPointEndpoints
{
    private const string ReadAction = "MeasurementRead", CommandAction = "MeasurementCommand";
    internal static bool CanRead(TrustedActor? a) => a?.CanRead == true && a.Permissions.Contains("equipment.read") && a.Permissions.Contains("maintenance.read");
    internal static bool CanWrite(TrustedActor? a, Guid vessel) => CanRead(a) && a!.HasWriteAttribution && a.CanEditVessel(vessel) &&
        a.Permissions.Contains("maintenance.measurement.write");

    public static void MapMeasurementPoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/equipment-hierarchy/{vessel:guid}/measurement-points").RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("MeasurementPointList").Produces<MeasurementPage>();
        g.MapPost("", (Guid vessel, HttpContext h, PoseidonDbContext db) => PmsCommands.Run<MeasurementPointSaveCommand>(h, db, vessel, "MeasurementPointSave", CommandAction,
                null, null, (a, v) => CanWrite(a, v), c => c.Id, Save, ReadPoint))
            .WithName("MeasurementPointSave").Accepts<MeasurementPointSaveCommand>("application/json").Produces<MeasurementPointOutcome>().Produces<PmsFailure>(422);
        g.MapGet("/{point:guid}/readings", Readings).WithName("MeasurementReadingList").Produces<MeasurementReadingPage>();
        g.MapPost("/{point:guid}/readings", (Guid vessel, Guid point, HttpContext h, PoseidonDbContext db) => PmsCommands.Run<MeasurementReadingCommand>(h, db, vessel,
                "MeasurementReadingRecord", CommandAction, point, null, (a, v) => CanWrite(a, v), c => c.Id, Record, ReadReading))
            .WithName("MeasurementReadingRecord").Accepts<MeasurementReadingCommand>("application/json").Produces<MeasurementReadingOutcome>().Produces<PmsFailure>(422);
    }

    internal static MeasurementReadingDto Dto(MeasurementReading r) => new(r.Id, r.PointId, r.Value, r.Unit, r.MeasuredAt, r.RecordedAt, r.SourceType,
        r.SourceReference, r.EvidenceFileId, r.PreviousReadingId, r.Delta, r.PropagatedFromReadingId, r.ActorId, r.Provenance);
    private static MeasurementPointDefinition Definition(MeasurementPoint p) => new(p.Code, p.Kind, p.Unit, p.Source, p.Precision, p.RangeMin, p.RangeMax, p.ParentPointId);
    internal static MeasurementPointRef Ref(MeasurementPoint p) => new(p.TenantId, p.VesselId, p.AssetId, p.Id, p.Code, p.Kind, p.Unit);
    private static MeasurementLatest? Latest(MeasurementReading? r) => r == null ? null : new(r.Id, r.PointId, r.Value, r.MeasuredAt);

    // Latest reading per point: chronological by measured time, then record time and id.
    internal static IQueryable<MeasurementReading> LatestOf(PoseidonDbContext db, Guid vessel, Guid point) =>
        db.MeasurementReadings.AsNoTracking().Where(r => r.VesselId == vessel && r.PointId == point)
            .OrderByDescending(r => r.MeasuredAt).ThenByDescending(r => r.RecordedAt).ThenByDescending(r => r.Id);

    internal static MeasurementPointDto Project(MeasurementPoint p, MeasurementReading? latest, MaintenanceObservationSettings settings, DateTimeOffset now)
    {
        var rule = settings.Measurement(p.TenantId, p.VesselId, p.Code);
        var configured = PmsMeasurementPolicy.ValidPolicy(rule, p.Kind, p.Precision);
        var band = PmsMeasurementPolicy.CurrentBand(Ref(p), Latest(latest), rule, p.Precision);
        var freshness = !configured ? PmsMeasurementPolicy.Unconfigured : latest == null ? PmsMeasurementPolicy.Unknown
            : latest.MeasuredAt > now ? PmsMeasurementPolicy.Unknown : now - latest.MeasuredAt <= TimeSpan.FromMinutes(rule!.FreshnessMinutes) ? "CURRENT" : "STALE";
        // A stale or future reading cannot assert a current band.
        if (freshness != "CURRENT" && band is PmsMeasurementPolicy.Normal or PmsMeasurementPolicy.Caution) band = PmsMeasurementPolicy.Unknown;
        return new(p.Id, p.AssetId, p.Code, p.Kind, p.Unit, p.Source, p.Precision, p.RangeMin, p.RangeMax, p.ParentPointId, p.Revision, p.Version, p.Provenance,
            latest == null ? null : Dto(latest), band, configured ? "CONFIGURED" : PmsMeasurementPolicy.Unconfigured, configured ? rule!.Version : null,
            configured ? rule!.FreshnessMinutes : null, freshness);
    }

    private static async Task<IResult?> ReadGate(HttpContext h, PoseidonDbContext db, Guid vessel)
    {
        h.Response.Headers.CacheControl = "no-store";
        var actor = PmsCommands.Actor(h);
        if (!CanRead(actor)) return await PmsCommands.Fail(h, ReadAction, 403, "permission_denied");
        if (!await PmsCommands.InScope(db, actor!, vessel, h.RequestAborted)) return await PmsCommands.Fail(h, ReadAction, 404, "resource_not_found");
        return null;
    }

    private static async Task<IResult> List(Guid vessel, HttpContext h, PoseidonDbContext db, MaintenanceObservationSettings settings)
    {
        if (await ReadGate(h, db, vessel) is { } denied) return denied;
        var ct = h.RequestAborted; var now = PmsCommands.Now(h);
        var points = await db.MeasurementPoints.AsNoTracking().Where(p => p.VesselId == vessel)
            .OrderBy(p => p.Code).ThenBy(p => p.Id).Take(500).ToArrayAsync(ct);
        var ids = points.Select(p => p.Id).ToArray();
        var latest = await db.MeasurementReadings.AsNoTracking().Where(r => r.VesselId == vessel && ids.Contains(r.PointId))
            .GroupBy(r => r.PointId).Select(g => g.OrderByDescending(r => r.MeasuredAt).ThenByDescending(r => r.RecordedAt).ThenByDescending(r => r.Id).First())
            .ToArrayAsync(ct);
        var byPoint = latest.ToDictionary(r => r.PointId);
        await PmsCommands.Audit(h, ReadAction, "Success", vessel);
        return Results.Json(new MeasurementPage([.. points.Select(p => Project(p, byPoint.GetValueOrDefault(p.Id), settings, now))],
            CanWrite(PmsCommands.Actor(h), vessel), PmsMeasurementPolicy.Kinds, PmsMeasurementPolicy.Units.ToDictionary(x => x.Key, x => x.Value),
            PmsMeasurementPolicy.CallerReadingSources, PmsMeasurementPolicy.SourceVersion), PmsCommands.Json);
    }

    private static async Task<IResult> Readings(Guid vessel, Guid point, HttpContext h, PoseidonDbContext db, MaintenanceObservationSettings settings)
    {
        if (await ReadGate(h, db, vessel) is { } denied) return denied;
        var ct = h.RequestAborted;
        var row = await db.MeasurementPoints.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.Id == point, ct);
        if (row == null) return await PmsCommands.Fail(h, ReadAction, 404, "resource_not_found", point);
        var items = await LatestOf(db, vessel, point).Take(200).ToArrayAsync(ct);
        await PmsCommands.Audit(h, ReadAction, "Success", point);
        return Results.Json(new MeasurementReadingPage(Project(row, items.FirstOrDefault(), settings, PmsCommands.Now(h)), [.. items.Select(Dto)],
            CanWrite(PmsCommands.Actor(h), vessel)), PmsCommands.Json);
    }

    private static async Task<object?> ReadPoint(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid id, HttpContext h, CancellationToken ct)
    {
        var row = await db.MeasurementPoints.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.Id == id, ct);
        if (row == null) return null;
        var latest = await LatestOf(db, vessel, id).FirstOrDefaultAsync(ct);
        return new MeasurementPointOutcome(Project(row, latest, h.RequestServices.GetRequiredService<MaintenanceObservationSettings>(), PmsCommands.Now(h)));
    }

    private static async Task<object?> ReadReading(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid id, HttpContext h, CancellationToken ct)
    {
        var row = await db.MeasurementReadings.AsNoTracking().SingleOrDefaultAsync(r => r.VesselId == vessel && r.Id == id, ct);
        if (row == null) return null;
        var derived = await db.MeasurementReadings.AsNoTracking().Where(r => r.VesselId == vessel && r.PropagatedFromReadingId == id)
            .OrderBy(r => r.PointId).ToArrayAsync(ct);
        // Skipped sub counters are a transient admission outcome; a re-read reports derived rows only.
        return new MeasurementReadingOutcome(Dto(row), [.. derived.Select(Dto)], h.Items["PmsSkipped"] as MeasurementPropagationSkip[] ?? []);
    }

    private static async Task<PmsOutcome> Save(PmsContext<MeasurementPointSaveCommand> c)
    {
        var cmd = c.Command; var db = c.Db;
        var definition = new MeasurementPointDefinition(cmd.Code, cmd.Kind, cmd.Unit, cmd.Source, cmd.Precision, cmd.RangeMin, cmd.RangeMax, cmd.ParentPointId);
        if (cmd.Id == Guid.Empty || cmd.AssetId == Guid.Empty) return PmsOutcome.Fail(400, "invalid_request");
        if (PmsMeasurementPolicy.ValidateDefinition(definition, cmd.Id) is { } invalid) return PmsOutcome.Fail(422, invalid);
        if (!await db.Assets.AnyAsync(a => a.VesselId == c.Vessel && a.Id == cmd.AssetId, c.Ct)) return PmsOutcome.Fail(400, "linked_resource_unavailable");
        var row = await db.MeasurementPoints.SingleOrDefaultAsync(p => p.Id == cmd.Id, c.Ct);
        if (row != null && row.VesselId != c.Vessel) return PmsOutcome.Fail(404, "resource_not_found");
        if (row == null && cmd.ExpectedVersion != null || row != null && row.Version != cmd.ExpectedVersion) return PmsOutcome.Fail(409, "version_conflict");
        if (await db.MeasurementPoints.AnyAsync(p => p.VesselId == c.Vessel && p.Code == cmd.Code && p.Id != cmd.Id, c.Ct)) return PmsOutcome.Fail(409, "duplicate_code");
        if (row != null)
        {
            if (row.AssetId != cmd.AssetId) return PmsOutcome.Fail(409, "immutable_context");
            // Recorded history keeps its meaning: kind/unit/precision freeze once a reading exists.
            if ((row.Kind != cmd.Kind || row.Unit != cmd.Unit || row.Precision != cmd.Precision) &&
                await db.MeasurementReadings.AnyAsync(r => r.VesselId == c.Vessel && r.PointId == row.Id, c.Ct)) return PmsOutcome.Fail(409, "immutable_context");
        }
        if (cmd.ParentPointId is { } parent)
        {
            // Complete vessel counter forest read inside the serializable transaction.
            var counters = await db.MeasurementPoints.AsNoTracking().Where(p => p.VesselId == c.Vessel && p.Kind == PmsMeasurementPolicy.RunningHours)
                .Select(p => new EquipmentHierarchyNode(p.TenantId, p.VesselId, p.Id, p.ParentPointId)).ToListAsync(c.Ct);
            if (!counters.Any(n => n.Id == parent)) return PmsOutcome.Fail(400, "linked_resource_unavailable");
            if (counters.All(n => n.Id != cmd.Id)) counters.Add(new(c.Actor.TenantId, c.Vessel, cmd.Id, null));
            var check = EquipmentHierarchyPolicy.ValidateReparent(c.Actor.TenantId, c.Vessel, counters, cmd.Id, parent);
            if (check == EquipmentHierarchyValidation.Cycle) return PmsOutcome.Fail(422, "hierarchy_cycle");
            if (check != EquipmentHierarchyValidation.Valid) return PmsOutcome.Fail(400, "linked_resource_unavailable");
        }
        if (row == null)
        {
            row = new MeasurementPoint { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, AssetId = cmd.AssetId, Revision = 1 };
            db.MeasurementPoints.Add(row);
        }
        else row.Revision = checked(row.Revision + 1);
        row.Code = cmd.Code; row.Kind = cmd.Kind; row.Unit = cmd.Unit; row.Source = cmd.Source; row.Precision = cmd.Precision;
        row.RangeMin = cmd.RangeMin; row.RangeMax = cmd.RangeMax; row.ParentPointId = cmd.ParentPointId; row.ActorId = c.Actor.Id; row.SavedAt = c.Now;
        await db.SaveChangesAsync(c.Ct);
        db.MeasurementPointRevisions.Add(row.Snapshot()); await db.SaveChangesAsync(c.Ct);
        return PmsOutcome.Ok(row.Id, row.Revision);
    }

    private static async Task<PmsOutcome> Record(PmsContext<MeasurementReadingCommand> c)
    {
        var cmd = c.Command; var db = c.Db; var pointId = c.RouteId!.Value;
        if (cmd.Id == Guid.Empty || !PmsMeasurementPolicy.CallerReadingSources.Contains(cmd.SourceType, StringComparer.Ordinal) ||
            !PmsCommands.Text(cmd.SourceReference, 200) || cmd.EvidenceFileId == Guid.Empty || cmd.MeasuredAt == default) return PmsOutcome.Fail(400, "invalid_request");
        var point = await db.MeasurementPoints.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == c.Vessel && p.Id == pointId, c.Ct);
        if (point == null) return PmsOutcome.Fail(404, "resource_not_found");
        // A DEVICE point accepts device/import readings; a MANUAL point accepts manual/import readings.
        if (cmd.SourceType != PmsMeasurementPolicy.Import && cmd.SourceType != point.Source) return PmsOutcome.Fail(422, "measurement_definition_invalid");
        if (await db.MeasurementReadings.AnyAsync(r => r.Id == cmd.Id, c.Ct)) return PmsOutcome.Fail(409, "version_conflict");
        if (cmd.EvidenceFileId is { } file && !await db.StoredFiles.AnyAsync(f => f.VesselId == c.Vessel && f.Id == file, c.Ct))
            return PmsOutcome.Fail(400, "linked_resource_unavailable");
        // PostgreSQL keeps microseconds; admit and store the same instant.
        var utc = cmd.MeasuredAt.ToUniversalTime(); var measuredAt = utc.AddTicks(-(utc.Ticks % 10));
        var previous = await LatestOf(db, c.Vessel, point.Id).FirstOrDefaultAsync(c.Ct);
        var definition = Definition(point);
        if (PmsMeasurementPolicy.ValidateReading(definition, cmd.Value, measuredAt, c.Now, Latest(previous), out var value) is { } refused)
            return PmsOutcome.Fail(refused == "invalid_request" ? 400 : 422, refused);
        string? delta = null;
        if (PmsMeasurementPolicy.Counter(point.Kind) && previous != null && PmsMeasurementPolicy.Decimal(previous.Value, 18, out var prior))
            delta = PmsMeasurementPolicy.Format(value - prior);
        var reading = new MeasurementReading { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, PointId = point.Id, AssetId = point.AssetId,
            Value = cmd.Value, Unit = point.Unit, MeasuredAt = measuredAt, RecordedAt = c.Now, SourceType = cmd.SourceType, SourceReference = cmd.SourceReference,
            EvidenceFileId = cmd.EvidenceFileId, PreviousReadingId = previous?.Id, Delta = delta, ActorId = c.Actor.Id };
        db.MeasurementReadings.Add(reading);
        var skipped = Array.Empty<MeasurementPropagationSkip>();
        if (delta != null && PmsMeasurementPolicy.Decimal(delta, 18, out var d) && d > 0)
        {
            var counters = await db.MeasurementPoints.AsNoTracking().Where(p => p.VesselId == c.Vessel && p.Kind == PmsMeasurementPolicy.RunningHours).ToArrayAsync(c.Ct);
            var nodes = new List<MeasurementCounterNode>();
            var latestByPoint = new Dictionary<Guid, MeasurementReading>();
            foreach (var counter in counters)
            {
                var last = counter.Id == point.Id ? null : await LatestOf(db, c.Vessel, counter.Id).FirstOrDefaultAsync(c.Ct);
                if (last != null) latestByPoint[counter.Id] = last;
                nodes.Add(new(counter.Id, counter.ParentPointId, counter.Kind, last?.Value, last?.MeasuredAt));
            }
            var (derived, skip) = PmsMeasurementPolicy.Propagate(point.Id, d, measuredAt, nodes);
            skipped = skip;
            foreach (var x in derived)
            {
                var target = counters.Single(p => p.Id == x.PointId);
                db.MeasurementReadings.Add(new MeasurementReading { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = Guid.NewGuid(), PointId = target.Id,
                    AssetId = target.AssetId, Value = x.Value, Unit = target.Unit, MeasuredAt = measuredAt, RecordedAt = c.Now, SourceType = PmsMeasurementPolicy.Propagated,
                    SourceReference = null, PreviousReadingId = latestByPoint.GetValueOrDefault(target.Id)?.Id, Delta = x.Delta, PropagatedFromReadingId = reading.Id,
                    ActorId = c.Actor.Id, Provenance = "DerivedFromDeclaredParentCounter" });
            }
        }
        await db.SaveChangesAsync(c.Ct);
        c.Http.Items["PmsSkipped"] = skipped;
        return PmsOutcome.Ok(reading.Id, 1);
    }

    // DEV-053/DEV-108 plan trigger input, read under the caller's already-established scope.
    internal static async Task<CurrentMaintenanceObservation> ResolveForPlan(Guid vessel, Guid asset, MaintenanceInputs inputs, PoseidonDbContext db,
        MaintenanceObservationSettings settings, DateTimeOffset serverNow, CancellationToken ct)
    {
        var point = await db.MeasurementPoints.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.Id == inputs.MeasurementPointId, ct);
        var latest = point == null ? null : await LatestOf(db, vessel, point.Id).Where(r => r.MeasuredAt <= serverNow).FirstOrDefaultAsync(ct);
        var rule = point == null ? null : settings.Measurement(db.TenantId, vessel, point.Code);
        var assessment = PmsMeasurementPolicy.AssessTrigger(inputs, point == null ? null : Ref(point), point?.Precision ?? 0, Latest(latest), rule,
            db.TenantId, vessel, asset, serverNow);
        return new(assessment, latest?.MeasuredAt);
    }
}
