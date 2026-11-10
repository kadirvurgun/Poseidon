import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// Coordinated S4 contract. Actual OpenAPI regeneration is deferred to the PR gate.
type ProcessObservation<T> = { state: 'Observed' | 'Unavailable'; scope: 'TenantProcessSinceStart' } & T
export type SystemHealth = {
  vesselId: string; readAt: string
  api: ProcessObservation<{ requests: number | null; errors: number | null; averageLatencyMs: number | null }>
  audit: ProcessObservation<{ persisted: number | null; unavailable: number | null; averageLatencyMs: number | null }>
  sync: { state: 'Observed'; scope: 'SelectedVesselServerOutcomes'; committed: number; unresolvedConflicts: number; localBacklog: null }
  files: { state: 'ConfiguredUnprobed' | 'Unavailable'; adapter: 'LocalDevelopment' | 'S3' | 'Unavailable'; scanState: 'ConfiguredUnprobed' | 'Unavailable' }
  freshness: ProcessObservation<{ current: number | null; stale: number | null; future: number | null; unknown: number | null; unavailable: number | null }>
}
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const count = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v >= 0
const latency = (v: unknown) => typeof v === 'number' && Number.isFinite(v) && v >= 0
function process(v: unknown, fields: string[], latencyField = false) {
  return exact(v, ['state', 'scope', ...fields]) && v.scope === 'TenantProcessSinceStart' && typeof v.state === 'string' && ['Observed', 'Unavailable'].includes(v.state) &&
    fields.every(field => v.state === 'Unavailable' ? v[field] === null : latencyField && field === 'averageLatencyMs' ? latency(v[field]) : count(v[field]))
}
export function decodeSystemHealth(value: unknown, vessel: string): SystemHealth {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'readAt', 'api', 'audit', 'sync', 'files', 'freshness']) || value.vesselId !== vessel || !timestamp(value.readAt) ||
    !process(value.api, ['requests', 'errors', 'averageLatencyMs'], true) || !process(value.audit, ['persisted', 'unavailable', 'averageLatencyMs'], true) ||
    !process(value.freshness, ['current', 'stale', 'future', 'unknown', 'unavailable']) ||
    !exact(value.sync, ['state', 'scope', 'committed', 'unresolvedConflicts', 'localBacklog']) || value.sync.state !== 'Observed' || value.sync.scope !== 'SelectedVesselServerOutcomes' ||
    !count(value.sync.committed) || !count(value.sync.unresolvedConflicts) || value.sync.localBacklog !== null ||
    !exact(value.files, ['state', 'adapter', 'scanState']) || typeof value.files.state !== 'string' || !['ConfiguredUnprobed', 'Unavailable'].includes(value.files.state) ||
    typeof value.files.adapter !== 'string' || !['LocalDevelopment', 'S3', 'Unavailable'].includes(value.files.adapter) || typeof value.files.scanState !== 'string' || !['ConfiguredUnprobed', 'Unavailable'].includes(value.files.scanState)) throw new ApiError(502, 'invalid_health_contract')
  return value as SystemHealth
}
export async function loadSystemHealth(vessel: string, signal: AbortSignal) {
  if (!isGuid(vessel)) throw new ApiError(400, 'invalid_health_context')
  return decodeSystemHealth(await request<unknown>(`system-health?vesselId=${encodeURIComponent(vessel)}`, signal), vessel)
}
