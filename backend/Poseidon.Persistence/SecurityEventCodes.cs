using System.Collections.Frozen;

namespace Poseidon.Persistence;

// Fixed technical metadata only. No request values, clinical facts or decision content.
// Changes require matching endpoint behavior and real database audit coverage.
public static class SecurityEventCodes
{
    public static IReadOnlySet<string> Actions { get; } = new string[]
    {
        "RequestValidation", "AccessDenied", "Applicability", "ApplicabilityCommand", "ApplicabilityCurrentRead",
        "ApplicabilityHistory", "ApplicabilityRead", "ApplicabilitySimulation", "AuditDetail",
        "AuditQuery", "AuditRevision", "CompaniesRead", "CompanyCreate",
        "CompanyUpdate", "ControlEvidence", "ControlEvidenceCommand", "ControlEvidenceRead",
        "ControlEvidenceReplay", "ControlOrphans", "ControlOwners", "CredentialRead",
        "CredentialSave", "CrewCatalog", "CrewCatalogRead", "CrewCurrentAssignmentRead",
        "CrewHistory", "CrewPositions", "CrewProfile", "CrewProfileHistory",
        "CrewProfileRead", "CrewProfileSave", "CrewRead", "CrewSave",
        "DirectoryWrite", "EquipmentRead", "EquipmentSave", "FileAccessIssue",
        "FileDetail", "FileDownload", "FileList", "FileRequest",
        "HumanReview", "HumanReviewCommand", "HumanReviewReplay", "HumanTraceRead",
        "ImportRead", "ImportReport", "ImportSave", "Login",
        "Logout", "MaintenanceObservation", "MaintenanceObservationCapture", "MaintenanceObservationRead",
        "MaintenanceObservationRevoke", "MaintenanceRead", "MaintenanceSave", "OfflineConflictRead",
        "OfflineHistory", "OfflineInventory", "OfflineRequest", "ProfileHistory",
        "ProfileRead", "ProfileSave", "RegisterHistory", "RegisterList",
        "RegisterRead", "RegisterSave", "RegulatoryQualification", "RegulatoryQualificationRead",
        "RegulatorySourceBytesRead", "RuleCapabilities", "RulesRead", "Session",
        "TodayRead", "TodayWhyRead", "TugRead", "TugSave",
        "VesselCreate", "VesselUpdate", "VesselsRead", "WorkspaceRead",
        "WorkspaceSearch",
    }.ToFrozenSet(StringComparer.Ordinal);
    public static IReadOnlySet<string> Results { get; } = new string[]
    {
        "Attempted", "Authorized", "Denied", "Failed",
        "Invalid", "NotFound", "SetupRequired", "Success",
        "Unauthenticated", "acknowledgment_role_required", "assignment_policy_denied", "attestation_authority_changed",
        "attestation_policy_mismatch", "authoritative_evaluation_unavailable", "catalog_authority_changed", "catalog_version_reused",
        "clean_file_unavailable", "command_reuse_conflict", "conflict_changed", "construction_record_required",
        "construction_record_unavailable", "csrf_invalid", "current_evaluation_requires_server_time", "decision_content_required",
        "decision_type_gate_unavailable", "duplicate_code", "duplicate_lineage", "enrollment_limit",
        "evaluation_lineage_unavailable", "evaluation_unavailable", "example_rule_not_admissible", "field_policy_denied",
        "file_attempt_conflict", "file_attempt_frozen", "file_bytes_unavailable", "file_capability_invalid",
        "file_integrity_unavailable", "file_quarantined_or_unknown", "file_settings_unavailable", "file_type_or_size_invalid",
        "historical_outcome_unavailable", "human_acknowledgment_required", "human_authority_required", "human_catalog_authority_required",
        "idempotency_key_reused", "immutable_context", "immutable_file_reference", "immutable_position_conflict",
        "immutable_review_content", "incomplete_rule", "independent_catalog_authority_required", "input_review_required",
        "input_version_conflict", "invalid_as_of", "invalid_author_contract", "invalid_catalog",
        "invalid_device_proof", "invalid_fact_attestation", "invalid_file_metadata", "invalid_idempotency_key",
        "invalid_key_enrollment", "invalid_lineage", "invalid_offline_command", "invalid_offline_page",
        "invalid_offline_resolution", "invalid_query", "invalid_request", "invalid_review_request",
        "invalid_rule", "invalid_rule_set", "known_profile_value_required", "lineage_conflict",
        "lineage_unavailable", "linked_resource_unavailable", "linked_version_conflict", "observation_policy_denied",
        "offline_command_reused", "offline_conflict_changed", "offline_hash_mismatch", "offline_key_proof_denied",
        "offline_policy_denied", "offline_recovery_required", "offline_resolution_exists", "offline_retry",
        "owner_unavailable", "owning_context_changed", "owning_document_author_required", "owning_document_verifier_required",
        "owning_verification_requires_independent_evaluation", "permission_denied", "profile_context_conflict", "profile_unavailable",
        "protected_storage_unavailable", "qualified_evaluation_required", "qualified_source_required", "reauthorization_required",
        "request_limit", "resource_not_found", "resource_unavailable", "review_context_changed",
        "rule_identity_immutable", "rule_unavailable", "rule_version_mismatch", "separation_of_duties",
        "service_unavailable", "source_admission_pending", "source_assertion_not_admitted", "source_authority_mismatch",
        "source_contract_changed", "source_integrity_unavailable", "source_owner_authority_required", "source_revision_changed",
        "source_revision_unavailable", "submission_required", "submitted_content_changed", "superseding_rule_unavailable",
        "trace_reader_required", "tug_capability_context_unavailable", "validation_failed", "verification_unavailable",
        "terminal_review", "source_admission_and_signature_unavailable", "pending_required",
        "invalid_reason", "invalid_state", "expiry_not_due", "supersession_not_proven", "freshness_review_required",
        "version_conflict", "invalid_qualification_transition", "independent_reviewer_required",
        "incomplete_qualification", "acceptance_trigger_required", "impact_review_required",
        "outside_effective_dates", "superseding_rule_required",
    }.ToFrozenSet(StringComparer.Ordinal);
    public static bool Allows(string action, string result) => Actions.Contains(action) && Results.Contains(result);
}
