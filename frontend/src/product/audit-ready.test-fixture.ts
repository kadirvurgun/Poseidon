import type { AuditReadyChains } from './audit-ready-api'
import type { Trace } from './human-review-api'

// Synthetic, software-only fixtures. No real vessel, regulation, human or decision.
export const arVessel = '30000000-0000-4000-8000-000000000001'
export const arEvidence = '30000000-0000-4000-8000-000000000002'
export const arControl = '30000000-0000-4000-8000-000000000003'
export const arSource = '30000000-0000-4000-8000-000000000004'
export const arRequirement = '30000000-0000-4000-8000-000000000005'
const H = (c: string) => c.repeat(64)
export const arChains = (overrides: Partial<AuditReadyChains> = {}): AuditReadyChains => ({
  vesselId: arVessel, asOf: '2026-11-19T10:00:00+00:00', unavailable: 0, hasMore: false,
  items: [{ evidenceId: arEvidence, evidenceRevision: 2, controlId: arControl, controlRevision: 1, sources: [{
    sourceId: arSource, sourceRevision: 1, requirementId: arRequirement, requirementRevision: 1, ruleId: null, ruleRevision: null, availability: 'Available',
    locatorState: 'Known', locator: 'FICTIONAL Code/Chapter 9/para 4', titleState: 'Missing', title: null, provenance: 'SyntheticUnverified', sourceHash: H('A'), sourceSavedAt: '2026-11-18T08:00:00+00:00' }] }],
  ...overrides,
})
export const arTrace = (overrides: Partial<Trace> = {}): Trace => ({
  vesselId: arVessel, evidenceId: arEvidence, evidenceRevision: 2, controlId: arControl, controlRevision: 1, evidenceVersion: 4, controlVersion: 3,
  asOf: '2026-11-19T10:00:00+00:00', historical: false, disposition: 'REVIEW_REQUIRED', complete: false, offset: 0, hasMore: false, profileRevision: 2,
  evaluationId: null, auditHasMore: false, authorities: [], items: [],
  blockers: ['AUTHORITATIVE_EVALUATION_UNAVAILABLE', 'VERIFICATION_PENDING_OR_STALE'],
  evidenceContext: { provenance: null, observedAt: null, validUntil: null, freshUntil: null, timeState: 'UNKNOWN' },
  segments: [
    { kind: 'Source', id: arSource, revision: 1, availability: 'Available', state: 'DRAFT', why: 'SOURCE_ADMISSION_UNAVAILABLE', hash: H('A'), savedAt: '2026-11-18T08:00:00+00:00' },
    { kind: 'Requirement', id: arRequirement, revision: 1, availability: 'Available', state: 'DRAFT', why: 'SOURCE_ADMISSION_UNAVAILABLE', hash: H('B'), savedAt: '2026-11-18T08:05:00+00:00' },
    { kind: 'Applicability', id: null, revision: null, availability: 'Unavailable', state: 'REVIEW_REQUIRED', why: 'AUTHORITATIVE_EVALUATION_UNAVAILABLE', hash: null, savedAt: null },
    { kind: 'Control', id: arControl, revision: 1, availability: 'Available', state: 'COMPLETED', why: 'COMPLETION_AND_EFFECTIVENESS_UNKNOWN', hash: H('C'), savedAt: '2026-11-18T09:00:00+00:00' },
    { kind: 'Evidence', id: arEvidence, revision: 2, availability: 'Available', state: 'SUBMITTED', why: 'EXISTENCE_IS_NOT_VERIFICATION', hash: H('D'), savedAt: '2026-11-18T09:30:00+00:00' },
  ],
  auditItems: [
    { id: '30000000-0000-4000-8000-000000000010', revision: 1, kind: 'Verification', state: 'PENDING', currentState: 'PENDING', savedAt: '2026-11-18T10:00:00+00:00', validUntil: null, evaluationId: null, verificationId: null, verificationRevision: null,
      authorityId: '30000000-0000-4000-8000-000000000011', roleId: '30000000-0000-4000-8000-000000000012', authorityHash: H('E'), contextHash: H('F'), reasonCode: 'REQUEST_REVIEW', authorityRole: null },
    { id: '30000000-0000-4000-8000-000000000020', revision: 1, kind: 'Decision', state: 'RECORDED', currentState: 'RECORDED', savedAt: '2026-11-18T11:00:00+00:00', validUntil: null, evaluationId: null, verificationId: '30000000-0000-4000-8000-000000000010', verificationRevision: 1,
      authorityId: '30000000-0000-4000-8000-000000000021', roleId: '30000000-0000-4000-8000-000000000012', authorityHash: H('1'), contextHash: H('2'), reasonCode: 'HUMAN_DECIDED', authorityRole: 'DPA' },
  ],
  ...overrides,
})
