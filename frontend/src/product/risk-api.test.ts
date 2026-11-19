import { describe, expect, it } from 'vitest'
import { ApiError } from './api'
import { decodeRiskAssessment, decodeRiskTemplate, failureKind, freezeRiskCommand } from './risk-api'
import { parseMethod } from './RiskTemplateLibraryWorkspace'
import { riskAssessmentFixture, riskTemplateFixture, riskVessel } from './risk-test-fixtures'

describe('DEV-105/DEV-106 risk wire contract', () => {
  it('accepts an unconfigured-method assessment and keeps ratings empty', () => {
    const record = decodeRiskAssessment(riskAssessmentFixture(), riskVessel)
    expect(record.results[0].initial).toEqual({ state: 'METHOD_UNCONFIGURED', result: null, rank: null })
  })
  it('rejects a fabricated rating, a phantom approval and editable approved content', () => {
    expect(() => decodeRiskAssessment(riskAssessmentFixture({ results: [{ hazardId: 'x', initial: { state: 'METHOD_UNCONFIGURED', result: 'HIGH', rank: 3 }, residual: { state: 'NOT_ASSESSED', result: null, rank: null } }] }), riskVessel)).toThrow(ApiError)
    expect(() => decodeRiskAssessment(riskAssessmentFixture({ state: 'APPROVED', approvedRevision: null, canReview: false }), riskVessel)).toThrow(ApiError)
    expect(() => decodeRiskAssessment(riskAssessmentFixture({ state: 'APPROVED', approvedRevision: 2, canReview: false, canEdit: true, acknowledgementState: 'PENDING' }), riskVessel)).toThrow(ApiError)
    expect(() => decodeRiskAssessment(riskAssessmentFixture({ acknowledgementState: 'COMPLETE' }), riskVessel)).toThrow(ApiError)
    expect(() => decodeRiskAssessment(riskAssessmentFixture({ state: 'AUTO_APPROVED' }), riskVessel)).toThrow(ApiError)
    expect(() => decodeRiskAssessment(riskAssessmentFixture(), '00000000-0000-4000-8000-000000000999')).toThrow(ApiError)
  })
  it('requires a human publisher on every published or superseded template revision', () => {
    expect(decodeRiskTemplate(riskTemplateFixture()).revisions[0].status).toBe('DRAFT')
    const forged = riskTemplateFixture(); forged.revisions[0] = { ...forged.revisions[0], status: 'PUBLISHED', effectiveState: 'CURRENT', canEdit: false }
    expect(() => decodeRiskTemplate(forged)).toThrow(ApiError)
  })
  it('freezes a command with one key and an exact body and refuses foreign paths', () => {
    const attempt = freezeRiskCommand('POST', 'hse/risk-assessments?vesselId=' + riskVessel, { a: 1 }, riskVessel)
    expect(attempt.key).toMatch(/^[0-9a-f-]{36}$/); expect(attempt.body).toBe('{"a":1}')
    expect(() => freezeRiskCommand('POST', '../auth/session', {}, riskVessel)).toThrow(ApiError)
    expect(() => freezeRiskCommand('POST', 'hse/risk-assessments?vesselId=' + riskVessel + '&tenantId=x', {}, riskVessel)).toThrow(ApiError)
  })
  it('maps conflicts, incompleteness and separation of duties to distinct states', () => {
    expect(failureKind(new ApiError(409, 'version_conflict'))).toBe('conflict')
    expect(failureKind(new ApiError(409, 'invalid_state'))).toBe('stale')
    expect(failureKind(new ApiError(422, 'risk_record_incomplete'))).toBe('incomplete')
    expect(failureKind(new ApiError(403, 'separation_of_duties'))).toBe('forbidden')
    expect(failureKind(new TypeError('network'))).toBe('offline')
  })
  it('parses a tenant matrix without inventing a default', () => {
    const m = parseMethod('RM', 'Tenant', 'L1 Rare\nL2 Likely', 'S1 Minor\nS2 Major', 'L1 S1 A 0\nL1 S2 B 1\nL2 S1 B 1\nL2 S2 C 2', 'SMS-1', 'Configured by HSEQ owner')
    expect(m.cells).toHaveLength(4); expect(m.likelihoods.map(x => x.code)).toEqual(['L1', 'L2']); expect(m.cells[3]).toEqual({ likelihood: 'L2', severity: 'S2', result: 'C', rank: 2 })
    expect(parseMethod('RM', 'T', '', '', '', '', '').cells).toEqual([])
  })
})
