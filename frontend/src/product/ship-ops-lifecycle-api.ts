import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { decodeShipOpsRecord, type ShipOpsRecord } from './ship-operations-api'
import { decodeDowntimeRecord, downtimeLifecycleActions, validEvidencePin, type DowntimeEvidencePin, type DowntimeLifecycleState, type DowntimeRecord } from './downtime-api'

// DEV-101 SOF finalization / DEV-102 downtime lifecycle client contracts. Every
// command is an explicit human act with a server-provisioned appointment; the
// client never manufactures a signature, a FINAL/VERIFIED state or a claim.
export type OpsAuthority = { id: string; roleId: string; duty: string }
export type SofFinalizationEvent = { id: string; fromRevision: number; toRevision: number; actorId: string; roleId: string | null; duty: string | null; reasonCode: string; acknowledgedUnresolved: string[]; signatureState: 'SOF_SIGNATURE_NOT_RECORDED'; recordHash: string; savedAt: string }
export type SofFinalization = { recordId: string; vesselId: string; revision: number; version: number; sofState: 'DRAFT' | 'FINAL'; finalRevision: number | null; canFinalize: boolean; authorities: OpsAuthority[]; missingRequired: string[]; unresolvedReferences: string[]; events: SofFinalizationEvent[]; qualifications: string[] }
export type DowntimeLifecycleEvent = { id: string; fromRevision: number; toRevision: number; action: string; fromState: string | null; toState: string | null; actorId: string; roleId: string | null; duty: string | null; reasonCode: string; evidence: DowntimeEvidencePin | null; savedAt: string }
export type DowntimeLifecycleView = { recordId: string; vesselId: string; revision: number; version: number; state: DowntimeLifecycleState; lossOfHireCandidate: 'REVIEW_REQUIRED' | 'UNKNOWN'; claimEvidence: DowntimeEvidencePin[]; allowedActions: string[]; authorities: OpsAuthority[]; actorIsRecorder: boolean; missingRequired: string[]; evidenceOptions: { id: string; revision: number; label: string }[]; events: DowntimeLifecycleEvent[]; qualifications: string[] }
export type OpsLifecycleAttempt = { path: string; vessel: string; id: string; key: string; body: string; kind: 'Sof' | 'Downtime' }
export const sofReason = 'HUMAN_SOF_FINALIZATION', downtimeLifecycleReason = 'HUMAN_DOWNTIME_LIFECYCLE'
export const reviewActions = ['RETURN_FOR_CORRECTION', 'VERIFY', 'CLOSE']
const sofDuties = ['Master', 'ChiefOfficer', 'ChiefEngineer'], downtimeDuties = [...sofDuties, 'FleetManager', 'DPA']
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const integer = (v: unknown, min = 1): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const code = (v: unknown): v is string => typeof v === 'string' && /^[A-Z][A-Z0-9_]{0,79}$/.test(v)
const key = (v: unknown): v is string => typeof v === 'string' && /^[a-z][a-z0-9_]{0,63}$/.test(v)
const hash = (v: unknown) => typeof v === 'string' && /^[A-Fa-f0-9]{64}$/.test(v)
function fail(): never { throw new ApiError(502, 'invalid_ship_operations_contract') }
function authorities(v: unknown, duties: string[]): OpsAuthority[] {
  if (!Array.isArray(v) || v.length > 32) fail()
  for (const a of v) if (!exact(a, ['id', 'roleId', 'duty']) || !isGuid(a.id) || !isGuid(a.roleId) || !duties.includes(String(a.duty))) fail()
  return v as OpsAuthority[]
}
export function decodeSofFinalization(v: unknown, vessel: string, id: string): SofFinalization {
  if (!exact(v, ['recordId', 'vesselId', 'revision', 'version', 'sofState', 'finalRevision', 'canFinalize', 'authorities', 'missingRequired', 'unresolvedReferences', 'events', 'qualifications']) || v.recordId !== id || v.vesselId !== vessel ||
    !integer(v.revision) || !integer(v.version) || !(v.sofState === 'DRAFT' || v.sofState === 'FINAL') || !(v.finalRevision === null || integer(v.finalRevision)) || typeof v.canFinalize !== 'boolean' ||
    !Array.isArray(v.missingRequired) || !v.missingRequired.every(key) || !Array.isArray(v.unresolvedReferences) || !v.unresolvedReferences.every(key) || !Array.isArray(v.qualifications) || !v.qualifications.every(code) ||
    !v.qualifications.includes('SOF_SIGNATURE_NOT_RECORDED') || !Array.isArray(v.events) || v.events.length > 100) fail()
  const list = authorities(v.authorities, sofDuties)
  // A finalize affordance requires an appointment, complete required facts and a non-FINAL head.
  if (v.canFinalize && (list.length === 0 || v.missingRequired.length > 0 || v.sofState === 'FINAL')) fail()
  for (const e of v.events) if (!exact(e, ['id', 'fromRevision', 'toRevision', 'actorId', 'roleId', 'duty', 'reasonCode', 'acknowledgedUnresolved', 'signatureState', 'recordHash', 'savedAt']) || !isGuid(e.id) || !integer(e.fromRevision) || e.toRevision !== (e.fromRevision as number) + 1 ||
    !isGuid(e.actorId) || !(e.roleId === null || isGuid(e.roleId)) || !sofDuties.includes(String(e.duty)) || e.reasonCode !== sofReason || !Array.isArray(e.acknowledgedUnresolved) || !e.acknowledgedUnresolved.every(key) ||
    e.signatureState !== 'SOF_SIGNATURE_NOT_RECORDED' || !hash(e.recordHash) || !timestamp(e.savedAt)) fail()
  return { ...(v as unknown as SofFinalization), authorities: list }
}
export function decodeDowntimeLifecycle(v: unknown, vessel: string, id: string): DowntimeLifecycleView {
  if (!exact(v, ['recordId', 'vesselId', 'revision', 'version', 'state', 'lossOfHireCandidate', 'claimEvidence', 'allowedActions', 'authorities', 'actorIsRecorder', 'missingRequired', 'evidenceOptions', 'events', 'qualifications']) || v.recordId !== id || v.vesselId !== vessel ||
    !integer(v.revision) || !integer(v.version) || !['NOT_SUBMITTED', 'OPEN', 'IN_REVIEW', 'VERIFIED', 'CLOSED'].includes(String(v.state)) || !(v.lossOfHireCandidate === 'REVIEW_REQUIRED' || v.lossOfHireCandidate === 'UNKNOWN') ||
    !Array.isArray(v.claimEvidence) || !v.claimEvidence.every(validEvidencePin) || !Array.isArray(v.allowedActions) || !v.allowedActions.every(a => (downtimeLifecycleActions as readonly string[]).includes(a)) ||
    typeof v.actorIsRecorder !== 'boolean' || !Array.isArray(v.missingRequired) || !v.missingRequired.every(key) || !Array.isArray(v.qualifications) || !v.qualifications.every(code) ||
    !v.qualifications.includes('LOSS_OF_HIRE_CANDIDATE_ONLY_NO_ENTITLEMENT_OR_AMOUNT') || !Array.isArray(v.evidenceOptions) || v.evidenceOptions.length > 25 || !Array.isArray(v.events) || v.events.length > 100) fail()
  const list = authorities(v.authorities, downtimeDuties)
  // Separation of duties: a recorder is never offered verify or close.
  if (v.actorIsRecorder && v.allowedActions.some(a => a === 'VERIFY' || a === 'CLOSE')) fail()
  if (v.allowedActions.some(a => reviewActions.includes(a)) && list.length === 0) fail()
  for (const o of v.evidenceOptions) if (!exact(o, ['id', 'revision', 'label']) || !isGuid(o.id) || !integer(o.revision) || typeof o.label !== 'string' || o.label.length > 200) fail()
  for (const e of v.events) if (!exact(e, ['id', 'fromRevision', 'toRevision', 'action', 'fromState', 'toState', 'actorId', 'roleId', 'duty', 'reasonCode', 'evidence', 'savedAt']) || !isGuid(e.id) || !integer(e.fromRevision) || e.toRevision !== (e.fromRevision as number) + 1 ||
    !(downtimeLifecycleActions as readonly string[]).includes(String(e.action)) || !isGuid(e.actorId) || e.reasonCode !== downtimeLifecycleReason || !(e.evidence === null || validEvidencePin(e.evidence)) || !timestamp(e.savedAt) ||
    reviewActions.includes(String(e.action)) !== downtimeDuties.includes(String(e.duty))) fail()
  return { ...(v as unknown as DowntimeLifecycleView), authorities: list }
}
const sofPath = (vessel: string, id: string, suffix: string) => { if (!isGuid(vessel) || !isGuid(id)) throw new ApiError(400, 'invalid_context'); return 'port-activity-sof/' + id + suffix + '?vesselId=' + vessel }
const downtimePath = (vessel: string, id: string) => { if (!isGuid(vessel) || !isGuid(id)) throw new ApiError(400, 'invalid_context'); return 'operational-downtime/' + id + '/lifecycle?vessel=' + vessel }
export function freezeSofFinalize(view: SofFinalization, authority: string, acknowledged: string[]): OpsLifecycleAttempt {
  if (!view.canFinalize || !view.authorities.some(a => a.id === authority) || [...acknowledged].sort().join() !== [...view.unresolvedReferences].sort().join()) throw new ApiError(400, 'invalid_lifecycle_input')
  return { kind: 'Sof', vessel: view.vesselId, id: view.recordId, key: crypto.randomUUID(), path: sofPath(view.vesselId, view.recordId, '/finalize'),
    body: JSON.stringify({ expectedVersion: view.version, authorityId: authority, reasonCode: sofReason, acknowledgedUnresolved: [...acknowledged].sort() }) }
}
export function freezeDowntimeLifecycle(view: DowntimeLifecycleView, action: string, authority: string | null, evidence: DowntimeEvidencePin | null): OpsLifecycleAttempt {
  const review = reviewActions.includes(action), pin = action === 'LINK_CLAIM_EVIDENCE' || action === 'UNLINK_CLAIM_EVIDENCE'
  if (!view.allowedActions.includes(action) || review !== !!authority || review && !view.authorities.some(a => a.id === authority) || pin !== !!evidence || evidence && !validEvidencePin(evidence)) throw new ApiError(400, 'invalid_lifecycle_input')
  return { kind: 'Downtime', vessel: view.vesselId, id: view.recordId, key: crypto.randomUUID(), path: downtimePath(view.vesselId, view.recordId),
    body: JSON.stringify({ expectedVersion: view.version, action, reasonCode: downtimeLifecycleReason, authorityId: authority, evidence }) }
}
export const opsLifecycleApi = {
  sof: async (vessel: string, id: string, signal: AbortSignal) => decodeSofFinalization(await request<unknown>(sofPath(vessel, id, '/finalization'), signal), vessel, id),
  downtime: async (vessel: string, id: string, signal: AbortSignal) => decodeDowntimeLifecycle(await request<unknown>(downtimePath(vessel, id), signal), vessel, id),
  send: async (attempt: OpsLifecycleAttempt, csrf: string, signal: AbortSignal): Promise<ShipOpsRecord | DowntimeRecord> => {
    const value = await request<unknown>(attempt.path, signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body })
    const receipt = attempt.kind === 'Sof' ? decodeShipOpsRecord(value, attempt.vessel, 'Port', attempt.id) : decodeDowntimeRecord(value, attempt.vessel, attempt.id)
    if (receipt.currentRevision || receipt.canEdit) fail()
    return receipt
  },
}
