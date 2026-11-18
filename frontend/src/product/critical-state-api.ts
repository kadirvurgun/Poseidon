import type { components } from '../generated/api'
import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// DEV-024 / T-S2-005. Shared critical-state explanation contract. The server decides what may
// be disclosed; the decoder only rejects shapes that would let a redacted entry carry source
// details or let any entry claim a positive (ready/verified) state.
export type CriticalStateExplanation = components['schemas']['CriticalStateExplanation']
export type CriticalStates = components['schemas']['CriticalStatesDto']
export const criticalStateKeys = ['vessel.onboarding', 'vessel.profile.freshness', 'vessel.compliance.applicability'] as const
export type CriticalStateKey = typeof criticalStateKeys[number]
export const criticalStates = ['BLOCKED', 'REVIEW_REQUIRED', 'UNKNOWN', 'STALE', 'UNAVAILABLE', 'DECLARED_UNVERIFIED', 'QUALIFIED_CURRENT_UNVERIFIED'] as const
export const redactedStates = ['REVIEW_REQUIRED', 'UNKNOWN', 'UNAVAILABLE'] as const
export const resolutionTypes = ['PROFILE_DECLARATION', 'REQUIRED_DECLARATION', 'POLICY_OR_EVIDENCE', 'QUALIFIED_EVIDENCE', 'SOURCE_DECLARATION',
  'SOURCE_RENEWAL', 'FRESHNESS_POLICY_CONFIGURATION', 'SERVICE_RECOVERY', 'RE_EVALUATION', 'QUALIFIED_EVALUATION', 'CATALOG_QUALIFICATION', 'HUMAN_REVIEW'] as const
const freshnessStates = ['FRESH', 'STALE', 'UNKNOWN', 'UNAVAILABLE']
const sourceKinds = ['RegulatoryDraft', 'ApplicabilityEvaluation']

type ObjectValue = Record<string, unknown>
const object = (v: unknown): v is ObjectValue => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is ObjectValue => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const code = (v: unknown) => typeof v === 'string' && /^[A-Z][A-Z0-9_]{0,99}$/.test(v)
const text = (v: unknown, max = 200) => typeof v === 'string' && v.length > 0 && v.length <= max && !/\p{Cc}/u.test(v)
const one = (values: readonly string[], v: unknown) => typeof v === 'string' && values.includes(v)
function fail(): never { throw new ApiError(502, 'invalid_critical_state_contract') }

const base = ['key', 'subjectId', 'state', 'disclosure', 'asOf'] as const
const full = [...base, 'verification', 'source', 'reasonCodes', 'freshness', 'blockers', 'href'] as const

export function decodeCriticalState(value: unknown, vessel: string): CriticalStateExplanation {
  if (!object(value) || !one(criticalStateKeys, value.key) || value.subjectId !== vessel || !timestamp(value.asOf)) fail()
  if (value.disclosure === 'STATE_ONLY') {
    // A redacted entry carries nothing beyond existence and a coarse non-positive state.
    if (!exact(value, base) || !one(redactedStates, value.state)) fail()
    return value as CriticalStateExplanation
  }
  const keys = value.source === undefined ? full.filter(k => k !== 'source') : full
  if (value.disclosure !== 'FULL' || !exact(value, keys) || !one(criticalStates, value.state) || value.verification !== 'NOT_VERIFIED' ||
      !Array.isArray(value.reasonCodes) || value.reasonCodes.length > 64 || !value.reasonCodes.every(code) ||
      new Set(value.reasonCodes).size !== value.reasonCodes.length || !text(value.href, 512) || !String(value.href).startsWith('/app/')) fail()
  if (value.source !== undefined && value.source !== null && !(exact(value.source, ['kind', 'id', 'revision', 'recordedAt', 'provenance']) &&
      one(sourceKinds, value.source.kind) && isGuid(value.source.id) &&
      (value.source.revision === null || typeof value.source.revision === 'number' && Number.isSafeInteger(value.source.revision) && value.source.revision > 0) &&
      (value.source.recordedAt === null || timestamp(value.source.recordedAt)) && text(value.source.provenance, 64))) fail()
  // Only applicability without any recorded evaluation may lack an owning source record.
  if ((value.source === undefined || value.source === null) && !(value.key === 'vessel.compliance.applicability' && value.state === 'UNAVAILABLE')) fail()
  const f = value.freshness
  if (!exact(f, ['state', 'reasonCode', 'observedAt', 'checkedAt']) || !one(freshnessStates, f.state) || !code(f.reasonCode) ||
      !(f.observedAt === null || timestamp(f.observedAt)) || !timestamp(f.checkedAt)) fail()
  if (!Array.isArray(value.blockers) || value.blockers.length > 64 || !value.blockers.every(b => exact(b, ['fieldPath', 'reasonCode', 'resolutionType']) &&
      typeof b.fieldPath === 'string' && /^(profile|applicability|vessel)(\.[A-Za-z]{1,64})?$/.test(b.fieldPath) && code(b.reasonCode) && one(resolutionTypes, b.resolutionType)))
    fail()
  return value as CriticalStateExplanation
}

export function decodeCriticalStates(value: unknown, vessel: string): CriticalStates {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'readAt', 'scope', 'states']) || value.vesselId !== vessel || !timestamp(value.readAt) ||
      value.scope !== 'VISIBLE_ONLY' || !Array.isArray(value.states) || value.states.length > criticalStateKeys.length) fail()
  const states = value.states.map(state => decodeCriticalState(state, vessel))
  if (new Set(states.map(s => s.key)).size !== states.length) fail()
  return { vesselId: vessel, readAt: value.readAt as string, scope: 'VISIBLE_ONLY', states }
}

export async function loadCriticalStates(vessel: string, signal: AbortSignal, key?: CriticalStateKey): Promise<CriticalStates> {
  if (!isGuid(vessel) || key !== undefined && !criticalStateKeys.includes(key)) throw new ApiError(400, 'invalid_context')
  const result = decodeCriticalStates(await request<unknown>(`vessels/${vessel}/critical-states${key ? `?key=${key}` : ''}`, signal), vessel)
  if (key && result.states.some(s => s.key !== key)) fail()
  return result
}
