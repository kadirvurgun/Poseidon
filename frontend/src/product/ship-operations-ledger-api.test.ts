import { describe, expect, it } from 'vitest'
import { shipOpsVessel } from './ship-operation-test-fixtures'
import { fuelLedgerFixture, portTimelineFixture } from './ship-operations-ledger-test-fixtures'
import { decodeFuelLedger, decodePortTimeline, ticksToMinutes } from './ship-operations-ledger-api'

describe('T-OPS2-004 ROB continuity review aid contract', () => {
  it('accepts a review-required gap and keeps the declared record a draft', () => {
    const ledger = decodeFuelLedger(fuelLedgerFixture(), shipOpsVessel)
    expect(ledger.entries[0].assessment.continuity.differenceMt).toBe('-0.5')
    expect(ledger.entries[0].record.fields.verification_state.value).toBe('PENDING')
  })
  it('rejects a manufactured clean state, a difference on UNKNOWN and a dropped not-verification qualifier', () => {
    expect(() => decodeFuelLedger(fuelLedgerFixture({ reviewState: 'NO_DISCREPANCY_DETECTED' }), shipOpsVessel)).toThrow()
    expect(() => decodeFuelLedger(fuelLedgerFixture({ balance: { state: 'UNKNOWN', differenceMt: '0', reason: 'BALANCE_TERM_NOT_DECLARED' } }), shipOpsVessel)).toThrow()
    expect(() => decodeFuelLedger(fuelLedgerFixture({ balance: { state: 'NO_DISCREPANCY_DETECTED', differenceMt: '1.0', reason: null } }), shipOpsVessel)).toThrow()
    expect(() => decodeFuelLedger({ ...fuelLedgerFixture(), qualifications: ['NO_AUTOMATIC_CORRECTION'] }, shipOpsVessel)).toThrow()
    expect(() => decodeFuelLedger({ ...fuelLedgerFixture(), unit: 'm3' }, shipOpsVessel)).toThrow()
    expect(() => decodeFuelLedger({ ...fuelLedgerFixture(), verified: true }, shipOpsVessel)).toThrow()
  })
})

describe('T-OPS2-004 truncated history boundary', () => {
  const truncated = (assessment: Record<string, unknown>) => ({ ...fuelLedgerFixture(assessment), truncated: true, qualifications: [...fuelLedgerFixture().qualifications, 'HISTORY_TRUNCATED'] })
  const boundary = { reviewState: 'UNKNOWN', continuity: { state: 'UNKNOWN', differenceMt: null, reason: 'HISTORY_TRUNCATED' }, flags: [], basis: ['FUEL_TYPE_AGGREGATE', 'TANK_REFERENCE_UNAVAILABLE', 'HISTORY_TRUNCATED'] }
  it('accepts an UNKNOWN cut-off boundary only when the history is declared truncated', () => {
    expect(decodeFuelLedger(truncated(boundary), shipOpsVessel).entries[0].assessment.continuity.reason).toBe('HISTORY_TRUNCATED')
    expect(() => decodeFuelLedger(fuelLedgerFixture(boundary), shipOpsVessel)).toThrow()
    expect(() => decodeFuelLedger({ ...fuelLedgerFixture(), truncated: true }, shipOpsVessel)).toThrow()
  })
  it('rejects a boundary presented as continuous or as a gap, and a dropped tank basis', () => {
    expect(() => decodeFuelLedger(truncated({ ...boundary, reviewState: 'NO_DISCREPANCY_DETECTED', continuity: { state: 'NO_DISCREPANCY_DETECTED', differenceMt: '0', reason: 'HISTORY_TRUNCATED' } }), shipOpsVessel)).toThrow()
    expect(() => decodeFuelLedger(truncated({ ...boundary, basis: ['FUEL_TYPE_AGGREGATE', 'TANK_REFERENCE_UNAVAILABLE'] }), shipOpsVessel)).toThrow()
    expect(() => decodeFuelLedger(fuelLedgerFixture({ basis: ['FUEL_TYPE_AGGREGATE'] }), shipOpsVessel)).toThrow()
  })
})

describe('T-OPS2-005 SOF timeline contract', () => {
  it('accepts exact tick durations and an open end only as UNKNOWN without a value', () => {
    expect(decodePortTimeline(portTimelineFixture(), shipOpsVessel).entries[0].assessment.durationTicks).toBe('36000000000')
    expect(decodePortTimeline(portTimelineFixture({ durationState: 'UNKNOWN', durationTicks: null, reviewState: 'REVIEW_REQUIRED', flags: ['EVENT_END_NOT_DECLARED'] }), shipOpsVessel).entries).toHaveLength(1)
    expect(() => decodePortTimeline(portTimelineFixture({ durationState: 'UNKNOWN', durationTicks: '0' }), shipOpsVessel)).toThrow()
    expect(() => decodePortTimeline({ ...portTimelineFixture(), qualifications: ['DERIVED_REVIEW_AID_NOT_VERIFICATION'] }, shipOpsVessel)).toThrow()
  })
  it('converts ticks to exact minutes without floating point', () => {
    expect(ticksToMinutes('36000000000')).toBe('60')
    expect(ticksToMinutes('54000000001')).toBe('90.000000001…')
    expect(ticksToMinutes('300000000')).toBe('0.5')
    expect(ticksToMinutes('6000000000000000000000')).toBe('10000000000000')
  })
})
