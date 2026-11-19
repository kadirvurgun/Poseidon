using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DailyEngineReferencesDto(Guid RecordId, Guid VesselId, long RecordRevision, uint RecordVersion, uint ReferenceVersion,
    long Sequence, bool CanLink, DailyEngineReferenceSourceDto[] Sources, DailyEngineReferenceItemDto[] Items,
    string Offline, string SourceVersion = DailyEngineReferencePolicy.SourceVersion);
public sealed record DailyEngineReferenceCandidatePageDto(Guid VesselId, string Kind, DailyEngineReferenceCandidateDto[] Items, int Offset, int Limit, bool HasMore);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DailyEngineReferenceCommand([property: JsonRequired] uint ExpectedRecordVersion, [property: JsonRequired] long RecordRevision,
    [property: JsonRequired] uint ExpectedReferenceVersion, [property: JsonRequired] string Operation, [property: JsonRequired] DailyEngineReferencePin Target);
public sealed record DailyEngineReferenceEventDto(Guid Id, Guid RecordId, Guid VesselId, long Sequence, uint ReferenceVersion, long RecordRevision,
    string Operation, DailyEngineReferencePin Target, Guid ActorId, DateTimeOffset SavedAt, string Qualification = "REVIEW_REQUIRED",
    string SourceVersion = DailyEngineReferencePolicy.SourceVersion);

// DEV-099: engine daily log WO/defect links. Online-only; the existing offline draft
// path is untouched and never carries references. Links are lineage, not approval.
public static class DailyEngineReferenceEndpoints
{
    public static void MapDailyEngineReferences(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/v1/engine-daily-logs").RequireAuthorization("DirectoryRead");
        group.MapGet("/reference-candidates", (HttpContext h, PoseidonDbContext db) => Candidates(h, db))
            .WithName("EngineDailyReferenceCandidates").Produces<DailyEngineReferenceCandidatePageDto>();
        group.MapGet("/{id:guid}/references", (Guid id, HttpContext h, PoseidonDbContext db) => Read(id, h, db))
            .WithName("EngineDailyReferences").Produces<DailyEngineReferencesDto>();
        group.MapPost("/{id:guid}/references", (Guid id, HttpContext h, PoseidonDbContext db) => Save(id, h, db))
            .WithName("EngineDailyReferenceRecord").Accepts<DailyEngineReferenceCommand>("application/json").Produces<DailyEngineReferenceEventDto>();
    }
    private const string Kind = "Engine";
    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static Task Audit(HttpContext h, string result, Guid? id = null) => h.RequestServices.GetRequiredService<SecurityAudit>()
        .WriteAsync(h, h.Request.Method == "GET" ? "DailyEngineReferenceRead" : "DailyEngineReferenceCommand", result, resourceId: id);
    private static async Task<IResult> Fail(HttpContext h, int status, string code) { await Audit(h, code); return Results.Json(new { code }, statusCode: status); }
    private static IQueryable<Vessel> Vessels(PoseidonDbContext db, TrustedActor actor, Guid vessel) =>
        actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel && (v.Kind == VesselKind.Ship || actor.Permissions.Contains("mode.cross")));
    private static bool Query(HttpContext h, string[] allowed, out Guid vessel) {
        vessel = Guid.Empty;
        return h.Request.Query.All(q => q.Value.Count == 1 && allowed.Contains(q.Key)) &&
            Guid.TryParseExact(h.Request.Query["vesselId"], "D", out vessel) && vessel != Guid.Empty;
    }
    // Live fresh-scope recheck of actor rights, engine read/write, vessel and every disclosed pin.
    private static async Task<bool> Current(HttpContext h, Guid vessel, string rights, DailyEngineReferencePin[] pins, bool write = false) {
        if (!await DailyOperationsEndpoints.Current(h, Kind, vessel, rights, write)) return false;
        await using var scope = h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var actor = scope.ServiceProvider.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (actor == null || DailyOperationsEndpoints.Rights(actor) != rights) return false;
        var db = scope.ServiceProvider.GetRequiredService<PoseidonDbContext>(); if (db.TenantId != actor.TenantId) return false;
        var readable = pins.Where(p => DailyEngineReferenceReader.CanRead(actor, p.Kind)).ToArray();
        var fence = await DailyEngineReferenceReader.Fence(db, actor, vessel, readable, false, h.RequestAborted);
        return fence != null && await Vessels(db, actor, vessel).Where(fence).AnyAsync(h.RequestAborted);
    }
    private static async Task<(DailyEngineReferenceLink[] Links, DailyEngineReferenceHead? Head)> Links(PoseidonDbContext db, Guid vessel, Guid id, CancellationToken ct, bool tracked = false) {
        var heads = tracked ? db.DailyEngineReferenceHeads : db.DailyEngineReferenceHeads.AsNoTracking();
        var head = await heads.SingleOrDefaultAsync(r => r.VesselId == vessel && r.Kind == Kind && r.RecordId == id, ct);
        if (head == null) return ([], null);
        var events = await db.DailyEngineReferenceEvents.AsNoTracking().Where(e => e.VesselId == vessel && e.Kind == Kind && e.RecordId == id).ToArrayAsync(ct);
        return (DailyEngineReferencePolicy.Active(events.Select(e => (e.Operation, DailyEngineReferenceReader.Pin(e), e.Sequence))), head);
    }
    private static bool CanLink(TrustedActor actor, Guid vessel, VesselKind kind) => kind == VesselKind.Ship && DailyOperationsEndpoints.CanWrite(actor, Kind, vessel) &&
        DailyEngineReferencePolicy.Kinds.Any(k => DailyEngineReferenceReader.CanRead(actor, k));

    private static async Task<IResult> Candidates(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store"; var offset = 0;
        if (!Query(h, ["vesselId", "kind", "offset"], out var vessel) || !DailyEngineReferencePolicy.Kinds.Contains(h.Request.Query["kind"].ToString()) ||
            h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 100000))
            return await Fail(h, 400, "invalid_query");
        var kind = h.Request.Query["kind"].ToString();
        var a = Actor(h); if (a == null || !DailyOperationsEndpoints.CanRead(a, Kind) || !DailyEngineReferenceReader.CanRead(a, kind)) return await Fail(h, 403, "permission_denied");
        var rights = DailyOperationsEndpoints.Rights(a);
        if (!await Vessels(db, a, vessel).AnyAsync(h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var (items, more) = await DailyEngineReferenceReader.Candidates(db, a, vessel, kind, offset, h.RequestAborted);
        var pins = items.Select(i => i.Pin).ToArray();
        if (!await Current(h, vessel, rights, pins)) return await Fail(h, 403, "permission_denied");
        await Audit(h, "Success", vessel);
        if (!await Current(h, vessel, rights, pins)) return await Fail(h, 403, "permission_denied");
        return Results.Ok(new DailyEngineReferenceCandidatePageDto(vessel, kind, items, offset, 25, more));
    }

    private static async Task<IResult> Read(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        if (!Query(h, ["vesselId"], out var vessel)) return await Fail(h, 400, "invalid_query");
        var a = Actor(h); if (a == null || !DailyOperationsEndpoints.CanRead(a, Kind)) return await Fail(h, 403, "permission_denied");
        var rights = DailyOperationsEndpoints.Rights(a); var ct = h.RequestAborted;
        var vesselKind = await Vessels(db, a, vessel).Select(v => (VesselKind?)v.Kind).SingleOrDefaultAsync(ct);
        if (vesselKind == null) return await Fail(h, 404, "resource_not_found");
        var record = await db.DailyOperations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.VesselId == vessel && r.Kind == Kind, ct);
        if (record == null) return await Fail(h, 404, "resource_not_found");
        var (links, head) = await Links(db, vessel, id, ct);
        var items = await DailyEngineReferenceReader.Resolve(db, a, vessel, links, ct);
        var disclosed = items.Where(i => i.Pin != null).Select(i => i.Pin!).ToArray();
        var dto = new DailyEngineReferencesDto(id, vessel, record.Revision, record.Version, head?.Version ?? 0, head?.Sequence ?? 0,
            CanLink(a, vessel, vesselKind.Value), DailyEngineReferenceReader.Sources(a), items, "ONLINE_ONLY");
        if (!await Current(h, vessel, rights, disclosed)) return await Fail(h, 403, "permission_denied");
        await Audit(h, "Success", id);
        if (!await Current(h, vessel, rights, disclosed)) return await Fail(h, 403, "permission_denied");
        return Results.Ok(dto);
    }

    private static async Task<IResult> Save(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = Actor(h);
        if (a == null || !DailyOperationsEndpoints.CanRead(a, Kind) || !a.HasWriteAttribution || !a.Permissions.Contains("daily.engine.write")) return await Fail(h, 403, "permission_denied");
        if (!Query(h, ["vesselId"], out var vessel)) return await Fail(h, 400, "invalid_query");
        if (!DailyOperationsEndpoints.CanWrite(a, Kind, vessel)) return await Fail(h, 403, "permission_denied");
        var rights = DailyOperationsEndpoints.Rights(a);
        if (await Vessels(db, a, vessel).Select(v => (VesselKind?)v.Kind).SingleOrDefaultAsync(ct) != VesselKind.Ship) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(h, 400, "invalid_idempotency_key");
        DailyEngineReferenceCommand command;
        try {
            if (!h.Request.HasJsonContentType()) throw new JsonException();
            using var buffer = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 8192, ct);
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 4 }); ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command = document.RootElement.Deserialize<DailyEngineReferenceCommand>(DailyOperationsEndpoints.Json) ?? throw new JsonException();
            if (command.RecordRevision <= 0 || command.ExpectedRecordVersion == 0 || !DailyEngineReferencePolicy.Operations.Contains(command.Operation) ||
                !DailyEngineReferencePolicy.Valid(command.Target)) throw new JsonException();
        } catch (Exception e) when (e is JsonException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        // Referencing a kind the actor cannot read is denied, including on replay after revocation.
        if (!DailyEngineReferenceReader.CanRead(a, command.Target.Kind)) return await Fail(h, 403, "permission_denied");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { vessel, id, command }, DailyOperationsEndpoints.Json));
        async Task<IResult?> Replay() {
            if (!await Current(h, vessel, rights, [], true)) return await Fail(h, 403, "permission_denied");
            var receipt = await db.DailyEngineReferenceReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt == null) return null;
            if (receipt.Fingerprint != fingerprint || receipt.VesselId != vessel || receipt.RecordId != id) return await Fail(h, 409, "idempotency_key_reused");
            await Audit(h, "Success", id);
            if (!await Current(h, vessel, rights, [], true)) return await Fail(h, 503, "command_outcome_withheld");
            return Results.Content(receipt.OutcomeJson, "application/json");
        }
        var replay = await Replay(); if (replay != null) return replay;
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null; string? committed = null;
        try {
            tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var record = await db.DailyOperations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.VesselId == vessel && r.Kind == Kind, ct);
            if (record == null) return await Fail(h, 404, "resource_not_found");
            if (record.SourceVersion != DailyOperationsPolicy.SourceVersion) return await Fail(h, 409, "source_contract_changed");
            if (record.Version != command.ExpectedRecordVersion || record.Revision != command.RecordRevision) return await Replay() ?? await Fail(h, 409, "version_conflict");
            var (links, head) = await Links(db, vessel, id, ct, tracked: true);
            if ((head?.Version ?? 0) != command.ExpectedReferenceVersion) return await Replay() ?? await Fail(h, 409, "version_conflict");
            var transition = DailyEngineReferencePolicy.Transition(links, command.Operation, command.Target);
            if (transition != null) return await Fail(h, transition == "invalid_request" ? 400 : 409, transition);
            var target = command.Target;
            if (command.Operation == "LINK") {
                // Link only the owning record's exact current revision that this actor can read.
                var exists = target.Kind == "WorkOrder"
                    ? await db.WorkOrders.AsNoTracking().Where(r => r.VesselId == vessel && r.Id == target.Id).Select(r => new { r.Revision, r.Version, r.Hash }).SingleOrDefaultAsync(ct)
                    : await db.Defects.AsNoTracking().Where(r => r.VesselId == vessel && r.Id == target.Id).Select(r => new { r.Revision, r.Version, r.Hash }).SingleOrDefaultAsync(ct);
                if (exists == null) return await Fail(h, 400, "linked_resource_unavailable");
                if (exists.Revision != target.Revision || exists.Version != target.Version || exists.Hash != target.Hash) return await Fail(h, 409, "linked_version_conflict");
                var fence = await DailyEngineReferenceReader.Fence(db, a, vessel, [target], true, ct);
                if (fence == null || !await Vessels(db, a, vessel).Where(fence).AnyAsync(ct)) return await Fail(h, 400, "linked_resource_unavailable");
            }
            if (head == null) { head = new() { TenantId = a.TenantId, VesselId = vessel, RecordId = id }; db.DailyEngineReferenceHeads.Add(head); }
            else head.Sequence = checked(head.Sequence + 1);
            await db.SaveChangesAsync(ct);
            var row = new DailyEngineReferenceEvent { TenantId = a.TenantId, VesselId = vessel, RecordId = id, Id = Guid.NewGuid(), Sequence = head.Sequence,
                RecordRevision = record.Revision, RecordVersion = record.Version, Operation = command.Operation, TargetKind = target.Kind,
                WorkOrderId = target.Kind == "WorkOrder" ? target.Id : null, WorkOrderRevision = target.Kind == "WorkOrder" ? target.Revision : null,
                DefectId = target.Kind == "Defect" ? target.Id : null, DefectRevision = target.Kind == "Defect" ? target.Revision : null,
                TargetVersion = target.Version, TargetHash = target.Hash, TargetSourceVersion = target.SourceVersion,
                SourceVersion = DailyEngineReferencePolicy.SourceVersion, ActorId = a.Id, SavedAt = now };
            db.DailyEngineReferenceEvents.Add(row); await db.SaveChangesAsync(ct);
            var outcome = JsonSerializer.Serialize(new DailyEngineReferenceEventDto(row.Id, id, vessel, row.Sequence, head.Version, record.Revision,
                row.Operation, target, a.Id, now), DailyOperationsEndpoints.Json);
            db.DailyEngineReferenceReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, RecordId = id, EventId = row.Id,
                Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = now });
            await db.SaveChangesAsync(ct);
            if (!await Current(h, vessel, rights, command.Operation == "LINK" ? [target] : [], true)) return await Fail(h, 403, "permission_denied");
            await tx.CommitAsync(ct); committed = outcome;
        } catch (DbUpdateConcurrencyException) {
            if (tx != null) await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await Replay() ?? await Fail(h, 409, "version_conflict");
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            if (tx != null) await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var conflict = PostgresFailure.IsSerialization(e) || PostgresFailure.Provider(e)?.SqlState == "23505";
            return await Replay() ?? await Fail(h, conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable");
        } finally { if (tx != null) await tx.DisposeAsync(); }
        // Committed: a later audit/fence failure is an unknown outcome, never a rejection.
        await Audit(h, "Success", id);
        if (!await Current(h, vessel, rights, [], true)) return await Fail(h, 503, "command_outcome_withheld");
        return Results.Content(committed!, "application/json");
    }
}
