using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TugSnapshotJob(Guid Id, string JobNumber, string Status, long Revision, string? AssistedVessel, string? JobType, string? OperationArea,
    DateTimeOffset? RequestedStart, DateTimeOffset? RequestedEnd);
public sealed record TugSnapshotBriefing(Guid Id, long Revision, string State, string ContentHash, bool Participant, bool Acknowledged, string AcknowledgementState);
public sealed record TugSnapshotExecution(Guid Id, Guid TugId, string State, long Sequence, string Hash, bool CanRecord, bool TowlineConnected, string[] RecordableKinds);
public sealed record TugSnapshotDailyLog(Guid Id, Guid TugId, DateOnly LogDate, string Watch, string State, uint Version, long Revision, TugDailyLogContent Content);
public sealed record TugOperationsSnapshotDto(Guid Id, Guid VesselId, Guid JobId, string PolicyVersion, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, string SnapshotHash,
    TugSnapshotJob Job, TugSnapshotBriefing? Briefing, TugSnapshotExecution[] Executions, TugSnapshotDailyLog[] DailyLogs, Guid[] WritableTugs, string[] Operations,
    string[] OnlineOnly, string[] Watches, string Classification = "ReadOnlyOperationalSnapshot;NotAReleaseOrGoDecision",
    string SourceVersion = TugOperationsOfflineRules.SourceVersion);
public sealed record TugOperationsOfflineCapabilityDto(Guid VesselId, string State, string[] Operations, string[] OnlineOnly, TugOperationsSnapshotLeaseDto[] Snapshots);
public sealed record TugOperationsSnapshotLeaseDto(Guid Id, Guid JobId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugOfflineBriefingAck([property: JsonRequired] Guid BriefingId, [property: JsonRequired] long Revision, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Choice, string? Reason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugOfflineExecutionEvent([property: JsonRequired] Guid ExecutionId, [property: JsonRequired] string BaseState, [property: JsonRequired] long BaseSequence,
    [property: JsonRequired] TowageEventInput Event);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugOfflineDailyEntry([property: JsonRequired] Guid LogId, [property: JsonRequired] Guid TugId, uint? BaseVersion, [property: JsonRequired] DateOnly LogDate,
    [property: JsonRequired] string Watch, [property: JsonRequired] TugDailyLogContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugOperationsOfflineCommand([property: JsonRequired] Guid CommandId, [property: JsonRequired] Guid SnapshotId, [property: JsonRequired] string Operation,
    [property: JsonRequired] DateTimeOffset CapturedAt, [property: JsonRequired] int OffsetMinutes, TugOfflineBriefingAck? BriefingAck = null,
    TugOfflineExecutionEvent? ExecutionEvent = null, TugOfflineDailyEntry? DailyEntry = null, Guid? ParentCommandId = null, string? Choice = null, string? Reason = null)
{ public override string ToString() => nameof(TugOperationsOfflineCommand); }
public sealed record TugOperationsOfflineResult(Guid CommandId, string Operation, string Result, Guid ResourceId, uint BaseVersion, uint ServerVersion,
    string[] ConflictFields, string[] Choices, Guid? ParentCommandId, DateTimeOffset ReceivedAt);
public sealed record TugOperationsServerState(long? Revision, string? Hash, string? State, long? Sequence, uint? Version);
public sealed record TugOperationsConflictDto(Guid CommandId, string Operation, Guid ResourceId, string[] ConflictFields, string[] Choices, bool Resolved,
    JsonElement Captured, TugOperationsServerState AtConflict, TugOperationsServerState Current);
internal sealed record TugOperationsProtected(JsonElement Captured, TugOperationsServerState Server);

// DEV-092 / T-TUG-013 /api/v1/tug-operations-offline?vessel=. A job snapshot is prefetched under an explicit operator
// policy (lease + hash recorded); briefing acknowledgements, execution ledger observations and daily-log entries captured
// offline replay through ONE strict command: CSRF, Idempotency-Key == CommandId, SERIALIZABLE, attributed EF audit and an
// immutable outcome per actor command (same command + same bytes returns the first outcome; different bytes are refused).
// A captured base that no longer matches the server on a material field becomes an explicit MaterialConflict listed in the
// conflict center; only a later human command (keepServer / useCaptured with a reason) resolves it. Every applied capture
// passes the owning module's ordinary rules (TowageEndpoints/TowageExecutionEndpoints/TugDailyLogEndpoints); this file adds
// no authority. Master release, readiness and execution start/transitions are refused here: they stay online human records.
public static class TugOperationsOfflineEndpoints
{
    private const string Read = "TugOperationsOfflineRead", Command = "TugOperationsOfflineCommand";
    private const int MaxBody = 262144;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static void MapTugOperationsOffline(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/tug-operations-offline").RequireAuthorization("DirectoryRead");
        g.MapGet("", Capability).WithName("TugOperationsOfflineCapability").Produces<TugOperationsOfflineCapabilityDto>();
        g.MapPost("/snapshots", Snapshot).WithName("TugOperationsOfflineSnapshot").Produces<TugOperationsSnapshotDto>();
        g.MapPost("/replay", Replay).WithName("TugOperationsOfflineReplay").Produces<TugOperationsOfflineResult>();
        g.MapGet("/outcomes", Outcomes).WithName("TugOperationsOfflineOutcomes");
        g.MapGet("/conflicts/{command:guid}", Conflict).WithName("TugOperationsOfflineConflict").Produces<TugOperationsConflictDto>();
    }

    private static TugOperationsOfflinePolicy? Policy(HttpContext h) => h.RequestServices.GetRequiredService<TugOperationsOfflineSettings>().Current;
    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static DateTimeOffset Now(HttpContext h) { var now = TugReadinessHttp.Now(h).ToUniversalTime(); return now.AddTicks(-(now.Ticks % 10)); }
    private static string Authority(TrustedActor a) => RegisterStorage.Hash(string.Join(',', a.Permissions.Where(p => p.StartsWith("tug.", StringComparison.Ordinal) ||
        p.StartsWith("hse.", StringComparison.Ordinal)).Order(StringComparer.Ordinal)));
    private static IDataProtector Protector(HttpContext h, Guid tenant, Guid actor, Guid snapshot) => h.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("Poseidon.Tug.OperationsOffline.v1", tenant.ToString("D"), actor.ToString("D"), snapshot.ToString("D"));
    private static string Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string PayloadDigest(TugOperationsOfflineCommand c) => Hex(JsonSerializer.SerializeToUtf8Bytes(new { c.Operation, c.BriefingAck, c.ExecutionEvent, c.DailyEntry }, Json));
    private static Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null, string[]? missing = null) =>
        TowageCommands.Fail(h, action, status, code, id, missing);

    // Exact actor, tenant and tug vessel scope. Unknown query keys are refused.
    private static async Task<(TrustedActor? Actor, Dictionary<string, Guid> Query)> Gate(HttpContext h, PoseidonDbContext db, string[] required, string[] optional)
    {
        if (!TugReadinessHttp.Query(h, required, optional, out var q)) return (null, q);
        var a = Actor(h);
        if (a == null || !a.CanReadTug || db.TenantId != a.TenantId) return (null, q);
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == q["vessel"], h.RequestAborted)) return (null, q);
        return (a, q);
    }
    private static async Task<bool> Csrf(HttpContext h)
    {
        if (h.Request.Headers["X-CSRF-TOKEN"].Count != 1) return false;
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static async Task<byte[]> Body(HttpContext h, int maximum)
    {
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > maximum) throw new JsonException();
        using var stream = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, stream, maximum, h.RequestAborted);
        using var document = JsonDocument.Parse(stream.ToArray(), new() { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
        return stream.ToArray();
    }
    private static async Task<IResult> Failure(HttpContext h, PoseidonDbContext db, Exception error, Guid? id)
    {
        db.ChangeTracker.Clear();
        var conflict = error is DbUpdateConcurrencyException || PostgresFailure.IsConflict(error);
        return await Fail(h, Command, conflict ? 409 : 503, conflict ? "offline_retry" : "service_unavailable", id);
    }

    // Tugs the actor may write for this job: the job's own vessel, executing tugs and proposed tugs within write scope.
    private static async Task<Guid[]> WritableTugs(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid job, CancellationToken ct)
    {
        var visible = TugReadinessHttp.Tugs(db, a).Select(v => v.Id);
        var executing = await db.TowageExecutions.AsNoTracking().Where(x => x.VesselId == vessel && x.JobId == job && visible.Contains(x.TugId)).Select(x => x.TugId).ToArrayAsync(ct);
        var proposed = await db.TugAssignmentProposals.AsNoTracking().Where(p => p.VesselId == vessel && p.JobId == job && p.State == TugAssignmentWorkflow.Proposed && visible.Contains(p.TugId))
            .Select(p => p.TugId).ToArrayAsync(ct);
        return executing.Concat(proposed).Append(vessel).Distinct().Where(t => TugReadinessHttp.Write(a, t)).Order().Take(12).ToArray();
    }

    private static async Task<IResult> Capability(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var (a, q) = await Gate(h, db, ["vessel"], []);
        if (a == null) return await Fail(h, Read, 404, "resource_not_found");
        var p = Policy(h); var now = Now(h);
        var rows = await db.TugOperationOfflineSnapshots.AsNoTracking().Where(s => s.ActorId == a.Id && s.VesselId == q["vessel"] && s.ExpiresAt > now)
            .OrderByDescending(s => s.IssuedAt).ThenBy(s => s.Id).Take(32).Select(s => new TugOperationsSnapshotLeaseDto(s.Id, s.JobId, s.IssuedAt, s.ExpiresAt)).ToArrayAsync(h.RequestAborted);
        await TugReadinessHttp.Observe(h, Read, "Success", q["vessel"]);
        return Results.Json(new TugOperationsOfflineCapabilityDto(q["vessel"], p != null && a.HasWriteAttribution ? "CONFIGURED_UNVERIFIED" : "UNAVAILABLE_POLICY",
            TugOperationsOfflineRules.CaptureOperations, TugOperationsOfflineRules.OnlineOnly, rows), FieldOpsJson.Options);
    }

    // Read-only snapshot of the job under current authorization; the hash pins exactly what the device holds.
    private static async Task<TugOperationsSnapshotDto?> Compose(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid jobId, Guid id, string policyVersion,
        DateTimeOffset issued, DateTimeOffset expires, CancellationToken ct)
    {
        var job = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId && j.VesselId == vessel, ct);
        if (job == null) return null;
        var briefingRow = await db.TowageBriefings.AsNoTracking().SingleOrDefaultAsync(b => b.VesselId == vessel && b.JobId == jobId, ct);
        TugSnapshotBriefing? briefing = null;
        if (briefingRow != null) {
            var content = TowageReader.BriefingContent(briefingRow.ContentJson);
            var acks = await db.TowageBriefingAcknowledgements.AsNoTracking().Where(k => k.BriefingId == briefingRow.Id && k.Revision == briefingRow.Revision)
                .Select(k => new { k.ActorId, k.Choice }).ToArrayAsync(ct);
            briefing = new(briefingRow.Id, briefingRow.Revision, briefingRow.State, briefingRow.Hash, content.Participants.Any(p => p.ActorId == a.Id),
                acks.Any(k => k.ActorId == a.Id), TowageBriefingPolicy.AcknowledgementState(briefingRow.State, content, acks.Select(k => (k.ActorId, k.Choice)).ToArray()));
        }
        var visible = TugReadinessHttp.Tugs(db, a).Select(v => v.Id);
        var rows = await db.TowageExecutions.AsNoTracking().Where(x => x.VesselId == vessel && x.JobId == jobId && visible.Contains(x.TugId)).OrderBy(x => x.StartedAt).Take(12).ToArrayAsync(ct);
        var executions = new List<TugSnapshotExecution>();
        foreach (var x in rows) {
            var running = x.State is TowageExecutionPolicy.Active or TowageExecutionPolicy.Suspended;
            var ledger = TowageExecutionPolicy.Ledger(await TowageExecutionEndpoints.LedgerEvents(db, x.Id, ct), issued);
            executions.Add(new(x.Id, x.TugId, x.State, x.Sequence, x.Hash, TugReadinessHttp.Write(a, x.TugId) && x.VerificationState == TowageExecutionPolicy.Pending,
                ledger.TowlineConnected, running ? TowageExecutionPolicy.RecordableKinds : TowageExecutionPolicy.TerminalKinds));
        }
        var tugs = await WritableTugs(db, a, vessel, jobId, ct);
        var from = DateOnly.FromDateTime(issued.UtcDateTime).AddDays(-7);
        var logs = await db.TugDailyLogs.AsNoTracking().Where(l => tugs.Contains(l.VesselId) && l.LogDate >= from).OrderByDescending(l => l.LogDate).ThenBy(l => l.Watch).Take(28)
            .ToArrayAsync(ct);
        var daily = logs.Select(l => new TugSnapshotDailyLog(l.Id, l.VesselId, l.LogDate, l.Watch, l.State, l.Version, l.Revision,
            JsonSerializer.Deserialize<TugDailyLogContent>(l.ContentJson, FieldOpsJson.Options) ?? throw new InvalidDataException("daily_log_unavailable"))).ToArray();
        var dto = new TugOperationsSnapshotDto(id, vessel, jobId, policyVersion, issued, expires, "", new(job.Id, job.JobNumber, job.Status, job.Revision, job.AssistedVessel,
            job.JobType, job.OperationArea, job.RequestedStart, job.RequestedEnd), briefing, [.. executions], daily, tugs, TugOperationsOfflineRules.CaptureOperations,
            TugOperationsOfflineRules.OnlineOnly, TugDailyLogPolicy.Watches);
        return dto with { SnapshotHash = Hex(JsonSerializer.SerializeToUtf8Bytes(dto, FieldOpsJson.Options)) };
    }

    private static async Task<IResult> Snapshot(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var (a, q) = await Gate(h, db, ["vessel", "job"], []);
        if (a == null) return await Fail(h, Command, 404, "resource_not_found");
        if (!a.HasWriteAttribution) return await Fail(h, Command, 403, "permission_denied", q["job"]);
        if (!await Csrf(h)) return await Fail(h, Command, 400, "csrf_invalid", q["job"]);
        try { using var empty = JsonDocument.Parse(await Body(h, 64)); if (empty.RootElement.EnumerateObject().Any()) throw new JsonException(); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException) { return await Fail(h, Command, 400, "invalid_request", q["job"]); }
        var p = Policy(h); if (p == null) return await Fail(h, Command, 403, "offline_policy_denied", q["job"]);
        var vessel = q["vessel"]; var jobId = q["job"]; var now = Now(h);
        await TugReadinessHttp.Observe(h, Command, "Attempted", jobId);
        TugOperationsSnapshotDto? dto = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)) {
            try {
                if (await db.TugOperationOfflineSnapshots.CountAsync(s => s.ActorId == a.Id && s.VesselId == vessel && s.JobId == jobId && s.ExpiresAt > now, ct) >= p.MaxActiveSnapshots)
                    { await tx.RollbackAsync(CancellationToken.None); return await Fail(h, Command, 409, "enrollment_limit", jobId); }
                var id = Guid.NewGuid(); var expires = now.AddSeconds(p.LeaseSeconds);
                dto = await Compose(db, a, vessel, jobId, id, p.Version, now, expires, ct);
                if (dto == null) { await tx.RollbackAsync(CancellationToken.None); return await Fail(h, Command, 404, "resource_not_found", jobId); }
                db.TugOperationOfflineSnapshots.Add(new() { TenantId = a.TenantId, Id = id, ActorId = a.Id, VesselId = vessel, JobId = jobId, PolicyVersion = p.Version,
                    PolicyDigest = p.Fingerprint(), AuthorityDigest = Authority(a), SnapshotHash = dto.SnapshotHash, IssuedAt = now, ExpiresAt = expires });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            } catch (Exception ex) when (ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)) {
                await tx.RollbackAsync(CancellationToken.None); return await Failure(h, db, ex, jobId);
            }
        }
        await TugReadinessHttp.Observe(h, Command, "Success", jobId);
        return Results.Json(dto, FieldOpsJson.Options);
    }

    private static uint Version(long value) => value is >= 0 and <= uint.MaxValue ? (uint)value : throw new InvalidDataException("version_out_of_range");

    private sealed record Evaluation(Guid ResourceId, uint BaseVersion, TugOperationsServerState Server, string[] Fields, string? CurrentState, IResult? Failure = null);

    // Reads the current owning record for a capture and compares the captured base on material fields only.
    private static async Task<Evaluation> Evaluate(HttpContext h, PoseidonDbContext db, TrustedActor a, TugOperationOfflineSnapshot s, TugOperationsOfflineCommand c, CancellationToken ct)
    {
        switch (c.Operation) {
            case TugOperationsOfflineRules.BriefingAck: {
                var k = c.BriefingAck!;
                var b = await db.TowageBriefings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == k.BriefingId && x.VesselId == s.VesselId && x.JobId == s.JobId, ct);
                if (b == null) return new(k.BriefingId, 0, new(null, null, null, null, null), [], null, await Fail(h, Command, 404, "resource_not_found", k.BriefingId));
                var server = new TugOperationsServerState(b.Revision, b.Hash, b.State, null, null);
                return new(b.Id, Version(k.Revision), server, TugOperationsOfflineRules.BriefingConflict(k.Revision, k.ContentHash, b.Revision, b.Hash, b.State), b.State);
            }
            case TugOperationsOfflineRules.ExecutionEvent: {
                var e = c.ExecutionEvent!;
                var x = await db.TowageExecutions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == e.ExecutionId && r.VesselId == s.VesselId && r.JobId == s.JobId, ct);
                if (x == null || !await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == x.TugId, ct))
                    return new(e.ExecutionId, 0, new(null, null, null, null, null), [], null, await Fail(h, Command, 404, "resource_not_found", e.ExecutionId));
                var server = new TugOperationsServerState(x.Revision, x.Hash, x.State, x.Sequence, x.Version);
                return new(x.Id, Version(e.BaseSequence), server, TugOperationsOfflineRules.ExecutionConflict(e.BaseState, e.BaseSequence, x.State, x.Sequence, e.Event.Kind), x.State);
            }
            default: {
                var d = c.DailyEntry!;
                var log = await db.TugDailyLogs.AsNoTracking().SingleOrDefaultAsync(l => l.Id == d.LogId, ct);
                if (log != null && log.VesselId != d.TugId) return new(d.LogId, 0, new(null, null, null, null, null), [], null, await Fail(h, Command, 404, "resource_not_found", d.LogId));
                var slot = log == null && await db.TugDailyLogs.AnyAsync(l => l.VesselId == d.TugId && l.LogDate == d.LogDate && l.Watch == d.Watch, ct);
                var server = new TugOperationsServerState(log?.Revision, log?.Hash, log?.State, null, log?.Version);
                return new(d.LogId, d.BaseVersion ?? 0, server, TugOperationsOfflineRules.DailyConflict(d.BaseVersion, log != null, slot, log?.Version, log?.State), log?.State);
            }
        }
    }

    // Applies one capture through the owning module's ordinary rules on the CURRENT base (resolution) or captured base.
    private static async Task<TowageOutcome> Apply(HttpContext h, PoseidonDbContext db, TrustedActor a, TugOperationOfflineSnapshot s, TugOperationsOfflineCommand c,
        Evaluation current, bool rebase, DateTimeOffset now, CancellationToken ct)
    {
        switch (c.Operation) {
            case TugOperationsOfflineRules.BriefingAck: {
                var k = c.BriefingAck!;
                return await TowageEndpoints.AcknowledgeBriefing(new(h, a, s.VesselId, k.BriefingId, new TowageBriefingAcknowledgeCommand(k.Revision, k.ContentHash, k.Choice, k.Reason), now, db, ct));
            }
            case TugOperationsOfflineRules.ExecutionEvent: {
                var e = c.ExecutionEvent!;
                return await TowageExecutionEndpoints.Record(new(h, a, s.VesselId, e.ExecutionId, new TowageExecutionEventCommand(e.Event), now, db, ct));
            }
            default: {
                var d = c.DailyEntry!;
                if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == d.TugId, ct) || !(await WritableTugs(db, a, s.VesselId, s.JobId, ct)).Contains(d.TugId))
                    return TowageOutcome.Fail(403, "permission_denied");
                if (d.BaseVersion == null && !rebase)
                    return await TugDailyLogEndpoints.Create(new(h, a, d.TugId, null, new TugDailyLogCreateCommand(d.LogId, d.LogDate, d.Watch, c.CapturedAt, d.Content), now, db, ct));
                var expected = rebase ? current.Server.Version ?? 0 : d.BaseVersion!.Value;
                return await TugDailyLogEndpoints.Update(new(h, a, d.TugId, d.LogId, new TugDailyLogUpdateCommand(expected, c.CapturedAt, d.Content), now, db, ct));
            }
        }
    }

    private static bool Shape(TugOperationsOfflineCommand c) =>
        c.CommandId != Guid.Empty && c.SnapshotId != Guid.Empty && TugOperationsOfflineRules.CaptureOperations.Contains(c.Operation) &&
        (c.BriefingAck != null ? 1 : 0) + (c.ExecutionEvent != null ? 1 : 0) + (c.DailyEntry != null ? 1 : 0) == 1 &&
        (c.Operation != TugOperationsOfflineRules.BriefingAck || c.BriefingAck is { BriefingId: var b } && b != Guid.Empty && TowageBriefingPolicy.AckChoices.Contains(c.BriefingAck.Choice)) &&
        (c.Operation != TugOperationsOfflineRules.ExecutionEvent || c.ExecutionEvent is { ExecutionId: var x } && x != Guid.Empty && TowageExecutionPolicy.States.Contains(c.ExecutionEvent.BaseState) &&
            c.ExecutionEvent.BaseSequence > 0 && TowageExecutionPolicy.ValidEvent(c.ExecutionEvent.Event)) &&
        (c.Operation != TugOperationsOfflineRules.DailyEntry || c.DailyEntry is { LogId: var l, TugId: var t } && l != Guid.Empty && t != Guid.Empty &&
            TugDailyLogPolicy.Watches.Contains(c.DailyEntry.Watch) && c.DailyEntry.BaseVersion is null or > 0) &&
        (c.ParentCommandId == null ? c.Choice == null && c.Reason == null :
            c.ParentCommandId != Guid.Empty && c.Choice is TugOperationsOfflineRules.KeepServer or TugOperationsOfflineRules.UseCaptured &&
            TugOperationsOfflineRules.ResolutionReasons.Contains(c.Reason));

    private static async Task<IResult> Replay(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var (a, q) = await Gate(h, db, ["vessel"], []);
        if (a == null) return await Fail(h, Command, 404, "resource_not_found");
        if (!a.HasWriteAttribution) return await Fail(h, Command, 403, "permission_denied");
        if (!await Csrf(h)) return await Fail(h, Command, 400, "csrf_invalid");
        byte[] bytes; TugOperationsOfflineCommand c; string? capturedText;
        try {
            bytes = await Body(h, MaxBody);
            c = JsonSerializer.Deserialize<TugOperationsOfflineCommand>(bytes, Json) ?? throw new JsonException();
            using var parsed = JsonDocument.Parse(bytes);
            capturedText = parsed.RootElement.GetProperty("capturedAt").GetString();
        } catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or FormatException or NotSupportedException) {
            return await Fail(h, Command, 400, "invalid_offline_command");
        }
        if (!Shape(c) || capturedText == null || !(capturedText.EndsWith('Z') || capturedText.EndsWith("+00:00", StringComparison.Ordinal)))
            return await Fail(h, Command, 400, "invalid_offline_command", c.CommandId == Guid.Empty ? null : c.CommandId);
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || keys[0] != c.CommandId.ToString("D")) return await Fail(h, Command, 400, "invalid_idempotency_key", c.CommandId);
        var p = Policy(h); if (p == null) return await Fail(h, Command, 403, "offline_policy_denied", c.CommandId);
        var vessel = q["vessel"]; var digest = Hex(bytes); var payload = PayloadDigest(c); var now = Now(h);
        await TugReadinessHttp.Observe(h, Command, "Attempted", c.CommandId);
        TugOperationsOfflineResult? result = null; TowageOutcome? failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)) {
            try {
                var s = await db.TugOperationOfflineSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == c.SnapshotId && x.ActorId == a.Id && x.VesselId == vessel, ct);
                if (s == null) { await tx.RollbackAsync(CancellationToken.None); return await Fail(h, Command, 404, "offline_snapshot_unavailable", c.CommandId); }
                // Idempotent reconnect: identical bytes return the immutable first outcome; different bytes are refused.
                var previous = await db.TugOperationOfflineOutcomes.AsNoTracking().SingleOrDefaultAsync(o => o.ActorId == a.Id && o.CommandId == c.CommandId, ct);
                if (previous != null) {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (previous.Digest != digest || previous.SnapshotId != s.Id) return await Fail(h, Command, 409, "offline_command_reused", c.CommandId);
                    await TugReadinessHttp.Observe(h, Command, "Success", previous.ResourceId);
                    return Results.Content(previous.OutcomeJson, "application/json");
                }
                if (s.PolicyVersion != p.Version || s.PolicyDigest != p.Fingerprint() || s.AuthorityDigest != Authority(a)) {
                    await tx.RollbackAsync(CancellationToken.None); return await Fail(h, Command, 403, "offline_policy_denied", c.CommandId);
                }
                TugOperationOfflineOutcome? parent = null;
                if (c.ParentCommandId != null) {
                    parent = await db.TugOperationOfflineOutcomes.AsNoTracking().SingleOrDefaultAsync(o => o.ActorId == a.Id && o.CommandId == c.ParentCommandId &&
                        o.SnapshotId == s.Id && o.Result == TugOperationsOfflineRules.MaterialConflict, ct);
                    if (parent == null || parent.Operation != c.Operation || parent.PayloadDigest != payload ||
                        await db.TugOperationOfflineOutcomes.AnyAsync(o => o.ActorId == a.Id && o.ParentCommandId == parent.CommandId, ct)) {
                        await tx.RollbackAsync(CancellationToken.None); return await Fail(h, Command, 409, "offline_conflict_changed", c.CommandId);
                    }
                    if (c.CapturedAt > now + TowageExecutionPolicy.ClockSkew) { await tx.RollbackAsync(CancellationToken.None); return await Fail(h, Command, 400, "invalid_offline_command", c.CommandId); }
                } else if (!TugOperationsOfflineRules.ValidCapture(c.CapturedAt, c.OffsetMinutes, s.IssuedAt, s.ExpiresAt, now)) {
                    await tx.RollbackAsync(CancellationToken.None); return await Fail(h, Command, 409, "offline_recovery_required", c.CommandId);
                }
                var current = await Evaluate(h, db, a, s, c, ct);
                if (current.Failure != null) { await tx.RollbackAsync(CancellationToken.None); return current.Failure; }
                string outcome; string[] fields = current.Fields;
                if (parent != null) {
                    var allowed = TugOperationsOfflineRules.Choices(c.Operation, parent.ConflictFields.Split(',', StringSplitOptions.RemoveEmptyEntries), current.CurrentState,
                        c.ExecutionEvent?.Event.Kind ?? "");
                    if (!allowed.Contains(c.Choice)) { await tx.RollbackAsync(CancellationToken.None); return await Fail(h, Command, 409, "invalid_offline_resolution", c.CommandId); }
                    if (c.Choice == TugOperationsOfflineRules.KeepServer) { outcome = TugOperationsOfflineRules.ServerRetained; fields = []; }
                    else {
                        var applied = await Apply(h, db, a, s, c, current, true, now, ct);
                        if (applied.Status != 200) { failure = applied; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); goto Done; }
                        outcome = TugOperationsOfflineRules.Committed; fields = [];
                    }
                } else if (fields.Length > 0) outcome = TugOperationsOfflineRules.MaterialConflict;
                else {
                    var applied = await Apply(h, db, a, s, c, current, false, now, ct);
                    if (applied.Status != 200) { failure = applied; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); goto Done; }
                    outcome = TugOperationsOfflineRules.Committed;
                }
                db.ChangeTracker.DetectChanges();
                var after = outcome == TugOperationsOfflineRules.Committed ? ServerVersion(db, c, current) : current.Server.Version ?? Version(current.Server.Sequence ?? current.Server.Revision ?? 0);
                var choices = outcome == TugOperationsOfflineRules.MaterialConflict ? TugOperationsOfflineRules.Choices(c.Operation, fields, current.CurrentState, c.ExecutionEvent?.Event.Kind ?? "") : [];
                result = new(c.CommandId, c.Operation, outcome, current.ResourceId, current.BaseVersion, after, fields, choices, c.ParentCommandId, now);
                var captured = JsonSerializer.SerializeToElement(new { c.BriefingAck, c.ExecutionEvent, c.DailyEntry }, Json);
                db.TugOperationOfflineOutcomes.Add(new() { TenantId = a.TenantId, ActorId = a.Id, CommandId = c.CommandId, SnapshotId = s.Id, VesselId = s.VesselId, JobId = s.JobId,
                    Operation = c.Operation, ResourceId = current.ResourceId, ParentCommandId = c.ParentCommandId, Digest = digest, PayloadDigest = payload, Result = outcome,
                    BaseVersion = current.BaseVersion, ServerVersion = after, ConflictFields = string.Join(',', fields),
                    ProtectedContext = Protector(h, a.TenantId, a.Id, s.Id).Protect(JsonSerializer.Serialize(new TugOperationsProtected(captured, current.Server), Json)),
                    OutcomeJson = JsonSerializer.Serialize(result, Json), CapturedAt = c.CapturedAt.ToUniversalTime(), OffsetMinutes = c.OffsetMinutes, ReceivedAt = now });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            } catch (Exception ex) when (ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)) {
                await tx.RollbackAsync(CancellationToken.None); return await Failure(h, db, ex, c.CommandId);
            } catch (InvalidDataException) {
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Fail(h, Command, 409, "offline_conflict_changed", c.CommandId);
            }
            Done:;
        }
        if (failure != null) return await Fail(h, Command, failure.Status, failure.Code!, c.CommandId, failure.Missing);
        await TugReadinessHttp.Observe(h, Command, "Success", result!.ResourceId);
        return Results.Json(result, Json);
    }

    // Version the owning record reports after the committed capture (tracked entity, before commit).
    private static uint ServerVersion(PoseidonDbContext db, TugOperationsOfflineCommand c, Evaluation current) =>
        c.Operation switch {
            TugOperationsOfflineRules.BriefingAck => Version(current.Server.Revision ?? 0),
            TugOperationsOfflineRules.ExecutionEvent => Version(db.ChangeTracker.Entries<TowageExecution>().SingleOrDefault(e => e.Entity.Id == current.ResourceId)?.Entity.Sequence ?? 0),
            _ => Version(db.ChangeTracker.Entries<TugDailyLog>().SingleOrDefault(e => e.Entity.Id == current.ResourceId)?.Entity.Revision ?? 0)
        };

    private static async Task<IResult> Outcomes(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var (a, q) = await Gate(h, db, ["vessel", "job"], []);
        if (a == null) return await Fail(h, Read, 404, "resource_not_found");
        var rows = await db.TugOperationOfflineOutcomes.AsNoTracking().Where(o => o.ActorId == a.Id && o.VesselId == q["vessel"] && o.JobId == q["job"])
            .OrderByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Take(101).Select(o => o.OutcomeJson).ToArrayAsync(h.RequestAborted);
        await TugReadinessHttp.Observe(h, Read, "Success", q["job"]);
        return Results.Json(new { items = rows.Take(100).Select(r => JsonSerializer.Deserialize<TugOperationsOfflineResult>(r, Json)), hasMore = rows.Length > 100 }, Json);
    }

    // Protected conflict detail: only the actor who captured it, under current scope; choices are recomputed now.
    private static async Task<IResult> Conflict(Guid command, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var (a, q) = await Gate(h, db, ["vessel"], []);
        if (a == null) return await Fail(h, Read, 404, "resource_not_found", command);
        var o = await db.TugOperationOfflineOutcomes.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.CommandId == command && x.VesselId == q["vessel"] &&
            x.Result == TugOperationsOfflineRules.MaterialConflict, ct);
        if (o == null) return await Fail(h, Read, 404, "resource_not_found", command);
        var s = await db.TugOperationOfflineSnapshots.AsNoTracking().SingleAsync(x => x.Id == o.SnapshotId && x.ActorId == a.Id, ct);
        var context = JsonSerializer.Deserialize<TugOperationsProtected>(Protector(h, a.TenantId, a.Id, s.Id).Unprotect(o.ProtectedContext), Json)!;
        var capture = new TugOperationsOfflineCommand(o.CommandId, s.Id, o.Operation, o.CapturedAt, o.OffsetMinutes,
            context.Captured.TryGetProperty("briefingAck", out var k) && k.ValueKind == JsonValueKind.Object ? k.Deserialize<TugOfflineBriefingAck>(Json) : null,
            context.Captured.TryGetProperty("executionEvent", out var e) && e.ValueKind == JsonValueKind.Object ? e.Deserialize<TugOfflineExecutionEvent>(Json) : null,
            context.Captured.TryGetProperty("dailyEntry", out var d) && d.ValueKind == JsonValueKind.Object ? d.Deserialize<TugOfflineDailyEntry>(Json) : null);
        var current = await Evaluate(h, db, a, s, capture, ct);
        if (current.Failure != null) return current.Failure;
        var resolved = await db.TugOperationOfflineOutcomes.AnyAsync(x => x.ActorId == a.Id && x.ParentCommandId == command, ct);
        var fields = o.ConflictFields.Split(',', StringSplitOptions.RemoveEmptyEntries);
        await TugReadinessHttp.Observe(h, Read, "Success", o.ResourceId);
        return Results.Json(new TugOperationsConflictDto(o.CommandId, o.Operation, o.ResourceId, fields,
            resolved ? [] : TugOperationsOfflineRules.Choices(o.Operation, fields, current.CurrentState, capture.ExecutionEvent?.Event.Kind ?? ""), resolved,
            context.Captured, context.Server, current.Server), Json);
    }
}
