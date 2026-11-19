using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SyncOperationReferenceDto([property: JsonRequired] Guid OperationId, [property: JsonRequired] string Domain,
    [property: JsonRequired] Guid ResourceId, [property: JsonRequired] string PayloadDigest)
{
    public override string ToString() => nameof(SyncOperationReferenceDto);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SyncOperationsQuery([property: JsonRequired] Guid VesselId, [property: JsonRequired] SyncOperationReferenceDto[] Operations)
{
    public override string ToString() => nameof(SyncOperationsQuery);
}
public sealed record SyncOperationOutcomeDto(Guid OperationId, string Domain, Guid ResourceId, string State, string? Result,
    uint? BaseVersion, uint? ServerVersion, long? Revision, Guid? ParentCommandId, DateTimeOffset? ReceivedAt);
public sealed record SyncOperationsResultDto(Guid VesselId, DateTimeOffset ServerTime, SyncOperationOutcomeDto[] Items);

// DEV-034 / T-S4-002 — 18.01 Backlog row 35, H35 "/api/v1/sync/operations".
// Reconnect reconciliation: the device presents the idempotency keys (command UUIDs)
// and SHA-256 digests of its exact frozen queued bodies; the server answers from the
// durable per-domain receipts only. It never executes or replays a business command,
// so a lost replay response is recovered without a second effect, and the typed
// adapters (crew/tug/daily replay) remain the only write path with expected versions.
// Digests travel in the POST body (never URLs/logs) and are never echoed back.
public static class SyncOperationEndpoints
{
    private const int MaxBodyBytes = 16384;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 4, PropertyNameCaseInsensitive = false };

    public static void MapSyncOperations(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/sync/operations", Reconcile).RequireAuthorization("DirectoryRead").WithName("SyncOperationReconcile")
            .Accepts<SyncOperationsQuery>("application/json").Produces<SyncOperationsResultDto>();

    private static async Task<IResult> Fail(HttpContext h, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "SyncOperationRead", code);
        return Results.Json(new { code }, statusCode: status);
    }

    private static bool Unique(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == value.EnumerateObject().Count() &&
            value.EnumerateObject().All(x => Unique(x.Value)),
        JsonValueKind.Array => value.EnumerateArray().All(Unique),
        _ => true,
    };

    internal static async Task<SyncOperationsQuery?> Parse(HttpContext h)
    {
        if (!h.Request.HasJsonContentType()) return null;
        try
        {
            using var ms = new MemoryStream();
            await SecureFilePolicy.CopyAsync(h.Request.Body, ms, MaxBodyBytes, h.RequestAborted);
            var body = ms.ToArray();
            using var json = JsonDocument.Parse(body, new() { MaxDepth = 4 });
            if (json.RootElement.ValueKind != JsonValueKind.Object || !Unique(json.RootElement)) return null;
            return JsonSerializer.Deserialize<SyncOperationsQuery>(body, Json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException) { return null; }
    }

    private static async Task<IResult> Reconcile(HttpContext h, PoseidonDbContext db)
    {
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (a == null || !a.CanRead) return await Fail(h, 403, "permission_denied");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid"); }
        var query = await Parse(h);
        var requests = query?.Operations?.Select(o => o is null ? null! : new SyncOperationReference(o.OperationId, o.Domain, o.ResourceId, o.PayloadDigest)).ToArray();
        if (query == null || query.VesselId == Guid.Empty || SyncOperationReconciliation.Validate(requests) != null)
            return await Fail(h, 400, "invalid_request");
        var vesselId = query.VesselId;
        // Server-side tenant (query filter) and actor vessel scope. Header/body tenant values are never trusted.
        var vessel = await a.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(v => v.Id == vesselId, h.RequestAborted);
        if (vessel == null) return await Fail(h, 404, "resource_unavailable");

        var rights = SyncOperationReconciliation.Domains.ToDictionary(d => d, d => d switch
        {
            SyncOperationReconciliation.CrewWatchGroup => OfflineApplicationEndpoints.ReplayRights(a, vesselId),
            SyncOperationReconciliation.TugPlanWindow => TugEndpoints.SyncRights(h, a, vesselId),
            _ => DailyOperationsEndpoints.CanWrite(a, SyncOperationReconciliation.DailyKind(d)!, vesselId) && DailyOfflineEndpoints.ShipVisible(a, vessel),
        }, StringComparer.Ordinal);

        // Only the caller's own durable receipts are read (actor-bound key scope, tenant query filter).
        var ids = requests!.Select(r => r.OperationId).ToArray();
        var stored = new Dictionary<Guid, List<SyncStoredOutcome>>();
        void Add(SyncStoredOutcome s) { if (!stored.TryGetValue(s.OperationId, out var l)) stored[s.OperationId] = l = []; l.Add(s); }
        var details = new Dictionary<(Guid, string), (string Result, uint Base, uint Server, long Revision, Guid? Parent, DateTimeOffset Received)>();

        var crew = await db.OfflineOutcomes.AsNoTracking().Where(o => o.ActorId == a.Id && ids.Contains(o.CommandId)).ToArrayAsync(h.RequestAborted);
        var crewEnrollmentIds = crew.Select(o => o.EnrollmentId).Distinct().ToArray();
        var crewEnrollments = await db.OfflineEnrollments.AsNoTracking().Where(e => e.ActorId == a.Id && crewEnrollmentIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, h.RequestAborted);
        foreach (var o in crew)
        {
            Add(new(o.CommandId, SyncOperationReconciliation.CrewWatchGroup, o.AssignmentId, o.VesselId, o.Digest, o.Result,
                crewEnrollments.TryGetValue(o.EnrollmentId, out var e) && OfflineApplicationEndpoints.EnrollmentCurrent(h, e)));
            details[(o.CommandId, SyncOperationReconciliation.CrewWatchGroup)] = (o.Result, o.BaseVersion, o.ServerVersion, o.Revision, o.ParentCommandId, o.ReceivedAt);
        }

        var tug = await db.TugOfflineOutcomes.AsNoTracking().Where(o => o.ActorId == a.Id && ids.Contains(o.CommandId)).ToArrayAsync(h.RequestAborted);
        var tugEnrollmentIds = tug.Select(o => o.EnrollmentId).Distinct().ToArray();
        var tugEnrollments = await db.TugOfflineEnrollments.AsNoTracking().Where(e => e.ActorId == a.Id && tugEnrollmentIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, h.RequestAborted);
        foreach (var o in tug)
        {
            Add(new(o.CommandId, SyncOperationReconciliation.TugPlanWindow, o.JobId, o.VesselId, o.Digest, o.Result,
                tugEnrollments.TryGetValue(o.EnrollmentId, out var e) && TugEndpoints.SyncEnrollmentCurrent(h, e)));
            details[(o.CommandId, SyncOperationReconciliation.TugPlanWindow)] = (o.Result, o.BaseVersion, o.ServerVersion, o.Revision, o.ParentCommandId, o.ReceivedAt);
        }

        var daily = await db.DailyOfflineOutcomes.AsNoTracking().Where(o => o.ActorId == a.Id && ids.Contains(o.CommandId)).ToArrayAsync(h.RequestAborted);
        var dailyEnrollmentIds = daily.Select(o => o.EnrollmentId).Distinct().ToArray();
        var dailyEnrollments = await db.DailyOfflineEnrollments.AsNoTracking().Where(e => e.ActorId == a.Id && dailyEnrollmentIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, h.RequestAborted);
        foreach (var o in daily)
        {
            var domain = "Daily" + o.Kind;
            Add(new(o.CommandId, domain, o.RecordId, o.VesselId, o.Digest, o.Result,
                dailyEnrollments.TryGetValue(o.EnrollmentId, out var e) && DailyOfflineEndpoints.EnrollmentCurrent(h, e)));
            details[(o.CommandId, domain)] = (o.Result, o.BaseVersion, o.ServerVersion, o.Revision, o.ParentCommandId, o.ReceivedAt);
        }

        var items = new SyncOperationOutcomeDto[requests!.Length];
        for (var i = 0; i < requests.Length; i++)
        {
            var r = requests[i];
            var candidates = stored.GetValueOrDefault(r.OperationId);
            // Prefer the receipt in the requested domain; any other binding of the key is a reuse.
            var match = candidates?.FirstOrDefault(s => s.Domain == r.Domain) ?? candidates?.FirstOrDefault();
            var state = SyncOperationReconciliation.Classify(r, vesselId, rights[r.Domain], match);
            items[i] = state == SyncOperationState.Recorded && details.TryGetValue((r.OperationId, r.Domain), out var d)
                ? new(r.OperationId, r.Domain, r.ResourceId, state.ToString(), d.Result, d.Base, d.Server, d.Revision, d.Parent, d.Received)
                : new(r.OperationId, r.Domain, r.ResourceId, state.ToString(), null, null, null, null, null, null);
        }
        try
        {
            // Fixed technical metadata only: no keys, digests, domain values or counts in the audit row.
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "SyncOperationRead", "Success", resourceId: vesselId);
        }
        catch (AuditUnavailableException) { return Results.Json(new { code = "service_unavailable" }, statusCode: 503); }
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        return Results.Ok(new SyncOperationsResultDto(vesselId, now, items));
    }
}
