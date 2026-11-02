import { describe, expect, it } from 'vitest'
import { blankCalculation, blankCapability, completeCalculation, validCalculation, validCapability } from './tug-capability-contract'
import { blank, validContent } from './control-evidence-api'
import { blank as qualification, validContent as validQualification } from './qualification-api'
import { decodePlanningEvidence } from './tug-planning-api'

const tug = 'ceca1000-0000-4000-8000-000000000001'
const evidence = () => ({ availableBollardPull: null, capacityState: 'ReviewRequired', bp: [], conflictState: 'Unknown', conflicts: [], coverage: 'CurrentAuthorizedOwningFacts;DraftGeometry;MasterDecisionSeparate', readiness: 'ReviewRequired', ownedBp: [{ tugId: tug, state: 'Unverified', tonnes: null, why: 'CurrentIndependentVerificationRequired', evidenceId: null, evidenceRevision: null, conditionId: null, conditionRevision: null, ruleId: null, qualificationRevision: null, qualificationHash: null, validUntil: null, observedAt: null }], readinessFacts: [], operationalConflicts: [] })
describe('owned capability and source policy wire', () => {
  it('checks complete capacity aggregation with exact decimal arithmetic rather than trusting a scalar total', () => {
    const value = evidence()
    const owned = { ...value.ownedBp[0], state: 'CurrentPlanningCapacity', tonnes: '66.294', evidenceId: tug, evidenceRevision: 3,
      conditionId: tug, conditionRevision: 2, ruleId: tug, qualificationRevision: 4, qualificationHash: 'A'.repeat(64), validUntil: '2026-11-03T00:00:00Z', observedAt: '2026-11-01T00:00:00Z' }
    const complete = { ...value, ownedBp: [owned], capacityState: 'CurrentPlanningCapacity', availableBollardPull: '66.294' }
    expect(decodePlanningEvidence(complete, [tug]).availableBollardPull).toBe('66.294')
    expect(() => decodePlanningEvidence({ ...complete, availableBollardPull: '66.295' }, [tug])).toThrow()
  })
  it('retains incomplete drafts and nullable choices without default factors or reference approvals', () => {
    expect(validCapability(blankCapability('BollardPullTest'))).toBe(true)
    expect(validContent({ ...blank(), tugCapability: blankCapability('Condition') })).toBe(true)
    expect(validCalculation(blankCalculation())).toBe(true)
    expect(completeCalculation(blankCalculation())).toBe(false)
    expect(validQualification({ ...qualification(), tugCalculation: blankCalculation() })).toBe(true)
  })
  it.each([1, '1e0', '1,0', '1.0000001', '+1', '01', '1\n'])('rejects coerced, lossy or unsupported coefficient %j', raw => {
    expect(validCapability({ ...blankCapability('Condition'), conditionFactor: raw })).toBe(false)
  })
  it('rejects missing nested fields, foreign kinds, zone-free dates and mismatched pins', () => {
    const { measuredTonnes: _omitted, ...missing } = blankCapability('BollardPullTest')
    expect(validCapability(missing)).toBe(false)
    expect(validCapability({ ...blankCapability('Condition'), measuredTonnes: '70' })).toBe(false)
    expect(validCapability({ ...blankCapability('BollardPullTest'), testAt: '2026-11-02T12:00:00' })).toBe(false)
    expect(validCapability({ ...blankCapability('BollardPullTest'), conditionEvidenceId: tug })).toBe(false)
    expect(validCalculation({ ...blankCalculation(), formula: 'EscortDefault' })).toBe(false)
  })
  it('fails closed on forged green, partial sums, omitted adapters and foreign projections', () => {
    expect(decodePlanningEvidence(evidence(), [tug]).availableBollardPull).toBeNull()
    for (const patch of [{ readiness: 'Ready' }, { availableBollardPull: '70' }, { capacityState: 'CurrentPlanningCapacity' }, { ownedBp: [] }, { readinessFacts: [{ tugId: 'foreign' }] }]) {
      // Empty owning rows can legitimately mean no source, but cannot validate a nonnull total.
      if ('ownedBp' in patch) expect(() => decodePlanningEvidence({ ...evidence(), ...patch, availableBollardPull: '70' }, [tug])).toThrow()
      else expect(() => decodePlanningEvidence({ ...evidence(), ...patch }, [tug])).toThrow()
    }
    const { ownedBp: _omitted, ...missing } = evidence()
    expect(() => decodePlanningEvidence(missing, [tug])).toThrow()
  })
})
