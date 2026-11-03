import { ApiError } from './api'
import { timestamp } from './date-validation'
import { isGuid } from './identifiers'

export type CrewRoleCoverage = { positionId: string; required: number; declared: number; qualified: number; unknown: number; state: string; reason: string }
export type TugCrewCoverage = { tugId: string; state: string; reason: string; windowStart: string | null; windowEnd: string | null;
  assessedAt: string; msmdId: string | null; msmdRevision: number | null; roles: CrewRoleCoverage[]; validUntil: string | null; coverage: string }
const exact = (value: unknown, keys: string[]): value is Record<string, unknown> => value !== null && typeof value === 'object' && !Array.isArray(value) &&
  Object.keys(value).length === keys.length && keys.every(k => Object.hasOwn(value, k))
const integer = (value: unknown): value is number => typeof value === 'number' && Number.isSafeInteger(value) && value >= 0
const time = (value: unknown) => value === null || timestamp(value)
const fail = (): never => { throw new ApiError(502, 'invalid_tug_crew_contract') }
const emptyReasons = ['DeclaredFutureOrCurrentJobWindowRequired', 'CurrentAssignmentPolicyUnavailable', 'SourceQualifiedMsmdRequired',
  'CurrentMsmdReferencesAmbiguous', 'SourceQualifiedMsmdDoesNotCoverJobWindow', 'CrewProjectionLimitExceeded', 'SourceRoleProfileInvalid']
export function decodeTugCrewCoverage(value: unknown, assigned: string[]): TugCrewCoverage[] {
  if (!Array.isArray(value) || value.length !== assigned.length) return fail()
  for (const c of value) {
    if (!exact(c, ['tugId', 'state', 'reason', 'windowStart', 'windowEnd', 'assessedAt', 'msmdId', 'msmdRevision', 'roles', 'validUntil', 'coverage']) ||
      !isGuid(c.tugId) || !assigned.includes(c.tugId) || !time(c.windowStart) || !time(c.windowEnd) || !timestamp(c.assessedAt) || !time(c.validUntil) ||
      c.coverage !== 'SourceQualifiedMinimumRolesOnly;OtherCrewGatesAndMasterDecisionSeparate' || !Array.isArray(c.roles) || c.roles.length > 128) return fail()
    if (c.state === 'Unavailable' || c.roles.length === 0) {
      if (c.msmdId !== null || c.msmdRevision !== null || c.validUntil !== null || c.roles.length !== 0 ||
        (c.state === 'Unavailable' ? c.reason !== 'CurrentProtectedCrewSourceAccessRequired' : c.state !== 'ReviewRequired' || !emptyReasons.includes(c.reason as string))) return fail()
      continue
    }
    if (!isGuid(c.msmdId) || !integer(c.msmdRevision) || c.msmdRevision === 0 || !timestamp(c.windowStart) || !timestamp(c.windowEnd) ||
      Date.parse(c.windowStart) >= Date.parse(c.windowEnd) || !timestamp(c.validUntil) || Date.parse(c.validUntil) <= Date.parse(c.windowEnd)) return fail()
    const roles: CrewRoleCoverage[] = []
    for (const r of c.roles) {
      if (!exact(r, ['positionId', 'required', 'declared', 'qualified', 'unknown', 'state', 'reason']) || !isGuid(r.positionId) ||
        !integer(r.required) || r.required < 1 || r.required > 1000 || !integer(r.declared) || r.declared > 1000 || !integer(r.qualified) ||
        !integer(r.unknown) || r.qualified + r.unknown > r.declared) return fail()
      const state = r.qualified >= r.required ? 'Current' : r.unknown > 0 ? 'ReviewRequired' : 'Blocked'
      const reason = state === 'Current' ? 'SourceQualifiedRoleCountMet' : state === 'Blocked' ? 'SourceQualifiedRoleCountNotMet' : 'CurrentRoleQualificationIncomplete'
      if (r.state !== state || r.reason !== reason) return fail()
      roles.push(r as CrewRoleCoverage)
    }
    if (new Set(roles.map(r => r.positionId.toLowerCase())).size !== roles.length || roles.reduce((total, r) => total + r.declared, 0) > 1000) return fail()
    const state = roles.some(r => r.state === 'Blocked') ? 'Blocked' : roles.some(r => r.state === 'ReviewRequired') ? 'ReviewRequired' : 'CurrentMinimumRoleCoverage'
    const reason = state === 'Blocked' ? 'SourceQualifiedRoleCountNotMet' : state === 'ReviewRequired' ? 'CurrentRoleQualificationIncomplete' : 'SourceQualifiedMinimumRolesOnly'
    if (c.reason === 'CrossRoleAllocationReviewRequired' ? c.state !== 'ReviewRequired' || roles.length < 2 : c.state !== state || c.reason !== reason) return fail()
  }
  if (new Set(value.map(c => c.tugId.toLowerCase())).size !== value.length) return fail()
  return value as TugCrewCoverage[]
}
