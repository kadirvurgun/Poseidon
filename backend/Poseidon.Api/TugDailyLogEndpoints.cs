using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugDailyLogCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] DateOnly LogDate, [property: JsonRequired] string Watch,
    DateTimeOffset? CapturedAt, [property: JsonRequired] TugDailyLogContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugDailyLogUpdateCommand([property: JsonRequired] uint ExpectedVersion, DateTimeOffset? CapturedAt, [property: JsonRequired] TugDailyLogContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugDailyLogSubmitCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugDailyLogReviewCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason, [property: JsonRequired] Guid AuthorityId);

public sealed record TugDailyLogDto(Guid Id, Guid VesselId, DateOnly LogDate, string Watch, long Revision, uint Version, string State, TugDailyLogContent Content, string Hash,
    Guid? SubmittedBy, long? VerifiedRevision, DateTimeOffset? CapturedAt, DateTimeOffset SyncedAt, string SyncState, string Provenance, DateTimeOffset SavedAt);
public sealed record TugDailyLogSummaryDto(Guid Id, DateOnly LogDate, string Watch, long Revision, string State, string MasterReview, DateTimeOffset SyncedAt);
public sealed record TugDailyLogListView(Guid VesselId, TugDailyLogSummaryDto[] Items, bool CanWrite, string[] Watches, string[] FuelUnits, string SourceVersion = TugDailyLogPolicy.SourceVersion);
public sealed record TugDailyLogReviewDto(Guid Id, long Revision, long ResultRevision, string Decision, string Reconciliation, string Duty, Guid ActorId, string Reason, DateTimeOffset DecidedAt);
public sealed record TugDailyLogRevisionDto(long Revision, string State, string ReasonCode, string Hash, Guid ActorId, DateTimeOffset? CapturedAt, DateTimeOffset SyncedAt);
public sealed record TugDailyExecutionDto(Guid Id, Guid JobId, string State, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, bool Linked);
public sealed record TugDailyLogView(TugDailyLogDto Log, TugDailyReconciliation Reconciliation, string MasterReview, int DefectsReported, string SafetyEvents,
    DateTimeOffset? IntervalFrom, DateTimeOffset? IntervalTo, TugDailyExecutionDto[] Executions, string[] SubmitBlockers, TugDailyLogReviewDto[] Reviews,
    TugDailyLogRevisionDto[] Revisions, TugAuthorityDto[] Authorities, bool CanEdit, bool CanSubmit, bool CanReview, string[] FuelUnits,
    string OfflineCapture = "InMemoryUnsavedOnly;DurableTugCaptureNotRegistered", string SourceVersion = TugDailyLogPolicy.SourceVersion);

// DEV-090 /api/v1/tug-daily-logs. One log per tug, date and watch; the client supplies the record id so an
// offline-composed command replays idempotently (Idempotency-Key receipt + ExpectedVersion; a changed base is a
// material 409 the person resolves). Master review is a Tug Master/Master human record from an appointment on the
// tug and never by an author or the submitter. Reconciliation is advisory and never auto-corrects a reading.
public static class TugDailyLogEndpoints
{
    private const string Read = "TugDailyLogRead", Command = "TugDailyLogCommand";

    public static void MapTugDailyLogs(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/tug-daily-logs").RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("TugDailyLogList").Produces<TugDailyLogListView>();
        g.MapGet("/{id:guid}", Detail).WithName("TugDailyLogRead").Produces<TugDailyLogView>();
        g.MapPost("", (HttpContext h, PoseidonDbContext db) => Run<TugDailyLogCreateCommand>(h, db, "TugDailyLogCreate", null, TugReadinessHttp.Write, c => c.Id, Create))
            .WithName("TugDailyLogCreate").Accepts<TugDailyLogCreateCommand>("application/json").Produces<TugDailyLogView>();
        g.MapPut("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => Run<TugDailyLogUpdateCommand>(h, db, "TugDailyLogUpdate", id, TugReadinessHttp.Write, _ => id, Update))
            .WithName("TugDailyLogUpdate").Accepts<TugDailyLogUpdateCommand>("application/json").Produces<TugDailyLogView>();
        g.MapPost("/{id:guid}/submit", (Guid id, HttpContext h, PoseidonDbContext db) => Run<TugDailyLogSubmitCommand>(h, db, "TugDailyLogSubmit", id, TugReadinessHttp.Write, _ => id, Submit))
            .WithName("TugDailyLogSubmit").Accepts<TugDailyLogSubmitCommand>("application/json").Produces<TugDailyLogView>().Produces<RiskFailure>(422);
        g.MapPost("/{id:guid}/review", (Guid id, HttpContext h, PoseidonDbContext db) => Run<TugDailyLogReviewCommand>(h, db, "TugDailyLogReview", id, (_, _) => true, _ => id, Review))
            .WithName("TugDailyLogReview").Accepts<TugDailyLogReviewCommand>("application/json").Produces<TugDailyLogView>();
    }

    private static Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string operation, Guid? id, Func<TrustedActor, Guid, bool> allowed, Func<T, Guid?> resource,
        Func<TowageContext<T>, Task<TowageOutcome>> apply) where T : class =>
        FieldOpsCommands.Run(h, db, operation, Command, id, a => a.CanReadTug, TugReadinessHttp.Tugs, allowed, resource, apply, ReadView);

    private static TugDailyLogContent Content(string json) => JsonSerializer.Deserialize<TugDailyLogContent>(json, FieldOpsJson.Options) ?? throw new InvalidDataException("daily_log_unavailable");
    private static string Json(TugDailyLogContent c) => JsonSerializer.Serialize(c, FieldOpsJson.Options);
    private static TimeZoneInfo? Zone(string? id) => id != null && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var z) ? z : null;
    private static int WatchOrder(string watch) => Array.IndexOf(TugDailyLogPolicy.Watches, watch);

    private static TugDailyLogDto Dto(TugDailyLog l) => new(l.Id, l.VesselId, l.LogDate, l.Watch, l.Revision, l.Version, l.State, Content(l.ContentJson), l.Hash,
        l.SubmittedBy, l.VerifiedRevision, l.CapturedAt, l.SyncedAt, "SYNCED", l.Provenance, l.SavedAt);

    private static void Stamp(TugDailyLog l, Guid actor, DateTimeOffset now) {
        l.ActorId = actor; l.SavedAt = now; l.SyncedAt = now;
        l.Hash = TugDailyLogPolicy.Hash(l.VesselId, l.Id, l.Revision, l.State, l.LogDate, l.Watch, l.ContentJson);
    }
    private static void Append(PoseidonDbContext db, TugDailyLog l, string reason) => db.TugDailyLogRevisions.Add(new() { TenantId = l.TenantId, VesselId = l.VesselId,
        LogId = l.Id, Revision = l.Revision, State = l.State, ReasonCode = reason, ContentJson = l.ContentJson, Hash = l.Hash, CapturedAt = l.CapturedAt,
        SyncedAt = l.SyncedAt, ActorId = l.ActorId, SavedAt = l.SavedAt, Provenance = l.Provenance });

    // Linked executions must be this tug's; linked defects must be this tug's and readable.
    private static async Task<TowageOutcome?> Links(TowageContext<TugDailyLogCreateCommand>? create, TowageContext<TugDailyLogUpdateCommand>? update, TugDailyLogContent c) {
        var db = create?.Db ?? update!.Db; var a = create?.Actor ?? update!.Actor; var tug = create?.Vessel ?? update!.Vessel; var ct = create?.Ct ?? update!.Ct;
        var now = create?.Now ?? update!.Now; var captured = create?.Command.CapturedAt ?? update?.Command.CapturedAt;
        if (!TugDailyLogPolicy.ValidDraft(c)) return TowageOutcome.Fail(400, "validation_failed");
        if (captured is { } at && (at.Offset != TimeSpan.Zero || at > now + TowageExecutionPolicy.ClockSkew)) return TowageOutcome.Fail(400, "validation_failed");
        if (TugDailyLogPolicy.MeterSequenceConflict(c)) return TowageOutcome.Fail(409, "meter_sequence_conflict");
        if (c.ExecutionIds.Length > 0 && await db.TowageExecutions.CountAsync(x => c.ExecutionIds.Contains(x.Id) && x.TugId == tug, ct) != c.ExecutionIds.Length)
            return TowageOutcome.Fail(422, "linked_resource_unavailable");
        if (c.DefectIds.Length > 0 && (!DefectEndpoints.CanRead(a) || await db.Defects.CountAsync(d => c.DefectIds.Contains(d.Id) && d.VesselId == tug, ct) != c.DefectIds.Length))
            return TowageOutcome.Fail(422, "linked_resource_unavailable");
        return null;
    }

    internal static async Task<TowageOutcome> Create(TowageContext<TugDailyLogCreateCommand> c) {
        var cmd = c.Command; var db = c.Db;
        if (cmd.Id == Guid.Empty || !TugDailyLogPolicy.Watches.Contains(cmd.Watch)) return TowageOutcome.Fail(400, "validation_failed");
        if (await Links(c, null, cmd.Content) is { } failure) return failure;
        if (await db.TugDailyLogs.AnyAsync(l => l.Id == cmd.Id || l.VesselId == c.Vessel && l.LogDate == cmd.LogDate && l.Watch == cmd.Watch, c.Ct)) return TowageOutcome.Fail(409, "version_conflict");
        var l = new TugDailyLog { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, LogDate = cmd.LogDate, Watch = cmd.Watch, ContentJson = Json(cmd.Content),
            AuthorsJson = FieldOpsCommands.WithAuthor("[]", c.Actor.Id), CapturedAt = cmd.CapturedAt };
        Stamp(l, c.Actor.Id, c.Now); db.TugDailyLogs.Add(l); Append(db, l, "CREATE");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(l.Id, l.Revision);
    }

    internal static async Task<TowageOutcome> Update(TowageContext<TugDailyLogUpdateCommand> c) {
        var db = c.Db;
        if (await Links(null, c, c.Command.Content) is { } failure) return failure;
        var l = await db.TugDailyLogs.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (l == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (l.Version != c.Command.ExpectedVersion) return TowageOutcome.Fail(409, "version_conflict");
        if (l.State is not (TugDailyLogPolicy.Draft or TugDailyLogPolicy.Returned)) return TowageOutcome.Fail(409, "invalid_state");
        l.Revision = checked(l.Revision + 1); l.State = TugDailyLogPolicy.Draft; l.ContentJson = Json(c.Command.Content);
        l.AuthorsJson = FieldOpsCommands.WithAuthor(l.AuthorsJson, c.Actor.Id); l.CapturedAt = c.Command.CapturedAt;
        Stamp(l, c.Actor.Id, c.Now); Append(db, l, "EDIT");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(l.Id, l.Revision);
    }

    private static async Task<TowageOutcome> Submit(TowageContext<TugDailyLogSubmitCommand> c) {
        var db = c.Db;
        var l = await db.TugDailyLogs.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (l == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (l.Version != c.Command.ExpectedVersion || l.Hash != c.Command.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        if (l.State != TugDailyLogPolicy.Draft) return TowageOutcome.Fail(409, "invalid_state");
        var content = Content(l.ContentJson);
        var blockers = TugDailyLogPolicy.SubmitBlockers(content, Zone(content.TimeZone) != null);
        if (blockers.Length > 0) return TowageOutcome.Fail(422, "daily_log_incomplete", blockers);
        l.Revision = checked(l.Revision + 1); l.State = TugDailyLogPolicy.Submitted; l.SubmittedBy = c.Actor.Id;
        l.AuthorsJson = FieldOpsCommands.WithAuthor(l.AuthorsJson, c.Actor.Id);
        Stamp(l, c.Actor.Id, c.Now); Append(db, l, "SUBMIT");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(l.Id, l.Revision);
    }

    private static async Task<TowageOutcome> Review(TowageContext<TugDailyLogReviewCommand> c) {
        var db = c.Db; var cmd = c.Command;
        var l = await db.TugDailyLogs.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (l == null) return TowageOutcome.Fail(404, "resource_not_found");
        var grant = TugOperationsAuthorities.Resolve(c.Http, c.Actor, l.VesselId, TugOperationsAuthorities.MasterDuties).SingleOrDefault(g => g.Id == cmd.AuthorityId);
        if (grant == null || !TugReadinessHttp.Write(c.Actor, l.VesselId)) return TowageOutcome.Fail(403, "tug_authority_required");
        if (l.Version != cmd.ExpectedVersion || l.Hash != cmd.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        var failure = TugDailyLogPolicy.ReviewFailure(l.State, cmd.Decision, c.Actor.Id, FieldOpsCommands.Authors(l.AuthorsJson), l.SubmittedBy, cmd.Reason);
        if (failure != null) return TowageOutcome.Fail(failure switch { "invalid_state" => 409, "separation_of_duties" => 403, _ => 400 }, failure);
        var reconciliation = (await Reconcile(db, l, Content(l.ContentJson), c.Now, c.Ct)).Result;
        var reviewed = l.Revision; var hash = l.Hash;
        l.Revision = checked(l.Revision + 1);
        l.State = cmd.Decision == "VERIFY" ? TugDailyLogPolicy.Verified : TugDailyLogPolicy.Returned;
        l.VerifiedRevision = l.State == TugDailyLogPolicy.Verified ? l.Revision : null;
        Stamp(l, c.Actor.Id, c.Now); Append(db, l, cmd.Decision == "VERIFY" ? "VERIFY" : "RETURN");
        db.TugDailyLogReviews.Add(new() { TenantId = l.TenantId, VesselId = l.VesselId, Id = Guid.NewGuid(), LogId = l.Id, Revision = reviewed, ResultRevision = l.Revision,
            Decision = cmd.Decision, ContentHash = hash, Reconciliation = reconciliation.State, ActorId = c.Actor.Id, AuthorityId = grant.Id, Duty = grant.Duty, Reason = cmd.Reason, DecidedAt = c.Now });
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(l.Id, l.Revision);
    }

    private sealed record Reconciled(TugDailyReconciliation Result, (DateTimeOffset From, DateTimeOffset To)? Interval, TugDailyExecutionDto[] Executions);

    private static async Task<Reconciled> Reconcile(PoseidonDbContext db, TugDailyLog l, TugDailyLogContent content, DateTimeOffset now, CancellationToken ct) {
        var interval = TugDailyLogPolicy.Interval(l.LogDate, l.Watch, Zone(content.TimeZone));
        // Previous closing per meter: the most recent earlier log (date, watch order) of this tug that recorded it.
        var earlier = await db.TugDailyLogs.AsNoTracking().Where(x => x.VesselId == l.VesselId && x.Id != l.Id && x.LogDate <= l.LogDate)
            .OrderByDescending(x => x.LogDate).Take(64).ToArrayAsync(ct);
        var previous = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in earlier.Where(x => x.LogDate < l.LogDate || WatchOrder(x.Watch) < WatchOrder(l.Watch)).OrderByDescending(x => x.LogDate).ThenByDescending(x => WatchOrder(x.Watch)))
            foreach (var m in Content(row.ContentJson).Meters.Where(m => m.Closing != null)) previous.TryAdd(m.Meter.Trim(), m.Closing!);
        var linked = content.ExecutionIds;
        var query = db.TowageExecutions.AsNoTracking().Where(x => x.TugId == l.VesselId);
        TowageExecution[] rows;
        if (interval is { } span) {
            var from = span.From; var to = span.To;
            rows = await query.Where(x => linked.Contains(x.Id) || x.StartedAt < to && (x.EndedAt == null || x.EndedAt >= from)).Take(100).ToArrayAsync(ct);
        } else rows = await query.Where(x => linked.Contains(x.Id)).ToArrayAsync(ct);
        var facts = new List<TugDailyExecutionFact>();
        foreach (var x in rows) facts.Add(new(x.Id, x.State, x.StartedAt, x.EndedAt, TowageExecutionPolicy.Ledger(await TowageExecutionEndpoints.LedgerEvents(db, x.Id, ct), now).ActiveIntervals));
        var result = TugDailyLogPolicy.Reconcile(content, previous, interval, facts, now);
        return new(result, interval, rows.OrderBy(x => x.StartedAt).Select(x => new TugDailyExecutionDto(x.Id, x.JobId, x.State, x.StartedAt, x.EndedAt, linked.Contains(x.Id))).ToArray());
    }

    private static async Task<object?> ReadView(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, CancellationToken ct) {
        var l = await db.TugDailyLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        if (l == null) return null;
        var content = Content(l.ContentJson); var now = TugReadinessHttp.Now(h);
        var r = await Reconcile(db, l, content, now, ct);
        var reviews = await db.TugDailyLogReviews.AsNoTracking().Where(v => v.LogId == l.Id).OrderByDescending(v => v.DecidedAt).Take(32)
            .Select(v => new TugDailyLogReviewDto(v.Id, v.Revision, v.ResultRevision, v.Decision, v.Reconciliation, v.Duty, v.ActorId, v.Reason, v.DecidedAt)).ToArrayAsync(ct);
        var revisions = await db.TugDailyLogRevisions.AsNoTracking().Where(v => v.LogId == l.Id).OrderByDescending(v => v.Revision).Take(50)
            .Select(v => new TugDailyLogRevisionDto(v.Revision, v.State, v.ReasonCode, v.Hash, v.ActorId, v.CapturedAt, v.SyncedAt)).ToArrayAsync(ct);
        var grants = TugOperationsAuthorities.Resolve(h, a, l.VesselId, TugOperationsAuthorities.MasterDuties);
        var write = TugReadinessHttp.Write(a, l.VesselId);
        var authors = FieldOpsCommands.Authors(l.AuthorsJson);
        return new TugDailyLogView(Dto(l), r.Result, TugDailyLogPolicy.MasterReview(l.State, r.Result.State), content.DefectIds.Length, "UNKNOWN:SafetyEventSourceUnavailable",
            r.Interval?.From, r.Interval?.To, r.Executions, l.State == TugDailyLogPolicy.Draft ? TugDailyLogPolicy.SubmitBlockers(content, Zone(content.TimeZone) != null) : [],
            reviews, revisions, TugOperationsAuthorities.Dto(grants), write && l.State is TugDailyLogPolicy.Draft or TugDailyLogPolicy.Returned, write && l.State == TugDailyLogPolicy.Draft,
            l.State == TugDailyLogPolicy.Submitted && grants.Length > 0 && write && !authors.Contains(a.Id) && l.SubmittedBy != a.Id, TugDailyLogPolicy.FuelUnits);
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TowageCommands.Fail(h, Read, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TowageCommands.Fail(h, Read, 400, "invalid_request");
        var vessel = q["vessel"]; var ct = h.RequestAborted;
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TowageCommands.Fail(h, Read, 404, "resource_not_found");
        var rows = await db.TugDailyLogs.AsNoTracking().Where(l => l.VesselId == vessel).OrderByDescending(l => l.LogDate).ThenByDescending(l => l.SavedAt).Take(62).ToArrayAsync(ct);
        await TugReadinessHttp.Observe(h, Read, "Success", vessel);
        return Results.Json(new TugDailyLogListView(vessel, rows.Select(l => new TugDailyLogSummaryDto(l.Id, l.LogDate, l.Watch, l.Revision, l.State,
            l.State == TugDailyLogPolicy.Verified ? "REVIEWED" : l.State == TugDailyLogPolicy.Returned ? "ATTENTION" : "PENDING", l.SyncedAt)).ToArray(), TugReadinessHttp.Write(a, vessel), TugDailyLogPolicy.Watches, TugDailyLogPolicy.FuelUnits),
            FieldOpsJson.Options);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TowageCommands.Fail(h, Read, 403, "permission_denied", id);
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TowageCommands.Fail(h, Read, 400, "invalid_request", id);
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == q["vessel"], h.RequestAborted)) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", id);
        var view = await ReadView(h, db, a, q["vessel"], id, h.RequestAborted);
        if (view == null) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", id);
        await TugReadinessHttp.Observe(h, Read, "Success", id);
        return Results.Json(view, FieldOpsJson.Options);
    }
}
