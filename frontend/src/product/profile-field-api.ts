import type { components } from '../generated/api'
import { ApiError, request } from './api'
import { isGuid as guid } from './identifiers'
import { timestamp } from './date-validation'

// DEV-020 / T-S2-001. Untrusted JSON is checked before components see it.
export type ProfileFields = components['schemas']['ProfileFieldsDto']
export type ProfileField = components['schemas']['ProfileFieldDto']
export type FieldSource = components['schemas']['FieldSourceDto']
export type FieldFreshness = components['schemas']['FieldFreshness']
export type FieldVersion = components['schemas']['ProfileFieldVersionDto']
export type FieldHistory = { items: FieldVersion[]; offset: number; limit: number; hasMore: boolean }
export type FieldSourceOutcome = components['schemas']['ProfileFieldSourceOutcomeDto']
export type DeclareFieldSource = {
  fieldPath: string; expectedValueRevision: number; expectedSequence: number; sourceKind: string
  reference: { state: 'Missing' | 'Unknown' | 'Known'; value: string | null }
  observedAt: { state: 'Missing' | 'Unknown' | 'Known'; value: string | null }
}

export const sourceKinds = ['DOCUMENT', 'REGISTRY_RECORD', 'CLASS_RECORD', 'OWNER_DECLARATION', 'SURVEY_OR_MEASUREMENT', 'OTHER', 'UNKNOWN'] as const
const freshnessStates = ['FRESH', 'STALE', 'UNKNOWN', 'UNAVAILABLE']
const valueStates = ['Missing', 'Unknown', 'Known']
type ObjectValue = Record<string, unknown>
const object = (v: unknown): v is ObjectValue => !!v && typeof v === 'object' && !Array.isArray(v)
const integer = (v: unknown, min: number) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const code = (v: unknown) => typeof v === 'string' && /^[A-Z][A-Z_]{0,63}$/.test(v)
const optionalTime = (v: unknown) => v === null || v === undefined || timestamp(v)
const text = (v: unknown) => typeof v === 'string' && v.length <= 500

function input(v: unknown, time = false) {
  return object(v) && valueStates.includes(v.state as string) &&
    (v.state === 'Known' ? (time ? timestamp(v.value) : typeof v.value === 'string' && text(v.value)) : v.value === null || v.value === undefined)
}
function freshness(v: unknown) {
  return object(v) && freshnessStates.includes(v.state as string) && code(v.reasonCode) && timestamp(v.checkedAt) && optionalTime(v.observedAt) &&
    (v.ageSeconds === null || v.ageSeconds === undefined || typeof v.ageSeconds === 'number')
}
function source(v: unknown) {
  return object(v) && integer(v.sequence, 1) && integer(v.valueRevision, 1) && (sourceKinds as readonly string[]).includes(v.sourceKind as string) &&
    input(v.reference) && input(v.observedAt, true) && guid(v.declaredBy) && guid(v.stewardCompanyId) && timestamp(v.recordedAt) &&
    (v.provenance === 'DeclaredUnverified' || v.provenance === 'SyntheticUnverified') && v.verificationState === 'NOT_VERIFIED'
}
function field(v: unknown) {
  return object(v) && typeof v.fieldPath === 'string' && /^[a-zA-Z]{1,64}$/.test(v.fieldPath) && typeof v.kind === 'string' &&
    object(v.value) && valueStates.includes(v.value.state as string) && (v.value.state === 'Known' ? typeof v.value.value === 'string' : v.value.value == null) &&
    (v.valueRevision == null || integer(v.valueRevision, 1)) && integer(v.latestSequence, 0) &&
    (v.source == null || source(v.source)) && (v.priorValueSource == null || source(v.priorValueSource)) &&
    !(v.source != null && v.priorValueSource != null) && freshness(v.freshness) && v.verificationState === 'NOT_VERIFIED'
}
export function decodeFields(value: unknown, vessel: string): ProfileFields {
  if (!object(value) || !guid(value.tenantId) || typeof value.vesselId !== 'string' || value.vesselId.toLowerCase() !== vessel.toLowerCase() ||
      !object(value.owner) || !guid(value.owner.legalEntityId) || !code(value.owner.binding) || !code(value.owner.registeredOwnerState) ||
      !integer(value.profileRevision, 0) || !optionalTime(value.profileSavedAt) || !timestamp(value.checkedAt) ||
      !object(value.policy) || !['UNCONFIGURED', 'RESOLVED', 'UNAVAILABLE'].includes(value.policy.status as string) ||
      !object(value.summary) || !freshnessStates.includes(value.summary.state as string) || !code(value.summary.reasonCode) ||
      !Array.isArray(value.fields) || value.fields.length > 64 || !value.fields.every(field) || typeof value.canDeclare !== 'boolean' ||
      // A positive summary can never coexist with a non-fresh field.
      (value.summary.state === 'FRESH' && (value.fields as ProfileField[]).some(x => x.freshness.state !== 'FRESH')))
    throw new ApiError(502, 'invalid_contract')
  return value as ProfileFields
}
function decodeHistory(value: unknown, offset: number): FieldHistory {
  if (!object(value) || value.offset !== offset || value.limit !== 25 || typeof value.hasMore !== 'boolean' || !Array.isArray(value.items) || value.items.length > 25)
    throw new ApiError(502, 'invalid_contract')
  let previous = Infinity
  for (const row of value.items) {
    if (!object(row) || !integer(row.valueRevision, 1) || (row.valueRevision as number) >= previous || !timestamp(row.recordedAt) || !object(row.value) ||
        !valueStates.includes(row.value.state as string) || !Array.isArray(row.sources) || !row.sources.every(source) || typeof row.moreSources !== 'boolean')
      throw new ApiError(502, 'invalid_contract')
    previous = row.valueRevision as number
  }
  return value as FieldHistory
}
function decodeOutcome(value: unknown, vessel: string, path: string): FieldSourceOutcome {
  if (!object(value) || !guid(value.tenantId) || typeof value.vesselId !== 'string' || value.vesselId.toLowerCase() !== vessel.toLowerCase() ||
      value.fieldPath !== path || !source(value.source)) throw new ApiError(502, 'invalid_contract')
  return value as FieldSourceOutcome
}
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  try { return await request<unknown>(path, signal, init) }
  catch (error) { if (error instanceof SyntaxError) throw new ApiError(502, 'invalid_contract'); throw error }
}
const base = (id: string) => `vessels/${encodeURIComponent(id)}/regulatory-profile`
export const loadProfileFields = async (id: string, signal: AbortSignal) => decodeFields(await json(`${base(id)}/fields`, signal), id)
export const loadFieldHistory = async (id: string, path: string, offset: number, signal: AbortSignal) =>
  decodeHistory(await json(`${base(id)}/fields/${encodeURIComponent(path)}/history?offset=${offset}`, signal), offset)
export const declareFieldSource = async (id: string, command: DeclareFieldSource, csrf: string, key: string, signal: AbortSignal) =>
  decodeOutcome(await json(`${base(id)}/field-sources`, signal, { method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body: JSON.stringify(command) }), id, command.fieldPath)
