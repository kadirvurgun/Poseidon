using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-081 / T-TUG-002: submit/cancel commands (60.02 §5 paragraphs 84 and 91..92).
// ExpectedVersion is the xmin the operator saw; Reason is required for cancel only.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugLifecycleCommand(uint ExpectedVersion, string? Reason = null);
public sealed record TugLifecycleFailure(string Code, string[] Missing, string CorrelationId);

// DEV-082 short/frequent board relative windows (server clock), never an approved plan.
public static class TugBoardWindows
{
    public static readonly IReadOnlyDictionary<string, int> Hours = new Dictionary<string, int>(StringComparer.Ordinal) { ["next4h"] = 4, ["next24h"] = 24, ["next72h"] = 72 };
}

public static partial class TugEndpoints
{
    private static void MapTugLifecycle(IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1").RequireAuthorization("DirectoryRead");
        g.MapPost("/tug-jobs/{id:guid}/submit", (Guid id, HttpContext h, PoseidonDbContext db) => Lifecycle(id, TugJobCommand.Submit, h, db))
            .WithName("TugSubmit").Accepts<TugLifecycleCommand>("application/json").Produces<TugRequestDto>().Produces<TugLifecycleFailure>(422);
        g.MapPost("/tug-jobs/{id:guid}/cancel", (Guid id, HttpContext h, PoseidonDbContext db) => Lifecycle(id, TugJobCommand.Cancel, h, db))
            .WithName("TugCancel").Accepts<TugLifecycleCommand>("application/json").Produces<TugRequestDto>();
    }
    private static Task ObserveLifecycle(HttpContext h, string result, Guid? id = null) {
        h.Items["TugObserved"] = true;
        return h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "TugLifecycle", result, resourceId: id);
    }
    private static async Task<IResult> LifecycleFail(HttpContext h, int status, string code, Guid? id = null, string[]? missing = null) {
        await ObserveLifecycle(h, code, id);
        return missing == null ? Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status)
            : Results.Json(new TugLifecycleFailure(code, missing, h.TraceIdentifier), statusCode: status);
    }
    private static async Task<IResult> Lifecycle(Guid id, TugJobCommand operation, HttpContext h, PoseidonDbContext db) {
        var a = Actor(h);
        if (!a.CanReadTug) return await LifecycleFail(h, 403, "permission_denied");
        if (h.Request.Query.Any(q => q.Key != "vessel" || q.Value.Count != 1) ||
            !Guid.TryParseExact(h.Request.Query["vessel"], "D", out var vessel) || vessel == Guid.Empty || id == Guid.Empty)
            return await LifecycleFail(h, 400, "invalid_request");
        if (!await Tugs(db, a).AnyAsync(v => v.Id == vessel, h.RequestAborted)) return await LifecycleFail(h, 404, "resource_not_found");
        if (!Write(a, vessel)) return await LifecycleFail(h, 403, "permission_denied");
        if (h.Request.Headers["X-CSRF-TOKEN"].Count != 1) return await LifecycleFail(h, 400, "csrf_invalid");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await LifecycleFail(h, 400, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await LifecycleFail(h, 400, "invalid_idempotency_key");
        var key = parsedKey.ToString("D");
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > 4096) return await LifecycleFail(h, 400, "invalid_request");
        TugLifecycleCommand command;
        try {
            using var buffer = new MemoryStream(); var bytes = new byte[1024]; int count;
            while ((count = await h.Request.Body.ReadAsync(bytes, h.RequestAborted)) > 0) {
                if (buffer.Length + count > 4096) return await LifecycleFail(h, 400, "invalid_request");
                buffer.Write(bytes, 0, count);
            }
            using var doc = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 3 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !UniqueMembers(doc.RootElement)) return await LifecycleFail(h, 400, "invalid_request");
            command = doc.RootElement.Deserialize<TugLifecycleCommand>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false }) ?? throw new JsonException();
        } catch (Exception e) when (e is JsonException or InvalidOperationException) { return await LifecycleFail(h, 400, "invalid_request"); }
        if (command.ExpectedVersion == 0 || operation == TugJobCommand.Submit && command.Reason != null ||
            operation == TugJobCommand.Cancel && !TugJobLifecycle.ValidReason(command.Reason)) return await LifecycleFail(h, 400, "validation_failed", id);
        var action = operation == TugJobCommand.Submit ? "TugSubmit" : "TugCancel";
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, id, action, command })));
        var ct = h.RequestAborted;
        await ObserveLifecycle(h, "Attempted", id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var job = await db.TugJobs.SingleOrDefaultAsync(j => j.Id == id && j.VesselId == vessel, ct);
            if (job == null) return await LifecycleFail(h, 404, "resource_not_found");
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != action || receipt.ResourceId != id || receipt.Fingerprint != fingerprint) return await LifecycleFail(h, 409, "idempotency_key_reused", id);
                var revision = await db.TugJobRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.JobId == id && r.VesselId == vessel && r.Revision == receipt.RegisterRevision, ct);
                if (revision == null) return await LifecycleFail(h, 409, "source_revision_unavailable", id);
                await ObserveLifecycle(h, "Success", id);
                return Results.Ok(Dto(revision, receipt.RegisterVersion!.Value, true));
            }
            if (job.Version != command.ExpectedVersion) return await LifecycleFail(h, 409, "version_conflict", id);
            // Owner eligibility is current trusted provisioning, rechecked at submission time.
            if (operation == TugJobCommand.Submit && job.RequestOwner is Guid owner && !Owners(h, vessel).Contains(owner))
                return await LifecycleFail(h, 403, "permission_denied", id);
            var transition = TugJobLifecycle.Evaluate(job.Status, operation, job.Inputs(), command.Reason);
            if (transition.ToState == null)
                return transition.Failure switch {
                    "tug_request_incomplete" => await LifecycleFail(h, 422, "tug_request_incomplete", id, transition.Missing),
                    "validation_failed" => await LifecycleFail(h, 400, "validation_failed", id),
                    _ => await LifecycleFail(h, 409, "invalid_state", id)
                };
            var now = DateTimeOffset.UtcNow;
            job.Status = transition.ToState; job.Revision = checked(job.Revision + 1); job.SavedAt = now;
            if (operation == TugJobCommand.Submit) job.SubmittedAt = now;
            else { job.CancelledAt = now; job.CancelReason = command.Reason; }
            // The submitted/cancelled body is the frozen content of the current draft revision, byte for byte.
            var current = await db.TugJobRevisions.AsNoTracking().SingleAsync(r => r.JobId == id && r.VesselId == vessel && r.Revision == job.Revision - 1, ct);
            var snapshot = job.Snapshot();
            snapshot.ContentJson = current.ContentJson; snapshot.SourceVersion = current.SourceVersion; db.TugJobRevisions.Add(snapshot); await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = action, Fingerprint = fingerprint,
                ResourceId = job.Id, RegisterRevision = job.Revision, RegisterVersion = job.Version, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await ObserveLifecycle(h, "Success", job.Id);
            return Results.Ok(Dto(snapshot, job.Version, false));
        } catch (DbUpdateConcurrencyException) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await LifecycleFail(h, 409, "version_conflict", id);
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            // Only serialization/deadlock (40001/40P01) and unique races (23505) are conflicts;
            // any other database failure is an unavailable outcome, never a silent conflict.
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var conflict = PostgresFailure.IsConflict(e);
            return await LifecycleFail(h, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable", id);
        }
    }
}
