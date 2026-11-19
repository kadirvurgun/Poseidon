using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DailyEngineReferenceCandidateDto(DailyEngineReferencePin Pin, string? Label, string? Detail, string Provenance,
    DateTimeOffset SavedAt, string Qualification, string Href);
public sealed record DailyEngineReferenceItemDto(string Kind, DailyEngineReferencePin? Pin, long Sequence, string State,
    string Qualification, string? Label, string? Href, string? Reason);
public sealed record DailyEngineReferenceSourceDto(string Kind, string State);

// DEV-099 owning WorkOrder/Defect reference reader for the engine daily log.
// Scope: actor tenant (query filter) + vessel + per-kind read permission + the owning
// readers' nested asset/plan fences (DowntimeOriginReader). Records the actor cannot
// read are never listed and an existing link to one resolves to UNKNOWN without
// identifiers. Nothing here verifies, closes or approves a linked record (DEV-052 separate).
internal static class DailyEngineReferenceReader
{
    internal static bool CanRead(TrustedActor? actor, string kind) => DailyEngineReferencePolicy.Kinds.Contains(kind) && DowntimeOriginReader.CanRead(actor, kind);
    internal static DailyEngineReferenceSourceDto[] Sources(TrustedActor actor) =>
        DailyEngineReferencePolicy.Kinds.Select(k => new DailyEngineReferenceSourceDto(k, CanRead(actor, k) ? "AVAILABLE" : "UNAVAILABLE")).ToArray();
    internal static DowntimeOriginPin Origin(DailyEngineReferencePin pin) => new(pin.Kind, pin.Id, pin.Revision, pin.Version, pin.Hash, pin.SourceVersion);
    internal static string Href(Guid vessel, DailyEngineReferencePin pin) =>
        pin.Kind == "WorkOrder" ? $"/app/work-order-center/{vessel:D}?id={pin.Id:D}" : $"/app/defects/{vessel:D}?id={pin.Id:D}";
    internal static DailyEngineReferencePin Pin(DailyEngineReferenceEvent e) => new(e.TargetKind,
        (e.TargetKind == "WorkOrder" ? e.WorkOrderId : e.DefectId)!.Value, (e.TargetKind == "WorkOrder" ? e.WorkOrderRevision : e.DefectRevision)!.Value,
        checked((uint)e.TargetVersion), e.TargetHash, e.TargetSourceVersion);

    // Exact pinned revision label; the owning record's own declared number, never a guess.
    private static string? Text(string? value) => value is { Length: > 0 and <= 200 } && !value.Any(char.IsControl) ? value : null;
    private static async Task<(string? Label, string? Detail)?> Label(PoseidonDbContext db, Guid vessel, DailyEngineReferencePin pin, CancellationToken ct) {
        if (pin.Kind == "WorkOrder") {
            var json = await db.WorkOrderRevisions.AsNoTracking().Where(r => r.VesselId == vessel && r.WorkOrderId == pin.Id && r.Revision == pin.Revision &&
                r.RecordVersion == pin.Version && r.Hash == pin.Hash && r.SourceVersion == pin.SourceVersion).Select(r => r.FieldsJson).SingleOrDefaultAsync(ct);
            if (json == null) return null;
            var f = WorkOrderPolicy.ReadFields(json);
            return (Text(f.GetValueOrDefault("work_order_no")?.Value), Text(f.GetValueOrDefault("execution_stage")?.Value));
        } else {
            var json = await db.DefectRevisions.AsNoTracking().Where(r => r.VesselId == vessel && r.DefectId == pin.Id && r.Revision == pin.Revision &&
                r.RecordVersion == pin.Version && r.Hash == pin.Hash && r.SourceVersion == pin.SourceVersion).Select(r => r.FieldsJson).SingleOrDefaultAsync(ct);
            if (json == null) return null;
            var f = DefectPolicy.ReadFields(json);
            return (Text(f.GetValueOrDefault("defect_no")?.Value), Text(f.GetValueOrDefault("criticality")?.Value));
        }
    }

    // Current owning heads in this vessel the actor can read, newest first. Bounded page.
    internal static async Task<(DailyEngineReferenceCandidateDto[] Items, bool HasMore)> Candidates(PoseidonDbContext db, TrustedActor actor, Guid vessel,
        string kind, int offset, CancellationToken ct) {
        if (!CanRead(actor, kind)) return ([], false);
        var heads = kind == "WorkOrder"
            ? await db.WorkOrders.AsNoTracking().Where(r => r.VesselId == vessel && r.SourceVersion == WorkOrderPolicy.SourceVersion)
                .OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id).Skip(offset).Take(26)
                .Select(r => new { r.Id, r.Revision, r.Version, r.Hash, r.SourceVersion, r.Provenance, r.SavedAt }).ToArrayAsync(ct)
            : await db.Defects.AsNoTracking().Where(r => r.VesselId == vessel && r.SourceVersion == DefectPolicy.SourceVersion)
                .OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id).Skip(offset).Take(26)
                .Select(r => new { r.Id, r.Revision, r.Version, r.Hash, r.SourceVersion, r.Provenance, r.SavedAt }).ToArrayAsync(ct);
        var items = new List<DailyEngineReferenceCandidateDto>();
        foreach (var h in heads.Take(25)) {
            var pin = new DailyEngineReferencePin(kind, h.Id, h.Revision, h.Version, h.Hash, h.SourceVersion);
            // Each candidate must pass the owning nested fence for this actor; otherwise it is omitted.
            if (!DailyEngineReferencePolicy.Valid(pin) || await DowntimeOriginReader.CaptureFence(db, actor, vessel, [Origin(pin)], true, ct) == null) continue;
            var label = await Label(db, vessel, pin, ct); if (label == null) continue;
            items.Add(new(pin, label.Value.Label, label.Value.Detail, h.Provenance, h.SavedAt, "REVIEW_REQUIRED", Href(vessel, pin)));
        }
        return (items.ToArray(), heads.Length > 25);
    }

    // Resolve each active link against the actor's current read scope and owning head.
    internal static async Task<DailyEngineReferenceItemDto[]> Resolve(PoseidonDbContext db, TrustedActor actor, Guid vessel,
        DailyEngineReferenceLink[] links, CancellationToken ct) {
        var result = new List<DailyEngineReferenceItemDto>();
        foreach (var link in links) {
            var pin = link.Pin;
            if (!CanRead(actor, pin.Kind)) { result.Add(new(pin.Kind, null, link.Sequence, "UNKNOWN", "REVIEW_REQUIRED", null, null, "REFERENCE_NOT_READABLE")); continue; }
            if (!DailyEngineReferencePolicy.Valid(pin) || await DowntimeOriginReader.CaptureFence(db, actor, vessel, [Origin(pin)], false, ct) == null) {
                result.Add(new(pin.Kind, null, link.Sequence, "UNKNOWN", "REVIEW_REQUIRED", null, null, "REFERENCE_UNAVAILABLE")); continue;
            }
            var label = await Label(db, vessel, pin, ct);
            if (label == null) { result.Add(new(pin.Kind, null, link.Sequence, "UNKNOWN", "REVIEW_REQUIRED", null, null, "REFERENCE_UNAVAILABLE")); continue; }
            var current = pin.Kind == "WorkOrder"
                ? await db.WorkOrders.AsNoTracking().AnyAsync(h => h.VesselId == vessel && h.Id == pin.Id && h.Revision == pin.Revision && h.Version == pin.Version && h.Hash == pin.Hash, ct)
                : await db.Defects.AsNoTracking().AnyAsync(h => h.VesselId == vessel && h.Id == pin.Id && h.Revision == pin.Revision && h.Version == pin.Version && h.Hash == pin.Hash, ct);
            result.Add(new(pin.Kind, pin, link.Sequence, current ? "LINKED_CURRENT" : "LINKED_SUPERSEDED", "REVIEW_REQUIRED",
                label.Value.Label, Href(vessel, pin), current ? null : "REFERENCE_CHANGED_SINCE_LINK"));
        }
        return result.ToArray();
    }

    // One composed fence for every disclosed pin (readable kinds only).
    internal static async Task<Expression<Func<Vessel, bool>>?> Fence(PoseidonDbContext db, TrustedActor actor, Guid vessel,
        IEnumerable<DailyEngineReferencePin> pins, bool currentOnly, CancellationToken ct) =>
        await DowntimeOriginReader.CaptureFence(db, actor, vessel, pins.Distinct().Select(Origin).ToArray(), currentOnly, ct);
}
