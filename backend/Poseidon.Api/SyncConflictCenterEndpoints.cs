using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-035 / T-S4-003 (18.01 Backlog row 36, screen offline-conflict-center, /api/v1/sync/conflicts).
// Read-only material-conflict index across the existing offline sources. It lists only the CURRENT
// actor's own recorded MaterialConflict outcomes for one explicitly selected vessel in the actor's
// tenant/vessel scope, and only for sources whose ordinary read right the actor holds now. It never
// exposes captured/server field values (those stay behind each source's protected conflict detail,
// which re-checks current ownership), never resolves anything and never infers a device backlog.
// Resolution remains a new, reasoned, expected-version command through the owning source replay.
public sealed record SyncConflictItemDto(string Source, Guid ResourceId, string? ResourceKind, Guid EnrollmentId, Guid CommandId,
    uint BaseVersion, uint ServerVersionAtConflict, DateTimeOffset? CapturedAt, DateTimeOffset ReceivedAt, string State,
    Guid? ResolutionCommandId, string? ResolutionResult);
public sealed record SyncConflictSourceDto(string Source, string State);
public sealed record SyncConflictPageDto(Guid VesselId, DateTimeOffset ReadAt, SyncConflictSourceDto[] Sources,
    SyncConflictItemDto[] Items, int Offset, bool HasMore);

internal sealed record SyncConflictRow(string Source, Guid ResourceId, string? ResourceKind, Guid EnrollmentId, Guid CommandId,
    uint BaseVersion, uint ServerVersion, DateTimeOffset? CapturedAt, DateTimeOffset ReceivedAt);
internal sealed record SyncConflictSuccessor(Guid EnrollmentId, Guid ParentCommandId, Guid CommandId, string Result);

public static class SyncConflictCenterEndpoints
{
    internal const int PageSize = 25;
    internal const int MaxOffset = 1000;
    internal const string CrewSource = "crewAssignment", TugSource = "tugPlan", DailySource = "dailyRecord", WorkOrderSource = "workOrderEvidence";
    internal const string CertificateRenewalSource = "certificateRenewal";

    public static void MapSyncConflictCenter(this IEndpointRouteBuilder app) => app.MapGet("/api/v1/sync/conflicts", List)
        .RequireAuthorization("DirectoryRead").WithName("SyncConflictList").Produces<SyncConflictPageDto>()
        .Produces(400).Produces(401).Produces(403).Produces(404);

    internal static bool CrewReadable(TrustedActor a) => a.CanRead && a.Permissions.Contains("crew.read");
    internal static bool TugReadable(TrustedActor a) => a.CanReadTug;
    internal static bool DailyReadable(TrustedActor a, string kind) => DailyOperationsEndpoints.CanRead(a, kind);
    internal static bool WorkOrderReadable(TrustedActor a) => WorkOrderEndpoints.CanRead(a);
    // S10 WP-C DEV-066: own certificate renewal offline draft conflicts require the ordinary certificate read right.
    internal static bool CertificateRenewalReadable(TrustedActor a) => CertificateReader.Read(a);
    // Navigation (DEV-022 pattern): the destination is projected only when the list endpoint can return
    // at least one source for this actor, i.e. the same CanRead gate plus one currently readable source.
    internal static bool CanOpen(TrustedActor a) => a.CanRead &&
        (CrewReadable(a) || TugReadable(a) || DailyOperationsPolicy.Kinds.Any(k => DailyReadable(a, k)) || WorkOrderReadable(a) || CertificateRenewalReadable(a));

    // Closed query: exactly one vesselId (canonical "D" GUID) and an optional bounded offset. Any other
    // member (for example a forged actorId/tenantId) is rejected instead of ignored.
    internal static bool TryQuery(IQueryCollection query, out Guid vessel, out int offset)
    {
        vessel = Guid.Empty; offset = 0;
        if (query.Any(q => q.Key is not ("vesselId" or "offset") || q.Value.Count != 1)) return false;
        if (!Guid.TryParseExact(query["vesselId"], "D", out vessel) || vessel == Guid.Empty) return false;
        if (query.ContainsKey("offset") && (!int.TryParse(query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) ||
            offset > MaxOffset || offset % PageSize != 0)) return false;
        return true;
    }

    // Pure, deterministic projection: open conflicts first, then newest receipt, then a stable id order.
    // A MaterialConflict successor means the earlier conflict was superseded by a new conflict, which is
    // itself listed as Open; only a non-conflict successor (Committed / ServerRetained) is Resolved.
    internal static (SyncConflictItemDto[] Items, bool HasMore) Project(IEnumerable<SyncConflictRow> rows,
        IEnumerable<SyncConflictSuccessor> successors, int offset)
    {
        var next = successors.GroupBy(s => (s.EnrollmentId, s.ParentCommandId)).ToDictionary(g => g.Key, g => g.OrderBy(s => s.CommandId).First());
        var items = rows.Select(r => {
            next.TryGetValue((r.EnrollmentId, r.CommandId), out var s);
            var state = s == null ? "Open" : s.Result == "MaterialConflict" ? "Superseded" : "Resolved";
            return new SyncConflictItemDto(r.Source, r.ResourceId, r.ResourceKind, r.EnrollmentId, r.CommandId, r.BaseVersion, r.ServerVersion,
                r.CapturedAt, r.ReceivedAt, state, s?.CommandId, s?.Result);
        }).OrderBy(i => i.State == "Open" ? 0 : 1).ThenByDescending(i => i.ReceivedAt).ThenBy(i => i.CommandId).ToArray();
        return (items.Skip(offset).Take(PageSize).ToArray(), items.Length > offset + PageSize);
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db, ActorResolver actors, SecurityAudit audit, TimeProvider time)
    {
        async Task<IResult> Fail(int status, string result) {
            await audit.WriteAsync(h, "SyncConflictList", result); return Results.StatusCode(status);
        }
        var actor = actors.Resolve(h.User);
        if (actor == null || !actor.CanRead) return await Fail(403, "Denied");
        if (!TryQuery(h.Request.Query, out var vessel, out var offset)) return await Fail(400, "Invalid");
        if (!await actor.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, h.RequestAborted)) return await Fail(404, "NotFound");
        var ct = h.RequestAborted; var bound = offset + PageSize + 1;
        var rows = new List<SyncConflictRow>(); var successors = new List<SyncConflictSuccessor>();
        var crew = CrewReadable(actor); var tug = TugReadable(actor);
        var dailyKinds = DailyOperationsPolicy.Kinds.Where(k => DailyReadable(actor, k)).ToArray();
        // Each source is bounded independently BEFORE the merge; the bound covers every row a page can show.
        if (crew) {
            var own = db.OfflineOutcomes.AsNoTracking().Where(o => o.ActorId == actor.Id && o.VesselId == vessel);
            rows.AddRange(await own.Where(o => o.Result == "MaterialConflict")
                .OrderBy(o => own.Any(s => s.EnrollmentId == o.EnrollmentId && s.ParentCommandId == o.CommandId) ? 1 : 0)
                .ThenByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Take(bound)
                .Select(o => new SyncConflictRow(CrewSource, o.AssignmentId, null, o.EnrollmentId, o.CommandId, o.BaseVersion, o.ServerVersion, o.CapturedAt, o.ReceivedAt))
                .ToArrayAsync(ct));
            var parents = rows.Where(r => r.Source == CrewSource).Select(r => r.CommandId).ToArray();
            successors.AddRange(await own.Where(o => o.ParentCommandId != null && parents.Contains(o.ParentCommandId.Value))
                .Select(o => new SyncConflictSuccessor(o.EnrollmentId, o.ParentCommandId!.Value, o.CommandId, o.Result)).ToArrayAsync(ct));
        }
        if (tug) {
            var own = db.TugOfflineOutcomes.AsNoTracking().Where(o => o.ActorId == actor.Id && o.VesselId == vessel);
            rows.AddRange(await own.Where(o => o.Result == "MaterialConflict")
                .OrderBy(o => own.Any(s => s.EnrollmentId == o.EnrollmentId && s.ParentCommandId == o.CommandId) ? 1 : 0)
                .ThenByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Take(bound)
                .Select(o => new SyncConflictRow(TugSource, o.JobId, null, o.EnrollmentId, o.CommandId, o.BaseVersion, o.ServerVersion, o.CapturedAt, o.ReceivedAt))
                .ToArrayAsync(ct));
            var parents = rows.Where(r => r.Source == TugSource).Select(r => r.CommandId).ToArray();
            successors.AddRange(await own.Where(o => o.ParentCommandId != null && parents.Contains(o.ParentCommandId.Value))
                .Select(o => new SyncConflictSuccessor(o.EnrollmentId, o.ParentCommandId!.Value, o.CommandId, o.Result)).ToArrayAsync(ct));
        }
        if (dailyKinds.Length > 0) {
            var own = db.DailyOfflineOutcomes.AsNoTracking().Where(o => o.ActorId == actor.Id && o.VesselId == vessel && dailyKinds.Contains(o.Kind));
            // Daily captures do not persist a client capture time on the outcome; it stays null (unknown), never invented.
            rows.AddRange(await own.Where(o => o.Result == "MaterialConflict")
                .OrderBy(o => own.Any(s => s.EnrollmentId == o.EnrollmentId && s.ParentCommandId == o.CommandId) ? 1 : 0)
                .ThenByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Take(bound)
                .Select(o => new SyncConflictRow(DailySource, o.RecordId, o.Kind, o.EnrollmentId, o.CommandId, o.BaseVersion, o.ServerVersion, (DateTimeOffset?)null, o.ReceivedAt))
                .ToArrayAsync(ct));
            var parents = rows.Where(r => r.Source == DailySource).Select(r => r.CommandId).ToArray();
            successors.AddRange(await own.Where(o => o.ParentCommandId != null && parents.Contains(o.ParentCommandId.Value))
                .Select(o => new SyncConflictSuccessor(o.EnrollmentId, o.ParentCommandId!.Value, o.CommandId, o.Result)).ToArrayAsync(ct));
        }
        var workOrders = WorkOrderReadable(actor);
        if (workOrders) {
            // DEV-057: material field conflicts of offline work order evidence (own captures only).
            var own = db.WorkOrderOfflineOutcomes.AsNoTracking().Where(o => o.ActorId == actor.Id && o.VesselId == vessel);
            rows.AddRange(await own.Where(o => o.Result == "MaterialConflict")
                .OrderBy(o => own.Any(s => s.EnrollmentId == o.EnrollmentId && s.ParentCommandId == o.CommandId) ? 1 : 0)
                .ThenByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Take(bound)
                .Select(o => new SyncConflictRow(WorkOrderSource, o.WorkOrderId, null, o.EnrollmentId, o.CommandId, o.BaseVersion, o.ServerVersion, (DateTimeOffset?)o.CapturedAt, o.ReceivedAt))
                .ToArrayAsync(ct));
            var parents = rows.Where(r => r.Source == WorkOrderSource).Select(r => r.CommandId).ToArray();
            successors.AddRange(await own.Where(o => o.ParentCommandId != null && parents.Contains(o.ParentCommandId.Value))
                .Select(o => new SyncConflictSuccessor(o.EnrollmentId, o.ParentCommandId!.Value, o.CommandId, o.Result)).ToArrayAsync(ct));
        }
        var renewals = CertificateRenewalReadable(actor);
        if (renewals) {
            // S10 WP-C DEV-066: material conflicts of offline certificate renewal drafts (own captures only).
            var own = db.CertificateRenewalOfflineOutcomes.AsNoTracking().Where(o => o.ActorId == actor.Id && o.VesselId == vessel);
            rows.AddRange(await own.Where(o => o.Result == "MaterialConflict")
                .OrderBy(o => own.Any(s => s.EnrollmentId == o.EnrollmentId && s.ParentCommandId == o.CommandId) ? 1 : 0)
                .ThenByDescending(o => o.ReceivedAt).ThenBy(o => o.CommandId).Take(bound)
                .Select(o => new SyncConflictRow(CertificateRenewalSource, o.RenewalId, null, o.EnrollmentId, o.CommandId, o.BaseVersion, o.ServerVersion, (DateTimeOffset?)o.CapturedAt, o.ReceivedAt))
                .ToArrayAsync(ct));
            var parents = rows.Where(r => r.Source == CertificateRenewalSource).Select(r => r.CommandId).ToArray();
            successors.AddRange(await own.Where(o => o.ParentCommandId != null && parents.Contains(o.ParentCommandId.Value))
                .Select(o => new SyncConflictSuccessor(o.EnrollmentId, o.ParentCommandId!.Value, o.CommandId, o.Result)).ToArrayAsync(ct));
        }
        var (items, more) = Project(rows, successors, offset);
        SyncConflictSourceDto[] sources = [
            new(CrewSource, crew ? "Included" : "NotPermitted"),
            new(TugSource, tug ? "Included" : "NotPermitted"),
            new(DailySource, dailyKinds.Length == DailyOperationsPolicy.Kinds.Length ? "Included" : dailyKinds.Length > 0 ? "Partial" : "NotPermitted"),
            new(WorkOrderSource, workOrders ? "Included" : "NotPermitted"),
            new(CertificateRenewalSource, renewals ? "Included" : "NotPermitted")];
        await audit.WriteAsync(h, "SyncConflictList", "Success", resourceId: vessel);
        return Results.Ok(new SyncConflictPageDto(vessel, time.GetUtcNow(), sources, items, offset, more));
    }
}
