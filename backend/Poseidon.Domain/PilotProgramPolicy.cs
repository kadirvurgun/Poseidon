using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

// Pilot program kit (DEV-076/077/078/079 pilot weeks, DEV-095 dual-mode proof, DEV-096 tug value readout, DEV-121
// v46.3 ship+tug proof metrics). Sources: 18.01 sheet Backlog rows 77-80, 96, 97, 122 and sheet "Sprint Plan" rows
// 14-17 (PILOT-W1..W4, "30-Day Design Partner Pilot"); 19.01 §2 "Dual-Mode Pilot Plan" paragraphs 43-67, §7 "Pilot
// Metrics" paragraphs 137-161, §11 "v46.3 Pilot Metrics" paragraphs 255-278; 63.05 paragraphs 142-156 (no value
// without a baseline; percentage only with baseline > 0).
//
// These are real-world pilot activities. Software cannot complete them: this kit only lets a real pilot team record
// and evidence them. A checklist item is satisfied only by a human-linked reference to a real existing record of the
// same tenant that the server verified; nothing is auto-satisfied, nothing is satisfied by AI/automation, and a
// missing baseline or measurement stays UNKNOWN (never 0). Week close and the CONTINUE/EXPAND/FIX/STOP decision are
// the appointed Product Owner's human records; the engine only recommends.
public sealed record PilotWeekDefinition(string Week, int Number, string[] Requirements, string Locator, PilotChecklistItem[] Items);
// RequiresAuditTrace: the linked record must carry a User-attributed write audit event. That proves a human wrote the
// record; it is NOT a completion proof. RequiresCompletion: the source AC says the flow must be "completed", so the
// record's own owner lifecycle must be in its completion state (work order execution CLOSED, defect lifecycle CLOSED,
// a recorded Master departure decision, an offline outcome that is not an unresolved conflict).
public sealed record PilotChecklistItem(string Code, string Week, string Requirement, string Area, string[] RecordTypes, bool RequiresAuditTrace,
    string Criterion, string Locator, bool RequiresCompletion = false);
public sealed record PilotValueMetric(string Id, string Mode, string Unit, string Direction, string[] Requirements, string? TelemetryMetricId, string Locator);

public static class PilotProgramCatalog
{
    public const string Version = "pilot-kit.v1";
    public const string Ship = "SHIP", Tug = "TUG", Shared = "SHARED";
    public static readonly string[] Areas = [Ship, Tug, Shared];
    public static readonly string[] Modes = [Ship, Tug];
    public const string W1 = "W1", W2 = "W2", W3 = "W3", W4 = "W4";
    public static readonly string[] Weeks = [W1, W2, W3, W4];

    // Record types a human may link. Server-side probes verify existence, tenant, type and vessel binding.
    public const string WorkOrder = "WORK_ORDER", Defect = "DEFECT", DepartureDecision = "DEPARTURE_DECISION", OfflineOutcome = "OFFLINE_OUTCOME",
        WorkOrderOfflineOutcome = "WORK_ORDER_OFFLINE_OUTCOME", DailyOfflineOutcome = "DAILY_OFFLINE_OUTCOME", SafetyEventOfflineOutcome = "SAFETY_EVENT_OFFLINE_OUTCOME",
        TugOfflineOutcome = "TUG_OFFLINE_OUTCOME", SafetyEvent = "SAFETY_EVENT", RiskAssessment = "RISK_ASSESSMENT", AuditPack = "AUDIT_PACK",
        TowageExecution = "TOWAGE_EXECUTION", TowagePlan = "TOWAGE_PLAN", TowageBriefing = "TOWAGE_BRIEFING", TugJob = "TUG_JOB", TugDailyLog = "TUG_DAILY_LOG",
        VesselCertificate = "VESSEL_CERTIFICATE", CrewAssignment = "CREW_ASSIGNMENT", ControlEvidence = "CONTROL_EVIDENCE", VoiceEventDraft = "VOICE_EVENT_DRAFT",
        UatRun = "UAT_RUN", PilotFeedback = "PILOT_FEEDBACK", PilotMeasurement = "PILOT_MEASUREMENT";
    public static readonly string[] RecordTypes = [WorkOrder, Defect, DepartureDecision, OfflineOutcome, WorkOrderOfflineOutcome, DailyOfflineOutcome,
        SafetyEventOfflineOutcome, TugOfflineOutcome, SafetyEvent, RiskAssessment, AuditPack, TowageExecution, TowagePlan, TowageBriefing, TugJob, TugDailyLog,
        VesselCertificate, CrewAssignment, ControlEvidence, VoiceEventDraft, UatRun, PilotFeedback, PilotMeasurement];
    // Records owned by the pilot program itself or by the UAT module: tenant-level, no vessel binding.
    public static readonly string[] ProgramOwnedRecordTypes = [UatRun, PilotFeedback, PilotMeasurement];

    private const string Backlog = "18.01/sheet:Backlog/row:", Plan = "19.01/paragraph:";
    private static PilotChecklistItem I(string code, string week, string dev, string area, string[] types, bool traced, string criterion, string locator,
        bool completed = false) => new(code, week, dev, area, types, traced, criterion, locator, completed);

    // Criterion texts are the source acceptance criteria (18.01 column G) or the 19.01 week plan cell, verbatim.
    public static readonly PilotWeekDefinition[] WeekDefinitions = [
        new(W1, 1, ["DEV-076"], Backlog + "77;18.01/sheet:Sprint Plan/row:14;" + Plan + "52-55", [
            I("W1_DAILY_FEEDBACK", W1, "DEV-076", Shared, [PilotFeedback], false, "Daily feedback captured", Backlog + "77"),
            I("W1_FIRST_LIVE_TRACE", W1, "DEV-076", Shared, [ControlEvidence, AuditPack], true, "first live trace demonstrated", Backlog + "77"),
            I("W1_SHIP_CERTIFICATE", W1, "DEV-076", Ship, [VesselCertificate], true, "Today/certificates/crew", Plan + "53"),
            I("W1_SHIP_CREW", W1, "DEV-076", Ship, [CrewAssignment], true, "Today/certificates/crew", Plan + "53"),
            I("W1_TUG_JOB_INTAKE", W1, "DEV-076", Tug, [TugJob], true, "Tug Today/job intake/job board", Plan + "54"),
        ]),
        new(W2, 2, ["DEV-077", "DEV-095"], Backlog + "78;" + Backlog + "96;18.01/sheet:Sprint Plan/row:15;" + Plan + "56-59", [
            I("W2_WORK_ORDER", W2, "DEV-077", Ship, [WorkOrder], true, "At least one WO ... completed with audit trace", Backlog + "78", true),
            I("W2_DEFECT_SAFEGUARD", W2, "DEV-077", Ship, [Defect], true, "one defect/safeguard ... completed with audit trace", Backlog + "78", true),
            I("W2_DEPARTURE_REVIEW", W2, "DEV-077", Ship, [DepartureDecision], true, "one departure review ... completed with audit trace", Backlog + "78", true),
            I("W2_OFFLINE_RECONNECT", W2, "DEV-077", Ship, [OfflineOutcome, WorkOrderOfflineOutcome, DailyOfflineOutcome, SafetyEventOfflineOutcome], true,
                "one offline/reconnect flow completed with audit trace", Backlog + "78", true),
            I("W2_DUAL_SHIP_SAFETY", W2, "DEV-095", Ship, [SafetyEvent, RiskAssessment], true, "1 ship departure/safety flow", Backlog + "96"),
            I("W2_DUAL_TUG_JOB", W2, "DEV-095", Tug, [TugJob], true, "1 tug job/towage flow", Backlog + "96"),
            I("W2_DUAL_TUG_TOWAGE", W2, "DEV-095", Tug, [TowageExecution], true, "1 tug job/towage flow", Backlog + "96"),
            I("W2_DUAL_SHARED_CREW_TUG", W2, "DEV-095", Tug, [CrewAssignment], true, "shared crew/PMS/evidence/audit/offline demonstrated", Backlog + "96"),
            I("W2_DUAL_SHARED_PMS_TUG", W2, "DEV-095", Tug, [WorkOrder], true, "shared crew/PMS/evidence/audit/offline demonstrated", Backlog + "96"),
            I("W2_DUAL_SHARED_EVIDENCE", W2, "DEV-095", Shared, [ControlEvidence, AuditPack], true, "shared crew/PMS/evidence/audit/offline demonstrated", Backlog + "96"),
            I("W2_DUAL_TUG_OFFLINE", W2, "DEV-095", Tug, [TugOfflineOutcome], true, "shared crew/PMS/evidence/audit/offline demonstrated", Backlog + "96"),
        ]),
        new(W3, 3, ["DEV-078"], Backlog + "79;18.01/sheet:Sprint Plan/row:16;" + Plan + "60-63", [
            I("W3_EVENT_FLOW", W3, "DEV-078", Ship, [SafetyEvent], true, "At least one event flow", Backlog + "79"),
            I("W3_AUDIT_PACK_RETRIEVAL", W3, "DEV-078", Ship, [AuditPack], true, "audit pack retrieval", Backlog + "79"),
            I("W3_VOICE_OCR_CORRECTION", W3, "DEV-078", Shared, [VoiceEventDraft], true, "OCR/voice correction metrics", Backlog + "79"),
            I("W3_NO_AI_AUTHORITY_BYPASS", W3, "DEV-078", Shared, [AuditPack], true, "no AI authority bypass", Backlog + "79"),
            I("W3_TUG_TOWAGE_PLAN", W3, "DEV-078", Tug, [TowagePlan], true, "Towage plan/risk/briefing/execution/AI assist", Plan + "62"),
            I("W3_TUG_BRIEFING", W3, "DEV-078", Tug, [TowageBriefing], true, "Towage plan/risk/briefing/execution/AI assist", Plan + "62"),
        ]),
        new(W4, 4, ["DEV-079", "DEV-096", "DEV-121"], Backlog + "80;" + Backlog + "97;" + Backlog + "122;18.01/sheet:Sprint Plan/row:17;" + Plan + "64-67", [
            I("W4_BEFORE_AFTER_BASELINE", W4, "DEV-079", Shared, [PilotMeasurement], false, "Before/after metrics", Backlog + "80"),
            I("W4_SHIP_UAT_SIGNOFF", W4, "DEV-121", Ship, [UatRun], false, "signed UAT", Backlog + "122"),
            I("W4_TUG_UAT_SIGNOFF", W4, "DEV-121", Tug, [UatRun], false, "signed UAT", Backlog + "122"),
            I("W4_TUG_DAILY_LOG", W4, "DEV-079", Tug, [TugDailyLog], true, "Daily log/history/UAT/value", Plan + "66"),
        ]),
    ];
    public static readonly PilotChecklistItem[] Items = WeekDefinitions.SelectMany(w => w.Items).ToArray();
    public static PilotWeekDefinition? Week(string? code) => WeekDefinitions.FirstOrDefault(w => w.Week == code);
    public static PilotChecklistItem? Item(string? code) => Items.FirstOrDefault(i => i.Code == code);

    // Units: SECOND, COUNT, PERCENT (0..100), PERSON. Direction is the comparison sense stated by the source metric;
    // NONE means the source states no better direction (AI correction is measured, not targeted).
    public const string Second = "SECOND", Count = "COUNT", Percent = "PERCENT", Person = "PERSON";
    public static readonly string[] Units = [Second, Count, Percent, Person];
    public const string LowerBetter = "LOWER_BETTER", HigherBetter = "HIGHER_BETTER", NoDirection = "NONE";
    private static PilotValueMetric M(string id, string mode, string unit, string direction, string[] devs, string? telemetry, string locator) =>
        new(id, mode, unit, direction, devs, telemetry, locator);
    public static readonly PilotValueMetric[] ValueMetrics = [
        M("SHIP_TIME_TO_COMPLETE", Ship, Second, LowerBetter, ["DEV-121"], null, Backlog + "122;" + Plan + "144-146"),
        M("SHIP_DUPLICATE_ENTRY", Ship, Count, LowerBetter, ["DEV-121"], null, Backlog + "122;" + Plan + "271-272"),
        M("SHIP_EVIDENCE_COMPLETENESS", Ship, Percent, HigherBetter, ["DEV-121", "DEV-079"], null, Backlog + "122;" + Plan + "269-270"),
        M("SHIP_READINESS_ACCURACY", Ship, Percent, HigherBetter, ["DEV-121"], null, Backlog + "122"),
        M("SHIP_OFFLINE_SUCCESS", Ship, Percent, HigherBetter, ["DEV-121"], PilotMetricsPolicy.SyncConflicts, Backlog + "122;" + Plan + "153-154"),
        M("SHIP_USER_ADOPTION", Ship, Person, HigherBetter, ["DEV-121"], PilotMetricsPolicy.RoleAdoption, Backlog + "122;" + Plan + "159-160"),
        M("SHIP_AI_CORRECTION", Ship, Percent, NoDirection, ["DEV-078"], PilotMetricsPolicy.AiCorrectionRate, Backlog + "79;" + Plan + "156-157"),
        M("TUG_TIME_TO_COMPLETE", Tug, Second, LowerBetter, ["DEV-121"], null, Backlog + "122;" + Plan + "144,146"),
        M("TUG_JOB_PREP_TIME", Tug, Second, LowerBetter, ["DEV-096"], null, Backlog + "97;" + Plan + "273-274"),
        M("TUG_ASSIGNMENT_CONFLICT_VISIBILITY", Tug, Percent, HigherBetter, ["DEV-096"], null, Backlog + "97"),
        M("TUG_EVIDENCE_COMPLETENESS", Tug, Percent, HigherBetter, ["DEV-096", "DEV-121"], null, Backlog + "97;" + Plan + "152"),
        M("TUG_READINESS_ACCURACY", Tug, Percent, HigherBetter, ["DEV-121"], null, Backlog + "122"),
        M("TUG_OFFLINE_SUCCESS", Tug, Percent, HigherBetter, ["DEV-096", "DEV-121"], PilotMetricsPolicy.SyncConflicts, Backlog + "97;" + Plan + "155"),
        M("TUG_USER_ADOPTION", Tug, Person, HigherBetter, ["DEV-096", "DEV-121"], PilotMetricsPolicy.RoleAdoption, Backlog + "97;" + Plan + "161"),
        M("TUG_AI_CORRECTION", Tug, Percent, NoDirection, ["DEV-078"], PilotMetricsPolicy.AiCorrectionRate, Backlog + "79;" + Plan + "158"),
    ];
    public static PilotValueMetric? Metric(string? id) => ValueMetrics.FirstOrDefault(m => m.Id == id);
}

// Structured daily feedback and pilot issue triage (DEV-076 "Daily feedback captured; ... P0 issues triaged same day").
public static class PilotFeedbackPolicy
{
    public static readonly string[] Categories = ["USABILITY", "DATA_QUALITY", "PERFORMANCE", "OFFLINE", "AI_ASSIST", "TRAINING", "SAFETY_CONCERN", "OTHER"];
    public static readonly string[] Sentiments = ["POSITIVE", "NEUTRAL", "NEGATIVE"];
    // Role group only (no person): the same role vocabulary as the UAT catalog plus shore operations.
    public static readonly string[] Roles = ["MASTER", "DPA", "CHIEF_ENGINEER", "CREW", "TUG_MASTER", "TUG_OPERATIONS_RESPONSIBLE", "TUG_CREW", "SHORE_OFFICE", "OTHER"];
    public const int SummaryMin = 3, SummaryMax = 500;

    // Minimisation guard (security baseline: no PII in free text). Refuses an e-mail address, a URL with credentials
    // or a run of 7+ digits (phone, passport, crew or ID numbers). A heuristic, not an anonymisation guarantee.
    public static bool SuspectedPersonalData(string value)
    {
        if (value.Contains('@')) return true;
        var digits = 0;
        foreach (var c in value) {
            if (char.IsAsciiDigit(c)) { if (++digits >= 7) return true; }
            else if (c is not (' ' or '-' or '.' or '/' or '(' or ')' or '+')) digits = 0;
        }
        return false;
    }
}

public sealed record PilotIssueTiming(string State, DateOnly ReportedLocalDate, DateOnly? TriagedLocalDate);

public static class PilotIssuePolicy
{
    public const string P0 = "P0", P1 = "P1", P2 = "P2";
    public static readonly string[] Severities = [P0, P1, P2];
    public const string Open = "OPEN", Triaged = "TRIAGED", Resolved = "RESOLVED";
    public static readonly string[] States = [Open, Triaged, Resolved];
    public static readonly string[] Categories = ["ONBOARDING", "CERTIFICATE", "CREW", "PMS", "DEFECT", "DEPARTURE", "OFFLINE", "SAFETY_EVENT", "EVIDENCE", "AUDIT",
        "AI_ASSIST", "TUG_JOB", "TOWAGE", "UAT", "VALUE", "OTHER"];
    // DEV-076 AC names exactly onboarding/certificate/crew P0 issues for the same-day triage target.
    public static readonly string[] SameDayCategories = ["ONBOARDING", "CERTIFICATE", "CREW"];
    public const string SameDay = "TRIAGED_SAME_DAY", Late = "TRIAGED_LATE", DueToday = "TRIAGE_DUE_TODAY", Overdue = "OVERDUE", NoTarget = "NO_SOURCE_TARGET";
    public static readonly string[] TimingStates = [SameDay, Late, DueToday, Overdue, NoTarget];
    public const int TitleMin = 5, TitleMax = 200, NoteMin = 3, NoteMax = 500;

    public static bool SameDayTarget(string severity, string category) => severity == P0 && SameDayCategories.Contains(category, StringComparer.Ordinal);

    // "Same day" is the program's calendar day in its declared time zone (no invented hour-based SLA). Only the
    // source-named P0 categories carry the target; others are NO_SOURCE_TARGET and still visible.
    public static PilotIssueTiming Timing(string severity, string category, DateTimeOffset reportedAt, DateTimeOffset? triagedAt, DateTimeOffset now, TimeZoneInfo zone)
    {
        var reported = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reportedAt, zone).DateTime);
        DateOnly? triaged = triagedAt is { } t ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t, zone).DateTime) : null;
        if (!SameDayTarget(severity, category)) return new(NoTarget, reported, triaged);
        if (triaged is { } d) return new(d <= reported ? SameDay : Late, reported, triaged);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return new(today > reported ? Overdue : DueToday, reported, null);
    }
}

// Human-entered measurement (baseline or pilot period). Values are never derived; the evidence reference names where
// the measurement lives (time study, export, minutes).
public static class PilotMeasurementPolicy
{
    public const string Baseline = "BASELINE", Pilot = "PILOT";
    public static readonly string[] Phases = [Baseline, Pilot];
    public const int ReferenceMax = 200, MethodMin = 10, MethodMax = 500, MaxSample = 1_000_000;
    public const decimal MaxSeconds = 2_592_000m, MaxCount = 1_000_000m;

    public static bool ValidValue(string unit, decimal value) =>
        decimal.Round(value, 2) == value && value >= 0 && unit switch {
            PilotProgramCatalog.Percent => value <= 100,
            PilotProgramCatalog.Second => value <= MaxSeconds,
            PilotProgramCatalog.Count or PilotProgramCatalog.Person => value <= MaxCount && decimal.Truncate(value) == value,
            _ => false
        };
}

// Live probe of one linked record. Null = not found in this tenant (cross-tenant is indistinguishable by design).
// Completion: COMPLETED / NOT_COMPLETED from the record owner's lifecycle; null when the record type has no completion state.
public sealed record PilotRecordProbe(Guid? VesselId, string? Provenance, bool AuditTraced, string? Scope, string? State, Guid? ProgramId, string? Completion = null);
// A program-owned human record and its author, for separation of duties (feedback carries its week; measurements do not).
public sealed record PilotAuthoredRecord(Guid Id, string? Week, Guid ActorId);
public sealed record PilotLinkInput(Guid Id, string ItemCode, string RecordType, Guid RecordId, Guid ActorId, PilotRecordProbe? Probe);
public sealed record PilotIssueInput(Guid Id, string Week, string Severity, string Category, string State, PilotIssueTiming Timing);
public sealed record PilotItemOutcome(string Code, string State, int ValidLinks, string[] Reasons);
public sealed record PilotWeekOutcome(string Week, string Phase, string Recommendation, PilotItemOutcome[] Items, int OpenP0, int OverdueP0, int LateP0,
    string[] Reasons, string Hash, string Authority = PilotProgramPolicy.Authority);
public sealed record PilotProgramVessels(Guid ShipVesselId, Guid? TugVesselId);

public sealed record PilotMeasurementInput(string MetricId, string Mode, string Phase, decimal Value, string Unit, int SampleSize, Guid Id, Guid ActorId);
public sealed record PilotTelemetryCell(string State, string? Value, string UnitCode);
public sealed record PilotValueRow(string MetricId, string Mode, string Unit, string Direction, string Quality, decimal? Baseline, decimal? Current, decimal? Delta,
    decimal? PercentChange, string Comparison, string[] Reasons, Guid? BaselineId, Guid? CurrentId, PilotTelemetryCell? Telemetry, string Locator);
public sealed record PilotDefectSeverity(string Mode, string State, int Critical, int High, int Medium, int Low, int Unassessed, int SyntheticExcluded);
public sealed record PilotWeekCloseInput(string Week, string Outcome, string Hash);
public sealed record PilotProgramOutcome(string Recommendation, string[] Reasons, string Hash, string Authority = PilotProgramPolicy.Authority);

public static class PilotProgramPolicy
{
    public const string Authority = "ProductOwnerHumanDecisionRequired;EngineRecommendsOnly;AiCannotDecide;SoftwareCannotCompletePilot";
    public const string Satisfied = "SATISFIED", LinkInvalid = "LINK_INVALID", Missing = "MISSING";
    public static readonly string[] ItemStates = [Satisfied, LinkInvalid, Missing];
    public const string NotStarted = "NOT_STARTED", InProgress = "IN_PROGRESS", Ended = "ENDED";
    public static readonly string[] Phases = [NotStarted, InProgress, Ended];
    public const string ReadyForClose = "READY_FOR_HUMAN_CLOSE", NotReady = "NOT_READY";
    public static readonly string[] WeekRecommendations = [ReadyForClose, NotReady];
    public const string Accepted = "ACCEPTED", NotAccepted = "NOT_ACCEPTED";
    public static readonly string[] CloseOutcomes = [Accepted, NotAccepted];
    public const string Continue = "CONTINUE", Expand = "EXPAND", Fix = "FIX", Stop = "STOP";
    public static readonly string[] Decisions = [Continue, Expand, Fix, Stop];
    public const string ExpandEligible = "EXPAND_ELIGIBLE_PENDING_HUMAN_DECISION", FixRecommended = "FIX_RECOMMENDED";
    public static readonly string[] ProgramRecommendations = [ExpandEligible, FixRecommended];
    public const string Complete = "COMPLETE", Unknown = "UNKNOWN";
    public const string Improved = "IMPROVED", Worsened = "WORSENED", Unchanged = "UNCHANGED", NotComparable = "NOT_COMPARABLE";
    public const int RationaleMin = 10, RationaleMax = 2000, NameMin = 3, NameMax = 120, NoteMax = 500, ReasonMin = 10;
    public const string ProductOwnerDuty = "ProductOwner";
    public const string Completed = "COMPLETED", NotCompleted = "NOT_COMPLETED";
    public const string LinkAuthor = "LINK_AUTHOR", FeedbackAuthor = "FEEDBACK_AUTHOR", MeasurementAuthor = "MEASUREMENT_AUTHOR";

    public static bool ValidText(string? value, int min, int max) => value is { } v && v.Trim() == v && v.Length >= min && v.Length <= max && !v.Any(char.IsControl);
    public static bool ValidOptionalText(string? value, int max) => value == null || ValidText(value, 1, max);

    public static DateOnly WeekStart(DateOnly programStart, int number) => programStart.AddDays(7 * (number - 1));
    public static string Phase(DateOnly programStart, int number, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var start = WeekStart(programStart, number);
        return today < start ? NotStarted : today < start.AddDays(7) ? InProgress : Ended;
    }

    // Probe verdict for one link against the item. Never satisfied by an unverified, synthetic or out-of-scope record.
    public static string? LinkRefusal(PilotChecklistItem item, string recordType, PilotRecordProbe? probe, PilotProgramVessels vessels, Guid programId)
    {
        if (!item.RecordTypes.Contains(recordType, StringComparer.Ordinal)) return "record_type_not_allowed";
        if (probe == null) return "record_not_found";
        if (string.Equals(probe.Provenance, "SyntheticUnverified", StringComparison.Ordinal)) return "synthetic_record_not_linkable";
        if (PilotProgramCatalog.ProgramOwnedRecordTypes.Contains(recordType, StringComparer.Ordinal)) {
            if (recordType == PilotProgramCatalog.UatRun) {
                if (probe.State != UatPolicy.RunAttested) return "uat_run_not_attested";
                if (item.Area != PilotProgramCatalog.Shared && probe.Scope != item.Area) return "record_scope_mismatch";
            } else if (probe.ProgramId != programId) return "record_scope_mismatch";
        } else {
            var vesselOk = item.Area switch {
                PilotProgramCatalog.Ship => probe.VesselId == vessels.ShipVesselId,
                PilotProgramCatalog.Tug => vessels.TugVesselId != null && probe.VesselId == vessels.TugVesselId,
                _ => probe.VesselId == vessels.ShipVesselId || vessels.TugVesselId != null && probe.VesselId == vessels.TugVesselId
            };
            if (!vesselOk) return item.Area == PilotProgramCatalog.Tug && vessels.TugVesselId == null ? "tug_vessel_unset" : "record_vessel_mismatch";
        }
        if (item.RequiresCompletion && probe.Completion != Completed) return "record_not_completed";
        if (item.RequiresAuditTrace && !probe.AuditTraced) return "audit_trace_missing";
        return null;
    }

    // Separation of duties for a week close: the Product Owner must not have authored the evidence the week rests on —
    // any active link of the week, feedback recorded for the week or linked from it, or a measurement linked from it or
    // (for a week whose checklist uses the value readout, W4) any measurement the readout currently uses.
    // Mirrored by the PostgreSQL trigger pilot.guard_pilot_duty().
    public static string[] WeekCloseDutyConflicts(PilotWeekDefinition week, Guid actor, IReadOnlyCollection<PilotLinkInput> activeLinks,
        IReadOnlyCollection<PilotAuthoredRecord> feedback, IReadOnlyCollection<PilotAuthoredRecord> latestMeasurements, IReadOnlyCollection<PilotAuthoredRecord> linkedMeasurements)
    {
        var own = activeLinks.Where(l => week.Items.Any(i => i.Code == l.ItemCode)).ToArray();
        var linked = own.Select(l => l.RecordId).ToHashSet();
        var usesReadout = UsesReadout(week);
        var conflicts = new List<string>();
        if (own.Any(l => l.ActorId == actor)) conflicts.Add(LinkAuthor);
        if (feedback.Any(f => f.ActorId == actor && (f.Week == week.Week || linked.Contains(f.Id)))) conflicts.Add(FeedbackAuthor);
        if (linkedMeasurements.Any(m => m.ActorId == actor && linked.Contains(m.Id)) || usesReadout && latestMeasurements.Any(m => m.ActorId == actor))
            conflicts.Add(MeasurementAuthor);
        return conflicts.ToArray();
    }

    // Separation of duties for the program decision: the Product Owner must not have authored any active checklist link,
    // any feedback of the program's weeks or linked from any week (the decision rests on every week, mirroring the
    // week-close rule across W1-W4), nor any measurement the readout uses or a link references.
    // Mirrored by pilot.guard_pilot_duty() (migration 20261124100000_PilotDecisionFeedbackSeparation).
    public static string[] DecisionDutyConflicts(Guid actor, IReadOnlyCollection<PilotLinkInput> activeLinks, IReadOnlyCollection<PilotAuthoredRecord> feedback,
        IReadOnlyCollection<PilotAuthoredRecord> latestMeasurements, IReadOnlyCollection<PilotAuthoredRecord> linkedMeasurements)
    {
        var linked = activeLinks.Select(l => l.RecordId).ToHashSet();
        var conflicts = new List<string>();
        if (activeLinks.Any(l => l.ActorId == actor)) conflicts.Add(LinkAuthor);
        if (feedback.Any(f => f.ActorId == actor && (f.Week is { } w && PilotProgramCatalog.Week(w) != null || linked.Contains(f.Id)))) conflicts.Add(FeedbackAuthor);
        if (latestMeasurements.Any(m => m.ActorId == actor) || linkedMeasurements.Any(m => m.ActorId == actor && linked.Contains(m.Id))) conflicts.Add(MeasurementAuthor);
        return conflicts.ToArray();
    }

    // True exactly for W4 in the current catalog. The pilot.guard_pilot_duty() trigger hard-codes NEW."Week" = 'W4' for
    // this rule; PilotProgramPolicyTests.Uses_readout_is_true_exactly_for_w4_as_the_duty_trigger_hard_codes pins it so
    // catalog drift fails a test (a catalog change needs a new trigger migration).
    public static bool UsesReadout(PilotWeekDefinition week) =>
        week.Items.Any(i => i.RecordTypes.Contains(PilotProgramCatalog.PilotMeasurement, StringComparer.Ordinal));

    public static PilotWeekOutcome EvaluateWeek(PilotWeekDefinition week, string phase, IReadOnlyCollection<PilotLinkInput> links, IReadOnlyCollection<PilotIssueInput> issues,
        PilotProgramVessels vessels, Guid programId)
    {
        var items = week.Items.Select(item => {
            var own = links.Where(l => l.ItemCode == item.Code).ToArray();
            var refusals = own.Select(l => LinkRefusal(item, l.RecordType, l.Probe, vessels, programId)).ToArray();
            var valid = refusals.Count(r => r == null);
            var reasons = refusals.Where(r => r != null).Select(r => "LINK_" + r!.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            return new PilotItemOutcome(item.Code, valid > 0 ? Satisfied : own.Length > 0 ? LinkInvalid : Missing, valid, reasons);
        }).ToArray();
        var own = issues.Where(i => i.Week == week.Week).ToArray();
        var openP0 = own.Count(i => i.Severity == PilotIssuePolicy.P0 && i.State != PilotIssuePolicy.Resolved);
        var overdue = own.Count(i => i.Timing.State == PilotIssuePolicy.Overdue);
        var late = own.Count(i => i.Timing.State == PilotIssuePolicy.Late);
        var due = own.Count(i => i.Timing.State == PilotIssuePolicy.DueToday);
        var reasons = new List<string>();
        if (phase == NotStarted) reasons.Add("WEEK_NOT_STARTED");
        reasons.AddRange(items.Where(i => i.State != Satisfied).Select(i => "ITEM_" + i.State + ":" + i.Code));
        if (overdue > 0) reasons.Add("P0_TRIAGE_OVERDUE");
        if (late > 0) reasons.Add("P0_TRIAGE_LATE");
        if (due > 0) reasons.Add("P0_TRIAGE_PENDING");
        if (openP0 > 0) reasons.Add("P0_ISSUES_OPEN");
        var recommendation = reasons.Count == 0 ? ReadyForClose : NotReady;
        var canonical = new StringBuilder().Append(PilotProgramCatalog.Version).Append('\n').Append(week.Week).Append('|').Append(phase).Append('\n');
        foreach (var i in items) canonical.Append(i.Code).Append('|').Append(i.State).Append('|').Append(i.ValidLinks).Append('|').Append(string.Join(",", i.Reasons)).Append('\n');
        foreach (var l in links.Where(l => week.Items.Any(i => i.Code == l.ItemCode)).OrderBy(l => l.Id)) canonical.Append("L|").Append(l.Id.ToString("D")).Append('\n');
        foreach (var i in own.OrderBy(i => i.Id)) canonical.Append("I|").Append(i.Id.ToString("D")).Append('|').Append(i.State).Append('|').Append(i.Timing.State).Append('\n');
        canonical.Append(recommendation);
        return new(week.Week, phase, recommendation, items, openP0, overdue, late, reasons.ToArray(), Hash(canonical.ToString()));
    }

    // ACCEPTED only when the week is ready for close; NOT_ACCEPTED is always recordable (an honest human outcome).
    public static string? CloseRefusal(string outcome, PilotWeekOutcome week) =>
        !CloseOutcomes.Contains(outcome, StringComparer.Ordinal) ? "validation_failed" :
        week.Phase == NotStarted ? "week_not_started" :
        outcome == Accepted && week.Recommendation != ReadyForClose ? "week_not_ready" : null;

    // Latest human baseline and pilot measurement per metric/mode. A missing side is UNKNOWN with null values; a
    // percentage change requires baseline > 0 (63.05 p156). Telemetry (DEV-072) is context only and never fills a gap.
    public static PilotValueRow[] Readout(IReadOnlyCollection<PilotMeasurementInput> latest, Func<PilotValueMetric, PilotTelemetryCell?> telemetry)
    {
        return PilotProgramCatalog.ValueMetrics.Select(m => {
            var baseline = latest.SingleOrDefault(x => x.MetricId == m.Id && x.Mode == m.Mode && x.Phase == PilotMeasurementPolicy.Baseline);
            var current = latest.SingleOrDefault(x => x.MetricId == m.Id && x.Mode == m.Mode && x.Phase == PilotMeasurementPolicy.Pilot);
            var reasons = new List<string>();
            if (baseline == null) reasons.Add("BASELINE_MISSING");
            if (current == null) reasons.Add("PILOT_MEASUREMENT_MISSING");
            if (baseline != null && baseline.Unit != m.Unit || current != null && current.Unit != m.Unit) reasons.Add("UNIT_MISMATCH");
            var complete = reasons.Count == 0;
            decimal? delta = complete ? current!.Value - baseline!.Value : null;
            decimal? percent = null;
            if (complete && baseline!.Value > 0) percent = decimal.Round(delta!.Value * 100m / baseline.Value, 1, MidpointRounding.ToEven);
            else if (complete) reasons.Add("PERCENT_CHANGE_UNDEFINED_BASELINE_NOT_POSITIVE");
            var comparison = !complete || m.Direction == PilotProgramCatalog.NoDirection ? NotComparable :
                delta == 0 ? Unchanged : (delta < 0) == (m.Direction == PilotProgramCatalog.LowerBetter) ? Improved : Worsened;
            return new PilotValueRow(m.Id, m.Mode, m.Unit, m.Direction, complete ? Complete : Unknown, baseline?.Value, current?.Value, delta, percent, comparison,
                reasons.ToArray(), baseline?.Id, current?.Id, m.TelemetryMetricId == null ? null : telemetry(m), m.Locator);
        }).ToArray();
    }

    public static PilotProgramOutcome EvaluateProgram(IReadOnlyCollection<PilotWeekCloseInput> latestCloses, IReadOnlyCollection<PilotValueRow> readout,
        string uatShip, string uatTug, IReadOnlyCollection<PilotDefectSeverity> defects, int openP0)
    {
        var reasons = new List<string>();
        foreach (var w in PilotProgramCatalog.Weeks) {
            var close = latestCloses.SingleOrDefault(c => c.Week == w);
            if (close == null) reasons.Add("WEEK_NOT_CLOSED:" + w);
            else if (close.Outcome != Accepted) reasons.Add("WEEK_NOT_ACCEPTED:" + w);
        }
        foreach (var r in readout.Where(r => r.Quality != Complete)) reasons.Add("VALUE_UNKNOWN:" + r.MetricId);
        if (uatShip != PilotGatePolicy.Pass) reasons.Add("UAT_SHIP:" + uatShip);
        if (uatTug != PilotGatePolicy.Pass) reasons.Add("UAT_TUG:" + uatTug);
        foreach (var d in defects) {
            if (d.State != Complete) reasons.Add("DEFECTS_UNKNOWN:" + d.Mode);
            else if (d.Critical > 0) reasons.Add("CRITICAL_DEFECTS_UNRESOLVED:" + d.Mode);
            else if (d.Unassessed > 0) reasons.Add("UNASSESSED_DEFECTS_UNRESOLVED:" + d.Mode);
        }
        if (openP0 > 0) reasons.Add("P0_ISSUES_OPEN");
        var recommendation = reasons.Count == 0 ? ExpandEligible : FixRecommended;
        var canonical = new StringBuilder().Append(PilotProgramCatalog.Version).Append('\n');
        foreach (var c in latestCloses.OrderBy(c => c.Week, StringComparer.Ordinal)) canonical.Append("W|").Append(c.Week).Append('|').Append(c.Outcome).Append('|').Append(c.Hash).Append('\n');
        foreach (var r in readout) canonical.Append("M|").Append(r.MetricId).Append('|').Append(r.Quality).Append('|').Append(Number(r.Baseline)).Append('|').Append(Number(r.Current))
            .Append('|').Append(r.BaselineId?.ToString("D") ?? "-").Append('|').Append(r.CurrentId?.ToString("D") ?? "-").Append('\n');
        canonical.Append("U|").Append(uatShip).Append('|').Append(uatTug).Append('\n');
        foreach (var d in defects) canonical.Append("D|").Append(d.Mode).Append('|').Append(d.State).Append('|').Append(d.Critical).Append('|').Append(d.High).Append('|')
            .Append(d.Medium).Append('|').Append(d.Low).Append('|').Append(d.Unassessed).Append('\n');
        canonical.Append("P0|").Append(openP0).Append('\n').Append(recommendation);
        return new(recommendation, reasons.ToArray(), Hash(canonical.ToString()));
    }

    // EXPAND only when every condition is met; CONTINUE, FIX and STOP are always recordable with a rationale.
    public static string? DecisionRefusal(string decision, PilotProgramOutcome program) =>
        !Decisions.Contains(decision, StringComparer.Ordinal) ? "validation_failed" :
        decision == Expand && program.Recommendation != ExpandEligible ? "expand_not_eligible" : null;

    public static string Hash(string canonical) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    private static string Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";
}
