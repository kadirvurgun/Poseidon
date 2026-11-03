using Poseidon.Domain;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// A deliberately closed projection. Never serialize an entity, command, identity claim or ContentJson.
internal sealed record AuditMetadata(Guid? VesselId, Guid? ResourceId, string? Kind,
    long? BeforeRevision, long? AfterRevision, string Summary)
{
    internal static AuditMetadata From(EntityEntry entry, Guid? parentVessel, AuditCommandContext? command, bool requireResolvedScope)
    {
        var metadata = Project(entry, parentVessel);
        var scopeKind = metadata.VesselId is { } vessel && vessel != Guid.Empty ? "Vessel" : entry.Entity switch
        {
            Person or CrewIdentityRevision or CrewPersonalProfile or CrewPersonalProfileRevision => "TenantPerson",
            CommandReceipt r when r.Action is "CrewPersonSave" or "CrewIdentityConvert" or "CrewProfileSave" or "CrewPhotoSave" => "TenantPerson",
            Tenant or Company or RegisterDraft or RegisterRevision or RuleDraft or RuleRevision or
                RegulatoryQualificationHead or RegulatoryQualificationEvent or RegulatoryQualificationReceipt => "Tenant",
            CommandReceipt r when r.Action is "CompanyCreate" or "CompanyUpdate" or "SourceDraftSave" or "RequirementDraftSave" => "Tenant",
            ApplicabilityReceipt r when r.Action is "author" or "review" => "Tenant",
            _ => "Unresolved"
        };
        if (requireResolvedScope && scopeKind == "Unresolved")
            throw new InvalidOperationException("Current command audit scope is unavailable.");
        var fields = JsonSerializer.Deserialize<List<JsonElement>>(metadata.Summary) ?? [];
        if (entry.Entity is CommandReceipt or ApplicabilityReceipt or HumanReviewReceipt or ControlEvidenceReceipt or RegulatoryQualificationReceipt)
            fields.Add(JsonSerializer.SerializeToElement(Field("Binding", null, "CommittedResource")));
        fields.Add(JsonSerializer.SerializeToElement(Field("ScopeKind", null, scopeKind)));
        if (command != null)
        {
            if (!command.IsValid) throw new InvalidOperationException("Invalid audit command context.");
            fields.Add(JsonSerializer.SerializeToElement(Field("CommandName", null, command.EndpointName)));
            fields.Add(JsonSerializer.SerializeToElement(Field("TechnicalReasonCode", null, command.ReasonCode)));
        }
        var summary = JsonSerializer.Serialize(fields);
        if (fields.Count > 20 || summary.Length > 2048)
            throw new InvalidOperationException("Audit summary exceeds its controlled bounds.");
        return metadata with { Summary = summary };
    }

    private static object Field(string field, string? before, string? after, string evidence = "Allowlisted") =>
        new { field, before, after, evidence };
    private static string Fields(params object[] fields) => JsonSerializer.Serialize(fields);
    private static string Number(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static AuditMetadata Project(EntityEntry entry, Guid? parentVessel)
    {
        if (entry.Entity is CommandReceipt { Action: "CrewCatalogSave" } catalogReceipt)
            return new(catalogReceipt.ResourceId, catalogReceipt.ResourceId, "CrewCatalogReceipt", null, catalogReceipt.RegisterRevision, "[]");
        if (entry.Entity is CrewCatalogEvent catalog)
            return new(catalog.VesselId, catalog.VesselId, "CrewCatalog", null, catalog.Revision,
                Fields(Field("State", null, Closed(catalog.State, "Draft", "Reviewed", "Adopted", "Withdrawn")),
                    Field("Revision", null, Number(catalog.Revision)), Field("AuthorityId", null, catalog.AuthorityId.ToString("D")),
                    Field("Reason", null, null, "ValuesWithheld")));
        if (entry.Entity is CrewCatalogHead catalogHead)
            return new(catalogHead.VesselId, catalogHead.VesselId, "CrewCatalogHead", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue,
                catalogHead.Revision, Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(catalogHead.Revision)),
                    Field("ActiveRevision", entry.State == EntityState.Added ? null : entry.Property("ActiveRevision").OriginalValue is long prior ? Number(prior) : null,
                        catalogHead.ActiveRevision is long active ? Number(active) : null)));
        // Do not put exact source bytes, validation rationale or actor-provided text in audit summaries.
        if (entry.Entity is RegulatoryQualificationEvent qualification)
            return new(null, qualification.RuleId, "RegulatoryQualification", null, qualification.Revision,
                Fields(Field("State", null, Closed(qualification.State, "DRAFT", "IN_REVIEW", "ADOPTED", "AWAITING", "FUTURE_EFFECTIVE", "IN_FORCE", "SUPERSEDED")),
                    Field("RuleRevision", null, Number(qualification.RuleRevision)), Field("Revision", null, Number(qualification.Revision)),
                    Field("AuthorityId", null, qualification.AuthorityId.ToString("D"))));
        if (entry.Entity is RegulatoryFactAttestation fact)
            return new(fact.VesselId, fact.Id, "RegulatoryFactAttestation", null, fact.ProfileRevision,
                Fields(Field("RuleId", null, fact.RuleId.ToString("D")), Field("QualificationRevision", null, Number(fact.QualificationRevision)),
                    Field("AuthorityId", null, fact.AuthorityId.ToString("D"))));
        if (entry.Entity is RegulatoryQualificationHead head)
            return new(null, head.RuleId, "RegulatoryQualificationHead", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue, head.Revision,
                Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(head.Revision))));
        if (entry.Entity is RegulatoryQualificationReceipt receipt)
            return new(null, receipt.RuleId, "RegulatoryQualificationReceipt", null, null, "[]");
        if (entry.Entity is EvaluationQualification binding)
            return new(parentVessel, binding.EvaluationId, "EvaluationQualification", null, binding.QualificationRevision,
                Fields(Field("RuleId", null, binding.RuleId.ToString("D")), Field("QualificationRevision", null, Number(binding.QualificationRevision)),
                    Field("AttestationId", null, binding.AttestationId?.ToString("D"))));
        Guid? vessel = entry.Entity switch {
            OperationEquipmentBinding e => e.VesselId, EvaluationOwningReference e => e.VesselId,
            EvaluationUpstreamReference e => e.VesselId, MsmdRoleBinding e => e.VesselId,
            TugCapabilityBinding e => e.VesselId,
            TugOfflineEnrollment e => e.VesselId, TugOfflineOutcome e => e.VesselId, TugOfflineLifecycle e => e.VesselId,
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
            _ => parentVessel
        };
        Guid? resource = entry.Entity switch {
            OperationEquipmentBinding e => e.Id, EvaluationOwningReference e => e.EvaluationId,
            EvaluationUpstreamReference e => e.EvaluationId, MsmdRoleBinding e => e.Id,
            TugCapabilityBinding e => e.Id,
            TugOfflineEnrollment e => e.JobId, TugOfflineOutcome e => e.JobId, TugOfflineLifecycle e => e.JobId,
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
        if (entry.Entity is TugOfflineEnrollment) return new(vessel, resource, "TugOfflineEnrollment", null, null,
            Fields(Field("Binding", entry.State == EntityState.Added ? null : "ServerOwnedContext", "ServerOwnedContext")));
        if (entry.Entity is TechnicalOwner or TugOwner)
            return new(vessel, resource, "OwnerReference", null, null, Fields(Field("Binding", null, "OwnerReference")));
        if (entry.Entity is CrewPosition)
            return new(vessel, resource, "CrewPosition", null, null, Fields(Field("Binding", null, "PositionReference")));
        if (entry.Entity is MaintenanceJob)
            return new(vessel, resource, "MaintenanceJob", null, null, Fields(Field("Binding", null, "JobReference")));
        if (entry.Entity is MaintenanceObservation observation && entry.State == EntityState.Added)
            // The immutable row itself is the anchor, with tenant/vessel/asset ownership
            // enforced by its composite FK. SourceRevision is not an aggregate revision.
            // Do not copy declared Value/Source/Unit/PolicyVersion or imply verification.
            return new(observation.VesselId, observation.Id, "MaintenanceObservation", null, null,
                Fields(Field("Kind", null, AuditValuePolicy.Value("Kind", observation.Kind)), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is Person person)
            return new(vessel, resource, "Person", entry.State == EntityState.Added ? null : (long?)entry.Property("IdentityRevision").OriginalValue,
                person.IdentityRevision, Fields(Field("Binding", entry.State == EntityState.Added ? null : "TenantPersonRecord", "TenantPersonRecord"),
                    Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("IdentityRevision").OriginalValue!), Number(person.IdentityRevision))));
        if (entry.Entity is ApplicabilityEvaluation)
            return new(vessel, resource, "ApplicabilityEvaluation", null, null, Fields(Field("Outcome", null, "EvaluationRecorded")));
        if (entry.Entity is ImportPreview)
            return new(vessel, resource, "ImportPreview", null, null, Fields(Field("Outcome", null, "PreviewRecorded")));
        if (entry.Entity is ImportRowResult rowResult)
            return new(vessel, resource, "ImportRowResult", null, null,
                Fields(Field("Outcome", null, Closed(rowResult.Outcome, "valid", "rejected"))));
        if (entry.Entity is ImportBatch && entry.State == EntityState.Modified && LegacyImportStorage.IsConversion(entry))
            return new(vessel, resource, "ImportBatch", (long?)entry.Property("Revision").OriginalValue, (long?)entry.Property("Revision").CurrentValue,
                Fields(Field("Binding", "LegacyStorage", "ProtectedStorage")));
        if (entry.Entity is MaintenanceObservationRevocation)
            return new(vessel, resource, "MaintenanceObservationRevocation", null, null,
                Fields(Field("Lifecycle", null, "Revoked")));
        if (entry.Entity is EvaluationRule evaluatedRule)
            return new(vessel, resource, "EvaluationRule", null, evaluatedRule.RuleRevision,
                Fields(Field("RuleId", null, evaluatedRule.RuleId.ToString("D")), Field("RuleRevision", null, Number(evaluatedRule.RuleRevision))));
        if (entry.Entity is EvaluationUpstreamReference upstream)
            return new(vessel, resource, "EvaluationUpstreamReference", null, upstream.RuleRevision,
                Fields(Field("RuleId", null, upstream.RuleId.ToString("D")), Field("RuleRevision", null, Number(upstream.RuleRevision)),
                    Field("UpstreamId", null, upstream.UpstreamId.ToString("D"))));
        if (entry.Entity is TugOfflineLifecycle tl) {
            if (tl.Action is not ("Enrolled" or "KeyEnrolled" or "Renewed" or "Revoked" or "KeyLost" or "CleanupAllowed")) throw new InvalidOperationException("Unsupported tug lifecycle event.");
            return new(vessel, resource, "TugOfflineLifecycle", null, null, Fields(Field("Lifecycle", null, tl.Action)));
        }
        if (entry.Entity is TugOfflineOutcome to) {
            if (to.Result is not ("Committed" or "MaterialConflict")) throw new InvalidOperationException("Unsupported tug replay outcome.");
            return new(vessel, resource, "TugOfflineOutcome", null, to.Revision,
                Fields(Field("Outcome", null, to.Result), Field("ServerVersion", null, Number(to.ServerVersion))));
        }
        if (entry.Entity is OfflineLifecycle lifecycle) {
            if (lifecycle.Action is not ("Enrolled" or "KeyEnrolled" or "Renewed" or "Revoked" or "KeyLost" or "CleanupAllowed"))
                throw new InvalidOperationException("Unsupported offline lifecycle event.");
            return new(vessel, resource, "OfflineLifecycle", null, null, JsonSerializer.Serialize(new[] {
                new { field = "Lifecycle", before = (string?)null, after = lifecycle.Action, evidence = "Allowlisted" } }));
        }
        if (entry.Entity is OfflineEnrollment) return new(vessel, resource, "OfflineEnrollment", null, null,
            Fields(Field("Binding", entry.State == EntityState.Added ? null : "ServerOwnedContext", "ServerOwnedContext")));
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
            if (ce.Lifecycle is not ("DRAFT" or "SUBMITTED" or "COMPLETED") || ce.ReasonCode is not ("INITIAL_AUTHORING" or "AUTHORING_CORRECTION" or "SYNTHETIC_INCOMPLETE_DRAFT" or "SUBMIT" or "COMPLETE")) throw new InvalidOperationException("Invalid control/evidence change metadata.");
            return new(vessel, resource, ce.Kind, null, ce.Revision, JsonSerializer.Serialize(new[] {
                new { field = "ReasonCode", before = (string?)null, after = ce.ReasonCode, evidence = "Allowlisted" },
                new { field = "Lifecycle", before = (string?)null, after = ce.Lifecycle, evidence = "Allowlisted" }
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
        var revisionProperty = entry.Entity is Person ? "IdentityRevision" : entry.Entity is CommandReceipt ? "RegisterRevision" : "Revision";
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
            string? Value(object? value) {
                try { return AuditValuePolicy.Value(name, value); }
                catch (InvalidOperationException) {
                    // Fixed model/allowlist metadata only, never the rejected value.
                    throw new InvalidOperationException($"Unsupported audit field type: {entry.Metadata.ClrType.Name}.{name} ({value?.GetType().Name}).");
                }
            }
            fields.Add(new { field = name, before = entry.State == EntityState.Added ? null : Value(property.OriginalValue),
                after = entry.State == EntityState.Deleted ? null : Value(property.CurrentValue),
                evidence = AuditValuePolicy.Evidence(name) });
        }
        return new(vessel, resource, kind, before, after, JsonSerializer.Serialize(fields));
    }

    private static string Closed(string value, params string[] allowed) => allowed.Contains(value, StringComparer.Ordinal)
        ? value : throw new InvalidOperationException("Invalid controlled audit state.");
}
