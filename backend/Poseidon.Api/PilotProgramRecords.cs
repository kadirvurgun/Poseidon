using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Operator configuration (deployment), never a product default: without an approved retention period and its policy
// reference pilot feedback free text is not collected (security baseline "Saklama süreleri veri sınıfına göre
// kontrollü policy"; privacy by default).
public sealed class PilotProgramSettings
{
    public int? FeedbackRetentionDays { get; set; }
    public string? RetentionPolicyRef { get; set; }
    internal bool Configured => FeedbackRetentionDays is >= 1 and <= 400 && RetentionPolicyRef is { Length: > 0 and <= 128 } &&
        RetentionPolicyRef.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or ':' or '/');
}

internal static class PilotProgramAccess
{
    private static bool Tenant(TrustedActor? a) => a != null && a.CanRead && (a.TenantWideRead || a.TenantWideManage);
    internal static bool Read(TrustedActor? a) => Tenant(a) && a!.Permissions.Contains("pilot.program.read");
    private static bool Write(TrustedActor? a, string permission) => Read(a) && a!.HasWriteAttribution && a.Permissions.Contains(permission);
    internal static bool Manage(TrustedActor? a) => Write(a, "pilot.program.manage");
    internal static bool Record(TrustedActor? a) => Write(a, "pilot.program.record");
    internal static bool Triage(TrustedActor? a) => Write(a, "pilot.program.triage");
    // Necessary, never sufficient: an exact current Product Owner appointment (Membership.PilotGateAuthorities) is also required.
    internal static bool Decide(TrustedActor? a) => Write(a, "pilot.program.decide");

    // Same appointment rule as the DEV-075 pilot gate (PilotAccess.Resolve), gated by the pilot program permission.
    internal static PilotGateAuthority[] Resolve(FederationSettings settings, TrustedActor? actor, DateTimeOffset now) {
        if (actor == null || !Decide(actor) || actor.RoleContext == null || !PilotAccess.Human(settings, actor)) return [];
        var grants = settings.Memberships.Single(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).PilotGateAuthorities ?? [];
        if (grants.Length > 64) return [];
        var roles = actor.RoleContext.Split(',');
        return grants.Where(g => g != null && g.Id != Guid.Empty && grants.Count(o => o != null && o.Id == g.Id) == 1 &&
            string.Equals(g.Duty, PilotProgramPolicy.ProductOwnerDuty, StringComparison.Ordinal) && g.RoleId != Guid.Empty && roles.Contains(g.RoleId.ToString("D")) &&
            g.AppointmentSource is { Length: > 0 and <= 200 } source && source.Trim() == source && !source.Any(char.IsControl) &&
            g.ValidUntil > g.ValidFrom && g.ValidFrom <= now && now < g.ValidUntil).OrderBy(g => g.Id).ToArray();
    }
}

// Server-side probe of a linked record: exists in this tenant (query filters + explicit tenant), carries the record
// type the item allows, its vessel (or tug) binding and provenance. The audit trace is the existence of an attributed
// user audit event for the record, checked once at link time (audit events are immutable).
internal static class PilotRecordProbes
{
    private sealed record Spec(Type Entity, string IdProperty, string? TraceProperty);
    private static readonly Dictionary<string, Spec> Specs = new(StringComparer.Ordinal) {
        [PilotProgramCatalog.WorkOrder] = new(typeof(WorkOrder), "Id", null),
        [PilotProgramCatalog.Defect] = new(typeof(Defect), "Id", null),
        [PilotProgramCatalog.DepartureDecision] = new(typeof(DepartureDecision), "Id", null),
        [PilotProgramCatalog.OfflineOutcome] = new(typeof(OfflineOutcome), "CommandId", "AssignmentId"),
        [PilotProgramCatalog.WorkOrderOfflineOutcome] = new(typeof(WorkOrderOfflineOutcome), "CommandId", "WorkOrderId"),
        [PilotProgramCatalog.DailyOfflineOutcome] = new(typeof(DailyOfflineOutcome), "CommandId", "RecordId"),
        [PilotProgramCatalog.SafetyEventOfflineOutcome] = new(typeof(SafetyEventOfflineOutcome), "CommandId", "EventId"),
        [PilotProgramCatalog.TugOfflineOutcome] = new(typeof(TugOfflineOutcome), "CommandId", "JobId"),
        [PilotProgramCatalog.SafetyEvent] = new(typeof(SafetyEvent), "Id", null),
        [PilotProgramCatalog.RiskAssessment] = new(typeof(RiskAssessment), "Id", null),
        [PilotProgramCatalog.AuditPack] = new(typeof(AuditPackManifest), "Id", null),
        [PilotProgramCatalog.TowageExecution] = new(typeof(TowageExecution), "Id", null),
        [PilotProgramCatalog.TowagePlan] = new(typeof(TowagePlan), "Id", null),
        [PilotProgramCatalog.TowageBriefing] = new(typeof(TowageBriefing), "Id", null),
        [PilotProgramCatalog.TugJob] = new(typeof(TugJob), "Id", null),
        [PilotProgramCatalog.TugDailyLog] = new(typeof(TugDailyLog), "Id", null),
        [PilotProgramCatalog.VesselCertificate] = new(typeof(VesselCertificate), "Id", null),
        [PilotProgramCatalog.CrewAssignment] = new(typeof(CrewAssignment), "Id", null),
        [PilotProgramCatalog.ControlEvidence] = new(typeof(ControlEvidenceHead), "Id", null),
        [PilotProgramCatalog.VoiceEventDraft] = new(typeof(VoiceEventDraft), "Id", null),
    };
    internal static IReadOnlyCollection<string> OperationalTypes => Specs.Keys;
    internal static Type? EntityType(string recordType) => Specs.TryGetValue(recordType, out var s) ? s.Entity : null;

    internal static Task<PilotRecordProbe?> Probe(PoseidonDbContext db, string recordType, Guid id, bool checkAudit, CancellationToken ct) => recordType switch {
        PilotProgramCatalog.UatRun => Uat(db, id, ct),
        PilotProgramCatalog.PilotFeedback => Feedback(db, id, ct),
        PilotProgramCatalog.PilotMeasurement => Measurement(db, id, ct),
        _ when Specs.TryGetValue(recordType, out var spec) => (Task<PilotRecordProbe?>)typeof(PilotRecordProbes).GetMethod(nameof(Operational),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.MakeGenericMethod(spec.Entity)
            .Invoke(null, [db, id, spec.IdProperty, spec.TraceProperty, checkAudit, ct])!,
        _ => Task.FromResult<PilotRecordProbe?>(null)
    };

    private static async Task<PilotRecordProbe?> Operational<T>(PoseidonDbContext db, Guid id, string idProperty, string? traceProperty, bool checkAudit, CancellationToken ct)
        where T : class, ITenantEntity {
        var tenant = db.TenantId;
        var entity = await db.Set<T>().AsNoTracking().Where(e => e.TenantId == tenant && EF.Property<Guid>(e, idProperty) == id).FirstOrDefaultAsync(ct);
        if (entity == null) return null;
        var entry = db.Entry(entity);
        // A towage execution belongs to its assigned tug; other tug records carry the tug as VesselId.
        var vessel = entry.Metadata.FindProperty("TugId") != null ? (Guid)entry.Property("TugId").CurrentValue! : (Guid)entry.Property("VesselId").CurrentValue!;
        var provenance = entry.Metadata.FindProperty("Provenance") != null ? entry.Property("Provenance").CurrentValue as string : null;
        var traced = false;
        if (checkAudit) {
            var key = string.Join('/', entry.Metadata.FindPrimaryKey()!.Properties.Select(p => Convert.ToString(entry.Property(p.Name).CurrentValue, System.Globalization.CultureInfo.InvariantCulture)));
            var traceId = traceProperty == null ? id : (Guid)entry.Property(traceProperty).CurrentValue!;
            traced = await db.AuditEvents.AsNoTracking().AnyAsync(a => a.TenantId == tenant && a.ActorType == "User" && (a.ResourceId == traceId || a.EntityId == key), ct);
        }
        return new(vessel, provenance, traced, null, null, null);
    }

    private static async Task<PilotRecordProbe?> Uat(PoseidonDbContext db, Guid id, CancellationToken ct) {
        var tenant = db.TenantId;
        var r = await db.UatRuns.AsNoTracking().Where(x => x.TenantId == tenant && x.Id == id).Select(x => new { x.Scope, x.State, x.Provenance }).FirstOrDefaultAsync(ct);
        return r == null ? null : new(null, r.Provenance, false, r.Scope, r.State, null);
    }
    private static async Task<PilotRecordProbe?> Feedback(PoseidonDbContext db, Guid id, CancellationToken ct) {
        var tenant = db.TenantId;
        var r = await db.PilotFeedback.AsNoTracking().Where(x => x.TenantId == tenant && x.Id == id).Select(x => new { x.ProgramId }).FirstOrDefaultAsync(ct);
        return r == null ? null : new(null, null, false, null, null, r.ProgramId);
    }
    private static async Task<PilotRecordProbe?> Measurement(PoseidonDbContext db, Guid id, CancellationToken ct) {
        var tenant = db.TenantId;
        var r = await db.PilotMeasurements.AsNoTracking().Where(x => x.TenantId == tenant && x.Id == id).Select(x => new { x.ProgramId }).FirstOrDefaultAsync(ct);
        return r == null ? null : new(null, null, false, null, null, r.ProgramId);
    }
}

// Read model of one program: live link probes, week outcomes, readout (human measurements + DEV-072 telemetry context
// + DEV-074/094 UAT state + real defect severity) and the program recommendation.
internal sealed record PilotProgramState(PilotProgram Program, TimeZoneInfo Zone, PilotProgramVessels Vessels, PilotChecklistLink[] Links,
    Dictionary<Guid, string?> LinkRefusals, PilotIssue[] Issues, Dictionary<Guid, PilotIssueTiming> Timings, PilotWeekOutcome[] Weeks,
    PilotWeekClose[] Closes, PilotMeasurement[] LatestMeasurements, PilotValueRow[] Readout, PilotGateInput UatShip, PilotGateInput UatTug,
    PilotDefectSeverity[] Defects, int OpenP0, PilotProgramOutcome Review, string TelemetryState, string TelemetryRetention, int TelemetryWindowDays);

internal static class PilotProgramReader
{
    internal const int MaxLinks = 512, MaxIssues = 1000, MaxMeasurements = 2000, MaxDefects = 2000, TelemetryWindow = 30;

    internal static TimeZoneInfo? Zone(string id) => TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;

    internal static async Task<PilotProgramState?> Load(HttpContext h, PoseidonDbContext db, TrustedActor actor, Guid id, DateTimeOffset now, CancellationToken ct) {
        var program = await db.PilotPrograms.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (program == null) return null;
        var zone = Zone(program.TimeZone) ?? throw new InvalidDataException("pilot_program_time_zone_unavailable");
        var vessels = new PilotProgramVessels(program.ShipVesselId, program.TugVesselId);
        var links = await db.PilotChecklistLinks.AsNoTracking().Where(l => l.ProgramId == id && l.WithdrawnAt == null)
            .OrderBy(l => l.LinkedAt).ThenBy(l => l.Id).Take(MaxLinks + 1).ToArrayAsync(ct);
        if (links.Length > MaxLinks) throw new InvalidDataException("pilot_program_projection_limit");
        var refusals = new Dictionary<Guid, string?>();
        var inputs = new List<PilotLinkInput>();
        foreach (var l in links) {
            var probe = await PilotRecordProbes.Probe(db, l.RecordType, l.RecordId, false, ct);
            // Audit events are immutable: the trace verified at link time stays valid.
            probe = probe == null ? null : probe with { AuditTraced = l.AuditTracedAtLink };
            var item = PilotProgramCatalog.Item(l.ItemCode)!;
            refusals[l.Id] = PilotProgramPolicy.LinkRefusal(item, l.RecordType, probe, vessels, id);
            inputs.Add(new(l.Id, l.ItemCode, l.RecordType, l.RecordId, l.ActorId, probe));
        }
        var issues = await db.PilotIssues.AsNoTracking().Where(i => i.ProgramId == id).OrderByDescending(i => i.ReportedAt).ThenBy(i => i.Id).Take(MaxIssues + 1).ToArrayAsync(ct);
        if (issues.Length > MaxIssues) throw new InvalidDataException("pilot_program_projection_limit");
        var timings = issues.ToDictionary(i => i.Id, i => PilotIssuePolicy.Timing(i.Severity, i.Category, i.ReportedAt, i.TriagedAt, now, zone));
        var issueInputs = issues.Select(i => new PilotIssueInput(i.Id, i.Week, i.Severity, i.Category, i.State, timings[i.Id])).ToArray();
        var weeks = PilotProgramCatalog.WeekDefinitions.Select(w => PilotProgramPolicy.EvaluateWeek(w, PilotProgramPolicy.Phase(program.StartDate, w.Number, now, zone),
            inputs, issueInputs, vessels, id)).ToArray();
        var closes = await db.PilotWeekCloses.AsNoTracking().Where(c => c.ProgramId == id).OrderByDescending(c => c.RecordedAt).ThenByDescending(c => c.Sequence).Take(256).ToArrayAsync(ct);
        var measurements = await db.PilotMeasurements.AsNoTracking().Where(m => m.ProgramId == id).Take(MaxMeasurements + 1).ToArrayAsync(ct);
        if (measurements.Length > MaxMeasurements) throw new InvalidDataException("pilot_program_projection_limit");
        var latest = measurements.GroupBy(m => (m.MetricId, m.Mode, m.Phase)).Select(g => g.OrderByDescending(m => m.Sequence).First()).ToArray();

        // DEV-072 telemetry is context only: shown when the actor may read pilot metrics, per visible mode, unchanged.
        var telemetryState = "NOT_AUTHORIZED"; var retention = "UNKNOWN"; var window = TelemetryWindow;
        PilotMetricResult[] telemetry = [];
        if (PilotMetricsEndpoints.CanReadSummary(actor)) {
            string[] areas = [.. (actor.CanSeeShip ? [PilotMetricsPolicy.Ship] : Array.Empty<string>()), .. (actor.CanSeeTug ? [PilotMetricsPolicy.Tug] : Array.Empty<string>())];
            var summary = await PilotMetricsEndpoints.BuildSummary(db, h.RequestServices.GetRequiredService<PilotTelemetrySettings>(), areas, TelemetryWindow, now, ct);
            telemetry = summary.Items; telemetryState = "AUTHORIZED"; retention = summary.RetentionState; window = summary.WindowDays;
        }
        PilotTelemetryCell? Cell(PilotValueMetric m) {
            if (telemetryState != "AUTHORIZED") return new(telemetryState, null, "");
            var result = telemetry.SingleOrDefault(r => r.MetricId == m.TelemetryMetricId);
            if (result == null) return new(MetricStates.Unknown, null, "");
            if (result.State == MetricStates.Unavailable) return new(MetricStates.Unavailable, null, result.UnitCode);
            var cell = result.Breakdown.SingleOrDefault(c => c.Dimension == "AREA" && c.Key == m.Mode);
            return cell == null ? new(MetricStates.Unknown, null, result.UnitCode) : new(cell.State, cell.State == MetricStates.Value ? cell.Value : null, result.UnitCode);
        }
        var readout = PilotProgramPolicy.Readout(latest.Select(m => new PilotMeasurementInput(m.MetricId, m.Mode, m.Phase, m.Value, m.Unit, m.SampleSize, m.Id, m.ActorId)).ToArray(), Cell);

        // DEV-074/094 UAT sign-off state, read from the UAT owner (not duplicated).
        var uatShip = await new UatPilotGateSource(UatScenarioCatalog.Ship).ReadAsync(db, now, ct);
        var uatTug = await new UatPilotGateSource(UatScenarioCatalog.Tug).ReadAsync(db, now, ct);
        PilotDefectSeverity[] defects = [await Defects(db, PilotProgramCatalog.Ship, program.ShipVesselId, ct), await Defects(db, PilotProgramCatalog.Tug, program.TugVesselId, ct)];
        var openP0 = issues.Count(i => i.Severity == PilotIssuePolicy.P0 && i.State != PilotIssuePolicy.Resolved);
        var latestCloses = PilotProgramCatalog.Weeks.Select(w => closes.Where(c => c.Week == w).OrderByDescending(c => c.Sequence).FirstOrDefault())
            .Where(c => c != null).Select(c => new PilotWeekCloseInput(c!.Week, c.Outcome, c.WeekHash)).ToArray();
        var review = PilotProgramPolicy.EvaluateProgram(latestCloses, readout, uatShip.State, uatTug.State, defects, openP0);
        return new(program, zone, vessels, links, refusals, issues, timings, weeks, closes, latest, readout, uatShip, uatTug, defects, openP0, review,
            telemetryState, retention, window);
    }

    // Unresolved defect severity from the defect owner's lifecycle: not CLOSED (or no lifecycle yet) is unresolved; the
    // severity is the human-assessed criticality, otherwise UNASSESSED (never assumed Low). Synthetic rows are excluded.
    private static async Task<PilotDefectSeverity> Defects(PoseidonDbContext db, string mode, Guid? vessel, CancellationToken ct) {
        if (vessel == null) return new(mode, PilotProgramPolicy.Unknown, 0, 0, 0, 0, 0, 0);
        var rows = await db.Defects.AsNoTracking().Where(d => d.VesselId == vessel).Select(d => new { d.Id, d.Provenance }).Take(MaxDefects + 1).ToArrayAsync(ct);
        if (rows.Length > MaxDefects) return new(mode, PilotProgramPolicy.Unknown, 0, 0, 0, 0, 0, 0);
        var lifecycles = await db.DefectLifecycles.AsNoTracking().Where(l => l.VesselId == vessel).Select(l => new { l.DefectId, l.Stage, l.AssessedCriticality })
            .Take(MaxDefects + 1).ToArrayAsync(ct);
        if (lifecycles.Length > MaxDefects) return new(mode, PilotProgramPolicy.Unknown, 0, 0, 0, 0, 0, 0);
        var synthetic = rows.Count(r => r.Provenance == "SyntheticUnverified");
        var open = rows.Where(r => r.Provenance != "SyntheticUnverified").Select(r => lifecycles.FirstOrDefault(l => l.DefectId == r.Id))
            .Where(l => l == null || !DefectLifecyclePolicy.IsClosed(l.Stage)).Select(l => l?.AssessedCriticality).ToArray();
        return new(mode, PilotProgramPolicy.Complete, open.Count(c => c == "Critical"), open.Count(c => c == "High"), open.Count(c => c == "Medium"),
            open.Count(c => c == "Low"), open.Count(c => c is null or not ("Critical" or "High" or "Medium" or "Low")), synthetic);
    }

    internal static string WeekSnapshot(PilotWeekOutcome w) => JsonSerializer.Serialize(new {
        w.Week, w.Phase, w.Recommendation, w.OpenP0, w.OverdueP0, w.LateP0, items = w.Items.Select(i => new { i.Code, i.State, i.ValidLinks }), reasons = w.Reasons.Take(64) });
    internal static string ProgramSnapshot(PilotProgramState s) => JsonSerializer.Serialize(new {
        s.Review.Recommendation, reasons = s.Review.Reasons.Take(128), uat = new { ship = s.UatShip.State, tug = s.UatTug.State }, s.OpenP0,
        defects = s.Defects.Select(d => new { d.Mode, d.State, d.Critical, d.High, d.Medium, d.Low, d.Unassessed }),
        metrics = s.Readout.Select(r => new { r.MetricId, r.Quality, r.Baseline, r.Current, r.Delta, r.PercentChange, r.Comparison }) });
}
