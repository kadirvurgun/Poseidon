using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-086 local adapter boundary. WP-A owns the shared crew-eligibility engine; integration registers an
// implementation (e.g. over ICrewEligibilityEvaluator). With no registration every tug-specific criterion
// stays UNKNOWN (REVIEW_REQUIRED). The adapter returns closed codes only: no names, medical or document data.
public interface ITugCrewCompetenceSource
{
    Task<TugCrewCompetenceObservation> Observe(TugCrewCompetenceQuery query, CancellationToken ct);
}
public sealed record TugCrewCompetenceQuery(Guid TenantId, Guid TugId, Guid JobId, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, TrustedActor Actor);
public sealed record TugCrewCompetenceObservation(string Source, TugCrewCriterion[] Criteria, DateTimeOffset? ValidUntil);

public sealed record TugCrewReadinessView(Guid TugId, Guid AssignmentId, string State, string EvaluatorState, string? EvaluatorSource,
    TugReadinessCheck[] Checks, TugCrewReadinessDto MinimumManning, DateTimeOffset AssessedAt, DateTimeOffset? ValidUntil,
    string Coverage = "SharedCrewEngineAndTowageCompetence;UnverifiedNeverPositive;MasterDecisionSeparate");
public sealed record TowEquipmentAssessment(Guid TugId, string State, TowEquipmentRow[] Items, TugReadinessCheck[] Checks, DateTimeOffset AssessedAt);
public sealed record TowEquipmentRow(Guid Id, long Revision, uint Version, TowEquipmentInputs Inputs, string Provenance, DateTimeOffset SavedAt,
    TugReadinessCheck Check, TowEquipmentVerificationDto[] Verifications);
public sealed record TowEquipmentVerificationDto(Guid Id, long ItemRevision, string Outcome, string? Reason, string Duty, DateTimeOffset RecordedAt);
public sealed record TugConflictReason(string Kind, string Result, string Reason, Guid? SourceId);

internal static class TugReadinessAssessor
{
    internal const int Limit = 128;

    internal static async Task<TowEquipmentAssessment> Equipment(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid tug, DateTimeOffset asOf, CancellationToken ct) {
        var items = await db.TowEquipmentItems.AsNoTracking().Where(x => x.VesselId == tug).OrderBy(x => x.Kind).ThenBy(x => x.Id).Take(Limit).ToArrayAsync(ct);
        var ids = items.Select(x => x.Id).ToArray();
        var verifications = await db.TowEquipmentVerifications.AsNoTracking().Where(v => v.VesselId == tug && ids.Contains(v.ItemId))
            .OrderByDescending(v => v.RecordedAt).Take(Limit * 4).ToArrayAsync(ct);
        // PMS and certificate links are read through their owning gates; without access they stay UNKNOWN.
        var pms = MaintenanceCurrentReader.Permitted(a) ? await MaintenanceCurrentReader.Read(tug, h, db, a, asOf, referenceAt: asOf) : null;
        var certificateIds = items.Where(x => x.CertificateId != null).Select(x => x.CertificateId!.Value).ToArray();
        var certificates = CertificateReader.Read(a) ? await (from c in db.VesselCertificates.AsNoTracking()
            join r in db.VesselCertificateRevisions.AsNoTracking() on new { c.VesselId, Id = c.Id, c.Revision } equals new { r.VesselId, Id = r.CertificateId, r.Revision }
            where c.VesselId == tug && certificateIds.Contains(c.Id) select new { c.Id, r.ExpiryDate }).ToArrayAsync(ct) : null;
        var date = DateOnly.FromDateTime(asOf.UtcDateTime);
        var rows = items.Select(x => {
            var own = verifications.Where(v => v.ItemId == x.Id).ToArray();
            var current = own.Where(v => v.ItemRevision == x.Revision).OrderByDescending(v => v.RecordedAt).FirstOrDefault();
            string? due = x.AssetId == null || pms == null ? null : pms.Where(p => p.Row.AssetId == x.AssetId).Select(p => p.Row.Due.State).ToArray() switch {
                { Length: 0 } => null, var states => states.Contains("Due") ? "Due" : "NotDue" };
            var certificate = certificates?.SingleOrDefault(c => c.Id == x.CertificateId);
            var evidence = new TowEquipmentEvidence(current?.Outcome, due, certificate != null, certificate?.ExpiryDate);
            return new TowEquipmentRow(x.Id, x.Revision, x.Version, x.Inputs(), x.Provenance, x.SavedAt,
                TowEquipmentPolicy.Assess(x.Id, x.Revision, x.Inputs(), evidence, date),
                own.Take(8).Select(v => new TowEquipmentVerificationDto(v.Id, v.ItemRevision, v.Outcome, v.Reason, v.Duty, v.RecordedAt)).ToArray());
        }).ToArray();
        var checks = rows.Select(r => r.Check).Concat(TowEquipmentPolicy.Missing(items.Select(x => x.Kind))).ToArray();
        // Open critical defects on the tug block positive readiness; undetermined criticality stays UNKNOWN.
        checks = [.. checks, .. await Defects(db, a, tug, ct)];
        var state = TugReadinessEngine.StateOf(checks);
        return new(tug, state, rows, checks, asOf);
    }

    internal static async Task<TugReadinessCheck[]> Defects(PoseidonDbContext db, TrustedActor a, Guid tug, CancellationToken ct) {
        if (!DefectAssetContextReader.CanRead(a)) return [new("pms", TugReadinessEngine.Unknown, "DefectAccessRequired")];
        var defects = await db.Defects.AsNoTracking().Where(d => d.VesselId == tug).OrderBy(d => d.Id).Take(Limit + 1).ToArrayAsync(ct);
        if (defects.Length > Limit) return [new("pms", TugReadinessEngine.Unknown, "DefectProjectionLimitExceeded")];
        var result = new List<TugReadinessCheck>();
        foreach (var d in defects) {
            SortedDictionary<string, DefectFact> fields;
            try { fields = DefectPolicy.ReadFields(d.FieldsJson); } catch (InvalidDataException) { result.Add(new("pms", TugReadinessEngine.Unknown, "DefectFieldsUnavailable", true, d.Id, d.Revision)); continue; }
            // No defect closure/verification exists yet: every declaration is treated as open.
            var criticality = fields.TryGetValue("criticality", out var c) && c.State == "Known" ? c.Value : null;
            result.Add(criticality switch {
                "Critical" => new("pms", TugReadinessEngine.Fail, "DeclaredCriticalDefectOpen", true, d.Id, d.Revision),
                "High" => new("pms", TugReadinessEngine.Condition, "DeclaredHighDefectOpen", false, d.Id, d.Revision),
                null => new("pms", TugReadinessEngine.Unknown, "DefectCriticalityUnknown", true, d.Id, d.Revision),
                _ => new("pms", TugReadinessEngine.Pass, "DeclaredLowerCriticalityDefect", false, d.Id, d.Revision)
            });
        }
        return result.ToArray();
    }

    internal static async Task<TugCrewReadinessView> Crew(HttpContext h, PoseidonDbContext db, TrustedActor a, TugAssignmentProposal p, DateTimeOffset now, CancellationToken ct) {
        var plan = new TugPlanInputs(p.PlannedStart, p.PlannedEnd, [p.TugId]);
        var manning = await TugCrewReadinessReader.Read(db, a, h.RequestServices.GetRequiredService<FederationSettings>(), p.TugId, plan, now, ct,
            h.RequestServices.GetRequiredService<CrewAssignmentPolicy>(), CrewFilePolicy.Current(h));
        var criteria = new List<TugCrewCriterion> {
            new(TugCrewCompetencePolicy.MinimumManning, manning.State == "CurrentMinimumRoleCoverage" ? TugReadinessEngine.Pass : manning.State == "Blocked" ? TugReadinessEngine.Fail : TugReadinessEngine.Unknown,
                manning.Reason, manning.State == "CurrentMinimumRoleCoverage") };
        var source = h.RequestServices.GetService<ITugCrewCompetenceSource>();
        string evaluator = "Unavailable"; string? name = null; DateTimeOffset? until = manning.ValidUntil;
        if (source != null) {
            try {
                var observed = await source.Observe(new(a.TenantId, p.TugId, p.JobId, p.PlannedStart, p.PlannedEnd, a), ct);
                if (observed.Criteria.Length <= 64 && observed.Source is { Length: > 0 and <= 80 }) {
                    // The shared engine's MinimumManning, when given, can only lower the reader's state.
                    criteria.AddRange(observed.Criteria);
                    evaluator = "Observed"; name = observed.Source;
                    if (observed.ValidUntil != null) until = until == null || observed.ValidUntil < until ? observed.ValidUntil : until;
                    if (observed.ValidUntil <= p.PlannedEnd) criteria.Add(new(TugCrewCompetencePolicy.Credential, TugReadinessEngine.Unknown, "EvaluationDoesNotCoverWindow", false));
                } else evaluator = "InvalidObservation";
            } catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TimeoutException) { evaluator = "Unavailable"; }
        }
        var checks = TugCrewCompetencePolicy.Assess(criteria);
        if (evaluator != "Observed") checks = [.. checks.Where(c => c.Category == "crew"),
            .. TugCrewCompetencePolicy.Criteria.Skip(1).Select(c => new TugReadinessCheck("crew_competence", TugReadinessEngine.Unknown, c + ":CrewEligibilityEvaluatorUnavailable"))];
        var state = TugReadinessEngine.StateOf(checks);
        return new(p.TugId, p.Id, state, evaluator, name, checks, manning, now, until);
    }

    // DEV-083 conflict aggregation for one proposal: time overlap, crew, equipment, PMS/defect and certificate,
    // each with an explicit reason. Conflicts are surfaced for the human decision, never auto-resolved.
    internal static async Task<TugConflictReason[]> Conflicts(HttpContext h, PoseidonDbContext db, TrustedActor a, TugAssignmentProposal p, DateTimeOffset now, CancellationToken ct) {
        var result = new List<TugConflictReason>();
        var visible = Tugs(db, a).Select(v => v.Id);
        var others = await db.TugAssignmentProposals.AsNoTracking().Where(o => o.TugId == p.TugId && o.Id != p.Id && o.State == TugAssignmentWorkflow.Proposed &&
                visible.Contains(o.VesselId) && o.PlannedStart < p.PlannedEnd && p.PlannedStart < o.PlannedEnd).OrderBy(o => o.Id).Take(Limit)
            .Select(o => new TugAssignmentWindow(o.Id, o.TugId, o.PlannedStart, o.PlannedEnd, o.State, o.MasterAck)).ToArrayAsync(ct);
        foreach (var o in TugAssignmentWorkflow.TimeOverlaps(new(p.Id, p.TugId, p.PlannedStart, p.PlannedEnd, p.State, p.MasterAck), others))
            result.Add(new("TimeOverlap", TugReadinessEngine.Unknown, o.MasterAck == TugAssignmentWorkflow.Accepted ? "OverlapsAcknowledgedProposal" : "OverlapsTentativeProposal", o.Id));
        var plan = new TugPlanInputs(p.PlannedStart, p.PlannedEnd, [p.TugId]);
        foreach (var c in await TugOperationalConflicts.Read(db, a, p.JobId, [p.TugId], p.PlannedStart, p.PlannedEnd, ct))
            result.Add(new(c.Kind == "CrewAssignment" ? "Crew" : "TimeOverlap", TugReadinessEngine.Unknown, c.Why, c.ResourceId));
        foreach (var f in await TugReadinessConsumer.Read(db, a, p.TugId, plan, now, ct, CrewFilePolicy.Current(h), h))
            if (f.State is "Blocked" or "Unavailable")
                result.Add(new(Kind(f.Source), f.State == "Blocked" ? TugReadinessEngine.Fail : TugReadinessEngine.Unknown, f.Why, f.Id));
        foreach (var c in (await Equipment(h, db, a, p.TugId, p.PlannedEnd, ct)).Checks.Where(c => c.Result is TugReadinessEngine.Fail))
            result.Add(new(c.Category == "pms" ? "PmsDefect" : "Equipment", c.Result, c.Reason, c.SourceId));
        return result.ToArray();
    }

    internal static string Kind(string source) => source switch {
        "Crew" or "CrewAssignment" or "CrewMinimumManning" => "Crew",
        "CrewCertificate" or "BollardPullCertificate" => "Certificate",
        "EquipmentPms" or "MaintenancePlan" => "PmsDefect",
        _ => "Equipment"
    };
    private static string Category(string source) => Kind(source) switch { "Crew" => "crew", "Certificate" => "certificate", "PmsDefect" => "pms", _ => "equipment" };

    internal sealed record Assessment(TugReadinessOutcome Outcome, TugReadinessCheck[] Checks, string? BollardPullMargin, string? AvailableBollardPull,
        TugCrewReadinessView Crew, TowEquipmentAssessment Equipment, TugConflictReason[] Conflicts);

    // DEV-084 aggregate for one proposal. Every input is read through its owning gate at server time.
    internal static async Task<Assessment> Assess(HttpContext h, PoseidonDbContext db, TrustedActor a, TugAssignmentProposal p, TugJob job, DateTimeOffset now, CancellationToken ct) {
        var checks = new List<TugReadinessCheck>();
        var plan = new TugPlanInputs(p.PlannedStart, p.PlannedEnd, [p.TugId]);
        var evidence = await TugPlanningEvidenceAdapter.Read(db, a, h.RequestServices.GetRequiredService<TugPlanningPolicy>(),
            h.RequestServices.GetRequiredService<MaintenanceObservationSettings>(), p.JobId, plan, ct,
            h.RequestServices.GetRequiredService<FederationSettings>(), CrewFilePolicy.Current(h), h, now);
        foreach (var f in evidence.ReadinessFacts ?? []) {
            if (f.Source == "OperationContext") continue; // operation applicability is the regulatory gate, not a tug readiness category
            checks.Add(TugReadinessEngine.FromFact(Category(f.Source), f.State, f.Source + ":" + f.Why, f.Id, f.Revision));
        }
        var crew = await Crew(h, db, a, p, now, ct);
        checks.AddRange(crew.Checks.Where(c => c.Category == "crew_competence"));
        var equipment = await Equipment(h, db, a, p.TugId, p.PlannedEnd, ct);
        checks.AddRange(equipment.Checks.Select(c => c.Category == "pms" ? c : c with { Category = "tow_equipment" }));
        // S8 DEV-087/088: the communication input comes only from a current approved towage plan that covers this
        // tug (otherwise UNKNOWN); an incomplete towage risk briefing is a FAIL that blocks READY.
        var towage = await TowageReader.Read(db, a, p.VesselId, p.JobId, ct);
        checks.Add(TowagePlanPolicy.Communication(towage.Plan, p.TugId, job.Revision));
        checks.AddRange(TowageBriefingPolicy.Checks(towage.Briefing, towage.Plan, towage.Risk, towage.RiskReadable));
        // No whole-window weather source exists yet (60.02 paragraph 196): UNKNOWN.
        checks.Add(new("weather", TugReadinessEngine.Unknown, "WholeWindowWeatherSourceUnavailable"));
        var capacity = evidence.OwnedBp?.SingleOrDefault(c => c.TugId == p.TugId);
        decimal? available = capacity is { State: "CurrentPlanningCapacity", Tonnes: { } t } && decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) ? v : null;
        var (margin, bp) = TugReadinessEngine.BollardPullMargin(job.RequiredBollardPull, available);
        checks.Add(bp with { SourceId = capacity?.EvidenceId, SourceRevision = capacity?.EvidenceRevision });
        checks.Add(p.MasterAck switch {
            TugAssignmentWorkflow.Accepted => new("assignment", TugReadinessEngine.Pass, "MasterAcknowledgedScope", true, p.Id, p.Revision),
            TugAssignmentWorkflow.Rejected => new("assignment", TugReadinessEngine.Fail, "MasterRejectedScope", true, p.Id, p.Revision),
            _ => new("assignment", TugReadinessEngine.Unknown, "MasterAcknowledgementPending", true, p.Id, p.Revision)
        });
        if (p.JobRevision != job.Revision) checks.Add(new("assignment", TugReadinessEngine.Unknown, "RequestChangedSinceProposal", true, job.Id, job.Revision));
        if (p.PlannedEnd <= now) checks.Add(new("assignment", TugReadinessEngine.Unknown, "PlannedWindowElapsed", true, p.Id, p.Revision));
        var conflicts = await Conflicts(h, db, a, p, now, ct);
        foreach (var c in conflicts.Where(c => c.Kind == "TimeOverlap"))
            checks.Add(new("assignment", c.Result, "Conflict:" + c.Reason, true, c.SourceId));
        var all = checks.ToArray();
        return new(TugReadinessEngine.Evaluate(all), all, margin, available?.ToString("0.##", CultureInfo.InvariantCulture), crew, equipment, conflicts);
    }

    private static IQueryable<Vessel> Tugs(PoseidonDbContext db, TrustedActor a) => TugReadinessHttp.Tugs(db, a);
}
