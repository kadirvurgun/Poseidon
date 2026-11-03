using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Shared current source/file/control/independent-verification qualification path.
// Callers authorize the owning object/module before looking up its references.
internal static class QualifiedOwningDocumentReader
{
    internal static async Task<(ControlEvidenceRevision Row, ControlEvidenceContent Content, string Hash, DateTimeOffset Until)?> Read(
        PoseidonDbContext db, FederationSettings settings, Guid vessel, Guid? id, string? kind,
        DateTimeOffset now, CancellationToken ct, HashSet<Guid>? ancestors = null)
    {
        if (id == null) return null;
        var row = await db.ControlEvidenceRevisions.AsNoTracking().SingleOrDefaultAsync(e => e.VesselId == vessel && e.Kind == "Evidence" && e.Id == id &&
            db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == e.Kind && h.Id == e.Id && h.Revision == e.Revision), ct);
        if (row == null || row.SavedAt > now || row.Lifecycle is not ("SUBMITTED" or "COMPLETED") || row.FileId == null ||
            kind != null && !await db.OperationEquipmentBindings.AnyAsync(b => b.VesselId == vessel && b.Id == row.Id && b.Revision == row.Revision && b.DocumentKind == kind, ct)) return null;
        var content = JsonSerializer.Deserialize<ControlEvidenceContent>(row.ContentJson, RegulatoryQualificationEndpoints.Json)!;
        if (kind != null && !OperationEquipmentInputs.Complete(content) || content.ObservedAt == null || content.ObservedAt > now || content.ValidFrom == null || content.ValidFrom > now ||
            content.ValidUntil == null || content.ValidUntil <= now || content.FreshUntil == null || content.FreshUntil <= now ||
            !await db.StoredFiles.AnyAsync(f => f.Id == row.FileId && f.VesselId == vessel && f.Revision == row.FileRevision && f.Checksum == row.Checksum &&
                db.FileStages.Any(s => s.Id == f.Id && s.VesselId == vessel && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport"), ct)) return null;
        if (kind == null || ComplianceInputs.Kinds.Contains(kind)) {
            var file = await db.StoredFiles.AsNoTracking().SingleAsync(f => f.Id == row.FileId && f.VesselId == vessel && f.Revision == row.FileRevision, ct);
            if (db.QualifiedInputs == null || !await db.QualifiedInputs.FileIntact(file, ct)) return null;
        }
        // Referencing an ID does not let a caller choose one of contradictory
        // current owning records. Drafts remain legal; withdrawal/revision is
        // an ordinary evidence command, never an automatic safety decision.
        if (kind != null) {
        var ownerJob = content.Operation?.JobId ?? content.ServiceAuthorization?.JobId;
        var candidates = await db.ControlEvidenceRevisions.AsNoTracking().Where(e => e.VesselId == vessel && e.Kind == "Evidence" && (e.Lifecycle == "SUBMITTED" || e.Lifecycle == "COMPLETED") &&
            db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == e.Kind && h.Id == e.Id && h.Revision == e.Revision) &&
            db.OperationEquipmentBindings.Any(b => b.VesselId == vessel && b.Id == e.Id && b.Revision == e.Revision && b.DocumentKind == kind && b.JobId == ownerJob)).Take(129).ToArrayAsync(ct);
        if (candidates.Length > 128 || candidates.Count(e => {
            var c = JsonSerializer.Deserialize<ControlEvidenceContent>(e.ContentJson, RegulatoryQualificationEndpoints.Json)!;
            return OperationEquipmentInputs.Complete(c) && c.ValidFrom <= now && c.ValidUntil > now &&
                (content.CrewRoleAssessment == null || c.CrewRoleAssessment?.AssignmentId == content.CrewRoleAssessment.AssignmentId && c.CrewRoleAssessment?.Criterion == content.CrewRoleAssessment.Criterion);
        }) != 1) return null;
        // The exact recorded owner revisions must still be heads. Plan geometry alone supplies no facts.
        var binding = await db.OperationEquipmentBindings.AsNoTracking().SingleAsync(b => b.VesselId == vessel && b.Id == row.Id && b.Revision == row.Revision, ct);
        var expected = OperationEquipmentBinding.From(row.TenantId, vessel, row.Id, row.Revision, content);
        if (binding.JobId != expected.JobId || binding.JobRevision != expected.JobRevision || binding.PlanRevision != expected.PlanRevision ||
            binding.AssetId != expected.AssetId || binding.AssetRevision != expected.AssetRevision || binding.ServiceId != expected.ServiceId || binding.ServiceRevision != expected.ServiceRevision || binding.AssignmentId != expected.AssignmentId || binding.AssignmentRevision != expected.AssignmentRevision || binding.PositionId != expected.PositionId || binding.CredentialId != expected.CredentialId || binding.CredentialRevision != expected.CredentialRevision) return null;
        var jobVersion = content.Operation?.JobVersion ?? content.ServiceAuthorization?.JobVersion;
        var planVersion = content.Operation?.PlanVersion ?? content.ServiceAuthorization?.PlanVersion;
        if (binding.JobId != null && (!await db.TugJobs.AnyAsync(j => j.VesselId == vessel && j.Id == binding.JobId && j.Revision == binding.JobRevision && j.Version == jobVersion, ct) ||
            !await db.TugDraftPlans.AnyAsync(p => p.VesselId == vessel && p.JobId == binding.JobId && p.Revision == binding.PlanRevision && p.Version == planVersion && p.RequestRevision == binding.JobRevision, ct))) return null;
        if (content.WinchFitment is { } w && !await db.Assets.AnyAsync(a => a.VesselId == vessel && a.Id == w.AssetId && a.Revision == w.AssetRevision && a.Version == w.AssetVersion, ct)) return null;
        if (content.CrewRoleAssessment is { } role && !await ComplianceInputContext.AssignmentCurrent(db, vessel, role, now, ct)) return null;
        }
        var verification = await db.HumanReviewEvents.AsNoTracking().Where(e => e.VesselId == vessel && e.Kind == "Verification" && e.EvidenceId == row.Id && e.EvidenceRevision == row.Revision &&
            db.HumanReviewHeads.Any(h => h.VesselId == vessel && h.Id == e.Id && h.Revision == e.Revision)).OrderByDescending(e => e.SavedAt).ThenBy(e => e.Id).FirstOrDefaultAsync(ct);
        if (verification?.State != "VERIFIED" || verification.ActorId == row.ActorId || verification.ValidUntil == null || verification.ValidUntil <= now || verification.EvaluationId == null ||
            verification.EvaluationId is { } inputEvaluation && await db.EvaluationOwningReferences.AnyAsync(p => p.EvaluationId == inputEvaluation && p.Id == row.Id, ct) ||
            (content.Operation?.JobId ?? content.ServiceAuthorization?.JobId) is { } owningJob && verification.OperationId != owningJob ||
            !await db.ControlEvidenceHeads.AnyAsync(h => h.VesselId == vessel && h.Kind == "Control" && h.Id == verification.ControlId && h.Revision == verification.ControlRevision, ct) ||
            !OperationEquipmentContext.CurrentVerifier(settings, verification, kind ?? "Evidence", now) || (await CurrentRegulatoryContext.Stale(db, verification.EvaluationId.Value, vessel, now, ct, settings, ancestors)).Length != 0) return null;
        var requirements = await db.ControlEvidenceRequirements.AsNoTracking().Where(p => p.VesselId == vessel && p.Kind == "Evidence" && p.Id == row.Id && p.Revision == row.Revision).ToArrayAsync(ct);
        if (requirements.Length == 0) return null;
        var controlRow = await db.ControlEvidenceRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Kind == "Control" && x.Id == verification.ControlId && x.Revision == verification.ControlRevision, ct);
        if (controlRow == null ||
            !await db.ControlEvidenceEdges.AnyAsync(x => x.VesselId == vessel && x.Kind == "Evidence" && x.Id == row.Id && x.Revision == row.Revision && x.TargetKind == "Control" && x.TargetId == verification.ControlId && x.TargetRevision == verification.ControlRevision, ct)) return null;
        var evaluated = await db.ApplicabilityEvaluations.AsNoTracking().SingleAsync(x => x.Id == verification.EvaluationId && x.VesselId == vessel, ct);
        var outcome = JsonSerializer.Deserialize<EvaluationDto>(evaluated.ResultJson, RegulatoryQualificationEndpoints.Json)!;
        if (!outcome.Authoritative || outcome.Conflicts.Length != 0 || requirements.Any(p => !outcome.Results.Any(r => r.RuleId == p.RuleId && r.Revision == p.RuleRevision && r.Prediction.Authoritative && r.Prediction.Result == "APPLICABLE"))) return null;
        var sourcePins = new List<string>(); var until = new List<DateTimeOffset> { content.ValidUntil.Value, content.FreshUntil.Value, verification.ValidUntil.Value };
        until.Add(settings.Memberships.Single(m => m.TenantId == row.TenantId && m.ActorId == verification.ActorId).HumanAuthorities.Single(g => g.Id == verification.AuthorityId).ValidUntil);
        foreach (var pin in requirements) {
            var rule = await db.RuleRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == pin.RuleId && r.Revision == pin.RuleRevision, ct);
            if (rule == null || !await db.RuleDrafts.AnyAsync(r => r.Id == rule.Id && r.Revision == rule.Revision, ct)) return null;
            var q = await CurrentRegulatoryContext.Qualification(db, rule, ct);
            if (q?.State != "IN_FORCE" || !TugOwnedCapacityConsumer.CurrentQualifier(settings, q, now, now)) return null;
            until.Add(settings.Memberships.Single(m => m.TenantId == row.TenantId && m.ActorId == q.ActorId).RegulatoryAuthorities.Single(g => g.Id == q.AuthorityId).ValidUntil);
            var control = RegulatoryQualificationEndpoints.Content(q).Control;
            var owningPolicy = RegulatoryQualificationEndpoints.Content(q).OwningDocuments?.SingleOrDefault(p => p.DocumentKind == kind);
            if (kind != null && (owningPolicy == null || owningPolicy.VerifierRole != verification.RoleId || owningPolicy.AuthorRole != content.OwnerRole ||
                row.RoleContext?.Split(',').Contains(owningPolicy.AuthorRole.GetValueOrDefault().ToString("D")) != true)) return null;
            if (control == null || control.RequiredVerificationMechanism != null || control.MaxAgeHours != null && content.ObservedAt.Value.AddHours(control.MaxAgeHours.Value) <= now ||
                control.RequireAcknowledgment == true && content.Acknowledgments?.Any(a => a.RuleId == q.RuleId && a.SourceHash == q.SourceHash && a.QualificationHash == q.Hash) != true) return null;
            var lifecycle = ApplicabilityEngine.Parse(rule.RuleJson!).Lifecycle;
            if (!ApplicabilityEngine.Date(lifecycle.EffectiveFrom, out var from) || DateOnly.FromDateTime(now.UtcDateTime) < from ||
                lifecycle.EffectiveTo != null && ApplicabilityEngine.Date(lifecycle.EffectiveTo, out var to) && DateOnly.FromDateTime(now.UtcDateTime) > to) return null;
            if (lifecycle.EffectiveTo != null && ApplicabilityEngine.Date(lifecycle.EffectiveTo, out var end) && end < DateOnly.MaxValue) until.Add(new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));
            if (control.MaxAgeHours != null) until.Add(content.ObservedAt.Value.AddHours(control.MaxAgeHours.Value));
            sourcePins.Add(q.Hash);
        }
        return (row, content, RegisterStorage.Hash(JsonSerializer.Serialize(new { row.Hash, row.Revision, verification.Id, verificationRevision = verification.Revision, verification.ContextHash, verification.AuthorityHash, sources = sourcePins })), until.Min());
    }
}
