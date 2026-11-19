using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Server implementation of ICrewEligibilityEvaluator. Reads only tenant/vessel-scoped
// rows through the query-filtered context; never IgnoreQueryFilters, never writes.
public sealed class CrewEligibilityEvaluator(PoseidonDbContext db, FederationSettings settings, CrewAssignmentPolicy declarations,
    ICrewWorkRestSource workRest, TimeProvider time) : ICrewEligibilityEvaluator
{
    private const int MaxMembers = 500;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static bool CanEvaluate(TrustedActor? a) => a != null && a.CanRead && a.Permissions.Contains("crew.read") &&
        (a.Permissions.Contains("crew.eligibility.read") || a.Permissions.Contains("crew.safe_manning.read"));

    private static CrewEligibilityResult Withheld(Guid assignment, string code) =>
        new(assignment, null, CrewEligibilityStates.ReviewRequired, [new(code, "ACCESS", CrewEligibilityStates.ReviewRequired)], [], CrewEligibilityPolicy.Qualifications);

    private async Task<bool> InScope(TrustedActor actor, Guid vessel, CancellationToken ct) =>
        CanEvaluate(actor) && db.TenantId == actor.TenantId && await actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == vessel, ct);

    public async Task<CrewEligibilityResult> EvaluateAssignmentAsync(TrustedActor actor, Guid vesselId, Guid assignmentId, CrewEligibilityWindow window, CancellationToken ct)
    {
        if (!await InScope(actor, vesselId, ct)) return Withheld(assignmentId, "PROTECTED_CREW_SOURCE_ACCESS_REQUIRED");
        var assignment = await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.VesselId == vesselId && a.Id == assignmentId, ct);
        if (assignment == null) return Withheld(assignmentId, "PROTECTED_CREW_SOURCE_ACCESS_REQUIRED");
        return await CoreAsync(assignment, window, time.GetUtcNow(), ct);
    }

    public async Task<SafeManningEvaluation> EvaluateVesselAsync(TrustedActor actor, Guid vesselId, CrewEligibilityWindow window, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        SafeManningEvaluation Result(string state, CrewEligibilityReason[] reasons, string msmd, Guid? msmdId, long? msmdRevision, ManningRoleCoverage[] roles, CrewEligibilityResult[] members) {
            var hash = RegisterStorage.Hash(JsonSerializer.Serialize(new { vesselId, state, reasons, msmd, msmdId, msmdRevision, roles,
                members = members.Select(m => new { m.AssignmentId, m.PositionId, m.State, m.Reasons }), window }, Json));
            return new(vesselId, state, reasons, msmd, msmdId, msmdRevision, roles, members, window, now, hash, SafeManningPolicy.Qualifications);
        }
        if (!await InScope(actor, vesselId, ct))
            return Result(CrewEligibilityStates.ReviewRequired, [new("PROTECTED_CREW_SOURCE_ACCESS_REQUIRED", "ACCESS", CrewEligibilityStates.ReviewRequired)], "ACCESS_REQUIRED", null, null, [], []);
        if (!window.Valid)
            return Result(CrewEligibilityStates.ReviewRequired, [new("EVALUATION_WINDOW_INVALID", "ASSIGNMENT", CrewEligibilityStates.ReviewRequired)], "NOT_EVALUATED", null, null, [], []);
        var assignments = await db.CrewAssignments.AsNoTracking().Where(a => a.VesselId == vesselId &&
                (a.From == null || a.From <= window.To) && (a.To == null || a.To > window.From))
            .OrderBy(a => a.Id).Take(MaxMembers + 1).ToArrayAsync(ct);
        if (assignments.Length > MaxMembers)
            return Result(CrewEligibilityStates.ReviewRequired, [new("CREW_PROJECTION_LIMIT_EXCEEDED", "CREW", CrewEligibilityStates.ReviewRequired)], "NOT_EVALUATED", null, null, [], []);
        var members = new List<CrewEligibilityResult>();
        foreach (var assignment in assignments) members.Add(await CoreAsync(assignment, window, now, ct));
        var msmd = await Msmd(actor, vesselId, window, now, ct);
        var (state, reasons, roles) = SafeManningPolicy.Evaluate(msmd.State, msmd.Roles, members.ToArray());
        return Result(state, reasons, msmd.State, msmd.Id, msmd.Revision, roles, members.ToArray());
    }

    // Caller has already authorized vessel scope and the owning read permission.
    internal async Task<CrewEligibilityResult> CoreAsync(CrewAssignment assignment, CrewEligibilityWindow window, DateTimeOffset now, CancellationToken ct)
    {
        var vessel = assignment.VesselId;
        var date = declarations.ReferenceDate(now) ?? DateOnly.FromDateTime(now.UtcDateTime);
        var presence = CrewRosterPresencePolicy.Evaluate(assignment.Status, assignment.Provenance, assignment.From, assignment.To, assignment.SourceObservedAt,
            date, now, declarations.Configured,
            assignment.AssignmentPolicyVersion == declarations.Version && assignment.AssignmentPolicyFingerprint == declarations.Fingerprint(),
            declarations.EffectiveStatuses, declarations.PlannedStatuses, declarations.Provenances, declarations.RequireSourceObservation);
        var requirements = assignment.PositionId is { } position
            ? await db.CrewCompetenceRequirements.AsNoTracking().Where(r => r.VesselId == vessel && r.PositionId == position && r.Status == "ACTIVE")
                .OrderBy(r => r.Id).Take(201).ToArrayAsync(ct) : [];
        var credentials = await db.Credentials.AsNoTracking().Where(c => c.VesselId == vessel && c.AssignmentId == assignment.Id && c.PersonId == assignment.PersonId)
            .OrderBy(c => c.Id).Take(101).ToArrayAsync(ct);
        var credentialIds = credentials.Select(c => c.Id).ToArray();
        var assertions = (await db.CrewCredentialAssertions.AsNoTracking().Where(a => a.VesselId == vessel && credentialIds.Contains(a.CredentialId)).ToArrayAsync(ct))
            .GroupBy(a => a.CredentialId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Sequence).First());
        var records = await db.CrewCompetenceRecords.AsNoTracking().Where(r => r.VesselId == vessel && r.AssignmentId == assignment.Id)
            .OrderBy(r => r.Id).Take(201).ToArrayAsync(ct);
        var recordIds = records.Select(r => r.Id).ToArray();
        var signOffs = (await db.CrewCompetenceSignOffs.AsNoTracking().Where(s => s.VesselId == vessel && recordIds.Contains(s.RecordId)).ToArrayAsync(ct))
            .GroupBy(s => s.RecordId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Sequence).First());
        var rest = await workRest.ReadAsync(vessel, assignment.Id, window, ct);
        var inputs = new CrewEligibilityInputs(assignment.Id, assignment.PositionId, presence.State, window, assignment.From, assignment.To,
            requirements.Length > 200 ? [] : requirements.Select(r => new CrewRequirementFact(r.Id, r.RequirementType, r.Reference, r.Mandatory, r.ValidityMonths,
                r.VesselTypeScope, r.CargoScope, r.CompetenceLinkId, r.FamiliarizationLinkId)).ToArray(),
            credentials.Take(100).Select(c => {
                var a = assertions.GetValueOrDefault(c.Id);
                var current = a != null && a.CredentialRevision == c.Revision &&
                    CrewAuthorityResolver.StillValid(settings, c.TenantId, a.ActorId, a.AuthorityId, a.RoleId, CrewAuthorityResolver.CredentialVerification, vessel, now);
                return new CrewCredentialFact(c.Id, c.CertificateTypeReference, c.ExpiryDate, c.EndorsementRequired, c.EndorsementExpiry, a?.State, current);
            }).ToArray(),
            records.Take(200).Select(r => {
                var s = signOffs.GetValueOrDefault(r.Id);
                var current = s != null && s.RecordRevision == r.Revision &&
                    CrewAuthorityResolver.StillValid(settings, r.TenantId, s.ActorId, s.AuthorityId, s.RoleId, CrewAuthorityResolver.CompetenceAssessment, vessel, now);
                return new CrewCompetenceFact(r.Id, r.RequirementId, r.Kind, r.CompletedOn, r.ValidUntil, s?.Outcome, current);
            }).ToArray(), rest);
        var result = CrewEligibilityPolicy.Evaluate(inputs);
        // Truncated inputs can never be complete evidence.
        if (requirements.Length > 200 || credentials.Length > 100 || records.Length > 200)
            result = result with { State = result.State == CrewEligibilityStates.NotEligible ? result.State : CrewEligibilityStates.ReviewRequired,
                Reasons = [.. result.Reasons, new("CREW_INPUT_LIMIT_EXCEEDED", "CREW", CrewEligibilityStates.ReviewRequired)] };
        return result;
    }

    private async Task<(string State, MsmdRoleRequirement[]? Roles, Guid? Id, long? Revision)> Msmd(TrustedActor actor, Guid vessel, CrewEligibilityWindow window, DateTimeOffset now, CancellationToken ct)
    {
        if (!OperationEquipmentCommands.Read(actor)) return ("SOURCE_ACCESS_REQUIRED", null, null, null);
        var documents = await OperationEquipmentCommands.Visible(db, actor, db.ControlEvidenceRevisions.AsNoTracking())
            .Where(r => r.VesselId == vessel && r.Kind == "Evidence" && (r.Lifecycle == "SUBMITTED" || r.Lifecycle == "COMPLETED") &&
                db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == r.Kind && h.Id == r.Id && h.Revision == r.Revision) &&
                db.OperationEquipmentBindings.Any(b => b.VesselId == vessel && b.Id == r.Id && b.Revision == r.Revision && b.DocumentKind == "Msmd"))
            .OrderBy(r => r.Id).Take(129).ToArrayAsync(ct);
        var complete = documents.Where(r => {
            var c = JsonSerializer.Deserialize<ControlEvidenceContent>(r.ContentJson, RegulatoryQualificationEndpoints.Json)!;
            return c.Msmd != null && ComplianceInputs.Complete(c) && c.ValidFrom <= now && c.ValidUntil > now;
        }).ToArray();
        if (documents.Length > 128) return ("REFERENCES_AMBIGUOUS", null, null, null);
        if (complete.Length != 1) return (complete.Length == 0 ? "SOURCE_QUALIFIED_MSMD_REQUIRED" : "REFERENCES_AMBIGUOUS", null, null, null);
        var document = await QualifiedOwningDocumentReader.Read(db, settings, vessel, complete[0].Id, "Msmd", now, ct);
        if (document == null) return ("NOT_SOURCE_QUALIFIED", null, null, null);
        var windowEnd = new DateTimeOffset(window.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        if (document.Value.Until <= windowEnd) return ("DOES_NOT_COVER_WINDOW", null, document.Value.Row.Id, document.Value.Row.Revision);
        return (SafeManningPolicy.MsmdCurrent, document.Value.Content.Msmd!.Roles, document.Value.Row.Id, document.Value.Row.Revision);
    }
}
