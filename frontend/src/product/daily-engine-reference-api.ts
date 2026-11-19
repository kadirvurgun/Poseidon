import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// DEV-099 / T-OPS2-003: engine daily log links to exact owning WorkOrder/Defect revisions.
// Links are lineage only (REVIEW_REQUIRED); this module never shows a linked record as verified.
export const engineReferenceSource = '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:100;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2177-2178:v46.3'
export const owningSources = {
  WorkOrder: '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:55;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:1426-1463:v46.3',
  Defect: '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:56;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:443-465:v46.3',
} as const
export type EngineReferenceKind = keyof typeof owningSources
export const engineReferenceKinds: readonly EngineReferenceKind[] = ['WorkOrder', 'Defect']
export type EngineReferencePin = { kind: EngineReferenceKind; id: string; revision: number; version: number; hash: string; sourceVersion: string }
export type EngineReferenceItem = { kind: EngineReferenceKind; pin: EngineReferencePin | null; sequence: number; state: 'LINKED_CURRENT' | 'LINKED_SUPERSEDED' | 'UNKNOWN'; qualification: 'REVIEW_REQUIRED'; label: string | null; href: string | null; reason: 'REFERENCE_CHANGED_SINCE_LINK' | 'REFERENCE_NOT_READABLE' | 'REFERENCE_UNAVAILABLE' | null }
export type EngineReferences = { recordId: string; vesselId: string; recordRevision: number; recordVersion: number; referenceVersion: number; sequence: number; canLink: boolean; sources: { kind: EngineReferenceKind; state: 'AVAILABLE' | 'UNAVAILABLE' }[]; items: EngineReferenceItem[]; offline: 'ONLINE_ONLY'; sourceVersion: string }
export type EngineReferenceCandidate = { pin: EngineReferencePin; label: string | null; detail: string | null; provenance: string; savedAt: string; qualification: 'REVIEW_REQUIRED'; href: string }
export type EngineReferenceCandidatePage = { vesselId: string; kind: EngineReferenceKind; items: EngineReferenceCandidate[]; offset: number; limit: 25; hasMore: boolean }
export type EngineReferenceEvent = { id: string; recordId: string; vesselId: string; sequence: number; referenceVersion: number; recordRevision: number; operation: 'LINK' | 'UNLINK'; target: EngineReferencePin; actorId: string; savedAt: string; qualification: 'REVIEW_REQUIRED'; sourceVersion: string }
export type EngineReferenceAttempt = { key: string; recordId: string; vessel: string; body: string; operation: 'LINK' | 'UNLINK'; target: EngineReferencePin }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const integer = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const label = (v: unknown) => v === null || typeof v === 'string' && v.length > 0 && v.length <= 200 && !/\p{Cc}/u.test(v)
function fail(): never { throw new ApiError(502, 'invalid_engine_reference_contract') }
const href = (vessel: string, pin: EngineReferencePin) => pin.kind === 'WorkOrder' ? `/app/work-order-center/${vessel}?id=${pin.id}` : `/app/defects/${vessel}?id=${pin.id}`

export function decodePin(value: unknown, kind?: EngineReferenceKind): EngineReferencePin {
  if (!exact(value, ['kind', 'id', 'revision', 'version', 'hash', 'sourceVersion']) || !engineReferenceKinds.includes(value.kind as EngineReferenceKind) ||
    kind !== undefined && value.kind !== kind || !isGuid(value.id) || !integer(value.revision) || !integer(value.version, 1, 4294967295) ||
    typeof value.hash !== 'string' || !/^[0-9A-Fa-f]{64}$/.test(value.hash) || value.sourceVersion !== owningSources[value.kind as EngineReferenceKind]) fail()
  return value as EngineReferencePin
}
export function decodeEngineReferences(value: unknown, vessel: string, recordId: string): EngineReferences {
  if (!isGuid(vessel) || !isGuid(recordId) || !exact(value, ['recordId', 'vesselId', 'recordRevision', 'recordVersion', 'referenceVersion', 'sequence', 'canLink', 'sources', 'items', 'offline', 'sourceVersion']) ||
    value.recordId !== recordId || value.vesselId !== vessel || !integer(value.recordRevision) || !integer(value.recordVersion, 1, 4294967295) ||
    !integer(value.referenceVersion, 0, 4294967295) || !integer(value.sequence, 0) || typeof value.canLink !== 'boolean' || value.offline !== 'ONLINE_ONLY' ||
    value.sourceVersion !== engineReferenceSource || !Array.isArray(value.sources) || value.sources.length !== 2 ||
    !engineReferenceKinds.every(kind => (value.sources as unknown[]).filter(s => exact(s, ['kind', 'state']) && s.kind === kind && (s.state === 'AVAILABLE' || s.state === 'UNAVAILABLE')).length === 1) ||
    (value.referenceVersion === 0) !== (value.sequence === 0) || !Array.isArray(value.items) || value.items.length > 64) fail()
  const seen = new Set<string>()
  for (const item of value.items) {
    if (!exact(item, ['kind', 'pin', 'sequence', 'state', 'qualification', 'label', 'href', 'reason']) || !engineReferenceKinds.includes(item.kind as EngineReferenceKind) ||
      !integer(item.sequence) || item.sequence > Number(value.sequence) || item.qualification !== 'REVIEW_REQUIRED' || !label(item.label)) fail()
    if (item.state === 'UNKNOWN') {
      // Unreadable/unavailable links never disclose an identifier, hash or label.
      if (item.pin !== null || item.label !== null || item.href !== null || !['REFERENCE_NOT_READABLE', 'REFERENCE_UNAVAILABLE'].includes(String(item.reason))) fail()
      continue
    }
    const pin = decodePin(item.pin, item.kind as EngineReferenceKind)
    if (!['LINKED_CURRENT', 'LINKED_SUPERSEDED'].includes(String(item.state)) || item.href !== href(vessel, pin) ||
      (item.state === 'LINKED_CURRENT' ? item.reason !== null : item.reason !== 'REFERENCE_CHANGED_SINCE_LINK') || seen.has(`${pin.kind}/${pin.id}`)) fail()
    seen.add(`${pin.kind}/${pin.id}`)
  }
  return value as EngineReferences
}
export function decodeCandidatePage(value: unknown, vessel: string, kind: EngineReferenceKind, offset: number): EngineReferenceCandidatePage {
  if (!exact(value, ['vesselId', 'kind', 'items', 'offset', 'limit', 'hasMore']) || value.vesselId !== vessel || value.kind !== kind || value.offset !== offset ||
    value.limit !== 25 || typeof value.hasMore !== 'boolean' || !Array.isArray(value.items) || value.items.length > 25) fail()
  const ids = new Set<string>()
  for (const row of value.items) {
    if (!exact(row, ['pin', 'label', 'detail', 'provenance', 'savedAt', 'qualification', 'href']) || !label(row.label) || !label(row.detail) ||
      !['UserDeclaredUnverified', 'SyntheticUnverified'].includes(String(row.provenance)) || !timestamp(row.savedAt) || row.qualification !== 'REVIEW_REQUIRED') fail()
    const pin = decodePin(row.pin, kind)
    if (row.href !== href(vessel, pin) || ids.has(pin.id)) fail()
    ids.add(pin.id)
  }
  return value as EngineReferenceCandidatePage
}
export function decodeEngineReferenceEvent(value: unknown, attempt: EngineReferenceAttempt): EngineReferenceEvent {
  if (!exact(value, ['id', 'recordId', 'vesselId', 'sequence', 'referenceVersion', 'recordRevision', 'operation', 'target', 'actorId', 'savedAt', 'qualification', 'sourceVersion']) ||
    !isGuid(value.id) || value.recordId !== attempt.recordId || value.vesselId !== attempt.vessel || !integer(value.sequence) || !integer(value.referenceVersion, 1, 4294967295) ||
    !integer(value.recordRevision) || value.operation !== attempt.operation || !isGuid(value.actorId) || !timestamp(value.savedAt) ||
    value.qualification !== 'REVIEW_REQUIRED' || value.sourceVersion !== engineReferenceSource) fail()
  const pin = decodePin(value.target)
  if (JSON.stringify(pin) !== JSON.stringify(attempt.target)) fail()
  return value as EngineReferenceEvent
}
// Freeze one exact command; retries after an unknown outcome reuse the same key and body.
export function engineReferenceAttempt(references: EngineReferences, operation: 'LINK' | 'UNLINK', target: EngineReferencePin): EngineReferenceAttempt {
  const pin = decodePin(target)
  const body = JSON.stringify({ expectedRecordVersion: references.recordVersion, recordRevision: references.recordRevision, expectedReferenceVersion: references.referenceVersion, operation,
    target: { kind: pin.kind, id: pin.id, revision: pin.revision, version: pin.version, hash: pin.hash, sourceVersion: pin.sourceVersion } })
  return { key: crypto.randomUUID(), recordId: references.recordId, vessel: references.vesselId, body, operation, target: pin }
}
const guard = (vessel: string, id?: string) => { if (!isGuid(vessel) || id !== undefined && !isGuid(id)) throw new ApiError(400, 'invalid_context') }
export const engineReferenceApi = {
  read: async (vessel: string, recordId: string, signal: AbortSignal) => { guard(vessel, recordId); return decodeEngineReferences(await request<unknown>(`engine-daily-logs/${recordId}/references?vesselId=${vessel}`, signal), vessel, recordId) },
  candidates: async (vessel: string, kind: EngineReferenceKind, offset: number, signal: AbortSignal) => {
    guard(vessel); if (!engineReferenceKinds.includes(kind) || !integer(offset, 0, 100000)) throw new ApiError(400, 'invalid_context')
    return decodeCandidatePage(await request<unknown>(`engine-daily-logs/reference-candidates?vesselId=${vessel}&kind=${kind}&offset=${offset}`, signal), vessel, kind, offset)
  },
  record: async (attempt: EngineReferenceAttempt, csrf: string, signal: AbortSignal) => { guard(attempt.vessel, attempt.recordId); return decodeEngineReferenceEvent(await request<unknown>(`engine-daily-logs/${attempt.recordId}/references?vesselId=${attempt.vessel}`, signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body }), attempt) },
}
