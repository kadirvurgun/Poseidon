using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugOfflineCapture(Guid CommandId, Guid KeyId, Guid DeviceId, string PolicyVersion,
    string SourceVersion, string Operation, uint BaseVersion, uint RequestVersion, DateTimeOffset CapturedAt,
    int OffsetMinutes, [property: JsonRequired] DateTimeOffset? PlannedStart, [property: JsonRequired] DateTimeOffset? PlannedEnd,
    Guid? ParentCommandId = null, uint? ExpectedServerVersion = null, string? Choice = null, string? Reason = null);
public sealed record TugOfflineContext(Guid Id, Guid TenantId, Guid ActorId, Guid VesselId, Guid JobId,
    Guid DeviceId, Guid KeyId, string Challenge, string PolicyVersion, string SourceVersion, uint RequestVersion,
    uint BaseVersion, long BaseRevision, DateTimeOffset? PlannedStart, DateTimeOffset? PlannedEnd,
    DateTimeOffset ServerTime, DateTimeOffset ExpiresAt);
public sealed record TugOfflineResult(Guid CommandId, string Result, uint ServerVersion, long Revision, Guid? ParentCommandId);
internal sealed record TugOfflineValues(DateTimeOffset? PlannedStart, DateTimeOffset? PlannedEnd);
internal sealed record TugOfflineConflictValues(TugOfflineValues Base, TugOfflineValues Captured, TugOfflineValues Server, string? Reason);

public static partial class TugEndpoints
{
    private static readonly JsonSerializerOptions OfflineJson = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 6 };
    private static void MapTugOffline(IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/tug-jobs/{id:guid}/plan/offline").RequireAuthorization("DirectoryRead");
        g.MapPost("", TugEnroll).WithName("TugOfflineEnroll").Produces<TugOfflineContext>();
        g.MapGet("", TugInventory).WithName("TugOfflineInventory");
        g.MapPost("/{enrollment:guid}/{action}", TugOfflineAction).WithName("TugOfflineAction");
        g.MapGet("/{enrollment:guid}/history", TugOfflineHistory).WithName("TugOfflineHistory");
        g.MapGet("/{enrollment:guid}/conflicts/{command:guid}", TugOfflineConflict).WithName("TugOfflineConflict");
    }
    private static DateTimeOffset OfflineNow(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    private static IDataProtector TugProtector(HttpContext h, TugOfflineEnrollment e) => h.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("Poseidon.Tug.Offline.v1", e.TenantId.ToString(), e.ActorId.ToString(), e.VesselId.ToString(), e.JobId.ToString(), e.Id.ToString());
    private static IQueryable<TugOfflineEnrollment> TugEnrollments(HttpContext h, PoseidonDbContext db, Guid vessel, Guid id) =>
        db.TugOfflineEnrollments.Where(e => e.ActorId == Actor(h).Id && e.VesselId == vessel && e.JobId == id);
    private static IQueryable<TugOfflineOutcome> TugOutcomes(PoseidonDbContext db, TugOfflineEnrollment e) =>
        db.TugOfflineOutcomes.Where(o => o.ActorId == e.ActorId && o.EnrollmentId == e.Id && o.VesselId == e.VesselId && o.JobId == e.JobId);
    private static void TugOfflineEvent(PoseidonDbContext db, TugOfflineEnrollment e, string action, DateTimeOffset now) => db.TugOfflineLifecycles.Add(new() {
        TenantId = e.TenantId, Id = Guid.NewGuid(), EnrollmentId = e.Id, ActorId = e.ActorId, VesselId = e.VesselId, JobId = e.JobId, Action = action, RecordedAt = now });
    private static bool TugOfflineCurrent(HttpContext h, TugOfflineEnrollment e) => e.RevokedAt == null && Planning(h).Offline.Configured &&
        Planning(h).Scope(Actor(h), e.VesselId) != null && e.PolicyDigest == Planning(h).Offline.Digest(Planning(h));
    // Ordinary current write, job, vessel, mode, policy and every assigned reference precede inventory/recovery/history.
    private static async Task<(TugJob Job, TugDraftPlan Plan)?> TugOfflineOwner(Guid id, Guid vessel, HttpContext h, PoseidonDbContext db) {
        if (!PlanWrite(Actor(h), vessel) || Planning(h).Scope(Actor(h), vessel) == null) return null;
        var job = await db.TugJobs.SingleOrDefaultAsync(j => j.Id == id && j.VesselId == vessel, h.RequestAborted);
        var plan = await db.TugDraftPlans.SingleOrDefaultAsync(p => p.JobId == id && p.VesselId == vessel, h.RequestAborted);
        if (job == null || plan == null) return null;
        var eligible = await Eligible(h, db, vessel);
        if (await db.TugDraftPlanTugs.AnyAsync(t => t.VesselId == vessel && t.JobId == id && t.Revision == plan.Revision && !eligible.Contains(t.TugId), h.RequestAborted)) return null;
        return (job, plan);
    }
    private static TugOfflineContext TugContext(HttpContext h, TugOfflineEnrollment e) {
        var b = JsonSerializer.Deserialize<TugOfflineValues>(TugProtector(h, e).Unprotect(e.ProtectedBase), OfflineJson)!;
        return new(e.Id, e.TenantId, e.ActorId, e.VesselId, e.JobId, e.DeviceId, e.KeyId, e.Challenge,
            Planning(h).Offline.Version, TugOfflinePolicy.Source, e.RequestVersion, e.BaseVersion, e.BaseRevision,
            b.PlannedStart, b.PlannedEnd, OfflineNow(h), e.ExpiresAt);
    }
    private static TugOfflineResult TugResult(TugOfflineOutcome o) => new(o.CommandId, o.Result, o.ServerVersion, o.Revision, o.ParentCommandId);
    private static async Task<bool> TugCsrf(HttpContext h) {
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static async Task<byte[]> TugBody(HttpContext h) {
        if (!h.Request.HasJsonContentType()) throw new JsonException();
        using var ms = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, ms, 4096, h.RequestAborted);
        using var doc = JsonDocument.Parse(ms.ToArray(), new() { MaxDepth = 6 });
        if (!Unique(doc.RootElement)) throw new JsonException();
        return ms.ToArray();
    }
    private static async Task<IResult> TugEnroll(Guid id, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db, true); if (failure != null) return failure;
        if (!await TugCsrf(h)) return await Fail(h, 403, "csrf_invalid");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        var owner = await TugOfflineOwner(id, vessel, h, db);
        if (owner == null) return await Fail(h, 404, "resource_not_found");
        var p = Planning(h); var now = OfflineNow(h); var a = Actor(h);
        if (!p.Offline.Configured) return await Fail(h, 403, "offline_policy_denied");
        if (await TugEnrollments(h, db, vessel, id).CountAsync(e => e.RevokedAt == null, h.RequestAborted) >= 8) return await Fail(h, 409, "enrollment_limit");
        var e = new TugOfflineEnrollment { TenantId = a.TenantId, Id = Guid.NewGuid(), ActorId = a.Id, VesselId = vessel, JobId = id,
            DeviceId = Guid.NewGuid(), KeyId = Guid.NewGuid(), Challenge = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            PolicyDigest = p.Offline.Digest(p), RequestVersion = owner.Value.Job.Version, BaseVersion = owner.Value.Plan.Version, BaseRevision = owner.Value.Plan.Revision,
            CreatedAt = now, ExpiresAt = new[] { now.AddSeconds(p.Offline.LeaseSeconds), p.ExpiresAt }.Min() };
        e.ProtectedBase = TugProtector(h, e).Protect(JsonSerializer.Serialize(new TugOfflineValues(owner.Value.Plan.PlannedStart, owner.Value.Plan.PlannedEnd), OfflineJson));
        db.TugOfflineEnrollments.Add(e); TugOfflineEvent(db, e, "Enrolled", now);
        try { await db.SaveChangesAsync(h.RequestAborted); await tx.CommitAsync(h.RequestAborted); }
        catch (Exception ex) when (ex is DbUpdateException || PostgresFailure.IsSerialization(ex)) { return await Fail(h, 409, "offline_retry"); }
        return Results.Ok(TugContext(h, e));
    }
    private static async Task<IResult> TugInventory(Guid id, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db, true); if (failure != null) return failure;
        if (await TugOfflineOwner(id, vessel, h, db) == null) return await Fail(h, 404, "resource_not_found");
        var rows = await TugEnrollments(h, db, vessel, id).OrderByDescending(e => e.CreatedAt).Take(32).Select(e => new { e.Id, e.ExpiresAt, e.RevokedAt }).ToArrayAsync(h.RequestAborted);
        await Observe(h, "Success", id); return Results.Ok(new { items = rows });
    }
    private static async Task<IResult> TugOfflineAction(Guid id, Guid enrollment, string action, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db, true); if (failure != null) return failure;
        if (!await TugCsrf(h)) return await Fail(h, 403, "csrf_invalid");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        var owner = await TugOfflineOwner(id, vessel, h, db);
        if (owner == null) return await Fail(h, 404, "resource_not_found");
        var e = await TugEnrollments(h, db, vessel, id).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null) return await Fail(h, 404, "resource_not_found");
        var now = OfflineNow(h);
        if (action is "revoke" or "lost" or "cleanup") {
            if (action != "cleanup" && e.RevokedAt == null) e.RevokedAt = now;
            TugOfflineEvent(db, e, action == "cleanup" ? "CleanupAllowed" : action == "lost" ? "KeyLost" : "Revoked", now);
        } else {
            if (!TugOfflineCurrent(h, e)) return await Fail(h, 403, "offline_policy_denied");
            if (action == "renew") {
                // Reauthorization never silently rebases a retained capture.
                e.ExpiresAt = new[] { now.AddSeconds(Planning(h).Offline.LeaseSeconds), Planning(h).ExpiresAt }.Min(); TugOfflineEvent(db, e, "Renewed", now);
            } else if (e.ExpiresAt <= now) return await Fail(h, 403, "reauthorization_required");
            else if (action == "key") {
                OfflineKeyEnrollment? key;
                try { key = JsonSerializer.Deserialize<OfflineKeyEnrollment>(await TugBody(h), OfflineJson); }
                catch (Exception ex) when (ex is JsonException or InvalidDataException) { return await Fail(h, 400, "invalid_key_enrollment"); }
                if (key == null || !OfflineApplicationEndpoints.VerifyKey(key.PublicKey, key.Proof, $"{e.Id:D}/{e.KeyId:D}/{e.Challenge}") || e.PublicKey != null && e.PublicKey != key.PublicKey)
                    return await Fail(h, 403, "invalid_key_enrollment");
                if (e.PublicKey == null) { e.PublicKey = key.PublicKey; TugOfflineEvent(db, e, "KeyEnrolled", now); }
            } else if (action == "replay") return await TugReplay(e, owner.Value.Job, owner.Value.Plan, tx, h, db);
            else return await Fail(h, 404, "resource_not_found");
        }
        try { await db.SaveChangesAsync(h.RequestAborted); await tx.CommitAsync(h.RequestAborted); }
        catch (Exception ex) when (ex is DbUpdateException || PostgresFailure.IsSerialization(ex)) { return await Fail(h, 409, "offline_retry"); }
        return action == "renew" ? Results.Ok(TugContext(h, e)) : Results.Ok(new { state = action == "key" ? "KeyEnrolled" : "RetainedLocked" });
    }
    private static async Task<IResult> TugReplay(TugOfflineEnrollment e, TugJob job, TugDraftPlan plan,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, HttpContext h, PoseidonDbContext db) {
        byte[] bytes; TugOfflineCapture c;
        try {
            bytes = await TugBody(h); c = JsonSerializer.Deserialize<TugOfflineCapture>(bytes, OfflineJson) ?? throw new JsonException();
            using var doc = JsonDocument.Parse(bytes);
            foreach (var name in new[] { "capturedAt", "plannedStart", "plannedEnd" }) {
                var v = doc.RootElement.GetProperty(name);
                if (v.ValueKind != JsonValueKind.Null && (v.ValueKind != JsonValueKind.String || !(v.GetString()!.EndsWith('Z') || v.GetString()!.EndsWith("+00:00", StringComparison.Ordinal)))) throw new JsonException();
            }
        } catch (Exception ex) when (ex is JsonException or InvalidDataException or KeyNotFoundException) { return await Fail(h, 400, "invalid_offline_command"); }
        var now = OfflineNow(h);
        if (c.CommandId == Guid.Empty || c.KeyId != e.KeyId || c.DeviceId != e.DeviceId || c.PolicyVersion != Planning(h).Offline.Version ||
            c.SourceVersion != TugOfflinePolicy.Source || c.Operation != TugOfflinePolicy.Operation || c.RequestVersion != e.RequestVersion ||
            c.OffsetMinutes is < -840 or > 840 || c.CapturedAt < e.CreatedAt || c.CapturedAt > now || c.CapturedAt >= e.ExpiresAt ||
            c.BaseVersion == 0 || c.ParentCommandId == null && (c.BaseVersion != e.BaseVersion || c.ExpectedServerVersion != null || c.Choice != null || c.Reason != null) ||
            c.ParentCommandId != null && (c.ParentCommandId == Guid.Empty || c.Choice is not ("keepServer" or "useCaptured") || c.ExpectedServerVersion != c.BaseVersion ||
                c.Reason is not ("INPUT_CORRECTED" or "CURRENT_SERVER_RETAINED"))) return await Fail(h, 400, "invalid_offline_command");
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        if (h.Request.Headers["X-Offline-Proof"].Count != 1 || !OfflineApplicationEndpoints.VerifyKey(e.PublicKey, h.Request.Headers["X-Offline-Proof"], $"{e.Id:D}/{c.CommandId:D}/{digest}"))
            return await Fail(h, 403, "invalid_device_proof");
        var previous = await db.TugOfflineOutcomes.SingleOrDefaultAsync(o => o.ActorId == e.ActorId && o.CommandId == c.CommandId, h.RequestAborted);
        if (previous != null) return previous.EnrollmentId == e.Id && previous.Digest == digest ? Results.Ok(TugResult(previous)) : await Fail(h, 409, "idempotency_key_reused");
        var local = new TugOfflineValues(c.PlannedStart, c.PlannedEnd);
        if (c.ParentCommandId != null) {
            var parent = await TugOutcomes(db, e).SingleOrDefaultAsync(o => o.CommandId == c.ParentCommandId && o.Result == "MaterialConflict", h.RequestAborted);
            if (parent == null || await TugOutcomes(db, e).AnyAsync(o => o.ParentCommandId == parent.CommandId, h.RequestAborted)) return await Fail(h, 409, "conflict_changed");
            var original = JsonSerializer.Deserialize<TugOfflineConflictValues>(TugProtector(h, e).Unprotect(parent.ProtectedContext), OfflineJson)!;
            if (c.Choice == "useCaptured" && local != original.Captured) return await Fail(h, 409, "conflict_changed");
            if (c.Choice == "keepServer") local = new(plan.PlannedStart, plan.PlannedEnd);
        }
        try { TugDraftPlanning.Apply(new(null, null, []), new("Window", local.PlannedStart, local.PlannedEnd)); }
        catch (ArgumentException) { return await Fail(h, 400, "invalid_offline_command"); }
        var conflict = job.Version != e.RequestVersion || plan.Version != c.BaseVersion;
        var server = new TugOfflineValues(plan.PlannedStart, plan.PlannedEnd);
        if (!conflict) {
            var result = await ApplyPlan(e.JobId, e.VesselId, new(c.BaseVersion, c.RequestVersion, Planning(h).Version, new("Window", local.PlannedStart, local.PlannedEnd)), c.CommandId, h, db);
            if (result is not IStatusCodeHttpResult { StatusCode: 200 }) return result;
        }
        var outcome = new TugOfflineOutcome { TenantId = e.TenantId, ActorId = e.ActorId, CommandId = c.CommandId, EnrollmentId = e.Id,
            VesselId = e.VesselId, JobId = e.JobId, ParentCommandId = c.ParentCommandId, Digest = digest,
            BaseVersion = c.BaseVersion, RequestVersion = c.RequestVersion, CapturedAt = c.CapturedAt, OffsetMinutes = c.OffsetMinutes,
            Result = conflict ? "MaterialConflict" : "Committed", ServerVersion = plan.Version, Revision = plan.Revision, ReceivedAt = now,
            ProtectedContext = TugProtector(h, e).Protect(JsonSerializer.Serialize(new TugOfflineConflictValues(
                JsonSerializer.Deserialize<TugOfflineValues>(TugProtector(h, e).Unprotect(e.ProtectedBase), OfflineJson)!, local, server, c.Reason), OfflineJson)) };
        db.TugOfflineOutcomes.Add(outcome);
        try { await db.SaveChangesAsync(h.RequestAborted); await tx.CommitAsync(h.RequestAborted); }
        catch (Exception ex) when (ex is DbUpdateException || PostgresFailure.IsSerialization(ex)) { return await Fail(h, 409, "offline_retry"); }
        return Results.Ok(TugResult(outcome));
    }
    private static async Task<IResult> TugOfflineHistory(Guid id, Guid enrollment, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db, true); if (failure != null) return failure;
        if (await TugOfflineOwner(id, vessel, h, db) == null) return await Fail(h, 404, "resource_not_found");
        var e = await TugEnrollments(h, db, vessel, id).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null || !TugOfflineCurrent(h, e) || e.ExpiresAt <= OfflineNow(h)) return await Fail(h, 403, "offline_policy_denied");
        var rows = await TugOutcomes(db, e).OrderByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Take(101).ToArrayAsync(h.RequestAborted);
        await Observe(h, "Success", id); return Results.Ok(new { items = rows.Take(100).Select(TugResult), hasMore = rows.Length > 100 });
    }
    private static async Task<IResult> TugOfflineConflict(Guid id, Guid enrollment, Guid command, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await PlanningGate(h, db, true); if (failure != null) return failure;
        var owner = await TugOfflineOwner(id, vessel, h, db); if (owner == null) return await Fail(h, 404, "resource_not_found");
        var e = await TugEnrollments(h, db, vessel, id).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null || !TugOfflineCurrent(h, e) || e.ExpiresAt <= OfflineNow(h)) return await Fail(h, 403, "offline_policy_denied");
        var o = await TugOutcomes(db, e).SingleOrDefaultAsync(o => o.CommandId == command && o.Result == "MaterialConflict", h.RequestAborted);
        if (o == null) return await Fail(h, 404, "resource_not_found");
        var values = JsonSerializer.Deserialize<TugOfflineConflictValues>(TugProtector(h, e).Unprotect(o.ProtectedContext), OfflineJson)!;
        var resolved = await TugOutcomes(db, e).AnyAsync(o => o.ParentCommandId == command, h.RequestAborted);
        await Observe(h, "Success", id);
        return Results.Ok(new { commandId = command, currentVersion = owner.Value.Plan.Version, values.Base, values.Captured,
            server = new TugOfflineValues(owner.Value.Plan.PlannedStart, owner.Value.Plan.PlannedEnd),
            choices = resolved || owner.Value.Job.Version != e.RequestVersion ? Array.Empty<string>() : ["keepServer", "useCaptured"] });
    }
}
