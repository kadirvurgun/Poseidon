// Synthetic DEV-100/101 ledger transport fixtures (component and contract tests only).
import { shipOpsSourceVersion } from './ship-operation-fields'
import { shipOpsRecordFixture, shipOpsRecordId, shipOpsVessel } from './ship-operation-test-fixtures'

const fuelQualifications = ['DERIVED_REVIEW_AID_NOT_VERIFICATION', 'NO_AUTOMATIC_CORRECTION', 'MISSING_TERM_NOT_ASSUMED_ZERO', 'NO_TOLERANCE_APPROVED', 'NO_COMPUTED_EMISSIONS', 'FUEL_TYPE_ONLY_TANK_REFERENCE_UNAVAILABLE', 'TANK_REFERENCE_UNAVAILABLE']
const portQualifications = ['DERIVED_REVIEW_AID_NOT_VERIFICATION', 'SOF_SIGNATURE_NOT_RECORDED', 'SOF_FINALIZATION_UNAVAILABLE', 'OPEN_END_NOT_ASSUMED', 'NOT_PORT_OPERATING_SYSTEM']
export function fuelLedgerFixture(assessment: Record<string, unknown> = {}) {
  return { vesselId: shipOpsVessel, window: 100, truncated: false, unit: 'mt', grouping: 'FUEL_TYPE_ONLY_TANK_REFERENCE_UNAVAILABLE', qualifications: fuelQualifications, sourceVersion: shipOpsSourceVersion, unplaced: [],
    entries: [{ record: shipOpsRecordFixture(), assessment: { recordId: shipOpsRecordId, reviewState: 'REVIEW_REQUIRED', balance: { state: 'NO_DISCREPANCY_DETECTED', differenceMt: '0.0', reason: null }, continuity: { state: 'REVIEW_REQUIRED', differenceMt: '-0.5', reason: 'ROB_CONTINUITY_GAP' }, previousRecordId: null, flags: ['ROB_CONTINUITY_GAP'], basis: ['FUEL_TYPE_AGGREGATE', 'TANK_REFERENCE_UNAVAILABLE'], ...assessment } }] }
}
export function portTimelineFixture(assessment: Record<string, unknown> = {}) {
  return { vesselId: shipOpsVessel, window: 100, truncated: false, durationUnit: 'tick', qualifications: portQualifications, sourceVersion: shipOpsSourceVersion, unplaced: [],
    entries: [{ record: shipOpsRecordFixture('Port'), assessment: { recordId: shipOpsRecordId, reviewState: 'NO_DISCREPANCY_DETECTED', durationState: 'KNOWN', durationTicks: '36000000000', flags: [], ...assessment } }] }
}
