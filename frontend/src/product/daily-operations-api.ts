import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp, calendar } from './date-validation'
import { dailyFields, type DailyKind, type DailyFields } from './daily-report-fields'
export const dailySourceVersion = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3'
export const dailyRoutes: Record<DailyKind, { app: string; api: string; firstRow: number; keys: readonly string[] }> = {
  Noon: { app: 'noon-report', api: 'noon-reports', firstRow: 2128, keys: ['report_date', 'report_time_utc', 'vessel_id', 'voyage_id', 'position_lat', 'position_lon', 'activity', 'from_port', 'to_port', 'eta', 'distance_run_nm', 'distance_to_go_nm', 'avg_speed_kn', 'weather_summary', 'cargo_status', 'main_engine_hours', 'fuel_summary', 'rob_summary', 'master_review', 'evidence_ref'] },
  Deck: { app: 'deck-daily-log', api: 'deck-daily-logs', firstRow: 2148, keys: ['log_date', 'watch_period', 'vessel_id', 'position', 'heading', 'speed', 'weather', 'visibility', 'nav_status', 'port_activity', 'pilot_tug_activity', 'ballast_activity', 'deck_work', 'safety_observation', 'handover_note', 'officer_of_watch', 'master_review', 'evidence_ref'] },
  Engine: { app: 'engine-daily-log', api: 'engine-daily-logs', firstRow: 2166, keys: ['log_date', 'watch_period', 'vessel_id', 'main_engine_status', 'main_engine_hours', 'aux_engine_hours', 'fuel_consumption', 'lube_oil_consumption', 'fresh_water_production', 'critical_measurements', 'alarms', 'defects', 'work_orders', 'bunkering_activity', 'bilge_sludge_status', 'environmental_control', 'watch_engineer', 'chief_engineer_review', 'handover_note', 'evidence_ref'] },
}
export type DailySchema = { key: string; type: 'Text' | 'Number' | 'Date' | 'Reference' | 'Code' | 'Narrative'; required: boolean; options: string[]; readOnly: boolean; referenceUnavailable: boolean; sourceRow: number; unit: string | null }
export type DailyOffline = { supported: boolean; state: 'UNAVAILABLE_POLICY' | 'CONFIGURED_UNVERIFIED'; operation: string }
export type DailyCapabilities = { vesselId: string; kind: DailyKind; canRead: boolean; canWrite: boolean; canReview: boolean; canSubmit: false; schema: DailySchema[]; sourceVersion: string; offline: DailyOffline; destinations: { key: string; href: string }[] }
export type DailyRecord = { id: string; vesselId: string; kind: DailyKind; revision: number; version: number; fields: DailyFields; state: 'DRAFT'; provenance: 'UserDeclaredUnverified' | 'SyntheticUnverified'; savedAt: string; hash: string; reviewState: 'PENDING' | 'RECORDED_UNASSESSED'; reviewedRevision: number | null; reviewedBy: string | null; reviewedAt: string | null; blockers: string[] }
export type DailyPage = { items: DailyRecord[]; offset: number; limit: 25; hasMore: boolean }
export type DailyCollection = DailyPage & { canWrite: boolean; canReview: boolean; canSubmit: false; offline: DailyOffline }
export type DailyAttempt = { key: string; id: string; vessel: string; kind: DailyKind; expectedVersion: number; body: string }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const text = (v: unknown, max = 200): v is string => typeof v === 'string' && v.length > 0 && v.length <= max && !/\p{Cc}/u.test(v)
const integer = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
function fail(): never { throw new ApiError(502, 'invalid_daily_contract') }
const narrativeKeys: Record<DailyKind, readonly string[]> = { Noon: ['weather_summary'], Deck: ['deck_work', 'safety_observation', 'handover_note'], Engine: ['critical_measurements', 'alarms', 'defects', 'handover_note'] }
const exactDecimal = (value: unknown) => typeof value === 'string' && value.length <= 48 && /^-?[0-9]+(?:\.[0-9]+)?$(?![\s\S])/.test(value)
function offline(value: unknown, kind: DailyKind): DailyOffline {
  if (!exact(value, ['supported', 'state', 'operation']) || typeof value.supported !== 'boolean' || value.state !== (value.supported ? 'CONFIGURED_UNVERIFIED' : 'UNAVAILABLE_POLICY') || value.operation !== `daily-operations.${kind.toLowerCase()}-draft.v1`) fail()
  return value as DailyOffline
}
export function decodeDailyCapabilities(value: unknown, vessel: string, kind: DailyKind): DailyCapabilities {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'kind', 'canRead', 'canWrite', 'canReview', 'canSubmit', 'schema', 'sourceVersion', 'offline', 'destinations']) || value.vesselId !== vessel || value.kind !== kind || typeof value.canRead !== 'boolean' || typeof value.canWrite !== 'boolean' || typeof value.canReview !== 'boolean' || value.canSubmit !== false || value.sourceVersion !== dailySourceVersion || !Array.isArray(value.schema) || value.schema.length !== dailyRoutes[kind].keys.length || !Array.isArray(value.destinations) || value.destinations.length > 5) fail()
  const keys = dailyRoutes[kind].keys
  for (const field of value.schema) if (!exact(field, ['key', 'type', 'required', 'options', 'readOnly', 'referenceUnavailable', 'sourceRow', 'unit']) || typeof field.key !== 'string' || !keys.includes(field.key) || field.sourceRow !== dailyRoutes[kind].firstRow + keys.indexOf(field.key) || typeof field.type !== 'string' || !['Text', 'Number', 'Date', 'Reference', 'Code', 'Narrative'].includes(field.type) || typeof field.required !== 'boolean' || typeof field.readOnly !== 'boolean' || field.readOnly !== ['vessel_id', 'master_review', 'chief_engineer_review'].includes(field.key) || typeof field.referenceUnavailable !== 'boolean' || !Array.isArray(field.options) || field.options.length > 128 || !field.options.every(option => text(option, 100)) || new Set(field.options).size !== field.options.length || !(field.unit === null || text(field.unit, 100))) fail()
  if (new Set(value.schema.map(field => field.key)).size !== keys.length) fail()
  for (const destination of value.destinations) if (!exact(destination, ['key', 'href']) || !Object.values(dailyRoutes).some(route => destination.key === route.app && destination.href === `/app/${route.app}/${vessel}`) &&
    !((kind === 'Noon' || kind === 'Engine') && destination.key === 'fuel-bunker-log' && destination.href === `/app/fuel-bunker-log/${vessel}` || kind === 'Noon' && destination.key === 'port-activity-sof' && destination.href === `/app/port-activity-sof/${vessel}` || kind === 'Engine' && destination.key === 'operational-downtime' && destination.href === `/app/operational-downtime/${vessel}`)) fail()
  if (new Set(value.destinations.map(row => row.key)).size !== value.destinations.length) fail()
  return { ...value, offline: offline(value.offline, kind) } as DailyCapabilities
}
export function decodeDailyRecord(value: unknown, vessel: string, kind: DailyKind, id?: string): DailyRecord {
  if (!exact(value, ['id', 'vesselId', 'kind', 'revision', 'version', 'fields', 'state', 'provenance', 'savedAt', 'hash', 'reviewState', 'reviewedRevision', 'reviewedBy', 'reviewedAt', 'blockers']) || !isGuid(value.id) || id !== undefined && value.id !== id || value.vesselId !== vessel || value.kind !== kind || !integer(value.revision) || !integer(value.version, 1, 4294967295) || value.state !== 'DRAFT' || typeof value.provenance !== 'string' || !['UserDeclaredUnverified', 'SyntheticUnverified'].includes(value.provenance) || !timestamp(value.savedAt) || typeof value.hash !== 'string' || !/^[A-F0-9]{64}$/.test(value.hash) || !exact(value.fields, dailyRoutes[kind].keys) || !Array.isArray(value.blockers) || value.blockers.length > 128 || !value.blockers.every(code => text(code)) || !(value.reviewState === 'PENDING' || value.reviewState === 'RECORDED_UNASSESSED')) fail()
  for (const [key, field] of Object.entries(value.fields)) {
    if (!exact(field, ['state', 'value']) || typeof field.state !== 'string' || !['Missing', 'Unknown', 'Known'].includes(field.state) || (field.state === 'Known' ? typeof field.value !== 'string' || field.value.length < 1 || field.value.length > (narrativeKeys[kind].includes(key) ? 4000 : 512) || /\p{Cc}/u.test(narrativeKeys[kind].includes(key) ? field.value.replace(/[\r\n\t]/g, '') : field.value) : field.value !== null)) fail()
    if (key === 'vessel_id' && (field.state !== 'Known' || field.value !== vessel) || ['master_review', 'chief_engineer_review'].includes(key) && (field.state !== 'Known' || field.value !== (kind === 'Noon' ? 'DRAFT' : 'PENDING'))) fail()
    const metadata = dailyFields(kind).find(f => f.key === key)
    if (field.state === 'Known' && metadata?.kind === 'number' && !exactDecimal(field.value) || field.state === 'Known' && key === 'report_date' && !calendar(field.value)) fail()
  }
  if (value.reviewState === 'PENDING' ? value.reviewedRevision !== null || value.reviewedBy !== null || value.reviewedAt !== null : !integer(value.reviewedRevision) || value.reviewedRevision > value.revision || !isGuid(value.reviewedBy) || !timestamp(value.reviewedAt)) fail()
  return value as DailyRecord
}
export function decodeDailyPage(value: unknown, vessel: string, kind: DailyKind, offset: number, collection: boolean, id?: string): DailyPage | DailyCollection {
  if (!exact(value, ['items', 'offset', 'limit', 'hasMore', ...(collection ? ['canWrite', 'canReview', 'canSubmit', 'offline'] : [])]) || value.offset !== offset || value.limit !== 25 || typeof value.hasMore !== 'boolean' || !Array.isArray(value.items) || value.items.length > 25 || value.hasMore && value.items.length !== 25) fail()
  const items = value.items.map(row => decodeDailyRecord(row, vessel, kind, id))
  if (new Set(items.map(row => collection ? row.id : row.revision)).size !== items.length) fail()
  if (collection && (typeof value.canWrite !== 'boolean' || typeof value.canReview !== 'boolean' || value.canSubmit !== false)) fail()
  return { ...value, items, ...(collection ? { offline: offline(value.offline, kind) } : {}) } as DailyPage | DailyCollection
}
const root = (kind: DailyKind, vessel: string) => { if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context'); return `${dailyRoutes[kind].api}?vesselId=${vessel}` }
const offsetQuery = (offset: number) => { if (!integer(offset, 0, 100000)) throw new ApiError(400, 'invalid_context'); return `&offset=${offset}` }
export const dailyApi = {
  capabilities: async (kind: DailyKind, vessel: string, signal: AbortSignal) => decodeDailyCapabilities(await request<unknown>(`${dailyRoutes[kind].api}/capabilities?vesselId=${vessel}`, signal), vessel, kind),
  list: async (kind: DailyKind, vessel: string, offset: number, signal: AbortSignal) => decodeDailyPage(await request<unknown>(root(kind, vessel) + offsetQuery(offset), signal), vessel, kind, offset, true) as DailyCollection,
  read: async (kind: DailyKind, vessel: string, id: string, signal: AbortSignal) => { if (!isGuid(id) || !isGuid(vessel)) throw new ApiError(400, 'invalid_context'); return decodeDailyRecord(await request<unknown>(`${dailyRoutes[kind].api}/${id}?vesselId=${vessel}`, signal), vessel, kind, id) },
  history: async (kind: DailyKind, vessel: string, id: string, offset: number, signal: AbortSignal) => { if (!isGuid(id) || !isGuid(vessel)) throw new ApiError(400, 'invalid_context'); return decodeDailyPage(await request<unknown>(`${dailyRoutes[kind].api}/${id}/history?vesselId=${vessel}${offsetQuery(offset)}`, signal), vessel, kind, offset, false, id) as DailyPage },
  save: async (attempt: DailyAttempt, csrf: string, signal: AbortSignal) => decodeDailyRecord(await request<unknown>(`${dailyRoutes[attempt.kind].api}${attempt.expectedVersion > 0 ? `/${attempt.id}` : ''}?vesselId=${attempt.vessel}`, signal, { method: attempt.expectedVersion > 0 ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body }), attempt.vessel, attempt.kind, attempt.id),
}
export function dailyAttempt(kind: DailyKind, vessel: string, fields: DailyFields, record?: DailyRecord, recordId?: string): DailyAttempt {
  if (!isGuid(vessel) || recordId !== undefined && !isGuid(recordId) || record && (record.vesselId !== vessel || record.kind !== kind || recordId !== undefined && recordId !== record.id)) throw new ApiError(400, 'invalid_context')
  const id = record?.id ?? recordId ?? crypto.randomUUID(), expectedVersion = record?.version ?? 0
  const mutable = Object.fromEntries(dailyFields(kind).map(field => [field.key, fields[field.key] ?? { state: 'Missing', value: null }]))
  return { key: crypto.randomUUID(), id, vessel, kind, expectedVersion, body: JSON.stringify({ id, expectedVersion, fields: mutable, reasonCode: record ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING' }) }
}
