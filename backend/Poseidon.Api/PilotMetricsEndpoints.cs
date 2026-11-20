using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Operator configuration (deployment), never a product default: without an approved retention
// period and its policy reference the pilot telemetry collects nothing (privacy by default,
// security baseline "Saklama süreleri veri sınıfına göre kontrollü policy"). The 54.01 registry
// key retention.policy_ref (ordinal 12, 58.02) is the target owner binding; it is not implemented.
public sealed class PilotTelemetrySettings
{
    public int? RetentionDays { get; set; }
    public string? RetentionPolicyRef { get; set; }
    internal bool Configured => RetentionDays is >= 1 and <= 400 && RetentionPolicyRef is { Length: > 0 and <= 128 } &&
        RetentionPolicyRef.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or ':' or '/');
}

// Technical abuse guard per (tenant, actor), not an approved quota: a fixed one-minute window.
public sealed class PilotTelemetryRateLimiter(TimeProvider clock)
{
    public const int RequestsPerMinute = 30;
    private readonly ConcurrentDictionary<(Guid, Guid), (long Window, int Count)> windows = new();
    public bool TryAcquire(Guid tenant, Guid actor)
    {
        var window = clock.GetUtcNow().ToUnixTimeSeconds() / 60;
        if (windows.Count > 10_000) windows.Clear();
        var next = windows.AddOrUpdate((tenant, actor), (window, 1), (_, x) => x.Window == window ? (window, x.Count + 1) : (window, 1));
        return next.Count <= RequestsPerMinute;
    }
}

// Process-local counters with closed labels only (OperationalTelemetry pattern): no tenant,
// actor, bucket or free text label. Listener failure never changes the committed outcome.
public static class PilotTelemetry
{
    public const string MeterName = "Poseidon.Pilot";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Accepted = Meter.CreateCounter<long>("poseidon.pilot.events.accepted", "{event}");
    private static readonly Counter<long> Refused = Meter.CreateCounter<long>("poseidon.pilot.batches.refused", "{batch}");
    public static void ObserveAccepted(IEnumerable<PilotMetricEventRecord> events)
    {
        foreach (var e in events)
        {
            if (!PilotMetricsPolicy.Kinds.Contains(e.Kind, StringComparer.Ordinal) || !PilotMetricsPolicy.Areas.Contains(e.Area, StringComparer.Ordinal)) continue;
            try { Accepted.Add(1, new TagList { { "poseidon.pilot.kind", e.Kind }, { "poseidon.pilot.area", e.Area } }); } catch { }
        }
    }
    public static void ObserveRefused(string reason)
    {
        var label = reason is "invalid_request" or "request_limit" or "retention_unconfigured" or "permission_denied" or "idempotency_key_reused" ? reason : "other";
        try { Refused.Add(1, new KeyValuePair<string, object?>("poseidon.pilot.reason", label)); } catch { }
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotMetricEventsCommand([property: JsonRequired] PilotMetricEventCommand[] Events);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotMetricEventCommand([property: JsonRequired] string Kind, [property: JsonRequired] string Area, string? Journey, string? AiSource,
    int? DurationMs, [property: JsonRequired] DateTimeOffset OccurredAt);
public sealed record PilotMetricIngestDto(Guid BatchId, int Accepted, string SchemaVersion, bool Replayed);
public sealed record PilotMetricCapabilitiesDto(bool CanContribute, bool CanRead, string RetentionState, int? RetentionDays, string SchemaVersion,
    int MaxEventsPerBatch, int RequestsPerMinute, string[] Kinds, string[] Areas, string[] ShipJourneys, string[] TugJourneys, string[] AiSources,
    int[] WindowDays, int MinContributors);
public sealed record PilotMetricSummaryDto(string ContractVersion, int WindowDays, DateTimeOffset From, DateTimeOffset AsOf, string Basis,
    string RetentionState, string[] Areas, PilotMetricResult[] Items);

// DEV-072 / T-S11-004. Ingest: CSRF, Idempotency-Key receipt, strict JSON, one SERIALIZABLE
// transaction through the audited SaveChanges path. Tenant, contributor and role buckets come from
// the trusted server actor; the body has no identity field and unknown members are refused.
public static class PilotMetricsEndpoints
{
    private const string ReadAction = "PilotMetricsRead", CommandAction = "PilotMetricsCommand", Operation = "PilotMetricsSubmit";
    private const int MaxBody = 32768, MaxBatches = 2000;
    private static readonly JsonSerializerOptions Json = TechnicalRecordCommands.Json;

    internal static bool CanContribute(TrustedActor? a) => a is { CanRead: true, HasWriteAttribution: true } && a.Permissions.Contains(PilotMetricsPolicy.ContributePermission);
    // Tenant-level aggregate: a vessel-only grant does not see fleet pilot analytics.
    internal static bool CanReadSummary(TrustedActor? a) => a is { CanRead: true } && (a.TenantWideRead || a.TenantWideManage) && a.Permissions.Contains(PilotMetricsPolicy.ReadPermission);

    public static void MapPilotMetrics(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/pilot-metrics").RequireAuthorization("DirectoryRead");
        g.MapGet("/capabilities", (Delegate)Capabilities).WithName("PilotMetricsCapabilities").Produces<PilotMetricCapabilitiesDto>()
            .WithSummary("Pilot telemetry schema and whether this membership is enrolled. No identity or stored value is returned.");
        g.MapPost("/events", (HttpContext h, PoseidonDbContext db) => Submit(h, db)).WithName("PilotMetricsSubmit")
            .Accepts<PilotMetricEventsCommand>("application/json").Produces<PilotMetricIngestDto>().Produces(400).Produces(403).Produces(409).Produces(429).Produces(503)
            .WithSummary("Records one closed-code pilot event batch for the trusted actor. Free text, identities and record ids are not accepted.");
        g.MapGet("/summary", (HttpContext h, PoseidonDbContext db) => Summary(h, db)).WithName("PilotMetricsSummary").Produces<PilotMetricSummaryDto>()
            .WithSummary("Pilot metric observations over a bounded window. SUPPRESSED, UNKNOWN and UNAVAILABLE carry no value; zero is never substituted.");
    }

    private static PilotTelemetrySettings Settings(HttpContext h) => h.RequestServices.GetRequiredService<PilotTelemetrySettings>();

    private static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null)
    {
        if (action == CommandAction) PilotTelemetry.ObserveRefused(code);
        return await TechnicalRecordCommands.Fail(h, action, status, code, id);
    }

    private static async Task<IResult> Capabilities(HttpContext h)
    {
        h.Response.Headers.CacheControl = "no-store";
        if (h.Request.Query.Count != 0) return await Fail(h, ReadAction, 400, "invalid_query");
        var actor = TechnicalRecordCommands.Actor(h);
        if (actor?.CanRead != true) return await Fail(h, ReadAction, 403, "permission_denied");
        var settings = Settings(h);
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success");
        return Results.Json(new PilotMetricCapabilitiesDto(CanContribute(actor) && settings.Configured, CanReadSummary(actor),
            settings.Configured ? "CONFIGURED" : "UNCONFIGURED", settings.Configured ? settings.RetentionDays : null, PilotMetricsPolicy.SchemaVersion,
            PilotMetricsPolicy.MaxEventsPerBatch, PilotTelemetryRateLimiter.RequestsPerMinute, PilotMetricsPolicy.Kinds, PilotMetricsPolicy.Areas,
            PilotMetricsPolicy.ShipJourneys, PilotMetricsPolicy.TugJourneys, PilotMetricsPolicy.AiSources, PilotMetricsPolicy.WindowDays,
            PilotMetricsPolicy.MinContributors), Json);
    }

    private static async Task<IResult> Submit(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (h.Request.Query.Count != 0) return await Fail(h, CommandAction, 400, "invalid_query");
        var actor = TechnicalRecordCommands.Actor(h);
        if (actor == null || db.TenantId != actor.TenantId || !CanContribute(actor)) return await Fail(h, CommandAction, 403, "permission_denied");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, CommandAction, 403, "csrf_invalid"); }
        var settings = Settings(h);
        if (!settings.Configured) return await Fail(h, CommandAction, 503, "retention_unconfigured");
        if (!h.RequestServices.GetRequiredService<PilotTelemetryRateLimiter>().TryAcquire(actor.TenantId, actor.Id))
            return await Fail(h, CommandAction, 429, "request_limit");
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty)
            return await Fail(h, CommandAction, 400, "invalid_idempotency_key");
        var key = parsedKey.ToString("D");
        var now = TechnicalRecordCommands.Now(h);
        PilotMetricEventsCommand command;
        try
        {
            if (!h.Request.HasJsonContentType() || h.Request.ContentLength > MaxBody) throw new JsonException();
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, MaxBody, ct);
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 6 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command = document.RootElement.Deserialize<PilotMetricEventsCommand>(Json) ?? throw new JsonException();
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException or ArgumentException)
        { return await Fail(h, CommandAction, 400, "invalid_request"); }
        if (command.Events is not { Length: >= 1 and <= PilotMetricsPolicy.MaxEventsPerBatch } || command.Events.Any(e => e == null))
            return await Fail(h, CommandAction, 400, "invalid_request");
        var inputs = command.Events.Select(e => new PilotMetricEventInput(e.Kind, e.Area, e.Journey, e.AiSource, e.DurationMs, e.OccurredAt)).ToArray();
        if (inputs.Any(e => PilotMetricsPolicy.Validate(e, now) != null)) return await Fail(h, CommandAction, 400, "invalid_request");
        var records = inputs.Select(PilotMetricsPolicy.Normalize).ToArray();
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { Operation, command }, Json));
        await TechnicalRecordCommands.Audit(h, CommandAction, "Attempted");
        PilotMetricIngestDto? result = null; string? failure = null; var status = 0;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                var receipt = await db.PilotMetricReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Fingerprint != fingerprint) { status = 409; failure = "idempotency_key_reused"; }
                    else result = new(receipt.BatchId, receipt.Accepted, PilotMetricsPolicy.SchemaVersion, true);
                }
                else
                {
                    var salt = await db.PilotMetricTenantSalts.AsNoTracking().SingleOrDefaultAsync(ct);
                    if (salt == null)
                    {
                        salt = new() { TenantId = actor.TenantId, Salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32), CreatedAt = now };
                        db.PilotMetricTenantSalts.Add(salt);
                    }
                    // Retention purge of this tenant's expired rows, bounded per command, through the audited path.
                    db.PilotMetricBatches.RemoveRange(await db.PilotMetricBatches.Where(b => b.ExpiresAt <= now).OrderBy(b => b.ExpiresAt).Take(100).ToArrayAsync(ct));
                    db.PilotMetricReceipts.RemoveRange(await db.PilotMetricReceipts.Where(r => r.ExpiresAt <= now).OrderBy(r => r.ExpiresAt).Take(100).ToArrayAsync(ct));
                    var received = PilotMetricsPolicy.Hour(now);
                    var expires = received.AddDays(settings.RetentionDays!.Value);
                    var batch = new PilotMetricBatch { TenantId = actor.TenantId, Id = Guid.NewGuid(), SchemaVersion = PilotMetricsPolicy.SchemaVersion,
                        ActorBucket = PilotMetricsPolicy.Bucket(salt.Salt, "actor:" + actor.Id.ToString("D")),
                        RoleBucket = PilotMetricsPolicy.Bucket(salt.Salt, "role:" + actor.RoleContext),
                        EventCount = records.Length, EventsJson = JsonSerializer.Serialize(records, Json), ReceivedHour = received, ExpiresAt = expires,
                        RetentionPolicyRef = settings.RetentionPolicyRef! };
                    db.PilotMetricBatches.Add(batch);
                    await db.SaveChangesAsync(ct);
                    db.PilotMetricReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Fingerprint = fingerprint, BatchId = batch.Id,
                        Accepted = records.Length, SavedAt = now, ExpiresAt = expires });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                    result = new(batch.Id, records.Length, PilotMetricsPolicy.SchemaVersion, false);
                    PilotTelemetry.ObserveAccepted(records);
                }
            }
            catch (DbUpdateConcurrencyException)
            { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); status = 409; failure = "version_conflict"; }
            catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
            {
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                (status, failure) = PostgresFailure.IsConflict(ex) ? (409, "version_conflict") : (503, "service_unavailable");
            }
        }
        if (failure != null) return await Fail(h, CommandAction, status, failure);
        try { await TechnicalRecordCommands.Audit(h, CommandAction, "Success", result!.BatchId); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex))
        { return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503); }
        return Results.Json(result, Json);
    }

    private static async Task<IResult> Summary(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var query = h.Request.Query;
        var window = 7;
        if (query.Any(x => x.Key != "windowDays" || x.Value.Count != 1) ||
            query.ContainsKey("windowDays") && (!int.TryParse(query["windowDays"], NumberStyles.None, CultureInfo.InvariantCulture, out window) ||
                !PilotMetricsPolicy.WindowDays.Contains(window)))
            return await Fail(h, ReadAction, 400, "invalid_query");
        var actor = TechnicalRecordCommands.Actor(h);
        if (actor == null || db.TenantId != actor.TenantId || !CanReadSummary(actor)) return await Fail(h, ReadAction, 403, "permission_denied");
        // Ship/tug mode isolation applies to analytics too.
        string[] areas = [.. (actor.CanSeeShip ? [PilotMetricsPolicy.Ship] : Array.Empty<string>()), .. (actor.CanSeeTug ? [PilotMetricsPolicy.Tug] : Array.Empty<string>())];
        var settings = Settings(h);
        var now = TechnicalRecordCommands.Now(h);
        if (!settings.Configured)
        {
            await TechnicalRecordCommands.Audit(h, ReadAction, "Success");
            return Results.Json(new PilotMetricSummaryDto(MetricEndpoints.ContractVersion, window, now.AddDays(-window), now, "DECLARED_UNVERIFIED", "UNCONFIGURED",
                areas, PilotMetricsPolicy.Unavailable("RETENTION_UNCONFIGURED")), Json);
        }
        var effective = Math.Min(window, settings.RetentionDays!.Value);
        var from = PilotMetricsPolicy.Hour(now.AddDays(-effective));
        PilotMetricResult[] items;
        var rows = await db.PilotMetricBatches.AsNoTracking().Where(b => b.ReceivedHour >= from && b.ExpiresAt > now)
            .OrderByDescending(b => b.ReceivedHour).ThenBy(b => b.Id).Take(MaxBatches + 1)
            .Select(b => new { b.ActorBucket, b.RoleBucket, b.EventsJson }).ToArrayAsync(ct);
        if (rows.Length > MaxBatches) items = PilotMetricsPolicy.Unavailable("QUERY_LIMIT_EXCEEDED");
        else
        {
            try
            {
                var views = rows.Select(r => new PilotMetricBatchView(r.ActorBucket, r.RoleBucket,
                    (JsonSerializer.Deserialize<PilotMetricEventRecord[]>(r.EventsJson, Json) ?? throw new InvalidDataException())
                        .Where(e => areas.Contains(e.Area, StringComparer.Ordinal)).ToArray())).ToArray();
                items = PilotMetricsPolicy.Summarize(views);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
            { items = PilotMetricsPolicy.Unavailable("SOURCE_UNREADABLE"); }
        }
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success");
        return Results.Json(new PilotMetricSummaryDto(MetricEndpoints.ContractVersion, effective, from, now, "DECLARED_UNVERIFIED", "CONFIGURED", areas, items), Json);
    }
}
