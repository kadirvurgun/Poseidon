import { ApiError, request } from './api'
import { calendar, timestamp } from './date-validation'
import { isGuid } from './identifiers'
import { decodeShipOpsOrigin, type ShipOperationOriginPin } from './ship-operations-api'
import type { ShipOpsFact, ShipOpsKind } from './ship-operation-fields'
export type ShipOpsOriginContext = { vesselId: string; pin: ShipOperationOriginPin; current: boolean; state: 'DRAFT'; qualification: 'REVIEW_REQUIRED'; provenance: 'UserDeclaredUnverified' | 'SyntheticUnverified'; savedAt: string; fields: { key: string; type: 'Date' | 'Text' | 'Code' | 'Number'; value: ShipOpsFact; sourceRow: number; unit: null }[]; href: string }
export type ShipOpsOriginSelector = { kind: 'Noon' | 'Engine'; id: string; revision: number }
const keys = { Noon: [{ key: 'report_date', type: 'Date', row: 2128 }, { key: 'report_time_utc', type: 'Text', row: 2129 }, { key: 'activity', type: 'Code', row: 2134 }, { key: 'fuel_summary', type: 'Text', row: 2144 }, { key: 'rob_summary', type: 'Text', row: 2145 }], Engine: [{ key: 'log_date', type: 'Text', row: 2166 }, { key: 'watch_period', type: 'Code', row: 2167 }, { key: 'fuel_consumption', type: 'Number', row: 2172 }, { key: 'bunkering_activity', type: 'Text', row: 2179 }] }
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, names: string[]): value is Record<string, unknown> => object(value) && Object.keys(value).length === names.length && names.every(key => Object.hasOwn(value, key))
function fail(): never { throw new ApiError(502, 'invalid_ship_operation_origin') }
export function decodeShipOpsOriginContext(value: unknown, vessel: string, target: ShipOpsKind, selector: ShipOpsOriginSelector, expected?: ShipOperationOriginPin): ShipOpsOriginContext {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'pin', 'current', 'state', 'qualification', 'provenance', 'savedAt', 'fields', 'href']) || value.vesselId !== vessel || typeof value.current !== 'boolean' || value.state !== 'DRAFT' || value.qualification !== 'REVIEW_REQUIRED' || !(value.provenance === 'UserDeclaredUnverified' || value.provenance === 'SyntheticUnverified') || !timestamp(value.savedAt) || !Array.isArray(value.fields)) fail()
  const pin = decodeShipOpsOrigin(value.pin, target), source = keys[pin.kind]
  if (pin.kind !== selector.kind || pin.id !== selector.id || pin.revision !== selector.revision || expected && (pin.kind !== expected.kind || pin.id !== expected.id || pin.revision !== expected.revision || pin.version !== expected.version || pin.hash !== expected.hash || pin.sourceVersion !== expected.sourceVersion) || value.href !== '/app/' + (pin.kind === 'Noon' ? 'noon-report' : 'engine-daily-log') + '/' + vessel + '?id=' + pin.id || value.fields.length !== source.length) fail()
  const seen = new Set<string>()
  for (const field of value.fields) {
    if (!exact(field, ['key', 'type', 'value', 'sourceRow', 'unit']) || typeof field.key !== 'string' || seen.has(field.key) || field.unit !== null || !exact(field.value, ['state', 'value'])) fail()
    const definition = source.find(row => row.key === field.key), fact = field.value
    if (!definition || field.type !== definition.type || field.sourceRow !== definition.row) fail()
    if (fact.state === 'Known') { if (typeof fact.value !== 'string' || !fact.value.trim() || fact.value.length > 512 || /\p{Cc}/u.test(fact.value) || field.type === 'Date' && !calendar(fact.value)) fail() }
    else if (!(fact.state === 'Missing' || fact.state === 'Unknown') || fact.value !== null) fail()
    seen.add(field.key)
  }
  return { ...value, pin } as ShipOpsOriginContext
}
export async function readShipOpsOrigin(vessel: string, target: ShipOpsKind, selector: ShipOpsOriginSelector, signal: AbortSignal, expected?: ShipOperationOriginPin) {
  if (!isGuid(vessel) || !isGuid(selector.id) || !Number.isSafeInteger(selector.revision) || selector.revision < 1 || target === 'Port' && selector.kind !== 'Noon') throw new ApiError(400, 'invalid_context')
  return decodeShipOpsOriginContext(await request<unknown>('vessels/' + vessel + '/ship-operation-origins/' + selector.kind + '/' + selector.id + '?revision=' + selector.revision, signal), vessel, target, selector, expected)
}
