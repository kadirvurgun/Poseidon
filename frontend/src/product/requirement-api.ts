import { isGuid as guid } from './identifiers'
import { timestamp } from './date-validation'
import { ApiError, request } from './api'
import { validContent } from './register-api'
import type { RegisterContent } from './register-api'
import type { components } from '../generated/api'

// DEV-026 / T-S3-001: read-only lifecycle projection of /api/v1/requirements.
// The lifecycle is server-derived from recorded qualification heads; this client
// never computes, defaults or upgrades it. Any unexpected value is a contract error.
type Schemas = components['schemas']
export const lifecycles = ['DRAFT', 'ADOPTED', 'FUTURE', 'IN_FORCE', 'SUPERSEDED', 'UNAVAILABLE'] as const
export type RequirementLifecycle = typeof lifecycles[number]
const states = ['DRAFT', 'IN_REVIEW', 'ADOPTED', 'AWAITING', 'FUTURE_EFFECTIVE', 'IN_FORCE', 'SUPERSEDED'] as const
// MIXED: bound rules disagree (derived value is a software precedence, expert review required).
// LIMIT: more bound rules than one projection carries; UNAVAILABLE, never a partial derivation.
const bases = ['NO_QUALIFIED_RULE', 'RECORDED_QUALIFICATION', 'UNRECOGNIZED_QUALIFICATION_STATE', 'MIXED_QUALIFICATION_STATES', 'QUALIFICATION_LIMIT_EXCEEDED'] as const
export type RequirementQualification = Omit<Schemas['RequirementQualificationDto'], 'state' | 'lifecycle'> & { state: typeof states[number]; lifecycle: RequirementLifecycle }
export type Requirement = Omit<Schemas['RequirementDto'], 'lifecycle' | 'lifecycleBasis' | 'content' | 'qualifications'> & { lifecycle: RequirementLifecycle; lifecycleBasis: typeof bases[number]; content: RegisterContent; qualifications: RequirementQualification[] }
const positive = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const record = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: Record<string, unknown>, keys: readonly string[]) => Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const member = <T extends string>(list: readonly T[], v: unknown): v is T => typeof v === 'string' && (list as readonly string[]).includes(v)
const invalid = (): never => { throw new ApiError(502, 'invalid_contract') }

function decodeQualification(value: unknown): RequirementQualification {
  if (!record(value) || !exact(value, ['ruleId', 'ruleRevision', 'qualificationRevision', 'requirementRevision', 'state', 'lifecycle', 'current', 'savedAt']) ||
    !guid(value.ruleId) || !positive(value.ruleRevision) || !positive(value.qualificationRevision) || !positive(value.requirementRevision) ||
    !member(states, value.state) || !member(lifecycles, value.lifecycle) || typeof value.current !== 'boolean' || !timestamp(value.savedAt)) return invalid()
  return value as RequirementQualification
}
export function decodeRequirement(value: unknown): Requirement {
  if (!record(value) || !exact(value, ['id', 'revision', 'version', 'lifecycle', 'lifecycleBasis', 'savedAt', 'provenance', 'hash', 'content', 'sourceId', 'sourceRevision', 'qualifications']) ||
    !guid(value.id) || !positive(value.revision) || !positive(value.version) || (value.version as number) > 4294967295 ||
    !member(lifecycles, value.lifecycle) || !member(bases, value.lifecycleBasis) || !timestamp(value.savedAt) ||
    !member(['DeclaredUnverified', 'SyntheticUnverified'] as const, value.provenance) || typeof value.hash !== 'string' || !/^[A-F0-9]{64}$/.test(value.hash) ||
    !validContent(value.content) || !(value.sourceId === null && value.sourceRevision === null || guid(value.sourceId) && positive(value.sourceRevision)) ||
    !Array.isArray(value.qualifications) || value.qualifications.length > 100) return invalid()
  const qualifications = (value.qualifications as unknown[]).map(decodeQualification)
  // No current recorded qualification can only ever be a draft; a basis/lifecycle mismatch is not repaired here.
  const current = qualifications.filter(q => q.current), terms = new Set(current.map(q => q.lifecycle)).size
  if (value.lifecycleBasis === 'NO_QUALIFIED_RULE' && (value.lifecycle !== 'DRAFT' || current.length > 0) ||
    value.lifecycleBasis === 'RECORDED_QUALIFICATION' && (current.length === 0 || value.lifecycle === 'UNAVAILABLE' || terms !== 1) ||
    value.lifecycleBasis === 'MIXED_QUALIFICATION_STATES' && (terms < 2 || value.lifecycle === 'UNAVAILABLE') ||
    value.lifecycleBasis === 'QUALIFICATION_LIMIT_EXCEEDED' && (value.lifecycle !== 'UNAVAILABLE' || qualifications.length > 0) ||
    value.lifecycleBasis === 'UNRECOGNIZED_QUALIFICATION_STATE' && value.lifecycle !== 'UNAVAILABLE' ||
    current.some(q => q.requirementRevision !== value.revision)) return invalid()
  return { ...(value as Requirement), qualifications }
}
export const readRequirement = async (id: string, signal: AbortSignal) => {
  const value = decodeRequirement(await request<unknown>(`requirements/${encodeURIComponent(id)}`, signal))
  if (value.id.toLowerCase() !== id.toLowerCase()) return invalid()
  return value
}
