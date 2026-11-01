import { request } from './api'

// Hand-authored consumer contract. OpenAPI generation is deferred to the foundation gate.
export type CrewRow = {
  id: string; vesselId: string; personId: string; version: number; revision: number;
  positionId: string | null; rank: string | null; capacity: string | null; department: string | null;
  from: string | null; to: string | null; contractEndDate: string | null; watchGroup: string | null;
  assignmentStatus: string; provenance: string; fullName: string | null; employeeNo: string | null;
  privacyScope: string; sourceObservedAt: string | null; eligibilityState: string;
  workRestState: string; certificateState: string; competenceState: string; contactStatus: string; nationality?: string | null; nationalityState?: string;
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
export const roster = (vessel: string, offset: number, signal: AbortSignal) => request<CrewPage>(`${crewPath(vessel)}?offset=${offset}`, signal)
export const profile = (vessel: string, assignment: string, signal: AbortSignal) => request<CrewProfile>(`${crewPath(vessel)}/${encodeURIComponent(assignment)}`, signal)
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
