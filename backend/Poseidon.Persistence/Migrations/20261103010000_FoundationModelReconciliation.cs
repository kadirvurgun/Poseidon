using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Poseidon.Persistence.Migrations;

// Generated final model metadata follows all retained additive authoring migrations.
// Their tables, columns, FKs and immutable triggers already exist. The old snapshot
// omitted them; applying the raw generated diff would incorrectly recreate them.
// Reconcile only absent supporting index names and schema-local PK names here.
public partial class FoundationModelReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE INDEX IF NOT EXISTS "IX_RegulatoryDrafts_ConstructionRecord" ON core."RegulatoryDrafts" ("TenantId", "ConstructionFileId", "VesselId", "ConstructionFileRevision", "ConstructionFileChecksum");
        CREATE INDEX IF NOT EXISTS "IX_RegulatoryDraftRevisions_ConstructionRecord" ON core."RegulatoryDraftRevisions" ("TenantId", "ConstructionFileId", "VesselId", "ConstructionFileRevision", "ConstructionFileChecksum");
        CREATE INDEX IF NOT EXISTS "IX_CommandReceipts_ProfileOutcome" ON core."CommandReceipts" ("TenantId", "ProfileVesselId", "RegisterRevision");
        CREATE INDEX IF NOT EXISTS "IX_ApplicabilityEvaluations_TenantId_VesselId_ProfileRevision" ON core."ApplicabilityEvaluations" ("TenantId", "VesselId", "ProfileRevision");
        CREATE INDEX IF NOT EXISTS "IX_ApplicabilityReceipts_TenantId_EvaluationResourceId" ON core."ApplicabilityReceipts" ("TenantId", "EvaluationResourceId");
        CREATE INDEX IF NOT EXISTS "IX_ApplicabilityReceipts_TenantId_RuleResourceId" ON core."ApplicabilityReceipts" ("TenantId", "RuleResourceId");
        CREATE INDEX IF NOT EXISTS "IX_ApplicabilityReceipts_TenantId_VesselId" ON core."ApplicabilityReceipts" ("TenantId", "VesselId");
        CREATE INDEX IF NOT EXISTS "IX_CapabilityBindings_TenantId_VesselId_AssetId_AssetRevision" ON tug."CapabilityBindings" ("TenantId", "VesselId", "AssetId", "AssetRevision");
        CREATE INDEX IF NOT EXISTS "IX_CapabilityBindings_TenantId_VesselId_ConditionKind_Conditio~" ON tug."CapabilityBindings" ("TenantId", "VesselId", "ConditionKind", "ConditionId", "ConditionRevision");
        CREATE INDEX IF NOT EXISTS "IX_CatalogHeads_TenantId_VesselId_ActiveRevision" ON crew."CatalogHeads" ("TenantId", "VesselId", "ActiveRevision");
        CREATE INDEX IF NOT EXISTS "IX_ControlEvidenceEdges_TenantId_VesselId_TargetKind_TargetId_~" ON core."ControlEvidenceEdges" ("TenantId", "VesselId", "TargetKind", "TargetId", "TargetRevision");
        CREATE INDEX IF NOT EXISTS "IX_ControlEvidenceReceipts_TenantId_VesselId_Kind_ResourceId" ON core."ControlEvidenceReceipts" ("TenantId", "VesselId", "Kind", "ResourceId");
        CREATE INDEX IF NOT EXISTS "IX_ControlEvidenceRequirements_TenantId_RequirementKind_Requir~" ON core."ControlEvidenceRequirements" ("TenantId", "RequirementKind", "RequirementId", "RequirementRevision");
        CREATE INDEX IF NOT EXISTS "IX_ControlEvidenceRequirements_TenantId_RuleId_RuleRevision" ON core."ControlEvidenceRequirements" ("TenantId", "RuleId", "RuleRevision");
        CREATE INDEX IF NOT EXISTS "IX_ControlEvidenceRequirements_TenantId_SourceKind_SourceId_So~" ON core."ControlEvidenceRequirements" ("TenantId", "SourceKind", "SourceId", "SourceRevision");
        CREATE INDEX IF NOT EXISTS "IX_ControlEvidenceRevisions_TenantId_VesselId_FileId_FileRevis~" ON core."ControlEvidenceRevisions" ("TenantId", "VesselId", "FileId", "FileRevision", "Checksum");
        CREATE INDEX IF NOT EXISTS "IX_EvaluationOwningReferences_TenantId_VesselId_EvaluationId" ON core."EvaluationOwningReferences" ("TenantId", "VesselId", "EvaluationId");
        CREATE INDEX IF NOT EXISTS "IX_EvaluationOwningReferences_TenantId_VesselId_Kind_Id_Revisi~" ON core."EvaluationOwningReferences" ("TenantId", "VesselId", "Kind", "Id", "Revision");
        CREATE INDEX IF NOT EXISTS "IX_EvaluationQualifications_TenantId_AttestationId" ON core."EvaluationQualifications" ("TenantId", "AttestationId");
        CREATE INDEX IF NOT EXISTS "IX_EvaluationQualifications_TenantId_RuleId_QualificationRevis~" ON core."EvaluationQualifications" ("TenantId", "RuleId", "QualificationRevision");
        CREATE INDEX IF NOT EXISTS "IX_EvaluationRules_TenantId_RuleId_RuleRevision" ON core."EvaluationRules" ("TenantId", "RuleId", "RuleRevision");
        CREATE INDEX IF NOT EXISTS "IX_EvaluationUpstreamReferences_TenantId_RuleId_RuleRevision" ON core."EvaluationUpstreamReferences" ("TenantId", "RuleId", "RuleRevision");
        CREATE INDEX IF NOT EXISTS "IX_EvaluationUpstreamReferences_TenantId_VesselId_EvaluationId" ON core."EvaluationUpstreamReferences" ("TenantId", "VesselId", "EvaluationId");
        CREATE INDEX IF NOT EXISTS "IX_EvaluationUpstreamReferences_TenantId_VesselId_UpstreamId" ON core."EvaluationUpstreamReferences" ("TenantId", "VesselId", "UpstreamId");
        CREATE INDEX IF NOT EXISTS "IX_HumanReviewEvents_TenantId_VesselId_ControlKind_ControlId_C~" ON core."HumanReviewEvents" ("TenantId", "VesselId", "ControlKind", "ControlId", "ControlRevision");
        CREATE INDEX IF NOT EXISTS "IX_HumanReviewEvents_TenantId_VesselId_EvaluationId" ON core."HumanReviewEvents" ("TenantId", "VesselId", "EvaluationId");
        CREATE INDEX IF NOT EXISTS "IX_HumanReviewEvents_TenantId_VesselId_EvidenceKind_EvidenceId~" ON core."HumanReviewEvents" ("TenantId", "VesselId", "EvidenceKind", "EvidenceId", "EvidenceRevision");
        CREATE INDEX IF NOT EXISTS "IX_HumanReviewEvents_TenantId_VesselId_VerificationId_Verifica~" ON core."HumanReviewEvents" ("TenantId", "VesselId", "VerificationId", "VerificationRevision");
        CREATE INDEX IF NOT EXISTS "IX_HumanReviewReceipts_TenantId_VesselId_ResourceId" ON core."HumanReviewReceipts" ("TenantId", "VesselId", "ResourceId");
        CREATE INDEX IF NOT EXISTS "IX_MsmdRoleBindings_TenantId_PositionId_VesselId" ON core."MsmdRoleBindings" ("TenantId", "PositionId", "VesselId");
        CREATE INDEX IF NOT EXISTS "IX_MsmdRoleBindings_TenantId_VesselId_Kind_Id_Revision" ON core."MsmdRoleBindings" ("TenantId", "VesselId", "Kind", "Id", "Revision");
        CREATE INDEX IF NOT EXISTS "IX_OfflineEnrollments_TenantId_VesselId_JobId_BaseRevision" ON tug."OfflineEnrollments" ("TenantId", "VesselId", "JobId", "BaseRevision");
        CREATE INDEX IF NOT EXISTS "IX_OfflineLifecycle_TenantId_EnrollmentId_ActorId_VesselId_Job~" ON tug."OfflineLifecycle" ("TenantId", "EnrollmentId", "ActorId", "VesselId", "JobId");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_OfflineOutcomes_TenantId_ActorId_ParentCommandId" ON tug."OfflineOutcomes" ("TenantId", "ActorId", "ParentCommandId") WHERE "ParentCommandId" IS NOT NULL;
        CREATE INDEX IF NOT EXISTS "IX_OfflineOutcomes_TenantId_ActorId_ParentCommandId_Enrollment~" ON tug."OfflineOutcomes" ("TenantId", "ActorId", "ParentCommandId", "EnrollmentId");
        CREATE INDEX IF NOT EXISTS "IX_OfflineOutcomes_TenantId_EnrollmentId_ActorId_VesselId_JobId" ON tug."OfflineOutcomes" ("TenantId", "EnrollmentId", "ActorId", "VesselId", "JobId");
        CREATE INDEX IF NOT EXISTS "IX_OfflineOutcomes_TenantId_VesselId_JobId_Revision" ON tug."OfflineOutcomes" ("TenantId", "VesselId", "JobId", "Revision");
        CREATE INDEX IF NOT EXISTS "IX_OperationEquipmentBindings_TenantId_PositionId_VesselId" ON core."OperationEquipmentBindings" ("TenantId", "PositionId", "VesselId");
        CREATE INDEX IF NOT EXISTS "IX_OperationEquipmentBindings_TenantId_VesselId_AssetId_AssetR~" ON core."OperationEquipmentBindings" ("TenantId", "VesselId", "AssetId", "AssetRevision");
        CREATE INDEX IF NOT EXISTS "IX_OperationEquipmentBindings_TenantId_VesselId_AssignmentId_A~" ON core."OperationEquipmentBindings" ("TenantId", "VesselId", "AssignmentId", "AssignmentRevision");
        CREATE INDEX IF NOT EXISTS "IX_OperationEquipmentBindings_TenantId_VesselId_CredentialId_C~" ON core."OperationEquipmentBindings" ("TenantId", "VesselId", "CredentialId", "CredentialRevision");
        CREATE INDEX IF NOT EXISTS "IX_OperationEquipmentBindings_TenantId_VesselId_JobId_JobRevis~" ON core."OperationEquipmentBindings" ("TenantId", "VesselId", "JobId", "JobRevision");
        CREATE INDEX IF NOT EXISTS "IX_OperationEquipmentBindings_TenantId_VesselId_JobId_PlanRevi~" ON core."OperationEquipmentBindings" ("TenantId", "VesselId", "JobId", "PlanRevision");
        CREATE INDEX IF NOT EXISTS "IX_OperationEquipmentBindings_TenantId_VesselId_ServiceKind_Se~" ON core."OperationEquipmentBindings" ("TenantId", "VesselId", "ServiceKind", "ServiceId", "ServiceRevision");
        CREATE INDEX IF NOT EXISTS "IX_RegulatoryFactAttestations_TenantId_RuleId_QualificationRev~" ON core."RegulatoryFactAttestations" ("TenantId", "RuleId", "QualificationRevision");
        CREATE INDEX IF NOT EXISTS "IX_RegulatoryFactAttestations_TenantId_VesselId_ProfileRevision" ON core."RegulatoryFactAttestations" ("TenantId", "VesselId", "ProfileRevision");
        CREATE INDEX IF NOT EXISTS "IX_RegulatoryQualificationEvents_TenantId_RuleId_RuleRevision" ON core."RegulatoryQualificationEvents" ("TenantId", "RuleId", "RuleRevision");
        CREATE INDEX IF NOT EXISTS "IX_RegulatoryQualificationEvents_TenantId_SupersedingRuleId_Su~" ON core."RegulatoryQualificationEvents" ("TenantId", "SupersedingRuleId", "SupersedingRuleRevision");
        CREATE INDEX IF NOT EXISTS "IX_RegulatoryQualificationReceipts_TenantId_RuleId" ON core."RegulatoryQualificationReceipts" ("TenantId", "RuleId");
        CREATE INDEX IF NOT EXISTS "IX_RuleRevisions_TenantId_RequirementKind_RequirementId_Requir~" ON core."RuleRevisions" ("TenantId", "RequirementKind", "RequirementId", "RequirementRevision");
        CREATE INDEX IF NOT EXISTS "IX_RuleRevisions_TenantId_SourceKind_SourceId_SourceRevision" ON core."RuleRevisions" ("TenantId", "SourceKind", "SourceId", "SourceRevision");
        ALTER TABLE tug."Owners" RENAME CONSTRAINT "PK_Owners" TO "PK_Owners1";
        ALTER TABLE tug."Jobs" RENAME CONSTRAINT "PK_Jobs" TO "PK_Jobs1";
        ALTER TABLE tug."Assignments" RENAME CONSTRAINT "PK_Assignments" TO "PK_Assignments1";
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Use a controlled forward migration or restore; retained lineage is not dropped.");
}
