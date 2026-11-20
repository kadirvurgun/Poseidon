import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import type { components } from '../generated/api'

// S7 WP-A (DEV-048/049/050) consumer contract. Generated shapes plus defensive decoders:
// an unknown state never becomes a positive presentation.
type S = components['schemas']
export type EligibilityState = 'ELIGIBLE' | 'ATTENTION' | 'NOT_ELIGIBLE' | 'REVIEW_REQUIRED'
export const eligibilityStates: readonly EligibilityState[] = ['ELIGIBLE', 'ATTENTION', 'NOT_ELIGIBLE', 'REVIEW_REQUIRED']
export const credentialStatuses = ['VERIFIED_CURRENT', 'UNVERIFIED', 'EXPIRED', 'EXPIRY_UNKNOWN', 'VERIFICATION_NEGATIVE', 'VERIFICATION_NOT_CURRENT'] as const
export type CredentialStatus = (typeof credentialStatuses)[number]
export type EligibilityResult = S['CrewEligibilityResult']
export type SafeManningView = S['SafeManningView']
export type CertificateRegister = S['CrewCertificateRegister']
export type VerificationView = S['CrewCredentialVerificationView']
export type CompetenceMatrix = S['CrewCompetenceMatrix']
export type CompetenceView = S['CrewCompetenceView']
export type RequirementCommand = S['CrewCompetenceRequirementCommand']
export type RecordCommand = S['CrewCompetenceRecordCommand']
export type SignOffCommand = S['CrewCompetenceSignOffCommand']
export type AssertionCommand = S['CrewCredentialAssertionCommand']
export type DecisionCommand = S['SafeManningDecisionCommand']
export type CommandOutcome = S['CrewCommandOutcome']

const invalid = () => new ApiError(502, 'invalid_contract')
const obj = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const crew = (vessel: string) => `vessels/${encodeURIComponent(vessel)}/crew`
const window = (from: string, to: string) => `from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`

export function decodeEligibility(v: unknown): EligibilityResult {
  if (!obj(v) || !isGuid(v.assignmentId) || !eligibilityStates.includes(v.state as EligibilityState) || !Array.isArray(v.reasons) || !Array.isArray(v.gaps) ||
    v.authority !== 'ADVISORY_ONLY_NOT_A_DECISION') throw invalid()
  // ELIGIBLE with any blocking reason is contradictory; refuse instead of showing it.
  if (v.state === 'ELIGIBLE' && v.reasons.some(r => obj(r) && r.severity !== 'INFO')) throw invalid()
  return v as unknown as EligibilityResult
}
export function decodeSafeManning(v: unknown): SafeManningView {
  if (!obj(v) || !obj(v.evaluation) || typeof v.canDecide !== 'boolean' || !Array.isArray(v.authorities)) throw invalid()
  const e = v.evaluation
  if (!eligibilityStates.includes(e.state as EligibilityState) || typeof e.hash !== 'string' || !/^[0-9a-f]{64}$/i.test(e.hash) || !Array.isArray(e.members) ||
    !Array.isArray(e.roles) || e.authority !== 'ADVISORY_ONLY_NOT_A_DECISION') throw invalid()
  e.members.forEach(decodeEligibility)
  if (e.state === 'ELIGIBLE' && e.msmdState !== 'CURRENT') throw invalid()
  return v as unknown as SafeManningView
}
export function decodeRegister(v: unknown): CertificateRegister {
  if (!obj(v) || !Array.isArray(v.items) || typeof v.truncated !== 'boolean') throw invalid()
  for (const r of v.items) if (!obj(r) || !isGuid(r.credentialId) || !credentialStatuses.includes(r.status as CredentialStatus) ||
    r.status === 'VERIFIED_CURRENT' && (r.assertionState !== 'Verified' || r.assertionCurrent !== true)) throw invalid()
  return v as unknown as CertificateRegister
}

const send = <T>(path: string, body: unknown, csrf: string, key: string, signal: AbortSignal) => request<T>(path, signal, {
  method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body: JSON.stringify(body),
})

export const crewComplianceApi = {
  register: async (vessel: string, signal: AbortSignal) => decodeRegister(await request<unknown>(`${crew(vessel)}/certificates`, signal)),
  verification: (vessel: string, assignment: string, id: string, signal: AbortSignal) =>
    request<VerificationView>(`${crew(vessel)}/${encodeURIComponent(assignment)}/certificates/${encodeURIComponent(id)}/verification`, signal),
  recordAssertion: (vessel: string, assignment: string, id: string, body: AssertionCommand, csrf: string, key: string, signal: AbortSignal) =>
    send<CommandOutcome>(`${crew(vessel)}/${encodeURIComponent(assignment)}/certificates/${encodeURIComponent(id)}/verification`, body, csrf, key, signal),
  matrix: (vessel: string, signal: AbortSignal) => request<CompetenceMatrix>(`${crew(vessel)}/competence-matrix`, signal),
  saveRequirement: (vessel: string, body: RequirementCommand, csrf: string, key: string, signal: AbortSignal) =>
    send<CommandOutcome>(`${crew(vessel)}/competence-matrix`, body, csrf, key, signal),
  competence: async (vessel: string, assignment: string, from: string, to: string, signal: AbortSignal) => {
    const v = await request<CompetenceView>(`${crew(vessel)}/${encodeURIComponent(assignment)}/competence?${window(from, to)}`, signal)
    decodeEligibility(v.evaluation)
    return v
  },
  saveRecord: (vessel: string, assignment: string, body: RecordCommand, csrf: string, key: string, signal: AbortSignal) =>
    send<CommandOutcome>(`${crew(vessel)}/${encodeURIComponent(assignment)}/competence`, body, csrf, key, signal),
  signOff: (vessel: string, assignment: string, record: string, body: SignOffCommand, csrf: string, key: string, signal: AbortSignal) =>
    send<CommandOutcome>(`${crew(vessel)}/${encodeURIComponent(assignment)}/competence/${encodeURIComponent(record)}/sign-off`, body, csrf, key, signal),
  evaluate: async (vessel: string, from: string, to: string, signal: AbortSignal) =>
    decodeSafeManning(await request<unknown>(`vessels/${encodeURIComponent(vessel)}/safe-manning/evaluate?${window(from, to)}`, signal)),
  recordDecision: (vessel: string, body: DecisionCommand, csrf: string, key: string, signal: AbortSignal) =>
    send<CommandOutcome>(`vessels/${encodeURIComponent(vessel)}/safe-manning/decisions`, body, csrf, key, signal),
}

// Badge tone: nothing is ever presented as a green "ready"; ELIGIBLE is advisory info.
export const eligibilityTone = (state: string): 'info' | 'warning' | 'danger' =>
  state === 'ELIGIBLE' ? 'info' : state === 'NOT_ELIGIBLE' ? 'danger' : 'warning'
export const credentialTone = (status: string): 'info' | 'warning' | 'danger' =>
  status === 'VERIFIED_CURRENT' ? 'info' : status === 'EXPIRED' || status === 'VERIFICATION_NEGATIVE' ? 'danger' : 'warning'
