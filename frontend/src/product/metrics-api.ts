import type { components } from '../generated/api'
import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// PLAN-1511-5501-SLICE-01: typed consumer of the 55.01 scalar contract.
// Today/Why (61.03, DEV-023/024) render these values; they never compute a
// second formula. A value exists ONLY in state VALUE; SUPPRESSED, UNKNOWN and
// UNAVAILABLE must render as such and never as zero or green.
export type MetricScalar = components['schemas']['MetricScalarDto'] & {
  state: MetricState; quality: MetricQuality; scope: MetricScope
}
// expectedSources = readSources + withheldSources (current access) +
// unreadableSources (undecodable source) + notCollectedSources (no declaration).
export type MetricScope = components['schemas']['MetricScopeDto'] & { kind: 'TENANT_VISIBLE' | 'VESSEL' }
export type MetricDefinition = components['schemas']['MetricDefinitionDto'] & {
  allowedActions: ('view')[]; scopes: ('TENANT_VISIBLE' | 'VESSEL' | 'PILOT_WINDOW')[]
}
export type MetricCatalog = { contractVersion: 'metrics.v1'; items: MetricDefinition[] }
export type MetricState = 'VALUE' | 'SUPPRESSED' | 'UNKNOWN' | 'UNAVAILABLE'
export type MetricQuality = 'COMPLETE' | 'PARTIAL' | 'UNKNOWN'
export const metricIds = {
  onboardingReviewRequired: 'onboarding.vessels.review_required',
  onboardingUnknownFields: 'onboarding.required_fields.unknown',
  crewDeclaredPersons: 'crew.assignments.declared_persons',
} as const

const scalarKeys = ['metricId', 'definitionVersion', 'ownerModule', 'state', 'value', 'unitCode', 'quality', 'asOf', 'sourceProjectionId',
  'sourceWatermark', 'scope', 'unknownCount', 'reasons', 'basis', 'freshnessRule', 'source']
const definitionKeys = ['id', 'version', 'ownerModule', 'label', 'unitCode', 'sourceProjectionId', 'aggregation', 'minCohort', 'freshnessRule',
  'requiredPermissions', 'allowedActions', 'scopes', 'basis', 'source']
const object = (x: unknown): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x)
const exact = (x: Record<string, unknown>, keys: string[]) => Object.keys(x).length === keys.length && keys.every(k => k in x)
const count = (x: unknown) => typeof x === 'number' && Number.isSafeInteger(x) && x >= 0
const text = (x: unknown, max = 200) => typeof x === 'string' && x.length > 0 && x.length <= max
const codes = (x: unknown) => Array.isArray(x) && x.length <= 16 && x.every(c => typeof c === 'string' && /^[A-Z][A-Z0-9_]{1,63}$/.test(c))
const strings = (x: unknown) => Array.isArray(x) && x.length > 0 && x.length <= 16 && x.every(c => text(c, 120))
const members = (x: unknown, allowed: readonly string[]) => Array.isArray(x) && x.length > 0 && new Set(x).size === x.length &&
  x.every(c => typeof c === 'string' && allowed.includes(c))
// PILOT_WINDOW (S11 WP-B DEV-072): evaluated only by pilot-metrics/summary, never by the scalar endpoint.
const metricScopes = ['TENANT_VISIBLE', 'VESSEL', 'PILOT_WINDOW'] as const
const metricActions = ['view'] as const
function fail(): never { throw new ApiError(502, 'invalid_metric_contract') }

export function decodeMetricScalar(x: unknown, metricId: string, vesselId?: string): MetricScalar {
  if (!object(x) || !exact(x, scalarKeys) || x.metricId !== metricId || !count(x.definitionVersion) || x.definitionVersion === 0 ||
    !text(x.ownerModule) || !text(x.unitCode, 32) || !text(x.sourceProjectionId, 120) || !text(x.basis, 64) || !text(x.freshnessRule) ||
    !text(x.source, 400) || typeof x.asOf !== 'string' || !timestamp(x.asOf) ||
    !(x.sourceWatermark === null || typeof x.sourceWatermark === 'string' && timestamp(x.sourceWatermark)) ||
    !count(x.unknownCount) || !codes(x.reasons) || !['VALUE', 'SUPPRESSED', 'UNKNOWN', 'UNAVAILABLE'].includes(x.state as string) ||
    !['COMPLETE', 'PARTIAL', 'UNKNOWN'].includes(x.quality as string) || !object(x.scope)) fail()
  const s = x.scope
  if (!exact(s, ['kind', 'vesselId', 'expectedSources', 'readSources', 'withheldSources', 'unreadableSources', 'notCollectedSources']) ||
    !count(s.expectedSources) || !count(s.readSources) || !count(s.withheldSources) || !count(s.unreadableSources) || !count(s.notCollectedSources) ||
    (vesselId ? s.kind !== 'VESSEL' || typeof s.vesselId !== 'string' || !isGuid(s.vesselId) || s.vesselId.toLowerCase() !== vesselId.toLowerCase()
      : s.kind !== 'TENANT_VISIBLE' || s.vesselId !== null)) fail()
  // Coverage must account for every visible source: read + withheld +
  // unreadable + not collected = expected. UNAVAILABLE reads nothing.
  const unread = Number(s.withheldSources) + Number(s.unreadableSources) + Number(s.notCollectedSources)
  if (x.state === 'UNAVAILABLE' ? Number(s.readSources) + unread !== 0 : Number(s.readSources) + unread !== Number(s.expectedSources)) fail()
  // Fail closed on a value outside VALUE, a VALUE without a decimal string, or
  // a COMPLETE quality claim over withheld/unknown sources (false green).
  if (x.state === 'VALUE' ? typeof x.value !== 'string' || !/^(0|[1-9][0-9]{0,17})(\.[0-9]{1,8})?$/.test(x.value) : x.value !== null) fail()
  if (x.state !== 'VALUE' && x.sourceWatermark !== null) fail()
  if (x.quality === 'COMPLETE' && (Number(x.unknownCount) > 0 || unread > 0 || x.state === 'UNKNOWN' || x.state === 'UNAVAILABLE')) fail()
  // A VALUE always has a known quality; PARTIAL must name something unknown or unread.
  if (x.state === 'VALUE' && x.quality === 'UNKNOWN') fail()
  if (x.quality === 'PARTIAL' && Number(x.unknownCount) === 0 && unread === 0) fail()
  if ((x.state === 'UNKNOWN' || x.state === 'UNAVAILABLE') && x.quality !== 'UNKNOWN') fail()
  if (x.state === 'SUPPRESSED' && !(x.reasons as string[]).some(r => r === 'SMALL_GROUP' || r === 'COMPLEMENTARY_SUPPRESSION')) fail()
  return x as MetricScalar
}

export function decodeMetricCatalog(x: unknown): MetricCatalog {
  if (!object(x) || !exact(x, ['contractVersion', 'items']) || x.contractVersion !== 'metrics.v1' || !Array.isArray(x.items) || x.items.length > 64) fail()
  const ids = new Set<string>()
  for (const d of x.items) {
    if (!object(d) || !exact(d, definitionKeys) || !text(d.id, 120) || ids.has(d.id as string) || !count(d.version) || d.version === 0 ||
      !count(d.minCohort) || d.minCohort === 0 || !strings(d.requiredPermissions) || !members(d.allowedActions, metricActions) || !members(d.scopes, metricScopes) ||
      !['ownerModule', 'label', 'unitCode', 'sourceProjectionId', 'aggregation', 'freshnessRule', 'basis', 'source'].every(k => text(d[k], 400))) fail()
    ids.add(d.id as string)
  }
  return x as MetricCatalog
}

export const metricCatalog = async (signal: AbortSignal) => decodeMetricCatalog(await request<unknown>('metrics/definitions', signal))
export const metricScalar = async (metricId: string, signal: AbortSignal, vesselId?: string) => decodeMetricScalar(
  await request<unknown>(`metrics/scalar?metricId=${encodeURIComponent(metricId)}${vesselId ? `&vesselId=${encodeURIComponent(vesselId)}` : ''}`, signal),
  metricId, vesselId)
