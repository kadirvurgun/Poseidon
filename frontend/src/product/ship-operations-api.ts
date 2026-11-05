import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { mutableShipOpsFields, shipOpsCodeOptions, shipOpsFields, shipOpsRequired, shipOpsSourceVersion, type ShipOpsFieldDefinition, type ShipOpsFields, type ShipOpsKind } from './ship-operation-fields'
export type ShipOpsOffline = { supported: false; state: 'UNAVAILABLE'; operation: 'ShipOpsDraft' }
export type ShipOperationOriginPin = { kind: 'Noon' | 'Engine'; id: string; revision: number; version: number; hash: string; sourceVersion: string }
export type ShipOpsRecord = { id: string; vesselId: string; kind: ShipOpsKind; revision: number; version: number; fields: ShipOpsFields; state: 'DRAFT'; provenance: 'UserDeclaredUnverified' | 'SyntheticUnverified'; savedAt: string; hash: string; sourceVersion: string; blockers: string[]; currentRevision: boolean; canEdit: boolean; origin: ShipOperationOriginPin | null }
export type ShipOpsCapabilities = { vesselId: string; kind: ShipOpsKind; canRead: boolean; canWrite: boolean; canReview: false; canSubmit: false; schema: ShipOpsFieldDefinition[]; sourceVersion: string; offline: ShipOpsOffline }
export type ShipOpsPage = { items: ShipOpsRecord[]; offset: number; limit: 25; hasMore: boolean; canWrite: boolean; sourceVersion: string; offline: ShipOpsOffline }
export type ShipOpsHistory = { items: ShipOpsRecord[]; offset: number; limit: 25; hasMore: boolean }
export type ShipOpsAttempt = { owner: string; id: string; vessel: string; kind: ShipOpsKind; key: string; expectedVersion: number; body: string }
export const shipOpsOriginSourceVersion = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3'
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const integer = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const uint = (v: unknown, min = 1) => integer(v, min, 4294967295)
const hash = (v: unknown) => typeof v === 'string' && /^[a-fA-F0-9]{64}$/.test(v)
const text = (v: unknown, max: number, multiline = false): v is string => typeof v === 'string' && !!v.trim() && v.length <= max && !/\p{Cc}/u.test(multiline ? v.replace(/[\r\n\t]/g, '') : v)
export const validShipOpsInstant = (v: unknown): v is string => typeof v === 'string' && v.length <= 40 && !v.endsWith('-00:00') && timestamp(v)
// Compare the source's seven fractional digits without rounding to browser ms.
function instantTicks(value: string) { const fraction = /\.([0-9]{1,7})(?=Z|[+-][0-9]{2}:[0-9]{2}$)/.exec(value); return BigInt(Date.parse(value.replace(/\.[0-9]{1,7}(?=Z|[+-][0-9]{2}:[0-9]{2}$)/, ''))) * 10000n + BigInt((fraction?.[1] ?? '').padEnd(7, '0')) }
function eventOrder(fields: ShipOpsFields) { const start = fields.event_start, end = fields.event_end; return start?.state !== 'Known' || end?.state !== 'Known' || !validShipOpsInstant(start.value) || !validShipOpsInstant(end.value) || instantTicks(end.value) >= instantTicks(start.value) }
const sameOptions = (value: unknown, expected: readonly string[]) => Array.isArray(value) && value.length === expected.length && expected.every(option => value.includes(option))
function fail(): never { throw new ApiError(502, 'invalid_ship_operations_contract') }
function offline(value: unknown): ShipOpsOffline { if (!exact(value, ['supported', 'state', 'operation']) || value.supported !== false || value.state !== 'UNAVAILABLE' || value.operation !== 'ShipOpsDraft') fail(); return value as ShipOpsOffline }
export function decodeShipOpsOrigin(value: unknown, kind: ShipOpsKind): ShipOperationOriginPin {
  if (!exact(value, ['kind', 'id', 'revision', 'version', 'hash', 'sourceVersion']) || !(value.kind === 'Noon' || kind === 'Fuel' && value.kind === 'Engine') || !isGuid(value.id) || !integer(value.revision) || !uint(value.version) || !hash(value.hash) || value.sourceVersion !== shipOpsOriginSourceVersion) fail()
  return value as ShipOperationOriginPin
}
function facts(value: unknown, kind: ShipOpsKind, vessel: string, readonly: boolean): value is ShipOpsFields {
  const definitions = readonly ? shipOpsFields(kind) : mutableShipOpsFields(kind)
  if (!exact(value, definitions.map(field => field.key))) return false
  for (const field of definitions) {
    const fact = value[field.key]
    if (!exact(fact, ['state', 'value'])) return false
    if (fact.state === 'Known') {
      if (!text(fact.value, field.type === 'Narrative' ? 4000 : 512, field.type === 'Narrative')) return false
      if (field.type === 'Reference' && (field.key !== 'vessel_id' || fact.value !== vessel)) return false
      if (field.type === 'Number' && (fact.value.length > 48 || !/^-?[0-9]+(?:\.[0-9]+)?$/.test(fact.value))) return false
      if (field.type === 'Instant' && !validShipOpsInstant(fact.value)) return false
      if (field.type === 'Code' && !shipOpsCodeOptions[field.key]?.includes(fact.value)) return false
    } else if (!(fact.state === 'Missing' || fact.state === 'Unknown') || fact.value !== null) return false
  }
  if (readonly) {
    const required = kind === 'Fuel' ? { vessel_id: vessel, compliance_state: 'REVIEW_REQUIRED', verification_state: 'PENDING' } : { sof_state: 'DRAFT' }
    for (const [key, expected] of Object.entries(required)) { const fact = value[key]; if (!object(fact) || fact.state !== 'Known' || fact.value !== expected) return false }
  }
  return kind !== 'Port' || eventOrder(value as ShipOpsFields)
}
export function shipOpsFieldErrors(value: ShipOpsFields, kind: ShipOpsKind): Record<string, string[]> {
  const errors: Record<string, string[]> = {}
  for (const field of mutableShipOpsFields(kind)) {
    const fact = value[field.key]
    if (!fact || fact.state !== 'Known') continue
    const entry = fact.value
    if (!text(entry, field.type === 'Narrative' ? 4000 : 512, field.type === 'Narrative') || field.type === 'Reference' || field.type === 'Number' && (entry.length > 48 || !/^-?[0-9]+(?:\.[0-9]+)?$/.test(entry)) || field.type === 'Instant' && !validShipOpsInstant(entry) || field.type === 'Code' && !shipOpsCodeOptions[field.key]?.includes(entry)) errors[field.key] = ['invalid_declared_value']
  }
  if (kind === 'Port' && !eventOrder(value)) errors.event_end = ['event_order_invalid']
  return errors
}
export function decodeShipOpsRecord(value: unknown, vessel: string, kind: ShipOpsKind, id?: string): ShipOpsRecord {
  if (!isGuid(vessel) || !exact(value, ['id', 'vesselId', 'kind', 'revision', 'version', 'fields', 'state', 'provenance', 'savedAt', 'hash', 'sourceVersion', 'blockers', 'currentRevision', 'canEdit', 'origin']) || !isGuid(value.id) || id !== undefined && value.id !== id || value.vesselId !== vessel || value.kind !== kind || !integer(value.revision) || !uint(value.version) || !facts(value.fields, kind, vessel, true) || value.state !== 'DRAFT' || !(value.provenance === 'UserDeclaredUnverified' || value.provenance === 'SyntheticUnverified') || !timestamp(value.savedAt) || !hash(value.hash) || value.sourceVersion !== shipOpsSourceVersion || !Array.isArray(value.blockers) || value.blockers.length > 128 || !value.blockers.every(code => text(code, 200)) || typeof value.currentRevision !== 'boolean' || typeof value.canEdit !== 'boolean' || value.canEdit && !value.currentRevision) fail()
  return { ...value, origin: value.origin === null ? null : decodeShipOpsOrigin(value.origin, kind) } as ShipOpsRecord
}
export function decodeShipOpsCapabilities(value: unknown, vessel: string, kind: ShipOpsKind): ShipOpsCapabilities {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'kind', 'canRead', 'canWrite', 'canReview', 'canSubmit', 'schema', 'sourceVersion', 'offline']) || value.vesselId !== vessel || value.kind !== kind || typeof value.canRead !== 'boolean' || typeof value.canWrite !== 'boolean' || value.canWrite && !value.canRead || value.canReview !== false || value.canSubmit !== false || value.sourceVersion !== shipOpsSourceVersion || !Array.isArray(value.schema) || value.schema.length !== shipOpsFields(kind).length) fail()
  const schema: ShipOpsFieldDefinition[] = []
  for (const entry of value.schema) {
    if (!exact(entry, ['key', 'type', 'required', 'options', 'readOnly', 'referenceUnavailable', 'sourceRow', 'unit', 'requiredWhen']) || typeof entry.key !== 'string') fail()
    const info = shipOpsFields(kind).find(field => field.key === entry.key), options = shipOpsCodeOptions[entry.key] ?? [], required = shipOpsRequired[kind].includes(entry.key)
    if (!info || schema.some(field => field.key === entry.key) || entry.type !== info.type || entry.readOnly !== !!info.readOnly || entry.sourceRow !== info.sourceRow || entry.referenceUnavailable !== (info.type === 'Reference' && !info.readOnly) || entry.required !== required || entry.requiredWhen !== (required ? null : 'SOURCE_CONDITIONAL_UNRESOLVED') || entry.unit !== (info.key.endsWith('_mt') ? 'mt' : info.key === 'sulphur_pct' ? '%' : null) || !sameOptions(entry.options, options)) fail()
    schema.push(entry as ShipOpsFieldDefinition)
  }
  return { ...value, schema, offline: offline(value.offline) } as ShipOpsCapabilities
}
function page(value: unknown, vessel: string, kind: ShipOpsKind, offset: number, historyId?: string): ShipOpsHistory {
  if (!object(value) || !Array.isArray(value.items) || value.items.length > 25 || value.offset !== offset || value.limit !== 25 || typeof value.hasMore !== 'boolean') fail()
  const items = value.items.map(row => decodeShipOpsRecord(row, vessel, kind, historyId))
  if (new Set(items.map(row => historyId ? row.revision : row.id)).size !== items.length) fail()
  return { items, offset, limit: 25, hasMore: value.hasMore }
}
const resource = (kind: ShipOpsKind, vessel: string, id?: string) => { if (!isGuid(vessel) || id !== undefined && !isGuid(id)) throw new ApiError(400, 'invalid_context'); return (kind === 'Fuel' ? 'fuel-bunker-logs' : 'port-activity-sof') + (id ? '/' + id : '') + '?vesselId=' + vessel }
const offsetQuery = (offset: number) => { if (!integer(offset, 0, 100000)) throw new ApiError(400, 'invalid_context'); return String(offset) }
export function freezeShipOpsCommand(kind: ShipOpsKind, vessel: string, id: string, owner: string, fields: ShipOpsFields, record: ShipOpsRecord | null, origin: ShipOperationOriginPin | null): ShipOpsAttempt {
  if (!isGuid(id) || !isGuid(vessel) || owner.split('/').length !== 2 || !owner.split('/').every(isGuid) || !facts(fields, kind, vessel, false) || record && (record.id !== id || record.vesselId !== vessel || record.kind !== kind || !record.currentRevision || !record.canEdit)) throw new ApiError(400, 'invalid_draft_input')
  if (origin) decodeShipOpsOrigin(origin, kind)
  const expectedVersion = record?.version ?? 0
  return { owner, id, vessel, kind, key: crypto.randomUUID(), expectedVersion, body: JSON.stringify({ id, expectedVersion, fields, reasonCode: record ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING', origin }) }
}
export const shipOperationsApi = {
  capabilities: async (kind: ShipOpsKind, vessel: string, signal: AbortSignal) => { resource(kind, vessel); return decodeShipOpsCapabilities(await request<unknown>((kind === 'Fuel' ? 'fuel-bunker-logs' : 'port-activity-sof') + '/capabilities?vesselId=' + vessel, signal), vessel, kind) },
  list: async (kind: ShipOpsKind, vessel: string, offset: number, signal: AbortSignal): Promise<ShipOpsPage> => { const value = await request<unknown>(resource(kind, vessel) + '&offset=' + offsetQuery(offset), signal); if (!exact(value, ['items', 'offset', 'limit', 'hasMore', 'canWrite', 'sourceVersion', 'offline']) || typeof value.canWrite !== 'boolean' || value.sourceVersion !== shipOpsSourceVersion) fail(); return { ...page(value, vessel, kind, offset), canWrite: value.canWrite, sourceVersion: shipOpsSourceVersion, offline: offline(value.offline) } },
  read: async (kind: ShipOpsKind, vessel: string, id: string, signal: AbortSignal) => decodeShipOpsRecord(await request<unknown>(resource(kind, vessel, id), signal), vessel, kind, id),
  history: async (kind: ShipOpsKind, vessel: string, id: string, offset: number, signal: AbortSignal) => { const base = resource(kind, vessel, id).replace('?vesselId=', '/history?vesselId='); const value = await request<unknown>(base + '&offset=' + offsetQuery(offset), signal); if (!exact(value, ['items', 'offset', 'limit', 'hasMore'])) fail(); return page(value, vessel, kind, offset, id) },
  save: async (attempt: ShipOpsAttempt, csrf: string, signal: AbortSignal) => { const value = await request<unknown>(resource(attempt.kind, attempt.vessel, attempt.expectedVersion ? attempt.id : undefined), signal, { method: attempt.expectedVersion ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body }); const receipt = decodeShipOpsRecord(value, attempt.vessel, attempt.kind, attempt.id); if (receipt.currentRevision || receipt.canEdit) fail(); return receipt },
}
