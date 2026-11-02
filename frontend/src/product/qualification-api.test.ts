import { describe, it, expect } from 'vitest'
import { decodeQualification } from './qualification-api'
import { decodeTrace } from './human-review-api'
import { reviewAnchor, reviewTrace } from './human-review.test-fixtures'

import { qualifiedFixture } from './qualification.test-fixtures'
describe('REGULATORY-ADMISSION-CURRENT-01 strict wire (authored, NOT RUN)', () => {
  it('retains source and policy hashes and rejects expanded authority and hidden bytes', () => {
    const row = qualifiedFixture(); expect(decodeQualification(row, row.ruleId).state).toBe('IN_FORCE')
    expect(() => decodeQualification({ ...row, accepted: true }, row.ruleId)).toThrow()
    expect(() => decodeQualification({ ...row, content: { ...row.content, sourceBytesBase64: 'cHJpdmF0ZQ==' } }, row.ruleId)).toThrow()
    expect(() => decodeQualification({ ...row, contract: { ...row.contract, signatureVerified: true } }, row.ruleId)).toThrow()
    expect(() => decodeQualification({ ...row, content: { ...row.content, control: { ...row.content.control, maxAgeHours: -1 } } }, row.ruleId)).toThrow()
  })
  it('accepts assigned metadata history while rejecting rationale and decision text inside that projection', () => {
    const trace = { ...reviewTrace(), auditItems: [{ id: reviewAnchor.controlId, revision: 1, kind: 'Decision', state: 'RECORDED', currentState: 'RECORDED', savedAt: '2026-11-02T00:00:00Z', validUntil: null,
      evaluationId: null, verificationId: reviewAnchor.evidenceId, verificationRevision: 1, authorityId: reviewAnchor.controlId, roleId: reviewAnchor.controlId, authorityHash: 'A'.repeat(64), contextHash: 'B'.repeat(64) }], auditHasMore: false }
    expect(decodeTrace(trace, reviewAnchor, 0).auditItems).toHaveLength(1)
    expect(() => decodeTrace({ ...trace, auditItems: [{ ...trace.auditItems[0], rationale: 'private' }] }, reviewAnchor, 0)).toThrow()
    expect(() => decodeTrace({ ...trace, auditItems: [{ ...trace.auditItems[0], humanDecision: 'private' }] }, reviewAnchor, 0)).toThrow()
  })
})
