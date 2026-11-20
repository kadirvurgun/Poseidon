using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record PmsCommandCenterDto(Guid VesselId, string ReadinessState, DateTimeOffset LastRefreshAt, PmsSection[] Sections, string Advisory);

// Read-only inputs owned by other modules. WorkOrder state (S8 WP-B) and spare stock / critical
// spare gaps (S8 WP-B) are wired through PmsCommandCenterSources.cs; the Unavailable* fallbacks
// answer UNKNOWN, which the command center shows as UNKNOWN and never as zero or healthy.
public sealed record PmsSourceContext(PoseidonDbContext Db, TrustedActor Actor, Guid VesselId, DateTimeOffset Now, CancellationToken Ct);
public sealed record PmsSourceResult(bool Available, string? Reason, PmsCommandItem[] Items, int QuietCount, DateTimeOffset? AsOf, bool AccessDenied = false)
{
    public static PmsSourceResult Unavailable(string reason) => new(false, reason, [], 0, null);
    public static PmsSourceResult Denied(string reason) => new(false, reason, [], 0, null, true);
}
public interface IPmsWorkOrderStateSource { Task<PmsSourceResult> OpenWorkOrdersAsync(PmsSourceContext context); }
public interface IPmsSpareGapSource { Task<PmsSourceResult> CriticalSpareGapsAsync(PmsSourceContext context); }
internal sealed class UnavailablePmsWorkOrderStateSource : IPmsWorkOrderStateSource
{
    public Task<PmsSourceResult> OpenWorkOrdersAsync(PmsSourceContext context) =>
        Task.FromResult(PmsSourceResult.Unavailable("WORK_ORDER_STATE_SOURCE_UNAVAILABLE"));
}
internal sealed class UnavailablePmsSpareGapSource : IPmsSpareGapSource
{
    public Task<PmsSourceResult> CriticalSpareGapsAsync(PmsSourceContext context) =>
        Task.FromResult(PmsSourceResult.Unavailable("SPARE_GAP_SOURCE_UNAVAILABLE"));
}

// DEV-056 / T-S8-005: one exception view per vessel. Every section is gated by its owning module's
// read predicate; a forbidden section is ACCESS_REQUIRED, a missing source UNKNOWN.
public static class PmsCommandCenterEndpoints
{
    private const string ReadAction = "PmsCommandCenterRead";
    private const int DefectLimit = 500;
    internal static bool CanRead(TrustedActor? a) => a?.CanRead == true && a.Permissions.Contains("equipment.read");

    public static void MapPmsCommandCenter(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/v1/pms").RequireAuthorization("DirectoryRead")
            .MapGet("/command-center", (HttpContext h, PoseidonDbContext db) => Read(h, db)).WithName("PmsCommandCenterRead").Produces<PmsCommandCenterDto>();

    private static PmsSection FromSource(string key, PmsSourceResult r) => r.Available
        ? PmsCommandCenterPolicy.Section(key, r.Items, r.QuietCount, r.AsOf)
        : PmsCommandCenterPolicy.Unavailable(key, r.Reason ?? "SOURCE_UNAVAILABLE", r.AccessDenied);

    private static async Task<IResult> Read(HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead); if (failure != null) return failure;
        var now = TechnicalRecordCommands.Now(h);
        var (ordered, _) = await Compose(h, db, actor!, vessel, now, h.RequestAborted);
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", vessel);
        return Results.Json(new PmsCommandCenterDto(vessel, PmsCommandCenterPolicy.Readiness(ordered), now, ordered, "ADVISORY_EXCEPTION_VIEW_NOT_A_FITNESS_DECISION"),
            TechnicalRecordCommands.Json);
    }

    // S9 DEV-058: the same exception sections are the technical input of the departure gate. The caller has
    // already established vessel scope and CanRead; every section keeps its owning module's read predicate.
    internal static async Task<(PmsSection[] Sections, MaintenanceCurrentProjection[]? Plans)> Compose(HttpContext h, PoseidonDbContext db, TrustedActor actor,
        Guid vessel, DateTimeOffset now, CancellationToken ct)
    {
        var sections = new List<PmsSection>();
        MaintenanceCurrentProjection[]? plans = null;

        // Maintenance plans: overdue / critical overdue; healthy (NotDue) rows stay quiet.
        if (MaintenanceCurrentReader.Permitted(actor!))
        {
            var rows = plans = await MaintenanceCurrentReader.Read(vessel, h, db, actor!, now);
            if (rows == null) { sections.Add(PmsCommandCenterPolicy.Unavailable("critical_overdue_jobs", "MAINTENANCE_SOURCE_UNAVAILABLE")); sections.Add(PmsCommandCenterPolicy.Unavailable("overdue_jobs", "MAINTENANCE_SOURCE_UNAVAILABLE")); }
            else
            {
                var critical = new List<PmsCommandItem>(); var other = new List<PmsCommandItem>(); var quietCritical = 0; var quiet = 0;
                foreach (var p in rows)
                {
                    var inputs = p.Row.Inputs;
                    var classified = PmsCommandCenterPolicy.Classify(p.Row.Id, p.Row.Revision, p.Row.AssetId, p.Row.Due, inputs.Criticality, p.Row.AssetCriticality,
                        inputs.TimeZone, p.SourceObservedAt, now);
                    if (classified == null) { if (PmsCommandCenterPolicy.IsCritical(inputs.Criticality, p.Row.AssetCriticality)) quietCritical++; else quiet++; continue; }
                    (classified.Value.Section == "critical_overdue_jobs" ? critical : other).Add(classified.Value.Item);
                }
                sections.Add(PmsCommandCenterPolicy.Section("critical_overdue_jobs", critical, quietCritical, now));
                sections.Add(PmsCommandCenterPolicy.Section("overdue_jobs", other, quiet, now));
            }
        }
        else
        {
            sections.Add(PmsCommandCenterPolicy.Unavailable("critical_overdue_jobs", "MAINTENANCE_ACCESS_REQUIRED", true));
            sections.Add(PmsCommandCenterPolicy.Unavailable("overdue_jobs", "MAINTENANCE_ACCESS_REQUIRED", true));
        }

        var context = new PmsSourceContext(db, actor!, vessel, now, ct);
        sections.Add(FromSource("open_work_orders", WorkOrderEndpoints.CanRead(actor)
            ? await h.RequestServices.GetRequiredService<IPmsWorkOrderStateSource>().OpenWorkOrdersAsync(context) : PmsSourceResult.Denied("WORK_ORDER_ACCESS_REQUIRED")));

        // Condition alerts: open critical defects, expired temporary measures, unassessed criticality.
        if (!DefectEndpoints.CanRead(actor)) sections.Add(PmsCommandCenterPolicy.Unavailable("condition_alerts", "DEFECT_ACCESS_REQUIRED", true));
        else
        {
            var defects = await db.Defects.AsNoTracking().Where(d => d.VesselId == vessel).OrderBy(d => d.Id).Take(DefectLimit + 1)
                .Select(d => new { d.Id, d.Revision, d.FieldsJson, d.SavedAt }).ToArrayAsync(ct);
            if (defects.Length > DefectLimit) sections.Add(PmsCommandCenterPolicy.Unavailable("condition_alerts", "DEFECT_PROJECTION_LIMIT_EXCEEDED"));
            else
            {
                var ids = defects.Select(d => d.Id).ToArray();
                var heads = (await db.DefectLifecycles.AsNoTracking().Where(l => l.VesselId == vessel && ids.Contains(l.DefectId)).Select(l => l.DefectId).ToArrayAsync(ct)).ToHashSet();
                var records = (await db.DefectLifecycleRecords.AsNoTracking().Where(r => r.VesselId == vessel && ids.Contains(r.DefectId)).ToArrayAsync(ct))
                    .GroupBy(r => r.DefectId).ToDictionary(g => g.Key, g => g.Select(r => new DefectLifecycleEntry(r.Id, r.Sequence, r.Kind, r.StageAfter, r.DefectRevision,
                        r.Criticality, r.MeasureKind, r.Temporary, r.ExpiresAt, r.MeasureId, r.Outcome, r.ActorId, r.RecordedAt)).ToArray());
                var items = new List<PmsCommandItem>(); var quiet = 0;
                foreach (var d in defects)
                {
                    string? declared;
                    try { declared = DefectPolicy.ReadFields(d.FieldsJson).TryGetValue("criticality", out var f) && f.State == "Known" ? f.Value : null; }
                    catch (InvalidDataException) { items.Add(new("Defect", d.Id, d.Revision, "REVIEW", [new("DEFECT_FIELDS_UNAVAILABLE")], d.SavedAt, PmsCommandCenterPolicy.FreshnessUnknown)); continue; }
                    var lifecycle = heads.Contains(d.Id) ? DefectLifecyclePolicy.Evaluate(records.GetValueOrDefault(d.Id) ?? [], declared, d.Revision, now) : null;
                    var closedAt = records.GetValueOrDefault(d.Id)?.LastOrDefault(r => r.Kind == "CLOSE");
                    var item = lifecycle?.Stage == DefectLifecyclePolicy.Closed && closedAt != null && closedAt.DefectRevision != d.Revision
                        ? new PmsCommandItem("Defect", d.Id, d.Revision, "REVIEW", [new("DECLARATION_CHANGED_AFTER_CLOSURE")], d.SavedAt, PmsCommandCenterPolicy.Current)
                        : PmsCommandCenterPolicy.DefectAlert(d.Id, d.Revision, lifecycle, declared, d.SavedAt);
                    if (item == null) quiet++; else items.Add(item);
                }
                sections.Add(PmsCommandCenterPolicy.Section("condition_alerts", items, quiet, now));
            }
        }

        sections.Add(FromSource("critical_spare_gaps", await h.RequestServices.GetRequiredService<IPmsSpareGapSource>().CriticalSpareGapsAsync(context)));
        return (PmsCommandCenterPolicy.SectionKeys.Select(k => sections.Single(s => s.Key == k)).ToArray(), plans);
    }
}
