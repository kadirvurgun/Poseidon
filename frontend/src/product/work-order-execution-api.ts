import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// DEV-054 / T-S8-003 strict client contract for /api/v1/work-orders/{id}/execution. Decoders are closed:
// an unknown state, a gate that claims verification outside VERIFIED/CLOSED, or extra members are
// contract breaches, never rendered. Verification/closure are human records decided by the server.
export const executionStates = ['RELEASED', 'IN_PROGRESS', 'EVIDENCE_SUBMITTED', 'VERIFIED', 'CLOSED'] as const
export type ExecutionState = typeof executionStates[number]
export const evidenceKinds = ['READING', 'SPARES_RECORD', 'PHOTO', 'DOCUMENT', 'WORK_DONE'] as const
export type EvidenceKind = typeof evidenceKinds[number]
export const gateStates = ['REVIEW_REQUIRED', 'MISSING', 'PRESENT_PENDING_HUMAN_VERIFICATION', 'HUMAN_VERIFIED'] as const
export type GateState = typeof gateStates[number]
export const returnReasons = ['EVIDENCE_INSUFFICIENT', 'READING_NOT_ACCEPTED', 'WORK_INCOMPLETE', 'OTHER_SEE_FINDINGS'] as const
export type ReturnReason = typeof returnReasons[number]
export const executionActions = ['RELEASE', 'REQUIREMENTS', 'START', 'SUBMIT', 'VERIFY', 'RETURN', 'CLOSE'] as const
export type ExecutionAction = typeof executionActions[number]
export const transitions = ['RELEASE', 'REQUIREMENTS', 'START', 'EVIDENCE', 'SUBMIT', 'VERIFY', 'RETURN', 'CLOSE'] as const
export type FilePin = { id: string; revision: number; checksum: string }
export type Reading = { code: string; value: string; unit: string | null; takenAt: string | null; note: string | null }
export type SpareUse = { spareId: string | null; spareRevision: number | null; quantity: string; description: string | null }
export type EvidenceFile = { kind: 'PHOTO' | 'DOCUMENT'; file: FilePin; caption: string | null }
export type Evidence = { readings: Reading[]; spares: SpareUse[]; files: EvidenceFile[]; workDone: string | null; findings: string | null }
export type Gate = { state: GateState; required: EvidenceKind[]; present: EvidenceKind[]; missing: EvidenceKind[] }
export type Execution = { workOrderId: string; vesselId: string; revision: number; version: number; workOrderRevision: number; workOrderVersion: number; state: ExecutionState
  requiredEvidence: EvidenceKind[] | null; evidence: Evidence; evidenceHash: string; gate: Gate; blockers: string[]; transition: typeof transitions[number]
  releasedAt: string; startedAt: string | null; submittedAt: string | null; verifiedAt: string | null; closedAt: string | null; returnReason: ReturnReason | null; returnedAt: string | null
  releasedByCurrentActor: boolean; executedByCurrentActor: boolean; sourceVersion: string; hash: string; provenance: string; savedAt: string; currentRevision: boolean; qualification: 'HUMAN_RECORDED_NOT_RETURN_TO_SERVICE' }
export type ExecutionCapabilities = { canRelease: boolean; canExecute: boolean; canVerify: boolean; canClose: boolean; evidenceKinds: EvidenceKind[]; fileKinds: ('PHOTO' | 'DOCUMENT')[]; returnReasons: ReturnReason[]
  offline: { supported: boolean; state: 'UNAVAILABLE_POLICY' | 'CONFIGURED_UNVERIFIED' | 'CONFIGURED_REQUIRES_IN_PROGRESS' }; sourceVersion: string; qualification: 'HUMAN_RECORDED_NOT_RETURN_TO_SERVICE' }
export type ExecutionView = { workOrderId: string; vesselId: string; workOrderRevision: number; workOrderVersion: number; capabilities: ExecutionCapabilities; current: Execution | null }
export type ExecutionHistory = { items: Execution[]; offset: number; limit: 25; hasMore: boolean }
export type ExecutionCommand = { vessel: string; workOrderId: string; key: string; kind: 'transition' | 'evidence'; body: string }
export const emptyEvidence = (): Evidence => ({ readings: [], spares: [], files: [], workDone: null, findings: null })

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const int = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const one = <T extends string>(v: unknown, set: readonly T[]): v is T => typeof v === 'string' && (set as readonly string[]).includes(v)
const list = <T extends string>(v: unknown, set: readonly T[]): v is T[] => Array.isArray(v) && v.length <= set.length && v.every(x => one(x, set)) && new Set(v).size === v.length
const text = (v: unknown, max: number, lines = false) => v === null || typeof v === 'string' && v.trim().length > 0 && v.length <= max && !/\p{Cc}/u.test(lines ? v.replace(/[\r\n\t]/g, '') : v)
const when = (v: unknown) => v === null || timestamp(v)
const hash = (v: unknown, upper = false) => typeof v === 'string' && (upper ? /^[A-F0-9]{64}$/ : /^[A-Fa-f0-9]{64}$/).test(v)
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 64 && v.every(c => typeof c === 'string' && /^[A-Z0-9_]{1,96}$/.test(c))
export const lexicalNumber = (v: string) => v.length <= 48 && /^-?[0-9]+(?:\.[0-9]+)?$/.test(v)
export const positiveQuantity = (v: string) => /^(?:0|[1-9][0-9]{0,27})(?:\.[0-9]*[1-9])?$/.test(v) && !/^0(?:\.0*)?$/.test(v)
function fail(): never { throw new ApiError(502, 'invalid_work_order_execution_contract') }

export function validEvidence(v: unknown): v is Evidence {
  if (!exact(v, ['readings', 'spares', 'files', 'workDone', 'findings']) || !Array.isArray(v.readings) || v.readings.length > 50 || !Array.isArray(v.spares) || v.spares.length > 50 ||
    !Array.isArray(v.files) || v.files.length > 20 || !text(v.workDone, 4000, true) || !text(v.findings, 4000, true)) return false
  const readings = v.readings.every(r => exact(r, ['code', 'value', 'unit', 'takenAt', 'note']) && typeof r.code === 'string' && text(r.code, 64) && typeof r.value === 'string' && lexicalNumber(r.value) &&
    text(r.unit, 32) && when(r.takenAt) && text(r.note, 500, true)) && new Set(v.readings.map(r => (r as Reading).code)).size === v.readings.length
  const spares = v.spares.every(s => exact(s, ['spareId', 'spareRevision', 'quantity', 'description']) && typeof s.quantity === 'string' && positiveQuantity(s.quantity) && text(s.description, 200) &&
    (s.spareId === null ? s.spareRevision === null && s.description !== null : isGuid(s.spareId) && int(s.spareRevision)))
  const files = v.files.every(f => exact(f, ['kind', 'file', 'caption']) && (f.kind === 'PHOTO' || f.kind === 'DOCUMENT') && exact(f.file, ['id', 'revision', 'checksum']) &&
    isGuid(f.file.id) && int(f.file.revision) && hash(f.file.checksum) && text(f.caption, 200)) && new Set(v.files.map(f => (f as EvidenceFile).file.id)).size === v.files.length
  return readings && spares && files
}
function decodeGate(v: unknown, state: ExecutionState): Gate {
  if (!exact(v, ['state', 'required', 'present', 'missing']) || !one(v.state, gateStates) || !list(v.required, evidenceKinds) || !list(v.present, evidenceKinds) || !list(v.missing, evidenceKinds)) fail()
  // HUMAN_VERIFIED is only possible after an attributed verification; REVIEW_REQUIRED never lists requirements.
  if (v.state === 'HUMAN_VERIFIED' && state !== 'VERIFIED' && state !== 'CLOSED' || v.state === 'REVIEW_REQUIRED' && v.required.length > 0 || (v.state === 'MISSING') !== (v.missing.length > 0)) fail()
  return v as Gate
}
export function decodeExecution(v: unknown, vessel: string, workOrder: string): Execution {
  if (!exact(v, ['workOrderId', 'vesselId', 'revision', 'version', 'workOrderRevision', 'workOrderVersion', 'state', 'requiredEvidence', 'evidence', 'evidenceHash', 'gate', 'blockers', 'transition',
    'releasedAt', 'startedAt', 'submittedAt', 'verifiedAt', 'closedAt', 'returnReason', 'returnedAt', 'releasedByCurrentActor', 'executedByCurrentActor', 'sourceVersion', 'hash', 'provenance', 'savedAt',
    'currentRevision', 'qualification']) || v.workOrderId !== workOrder || v.vesselId !== vessel || !int(v.revision) || !int(v.version, 1, 4294967295) || !int(v.workOrderRevision) ||
    !int(v.workOrderVersion, 1, 4294967295) || !one(v.state, executionStates) || !(v.requiredEvidence === null || list(v.requiredEvidence, evidenceKinds) && v.requiredEvidence.length > 0) ||
    !validEvidence(v.evidence) || !hash(v.evidenceHash, true) || !codes(v.blockers) || !one(v.transition, transitions) || !timestamp(v.releasedAt) ||
    ![v.startedAt, v.submittedAt, v.verifiedAt, v.closedAt, v.returnedAt].every(when) || !(v.returnReason === null || one(v.returnReason, returnReasons)) ||
    typeof v.releasedByCurrentActor !== 'boolean' || typeof v.executedByCurrentActor !== 'boolean' || typeof v.sourceVersion !== 'string' || !hash(v.hash) ||
    typeof v.provenance !== 'string' || !timestamp(v.savedAt) || typeof v.currentRevision !== 'boolean' || v.qualification !== 'HUMAN_RECORDED_NOT_RETURN_TO_SERVICE') fail()
  const gate = decodeGate(v.gate, v.state)
  if ((v.state === 'VERIFIED' || v.state === 'CLOSED') && v.verifiedAt === null || v.state === 'CLOSED' && v.closedAt === null) fail()
  return { ...(v as Execution), gate }
}
export function decodeExecutionView(v: unknown, vessel: string, workOrder: string): ExecutionView {
  if (!exact(v, ['workOrderId', 'vesselId', 'workOrderRevision', 'workOrderVersion', 'capabilities', 'current']) || v.workOrderId !== workOrder || v.vesselId !== vessel ||
    !int(v.workOrderRevision) || !int(v.workOrderVersion, 1, 4294967295)) fail()
  const c = v.capabilities
  if (!exact(c, ['canRelease', 'canExecute', 'canVerify', 'canClose', 'evidenceKinds', 'fileKinds', 'returnReasons', 'offline', 'sourceVersion', 'qualification']) ||
    ![c.canRelease, c.canExecute, c.canVerify, c.canClose].every(b => typeof b === 'boolean') || !list(c.evidenceKinds, evidenceKinds) || !list(c.fileKinds, ['PHOTO', 'DOCUMENT'] as const) ||
    !list(c.returnReasons, returnReasons) || !exact(c.offline, ['supported', 'state']) || typeof c.offline.supported !== 'boolean' ||
    !one(c.offline.state, ['UNAVAILABLE_POLICY', 'CONFIGURED_UNVERIFIED', 'CONFIGURED_REQUIRES_IN_PROGRESS'] as const) || c.offline.supported !== (c.offline.state !== 'UNAVAILABLE_POLICY') ||
    typeof c.sourceVersion !== 'string' || c.qualification !== 'HUMAN_RECORDED_NOT_RETURN_TO_SERVICE') fail()
  return { ...(v as ExecutionView), current: v.current === null ? null : decodeExecution(v.current, vessel, workOrder) }
}
const root = (vessel: string, workOrder: string, suffix = '') => {
  if (!isGuid(vessel) || !isGuid(workOrder)) throw new ApiError(400, 'invalid_context')
  return `work-orders/${workOrder}/execution${suffix}?vessel=${vessel}`
}
export type TransitionInput = { action: ExecutionAction; requiredEvidence?: EvidenceKind[] | null; reasonCode?: ReturnReason | null }
// Freezes the exact body and idempotency key; an uncertain outcome is retried with the same key/body.
export function freezeTransition(vessel: string, view: ExecutionView, input: TransitionInput): ExecutionCommand {
  const current = view.current, release = input.action === 'RELEASE'
  if (release ? current !== null : current === null || !current.currentRevision) throw new ApiError(400, 'invalid_execution_input')
  const hashBound = ['SUBMIT', 'VERIFY', 'RETURN', 'CLOSE'].includes(input.action)
  const body = { workOrderId: view.workOrderId, action: input.action, expectedVersion: current?.version ?? 0, expectedRevision: current?.revision ?? 0,
    workOrderRevision: release ? view.workOrderRevision : null, workOrderVersion: release ? view.workOrderVersion : null,
    requiredEvidence: input.action === 'RELEASE' || input.action === 'REQUIREMENTS' ? input.requiredEvidence ?? null : null,
    expectedEvidenceHash: hashBound ? current!.evidenceHash : null, reasonCode: input.action === 'RETURN' ? input.reasonCode ?? null : null }
  if (input.action === 'REQUIREMENTS' && !body.requiredEvidence?.length) throw new ApiError(400, 'invalid_execution_input')
  if (input.action === 'RETURN' && !body.reasonCode) throw new ApiError(400, 'invalid_execution_input')
  return { vessel, workOrderId: view.workOrderId, key: crypto.randomUUID(), kind: 'transition', body: JSON.stringify(body) }
}
export function freezeEvidence(vessel: string, current: Execution, evidence: Evidence): ExecutionCommand {
  if (!current.currentRevision || current.state !== 'IN_PROGRESS' || !validEvidence(evidence)) throw new ApiError(400, 'invalid_execution_input')
  return { vessel, workOrderId: current.workOrderId, key: crypto.randomUUID(), kind: 'evidence',
    body: JSON.stringify({ workOrderId: current.workOrderId, expectedVersion: current.version, expectedRevision: current.revision, evidence }) }
}
export const workOrderExecutionApi = {
  view: async (vessel: string, workOrder: string, signal: AbortSignal) => decodeExecutionView(await request<unknown>(root(vessel, workOrder), signal), vessel, workOrder),
  history: async (vessel: string, workOrder: string, offset: number, signal: AbortSignal): Promise<ExecutionHistory> => {
    if (!int(offset, 0, 100000)) throw new ApiError(400, 'invalid_context')
    const v = await request<unknown>(root(vessel, workOrder, '/history') + '&offset=' + offset, signal)
    if (!exact(v, ['items', 'offset', 'limit', 'hasMore']) || !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset || v.limit !== 25 || typeof v.hasMore !== 'boolean') fail()
    const items = v.items.map(item => decodeExecution(item, vessel, workOrder))
    if (new Set(items.map(i => i.revision)).size !== items.length || items.filter(i => i.currentRevision).length > 1) fail()
    return { items, offset, limit: 25, hasMore: v.hasMore }
  },
  send: async (command: ExecutionCommand, csrf: string, signal: AbortSignal) => {
    const value = await request<unknown>(root(command.vessel, command.workOrderId, command.kind === 'transition' ? '/transitions' : '/evidence'), signal,
      { method: command.kind === 'transition' ? 'POST' : 'PUT', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: command.body })
    const receipt = decodeExecution(value, command.vessel, command.workOrderId)
    // A receipt is never the editable current record; a fresh owning GET establishes that.
    if (receipt.currentRevision) fail()
    return receipt
  },
}
