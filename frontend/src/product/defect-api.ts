import { ApiError, request } from './api'
import { timestamp } from './date-validation'
import { isGuid } from './identifiers'
import { defectFieldInfo, defectSchema, defectSourceVersion, type DefectFieldDefinition, type DefectFields } from './defect-fields'
export type DefectAssetPin = { id: string; revision: number; version: number; hash: string }
export const defectTemporalStates = ['MEASURE_MISSING', 'MEASURE_UNKNOWN', 'DEADLINE_MISSING', 'DEADLINE_UNKNOWN', 'EXPIRED_DECLARED', 'NOT_DUE_DECLARED'] as const
export type DefectTemporalState = typeof defectTemporalStates[number]
export type DefectTemporal = { temporaryMeasureState: DefectTemporalState; assessedAt: string; closureAvailable: false }
export type DefectRecord = { id: string; vesselId: string; revision: number; version: number; fields: DefectFields; asset: DefectAssetPin | null; sourceVersion: string; hash: string; provenance: string; savedAt: string; stage: 'DECLARATION'; qualification: 'REVIEW_REQUIRED'; blockers: string[]; currentRevision: boolean; canEdit: boolean; temporal: DefectTemporal }
export type DefectCapabilities = { vesselId: string; canRead: boolean; canWrite: boolean; canClose: false; canVerify: false; canAssessEffectiveness: false; schema: DefectFieldDefinition[]; sourceVersion: string; offline: { supported: false; state: 'UNAVAILABLE' } }
export type DefectHistory = { items: DefectRecord[]; offset: number; limit: 25; hasMore: boolean }
export type DefectPage = DefectHistory & { canWrite: boolean; sourceVersion: string }
export type DefectAssetContext = { vesselId: string; pin: DefectAssetPin; current: true; qualification: 'REVIEW_REQUIRED'; provenance: string; savedAt: string; asset: { code: string | null; name: string | null; level: 'System' | 'Equipment' | 'Component'; criticality: string | null; maker: string | null; model: string | null; location: string | null }; href: string }
export type DefectAttempt = { owner: string; vessel: string; id: string; expectedVersion: number; key: string; body: string }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const integer = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const uint = (v: unknown) => integer(v, 1, 4294967295)
const text = (v: unknown, max = 256, multiline = false): v is string => typeof v === 'string' && !!v.trim() && v.length <= max && !/\p{Cc}/u.test(multiline ? v.replace(/[\r\n\t]/g, '') : v)
const hash = (v: unknown): v is string => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 128 && v.every(code => text(code, 256))
const sameOptions = (v: unknown, expected: readonly string[]) => Array.isArray(v) && v.length === expected.length && expected.every(option => v.includes(option))
const instant = (v: unknown): v is string => typeof v === 'string' && v.length <= 40 && !v.endsWith('-00:00') && timestamp(v)
function fail(): never { throw new ApiError(502, 'invalid_defect_contract') }
export function validDefectAssetPin(v: unknown): v is DefectAssetPin { return exact(v, ['id', 'revision', 'version', 'hash']) && isGuid(v.id) && integer(v.revision) && uint(v.version) && hash(v.hash) }
function validFact(v: unknown, field: DefectFieldDefinition) {
  if (!exact(v, ['state', 'value'])) return false
  if (v.state === 'Missing' || v.state === 'Unknown') return v.value === null
  if (v.state !== 'Known' || !text(v.value, field.type === 'Narrative' ? 4000 : 256, field.type === 'Narrative')) return false
  return (field.type !== 'Code' || field.options.includes(v.value)) && (field.type !== 'Instant' || instant(v.value))
}
export function defectInputErrors(fields: DefectFields) { return Object.fromEntries(defectSchema().filter(field => !validFact(fields[field.key], field)).map(field => [field.key, ['invalid_declared_value']])) }
function validFields(v: unknown): v is DefectFields { return exact(v, defectFieldInfo().map(field => field.key)) && defectSchema().every(field => validFact(v[field.key], field)) }
function temporal(value: unknown, fields: DefectFields): DefectTemporal {
  if (!exact(value, ['temporaryMeasureState', 'assessedAt', 'closureAvailable']) || !defectTemporalStates.some(state => value.temporaryMeasureState === state) || !timestamp(value.assessedAt) || !/(?:Z|\+00:00)$/.test(value.assessedAt) || value.closureAvailable !== false) fail()
  const measure = fields.temporary_measure, expiry = fields.temporary_measure_expires_at
  const expected = measure.state === 'Missing' ? ['MEASURE_MISSING'] : measure.state === 'Unknown' ? ['MEASURE_UNKNOWN'] : expiry.state === 'Missing' ? ['DEADLINE_MISSING'] : expiry.state === 'Unknown' ? ['DEADLINE_UNKNOWN'] : ['EXPIRED_DECLARED', 'NOT_DUE_DECLARED']
  if (!expected.includes(value.temporaryMeasureState as string)) fail()
  return value as DefectTemporal
}
export function decodeDefectRecord(value: unknown, vessel: string, id?: string): DefectRecord {
  if (!isGuid(vessel) || !exact(value, ['id', 'vesselId', 'revision', 'version', 'fields', 'asset', 'sourceVersion', 'hash', 'provenance', 'savedAt', 'stage', 'qualification', 'blockers', 'currentRevision', 'canEdit', 'temporal']) || !isGuid(value.id) || id !== undefined && value.id !== id || value.vesselId !== vessel || !integer(value.revision) || !uint(value.version) || !validFields(value.fields) || !(value.asset === null || validDefectAssetPin(value.asset)) || value.sourceVersion !== defectSourceVersion || !hash(value.hash) || !text(value.provenance) || !timestamp(value.savedAt) || value.stage !== 'DECLARATION' || value.qualification !== 'REVIEW_REQUIRED' || !codes(value.blockers) || typeof value.currentRevision !== 'boolean' || typeof value.canEdit !== 'boolean' || value.canEdit && !value.currentRevision) fail()
  return { ...value, temporal: temporal(value.temporal, value.fields) } as DefectRecord
}
export function decodeDefectCapabilities(value: unknown, vessel: string): DefectCapabilities {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'canRead', 'canWrite', 'canClose', 'canVerify', 'canAssessEffectiveness', 'schema', 'sourceVersion', 'offline']) || value.vesselId !== vessel || typeof value.canRead !== 'boolean' || typeof value.canWrite !== 'boolean' || value.canWrite && !value.canRead || value.canClose !== false || value.canVerify !== false || value.canAssessEffectiveness !== false || value.sourceVersion !== defectSourceVersion || !Array.isArray(value.schema) || value.schema.length !== defectSchema().length || !exact(value.offline, ['supported', 'state']) || value.offline.supported !== false || value.offline.state !== 'UNAVAILABLE') fail()
  const schema: DefectFieldDefinition[] = []
  for (const field of value.schema) {
    if (!exact(field, ['key', 'type', 'required', 'options', 'sourceLocator', 'requiredWhen'])) fail()
    const expected = defectSchema().find(entry => entry.key === field.key)
    if (!expected || schema.some(entry => entry.key === field.key) || field.type !== expected.type || field.required !== false || !sameOptions(field.options, expected.options) || field.sourceLocator !== expected.sourceLocator || field.requiredWhen !== expected.requiredWhen) fail()
    schema.push(field as DefectFieldDefinition)
  }
  return { ...value, schema } as DefectCapabilities
}
export function decodeDefectAssetContext(value: unknown, vessel: string, id: string, revision?: number, expected?: DefectAssetPin): DefectAssetContext {
  if (!exact(value, ['vesselId', 'pin', 'current', 'qualification', 'provenance', 'savedAt', 'asset', 'href']) || value.vesselId !== vessel || !validDefectAssetPin(value.pin) || value.pin.id !== id || revision !== undefined && value.pin.revision !== revision || expected && (value.pin.id !== expected.id || value.pin.revision !== expected.revision || value.pin.version !== expected.version || value.pin.hash !== expected.hash) || value.current !== true || value.qualification !== 'REVIEW_REQUIRED' || !text(value.provenance) || !timestamp(value.savedAt) || !exact(value.asset, ['code', 'name', 'level', 'criticality', 'maker', 'model', 'location']) || (typeof value.asset.level !== 'string' || !['System', 'Equipment', 'Component'].includes(value.asset.level)) || value.href !== '/app/equipment-hierarchy/' + vessel + '?id=' + id) fail()
  for (const key of ['code', 'name', 'criticality', 'maker', 'model', 'location']) { const entry = value.asset[key]; if (entry !== null && !(typeof entry === 'string' && entry.length <= 4096 && !/\p{Cc}/u.test(entry))) fail() }
  return value as DefectAssetContext
}
function history(value: unknown, vessel: string, offset: number, id?: string): DefectHistory {
  if (!object(value) || !Array.isArray(value.items) || value.items.length > 25 || value.offset !== offset || value.limit !== 25 || typeof value.hasMore !== 'boolean') fail()
  const items = value.items.map(item => decodeDefectRecord(item, vessel, id))
  if (new Set(items.map(item => id ? item.revision : item.id)).size !== items.length) fail()
  return { items, offset, limit: 25, hasMore: value.hasMore }
}
const root = (vessel: string, id?: string) => { if (!isGuid(vessel) || id !== undefined && !isGuid(id)) throw new ApiError(400, 'invalid_context'); return 'defects' + (id ? '/' + id : '') + '?vessel=' + vessel }
const offsetQuery = (offset: number) => { if (!integer(offset, 0, 100000)) throw new ApiError(400, 'invalid_context'); return String(offset) }
export function freezeDefectCommand(vessel: string, id: string, owner: string, fields: DefectFields, record: DefectRecord | null, asset: DefectAssetPin | null): DefectAttempt {
  if (!isGuid(vessel) || !isGuid(id) || owner.split('/').length !== 2 || !owner.split('/').every(isGuid) || !validFields(fields) || asset !== null && !validDefectAssetPin(asset) || record && (record.id !== id || record.vesselId !== vessel || !record.currentRevision || !record.canEdit)) throw new ApiError(400, 'invalid_defect_input')
  return { owner, vessel, id, expectedVersion: record?.version ?? 0, key: crypto.randomUUID(), body: JSON.stringify({ id, expectedVersion: record?.version ?? 0, fields, asset, sourceVersion: defectSourceVersion }) }
}
export const defectApi = {
  capabilities: async (vessel: string, signal: AbortSignal) => { root(vessel); return decodeDefectCapabilities(await request<unknown>('defects/capabilities?vessel=' + vessel, signal), vessel) },
  read: async (vessel: string, id: string, signal: AbortSignal) => decodeDefectRecord(await request<unknown>(root(vessel, id), signal), vessel, id),
  list: async (vessel: string, offset: number, signal: AbortSignal): Promise<DefectPage> => { const value = await request<unknown>(root(vessel) + '&offset=' + offsetQuery(offset), signal); if (!exact(value, ['items', 'offset', 'limit', 'hasMore', 'canWrite', 'sourceVersion']) || typeof value.canWrite !== 'boolean' || value.sourceVersion !== defectSourceVersion) fail(); return { ...history(value, vessel, offset), canWrite: value.canWrite, sourceVersion: defectSourceVersion } },
  history: async (vessel: string, id: string, offset: number, signal: AbortSignal) => { const value = await request<unknown>(root(vessel, id).replace('?vessel=', '/history?vessel=') + '&offset=' + offsetQuery(offset), signal); if (!exact(value, ['items', 'offset', 'limit', 'hasMore'])) fail(); return history(value, vessel, offset, id) },
  assetContext: async (vessel: string, id: string, revision: number, signal: AbortSignal, expected?: DefectAssetPin) => { root(vessel, id); if (!integer(revision)) throw new ApiError(400, 'invalid_context'); return decodeDefectAssetContext(await request<unknown>('vessels/' + vessel + '/defect-asset-context/' + id + '?revision=' + revision, signal), vessel, id, revision, expected) },
  save: async (attempt: DefectAttempt, csrf: string, signal: AbortSignal) => { const value = await request<unknown>(root(attempt.vessel, attempt.expectedVersion ? attempt.id : undefined), signal, { method: attempt.expectedVersion ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body }); const receipt = decodeDefectRecord(value, attempt.vessel, attempt.id); if (receipt.currentRevision || receipt.canEdit) fail(); return receipt },
}
