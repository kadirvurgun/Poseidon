using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record SafetyEventOfflineScope(Guid TenantId, Guid ActorId, Guid VesselId);
public sealed record SafetyEventOfflineField(string Key, string ClassificationReference, string Purpose, string PrivacyClass);

// DEV-060 offline capture: server operator configuration only. No approved classification/retention decision for
// offline safety-event drafts exists in the sources, so every captured part must be classified with the most
// restrictive existing class and a missing/incomplete policy never permits local persistence (UNAVAILABLE_POLICY).
// Only NAMED draft capture is offline: CONFIDENTIAL reports, submit, triage, investigation and closure stay online.
// Gate SAFETY-OFFLINE-CLASSIFICATION remains open until an authorized privacy/retention decision exists.
public sealed class SafetyEventOfflinePolicy
{
    public const string RequiredPrivacyClass = "RestrictedOperationalDraft";
    public const string Operation = "safety-event.capture.v1";
    public static readonly string[] Parts = ["classification", "occurrence", "context", "description", "immediateAction"];
    public string Version { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string StoragePolicyReference { get; set; } = "";
    public string KeyPolicyReference { get; set; } = "";
    public string RetentionPolicyReference { get; set; } = "";
    public int RetentionSeconds { get; set; }
    public int LeaseSeconds { get; set; }
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxEnrollments { get; set; }
    public int MaxRecords { get; set; }
    public int MaxEncryptedBytes { get; set; }
    public int MaxDeviceRecords { get; set; }
    public int MaxDeviceEncryptedBytes { get; set; }
    public SafetyEventOfflineField[] Fields { get; set; } = [];
    public SafetyEventOfflineScope[] Scopes { get; set; } = [];
    private static bool Text(string? value, int maximum = 256) => value is { Length: > 0 } && value.Length <= maximum && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    internal bool Configured => Text(Version, 80) && SourceVersion == SafetyEventPolicy.SourceVersion &&
        new[] { StoragePolicyReference, KeyPolicyReference, RetentionPolicyReference }.All(v => Text(v)) &&
        LeaseSeconds is >= 60 and <= 86400 && RetentionSeconds >= LeaseSeconds && RetentionSeconds <= 2592000 &&
        NotBefore.Offset == TimeSpan.Zero && ExpiresAt.Offset == TimeSpan.Zero && ExpiresAt > NotBefore &&
        MaxEnrollments is >= 1 and <= 32 && MaxRecords is >= 2 and <= 128 && MaxEncryptedBytes is >= 131072 and <= 4194304 &&
        MaxDeviceRecords >= MaxRecords && MaxDeviceRecords <= 512 && MaxDeviceEncryptedBytes >= MaxEncryptedBytes && MaxDeviceEncryptedBytes <= 16777216 &&
        Fields is { Length: 5 } && Fields.All(f => f != null && Parts.Contains(f.Key) && Text(f.ClassificationReference) && Text(f.Purpose) && f.PrivacyClass == RequiredPrivacyClass) &&
        Fields.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count() == Fields.Length &&
        Scopes is { Length: > 0 and <= 256 } && Scopes.All(s => s != null && s.TenantId != Guid.Empty && s.ActorId != Guid.Empty && s.VesselId != Guid.Empty) && Scopes.Distinct().Count() == Scopes.Length;
    internal string Fingerprint() => RegisterStorage.Hash(JsonSerializer.Serialize(Snapshot()));
    internal bool Allows(Guid tenant, Guid actor, Guid vessel, DateTimeOffset now) => Configured && now >= NotBefore && now < ExpiresAt && Scopes.Contains(new(tenant, actor, vessel));
    internal SafetyEventOfflinePolicy Snapshot() => new() { Version = Version, SourceVersion = SourceVersion, StoragePolicyReference = StoragePolicyReference,
        KeyPolicyReference = KeyPolicyReference, RetentionPolicyReference = RetentionPolicyReference, RetentionSeconds = RetentionSeconds, LeaseSeconds = LeaseSeconds,
        NotBefore = NotBefore, ExpiresAt = ExpiresAt, MaxEnrollments = MaxEnrollments, MaxRecords = MaxRecords, MaxEncryptedBytes = MaxEncryptedBytes,
        MaxDeviceRecords = MaxDeviceRecords, MaxDeviceEncryptedBytes = MaxDeviceEncryptedBytes,
        Fields = Fields.Select(f => f with { }).OrderBy(f => f.Key, StringComparer.Ordinal).ToArray(),
        Scopes = Scopes.OrderBy(s => s.TenantId).ThenBy(s => s.ActorId).ThenBy(s => s.VesselId).ToArray() };
}
public sealed class SafetyEventOfflineSettings
{
    public SafetyEventOfflinePolicy? Policy { get; set; }
    internal SafetyEventOfflinePolicy? For(Guid tenant, Guid actor, Guid vessel, DateTimeOffset now) => Policy?.Allows(tenant, actor, vessel, now) == true ? Policy.Snapshot() : null;
}

public sealed record SafetyEventOfflineContextDto(Guid Id, Guid TenantId, Guid ActorId, Guid VesselId, Guid DeviceId, Guid KeyId, string Challenge, string PolicyVersion,
    string PolicyDigest, string AuthorityDigest, string SourceVersion, string Operation, string[] AllowedParts, string[] Categories, string[] OperationContexts,
    string[] Severities, DateTimeOffset ServerTime, DateTimeOffset ExpiresAt, DateTimeOffset RetainUntil, int MaxRecords, int MaxEncryptedBytes,
    int MaxDeviceRecords, int MaxDeviceEncryptedBytes)
{ public override string ToString() => nameof(SafetyEventOfflineContextDto); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SafetyEventOfflineCapture(Guid CommandId, Guid EventId, Guid KeyId, Guid DeviceId, string PolicyVersion, string PolicyDigest,
    string SourceVersion, string Operation, DateTimeOffset CapturedAt, int OffsetMinutes, [property: JsonRequired] SafetyEventDraftFields Fields)
{ public override string ToString() => nameof(SafetyEventOfflineCapture); }
public sealed record SafetyEventOfflineResult(Guid CommandId, string Result, Guid EventId, string EventNo, long Revision, DateTimeOffset ReceivedAt);

// DEV-060 / DEV-033 protocol: /api/v1/safety-event-offline?vesselId=. Same enrollment/key proof/signed replay as the
// shared offline application, in its own resource namespace. A replay creates one NAMED DRAFT (Origin OFFLINE)
// exactly once; the same command and digest returns the immutable first outcome. Evidence files are online only.
public static class SafetyEventOfflineEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 10, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private const string Read = "SafetyEventOfflineRead", Request = "SafetyEventOfflineRequest";
    public static void MapSafetyEventOffline(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/safety-event-offline").RequireAuthorization("DirectoryRead");
        g.MapGet("", Inventory).WithName("SafetyEventOfflineInventory");
        g.MapPost("", Enroll).WithName("SafetyEventOfflineEnroll").Produces<SafetyEventOfflineContextDto>();
        g.MapPost("/{enrollment:guid}/{action}", Action).WithName("SafetyEventOfflineAction");
        g.MapGet("/{enrollment:guid}/history", History).WithName("SafetyEventOfflineHistory");
    }
    private static TrustedActor? Actor(HttpContext h) => SafetyEventCommands.Actor(h);
    private static DateTimeOffset Now(HttpContext h) { var now = SafetyEventCommands.Now(h).ToUniversalTime(); return now.AddTicks(-(now.Ticks % 10)); }
    private static SafetyEventOfflinePolicy? Policy(HttpContext h, Guid vessel) => Actor(h) is { } a ? h.RequestServices.GetRequiredService<SafetyEventOfflineSettings>().For(a.TenantId, a.Id, vessel, Now(h)) : null;
    private static bool Rights(TrustedActor? a) => SafetyEventAccess.Reporter(a) && a!.HasWriteAttribution;
    private static string Authority(TrustedActor a) => RegisterStorage.Hash(string.Join(',', a.Permissions.Where(p => p.StartsWith("hse.safety_event.", StringComparison.Ordinal)).Order(StringComparer.Ordinal)));
    internal static SafetyEventOfflineDto Capability(HttpContext h, TrustedActor a, Guid vessel) =>
        Rights(a) && Policy(h, vessel) != null ? new(true, "CONFIGURED_UNVERIFIED") : new(false, "UNAVAILABLE_POLICY");
    private static async Task<IResult> Fail(HttpContext h, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, Request, code); return Results.Json(new { code }, statusCode: status);
    }
    private static bool Query(HttpContext h, bool paging, out Guid vessel, out int offset)
    {
        vessel = Guid.Empty; offset = 0;
        return h.Request.Query.All(q => q.Value.Count == 1 && (q.Key == "vesselId" || paging && q.Key == "offset")) && Guid.TryParseExact(h.Request.Query["vesselId"], "D", out vessel) && vessel != Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) && offset <= 100000);
    }
    private static async Task<(TrustedActor? Actor, Guid Vessel, string Rights)> Gate(HttpContext h, PoseidonDbContext db, bool paging = false)
    {
        if (!Query(h, paging, out var vessel, out _)) return (null, Guid.Empty, "");
        var a = Actor(h); if (!Rights(a) || db.TenantId != a!.TenantId) return (null, Guid.Empty, "");
        if (!await a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == vessel, h.RequestAborted)) return (null, Guid.Empty, "");
        return (a, vessel, Authority(a));
    }
    private static bool Matches(HttpContext h, SafetyEventOfflineEnrollment e, SafetyEventOfflinePolicy? p, DateTimeOffset now) => p != null && e.RevokedAt == null && now < e.RetainUntil &&
        Actor(h) is { } a && a.Id == e.ActorId && a.TenantId == e.TenantId && e.AuthorityDigest == Authority(a) && e.PolicyVersion == p.Version &&
        e.PolicyDigest == p.Fingerprint() && e.SourceVersion == p.SourceVersion;
    // Revocation/key binding observed in a NEW trusted tenant context together with vessel visibility.
    private static async Task<bool> Fresh(HttpContext h, SafetyEventOfflineEnrollment expected, string rights, string? publicKey, bool privateAccess = true, bool captureLease = false)
    {
        await using var scope = h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ActorResolver>(); var a = resolver.Resolve(h.User);
        if (a == null || a.Id != expected.ActorId || a.TenantId != expected.TenantId || !Rights(a) || Authority(a) != rights) return false;
        PoseidonDbContext db; try { db = scope.ServiceProvider.GetRequiredService<PoseidonDbContext>(); } catch (UnauthorizedAccessException) { return false; }
        if (db.TenantId != expected.TenantId) return false;
        var vessels = a.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == expected.VesselId);
        var fresh = await db.SafetyEventOfflineEnrollments.AsNoTracking().Where(e => e.Id == expected.Id && e.ActorId == expected.ActorId && e.VesselId == expected.VesselId && vessels.Any())
            .SingleOrDefaultAsync(h.RequestAborted);
        if (fresh == null || fresh.DeviceId != expected.DeviceId || fresh.KeyId != expected.KeyId || fresh.PublicKey != publicKey || fresh.Challenge != expected.Challenge ||
            fresh.PolicyVersion != expected.PolicyVersion || fresh.PolicyDigest != expected.PolicyDigest || fresh.AuthorityDigest != expected.AuthorityDigest ||
            fresh.SourceVersion != expected.SourceVersion || fresh.CreatedAt != expected.CreatedAt || fresh.RetainUntil != expected.RetainUntil) return false;
        var live = resolver.Resolve(h.User);
        return live != null && live.Id == expected.ActorId && Rights(live) && Authority(live) == rights &&
            (!privateAccess || Matches(h, fresh, Policy(h, expected.VesselId), Now(h)) && (!captureLease || Now(h) < fresh.ExpiresAt));
    }
    private static async Task<IResult> Committed(HttpContext h, SafetyEventOfflineEnrollment e, string rights, Func<IResult> response, bool privateAccess = true, bool captureLease = false, bool audit = false)
    {
        try
        {
            if (audit) await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, Request, "Success", resourceId: e.Id);
            if (!await Fresh(h, e, rights, e.PublicKey, privateAccess, captureLease)) return await Fail(h, 503, "command_outcome_withheld");
            return response();
        }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)) { return Results.Json(new { code = "command_outcome_withheld" }, statusCode: 503); }
    }
    private static IQueryable<SafetyEventOfflineEnrollment> Enrollments(PoseidonDbContext db, Guid actor, Guid vessel) => db.SafetyEventOfflineEnrollments.Where(e => e.ActorId == actor && e.VesselId == vessel);
    private static IQueryable<SafetyEventOfflineOutcome> Outcomes(PoseidonDbContext db, SafetyEventOfflineEnrollment e) =>
        db.SafetyEventOfflineOutcomes.Where(o => o.ActorId == e.ActorId && o.EnrollmentId == e.Id && o.VesselId == e.VesselId);
    private static void Event(PoseidonDbContext db, SafetyEventOfflineEnrollment e, string action, DateTimeOffset now) => db.SafetyEventOfflineLifecycles.Add(new() {
        TenantId = e.TenantId, Id = Guid.NewGuid(), EnrollmentId = e.Id, ActorId = e.ActorId, VesselId = e.VesselId, Action = action, RecordedAt = now });
    private static SafetyEventOfflineContextDto Context(HttpContext h, SafetyEventOfflineEnrollment e, SafetyEventOfflinePolicy p) => new(e.Id, e.TenantId, e.ActorId, e.VesselId,
        e.DeviceId, e.KeyId, e.Challenge, e.PolicyVersion, e.PolicyDigest, e.AuthorityDigest, e.SourceVersion, SafetyEventOfflinePolicy.Operation, p.Fields.Select(f => f.Key).ToArray(),
        SafetyEventPolicy.Categories, SafetyEventPolicy.OperationContexts, SafetyEventPolicy.Severities, Now(h), e.ExpiresAt, e.RetainUntil, p.MaxRecords, p.MaxEncryptedBytes,
        p.MaxDeviceRecords, p.MaxDeviceEncryptedBytes);

    private static async Task<IResult> Inventory(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var gate = await Gate(h, db); if (gate.Actor == null) return await Fail(h, 404, "resource_unavailable");
        var rows = await Enrollments(db, gate.Actor.Id, gate.Vessel).AsNoTracking().OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id).Take(32)
            .Select(e => new { e.Id, e.ExpiresAt, e.RetainUntil, e.RevokedAt, e.PolicyVersion }).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, Read, "Success", resourceId: gate.Vessel);
        if (!Rights(Actor(h)) || Authority(Actor(h)!) != gate.Rights) return await Fail(h, 403, "permission_denied");
        return Results.Ok(new { items = rows });
    }
    private static async Task<IResult> Failure(HttpContext h, Exception error) => await Fail(h,
        error is DbUpdateConcurrencyException || PostgresFailure.IsSerialization(error) || PostgresFailure.Provider(error)?.SqlState == "23505" ? 409 : 503, "offline_retry");
    private static async Task<IResult> Enroll(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var gate = await Gate(h, db); if (gate.Actor == null) return await Fail(h, 403, "permission_denied");
        if (!await SafetyEventCommands.Csrf(h)) return await Fail(h, 403, "csrf_invalid");
        var p = Policy(h, gate.Vessel); if (p == null) return await Fail(h, 403, "offline_policy_denied");
        IDbContextTransaction? tx = null; SafetyEventOfflineEnrollment? committed = null;
        try
        {
            tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, h.RequestAborted);
            if (await Enrollments(db, gate.Actor.Id, gate.Vessel).CountAsync(e => e.RevokedAt == null, h.RequestAborted) >= p.MaxEnrollments) return await Fail(h, 409, "enrollment_limit");
            var now = Now(h); var e = new SafetyEventOfflineEnrollment { TenantId = gate.Actor.TenantId, ActorId = gate.Actor.Id, VesselId = gate.Vessel, Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(), KeyId = Guid.NewGuid(), Challenge = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), PolicyVersion = p.Version, PolicyDigest = p.Fingerprint(),
                AuthorityDigest = gate.Rights, SourceVersion = p.SourceVersion, CreatedAt = now, ExpiresAt = new[] { now.AddSeconds(p.LeaseSeconds), p.ExpiresAt }.Min(),
                RetainUntil = now.AddSeconds(p.RetentionSeconds) };
            db.SafetyEventOfflineEnrollments.Add(e); Event(db, e, "Enrolled", now);
            await db.SaveChangesAsync(h.RequestAborted);
            if (!Rights(Actor(h)) || Authority(Actor(h)!) != gate.Rights || !Matches(h, e, Policy(h, gate.Vessel), Now(h))) return await Fail(h, 403, "permission_denied");
            await tx.CommitAsync(h.RequestAborted); committed = e;
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)) { return await Failure(h, ex); }
        finally { if (tx != null) await tx.DisposeAsync(); }
        var context = Context(h, committed!, p);
        return await Committed(h, committed!, gate.Rights, () => Results.Ok(context), captureLease: true);
    }
    private static async Task<byte[]> Body(HttpContext h, int maximum)
    {
        if (!h.Request.HasJsonContentType()) throw new JsonException(); using var stream = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, stream, maximum, h.RequestAborted);
        using var document = JsonDocument.Parse(stream.ToArray(), new() { MaxDepth = 10 }); ApplicabilityEngine.RejectDuplicateKeys(document.RootElement); return stream.ToArray();
    }
    private static async Task<IResult> Action(Guid enrollment, string action, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var gate = await Gate(h, db); if (gate.Actor == null) return await Fail(h, 403, "permission_denied");
        if (!await SafetyEventCommands.Csrf(h)) return await Fail(h, 403, "csrf_invalid");
        if (action is not ("renew" or "key" or "revoke" or "lost" or "cleanup" or "replay")) return await Fail(h, 404, "resource_unavailable");
        IDbContextTransaction? tx = null; SafetyEventOfflineEnrollment? committed = null; SafetyEventOfflinePolicy? committedPolicy = null;
        try
        {
            tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, h.RequestAborted);
            var e = await Enrollments(db, gate.Actor.Id, gate.Vessel).SingleOrDefaultAsync(x => x.Id == enrollment, h.RequestAborted);
            if (e == null) return await Fail(h, 404, "resource_unavailable"); var now = Now(h); var p = Policy(h, gate.Vessel); var originalKey = e.PublicKey;
            if (action is "revoke" or "lost" or "cleanup")
            {
                if (action == "cleanup") Event(db, e, "CleanupAllowed", now);
                else if (e.RevokedAt == null) { e.RevokedAt = now; Event(db, e, action == "lost" ? "KeyLost" : "Revoked", now); }
                else Event(db, e, action == "lost" ? "KeyLost" : "Revoked", now);
            }
            else
            {
                if (!Matches(h, e, p, now)) return await Fail(h, 403, "offline_policy_denied");
                if (action == "renew") { e.ExpiresAt = new[] { now.AddSeconds(p!.LeaseSeconds), p.ExpiresAt, e.RetainUntil }.Min(); Event(db, e, "Renewed", now); }
                else if (action == "replay") return await Replay(h, db, tx, e, gate.Rights);
                else
                {
                    if (e.ExpiresAt <= now) return await Fail(h, 403, "reauthorization_required");
                    var key = JsonSerializer.Deserialize<OfflineKeyEnrollment>(await Body(h, 1024), Json);
                    if (key == null || !OfflineApplicationEndpoints.VerifyKey(key.PublicKey, key.Proof, $"safety-event/{e.VesselId:D}/{e.Id:D}/{e.KeyId:D}/{e.Challenge}") || e.PublicKey != null && e.PublicKey != key.PublicKey)
                        return await Fail(h, 403, "invalid_key_enrollment");
                    if (e.PublicKey == null) { e.PublicKey = key.PublicKey; Event(db, e, "KeyEnrolled", now); }
                }
            }
            await db.SaveChangesAsync(h.RequestAborted);
            if (!await Fresh(h, e, gate.Rights, originalKey, privateAccess: action is "renew" or "key", captureLease: action == "key")) return await Fail(h, 403, "permission_denied");
            await tx.CommitAsync(h.RequestAborted); committed = e; committedPolicy = p;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(ex)) { return await Failure(h, ex); }
        finally { if (tx != null) await tx.DisposeAsync(); }
        var saved = committed!;
        var context = action == "renew" ? Context(h, saved, committedPolicy!) : null;
        return await Committed(h, saved, gate.Rights, () => action switch {
            "renew" => Results.Ok(context),
            "cleanup" => Results.Ok(new { saved.Id, saved.TenantId, saved.ActorId, saved.VesselId, saved.DeviceId, saved.KeyId, saved.PolicyVersion, saved.PolicyDigest, saved.AuthorityDigest, saved.SourceVersion }),
            _ => Results.Ok(new { state = action == "key" ? "KeyEnrolled" : "Revoked" })
        }, privateAccess: action is "renew" or "key", captureLease: action == "renew");
    }
    private static async Task<IResult> Replay(HttpContext h, PoseidonDbContext db, IDbContextTransaction tx, SafetyEventOfflineEnrollment e, string rights)
    {
        var bytes = await Body(h, 131072); var capture = JsonSerializer.Deserialize<SafetyEventOfflineCapture>(bytes, Json); var now = Now(h); var ct = h.RequestAborted;
        using var parsed = JsonDocument.Parse(bytes); var capturedText = parsed.RootElement.TryGetProperty("capturedAt", out var at) && at.ValueKind == JsonValueKind.String ? at.GetString() : null;
        if (capture == null || capture.CommandId == Guid.Empty || capture.EventId == Guid.Empty || capture.KeyId != e.KeyId || capture.DeviceId != e.DeviceId ||
            capture.PolicyVersion != e.PolicyVersion || capture.PolicyDigest != e.PolicyDigest || capture.SourceVersion != e.SourceVersion || capture.Operation != SafetyEventOfflinePolicy.Operation ||
            capturedText == null || !(capturedText.EndsWith('Z') || capturedText.EndsWith("+00:00", StringComparison.Ordinal)) || capture.Fields.Evidence is { Length: > 0 })
            return await Fail(h, 400, "invalid_offline_command");
        var idempotency = h.Request.Headers["Idempotency-Key"]; if (idempotency.Count != 1 || idempotency[0] != capture.CommandId.ToString("D")) return await Fail(h, 400, "invalid_idempotency_key");
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        if (h.Request.Headers["X-Offline-Hash"].Count != 1 || !string.Equals(h.Request.Headers["X-Offline-Hash"], digest, StringComparison.Ordinal)) return await Fail(h, 400, "offline_hash_mismatch");
        if (h.Request.Headers["X-Offline-Signature"].Count != 1 || !OfflineApplicationEndpoints.VerifyKey(e.PublicKey, h.Request.Headers["X-Offline-Signature"], digest)) return await Fail(h, 403, "offline_key_proof_denied");
        // Idempotent reconnect: the same key and digest returns the immutable first outcome.
        var old = await Outcomes(db, e).AsNoTracking().SingleOrDefaultAsync(o => o.CommandId == capture.CommandId, ct);
        if (old != null)
        {
            if (old.Digest != digest) return await Fail(h, 409, "offline_command_reused");
            if (!await Fresh(h, e, rights, e.PublicKey)) return await Fail(h, 403, "permission_denied");
            return await Committed(h, e, rights, () => Results.Content(old.OutcomeJson, "application/json"), audit: true);
        }
        if (e.ExpiresAt <= now || !OfflineWatchCapture.ValidCaptureTime(capture.CapturedAt, capture.OffsetMinutes, e.CreatedAt, now, e.ExpiresAt)) return await Fail(h, 409, "offline_recovery_required");
        if (await db.SafetyEvents.AnyAsync(x => x.Id == capture.EventId, ct)) return await Fail(h, 409, "offline_event_exists");
        if (Actor(h) is not { } writer || writer.Id != e.ActorId || writer.TenantId != e.TenantId) return await Fail(h, 403, "permission_denied");
        var created = await SafetyEventEndpoints.CreateDraft(db, writer, e.VesselId, capture.EventId, SafetyEventPolicy.Named, capture.Fields, null, "OFFLINE", now, ct);
        if (created.Status != 200) return await Fail(h, created.Status, created.Code!);
        var outcome = new SafetyEventOfflineResult(capture.CommandId, "Committed", capture.EventId, SafetyEventEndpoints.EventNo(capture.EventId, now), created.Revision, now);
        var json = JsonSerializer.Serialize(outcome, Json);
        db.SafetyEventOfflineOutcomes.Add(new() { TenantId = e.TenantId, ActorId = e.ActorId, CommandId = capture.CommandId, EnrollmentId = e.Id, VesselId = e.VesselId, EventId = capture.EventId,
            Digest = digest, Result = "Committed", Revision = created.Revision, OutcomeJson = json, CapturedAt = capture.CapturedAt.ToUniversalTime(), ReceivedAt = now });
        await db.SaveChangesAsync(ct);
        if (!await Fresh(h, e, rights, e.PublicKey, captureLease: true)) return await Fail(h, 403, "permission_denied");
        await tx.CommitAsync(ct);
        return await Committed(h, e, rights, () => Results.Content(json, "application/json"), audit: true);
    }
    private static async Task<IResult> History(Guid enrollment, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var gate = await Gate(h, db, true); if (gate.Actor == null) return await Fail(h, 404, "resource_unavailable");
        var e = await Enrollments(db, gate.Actor.Id, gate.Vessel).AsNoTracking().SingleOrDefaultAsync(x => x.Id == enrollment, h.RequestAborted);
        if (e == null || !Matches(h, e, Policy(h, gate.Vessel), Now(h))) return await Fail(h, 403, "offline_policy_denied");
        Query(h, true, out _, out var offset);
        var rows = await Outcomes(db, e).AsNoTracking().OrderByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Skip(offset).Take(26).Select(o => o.OutcomeJson).ToArrayAsync(h.RequestAborted);
        if (!await Fresh(h, e, gate.Rights, e.PublicKey)) return await Fail(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, Read, "Success", resourceId: e.Id);
        return Results.Ok(new { items = rows.Take(25).Select(s => JsonSerializer.Deserialize<SafetyEventOfflineResult>(s, Json)), offset, hasMore = rows.Length > 25 });
    }
}
