using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TugCrewReadinessDto(Guid TugId, string State, string Reason, DateTimeOffset? WindowStart,
    DateTimeOffset? WindowEnd, DateTimeOffset AssessedAt, Guid? MsmdId, long? MsmdRevision, ManningRoleCoverage[] Roles,
    DateTimeOffset? ValidUntil, string Coverage = "SourceQualifiedMinimumRolesOnly;OtherCrewGatesAndMasterDecisionSeparate");

internal static class TugCrewReadinessReader
{
    internal static async Task<TugCrewReadinessDto> Read(PoseidonDbContext db, TrustedActor actor, FederationSettings settings,
        Guid tug, TugPlanInputs plan, DateTimeOffset now, CancellationToken ct, CrewAssignmentPolicy? declarations = null, CrewFilePolicy? files = null)
    {
        TugCrewReadinessDto Empty(string state, string reason) => new(tug, state, reason, plan.PlannedStart, plan.PlannedEnd, now, null, null, [], null);
        if (!actor.CanReadTug || !ComplianceInputContext.CrewRead(actor) || !OperationEquipmentCommands.Read(actor) ||
            !actor.Permissions.Contains("human.review.read") || !actor.Permissions.Contains("applicability.read") ||
            !await actor.Scope(db.Vessels).AnyAsync(v => v.Id == tug && v.Kind == VesselKind.Tug, ct))
            return Empty("Unavailable", "CurrentProtectedCrewSourceAccessRequired");
        if (plan.PlannedStart is not { } start || plan.PlannedEnd is not { } end || end <= start || end <= now)
            return Empty("ReviewRequired", "DeclaredFutureOrCurrentJobWindowRequired");
        var runtime = db.QualifiedInputs;
        if (runtime == null || declarations == null || !declarations.Configured || declarations.ReferenceDate(start) is not { } fromDate ||
            declarations.ReferenceDate(end) is not { } toDate) return Empty("ReviewRequired", "CurrentAssignmentPolicyUnavailable");
        // Every source must be current now AND cover the declared job end. No
        // requested-time/clock fallback supplies a missing draft planning window.
        var horizon = end;
        var documents = await OperationEquipmentCommands.Visible(db, actor, db.ControlEvidenceRevisions.AsNoTracking())
            .Where(r => r.VesselId == tug && r.Kind == "Evidence" && (r.Lifecycle == "SUBMITTED" || r.Lifecycle == "COMPLETED") &&
                db.ControlEvidenceHeads.Any(h => h.VesselId == tug && h.Kind == r.Kind && h.Id == r.Id && h.Revision == r.Revision) &&
                db.OperationEquipmentBindings.Any(b => b.VesselId == tug && b.Id == r.Id && b.Revision == r.Revision && b.DocumentKind == "Msmd"))
            .OrderBy(r => r.Id).Take(129).ToArrayAsync(ct);
        var complete = documents.Where(r => {
            var c = JsonSerializer.Deserialize<ControlEvidenceContent>(r.ContentJson, RegulatoryQualificationEndpoints.Json)!;
            return c.Msmd != null && ComplianceInputs.Complete(c) && c.ValidFrom <= now && c.ValidUntil > now;
        }).ToArray();
        if (documents.Length > 128 || complete.Length != 1) return Empty("ReviewRequired", complete.Length == 0 ? "SourceQualifiedMsmdRequired" : "CurrentMsmdReferencesAmbiguous");
        var document = await QualifiedOwningDocumentReader.Read(db, settings, tug, complete[0].Id, "Msmd", now, ct);
        var startDocument = await QualifiedOwningDocumentReader.Read(db, settings, tug, complete[0].Id, "Msmd", start, ct);
        var futureDocument = await QualifiedOwningDocumentReader.Read(db, settings, tug, complete[0].Id, "Msmd", horizon, ct);
        if (document == null || startDocument == null || futureDocument == null || document.Value.Hash != startDocument.Value.Hash ||
            document.Value.Hash != futureDocument.Value.Hash || document.Value.Until <= horizon)
            return Empty("ReviewRequired", "SourceQualifiedMsmdDoesNotCoverJobWindow");
        var msmd = document.Value.Content.Msmd!;
        if (msmd.Roles is not { Length: > 0 } roles) return Empty("ReviewRequired", "SourceRoleProfileInvalid");
        var positions = roles.Select(r => r.PositionId).ToArray();
        var assignments = await db.CrewAssignments.AsNoTracking().Where(a => a.VesselId == tug && a.PositionId != null && positions.Contains(a.PositionId.Value) &&
            (a.From == null || a.From <= toDate) && (a.To == null || a.To > fromDate))
            .OrderBy(a => a.Id).Take(1001).ToArrayAsync(ct);
        if (assignments.Length > 1000) return Empty("ReviewRequired", "CrewProjectionLimitExceeded");
        var candidates = new List<ManningCandidate>(); var expiries = new List<DateTimeOffset> { document.Value.Until };
        foreach (var assignment in assignments) {
            var position = await db.CrewPositions.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == tug && p.Id == assignment.PositionId, ct);
            if (position == null || assignment.ContractEndDate < toDate || !await runtime.AssignmentPolicyCurrent(db, assignment, position, now, ct) ||
                !await runtime.AssignmentPolicyCurrent(db, assignment, position, start > now ? start : now, ct) ||
                !await runtime.AssignmentPolicyCurrent(db, assignment, position, horizon, ct)) {
                candidates.Add(new(assignment.PositionId!.Value, assignment.PersonId, null)); continue;
            }
            var observation = await Role(db, actor, settings, tug, document.Value.Row.Id, assignment, position, start, now, horizon, fromDate, toDate, ct, files);
            candidates.Add(new(position.Id, assignment.PersonId, observation.Value));
            if (observation.Until != null) expiries.Add(observation.Until.Value);
        }
        var outcome = CrewJobCoveragePolicy.Observe(roles, candidates.ToArray());
        if (outcome.Roles.Length == 0) return Empty("ReviewRequired", outcome.Reason);
        return new(tug, outcome.State, outcome.Reason, start, end, now, document.Value.Row.Id, document.Value.Row.Revision,
            outcome.Roles, expiries.Min());
    }

    private static async Task<(bool? Value, DateTimeOffset? Until)> Role(PoseidonDbContext db, TrustedActor actor, FederationSettings settings,
        Guid tug, Guid msmd, CrewAssignment assignment, CrewPosition position, DateTimeOffset start, DateTimeOffset now, DateTimeOffset horizon,
        DateOnly fromDate, DateOnly toDate, CancellationToken ct, CrewFilePolicy? files)
    {
        var policies = await db.RegulatoryQualificationEvents.AsNoTracking().Where(q => q.State == "IN_FORCE" &&
            db.RegulatoryQualificationHeads.Any(h => h.RuleId == q.RuleId && h.Revision == q.Revision)).OrderBy(q => q.RuleId).Take(1025).ToArrayAsync(ct);
        if (policies.Length > 1024) return (null, null);
        var eligible = policies.Where(q => RegulatoryQualificationEndpoints.Content(q).Eligibility?.PositionId == position.Id &&
            RegulatoryQualificationEndpoints.Content(q).Inputs?.Any(i => i.Field == "crew.requiredRoleEligible" && i.Basis == "HumanAttested") == true).ToArray();
        if (eligible.Length != 1 || !TugOwnedCapacityConsumer.CurrentQualifier(settings, eligible[0], start < now ? start : now, horizon)) return (null, null);
        var q = eligible[0];
        var rule = await db.RuleRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == q.RuleId && r.Revision == q.RuleRevision, ct);
        if (rule == null || (await CurrentRegulatoryContext.Qualification(db, rule, ct))?.Hash != q.Hash) return (null, null);
        var lifecycle = ApplicabilityEngine.Parse(rule.RuleJson!).Lifecycle;
        var earliest = DateOnly.FromDateTime((start < now ? start : now).UtcDateTime);
        var last = DateOnly.FromDateTime(horizon.UtcDateTime);
        if (!ApplicabilityEngine.Date(lifecycle.EffectiveFrom, out var effectiveFrom) || earliest < effectiveFrom ||
            lifecycle.EffectiveTo != null && (!ApplicabilityEngine.Date(lifecycle.EffectiveTo, out var effectiveTo) || last > effectiveTo)) return (null, null);
        var assessments = await OperationEquipmentCommands.Visible(db, actor, db.ControlEvidenceRevisions.AsNoTracking())
            .Where(r => r.VesselId == tug && r.Kind == "Evidence" &&
                db.ControlEvidenceHeads.Any(h => h.VesselId == tug && h.Kind == r.Kind && h.Id == r.Id && h.Revision == r.Revision) &&
                db.OperationEquipmentBindings.Any(b => b.VesselId == tug && b.Id == r.Id && b.Revision == r.Revision && b.DocumentKind == "CrewRoleAssessment" &&
                    b.AssignmentId == assignment.Id && b.AssignmentRevision == assignment.Revision && b.PositionId == position.Id)).OrderBy(r => r.Id).Take(33).ToArrayAsync(ct);
        if (assessments.Length == 0 || assessments.Length > 32) return (null, null);
        if (assessments.Any(r => JsonSerializer.Deserialize<ControlEvidenceContent>(r.ContentJson, RegulatoryQualificationEndpoints.Json)?.CrewRoleAssessment?.Credential != null) &&
            (files == null || !files.Allows(actor, false) || !files.Kinds.Contains("CertificateDocument"))) return (null, null);
        var refs = new OwningInputReferences(MsmdId: msmd, RoleAssessmentIds: assessments.Select(r => r.Id).ToArray());
        if (!await OperationEquipmentContext.Authorized(db, actor, tug, refs, ct)) return (null, null);
        var policy = RegulatoryQualificationEndpoints.Content(q).Eligibility;
        var current = await QualifiedRoleCriteriaReader.Read(db, settings, tug, policy, refs, now, ct);
        var beginning = await QualifiedRoleCriteriaReader.Read(db, settings, tug, policy, refs, start, ct);
        var future = await QualifiedRoleCriteriaReader.Read(db, settings, tug, policy, refs, horizon, ct);
        if (current == null || beginning == null || future == null || current.Value != future.Value || current.Value != beginning.Value ||
            !current.Hashes.SequenceEqual(future.Hashes) || !current.Hashes.SequenceEqual(beginning.Hashes) || current.Subject.AssignmentId != assignment.Id ||
            current.Subject.AssignmentRevision != assignment.Revision || current.Subject.AssignmentVersion != assignment.Version ||
            current.Subject.PositionId != position.Id || current.Subject.PositionVersion != position.Version || current.Subject.From == null || current.Subject.To == null ||
            current.Subject.From > fromDate || current.Subject.To <= toDate || current.Until <= horizon) return (null, null);
        var until = new[] { current.Until, settings.Memberships.Single(m => m.TenantId == q.TenantId && m.ActorId == q.ActorId)
            .RegulatoryAuthorities.Single(g => g.Id == q.AuthorityId).ValidUntil }.Min();
        var input = RegulatoryQualificationEndpoints.Content(q).Inputs!.Single(i => i.Field == "crew.requiredRoleEligible");
        if (input.MaxAgeHours != null) until = new[] { until, current.ObservedAt.AddHours(input.MaxAgeHours.Value) }.Min();
        if (lifecycle.EffectiveTo != null && ApplicabilityEngine.Date(lifecycle.EffectiveTo, out var untilDate) && untilDate < DateOnly.MaxValue)
            until = new[] { until, new DateTimeOffset(untilDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) }.Min();
        return until <= horizon ? (null, null) : (current.Value, until);
    }
}
