using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed class NotificationSettings
{
    public NotificationRule[] Rules { get; set; } = [];
    internal NotificationRule[]? For(Guid tenant, Guid vessel)
    {
        if (Rules == null || Rules.Any(r => r == null)) return null;
        var rules = Rules.Where(r => r.TenantId == tenant && r.VesselId == vessel).Take(65).ToArray();
        return rules.Length > 64 || rules.Any(r => !NotificationPolicy.Valid(r)) || rules.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count() != rules.Length
            ? null : rules.Select(r => r with { }).OrderBy(r => r.Key, StringComparer.Ordinal).ToArray();
    }
}
public sealed record NotificationDto(Guid Id, Guid VesselId, Guid SourceId, long SourceRevision, string RuleKey,
    string PolicyVersion, string SourceState, string Severity, bool Mandatory, Guid RecipientRoleId, Guid? EscalationRoleId,
    DateOnly? DueDate, DateTimeOffset CreatedAt, DateTimeOffset? EscalateAt, string State, long Revision, uint Version,
    string Provenance = "ConfiguredUnverified");
public sealed record NotificationInboxDto(NotificationDto[] Items, int Offset, bool HasMore, string PolicyState, string Delivery,
    bool CanManage, bool CanAcknowledge, bool CanWritePreference);
public sealed record NotificationPreferenceDto(bool MutedAdvisory, long Revision, uint Version, bool MandatoryRouteMutable = false);
public sealed record NotificationSavedDto(Guid Id, long Revision, uint Version, string State);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotificationAckCommand([property: JsonRequired] uint ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotificationPreferenceCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] bool MutedAdvisory);

public static class NotificationEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 4 };
    internal static bool CanRead(TrustedActor? actor) => actor != null && actor.CanRead && actor.HasWriteAttribution &&
        new[] { "notification.read", "maintenance.read", "equipment.read" }.All(actor.Permissions.Contains);
    private static bool Manage(TrustedActor? actor, Guid vessel) => CanRead(actor) && actor!.CanEditVessel(vessel) && actor.Permissions.Contains("notification.manage");
    private static Guid[] Roles(TrustedActor actor) => actor.RoleContext?.Split(',').Select(Guid.Parse).ToArray() ?? [];
    private static string Rights(TrustedActor a) => RegisterStorage.Hash(JsonSerializer.Serialize(new {
        a.Id, a.TenantId, a.RoleContext, a.TenantWideRead, a.TenantWideManage, a.AuditCapabilities,
        vessels = a.VesselGrants.OrderBy(v => v.VesselId).ThenBy(v => v.CanEdit)
    }, Json));
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Current(HttpContext h, TrustedActor actor) => Actor(h) is { } live && CanRead(live) && Rights(live) == Rights(actor);
    private static IQueryable<Notification> Visible(PoseidonDbContext db, TrustedActor actor, Guid vessel)
    {
        var roles = Roles(actor); var manage = Manage(actor, vessel);
        return db.Notifications.Where(n => n.VesselId == vessel && n.State != "SyntheticUnverified" &&
            (manage || roles.Contains(n.RecipientRoleId) || n.EscalationRoleId != null && roles.Contains(n.EscalationRoleId.Value) &&
                db.NotificationEvents.Any(e => e.VesselId == vessel && e.NotificationId == n.Id && e.Kind == "Escalated")));
    }
    private static async Task<IResult> Fail(HttpContext h, int status, string code)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,
            h.Request.Method is "GET" or "HEAD" ? "NotificationRead" : "NotificationCommand", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static bool Query(HttpContext h, bool paging, out Guid vessel, out int offset)
    {
        vessel = Guid.Empty; offset = 0;
        return h.Request.Query.All(q => q.Value.Count == 1 && (q.Key == "vesselId" || paging && q.Key == "offset")) &&
            Guid.TryParseExact(h.Request.Query["vesselId"], "D", out vessel) && vessel != Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) && offset <= 100000);
    }
    private static async Task<bool> Scope(PoseidonDbContext db, TrustedActor actor, Guid vessel, CancellationToken ct) =>
        await actor.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, ct);
    private static NotificationDto Dto(Notification n, bool sourceCurrent) => new(n.Id, n.VesselId, n.PlanId, n.PlanRevision, n.RuleKey,
        n.PolicyVersion, n.SourceState, n.Severity, n.Mandatory, n.RecipientRoleId, n.EscalationRoleId,
        n.DueDate, n.CreatedAt, n.EscalateAt, sourceCurrent || n.State == "SourceChanged" ? n.State : "SourceUnavailable", n.Revision, n.Version);

    // Only re-resolve the bounded, already-authorized page. Missing heads or
    // unresolved source/owner state project Unavailable, never a factual clear.
    private static async Task<HashSet<Guid>> ConfirmedSources(HttpContext h, PoseidonDbContext db, Guid vessel, Notification[] notices)
    {
        if (notices.Length == 0) return [];
        var tenant = db.TenantId; var rules = h.RequestServices.GetRequiredService<NotificationSettings>().For(tenant, vessel);
        if (rules is not { Length: > 0 }) return [];
        var owners = TechnicalEndpoints.EligibleOwners(h, vessel);
        var ids = notices.Select(n => n.PlanId).Distinct().ToArray();
        var sources = await MaintenanceCurrentReader.ReadOwned(vessel, db, owners,
            h.RequestServices.GetRequiredService<MaintenanceObservationSettings>(),
            h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), h.RequestAborted, limit: ids.Length, planIds: ids);
        var episodes = await db.NotificationEpisodes.AsNoTracking().Where(e => e.VesselId == vessel && ids.Contains(e.PlanId))
            .Take(4097).ToArrayAsync(h.RequestAborted);
        if (episodes.Length > 4096 || !owners.Order().SequenceEqual(TechnicalEndpoints.EligibleOwners(h, vessel).Order())) return [];
        var confirmed = new HashSet<Guid>();
        foreach (var notice in notices) {
            var source = sources.SingleOrDefault(s => s.Row.Id == notice.PlanId);
            var rule = rules.SingleOrDefault(r => r.Key == notice.RuleKey && NotificationEngine.Digest(r) == notice.PolicyDigest);
            if (source == null || rule == null || NotificationEngine.Active(rule, source, owners) != true ||
                source.Row.Revision != notice.PlanRevision || source.Row.OwnerRoleId != notice.RecipientRoleId ||
                NotificationEngine.SourceHash(source) != notice.SourceContextHash) continue;
            if (episodes.Any(e => e.PlanId == notice.PlanId && e.RuleKey == notice.RuleKey && e.PolicyDigest == notice.PolicyDigest &&
                e.Episode == notice.Episode && e.Active == true && e.SourceContextHash == notice.SourceContextHash)) confirmed.Add(notice.Id);
        }
        var current = h.RequestServices.GetRequiredService<NotificationSettings>().For(tenant, vessel);
        return current != null && current.SequenceEqual(rules) ? confirmed : [];
    }

    public static void MapNotifications(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notification-escalation").RequireAuthorization("DirectoryRead");
        group.MapGet("", Inbox).WithName("NotificationInbox").Produces<NotificationInboxDto>();
        group.MapGet("/{id:guid}/history", History).WithName("NotificationHistory");
        group.MapGet("/preference", Preference).WithName("NotificationPreferenceRead").Produces<NotificationPreferenceDto>();
        group.MapPost("/refresh", Refresh).WithName("NotificationRefresh");
        group.MapPost("/{id:guid}/acknowledge", (Guid id, HttpContext h, PoseidonDbContext db) => Command(id, h, db))
            .WithName("NotificationAcknowledge").Accepts<NotificationAckCommand>("application/json").Produces<NotificationSavedDto>();
        group.MapPut("/preference", (HttpContext h, PoseidonDbContext db) => Command(null, h, db))
            .WithName("NotificationPreference").Accepts<NotificationPreferenceCommand>("application/json").Produces<NotificationPreferenceDto>();
    }
    private static async Task<IResult> Inbox(HttpContext h, PoseidonDbContext db, NotificationSettings settings)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!CanRead(a)) return await Fail(h, 403, "permission_denied");
        if (!Query(h, true, out var vessel, out var offset)) return await Fail(h, 400, "invalid_query");
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        var muted = await db.NotificationPreferences.Where(p => p.VesselId == vessel && p.ActorId == a.Id).Select(p => p.MutedAdvisory).SingleOrDefaultAsync(ct);
        var rows = await Visible(db, a, vessel).AsNoTracking().Where(n => !muted || n.Mandatory || n.Severity == "Critical")
            .OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Skip(offset).Take(26).ToArrayAsync(ct);
        var confirmed = await ConfirmedSources(h, db, vessel, rows.Take(25).ToArray());
        if (!Current(h, a)) return await Fail(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "NotificationRead", "Success", resourceId: vessel);
        if (!Current(h, a)) return await Fail(h, 403, "permission_denied");
        return Results.Ok(new NotificationInboxDto(rows.Take(25).Select(n => Dto(n, confirmed.Contains(n.Id))).ToArray(), offset, rows.Length > 25,
            settings.For(a.TenantId, vessel) is { Length: > 0 } ? "ConfiguredUnverified" : "Unavailable", "InAppOnly",
            Manage(a, vessel), a.Permissions.Contains("notification.acknowledge"), a.Permissions.Contains("notification.preference.write")));
    }
    private static async Task<IResult> History(Guid id, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!CanRead(a)) return await Fail(h, 403, "permission_denied");
        if (!Query(h, true, out var vessel, out var offset)) return await Fail(h, 400, "invalid_query");
        if (!await Scope(db, a, vessel, ct) || !await Visible(db, a, vessel).AnyAsync(n => n.Id == id, ct)) return await Fail(h, 404, "resource_not_found");
        var items = await db.NotificationEvents.AsNoTracking().Where(e => e.VesselId == vessel && e.NotificationId == id)
            .OrderByDescending(e => e.Sequence).Skip(offset).Take(26).Select(e => new { e.Sequence, e.Kind, e.RecordedAt }).ToArrayAsync(ct);
        if (!Current(h, a)) return await Fail(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "NotificationRead", "Success", resourceId: id);
        if (!Current(h, a)) return await Fail(h, 403, "permission_denied");
        return Results.Ok(new { items = items.Take(25), offset, hasMore = items.Length > 25 });
    }
    private static async Task<IResult> Preference(HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); if (!CanRead(a)) return await Fail(h, 403, "permission_denied");
        if (!Query(h, false, out var vessel, out _)) return await Fail(h, 400, "invalid_query");
        if (!await Scope(db, a, vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var p = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.ActorId == a.Id, h.RequestAborted);
        if (!Current(h, a)) return await Fail(h, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "NotificationRead", "Success", resourceId: vessel);
        if (!Current(h, a)) return await Fail(h, 403, "permission_denied");
        return Results.Ok(new NotificationPreferenceDto(p?.MutedAdvisory ?? false, p?.Revision ?? 0, p?.Version ?? 0));
    }
    private static NotificationEvent Event(Notification n, string kind, Guid? actor, DateTimeOffset now) => new() {
        TenantId = n.TenantId, VesselId = n.VesselId, Id = Guid.NewGuid(), NotificationId = n.Id,
        Sequence = n.Revision, Kind = kind, ActorId = actor, RecordedAt = now
    };
    private static async Task<IResult> Refresh(HttpContext h, PoseidonDbContext db, NotificationEngine engine)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!Query(h, false, out var vessel, out _)) return await Fail(h, 400, "invalid_query");
        if (!Manage(a, vessel)) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid"); }
        try {
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 2048, ct);
            if (buffer.Length > 0) {
                if (!h.Request.HasJsonContentType()) throw new JsonException();
                using var body = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 2 });
                if (body.RootElement.ValueKind != JsonValueKind.Object || body.RootElement.EnumerateObject().Any()) throw new JsonException();
            }
        } catch (Exception e) when (e is JsonException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        var result = await engine.EvaluateAsync(db, vessel, a.Id, () => TechnicalEndpoints.EligibleOwners(h, vessel),
            () => Current(h, a) && Manage(Actor(h), vessel), ct);
        if (result.Status != "Completed") return result.Status switch {
            "Denied" => await Fail(h, 403, "permission_denied"),
            "LeaseBusy" or "LeaseLost" or "Conflict" => await Fail(h, 409, "version_conflict"),
            "CapacityUnavailable" => await Fail(h, 503, "request_limit"),
            "Unavailable" => await Fail(h, 503, "source_contract_changed"),
            _ => await Fail(h, 503, "service_unavailable")
        };
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "NotificationCommand", "Success", resourceId: vessel);
        return Results.Ok(new { created = result.Created, escalated = result.Escalated, sourceChanged = result.SourceChanged,
            ownerUnavailable = result.OwnerUnavailable, delivery = "InAppOnly", policyState = "ConfiguredUnverified" });
    }
    private static async Task<IResult> Command(Guid? id, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (!CanRead(a) || !a.Permissions.Contains(id != null ? "notification.acknowledge" : "notification.preference.write")) return await Fail(h, 403, "permission_denied");
        if (!Query(h, false, out var vessel, out _)) return await Fail(h, 400, "invalid_query");
        if (!await Scope(db, a, vessel, ct)) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(h, 400, "invalid_idempotency_key");
        uint expected; bool muted = false; string body;
        try {
            if (!h.Request.HasJsonContentType()) throw new JsonException();
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 2048, ct);
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            body = document.RootElement.GetRawText();
            if (id != null) expected = (document.RootElement.Deserialize<NotificationAckCommand>(Json) ?? throw new JsonException()).ExpectedVersion;
            else { var preference = document.RootElement.Deserialize<NotificationPreferenceCommand>(Json) ?? throw new JsonException(); expected = preference.ExpectedVersion; muted = preference.MutedAdvisory; }
        } catch (Exception e) when (e is JsonException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        var action = id == null ? "Preference" : "Acknowledge";
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { vessel, id, action, body }, Json));
        async Task<IResult?> Replay() {
            if (!Current(h, a)) return await Fail(h, 403, "permission_denied");
            if (id != null && !await Visible(db, Actor(h), vessel).AnyAsync(n => n.Id == id, ct)) return await Fail(h, 404, "resource_not_found");
            var receipt = await db.NotificationReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt == null) return null;
            if (receipt.Action != action || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel) return await Fail(h, 409, "idempotency_key_reused");
            if (!Current(h, a)) return await Fail(h, 403, "permission_denied");
            return Results.Content(receipt.OutcomeJson, "application/json");
        }
        var replay = await Replay(); if (replay != null) return replay;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            object result;
            if (id != null) {
                var notice = await Visible(db, a, vessel).SingleOrDefaultAsync(n => n.Id == id, ct);
                if (notice == null) return await Fail(h, 404, "resource_not_found");
                if (notice.Version != expected) return await Fail(h, 409, "version_conflict");
                if (notice.State is not ("Open" or "Escalated")) return await Fail(h, 409, "invalid_state");
                if (!(await ConfirmedSources(h, db, vessel, [notice])).Contains(notice.Id)) return await Fail(h, 409, "input_review_required");
                notice.State = "Acknowledged"; notice.Revision++;
                db.NotificationEvents.Add(Event(notice, "Acknowledged", a.Id, h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow()));
                await db.SaveChangesAsync(ct); result = new NotificationSavedDto(notice.Id, notice.Revision, notice.Version, notice.State);
            } else {
                var p = await db.NotificationPreferences.SingleOrDefaultAsync(p => p.VesselId == vessel && p.ActorId == a.Id, ct);
                if ((p?.Version ?? 0) != expected) return await Fail(h, 409, "version_conflict");
                if (p == null) { p = new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id }; db.NotificationPreferences.Add(p); }
                else { p.Revision++; db.Entry(p).Property(x => x.MutedAdvisory).IsModified = true; }
                p.MutedAdvisory = muted; await db.SaveChangesAsync(ct);
                result = new NotificationPreferenceDto(p.MutedAdvisory, p.Revision, p.Version);
            }
            var outcome = JsonSerializer.Serialize(result, Json);
            db.NotificationReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, Action = action,
                ResourceId = id ?? a.Id, NotificationId = id, PreferenceActorId = id == null ? a.Id : null,
                Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow() });
            await db.SaveChangesAsync(ct);
            if (!Current(h, a) || id != null && !a.Permissions.Contains("notification.acknowledge")) return await Fail(h, 403, "permission_denied");
            await tx.CommitAsync(ct); return Results.Content(outcome, "application/json");
        } catch (DbUpdateConcurrencyException) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Replay() ?? await Fail(h, 409, "version_conflict");
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var conflict = PostgresFailure.IsSerialization(e) || PostgresFailure.Provider(e)?.SqlState == "23505";
            return await Replay() ?? await Fail(h, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable");
        }
    }
}
