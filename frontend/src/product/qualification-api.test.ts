import { describe, it, expect } from 'vitest'
import { decodeQualification, validContent } from './qualification-api'
import { decodeTrace } from './human-review-api'
import { reviewAnchor, reviewTrace } from './human-review.test-fixtures'

import { qualifiedFixture } from './qualification.test-fixtures'
describe('REGULATORY-ADMISSION-CURRENT-01 strict wire (authored, NOT RUN)', () => {
  it.each(['requireFile', 'requireAcknowledgment'] as const)('keeps %s unknown in drafts, rejects wire absence and requires a known choice after review', field => {
    const row = qualifiedFixture()
    const missing: Record<string, unknown> = { ...row.content.control }
    delete missing[field]
    expect(validContent({ ...row.content, control: missing })).toBe(false)
    for (const state of ['IN_REVIEW', 'ADOPTED', 'IN_FORCE']) {
      const content = { ...row.content, control: { ...row.content.control, [field]: null } }
      expect(() => decodeQualification({ ...row, state, content }, row.ruleId)).toThrow()
      const draft = { ...row, state: 'DRAFT', content, blockers: ['CONTROL_POLICY_MISSING'] }
      expect(decodeQualification(draft, row.ruleId).content.control?.[field]).toBeNull()
    }
    for (const choice of [false, true]) {
      const content = { ...row.content, control: { ...row.content.control, [field]: choice, acknowledgmentRole: row.roleId } }
      expect(decodeQualification({ ...row, content }, row.ruleId).content.control?.[field]).toBe(choice)
    }
    for (const choice of ['false', 0, '', undefined]) {
      expect(validContent({ ...row.content, control: { ...row.content.control, [field]: choice } })).toBe(false)
    }
  })
  it('requires a cited mechanism and an acknowledgment role only when their policy requires them', () => {
    const row = qualifiedFixture()
    const mechanism = { ...row.content.control, requiredVerificationMechanism: 'FICTIONAL_MECHANISM', verificationMechanismSourceLocator: null }
    expect(() => decodeQualification({ ...row, content: { ...row.content, control: mechanism } }, row.ruleId)).toThrow()
    expect(decodeQualification({ ...row, state: 'DRAFT', content: { ...row.content, control: mechanism }, blockers: ['CONTROL_POLICY_MISSING'] }, row.ruleId).state).toBe('DRAFT')
    expect(decodeQualification({ ...row, content: { ...row.content, control: { ...mechanism, verificationMechanismSourceLocator: 'synthetic/mechanism' } } }, row.ruleId).state).toBe('IN_FORCE')
    expect(() => decodeQualification({ ...row, content: { ...row.content, control: { ...row.content.control, requireAcknowledgment: true } } }, row.ruleId)).toThrow()
    expect(decodeQualification(row, row.ruleId).content.control?.requireAcknowledgment).toBe(false)
  })
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
