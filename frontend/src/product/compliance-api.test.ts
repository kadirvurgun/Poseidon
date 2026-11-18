import { describe, expect, it } from 'vitest'
import { decodeOnboarding, decodePassport } from './compliance-api'
import { assessmentFixture, capturedInputFixture, complianceVessel, otherComplianceVessel, passportFixture, recordedReviewFixture } from './compliance-test-fixtures'

describe('S5-CONSUMER-01 scoped compliance contracts (authored; execution deferred)', () => {
  it('accepts the closed field set in server order and keeps complete declarations unverified', () => {
    const a = assessmentFixture(); a.fields.reverse()
    expect(decodeOnboarding(a, complianceVessel).state).toBe('REVIEW_REQUIRED')
    const complete = { ...a, state: 'DECLARED_COMPLETE_UNVERIFIED', fields: a.fields.map(f => ({ ...f, state: 'Known', complete: true })), blockers: ['VERIFICATION_NOT_ESTABLISHED'] }
    expect(decodeOnboarding(complete, complianceVessel)).toMatchObject({ freshness: 'Unverified', canSubmit: false })
    expect(() => decodeOnboarding({ ...complete, canSubmit: true }, complianceVessel)).toThrow()
  })
  it('rejects duplicate, missing, unknown and inconsistent critical field contracts', () => {
    const a = assessmentFixture()
    for (const fields of [a.fields.slice(1), [...a.fields.slice(1), a.fields[1]], a.fields.map((f, i) => i ? f : { ...f, key: 'private.health' }), a.fields.map((f, i) => i ? f : { ...f, critical: false }), a.fields.map((f, i) => i ? f : { ...f, state: 'Unknown', complete: true })]) expect(() => decodeOnboarding({ ...a, fields }, complianceVessel)).toThrow()
    expect(() => decodeOnboarding({ ...a, state: 'DECLARED_COMPLETE_UNVERIFIED' }, complianceVessel)).toThrow()
  })
  it('rejects foreign vessels, readiness, certificate assumptions and forged override authority', () => {
    const p = passportFixture()
    expect(() => decodePassport(p, otherComplianceVessel)).toThrow()
    for (const change of [{ state: 'READY' }, { certificateCoverage: 'COMPLETE' }, { onboarding: { ...p.onboarding, vesselId: otherComplianceVessel } }, { applicability: { ...p.applicability, manualOverrideAvailable: true } }, { plan: { ...p.plan, orphanCriticalCount: 0 } }, { plan: { ...p.plan, coverage: ['UNAVAILABLE'] } }]) expect(() => decodePassport({ ...p, ...change }, complianceVessel)).toThrow()
    expect(decodePassport(p, complianceVessel).plan.uncontrolledRequirementCount).toBeNull()
  })
  it('retains stale captured inputs with exact hashes without promoting current applicability', () => {
    const p = passportFixture(); p.applicability = recordedReviewFixture()
    expect(decodePassport(p, complianceVessel).applicability).toMatchObject({ available: true, current: false, inputs: { profileRevision: 1 } })
    expect(() => decodePassport({ ...p, applicability: { ...p.applicability, inputs: { ...p.applicability.inputs, vesselId: otherComplianceVessel } } }, complianceVessel)).toThrow()
    const inputs = capturedInputFixture()
    expect(() => decodePassport({ ...p, applicability: { ...p.applicability, inputs: { ...inputs, facts: { ...inputs.facts, 'private.health': inputs.facts['vessel.flag'] } } } }, complianceVessel)).toThrow()
    expect(() => decodePassport({ ...p, applicability: { ...p.applicability, inputHash: null } }, complianceVessel)).toThrow()
    expect(() => decodePassport({ ...p, applicability: { ...p.applicability, current: true, available: true, reviewerState: 'QUALIFIED_CURRENT', blockers: [] } }, complianceVessel)).toThrow()
  })
  it('rejects invented drilldown destinations and unpaired pins', () => {
    const p = passportFixture()
    expect(() => decodePassport({ ...p, applicability: { ...p.applicability, href: '/app/applicability' } }, complianceVessel)).toThrow()
    const requirement = { ruleId: otherComplianceVessel, ruleRevision: 1, requirementId: null, requirementRevision: 2, sourceId: null, sourceRevision: null, controlIds: [], state: 'REVIEW_REQUIRED', blockers: [] }
    expect(() => decodePassport({ ...p, plan: { ...p.plan, requirements: [requirement] } }, complianceVessel)).toThrow()
  })
})
