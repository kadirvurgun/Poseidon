import type { RiskAssessment, RiskTemplateDetail } from './risk-api'

export const riskVessel = '00000000-0000-4000-8000-00000000a105'
export const riskTenant = '00000000-0000-4000-8000-00000000b105'
export const riskActor = '00000000-0000-4000-8000-00000000c105'
export const riskId = '00000000-0000-4000-8000-00000000d105'
export const riskHash = 'A'.repeat(64)
const unrated = { state: 'METHOD_UNCONFIGURED', result: null, rank: null }
export function riskAssessmentFixture(patch: Partial<RiskAssessment> = {}): RiskAssessment {
  return {
    id: riskId, vesselId: riskVessel, assessmentNo: 'FICTIONAL-RA-1', revision: 2, version: 7, state: 'IN_REVIEW',
    content: { title: 'FICTIONAL mast work', operationContext: 'FICTIONAL port maintenance', workType: 'MAINTENANCE', workScope: null, methodId: null,
      hazards: [{ id: '00000000-0000-4000-8000-00000000e105', step: null, description: 'FICTIONAL fall from height', consequences: ['Injury'],
        initial: { likelihood: 'L2', severity: 'S2', reason: null }, controls: [{ id: '00000000-0000-4000-8000-00000000f105', description: 'FICTIONAL harness', kind: 'EXISTING', owner: null, required: true, barrierRefs: [] }],
        barrierRefs: [], residual: { likelihood: null, severity: null, reason: null } }],
      stopWorkRequired: null, stopWorkConditions: [], participants: [] },
    links: [], template: null, method: null,
    results: [{ hazardId: '00000000-0000-4000-8000-00000000e105', initial: unrated, residual: unrated }],
    blockers: ['risk_method_unconfigured', 'stop_work_unknown'], hash: riskHash, approvedRevision: null, acknowledgementState: 'NOT_APPLICABLE',
    acknowledgements: [], decisions: [], revisions: [{ revision: 2, state: 'IN_REVIEW', reasonCode: 'SUBMIT', hash: riskHash, actorId: riskActor, savedAt: '2026-11-20T00:00:00Z' }],
    canEdit: false, canSubmit: false, canReview: true, canAmend: false, canAcknowledge: false, provenance: 'UserDeclaredUnverified', savedAt: '2026-11-20T00:00:00Z',
    sourceVersion: 'synthetic', ...patch,
  }
}
export function riskTemplateFixture(): RiskTemplateDetail {
  const content = { title: 'FICTIONAL bunkering', workType: 'BUNKERING', vesselKinds: [], requiredBarriers: [], hazards: [], sourceRefs: [], approvalOwner: null, changeReason: null }
  return { id: riskId, templateCode: 'FICTIONAL-T', latestRevision: 1, version: 3, decisions: [], suggestions: [], suggestionCount: 0, canRevise: false, canSuggest: false, sourceVersion: 'synthetic',
    revisions: [{ templateId: riskId, templateCode: 'FICTIONAL-T', revision: 1, version: 4, status: 'DRAFT', effectiveState: 'NOT_EFFECTIVE', effectiveFrom: null, content, hash: riskHash,
      blockers: ['hazard_missing'], publishedBy: null, publishedAt: null, supersededByRevision: null, canEdit: true, canSubmit: false, canPublish: false, actorId: riskActor,
      savedAt: '2026-11-20T00:00:00Z', provenance: 'UserDeclaredUnverified' }] }
}
