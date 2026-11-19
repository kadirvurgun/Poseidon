// Synthetic DEV-101/102 lifecycle transport fixtures (component and contract tests only).
// No fixture represents a recorded signature, a verified closure or a claim decision.
import { shipOpsRecordId, shipOpsVessel } from './ship-operation-test-fixtures'

const authority = { id: '40000000-0000-4000-8000-000000000001', roleId: '40000000-0000-4000-8000-000000000002', duty: 'Master' }
export function sofFinalizationFixture() {
  return { recordId: shipOpsRecordId, vesselId: shipOpsVessel, revision: 3, version: 7, sofState: 'DRAFT', finalRevision: null, canFinalize: true, authorities: [authority],
    missingRequired: [], unresolvedReferences: ['voyage_id', 'port_call_id', 'port'], events: [],
    qualifications: ['FINAL_IS_INTERNAL_NOT_EXTERNAL_AGREEMENT', 'SOF_SIGNATURE_NOT_RECORDED', 'VOYAGE_ENTITY_UNAVAILABLE_VOYAGE_UNKNOWN', 'LINEAGE_VIA_NOON_ORIGIN_PIN_ONLY', 'AMENDMENT_CREATES_NEW_REVISION'] }
}
export function downtimeLifecycleFixture() {
  return { recordId: shipOpsRecordId, vesselId: shipOpsVessel, revision: 2, version: 4, state: 'IN_REVIEW', lossOfHireCandidate: 'REVIEW_REQUIRED', claimEvidence: [],
    allowedActions: ['RETURN_FOR_CORRECTION', 'VERIFY'], authorities: [{ ...authority, duty: 'ChiefEngineer' }], actorIsRecorder: false, missingRequired: [], evidenceOptions: [], events: [],
    qualifications: ['LOSS_OF_HIRE_CANDIDATE_ONLY_NO_ENTITLEMENT_OR_AMOUNT', 'CLAIM_EVIDENCE_LINK_IS_NOT_VERIFICATION', 'VERIFIED_CLOSURE_IS_NOT_SAFETY_RELEASE', 'SEPARATION_OF_DUTIES_RECORDER_NEVER_VERIFIES_OR_CLOSES', 'EXTERNAL_SHARING_NOT_AUTOMATIC'] }
}
