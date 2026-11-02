import { describe, expect, it } from 'vitest'
import { decodeEvaluation, decodeReason, decodeRule, ruleContract } from './applicability-api'

export const ruleFixture = () => ({ version: 7, current: { id: '10000000-0000-4000-8000-000000000001', revision: 1,
  contract: ruleContract, ruleJson: null, rule: null, hash: 'A'.repeat(64), sourceId: null, sourceRevision: null, requirementId: null, requirementRevision: null,
  lifecycle: 'DRAFT', requestedLifecycle: 'DRAFT', reviewStatus: 'NOT_REQUESTED', savedAt: '2026-11-02T00:00:00Z' } })
// Contract fixture only, not a captured approved host result or vessel fact.
export const simulationFixture = () => {
  const fields: Record<string, number> = {
    'context.mode': 0, evaluationDate: 2, 'vessel.flag': 0, 'vessel.type': 0, 'vessel.gt': 1, 'vessel.mainPropulsionKw': 1,
    'vessel.buildDate': 2, 'vessel.contractForConstructionDate': 2, 'vessel.classSociety': 0, 'vessel.tradingArea': 0, 'vessel.operatingArea': 0,
    'operation.jobType': 0, 'operation.profile': 0, 'operation.serviceContext': 0, 'cargo.packagedDangerousGoods': 3, 'cargo.type': 0,
    'equipment.towingWinchFitted': 3, 'class.rulePack.IACS_M79': 3, 'regulatory.ismApplicable': 3, 'regulatory.msmdVerified': 3,
    'crew.requiredRoleEligible': 3, 'evidence.sourceVerified': 3, 'source.lifecycle': 0,
  }
  const facts = Object.fromEntries(Object.entries(fields).map(([field, type]) => [field, { type, knowledge: 'Unknown', value: null as unknown, provenance: 'AdapterUnavailable', revision: 'controlled-input-catalog-v1', observedAt: null, freshUntil: null, authoritativeEmpty: false }]))
  return { id: '10000000-0000-4000-8000-000000000002', vesselId: ruleFixture().current.id, savedAt: '2026-11-02T00:00:00Z', asOf: '2026-11-02T00:00:00Z',
    adapterVersion: 'controlled-input-catalog-v1', profileRevision: null, snapshotHash: 'B'.repeat(64), ruleSetHash: 'C'.repeat(64),
    snapshot: { adapterVersion: 'controlled-input-catalog-v1', vesselId: ruleFixture().current.id, vesselVersion: 19, profileRevision: null, asOf: '2026-11-02T00:00:00Z', facts },
    results: [{ ruleId: ruleFixture().current.id, revision: 1, hash: 'A'.repeat(64), sourceId: null, sourceRevision: null, requirementId: null, requirementRevision: null,
      prediction: { result: 'REVIEW_REQUIRED', currentResult: 'REVIEW_REQUIRED', authoritative: false, lifecycle: 'DRAFT', reason: { path: 'when', truth: 0, reason: 'Missing', field: 'vessel.flag', operator: 'EQ', children: [], reviewRequired: true }, predictedActivations: [], blockers: ['preview_only'] } }],
    conflicts: [], preview: true, simulation: true, authoritative: false, currentResult: 'REVIEW_REQUIRED' }
}
describe('LA-08 strict consumer boundaries (AUTHORED / NOT RUN)', () => {
  it('keeps incomplete drafts explicit and refuses admitted lifecycle claims', () => {
    expect(decodeRule(ruleFixture()).current.rule).toBeNull()
    for (const lifecycle of ['IN_FORCE', ['DRAFT'], null]) expect(() => decodeRule({ ...ruleFixture(), current: { ...ruleFixture().current, lifecycle } })).toThrow()
    expect(() => decodeRule({ ...ruleFixture(), actorContext: {} })).toThrow()
    expect(() => decodeRule({ ...ruleFixture(), current: { ...ruleFixture().current, sourceRevision: 1 } })).toThrow()
  })
  it('rejects source trust expansion and private fields in the returned context', () => {
    const value = simulationFixture(); expect(decodeEvaluation(value).preview).toBe(true)
    expect(() => decodeEvaluation({ ...value, authoritative: true })).toThrow()
    expect(() => decodeEvaluation({ ...value, currentResult: 'READY' })).toThrow()
    expect(() => decodeEvaluation({ ...value, snapshot: { ...value.snapshot, medicalExpiry: '2030-01-01' } })).toThrow()
    expect(() => decodeEvaluation({ ...value, snapshot: { ...value.snapshot, facts: { ...value.snapshot.facts, 'private.health': {} } } })).toThrow()
  })
  it('preserves true false unknown as actual enums and bounds hostile reason trees', () => {
    const reason = simulationFixture().results[0].prediction.reason
    expect(decodeReason(reason).truth).toBe(0)
    for (const truth of [[0], '0', true, null, 3]) expect(() => decodeReason({ ...reason, truth })).toThrow()
    let tree: unknown = reason
    for (let i = 0; i < 15; i++) tree = { ...reason, children: [tree] }
    expect(() => decodeReason(tree)).toThrow()
  })
  it('retains zero and false without numeric coercion or privacy field widening', () => {
    const value = simulationFixture()
    value.snapshot.facts['vessel.gt'] = { ...value.snapshot.facts['vessel.gt'], knowledge: 'Unverified', value: '0' }
    value.snapshot.facts['cargo.packagedDangerousGoods'] = { ...value.snapshot.facts['cargo.packagedDangerousGoods'], knowledge: 'Unverified', value: false }
    expect(decodeEvaluation(value).authoritative).toBe(false)
    value.snapshot.facts['vessel.gt'].value = 0
    expect(() => decodeEvaluation(value)).toThrow()
  })
  it('retains exact saved rule text for editing instead of JSON number reserialization', () => {
    const text = '{"when":{"field":"vessel.gt","operator":"GT","value":9007199254740992.0000000001}}'
    const value = { ...ruleFixture(), current: { ...ruleFixture().current, ruleJson: text, rule: JSON.parse(text) as Record<string, unknown> } }
    expect(decodeRule(value).current.ruleJson).toBe(text)
    expect(() => decodeRule({ ...value, current: { ...value.current, ruleJson: '{"when":false}' } })).toThrow()
  })
})
