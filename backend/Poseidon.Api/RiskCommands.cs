using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record RiskFailure(string Code, string[] Missing, string CorrelationId);
internal sealed record RiskOutcome(int Status, string? Code, Guid ResourceId, long Revision, string[]? Missing = null)
{
    internal static RiskOutcome Ok(Guid id, long revision) => new(200, null, id, revision);
    internal static RiskOutcome Fail(int status, string code, string[]? missing = null) => new(status, code, Guid.Empty, 0, missing);
}
internal sealed record RiskContext<T>(TrustedActor Actor, Guid? Vessel, Guid? RouteId, Guid? SubId, T Command, DateTimeOffset Now, PoseidonDbContext Db, CancellationToken Ct);

// Shared closed command transport for DEV-105/DEV-106 (NATIVE-COMMAND-1): server
// authorization, exact vessel scope, CSRF, Idempotency-Key receipt, bounded strict JSON,
// one SERIALIZABLE transaction and attributed audit. Replays re-read the committed
// resource under current authorization; they never repeat the business effect.
internal static class RiskCommands
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private const int MaxBody = 262144;

    internal static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    internal static DateTimeOffset Now(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    internal static Task Audit(HttpContext h, string action, string result, Guid? id = null) =>
        h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, result, resourceId: id);
    internal static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? id = null, string[]? missing = null)
    {
        h.Response.Headers.CacheControl = "no-store";
        await Audit(h, action, code, id);
        return missing == null ? Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status)
            : Results.Json(new RiskFailure(code, missing, h.TraceIdentifier), statusCode: status);
    }

    // Exactly ?vesselId=<guid> for vessel-scoped routes, no query for tenant routes;
    // extra names (tenant, role, actor hints) are rejected.
    internal static bool Query(HttpContext h, bool vesselScoped, out Guid vessel, string[]? extra = null)
    {
        vessel = Guid.Empty; var allowed = extra ?? [];
        if (h.Request.Query.Any(q => q.Value.Count != 1 || q.Key != "vesselId" && !allowed.Contains(q.Key))) return false;
        if (!vesselScoped) return !h.Request.Query.ContainsKey("vesselId");
        return Guid.TryParseExact(h.Request.Query["vesselId"], "D", out vessel) && vessel != Guid.Empty;
    }
    internal static IQueryable<Vessel> Vessels(PoseidonDbContext db, TrustedActor actor, Guid vessel) =>
        actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel);

    internal static async Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string operation, string auditAction, bool vesselScoped,
        Guid? routeId, Guid? subId, Func<TrustedActor, Guid?, bool> allowed, Func<T, Guid?> resourceOf,
        Func<RiskContext<T>, Task<RiskOutcome>> apply, Func<PoseidonDbContext, TrustedActor, Guid?, Guid, CancellationToken, Task<object?>> read) where T : class
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (!Query(h, vesselScoped, out var parsed)) return await Fail(h, auditAction, 400, "invalid_query");
        Guid? vessel = vesselScoped ? parsed : null;
        var actor = Actor(h);
        if (actor == null || !actor.HasWriteAttribution || db.TenantId != actor.TenantId || !allowed(actor, vessel)) return await Fail(h, auditAction, 403, "permission_denied", routeId);
        if (vessel != null && !await Vessels(db, actor, vessel.Value).AnyAsync(ct)) return await Fail(h, auditAction, 404, "resource_not_found", routeId);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, auditAction, 403, "csrf_invalid", routeId); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await Fail(h, auditAction, 400, "invalid_idempotency_key", routeId);
        var key = parsedKey.ToString("D");
        T command;
        try
        {
            if (!h.Request.HasJsonContentType() || h.Request.ContentLength > MaxBody) throw new JsonException();
            using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
            while ((count = await h.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > MaxBody) throw new JsonException();
                buffer.Write(chunk, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command = document.RootElement.Deserialize<T>(Json) ?? throw new JsonException();
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException)
        { return await Fail(h, auditAction, 400, "invalid_request", routeId); }
        var resource = resourceOf(command) ?? routeId ?? Guid.Empty;
        if (resource == Guid.Empty) return await Fail(h, auditAction, 400, "invalid_request");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation, vessel, routeId, subId, command }, Json));
        async Task<IResult> Fresh(Guid id)
        {
            var live = Actor(h);
            if (live == null || !allowed(live, vessel)) return await Fail(h, auditAction, 403, "permission_denied", id);
            db.ChangeTracker.Clear();
            var value = await read(db, live, vessel, id, ct);
            if (value == null) return await Fail(h, auditAction, 503, "command_outcome_withheld", id);
            await Audit(h, auditAction, "Success", id);
            return Results.Json(value, Json);
        }
        await Audit(h, auditAction, "Attempted", resource);
        Guid? committed = null; RiskOutcome? failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                var receipt = await db.RiskCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Action != operation || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel)
                        return await Fail(h, auditAction, 409, "idempotency_key_reused", resource);
                    return await Fresh(receipt.ResourceId);
                }
                var outcome = await apply(new(actor, vessel, routeId, subId, command, Now(h), db, ct));
                if (outcome.Status != 200) { failure = outcome; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
                else
                {
                    db.RiskCommandReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = operation, Fingerprint = fingerprint,
                        VesselId = vessel, ResourceId = outcome.ResourceId, Revision = Math.Max(1, outcome.Revision), SavedAt = Now(h) });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = outcome.ResourceId;
                }
            }
            catch (DbUpdateConcurrencyException)
            { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); failure = RiskOutcome.Fail(409, "version_conflict"); }
            catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
            {
                // Only serialization/deadlock (40001/40P01) and unique races (23505) are conflicts.
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                failure = PostgresFailure.IsConflict(ex) ? RiskOutcome.Fail(409, "version_conflict") : RiskOutcome.Fail(503, "service_unavailable");
            }
        }
        if (failure != null) return await Fail(h, auditAction, failure.Status, failure.Code!, resource, failure.Missing);
        try { return await Fresh(committed!.Value); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex))
        { return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503); }
    }

    internal static Guid[] AuthorIds(string json) => JsonSerializer.Deserialize<Guid[]>(json, Json) ?? [];
    internal static string WithAuthor(string json, Guid actor) =>
        JsonSerializer.Serialize(AuthorIds(json).Append(actor).Distinct().Order().ToArray(), Json);
}

public sealed record RiskLinkRequest(string Kind, Guid Id);
public sealed record RiskLinkPin(string Kind, Guid Id, long Revision);
public sealed record RiskLinkDto(string Kind, Guid Id, long Revision, string Currency);

// Traceable references to tug job / work order / defect / operational event (downtime).
// The server pins the exact current revision visible to the actor in the same vessel; a
// later change of the target shows STALE and never silently re-pins.
internal static class RiskLinkReader
{
    private static bool Visible(TrustedActor actor, string kind) => kind switch
    {
        "TugJob" => actor.CanReadTug,
        "WorkOrder" => WorkOrderEndpoints.CanRead(actor),
        "Defect" => DefectAssetContextReader.CanRead(actor),
        "OperationalEvent" => OperationalDowntimeEndpoints.CanRead(actor),
        _ => false
    };
    private static Task<long?> Head(PoseidonDbContext db, string kind, Guid vessel, Guid id, CancellationToken ct) => kind switch
    {
        "TugJob" => db.TugJobs.AsNoTracking().Where(j => j.VesselId == vessel && j.Id == id).Select(j => (long?)j.Revision).SingleOrDefaultAsync(ct),
        "WorkOrder" => db.WorkOrders.AsNoTracking().Where(j => j.VesselId == vessel && j.Id == id).Select(j => (long?)j.Revision).SingleOrDefaultAsync(ct),
        "Defect" => db.Defects.AsNoTracking().Where(j => j.VesselId == vessel && j.Id == id).Select(j => (long?)j.Revision).SingleOrDefaultAsync(ct),
        "OperationalEvent" => db.Downtimes.AsNoTracking().Where(j => j.VesselId == vessel && j.Id == id).Select(j => (long?)j.Revision).SingleOrDefaultAsync(ct),
        _ => Task.FromResult<long?>(null)
    };
    internal static async Task<RiskLinkPin[]?> Pin(PoseidonDbContext db, TrustedActor actor, Guid vessel, RiskLinkRequest[]? requests, RiskLinkPin[] existing, CancellationToken ct)
    {
        if (requests == null || requests.Length > RiskPolicy.MaxLinks || requests.Any(r => r == null || r.Id == Guid.Empty || !RiskPolicy.LinkKinds.Contains(r.Kind)) ||
            requests.Select(r => (r.Kind, r.Id)).Distinct().Count() != requests.Length) return null;
        var pins = new List<RiskLinkPin>();
        foreach (var r in requests)
        {
            var kept = existing.SingleOrDefault(p => p.Kind == r.Kind && p.Id == r.Id);
            if (kept != null) { pins.Add(kept); continue; }
            if (!Visible(actor, r.Kind)) return null;
            var revision = await Head(db, r.Kind, vessel, r.Id, ct);
            if (revision is not > 0) return null;
            pins.Add(new(r.Kind, r.Id, revision.Value));
        }
        return [.. pins];
    }
    internal static async Task<RiskLinkDto[]> Describe(PoseidonDbContext db, TrustedActor actor, Guid vessel, RiskLinkPin[] pins, CancellationToken ct)
    {
        var result = new List<RiskLinkDto>();
        foreach (var p in pins)
        {
            var head = Visible(actor, p.Kind) ? await Head(db, p.Kind, vessel, p.Id, ct) : null;
            result.Add(new(p.Kind, p.Id, p.Revision, head == null ? "UNAVAILABLE" : head == p.Revision ? "CURRENT" : "STALE"));
        }
        return [.. result];
    }
}
