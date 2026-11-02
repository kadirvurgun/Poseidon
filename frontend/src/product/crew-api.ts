import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { calendar, timestamp } from './date-validation'

// Hand-authored consumer contract. OpenAPI generation is deferred to the foundation gate.
export type CrewRow = {
  id: string; vesselId: string; personId: string; version: number; revision: number;
  positionId: string | null; rank: string | null; capacity: string | null; department: string | null;
  from: string | null; to: string | null; contractEndDate: string | null; watchGroup: string | null;
  assignmentStatus: string; provenance: string; fullName: string | null; employeeNo: string | null;
  privacyScope: string; sourceObservedAt: string | null; eligibilityState: string;
  workRestState: string; certificateState: string; competenceState: string; contactStatus: string; nationality: string | null; nationalityState: string;
  presenceState: 'Draft' | 'Unknown' | 'Stale' | 'OutsidePeriod' | 'PlannedDeclaration' | 'OnboardDeclaration'; presenceReason: string; positionState: string;
}
export type CrewIdentityPolicy = { version: string; purpose: string; employeeNumberMode: string }
export type CrewPage = { items: CrewRow[]; offset: number; hasMore: boolean; canWrite: boolean; canWriteIdentity: boolean; identityPolicy?: CrewIdentityPolicy | null; declarationPolicy?: { version: string; statuses: string[] } | null }
export type CrewPosition = { id: string; code: string; rank: string | null; capacity: string | null; department: string | null; provenance: string }
export type CrewProfile = { assignment: CrewRow; employmentState: string; fitnessState: string; identityStatus: string; contactStatus: string; healthStatus: string; personVersion: number | null; identityPolicy?: CrewIdentityPolicy | null; canConvertIdentity?: boolean }
export type CrewCommand = {
  id: string; personId: string | null; baseVersion: number | null; fullName: string | null; employeeNo: string | null;
  positionId: string | null; from: string | null; to: string | null; contractEndDate: string | null;
  watchGroup: string | null; sourceObservedAt: string | null; declaredStatus?: string | null; assignmentPolicyVersion?: string | null; identityPolicyVersion?: string | null; identityPurpose?: string | null; positionPolicyVersion?: string | null;
}
export const crewPath = (vessel: string) => `vessels/${encodeURIComponent(vessel)}/crew`
const obj = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const uint = (v: unknown) => typeof v === 'number' && Number.isInteger(v) && v > 0 && v <= 4294967295
const text = (v: unknown) => typeof v === 'string'
const nullable = (v: unknown) => v === null || text(v)
const day = (v: unknown) => v === null || typeof v === 'string' && calendar(v)
const invalid = () => new ApiError(502, 'invalid_contract')
export function decodeCrewRow(v: unknown, vessel: string): CrewRow {
  if (!obj(v) || !isGuid(v.id) || !isGuid(v.personId) || v.vesselId !== vessel || !uint(v.version) ||
    !Number.isSafeInteger(v.revision) || (v.revision as number) < 1 || !(v.positionId === null || isGuid(v.positionId)) ||
    !['rank', 'capacity', 'department', 'watchGroup', 'fullName', 'employeeNo', 'nationality'].every(k => nullable(v[k])) ||
    !['from', 'to', 'contractEndDate'].every(k => day(v[k])) || !(v.sourceObservedAt === null || timestamp(v.sourceObservedAt)) ||
    !['assignmentStatus', 'provenance', 'eligibilityState', 'workRestState', 'certificateState', 'competenceState', 'contactStatus', 'nationalityState'].every(k => text(v[k])) ||
    !['eligibilityState', 'workRestState', 'certificateState', 'competenceState'].every(k => v[k] === 'Unknown') ||
    !['Withheld', 'LegacyUnavailable', 'IdentityPermitted', 'Unavailable'].includes(v.privacyScope as string) ||
    !['Draft', 'Unknown', 'Stale', 'OutsidePeriod', 'PlannedDeclaration', 'OnboardDeclaration'].includes(v.presenceState as string) ||
    !['incomplete_declaration', 'assignment_policy_unavailable', 'assignment_policy_changed', 'source_unrecognized', 'period_missing_or_invalid',
      'source_observation_missing_or_future', 'period_ended', 'unverified_planned_declaration', 'unverified_onboard_declaration', 'period_not_started', 'status_unrecognized'].includes(v.presenceReason as string) ||
    !['Unknown', 'Missing', 'PolicyUnavailable', 'Stale', 'DeclaredUnverified', 'Unavailable'].includes(v.positionState as string) ||
    v.privacyScope !== 'IdentityPermitted' && (v.fullName !== null || v.employeeNo !== null) ||
    v.positionState !== 'DeclaredUnverified' && (v.rank !== null || v.capacity !== null || v.department !== null)) throw invalid()
  return v as unknown as CrewRow
}
export const roster = async (vessel: string, offset: number, signal: AbortSignal): Promise<CrewPage> => {
  const v = await request<unknown>(`${crewPath(vessel)}?offset=${offset}`, signal)
  if (!obj(v) || !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset ||
    !['hasMore', 'canWrite', 'canWriteIdentity'].every(k => typeof v[k] === 'boolean')) throw invalid()
  v.items = v.items.map(r => decodeCrewRow(r, vessel))
  return v as unknown as CrewPage
}
export const profile = async (vessel: string, assignment: string, signal: AbortSignal): Promise<CrewProfile> => {
  const v = await request<unknown>(`${crewPath(vessel)}/${encodeURIComponent(assignment)}`, signal)
  if (!obj(v) || !['employmentState', 'fitnessState', 'identityStatus', 'contactStatus', 'healthStatus'].every(k => text(v[k])) ||
    !(v.personVersion === null || uint(v.personVersion))) throw invalid()
  const row = decodeCrewRow(v.assignment, vessel); if (row.id !== assignment) throw invalid()
  return { ...v, assignment: row } as CrewProfile
}
export type CrewCurrentAssignment = { state: string; vesselId?: string; assignmentId?: string; policyVersion?: string; position?: { rank: string | null; department: string | null } | null }
export async function currentAssignment(vessel: string, assignment: string, signal: AbortSignal): Promise<CrewCurrentAssignment> {
  const v = await request<unknown>(`${crewPath(vessel)}/${encodeURIComponent(assignment)}/current-assignment`, signal)
  if (!obj(v) || !['PolicyUnavailable', 'PolicyStale', 'Missing', 'Ambiguous', 'ConfiguredEffectiveDeclaration'].includes(v.state as string)) throw invalid()
  if (v.state === 'PolicyUnavailable') return { state: 'PolicyUnavailable' }
  if (!text(v.policyVersion) || !text(v.timeZone) || !day(v.referenceDate) || v.referenceDate === null || v.authority !== 'UnverifiedDeclaration;NoQualificationOrSafetyDecision') throw invalid()
  if (v.state !== 'ConfiguredEffectiveDeclaration') {
    if (v.assignmentId !== null || v.vesselId !== null || v.position !== null) throw invalid()
    return { state: v.state as string, policyVersion: v.policyVersion as string }
  }
  if (!isGuid(v.assignmentId) || !isGuid(v.vesselId) || !(v.position === null || obj(v.position) && isGuid(v.position.id) &&
    ['rank', 'department', 'capacity'].every(k => nullable((v.position as Record<string, unknown>)[k])))) throw invalid()
  return v as CrewCurrentAssignment
}

export type CrewHistoryRow = { revision: number; from: string | null; to: string | null; contractEndDate: string | null; positionId: string | null;
  status: string; provenance: string; sourceObservedAt: string | null; savedAt: string; assignmentPolicyVersion: string | null; positionPolicyVersion: string | null }
export async function history(vessel: string, assignment: string, signal: AbortSignal, offset = 0): Promise<{ items: CrewHistoryRow[]; offset: number; hasMore: boolean }> {
  const v = await request<unknown>(`${crewPath(vessel)}/${encodeURIComponent(assignment)}/history?offset=${offset}`, signal)
  if (!obj(v) || v.historicalIdentity !== 'Unavailable' || !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset || typeof v.hasMore !== 'boolean') throw invalid()
  let previous = Number.MAX_SAFE_INTEGER
  for (const r of v.items) {
    if (!obj(r) || !Number.isSafeInteger(r.revision) || (r.revision as number) < 1 || (r.revision as number) >= previous ||
      !['from', 'to', 'contractEndDate'].every(k => day(r[k])) || !(r.positionId === null || isGuid(r.positionId)) ||
      !text(r.status) || !text(r.provenance) || !timestamp(r.savedAt) || !(r.sourceObservedAt === null || timestamp(r.sourceObservedAt)) ||
      !nullable(r.assignmentPolicyVersion) || !nullable(r.positionPolicyVersion)) throw invalid()
    previous = r.revision as number
  }
  return { items: v.items as CrewHistoryRow[], offset, hasMore: v.hasMore }
}
export type CrewOwningInput = { field: string; state: 'Withheld' | 'Missing' | 'DeclaredUnverified' | 'Unavailable' | 'EvaluationUnavailable'; ownerTask: string; reason: string }
export async function owningInputs(vessel: string, assignment: string, signal: AbortSignal): Promise<CrewOwningInput[]> {
  const v = await request<unknown>(`${crewPath(vessel)}/${encodeURIComponent(assignment)}/owning-inputs`, signal)
  const expected = ['certificate_state', 'contact_status', 'competence_state', 'work_rest_state', 'eligibility_state', 'fitness_state']
  if (!obj(v) || v.assignmentId !== assignment || v.authority !== 'InputObservationOnly' || !Array.isArray(v.inputs) || v.inputs.length !== expected.length ||
    v.inputs.some((r, i) => !obj(r) || r.field !== expected[i] || !['Withheld', 'Missing', 'DeclaredUnverified', 'Unavailable', 'EvaluationUnavailable'].includes(r.state as string) ||
      !text(r.ownerTask) || !text(r.reason))) throw invalid()
  return v.inputs as CrewOwningInput[]
}
export const positions = (vessel: string, signal: AbortSignal) => request<CrewPosition[]>(`${crewPath(vessel)}/positions`, signal)
export const saveCrew = (vessel: string, command: CrewCommand, csrf: string, key: string, signal: AbortSignal) =>
  request<{ id: string; version: number; revision: number; replayed: boolean }>(crewPath(vessel), signal, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body: JSON.stringify(command),
  })

export const savePerson = (vessel: string, command: CrewCommand, csrf: string, key: string, signal: AbortSignal) =>
  request<{ version: number; replayed: boolean }>(`${crewPath(vessel)}/${encodeURIComponent(command.id)}/person`, signal, {
    method: 'PUT', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key },
    body: JSON.stringify({ baseVersion: command.baseVersion, fullName: command.fullName, employeeNo: command.employeeNo, policyVersion: command.identityPolicyVersion, purpose: command.identityPurpose }),
  })

export const positionPolicy = (vessel: string, signal: AbortSignal) => request<{ state: string; version: string | null; items: CrewPosition[] }>(`${crewPath(vessel)}/position-policy`, signal)
export const convertPerson = (vessel: string, command: CrewCommand, csrf: string, key: string, signal: AbortSignal) =>
  request<{ version: number; replayed: boolean }>(`${crewPath(vessel)}/${encodeURIComponent(command.id)}/person/convert`, signal, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key },
    body: JSON.stringify({ baseVersion: command.baseVersion, policyVersion: command.identityPolicyVersion, purpose: command.identityPurpose }),
  })
