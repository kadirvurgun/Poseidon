import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { shipOpsSourceVersion } from './ship-operation-fields'
import { decodeShipOpsRecord, type ShipOpsRecord } from './ship-operations-api'

// DEV-100 / DEV-101 derived review aid. Every finding is REVIEW_REQUIRED for a
// human; UNKNOWN stays UNKNOWN and "no discrepancy detected" is never shown as
// verified, ready or compliant.
export type LedgerReviewState = 'REVIEW_REQUIRED' | 'UNKNOWN' | 'NO_DISCREPANCY_DETECTED'
export type FuelLedgerCheck = { state: LedgerReviewState; differenceMt: string | null; reason: string | null }
export type FuelLedgerAssessment = { recordId: string; reviewState: LedgerReviewState; balance: FuelLedgerCheck; continuity: FuelLedgerCheck; previousRecordId: string | null; flags: string[]; basis: string[] }
export type FuelLedgerEntry = { record: ShipOpsRecord; assessment: FuelLedgerAssessment }
export type FuelLedger = { vesselId: string; entries: FuelLedgerEntry[]; unplaced: ShipOpsRecord[]; window: number; truncated: boolean; unit: 'mt'; grouping: string; qualifications: string[]; sourceVersion: string }
export type PortTimelineAssessment = { recordId: string; reviewState: LedgerReviewState; durationState: 'KNOWN' | 'UNKNOWN'; durationTicks: string | null; flags: string[] }
export type PortTimelineEntry = { record: ShipOpsRecord; assessment: PortTimelineAssessment }
export type PortTimeline = { vesselId: string; entries: PortTimelineEntry[]; unplaced: ShipOpsRecord[]; window: number; truncated: boolean; durationUnit: 'tick'; qualifications: string[]; sourceVersion: string }

const states: readonly string[] = ['REVIEW_REQUIRED', 'UNKNOWN', 'NO_DISCREPANCY_DETECTED']
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const code = (v: unknown) => typeof v === 'string' && /^[A-Z][A-Z0-9_]{0,79}$/.test(v)
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 32 && v.every(code) && new Set(v).size === v.length
const decimal = (v: unknown) => v === null || typeof v === 'string' && /^-?[0-9]{1,30}(?:\.[0-9]{1,30})?$/.test(v)
function fail(): never { throw new ApiError(502, 'invalid_ship_operations_contract') }
function check(v: unknown): FuelLedgerCheck {
  if (!exact(v, ['state', 'differenceMt', 'reason']) || typeof v.state !== 'string' || !states.includes(v.state) || !decimal(v.differenceMt) || !(v.reason === null || code(v.reason))) fail()
  // A difference only exists when every term was declared: UNKNOWN carries none.
  if (v.state === 'UNKNOWN' ? v.differenceMt !== null : v.differenceMt === null) fail()
  if (v.state === 'NO_DISCREPANCY_DETECTED' && !/^-?0(?:\.0+)?$/.test(v.differenceMt as string)) fail()
  return v as FuelLedgerCheck
}
function overall(state: unknown, flags: string[], parts: string[]) {
  const expected = flags.length || parts.includes('REVIEW_REQUIRED') ? 'REVIEW_REQUIRED' : parts.includes('UNKNOWN') ? 'UNKNOWN' : 'NO_DISCREPANCY_DETECTED'
  if (state !== expected) fail()
}
function common(value: unknown, vessel: string, keys: readonly string[]) {
  if (!isGuid(vessel) || !exact(value, keys) || value.vesselId !== vessel || value.window !== 100 || typeof value.truncated !== 'boolean' || value.sourceVersion !== shipOpsSourceVersion || !codes(value.qualifications) ||
    !value.qualifications.includes('DERIVED_REVIEW_AID_NOT_VERIFICATION') || value.qualifications.includes('HISTORY_TRUNCATED') !== value.truncated || !Array.isArray(value.entries) || !Array.isArray(value.unplaced) || value.entries.length + value.unplaced.length > 100) fail()
  return value
}
export function decodeFuelLedger(input: unknown, vessel: string): FuelLedger {
  const value = common(input, vessel, ['vesselId', 'entries', 'unplaced', 'window', 'truncated', 'unit', 'grouping', 'qualifications', 'sourceVersion'])
  if (value.unit !== 'mt' || value.grouping !== 'FUEL_TYPE_ONLY_TANK_REFERENCE_UNAVAILABLE' || !(value.qualifications as string[]).includes('MISSING_TERM_NOT_ASSUMED_ZERO') || !(value.qualifications as string[]).includes('TANK_REFERENCE_UNAVAILABLE')) fail()
  const entries = (value.entries as unknown[]).map(entry => {
    if (!exact(entry, ['record', 'assessment']) || !exact(entry.assessment, ['recordId', 'reviewState', 'balance', 'continuity', 'previousRecordId', 'flags', 'basis'])) fail()
    const record = decodeShipOpsRecord(entry.record, vessel, 'Fuel'), a = entry.assessment
    if (a.recordId !== record.id || !(a.previousRecordId === null || isGuid(a.previousRecordId)) || !codes(a.flags) || !codes(a.basis) || !a.basis.includes('TANK_REFERENCE_UNAVAILABLE') || record.canEdit && !record.currentRevision) fail()
    const balance = check(a.balance), continuity = check(a.continuity)
    overall(a.reviewState, a.flags, [balance.state, continuity.state])
    return { record, assessment: { ...a, balance, continuity } as FuelLedgerAssessment }
  })
  // A cut-off boundary is never continuous and never a gap: it must be UNKNOWN / HISTORY_TRUNCATED.
  for (const { assessment } of entries) if ((assessment.continuity.reason === 'HISTORY_TRUNCATED') !== assessment.basis.includes('HISTORY_TRUNCATED') || assessment.continuity.reason === 'HISTORY_TRUNCATED' && (!value.truncated || assessment.continuity.state !== 'UNKNOWN')) fail()
  const unplaced = (value.unplaced as unknown[]).map(row => decodeShipOpsRecord(row, vessel, 'Fuel'))
  if (new Set([...entries.map(e => e.record.id), ...unplaced.map(r => r.id)]).size !== entries.length + unplaced.length) fail()
  return { ...(value as unknown as FuelLedger), entries, unplaced }
}
export function decodePortTimeline(input: unknown, vessel: string): PortTimeline {
  const value = common(input, vessel, ['vesselId', 'entries', 'unplaced', 'window', 'truncated', 'durationUnit', 'qualifications', 'sourceVersion'])
  if (value.durationUnit !== 'tick' || !(value.qualifications as string[]).includes('SOF_SIGNATURE_NOT_RECORDED')) fail()
  const entries = (value.entries as unknown[]).map(entry => {
    if (!exact(entry, ['record', 'assessment']) || !exact(entry.assessment, ['recordId', 'reviewState', 'durationState', 'durationTicks', 'flags'])) fail()
    const record = decodeShipOpsRecord(entry.record, vessel, 'Port'), a = entry.assessment
    if (a.recordId !== record.id || !codes(a.flags) || record.fields.sof_state.value !== 'DRAFT') fail()
    if (a.durationState === 'KNOWN' ? typeof a.durationTicks !== 'string' || !/^[0-9]{1,20}$/.test(a.durationTicks) : a.durationState !== 'UNKNOWN' || a.durationTicks !== null) fail()
    if (a.reviewState !== (a.flags.length ? 'REVIEW_REQUIRED' : 'NO_DISCREPANCY_DETECTED')) fail()
    return { record, assessment: a as unknown as PortTimelineAssessment }
  })
  const unplaced = (value.unplaced as unknown[]).map(row => decodeShipOpsRecord(row, vessel, 'Port'))
  if (new Set([...entries.map(e => e.record.id), ...unplaced.map(r => r.id)]).size !== entries.length + unplaced.length) fail()
  return { ...(value as unknown as PortTimeline), entries, unplaced }
}
// Exact elapsed minutes from 100 ns ticks without floating point: integer
// minutes plus a lossless decimal remainder (7 digits per tick fraction).
export function ticksToMinutes(ticks: string): string {
  const value = BigInt(ticks), unit = 600000000n, whole = value / unit, rest = value % unit
  if (rest === 0n) return whole.toString()
  return whole.toString() + '.' + (rest * 10n ** 9n / unit).toString().padStart(9, '0').replace(/0+$/, '') + (rest * 10n ** 9n % unit ? '…' : '')
}
export const shipOperationsLedgerApi = {
  fuel: async (vessel: string, signal: AbortSignal) => { if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context'); return decodeFuelLedger(await request<unknown>('fuel-bunker-logs/ledger?vesselId=' + vessel, signal), vessel) },
  port: async (vessel: string, signal: AbortSignal) => { if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context'); return decodePortTimeline(await request<unknown>('port-activity-sof/timeline?vesselId=' + vessel, signal), vessel) },
}
