import { ApiError, request } from './api'
import { calendar, timestamp } from './date-validation'
import { isGuid } from './identifiers'
import { workOrderFieldInfo, workOrderSchema, workOrderSourceVersion, type WorkOrderFieldDefinition, type WorkOrderFields } from './work-order-fields'
export type WorkOrderPlanPin = { id: string; revision: number; version: number; hash: string; sourceVersion: 'poseidon.maintenance-plan-context.v1' }
export type WorkOrderRecord = { id: string; vesselId: string; revision: number; version: number; fields: WorkOrderFields; plan: WorkOrderPlanPin | null; sourceVersion: string; hash: string; provenance: string; savedAt: string; state: 'DRAFT'; qualification: 'REVIEW_REQUIRED'; blockers: string[]; currentRevision: boolean; canEdit: boolean }
export type WorkOrderCapabilities = { vesselId: string; canRead: boolean; canWrite: boolean; canClose: false; canVerify: false; canRelease: false; canSubmitEvidence: false; schema: WorkOrderFieldDefinition[]; sourceVersion: string; offline: { supported: false; state: 'UNAVAILABLE' } }
export type WorkOrderHistory = { items: WorkOrderRecord[]; offset: number; limit: 25; hasMore: boolean }
export type WorkOrderPage = WorkOrderHistory & { canWrite: boolean; sourceVersion: string }
export type WorkOrderPlanContext = { vesselId: string; pin: WorkOrderPlanPin; current: true; qualification: 'REVIEW_REQUIRED'; provenance: string; savedAt: string; plan: { assetId: string; assetRevision: number; jobId: string | null; status: 'Draft' }; asset: { code: string | null; name: string | null; level: 'System' | 'Equipment' | 'Component'; criticality: string | null; maker: string | null; model: string | null; location: string | null }; href: string }
export type WorkOrderAttempt = { owner: string; vessel: string; id: string; expectedVersion: number; key: string; body: string }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const integer = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const uint = (v: unknown) => integer(v, 1, 4294967295)
const text = (v: unknown, max = 256, multiline = false): v is string => typeof v === 'string' && !!v.trim() && v.length <= max && !/\p{Cc}/u.test(multiline ? v.replace(/[\r\n\t]/g, '') : v)
const hash = (v: unknown): v is string => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 128 && v.every(code => text(code, 256))
const sameOptions = (v: unknown, expected: readonly string[]) => Array.isArray(v) && v.length === expected.length && expected.every(option => v.includes(option))
function fail(): never { throw new ApiError(502, 'invalid_work_order_contract') }
export function validWorkOrderPlanPin(v: unknown): v is WorkOrderPlanPin { return exact(v, ['id', 'revision', 'version', 'hash', 'sourceVersion']) && isGuid(v.id) && integer(v.revision) && uint(v.version) && hash(v.hash) && v.sourceVersion === 'poseidon.maintenance-plan-context.v1' }
function validFact(v: unknown, field: WorkOrderFieldDefinition) {
  if (!exact(v, ['state', 'value'])) return false
  if (v.state === 'Missing' || v.state === 'Unknown') return v.value === null
  if (field.knownUnavailable || v.state !== 'Known' || !text(v.value, field.type === 'Narrative' ? 4000 : 256, field.type === 'Narrative')) return false
  return (field.type !== 'Code' || field.options.includes(v.value)) && (field.type !== 'Date' || calendar(v.value)) && (field.type !== 'Number' || v.value.length <= 48 && /^-?[0-9]+(?:\.[0-9]+)?$/.test(v.value))
}
export function workOrderInputErrors(fields: WorkOrderFields): Record<string, string[]> {
  const errors: Record<string, string[]> = Object.fromEntries(workOrderSchema().filter(field => !validFact(fields[field.key], field)).map(field => [field.key, ['invalid_declared_value']]))
  for (const [start, end] of [['planned_start', 'planned_finish'], ['actual_start', 'actual_finish']] as const) { const from = fields[start], to = fields[end]; if (from.state === 'Known' && to.state === 'Known' && from.value && to.value && calendar(from.value) && calendar(to.value) && to.value < from.value) errors[end] = ['work_order_date_order_invalid'] }
  return errors
}
function validFields(v: unknown): v is WorkOrderFields { return exact(v, workOrderFieldInfo().map(field => field.key)) && workOrderSchema().every(field => validFact(v[field.key], field)) && Object.keys(workOrderInputErrors(v as WorkOrderFields)).length === 0 }
export function decodeWorkOrderRecord(value: unknown, vessel: string, id?: string): WorkOrderRecord {
  if (!isGuid(vessel) || !exact(value, ['id', 'vesselId', 'revision', 'version', 'fields', 'plan', 'sourceVersion', 'hash', 'provenance', 'savedAt', 'state', 'qualification', 'blockers', 'currentRevision', 'canEdit']) || !isGuid(value.id) || id !== undefined && value.id !== id || value.vesselId !== vessel || !integer(value.revision) || !uint(value.version) || !validFields(value.fields) || !(value.plan === null || validWorkOrderPlanPin(value.plan)) || value.sourceVersion !== workOrderSourceVersion || !hash(value.hash) || !text(value.provenance) || !timestamp(value.savedAt) || value.state !== 'DRAFT' || value.qualification !== 'REVIEW_REQUIRED' || !codes(value.blockers) || typeof value.currentRevision !== 'boolean' || typeof value.canEdit !== 'boolean' || value.canEdit && !value.currentRevision) fail()
  return value as WorkOrderRecord
}
export function decodeWorkOrderCapabilities(value: unknown, vessel: string): WorkOrderCapabilities {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'canRead', 'canWrite', 'canClose', 'canVerify', 'canRelease', 'canSubmitEvidence', 'schema', 'sourceVersion', 'offline']) || value.vesselId !== vessel || typeof value.canRead !== 'boolean' || typeof value.canWrite !== 'boolean' || value.canWrite && !value.canRead || value.canClose !== false || value.canVerify !== false || value.canRelease !== false || value.canSubmitEvidence !== false || value.sourceVersion !== workOrderSourceVersion || !Array.isArray(value.schema) || value.schema.length !== workOrderSchema().length || !exact(value.offline, ['supported', 'state']) || value.offline.supported !== false || value.offline.state !== 'UNAVAILABLE') fail()
  const schema: WorkOrderFieldDefinition[] = []
  for (const field of value.schema) {
    if (!exact(field, ['key', 'type', 'required', 'options', 'sourceLocator', 'requiredWhen', 'knownUnavailable'])) fail()
    const expected = workOrderSchema().find(entry => entry.key === field.key)
    if (!expected || schema.some(entry => entry.key === field.key) || field.type !== expected.type || field.required !== expected.required || !sameOptions(field.options, expected.options) || field.sourceLocator !== expected.sourceLocator || field.requiredWhen !== expected.requiredWhen || field.knownUnavailable !== expected.knownUnavailable) fail()
    schema.push(field as WorkOrderFieldDefinition)
  }
  return { ...value, schema } as WorkOrderCapabilities
}
export function decodeWorkOrderPlanContext(value: unknown, vessel: string, id: string, revision?: number, expected?: WorkOrderPlanPin): WorkOrderPlanContext {
  if (!exact(value, ['vesselId', 'pin', 'current', 'qualification', 'provenance', 'savedAt', 'plan', 'asset', 'href']) || value.vesselId !== vessel || !validWorkOrderPlanPin(value.pin) || value.pin.id !== id || revision !== undefined && value.pin.revision !== revision || expected && (value.pin.id !== expected.id || value.pin.revision !== expected.revision || value.pin.version !== expected.version || value.pin.hash !== expected.hash || value.pin.sourceVersion !== expected.sourceVersion) || value.current !== true || value.qualification !== 'REVIEW_REQUIRED' || !text(value.provenance) || !timestamp(value.savedAt) || !exact(value.plan, ['assetId', 'assetRevision', 'jobId', 'status']) || !isGuid(value.plan.assetId) || !integer(value.plan.assetRevision) || !(value.plan.jobId === null || isGuid(value.plan.jobId)) || value.plan.status !== 'Draft' || !exact(value.asset, ['code', 'name', 'level', 'criticality', 'maker', 'model', 'location']) || typeof value.asset.level !== 'string' || !['System', 'Equipment', 'Component'].includes(value.asset.level) || value.href !== '/app/maintenance-planning/' + vessel + '?id=' + id) fail()
  for (const key of ['code', 'name', 'criticality', 'maker', 'model', 'location']) { const entry = value.asset[key]; if (entry !== null && !(typeof entry === 'string' && entry.length <= 4096 && !/\p{Cc}/u.test(entry))) fail() }
  return value as WorkOrderPlanContext
}
function history(value: unknown, vessel: string, offset: number, id?: string): WorkOrderHistory {
  if (!object(value) || !Array.isArray(value.items) || value.items.length > 25 || value.offset !== offset || value.limit !== 25 || typeof value.hasMore !== 'boolean') fail()
  const items = value.items.map(item => decodeWorkOrderRecord(item, vessel, id))
  if (new Set(items.map(item => id ? item.revision : item.id)).size !== items.length) fail()
  return { items, offset, limit: 25, hasMore: value.hasMore }
}
const root = (vessel: string, id?: string) => { if (!isGuid(vessel) || id !== undefined && !isGuid(id)) throw new ApiError(400, 'invalid_context'); return 'work-orders' + (id ? '/' + id : '') + '?vessel=' + vessel }
const offsetQuery = (offset: number) => { if (!integer(offset, 0, 100000)) throw new ApiError(400, 'invalid_context'); return String(offset) }
export function freezeWorkOrderCommand(vessel: string, id: string, owner: string, fields: WorkOrderFields, record: WorkOrderRecord | null, plan: WorkOrderPlanPin | null): WorkOrderAttempt {
  if (!isGuid(vessel) || !isGuid(id) || owner.split('/').length !== 2 || !owner.split('/').every(isGuid) || !validFields(fields) || plan !== null && !validWorkOrderPlanPin(plan) || record && (record.id !== id || record.vesselId !== vessel || !record.currentRevision || !record.canEdit)) throw new ApiError(400, 'invalid_work_order_input')
  return { owner, vessel, id, expectedVersion: record?.version ?? 0, key: crypto.randomUUID(), body: JSON.stringify({ id, expectedVersion: record?.version ?? 0, fields, plan, sourceVersion: workOrderSourceVersion }) }
}
export const workOrderApi = {
  capabilities: async (vessel: string, signal: AbortSignal) => { root(vessel); return decodeWorkOrderCapabilities(await request<unknown>('work-orders/capabilities?vessel=' + vessel, signal), vessel) },
  read: async (vessel: string, id: string, signal: AbortSignal) => decodeWorkOrderRecord(await request<unknown>(root(vessel, id), signal), vessel, id),
  list: async (vessel: string, offset: number, signal: AbortSignal): Promise<WorkOrderPage> => { const value = await request<unknown>(root(vessel) + '&offset=' + offsetQuery(offset), signal); if (!exact(value, ['items', 'offset', 'limit', 'hasMore', 'canWrite', 'sourceVersion']) || typeof value.canWrite !== 'boolean' || value.sourceVersion !== workOrderSourceVersion) fail(); return { ...history(value, vessel, offset), canWrite: value.canWrite, sourceVersion: workOrderSourceVersion } },
  history: async (vessel: string, id: string, offset: number, signal: AbortSignal) => { const value = await request<unknown>(root(vessel, id).replace('?vessel=', '/history?vessel=') + '&offset=' + offsetQuery(offset), signal); if (!exact(value, ['items', 'offset', 'limit', 'hasMore'])) fail(); return history(value, vessel, offset, id) },
  planContext: async (vessel: string, id: string, revision: number, signal: AbortSignal, expected?: WorkOrderPlanPin) => { root(vessel, id); if (!integer(revision)) throw new ApiError(400, 'invalid_context'); return decodeWorkOrderPlanContext(await request<unknown>('vessels/' + vessel + '/work-order-plan-origins/' + id + '?revision=' + revision, signal), vessel, id, revision, expected) },
  save: async (attempt: WorkOrderAttempt, csrf: string, signal: AbortSignal) => { const value = await request<unknown>(root(attempt.vessel, attempt.expectedVersion ? attempt.id : undefined), signal, { method: attempt.expectedVersion ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body }); const receipt = decodeWorkOrderRecord(value, attempt.vessel, attempt.id); if (receipt.currentRevision || receipt.canEdit) fail(); return receipt },
}
