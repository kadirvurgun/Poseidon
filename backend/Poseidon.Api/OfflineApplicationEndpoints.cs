using System.Data;
using System.Globalization;
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
public sealed record OfflineReplayCommand(Guid CommandId, Guid KeyId, Guid DeviceId, string PolicyVersion,
    string SourceVersion, string Operation, uint BaseVersion, DateTimeOffset CapturedAt, int OffsetMinutes,
    [property: JsonRequired] string? WatchGroup, Guid? ParentCommandId = null, uint? ExpectedServerVersion = null,
    string? Choice = null, string? Reason = null)
{
    public override string ToString() => nameof(OfflineReplayCommand);
}
public sealed record OfflineContextDto(Guid Id, Guid TenantId, Guid ActorId, Guid VesselId, Guid AssignmentId,
    Guid DeviceId, Guid KeyId, string KeyChallenge, string SessionBinding, string PolicyVersion, string SourceVersion,
    uint BaseVersion, long BaseRevision, string? BaseWatchGroup, string[] WatchGroups,
    DateTimeOffset ServerTime, DateTimeOffset ExpiresAt, int MaxRecords, int MaxEncryptedBytes,
    int MaxDeviceRecords, int MaxDeviceEncryptedBytes)
{
    public override string ToString() => nameof(OfflineContextDto);
}
public sealed record OfflineOutcomeDto(Guid CommandId, string Result, uint BaseVersion, uint ServerVersion,
    long Revision, DateTimeOffset CapturedAt, int OffsetMinutes, DateTimeOffset ReceivedAt, Guid? ParentCommandId);
public sealed record OfflineEnrollmentHeaderDto(Guid Id, DateTimeOffset? RevokedAt, DateTimeOffset ExpiresAt, string PolicyVersion);
public sealed record OfflineInventoryDto(OfflineEnrollmentHeaderDto[] Items);
public sealed record OfflineHistoryDto(OfflineOutcomeDto[] Items, int Offset, bool HasMore);
public sealed record OfflineKeyStatusDto(string State);
public sealed record OfflineCleanupDto(Guid Id, Guid TenantId, Guid ActorId, Guid VesselId, Guid AssignmentId, Guid DeviceId, Guid KeyId, string PolicyVersion);
public sealed record OfflineConflictValueDto(string Kind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null)
{
    public override string ToString() => nameof(OfflineConflictValueDto);
}
public sealed record OfflineConflictFieldDto(string Key, string Label, OfflineConflictValueDto Captured, OfflineConflictValueDto Server, string[] Choices);
public sealed record OfflineConflictDetailDto(Guid Id, string Revision, string ContextKey, OfflineConflictValueDto Base, uint CurrentVersion,
    OfflineOutcomeDto? Resolved, uint OriginalServerVersion, OfflineConflictValueDto? ResolvedValue, string? ResolutionReason, Guid? ResolvedBy, OfflineConflictFieldDto[] Fields)
{
    public override string ToString() => nameof(OfflineConflictDetailDto);
}
internal sealed record OfflinePrivateContext(string? Base, string? Local, string? Server, string? Resolved, string? Reason)
{
    public override string ToString() => nameof(OfflinePrivateContext);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OfflineKeyEnrollment(string PublicKey, string Proof)
{
    public override string ToString() => nameof(OfflineKeyEnrollment);
}

public static class OfflineApplicationEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 4, PropertyNameCaseInsensitive = false };
    public static void MapOfflineApplication(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/offline").RequireAuthorization("DirectoryRead");
        g.MapPost("", Enroll).WithName("OfflineEnroll").Produces<OfflineContextDto>();
        g.MapGet("", Inventory).WithName("OfflineInventory").Produces<OfflineInventoryDto>();
        g.MapPost("/{enrollment:guid}/renew", Renew).WithName("OfflineRenew").Produces<OfflineContextDto>();
        g.MapPost("/{enrollment:guid}/key", EnrollKey).WithName("OfflineKeyEnroll").Accepts<OfflineKeyEnrollment>("application/json").Produces<OfflineKeyStatusDto>();
        g.MapPost("/{enrollment:guid}/revoke", Revoke).WithName("OfflineRevoke");
        g.MapPost("/{enrollment:guid}/lost", Lost).WithName("OfflineKeyLost");
        g.MapPost("/{enrollment:guid}/cleanup", Cleanup).WithName("OfflineCleanupAuthorization").Produces<OfflineCleanupDto>();
        g.MapPost("/{enrollment:guid}/replay", Replay).WithName("OfflineReplay").Accepts<OfflineReplayCommand>("application/json").Produces<OfflineOutcomeDto>();
        g.MapGet("/{enrollment:guid}/history", History).WithName("OfflineHistory").Produces<OfflineHistoryDto>();
        g.MapGet("/{enrollment:guid}/conflicts/{command:guid}", Conflict).WithName("OfflineConflict").Produces<OfflineConflictDetailDto>();
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static DateTimeOffset Now(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    // Ordinary command rights are required BEFORE inventory, receipt recovery, counts and conflict projections.
    private static async Task<CrewAssignment?> Owner(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h);
        if (!a.CanRead || !a.HasWriteAttribution || !a.Permissions.Contains("crew.read") || !a.Permissions.Contains("crew.write") ||
            !(a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit)) ||
            !await a.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, h.RequestAborted)) return null;
        return await db.CrewAssignments.SingleOrDefaultAsync(x => x.Id == assignment && x.VesselId == vessel, h.RequestAborted);
    }
    private static OfflineApplicationPolicy Policy(HttpContext h) => h.RequestServices.GetRequiredService<OfflineApplicationPolicy>();
    private static IDataProtector Protector(HttpContext h, OfflineEnrollment e) => h.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("Poseidon.Offline.Private.v1", e.TenantId.ToString(), e.ActorId.ToString(), e.VesselId.ToString(), e.AssignmentId.ToString(), e.Id.ToString());
    private static OfflinePrivateContext Private(HttpContext h, OfflineEnrollment e, string value) =>
        JsonSerializer.Deserialize<OfflinePrivateContext>(Protector(h, e).Unprotect(value), Json)!;
    private static string Protect(HttpContext h, OfflineEnrollment e, OfflinePrivateContext value) => Protector(h, e).Protect(JsonSerializer.Serialize(value, Json));
    private static IQueryable<OfflineEnrollment> Enrollments(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid assignment) =>
        db.OfflineEnrollments.Where(e => e.ActorId == a.Id && e.VesselId == vessel && e.AssignmentId == assignment);
    private static IQueryable<OfflineOutcome> Outcomes(PoseidonDbContext db, TrustedActor a, OfflineEnrollment e) =>
        db.OfflineOutcomes.Where(o => o.ActorId == a.Id && o.EnrollmentId == e.Id && o.VesselId == e.VesselId && o.AssignmentId == e.AssignmentId);
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "OfflineRequest", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static async Task<bool> Csrf(HttpContext h) {
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static bool Current(HttpContext h, OfflineEnrollment e, DateTimeOffset now) {
        var p = Policy(h); var a = Actor(h);
        return e.RevokedAt == null && p.Allows(a.TenantId, a.Id, e.VesselId, now) && e.PolicyVersion == p.Version && e.PolicyDigest == p.Fingerprint() && e.SourceVersion == p.SourceVersion;
    }
    private static OfflineContextDto Context(HttpContext h, OfflineEnrollment e, DateTimeOffset now) {
        var p = Policy(h); var b = Private(h, e, e.ProtectedBase).Base;
        // Removed controlled values are not exposed by a historical cache projection.
        if (!OfflineWatchCapture.ValidValue(b, p.WatchGroups)) throw new CryptographicException("offline_snapshot_policy_unavailable");
        return new(e.Id, e.TenantId, e.ActorId, e.VesselId, e.AssignmentId, e.DeviceId, e.KeyId, e.KeyChallenge,
            e.Id.ToString("D"), e.PolicyVersion, e.SourceVersion, e.BaseVersion, e.BaseRevision, b,
            p.WatchGroups, now, e.ExpiresAt, p.MaxRecords, p.MaxEncryptedBytes, p.MaxDeviceRecords, p.MaxDeviceEncryptedBytes);
    }
    private static OfflineOutcomeDto Dto(OfflineOutcome o) => new(o.CommandId, o.Result, o.BaseVersion, o.ServerVersion, o.Revision,
        o.CapturedAt, o.OffsetMinutes, o.ReceivedAt, o.ParentCommandId);
    private static void Event(PoseidonDbContext db, OfflineEnrollment e, string action, DateTimeOffset now) => db.OfflineLifecycles.Add(new() {
        TenantId = e.TenantId, Id = Guid.NewGuid(), EnrollmentId = e.Id, ActorId = e.ActorId, VesselId = e.VesselId,
        AssignmentId = e.AssignmentId, Action = action, RecordedAt = now });

    private static async Task<IResult> Enroll(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db)
    {
        var owner = await Owner(vessel, assignment, h, db); if (owner == null) return await Fail(h, 404, "resource_unavailable");
        if (!await Csrf(h)) return await Fail(h, 403, "csrf_invalid");
        var p = Policy(h); var a = Actor(h); var now = Now(h);
        if (!p.Allows(a.TenantId, a.Id, vessel, now) || owner.Status != "Draft" || !OfflineWatchCapture.ValidValue(owner.WatchGroup, p.WatchGroups))
            return await Fail(h, 403, "offline_policy_denied");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        if (await Enrollments(db, a, vessel, assignment).CountAsync(e => e.RevokedAt == null, h.RequestAborted) >= p.MaxEnrollments)
            return await Fail(h, 409, "enrollment_limit");
        var e = new OfflineEnrollment { TenantId = a.TenantId, ActorId = a.Id, Id = Guid.NewGuid(), VesselId = vessel, AssignmentId = assignment,
            DeviceId = Guid.NewGuid(), KeyId = Guid.NewGuid(), KeyChallenge = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            PolicyVersion = p.Version, PolicyDigest = p.Fingerprint(), SourceVersion = p.SourceVersion,
            BaseVersion = owner.Version, BaseRevision = owner.Revision, CreatedAt = now,
            ExpiresAt = new[] { now.AddSeconds(p.LeaseSeconds), p.ExpiresAt }.Min() };
        e.ProtectedBase = Protect(h, e, new(owner.WatchGroup, null, null, null, null));
        db.OfflineEnrollments.Add(e); Event(db, e, "Enrolled", now);
        try { await db.SaveChangesAsync(h.RequestAborted); await tx.CommitAsync(h.RequestAborted); }
        catch (DbUpdateException) { return await Fail(h, 409, "offline_retry"); }
        return Results.Ok(Context(h, e, now));
    }
    private static async Task<IResult> Inventory(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db) {
        if (await Owner(vessel, assignment, h, db) == null) return await Fail(h, 404, "resource_unavailable");
        var a = Actor(h);
        // Only current actor/context, bounded; no cross-context device inventory/counts.
        var rows = await Enrollments(db, a, vessel, assignment).OrderByDescending(e => e.CreatedAt).Take(32)
            .Select(e => new OfflineEnrollmentHeaderDto(e.Id, e.RevokedAt, e.ExpiresAt, e.PolicyVersion)).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "OfflineInventory", "Success", resourceId: assignment);
        return Results.Ok(new OfflineInventoryDto(rows));
    }
    private static Task<IResult> Renew(Guid vessel, Guid assignment, Guid enrollment, HttpContext h, PoseidonDbContext db) => Lifecycle(vessel, assignment, enrollment, "Renewed", h, db);
    private static Task<IResult> Revoke(Guid vessel, Guid assignment, Guid enrollment, HttpContext h, PoseidonDbContext db) => Lifecycle(vessel, assignment, enrollment, "Revoked", h, db);
    private static Task<IResult> Lost(Guid vessel, Guid assignment, Guid enrollment, HttpContext h, PoseidonDbContext db) => Lifecycle(vessel, assignment, enrollment, "KeyLost", h, db);
    internal static bool VerifyKey(string? publicKey, string? proof, string message) {
        if (publicKey is not { Length: > 0 and <= 256 } || proof is not { Length: > 0 and <= 128 }) return false;
        try {
            using var key = ECDsa.Create(); var bytes = Convert.FromBase64String(publicKey);
            key.ImportSubjectPublicKeyInfo(bytes, out var read);
            return read == bytes.Length && key.KeySize == 256 && key.ExportParameters(false).Curve.Oid.Value == ECCurve.NamedCurves.nistP256.Oid.Value &&
                key.VerifyData(System.Text.Encoding.UTF8.GetBytes(message), Convert.FromBase64String(proof), HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        } catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException) { return false; }
    }
    private static async Task<IResult> EnrollKey(Guid vessel, Guid assignment, Guid enrollment, HttpContext h, PoseidonDbContext db) {
        if (await Owner(vessel, assignment, h, db) == null) return await Fail(h, 404, "resource_unavailable");
        if (!await Csrf(h)) return await Fail(h, 403, "csrf_invalid");
        var e = await Enrollments(db, Actor(h), vessel, assignment).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null || !Current(h, e, Now(h)) || e.ExpiresAt <= Now(h)) return await Fail(h, 403, "offline_policy_denied");
        OfflineKeyEnrollment? input;
        try {
            if (!h.Request.HasJsonContentType()) return await Fail(h, 400, "invalid_key_enrollment");
            using var ms = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, ms, 1024, h.RequestAborted);
            using var parsed = JsonDocument.Parse(ms.ToArray()); if (!Unique(parsed.RootElement)) return await Fail(h, 400, "invalid_key_enrollment");
            input = JsonSerializer.Deserialize<OfflineKeyEnrollment>(ms.ToArray(), Json);
        } catch (Exception ex) when (ex is JsonException or InvalidDataException) { return await Fail(h, 400, "invalid_key_enrollment"); }
        if (input == null || !VerifyKey(input.PublicKey, input.Proof, $"{e.Id:D}/{e.KeyId:D}/{e.KeyChallenge}") ||
            e.PublicSigningKey != null && e.PublicSigningKey != input.PublicKey) return await Fail(h, 403, "invalid_key_enrollment");
        if (e.PublicSigningKey == null) {
            e.PublicSigningKey = input.PublicKey; Event(db, e, "KeyEnrolled", Now(h));
            try { await db.SaveChangesAsync(h.RequestAborted); } catch (DbUpdateException) { return await Fail(h, 409, "offline_retry"); }
        }
        return Results.Ok(new OfflineKeyStatusDto("KeyEnrolled"));
    }
    private static async Task<IResult> Cleanup(Guid vessel, Guid assignment, Guid enrollment, HttpContext h, PoseidonDbContext db) {
        if (await Owner(vessel, assignment, h, db) == null) return await Fail(h, 404, "resource_unavailable");
        if (!await Csrf(h)) return await Fail(h, 403, "csrf_invalid");
        var e = await Enrollments(db, Actor(h), vessel, assignment).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null) return await Fail(h, 404, "resource_unavailable");
        // Cleanup reveals no cached values and grants no decryption/capture/replay rights.
        // Remains reachable after expiry/revocation/policy withdrawal, under current ordinary ownership rights.
        Event(db, e, "CleanupAllowed", Now(h)); await db.SaveChangesAsync(h.RequestAborted);
        return Results.Ok(new OfflineCleanupDto(e.Id, e.TenantId, e.ActorId, e.VesselId, e.AssignmentId, e.DeviceId, e.KeyId, e.PolicyVersion));
    }
    private static async Task<IResult> Lifecycle(Guid vessel, Guid assignment, Guid enrollment, string action, HttpContext h, PoseidonDbContext db) {
        if (await Owner(vessel, assignment, h, db) == null) return await Fail(h, 404, "resource_unavailable");
        if (!await Csrf(h)) return await Fail(h, 403, "csrf_invalid");
        var e = await Enrollments(db, Actor(h), vessel, assignment).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null) return await Fail(h, 404, "resource_unavailable");
        var now = Now(h);
        if (action == "Renewed") {
            if (!Current(h, e, now)) return await Fail(h, 403, "offline_policy_denied");
            e.ExpiresAt = new[] { now.AddSeconds(Policy(h).LeaseSeconds), Policy(h).ExpiresAt }.Min();
        } else if (e.RevokedAt == null) e.RevokedAt = now;
        Event(db, e, action, now);
        try { await db.SaveChangesAsync(h.RequestAborted); }
        catch (DbUpdateException) { return await Fail(h, 409, "offline_retry"); }
        return action == "Renewed" ? Results.Ok(Context(h, e, now)) : Results.Ok(new { state = "RetainedLocked" });
    }

    // Raw body is bounded and hash checked. Both body and UUID remain frozen for uncertain retries.
    private static bool Unique(JsonElement value) => value.ValueKind != JsonValueKind.Object ||
        value.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == value.EnumerateObject().Count() && value.EnumerateObject().All(x => Unique(x.Value));
    private static async Task<IResult> Replay(Guid vessel, Guid assignment, Guid enrollment, HttpContext h, PoseidonDbContext db)
    {
        var owner = await Owner(vessel, assignment, h, db); if (owner == null) return await Fail(h, 404, "resource_unavailable");
        if (!await Csrf(h)) return await Fail(h, 403, "csrf_invalid");
        OfflineReplayCommand? c; byte[] body;
        try {
            if (!h.Request.HasJsonContentType()) return await Fail(h, 400, "invalid_offline_command");
            using var ms = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, ms, 4096, h.RequestAborted); body = ms.ToArray();
            using var json = JsonDocument.Parse(body, new() { MaxDepth = 4 });
            if (!Unique(json.RootElement)) return await Fail(h, 400, "invalid_offline_command");
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("capturedAt", out var captured) ||
                captured.ValueKind != JsonValueKind.String || captured.GetString() is not { } capturedText ||
                !(capturedText.EndsWith('Z') || capturedText.EndsWith("+00:00", StringComparison.Ordinal)))
                return await Fail(h, 400, "invalid_offline_command");
            c = JsonSerializer.Deserialize<OfflineReplayCommand>(body, Json);
        } catch (Exception ex) when (ex is JsonException or InvalidDataException) { return await Fail(h, 400, "invalid_offline_command"); }
        if (c == null || c.CommandId == Guid.Empty || c.KeyId == Guid.Empty || c.DeviceId == Guid.Empty) return await Fail(h, 400, "invalid_offline_command");
        var hash = Convert.ToHexString(SHA256.HashData(body));
        if (h.Request.Headers["X-Offline-Hash"].Count != 1 || !string.Equals(hash, h.Request.Headers["X-Offline-Hash"], StringComparison.OrdinalIgnoreCase))
            return await Fail(h, 400, "offline_hash_mismatch");
        var a = Actor(h); var p = Policy(h); var now = Now(h);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        // Refresh the aggregate inside the serializable transaction (no stale pre-transaction tracking).
        await db.Entry(owner).ReloadAsync(h.RequestAborted);
        var e = await Enrollments(db, a, vessel, assignment).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null || !Current(h, e, now) || e.KeyId != c.KeyId || e.DeviceId != c.DeviceId || c.SourceVersion != e.SourceVersion ||
            c.PolicyVersion != e.PolicyVersion || c.Operation != OfflineWatchCapture.Operation || owner.Status != "Draft")
            return await Fail(h, 403, "offline_policy_denied");
        if (h.Request.Headers["X-Offline-Signature"].Count != 1 || !VerifyKey(e.PublicSigningKey, h.Request.Headers["X-Offline-Signature"], hash))
            return await Fail(h, 403, "offline_key_proof_denied");
        // Current ordinary write rights/policy/device/source precede even immutable receipt recovery.
        var recorded = await Outcomes(db, a, e).SingleOrDefaultAsync(o => o.CommandId == c.CommandId, h.RequestAborted);
        if (recorded != null) {
            if (recorded.EnrollmentId != e.Id || recorded.Digest != hash) return await Fail(h, 409, "offline_command_reused");
            OperationalTelemetry.ObserveReplay(ReplayObservation.Recovered);
            return Results.Ok(Dto(recorded));
        }
        if (e.ExpiresAt <= now || !OfflineWatchCapture.ValidCaptureTime(c.CapturedAt, c.OffsetMinutes, e.CreatedAt, now, e.ExpiresAt) ||
            !OfflineWatchCapture.ValidValue(c.WatchGroup, p.WatchGroups)) return await Fail(h, 409, "offline_recovery_required");
        var original = Private(h, e, e.ProtectedBase);
        string? local = c.WatchGroup; OfflineOutcome? parent = null;
        if (c.ParentCommandId != null) {
            parent = await Outcomes(db, a, e).SingleOrDefaultAsync(o => o.CommandId == c.ParentCommandId && o.Result == "MaterialConflict", h.RequestAborted);
            if (parent == null || c.WatchGroup != null || c.ExpectedServerVersion == null || c.BaseVersion != c.ExpectedServerVersion ||
                c.Reason is not { Length: > 0 and <= 500 } || string.IsNullOrWhiteSpace(c.Reason) || c.Reason.Any(char.IsControl) || c.Choice is not ("keepServer" or "useCaptured"))
                return await Fail(h, 400, "invalid_offline_resolution");
            if (c.ExpectedServerVersion != owner.Version) return await Fail(h, 409, "offline_conflict_changed");
            // At most one immutable resolution successor; races serialize at the DB unique index.
            if (await Outcomes(db, a, e).AnyAsync(o => o.ParentCommandId == parent.CommandId, h.RequestAborted)) return await Fail(h, 409, "offline_resolution_exists");
            var captured = Private(h, e, parent.ProtectedContext);
            local = c.Choice == "keepServer" ? owner.WatchGroup : captured.Local;
            if (!OfflineWatchCapture.ValidValue(local, p.WatchGroups)) return await Fail(h, 403, "offline_policy_denied");
        } else if (c.BaseVersion != e.BaseVersion || c.Choice != null || c.Reason != null || c.ExpectedServerVersion != null)
            return await Fail(h, 400, "invalid_offline_command");
        var binding = new OfflineReplayBinding(a.TenantId, a.Id, vessel, e.DeviceId, assignment);
        var decision = OfflineReplayPolicy.Evaluate(new(c.CommandId, binding, c.Operation, c.BaseVersion.ToString(CultureInfo.InvariantCulture), c.PolicyVersion,
            ["watch_group"], c.CapturedAt), new(binding, true, true, true, p.Version, p.NotBefore, e.ExpiresAt, now,
            new(c.Operation, new HashSet<string>(["watch_group"], StringComparer.Ordinal), true, false, false), owner.Version.ToString(CultureInfo.InvariantCulture)), hash);
        if (decision is not (OfflineReplayDecision.MaterialConflict or OfflineReplayDecision.EligibleForTransactionalRevalidation)) return await Fail(h, 409, "offline_recovery_required");
        var serverWatch = owner.WatchGroup;
        var outcome = new OfflineOutcome { TenantId = a.TenantId, ActorId = a.Id, CommandId = c.CommandId, EnrollmentId = e.Id,
            ParentCommandId = parent?.CommandId, VesselId = vessel, AssignmentId = assignment, Digest = hash, BaseVersion = c.BaseVersion,
            ServerVersion = owner.Version, Revision = owner.Revision, CapturedAt = c.CapturedAt, OffsetMinutes = c.OffsetMinutes, ReceivedAt = now,
            Result = decision == OfflineReplayDecision.MaterialConflict ? "MaterialConflict" : "Committed" };
        if (decision == OfflineReplayDecision.EligibleForTransactionalRevalidation) {
            // Reuse the ordinary handler, under THIS transaction and the real request actor.
            // Copy only the supported field; current remaining domain fields and server source time are preserved.
            h.Request.Headers["Idempotency-Key"] = c.CommandId.ToString("D");
            var command = new CrewSaveCommand(owner.Id, owner.PersonId, owner.Version, null, null, owner.PositionId, owner.From, owner.To,
                owner.ContractEndDate, local, owner.SourceObservedAt, PositionPolicyVersion: (await CrewPositionPolicy.Current(h, db, vessel)).Version);
            var result = await CrewEndpoints.ApplyImport(vessel, command, h, db, h.RequestServices.GetRequiredService<IAntiforgery>(), false, preservePositionLineage: true);
            if (result is not IStatusCodeHttpResult status || status.StatusCode != 200) return result; // disposal rolls back all saves
            outcome.ServerVersion = owner.Version; outcome.Revision = owner.Revision;
        }
        outcome.ProtectedContext = Protect(h, e, new(original.Base, local, serverWatch,
            outcome.Result == "Committed" ? local : null, c.Reason));
        db.OfflineOutcomes.Add(outcome);
        try {
            await db.SaveChangesAsync(h.RequestAborted);
            // Return the durable PostgreSQL timestamp precision on first delivery too.
            // Recovery must yield exactly the original persisted outcome.
            await db.Entry(outcome).ReloadAsync(h.RequestAborted);
            await tx.CommitAsync(h.RequestAborted);
        }
        catch (DbUpdateException) { return await Fail(h, 409, "offline_retry"); }
        catch (Exception ex) when (PostgresFailure.IsSerialization(ex)) { return await Fail(h, 409, "offline_retry"); }
        OperationalTelemetry.ObserveReplay(outcome.Result == "Committed" ? ReplayObservation.Committed : ReplayObservation.MaterialConflict);
        return Results.Ok(Dto(outcome));
    }
    private static async Task<IResult> History(Guid vessel, Guid assignment, Guid enrollment, HttpContext h, PoseidonDbContext db) {
        if (await Owner(vessel, assignment, h, db) == null) return await Fail(h, 404, "resource_unavailable");
        var e = await Enrollments(db, Actor(h), vessel, assignment).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null || !Current(h, e, Now(h))) return await Fail(h, 403, "offline_policy_denied");
        if (h.Request.Query.Any(q => q.Key != "offset" || q.Value.Count != 1) ||
            h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed is < 0 or > 1000000))
            return await Fail(h, 400, "invalid_offline_page");
        var offset = h.Request.Query.ContainsKey("offset") ? int.Parse(h.Request.Query["offset"]!, CultureInfo.InvariantCulture) : 0;
        var rows = await Outcomes(db, Actor(h), e).OrderByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "OfflineHistory", "Success", resourceId: assignment);
        return Results.Ok(new OfflineHistoryDto(rows.Take(25).Select(Dto).ToArray(), offset, rows.Length > 25));
    }
    private static async Task<IResult> Conflict(Guid vessel, Guid assignment, Guid enrollment, Guid command, HttpContext h, PoseidonDbContext db) {
        var owner = await Owner(vessel, assignment, h, db); if (owner == null) return await Fail(h, 404, "resource_unavailable");
        var e = await Enrollments(db, Actor(h), vessel, assignment).SingleOrDefaultAsync(e => e.Id == enrollment, h.RequestAborted);
        if (e == null || !Current(h, e, Now(h))) return await Fail(h, 403, "offline_policy_denied");
        var o = await Outcomes(db, Actor(h), e).SingleOrDefaultAsync(o => o.CommandId == command && o.Result == "MaterialConflict", h.RequestAborted);
        if (o == null) return await Fail(h, 404, "resource_unavailable");
        var value = Private(h, e, o.ProtectedContext); var p = Policy(h);
        OfflineConflictValueDto Project(string? text) => text == null ? new("unknown") : OfflineWatchCapture.ValidValue(text, p.WatchGroups)
            ? new("known", text) : new("withheld");
        var resolved = await Outcomes(db, Actor(h), e).SingleOrDefaultAsync(x => x.ParentCommandId == command, h.RequestAborted);
        var resolution = resolved == null ? null : Private(h, e, resolved.ProtectedContext);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "OfflineConflictRead", "Success", resourceId: assignment);
        return Results.Ok(new OfflineConflictDetailDto(command, owner.Version.ToString(CultureInfo.InvariantCulture), e.Id.ToString("D"),
            Project(value.Base), owner.Version, resolved == null ? null : Dto(resolved), o.ServerVersion,
            resolution == null ? null : Project(resolution.Resolved), resolution?.Reason, resolved?.ActorId,
            [new("watch_group", "Watch group", Project(value.Local), Project(owner.WatchGroup),
                resolved == null && owner.Status == "Draft" &&
                    OfflineWatchCapture.ValidValue(value.Local, p.WatchGroups) && OfflineWatchCapture.ValidValue(owner.WatchGroup, p.WatchGroups)
                    ? ["keepServer", "useCaptured"] : [])]));
    }
}
