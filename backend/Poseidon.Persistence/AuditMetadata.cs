using Poseidon.Domain;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// A deliberately closed projection. Never serialize an entity, command, identity claim or ContentJson.
internal sealed record AuditMetadata(Guid? VesselId, Guid? ResourceId, string? Kind,
    long? BeforeRevision, long? AfterRevision, string Summary)
{
    internal static AuditMetadata From(EntityEntry entry)
    {
        Guid? vessel = entry.Entity switch {
            HumanReviewHead e => e.VesselId, HumanReviewEvent e => e.VesselId, HumanReviewReceipt e => e.VesselId,
            ControlEvidenceHead e => e.VesselId, ControlEvidenceRevision e => e.VesselId, ControlEvidenceRequirement e => e.VesselId, ControlEvidenceEdge e => e.VesselId, ControlEvidenceReceipt e => e.VesselId,
            ApplicabilityEvaluation e => e.VesselId, ApplicabilityReceipt r => r.VesselId,
            OfflineEnrollment o => o.VesselId, OfflineOutcome o => o.VesselId, OfflineLifecycle o => o.VesselId,
            Credential c => c.VesselId, CredentialRevision c => c.VesselId, ImportBatch b => b.VesselId, ImportPreview p => p.VesselId, ImportRowResult r => r.VesselId,
            TugDraftPlan p => p.VesselId, TugDraftPlanRevision p => p.VesselId, TugDraftPlanTug p => p.VesselId,
            TugJob j => j.VesselId, TugJobRevision j => j.VesselId, TugOwner o => o.VesselId, TugAssignment a => a.VesselId,
            MaintenanceObservation o => o.VesselId,
            Asset a => a.VesselId, AssetRevision r => r.VesselId, MaintenancePlan p => p.VesselId,
            MaintenancePlanRevision r => r.VesselId, MaintenanceJob j => j.VesselId, TechnicalOwner o => o.VesselId,
            CrewAssignment c => c.VesselId, CrewAssignmentRevision c => c.VesselId, CrewPosition c => c.VesselId,
            Document d => d.VesselId, DocumentVersion d => d.VesselId,
            FileStage f => f.VesselId, StoredFile f => f.VesselId,
            Vessel v => v.Id, VesselProfile p => p.VesselId, TugProfile p => p.VesselId,
            RegulatoryDraft d => d.VesselId, RegulatoryDraftRevision r => r.VesselId,
            CommandReceipt r when r.Action is "ProfileSave" or "VesselCreate" or "VesselUpdate" => r.ResourceId,
            _ => null
        };
        Guid? resource = entry.Entity switch {
            HumanReviewHead e => e.Id, HumanReviewEvent e => e.Id, HumanReviewReceipt e => e.ResourceId,
            ControlEvidenceHead e => e.Id, ControlEvidenceRevision e => e.Id, ControlEvidenceRequirement e => e.Id, ControlEvidenceEdge e => e.Id, ControlEvidenceReceipt e => e.ResourceId,
            RuleDraft d => d.Id, RuleRevision r => r.Id, ApplicabilityEvaluation e => e.Id,
            EvaluationRule r => r.EvaluationId, ApplicabilityReceipt r => r.ResourceId,
            OfflineEnrollment o => o.AssignmentId, OfflineOutcome o => o.AssignmentId, OfflineLifecycle o => o.AssignmentId,
            Credential c => c.Id, CredentialRevision c => c.CredentialId, ImportBatch b => b.Id, ImportPreview p => p.BatchId, ImportRowResult r => r.PreviewId,
            TugDraftPlan p => p.JobId, TugDraftPlanRevision p => p.JobId, TugDraftPlanTug p => p.JobId,
            TugJob j => j.Id, TugJobRevision j => j.JobId, TugOwner o => o.Id, TugAssignment a => a.JobId,
            MaintenanceObservation o => o.Id, MaintenanceObservationRevocation r => r.ObservationId,
            Asset a => a.Id, AssetRevision r => r.AssetId, MaintenancePlan p => p.Id,
            MaintenancePlanRevision r => r.PlanId, MaintenanceJob j => j.Id, TechnicalOwner o => o.Id,
            CrewPersonalProfile p => p.PersonId, CrewPersonalProfileRevision p => p.PersonId,
            CrewIdentityRevision r => r.PersonId,
            Person p => p.Id, CrewPosition c => c.Id, CrewAssignment c => c.Id, CrewAssignmentRevision c => c.AssignmentId,
            Document d => d.Id, DocumentVersion d => d.DocumentId,
            FileStage f => f.Id, StoredFile f => f.Id,
            Tenant t => t.Id, Company c => c.Id, Vessel v => v.Id,
            RegisterDraft d => d.Id, RegisterRevision r => r.Id, CommandReceipt r => r.ResourceId,
            _ => vessel
        };
        if (entry.Entity is OfflineLifecycle lifecycle) {
            if (lifecycle.Action is not ("Enrolled" or "KeyEnrolled" or "Renewed" or "Revoked" or "KeyLost" or "CleanupAllowed"))
                throw new InvalidOperationException("Unsupported offline lifecycle event.");
            return new(vessel, resource, "OfflineLifecycle", null, null, JsonSerializer.Serialize(new[] {
                new { field = "Lifecycle", before = (string?)null, after = lifecycle.Action, evidence = "Allowlisted" } }));
        }
        if (entry.Entity is OfflineEnrollment) return new(vessel, resource, "OfflineEnrollment", null, null,
            "[{\"field\":\"Binding\",\"before\":null,\"after\":\"ServerOwnedContext\",\"evidence\":\"Allowlisted\"}]");
        if (entry.Entity is OfflineOutcome outcome) {
            if (outcome.Result is not ("Committed" or "MaterialConflict")) throw new InvalidOperationException("Unsupported offline outcome.");
            return new(vessel, resource, "OfflineOutcome", null, outcome.Revision, JsonSerializer.Serialize(new[] {
                new { field = "Outcome", before = (string?)null, after = outcome.Result, evidence = "Allowlisted" } }));
        }
        if (entry.Entity is HumanReviewEvent review) {
            if (review.ReasonCode is not ("REQUEST_REVIEW" or "HUMAN_VERIFIED" or "HUMAN_REJECTED" or "VALIDITY_ELAPSED" or "INPUT_SUPERSEDED" or "HUMAN_DECIDED"))
                throw new InvalidOperationException("Invalid review metadata.");
            return new(vessel, resource, "HumanReview", null, review.Revision, JsonSerializer.Serialize(new[] {
                new { field = "ReasonCode", before = (string?)null, after = review.ReasonCode, evidence = "Allowlisted" },
                new { field = "State", before = (string?)null, after = review.State, evidence = "Allowlisted" }
            }));
        }
        if (entry.Entity is ControlEvidenceRevision ce) {
            if (ce.Lifecycle != "DRAFT" || ce.ReasonCode is not ("INITIAL_AUTHORING" or "AUTHORING_CORRECTION" or "SYNTHETIC_INCOMPLETE_DRAFT")) throw new InvalidOperationException("Invalid control/evidence change metadata.");
            return new(vessel, resource, ce.Kind, null, ce.Revision, JsonSerializer.Serialize(new[] {
                new { field = "ReasonCode", before = (string?)null, after = ce.ReasonCode, evidence = "Allowlisted" },
                new { field = "Lifecycle", before = (string?)null, after = "DRAFT", evidence = "Allowlisted" }
            }));
        }
        if (entry.Entity is RuleRevision rule) {
            if (rule.ReasonCode is not ("INITIAL_AUTHORING" or "AUTHORING_CORRECTION" or "REQUEST_REVIEW" or "SYNTHETIC_INCOMPLETE_DRAFT") ||
                rule.Lifecycle != "DRAFT" || rule.ReviewStatus is not ("NOT_REQUESTED" or "BLOCKED_SOURCE_ADMISSION")) throw new InvalidOperationException("Invalid rule change metadata.");
            return new(vessel, resource, "RuleRevision", null, rule.Revision, JsonSerializer.Serialize(new[] {
                new { field = "ReasonCode", before = (string?)null, after = rule.ReasonCode, evidence = "Allowlisted" },
                new { field = "ReviewStatus", before = (string?)null, after = rule.ReviewStatus, evidence = "Allowlisted" }
            }));
        }
        var kind = entry.Entity switch { RuleDraft => "RuleDraft", ApplicabilityEvaluation => "ApplicabilityEvaluation", EvaluationRule => "EvaluationRule", RegisterDraft d => d.Kind, RegisterRevision r => r.Kind, _ => null };
        var revisionProperty = entry.Entity is Person ? "IdentityRevision" : "Revision";
        var revision = entry.Metadata.FindProperty(revisionProperty) == null ? null : entry.Property(revisionProperty);
        long? before = revision != null && entry.State != EntityState.Added ? (long?)revision.OriginalValue : null;
        long? after = revision != null && entry.State != EntityState.Deleted ? (long?)revision.CurrentValue : null;
        var fields = new List<object>();
        foreach (var property in entry.Properties.OrderBy(p => p.Metadata.Name, StringComparer.Ordinal))
        {
            var name = property.Metadata.Name;
            // Text values, bodies, fingerprints, receipt keys and personal details are never copied.
            if (!AuditValuePolicy.Includes(name)) continue;
            if (entry.State == EntityState.Modified && !property.IsModified) continue;
            string? Value(object? value) => AuditValuePolicy.Value(name, value);
            fields.Add(new { field = name, before = entry.State == EntityState.Added ? null : Value(property.OriginalValue),
                after = entry.State == EntityState.Deleted ? null : Value(property.CurrentValue),
                evidence = AuditValuePolicy.Evidence(name) });
        }
        return new(vessel, resource, kind, before, after, JsonSerializer.Serialize(fields));
    }
}
