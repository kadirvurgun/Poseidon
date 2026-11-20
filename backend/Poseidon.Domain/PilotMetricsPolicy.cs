using System.Globalization;

namespace Poseidon.Domain;

// DEV-072 / T-S11-004 (18.01 Backlog row 73): privacy-safe pilot metric event schema and
// summary evaluation. 19.01 paragraph:137-161 names the pilot metrics (task success, time to
// action, exception interception, evidence search time, offline success/conflict, AI
// correction, adoption by Master/DPA/C/E/Crew and Tug Ops/Master/C/E/Crew); 19.01
// paragraph:136 excludes protected-report identity from generic analytics.
//
// The schema is CLOSED: codes and one bounded duration only. There is no free text, record id,
// vessel id, person id, name, query string, URL or device field, so neither a protected report
// identity nor any other personal datum can be carried by an event. Actor and role are derived
// by the server and stored only as tenant-salted pseudonymous buckets. Aggregates are
// observations of instrumented client behaviour, never accuracy, safety or acceptance claims.
public sealed record PilotMetricEventInput(string Kind, string Area, string? Journey, string? AiSource, int? DurationMs, DateTimeOffset OccurredAt);

// Persisted, server-normalised form (no actor identity; OccurredHour is truncated to the hour).
public sealed record PilotMetricEventRecord(string Kind, string Area, string? Journey, string? AiSource, int? DurationMs, DateTimeOffset OccurredHour);

// One stored batch as the summary reads it: the pseudonymous contributor and role-group buckets
// apply to every event of the batch.
public sealed record PilotMetricBatchView(string ActorBucket, string RoleBucket, IReadOnlyList<PilotMetricEventRecord> Events);

public sealed record PilotMetricCell(string Dimension, string Key, string State, string? Value, int? Contributors, string[] Reasons);
public sealed record PilotMetricResult(string MetricId, int DefinitionVersion, string UnitCode, string State, string? Value, int? Contributors,
    string[] Reasons, PilotMetricCell[] Breakdown);

public static class PilotMetricsPolicy
{
    public const string SchemaVersion = "pilot-event.v1";
    public const string ReadPermission = "pilot.metrics.read";
    // Operator-provisioned pilot enrolment: only enrolled memberships contribute events.
    public const string ContributePermission = "pilot.metrics.contribute";
    public const string Ship = "SHIP", Tug = "TUG";
    public const int MaxEventsPerBatch = 50;
    public const int MaxDurationMs = 600_000;
    public static readonly TimeSpan MaxPast = TimeSpan.FromDays(7);
    public static readonly TimeSpan MaxFuture = TimeSpan.FromMinutes(5);
    public static readonly int[] WindowDays = [7, 30];
    // Same unapproved small-group default the crew scalar uses (64.01 paragraph:1398): a cell
    // backed by fewer distinct pseudonymous contributors is SUPPRESSED. Not an approved privacy threshold.
    public const int MinContributors = 5;

    public const string JourneyStarted = "JOURNEY_STARTED", JourneyCompleted = "JOURNEY_COMPLETED", JourneyAbandoned = "JOURNEY_ABANDONED";
    public const string RoleActive = "ROLE_ACTIVE";
    public const string AiAccepted = "AI_SUGGESTION_ACCEPTED", AiCorrected = "AI_SUGGESTION_CORRECTED", AiRejected = "AI_SUGGESTION_REJECTED";
    public const string SyncConflict = "SYNC_CONFLICT";
    public const string EvidenceSearch = "EVIDENCE_SEARCH";
    public static readonly string[] Kinds = [JourneyStarted, JourneyCompleted, JourneyAbandoned, RoleActive, AiAccepted, AiCorrected, AiRejected, SyncConflict, EvidenceSearch];
    public static readonly string[] Areas = [Ship, Tug];
    // 19.01 paragraph:142 (ship) and :143 (tug) task-success journeys.
    public static readonly string[] ShipJourneys = ["SHIP_DEPARTURE", "SHIP_CERTIFICATE", "SHIP_CREW", "SHIP_PMS"];
    public static readonly string[] TugJourneys = ["TUG_JOB", "TUG_ASSIGNMENT", "TUG_READINESS", "TUG_PLAN", "TUG_EXECUTION"];
    public static readonly string[] Journeys = [.. ShipJourneys, .. TugJourneys];
    // 19.01 paragraph:157 (OCR/voice) and :158 (OCR/voice/tug recommendation).
    public static readonly string[] AiSources = ["OCR", "VOICE", "TUG_RECOMMENDATION"];

    public const string JourneyCompletion = "pilot.journey.completion";
    public const string RoleAdoption = "pilot.role.adoption";
    public const string AiCorrectionRate = "pilot.ai.correction_rate";
    public const string SyncConflicts = "pilot.sync.conflicts";
    public const string EvidenceSearchP95 = "pilot.evidence.search_p95";
    public static readonly string[] MetricIds = [JourneyCompletion, RoleAdoption, AiCorrectionRate, SyncConflicts, EvidenceSearchP95];

    private static bool Journey(string kind) => kind is JourneyStarted or JourneyCompleted or JourneyAbandoned;
    private static bool Ai(string kind) => kind is AiAccepted or AiCorrected or AiRejected;

    // Returns null when valid, otherwise a closed error code. Every unknown code is rejected,
    // never coerced; an optional field that does not belong to the kind is rejected too.
    public static string? Validate(PilotMetricEventInput e, DateTimeOffset now)
    {
        if (e == null || !Kinds.Contains(e.Kind, StringComparer.Ordinal) || !Areas.Contains(e.Area, StringComparer.Ordinal)) return "invalid_event_code";
        if (Journey(e.Kind) != (e.Journey != null)) return "invalid_event_shape";
        if (e.Journey != null && !(e.Area == Ship ? ShipJourneys : TugJourneys).Contains(e.Journey, StringComparer.Ordinal)) return "invalid_event_code";
        if (Ai(e.Kind) != (e.AiSource != null)) return "invalid_event_shape";
        if (e.AiSource != null && (!AiSources.Contains(e.AiSource, StringComparer.Ordinal) || e.Area == Ship && e.AiSource == "TUG_RECOMMENDATION")) return "invalid_event_code";
        if ((e.Kind == EvidenceSearch) != (e.DurationMs != null)) return "invalid_event_shape";
        if (e.DurationMs is < 0 or > MaxDurationMs) return "invalid_event_value";
        if (e.OccurredAt.Offset != TimeSpan.Zero || e.OccurredAt < now - MaxPast || e.OccurredAt > now + MaxFuture) return "invalid_event_time";
        return null;
    }

    public static PilotMetricEventRecord Normalize(PilotMetricEventInput e) =>
        new(e.Kind, e.Area, e.Journey, e.AiSource, e.DurationMs, Hour(e.OccurredAt));

    public static DateTimeOffset Hour(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    // Pseudonymous bucket: SHA-256 over a per-tenant random salt and the subject. The salt never
    // leaves the database; without it a bucket cannot be matched to a known actor or role id.
    public static string Bucket(byte[] tenantSalt, string subject)
    {
        ArgumentNullException.ThrowIfNull(tenantSalt);
        if (tenantSalt.Length < 32) throw new ArgumentException("Pilot metric salt is too short.", nameof(tenantSalt));
        var bytes = System.Text.Encoding.UTF8.GetBytes(subject);
        var input = new byte[tenantSalt.Length + 1 + bytes.Length];
        tenantSalt.CopyTo(input, 0); input[tenantSalt.Length] = 0x1F; bytes.CopyTo(input, tenantSalt.Length + 1);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(input))[..32];
    }

    // Retention unconfigured: no value exists for any metric (privacy by default, never a zero).
    public static PilotMetricResult[] Unavailable(string reason) =>
        MetricIds.Select(id => new PilotMetricResult(id, Version(id), Unit(id), MetricStates.Unavailable, null, null, [reason], [])).ToArray();

    public static int Version(string id) => 1;
    public static string Unit(string id) => id switch
    {
        JourneyCompletion or AiCorrectionRate => "permille",
        RoleAdoption => "person",
        SyncConflicts => "conflict",
        EvidenceSearchP95 => "millisecond",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    private sealed record Row(string Actor, string Role, PilotMetricEventRecord Event);

    public static PilotMetricResult[] Summarize(IEnumerable<PilotMetricBatchView> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);
        var rows = batches.SelectMany(b => b.Events.Select(e => new Row(b.ActorBucket, b.RoleBucket, e))).ToArray();
        return
        [
            Evaluate(JourneyCompletion, rows.Where(r => r.Event.Kind is JourneyCompleted or JourneyAbandoned).ToArray(),
                r => [("AREA", r.Event.Area), ("JOURNEY", r.Event.Journey!)], Ratio(JourneyCompleted)),
            Evaluate(RoleAdoption, rows, r => [("AREA", r.Event.Area), ("ROLE_GROUP", r.Role)],
                cell => cell.Select(r => r.Actor).Distinct(StringComparer.Ordinal).LongCount()),
            Evaluate(AiCorrectionRate, rows.Where(r => Ai(r.Event.Kind)).ToArray(),
                r => [("AREA", r.Event.Area), ("AI_SOURCE", r.Event.AiSource!)], Ratio(AiCorrected)),
            Evaluate(SyncConflicts, rows.Where(r => r.Event.Kind == SyncConflict).ToArray(), r => [("AREA", r.Event.Area)], cell => cell.LongCount()),
            Evaluate(EvidenceSearchP95, rows.Where(r => r.Event.Kind == EvidenceSearch).ToArray(), r => [("AREA", r.Event.Area)],
                cell => P95(cell.Select(r => (long)r.Event.DurationMs!.Value))),
        ];
    }

    // Share of terminal observations with the given outcome, in permille (half-up). Denominator is
    // the terminal set of the cell only, so in-flight or never-instrumented journeys are not counted as failures.
    private static Func<Row[], long> Ratio(string kind) => cell =>
        (cell.LongCount(r => r.Event.Kind == kind) * 2000 + cell.Length) / (2L * cell.Length);

    // Nearest-rank 95th percentile.
    public static long P95(IEnumerable<long> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) throw new ArgumentException("No values.", nameof(values));
        var rank = (int)Math.Ceiling(0.95 * sorted.Length);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }

    private static PilotMetricResult Evaluate(string id, Row[] rows, Func<Row, (string Dimension, string Key)[]> dimensions, Func<Row[], long> value)
    {
        var (state, text, contributors, reasons) = Cell(rows, value);
        var cells = new List<PilotMetricCell>();
        foreach (var group in rows.SelectMany(r => dimensions(r).Select(d => (d.Dimension, d.Key, Row: r))).GroupBy(x => x.Dimension, StringComparer.Ordinal))
        {
            var members = group.GroupBy(x => x.Key, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => { var c = Cell(g.Select(x => x.Row).ToArray(), value); return new PilotMetricCell(group.Key, g.Key, c.State, c.Value, c.Contributors, c.Reasons); })
                .ToList();
            // 55.01 paragraph:646-647 complementary suppression: when exactly one cell of a dimension is
            // suppressed under a published total, the next smallest published cell is suppressed too so the
            // hidden cell cannot be differenced out of the total.
            if (state == MetricStates.Value && members.Count(m => m.State == MetricStates.Suppressed) == 1 && members.Any(m => m.State == MetricStates.Value))
            {
                var smallest = members.Where(m => m.State == MetricStates.Value).OrderBy(m => m.Contributors).ThenBy(m => m.Key, StringComparer.Ordinal).First();
                members[members.IndexOf(smallest)] = smallest with { State = MetricStates.Suppressed, Value = null, Contributors = null, Reasons = ["COMPLEMENTARY_SUPPRESSION"] };
            }
            cells.AddRange(members);
        }
        return new(id, Version(id), Unit(id), state, text, contributors, reasons, [.. cells]);
    }

    private static (string State, string? Value, int? Contributors, string[] Reasons) Cell(Row[] rows, Func<Row[], long> value)
    {
        // No observation is UNKNOWN, never a zero (55.01 paragraph:652).
        if (rows.Length == 0) return (MetricStates.Unknown, null, null, ["NO_OBSERVATIONS"]);
        var contributors = rows.Select(r => r.Actor).Distinct(StringComparer.Ordinal).Count();
        if (contributors < MinContributors) return (MetricStates.Suppressed, null, null, ["SMALL_GROUP"]);
        return (MetricStates.Value, value(rows).ToString(CultureInfo.InvariantCulture), contributors, []);
    }
}
