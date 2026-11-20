import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import type { Command } from './tug-readiness-api'

// S10 WP-A client contracts: DEV-064 audit packs (/api/v1/audit-ready/packs), DEV-065 data trust (/api/v1/data-trust)
// and DEV-113 industry assurance (/api/v1/industry-assurance). Closed server codes only: an unknown state is a contract
// failure and is never coerced to a green/ready/verified value in the browser.
export const packKinds = ['Source', 'Requirement', 'Control', 'Evidence', 'Verification', 'Decision', 'History'] as const
export const packStates = ['Current', 'Superseded', 'Unavailable', 'Recorded'] as const
export const integrityStates = ['Match', 'Mismatch', 'SourceUnavailable'] as const
export const signalKinds = ['MISSING', 'STALE', 'CONFLICTING', 'UNVERIFIED', 'ORPHAN'] as const
export const dispositions = ['REVIEW_REQUIRED', 'NO_MATERIAL_SIGNAL'] as const
export const subjectKinds = ['Evidence', 'Control', 'ProfileField'] as const
export const applicabilities = ['APPLICABLE', 'NOT_APPLICABLE', 'REVIEW_REQUIRED'] as const
export const changes = ['NEW', 'CHANGED', 'UNCHANGED', 'ABSENT'] as const
export const mediaKinds = ['PHOTO', 'VIDEO'] as const

export type PackItem = { ordinal: number; kind: typeof packKinds[number]; resourceId: string; revision: number; state: typeof packStates[number]; hash: string | null
  evidenceId: string; controlId: string; savedAt: string | null; integrity: typeof integrityStates[number]; fileHref: string | null; historyHref: string | null }
export type Pack = { id: string; vesselId: string; label: string; notice: string; sourceVersion: string; asOf: string; createdAt: string; createdByMe: boolean; manifestHash: string
  itemCount: number; included: number; excluded: number; unavailableChains: number; truncated: boolean; previousPackId: string | null; superseded: number; items: PackItem[] }
export type PackSummary = { id: string; asOf: string; createdAt: string; createdByMe: boolean; itemCount: number; manifestHash: string; previousPackId: string | null }
export type PackList = { vesselId: string; label: string; notice: string; canCompile: boolean; items: PackSummary[]; hasMore: boolean }

export type Signal = { kind: typeof signalKinds[number]; reason: string; material: boolean }
export type TrustItem = { subjectKind: typeof subjectKinds[number]; subjectId: string | null; fieldPath: string | null; revision: number; title: string | null; provenance: string | null
  sourceKind: string | null; observedAt: string | null; freshUntil: string | null; freshness: string; verification: string; disposition: typeof dispositions[number]; signals: Signal[]; savedAt: string | null }
export type TrustCounts = { missing: number; stale: number; conflicting: number; unverified: number; orphan: number }
export type TrustPage = { vesselId: string; asOf: string; state: 'AVAILABLE' | 'UNAVAILABLE'; disposition: typeof dispositions[number]; readinessEffect: string; counts: TrustCounts
  items: TrustItem[]; profileFields: TrustItem[]; offset: number; limit: number; total: number; hasMore: boolean; withheld: number; truncated: boolean; sourceVersion: string }

export type Binding = { header: string; field: string }
export type Mapping = { id: string; key: string; version: number; sourceVersion: string; bindings: Binding[]; hash: string; createdAt: string }
export type ItemContent = { questionRef: string; chapter: string | null; observationType: string; observationCategory: string | null; observationText: string | null; inspectorComment: string | null }
export type Item = { questionRef: string; revision: number; content: ItemContent; hash: string; evidence: number; savedAt: string }
export type Finding = { id: string; questionRef: string; firstItemRevision: number; latestItemRevision: number; observationPresent: boolean; state: 'OPEN'; verification: 'UNVERIFIED'
  actionLinkState: 'UNLINKED' | 'LINKED'; actionId: string | null; revision: number; version: number }
export type Evidence = { id: string; questionRef: string; fileId: string; fileRevision: number; mediaKind: string; capturedAt: string; origin: string; verification: 'UNVERIFIED'; fileHref: string }
export type DiffEntry = { questionRef: string; change: typeof changes[number]; fields: string[]; currentRevision: number | null }
export type Import = { id: string; state: 'PENDING_REVIEW' | 'ACCEPTED' | 'REJECTED'; mappingVersion: number; sourceVersion: string; diff: DiffEntry[]; diffHash: string; importedByMe: boolean
  importedAt: string; decidedAt: string | null; version: number; new: number; changed: number; unchanged: number; absent: number }
export type Inspection = { id: string; vesselId: string; programme: string; referenceNo: string; inspectionDate: string; state: string; classification: string; applicability: string
  revision: number; provenance: string; sourceVersion: string; items: Item[]; findings: Finding[]; evidence: Evidence[]; imports: Import[]; notice: string }
export type InspectionSummary = { id: string; referenceNo: string; inspectionDate: string; state: string; applicability: string; items: number; openFindings: number; pendingImport: boolean }
export type AssurancePage = { vesselId: string; classification: string; applicability: typeof applicabilities[number]; sourceVersion: string; inspections: InspectionSummary[]
  mappings: Mapping[]; capabilities: { canImport: boolean; canAccept: boolean; canCaptureEvidence: boolean; canLinkAction: boolean; canWriteMapping: boolean; mediaKinds: string[]
    availableMediaKinds: string[]; fields: string[]; requiredFields: string[] }; notice: string }

type Obj = Record<string, unknown>
const fail = (): never => { throw new ApiError(502, 'invalid_assurance_contract') }
const obj = (v: unknown): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v)
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const time = (v: unknown) => typeof v === 'string' && timestamp(v)
const optTime = (v: unknown) => v === null || time(v)
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && (values as readonly string[]).includes(v)
const hash = (v: unknown) => typeof v === 'string' && /^[A-Fa-f0-9]{64}$/.test(v)
const sameVessel = (v: unknown, vessel: string) => typeof v === 'string' && v.toLowerCase() === vessel.toLowerCase()

export function decodePackItem(v: unknown): PackItem {
  if (!obj(v) || !int(v.ordinal) || !oneOf(v.kind, packKinds) || !isGuid(v.resourceId) || !int(v.revision) || !oneOf(v.state, packStates) ||
    !(v.hash === null || hash(v.hash)) || !isGuid(v.evidenceId) || !isGuid(v.controlId) || !optTime(v.savedAt) || !oneOf(v.integrity, integrityStates) ||
    !(v.fileHref === null || typeof v.fileHref === 'string' && v.fileHref.startsWith('/api/v1/vessels/')) ||
    !(v.historyHref === null || typeof v.historyHref === 'string' && v.historyHref.startsWith('/api/v1/audit?')) ||
    (v.state === 'Unavailable') !== (v.hash === null)) return fail()
  return v as unknown as PackItem
}
export function decodePack(v: unknown, vessel: string): Pack {
  if (!obj(v) || !isGuid(v.id) || !sameVessel(v.vesselId, vessel) || v.label !== 'SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD' || typeof v.notice !== 'string' ||
    !time(v.asOf) || !time(v.createdAt) || typeof v.createdByMe !== 'boolean' || !hash(v.manifestHash) || !int(v.itemCount) || !int(v.included) || !int(v.excluded) ||
    !int(v.unavailableChains) || typeof v.truncated !== 'boolean' || !(v.previousPackId === null || isGuid(v.previousPackId)) || !int(v.superseded) ||
    !Array.isArray(v.items) || v.items.length > 2048 || v.included !== v.items.length || (v.included as number) + (v.excluded as number) !== v.itemCount) return fail()
  return { ...(v as unknown as Pack), items: v.items.map(decodePackItem) }
}
export function decodePackList(v: unknown, vessel: string): PackList {
  if (!obj(v) || !sameVessel(v.vesselId, vessel) || v.label !== 'SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD' || typeof v.canCompile !== 'boolean' || !Array.isArray(v.items) ||
    v.items.length > 50 || typeof v.hasMore !== 'boolean' || v.items.some(i => !obj(i) || !isGuid(i.id) || !time(i.asOf) || !int(i.itemCount) || !hash(i.manifestHash))) return fail()
  return v as unknown as PackList
}

function decodeTrustItem(v: unknown): TrustItem {
  if (!obj(v) || !oneOf(v.subjectKind, subjectKinds) || !(v.subjectId === null || isGuid(v.subjectId)) || !int(v.revision) || !oneOf(v.disposition, dispositions) ||
    !Array.isArray(v.signals) || v.signals.some(s => !obj(s) || !oneOf(s.kind, signalKinds) || typeof s.reason !== 'string' || typeof s.material !== 'boolean') ||
    // False green blocked: a material signal can never come with a non-review disposition.
    (v.signals as Signal[]).some(s => s.material) && v.disposition !== 'REVIEW_REQUIRED' || !optTime(v.observedAt) || !optTime(v.savedAt)) return fail()
  return v as unknown as TrustItem
}
export function decodeTrust(v: unknown, vessel: string): TrustPage {
  if (!obj(v) || !sameVessel(v.vesselId, vessel) || !time(v.asOf) || !oneOf(v.state, ['AVAILABLE', 'UNAVAILABLE'] as const) || !oneOf(v.disposition, dispositions) ||
    !obj(v.counts) || !Array.isArray(v.items) || v.items.length > 100 || !Array.isArray(v.profileFields) || !int(v.offset) || !int(v.limit, 1) || !int(v.total) ||
    typeof v.hasMore !== 'boolean' || !int(v.withheld) || typeof v.truncated !== 'boolean') return fail()
  const items = v.items.map(decodeTrustItem), profileFields = v.profileFields.map(decodeTrustItem)
  if ([...items, ...profileFields].some(i => i.disposition === 'REVIEW_REQUIRED') && v.disposition !== 'REVIEW_REQUIRED') return fail()
  return { ...(v as unknown as TrustPage), items, profileFields }
}

export function decodeInspection(v: unknown, vessel: string): Inspection {
  if (!obj(v) || !isGuid(v.id) || !sameVessel(v.vesselId, vessel) || v.programme !== 'SIRE_2_0' || !Array.isArray(v.items) || !Array.isArray(v.findings) ||
    !Array.isArray(v.evidence) || !Array.isArray(v.imports) || !int(v.revision, 1) ||
    v.findings.some(f => !obj(f) || f.state !== 'OPEN' || f.verification !== 'UNVERIFIED' || !oneOf(f.actionLinkState, ['UNLINKED', 'LINKED'] as const)) ||
    v.evidence.some(e => !obj(e) || e.verification !== 'UNVERIFIED' || !oneOf(e.mediaKind, mediaKinds)) ||
    v.imports.some(i => !obj(i) || !oneOf(i.state, ['PENDING_REVIEW', 'ACCEPTED', 'REJECTED'] as const) || !hash(i.diffHash) || !Array.isArray(i.diff) ||
      (i.diff as unknown[]).some(d => !obj(d) || !oneOf(d.change, changes)))) return fail()
  return v as unknown as Inspection
}
export function decodeAssurancePage(v: unknown, vessel: string): AssurancePage {
  if (!obj(v) || !sameVessel(v.vesselId, vessel) || !oneOf(v.applicability, applicabilities) || !Array.isArray(v.inspections) || !Array.isArray(v.mappings) || !obj(v.capabilities)) return fail()
  return v as unknown as AssurancePage
}

const q = (vessel: string) => `vesselId=${encodeURIComponent(vessel)}`
const cmd = (path: string, body: Record<string, unknown>, key: string = crypto.randomUUID()): Command => ({ key, method: 'POST', path, body })
export const assuranceApi = {
  packs: async (vessel: string, signal: AbortSignal) => decodePackList(await request<unknown>(`audit-ready/packs?${q(vessel)}`, signal), vessel),
  pack: async (vessel: string, id: string, signal: AbortSignal) => { if (!isGuid(id)) fail(); return decodePack(await request<unknown>(`audit-ready/packs/${id}?${q(vessel)}`, signal), vessel) },
  exportHref: (vessel: string, id: string) => `/api/v1/audit-ready/packs/${id}/export?${q(vessel)}`,
  compile: (vessel: string, previousPackId: string | null) => cmd(`audit-ready/packs?${q(vessel)}`, { id: crypto.randomUUID(), previousPackId }),
  trust: async (vessel: string, offset: number, signal: AbortSignal) => decodeTrust(await request<unknown>(`data-trust?${q(vessel)}&offset=${offset}&limit=50`, signal), vessel),
  page: async (vessel: string, signal: AbortSignal) => decodeAssurancePage(await request<unknown>(`industry-assurance?${q(vessel)}`, signal), vessel),
  inspection: async (vessel: string, id: string, signal: AbortSignal) => { if (!isGuid(id)) fail(); return decodeInspection(await request<unknown>(`industry-assurance/inspections/${id}?${q(vessel)}`, signal), vessel) },
  mapping: (key: string, bindings: Binding[]) => cmd('industry-assurance/mappings', { id: crypto.randomUUID(), key, bindings }),
  stage: (vessel: string, body: { inspectionId: string; mappingId: string; referenceNo: string | null; inspectionDate: string | null; workbook: string }) =>
    cmd(`industry-assurance/imports?${q(vessel)}`, { id: crypto.randomUUID(), ...body }),
  decide: (vessel: string, i: Import, decision: 'ACCEPT' | 'REJECT', accept: string[], reject: string[], reason: string) =>
    cmd(`industry-assurance/imports/${i.id}/decision?${q(vessel)}`, { expectedVersion: i.version, diffHash: i.diffHash, decision, acceptChanged: accept, rejectChanged: reject, reason }),
  // The key is the offline command id: a capture queued offline is replayed with the same key and body.
  evidence: (vessel: string, inspection: string, body: { questionRef: string; file: { id: string; revision: number; checksum: string }; mediaKind: string; capturedAt: string; origin: string }) =>
    cmd(`industry-assurance/inspections/${inspection}/evidence?${q(vessel)}`, { id: crypto.randomUUID(), ...body }),
  link: (vessel: string, f: Finding, actionId: string) => cmd(`industry-assurance/findings/${f.id}/action-link?${q(vessel)}`, { expectedVersion: f.version, actionId }),
}

// Exact retained command: an uncertain outcome is retried with the SAME key and body.
export async function sendCommand(command: Command, csrf: string, signal: AbortSignal): Promise<unknown> {
  const response = await fetch(`/api/v1/${command.path}`, { credentials: 'same-origin', signal, method: command.method,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: JSON.stringify(command.body) })
  let body: unknown = null
  try { body = await response.json() } catch { /* Proxy errors need not be JSON. */ }
  if (!response.ok) {
    const f = obj(body) ? body : {}
    throw Object.assign(new ApiError(response.status, typeof f.code === 'string' ? f.code : 'request_failed'), { missing: Array.isArray(f.missing) ? f.missing.filter(x => typeof x === 'string') : [] })
  }
  return body
}

// Durable per-vessel queue of offline evidence captures (DEV-113). Each entry keeps its exact command (key + body), so a
// replay after reconnect is idempotent at the server receipt. Storage failures degrade to "not queued", never to "sent".
const queueKey = (vessel: string) => `poseidon.assurance.evidence-queue.v1:${vessel.toLowerCase()}`
export function readQueue(vessel: string): Command[] {
  try {
    const raw = localStorage.getItem(queueKey(vessel)); if (!raw) return []
    const parsed: unknown = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed.filter((c): c is Command => obj(c) && typeof c.key === 'string' && isGuid(c.key) && typeof c.path === 'string' &&
      c.path.startsWith('industry-assurance/inspections/') && c.method === 'POST' && obj(c.body)).slice(0, 100) : []
  } catch { return [] }
}
export function writeQueue(vessel: string, commands: Command[]): boolean {
  try { localStorage.setItem(queueKey(vessel), JSON.stringify(commands.slice(0, 100))); return true } catch { return false }
}

export function base64(bytes: ArrayBuffer): string {
  let s = ''; const view = new Uint8Array(bytes)
  for (let i = 0; i < view.length; i += 0x8000) s += String.fromCharCode(...view.subarray(i, i + 0x8000))
  return btoa(s)
}
