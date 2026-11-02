using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class ComplianceInputContext
{
    // These references expose protected assessment existence, so require the
    // whole protected family before even finding/counting a candidate.
    internal static readonly string[] CrewPermissions = ["crew.read", "crew.identity.read", "crew.certificate.read", "crew.profile.medical.read", "crew.assessment.read", "crew.documents.read"];
    internal static bool CrewRead(TrustedActor a) => CrewPermissions.All(a.Permissions.Contains);
    internal static Guid[] DocumentIds(OwningInputReferences r) => new[] { r.OperationId, r.CargoPlanId, r.WinchFitmentId, r.ClassPackId, r.MsmdId, r.SourceEvidenceId }
        .Where(x => x != null).Select(x => x!.Value).Concat(r.RoleAssessmentIds ?? []).Distinct().ToArray();
    internal static async Task<bool> Authorized(PoseidonDbContext db, TrustedActor a, Guid vessel, OwningInputReferences r, CancellationToken ct, HashSet<Guid>? upstreamPath) {
        if (r.RoleAssessmentIds is { } roles && (roles.Length > 32 || roles.Contains(Guid.Empty) || roles.Distinct().Count() != roles.Length || !CrewRead(a))) return false;
        if (r.SourceEvidenceId is { } id && (id == Guid.Empty || !await OperationEquipmentCommands.Visible(db, a, db.ControlEvidenceRevisions.AsNoTracking()).AnyAsync(e => e.VesselId == vessel && e.Kind == "Evidence" && e.Id == id &&
            db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == e.Kind && h.Id == e.Id && h.Revision == e.Revision) &&
            (e.FileId == null || db.FileStages.Any(f => f.Id == e.FileId && f.VesselId == vessel && f.PersonId == null && f.DocumentId == null && f.State == "CleanTransport")), ct))) return false;
        if (r.Ism is { } ism) {
            var path = upstreamPath == null ? new HashSet<Guid>() : new HashSet<Guid>(upstreamPath);
            if (ism.EvaluationId == Guid.Empty || ism.RuleId == Guid.Empty || ism.RuleRevision <= 0 || path.Count >= 16 || !path.Add(ism.EvaluationId)) return false;
            var upstream = await OperationEquipmentCommands.Evaluations(db, a).SingleOrDefaultAsync(e => e.Id == ism.EvaluationId && e.VesselId == vessel, ct);
            if (upstream == null) return false;
            using var snapshot = JsonDocument.Parse(upstream.SnapshotJson);
            if (snapshot.RootElement.TryGetProperty("owningInputs", out var owning) && owning.ValueKind == JsonValueKind.Object &&
                !await OperationEquipmentContext.Authorized(db, a, vessel, owning.Deserialize<OwningInputReferences>(RegulatoryQualificationEndpoints.Json), ct, path)) return false;
        }
        return true;
    }
    internal static async Task<bool> AssignmentCurrent(PoseidonDbContext db, Guid vessel, CrewRoleAssessmentReference r, DateTimeOffset now, CancellationToken ct) {
        var a = await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.VesselId == vessel && a.Id == r.AssignmentId && a.Revision == r.AssignmentRevision && a.Version == r.AssignmentVersion && a.PositionId == r.PositionId, ct);
        var p = await db.CrewPositions.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.Id == r.PositionId && p.Version == r.PositionVersion, ct);
        if (a == null || p == null) return false;
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        if (r.Credential is { } credential) {
            var version = db.QualifiedInputs?.DocumentPolicyVersion; var purpose = db.QualifiedInputs?.DocumentPurpose;
            if (version == null) return false;
            var record = await db.Credentials.AsNoTracking().SingleOrDefaultAsync(c => c.VesselId == vessel && c.Id == credential.Id && c.AssignmentId == r.AssignmentId && c.PersonId == a.PersonId && c.Revision == credential.Revision && c.Version == credential.Version && c.IssueDate != null && c.IssueDate <= r.From && c.ExpiryDate != null && c.ExpiryDate >= r.To, ct);
            if (record?.DocumentReference == null) return false;
            var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f => f.VesselId == vessel && f.Id == record.DocumentReference && db.FileStages.Any(s => s.Id == f.Id && s.VesselId == vessel && s.PersonId == a.PersonId && s.PersonalKind == "CertificateDocument" && s.PersonalPolicyVersion == version && s.PersonalPurpose == purpose && s.State == "CleanTransport"), ct);
            if (file == null || !await db.QualifiedInputs!.FileIntact(file, ct)) return false;
        }
        return a != null && p != null && r.From <= date && date < r.To && a.From <= r.From && (a.To == null || a.To >= r.To) &&
            db.QualifiedInputs != null && await db.QualifiedInputs.AssignmentPolicyCurrent(db, a, p, now, ct);
    }
    internal static async Task<bool?> ManningCount(PoseidonDbContext db, Guid vessel, MsmdReference msmd, CrewRoleAssessmentReference subject, DateTimeOffset now, CancellationToken ct) {
        var requirement = msmd.Roles?.SingleOrDefault(r => r.PositionId == subject.PositionId);
        if (requirement == null || db.QualifiedInputs == null) return null;
        var position = await db.CrewPositions.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel && p.Id == subject.PositionId, ct);
        if (position == null) return null;
        var rows = await db.CrewAssignments.AsNoTracking().Where(a => a.VesselId == vessel && a.PositionId == subject.PositionId && a.From <= subject.From && (a.To == null || a.To >= subject.To)).Take(1001).ToArrayAsync(ct);
        if (rows.Length > 1000) return null;
        var current = new HashSet<Guid>();
        foreach (var row in rows) if (await db.QualifiedInputs.AssignmentPolicyCurrent(db, row, position, now, ct)) current.Add(row.PersonId);
        return current.Count >= requirement.Count;
    }
    internal static async Task<object[]> Provenance(PoseidonDbContext db, Guid vessel, OwningInputReferences refs, CancellationToken ct, Guid? evaluation) {
        var result = new List<object>();
        if (refs.SourceEvidenceId is { } source) {
            var row = await db.ControlEvidenceRevisions.AsNoTracking().SingleAsync(e => e.VesselId == vessel && e.Kind == "Evidence" && e.Id == source && db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == e.Kind && h.Id == e.Id && h.Revision == e.Revision), ct);
            var reviews = await db.HumanReviewEvents.AsNoTracking().Where(e => e.VesselId == vessel && e.Kind == "Verification" && e.EvidenceId == source && db.HumanReviewHeads.Any(h => h.VesselId == vessel && h.Id == e.Id && h.Revision == e.Revision)).OrderBy(e => e.Id).Select(e => new { e.Id, e.Revision, e.State, e.ContextHash, e.AuthorityHash }).ToArrayAsync(ct);
            result.Add(new { sourceEvidence = row.Id, row.Revision, row.Hash, reviews });
            if (evaluation != null && !db.EvaluationOwningReferences.Local.Any(p => p.EvaluationId == evaluation && p.Id == row.Id)) db.EvaluationOwningReferences.Add(new() { TenantId = row.TenantId, VesselId = vessel, EvaluationId = evaluation.Value, Id = row.Id, Revision = row.Revision });
        }
        if (refs.Ism is { } ism) {
            var row = await db.ApplicabilityEvaluations.AsNoTracking().SingleAsync(e => e.Id == ism.EvaluationId && e.VesselId == vessel, ct);
            result.Add(new { upstream = row.Id, row.SnapshotHash, ism.RuleId, ism.RuleRevision });
            if (evaluation != null) {
                foreach (var pin in await db.EvaluationOwningReferences.AsNoTracking().Where(p => p.EvaluationId == row.Id && p.VesselId == vessel).ToArrayAsync(ct))
                    if (!db.EvaluationOwningReferences.Local.Any(p => p.EvaluationId == evaluation && p.Id == pin.Id)) db.EvaluationOwningReferences.Add(new() { TenantId = row.TenantId, VesselId = vessel, EvaluationId = evaluation.Value, Id = pin.Id, Revision = pin.Revision });
            }
            if (evaluation != null) db.EvaluationUpstreamReferences.Add(new() { TenantId = row.TenantId, VesselId = vessel, EvaluationId = evaluation.Value, UpstreamId = row.Id, RuleId = ism.RuleId, RuleRevision = ism.RuleRevision });
        }
        foreach (var id in refs.RoleAssessmentIds ?? []) {
            var binding = await db.OperationEquipmentBindings.AsNoTracking().SingleAsync(b => b.VesselId == vessel && b.Id == id && db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == b.Kind && h.Id == b.Id && h.Revision == b.Revision), ct);
            var assignment = await db.CrewAssignments.AsNoTracking().Where(a => a.VesselId == vessel && a.Id == binding.AssignmentId).Select(a => new { a.Id, a.Revision, a.Version, a.PositionId, a.AssignmentPolicyFingerprint, a.PositionPolicyFingerprint }).SingleOrDefaultAsync(ct);
            var position = await db.CrewPositions.AsNoTracking().Where(p => p.VesselId == vessel && p.Id == binding.PositionId).Select(p => new { p.Id, p.Version }).SingleOrDefaultAsync(ct);
            var credentials = await db.Credentials.AsNoTracking().Where(c => c.VesselId == vessel && c.AssignmentId == binding.AssignmentId).OrderBy(c => c.Id).Select(c => new { c.Id, c.Revision, c.Version }).ToArrayAsync(ct);
            var assignmentSet = await db.CrewAssignments.AsNoTracking().Where(a => a.VesselId == vessel && a.PositionId == binding.PositionId).OrderBy(a => a.Id).Select(a => new { a.Id, a.Version, a.Revision }).ToArrayAsync(ct);
            result.Add(new { assessment = id, assignment, position, assignmentSetHash = RegisterStorage.Hash(JsonSerializer.Serialize(assignmentSet)), credentialSetHash = RegisterStorage.Hash(JsonSerializer.Serialize(credentials)), policy = db.QualifiedInputs == null ? null : await db.QualifiedInputs.PolicyFingerprint(db, vessel, DateTimeOffset.UtcNow, ct) });
        }
        return result.ToArray();
    }
    internal static async Task<(bool Value, string Hash, DateTimeOffset Observed, DateTimeOffset Until)?> Ism(PoseidonDbContext db, FederationSettings settings, Guid vessel, RegulatoryQualificationEvent q,
        EvaluationInputReference? input, DateTimeOffset now, CancellationToken ct, HashSet<Guid>? ancestors) {
        var policy = RegulatoryQualificationEndpoints.Content(q).IsmDependency;
        if (input == null || policy == null || policy.RuleId != input.RuleId || policy.RuleRevision != input.RuleRevision || input.RuleId == q.RuleId) return null;
        var row = await db.ApplicabilityEvaluations.AsNoTracking().SingleOrDefaultAsync(e => e.VesselId == vessel && e.Id == input.EvaluationId && e.AsOf <= now, ct);
        if (row == null || (await CurrentRegulatoryContext.Stale(db, row.Id, vessel, now, ct, settings, ancestors)).Length != 0) return null;
        var dto = JsonSerializer.Deserialize<EvaluationDto>(row.ResultJson, RegulatoryQualificationEndpoints.Json)!;
        var prediction = dto.Results.SingleOrDefault(r => r.RuleId == input.RuleId && r.Revision == input.RuleRevision)?.Prediction;
        if (!dto.Authoritative || dto.Conflicts.Length != 0 || prediction?.Authoritative != true || prediction.Result is not ("APPLICABLE" or "NOT_APPLICABLE")) return null;
        var qualification = await db.RegulatoryQualificationEvents.AsNoTracking().SingleOrDefaultAsync(e => e.RuleId == input.RuleId && db.RegulatoryQualificationHeads.Any(h => h.RuleId == e.RuleId && h.Revision == e.Revision), ct);
        if (qualification == null || !TugOwnedCapacityConsumer.CurrentQualifier(settings, qualification, now, now)) return null;
        var until = settings.Memberships.Single(m => m.TenantId == q.TenantId && m.ActorId == qualification.ActorId).RegulatoryAuthorities.Single(g => g.Id == qualification.AuthorityId).ValidUntil;
        return (prediction.Result == "APPLICABLE", RegisterStorage.Hash(row.SnapshotHash + ":" + qualification.Hash), row.AsOf, until);
    }
}
